using ISIDA.SymbiontEnv.Contract;
using Velum.Configuration;

namespace Velum.SolidHomeostasis
{
  /// <summary>Пороги «плохая / хорошая» probe (§4.6 плана метрик среды).</summary>
  internal static class VelumSolidMetricProbeThresholds
  {
    internal static float GoodThreshold =>
        MetricProbeThresholds.GoodThreshold(VelumAppConfig.SolidEnvironmentMetricDeltaEpsilon);

    internal static bool IsProbeBad(float value) =>
        MetricProbeThresholds.IsProbeBad(value, VelumAppConfig.SolidEnvironmentMetricDeltaEpsilon);

    internal static bool IsProbeGood(float value) =>
        MetricProbeThresholds.IsProbeGood(value, VelumAppConfig.SolidEnvironmentMetricDeltaEpsilon);
  }
}
