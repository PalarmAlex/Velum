using System;
using System.Collections.Generic;
using System.IO;
using ISIDA.Common;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Velum.ReactiveCore;
using Velum.SolidHomeostasis;

namespace Velum.ReactiveCore.Export
{
  /// <summary>Экспорт детали в DXF: развёртка листового металла или ортогональная проекция.</summary>
  internal static class VelumDxfExportService
  {
    internal sealed class ExportRequest
    {
      public ModelDoc2 ModelDoc { get; set; }

      /// <summary>Каталог канонического DXF, который фиксируется в свойствах детали.</summary>
      public string CanonicalFolder { get; set; }

      /// <summary>Необязательный каталог выдачи; в свойства детали не записывается.</summary>
      public string DeliveryFolder { get; set; }

      /// <summary>Имя delivery-копии без расширения. Не влияет на canonical DXF.</summary>
      public string DeliveryFileName { get; set; }

      public string FileNamePattern { get; set; }

      public string ResolvedFileName { get; set; }

      /// <summary>
      /// Пакетный режим: свойство «Имя файла dxf» только читается — finalize не перезаписывает
      /// существующее значение имени.
      /// </summary>
      public bool ReadOnlyFileNameProperty { get; set; }

      public string ConfigName { get; set; }

      public VelumDxfProjectionView ProjectionView { get; set; }

      public IReadOnlyDictionary<string, string> TemplateContext { get; set; }
    }

    internal sealed class ExportResult
    {
      public bool Success { get; set; }

      public string Message { get; set; }

      public string OutputPath { get; set; }

      public string DeliveryPath { get; set; }

      /// <summary>False означает, что канонический экспорт успешен, но копия не доставлена.</summary>
      public bool Delivered { get; set; }

      public string DeliveryError { get; set; }

      public int ExportedConfigCount { get; set; }

      public IReadOnlyList<string> EmptySuffixProperties { get; set; }

      public bool IsEmptyDocument { get; set; }

      /// <summary>Свойство «Имя файла dxf» пустое — нужна первая выгрузка через диалог.</summary>
      public bool IsFirstExport { get; set; }
    }

    internal sealed class MultiExportProgress
    {
      public int CurrentIndex { get; set; }

      public int TotalCount { get; set; }

      public string ConfigName { get; set; }

      public string PartTitle { get; set; }
    }

    internal static bool IsPartSavedOnDisk(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return false;

      try
      {
        string path = modelDoc.GetPathName();
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
      }
      catch
      {
        return false;
      }
    }

    internal static ExportResult TryExportAllConfigurations(
        ExportRequest request,
        Action<MultiExportProgress> onProgress = null)
    {
      var aggregate = new ExportResult();
      ModelDoc2 modelDoc = request?.ModelDoc;
      if (modelDoc == null)
      {
        aggregate.Message = "Нет активной детали.";
        return aggregate;
      }

      IReadOnlyList<string> configs = VelumDxfNeedFlagResolver.CollectExportable(modelDoc);
      if (configs.Count == 0)
      {
        aggregate.Message = "Нет конфигураций с «Нужен dxf = Да».";
        return aggregate;
      }

      int exported = 0;
      string lastPath = string.Empty;
      string lastError = string.Empty;
      var exportedConfigs = new List<string>();
      var emptySuffixErrors = new List<string>();

      for (int i = 0; i < configs.Count; i++)
      {
        string configName = configs[i];
        onProgress?.Invoke(new MultiExportProgress
        {
          CurrentIndex = i + 1,
          TotalCount = configs.Count,
          ConfigName = configName,
          PartTitle = TryGetDocumentTitle(modelDoc)
        });

        ExportRequest single = CloneRequestForConfig(request, configName);
        ExportResult one = TryExport(single);
        if (!one.Success)
        {
          lastError = one.Message;
          if (one.EmptySuffixProperties != null && one.EmptySuffixProperties.Count > 0)
            emptySuffixErrors.Add(one.Message);
          continue;
        }

        exported++;
        exportedConfigs.Add(configName);
        lastPath = one.OutputPath;
      }

      if (exported <= 0)
      {
        aggregate.Message = VelumDxfFileNameHelper.FormatEmptySuffixPropertiesErrorList(emptySuffixErrors);
        if (string.IsNullOrWhiteSpace(aggregate.Message))
        {
          aggregate.Message = string.IsNullOrWhiteSpace(lastError)
              ? "Не удалось экспортировать DXF ни для одной конфигурации."
              : lastError;
        }

        return aggregate;
      }

      if (exportedConfigs.Count > 1)
      {
        VelumExportDocumentationGeometryStampHelper.TryResyncPerConfigDxfGeometryStamps(
            modelDoc,
            exportedConfigs);
      }

      aggregate.Success = true;
      aggregate.ExportedConfigCount = exported;
      aggregate.OutputPath = lastPath;
      aggregate.Message = exported == 1
          ? "DXF сохранён: " + lastPath
          : "Экспортировано конфигураций: " + exported + ". Последний файл: " + lastPath;
      string suffixErrorList = VelumDxfFileNameHelper.FormatEmptySuffixPropertiesErrorList(emptySuffixErrors);
      if (!string.IsNullOrWhiteSpace(suffixErrorList))
        aggregate.Message += System.Environment.NewLine + System.Environment.NewLine + suffixErrorList;
      return aggregate;
    }

    internal static ExportResult TryExport(ExportRequest request)
    {
      var result = new ExportResult();
      ModelDoc2 modelDoc = request?.ModelDoc;
      if (modelDoc == null)
      {
        result.Message = "Нет активной детали.";
        return result;
      }

      if (modelDoc.GetType() != (int)swDocumentTypes_e.swDocPART)
      {
        result.Message = "Экспорт DXF доступен только для детали.";
        return result;
      }

      if (!IsPartSavedOnDisk(modelDoc))
      {
        result.Message = "Сначала сохраните деталь по правилам КБ.";
        return result;
      }

      string configName = VelumDxfArtifactResolver.TryResolveDxfConfigurationName(
          modelDoc,
          request.ConfigName);

      if (!VelumDxfFileNameHelper.TryActivateConfiguration(modelDoc, configName))
      {
        result.Message = "Не удалось активировать конфигурацию: " + configName;
        return result;
      }

      if (VelumDxfPartGeometryHelper.IsEmptyDocument(modelDoc))
      {
        result.IsEmptyDocument = true;
        result.Message = VelumDxfPartGeometryHelper.EmptyDocumentMessage;
        return result;
      }

      // До resolve имени: живые ссылки L/W/T для листовых (суффикс имени / спецификация).
      VelumBlankSizePropertyLinksService.TryEnsure(modelDoc, "repair", out _);

      IReadOnlyDictionary<string, string> templateContext = request.TemplateContext
          ?? VelumDxfFileNameHelper.BuildTemplateContext(modelDoc);
      if (VelumDxfFileNameHelper.TryGetEmptyReferencedSuffixProperties(
              VelumDxfQuantityToken.StripQuantityTokens(request.FileNamePattern),
              modelDoc,
              templateContext,
              out IReadOnlyList<string> emptySuffixProperties))
      {
        result.EmptySuffixProperties = emptySuffixProperties;
        result.Message = VelumDxfFileNameHelper.FormatEmptySuffixPropertiesError(
            emptySuffixProperties,
            TryGetDocumentTitle(modelDoc),
            configName);
        return result;
      }

      // Пакетный режим: базовое имя берётся только из свойства «Имя файла dxf».
      // Пустое свойство — не выгружаем (это первая выгрузка, она делается вручную через диалог).
      if (request.ReadOnlyFileNameProperty)
      {
        string propertyFileName = VelumDxfArtifactResolver.TryReadPerConfigFileName(modelDoc, configName);
        if (string.IsNullOrWhiteSpace(propertyFileName))
        {
          result.IsFirstExport = true;
          result.Message = "Свойство «Имя файла dxf» не задано — нужна первая выгрузка через диалог DXF.";
          return result;
        }

        request.ResolvedFileName = propertyFileName;
      }

      string baseName = VelumDxfFileNameHelper.PrepareResolvedFileName(request.ResolvedFileName);
      if (string.IsNullOrWhiteSpace(baseName))
      {
        baseName = VelumDxfFileNameHelper.BuildFullFileName(
            VelumDxfQuantityToken.StripQuantityTokens(request.FileNamePattern),
            modelDoc,
            configName,
            request.TemplateContext);
      }

      if (!VelumDxfFileNameHelper.TryValidateBaseFileName(baseName, modelDoc, configName, out string validationMessage))
      {
        result.Message = validationMessage;
        return result;
      }

      string folder = (request.CanonicalFolder ?? string.Empty).Trim();
      if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
      {
        result.Message = "Укажите существующий каталог для экспорта DXF.";
        return result;
      }

      string outputPath = Path.Combine(folder, baseName + ".dxf");
      VelumDxfProjectionView projectionView = request.ProjectionView;
      bool isSheetMetalExport = VelumSolidSheetMetalHelper.IsSheetMetalPart(modelDoc) &&
          VelumSolidSheetMetalHelper.TryFindFlatPatternFeature(modelDoc) != null;
      string foldRestoreConfig = isSheetMetalExport
          ? VelumDxfArtifactResolver.TryGetActiveConfigurationName(modelDoc)
          : null;

      try
      {
        bool exported = TryExportGeometry(modelDoc, outputPath, projectionView, out string flatError);

        if (!exported)
        {
          result.Message = string.IsNullOrWhiteSpace(flatError)
              ? "Не удалось экспортировать DXF."
              : flatError;
          Logger.Warning("Velum DXF export failed: " + result.Message);
          return result;
        }

        if (!File.Exists(outputPath))
        {
          result.Message = "Файл DXF не создан: " + outputPath;
          Logger.Warning(result.Message);
          return result;
        }

        VelumDxfFilePostProcessor.TryFixExportedFile(outputPath);

        // Свернуть ДО фиксации штампа: иначе ForceRebuild после sync → «DXF устарел».
        if (isSheetMetalExport)
        {
          VelumExportDocumentationGeometryStampHelper.RunWithGeometryPendingStampSyncSuppressed(() =>
              VelumSolidSheetMetalHelper.TryRestoreFoldedDisplay(
                  modelDoc,
                  foldRestoreConfig ?? configName));
        }

        VelumDxfFinalizeService.FinalizeResult finalize =
            VelumDxfFinalizeService.TryFinalize(new VelumDxfFinalizeService.FinalizeRequest
            {
              ModelDoc = modelDoc,
              CanonicalFolder = folder,
              FileNamePattern = VelumDxfQuantityToken.StripQuantityTokens(request.FileNamePattern),
              ResolvedFileName = baseName,
              ConfigName = configName,
              ProjectionView = projectionView,
              TemplateContext = templateContext,
              PersistSettings = true,
              PreserveExistingFileNameProperty = request.ReadOnlyFileNameProperty
            });

        if (!finalize.Success)
        {
          result.Message = string.IsNullOrWhiteSpace(finalize.Message)
              ? "DXF создан, но метаданные не зафиксированы: " + outputPath
              : finalize.Message;
          Logger.Warning("Velum DXF export: finalize failed: " + result.Message);
          return result;
        }

        result.Success = true;
        result.OutputPath = outputPath;
        result.ExportedConfigCount = 1;
        result.Message = "DXF сохранён: " + outputPath;
        result.Delivered = VelumExportDeliveryHelper.TryCopyToDelivery(
            outputPath,
            request.DeliveryFolder,
            string.IsNullOrWhiteSpace(request.DeliveryFileName)
                ? baseName
                : request.DeliveryFileName,
            ".dxf",
            out string deliveryPath,
            out string deliveryError);
        result.DeliveryPath = deliveryPath;
        result.DeliveryError = deliveryError;
        if (!result.Delivered)
          result.Message += System.Environment.NewLine +
              "ExportedNotDelivered: канонический DXF сохранён, копия не доставлена: " + deliveryError;
        Logger.Info("Velum DXF export OK: " + outputPath);
        return result;
      }
      catch (Exception ex)
      {
        result.Message = ex.Message;
        Logger.Warning("Velum DXF export exception: " + ex.Message);
        return result;
      }
    }

    private static ExportRequest CloneRequestForConfig(ExportRequest request, string configName)
    {
      return new ExportRequest
      {
        ModelDoc = request.ModelDoc,
        CanonicalFolder = request.CanonicalFolder,
        DeliveryFolder = request.DeliveryFolder,
        DeliveryFileName = request.DeliveryFileName,
        FileNamePattern = request.FileNamePattern,
        ResolvedFileName = null,
        ReadOnlyFileNameProperty = request.ReadOnlyFileNameProperty,
        ConfigName = configName,
        ProjectionView = request.ProjectionView,
        TemplateContext = request.TemplateContext
      };
    }

    private static string TryGetDocumentTitle(ModelDoc2 modelDoc)
    {
      try
      {
        return modelDoc?.GetTitle() ?? string.Empty;
      }
      catch
      {
        return string.Empty;
      }
    }

    private static bool TryExportGeometry(
        ModelDoc2 modelDoc,
        string outputPath,
        VelumDxfProjectionView projectionView,
        out string error)
    {
      if (VelumSolidSheetMetalHelper.IsSheetMetalPart(modelDoc) &&
          VelumSolidSheetMetalHelper.TryFindFlatPatternFeature(modelDoc) != null)
        return TryExportFlatPattern(modelDoc, outputPath, out error);

      if (VelumDxfPartGeometryHelper.HasSolidBodies(modelDoc))
        return TryExportProjection(modelDoc, outputPath, projectionView, out error);

      return VelumDxfSketchExportHelper.TryExportFirstSketch(modelDoc, outputPath, out error);
    }

    private static bool TryExportFlatPattern(ModelDoc2 modelDoc, string outputPath, out string error)
    {
      error = string.Empty;
      Feature flatPattern = VelumSolidSheetMetalHelper.TryFindFlatPatternFeature(modelDoc);
      if (flatPattern == null)
      {
        error = "Нет развёртки. Создайте развёртку листового металла в SolidWorks.";
        return false;
      }

      string restoreConfigName = VelumDxfArtifactResolver.TryGetActiveConfigurationName(modelDoc);
      try
      {
        if (!VelumSolidSheetMetalHelper.TryEnsureFlatPatternReady(
                modelDoc, flatPattern, out _, out error))
          return false;

        flatPattern = VelumSolidSheetMetalHelper.TryFindFlatPatternFeature(modelDoc) ?? flatPattern;
        if (!flatPattern.Select2(false, -1))
        {
          error = "Не удалось выбрать feature развёртки.";
          return false;
        }

        // ExportToDWG2 вместо устаревшего ExportFlatPatternView: меньше шансов
        // оставить деталь в развёрнутом состоянии после первой выгрузки.
        string modelPath = modelDoc.GetPathName();
        if (string.IsNullOrWhiteSpace(modelPath))
        {
          error = "Сначала сохраните деталь по правилам КБ.";
          return false;
        }

        // 1 = geometry; линии гиба (4) намеренно не включаем — для лазера нужен только контур.
        const int sheetMetalOptions = 1;
        PartDoc partDoc = (PartDoc)modelDoc;
        bool ok = partDoc.ExportToDWG2(
            outputPath,
            modelPath,
            (int)swExportToDWG_e.swExportToDWG_ExportSheetMetal,
            true,
            null,
            false,
            false,
            sheetMetalOptions,
            null);

        if (!ok)
        {
          // Fallback на старый API, если ExportToDWG2 недоступен/отказал.
          flatPattern = VelumSolidSheetMetalHelper.TryFindFlatPatternFeature(modelDoc) ?? flatPattern;
          flatPattern.Select2(false, 0);
          ok = partDoc.ExportFlatPatternView(outputPath, 1);
        }

        if (!ok)
          error = "SolidWorks не выполнил экспорт развёртки.";
        return ok;
      }
      catch (Exception ex)
      {
        error = ex.Message;
        return false;
      }
      finally
      {
        // Свёртка здесь — страховка при ошибке до finalize в TryExport.
        // Pending не пишем: иначе метрика мигнёт «устарел» на время разворота.
        VelumExportDocumentationGeometryStampHelper.RunWithGeometryPendingStampSyncSuppressed(() =>
            VelumSolidSheetMetalHelper.TryRestoreFoldedDisplay(modelDoc, restoreConfigName));
      }
    }

    private static bool TryExportProjection(
        ModelDoc2 modelDoc,
        string outputPath,
        VelumDxfProjectionView projectionView,
        out string error)
    {
      error = string.Empty;
      MathTransform savedOrientation = VelumDxfStandardViewHelper.TryCaptureOrientation(modelDoc);

      try
      {
        if (!VelumDxfStandardViewHelper.EnsureStandardView(modelDoc, projectionView))
        {
          error = "Не удалось установить вид " + projectionView + ".";
          return false;
        }

        return VelumDxfProjectionExportHelper.TryExport(
            modelDoc, outputPath, projectionView, out error);
      }
      catch (Exception ex)
      {
        error = ex.Message;
        return false;
      }
      finally
      {
        VelumDxfStandardViewHelper.TryRestoreOrientation(modelDoc, savedOrientation);
      }
    }
  }
}
