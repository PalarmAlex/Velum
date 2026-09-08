using System;
using System.Collections.Generic;
using ISIDA.Actions;
using ISIDA.Gomeostas;
using ISIDA.SymbiontEnv.Contract;
using Velum.Configuration;

namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Целевое значение P_i по суммарному шаблонному давлению всех still-bad метрик на параметр:
  /// <c>NormaWell + Σ(effect × scale)</c> — например 4 метрики с |effect|=1 → порог−4, при отпускании одной → порог−3.
  /// </summary>
  internal static class VelumSolidMetricCumulativePressureTarget
  {
    internal const float MaintainEpsilon = 0.02f;

    /// <summary>
    /// Сумма signed вкладов EA всех still-bad probe на один параметр (как в <see cref="EnvironmentMetricPressureComposer"/>).
    /// </summary>
    internal static float SumBadPressureDelta(
        int paramId,
        IReadOnlyList<string> stillBadProbeKeys,
        IReadOnlyDictionary<string, VelumSolidProbeInfluenceContext> probeInfluenceByKey)
    {
      if (stillBadProbeKeys == null || stillBadProbeKeys.Count == 0)
        return 0f;

      float metricEpsilon = VelumAppConfig.SolidEnvironmentMetricDeltaEpsilon;
      float sum = 0f;

      foreach (string probeKey in stillBadProbeKeys)
      {
        string key = (probeKey ?? string.Empty).Trim();
        if (key.Length == 0)
          continue;

        InfluenceActionSystem.GomeostasisInfluenceAction ea =
            VelumSolidEnvironmentInfluenceCatalog.TryGetByProbeKey(key);
        if (ea?.Influences == null)
          continue;
        if (!ea.Influences.TryGetValue(paramId, out int catalogEffect) || catalogEffect == 0)
          continue;
        if (VelumSolidMetricPressurePauseRegistry.IsPressurePaused(paramId, key))
          continue;

        ProbeInfluenceScale scale = ResolveScale(key, probeInfluenceByKey);
        float scaledCatalog = catalogEffect * scale.SlotScale;
        if (scale.IsComposite && Math.Abs(scaledCatalog) < metricEpsilon)
          continue;

        sum += scaledCatalog;
      }

      return ClampTotalDelta(sum);
    }

    /// <summary>
    /// Абсолютная цель давления относительно <see cref="GomeostasSystem.ParameterData.NormaWell"/>.
    /// </summary>
    internal static float ComputeTarget(GomeostasSystem.ParameterData param, float totalPressureDelta)
    {
      if (param == null)
        return 0f;

      float target = param.NormaWell + totalPressureDelta;
      return VelumSolidMetricLatchedBadValue.Clamp(param, target);
    }

    internal static bool ShouldWriteTarget(GomeostasSystem.ParameterData param, float target) =>
        param != null && Math.Abs(param.Value - target) > MaintainEpsilon;

    /// <summary>
    /// Оценка давления по снимку gate для одного P_i: сумма bad-метрик, полнота снимка, all-good.
    /// Если в edit-контексте нет применимых влияющих проб — возвращает «пустое» all-good
    /// (pressureDelta=0), чтобы оркестратор мог отпустить ранее engaged параметр.
    /// </summary>
    internal static bool TryEvaluateParamFromSnapshot(
        int paramId,
        IReadOnlyDictionary<string, float> probeSnapshot,
        IReadOnlyDictionary<string, VelumSolidProbeInfluenceContext> probeInfluenceByKey,
        VelumSolidDocumentEditContext editContext,
        out float pressureDelta,
        out bool allExplicitGood,
        out bool snapshotCompleteForParam,
        out bool hasBadInfluencingProbe)
    {
      pressureDelta = 0f;
      allExplicitGood = false;
      snapshotCompleteForParam = false;
      hasBadInfluencingProbe = false;

      if (paramId <= 0 || probeSnapshot == null || probeSnapshot.Count == 0)
        return false;

      float metricEpsilon = VelumAppConfig.SolidEnvironmentMetricDeltaEpsilon;
      float sum = 0f;
      int influencingCount = 0;
      int presentCount = 0;
      int goodCount = 0;
      int badCount = 0;

      foreach (InfluenceActionSystem.GomeostasisInfluenceAction ea in
               VelumSolidEnvironmentInfluenceCatalog.GetActiveEnvironmentActions())
      {
        string probeKey = (ea?.ProbeKey ?? string.Empty).Trim();
        if (probeKey.Length == 0 || ea?.Influences == null)
          continue;
        if (!ea.Influences.TryGetValue(paramId, out int catalogEffect) || catalogEffect == 0)
          continue;

        if (!VelumSolidExportDocumentationProbe.IsProbeApplicableForPressureEvaluation(
                probeKey, probeSnapshot, editContext))
          continue;
        if (!VelumSolidWorksHomeostasisMetrics.IsProbeAllowedForDocumentContext(probeKey, editContext))
          continue;

        influencingCount++;
        if (!probeSnapshot.TryGetValue(probeKey, out float metric))
          continue;

        presentCount++;
        if (VelumSolidMetricProbeThresholds.IsProbeBad(metric))
        {
          badCount++;
          hasBadInfluencingProbe = true;
          if (VelumSolidMetricPressurePauseRegistry.IsPressurePaused(paramId, probeKey))
            continue;

          ProbeInfluenceScale scale = ResolveScale(probeKey, probeInfluenceByKey);
          float scaledCatalog = catalogEffect * scale.SlotScale;
          if (scale.IsComposite && Math.Abs(scaledCatalog) < metricEpsilon)
            continue;

          sum += scaledCatalog;
        }
        else if (VelumSolidMetricProbeThresholds.IsProbeGood(metric))
        {
          goodCount++;
        }
      }

      // В текущем edit-контексте нет применимых проб (например DXF на сборке без edit-target).
      // Плашка уже не «давит»; для engaged P_i это сигнал к full release, а не к пропуску такта.
      if (influencingCount == 0)
      {
        pressureDelta = 0f;
        hasBadInfluencingProbe = false;
        snapshotCompleteForParam = true;
        allExplicitGood = true;
        return true;
      }

      snapshotCompleteForParam = presentCount == influencingCount;
      allExplicitGood = snapshotCompleteForParam && badCount == 0 && goodCount == influencingCount;
      pressureDelta = ClampTotalDelta(sum);
      return true;
    }

    internal static void CollectAllInfluencedParamIds(HashSet<int> paramIds)
    {
      if (paramIds == null)
        return;

      foreach (InfluenceActionSystem.GomeostasisInfluenceAction ea in
               VelumSolidEnvironmentInfluenceCatalog.GetActiveEnvironmentActions())
      {
        if (ea?.Influences == null)
          continue;

        foreach (KeyValuePair<int, int> inf in ea.Influences)
        {
          if (inf.Value != 0)
            paramIds.Add(inf.Key);
        }
      }
    }

    private static ProbeInfluenceScale ResolveScale(
        string probeKey,
        IReadOnlyDictionary<string, VelumSolidProbeInfluenceContext> probeInfluenceByKey)
    {
      if (probeInfluenceByKey != null &&
          probeInfluenceByKey.TryGetValue(probeKey, out VelumSolidProbeInfluenceContext ctx))
        return new ProbeInfluenceScale(ctx.YesSlots, ctx.TotalSlots);

      return ProbeInfluenceScale.BinaryNeutral;
    }

    private static float ClampTotalDelta(float delta)
    {
      float max = EnvironmentMetricPressureComposer.MaxDeltaPerParameterPerPulse;
      if (delta > max)
        return max;
      if (delta < -max)
        return -max;
      return delta;
    }
  }
}
