using System;

namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Категории проб метрик среды для частичного пересбора снимка на такте пульса.
  /// </summary>
  [Flags]
  internal enum VelumSolidProbeCategory
  {
    None = 0,
    Document = 1,
    PartMaterial = 4,
    ExportDocumentation = 16,
    /// <summary>Host-global пробы (реестр документов и т.п.) — без привязки к активному документу SW.</summary>
    HostGlobal = 32,
    Full = Document | PartMaterial | ExportDocumentation | HostGlobal
  }
}
