using System;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Xarial.XCad;
using Xarial.XCad.Documents;
using Xarial.XCad.SolidWorks;

namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Определяет активный документ и деталь под редактированием в сборке.
  /// Резолв содержит дорогой COM (<c>IActiveDoc2</c>, <c>AssemblyDoc.GetEditTarget</c>) и вызывается
  /// многократно за такт пульса (мост среды, обход плашек мозаики панели, оркестратор давления),
  /// поэтому результат кэшируется. Обновление кэша — только на UI-потоке SolidWorks
  /// (<see cref="ResolveFresh"/>), сброс — при смене документа и сессии (<see cref="Invalidate"/>).
  /// </summary>
  internal static class VelumSolidDocumentEditContextResolver
  {
    private static readonly object Sync = new object();
    private static readonly VelumSolidDocumentEditContext EmptyContext = new VelumSolidDocumentEditContext();

    private static IXApplication _cachedApp;
    private static VelumSolidDocumentEditContext _cachedContext;

    /// <summary>
    /// Сбросить кэш контекста: смена активного документа, старт/остановка пульсации, смена сессии SW.
    /// </summary>
    internal static void Invalidate()
    {
      lock (Sync)
      {
        _cachedApp = null;
        _cachedContext = null;
      }
    }

    /// <summary>
    /// Контекст активного документа. Основной путь — из кэша (без COM), поэтому вызов безопасен
    /// с потока пульсации ISIDA: обращения к COM SolidWorks остаются на UI-потоке
    /// (<see cref="ResolveFresh"/>, вызывается на такте планировщиком проб).
    /// </summary>
    internal static VelumSolidDocumentEditContext Resolve(IXApplication app)
    {
      if (app == null)
        return EmptyContext;

      lock (Sync)
      {
        if (_cachedContext != null && ReferenceEquals(_cachedApp, app))
          return _cachedContext;
      }

      return ResolveFresh(app);
    }

    /// <summary>
    /// Пересобрать контекст через COM и обновить кэш. Вызывать с UI-потока SolidWorks —
    /// это точка обновления кэша на такте пульса.
    /// </summary>
    internal static VelumSolidDocumentEditContext ResolveFresh(IXApplication app)
    {
      VelumSolidDocumentEditContext ctx = Build(app);
      lock (Sync)
      {
        _cachedApp = app;
        _cachedContext = ctx;
      }

      return ctx;
    }

    private static VelumSolidDocumentEditContext Build(IXApplication app)
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
      {
        ctx.EditTargetPartModel = TryResolveAssemblyEditTargetPart(assy);
        if (ctx.EditTargetPartModel != null)
          ctx.EditTargetDocumentKey = VelumSolidWorksModelDocHelper.TryGetDispatchDocumentKey(ctx.EditTargetPartModel);
      }

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