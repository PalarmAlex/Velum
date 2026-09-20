using System;
using System.IO;
using SolidWorks.Interop.sldworks;
using Velum.ReactiveCore;

namespace Velum.ReactiveCore.Export
{
  /// <summary>Результат поиска PDF-артефакта для чертежа.</summary>
  internal sealed class ResolvedPdfArtifact
  {
    internal bool Found { get; set; }

    internal string FullPath { get; set; }

    internal string BaseName { get; set; }

    internal string Reason { get; set; }
  }

  /// <summary>
  /// Единый резолвер PDF: ожидаемое имя = basename чертежа.
  /// «Путь pdf» принимается только если basename файла совпадает с текущим именем чертежа
  /// (после переименования SLDDRW старый PDF не засчитывается).
  /// Иначе: pdfRoot\{BaseName}.pdf → каталог SLDDRW\{BaseName}.pdf.
  /// </summary>
  internal static class VelumPdfArtifactResolver
  {
    internal static ResolvedPdfArtifact Resolve(ModelDoc2 modelDoc, string pdfRoot = null)
    {
      if (modelDoc == null)
      {
        return new ResolvedPdfArtifact
        {
          Found = false,
          Reason = "model_null"
        };
      }

      string baseName = VelumPdfFileNameHelper.ResolveDrawingBaseFileName(modelDoc);
      if (string.IsNullOrWhiteSpace(baseName))
      {
        return new ResolvedPdfArtifact
        {
          Found = false,
          Reason = "drawing_base_missing"
        };
      }

      string storedPath = TryReadPdfPathProperty(modelDoc);
      string resolvedStored = null;
      bool storedResolved = !string.IsNullOrWhiteSpace(storedPath) &&
          TryResolveExistingFile(storedPath, out resolvedStored);
      if (storedResolved && IsSamePdfBaseName(resolvedStored, baseName))
      {
        return new ResolvedPdfArtifact
        {
          Found = true,
          FullPath = resolvedStored,
          BaseName = baseName,
          Reason = "stored_path"
        };
      }

      string root = (pdfRoot ?? string.Empty).Trim();
      if (root.Length > 0)
      {
        string candidate = Path.Combine(root, baseName + ".pdf");
        if (TryResolveExistingFile(candidate, out string resolvedRoot))
        {
          return new ResolvedPdfArtifact
          {
            Found = true,
            FullPath = resolvedRoot,
            BaseName = baseName,
            Reason = "pdf_root"
          };
        }
      }

      string drawingDir = TryGetDrawingDirectory(modelDoc);
      if (!string.IsNullOrWhiteSpace(drawingDir))
      {
        string candidate = Path.Combine(drawingDir, baseName + ".pdf");
        if (TryResolveExistingFile(candidate, out string resolvedDrawingDir))
        {
          return new ResolvedPdfArtifact
          {
            Found = true,
            FullPath = resolvedDrawingDir,
            BaseName = baseName,
            Reason = "drawing_dir"
          };
        }
      }

      string notFoundReason;
      if (storedResolved)
        notFoundReason = "stored_path_stale";
      else if (string.IsNullOrWhiteSpace(storedPath))
        notFoundReason = "not_found";
      else
        notFoundReason = "stored_path_not_found";

      return new ResolvedPdfArtifact
      {
        Found = false,
        BaseName = baseName,
        Reason = notFoundReason
      };
    }

    /// <summary>Путь PDF в свойстве относится к прежнему имени чертежа.</summary>
    internal static bool HasStoredPdfBaseNameMismatch(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return false;

      string expected = VelumPdfFileNameHelper.ResolveDrawingBaseFileName(modelDoc);
      string stored = TryReadPdfPathProperty(modelDoc);
      if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(stored))
        return false;

      try
      {
        return !string.Equals(
            Path.GetFileNameWithoutExtension(stored.Trim()),
            expected.Trim(),
            StringComparison.OrdinalIgnoreCase);
      }
      catch
      {
        return false;
      }
    }

    private static bool IsSamePdfBaseName(string pdfPath, string expectedBaseName)
    {
      if (string.IsNullOrWhiteSpace(pdfPath) || string.IsNullOrWhiteSpace(expectedBaseName))
        return false;

      try
      {
        string actual = Path.GetFileNameWithoutExtension(pdfPath);
        return string.Equals(actual, expectedBaseName.Trim(), StringComparison.OrdinalIgnoreCase);
      }
      catch
      {
        return false;
      }
    }

    internal static bool TryClearPdfExportMetadata(ModelDoc2 modelDoc, out string message)
    {
      message = string.Empty;
      if (modelDoc == null)
      {
        message = "model_missing";
        return false;
      }

      bool ok = true;
      string localMessage = string.Empty;
      VelumExportDocumentationGeometryStampHelper.RunWithGeometryPendingStampSyncSuppressed(() =>
      {
        CustomPropertyManager cpm =
            VelumRecipeSolidWorksCustomProperties.TryGetManager(modelDoc, "document");
        if (cpm == null)
        {
          localMessage = "property_manager_unavailable";
          ok = false;
          return;
        }

        ok = TryClearDocumentProperty(
            cpm,
            VelumExportDocumentationProperties.PdfPath,
            out string pathMessage);
        if (!string.IsNullOrWhiteSpace(pathMessage))
          localMessage = pathMessage;

        ok &= VelumExportDocumentationGeometryStampHelper.TryClearGeometryStamp(
            modelDoc,
            VelumExportDocumentationProperties.PdfGeometryUpdateStamp,
            null,
            out string exportMessage);
        if (!string.IsNullOrWhiteSpace(exportMessage))
          localMessage = exportMessage;

        ok &= VelumExportDocumentationGeometryStampHelper.TryClearGeometryStamp(
            modelDoc,
            VelumExportDocumentationProperties.PdfGeometryPendingStamp,
            null,
            out string pendingMessage);
        if (!string.IsNullOrWhiteSpace(pendingMessage))
          localMessage = pendingMessage;
      });

      if (!string.IsNullOrWhiteSpace(localMessage))
        message = localMessage;

      return ok;
    }

    internal static string TryReadPdfPathProperty(ModelDoc2 modelDoc)
    {
      return VelumRelativeDocumentPathResolver.ToFull(TryReadPdfPathPropertyRaw(modelDoc));
    }

    /// <summary>
    /// Читает свойство «Путь pdf» без достройки префикса — ровно как хранится в документе
    /// (может быть относительным). Используется зеркалом реестра и мигратором.
    /// </summary>
    internal static string TryReadPdfPathPropertyRaw(ModelDoc2 modelDoc)
    {
      CustomPropertyManager cpm =
          VelumRecipeSolidWorksCustomProperties.TryGetManager(modelDoc, "document");
      if (cpm == null)
        return string.Empty;

      if (!VelumRecipeSolidWorksCustomProperties.TryGetValue(
              cpm,
              VelumExportDocumentationProperties.PdfPath,
              out string raw))
        return string.Empty;

      return (raw ?? string.Empty).Trim();
    }

    private static bool TryClearDocumentProperty(
        CustomPropertyManager cpm,
        string propertyName,
        out string message)
    {
      message = string.Empty;
      if (cpm == null || string.IsNullOrWhiteSpace(propertyName))
      {
        message = "property_manager_unavailable";
        return false;
      }

      return VelumRecipeSolidWorksCustomProperties.TrySetValue(
          cpm,
          propertyName,
          string.Empty,
          "always",
          VelumSolidCustomPropertyTypes.TypeKeyText,
          out bool skipped,
          out message) && !skipped;
    }

    private static string TryGetDrawingDirectory(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return string.Empty;

      try
      {
        string drawingPath = modelDoc.GetPathName();
        if (string.IsNullOrWhiteSpace(drawingPath))
          return string.Empty;

        string dir = Path.GetDirectoryName(drawingPath);
        return string.IsNullOrWhiteSpace(dir) ? string.Empty : dir;
      }
      catch
      {
        return string.Empty;
      }
    }

    private static bool TryResolveExistingFile(string path, out string resolvedPath)
    {
      resolvedPath = null;
      if (string.IsNullOrWhiteSpace(path))
        return false;

      try
      {
        string candidate = System.Environment.ExpandEnvironmentVariables(path.Trim());
        if (!Path.IsPathRooted(candidate))
          candidate = VelumRelativeDocumentPathResolver.ToFull(candidate);

        if (File.Exists(candidate))
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
