using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
    /// и дописывает новые блоки в окно вывода.
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

        TrimCondensedOutputIfNeeded();
        for (int i = 0; i < blocks.Count; i++)
        {
          if (_txtAgentOutput.TextLength > 0)
            _txtAgentOutput.AppendText(Environment.NewLine + Environment.NewLine);
          _txtAgentOutput.AppendText(blocks[i]);
        }
        _txtAgentOutput.SelectionStart = _txtAgentOutput.TextLength;
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
    /// (текстовый буфер WinForms не бесконечен). Обрезает старые блоки, оставляя хвост.
    /// </summary>
    private void TrimCondensedOutputIfNeeded()
    {
      const int maxChars = 200000;
      if (_txtAgentOutput == null || _txtAgentOutput.TextLength <= maxChars)
        return;

      try
      {
        string text = _txtAgentOutput.Text;
        int cut = text.Length - (maxChars / 2);
        if (cut <= 0)
          return;

        // Режем по границе блока (по двойному переводу строки), чтобы не рвать блок пополам.
        int boundary = text.IndexOf(
            Environment.NewLine + Environment.NewLine,
            cut,
            StringComparison.Ordinal);
        if (boundary >= 0 && boundary + 2 * Environment.NewLine.Length < text.Length)
          cut = boundary + 2 * Environment.NewLine.Length;

        _txtAgentOutput.Text = text.Substring(cut);
      }
      catch
      {
      }
    }
  }
}
