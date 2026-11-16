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
        GlobalTimer.PulsationStateChanged += OnPulsationStateChanged;
        _hooked = true;
      }

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
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum motor pulse error: " + ex.Message);
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
