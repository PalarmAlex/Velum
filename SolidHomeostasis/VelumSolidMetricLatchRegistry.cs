using System.Collections.Generic;
using ISIDA.Gomeostas;

namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Зафиксированные целевые значения P_i на эпизод давления метрик (latched bad).
  /// </summary>
  internal static class VelumSolidMetricLatchRegistry
  {
    private static readonly Dictionary<int, float> LatchedByParamId = new Dictionary<int, float>();

    internal static void Clear()
    {
      LatchedByParamId.Clear();
    }

    internal static void ClearParam(int paramId)
    {
      LatchedByParamId.Remove(paramId);
    }

    internal static bool TryGetLatch(int paramId, out float latchedValue)
    {
      return LatchedByParamId.TryGetValue(paramId, out latchedValue);
    }

    internal static float EnsureLatch(GomeostasSystem g, int paramId)
    {
      if (LatchedByParamId.TryGetValue(paramId, out float existing))
        return existing;

      GomeostasSystem.ParameterData param = FindParameter(g, paramId);
      if (param == null)
        return 0f;

      float latch = VelumSolidMetricLatchedBadValue.Compute(g, param);
      LatchedByParamId[paramId] = latch;
      return latch;
    }

    private static GomeostasSystem.ParameterData FindParameter(GomeostasSystem g, int paramId)
    {
      foreach (GomeostasSystem.ParameterData p in g.GetAllParameters())
      {
        if (p != null && p.Id == paramId)
          return p;
      }

      return null;
    }
  }
}
