using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Xarial.XCad;
using Xarial.XCad.Documents;
using Xarial.XCad.SolidWorks;

namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Определяет активный документ и деталь под редактированием в сборке.
  /// </summary>
  internal static class VelumSolidDocumentEditContextResolver
  {
    internal static VelumSolidDocumentEditContext Resolve(IXApplication app)
    {
      var ctx = new VelumSolidDocumentEditContext();
      if (app == null || !app.IsAlive)
        return ctx;

      IXDocument ixDoc = null;
      try
      {
        ixDoc = app.Documents.Active;
      }
      catch
      {
        return ctx;
      }

      ctx.ActiveDocument = ixDoc;
      ctx.IsPartDocument = ixDoc is IXPart;
      ctx.IsAssemblyDocument = ixDoc is IXAssembly;
      ctx.IsDrawingDocument = ixDoc is IXDrawing;

      ModelDoc2 activeModel = VelumSolidWorksModelDocHelper.TryGetActiveModelDoc2(app, ixDoc);
      ctx.ActiveModelDoc = activeModel;
      if (activeModel != null)
        ctx.DocumentKey = VelumSolidWorksModelDocHelper.TryGetDispatchDocumentKey(activeModel);

      if (ctx.IsAssemblyDocument && activeModel is AssemblyDoc assy)
        ctx.EditTargetPartModel = TryResolveAssemblyEditTargetPart(assy);

      return ctx;
    }

    private static ModelDoc2 TryResolveAssemblyEditTargetPart(AssemblyDoc assy)
    {
      if (assy == null)
        return null;

      try
      {
        object target = assy.GetEditTarget();
        if (target is ModelDoc2 targetMd &&
            targetMd.GetType() == (int)swDocumentTypes_e.swDocPART)
          return targetMd;
      }
      catch
      {
      }

      return null;
    }
  }
}
