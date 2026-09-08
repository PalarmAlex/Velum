using System;
using System.Collections.Generic;
using System.Linq;
using ISIDA.Actions;
using ISIDA.Common;
using ISIDA.Gomeostas;
using Velum.Configuration;
using Velum.UI.ProductRegistry;

namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Оркестратор cumulative bad / release для метрик среды (docs/SOLID_METRIC_PRESSURE.md).
  /// На один P_i суммируется шаблонное давление всех still-bad метрик; цель = NormaWell + Σeffect.
  /// Release в норму — только когда все метрики параметра явно good в полном снимке gate.
  /// </summary>
  internal static class VelumSolidMetricPressureOrchestrator
  {
    private sealed class MetricState
    {
      internal bool WasBad;
    }

    private static readonly Dictionary<string, MetricState> States =
        new Dictionary<string, MetricState>(StringComparer.Ordinal);

    internal static void Clear()
    {
      States.Clear();
      VelumSolidMetricPressurePauseRegistry.Clear();
      VelumSolidMetricPressureEngageRegistry.Clear();
    }

    /// <summary>Cumulative engage и release на такте до гомеостаза.</summary>
    internal static bool TryBuildHostWritesBeforeGomeostasis(
        int pulseNumber,
        GomeostasSystem g,
        IReadOnlyDictionary<string, float> probeSnapshot,
        IReadOnlyDictionary<string, VelumSolidProbeInfluenceContext> probeInfluenceByKey,
        VelumSolidDocumentEditContext editContext,
        out VelumSolidMetricPressureHostWrites hostWrites)

    {

      hostWrites = null;

      if (g == null || probeSnapshot == null || probeSnapshot.Count == 0)

        return false;



      VelumSolidMetricPressurePauseRegistry.SyncWithGomeostasis(g);



      bool observationMode = VelumAppConfig.ObservationMode;

      // COM-снимок SW может быть stale/timeout после модалки реестра; host-global
      // (реестр) не зависит от COM — отпускать их можно без «доверия» к solid-снимку.
      bool trustSolidSnapshotForRelease = !VelumSolidEnvironmentGate.SnapshotUntrustworthy;

      var releaseWrites = new Dictionary<int, float>();

      var engageWrites = new Dictionary<int, float>();

      var releasingProbeKeys = new List<string>();

      var stillBadProbeKeys = new List<string>();



      foreach (InfluenceActionSystem.GomeostasisInfluenceAction ea in VelumSolidEnvironmentInfluenceCatalog.GetActiveEnvironmentActions())

      {

        string probeKey = (ea.ProbeKey ?? string.Empty).Trim();

        if (probeKey.Length == 0)

          continue;

        if (!VelumSolidExportDocumentationProbe.IsProbeApplicableForPressureEvaluation(
                probeKey, probeSnapshot, editContext))
          continue;
        if (!VelumSolidWorksHomeostasisMetrics.IsProbeAllowedForDocumentContext(probeKey, editContext))
          continue;

        if (!probeSnapshot.TryGetValue(probeKey, out float metric))

          continue;



        MetricState state = GetOrCreateState(probeKey);

        bool isBad = VelumSolidMetricProbeThresholds.IsProbeBad(metric);

        bool isGood = VelumSolidMetricProbeThresholds.IsProbeGood(metric);



        if (isGood)

        {

          bool trustRelease = trustSolidSnapshotForRelease
              || VelumProductRegistryIntegrityProbes.IsRegistryProbeKey(probeKey);

          if (state.WasBad && trustRelease &&

              !AnyStillBadProbeSharesParam(probeKey, stillBadProbeKeys))

            releasingProbeKeys.Add(probeKey);

          else if (trustRelease)

            ResetState(state);

          continue;

        }



        if (!isBad)

          continue;



        state.WasBad = true;

        stillBadProbeKeys.Add(probeKey);

      }



      if (!observationMode)

      {

        var paramIdsToUpdate = new HashSet<int>();

        VelumSolidMetricCumulativePressureTarget.CollectAllInfluencedParamIds(paramIdsToUpdate);

        var hostGlobalOnlyParams = new HashSet<int>();
        VelumProductRegistryIntegrityProbes.CollectInfluencedParamIds(hostGlobalOnlyParams);



        foreach (int paramId in paramIdsToUpdate)

        {

          if (!VelumSolidMetricCumulativePressureTarget.TryEvaluateParamFromSnapshot(
                  paramId,
                  probeSnapshot,
                  probeInfluenceByKey,
                  editContext,
                  out float pressureDelta,
                  out bool allExplicitGood,
                  out bool snapshotCompleteForParam,
                  out bool hasBadInfluencingProbe))
            continue;

          if (Math.Abs(pressureDelta) > float.Epsilon)
          {
            TryComposeCumulativeEngageWrite(
                g,
                paramId,
                pressureDelta,
                stillBadProbeKeys,
                engageWrites,
                pulseNumber);
            continue;
          }

          // pressureDelta == 0: либо все применимые пробы good, либо в контексте
          // не осталось влияющих проб (смена деталь→сборка и т.п.) — тогда release.
          bool trustReleaseForParam = trustSolidSnapshotForRelease
              || hostGlobalOnlyParams.Contains(paramId);
          if (hasBadInfluencingProbe ||
              !trustReleaseForParam ||
              !snapshotCompleteForParam ||
              !allExplicitGood)
            continue;

          TryComposeFullReleaseWrite(g, paramId, releaseWrites, pulseNumber);

        }

      }



      if (!observationMode && releasingProbeKeys.Count > 0)

      {

        foreach (string probeKey in releasingProbeKeys)

        {

          InfluenceActionSystem.GomeostasisInfluenceAction ea =

              VelumSolidEnvironmentInfluenceCatalog.TryGetByProbeKey(probeKey);

          if (ea != null)

            AppGlobalState.RecordEnvironmentProbeRelease(ea.Id);

          VelumSolidDiagLog.WritePulseHost(

              $"release probe={probeKey} pulse={pulseNumber} cumulative");

        }

      }



      if (releaseWrites.Count == 0 && engageWrites.Count == 0)

        return false;



      hostWrites = new VelumSolidMetricPressureHostWrites

      {

        ReleaseWrites = releaseWrites.Count > 0 ? releaseWrites : null,

        EngageWrites = engageWrites.Count > 0 ? engageWrites : null

      };

      return true;

    }



    private static void TryComposeCumulativeEngageWrite(

        GomeostasSystem g,

        int paramId,

        float pressureDelta,

        IReadOnlyList<string> stillBadProbeKeys,

        Dictionary<int, float> engageWrites,

        int pulseNumber)

    {

      GomeostasSystem.ParameterData param = FindParameter(g, paramId);

      if (param == null)

        return;



      float target = VelumSolidMetricCumulativePressureTarget.ComputeTarget(param, pressureDelta);

      bool wasEngaged = VelumSolidMetricPressureEngageRegistry.WasEngaged(paramId);
      bool targetChanged = VelumSolidMetricPressureEngageRegistry.HasTargetChanged(paramId, target);
      bool stillInBadZone = VelumSolidMetricLatchedBadValue.IsInBadZone(g, param);

      VelumSolidMetricPressureEngageRegistry.MarkEngaged(paramId, target);

      // Повторная запись «вверх» к цели внутри bad zone ISIDA трактует как isImproving → transient Well.
      if (wasEngaged && !targetChanged && stillInBadZone)
      {
        VelumSolidDiagLog.WriteError(
            $"MetricPressure cumulativeEngage param={paramId} pulse={pulseNumber} " +
            $"hold target={target:F3} cur={param.Value:F3} (skip rewrite)");
        return;
      }

      if (!VelumSolidMetricCumulativePressureTarget.ShouldWriteTarget(param, target))
      {
        VelumSolidDiagLog.WriteError(
            $"MetricPressure cumulativeEngage param={paramId} pulse={pulseNumber} " +
            $"atTarget target={target:F3} cur={param.Value:F3} (skip rewrite)");
        return;
      }

      engageWrites[paramId] = target;

      foreach (string probeKey in stillBadProbeKeys)
      {
        InfluenceActionSystem.GomeostasisInfluenceAction ea =
            VelumSolidEnvironmentInfluenceCatalog.TryGetByProbeKey(probeKey);
        if (ea?.Influences == null)
          continue;
        if (!ea.Influences.TryGetValue(paramId, out int effect) || effect == 0)
          continue;
        if (VelumSolidMetricPressurePauseRegistry.IsPressurePaused(paramId, probeKey))
          continue;

        AppGlobalState.RecordEnvironmentProbePressure(ea.Id);
      }

      VelumSolidDiagLog.WriteError(
          $"MetricPressure cumulativeEngage param={paramId} pulse={pulseNumber} " +
          $"deltaSum={pressureDelta:F3} target={target:F3} cur={param.Value:F3}");

    }



    private static void TryComposeFullReleaseWrite(

        GomeostasSystem g,

        int paramId,

        Dictionary<int, float> releaseWrites,

        int pulseNumber)

    {

      if (!VelumSolidMetricPressureEngageRegistry.WasEngaged(paramId))
        return;

      GomeostasSystem.ParameterData param = FindParameter(g, paramId);
      if (param == null)
        return;

      if (!VelumSolidMetricParameterRelease.TryComposeFullNormWriteForParam(g, paramId, out float normTarget))
        return;

      if (!VelumSolidMetricCumulativePressureTarget.ShouldWriteTarget(param, normTarget))
      {
        VelumSolidMetricPressureEngageRegistry.ClearEngaged(paramId);
        return;
      }

      releaseWrites[paramId] = normTarget;
      VelumSolidMetricPressureEngageRegistry.ClearEngaged(paramId);
      VelumSolidDiagLog.WriteError(
          $"MetricPressure cumulativeRelease param={paramId} pulse={pulseNumber} target={normTarget:F3} cur={param.Value:F3}");

    }



    private static bool AnyStillBadProbeSharesParam(

        string releasingProbeKey,

        IReadOnlyList<string> stillBadProbeKeys)

    {

      InfluenceActionSystem.GomeostasisInfluenceAction releasingEa =

          VelumSolidEnvironmentInfluenceCatalog.TryGetByProbeKey(releasingProbeKey);

      if (releasingEa?.Influences == null)

        return false;



      foreach (string stillBadKey in stillBadProbeKeys)

      {

        if (string.Equals(stillBadKey, releasingProbeKey, StringComparison.Ordinal))

          continue;



        InfluenceActionSystem.GomeostasisInfluenceAction stillBadEa =

            VelumSolidEnvironmentInfluenceCatalog.TryGetByProbeKey(stillBadKey);

        if (stillBadEa?.Influences == null)

          continue;



        foreach (int paramId in releasingEa.Influences.Keys)

        {

          if (releasingEa.Influences[paramId] == 0)

            continue;

          if (stillBadEa.Influences.TryGetValue(paramId, out int effect) && effect != 0)

            return true;

        }

      }



      return false;

    }



    private static MetricState GetOrCreateState(string probeKey)

    {

      if (!States.TryGetValue(probeKey, out MetricState state))

      {

        state = new MetricState();

        States[probeKey] = state;

      }



      return state;

    }



    private static void ResetState(MetricState state)

    {

      state.WasBad = false;

    }



    private static GomeostasSystem.ParameterData FindParameter(GomeostasSystem g, int paramId)

    {

      return g.GetAllParameters()?.FirstOrDefault(p => p.Id == paramId);

    }

  }

}


