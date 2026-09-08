using System;
using System.Collections.Generic;
using System.Linq;
using ISIDA.Actions;
using ISIDA.Gomeostas;
using ISIDA.SymbiontEnv.Contract;
using Velum.Configuration;

namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Связывает снимок проб метрик SolidWorks с EA среды (<c>ProbeKey</c> в InfluenceActions.dat):
  /// давление пока probe плохая (инкремент каждый пульс + composite scale), release и release-pause — в оркестраторе.
  /// См. <c>docs/SOLID_METRIC_PRESSURE.md</c>.
  /// </summary>
  public static class VelumSolidEnvironmentInfluenceComposer
  {
    /// <summary>Уникальные ProbeKey из EA среды.</summary>
    public static IReadOnlyList<string> EnumerateDistinctEnvironmentProbeKeys() =>
        VelumSolidEnvironmentInfluenceCatalog.EnumerateDistinctEnvironmentProbeKeys();

    /// <summary>
    /// Строит словарь значений параметров для фазы A (давление) по выбранным probe keys.
    /// </summary>
    public static bool TryComposePressureWritesForProbes(
        GomeostasSystem g,
        IReadOnlyDictionary<string, float> probeSnapshot,
        IReadOnlyDictionary<string, VelumSolidProbeInfluenceContext> probeInfluenceByKey,
        IEnumerable<string> probeKeysForPressure,
        out Dictionary<int, float> parameterValuesToWrite)
    {
      return TryComposePressureWritesForProbes(
          g,
          probeSnapshot,
          probeInfluenceByKey,
          probeKeysForPressure,
          null,
          out parameterValuesToWrite);
    }

    /// <summary>
    /// Строит словарь значений параметров для фазы A (давление) по выбранным probe keys
    /// с опциональной паузой импульсов на пару (paramId, probeKey).
    /// </summary>
    public static bool TryComposePressureWritesForProbes(
        GomeostasSystem g,
        IReadOnlyDictionary<string, float> probeSnapshot,
        IReadOnlyDictionary<string, VelumSolidProbeInfluenceContext> probeInfluenceByKey,
        IEnumerable<string> probeKeysForPressure,
        Func<int, string, bool> isPressureSuppressed,
        out Dictionary<int, float> parameterValuesToWrite)
    {
      return TryComposePressureWritesForProbes(
          g,
          probeSnapshot,
          probeInfluenceByKey,
          probeKeysForPressure,
          isPressureSuppressed,
          ids => g.HostGetParameterValues(ids),
          out parameterValuesToWrite);
    }

    /// <summary>
    /// Строит pressure-write с произвольным источником текущих значений P_i
    /// (например после overlay release-целей на этом такте).
    /// </summary>
    public static bool TryComposePressureWritesForProbes(
        GomeostasSystem g,
        IReadOnlyDictionary<string, float> probeSnapshot,
        IReadOnlyDictionary<string, VelumSolidProbeInfluenceContext> probeInfluenceByKey,
        IEnumerable<string> probeKeysForPressure,
        Func<int, string, bool> isPressureSuppressed,
        Func<IEnumerable<int>, IReadOnlyDictionary<int, float>> getCurrentParameterValues,
        out Dictionary<int, float> parameterValuesToWrite)
    {
      parameterValuesToWrite = null;
      if (g == null || probeSnapshot == null || probeKeysForPressure == null || getCurrentParameterValues == null)
        return false;

      IReadOnlyList<EnvironmentProbePressureRule> rules = BuildRulesFromCatalog();
      IReadOnlyDictionary<string, ProbeInfluenceScale> scales = MapInfluenceScales(probeInfluenceByKey);
      float epsilon = VelumAppConfig.SolidEnvironmentMetricDeltaEpsilon;

      return EnvironmentMetricPressureComposer.TryComposePressureWrites(
          probeSnapshot,
          rules,
          scales,
          probeKeysForPressure,
          epsilon,
          getCurrentParameterValues,
          out parameterValuesToWrite,
          isPressureSuppressed);
    }

    /// <summary>
    /// Legacy-обёртка: все плохие пробы на такте (используется только если оркестратор не фильтрует ключи).
    /// </summary>
    public static bool TryComposePulseParameterWrites(
        GomeostasSystem g,
        IReadOnlyDictionary<string, float> probeSnapshot,
        IReadOnlyDictionary<string, VelumSolidProbeInfluenceContext> probeInfluenceByKey,
        out Dictionary<int, float> parameterValuesToWrite)
    {
      if (probeSnapshot == null || probeSnapshot.Count == 0)
      {
        parameterValuesToWrite = null;
        return false;
      }

      IReadOnlyList<EnvironmentProbePressureRule> rules = BuildRulesFromCatalog();
      IReadOnlyDictionary<string, ProbeInfluenceScale> scales = MapInfluenceScales(probeInfluenceByKey);
      float epsilon = VelumAppConfig.SolidEnvironmentMetricDeltaEpsilon;

      return EnvironmentMetricPressureComposer.TryComposePulseParameterWrites(
          probeSnapshot,
          rules,
          scales,
          epsilon,
          ids => g.HostGetParameterValues(ids),
          out parameterValuesToWrite);
    }

    private static IReadOnlyList<EnvironmentProbePressureRule> BuildRulesFromCatalog()
    {
      return VelumSolidEnvironmentInfluenceCatalog.GetActiveEnvironmentActions()
          .Select(ea => new EnvironmentProbePressureRule
          {
            ProbeKey = (ea.ProbeKey ?? string.Empty).Trim(),
            Influences = ea.Influences ?? new Dictionary<int, int>()
          })
          .Where(r => r.ProbeKey.Length > 0)
          .ToList();
    }

    private static IReadOnlyDictionary<string, ProbeInfluenceScale> MapInfluenceScales(
        IReadOnlyDictionary<string, VelumSolidProbeInfluenceContext> probeInfluenceByKey)
    {
      if (probeInfluenceByKey == null || probeInfluenceByKey.Count == 0)
        return null;

      var mapped = new Dictionary<string, ProbeInfluenceScale>(StringComparer.Ordinal);
      foreach (KeyValuePair<string, VelumSolidProbeInfluenceContext> kv in probeInfluenceByKey)
      {
        VelumSolidProbeInfluenceContext ctx = kv.Value;
        mapped[kv.Key] = new ProbeInfluenceScale(ctx.YesSlots, ctx.TotalSlots);
      }

      return mapped;
    }
  }
}
