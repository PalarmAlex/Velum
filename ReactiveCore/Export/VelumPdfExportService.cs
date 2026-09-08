using System;
using System.IO;
using ISIDA.Common;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Velum.ReactiveCore;
using Velum.SolidHomeostasis;

namespace Velum.ReactiveCore.Export
{
  /// <summary>Экспорт чертежа SolidWorks в PDF.</summary>
  internal static class VelumPdfExportService
  {
    internal sealed class ExportRequest
    {
      public ModelDoc2 ModelDoc { get; set; }
      /// <summary>Каталог канонического PDF, который фиксируется в «Путь pdf».</summary>
      public string CanonicalFolder { get; set; }
      /// <summary>Необязательный каталог выдачи, не влияющий на свойства чертежа.</summary>
      public string DeliveryFolder { get; set; }
    }

    internal sealed class ExportResult
    {
      public bool Success { get; set; }
      public string Message { get; set; }
      public string OutputPath { get; set; }
      public string DeliveryPath { get; set; }
      public bool Delivered { get; set; }
      public string DeliveryError { get; set; }
    }

    internal static bool IsDrawingSavedOnDisk(ModelDoc2 modelDoc)
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

    internal static ExportResult TryExport(ExportRequest request)
    {
      var result = new ExportResult();
      ModelDoc2 modelDoc = request?.ModelDoc;
      if (modelDoc == null)
      {
        result.Message = "Нет активного чертежа.";
        return result;
      }

      if (modelDoc.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
      {
        result.Message = "Экспорт PDF доступен только для чертежа.";
        return result;
      }

      if (!IsDrawingSavedOnDisk(modelDoc))
      {
        result.Message = "Сначала сохраните чертёж по правилам КБ.";
        return result;
      }

      string baseName = VelumPdfFileNameHelper.ResolveDrawingBaseFileName(modelDoc);
      if (!VelumPdfFileNameHelper.TryValidateBaseFileName(baseName, out string validationMessage))
      {
        result.Message = validationMessage;
        return result;
      }

      string folder = (request.CanonicalFolder ?? string.Empty).Trim();
      if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
      {
        result.Message = "Укажите существующий каталог для экспорта PDF.";
        return result;
      }

      string outputPath = Path.Combine(folder, baseName + ".pdf");

      try
      {
        if (!TrySaveDrawingAsPdf(modelDoc, outputPath, out string exportError))
        {
          result.Message = string.IsNullOrWhiteSpace(exportError)
              ? "Не удалось экспортировать PDF."
              : exportError;
          Logger.Warning("Velum PDF export failed: " + result.Message);
          return result;
        }

        if (!File.Exists(outputPath))
        {
          result.Message = "Файл PDF не создан: " + outputPath;
          Logger.Warning(result.Message);
          return result;
        }

        VelumExportDocumentationGeometryStampHelper.RunWithGeometryPendingStampSyncSuppressed(() =>
        {
          if (!VelumPdfFileNameHelper.TryWritePdfPathProperty(modelDoc, outputPath, out string propMessage))
          {
            Logger.Warning("Velum PDF export: не записан Путь pdf: " + propMessage);
          }

          if (!VelumExportDocumentationGeometryStampHelper.TryFinalizePdfExportStamps(
                  modelDoc,
                  out string stampMessage))
          {
            Logger.Warning(
                "Velum PDF export: штампы ревизии чертежа: " + stampMessage +
                "; устаревание PDF может остаться ошибочным");
          }
          else
          {
            Logger.Info("Velum PDF export stamps: " + stampMessage);
          }

          // Arm до выхода из suppress: иначе отложенный ModifyNotify после SaveAs PDF
          // успевает записать pending до absorb.
          VelumExportDocumentationGeometryStampHelper.ArmAfterPdfExport(modelDoc);
        });

        // Дожим после побочных COM-событий SaveAs PDF (GetUpdateStamp мог снова вырасти).
        VelumExportDocumentationGeometryStampHelper.RunWithGeometryPendingStampSyncSuppressed(() =>
        {
          if (VelumExportDocumentationGeometryStampHelper.TryFinalizePdfExportStamps(
                  modelDoc,
                  out string catchUpMessage))
            Logger.Info("Velum PDF export stamps catch-up: " + catchUpMessage);
          else
            Logger.Warning("Velum PDF export stamps catch-up: " + catchUpMessage);

          VelumExportDocumentationGeometryStampHelper.ArmAfterPdfExport(modelDoc);
        });

        VelumPdfFileNameHelper.PersistExportSettings(folder);

        result.Success = true;
        result.OutputPath = outputPath;
        result.Message = "PDF сохранён: " + outputPath;
        result.Delivered = VelumExportDeliveryHelper.TryCopyToDelivery(
            outputPath,
            request.DeliveryFolder,
            baseName,
            ".pdf",
            out string deliveryPath,
            out string deliveryError);
        result.DeliveryPath = deliveryPath;
        result.DeliveryError = deliveryError;
        if (!result.Delivered)
          result.Message += System.Environment.NewLine +
              "ExportedNotDelivered: канонический PDF сохранён, копия не доставлена: " + deliveryError;
        Logger.Info("Velum PDF export OK: " + outputPath);
        VelumSolidProbeRefreshPlanner.MarkExportDocumentationStale();
        Velum.UI.ProductRegistry.VelumProductRegistryExportMetaSync.TrySyncOpenDocumentFromDisk(modelDoc);
        Velum.UI.ProductRegistry.VelumProductRegistryExportMetaSync.TrySyncPdfModelStampAtExport(modelDoc);
        return result;
      }
      catch (Exception ex)
      {
        result.Message = ex.Message;
        Logger.Warning("Velum PDF export exception: " + ex.Message);
        return result;
      }
    }

    private static bool TrySaveDrawingAsPdf(ModelDoc2 modelDoc, string outputPath, out string error)
    {
      error = string.Empty;
      if (modelDoc == null || string.IsNullOrWhiteSpace(outputPath))
      {
        error = "Нет чертежа или пути для сохранения PDF.";
        return false;
      }

      try
      {
        int errors = 0;
        int warnings = 0;
        bool ok = modelDoc.Extension.SaveAs(
            outputPath,
            (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
            (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
            null,
            ref errors,
            ref warnings);

        if (!ok)
        {
          error = "SolidWorks SaveAs PDF завершился с ошибкой (errors=" +
                  errors.ToString() + ", warnings=" + warnings.ToString() + ").";
          return false;
        }

        return true;
      }
      catch (Exception ex)
      {
        error = ex.Message;
        return false;
      }
    }
  }
}
