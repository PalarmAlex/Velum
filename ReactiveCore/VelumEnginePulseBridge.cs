using System;
using ISIDA.Common;
using Velum.Isida;
using Velum.SolidHomeostasis;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Подписки Velum на пульс ISIDA: моторные ответы на <see cref="GlobalTimer.OnPulseCompleted"/>.
  /// </summary>
  public static class VelumEnginePulseBridge
  {
    private static readonly object Sync = new object();
    private static bool _hooked;
    private static bool _hasLoggedOverallState;
    private static AppGlobalState.HomeostasisState _lastLoggedOverallState;

    /// <summary>Подписаться на фазы пульса адаптера Velum↔ISIDA.</summary>
    public static void Hook()
    {
      lock (Sync)
      {
        if (_hooked)
          return;

        GlobalTimer.OnPulseCompleted += OnPulseCompleted;
        GlobalTimer.OnPulseAfterGomeostasisBeforePsychic += OnPulseAfterGomeostasisBeforePsychic;
        GlobalTimer.PulsationStateChanged += OnPulsationStateChanged;
        _hooked = true;
      }

      VelumScenarioHost.EnsureWired();
      Velum.UI.ProductRegistry.VelumProductRegistryIntegrityScheduler.EnsureAttached();
    }

    /// <summary>Отписаться и сбросить состояние адаптера.</summary>
    public static void Unhook()
    {
      lock (Sync)
      {
        if (!_hooked)
          return;

        GlobalTimer.OnPulseCompleted -= OnPulseCompleted;
        GlobalTimer.OnPulseAfterGomeostasisBeforePsychic -= OnPulseAfterGomeostasisBeforePsychic;
        GlobalTimer.PulsationStateChanged -= OnPulsationStateChanged;
        _hooked = false;
      }

      RecipeDispatcher.ResetMotorDispatchState();
      RecipeDispatchCooldownTracker.Clear();
      RecipeDispatchEpisodeTracker.Clear();
      VelumSolidMetricPressureOrchestrator.Clear();
      _hasLoggedOverallState = false;
    }

    private static void OnPulseCompleted(int pulseNumber)
    {
      try
      {
        if (!GlobalTimer.IsPulsationRunning)
          return;

        LogOverallStateTransitionIfChanged(pulseNumber);
        RecipeDispatcher.ProcessPulse(pulseNumber);
        VelumScenarioHost.OnPulseCompleted();
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum motor pulse error: " + ex.Message);
      }
    }

    /// <summary>
    /// Фаза пульса «после гомеостаза, до психики»: прогон сценария оператора.
    /// Обработка обязана завершиться до продолжения пульса, поэтому выполняется синхронно
    /// на UI-потоке панели (<see cref="VelumSolidEnvironmentBridge.RunOnTaskPaneUiThread"/> делает <c>Invoke</c>),
    /// где пульт имеет доступ к COM и синглтонам движка.
    /// </summary>
    private static void OnPulseAfterGomeostasisBeforePsychic(int pulseNumber)
    {
      try
      {
        if (!GlobalTimer.IsPulsationRunning)
          return;

        VelumSolidEnvironmentBridge.RunOnTaskPaneUiThread(
            () => VelumScenarioHost.OnPulseAfterGomeostasisBeforePsychic(pulseNumber));
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum scenario pulse error: " + ex.Message);
      }
    }

    private static void LogOverallStateTransitionIfChanged(int pulseNumber)
    {
      AppGlobalState.HomeostasisState current = AppGlobalState.CurrentOverallState;
      if (_hasLoggedOverallState && current == _lastLoggedOverallState)
        return;

      string transition = _hasLoggedOverallState
          ? _lastLoggedOverallState + "→" + current
          : current.ToString();

      VelumSolidDiagLog.WriteError(
          "OverallState pulse=" + pulseNumber.ToString(System.Globalization.CultureInfo.InvariantCulture) +
          " " + transition);
      _hasLoggedOverallState = true;
      _lastLoggedOverallState = current;
    }

    private static void OnPulsationStateChanged()
    {
      try
      {
        VelumSolidEnvironmentBridge.SyncSolidPollingWithPulseState();
        Velum.UI.ProductRegistry.VelumProductRegistryIntegrityScheduler.EnsureAttached();

        if (!GlobalTimer.IsPulsationRunning)
        {
          // Прервать прогон сценария при любой остановке пульса (смерть, ошибка, внешний/UI-стоп).
          VelumScenarioHost.OnPulsationStopped();

          // Зафиксировать UpdateStamp документа при остановке пульсации
          // (покрывает все сценарии: смерть агента, ошибка, внешний стоп, UI-стоп)
          VelumSolidProbeRefreshPlanner.CaptureDocumentStampOnPulseStop();

          VelumCommandIdleFlusher.OnPulseStopped();
          VelumAdapterSleepReset.ResetVelumAdapterState();
          VelumAdapterSleepReset.ResetEngineEpisodeState(requirePulseRunning: false);
          _hasLoggedOverallState = false;
        }
      }
      catch
      {
      }
    }
  }
}
