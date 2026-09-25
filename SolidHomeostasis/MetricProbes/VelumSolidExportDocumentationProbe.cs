using System;
using System.Collections.Generic;
using System.IO;
using ISIDA.SymbiontEnv.Contract;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Velum.Configuration;
using Velum.ReactiveCore;
using Velum.ReactiveCore.Export;

namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Опрос метрик PDF (чертёж) и DXF (деталь) по пользовательским свойствам документа.
  /// </summary>
  internal static class VelumSolidExportDocumentationProbe
  {
    internal enum ExportDocProbeKind
    {
      PdfFileExists,
      PdfIsOutdated,
      PdfDrawingPathAvailable,
      DxfFileExists,
      DxfIsOutdated
    }

    internal const string ProbeKeyPdfFileExists = "Velum.Solid.Pdf.FileExists";
    internal const string ProbeKeyPdfIsOutdated = "Velum.Solid.Pdf.IsOutdated";
    internal const string ProbeKeyPdfDrawingPathAvailable = "Velum.Solid.Pdf.DrawingPathAvailable";
    internal const string ProbeKeyDxfFileExists = "Velum.Solid.Dxf.FileExists";
    internal const string ProbeKeyDxfIsOutdated = "Velum.Solid.Dxf.IsOutdated";

    internal static bool TryParseProbeKey(string key, out ExportDocProbeKind kind)
    {
      string k = (key ?? string.Empty).Trim();
      if (string.Equals(k, ProbeKeyPdfFileExists, StringComparison.Ordinal))
      {
        kind = ExportDocProbeKind.PdfFileExists;
        return true;
      }

      if (string.Equals(k, ProbeKeyPdfIsOutdated, StringComparison.Ordinal))
      {
        kind = ExportDocProbeKind.PdfIsOutdated;
        return true;
      }
      if (string.Equals(k, ProbeKeyPdfDrawingPathAvailable, StringComparison.Ordinal))
      {
        kind = ExportDocProbeKind.PdfDrawingPathAvailable;
        return true;
      }

      if (string.Equals(k, ProbeKeyDxfFileExists, StringComparison.Ordinal))
      {
        kind = ExportDocProbeKind.DxfFileExists;
        return true;
      }

      if (string.Equals(k, ProbeKeyDxfIsOutdated, StringComparison.Ordinal))
      {
        kind = ExportDocProbeKind.DxfIsOutdated;
        return true;
      }

      kind = default;
      return false;
    }

    /// <summary>
    /// Ключ пробы «файл существует» для пары PDF/DXF (для IsOutdated — prerequisite).
    /// </summary>
    internal static bool TryGetFileExistsProbeKeyForOutdated(string outdatedProbeKey, out string fileExistsProbeKey)
    {
      fileExistsProbeKey = null;
      if (!TryParseProbeKey(outdatedProbeKey, out ExportDocProbeKind kind))
        return false;

      switch (kind)
      {
        case ExportDocProbeKind.PdfIsOutdated:
          fileExistsProbeKey = ProbeKeyPdfFileExists;
          return true;
        case ExportDocProbeKind.DxfIsOutdated:
          fileExistsProbeKey = ProbeKeyDxfFileExists;
          return true;
        default:
          return false;
      }
    }

    internal static bool IsOutdatedProbeKind(ExportDocProbeKind kind) =>
        kind == ExportDocProbeKind.PdfIsOutdated || kind == ExportDocProbeKind.DxfIsOutdated;

    internal static bool IsPdfProbeKind(ExportDocProbeKind kind) =>
        kind == ExportDocProbeKind.PdfFileExists ||
        kind == ExportDocProbeKind.PdfIsOutdated ||
        kind == ExportDocProbeKind.PdfDrawingPathAvailable;

    internal static bool IsDxfProbeKind(ExportDocProbeKind kind) =>
        kind == ExportDocProbeKind.DxfFileExists || kind == ExportDocProbeKind.DxfIsOutdated;

    internal static bool IsPdfProbeKey(string probeKey)
    {
      return TryParseProbeKey(probeKey, out ExportDocProbeKind kind) && IsPdfProbeKind(kind);
    }

    internal static bool IsDxfProbeKey(string probeKey)
    {
      return TryParseProbeKey(probeKey, out ExportDocProbeKind kind) && IsDxfProbeKind(kind);
    }

    internal static bool IsDrawingPathProbeKey(string probeKey)
    {
      return TryParseProbeKey(probeKey, out ExportDocProbeKind kind) &&
             kind == ExportDocProbeKind.PdfDrawingPathAvailable;
    }

    internal static bool IsProbeKindForDocumentType(ExportDocProbeKind kind, swDocumentTypes_e docType)
    {
      if (kind == ExportDocProbeKind.PdfDrawingPathAvailable)
      {
        return docType == swDocumentTypes_e.swDocPART ||
               docType == swDocumentTypes_e.swDocASSEMBLY ||
               docType == swDocumentTypes_e.swDocDRAWING;
      }

      if (IsPdfProbeKind(kind))
        return docType == swDocumentTypes_e.swDocDRAWING;

      if (IsDxfProbeKind(kind))
        return docType == swDocumentTypes_e.swDocPART;

      return false;
    }

    /// <summary>
    /// Проверка версии имеет смысл только если экспорт требуется и файл уже есть на диске.
    /// </summary>
    internal static bool IsOutdatedProbeApplicable(ModelDoc2 partModel, ExportDocProbeKind kind)
    {
      if (!IsOutdatedProbeKind(kind))
        return true;

      string needPropertyName;
      string pathPropertyName;
      switch (kind)
      {
        case ExportDocProbeKind.PdfIsOutdated:
          needPropertyName = VelumExportDocumentationProperties.NeedPdf;
          pathPropertyName = VelumExportDocumentationProperties.PdfPath;
          break;
        case ExportDocProbeKind.DxfIsOutdated:
          needPropertyName = VelumExportDocumentationProperties.NeedDxf;
          pathPropertyName = VelumExportDocumentationProperties.DxfPath;
          break;
        default:
          return false;
      }

      return TryIsExportFilePresentForOutdatedCheck(partModel, needPropertyName, pathPropertyName);
    }

    /// <summary>
    /// IsOutdated участвует в снимке/давлении только если prerequisite FileExists в gate явно good.
    /// </summary>
    internal static bool IsOutdatedProbeApplicableInSnapshot(
        string outdatedProbeKey,
        IReadOnlyDictionary<string, float> probeSnapshot)
    {
      if (probeSnapshot == null ||
          !TryGetFileExistsProbeKeyForOutdated(outdatedProbeKey, out string fileExistsKey))
        return false;

      if (!probeSnapshot.TryGetValue(fileExistsKey, out float fileExistsMetric))
        return false;

      return VelumSolidMetricProbeThresholds.IsProbeGood(fileExistsMetric);
    }

    /// <summary>
    /// Удаляет IsOutdated из снимка, если файл ещё отсутствует (не дублировать давление FileExists).
    /// </summary>
    internal static void RemoveInapplicableOutdatedFromSnapshot(
        IDictionary<string, float> values,
        IDictionary<string, string> tooltips = null,
        IDictionary<string, VelumSolidProbeInfluenceContext> influenceContexts = null)
    {
      if (values == null || values.Count == 0)
        return;

      float metricEpsilon = VelumAppConfig.SolidEnvironmentMetricDeltaEpsilon;
      var toRemove = new List<string>();

      foreach (KeyValuePair<string, float> kv in values)
      {
        if (!TryParseProbeKey(kv.Key, out ExportDocProbeKind kind) || !IsOutdatedProbeKind(kind))
          continue;

        if (!TryGetFileExistsProbeKeyForOutdated(kv.Key, out string fileExistsKey))
          continue;

        if (!values.TryGetValue(fileExistsKey, out float fileExistsMetric) ||
            MetricProbeThresholds.IsProbeBad(fileExistsMetric, metricEpsilon))
          toRemove.Add(kv.Key);
      }

      foreach (string key in toRemove)
      {
        values.Remove(key);
        tooltips?.Remove(key);
        influenceContexts?.Remove(key);
      }
    }

    /// <summary>
    /// Учитывается ли probe в расчёте давления/полноты снимка (иерархия FileExists → IsOutdated).
    /// </summary>
    internal static bool IsProbeApplicableForPressureEvaluation(
        string probeKey,
        IReadOnlyDictionary<string, float> probeSnapshot,
        VelumSolidDocumentEditContext editContext = null)
    {
      string key = (probeKey ?? string.Empty).Trim();
      if (key.Length == 0)
        return false;

      if (!TryParseProbeKey(key, out ExportDocProbeKind kind))
        return true;

      if (!IsOutdatedProbeKind(kind))
        return true;

      ModelDoc2 modelDoc = TryResolveModelDocForProbeKind(kind, editContext);
      if (modelDoc != null && !IsOutdatedProbeApplicable(modelDoc, kind))
        return false;

      return IsOutdatedProbeApplicableInSnapshot(key, probeSnapshot);
    }

    /// <summary>
    /// Пересчитывает метрики PDF/DXF в снимке, когда проверка больше не применима
    /// (экспорт не требуется, файл отсутствует). Без этого давление не отпускает после «Нужен dxf/pdf = No».
    /// </summary>
    internal static void RefreshInapplicableProbesInSnapshot(
        VelumSolidDocumentEditContext editContext,
        IDictionary<string, float> values,
        IDictionary<string, string> tooltips = null)
    {
      if (values == null || values.Count == 0)
        return;

      var keys = new List<string>(values.Keys);
      for (int i = 0; i < keys.Count; i++)
      {
        string probeKey = keys[i];
        if (!TryParseProbeKey(probeKey, out ExportDocProbeKind kind))
          continue;

        ModelDoc2 modelDoc = TryResolveModelDocForProbeKind(kind, editContext);
        if (modelDoc == null || !ShouldRefreshProbeInSnapshot(modelDoc, kind))
          continue;

        float score = ScoreExportProbeWithoutBootstrap(modelDoc, kind, out string detail);
        values[probeKey] = score;
        if (tooltips != null && !string.IsNullOrEmpty(detail))
          tooltips[probeKey] = detail;
      }
    }

    private static bool ShouldRefreshProbeInSnapshot(ModelDoc2 modelDoc, ExportDocProbeKind kind)
    {
      if (IsOutdatedProbeKind(kind))
        return !IsOutdatedProbeApplicable(modelDoc, kind);

      if (kind == ExportDocProbeKind.PdfDrawingPathAvailable)
      {
        // На чертеже NeedPdf — у самого чертежа; на детали/сборке — у источника.
        if (!TryReadNeedFlag(modelDoc, VelumExportDocumentationProperties.NeedPdf, out bool needPdf))
          return false;
        return !needPdf;
      }

      string needPropertyName = IsPdfProbeKind(kind)
          ? VelumExportDocumentationProperties.NeedPdf
          : VelumExportDocumentationProperties.NeedDxf;

      if (string.Equals(needPropertyName, VelumExportDocumentationProperties.NeedDxf, StringComparison.Ordinal))
        return !VelumDxfNeedFlagResolver.HasActiveConfigExportable(modelDoc);

      if (!TryReadNeedFlag(modelDoc, needPropertyName, out bool needExport))
        return false;

      return !needExport;
    }

    private static float ScoreExportProbeWithoutBootstrap(
        ModelDoc2 modelDoc,
        ExportDocProbeKind kind,
        out string detail)
    {
      switch (kind)
      {
        case ExportDocProbeKind.PdfFileExists:
          return ScorePdfFileExists(modelDoc, out detail);
        case ExportDocProbeKind.PdfIsOutdated:
          return ScorePdfIsOutdated(modelDoc, out detail);
        case ExportDocProbeKind.PdfDrawingPathAvailable:
          return ScorePdfDrawingPathAvailable(modelDoc, out detail);
        case ExportDocProbeKind.DxfFileExists:
          return ScoreDxfFileExists(modelDoc, out detail);
        case ExportDocProbeKind.DxfIsOutdated:
          return ScoreDxfIsOutdated(modelDoc, out detail);
        default:
          detail = string.Empty;
          return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
      }
    }

    private static ModelDoc2 TryResolveModelDocForProbeKind(
        ExportDocProbeKind kind,
        VelumSolidDocumentEditContext editContext)
    {
      if (editContext == null)
        return null;

      if (kind == ExportDocProbeKind.PdfDrawingPathAvailable)
      {
        if (editContext.IsDrawingDocument ||
            editContext.IsPartDocument ||
            editContext.IsAssemblyDocument)
          return editContext.ActiveModelDoc;
        return null;
      }

      if (IsPdfProbeKind(kind))
        return editContext.IsDrawingDocument ? editContext.ActiveModelDoc : null;

      if (!IsDxfProbeKind(kind))
        return null;

      if (editContext.IsPartDocument)
        return editContext.ActiveModelDoc;

      return editContext.EditTargetPartModel;
    }

    internal static float ScoreDocumentProbe(ModelDoc2 modelDoc, ExportDocProbeKind kind, out string detail)
    {
      detail = string.Empty;
      if (modelDoc == null)
      {
        detail = "Экспортная документация:" + System.Environment.NewLine +
                 "Документ недоступен — условная оценка 100";
        return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
      }

      switch (kind)
      {
        case ExportDocProbeKind.PdfFileExists:
          TryEnsurePdfProbePropertiesOnDrawing(modelDoc);
          return ScorePdfFileExists(modelDoc, out detail);
        case ExportDocProbeKind.PdfIsOutdated:
          return ScorePdfIsOutdated(modelDoc, out detail);
        case ExportDocProbeKind.PdfDrawingPathAvailable:
          return ScorePdfDrawingPathAvailable(modelDoc, out detail);
        case ExportDocProbeKind.DxfFileExists:
          TryEnsureDxfProbePropertiesOnPart(modelDoc);
          return ScoreDxfFileExists(modelDoc, out detail);
        case ExportDocProbeKind.DxfIsOutdated:
          return ScoreDxfIsOutdated(modelDoc, out detail);
        default:
          detail = "Экспортная документация:" + System.Environment.NewLine + "Неизвестный тип пробы";
          return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
      }
    }

    /// <summary>
    /// Перед опросом Velum.Solid.Pdf.FileExists на чертеже: при отсутствии свойств создаёт
    /// «Нужен pdf» = Yes и пустой «Путь pdf», чтобы метрика могла давить на витал без диалога.
    /// </summary>
    private static void TryEnsurePdfProbePropertiesOnDrawing(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return;

      if (modelDoc.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
        return;

      TryEnsureExportProbeProperties(
          modelDoc,
          VelumExportDocumentationProperties.NeedPdf,
          VelumExportDocumentationProperties.PdfPath,
          VelumExportDocumentationProperties.FlagYes);
    }

    /// <summary>
    /// Перед опросом Velum.Solid.Dxf.FileExists на детали: при отсутствии свойства создаёт
    /// «Нужен dxf» по умолчанию из настроек.
    /// </summary>
    private static void TryEnsureDxfProbePropertiesOnPart(ModelDoc2 modelDoc)
    {
      VelumDxfBatchDocumentHelper.TryEnsureNeedDxfDefaultIfPart(modelDoc);
    }

    private static void TryEnsureExportProbeProperties(
        ModelDoc2 modelDoc,
        string needPropertyName,
        string pathPropertyName,
        string defaultNeedFlag)
    {
      CustomPropertyManager cpm =
          VelumRecipeSolidWorksCustomProperties.TryGetManager(modelDoc, "document");
      if (cpm == null)
        return;

      string needFlag = string.IsNullOrWhiteSpace(defaultNeedFlag)
          ? VelumExportDocumentationProperties.FlagYes
          : defaultNeedFlag;

      if (!VelumRecipeSolidWorksCustomProperties.TryPropertyExists(cpm, needPropertyName))
      {
        VelumRecipeSolidWorksCustomProperties.TrySetValue(
            cpm,
            needPropertyName,
            needFlag,
            "always",
            VelumSolidCustomPropertyTypes.TypeKeyBoolean,
            out _,
            out _);
      }

      if (!string.Equals(needFlag, VelumExportDocumentationProperties.FlagNo, StringComparison.Ordinal) &&
          !VelumRecipeSolidWorksCustomProperties.TryPropertyExists(cpm, pathPropertyName))
      {
        VelumRecipeSolidWorksCustomProperties.TrySetValue(
            cpm,
            pathPropertyName,
            string.Empty,
            "always",
            VelumSolidCustomPropertyTypes.TypeKeyText,
            out _,
            out _);
      }
    }

    private static float ScorePdfDrawingPathAvailable(ModelDoc2 modelDoc, out string detail)
    {
      detail = string.Empty;
      if (modelDoc == null)
      {
        detail = "PDF (путь чертежа):" + System.Environment.NewLine + "Документ недоступен";
        return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
      }

      int docType;
      try
      {
        docType = modelDoc.GetType();
      }
      catch
      {
        detail = "PDF (путь чертежа):" + System.Environment.NewLine + "Не удалось определить тип документа";
        return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
      }

      if (docType == (int)swDocumentTypes_e.swDocDRAWING)
        return ScorePdfDrawingPathAvailableOnDrawing(modelDoc, out detail);

      if (docType == (int)swDocumentTypes_e.swDocPART ||
          docType == (int)swDocumentTypes_e.swDocASSEMBLY)
        return ScorePdfDrawingPathAvailableOnSource(modelDoc, out detail);

      detail = "PDF (путь чертежа):" + System.Environment.NewLine + "Тип документа не поддерживается";
      return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
    }

    private static float ScorePdfDrawingPathAvailableOnSource(ModelDoc2 modelDoc, out string detail)
    {
      if (!TryReadNeedFlag(modelDoc, VelumExportDocumentationProperties.NeedPdf, out bool needPdf))
      {
        detail = "PDF (путь чертежа):" + System.Environment.NewLine +
                 "Свойство «" + VelumExportDocumentationProperties.NeedPdf +
                 "» не задано — проверка не требуется";
        return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
      }

      if (!needPdf)
      {
        detail = "PDF (путь чертежа):" + System.Environment.NewLine +
                 "Экспорт PDF не требуется — путь чертежа не проверяется";
        return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
      }

      if (VelumDrawingPathPropertyHelper.IsDrawingPathUsable(
              modelDoc, null, out string storedPath, out string reason))
      {
        detail = "PDF (путь чертежа):" + System.Environment.NewLine +
                 "Чертёж найден: " + storedPath;
        return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
      }

      if (string.Equals(reason, "missing_file", StringComparison.Ordinal))
      {
        detail = "PDF (путь чертежа):" + System.Environment.NewLine +
                 "Свойство задано, но файл недоступен: " + storedPath;
      }
      else
      {
        detail = "PDF (путь чертежа):" + System.Environment.NewLine +
                 "Свойство «" + VelumExportDocumentationProperties.DrawingPath + "» не задано";
      }

      return VelumSolidWorksHomeostasisMetrics.PartNoMaterialScore;
    }

    private static float ScorePdfDrawingPathAvailableOnDrawing(ModelDoc2 drawingDoc, out string detail)
    {
      if (!TryReadNeedFlag(drawingDoc, VelumExportDocumentationProperties.NeedPdf, out bool needPdf))
      {
        detail = "PDF (путь чертежа):" + System.Environment.NewLine +
                 "Свойство «" + VelumExportDocumentationProperties.NeedPdf +
                 "» не задано — проверка не требуется";
        return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
      }

      if (!needPdf)
      {
        detail = "PDF (путь чертежа):" + System.Environment.NewLine +
                 "Экспорт PDF не требуется — путь у источников не проверяется";
        return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
      }

      string drawingPath;
      try
      {
        drawingPath = (drawingDoc.GetPathName() ?? string.Empty).Trim();
      }
      catch
      {
        drawingPath = string.Empty;
      }

      if (string.IsNullOrWhiteSpace(drawingPath) || !VelumPathExists.FileExists(drawingPath))
      {
        detail = "PDF (путь чертежа):" + System.Environment.NewLine +
                 "Чертёж не сохранён на диске — сначала сохраните .slddrw";
        return VelumSolidWorksHomeostasisMetrics.PartNoMaterialScore;
      }

      var targets = new Dictionary<string, ModelDoc2>(StringComparer.OrdinalIgnoreCase);
      VelumDrawingPathSavePropagator.CollectReferencedPartOrAssemblyTargets(drawingDoc, targets);
      if (targets.Count == 0)
      {
        detail = "PDF (путь чертежа):" + System.Environment.NewLine +
                 "Нет модельных видов с деталью/сборкой — проверка не требуется";
        return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
      }

      int checkedLoaded = 0;
      int badLoaded = 0;
      string firstBad = string.Empty;
      foreach (KeyValuePair<string, ModelDoc2> entry in targets)
      {
        ModelDoc2 source = entry.Value;
        if (source == null)
          continue;

        checkedLoaded++;
        if (VelumDrawingPathPropertyHelper.IsDrawingPathUsable(
                source, drawingPath, out _, out string reason))
          continue;

        badLoaded++;
        if (string.IsNullOrEmpty(firstBad))
        {
          firstBad = Path.GetFileName(entry.Key) + " (" + reason + ")";
        }
      }

      if (checkedLoaded == 0)
      {
        detail = "PDF (путь чертежа):" + System.Environment.NewLine +
                 "Источники чертежа не загружены в сессию — откройте деталь/сборку или зафиксируйте путь";
        return VelumSolidWorksHomeostasisMetrics.PartNoMaterialScore;
      }

      if (badLoaded > 0)
      {
        detail = "PDF (путь чертежа):" + System.Environment.NewLine +
                 "У источников нет актуального пути к этому чертежу: " + firstBad +
                 (badLoaded > 1 ? " и ещё " + (badLoaded - 1) : string.Empty);
        return VelumSolidWorksHomeostasisMetrics.PartNoMaterialScore;
      }

      detail = "PDF (путь чертежа):" + System.Environment.NewLine +
               "У загруженных источников путь указывает на этот чертёж (" + checkedLoaded + ")";
      return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
    }

    private static float ScorePdfFileExists(ModelDoc2 drawingModel, out string detail)
    {
      detail = string.Empty;
      if (!TryReadNeedFlag(drawingModel, VelumExportDocumentationProperties.NeedPdf, out bool needExport))
      {
        detail = "PDF (наличие):" + System.Environment.NewLine +
                 "Логическое свойство «" + VelumExportDocumentationProperties.NeedPdf + "» не задано — проверка не требуется";
        return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
      }

      if (!needExport)
      {
        detail = "PDF (наличие):" + System.Environment.NewLine +
                 "Экспорт не требуется (свойство = No или legacy Нет)";
        return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
      }

      ResolvedPdfArtifact artifact = VelumPdfArtifactResolver.Resolve(drawingModel);
      if (!artifact.Found)
      {
        detail = "PDF (наличие):" + System.Environment.NewLine +
                 "Требуется экспорт, но PDF не найден";
        return VelumSolidWorksHomeostasisMetrics.PartNoMaterialScore;
      }

      detail = "PDF (наличие):" + System.Environment.NewLine +
               "Файл найден: " + artifact.FullPath;
      return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
    }

    private static float ScorePdfIsOutdated(ModelDoc2 drawingModel, out string detail)
    {
      detail = string.Empty;
      if (!TryReadNeedFlag(drawingModel, VelumExportDocumentationProperties.NeedPdf, out bool needExport))
      {
        detail = "PDF (версия):" + System.Environment.NewLine +
                 "Логическое свойство «" + VelumExportDocumentationProperties.NeedPdf + "» не задано — проверка не требуется";
        return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
      }

      if (!needExport)
      {
        detail = "PDF (версия):" + System.Environment.NewLine +
                 "Экспорт не требуется — версия не проверяется";
        return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
      }

      ResolvedPdfArtifact artifact = VelumPdfArtifactResolver.Resolve(drawingModel);
      if (!artifact.Found)
      {
        detail = "PDF (версия):" + System.Environment.NewLine +
                 "Файл отсутствует — проверка версии не выполняется";
        return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
      }

      if (VelumExportDocumentationGeometryStampHelper.TryGetCurrentUpdateStamp(drawingModel, out int currentStamp) &&
          VelumExportDocumentationGeometryStampHelper.TryReadStoredUpdateStamp(
              drawingModel,
              VelumExportDocumentationProperties.PdfGeometryUpdateStamp,
              out int storedStamp))
      {
        if (VelumExportDocumentationGeometryStampHelper.TryIsPdfOutdated(
                drawingModel,
                currentStamp,
                storedStamp))
        {
          string detailSuffix = storedStamp + "→" + currentStamp;
          if (VelumExportDocumentationGeometryStampHelper.TryReadStoredUpdateStamp(
                  drawingModel,
                  VelumExportDocumentationProperties.PdfGeometryPendingStamp,
                  out int pendingStamp) &&
              pendingStamp > storedStamp)
            detailSuffix = "pending " + pendingStamp + " > export " + storedStamp;

          detail = "PDF (версия):" + System.Environment.NewLine +
                   "Чертёж изменился после экспорта (" + detailSuffix + ")" + System.Environment.NewLine +
                   "Файл: " + artifact.FullPath;
          return VelumSolidWorksHomeostasisMetrics.PartNoMaterialScore;
        }

        detail = "PDF (версия):" + System.Environment.NewLine +
                 "Чертёж не менялся с момента экспорта (штамп " + currentStamp + ")";
        return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
      }

      string sourcePath = TryGetPartPath(drawingModel);
      if (string.IsNullOrWhiteSpace(sourcePath) || !VelumPathExists.FileExists(sourcePath))
      {
        detail = "PDF (версия):" + System.Environment.NewLine +
                 "Источник не сохранён на диск — версию проверить нельзя";
        return VelumSolidWorksHomeostasisMetrics.PartNoMaterialScore;
      }

      try
      {
        DateTime exportTime = File.GetLastWriteTimeUtc(artifact.FullPath);
        DateTime sourceTime = File.GetLastWriteTimeUtc(sourcePath);
        if (exportTime < sourceTime)
        {
          detail = "PDF (версия):" + System.Environment.NewLine +
                   "Файл устарел (нет штампа геометрии — сравнение дат):" + System.Environment.NewLine +
                   artifact.FullPath + System.Environment.NewLine +
                   "Источник новее: " + sourcePath;
          return VelumSolidWorksHomeostasisMetrics.PartNoMaterialScore;
        }

        detail = "PDF (версия):" + System.Environment.NewLine +
                 "Файл актуален (нет штампа геометрии — сравнение дат)";
        return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
      }
      catch (Exception ex)
      {
        VelumSolidDiagLog.WriteError("PDF IsOutdated: " + VelumSolidDiagLog.FormatEx(ex));
        detail = "PDF (версия):" + System.Environment.NewLine + "Ошибка сравнения дат файлов";
        return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
      }
    }

    private static float ScoreDxfFileExists(ModelDoc2 partModel, out string detail)
    {
      detail = string.Empty;
      if (!VelumDxfNeedFlagResolver.HasActiveConfigExportable(partModel))
      {
        detail = "DXF (наличие):" + System.Environment.NewLine +
                 "Логическое свойство «" + VelumExportDocumentationProperties.NeedDxf + "» не задано — проверка не требуется";
        return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
      }

      string catalogRaw = TryReadExportPathProperty(partModel, VelumExportDocumentationProperties.DxfPath);
      string catalog = VelumDxfArtifactResolver.NormalizeCatalogPath(catalogRaw, partModel);
      if (string.IsNullOrWhiteSpace(catalog) || !VelumPathExists.DirectoryExists(catalog))
      {
        detail = "DXF (наличие):" + System.Environment.NewLine +
                 "Требуется экспорт, но каталог «" + VelumExportDocumentationProperties.DxfPath + "» не задан — файл отсутствует";
        return VelumSolidWorksHomeostasisMetrics.PartNoMaterialScore;
      }

      string activeConfig = VelumDxfArtifactResolver.TryGetActiveConfigurationName(partModel);
      if (string.IsNullOrWhiteSpace(activeConfig))
      {
        detail = "DXF (наличие):" + System.Environment.NewLine +
                 "Не удалось определить активную конфигурацию";
        return VelumSolidWorksHomeostasisMetrics.PartNoMaterialScore;
      }

      ResolvedDxfArtifact artifact = VelumDxfArtifactResolver.Resolve(partModel, catalog, activeConfig);
      if (!artifact.Found)
      {
        detail = "DXF (наличие):" + System.Environment.NewLine +
                 "Каталог: " + catalog + System.Environment.NewLine +
                 "Отсутствует конфигурация: " + activeConfig;
        return VelumSolidWorksHomeostasisMetrics.PartNoMaterialScore;
      }

      detail = "DXF (наличие):" + System.Environment.NewLine +
               "Файл найден: " + artifact.FullPath;
      return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
    }

    private static float ScoreDxfIsOutdated(ModelDoc2 partModel, out string detail)
    {
      detail = string.Empty;
      if (!VelumDxfNeedFlagResolver.HasActiveConfigExportable(partModel))
      {
        detail = "DXF (версия):" + System.Environment.NewLine +
                 "Логическое свойство «" + VelumExportDocumentationProperties.NeedDxf + "» не задано — проверка не требуется";
        return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
      }

      string configName = VelumDxfArtifactResolver.TryGetActiveConfigurationName(partModel);
      if (string.IsNullOrWhiteSpace(configName))
      {
        detail = "DXF (версия):" + System.Environment.NewLine +
                 "Не удалось определить активную конфигурацию";
        return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
      }

      string catalogRaw = TryReadExportPathProperty(partModel, VelumExportDocumentationProperties.DxfPath);
      string catalog = VelumDxfArtifactResolver.NormalizeCatalogPath(catalogRaw, partModel);

      ResolvedDxfArtifact artifact = VelumDxfArtifactResolver.Resolve(partModel, catalog, configName);
      if (!artifact.Found)
      {
        detail = "DXF (версия):" + System.Environment.NewLine +
                 "Файл отсутствует — проверка версии не выполняется";
        return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
      }

      if (!VelumExportDocumentationGeometryStampHelper.TryGetCurrentUpdateStamp(partModel, out int currentStamp))
        currentStamp = 0;

      if (!VelumExportDocumentationGeometryStampHelper.TryReadStoredUpdateStamp(
              partModel,
              VelumExportDocumentationProperties.DxfGeometryUpdateStamp,
              configName,
              out int storedStamp))
      {
        detail = "DXF (версия):" + System.Environment.NewLine +
                 "Конфигурация: " + configName + System.Environment.NewLine +
                 "Файл: " + artifact.FullPath + System.Environment.NewLine +
                 "Нет штампа — проверка не выполняется";
        return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
      }

      if (VelumExportDocumentationGeometryStampHelper.TryIsPerConfigDxfOutdated(
              partModel,
              configName,
              currentStamp,
              storedStamp))
      {
        string detailSuffix = storedStamp + "\u2192" + currentStamp;
        if (VelumExportDocumentationGeometryStampHelper.TryReadStoredUpdateStamp(
                partModel,
                VelumExportDocumentationProperties.DxfGeometryPendingStamp,
                configName,
                out int pendingStamp) &&
            pendingStamp > storedStamp)
          detailSuffix = "pending " + pendingStamp + " > export " + storedStamp;

        detail = "DXF (версия):" + System.Environment.NewLine +
                 "Геометрия изменилась после экспорта (" + detailSuffix + ")" + System.Environment.NewLine +
                 "Конфигурация: " + configName + System.Environment.NewLine +
                 "Файл: " + artifact.FullPath;
        return VelumSolidWorksHomeostasisMetrics.PartNoMaterialScore;
      }

      detail = "DXF (версия):" + System.Environment.NewLine +
               "Конфигурация: " + configName + System.Environment.NewLine +
               "Файл: " + artifact.FullPath + System.Environment.NewLine +
               "Геометрия не менялась с момента экспорта (штамп " + currentStamp + ")";
      return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
    }

    private static float ScoreFileExists(
        ModelDoc2 partModel,
        string needPropertyName,
        string pathPropertyName,
        string formatLabel,
        out string detail)
    {
      detail = string.Empty;
      if (!TryReadNeedFlag(partModel, needPropertyName, out bool needExport))
      {
        detail = formatLabel + " (наличие):" + System.Environment.NewLine +
                 "Логическое свойство «" + needPropertyName + "» не задано — проверка не требуется";
        return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
      }

      if (!needExport)
      {
        detail = formatLabel + " (наличие):" + System.Environment.NewLine +
                 "Экспорт не требуется (свойство = No или legacy Нет)";
        return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
      }

      string exportPath = TryReadExportPathProperty(partModel, pathPropertyName);
      if (string.IsNullOrWhiteSpace(exportPath))
      {
        detail = formatLabel + " (наличие):" + System.Environment.NewLine +
                 "Требуется экспорт, но «" + pathPropertyName + "» не задан — файл отсутствует";
        return VelumSolidWorksHomeostasisMetrics.PartNoMaterialScore;
      }

      if (!TryResolveExistingFile(exportPath, partModel, out string resolvedPath))
      {
        detail = formatLabel + " (наличие):" + System.Environment.NewLine +
                 "Путь «" + exportPath + "» — файл не найден";
        return VelumSolidWorksHomeostasisMetrics.PartNoMaterialScore;
      }

      detail = formatLabel + " (наличие):" + System.Environment.NewLine +
               "Файл найден: " + resolvedPath;
      return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
    }

    private static float ScoreIsOutdated(
        ModelDoc2 partModel,
        string needPropertyName,
        string pathPropertyName,
        string geometryStampPropertyName,
        string formatLabel,
        string sourceChangeLabel,
        out string detail)
    {
      detail = string.Empty;
      if (!TryReadNeedFlag(partModel, needPropertyName, out bool needExport))
      {
        detail = formatLabel + " (версия):" + System.Environment.NewLine +
                 "Логическое свойство «" + needPropertyName + "» не задано — проверка не требуется";
        return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
      }

      if (!needExport)
      {
        detail = formatLabel + " (версия):" + System.Environment.NewLine +
                 "Экспорт не требуется — версия не проверяется";
        return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
      }

      string exportPath = TryReadExportPathProperty(partModel, pathPropertyName);
      if (!TryResolveExistingFile(exportPath, partModel, out string resolvedExportPath))
      {
        detail = formatLabel + " (версия):" + System.Environment.NewLine +
                 "Файл отсутствует — проверка версии не выполняется";
        return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
      }

      if (VelumExportDocumentationGeometryStampHelper.TryGetCurrentUpdateStamp(partModel, out int currentStamp) &&
          VelumExportDocumentationGeometryStampHelper.TryReadStoredUpdateStamp(
              partModel,
              geometryStampPropertyName,
              out int storedStamp))
      {
        if (currentStamp != storedStamp)
        {
          detail = formatLabel + " (версия):" + System.Environment.NewLine +
                   sourceChangeLabel + " изменился после экспорта" + System.Environment.NewLine +
                   "Штамп: " + storedStamp + " → " + currentStamp + System.Environment.NewLine +
                   "Файл: " + resolvedExportPath;
          return VelumSolidWorksHomeostasisMetrics.PartNoMaterialScore;
        }

        detail = formatLabel + " (версия):" + System.Environment.NewLine +
                 sourceChangeLabel + " не менялся с момента экспорта (штамп " + currentStamp + ")";
        return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
      }

      string sourcePath = TryGetPartPath(partModel);
      if (string.IsNullOrWhiteSpace(sourcePath) || !VelumPathExists.FileExists(sourcePath))
      {
        detail = formatLabel + " (версия):" + System.Environment.NewLine +
                 "Источник не сохранён на диск — версию проверить нельзя";
        return VelumSolidWorksHomeostasisMetrics.PartNoMaterialScore;
      }

      try
      {
        DateTime exportTime = File.GetLastWriteTimeUtc(resolvedExportPath);
        DateTime sourceTime = File.GetLastWriteTimeUtc(sourcePath);
        if (exportTime < sourceTime)
        {
          detail = formatLabel + " (версия):" + System.Environment.NewLine +
                   "Файл устарел (нет штампа геометрии — сравнение дат):" + System.Environment.NewLine +
                   resolvedExportPath + System.Environment.NewLine +
                   "Источник новее: " + sourcePath;
          return VelumSolidWorksHomeostasisMetrics.PartNoMaterialScore;
        }

        detail = formatLabel + " (версия):" + System.Environment.NewLine +
                 "Файл актуален (нет штампа геометрии — сравнение дат)";
        return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
      }
      catch (Exception ex)
      {
        VelumSolidDiagLog.WriteError(formatLabel + " IsOutdated: " + VelumSolidDiagLog.FormatEx(ex));
        detail = formatLabel + " (версия):" + System.Environment.NewLine + "Ошибка сравнения дат файлов";
        return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
      }
    }

    private static bool TryIsExportFilePresentForOutdatedCheck(
        ModelDoc2 partModel,
        string needPropertyName,
        string pathPropertyName)
    {
      if (partModel == null)
        return false;
      if (string.Equals(needPropertyName, VelumExportDocumentationProperties.NeedDxf, StringComparison.Ordinal))
      {
        if (!VelumDxfNeedFlagResolver.HasActiveConfigExportable(partModel))
          return false;
      }
      else if (!TryReadNeedFlag(partModel, needPropertyName, out bool needExport) || !needExport)
        return false;

      if (string.Equals(pathPropertyName, VelumExportDocumentationProperties.DxfPath, StringComparison.Ordinal))
      {
        if (!VelumDxfNeedFlagResolver.HasActiveConfigExportable(partModel))
          return false;

        string configName = VelumDxfArtifactResolver.TryGetActiveConfigurationName(partModel);
        string catalogRaw = TryReadExportPathProperty(partModel, pathPropertyName);
        string catalog = VelumDxfArtifactResolver.NormalizeCatalogPath(catalogRaw, partModel);
        if (!string.IsNullOrWhiteSpace(configName))
        {
          return VelumDxfArtifactResolver.Resolve(partModel, catalog, configName).Found;
        }

        return false;
      }

      if (string.Equals(pathPropertyName, VelumExportDocumentationProperties.PdfPath, StringComparison.Ordinal))
        return VelumPdfArtifactResolver.Resolve(partModel).Found;

      string exportPath = TryReadExportPathProperty(partModel, pathPropertyName);
      return TryResolveExistingFile(exportPath, partModel, out _);
    }

    private static readonly string[] PropertyReadScopes = { "active", "document", "default" };

    /// <summary>
    /// Путь экспорта: сначала document (куда пишет Velum), затем первое непустое значение из active/default.
    /// </summary>
    private static readonly string[] ExportPathReadScopes = { "document", "active", "default" };

    private static string TryReadExportPathProperty(ModelDoc2 modelDoc, string pathPropertyName)
    {
      if (modelDoc == null || string.IsNullOrWhiteSpace(pathPropertyName))
        return string.Empty;

      string emptyFromExistingProperty = string.Empty;
      foreach (string scope in ExportPathReadScopes)
      {
        CustomPropertyManager cpm = VelumRecipeSolidWorksCustomProperties.TryGetManager(modelDoc, scope);
        if (cpm == null)
          continue;

        if (!VelumRecipeSolidWorksCustomProperties.TryGetValue(cpm, pathPropertyName, out string raw))
          continue;

        string trimmed = (raw ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(trimmed))
        {
          // Свойство может хранить относительный путь — достраиваем префикс корневого каталога.
          return VelumRelativeDocumentPathResolver.ToFull(trimmed);
        }

        if (string.IsNullOrEmpty(emptyFromExistingProperty) &&
            VelumRecipeSolidWorksCustomProperties.TryPropertyExists(cpm, pathPropertyName))
          emptyFromExistingProperty = trimmed;
      }

      return emptyFromExistingProperty;
    }

    private static bool TryReadNeedFlag(ModelDoc2 partModel, string propertyName, out bool needExport)
    {
      needExport = false;
      if (!TryReadBooleanFromScopes(partModel, propertyName, out bool? flag))
        return false;

      needExport = flag ?? false;
      return true;
    }

    private static string TryReadProperty(ModelDoc2 partModel, string pathPropertyName)
    {
      foreach (string scope in PropertyReadScopes)
      {
        CustomPropertyManager cpm = VelumRecipeSolidWorksCustomProperties.TryGetManager(partModel, scope);
        if (cpm == null)
          continue;

        if (VelumRecipeSolidWorksCustomProperties.TryGetValue(cpm, pathPropertyName, out string raw))
          return (raw ?? string.Empty).Trim();
      }

      return string.Empty;
    }

    private static bool TryReadBooleanFromScopes(
        ModelDoc2 partModel,
        string propertyName,
        out bool? flag)
    {
      flag = null;
      if (partModel == null || string.IsNullOrWhiteSpace(propertyName))
        return false;

      foreach (string scope in PropertyReadScopes)
      {
        CustomPropertyManager cpm = VelumRecipeSolidWorksCustomProperties.TryGetManager(partModel, scope);
        if (cpm == null)
          continue;

        if (!VelumRecipeSolidWorksCustomProperties.TryGetBooleanValue(cpm, propertyName, out bool? scopeFlag))
          continue;

        flag = scopeFlag;
        return true;
      }

      return false;
    }

    private static string TryGetPartPath(ModelDoc2 partModel)
    {
      if (partModel == null)
        return string.Empty;

      try
      {
        return (partModel.GetPathName() ?? string.Empty).Trim();
      }
      catch
      {
        return string.Empty;
      }
    }

    private static bool TryResolveExistingFile(string path, ModelDoc2 modelDoc, out string resolvedPath)
    {
      resolvedPath = null;
      if (string.IsNullOrWhiteSpace(path))
        return false;

      if (TryResolveExistingFileCandidate(path, out resolvedPath))
        return true;

      if (modelDoc == null)
        return false;

      try
      {
        string docPath = TryGetPartPath(modelDoc);
        if (string.IsNullOrWhiteSpace(docPath))
          return false;

        string docDir = Path.GetDirectoryName(docPath);
        if (string.IsNullOrWhiteSpace(docDir))
          return false;

        string relativeCandidate = System.Environment.ExpandEnvironmentVariables(path.Trim());
        if (Path.IsPathRooted(relativeCandidate))
          return false;

        string combined = Path.GetFullPath(Path.Combine(docDir, relativeCandidate));
        return TryResolveExistingFileCandidate(combined, out resolvedPath);
      }
      catch
      {
        return false;
      }
    }

    private static bool TryResolveExistingFileCandidate(string path, out string resolvedPath)
    {
      resolvedPath = null;
      if (string.IsNullOrWhiteSpace(path))
        return false;

      try
      {
        string candidate = System.Environment.ExpandEnvironmentVariables(path.Trim());
        if (!Path.IsPathRooted(candidate))
        {
          // Относительный путь из свойства — сначала достройка префикса корневого каталога
          // (при пустой настройке ToFull вернёт как есть, сработает fallback от каталога документа).
          candidate = VelumRelativeDocumentPathResolver.ToFull(candidate);
          if (!Path.IsPathRooted(candidate))
            candidate = Path.GetFullPath(candidate);
        }

        if (VelumPathExists.FileExists(candidate))
        {
          resolvedPath = candidate;
          return true;
        }
      }
      catch
      {
      }

      return false;
    }
  }
}
