using System;
using System.Collections.Generic;

namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Параметры с cumulative engage в текущем эпизоде и их зафиксированная цель.
  /// Release в норму — только для engaged; повторный engage в bad zone не перезаписывается (иначе transient Well).
  /// </summary>
  internal static class VelumSolidMetricPressureEngageRegistry
  {
    private static readonly HashSet<int> EngagedParamIds = new HashSet<int>();
    private static readonly Dictionary<int, float> EngagedTargetByParamId = new Dictionary<int, float>();

    internal static void Clear()
    {
      EngagedParamIds.Clear();
      EngagedTargetByParamId.Clear();
    }

    internal static void MarkEngaged(int paramId, float target)
    {
      if (paramId <= 0)
        return;

      EngagedParamIds.Add(paramId);
      EngagedTargetByParamId[paramId] = target;
    }

    internal static bool WasEngaged(int paramId) => paramId > 0 && EngagedParamIds.Contains(paramId);

    internal static bool TryGetEngagedTarget(int paramId, out float target) =>
        EngagedTargetByParamId.TryGetValue(paramId, out target);

    internal static bool HasTargetChanged(int paramId, float newTarget)
    {
      if (!EngagedTargetByParamId.TryGetValue(paramId, out float previous))
        return true;

      return Math.Abs(previous - newTarget) > VelumSolidMetricCumulativePressureTarget.MaintainEpsilon;
    }

    internal static void ClearEngaged(int paramId)
    {
      if (paramId <= 0)
        return;

      EngagedParamIds.Remove(paramId);
      EngagedTargetByParamId.Remove(paramId);
    }

    internal static IReadOnlyList<int> EnumerateEngagedParamIds()
    {
      if (EngagedParamIds.Count == 0)
        return Array.Empty<int>();

      var copy = new int[EngagedParamIds.Count];
      EngagedParamIds.CopyTo(copy);
      return copy;
    }
  }
}
