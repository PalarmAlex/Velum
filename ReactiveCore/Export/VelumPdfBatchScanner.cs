using System;
using System.Collections.Generic;
using System.IO;
using SolidWorks.Interop.sldworks;
using Velum.ReactiveCore;
using Velum.SolidHomeostasis;
using Xarial.XCad.SolidWorks;

namespace Velum.ReactiveCore.Export
{
  /// <summary>Пакетная диагностика PDF: фаза 1 (ФС) + фаза 2 (SW API).</summary>
  internal static class VelumPdfBatchScanner
  {
    internal const SearchOption DrawingSearchOption = SearchOption.AllDirectories;
    internal const SearchOption PdfSearchOption = SearchOption.AllDirectories;

    internal sealed class ScanRequest
    {
      public string DrawingsRoot { get; set; }

      public string PdfRoot { get; set; }

      public ISwApplication SwApp { get; set; }

      /// <summary>Пути SLDDRW, уже открытые до операции — не закрывать после неё.</summary>
      public ISet<string> KeepOpenDrawingPaths { get; set; }

      /// <summary>При возврате true сканирование прерывается.</summary>
      public Func<bool> IsCancelled { get; set; }
    }

    internal sealed class ScanResult
    {
      public List<VelumPdfBatchDiagnosticRow> Rows { get; } = new List<VelumPdfBatchDiagnosticRow>();

      public List<string> OrphanPdfFiles { get; } = new List<string>();
    }

    internal static ScanResult Run(ScanRequest request)
    {
      var result = new ScanResult();
      string drawingsRoot = (request?.DrawingsRoot ?? string.Empty).Trim();
      string pdfRoot = (request?.PdfRoot ?? string.Empty).Trim();
      if (string.IsNullOrWhiteSpace(drawingsRoot) || !Directory.Exists(drawingsRoot))
        return result;
      if (string.IsNullOrWhiteSpace(pdfRoot) || !Directory.Exists(pdfRoot))
        return result;

      List<string> drawingFiles = CollectDrawingFiles(drawingsRoot);
      List<string> pdfFiles = CollectPdfFiles(pdfRoot);
      var claimedBaseNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

      for (int i = 0; i < drawingFiles.Count; i++)
      {
        if (request?.IsCancelled != null && request.IsCancelled())
          break;

        string baseName = Path.GetFileNameWithoutExtension(drawingFiles[i]);
        if (!string.IsNullOrWhiteSpace(baseName))
          claimedBaseNames.Add(baseName);
        RunPhase2ForDrawing(request, drawingFiles[i], pdfRoot, result.Rows);
      }

      CollectOrphanPdfFiles(pdfFiles, claimedBaseNames, result.Rows, result.OrphanPdfFiles);

      VelumPdfBatchDocumentHelper.TryCloseOpenDrawingsByPaths(
          request.SwApp,
          drawingFiles,
          request.KeepOpenDrawingPaths);

      return result;
    }

    internal static void RefreshDrawingRow(
        ModelDoc2 modelDoc,
        string drawingPath,
        string pdfRoot,
        IList<VelumPdfBatchDiagnosticRow> rows)
    {
      if (modelDoc == null || rows == null || rows.Count == 0)
        return;

      string normalizedDrawingPath = (drawingPath ?? string.Empty).Trim();
      if (normalizedDrawingPath.Length == 0)
        return;

      for (int i = rows.Count - 1; i >= 0; i--)
      {
        VelumPdfBatchDiagnosticRow row = rows[i];
        if (row == null ||
            !string.Equals(row.DrawingPath, normalizedDrawingPath, StringComparison.OrdinalIgnoreCase))
          continue;

        bool hasNeedFlag = VelumPdfBatchDocumentHelper.TryReadNeedPdf(modelDoc, out bool needPdf);
        VelumPdfBatchDiagnosticRow refreshed = ClassifyDrawing(
            null,
            modelDoc,
            row.DrawingPath,
            row.DrawingDisplayName,
            pdfRoot,
            hasNeedFlag,
            needPdf);
        if (refreshed == null)
        {
          rows.RemoveAt(i);
          continue;
        }

        refreshed.Selected = row.Selected;
        rows[i] = refreshed;
      }
    }

    private static void RunPhase2ForDrawing(
        ScanRequest request,
        string drawingPath,
        string pdfRoot,
        List<VelumPdfBatchDiagnosticRow> rows)
    {
      ISwApplication swApp = request?.SwApp;
      string displayName = Path.GetFileName(drawingPath);
      ModelDoc2 modelDoc = VelumPdfBatchDocumentHelper.TryOpenDrawingSilent(swApp, drawingPath, out _);
      if (modelDoc == null)
        return;

      try
      {
        bool hasNeedFlag = VelumPdfBatchDocumentHelper.TryReadNeedPdf(modelDoc, out bool needPdf);
        VelumPdfBatchDiagnosticRow row = ClassifyDrawing(
            swApp,
            modelDoc,
            drawingPath,
            displayName,
            pdfRoot,
            hasNeedFlag,
            needPdf);
        if (row != null)
          rows.Add(row);
      }
      finally
      {
        VelumPdfBatchDocumentHelper.TryReleaseDrawingAfterBatch(
            swApp,
            modelDoc,
            drawingPath,
            persistChanges: false,
            request?.KeepOpenDrawingPaths);
      }
    }

    /// <summary>Публичная обёртка классификации для диагностики из реестра изделия.</summary>
    internal static VelumPdfBatchDiagnosticRow ClassifyDrawingPublic(
        ISwApplication swApp,
        ModelDoc2 modelDoc,
        string drawingPath,
        string displayName,
        string pdfRoot,
        bool hasNeedFlag,
        bool needPdf,
        string sourceModelPath)
    {
      return ClassifyDrawing(swApp, modelDoc, drawingPath, displayName, pdfRoot, hasNeedFlag, needPdf, sourceModelPath);
    }

    private static VelumPdfBatchDiagnosticRow ClassifyDrawing(
        ISwApplication swApp,
        ModelDoc2 modelDoc,
        string drawingPath,
        string displayName,
        string pdfRoot,
        bool hasNeedFlag,
        bool needPdf,
        string sourceModelPath = null)
    {
      ResolvedPdfArtifact artifact = VelumPdfArtifactResolver.Resolve(modelDoc, pdfRoot);
      bool fileFound = artifact.Found;
      bool outdated = false;
      bool pdfNameMismatch = VelumPdfArtifactResolver.HasStoredPdfBaseNameMismatch(modelDoc);

      if (fileFound)
      {
        int currentStamp = 0;
        bool hasCurrentStamp = false;

        // Читаем UpdateStamp из детали-источника (если путь передан), а не из чертежа.
        // Это важно: пользователь может изменить деталь и сохранить, но не перестраивать
        // чертёж — тогда UpdateStamp чертежа не изменится, но деталь всё равно устареет.
        if (!string.IsNullOrWhiteSpace(sourceModelPath) && swApp != null)
        {
          ModelDoc2 sourceModel = VelumDxfBatchDocumentHelper.TryOpenPartSilent(swApp, sourceModelPath, out _);
          if (sourceModel != null)
          {
            hasCurrentStamp =
                VelumExportDocumentationGeometryStampHelper.TryGetCurrentUpdateStamp(
                    sourceModel, out currentStamp);
            string title = sourceModel.GetPathName();
            if (!string.IsNullOrWhiteSpace(title))
            {
              try { swApp.Sw.CloseDoc(title); } catch { }
            }
          }
        }

        if (!hasCurrentStamp)
        {
          hasCurrentStamp =
              VelumExportDocumentationGeometryStampHelper.TryGetCurrentUpdateStamp(
                  modelDoc, out currentStamp);
        }

        if (!VelumExportDocumentationGeometryStampHelper.TryReadStoredUpdateStamp(
                modelDoc,
                VelumExportDocumentationProperties.PdfGeometryUpdateStamp,
                out int storedStamp))
        {
          outdated = true;
        }
        else if (hasCurrentStamp &&
                 VelumExportDocumentationGeometryStampHelper.TryIsPdfOutdated(
                     modelDoc,
                     currentStamp,
                     storedStamp))
        {
          outdated = true;
        }
      }

      VelumPdfBatchRowStatus status;
      string statusText;
      bool selected;

      if (!hasNeedFlag || needPdf)
      {
        if (pdfNameMismatch)
        {
          status = VelumPdfBatchRowStatus.Outdated;
          statusText = "Имя PDF устарело";
          selected = true;
        }
        else if (!fileFound)
        {
          status = VelumPdfBatchRowStatus.NeedExport;
          statusText = "Нужен экспорт";
          selected = true;
        }
        else if (outdated)
        {
          status = VelumPdfBatchRowStatus.Outdated;
          statusText = "Устарел";
          selected = true;
        }
        else
        {
          status = VelumPdfBatchRowStatus.Ok;
          statusText = "OK";
          selected = false;
        }
      }
      else
      {
        if (fileFound)
        {
          status = VelumPdfBatchRowStatus.Junk;
          statusText = "Мусорный PDF";
          selected = true;
        }
        else
        {
          return null;
        }
      }

      return new VelumPdfBatchDiagnosticRow
      {
        DrawingPath = drawingPath,
        DrawingDisplayName = displayName,
        Status = status,
        StatusText = statusText,
        Selected = selected,
        PdfPath = artifact.FullPath
      };
    }

    /// <summary>
    /// Список чертежей в каталоге без открытия в SW (для быстрой загрузки в пакетную форму).
    /// </summary>
    internal static List<string> CollectDrawingFiles(string drawingsRoot)
    {
      var files = new List<string>();
      try
      {
        string root = (drawingsRoot ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
          return files;

        files.AddRange(Directory.EnumerateFiles(root, "*.slddrw", DrawingSearchOption));
      }
      catch
      {
      }

      return files;
    }

    /// <summary>
    /// Строки «Не проверено» по файлам SLDDRW — без диагностики штампов/NeedPdf.
    /// </summary>
    internal static List<VelumPdfBatchDiagnosticRow> BuildUncheckedRows(
        string drawingsRoot,
        string pdfRoot)
    {
      var rows = new List<VelumPdfBatchDiagnosticRow>();
      List<string> files = CollectDrawingFiles(drawingsRoot);
      string pdfFolder = (pdfRoot ?? string.Empty).Trim();
      bool pdfFolderOk = !string.IsNullOrWhiteSpace(pdfFolder) && Directory.Exists(pdfFolder);

      for (int i = 0; i < files.Count; i++)
      {
        string drawingPath = files[i];
        if (string.IsNullOrWhiteSpace(drawingPath))
          continue;

        string baseName = Path.GetFileNameWithoutExtension(drawingPath);
        string pdfPath = string.Empty;
        if (pdfFolderOk && !string.IsNullOrWhiteSpace(baseName))
        {
          string candidate = Path.Combine(pdfFolder, baseName + ".pdf");
          if (File.Exists(candidate))
            pdfPath = candidate;
        }

        rows.Add(new VelumPdfBatchDiagnosticRow
        {
          DrawingPath = drawingPath,
          DrawingDisplayName = Path.GetFileName(drawingPath),
          Status = VelumPdfBatchRowStatus.Unchecked,
          StatusText = "Не проверено",
          Selected = true,
          PdfPath = pdfPath
        });
      }

      return rows;
    }

    private static List<string> CollectPdfFiles(string pdfRoot)
    {
      var files = new List<string>();
      try
      {
        files.AddRange(Directory.EnumerateFiles(pdfRoot, "*.pdf", PdfSearchOption));
      }
      catch
      {
      }

      return files;
    }

    private static void CollectOrphanPdfFiles(
        IReadOnlyList<string> pdfFiles,
        ISet<string> claimedBaseNames,
        IReadOnlyList<VelumPdfBatchDiagnosticRow> rows,
        List<string> orphanPdfFiles)
    {
      if (pdfFiles == null || pdfFiles.Count == 0 || orphanPdfFiles == null)
        return;

      var claimedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      if (rows != null)
      {
        for (int i = 0; i < rows.Count; i++)
        {
          string path = rows[i]?.PdfPath;
          if (!string.IsNullOrWhiteSpace(path))
            claimedPaths.Add(path);
        }
      }

      for (int i = 0; i < pdfFiles.Count; i++)
      {
        string path = pdfFiles[i];
        if (string.IsNullOrWhiteSpace(path))
          continue;

        if (claimedPaths.Contains(path))
          continue;

        string baseName = Path.GetFileNameWithoutExtension(path);
        if (!string.IsNullOrWhiteSpace(baseName) &&
            claimedBaseNames != null &&
            claimedBaseNames.Contains(baseName))
          continue;

        orphanPdfFiles.Add(path);
      }
    }
  }
}
