using System.Collections.Generic;

namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Записи в host от оркестратора давления метрик: release и cumulative engage.
  /// </summary>
  internal sealed class VelumSolidMetricPressureHostWrites
  {
    internal Dictionary<int, float> ReleaseWrites { get; set; }

    /// <summary>
    /// Переход P_i в целевую bad zone: NormaWell + Σ шаблонных effect still-bad метрик.
    /// </summary>
    internal Dictionary<int, float> EngageWrites { get; set; }

    internal bool HasAnyWrites =>
        (ReleaseWrites != null && ReleaseWrites.Count > 0) ||
        (EngageWrites != null && EngageWrites.Count > 0);
  }
}
