using System.Collections.Generic;
using ISIDA.Actions;
using ISIDA.Common;
using ISIDA.Reflexes;
using Velum.ReactiveCore;
using Velum.SolidHomeostasis;

namespace Velum.Isida
{
  /// <summary>
  /// Полный сброс runtime Velum↔ISIDA при «сне» агента (остановка пульсации, смена документа).
  /// </summary>
  internal static class VelumAdapterSleepReset
  {
    /// <summary>
    /// Сброс движка, пока пульсация ещё включена (вызывать перед <see cref="GlobalTimer.Stop"/>).
    /// Снимает защёлку EA с пульта, иначе после повторного старта возможен ложный рефлекс.
    /// </summary>
    internal static void PrepareEngineSleepWhilePulseRunning()
    {
      if (!GlobalTimer.IsPulsationRunning || !VelumIsidaHost.IsReady)
        return;

      ClearOperatorInfluenceLatch();
      ResetEngineEpisodeState(requirePulseRunning: true);
    }

    /// <summary>Command-буфер, motor dispatch, метрики SW, давление среды.</summary>
    internal static void ResetVelumAdapterState(bool clearMetricGateSnapshot = false)
    {
      VelumCommandIdleFlusher.CancelPendingFlush();
      VelumSolidCommandBuffer.Clear();

      if (clearMetricGateSnapshot)
        VelumSolidWorksMetricsCache.Clear();
      else
        VelumSolidWorksMetricsCache.Invalidate();

      VelumSolidProbeRefreshPlanner.ResetForDocumentChange();

      if (clearMetricGateSnapshot)
        VelumSolidEnvironmentGate.Clear();
      else
        VelumSolidEnvironmentGate.MarkSnapshotStale();
      VelumSolidPulseMetricCompare.ReseedOptimalBaselineForProbeKeys(
          VelumSolidEnvironmentInfluenceComposer.EnumerateDistinctEnvironmentProbeKeys());

      RecipeDispatchCooldownTracker.Clear();
      RecipeDispatcher.ResetMotorDispatchState();
      VelumSolidMetricPressureOrchestrator.Clear();
    }

    /// <summary>G_AD, буферы стимулов, триггеры рефлексов.</summary>
    internal static void ResetEngineEpisodeState(bool requirePulseRunning)
    {
      try
      {
        if (!VelumIsidaHost.IsReady)
          return;
        if (requirePulseRunning && !GlobalTimer.IsPulsationRunning)
          return;

        int pulse = GlobalTimer.GlobalPulsCount;

        if (AdaptiveActionsSystem.IsInitialized)
          AdaptiveActionsSystem.Instance.ClearAllActiveState();

        AppGlobalState.ClearStimulusBuffersAfterThemeResolution();

        if (ReflexesActivator.IsInitialized)
          ReflexesActivator.Instance.ResetStates(pulse);
      }
      catch (System.Exception ex)
      {
        Logger.Warning("Velum adapter episode reset: " + ex.Message);
      }
    }

    private static void ClearOperatorInfluenceLatch()
    {
      try
      {
        InfluenceActionSystem influence = VelumIsidaHost.Context?.InfluenceActions;
        if (influence == null)
          return;

        influence.ApplyMultipleInfluenceActions(
            new List<int>(),
            new List<int>(),
            new List<int>());
      }
      catch (System.Exception ex)
      {
        Logger.Warning("Velum adapter influence latch clear: " + ex.Message);
      }
    }
  }
}
