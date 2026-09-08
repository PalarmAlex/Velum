namespace Velum.UI.ProductRegistry
{
  /// <summary>
  /// Иерархия значимости видов проблем: чем меньше ранг, тем важнее.
  /// У одной записи реестра в UI/снимке остаётся только наиболее важный вид
  /// (например, битая ссылка перекрывает «DXF устарел»).
  /// </summary>
  internal static class VelumProductRegistryProblemKindPriority
  {
    /// <summary>Ранг значимости (0 — наивысший приоритет).</summary>
    internal static int SeverityRank(VelumProductRegistryProblemKind kind)
    {
      switch (kind)
      {
        // Без файла модели остальные проверки бессмысленны.
        case VelumProductRegistryProblemKind.BrokenLink:
          return 0;

        // Документ вообще не в реестре.
        case VelumProductRegistryProblemKind.MissingRegistryEntry:
          return 10;

        // Ожидание чертежа в реестре (структурное).
        case VelumProductRegistryProblemKind.MissingDrawing:
          return 20;

        // DXF: сначала блокирующие условия, затем «нет файла», затем устаревание/мусор.
        case VelumProductRegistryProblemKind.DxfFirstExport:
          return 30;
        case VelumProductRegistryProblemKind.DxfCatalogUnavailable:
          return 31;
        case VelumProductRegistryProblemKind.MissingDxfProjection:
          return 32;
        case VelumProductRegistryProblemKind.NeedDxfExport:
          return 33;
        case VelumProductRegistryProblemKind.OutdatedDxf:
          return 34;
        case VelumProductRegistryProblemKind.JunkDxf:
          return 35;

        // PDF: аналогично — нет файла важнее устаревания.
        case VelumProductRegistryProblemKind.NeedPdfExport:
          return 40;
        case VelumProductRegistryProblemKind.OutdatedPdf:
          return 41;
        case VelumProductRegistryProblemKind.JunkPdf:
          return 42;

        // BOM mirror — расхождение состава/свойств.
        case VelumProductRegistryProblemKind.BomDiff:
          return 50;

        default:
          return 1000;
      }
    }

    /// <summary>
    /// &lt; 0, если <paramref name="a"/> важнее <paramref name="b"/>.
    /// </summary>
    internal static int Compare(VelumProductRegistryProblemKind a, VelumProductRegistryProblemKind b)
    {
      int cmp = SeverityRank(a).CompareTo(SeverityRank(b));
      if (cmp != 0)
        return cmp;
      return ((int)a).CompareTo((int)b);
    }
  }
}
