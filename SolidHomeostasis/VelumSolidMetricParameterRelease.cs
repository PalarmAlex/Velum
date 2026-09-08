using System.Linq;
using ISIDA.Gomeostas;
using ISIDA.SymbiontEnv.Contract;

namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Release виталов при улучшении пробы метрики: полный сброс в норму или частичное улучшение.
  /// </summary>
  internal static class VelumSolidMetricParameterRelease
  {
    internal static bool TryComposeFullNormWriteForParam(
        GomeostasSystem g,
        int paramId,
        out float targetValue)
    {
      targetValue = 0f;
      if (g == null)
        return false;

      GomeostasSystem.ParameterData param = FindParameter(g, paramId);
      if (param == null)
        return false;

      return EnvironmentMetricParameterRelease.TryComposeFullNormWriteForParam(
          ToContractState(param),
          out targetValue);
    }

    internal static bool TryComposePartialImprovementWrite(
        GomeostasSystem g,
        int paramId,
        float summedTemplatePressureEffects,
        out float targetValue)
    {
      targetValue = 0f;
      if (g == null)
        return false;

      GomeostasSystem.ParameterData param = FindParameter(g, paramId);
      if (param == null)
        return false;

      return EnvironmentMetricParameterRelease.TryComposePartialImprovementWrite(
          ToContractState(param),
          summedTemplatePressureEffects,
          out targetValue);
    }

    private static GomeostasisParameterState ToContractState(GomeostasSystem.ParameterData param)
    {
      return new GomeostasisParameterState
      {
        Id = param.Id,
        Value = param.Value,
        Speed = param.Speed,
        NormaWell = param.NormaWell,
        CriticalMinValue = param.CriticalMinValue,
        CriticalMaxValue = param.CriticalMaxValue
      };
    }

    private static GomeostasSystem.ParameterData FindParameter(GomeostasSystem g, int paramId)
    {
      return g.GetAllParameters()?.FirstOrDefault(p => p.Id == paramId);
    }
  }
}
