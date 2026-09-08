namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Какие категории проб опрашивать в зависимости от типа документа и режима SW.
  /// </summary>
  internal static class VelumSolidProbeCategoryPolicy
  {
    internal static VelumSolidProbeCategory ResolveCategoriesForContext(VelumSolidDocumentEditContext ctx)
    {
      if (ctx == null || ctx.ActiveDocument == null)
        return VelumSolidProbeCategory.None;

      if (ctx.IsAssemblyDocument && !ctx.HasPartLevelProbeTarget)
        return VelumSolidProbeCategory.Document | VelumSolidProbeCategory.HostGlobal;

      if (ctx.IsDrawingDocument)
        return VelumSolidProbeCategory.Document
            | VelumSolidProbeCategory.ExportDocumentation
            | VelumSolidProbeCategory.HostGlobal;

      var categories = VelumSolidProbeCategory.Document | VelumSolidProbeCategory.HostGlobal;

      if (ctx.HasPartLevelProbeTarget)
      {
        categories |= VelumSolidProbeCategory.PartMaterial |
                        VelumSolidProbeCategory.ExportDocumentation;
      }

      return categories;
    }
  }
}
