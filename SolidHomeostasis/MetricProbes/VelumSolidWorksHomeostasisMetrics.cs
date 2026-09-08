using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Xarial.XCad;
using Xarial.XCad.Documents;
using Xarial.XCad.Exceptions;
using Xarial.XCad.SolidWorks;
using Velum.ReactiveCore;
using Velum.UI.ProductRegistry;

namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Опрос SolidWorks: встроенные пробы метрик среды (шкала 0…100) по ключам из
  /// <c>metric-probes.json</c> / <c>ProbeKey</c> в <c>InfluenceActions.dat</c>.
  /// Величины давления на виталы — колонка «Воздействие» EA среды (ID ≥ 50).
  /// </summary>
  public static class VelumSolidWorksHomeostasisMetrics
  {
    internal const float PartNoMaterialScore = 50f;
    internal const float MaterialCompleteScore = 100f;

    internal const string ProbeKeyMaterial = "Velum.Solid.Material";
    internal const string ProbeKeyDocumentUnsavedNew = "Velum.Solid.Document.UnsavedNew";

    /// <summary>Категория встроенной пробы Velum.</summary>
    internal static VelumSolidProbeCategory GetProbeCategory(string probeKey)
    {
      string k = (probeKey ?? string.Empty).Trim();
      if (string.Equals(k, ProbeKeyDocumentUnsavedNew, StringComparison.Ordinal))
        return VelumSolidProbeCategory.Document;
      if (string.Equals(k, ProbeKeyMaterial, StringComparison.Ordinal))
        return VelumSolidProbeCategory.PartMaterial;
      if (VelumSolidExportDocumentationProbe.TryParseProbeKey(k, out _))
        return VelumSolidProbeCategory.ExportDocumentation;
      if (VelumBlankSizeProperties.IsBlankSizeProbeKey(k))
        return VelumSolidProbeCategory.ExportDocumentation;
      if (VelumProductRegistryIntegrityProbes.IsRegistryProbeKey(k))
        return VelumSolidProbeCategory.HostGlobal;
      return VelumSolidProbeCategory.None;
    }

    /// <summary>
    /// Выбирает ключи проб для пересбора по категориям и контексту документа.
    /// На сборке без edit-target — только document (без обхода деталей).
    /// </summary>
    internal static List<string> SelectProbeKeysForCategories(
        IEnumerable<string> allProbeKeys,
        VelumSolidProbeCategory categories,
        VelumSolidDocumentEditContext editContext)
    {
      var selected = new List<string>();
      if (allProbeKeys == null || categories == VelumSolidProbeCategory.None)
        return selected;

      foreach (string raw in allProbeKeys)
      {
        string key = (raw ?? string.Empty).Trim();
        if (key.Length == 0 || !IsBuiltInVelumSolidProbeKey(key))
          continue;

        VelumSolidProbeCategory cat = GetProbeCategory(key);
        if (cat == VelumSolidProbeCategory.None)
          continue;
        if ((categories & cat) == 0 && categories != VelumSolidProbeCategory.Full)
          continue;
        if (!IsProbeAllowedInContext(key, cat, editContext))
          continue;

        selected.Add(key);
      }

      return OrderProbeKeysForCollection(selected);
    }

    /// <summary>Лёгкие пробы раньше тяжёлых.</summary>
    internal static List<string> OrderProbeKeysForCollection(IEnumerable<string> probeKeys)
    {
      var list = new List<string>();
      if (probeKeys == null)
        return list;

      foreach (string k in probeKeys)
      {
        string key = (k ?? string.Empty).Trim();
        if (key.Length > 0)
          list.Add(key);
      }

      list.Sort(CompareProbeCollectionOrder);
      return list;
    }

    private static int CompareProbeCollectionOrder(string a, string b)
    {
      int pa = ProbeCollectionPriority(a);
      int pb = ProbeCollectionPriority(b);
      int cmp = pa.CompareTo(pb);
      return cmp != 0 ? cmp : string.Compare(a, b, StringComparison.Ordinal);
    }

    private static int ProbeCollectionPriority(string key)
    {
      VelumSolidProbeCategory cat = GetProbeCategory(key);
      switch (cat)
      {
        case VelumSolidProbeCategory.HostGlobal:
          return 0;
        case VelumSolidProbeCategory.Document:
          return 1;
        case VelumSolidProbeCategory.PartMaterial:
          return 2;
        case VelumSolidProbeCategory.ExportDocumentation:
          return 3;
        default:
          return 3;
      }
    }

    /// <summary>
    /// Учитывать ли probe при давлении/release в текущем контексте SW (чертёж → только PDF, деталь → DXF).
    /// </summary>
    internal static bool IsProbeAllowedForDocumentContext(
        string probeKey,
        VelumSolidDocumentEditContext editContext)
    {
      string key = (probeKey ?? string.Empty).Trim();
      if (key.Length == 0)
        return false;

      VelumSolidProbeCategory category = GetProbeCategory(key);
      if (category == VelumSolidProbeCategory.None)
        return true;

      return IsProbeAllowedInContext(key, category, editContext);
    }

    private static bool IsProbeAllowedInContext(
        string key,
        VelumSolidProbeCategory category,
        VelumSolidDocumentEditContext editContext)
    {
      if (category == VelumSolidProbeCategory.HostGlobal)
        return true;

      // Без активного документа SW допустимы только host-global пробы (реестр и т.п.).
      if (editContext == null || editContext.ActiveDocument == null)
        return false;

      if (editContext.IsDrawingDocument)
      {
        if (category == VelumSolidProbeCategory.PartMaterial)
          return false;

        if (category == VelumSolidProbeCategory.ExportDocumentation)
          return VelumSolidExportDocumentationProbe.IsPdfProbeKey(key);

        return true;
      }

      if (category == VelumSolidProbeCategory.PartMaterial)
        return editContext.HasPartLevelProbeTarget;

      if (category == VelumSolidProbeCategory.ExportDocumentation)
      {
        // Путь чертежа — и на детали/сборке (свойство источника), и при edit-target.
        if (VelumSolidExportDocumentationProbe.IsDrawingPathProbeKey(key))
        {
          return editContext.IsPartDocument ||
                 editContext.IsAssemblyDocument ||
                 editContext.HasPartLevelProbeTarget;
        }

        if (!editContext.HasPartLevelProbeTarget)
          return false;

        return VelumSolidExportDocumentationProbe.IsDxfProbeKey(key)
            || VelumBlankSizeProperties.IsBlankSizeProbeKey(key);
      }

      return true;
    }

    /// <summary>
    /// Собирает значения для указанных ключей проб (только встроенные ключи Velum).
    /// При недоступном SW или ошибке COM не подставляет нейтральные 100 — возвращает пустой или частичный снимок.
    /// Вызывать с потока UI, где разрешён COM SolidWorks.
    /// </summary>
    /// <param name="probeKeys">Ключи проб из воздействий среды (непустые строки).</param>
    /// <param name="app">Сессия SolidWorks (XCad).</param>
    /// <param name="tooltipAppendices">Дополнения к подсказкам по ключу пробы (если есть).</param>
    /// <param name="probeInfluenceByKey">Для каждого собранного ключа: число «да» и число слотов для масштаба воздействия (см. <see cref="VelumSolidProbeInfluenceContext"/>).</param>
    /// <param name="errorKind">Исход опроса для SessionHealth и метаданных gate.</param>
    /// <returns>Словарь «ключ пробы → значение 0…100».</returns>
    public static Dictionary<string, float> CollectDistinctProbes(
        IEnumerable<string> probeKeys,
        IXApplication app,
        out Dictionary<string, string> tooltipAppendices,
        out Dictionary<string, VelumSolidProbeInfluenceContext> probeInfluenceByKey,
        out VelumSolidProbeErrorKind errorKind)
    {
      return CollectDistinctProbes(
          probeKeys,
          app,
          null,
          out tooltipAppendices,
          out probeInfluenceByKey,
          out errorKind,
          null);
    }

    /// <summary>
    /// Собирает значения для указанных ключей проб с учётом контекста редактирования.
    /// </summary>
    internal static Dictionary<string, float> CollectDistinctProbes(
        IEnumerable<string> probeKeys,
        IXApplication app,
        VelumSolidDocumentEditContext editContext,
        out Dictionary<string, string> tooltipAppendices,
        out Dictionary<string, VelumSolidProbeInfluenceContext> probeInfluenceByKey,
        out VelumSolidProbeErrorKind errorKind,
        Func<bool> shouldAbort)
    {
      probeInfluenceByKey = new Dictionary<string, VelumSolidProbeInfluenceContext>(StringComparer.Ordinal);
      tooltipAppendices = new Dictionary<string, string>(StringComparer.Ordinal);
      errorKind = VelumSolidProbeErrorKind.None;
      List<string> keys = (probeKeys ?? Enumerable.Empty<string>())
          .Select(k => (k ?? string.Empty).Trim())
          .Where(k => k.Length > 0)
          .Distinct(StringComparer.Ordinal)
          .ToList();
      var r = new Dictionary<string, float>(StringComparer.Ordinal);
      if (keys.Count == 0)
        return r;

      if (editContext == null)
        editContext = VelumSolidDocumentEditContextResolver.Resolve(app);

      int builtInKeyCount = 0;
      foreach (string k in keys)
      {
        if (IsBuiltInVelumSolidProbeKey(k))
          builtInKeyCount++;
      }

      if (app == null || !app.IsAlive)
      {
        errorKind = VelumSolidProbeErrorKind.SessionUnavailable;
        return r;
      }

      IXDocument ixDoc = null;
      try
      {
        ixDoc = app.Documents.Active;
      }
      catch (Exception ex)
      {
        VelumSolidDiagLog.WriteError("CollectDistinctProbes Active: " + VelumSolidDiagLog.FormatEx(ex));
        errorKind = VelumSolidProbeErrorKind.ComAccessFailed;
        return r;
      }

      if (ixDoc is IXAssembly)
      {
        VelumSolidDiagLog.WriteVerbose(
            "CollectDistinctProbes: active doc=Assembly title=" + SafeDocTitle(ixDoc));
      }

      int probeFailures = 0;

      foreach (string k in keys)
      {
        if (shouldAbort != null && shouldAbort())
          break;

        if (!IsBuiltInVelumSolidProbeKey(k))
          continue;
        VelumSolidProbeContext.ProbeKey = k;
        try
        {
          if (!TrySampleSingleProbe(
                  k,
                  app,
                  editContext,
                  out float v,
                  out string tip))
            continue;
          r[k] = v;
          if (!string.IsNullOrEmpty(tip))
            tooltipAppendices[k] = tip;
          if (TryGetBuiltInProbeInfluenceContext(k, app, editContext, out VelumSolidProbeInfluenceContext infl))
            probeInfluenceByKey[k] = infl;
          else
            probeInfluenceByKey[k] = VelumSolidProbeInfluenceContext.BinaryNeutral;
        }
        catch (Exception ex)
        {
          probeFailures++;
          VelumSolidDiagLog.WriteError("CollectDistinctProbes key=" + k + ": " + VelumSolidDiagLog.FormatEx(ex));
        }
      }

      if (builtInKeyCount > 0 && r.Count == 0)
        errorKind = VelumSolidProbeErrorKind.ComAccessFailed;
      else if (probeFailures > 0)
        errorKind = VelumSolidProbeErrorKind.PartialProbeFailure;

      return r;
    }

    private static bool TryGetBuiltInProbeInfluenceContext(
        string key,
        IXApplication app,
        VelumSolidDocumentEditContext editContext,
        out VelumSolidProbeInfluenceContext ctx)
    {
      ctx = VelumSolidProbeInfluenceContext.BinaryNeutral;
      if (VelumProductRegistryIntegrityProbes.IsRegistryProbeKey(key))
      {
        if (VelumProductRegistryIntegrityProbes.TrySample(key, out float score, out _))
          ctx = score < 99f
              ? new VelumSolidProbeInfluenceContext(0, 1)
              : new VelumSolidProbeInfluenceContext(1, 1);
        return true;
      }

      IXDocument ixDoc = editContext?.ActiveDocument;
      if (string.Equals(key, ProbeKeyMaterial, StringComparison.Ordinal))
      {
        if (editContext != null && editContext.IsPartDocument && ixDoc is IXPart xp)
        {
          ctx = ResolveMaterialPartInfluenceContext(app, ixDoc, xp);
          return true;
        }

        if (editContext != null && editContext.EditTargetPartModel != null)
        {
          ctx = ResolveMaterialPartInfluenceContextFromModel(editContext.EditTargetPartModel);
          return true;
        }

        if (editContext != null && editContext.IsAssemblyDocument)
        {
          ctx = VelumSolidProbeInfluenceContext.BinaryNeutral;
          return true;
        }
      }
      else if (IsBuiltInVelumSolidProbeKey(key))
      {
        return true;
      }

      return false;
    }

    private static VelumSolidProbeInfluenceContext ResolveMaterialPartInfluenceContextFromModel(
        ModelDoc2 partModel)
    {
      float? com = VelumSolidWorksMaterialComHelper.ScorePartMaterialFromModelDoc(partModel);
      if (com.HasValue)
      {
        bool okCom = com.Value >= MaterialCompleteScore - 0.5f;
        return new VelumSolidProbeInfluenceContext(okCom ? 1 : 0, 1);
      }

      return new VelumSolidProbeInfluenceContext(0, 1);
    }

    private static VelumSolidProbeInfluenceContext ResolveMaterialPartInfluenceContext(
        IXApplication app,
        IXDocument ixDoc,
        IXPart xpart)
    {
      float? com = VelumSolidWorksMaterialComHelper.TryScorePartMaterialFromCom(app, ixDoc);
      if (com.HasValue)
      {
        bool okCom = com.Value >= MaterialCompleteScore - 0.5f;
        return new VelumSolidProbeInfluenceContext(okCom ? 1 : 0, 1);
      }

      try
      {
        IXMaterial mat = xpart.Configurations?.Active?.Material;
        bool has = PartHasMaterial(mat);
        return new VelumSolidProbeInfluenceContext(has ? 1 : 0, 1);
      }
      catch (EntityNotFoundException)
      {
        return new VelumSolidProbeInfluenceContext(0, 1);
      }
    }

    internal static bool IsBuiltInVelumSolidProbeKey(string k) =>
        string.Equals(k, ProbeKeyMaterial, StringComparison.Ordinal)
        || string.Equals(k, ProbeKeyDocumentUnsavedNew, StringComparison.Ordinal)
        || VelumSolidExportDocumentationProbe.TryParseProbeKey(k, out _)
        || VelumBlankSizeProperties.IsBlankSizeProbeKey(k)
        || VelumProductRegistryIntegrityProbes.IsRegistryProbeKey(k);

    private static bool TrySampleSingleProbe(
        string key,
        IXApplication app,
        VelumSolidDocumentEditContext editContext,
        out float value,
        out string detail)
    {
      value = 0f;
      detail = null;

      if (VelumProductRegistryIntegrityProbes.TrySample(key, out value, out detail))
        return true;

      IXDocument ixDoc = editContext?.ActiveDocument;
      if (ixDoc == null)
        return false;

      if (ixDoc is IXPart xp)
      {
        if (string.Equals(key, ProbeKeyDocumentUnsavedNew, StringComparison.Ordinal))
        {
          value = ScoreDocumentUnsavedNew(app, ixDoc, out detail);
          return true;
        }

        if (string.Equals(key, ProbeKeyMaterial, StringComparison.Ordinal))
        {
          value = MaterialScorePart(app, ixDoc, xp, out detail);
          return true;
        }

        if (VelumBlankSizeProperties.IsBlankSizeProbeKey(key))
        {
          ModelDoc2 partModel = VelumSolidWorksModelDocHelper.TryGetActiveModelDoc2(app, ixDoc);
          if (partModel == null)
            return false;

          value = VelumSolidBlankSizeProbe.ScoreLinksOk(partModel, out detail);
          return true;
        }

        if (VelumSolidExportDocumentationProbe.TryParseProbeKey(key, out VelumSolidExportDocumentationProbe.ExportDocProbeKind exportKind))
        {
          ModelDoc2 partModel = VelumSolidWorksModelDocHelper.TryGetActiveModelDoc2(app, ixDoc);
          if (partModel == null)
            return false;

          if (!VelumSolidExportDocumentationProbe.IsProbeKindForDocumentType(
                  exportKind,
                  swDocumentTypes_e.swDocPART))
            return false;

          value = VelumSolidExportDocumentationProbe.ScoreDocumentProbe(partModel, exportKind, out detail);
          return true;
        }
      }
      else if (ixDoc is IXDrawing)
      {
        if (string.Equals(key, ProbeKeyDocumentUnsavedNew, StringComparison.Ordinal))
        {
          value = ScoreDocumentUnsavedNew(app, ixDoc, out detail);
          return true;
        }

        if (VelumSolidExportDocumentationProbe.TryParseProbeKey(key, out VelumSolidExportDocumentationProbe.ExportDocProbeKind exportKindDrawing))
        {
          ModelDoc2 drawingModel = VelumSolidWorksModelDocHelper.TryGetActiveModelDoc2(app, ixDoc);
          if (drawingModel == null)
            return false;

          if (!VelumSolidExportDocumentationProbe.IsProbeKindForDocumentType(
                  exportKindDrawing,
                  swDocumentTypes_e.swDocDRAWING))
            return false;

          value = VelumSolidExportDocumentationProbe.ScoreDocumentProbe(
              drawingModel,
              exportKindDrawing,
              out detail);
          return true;
        }
      }
      else if (ixDoc is IXAssembly)
      {
        if (string.Equals(key, ProbeKeyDocumentUnsavedNew, StringComparison.Ordinal))
        {
          value = ScoreDocumentUnsavedNew(app, ixDoc, out detail);
          return true;
        }

        if (string.Equals(key, ProbeKeyMaterial, StringComparison.Ordinal))
        {
          if (editContext.EditTargetPartModel != null)
          {
            value = MaterialScorePartModel(editContext.EditTargetPartModel, out detail);
            return true;
          }

          return false;
        }

        if (VelumBlankSizeProperties.IsBlankSizeProbeKey(key))
        {
          if (editContext.EditTargetPartModel != null)
          {
            value = VelumSolidBlankSizeProbe.ScoreLinksOk(editContext.EditTargetPartModel, out detail);
            return true;
          }

          return false;
        }

        if (VelumSolidExportDocumentationProbe.TryParseProbeKey(key, out VelumSolidExportDocumentationProbe.ExportDocProbeKind exportKindA))
        {
          if (exportKindA == VelumSolidExportDocumentationProbe.ExportDocProbeKind.PdfDrawingPathAvailable)
          {
            // Свойство на самой сборке; при edit-in-context — на редактируемой детали.
            ModelDoc2 pathTarget = editContext.EditTargetPartModel ??
                VelumSolidWorksModelDocHelper.TryGetActiveModelDoc2(app, ixDoc);
            if (pathTarget == null)
              return false;

            swDocumentTypes_e pathDocType = editContext.EditTargetPartModel != null
                ? swDocumentTypes_e.swDocPART
                : swDocumentTypes_e.swDocASSEMBLY;
            if (!VelumSolidExportDocumentationProbe.IsProbeKindForDocumentType(exportKindA, pathDocType))
              return false;

            value = VelumSolidExportDocumentationProbe.ScoreDocumentProbe(
                pathTarget,
                exportKindA,
                out detail);
            return true;
          }

          if (editContext.EditTargetPartModel != null)
          {
            if (!VelumSolidExportDocumentationProbe.IsProbeKindForDocumentType(
                    exportKindA,
                    swDocumentTypes_e.swDocPART))
              return false;

            value = VelumSolidExportDocumentationProbe.ScoreDocumentProbe(
                editContext.EditTargetPartModel,
                exportKindA,
                out detail);
            return true;
          }

          return false;
        }
      }
      else if (IsBuiltInVelumSolidProbeKey(key))
      {
        value = MaterialCompleteScore;
        return true;
      }

      return false;
    }

    private static float ScoreDocumentUnsavedNew(IXApplication app, IXDocument ixDoc, out string detail)
    {
      detail = string.Empty;
      try
      {
        string path = ixDoc.Path ?? string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
          ModelDoc2 md = VelumSolidWorksModelDocHelper.TryGetActiveModelDoc2(app, ixDoc);
          if (md != null)
          {
            try
            {
              path = md.GetPathName() ?? string.Empty;
            }
            catch
            {
            }
          }
        }

        if (string.IsNullOrWhiteSpace(path))
        {
          detail = "Именование:" + System.Environment.NewLine +
                   "Part/Assembly без пути на диске — требуется политика КБ";
          return PartNoMaterialScore;
        }

        detail = "Именование:" + System.Environment.NewLine +
                 "Документ сохранён на диск — OK";
        return MaterialCompleteScore;
      }
      catch (Exception ex)
      {
        VelumSolidDiagLog.WriteError("DocumentUnsavedNew: " + VelumSolidDiagLog.FormatEx(ex));
        detail = "Именование:" + System.Environment.NewLine + "Ошибка чтения пути документа";
        return MaterialCompleteScore;
      }
    }

    private static float MaterialScorePart(IXApplication app, IXDocument ixDoc, IXPart xpart, out string detail)
    {
      detail = string.Empty;
      float? com = VelumSolidWorksMaterialComHelper.TryScorePartMaterialFromCom(app, ixDoc);
      if (com.HasValue)
      {
        bool okCom = com.Value >= MaterialCompleteScore - 0.5f;
        detail = "Анализ материала (COM):" + System.Environment.NewLine +
                 (okCom ? "Материал по данным SolidWorks задан — ОК" : "Материал по данным SolidWorks не задан — NO");
        return com.Value;
      }

      try
      {
        IXMaterial mat = xpart.Configurations?.Active?.Material;
        bool has = PartHasMaterial(mat);
        detail = has
            ? "Анализ материала:" + System.Environment.NewLine + "Материал активной конфигурации задан — ОК"
            : "Анализ материала:" + System.Environment.NewLine + "Материал активной конфигурации не задан — NO";
        return has ? MaterialCompleteScore : PartNoMaterialScore;
      }
      catch (EntityNotFoundException ex)
      {
        VelumSolidDiagLog.WriteError("Material XCad EntityNotFound: " + VelumSolidDiagLog.FormatEx(ex));
        detail = "Анализ материала:" + System.Environment.NewLine +
                 "Проверить материал через API не удалось — NO";
        return PartNoMaterialScore;
      }
    }

    private static float MaterialScorePartModel(ModelDoc2 partModel, out string detail)
    {
      detail = string.Empty;
      float? com = VelumSolidWorksMaterialComHelper.ScorePartMaterialFromModelDoc(partModel);
      if (com.HasValue)
      {
        bool okCom = com.Value >= MaterialCompleteScore - 0.5f;
        detail = "Анализ материала (COM, edit-target):" + System.Environment.NewLine +
                 (okCom ? "Материал по данным SolidWorks задан — ОК" : "Материал по данным SolidWorks не задан — NO");
        return com.Value;
      }

      detail = "Анализ материала (edit-target):" + System.Environment.NewLine +
               "Проверить материал через API не удалось — NO";
      return PartNoMaterialScore;
    }

    private static string SafeDocTitle(IXDocument ixDoc)
    {
      if (ixDoc == null)
        return "(null)";
      try
      {
        return ixDoc.Title ?? "(no title)";
      }
      catch
      {
        return "(title error)";
      }
    }

    private static string SafeModelPath(ModelDoc2 md)
    {
      if (md == null)
        return "(null)";
      try
      {
        string p = md.GetPathName();
        if (!string.IsNullOrEmpty(p))
          return p;
      }
      catch
      {
      }

      try
      {
        return md.GetTitle() ?? "(no title)";
      }
      catch
      {
        return "(path error)";
      }
    }

    private static bool PartHasMaterial(IXMaterial mat) =>
        mat != null && !string.IsNullOrWhiteSpace(mat.Name);

    private static float Clamp100(float v)
    {
      if (v < 0f)
        return 0f;
      if (v > 100f)
        return 100f;
      return v;
    }
  }
}
