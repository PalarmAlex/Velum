using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ISIDA.Common;
using ISIDA.Psychic.Automatism;
using ISIDA.Reflexes;
using Velum.Configuration;
using Velum.Isida.Logs;
using Velum.UI.Logs;

namespace Velum.UI
{
  /// <summary>
  /// Сокращённый лог «стимул → ответ» в окне вывода панели: периодический фоновый опрос
  /// хвоста <c>AgentLogs.csv</c> (того же источника, что отчёт по кнопке «Логи»), разбор
  /// типа и имени стимула/мотора по стартовым справочникам ISIDA и вывод пар «стимул → ответ».
  /// Файловое чтение — в пуле потоков; резолв (обращение к синглтонам ISIDA) и запись в
  /// <c>_txtAgentOutput</c> — в UI-потоке SolidWorks (см. правила проекта про COM/ISIDA и UI).
  /// </summary>
  public sealed partial class VelumAgentTaskPane
  {
    /// <summary>Хвостовой читатель <c>AgentLogs.csv</c>; stateful, трогается только из пула потоков.</summary>
    private readonly VelumAgentLogTailReader _condensedTailReader = new VelumAgentLogTailReader();

    /// <summary>Резолвер строк лога в записи; stateful (карта колонок), трогается только из UI-потока.</summary>
    private readonly VelumAgentLogCondensedResolver _condensedResolver = new VelumAgentLogCondensedResolver();

    /// <summary>Сигнатура последней выведенной пары — для подавления повторов (рефлекс держится несколько пульсов).</summary>
    private string _condensedLastEmittedSignature = string.Empty;

    /// <summary>Счётчик пульсов с прошлого опроса (для настройки периодичности).</summary>
    private int _condensedPollCounter;

    /// <summary>Фон ещё читает хвост — не плодим параллельные задачи.</summary>
    private bool _condensedBgRunning;

    /// <summary>Нужно ли фону перемотать хвост в конец файла (сброс при старте/остановке).</summary>
    private bool _condensedNeedSeekToEnd = true;

    /// <summary>
    /// Вызывается из обработчика пульса (<see cref="OnAfterGomeostasis"/>): копит пульсы и по
    /// достижении настройки периодичности запускает фоновый опрос хвоста лога.
    /// </summary>
    /// <param name="pulseNumber">Номер текущего пульса.</param>
    private void PumpCondensedLog(int pulseNumber)
    {
      int period = VelumAppConfig.CondensedLogPollPeriodPulses;
      _condensedPollCounter++;
      if (_condensedPollCounter < period)
        return;
      _condensedPollCounter = 0;

      if (_condensedBgRunning || !IsHandleCreated || IsDisposed)
        return;

      _condensedBgRunning = true;
      bool seekToEnd = _condensedNeedSeekToEnd;
      if (seekToEnd)
        _condensedNeedSeekToEnd = false;

      string path = VelumLogPaths.ResolveLogFile(VelumLogPaths.AgentLogCsvFileName);
      Task.Run(() =>
      {
        List<string> rawLines = null;
        try
        {
          if (seekToEnd)
            _condensedTailReader.Reset(true, path);
          rawLines = new List<string>(_condensedTailReader.ReadNewLines(path));
        }
        catch
        {
          rawLines = null;
        }
        finally
        {
          try
          {
            if (!IsDisposed && IsHandleCreated)
              BeginInvoke(new Action<object>(OnCondensedLogLinesRead), rawLines);
            else
              _condensedBgRunning = false;
          }
          catch
          {
            _condensedBgRunning = false;
          }
        }
      });
    }

    /// <summary>
    /// UI-поток: разбирает прочитанные сырые строки в пары «стимул → ответ», подавляет дубли
    /// и вставляет новые блоки в начало окна вывода (самый свежий блок — верхний).
    /// </summary>
    /// <param name="state">Список сырых строк хвоста (или <c>null</c> при сбое чтения).</param>
    private void OnCondensedLogLinesRead(object state)
    {
      try
      {
        if (IsDisposed)
          return;

        var rawLines = state as List<string>;
        if (rawLines == null || rawLines.Count == 0)
          return;

        var blocks = new List<string>();
        for (int i = 0; i < rawLines.Count; i++)
        {
          VelumCondensedLogRecord record = _condensedResolver.ProcessLine(rawLines[i]);
          if (record == null || !VelumAgentLogCondensedRules.ShouldEmit(record))
            continue;

          string signature = VelumAgentLogCondensedRules.BuildSignature(record);
          if (VelumAgentLogCondensedRules.IsDuplicate(signature, _condensedLastEmittedSignature))
            continue;

          string block = VelumAgentLogCondensedRules.BuildBlock(record, Environment.NewLine);
          if (string.IsNullOrEmpty(block))
            continue;

          _condensedLastEmittedSignature = signature;
          blocks.Add(block);
        }

        if (blocks.Count == 0)
          return;

        _txtAgentOutput.Text = VelumAgentLogCondensedRules.PrependBlocks(
            _txtAgentOutput.Text, blocks, Environment.NewLine);
        TrimCondensedOutputIfNeeded();

        // Самый свежий блок — сверху ленты: прокручиваем окно к началу.
        _txtAgentOutput.SelectionStart = 0;
        _txtAgentOutput.ScrollToCaret();
      }
      catch
      {
        // Окно вывода — некритичная диагностика: сбои резолва не должны влиять на панель.
      }
      finally
      {
        _condensedBgRunning = false;
      }
    }

    /// <summary>
    /// Сбрасывает состояние сокращённого лога (при остановке пульсации и при старте):
    /// следующее чтение начнёт с конца файла (печатаем только новые строки), дедуп и
    /// счётчик обнуляются. Сам файл не трогается.
    /// </summary>
    private void ResetCondensedLogState()
    {
      _condensedLastEmittedSignature = string.Empty;
      _condensedPollCounter = 0;
      _condensedNeedSeekToEnd = true;
    }

    /// <summary>
    /// Мягко ограничивает длину окна вывода, чтобы длительная работа не раздувала TextBox
    /// (текстовый буфер WinForms не бесконечен). Обрезает старые блоки с конца, оставляя
    /// свежие в начале.
    /// </summary>
    private void TrimCondensedOutputIfNeeded()
    {
      const int maxChars = 200000;
      if (_txtAgentOutput == null || _txtAgentOutput.TextLength <= maxChars)
        return;

      try
      {
        _txtAgentOutput.Text = VelumAgentLogCondensedRules.TrimToHead(
            _txtAgentOutput.Text, maxChars, Environment.NewLine);
      }
      catch
      {
      }
    }

    #region События создания объектов движком (у-рефлексы, автоматизмы)

    /// <summary>Подписка на <c>ConditionedReflexCreated</c> активного инстанса системы.</summary>
    private bool _crxCreatedHooked;

    /// <summary>Подписка на <c>AutomatizmCreated</c> активного инстанса системы.</summary>
    private bool _atmzCreatedHooked;

    /// <summary>
    /// Подавление вывода до первого пульса после подписки: загрузочная волна
    /// (<c>NotifyTreeOfLoadedReflexes</c>) поднимает события вне пульсации.
    /// </summary>
    private volatile bool _creationSuppressUntilFirstPulse;

    /// <summary>ID у-рефлексов, существовавших на момент подписки — не печатаем (загрузочная волна).</summary>
    private readonly HashSet<int> _creationBaselineCrxIds = new HashSet<int>();

    /// <summary>ID автоматизмов, существовавших на момент подписки — не печатаем.</summary>
    private readonly HashSet<int> _creationBaselineAtmzIds = new HashSet<int>();

    /// <summary>Уже выведенные сигнатуры «создание» — защита от повторных событий на тот же объект.</summary>
    private readonly HashSet<string> _creationEmittedSignatures = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Лок состояния хуков: <see cref="HookCreationEvents"/> вызывается и из UI-потока
    /// (<c>HookPulseEvents</c>), и из пульс-потока ISIDA (ленивая дописка), baseline-множества
    /// читаются обработчиками событий.
    /// </summary>
    private readonly object _creationHookLock = new object();

    /// <summary>
    /// Идемпотентная подписка на события создания объектов движка. Вызывается из
    /// <see cref="HookPulseEvents"/> и лениво из каждого пульса (<see cref="PumpCreationEvents"/>),
    /// пока ISIDA не инициализирована. При подписке снимается снимок существующих ID и
    /// включается подавление до следующего пульса — загрузочная волна в ленту не попадает.
    /// </summary>
    /// <returns><c>true</c>, если новая подписка выполнена в этом вызове (в этом пульсе
    /// подавление снимать нельзя).</returns>
    private bool HookCreationEvents()
    {
      bool justHooked = false;
      lock (_creationHookLock)
      {
        try
        {
          if (!_crxCreatedHooked && ConditionedReflexesSystem.IsInitialized)
          {
            ConditionedReflexesSystem.Instance.ConditionedReflexCreated += OnEngineConditionedReflexCreated;
            _crxCreatedHooked = true;
            SnapshotConditionedReflexBaseline();
            _creationSuppressUntilFirstPulse = true;
            justHooked = true;
          }

          if (!_atmzCreatedHooked && AutomatizmSystem.IsInitialized)
          {
            AutomatizmSystem.Instance.AutomatizmCreated += OnEngineAutomatizmCreated;
            _atmzCreatedHooked = true;
            SnapshotAutomatizmBaseline();
            _creationSuppressUntilFirstPulse = true;
            justHooked = true;
          }
        }
        catch
        {
          // Сбой подписки не критичен: попробуем в следующем пульсе.
        }
      }

      return justHooked;
    }

    /// <summary>
    /// Отписывается от событий создания (при <c>UnhookPulseEvents</c>). Состояние сбрасывается;
    /// после перезагрузки ISIDA (<see cref="RehookGlobalTimerAfterIsidaReload"/>) подписка
    /// повторится на новых инстансах систем.
    /// </summary>
    private void UnhookCreationEvents()
    {
      lock (_creationHookLock)
      {
        try
        {
          if (_crxCreatedHooked && ConditionedReflexesSystem.IsInitialized)
            ConditionedReflexesSystem.Instance.ConditionedReflexCreated -= OnEngineConditionedReflexCreated;
          if (_atmzCreatedHooked && AutomatizmSystem.IsInitialized)
            AutomatizmSystem.Instance.AutomatizmCreated -= OnEngineAutomatizmCreated;
        }
        catch
        {
        }

        _crxCreatedHooked = false;
        _atmzCreatedHooked = false;
        _creationSuppressUntilFirstPulse = false;
        _creationBaselineCrxIds.Clear();
        _creationBaselineAtmzIds.Clear();
        _creationEmittedSignatures.Clear();
      }
    }

    /// <summary>
    /// Вызывается из обработчика пульса: лениво доделывает подписку и со следующего пульса
    /// после неё снимает подавление. Фаза «после гомеостаза» наступает раньше формирования
    /// рефлексов в психике, поэтому легитимные создания текущего пульса не теряются.
    /// </summary>
    private void PumpCreationEvents()
    {
      if (!HookCreationEvents())
        _creationSuppressUntilFirstPulse = false;
    }

    private void SnapshotConditionedReflexBaseline()
    {
      _creationBaselineCrxIds.Clear();
      try
      {
        foreach (var reflex in ConditionedReflexesSystem.Instance.GetAllConditionedReflexes())
        {
          if (reflex != null)
            _creationBaselineCrxIds.Add(reflex.Id);
        }
      }
      catch
      {
      }
    }

    private void SnapshotAutomatizmBaseline()
    {
      _creationBaselineAtmzIds.Clear();
      try
      {
        foreach (var automatizm in AutomatizmSystem.Instance.GetAllAutomatizms())
        {
          if (automatizm != null)
            _creationBaselineAtmzIds.Add(automatizm.ID);
        }
      }
      catch
      {
      }
    }

    /// <summary>
    /// Поток пульса ISIDA: фильтрует загрузочную волну/подавление и маршалирует данные
    /// события в UI-поток SolidWorks (правила проекта про COM/UI).
    /// </summary>
    private void OnEngineConditionedReflexCreated(ConditionedReflexesSystem.ConditionedReflexCreatedEventArgs e)
    {
      if (e == null || _creationSuppressUntilFirstPulse)
        return;

      lock (_creationHookLock)
      {
        if (_creationBaselineCrxIds.Contains(e.ReflexId))
          return;
      }

      QueueCreationEventUi(VelumCreatedEntityKind.УсловныйРефлекс, e.ReflexId, e.Level3);
    }

    /// <summary>Поток пульса ISIDA: то же для автоматизмов (стимул не резолвится — Level3 нет).</summary>
    private void OnEngineAutomatizmCreated(int automatizmId)
    {
      if (automatizmId <= 0 || _creationSuppressUntilFirstPulse)
        return;

      lock (_creationHookLock)
      {
        if (_creationBaselineAtmzIds.Contains(automatizmId))
          return;
      }

      QueueCreationEventUi(VelumCreatedEntityKind.Автоматизм, automatizmId, 0);
    }

    private void QueueCreationEventUi(VelumCreatedEntityKind kind, int id, int stimulusImageId)
    {
      try
      {
        if (!IsDisposed && IsHandleCreated)
          BeginInvoke(new Action(() => EmitCreationBlockUi(kind, id, stimulusImageId)));
      }
      catch
      {
      }
    }

    /// <summary>
    /// UI-поток: резолвит мотор и стимул по справочникам ISIDA, собирает блок «СОЗДАНИЕ»
    /// и вставляет его в начало ленты вывода (дедуп по типу+ID). Маршалируется через
    /// <c>BeginInvoke</c> из <see cref="QueueCreationEventUi"/> (без params-массива —
    /// иначе WinForms разворачивает его в аргументы делегата).
    /// </summary>
    private void EmitCreationBlockUi(VelumCreatedEntityKind kind, int id, int stimulusImageId)
    {
      try
      {
        if (IsDisposed)
          return;

        var record = new VelumCreatedEntityRecord
        {
          Kind = kind,
          Id = id
        };

        string signature = VelumAgentCreationLogRules.BuildSignature(record);
        if (string.IsNullOrEmpty(signature))
          return;

        lock (_creationHookLock)
        {
          if (!_creationEmittedSignatures.Add(signature))
            return;
        }

        record.MotorName = VelumAgentLogCondensedResolver.ResolveCreatedMotorName(record.Kind, record.Id);

        if (stimulusImageId > 0)
        {
          List<VelumCondensedStimulusLine> lines =
              VelumAgentLogCondensedResolver.ResolveStimulusLines(stimulusImageId);
          if (lines.Count > 0)
            record.StimulusLine = lines[0].FormatLine();
        }

        string block = VelumAgentCreationLogRules.BuildBlock(record, Environment.NewLine);
        if (string.IsNullOrEmpty(block))
          return;

        _txtAgentOutput.Text = VelumAgentLogCondensedRules.PrependBlocks(
            _txtAgentOutput.Text, new List<string> { block }, Environment.NewLine);
        TrimCondensedOutputIfNeeded();

        _txtAgentOutput.SelectionStart = 0;
        _txtAgentOutput.ScrollToCaret();
      }
      catch
      {
        // Лента — некритичная диагностика: сбои резолва не должны влиять на панель.
      }
    }

    #endregion
  }
}
