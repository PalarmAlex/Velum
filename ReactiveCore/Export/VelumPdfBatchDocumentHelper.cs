using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Velum.ReactiveCore;
using Xarial.XCad.SolidWorks;

namespace Velum.ReactiveCore.Export
{
  /// <summary>Открытие/закрытие чертежей для пакетной диагностики и экспорта PDF.</summary>
  internal static class VelumPdfBatchDocumentHelper
  {
    private const int SwFileWithSameTitleAlreadyOpenError = (int)swFileLoadError_e.swFileWithSameTitleAlreadyOpen;

    internal static HashSet<string> CollectOpenDrawingPaths(ISwApplication swApp)
    {
      var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      if (swApp?.Sw == null)
        return paths;

      try
      {
        ModelDoc2 doc = swApp.Sw.GetFirstDocument() as ModelDoc2;
        while (doc != null)
        {
          if (doc.GetType() == (int)swDocumentTypes_e.swDocDRAWING)
          {
            string normalized = TryNormalizeDrawingPath(doc.GetPathName());
            if (!string.IsNullOrWhiteSpace(normalized))
              paths.Add(normalized);
          }

          doc = doc.GetNext() as ModelDoc2;
        }
      }
      catch
      {
      }

      return paths;
    }

    internal static string TryGetActiveDrawingPath(ISwApplication swApp)
    {
      if (swApp?.Sw == null)
        return string.Empty;

      try
      {
        ModelDoc2 active = swApp.Sw.IActiveDoc2 as ModelDoc2;
        if (active == null || active.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
          return string.Empty;

        return TryNormalizeDrawingPath(active.GetPathName());
      }
      catch
      {
        return string.Empty;
      }
    }

    internal static ModelDoc2 TryOpenDrawingSilent(ISwApplication swApp, string drawingPath, out string error)
    {
      error = string.Empty;
      if (swApp?.Sw == null)
      {
        error = "SolidWorks недоступен";
        return null;
      }

      if (string.IsNullOrWhiteSpace(drawingPath) || !File.Exists(drawingPath))
      {
        error = "Файл не найден";
        return null;
      }

      ModelDoc2 existing = TryFindOpenDrawingByPath(swApp, drawingPath);
      if (existing != null)
        return existing;

      try
      {
        int errors = 0;
        int warnings = 0;
        int options = (int)swOpenDocOptions_e.swOpenDocOptions_Silent;
        ModelDoc2 opened = swApp.Sw.OpenDoc6(
            drawingPath,
            (int)swDocumentTypes_e.swDocDRAWING,
            options,
            string.Empty,
            ref errors,
            ref warnings) as ModelDoc2;

        if (opened != null)
          return opened;

        existing = TryFindOpenDrawingByPath(swApp, drawingPath);
        if (existing != null)
          return existing;

        if (errors == SwFileWithSameTitleAlreadyOpenError)
          error = "Чертёж уже открыт в SolidWorks";
        else
          error = "OpenDoc6 errors=" + errors + " warnings=" + warnings;
        return null;
      }
      catch (Exception ex)
      {
        error = ex.Message;
        return null;
      }
    }

    internal static void TryReleaseDrawingAfterBatch(
        ISwApplication swApp,
        ModelDoc2 modelDoc,
        string drawingPath,
        bool persistChanges,
        ISet<string> keepOpenDrawingPaths)
    {
      if (modelDoc == null)
        return;

      try
      {
        if (persistChanges)
          TrySaveDrawingSilent(modelDoc, out _);

        string normalized = TryNormalizeDrawingPath(drawingPath);
        if (!string.IsNullOrWhiteSpace(normalized) &&
            keepOpenDrawingPaths != null &&
            keepOpenDrawingPaths.Contains(normalized))
          return;

        string title = modelDoc.GetTitle();
        if (!string.IsNullOrWhiteSpace(title) && swApp?.Sw != null)
          swApp.Sw.CloseDoc(title);
      }
      catch
      {
      }
    }

    /// <summary>
    /// Keep-open после PDF-экспорта: Save сдвигает GetUpdateStamp; ModifyNotify/пульс во время
    /// Save вне suppress успевают записать pending, а проверка через absorb маскирует outdated.
    /// Весь Save+дожим — под suppress; успех только по сырым штампам без absorb.
    /// Вызывающий код после этого обычно закрывает документ (свойства уже на диске).
    /// </summary>
    internal static void TryReconcileKeepOpenDrawingAfterPdfExport(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return;

      VelumExportDocumentationGeometryStampHelper.RunWithGeometryPendingStampSyncSuppressed(() =>
      {
        // Свойства PDF могли записаться без GetSaveFlag — всё равно уводим на диск.
        TrySaveDrawingSilent(modelDoc, out _, force: true);

        for (int attempt = 0; attempt < 6; attempt++)
        {
          VelumExportDocumentationGeometryStampHelper.TryFinalizePdfExportStamps(modelDoc, out _);

          if (TryIsDrawingDirty(modelDoc))
            TrySaveDrawingSilent(modelDoc, out _);

          // Save / FileSavePostNotify могли снова сдвинуть GetUpdateStamp — догоняем.
          VelumExportDocumentationGeometryStampHelper.TryFinalizePdfExportStamps(modelDoc, out _);

          if (TryIsDrawingDirty(modelDoc))
          {
            TrySaveDrawingSilent(modelDoc, out _);
            VelumExportDocumentationGeometryStampHelper.TryFinalizePdfExportStamps(modelDoc, out _);
          }

          if (!TryIsPdfOutdatedRawNow(modelDoc) && !TryIsDrawingDirty(modelDoc))
          {
            VelumExportDocumentationGeometryStampHelper.CommitPdfExportGeometryBaseline(modelDoc);
            return;
          }
        }

        // Не сошлось: absorb до Save вне модалки (иначе метрика залипает красной).
        VelumExportDocumentationGeometryStampHelper.TryFinalizePdfExportStamps(modelDoc, out _);
        VelumExportDocumentationGeometryStampHelper.ArmAfterPdfExport(modelDoc);
        TrySaveDrawingSilent(modelDoc, out _, force: true);
      });
    }

    private static bool TryIsDrawingDirty(ModelDoc2 modelDoc)
    {
      try
      {
        return modelDoc != null && modelDoc.GetSaveFlag();
      }
      catch
      {
        return false;
      }
    }

    private static bool TryIsPdfOutdatedRawNow(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return false;

      if (!VelumExportDocumentationGeometryStampHelper.TryGetCurrentUpdateStamp(
              modelDoc,
              out int currentStamp))
        currentStamp = 0;

      if (!VelumExportDocumentationGeometryStampHelper.TryReadStoredUpdateStamp(
              modelDoc,
              VelumExportDocumentationProperties.PdfGeometryUpdateStamp,
              out int exportStamp))
        return true;

      return VelumExportDocumentationGeometryStampHelper.TryIsPdfOutdatedIgnoringAbsorb(
          modelDoc,
          currentStamp,
          exportStamp);
    }

    internal static void TryCloseOpenDrawingsByPaths(
        ISwApplication swApp,
        IReadOnlyList<string> drawingPaths,
        ISet<string> keepOpenDrawingPaths)
    {
      if (swApp?.Sw == null || drawingPaths == null || drawingPaths.Count == 0)
        return;

      for (int i = 0; i < drawingPaths.Count; i++)
      {
        ModelDoc2 openDoc = TryFindOpenDrawingByPath(swApp, drawingPaths[i]);
        if (openDoc != null)
          TryReleaseDrawingAfterBatch(swApp, openDoc, drawingPaths[i], persistChanges: false, keepOpenDrawingPaths);
      }
    }

    internal static bool TrySaveDrawingSilent(ModelDoc2 modelDoc, out string error, bool force = false)
    {
      error = string.Empty;
      if (modelDoc == null)
      {
        error = "Нет документа";
        return false;
      }

      try
      {
        if (!force && !modelDoc.GetSaveFlag())
          return true;

        int errors = 0;
        int warnings = 0;
        bool saved = modelDoc.Save3(
            (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
            ref errors,
            ref warnings);
        if (!saved)
        {
          error = "Save3 errors=" + errors + " warnings=" + warnings;
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

    internal static ModelDoc2 TryFindOpenDrawingByPath(ISwApplication swApp, string drawingPath)
    {
      if (swApp?.Sw == null || string.IsNullOrWhiteSpace(drawingPath))
        return null;

      string expectedPath = TryNormalizeDrawingPath(drawingPath);
      if (string.IsNullOrWhiteSpace(expectedPath))
        return null;

      ModelDoc2 fromApi = TryGetOpenDocumentByPath(swApp, expectedPath);
      if (fromApi != null)
        return fromApi;

      try
      {
        ModelDoc2 active = swApp.Sw.IActiveDoc2 as ModelDoc2;
        if (active != null &&
            active.GetType() == (int)swDocumentTypes_e.swDocDRAWING &&
            PathsLikelySame(expectedPath, active.GetPathName()))
          return active;

        ModelDoc2 doc = swApp.Sw.GetFirstDocument() as ModelDoc2;
        while (doc != null)
        {
          if (doc.GetType() == (int)swDocumentTypes_e.swDocDRAWING &&
              PathsLikelySame(expectedPath, doc.GetPathName()))
            return doc;

          doc = doc.GetNext() as ModelDoc2;
        }
      }
      catch
      {
      }

      return null;
    }

    internal static bool TryReadNeedPdf(ModelDoc2 modelDoc, out bool needPdf)
    {
      needPdf = false;
      CustomPropertyManager cpm = VelumRecipeSolidWorksCustomProperties.TryGetManager(modelDoc, "document");
      if (cpm == null)
        return false;

      if (!VelumRecipeSolidWorksCustomProperties.TryGetBooleanValue(
              cpm,
              VelumExportDocumentationProperties.NeedPdf,
              out bool? flag))
        return false;

      needPdf = flag ?? false;
      return true;
    }

    internal static bool TrySetNeedPdf(ModelDoc2 modelDoc, bool needPdf, out string error)
    {
      error = string.Empty;
      if (modelDoc == null)
      {
        error = "Нет документа";
        return false;
      }

      CustomPropertyManager cpm = VelumRecipeSolidWorksCustomProperties.TryGetManager(modelDoc, "document");
      if (cpm == null)
      {
        error = "Менеджер свойств недоступен";
        return false;
      }

      string value = needPdf
          ? VelumExportDocumentationProperties.FlagYes
          : VelumExportDocumentationProperties.FlagNo;

      bool ok = VelumRecipeSolidWorksCustomProperties.TrySetValue(
          cpm,
          VelumExportDocumentationProperties.NeedPdf,
          value,
          "always",
          VelumSolidCustomPropertyTypes.TypeKeyBoolean,
          out bool skipped,
          out string message);
      if (!ok)
      {
        error = string.IsNullOrWhiteSpace(message) ? "Не удалось записать свойство" : message;
        return false;
      }

      if (skipped)
      {
        error = "Запись свойства пропущена";
        return false;
      }

      return true;
    }

    internal static bool TryActivateOrOpenDrawingVisible(ISwApplication swApp, string drawingPath, out string error)
    {
      error = string.Empty;
      if (swApp?.Sw == null)
      {
        error = "SolidWorks недоступен";
        return false;
      }

      if (string.IsNullOrWhiteSpace(drawingPath) || !File.Exists(drawingPath))
      {
        error = "Файл не найден";
        return false;
      }

      ModelDoc2 existing = TryFindOpenDrawingByPath(swApp, drawingPath);
      if (existing != null)
        return TryActivateModelDoc(swApp, existing, out error);

      try
      {
        int errors = 0;
        int warnings = 0;
        ModelDoc2 opened = swApp.Sw.OpenDoc6(
            drawingPath,
            (int)swDocumentTypes_e.swDocDRAWING,
            0,
            string.Empty,
            ref errors,
            ref warnings) as ModelDoc2;

        if (opened != null)
          return true;

        existing = TryFindOpenDrawingByPath(swApp, drawingPath);
        if (existing != null)
          return TryActivateModelDoc(swApp, existing, out error);

        if (errors == SwFileWithSameTitleAlreadyOpenError)
          error = "Чертёж уже открыт в SolidWorks";
        else
          error = "OpenDoc6 errors=" + errors + " warnings=" + warnings;
        return false;
      }
      catch (Exception ex)
      {
        error = ex.Message;
        return false;
      }
    }

    private static bool TryActivateModelDoc(ISwApplication swApp, ModelDoc2 modelDoc, out string error)
    {
      error = string.Empty;
      if (modelDoc == null)
      {
        error = "Нет документа";
        return false;
      }

      try
      {
        string title = modelDoc.GetTitle();
        if (string.IsNullOrWhiteSpace(title))
        {
          error = "Не удалось определить заголовок документа";
          return false;
        }

        int activateErrors = 0;
        swApp.Sw.ActivateDoc3(title, true, (int)swRebuildOnActivation_e.swDontRebuildActiveDoc, ref activateErrors);
        return true;
      }
      catch (Exception ex)
      {
        error = ex.Message;
        return false;
      }
    }

    internal static string TryReadPdfPath(ModelDoc2 modelDoc)
    {
      return VelumPdfArtifactResolver.TryReadPdfPathProperty(modelDoc);
    }

    internal static string TryNormalizeDrawingPath(string drawingPath)
    {
      if (string.IsNullOrWhiteSpace(drawingPath))
        return string.Empty;

      try
      {
        string fullPath = Path.GetFullPath(drawingPath.Trim());
        string longPath = TryGetLongPath(fullPath);
        return string.IsNullOrWhiteSpace(longPath) ? fullPath : longPath;
      }
      catch
      {
        return string.Empty;
      }
    }

    private static ModelDoc2 TryGetOpenDocumentByPath(ISwApplication swApp, string expectedPath)
    {
      if (swApp?.Sw == null || string.IsNullOrWhiteSpace(expectedPath))
        return null;

      string[] candidates =
      {
        expectedPath,
        TryGetLongPath(expectedPath)
      };

      for (int i = 0; i < candidates.Length; i++)
      {
        string candidate = candidates[i];
        if (string.IsNullOrWhiteSpace(candidate))
          continue;

        try
        {
          object doc = swApp.Sw.GetOpenDocumentByName(candidate);
          ModelDoc2 modelDoc = doc as ModelDoc2;
          if (modelDoc != null &&
              modelDoc.GetType() == (int)swDocumentTypes_e.swDocDRAWING &&
              PathsLikelySame(expectedPath, modelDoc.GetPathName()))
            return modelDoc;
        }
        catch
        {
        }
      }

      string fileName = Path.GetFileName(expectedPath);
      if (string.IsNullOrWhiteSpace(fileName))
        return null;

      try
      {
        object doc = swApp.Sw.GetOpenDocumentByName(fileName);
        ModelDoc2 modelDoc = doc as ModelDoc2;
        if (modelDoc != null &&
            modelDoc.GetType() == (int)swDocumentTypes_e.swDocDRAWING &&
            PathsLikelySame(expectedPath, modelDoc.GetPathName()))
          return modelDoc;
      }
      catch
      {
      }

      return null;
    }

    private static bool PathsLikelySame(string expectedPath, string openPath)
    {
      string normalizedExpected = TryNormalizeDrawingPath(expectedPath);
      string normalizedOpen = TryNormalizeDrawingPath(openPath);
      if (string.IsNullOrWhiteSpace(normalizedExpected) || string.IsNullOrWhiteSpace(normalizedOpen))
        return false;

      if (string.Equals(normalizedExpected, normalizedOpen, StringComparison.OrdinalIgnoreCase))
        return true;

      string expectedLong = TryGetLongPath(normalizedExpected);
      string openLong = TryGetLongPath(normalizedOpen);
      if (!string.IsNullOrWhiteSpace(expectedLong) &&
          !string.IsNullOrWhiteSpace(openLong) &&
          string.Equals(expectedLong, openLong, StringComparison.OrdinalIgnoreCase))
        return true;

      string expectedFile = Path.GetFileName(normalizedExpected);
      string openFile = Path.GetFileName(normalizedOpen);
      if (string.IsNullOrWhiteSpace(expectedFile) ||
          string.IsNullOrWhiteSpace(openFile) ||
          !string.Equals(expectedFile, openFile, StringComparison.OrdinalIgnoreCase))
        return false;

      string expectedDir = Path.GetFileName(Path.GetDirectoryName(normalizedExpected));
      string openDir = Path.GetFileName(Path.GetDirectoryName(normalizedOpen));
      return !string.IsNullOrWhiteSpace(expectedDir) &&
             !string.IsNullOrWhiteSpace(openDir) &&
             string.Equals(expectedDir, openDir, StringComparison.OrdinalIgnoreCase);
    }

    private static string TryGetLongPath(string path)
    {
      if (string.IsNullOrWhiteSpace(path))
        return string.Empty;

      try
      {
        var builder = new StringBuilder(512);
        uint length = GetLongPathName(path.Trim(), builder, (uint)builder.Capacity);
        if (length == 0)
          return string.Empty;
        if (length >= builder.Capacity)
        {
          builder = new StringBuilder((int)length + 1);
          length = GetLongPathName(path.Trim(), builder, (uint)builder.Capacity);
          if (length == 0)
            return string.Empty;
        }

        return builder.ToString();
      }
      catch
      {
        return string.Empty;
      }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetLongPathName(string lpszShortPath, StringBuilder lpszLongPath, uint cchBuffer);
  }
}
