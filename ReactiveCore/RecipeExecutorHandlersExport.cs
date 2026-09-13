using System;
using System.Collections.Generic;
using System.IO;
using ISIDA.Common;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Velum.ReactiveCore.Export;
using Velum.SolidHomeostasis;
using Velum.UI;
using Xarial.XCad.SolidWorks;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Handler'ы экспортной документации (PDF/DXF). Имена совпадают с handlers-catalog.json
  /// и шагами EnvironmentRecipes.yaml.
  /// </summary>
  internal static class RecipeExecutorHandlersExport
  {
    /// <summary>export_documentation_dialog — модальный диалог экспорта DXF.</summary>
    public static bool TryExecuteExportDocumentationDialog(
        int index,
        ModelDoc2 modelDoc,
        bool suppressWrongDocumentWarning,
        out RecipeStepExecutionResult result)
    {
      string docHint = TryGetDocumentHint(modelDoc);
      if (modelDoc == null)
      {
        result = new RecipeStepExecutionResult(index, "invoke", false, false, "model_doc_null");
        return false;
      }

      if (modelDoc.GetType() != (int)swDocumentTypes_e.swDocPART)
      {
        // У-рефлекс мог сработать на «не тот» тип документа: оператор действие не просил — тихо пропускаем.
        if (!suppressWrongDocumentWarning)
        {
          System.Windows.Forms.MessageBox.Show(
              "Экспорт DXF доступен только для детали.",
              "Экспорт DXF",
              System.Windows.Forms.MessageBoxButtons.OK,
              System.Windows.Forms.MessageBoxIcon.Information);
        }
        result = new RecipeStepExecutionResult(
            index,
            "invoke",
            true,
            true,
            "skipped_not_part " + docHint);
        Logger.Info("Velum export_documentation_dialog skipped (not SLDPRT) " + docHint);
        return true;
      }

      if (!VelumAdminAccess.IsAdmin)
      {
        result = new RecipeStepExecutionResult(
            index,
            "invoke",
            true,
            true,
            "skipped_not_admin " + docHint);
        Logger.Info("Velum export_documentation_dialog skipped (not admin) " + docHint);
        return true;
      }

      VelumDxfExportDialogHost.TryShowModal(modelDoc);
      result = new RecipeStepExecutionResult(
          index,
          "invoke",
          true,
          false,
          "export_documentation_dialog_closed " + docHint);
      Logger.Info("Velum export_documentation_dialog closed " + docHint);
      return true;
    }

    /// <summary>export_drawing_pdf_dialog — модальный диалог экспорта PDF чертежа.</summary>
    public static bool TryExecuteExportDrawingPdfDialog(
        int index,
        ModelDoc2 modelDoc,
        bool suppressWrongDocumentWarning,
        out RecipeStepExecutionResult result)
    {
      string docHint = TryGetDocumentHint(modelDoc);
      if (modelDoc == null)
      {
        result = new RecipeStepExecutionResult(index, "invoke", false, false, "model_doc_null");
        return false;
      }

      if (modelDoc.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
      {
        // У-рефлекс мог сработать на «не тот» тип документа: оператор действие не просил — тихо пропускаем.
        if (!suppressWrongDocumentWarning)
        {
          System.Windows.Forms.MessageBox.Show(
              "Экспорт PDF доступен только для чертежа.",
              "Экспорт PDF",
              System.Windows.Forms.MessageBoxButtons.OK,
              System.Windows.Forms.MessageBoxIcon.Information);
        }
        result = new RecipeStepExecutionResult(
            index,
            "invoke",
            true,
            true,
            "skipped_not_drawing " + docHint);
        Logger.Info("Velum export_drawing_pdf_dialog skipped (not SLDDRW) " + docHint);
        return true;
      }

      if (!VelumAdminAccess.IsAdmin)
      {
        result = new RecipeStepExecutionResult(
            index,
            "invoke",
            true,
            true,
            "skipped_not_admin " + docHint);
        Logger.Info("Velum export_drawing_pdf_dialog skipped (not admin) " + docHint);
        return true;
      }

      VelumPdfExportDialogHost.TryShowModal(modelDoc);
      result = new RecipeStepExecutionResult(
          index,
          "invoke",
          true,
          false,
          "export_drawing_pdf_dialog_closed " + docHint);
      Logger.Info("Velum export_drawing_pdf_dialog closed " + docHint);
      return true;
    }

    /// <summary>delete_pdf_artifact — удаление устаревших или ненужных PDF на активном чертеже.</summary>
    public static bool TryExecuteDeletePdfArtifact(
        int index,
        ModelDoc2 modelDoc,
        out RecipeStepExecutionResult result)
    {
      string docHint = TryGetDocumentHint(modelDoc);
      if (modelDoc == null)
      {
        result = new RecipeStepExecutionResult(index, "invoke", false, false, "model_doc_null");
        return false;
      }

      if (modelDoc.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
      {
        result = new RecipeStepExecutionResult(
            index,
            "invoke",
            true,
            true,
            "skipped_not_drawing " + docHint);
        Logger.Info("Velum delete_pdf_artifact skipped (not SLDDRW) " + docHint);
        return true;
      }

      bool hasNeedFlag = VelumPdfBatchDocumentHelper.TryReadNeedPdf(modelDoc, out bool needPdf);
      ResolvedPdfArtifact artifact = VelumPdfArtifactResolver.Resolve(modelDoc);
      if (!artifact.Found || string.IsNullOrWhiteSpace(artifact.FullPath))
      {
        result = new RecipeStepExecutionResult(
            index,
            "invoke",
            true,
            true,
            "delete_pdf_artifact nothing_to_delete " + docHint);
        Logger.Info("Velum delete_pdf_artifact skipped (not found) " + docHint);
        return true;
      }

      bool shouldDelete = false;
      if (hasNeedFlag && !needPdf)
        shouldDelete = true;
      else if (hasNeedFlag && needPdf)
      {
        if (VelumExportDocumentationGeometryStampHelper.TryGetCurrentUpdateStamp(modelDoc, out int currentStamp) &&
            VelumExportDocumentationGeometryStampHelper.TryReadStoredUpdateStamp(
                modelDoc,
                VelumExportDocumentationProperties.PdfGeometryUpdateStamp,
                out int storedStamp))
          shouldDelete = VelumExportDocumentationGeometryStampHelper.TryIsPdfOutdated(
              modelDoc,
              currentStamp,
              storedStamp);
      }

      if (!shouldDelete)
      {
        result = new RecipeStepExecutionResult(
            index,
            "invoke",
            true,
            true,
            "delete_pdf_artifact skipped_not_outdated " + docHint);
        Logger.Info("Velum delete_pdf_artifact skipped (not outdated/not needed) " + docHint);
        return true;
      }

      try
      {
        if (File.Exists(artifact.FullPath))
          File.Delete(artifact.FullPath);
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum delete_pdf_artifact: " + ex.Message);
        result = new RecipeStepExecutionResult(index, "invoke", false, false, ex.Message);
        return false;
      }

      VelumPdfArtifactResolver.TryClearPdfExportMetadata(modelDoc, out _);
      VelumSolidProbeRefreshPlanner.MarkExportDocumentationStale();
      result = new RecipeStepExecutionResult(
          index,
          "invoke",
          true,
          false,
          "delete_pdf_artifact deleted=1 " + docHint);
      Logger.Info("Velum delete_pdf_artifact deleted=1 " + docHint);
      return true;
    }

    /// <summary>export_documentation_create_files — создание PDF/DXF по флагу (заглушка).</summary>
    public static bool TryExecuteExportDocumentationCreateFiles(
        int index,
        ModelDoc2 modelDoc,
        out RecipeStepExecutionResult result)
    {
      string docHint = TryGetDocumentHint(modelDoc);
      Logger.Info(
          "Velum export_documentation_create_files (stub): экспорт не реализован " + docHint);
      result = new RecipeStepExecutionResult(
          index,
          "invoke",
          true,
          true,
          "stub:export_documentation_create_files");
      return true;
    }

    /// <summary>
    /// write_drawing_path — зафиксировать «путь чертежа».
    /// Чертёж: прописать путь во все referenced детали/сборки.
    /// Деталь/сборка: починить по одноимённому .slddrw в корне документа
    /// (не затирает валидный существующий путь); либо явный аргумент drawing_path.
    /// </summary>
    public static bool TryExecuteWriteDrawingPath(
        int index,
        IReadOnlyDictionary<string, string> parameters,
        ModelDoc2 modelDoc,
        SldWorks swApp,
        IReadOnlyDictionary<string, string> templateContext,
        out RecipeStepExecutionResult result)
    {
      string docHint = TryGetDocumentHint(modelDoc);
      if (modelDoc == null)
      {
        result = new RecipeStepExecutionResult(index, "invoke", false, false, "model_doc_null");
        return false;
      }

      if (modelDoc.GetType() == (int)swDocumentTypes_e.swDocDRAWING)
      {
        VelumDrawingPathSavePropagator.PropagationResult propagation =
            VelumDrawingPathSavePropagator.TryPropagateFromSavedDrawing(swApp, modelDoc);
        if (propagation.Failed > 0 && propagation.Written == 0 && propagation.Skipped == 0)
        {
          result = new RecipeStepExecutionResult(
              index,
              "invoke",
              false,
              false,
              "failed written=0 failed=" + propagation.Failed + " " + docHint);
          return false;
        }

        if (propagation.Failed > 0)
        {
          result = new RecipeStepExecutionResult(
              index,
              "invoke",
              false,
              false,
              "failed written=" + propagation.Written + " failed=" + propagation.Failed + " " + docHint);
          return false;
        }

        bool allSkipped = propagation.Written == 0;
        result = new RecipeStepExecutionResult(
            index,
            "invoke",
            true,
            allSkipped,
            (allSkipped ? "skipped_unchanged" : "written") +
                " count=" + (propagation.Written + propagation.Skipped) + " " + docHint);
        Logger.Info("Velum write_drawing_path propagated " + docHint);
        return true;
      }

      int docType = modelDoc.GetType();
      if (docType != (int)swDocumentTypes_e.swDocPART &&
          docType != (int)swDocumentTypes_e.swDocASSEMBLY)
      {
        result = new RecipeStepExecutionResult(
            index, "invoke", true, true, "skipped_unsupported_document " + docHint);
        return true;
      }

      string drawingPath = RecipeArgsParser.Get(parameters, "drawing_path");
      if (string.IsNullOrWhiteSpace(drawingPath))
        drawingPath = RecipeArgsParser.Get(parameters, "path");
      drawingPath = VelumRecipeTemplateResolver.Resolve(drawingPath, templateContext)?.Trim();

      bool written;
      bool skipped;
      string message;
      if (!string.IsNullOrWhiteSpace(drawingPath))
      {
        string stored = VelumDrawingPathPropertyHelper.TryRead(modelDoc);
        if (!string.IsNullOrWhiteSpace(stored) &&
            File.Exists(stored) &&
            !string.Equals(
                VelumDrawingPathPropertyHelper.NormalizePath(stored),
                VelumDrawingPathPropertyHelper.NormalizePath(drawingPath),
                StringComparison.OrdinalIgnoreCase))
        {
          // Явный аргумент — разрешаем смену; валидный путь без аргумента не трогаем (ветка repair).
        }

        written = false;
        skipped = false;
        message = string.Empty;
        VelumExportDocumentationGeometryStampHelper.RunWithGeometryPendingStampSyncSuppressed(() =>
        {
          written = VelumDrawingPathPropertyHelper.TryWrite(
              modelDoc, drawingPath, out skipped, out message);
        });
      }
      else
      {
        written = false;
        skipped = false;
        message = string.Empty;
        string repairedPath = string.Empty;
        VelumExportDocumentationGeometryStampHelper.RunWithGeometryPendingStampSyncSuppressed(() =>
        {
          written = VelumDrawingPathPropertyHelper.TryRepairFromSiblingDrawing(
              modelDoc, out skipped, out repairedPath, out message);
        });
        drawingPath = repairedPath;
      }

      if (!written)
      {
        string failHint = string.Equals(message, "sibling_drawing_not_found", StringComparison.Ordinal)
            ? "failed sibling_drawing_not_found: откройте чертёж и зафиксируйте путь " + docHint
            : "failed " + (message ?? string.Empty) + " " + docHint;
        result = new RecipeStepExecutionResult(index, "invoke", false, false, failHint);
        Logger.Warning("Velum write_drawing_path failed " + docHint + " " + message);
        return false;
      }

      string status = skipped ? "skipped_unchanged " : "written ";
      if (!string.IsNullOrWhiteSpace(drawingPath))
        status += drawingPath + " ";
      result = new RecipeStepExecutionResult(index, "invoke", true, skipped, status + docHint);
      Logger.Info("Velum write_drawing_path " + status + docHint);
      return true;
    }

    /// <summary>
    /// set_custom_property_if_part — только для SLDPRT (политика флагов при Save детали).
    /// </summary>
    public static bool TryExecuteSetCustomPropertyIfPart(
        int index,
        IReadOnlyDictionary<string, string> parameters,
        ModelDoc2 modelDoc,
        IReadOnlyDictionary<string, string> templateContext,
        out RecipeStepExecutionResult result)
    {
      if (modelDoc == null)
      {
        result = new RecipeStepExecutionResult(index, "invoke", false, false, "model_doc_null");
        return false;
      }

      if (modelDoc.GetType() != (int)swDocumentTypes_e.swDocPART)
      {
        result = new RecipeStepExecutionResult(
            index,
            "invoke",
            true,
            true,
            "skipped_not_part");
        Logger.Info("Velum set_custom_property_if_part skipped (not SLDPRT)");
        return true;
      }

      return RecipeExecutorHandlers.TryExecuteSetCustomProperty(
          index,
          parameters,
          modelDoc,
          templateContext,
          out result);
    }

    /// <summary>
    /// set_custom_property_if_part_or_assembly — SLDPRT/SLDASM (общие флаги документа).
    /// </summary>
    public static bool TryExecuteSetCustomPropertyIfPartOrAssembly(
        int index,
        IReadOnlyDictionary<string, string> parameters,
        ModelDoc2 modelDoc,
        IReadOnlyDictionary<string, string> templateContext,
        out RecipeStepExecutionResult result)
    {
      if (modelDoc == null)
      {
        result = new RecipeStepExecutionResult(index, "invoke", false, false, "model_doc_null");
        return false;
      }

      int docType = modelDoc.GetType();
      if (docType != (int)swDocumentTypes_e.swDocPART
          && docType != (int)swDocumentTypes_e.swDocASSEMBLY)
      {
        result = new RecipeStepExecutionResult(
            index,
            "invoke",
            true,
            true,
            "skipped_not_part_or_assembly");
        Logger.Info("Velum set_custom_property_if_part_or_assembly skipped (not SLDPRT/SLDASM)");
        return true;
      }

      return RecipeExecutorHandlers.TryExecuteSetCustomProperty(
          index,
          parameters,
          modelDoc,
          templateContext,
          out result);
    }

    private static string TryGetDocumentHint(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return "(no document)";

      try
      {
        string title = modelDoc.GetTitle();
        if (!string.IsNullOrWhiteSpace(title))
          return "doc=" + title;
      }
      catch
      {
      }

      return "(document)";
    }
  }
}
