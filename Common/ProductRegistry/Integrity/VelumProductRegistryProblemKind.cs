namespace Velum.UI.ProductRegistry
{
  /// <summary>Вид проблемы целостности реестра изделий.</summary>
  internal enum VelumProductRegistryProblemKind
  {
    BrokenLink = 1,
    MissingDrawing = 2,
    /// <summary>Открытый документ SW отсутствует в реестре (нет записи с тем же путём).</summary>
    MissingRegistryEntry = 3,

    // DXF fleet (зеркало + FS, только при закрытых документах)
    NeedDxfExport = 10,
    OutdatedDxf = 11,
    MissingDxfProjection = 12,
    DxfCatalogUnavailable = 13,
    DxfFirstExport = 14,
    JunkDxf = 16,

    // PDF fleet
    NeedPdfExport = 20,
    OutdatedPdf = 21,
    JunkPdf = 22,

    // BOM mirror (состав и tracked properties)
    BomDiff = 30
  }
}
