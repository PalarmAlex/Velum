using System;
using System.Collections.Generic;
using System.Linq;
using ISIDA.Gomeostas;

namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Временная пауза давления метрик на конкретный параметр P_i
  /// на период удержания временного состояния «Хорошо» в движке (multi-metric release).
  /// </summary>
  internal static class VelumSolidMetricPressurePauseRegistry
  {
    private static readonly Dictionary<int, HashSet<string>> PausedProbeKeysByParam =
        new Dictionary<int, HashSet<string>>();

    internal static void Clear()
    {
      PausedProbeKeysByParam.Clear();
    }

    internal static void PausePressure(int paramId, string probeKey)
    {
      string key = (probeKey ?? string.Empty).Trim();
      if (key.Length == 0)
        return;

      if (!PausedProbeKeysByParam.TryGetValue(paramId, out HashSet<string> set))
      {
        set = new HashSet<string>(StringComparer.Ordinal);
        PausedProbeKeysByParam[paramId] = set;
      }

      set.Add(key);
    }

    internal static bool IsPressurePaused(int paramId, string probeKey)
    {
      string key = (probeKey ?? string.Empty).Trim();
      if (key.Length == 0)
        return false;

      return PausedProbeKeysByParam.TryGetValue(paramId, out HashSet<string> set) && set.Contains(key);
    }

    internal static bool HasAnyPauseOnParam(int paramId) =>
        PausedProbeKeysByParam.TryGetValue(paramId, out HashSet<string> set) && set.Count > 0;

    /// <summary>Снять паузы для probe (ручное отключение метрики оператором).</summary>
    internal static void ClearPausesForProbeKey(string probeKey)
    {
      string key = (probeKey ?? string.Empty).Trim();
      if (key.Length == 0)
        return;

      foreach (HashSet<string> set in PausedProbeKeysByParam.Values)
        set.Remove(key);

      List<int> empty = PausedProbeKeysByParam
          .Where(kv => kv.Value.Count == 0)
          .Select(kv => kv.Key)
          .ToList();
      foreach (int paramId in empty)
        PausedProbeKeysByParam.Remove(paramId);
    }

    /// <summary>
    /// Снимает паузу с параметров, у которых истекло временное удержание Well
    /// (<see cref="GomeostasSystem.ParameterData.LastState"/> ≠ Well).
    /// </summary>
    internal static void SyncWithGomeostasis(GomeostasSystem g)
    {
      if (g == null || PausedProbeKeysByParam.Count == 0)
        return;

      var toRemove = new List<int>();
      foreach (KeyValuePair<int, HashSet<string>> kv in PausedProbeKeysByParam)
      {
        GomeostasSystem.ParameterData param = g.GetAllParameters()?.FirstOrDefault(p => p.Id == kv.Key);
        if (param == null || param.LastState != GomeostasSystem.ParameterState.Well)
          toRemove.Add(kv.Key);
      }

      foreach (int paramId in toRemove)
        PausedProbeKeysByParam.Remove(paramId);
    }
  }
}
