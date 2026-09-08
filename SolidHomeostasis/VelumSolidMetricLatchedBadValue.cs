using System;
using ISIDA.Gomeostas;

namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Целевое значение P_i в bad zone для удержания давления метрики (без инкремента каждый пульс).
  /// </summary>
  internal static class VelumSolidMetricLatchedBadValue
  {
    private const float MinZoneMargin = 5f;
    private const float MarginEpsilon = 0.01f;

    internal static float Compute(GomeostasSystem g, GomeostasSystem.ParameterData param)
    {
      if (param == null)
        return 0f;

      float difSensorPar = g?.DifSensorPar ?? 0.02f;
      float margin = Math.Max(MinZoneMargin, difSensorPar + MarginEpsilon);
      float target;

      if (param.Speed < 0f)
        target = param.NormaWell - margin;
      else if (param.Speed > 0f)
        target = param.NormaWell + margin;
      else
        target = param.Value >= param.NormaWell
            ? param.NormaWell + margin
            : param.NormaWell - margin;

      return Clamp(param, target);
    }

    /// <summary>
    /// Ограничивает partial release, пока на param ещё давят bad-метрики (не выходить из bad zone).
    /// </summary>
    internal static float ClampPartialReleaseTarget(
        GomeostasSystem.ParameterData param,
        float partialTarget,
        float difSensorPar)
    {
      if (param == null)
        return partialTarget;

      float margin = Math.Max(difSensorPar + MarginEpsilon, 1f);

      if (param.Speed < 0f)
      {
        float maxWhileBad = param.NormaWell - margin;
        if (partialTarget > maxWhileBad)
          partialTarget = maxWhileBad;
      }
      else if (param.Speed > 0f)
      {
        float minWhileBad = param.NormaWell + margin;
        if (partialTarget < minWhileBad)
          partialTarget = minWhileBad;
      }

      return Clamp(param, partialTarget);
    }

    internal static bool IsInBadZone(GomeostasSystem g, GomeostasSystem.ParameterData param)
    {
      if (g == null || param == null)
        return false;
      return g.Calculator.IsParameterInBadZone(param);
    }

    internal static float Clamp(GomeostasSystem.ParameterData param, float value)
    {
      if (param == null)
        return value;

      if (value < param.CriticalMinValue)
        return param.CriticalMinValue;
      if (value > param.CriticalMaxValue)
        return param.CriticalMaxValue;
      if (value < 0f)
        return 0f;
      if (value > 100f)
        return 100f;
      return value;
    }
  }
}
