using System;
using System.Collections.Generic;
using ISIDA.Common;
using ISIDA.Gomeostas;
using ISIDA.Scenarios;
using Velum.Configuration;
using Velum.SolidHomeostasis;

namespace Velum.Isida
{
  /// <summary>
  /// Хост прогонов сценариев оператора в Velum: владеет <see cref="OperatorScenarioRunner"/>
  /// и <see cref="VelumScenarioPult"/>, выполняет предзапуск (стадия, «норма», начальные значения
  /// гомеостаза), ускорение пульса и сбор простого отчёта по завершении.
  /// Фазы пульса подаёт <see cref="ReactiveCore.VelumEnginePulseBridge"/> (методы
  /// <see cref="OnPulseAfterGomeostasisBeforePsychic"/> / <see cref="OnPulseCompleted"/>).
  /// </summary>
  internal static class VelumScenarioHost
  {
    private static readonly VelumScenarioPult Pult = new VelumScenarioPult();
    private static readonly OperatorScenarioRunner Runner = new OperatorScenarioRunner();

    private static bool _wired;
    private static bool _accelActive;
    private static bool _observationModeSaved;
    private static bool _savedObservationMode;

    // Пауза стабилизации гомеостаза после программного сброса в «норму» перед стартом раннера.
    private static bool _settlePending;
    private static int _settleTargetPulse;
    private static ScenarioDocument _settleDoc;

    // Состояние группового прогона.
    private static bool _batchActive;
    private static int _batchCurrentIndex;
    private static int _batchTotalMembers;
    private static string _batchCurrentMemberTitle;
    private static string _batchReportFolder;
    private static List<BatchRunResult> _batchResults = new List<BatchRunResult>();
    private static VelumScenarioGroupDocument _batchGroupDoc;

    /// <summary>Результат одного члена группового прогона.</summary>
    internal sealed class BatchRunResult
    {
      internal int MemberIndex { get; set; }
      internal int ScenarioId { get; set; }
      internal string Title { get; set; } = "";
      internal OperatorScenarioCompletedEventArgs EventArgs { get; set; }
      internal ScenarioDocument Document { get; set; }
      internal string HtmlReportPath { get; set; } = "";
    }

    /// <summary>Изменение состояния «идёт прогон / нет» (для обновления кнопок UI).</summary>
    internal static event Action RunningStateChanged;

    /// <summary>Завершение прогона (для показа итога/отчёта в UI).</summary>
    internal static event Action<OperatorScenarioCompletedEventArgs> RunFinished;

    /// <summary>Сгенерирован HTML-отчёт (путь к файлу) — для кнопки «Открыть отчёт» в UI.</summary>
    internal static event Action<string> ReportGenerated;

    /// <summary>Истина, пока идёт прогон или пауза стабилизации.</summary>
    internal static bool IsBusy => Runner.IsRunning || _settlePending || _batchActive;

    /// <summary>
    /// Текущий индекс члена в групповом прогоне (−1 если одиночный).
    /// </summary>
    internal static int BatchCurrentIndex => _batchActive ? _batchCurrentIndex : -1;

    /// <summary>
    /// Общее число членов в групповом прогоне (0 если не групповой).
    /// </summary>
    internal static int BatchTotalMembers => _batchActive ? _batchTotalMembers : 0;

    /// <summary>
    /// Название текущего члена группы (для UI).
    /// </summary>
    internal static string BatchCurrentMemberTitle => _batchActive ? _batchCurrentMemberTitle : "";

    /// <summary>
    /// Подписка на событие смены состояния раннера (вызывается один раз при инициализации адаптера).
    /// </summary>
    internal static void EnsureWired()
    {
      if (_wired)
        return;
      Runner.RunningStateChanged += () => RunningStateChanged?.Invoke();
      Runner.Finished += OnRunnerFinished;
      _wired = true;
    }

    /// <summary>
    /// Запускает прогон сценария: предзапуск гомеостаза/стадии, сброс счётчика пульсов,
    /// ускорение и старт раннера. Возвращает текст ошибки при неуспехе.
    /// </summary>
    /// <param name="doc">Документ сценария (загруженный из хранилища).</param>
    /// <returns><c>true</c>, если прогон запущен (или поставлен в очередь стабилизации).</returns>
    internal static string TryStart(ScenarioDocument doc)
    {
      EnsureWired();

      if (IsBusy)
        return "Прогон уже выполняется.";

      if (doc == null || doc.Lines == null || doc.Lines.Count == 0)
        return "Сценарий пуст (нет шагов).";

      if (!VelumIsidaHost.IsReady)
        return "ISIDA не инициализирована — откройте вкладку агента и запустите пульсацию.";

      if (!GlobalTimer.IsPulsationRunning)
        return "Пульсация выключена — включите агента перед прогоном.";

      string preErr = TryApplyPreRun(doc);
      if (preErr != null)
        return preErr;

      // Ускорение пульса по календарю на время прогона (без анимации).
      int coeff = NormalizeCoeff(doc.Header.RunPulseTimingCoefficient);
      if (coeff > 1)
      {
        GlobalTimer.SetPulseWallClockAcceleration(coeff, suppressAnimation: true);
        _accelActive = true;
      }

      // Режим наблюдения по шапке сценария (сохранив прежний для восстановления).
      _savedObservationMode = AppGlobalState.ObservationMode;
      _observationModeSaved = true;
      AppGlobalState.ObservationMode = doc.Header.ScenarioObservationMode;

      // Если требуется «норма» — выдерживаем паузу стабилизации, затем старт.
      if (doc.Header.PreRunNormalHomeostasisState)
      {
        BeginSettle(doc);
        return null;
      }

      StartRunnerNow(doc);
      return null;
    }

    /// <summary>Останавливает прогон по команде пользователя.</summary>
    internal static void Stop()
    {
      if (_settlePending)
      {
        AbandonSettle();
        RestoreAfterRun();
        if (_batchActive)
          FinishBatchGroup(true);
        RunningStateChanged?.Invoke();
        return;
      }
      Runner.StopUser();
    }

    /// <summary>
    /// Запускает групповой прогон: последовательный запуск членов группы.
    /// </summary>
    /// <param name="groupId">ID группы.</param>
    /// <param name="reportFolder">Каталог для отчётов.</param>
    /// <returns>Текст ошибки либо <c>null</c>.</returns>
    internal static string TryStartGroup(int groupId, string reportFolder)
    {
      EnsureWired();

      if (IsBusy)
        return "Прогон уже выполняется.";

      if (!VelumIsidaHost.IsReady)
        return "ISIDA не инициализирована — откройте вкладку агента и запустите пульсацию.";

      if (!GlobalTimer.IsPulsationRunning)
        return "Пульсация выключена — включите агента перед прогоном.";

      try
      {
        VelumScenarioGroupDocument groupDoc = VelumScenarioGroupStorage.LoadGroup(groupId);
        if (groupDoc.Members == null || groupDoc.Members.Count == 0)
          return "В группе нет сценариев для запуска.";

        _batchActive = true;
        _batchCurrentIndex = 0;
        _batchTotalMembers = groupDoc.Members.Count;
        _batchReportFolder = reportFolder;
        _batchResults.Clear();
        _batchCurrentMemberTitle = "";
        _batchGroupDoc = groupDoc;

        // Запускаем первый член.
        return StartNextBatchMember(groupDoc);
      }
      catch (Exception ex)
      {
        _batchActive = false;
        return "Не удалось загрузить группу: " + ex.Message;
      }
    }

    private static string StartNextBatchMember(VelumScenarioGroupDocument groupDoc)
    {
      if (_batchCurrentIndex >= _batchTotalMembers)
      {
        FinishBatchGroup(false);
        return null;
      }

      VelumScenarioGroupMember member = groupDoc.Members[_batchCurrentIndex];

      try
      {
        ScenarioDocument doc = VelumScenarioStorage.LoadScenario(member.ScenarioId);
        doc = VelumScenarioGroupDocument.ApplyMemberToScenario(
            doc, member, groupDoc.RunPulseTimingCoefficient);

        string preErr = TryApplyPreRun(doc);
        if (preErr != null)
          return preErr;

        // Ускорение пульса по коэффициенту группы.
        int coeff = NormalizeCoeff(groupDoc.RunPulseTimingCoefficient);
        if (coeff > 1)
        {
          GlobalTimer.SetPulseWallClockAcceleration(coeff, suppressAnimation: true);
          _accelActive = true;
        }

        // Режим наблюдения.
        _savedObservationMode = AppGlobalState.ObservationMode;
        _observationModeSaved = true;
        AppGlobalState.ObservationMode = doc.Header.ScenarioObservationMode;

        // Имя текущего члена для UI.
        try
        {
          List<ScenarioHeader> registry = VelumScenarioStorage.LoadRegistry();
          ScenarioHeader hdr = registry.Find(h => h.Id == member.ScenarioId);
          _batchCurrentMemberTitle = "#" + member.ScenarioId + " " + (hdr?.Title ?? "");
        }
        catch
        {
          _batchCurrentMemberTitle = "#" + member.ScenarioId;
        }

        // Если требуется «норма» — пауза стабилизации, затем старт.
        if (doc.Header.PreRunNormalHomeostasisState)
        {
          BeginSettle(doc);
          return null;
        }

        StartRunnerNow(doc);
        return null;
      }
      catch (Exception ex)
      {
        return "Ошибка запуска члена #" + _batchCurrentIndex + ": " + ex.Message;
      }
    }

    /// <summary>
    /// Обработчик завершения раннера: различает одиночный и групповой сценарии.
    /// </summary>
    private static void OnRunnerFinished(object sender, OperatorScenarioCompletedEventArgs e)
    {
      if (_batchActive)
      {
        OnBatchMemberFinished(e);
        return;
      }
      RestoreAfterRun();
      WriteRunReport(e);
      RunningStateChanged?.Invoke();
      RunFinished?.Invoke(e);
    }

    private static void OnBatchMemberFinished(OperatorScenarioCompletedEventArgs e)
    {
      // Сохраняем результат текущего члена.
      VelumScenarioGroupDocument groupDoc = null;
      try
      {
        // Находим группу по ID из первого результата или загружаем текущую.
        var registry = VelumScenarioGroupStorage.LoadGroupRegistry();
        if (_batchCurrentIndex < registry.Count)
        {
          groupDoc = VelumScenarioGroupStorage.LoadGroup(registry[_batchCurrentIndex].Id);
        }
      }
      catch { /* группа уже удалена — не критично */ }

      string title = _batchCurrentMemberTitle;
      int sid = groupDoc != null && _batchCurrentIndex < groupDoc.Members.Count
          ? groupDoc.Members[_batchCurrentIndex].ScenarioId : 0;

      var memberResult = new BatchRunResult
      {
        MemberIndex = _batchCurrentIndex,
        ScenarioId = sid,
        Title = title,
        EventArgs = e,
        Document = e.Document
      };

      // Детальный HTML-отчёт по члену группы (сразу после его прогона, пока лог в окне актуален).
      try
      {
        if (memberResult.Document != null)
        {
          string folder = _batchReportFolder ?? VelumAppConfig.ScenarioReportsFolderPath;
          VelumScenarioReportHtmlBuilder.ReportResult hr =
              VelumScenarioReportHtmlBuilder.BuildAndSaveSingle(memberResult.Document, e, folder);
          if (hr != null)
            memberResult.HtmlReportPath = hr.HtmlPath;
        }
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum member HTML report failed: " + ex.Message);
      }

      _batchResults.Add(memberResult);

      if (!e.Success || e.AbortedByUser || e.AbortedByPulsationStop)
      {
        FinishBatchGroup(true);
        return;
      }

      _batchCurrentIndex++;
      RestoreAfterRun();

      if (_batchCurrentIndex >= _batchTotalMembers)
      {
        FinishBatchGroup(false);
        return;
      }

      // Запускаем следующего — перегружаем группу заново (маленький файл, безопасно).
      try
      {
        var registry = VelumScenarioGroupStorage.LoadGroupRegistry();
        if (_batchCurrentIndex < registry.Count)
        {
          VelumScenarioGroupDocument nextGroup = VelumScenarioGroupStorage.LoadGroup(registry[_batchCurrentIndex].Id);
          string err = StartNextBatchMember(nextGroup);
          if (err != null)
          {
            Logger.Warning("Velum batch run error at member " + _batchCurrentIndex + ": " + err);
            FinishBatchGroup(true);
          }
        }
        else
        {
          FinishBatchGroup(true);
        }
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum batch run exception at member " + _batchCurrentIndex + ": " + ex.Message);
        FinishBatchGroup(true);
      }
    }

    private static void FinishBatchGroup(bool userAborted)
    {
      _batchActive = false;
      RestoreAfterRun();
      WriteBatchReport();
      RunningStateChanged?.Invoke();
      _batchCurrentIndex = 0;
      _batchTotalMembers = 0;
      _batchCurrentMemberTitle = "";
    }

    private static void WriteBatchReport()
    {
      try
      {
        if (_batchResults.Count == 0)
          return;

        string folder = _batchReportFolder ?? VelumAppConfig.ScenarioReportsFolderPath;
        VelumScenarioReportHtmlBuilder.ReportResult result =
            VelumScenarioReportHtmlBuilder.BuildAndSaveGroup(_batchGroupDoc, _batchResults, folder);
        if (result != null && !string.IsNullOrEmpty(result.HtmlPath))
          ReportGenerated?.Invoke(result.HtmlPath);
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum batch report failed: " + ex.Message);
      }
      finally
      {
        _batchGroupDoc = null;
      }
    }

    /// <summary>Хост остановил пульсацию — прерываем прогон.</summary>
    internal static void OnPulsationStopped()
    {
      if (_settlePending)
      {
        AbandonSettle();
        RestoreAfterRun();
        if (_batchActive)
          FinishBatchGroup(true);
        RunningStateChanged?.Invoke();
        return;
      }
      if (Runner.IsRunning)
        Runner.OnPulsationStopped();
    }

    /// <summary>
    /// Фаза пульса «после гомеостаза, до психики»: сначала обрабатывает паузу стабилизации,
    /// иначе передаёт пульс раннеру. Вызывается на UI-потоке панели (см. мост).
    /// </summary>
    /// <param name="globalPulseCount">Текущий глобальный номер пульса.</param>
    internal static void OnPulseAfterGomeostasisBeforePsychic(int globalPulseCount)
    {
      if (_settlePending)
      {
        TickSettle(globalPulseCount);
        return;
      }
      Runner.OnGlobalPulseBeforeProcessing(globalPulseCount);
    }

    /// <summary>
    /// Фаза «пульс полностью обработан»: завершает прогон, если на этом пульсе был последний шаг.
    /// </summary>
    internal static void OnPulseCompleted()
    {
      Runner.TryFinishAfterPulseCompleted();
    }

    private static void StartRunnerNow(ScenarioDocument doc)
    {
      // Обнуляем глобальный счётчик, чтобы номера пульсов прогона совпадали с логами (как в AIStudio).
      GlobalTimer.Reset();
      Runner.Start(doc, () => Pult, () => VelumIsidaHost.Context.CancelWaitingPeriodAndResetMirror());
      RunningStateChanged?.Invoke();
    }

    private static void BeginSettle(ScenarioDocument doc)
    {
      _settleDoc = doc;
      _settlePending = true;
      // После сброса в норму ждём DynamicTime+1 пульсов, чтобы гомеостаз стабилизировался.
      int totalPulses = Math.Max(1, VelumAppConfig.DynamicTime + 1);
      _settleTargetPulse = GlobalTimer.GlobalPulsCount + totalPulses;
      RunningStateChanged?.Invoke();
    }

    private static void TickSettle(int globalPulseCount)
    {
      if (!GlobalTimer.IsPulsationRunning)
      {
        AbandonSettle();
        RestoreAfterRun();
        if (_batchActive)
          FinishBatchGroup(true);
        RunningStateChanged?.Invoke();
        return;
      }

      if (globalPulseCount < _settleTargetPulse)
        return;

      ScenarioDocument doc = _settleDoc;
      AbandonSettle();
      StartRunnerNow(doc);
    }

    private static void AbandonSettle()
    {
      _settlePending = false;
      _settleDoc = null;
    }

    /// <summary>
    /// Предзапуск: очистка данных, переход на стадию, программная «норма», начальные значения гомеостаза.
    /// </summary>
    private static string TryApplyPreRun(ScenarioDocument doc)
    {
      ScenarioHeader h = doc.Header ?? new ScenarioHeader();
      GomeostasSystem g = VelumIsidaHost.Context.Gomeostas;

      if (h.PreRunClearAgentData)
      {
        EvolutionStageChangeResult clear = g.ClearEvolutionStageDataForScenarioPreRun();
        if (clear != null && !clear.Success)
          return clear.Message ?? "Не удалось очистить данные стадии перед прогоном.";
      }

      bool wantStage = h.PreRunTargetStage >= 0 && h.PreRunTargetStage <= 5;
      if (wantStage)
      {
        int current = g.GetAgentState()?.EvolutionStage ?? 0;
        if (h.PreRunTargetStage != current)
        {
          bool force = h.PreRunTargetStage < current || h.PreRunTargetStage > current + 1;
          bool skipClear = !h.PreRunClearAgentData;
          EvolutionStageChangeResult res = g.SetEvolutionStage(h.PreRunTargetStage, force, skipClear);
          if (res != null && !res.Success)
            return res.Message ?? "Не удалось перейти на заданную стадию.";
        }
      }

      if (h.PreRunNormalHomeostasisState)
      {
        try
        {
          g.ApplySpeedOrientedNormalHomeostasisForScenarioPreRun();
        }
        catch (Exception ex)
        {
          return "Сброс в норму: " + ex.Message;
        }
      }

      // Начальные значения параметров из шапки («id=value;…»): пишем через штатный путь хоста.
      Dictionary<int, float> initial = ScenarioHomeostasisValuesFormat.Parse(h.InitialHomeostasisValues);
      if (initial.Count > 0)
        g.HostBatchUpdateParameterValues(initial);

      return null;
    }

    private static void RestoreAfterRun()
    {
      if (_accelActive)
      {
        GlobalTimer.ClearPulseWallClockAcceleration();
        _accelActive = false;
      }
      if (_observationModeSaved)
      {
        AppGlobalState.ObservationMode = _savedObservationMode;
        _observationModeSaved = false;
      }
    }

    /// <summary>
    /// Отчёт прогона: генерирует HTML со сравнением ожиданий с фактом по логам
    /// (<see cref="VelumScenarioReportHtmlBuilder"/>). Путь публикуется через <see cref="ReportGenerated"/>.
    /// </summary>
    private static void WriteRunReport(OperatorScenarioCompletedEventArgs e)
    {
      try
      {
        ScenarioDocument doc = e.Document;
        if (doc == null)
          return;

        string folder = VelumAppConfig.ScenarioReportsFolderPath;
        VelumScenarioReportHtmlBuilder.ReportResult result =
            VelumScenarioReportHtmlBuilder.BuildAndSaveSingle(doc, e, folder);
        if (result != null && !string.IsNullOrEmpty(result.HtmlPath))
          ReportGenerated?.Invoke(result.HtmlPath);
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum scenario report failed: " + ex.Message);
      }
    }

    private static int NormalizeCoeff(int coeff)
    {
      if (coeff == 5 || coeff == 10 || coeff == 20 || coeff == 50 || coeff == 100)
        return coeff;
      return 1;
    }
  }
}