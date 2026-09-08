using System;
using System.Globalization;
using System.IO;
using ISIDA.Common;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Velum.ReactiveCore;
using Velum.SolidHomeostasis;

namespace Velum.ReactiveCore.Export
{
  /// <summary>
  /// Фиксация метаданных DXF после того, как файл уже лежит на диске:
  /// каталог, имя, вид проекции, export-штамп, отпечаток файла.
  /// </summary>
  internal static class VelumDxfFinalizeService
  {
    internal sealed class FinalizeRequest
    {
      public ModelDoc2 ModelDoc { get; set; }

      /// <summary>Каталог канонического DXF; только он записывается в «Путь dxf».</summary>
      public string CanonicalFolder { get; set; }

      /// <summary>Зарезервирован для единообразия export API; finalize его не использует.</summary>
      public string DeliveryFolder { get; set; }

      public string FileNamePattern { get; set; }

      public string ResolvedFileName { get; set; }

      public string ConfigName { get; set; }

      public VelumDxfProjectionView ProjectionView { get; set; }

      public System.Collections.Generic.IReadOnlyDictionary<string, string> TemplateContext { get; set; }

      /// <summary>Писать настройки каталога/шаблона в AppConfig.</summary>
      public bool PersistSettings { get; set; }
    }

    internal sealed class FinalizeResult
    {
      public bool Success { get; set; }

      public string Message { get; set; }

      public string OutputPath { get; set; }
    }

    internal static FinalizeResult TryFinalize(FinalizeRequest request)
    {
      var result = new FinalizeResult();
      ModelDoc2 modelDoc = request?.ModelDoc;
      if (modelDoc == null)
      {
        result.Message = "Нет активной детали.";
        return result;
      }

      if (modelDoc.GetType() != (int)swDocumentTypes_e.swDocPART)
      {
        result.Message = "Фиксация DXF доступна только для детали.";
        return result;
      }

      if (!VelumDxfExportService.IsPartSavedOnDisk(modelDoc))
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

      string baseName = VelumDxfFileNameHelper.PrepareResolvedFileName(request.ResolvedFileName);
      if (string.IsNullOrWhiteSpace(baseName))
      {
        baseName = VelumDxfFileNameHelper.BuildFullFileName(
            request.FileNamePattern,
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
        result.Message = "Укажите существующий каталог DXF.";
        return result;
      }

      string outputPath = Path.Combine(folder, baseName + ".dxf");
      result.OutputPath = outputPath;

      if (!File.Exists(outputPath))
      {
        result.Message = "Файл DXF не найден: " + outputPath +
            System.Environment.NewLine + "Сначала выгрузите DXF, затем зафиксируйте метаданные.";
        return result;
      }

      if (!TryBuildFileFingerprint(outputPath, out string fingerprint, out string fingerError))
      {
        result.Message = string.IsNullOrWhiteSpace(fingerError)
            ? "Не удалось прочитать файл DXF."
            : fingerError;
        return result;
      }

      VelumDxfProjectionView projectionView = request.ProjectionView;
      string writeError = null;
      bool writeOk = true;

      VelumExportDocumentationGeometryStampHelper.RunWithGeometryPendingStampSyncSuppressed(() =>
      {
        if (!VelumDxfFileNameHelper.TryWriteDxfCatalogProperty(modelDoc, folder, out string propMessage))
        {
          writeOk = false;
          writeError = "Не записан Путь dxf: " + propMessage;
          return;
        }

        if (!VelumDxfArtifactResolver.TryWritePerConfigFileName(modelDoc, configName, baseName, out string nameMessage))
        {
          writeOk = false;
          writeError = "Не записано Имя файла dxf: " + nameMessage;
          return;
        }

        if (!VelumDxfFileNameHelper.TryWriteProjectionViewProperty(
                modelDoc,
                configName,
                projectionView,
                out string viewMessage))
        {
          writeOk = false;
          writeError = "Не записан Вид проекции dxf: " + viewMessage;
          return;
        }

        if (!TryWriteFingerprint(modelDoc, configName, fingerprint, out string fpMessage))
        {
          writeOk = false;
          writeError = "Не записан отпечаток файла dxf: " + fpMessage;
          return;
        }

        if (!VelumExportDocumentationGeometryStampHelper.TrySyncExportRevisionStampAfterPathWrite(
                modelDoc,
                VelumExportDocumentationProperties.DxfGeometryUpdateStamp,
                configName,
                out string stampMessage))
        {
          writeOk = false;
          writeError = "Не записан штамп ревизии детали: " + stampMessage;
        }
      });

      if (!writeOk)
      {
        result.Message = string.IsNullOrWhiteSpace(writeError)
            ? "Не удалось зафиксировать метаданные DXF."
            : writeError;
        Logger.Warning("Velum DXF finalize failed: " + result.Message);
        return result;
      }

      if (request.PersistSettings)
      {
        VelumDxfFileNameHelper.PersistExportSettings(
            request.FileNamePattern,
            folder,
            projectionView);
      }

      result.Success = true;
      result.Message = "Метаданные DXF зафиксированы: " + outputPath;
      Logger.Info("Velum DXF finalize OK: " + outputPath);
      VelumExportDocumentationGeometryStampHelper.ResetGeometryEditTracking(
          VelumSolidWorksModelDocHelper.TryGetDispatchDocumentKey(modelDoc));
      VelumExportDocumentationGeometryStampHelper.ArmAfterDxfExport(modelDoc);
      VelumSolidProbeRefreshPlanner.MarkExportDocumentationStale();
      Velum.UI.ProductRegistry.VelumProductRegistryExportMetaSync.TrySyncOpenDocumentFromDisk(modelDoc);
      return result;
    }

    internal static bool TryBuildFileFingerprint(string fullPath, out string fingerprint, out string error)
    {
      fingerprint = string.Empty;
      error = string.Empty;
      if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
      {
        error = "file_missing";
        return false;
      }

      try
      {
        var info = new FileInfo(fullPath);
        fingerprint = info.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture) +
            "|" +
            info.Length.ToString(CultureInfo.InvariantCulture);
        return true;
      }
      catch (Exception ex)
      {
        error = ex.Message;
        return false;
      }
    }

    internal static bool TryReadStoredFingerprint(ModelDoc2 modelDoc, string configName, out string fingerprint)
    {
      fingerprint = string.Empty;
      CustomPropertyManager cpm = VelumDxfArtifactResolver.TryGetConfigManager(modelDoc, configName);
      if (cpm == null)
        return false;

      if (!VelumRecipeSolidWorksCustomProperties.TryGetValue(
              cpm,
              VelumExportDocumentationProperties.DxfFileFingerprint,
              out string raw) ||
          string.IsNullOrWhiteSpace(raw))
        return false;

      fingerprint = raw.Trim();
      return true;
    }

    internal static bool TryWriteFingerprint(
        ModelDoc2 modelDoc,
        string configName,
        string fingerprint,
        out string message)
    {
      message = string.Empty;
      CustomPropertyManager cpm = VelumDxfArtifactResolver.TryGetConfigManager(modelDoc, configName);
      if (cpm == null)
      {
        message = "property_manager_unavailable";
        return false;
      }

      return VelumRecipeSolidWorksCustomProperties.TrySetValue(
          cpm,
          VelumExportDocumentationProperties.DxfFileFingerprint,
          fingerprint ?? string.Empty,
          "always",
          VelumSolidCustomPropertyTypes.TypeKeyText,
          out bool skipped,
          out message) && !skipped;
    }
  }
}
