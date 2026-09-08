using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Velum.ReactiveCore.Export;
using Xarial.XCad.SolidWorks;

namespace Velum.UI.AssemblyRegistry
{
  /// <summary>Сопоставление позиций реестра с чертежами и пакетная печать.</summary>
  internal static class VelumAssemblyRegistryPrintDrawingsService
  {
    internal enum PrintRowKind
    {
      Ready = 0,
      Missing = 1,
      Ambiguous = 2,
      Skipped = 3
    }

    internal sealed class PrintRow
    {
      internal string ComponentDisplayName { get; set; }

      internal string ModelPath { get; set; }

      internal string BaseName { get; set; }

      internal string DrawingPath { get; set; }

      internal string StatusText { get; set; }

      internal PrintRowKind Kind { get; set; }

      /// <summary>true — печать PDF через оболочку Windows, не через SolidWorks.</summary>
      internal bool IsPdfDocument { get; set; }

      /// <summary>Готов к печати; для Ambiguous — первый найденный путь, по умолчанию без галочки.</summary>
      internal bool CanPrint
      {
        get
        {
          return (Kind == PrintRowKind.Ready || Kind == PrintRowKind.Ambiguous) &&
                 !string.IsNullOrWhiteSpace(DrawingPath);
        }
      }
    }

    internal sealed class PrintResult
    {
      internal int Printed { get; set; }

      internal int Failed { get; set; }

      internal int Skipped { get; set; }

      internal bool Cancelled { get; set; }

      internal List<string> Errors { get; } = new List<string>();
    }

    internal static List<PrintRow> Resolve(IReadOnlyList<VelumAssemblyRegistryComponent> components)
    {
      return Resolve(components, drawingsCatalog: null);
    }

    /// <summary>Сопоставление позиций со чертежами по свойству «путь чертежа».</summary>
    /// <param name="components">Позиции реестра.</param>
    /// <param name="drawingsCatalog">Устарел: не используется; оставлен для совместимости вызовов.</param>
    internal static List<PrintRow> Resolve(
        IReadOnlyList<VelumAssemblyRegistryComponent> components,
        string drawingsCatalog)
    {
      var rows = new List<PrintRow>();
      if (components == null || components.Count == 0)
        return rows;

      var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

      for (int i = 0; i < components.Count; i++)
      {
        VelumAssemblyRegistryComponent item = components[i];
        if (item == null)
          continue;

        if (item.Kind != VelumAssemblyRegistryNodeKind.Part &&
            item.Kind != VelumAssemblyRegistryNodeKind.Assembly)
        {
          rows.Add(new PrintRow
          {
            ComponentDisplayName = DisplayName(item),
            ModelPath = item.FilePath ?? string.Empty,
            BaseName = string.Empty,
            DrawingPath = string.Empty,
            Kind = PrintRowKind.Skipped,
            StatusText = "Пропуск (не деталь/сборка)"
          });
          continue;
        }

        string modelPath = (item.FilePath ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(modelPath))
        {
          rows.Add(new PrintRow
          {
            ComponentDisplayName = DisplayName(item),
            ModelPath = string.Empty,
            BaseName = string.Empty,
            DrawingPath = string.Empty,
            Kind = PrintRowKind.Skipped,
            StatusText = "Нет пути к модели"
          });
          continue;
        }

        string baseName = Path.GetFileNameWithoutExtension(modelPath);
        string display = DisplayName(item);
        string drawingPath = TryReadDrawingPath(item);
        if (string.IsNullOrWhiteSpace(drawingPath) || !File.Exists(drawingPath))
        {
          rows.Add(new PrintRow
          {
            ComponentDisplayName = display,
            ModelPath = modelPath,
            BaseName = baseName ?? string.Empty,
            DrawingPath = string.Empty,
            Kind = PrintRowKind.Missing,
            StatusText = "Нет чертежа"
          });
          continue;
        }

        if (!seenPaths.Add(drawingPath))
          continue;

        rows.Add(new PrintRow
        {
          ComponentDisplayName = display,
          ModelPath = modelPath,
          BaseName = baseName ?? string.Empty,
          DrawingPath = drawingPath,
          Kind = PrintRowKind.Ready,
          StatusText = Path.GetFileName(drawingPath)
        });
      }

      return rows;
    }

    private static string TryReadDrawingPath(VelumAssemblyRegistryComponent item)
    {
      if (item?.PropertyValues != null &&
          item.PropertyValues.TryGetValue(
              Velum.ReactiveCore.VelumExportDocumentationProperties.DrawingPath,
              out string fromCache) &&
          !string.IsNullOrWhiteSpace(fromCache))
        return fromCache.Trim();

      return string.Empty;
    }

    internal static PrintResult PrintSelected(
        ISwApplication swApp,
        IReadOnlyList<PrintRow> rows,
        string printerName,
        int copies,
        Func<bool> isCancelled = null,
        Action<int, int, string> reportProgress = null)
    {
      var result = new PrintResult();
      if (swApp?.Sw == null || rows == null || rows.Count == 0)
        return result;

      string printer = (printerName ?? string.Empty).Trim();
      if (string.IsNullOrWhiteSpace(printer))
      {
        result.Errors.Add("Не выбран принтер.");
        return result;
      }

      int copyCount = copies < 1 ? 1 : copies;
      HashSet<string> keepOpen = VelumPdfBatchDocumentHelper.CollectOpenDrawingPaths(swApp);
      string activeTitleBefore = TryGetActiveDocumentTitle(swApp);

      // CloseDoc сразу после PrintOut4 даёт AV в SLDWORKS (печать ещё в полёте).
      var openedHere = new List<OpenedDrawing>();

      var toPrint = new List<PrintRow>();
      for (int i = 0; i < rows.Count; i++)
      {
        PrintRow row = rows[i];
        if (row != null && row.CanPrint)
          toPrint.Add(row);
      }

      int total = toPrint.Count;
      try
      {
        for (int index = 0; index < toPrint.Count; index++)
        {
          if (isCancelled != null && isCancelled())
          {
            result.Cancelled = true;
            break;
          }

          PrintRow row = toPrint[index];
          reportProgress?.Invoke(index + 1, total, row.ComponentDisplayName ?? row.BaseName);

          if (row.IsPdfDocument)
          {
            string pdfError;
            if (!TryPrintPdf(row.DrawingPath, printer, copyCount, out pdfError))
            {
              result.Failed++;
              result.Errors.Add(
                  (row.ComponentDisplayName ?? row.BaseName) + ": " +
                  (string.IsNullOrWhiteSpace(pdfError) ? "ошибка печати PDF" : pdfError));
            }
            else
            {
              result.Printed++;
            }

            TryPumpAndWait(400);
            continue;
          }

          ModelDoc2 existing = VelumPdfBatchDocumentHelper.TryFindOpenDrawingByPath(swApp, row.DrawingPath);
          bool weOpened = false;
          ModelDoc2 drawingDoc = existing;
          if (drawingDoc == null)
          {
            string openError;
            drawingDoc = VelumPdfBatchDocumentHelper.TryOpenDrawingSilent(
                swApp,
                row.DrawingPath,
                out openError);
            weOpened = drawingDoc != null;
            if (drawingDoc == null)
            {
              result.Failed++;
              result.Errors.Add(
                  (row.ComponentDisplayName ?? row.BaseName) + ": " +
                  (string.IsNullOrWhiteSpace(openError) ? "не удалось открыть" : openError));
              continue;
            }
          }

          if (weOpened)
            openedHere.Add(new OpenedDrawing { Doc = drawingDoc, Path = row.DrawingPath });

          string printError;
          if (!TryPrintDrawing(swApp, drawingDoc, printer, copyCount, out printError))
          {
            result.Failed++;
            result.Errors.Add(
                (row.ComponentDisplayName ?? row.BaseName) + ": " +
                (string.IsNullOrWhiteSpace(printError) ? "ошибка печати" : printError));
          }
          else
          {
            result.Printed++;
          }

          // Дать spooler/SW добрать задание до следующего Open/Activate.
          TryPumpAndWait(400);
        }
      }
      finally
      {
        TryPumpAndWait(800);
        for (int i = 0; i < openedHere.Count; i++)
        {
          OpenedDrawing entry = openedHere[i];
          VelumPdfBatchDocumentHelper.TryReleaseDrawingAfterBatch(
              swApp,
              entry.Doc,
              entry.Path,
              persistChanges: false,
              keepOpen);
        }

        TryRestoreActiveDocument(swApp, activeTitleBefore);
      }

      result.Skipped = Math.Max(0, rows.Count - toPrint.Count);
      return result;
    }

    private sealed class OpenedDrawing
    {
      internal ModelDoc2 Doc;
      internal string Path;
    }

    private static bool TryPrintPdf(
        string pdfPath,
        string printerName,
        int copies,
        out string error)
    {
      error = string.Empty;
      string path = (pdfPath ?? string.Empty).Trim();
      if (path.Length == 0 || !File.Exists(path))
      {
        error = "Файл не найден";
        return false;
      }

      int copyCount = copies < 1 ? 1 : copies;
      string printer = (printerName ?? string.Empty).Trim();

      for (int copyIndex = 0; copyIndex < copyCount; copyIndex++)
      {
        if (!TryPrintPdfOnce(path, printer, out error))
          return false;
        TryPumpAndWait(300);
      }

      return true;
    }

    private static bool TryPrintPdfOnce(string pdfPath, string printerName, out string error)
    {
      error = string.Empty;
      try
      {
        if (!string.IsNullOrWhiteSpace(printerName))
        {
          try
          {
            var printTo = new ProcessStartInfo
            {
              FileName = pdfPath,
              Verb = "printto",
              Arguments = "\"" + printerName + "\"",
              UseShellExecute = true,
              CreateNoWindow = true,
              WindowStyle = ProcessWindowStyle.Hidden
            };
            Process.Start(printTo);
            return true;
          }
          catch
          {
          }
        }

        var printDefault = new ProcessStartInfo
        {
          FileName = pdfPath,
          Verb = "print",
          UseShellExecute = true,
          CreateNoWindow = true,
          WindowStyle = ProcessWindowStyle.Hidden
        };
        Process.Start(printDefault);
        return true;
      }
      catch (Exception ex)
      {
        error = ex.Message;
        return false;
      }
    }

    private static bool TryPrintDrawing(
        ISwApplication swApp,
        ModelDoc2 drawingDoc,
        string printerName,
        int copies,
        out string error)
    {
      error = string.Empty;
      if (drawingDoc == null)
      {
        error = "Документ не загружен";
        return false;
      }

      try
      {
        if (!TryActivateDrawing(swApp, drawingDoc, out error))
          return false;

        ModelDocExtension ext = drawingDoc.Extension;
        if (ext == null)
        {
          error = "Extension недоступен";
          return false;
        }

        // PrintOut4 рассчитан на активный документ; после Activate перечитываем Extension.
        ModelDoc2 active = swApp?.Sw?.IActiveDoc2 as ModelDoc2;
        if (active != null)
        {
          drawingDoc = active;
          ext = drawingDoc.Extension;
          if (ext == null)
          {
            error = "Extension недоступен";
            return false;
          }
        }

        string origPrinter = string.Empty;
        try { origPrinter = drawingDoc.Printer ?? string.Empty; } catch { }

        int origUsePageSetup = ext.UsePageSetup;
        PrintSpecification printSpec = ext.GetPrintSpecification() as PrintSpecification;
        if (printSpec == null)
        {
          error = "PrintSpecification недоступен";
          return false;
        }

        try
        {
          // Формат/ориентация/масштаб — из свойств чертежа (document page setup).
          ext.UsePageSetup = (int)swPageSetupInUse_e.swPageSetupInUse_Document;
          try { drawingDoc.Printer = printerName; } catch { }

          // Не вызывать RestoreDefaults до PrintOut4: обнуляет внутреннее состояние spec.
          printSpec.ResetPrintRange();
          printSpec.PrintToFile = false;
          printSpec.NumberOfCopies = copies < 1 ? 1 : copies;
          // Все листы (-1,-1), как в типичных макросах SW; AddPrintRange на части версий даёт AV.
          printSpec.PrintRange = new int[] { -1, -1 };

          ext.PrintOut4(printerName, string.Empty, printSpec);
          TryPumpAndWait(200);
          return true;
        }
        finally
        {
          // Не трогаем PrintSpecification сразу после PrintOut4 — только UsePageSetup/Printer.
          try { drawingDoc.Printer = origPrinter; } catch { }
          try { ext.UsePageSetup = origUsePageSetup; } catch { }
        }
      }
      catch (Exception ex)
      {
        error = ex.Message;
        return false;
      }
    }

    private static bool TryActivateDrawing(ISwApplication swApp, ModelDoc2 drawingDoc, out string error)
    {
      error = string.Empty;
      if (swApp?.Sw == null || drawingDoc == null)
      {
        error = "SolidWorks недоступен";
        return false;
      }

      try
      {
        string title = drawingDoc.GetTitle();
        if (string.IsNullOrWhiteSpace(title))
        {
          error = "Нет заголовка чертежа";
          return false;
        }

        int errors = 0;
        swApp.Sw.ActivateDoc3(
            title,
            true,
            (int)swRebuildOnActivation_e.swDontRebuildActiveDoc,
            ref errors);

        ModelDoc2 active = swApp.Sw.IActiveDoc2 as ModelDoc2;
        if (active == null || active.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
        {
          error = "Не удалось активировать чертёж";
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

    private static void TryPumpAndWait(int milliseconds)
    {
      try
      {
        System.Windows.Forms.Application.DoEvents();
      }
      catch
      {
      }

      if (milliseconds > 0)
      {
        try
        {
          System.Threading.Thread.Sleep(milliseconds);
        }
        catch
        {
        }

        try
        {
          System.Windows.Forms.Application.DoEvents();
        }
        catch
        {
        }
      }
    }

    private static string DisplayName(VelumAssemblyRegistryComponent item)
    {
      if (item == null)
        return string.Empty;
      if (!string.IsNullOrWhiteSpace(item.Designation))
        return item.Designation;
      if (!string.IsNullOrWhiteSpace(item.FileTitle))
        return item.FileTitle;
      if (!string.IsNullOrWhiteSpace(item.FilePath))
        return Path.GetFileName(item.FilePath);
      return "(без имени)";
    }

    private static string TryGetActiveDocumentTitle(ISwApplication swApp)
    {
      try
      {
        ModelDoc2 active = swApp?.Sw?.IActiveDoc2 as ModelDoc2;
        return active?.GetTitle() ?? string.Empty;
      }
      catch
      {
        return string.Empty;
      }
    }

    private static void TryRestoreActiveDocument(ISwApplication swApp, string title)
    {
      if (swApp?.Sw == null || string.IsNullOrWhiteSpace(title))
        return;

      try
      {
        int errors = 0;
        swApp.Sw.ActivateDoc3(
            title,
            true,
            (int)swRebuildOnActivation_e.swDontRebuildActiveDoc,
            ref errors);
      }
      catch
      {
      }
    }
  }
}
