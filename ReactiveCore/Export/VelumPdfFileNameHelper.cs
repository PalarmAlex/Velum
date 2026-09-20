using System;
using System.IO;
using ISIDA.Common;
using SolidWorks.Interop.sldworks;
using Velum.Configuration;
using Velum.ReactiveCore;

namespace Velum.ReactiveCore.Export
{
  /// <summary>Имя PDF по чертежу, каталог вывода и запись свойства «Путь pdf».</summary>
  internal static class VelumPdfFileNameHelper
  {
    internal static string ResolveDrawingBaseFileName(ModelDoc2 drawingDoc)
    {
      if (drawingDoc == null)
        return string.Empty;

      try
      {
        string path = drawingDoc.GetPathName();
        if (!string.IsNullOrWhiteSpace(path))
        {
          string fromPath = Path.GetFileNameWithoutExtension(path);
          if (!string.IsNullOrWhiteSpace(fromPath))
            return SanitizeFileName(fromPath);
        }
      }
      catch
      {
      }

      try
      {
        string title = drawingDoc.GetTitle();
        if (!string.IsNullOrWhiteSpace(title))
        {
          if (title.EndsWith(".slddrw", StringComparison.OrdinalIgnoreCase))
            title = Path.GetFileNameWithoutExtension(title);

          return SanitizeFileName(title);
        }
      }
      catch
      {
      }

      return string.Empty;
    }

    internal static string TryResolveDefaultOutputFolder(ModelDoc2 drawingDoc)
    {
      if (drawingDoc != null)
      {
        string fromDocument = TryResolveOutputFolderFromSavedDocument(drawingDoc);
        if (!string.IsNullOrWhiteSpace(fromDocument))
        {
          VelumAppConfig.SetPdfDefaultOutputFolder(fromDocument);
          return fromDocument;
        }
      }

      string savedFolder = (VelumAppConfig.PdfDefaultOutputFolder ?? string.Empty).Trim();
      if (!string.IsNullOrWhiteSpace(savedFolder) && Directory.Exists(savedFolder))
        return savedFolder;

      return string.Empty;
    }

    private static string TryResolveOutputFolderFromSavedDocument(ModelDoc2 drawingDoc)
    {
      if (drawingDoc == null || !TryHasSavedDocumentPath(drawingDoc))
        return string.Empty;

      CustomPropertyManager cpm =
          VelumRecipeSolidWorksCustomProperties.TryGetManager(drawingDoc, "document");
      if (cpm != null &&
          VelumRecipeSolidWorksCustomProperties.TryGetValue(
              cpm,
              VelumExportDocumentationProperties.PdfPath,
              out string pathValue) &&
          !string.IsNullOrWhiteSpace(pathValue))
      {
        // Свойство может хранить относительный путь — достраиваем префикс корневого каталога.
        string trimmed = VelumRelativeDocumentPathResolver.ToFull(pathValue.Trim());
        if (Directory.Exists(trimmed))
          return trimmed;

        try
        {
          string dir = Path.GetDirectoryName(trimmed);
          if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
            return dir;
        }
        catch
        {
        }
      }

      try
      {
        string drawingPath = drawingDoc.GetPathName();
        if (!string.IsNullOrWhiteSpace(drawingPath))
        {
          string dir = Path.GetDirectoryName(drawingPath);
          if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
            return dir;
        }
      }
      catch
      {
      }

      return string.Empty;
    }

    private static bool TryHasSavedDocumentPath(ModelDoc2 modelDoc)
    {
      try
      {
        return !string.IsNullOrWhiteSpace(modelDoc?.GetPathName());
      }
      catch
      {
        return false;
      }
    }

    internal static bool TryValidateBaseFileName(string baseName, out string message)
    {
      message = string.Empty;
      if (string.IsNullOrWhiteSpace(baseName))
      {
        message = "Не удалось определить имя чертежа для файла PDF.";
        return false;
      }

      if (baseName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
      {
        message = "Имя файла содержит недопустимые символы.";
        return false;
      }

      return true;
    }

    /// <summary>
    /// Если логическое свойство «Нужен pdf» отсутствует — создаёт его со значением
    /// из настроек (<see cref="VelumAppConfig.NeedPdfDefault"/>).
    /// </summary>
    internal static bool EnsureNeedPdfFlag(ModelDoc2 drawingDoc, out string message)
    {
      message = string.Empty;
      if (drawingDoc == null)
      {
        message = "model_doc_null";
        return false;
      }

      CustomPropertyManager cpm =
          VelumRecipeSolidWorksCustomProperties.TryGetManager(drawingDoc, "document");
      if (cpm == null)
      {
        message = "property_manager_unavailable";
        return false;
      }

      if (VelumRecipeSolidWorksCustomProperties.TryGetBooleanValue(
              cpm,
              VelumExportDocumentationProperties.NeedPdf,
              out _))
        return true;

      string flag = VelumAppConfig.NeedPdfDefault
          ? VelumExportDocumentationProperties.FlagYes
          : VelumExportDocumentationProperties.FlagNo;

      return VelumRecipeSolidWorksCustomProperties.TrySetValue(
          cpm,
          VelumExportDocumentationProperties.NeedPdf,
          flag,
          "always",
          VelumSolidCustomPropertyTypes.TypeKeyBoolean,
          out bool skipped,
          out message) && !skipped;
    }

    /// <summary>
    /// Максимальное число попыток записи свойства «Путь pdf» (защита от нестабильности COM
    /// после SaveAs PDF: GetNames/Set2/Add3 могут упасть в dirty-состоянии).
    /// </summary>
    private const int PdfPathWriteMaxRetries = 3;

    /// <summary>
    /// Задержка между попытками записи свойства «Путь pdf» (мс).
    /// </summary>
    private const int PdfPathWriteRetryDelayMs = 150;

    internal static bool TryWritePdfPathProperty(ModelDoc2 drawingDoc, string fullPath, out string message)
    {
      message = string.Empty;
      if (drawingDoc == null || string.IsNullOrWhiteSpace(fullPath))
      {
        message = "model_or_path_missing";
        return false;
      }

      CustomPropertyManager cpm =
          VelumRecipeSolidWorksCustomProperties.TryGetManager(drawingDoc, "document");
      if (cpm == null)
      {
        message = "property_manager_unavailable";
        return false;
      }

      for (int attempt = 0; attempt < PdfPathWriteMaxRetries; attempt++)
      {
        bool ok = VelumRecipeSolidWorksCustomProperties.TrySetValue(
            cpm,
            VelumExportDocumentationProperties.PdfPath,
            // Хранится относительный путь (срез по префиксу корневого каталога).
            VelumRelativeDocumentPathResolver.ToStored(fullPath),
            "always",
            VelumSolidCustomPropertyTypes.TypeKeyText,
            out bool skipped,
            out message);

        if (ok && !skipped)
          return true;

        if (attempt < PdfPathWriteMaxRetries - 1)
        {
          Logger.Info("Velum PDF export: retry write property (attempt " +
              (attempt + 1).ToString() + "/" + PdfPathWriteMaxRetries.ToString() + "): " + message);
          System.Threading.Thread.Sleep(PdfPathWriteRetryDelayMs);
        }
      }

      return false;
    }

    internal static void PersistExportSettings(string outputFolder)
    {
      string folder = (outputFolder ?? string.Empty).Trim();
      if (!string.IsNullOrWhiteSpace(folder))
        VelumAppConfig.SetPdfDefaultOutputFolder(folder);
    }

    private static string SanitizeFileName(string display)
    {
      string value = (display ?? string.Empty).Trim();
      if (value.Length == 0)
        return string.Empty;

      foreach (char c in Path.GetInvalidFileNameChars())
        value = value.Replace(c, '_');

      return value.Trim();
    }
  }
}
