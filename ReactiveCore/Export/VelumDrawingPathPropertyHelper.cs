using System;
using System.IO;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Velum.ReactiveCore;

namespace Velum.ReactiveCore.Export
{
  /// <summary>Чтение и идемпотентная запись свойства «путь чертежа».</summary>
  internal static class VelumDrawingPathPropertyHelper
  {
    internal static string TryRead(ModelDoc2 modelDoc)
    {
      return VelumRelativeDocumentPathResolver.ToFull(TryReadRaw(modelDoc));
    }

    /// <summary>
    /// Читает свойство «путь чертежа» без достройки префикса — ровно как хранится в документе
    /// (может быть относительным). Используется мигратором и зеркалом реестра.
    /// </summary>
    internal static string TryReadRaw(ModelDoc2 modelDoc)
    {
      CustomPropertyManager cpm =
          VelumRecipeSolidWorksCustomProperties.TryGetManager(modelDoc, "document");
      return cpm != null &&
             VelumRecipeSolidWorksCustomProperties.TryGetValue(
                 cpm, VelumExportDocumentationProperties.DrawingPath, out string value)
          ? (value ?? string.Empty).Trim()
          : string.Empty;
    }

    /// <summary>Читает свойство «Нужен чертеж» у детали/сборки.</summary>
    internal static bool TryReadNeedDrawing(ModelDoc2 modelDoc, out bool needDrawing)
    {
      needDrawing = false;
      CustomPropertyManager cpm =
          VelumRecipeSolidWorksCustomProperties.TryGetManager(modelDoc, "document");
      if (cpm == null)
        return false;

      if (!VelumRecipeSolidWorksCustomProperties.TryGetBooleanValue(
              cpm,
              VelumExportDocumentationProperties.NeedDrawing,
              out bool? flag))
        return false;

      needDrawing = flag ?? false;
      return true;
    }

    /// <summary>
    /// Если «Нужен чертеж» отсутствует — создаёт логическое свойство со значением Yes.
    /// Существующее не меняет.
    /// </summary>
    internal static bool EnsureNeedDrawingFlag(ModelDoc2 modelDoc, out string message)
    {
      return VelumNeedDrawingPropertyWriter.EnsureYes(modelDoc, out message);
    }

    /// <summary>
    /// Пишет «Нужен чертеж»: если свойство есть — обновляет значение;
    /// если нет — создаёт логическое Yes/No.
    /// </summary>
    internal static bool TrySetNeedDrawing(ModelDoc2 modelDoc, bool needDrawing, out string error)
    {
      error = string.Empty;
      if (modelDoc == null)
      {
        error = "Нет документа";
        return false;
      }

      if (!IsPartOrAssemblyDoc(modelDoc))
      {
        error = "Свойство «Нужен чертеж» задаётся только для детали/сборки";
        return false;
      }

      return VelumNeedDrawingPropertyWriter.TryWrite(modelDoc, needDrawing, out error);
    }

    /// <summary>
    /// Открывает деталь/сборку при необходимости, обеспечивает «Нужен чертеж» через Save,
    /// пишет значение, сохраняет и закрывает документ, если открывали мы.
    /// </summary>
    internal static bool TrySetNeedDrawingToPath(
        ISldWorks swApp,
        string modelPath,
        bool needDrawing,
        out string error)
    {
      error = string.Empty;
      if (swApp == null || string.IsNullOrWhiteSpace(modelPath))
      {
        error = "SolidWorks или путь недоступны";
        return false;
      }

      string path = modelPath.Trim();
      string ext = Path.GetExtension(path);
      if (!string.Equals(ext, ".sldprt", StringComparison.OrdinalIgnoreCase) &&
          !string.Equals(ext, ".sldasm", StringComparison.OrdinalIgnoreCase))
      {
        error = "Ожидается путь детали (.sldprt) или сборки (.sldasm)";
        return false;
      }

      if (!File.Exists(path))
      {
        error = "Файл не найден";
        return false;
      }

      bool alreadyOpen = TryFindOpenModelByPath(swApp, path) != null;
      ModelDoc2 modelDoc = TryFindOpenModelByPath(swApp, path);
      bool openedHere = false;

      if (modelDoc == null)
      {
        int openErrors = 0;
        int openWarnings = 0;
        try
        {
          modelDoc = swApp.OpenDoc6(
              path,
              GetDocumentType(path),
              (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
              string.Empty,
              ref openErrors,
              ref openWarnings) as ModelDoc2;
          openedHere = modelDoc != null;
        }
        catch (Exception ex)
        {
          error = ex.Message;
          return false;
        }

        if (modelDoc == null)
        {
          modelDoc = TryFindOpenModelByPath(swApp, path);
          if (modelDoc == null)
          {
            error = "OpenDoc6 errors=" + openErrors + " warnings=" + openWarnings;
            return false;
          }
        }
      }

      try
      {
        try
        {
          if (modelDoc.IsOpenedViewOnly())
          {
            error = "Документ открыт только для просмотра";
            return false;
          }
        }
        catch
        {
        }

        try
        {
          int activateErrors = 0;
          swApp.ActivateDoc3(
              modelDoc.GetTitle() ?? string.Empty,
              true,
              (int)swRebuildOnActivation_e.swDontRebuildActiveDoc,
              ref activateErrors);
        }
        catch
        {
        }

        // Нет свойства → Save (FileSaveNotify создаёт Yes), затем пишем нужное значение.
        if (!TryReadNeedDrawing(modelDoc, out _))
        {
          EnsureNeedDrawingFlag(modelDoc, out _);
          TrySaveSilent(modelDoc);
        }

        if (!TrySetNeedDrawing(modelDoc, needDrawing, out error))
          return false;

        TrySaveSilent(modelDoc);

        // Зеркало реестра: sync с диска после Save (если форма реестра не открыта).
        try
        {
          Velum.UI.ProductRegistry.VelumProductRegistryExportMetaSync.TrySyncOpenDocumentFromDisk(modelDoc);
        }
        catch
        {
        }

        return true;
      }
      finally
      {
        if (openedHere && !alreadyOpen && modelDoc != null)
        {
          try
          {
            string title = modelDoc.GetTitle();
            if (!string.IsNullOrWhiteSpace(title))
              swApp.CloseDoc(title);
          }
          catch
          {
          }
        }
      }
    }

    private static void TrySaveSilent(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return;
      try
      {
        int saveErrors = 0;
        int saveWarnings = 0;
        modelDoc.Save3(
            (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
            ref saveErrors,
            ref saveWarnings);
      }
      catch
      {
      }
    }

    private static bool IsPartOrAssemblyDoc(ModelDoc2 modelDoc)
    {
      try
      {
        int type = modelDoc.GetType();
        return type == (int)swDocumentTypes_e.swDocPART ||
               type == (int)swDocumentTypes_e.swDocASSEMBLY;
      }
      catch
      {
        return false;
      }
    }

    /// <summary>
    /// true, если свойство задано и файл по пути существует.
    /// <paramref name="expectedDrawingPath"/> — если задан, путь должен совпадать с ним.
    /// </summary>
    internal static bool IsDrawingPathUsable(
        ModelDoc2 modelDoc,
        string expectedDrawingPath,
        out string storedPath,
        out string reason)
    {
      storedPath = TryRead(modelDoc);
      reason = string.Empty;
      if (string.IsNullOrWhiteSpace(storedPath))
      {
        reason = "empty";
        return false;
      }

      if (!File.Exists(storedPath))
      {
        reason = "missing_file";
        return false;
      }

      if (!string.IsNullOrWhiteSpace(expectedDrawingPath) &&
          !string.Equals(
              NormalizePath(storedPath),
              NormalizePath(expectedDrawingPath),
              StringComparison.OrdinalIgnoreCase))
      {
        reason = "path_mismatch";
        return false;
      }

      return true;
    }

    /// <summary>
    /// Ищет <c>{каталог документа}\{имя документа}.slddrw</c> рядом с сохранённой деталью/сборкой.
    /// </summary>
    internal static string TryFindSiblingDrawingPath(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return string.Empty;

      string modelPath;
      try
      {
        modelPath = (modelDoc.GetPathName() ?? string.Empty).Trim();
      }
      catch
      {
        return string.Empty;
      }

      if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
        return string.Empty;

      string directory;
      string baseName;
      try
      {
        directory = Path.GetDirectoryName(modelPath);
        baseName = Path.GetFileNameWithoutExtension(modelPath);
      }
      catch
      {
        return string.Empty;
      }

      if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(baseName))
        return string.Empty;

      string candidate = Path.Combine(directory, baseName + ".slddrw");
      return File.Exists(candidate) ? candidate : string.Empty;
    }

    /// <summary>
    /// Починка «путь чертежа» у детали/сборки по конвенции одноимённого .slddrw в корне документа.
    /// Не перезаписывает валидный существующий путь к файлу на диске.
    /// </summary>
    internal static bool TryRepairFromSiblingDrawing(
        ModelDoc2 modelDoc,
        out bool skipped,
        out string drawingPath,
        out string message)
    {
      skipped = false;
      drawingPath = string.Empty;
      message = string.Empty;
      if (modelDoc == null)
      {
        message = "model_null";
        return false;
      }

      string stored = TryRead(modelDoc);
      if (!string.IsNullOrWhiteSpace(stored) && File.Exists(stored))
      {
        skipped = true;
        drawingPath = stored;
        message = "unchanged_valid_path";
        return true;
      }

      string sibling = TryFindSiblingDrawingPath(modelDoc);
      if (string.IsNullOrWhiteSpace(sibling))
      {
        message = "sibling_drawing_not_found";
        return false;
      }

      drawingPath = sibling;
      return TryWrite(modelDoc, sibling, out skipped, out message);
    }

    internal static bool TryWrite(ModelDoc2 modelDoc, string drawingPath, out bool skipped, out string message)
    {
      skipped = false;
      message = string.Empty;
      string value = (drawingPath ?? string.Empty).Trim();
      if (modelDoc == null || string.IsNullOrWhiteSpace(value))
      {
        message = "model_or_path_missing";
        return false;
      }

      CustomPropertyManager cpm =
          VelumRecipeSolidWorksCustomProperties.TryGetManager(modelDoc, "document");
      if (cpm == null)
      {
        message = "property_manager_unavailable";
        return false;
      }

      // В свойстве хранится относительный путь (срез по префиксу корневого каталога),
      // при чтении достраивается обратно. Без настройки — абсолютный как раньше.
      string storedValue = VelumRelativeDocumentPathResolver.ToStored(value);
      if (string.Equals(TryReadRaw(modelDoc), storedValue, StringComparison.OrdinalIgnoreCase))
      {
        skipped = true;
        message = "unchanged";
        return true;
      }

      bool ok = VelumRecipeSolidWorksCustomProperties.TrySetValue(
          cpm,
          VelumExportDocumentationProperties.DrawingPath,
          storedValue,
          "always",
          VelumSolidCustomPropertyTypes.TypeKeyText,
          out skipped,
          out message);
      if (ok && !skipped)
        Velum.UI.ProductRegistry.VelumProductRegistryExportMetaSync.TrySyncOpenDocumentFromDisk(modelDoc);
      return ok;
    }

    /// <summary>
    /// Записывает путь в модель по имени файла, тихо открывая и сохраняя её, если
    /// она ещё не загружена в SolidWorks.
    /// </summary>
    internal static bool TryWriteToPath(
        ISldWorks swApp,
        string modelPath,
        string drawingPath,
        out bool skipped,
        out string message)
    {
      skipped = false;
      message = string.Empty;
      if (swApp == null || string.IsNullOrWhiteSpace(modelPath))
      {
        message = "sw_or_model_path_missing";
        return false;
      }

      string path = modelPath.Trim();
      if (!File.Exists(path))
      {
        message = "model_file_missing";
        return false;
      }

      ModelDoc2 modelDoc = TryFindOpenModelByPath(swApp, path);
      bool openedHere = false;
      int openErrors = 0;
      int openWarnings = 0;
      if (modelDoc == null)
      {
        try
        {
          modelDoc = swApp.OpenDoc6(
              path,
              GetDocumentType(path),
              (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
              string.Empty,
              ref openErrors,
              ref openWarnings) as ModelDoc2;
          openedHere = modelDoc != null;
        }
        catch (Exception ex)
        {
          message = ex.Message;
          return false;
        }
      }

      if (modelDoc == null)
      {
        message = "OpenDoc6 errors=" + openErrors + " warnings=" + openWarnings;
        return false;
      }

      try
      {
        bool written = false;
        bool localSkipped = false;
        string localMessage = string.Empty;
        VelumExportDocumentationGeometryStampHelper.RunWithGeometryPendingStampSyncSuppressed(() =>
        {
          written = TryWrite(modelDoc, drawingPath, out localSkipped, out localMessage);
        });
        skipped = localSkipped;
        message = localMessage;
        if (written && !skipped)
        {
          int saveErrors = 0;
          int saveWarnings = 0;
          if (!modelDoc.Save3(
                  (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                  ref saveErrors,
                  ref saveWarnings))
          {
            message = "Save3 errors=" + saveErrors + " warnings=" + saveWarnings;
            return false;
          }
        }
        return written;
      }
      finally
      {
        if (openedHere)
        {
          try
          {
            string title = modelDoc.GetTitle();
            if (!string.IsNullOrWhiteSpace(title))
              swApp.CloseDoc(title);
          }
          catch
          {
          }
        }
      }
    }

    private static int GetDocumentType(string modelPath)
    {
      string extension = Path.GetExtension(modelPath ?? string.Empty);
      return string.Equals(extension, ".sldasm", StringComparison.OrdinalIgnoreCase)
          ? (int)swDocumentTypes_e.swDocASSEMBLY
          : (int)swDocumentTypes_e.swDocPART;
    }

    internal static ModelDoc2 TryFindOpenModelByPath(ISldWorks swApp, string path)
    {
      string expected = NormalizePath(path);
      if (string.IsNullOrWhiteSpace(expected) || swApp == null)
        return null;

      try
      {
        ModelDoc2 doc = swApp.GetFirstDocument() as ModelDoc2;
        while (doc != null)
        {
          int type = doc.GetType();
          if ((type == (int)swDocumentTypes_e.swDocPART ||
               type == (int)swDocumentTypes_e.swDocASSEMBLY) &&
              string.Equals(NormalizePath(doc.GetPathName()), expected, StringComparison.OrdinalIgnoreCase))
            return doc;
          doc = doc.GetNext() as ModelDoc2;
        }
      }
      catch
      {
      }
      return null;
    }

    internal static string NormalizePath(string path)
    {
      if (string.IsNullOrWhiteSpace(path))
        return string.Empty;
      try
      {
        // Относительные хранимые значения достраиваются по префиксу корневого каталога
        // до GetFullPath (иначе путь развернётся от текущего каталога процесса).
        return VelumRelativeDocumentPathResolver.NormalizeForCompare(path);
      }
      catch
      {
        return path.Trim();
      }
    }
  }
}
