using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using ISIDA.Common;
using ISIDA.Gomeostas;
using Velum.Configuration;
using Velum.Isida;
using Velum.ReactiveCore;
using Velum.UI.ProductRegistry;
using Xarial.XCad;
using Xarial.XCad.Documents;
using Xarial.XCad.SolidWorks;

namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Подписка на <see cref="GlobalTimer.OnPulseBeforeGomeostasis"/>: опрос проб метрик SolidWorks по ProbeKey
  /// из EA среды (<see cref="VelumSolidEnvironmentInfluenceCatalog"/>), оркестратор давления/release
  /// (<see cref="VelumSolidMetricPressureOrchestrator"/>), публикация снимка и запись в гомеостаз.
  /// Release, давление и hold-refresh пишутся раздельно (см. <c>docs/SOLID_METRIC_PRESSURE.md</c>).
  /// Запись в движок — с синхронизацией PreviousValue (<see cref="ApplyHostSnapshotWithPreviousValueSync"/>).
  /// Pressure и hold-refresh не проходят <see cref="VelumAppConfig.SolidHostImpulseMinParameterDelta"/>.
  /// Сравнение дельт метрик — <see cref="VelumSolidPulseMetricCompare"/>.
  /// Пересбор снимка — <see cref="VelumSolidProbeRefreshPlanner"/>.
  /// </summary>
  public static class VelumSolidEnvironmentBridge
  {
    /// <summary>
    /// Шаг для двухфазной записи: достаточно, чтобы прошёл сеттер Value, не заметен в UI 0…100.
    /// </summary>
    private const float HostValueNudgeEpsilon = 0.0001f;

    /// <summary>
    /// Порог «значение среды реально изменилось между пульсами». Выше — одна запись в ISIDA без nudge,
    /// чтобы предыдущее значение параметра в движке оставалось от прошлого пульса и срабатывал переход Bad→Well
    /// (<see cref="HomeostasisCalculator.CalculateParameterState"/>). Ниже — только микродрейф: нужен прежний двухшаговый nudge.
    /// </summary>
    private const float HostValueMeaningfulChangeEpsilon = 0.02f;

    /// <summary>
    /// Сколько тактов пульса после старта пульсации не опрашивать SW (уменьшает ложные метрики сразу после включения).
    /// </summary>
    private const int SolidMetricsWarmupPulseCount = 2;

    private static bool _hooked;
    private static IXApplication _solidApp;
    private static Control _invokeTarget;
    private static volatile bool _pollSolidWhilePulse;
    private static int _solidMetricsWarmupPulsesRemaining;
    private static int _solidProbeRefreshInFlight;
    private static int _solidProbeRefreshGeneration;

    internal static event Action SolidCommandBufferChanged;

    /// <summary>
    /// Смена активного документа / эпизода: отменить результат уже запущенного COM-опроса,
    /// чтобы он не перезаписал gate снимком предыдущего документа.
    /// </summary>
    internal static void InvalidateInFlightSolidProbeRefresh()
    {
      Interlocked.Increment(ref _solidProbeRefreshGeneration);
    }

    /// <summary>
    /// Зарегистрировать сессию SolidWorks и элемент WinForms для маршаллинга COM на поток UI панели задач.
    /// </summary>
    public static void SetSolidWorksSession(IXApplication app, Control taskPaneControl)
    {
      VelumSolidWorksEventsConnector.Detach();
      VelumCommandIdleFlusher.Detach();

      _solidApp = app;
      _invokeTarget = taskPaneControl;

      VelumSolidCommandBuffer.SyncRecordingFromConfig();

      if (taskPaneControl != null)
        VelumCommandIdleFlusher.Attach();

      // Подписка на события — всегда при готовом ISIDA (независимо от пульсации)
      if (VelumIsidaHost.IsReady)
        EnsureEventSubscription();
    }

    /// <summary>Текущая сессия XCad/SolidWorks (может быть null).</summary>
    internal static ISwApplication TryGetSolidWorksApplication()
    {
      return _solidApp as ISwApplication;
    }

    /// <summary>
    /// Смена активного документа SW: новый эпизод среды — см. <see cref="VelumSolidDocumentEpisodeReset"/>.
    /// Под капотом — sync «Наименование» → Name реестра по FilePath (без OpenDoc).
    /// </summary>
    internal static void OnActiveSolidDocumentChanged()
    {
      VelumSolidDocumentEpisodeReset.OnActiveSolidDocumentChanged();
      VelumProductRegistryIntegrityScheduler.NotifyOpenDocumentsScopeChanged();
      try
      {
        VelumProductRegistryNameSync.TrySyncActiveDocument(
            TryGetSolidWorksApplication());
        VelumProductRegistryExportMetaSync.TrySyncActiveDocument(
            TryGetSolidWorksApplication());
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum registry name sync on active doc: " + ex.Message);
      }
    }

    /// <summary>Снять ссылки на SW (например, при отключении надстройки).</summary>
    public static void ClearSolidWorksSession()
    {
      VelumSolidWorksEventsConnector.Detach();
      VelumCommandIdleFlusher.Detach();
      VelumSolidWorksMetricsCache.Clear();
      VelumSolidPulseMetricCompare.Clear();
      VelumSolidMetricPressureOrchestrator.Clear();
      VelumSolidProbeRefreshPlanner.Clear();
      VelumSolidCommandBuffer.Clear();
      _solidApp = null;
      _invokeTarget = null;
      _pollSolidWhilePulse = false;
      _solidMetricsWarmupPulsesRemaining = 0;
      VelumProductRegistryIntegrityScheduler.SyncEnabledFromPulse();
    }

    /// <summary>Выполнить действие на потоке UI панели задач (COM SolidWorks / WinForms).</summary>
    public static void RunOnTaskPaneUiThread(Action action)
    {
      if (action == null)
        return;

      try
      {
        if (_invokeTarget != null && _invokeTarget.IsHandleCreated && _invokeTarget.InvokeRequired)
          _invokeTarget.Invoke(action);
        else
          action();
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum RunOnTaskPaneUiThread: " + ex.Message);
      }
    }

    /// <summary>
    /// Получить элемент WinForms панели задач (для прямого вызова методов на UI-потоке).
    /// </summary>
    internal static System.Windows.Forms.Control TryGetTaskPane()
    {
      return _invokeTarget;
    }

    /// <summary>Уведомить подписчиков об изменении буфера команд (безопасно с потока COM SolidWorks).</summary>
    internal static void NotifySolidCommandBufferChanged()
    {
      Action h = SolidCommandBufferChanged;
      if (h == null)
        return;
      RunOnTaskPaneUiThread(() =>
      {
        try
        {
          h();
        }
        catch
        {
        }
      });
    }

    /// <summary>
    /// Включить опрос метрик SW на каждом такте пульса (только при активной пульсации и готовой ISIDA).
    /// </summary>
    public static void SetSolidPollingEnabled(bool enabled)
    {
      bool was = _pollSolidWhilePulse;
      _pollSolidWhilePulse = enabled;
      if (enabled)
        VelumSolidWorksMetricsCache.Invalidate();
      if (enabled && !was)
        BeginSolidPulseProbeCycle();
      else if (!enabled)
        _solidMetricsWarmupPulsesRemaining = 0;

      VelumProductRegistryIntegrityScheduler.EnsureAttached();
    }

    /// <summary>
    /// Обеспечить подписку на события SW для отслеживания изменений.
    /// Работает при готовом ISIDA независимо от пульсации — отслеживание изменений
    /// является самостоятельным свойством системы, а не частью гомеостаза.
    /// </summary>
    private static void EnsureEventSubscription()
    {
      if (_solidApp is ISwApplication swApp)
      {
        if (!VelumSolidWorksEventsConnector.IsEventSubscriptionActive)
          VelumSolidWorksEventsConnector.Attach(swApp);
        else
          VelumSolidWorksEventsConnector.RehookActiveDocument(swApp);
      }
    }

    /// <summary>Согласовать флаг опроса с текущим состоянием пульсации и ISIDA.</summary>
    public static void SyncSolidPollingWithPulseState()
    {
      bool run;
      try
      {
        run = GlobalTimer.IsPulsationRunning && VelumIsidaHost.IsReady;
      }
      catch
      {
        run = false;
      }

      bool wasRunning = _pollSolidWhilePulse;
      _pollSolidWhilePulse = run;
      if (run)
      {
        if (!wasRunning)
        {
          BeginSolidPulseProbeCycle();
          // Проверяем накопленные вне пульса изменения для UI-индикации
          VelumSolidProbeRefreshPlanner.CheckForUntrackedChangesOnPulseStart();
        }
      }
      else
      {
        _solidMetricsWarmupPulsesRemaining = 0;
      }

      // Подписка на события — всегда при готовом ISIDA (независимо от пульсации)
      if (VelumIsidaHost.IsReady)
        EnsureEventSubscription();

      VelumProductRegistryIntegrityScheduler.EnsureAttached();
    }

    /// <summary>
    /// Включить или выключить трассировку по пульсу: запись метрик Velum→движок и ветки удержания Плохо/Хорошо в ISIDA
    /// (окно Output при отладке). Из Immediate Window: <c>VelumSolidEnvironmentBridge.SetSolidHomeostasisPulseTrace(true)</c>.
    /// </summary>
    public static void SetSolidHomeostasisPulseTrace(bool enabled)
    {
      VelumAppConfig.SetSetting("SolidHomeostasisPulseTrace", enabled ? "true" : "false");
      VelumSolidDiagLog.SetPulseSolidHomeostasisTrace(enabled);
    }

    /// <summary>Подписаться на пульс ISIDA и засеять снимок из движка.</summary>
    public static void HookAfterIsidaReady()
    {
      if (_hooked)
        return;

      if (!VelumIsidaHost.IsReady)
        return;

      SeedSnapshotFromEngine();
      VelumSolidDiagLog.SetPulseSolidHomeostasisTrace(VelumAppConfig.SolidHomeostasisPulseTrace);
      GlobalTimer.OnPulseBeforeGomeostasis += OnPulseBeforeGomeostasis;
      VelumEnginePulseBridge.Hook();
      _hooked = true;
      SyncSolidPollingWithPulseState();
    }

    /// <summary>Отписаться и очистить опубликованный снимок.</summary>
    public static void Unhook()
    {
      if (!_hooked)
        return;
      GlobalTimer.OnPulseBeforeGomeostasis -= OnPulseBeforeGomeostasis;
      VelumEnginePulseBridge.Unhook();
      // Dispose контекста ISIDA обнуляет делегаты GlobalTimer (ClearSystems) — шедулер
      // целостности должен сбросить флаг подписки, иначе после перезагрузки ISIDA
      // он не переподпишется на пульс и потеряет сканирование реестра.
      VelumProductRegistryIntegrityScheduler.Detach();
      _hooked = false;
      VelumSolidWorksMetricsCache.Clear();
      VelumSolidEnvironmentGate.Clear();
      VelumSolidPulseMetricCompare.Clear();
      VelumSolidMetricPressureOrchestrator.Clear();
      VelumSolidProbeRefreshPlanner.Clear();
      VelumCadDegradedModeSync.Reset();
    }

    /// <summary>
    /// Сброс кэша и снимка проб; значения метрик среды больше не читаются из движка по Id параметров.
    /// </summary>
    public static void SeedSnapshotFromEngine()
    {
      if (!VelumIsidaHost.IsReady)
        return;
      VelumSolidWorksMetricsCache.Invalidate();
      VelumSolidPulseMetricCompare.Clear();
      VelumSolidEnvironmentGate.Clear();
    }

    private static void OnPulseBeforeGomeostasis(int pulseNumber)
    {
      try
      {
        if (!VelumIsidaHost.IsReady)
          return;
        if (!GlobalTimer.IsPulsationRunning)
          return;

        GomeostasSystem g = VelumIsidaHost.Context.Gomeostas;

        VelumSolidDocumentEditContext editContext =
            _solidApp != null ? VelumSolidDocumentEditContextResolver.Resolve(_solidApp) : null;

        // Документ закрыт: снять только документные метрики; host-global (реестр) продолжает давить.
        if (editContext?.ActiveDocument == null)
        {
          if (VelumSolidMetricPressureReset.HasPendingEnvironmentPressureResidue())
            VelumSolidMetricPressureReset.OnNoActiveSolidDocument(g);

          VelumProductRegistryIntegrityProbes.PublishScoresToGate(registryOnlySnapshot: true);
          ApplyMetricPressureBeforeGomeostasis(pulseNumber, g, editContext);
        }
        else
        {
          EnsurePublishedSnapshotForPressure();
          PrunePublishedSnapshotForEditContext(editContext);
          VelumProductRegistryIntegrityProbes.PublishScoresToGate(registryOnlySnapshot: false);
          ApplyMetricPressureBeforeGomeostasis(pulseNumber, g, editContext);
        }

        if (_pollSolidWhilePulse)
        {
          if (_solidMetricsWarmupPulsesRemaining > 0)
            _solidMetricsWarmupPulsesRemaining--;
          else if (_solidApp != null)
            ScheduleSolidProbeRefresh(pulseNumber);
          else
          {
            VelumSolidEnvironmentGate.Clear();
            VelumSolidEnvironmentGate.SetLastProbeOutcome(
                VelumSolidProbeErrorKind.SessionUnavailable,
                pulseNumber);
          }
        }

        float sessionHealth = VelumSolidSessionHealth.ComputeScore(_solidApp);
        if (_pollSolidWhilePulse && _solidMetricsWarmupPulsesRemaining <= 0)
          VelumSolidDiagLog.WriteSessionHealth(pulseNumber, sessionHealth, VelumSolidEnvironmentGate.LastErrorKind);
        VelumCadDegradedModeSync.SyncFromSessionHealthOnPulse(pulseNumber, sessionHealth);

        IReadOnlyDictionary<string, float> probeSnap = VelumSolidEnvironmentGate.GetPublishedSnapshot();
        if (probeSnap != null && probeSnap.Count > 0)
          VelumSolidPulseMetricCompare.RecordSnapshot(probeSnap);
      }
      catch (Exception)
      {
      }
    }

    private static void PrunePublishedSnapshotForEditContext(VelumSolidDocumentEditContext editContext)
    {
      IReadOnlyDictionary<string, float> published = VelumSolidEnvironmentGate.GetPublishedSnapshot();
      if (published == null || published.Count == 0)
        return;

      var values = new Dictionary<string, float>(StringComparer.Ordinal);
      foreach (KeyValuePair<string, float> kv in published)
        values[kv.Key] = kv.Value;

      var influence = new Dictionary<string, VelumSolidProbeInfluenceContext>(StringComparer.Ordinal);
      IReadOnlyDictionary<string, VelumSolidProbeInfluenceContext> publishedInfluence =
          VelumSolidEnvironmentGate.GetPublishedInfluenceContexts();
      if (publishedInfluence != null)
      {
        foreach (KeyValuePair<string, VelumSolidProbeInfluenceContext> kv in publishedInfluence)
          influence[kv.Key] = kv.Value;
      }

      int before = values.Count;
      VelumSolidExportDocumentationProbe.RemoveInapplicableOutdatedFromSnapshot(values, null, influence);
      VelumSolidExportDocumentationProbe.RefreshInapplicableProbesInSnapshot(editContext, values, null);
      if (values.Count == before && !HasExportProbeValuesChanged(published, values))
        return;

      VelumSolidEnvironmentGate.Publish(values, influence);
    }

    private static void ApplyMetricPressureBeforeGomeostasis(
        int pulseNumber,
        GomeostasSystem g,
        VelumSolidDocumentEditContext editContext)
    {
      IReadOnlyDictionary<string, float> probeSnap = VelumSolidEnvironmentGate.GetPublishedSnapshot();
      if (probeSnap == null || probeSnap.Count == 0)
      {
        VelumSolidDiagLog.WritePulseHost(
            $"pulseArg={pulseNumber} pollSW={_pollSolidWhilePulse} warmup={_solidMetricsWarmupPulsesRemaining} snap=0 → skip influence writes");
        return;
      }

      IReadOnlyDictionary<string, VelumSolidProbeInfluenceContext> infl =
          VelumSolidEnvironmentGate.GetPublishedInfluenceContexts();
      if (!VelumSolidMetricPressureOrchestrator.TryBuildHostWritesBeforeGomeostasis(
              pulseNumber,
              g,
              probeSnap,
              infl,
              editContext,
              out VelumSolidMetricPressureHostWrites hostWrites) ||
          hostWrites == null || !hostWrites.HasAnyWrites)
        return;

      if (VelumAppConfig.ObservationMode)
        return;

      if (hostWrites.ReleaseWrites != null && hostWrites.ReleaseWrites.Count > 0)
      {
        VelumSolidDiagLog.WritePulseHost(
            $"pulseArg={pulseNumber} releaseWrites={hostWrites.ReleaseWrites.Count}");
        ApplyMetricPressureHostWrites(g, hostWrites.ReleaseWrites);
      }

      // Гонка: документ закрыли после сборки hostWrites — документные engage не пишем;
      // host-global (реестр) можно продолжать.
      bool noActiveDoc = VelumSolidMetricPressureReset.IsNoActiveSolidDocument();
      if (noActiveDoc && editContext?.ActiveDocument != null)
      {
        VelumSolidMetricPressureReset.OnNoActiveSolidDocument(g);
        VelumProductRegistryIntegrityProbes.PublishScoresToGate(registryOnlySnapshot: true);
        if (!VelumSolidMetricPressureOrchestrator.TryBuildHostWritesBeforeGomeostasis(
                pulseNumber,
                g,
                VelumSolidEnvironmentGate.GetPublishedSnapshot(),
                VelumSolidEnvironmentGate.GetPublishedInfluenceContexts(),
                null,
                out hostWrites) ||
            hostWrites == null || !hostWrites.HasAnyWrites)
          return;
      }

      if (hostWrites.EngageWrites != null && hostWrites.EngageWrites.Count > 0)
      {
        VelumSolidDiagLog.WritePulseHost(
            $"pulseArg={pulseNumber} globalPuls={GlobalTimer.GlobalPulsCount} snap={probeSnap.Count} " +
            $"engageWrites={hostWrites.EngageWrites.Count}");
        ApplyMetricPressureHostWrites(g, hostWrites.EngageWrites);
      }
    }

    /// <summary>
    /// Если gate пуст, но в кэше есть последний снимок - публикуем его как устаревший для непрерывного давления.
    /// </summary>
    private static void EnsurePublishedSnapshotForPressure()
    {
      VelumSolidDocumentEditContext editContext =
          _solidApp != null ? VelumSolidDocumentEditContextResolver.Resolve(_solidApp) : null;
      if (editContext?.ActiveDocument == null)
        return;

      IReadOnlyDictionary<string, float> published = VelumSolidEnvironmentGate.GetPublishedSnapshot();
      if (published != null && published.Count > 0)
        return;

      if (VelumSolidEnvironmentGate.LastSnapshotTimedOut)
        return;

      if (!VelumSolidWorksMetricsCache.TryGetCached(
              out Dictionary<string, float> values,
              out _,
              out Dictionary<string, VelumSolidProbeInfluenceContext> influence))
        return;

      VelumSolidExportDocumentationProbe.RemoveInapplicableOutdatedFromSnapshot(values, null, influence);
      VelumSolidEnvironmentGate.Publish(values, influence);
      VelumSolidEnvironmentGate.MarkSnapshotStale();
      VelumSolidDiagLog.WritePulseHost(
          $"EnsurePublishedSnapshotForPressure: restored from cache keys={values.Count}");
    }

    /// <summary>
    /// Абсолютные цели cumulative engage/release: одна запись без nudge (nudge давал ложный Well на удерживаемом P_i).
    /// </summary>
    private static void ApplyMetricPressureHostWrites(GomeostasSystem g, IReadOnlyDictionary<int, float> snap)
    {
      if (g == null || snap == null || snap.Count == 0)
        return;

      g.HostBatchUpdateParameterValues(snap);
      if (VelumSolidDiagLog.PulseSolidHomeostasis)
      {
        foreach (KeyValuePair<int, float> kv in snap)
          VelumSolidDiagLog.WritePulseHost($"ApplyMetricPressure: param={kv.Key} target={kv.Value:F3}");
      }
    }

    /// <summary>
    /// Обновление значений хоста: для параметров с заметным изменением снимка — одна запись (корректный PreviousValue для Well).
    /// Для неизменных относительно движка — двухшаговый nudge (обход отсутствия обновления PreviousValue при повторной записи того же float).
    /// </summary>
    private static void ApplyHostSnapshotWithPreviousValueSync(GomeostasSystem g, IReadOnlyDictionary<int, float> snap)
    {
      if (g == null || snap == null || snap.Count == 0)
        return;

      Dictionary<int, float> cur = g.HostGetParameterValues(snap.Keys);
      var jump = new Dictionary<int, float>();
      var steady = new Dictionary<int, float>();

      foreach (KeyValuePair<int, float> kv in snap)
      {
        if (!cur.TryGetValue(kv.Key, out float prev))
        {
          jump[kv.Key] = kv.Value;
          continue;
        }

        if (Math.Abs(prev - kv.Value) > HostValueMeaningfulChangeEpsilon)
          jump[kv.Key] = kv.Value;
        else
          steady[kv.Key] = kv.Value;
      }

      if (jump.Count > 0)
        g.HostBatchUpdateParameterValues(jump);

      if (steady.Count == 0)
      {
        if (VelumSolidDiagLog.PulseSolidHomeostasis && jump.Count > 0)
          VelumSolidDiagLog.WritePulseHost($"ApplyHost: only jump batch ids={jump.Count}");
        return;
      }

      var nudged = new Dictionary<int, float>(steady.Count);
      foreach (KeyValuePair<int, float> kv in steady)
        nudged[kv.Key] = NudgeParameterValueForPreviousSync(kv.Value);

      if (VelumSolidDiagLog.PulseSolidHomeostasis)
        VelumSolidDiagLog.WritePulseHost(
            $"ApplyHost: jump={jump.Count} steady={steady.Count} (steady → nudge+restore для PreviousValue)");

      g.HostBatchUpdateParameterValues(nudged);
      g.HostBatchUpdateParameterValues(steady);
    }

    private static float NudgeParameterValueForPreviousSync(float v)
    {
      if (v <= 0.001f)
        return Math.Min(100f, v + HostValueNudgeEpsilon);
      return Math.Max(0f, v - HostValueNudgeEpsilon);
    }

    private static void RefreshSnapshotFromUiThreadCore(int pulseNumber, int generation)
    {
      if (!VelumSolidProbeRefreshPlanner.TryPlanRefreshOnPulse(
              _solidApp,
              out VelumSolidProbeCategory categories,
              out VelumSolidDocumentEditContext editContext,
              out int currentUpdateStamp))
      {
        VelumSolidDiagLog.WritePulseHost(
            $"pulseArg={pulseNumber} probePlan=skip (snapshot unchanged)");
        return;
      }

      if (generation != Volatile.Read(ref _solidProbeRefreshGeneration))
        return;

      IReadOnlyList<string> allProbeKeys = VelumSolidEnvironmentInfluenceComposer.EnumerateDistinctEnvironmentProbeKeys();
      if (allProbeKeys.Count == 0)
      {
        VelumSolidWorksMetricsCache.Clear();
        VelumSolidEnvironmentGate.Clear();
        VelumSolidEnvironmentGate.SetLastProbeOutcome(VelumSolidProbeErrorKind.None, pulseNumber);
        VelumSolidProbeRefreshPlanner.NotifyRefreshCompleted(currentUpdateStamp);
        return;
      }

      List<string> keysToCollect = VelumSolidWorksHomeostasisMetrics.SelectProbeKeysForCategories(
          allProbeKeys,
          categories,
          editContext);
      if (keysToCollect.Count == 0)
      {
        VelumSolidDiagLog.WritePulseHost(
            $"pulseArg={pulseNumber} probePlan=no-keys categories={categories}");
        VelumSolidProbeRefreshPlanner.NotifyRefreshCompleted(currentUpdateStamp);
        return;
      }

      var mergedValues = CopyPublishedSnapshotValues();
      var mergedInfluence = CopyPublishedInfluenceContexts();
      var mergedTips = new Dictionary<string, string>(StringComparer.Ordinal);

      var stopwatch = Stopwatch.StartNew();
      int timeoutMs = VelumAppConfig.SolidProbeTimeoutMs;
      if (timeoutMs < 1)
        timeoutMs = 2500;

      VelumSolidProbeErrorKind collectKind = VelumSolidProbeErrorKind.None;
      VelumSolidProbeContext.Begin(pulseNumber);
      try
      {
        Dictionary<string, float> collected = VelumSolidWorksHomeostasisMetrics.CollectDistinctProbes(
            keysToCollect,
            _solidApp,
            editContext,
            out Dictionary<string, string> tips,
            out Dictionary<string, VelumSolidProbeInfluenceContext> inflFresh,
            out collectKind,
            () => stopwatch.ElapsedMilliseconds >= timeoutMs);

        MergeProbeResults(mergedValues, collected);
        MergeInfluenceResults(mergedInfluence, inflFresh);
        MergeTooltipResults(mergedTips, tips);
        VelumSolidExportDocumentationProbe.RemoveInapplicableOutdatedFromSnapshot(
            mergedValues,
            mergedTips,
            mergedInfluence);
        VelumSolidExportDocumentationProbe.RefreshInapplicableProbesInSnapshot(
            editContext,
            mergedValues,
            mergedTips);
      }
      finally
      {
        VelumSolidProbeContext.Clear();
      }

      bool timedOut = stopwatch.ElapsedMilliseconds >= timeoutMs;
      long elapsedMs = stopwatch.ElapsedMilliseconds;
      if (timedOut && collectKind == VelumSolidProbeErrorKind.None)
        collectKind = VelumSolidProbeErrorKind.TimedOut;

      if (mergedValues.Count > 0)
      {
        if (timedOut)
        {
          VelumSolidEnvironmentGate.SetLastProbeOutcome(collectKind, pulseNumber);
          VelumSolidEnvironmentGate.SetLastProbeMetadata(true, elapsedMs, false);
          VelumSolidProbeRefreshPlanner.NotifyRefreshIncomplete();
          VelumSolidDiagLog.WriteSolidProbeRefresh(pulseNumber, true, elapsedMs, false, timeoutMs);
          VelumSolidDiagLog.WritePulseHost(
              $"pulseArg={pulseNumber} probe=partial-timeout keys={keysToCollect.Count} elapsedMs={elapsedMs} gate=unchanged");
        }
        else
        {
          if (generation != Volatile.Read(ref _solidProbeRefreshGeneration))
          {
            VelumSolidDiagLog.WritePulseHost(
                $"pulseArg={pulseNumber} probe=discard (stale generation after doc change)");
            return;
          }

          VelumSolidWorksMetricsCache.Store(mergedValues, mergedTips, mergedInfluence);
          VelumSolidEnvironmentGate.Publish(mergedValues, mergedInfluence);
          VelumSolidEnvironmentGate.SetLastProbeOutcome(collectKind, pulseNumber);
          VelumSolidEnvironmentGate.SetLastProbeMetadata(false, elapsedMs, false);
          VelumSolidProbeRefreshPlanner.NotifyRefreshCompleted(currentUpdateStamp);
          VelumSolidDiagLog.WritePulseHost(
              $"pulseArg={pulseNumber} probe=ok keys={keysToCollect.Count} categories={categories} elapsedMs={elapsedMs}");
        }
      }
      else
      {
        VelumSolidEnvironmentGate.SetLastProbeOutcome(
            collectKind != VelumSolidProbeErrorKind.None
                ? collectKind
                : VelumSolidProbeErrorKind.ComAccessFailed,
            pulseNumber);
        VelumSolidEnvironmentGate.SetLastProbeMetadata(timedOut, elapsedMs, false);
        if (timedOut)
        {
          VelumSolidProbeRefreshPlanner.NotifyRefreshIncomplete();
          VelumSolidDiagLog.WriteSolidProbeRefresh(pulseNumber, true, elapsedMs, false, timeoutMs);
        }
        VelumSolidDiagLog.WritePulseHost(
            $"pulseArg={pulseNumber} probe=empty merged={mergedValues.Count} (keep previous gate snapshot)");
      }
    }

    private static Dictionary<string, float> CopyPublishedSnapshotValues()
    {
      IReadOnlyDictionary<string, float> published = VelumSolidEnvironmentGate.GetPublishedSnapshot();
      var copy = new Dictionary<string, float>(StringComparer.Ordinal);
      if (published == null || published.Count == 0)
        return copy;
      foreach (KeyValuePair<string, float> kv in published)
        copy[kv.Key] = kv.Value;
      return copy;
    }

    private static Dictionary<string, VelumSolidProbeInfluenceContext> CopyPublishedInfluenceContexts()
    {
      IReadOnlyDictionary<string, VelumSolidProbeInfluenceContext> published =
          VelumSolidEnvironmentGate.GetPublishedInfluenceContexts();
      var copy = new Dictionary<string, VelumSolidProbeInfluenceContext>(StringComparer.Ordinal);
      if (published == null || published.Count == 0)
        return copy;
      foreach (KeyValuePair<string, VelumSolidProbeInfluenceContext> kv in published)
        copy[kv.Key] = kv.Value;
      return copy;
    }

    private static void MergeProbeResults(
        Dictionary<string, float> target,
        IReadOnlyDictionary<string, float> collected)
    {
      if (target == null || collected == null)
        return;
      foreach (KeyValuePair<string, float> kv in collected)
        target[kv.Key] = kv.Value;
    }

    private static void MergeInfluenceResults(
        Dictionary<string, VelumSolidProbeInfluenceContext> target,
        IReadOnlyDictionary<string, VelumSolidProbeInfluenceContext> collected)
    {
      if (target == null || collected == null)
        return;
      foreach (KeyValuePair<string, VelumSolidProbeInfluenceContext> kv in collected)
        target[kv.Key] = kv.Value;
    }

    private static void MergeTooltipResults(
        Dictionary<string, string> target,
        IReadOnlyDictionary<string, string> collected)
    {
      if (target == null || collected == null)
        return;
      foreach (KeyValuePair<string, string> kv in collected)
      {
        if (!string.IsNullOrEmpty(kv.Value))
          target[kv.Key] = kv.Value;
      }
    }

    private static bool HasExportProbeValuesChanged(
        IReadOnlyDictionary<string, float> before,
        IReadOnlyDictionary<string, float> after)
    {
      if (before == null || after == null)
        return false;

      float metricEpsilon = VelumAppConfig.SolidEnvironmentMetricDeltaEpsilon;
      foreach (KeyValuePair<string, float> kv in after)
      {
        if (!VelumSolidExportDocumentationProbe.TryParseProbeKey(kv.Key, out _))
          continue;

        if (!before.TryGetValue(kv.Key, out float previous))
          return true;

        if (Math.Abs(previous - kv.Value) > metricEpsilon)
          return true;
      }

      return false;
    }

    /// <summary>
    /// Запланировать COM-опрос на UI-потоке панели (STA SolidWorks). Не блокирует такт пульса ISIDA.
    /// </summary>
    private static void ScheduleSolidProbeRefresh(int pulseNumber)
    {
      if (_invokeTarget == null || !_invokeTarget.IsHandleCreated)
        return;

      if (Interlocked.CompareExchange(ref _solidProbeRefreshInFlight, 1, 0) != 0)
      {
        VelumSolidDiagLog.WritePulseHost(
            $"pulseArg={pulseNumber} probePlan=skip (refresh in flight)");
        return;
      }

      int generation = Volatile.Read(ref _solidProbeRefreshGeneration);
      try
      {
        _invokeTarget.BeginInvoke(new Action(() => RunSolidProbeRefreshOnUiThread(pulseNumber, generation)));
      }
      catch
      {
        Interlocked.Exchange(ref _solidProbeRefreshInFlight, 0);
      }
    }

    private static void RunSolidProbeRefreshOnUiThread(int pulseNumber, int generation)
    {
      try
      {
        if (generation != Volatile.Read(ref _solidProbeRefreshGeneration))
        {
          VelumSolidDiagLog.WritePulseHost(
              $"pulseArg={pulseNumber} probePlan=skip (stale generation after doc change)");
          return;
        }

        RefreshSnapshotFromUiThreadCore(pulseNumber, generation);

        if (generation != Volatile.Read(ref _solidProbeRefreshGeneration))
          return;

        if (!VelumIsidaHost.IsReady || !GlobalTimer.IsPulsationRunning)
          return;

        VelumSolidDocumentEditContext editContext =
            _solidApp != null ? VelumSolidDocumentEditContextResolver.Resolve(_solidApp) : null;
        GomeostasSystem g = VelumIsidaHost.Context.Gomeostas;
        if (editContext?.ActiveDocument == null)
        {
          if (VelumSolidMetricPressureReset.HasPendingEnvironmentPressureResidue())
            VelumSolidMetricPressureReset.OnNoActiveSolidDocument(g);

          // Чистим document-specific probes из снимка — они от предыдущего документа
          // и не должны влиять на определение фокусной проблемы.
          VelumSolidEnvironmentGate.ClearDocumentSpecificProbes();
          VelumProductRegistryIntegrityProbes.PublishScoresToGate(registryOnlySnapshot: true);
          // Registry-only снимок freshly computed из кэша проблем — он достоверен:
          // не оставляем stale-флаг, иначе подсказка и мотор блокируются навсегда.
          VelumSolidEnvironmentGate.MarkSnapshotFresh();
          ApplyMetricPressureBeforeGomeostasis(pulseNumber, g, editContext);
          return;
        }

        IReadOnlyDictionary<string, float> probeSnap = VelumSolidEnvironmentGate.GetPublishedSnapshot();
        if (probeSnap == null || probeSnap.Count == 0)
          return;

        ApplyMetricPressureBeforeGomeostasis(pulseNumber, g, editContext);
      }
      catch
      {
      }
      finally
      {
        Interlocked.Exchange(ref _solidProbeRefreshInFlight, 0);
      }
    }

    /// <summary>
    /// Старт цикла опроса SW: сброс снимка/баз сравнения и прогрев, чтобы импульс давления среды
    /// снова срабатывал при ухудшении метрики (например несохранённый документ).
    /// Принудительный полный пересбор: добавляет Full ПОСЛЕ сброса, чтобы накопленные вне пульса
    /// изменения были учтены при первом COM-опросе.
    /// </summary>
    private static void BeginSolidPulseProbeCycle()
    {
      VelumSolidMetricPressureOrchestrator.Clear();

      Interlocked.Increment(ref _solidProbeRefreshGeneration);
      VelumSolidWorksMetricsCache.Invalidate();
      VelumSolidEnvironmentGate.Clear();
      VelumSolidPulseMetricCompare.Clear();
      VelumSolidProbeRefreshPlanner.ResetForNewPulseCycle(_solidApp);

      // Full ПОСЛЕ ResetForNewPulseCycle — чтобы не стёрся присваиванием
      VelumSolidProbeRefreshPlanner.MarkStale(VelumSolidProbeCategory.Full);
      _solidMetricsWarmupPulsesRemaining = SolidMetricsWarmupPulseCount;
      SeedEnvironmentProbeMetricBaselines();
    }

    private static void SeedEnvironmentProbeMetricBaselines()
    {
      VelumSolidPulseMetricCompare.SeedOptimalBaselineForProbeKeys(
          VelumSolidEnvironmentInfluenceComposer.EnumerateDistinctEnvironmentProbeKeys());
    }

    /// <summary>
    /// Принудительное обновление метрик вне пульсации (для штампов DXF/PDF).
    /// Вызывается из событий SolidWorks при изменении геометрии.
    /// </summary>
    internal static void RefreshMetricsOffPulse(VelumSolidProbeCategory categories = VelumSolidProbeCategory.ExportDocumentation)
    {
      if (_solidApp == null || !VelumIsidaHost.IsReady)
        return;

      if (_invokeTarget == null || !_invokeTarget.IsHandleCreated)
        return;

      // Если пульсация уже запущена - не мешаем, обновление произойдет на следующем такте
      if (GlobalTimer.IsPulsationRunning)
        return;

      // Проверяем, есть ли устаревшие категории
      if (!VelumSolidProbeRefreshPlanner.HasStaleCategories(categories))
        return;

      var editContext = VelumSolidDocumentEditContextResolver.Resolve(_solidApp);
      if (editContext?.ActiveDocument == null)
        return;

      // Запускаем обновление на UI-потоке
      RunOnTaskPaneUiThread(() =>
      {
        try
        {
          // Обновляем только необходимые категории
          RefreshSnapshotOffPulse(editContext, categories);
        }
        catch (Exception ex)
        {
          Logger.Info("RefreshMetricsOffPulse error: " + ex.Message);
        }
      });
    }

    /// <summary>
    /// Обновление снимка метрик вне пульсации (только для указанных категорий).
    /// </summary>
    private static void RefreshSnapshotOffPulse(VelumSolidDocumentEditContext editContext, VelumSolidProbeCategory categories)
    {
      try
      {
        // Получаем все ключи проб
        IReadOnlyList<string> allProbeKeys = VelumSolidEnvironmentInfluenceComposer.EnumerateDistinctEnvironmentProbeKeys();
        if (allProbeKeys.Count == 0)
          return;

        // Выбираем только ключи для указанных категорий
        List<string> keysToCollect = VelumSolidWorksHomeostasisMetrics.SelectProbeKeysForCategories(
            allProbeKeys,
            categories,
            editContext);

        if (keysToCollect.Count == 0)
          return;

        // Собираем значения проб
        var mergedValues = CopyPublishedSnapshotValues();
        var mergedInfluence = CopyPublishedInfluenceContexts();
        var mergedTips = new Dictionary<string, string>(StringComparer.Ordinal);

        Dictionary<string, float> collected = VelumSolidWorksHomeostasisMetrics.CollectDistinctProbes(
            keysToCollect,
            _solidApp,
            editContext,
            out Dictionary<string, string> tips,
            out Dictionary<string, VelumSolidProbeInfluenceContext> inflFresh,
            out VelumSolidProbeErrorKind collectKind,
            () => false); // без таймаута

        MergeProbeResults(mergedValues, collected);
        MergeInfluenceResults(mergedInfluence, inflFresh);
        MergeTooltipResults(mergedTips, tips);

        // Удаляем неприменимые пробы
        VelumSolidExportDocumentationProbe.RemoveInapplicableOutdatedFromSnapshot(
            mergedValues,
            mergedTips,
            mergedInfluence);

        if (mergedValues.Count > 0)
        {
          VelumSolidWorksMetricsCache.Store(mergedValues, mergedTips, mergedInfluence);
          VelumSolidEnvironmentGate.Publish(mergedValues, mergedInfluence);
          VelumSolidEnvironmentGate.SetLastProbeOutcome(VelumSolidProbeErrorKind.None, 0);
          VelumSolidEnvironmentGate.SetLastProbeMetadata(false, 0, false);

          // Сбрасываем флаги устаревания для обновленных категорий
          VelumSolidProbeRefreshPlanner.ClearStaleCategories(categories);
        }
      }
      catch (Exception ex)
      {
        Logger.Info("RefreshSnapshotOffPulse error: " + ex.Message);
      }
    }

  }
}
