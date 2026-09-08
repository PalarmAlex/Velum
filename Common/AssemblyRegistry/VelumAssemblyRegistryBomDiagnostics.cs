using System;
using System.Collections.Generic;
using System.IO;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Velum.ReactiveCore;
using Velum.ReactiveCore.Export;
using Velum.SolidHomeostasis;
using Xarial.XCad.SolidWorks;

namespace Velum.UI.AssemblyRegistry
{
  /// <summary>
  /// Диагностика DXF/PDF по составу реестра изделия (только переданные позиции, без россыпи).
  /// DXF: свойства из кэша реестра / уже загруженных в сборке документов — без OpenDoc.
  /// PDF: BuildUnchecked — без OpenDoc чертежей; RunPdf — silent open только при диагностике.
  /// </summary>
  internal static class VelumAssemblyRegistryBomDiagnostics
  {
    internal sealed class DxfResult
    {
      internal List<VelumDxfBatchDiagnosticRow> Rows { get; } = new List<VelumDxfBatchDiagnosticRow>();
    }

    internal sealed class PdfResult
    {
      internal List<VelumPdfBatchDiagnosticRow> Rows { get; } = new List<VelumPdfBatchDiagnosticRow>();
    }

    /// <summary>
    /// Строки «Не проверено» по составу — без классификации штампов (детали уже в сборке).
    /// </summary>
    internal static DxfResult BuildUncheckedDxf(
        ISwApplication swApp,
        IReadOnlyList<VelumAssemblyRegistryComponent> components,
        Func<bool> isCancelled = null,
        Action<int, int, string> reportProgress = null)
    {
      var result = new DxfResult();
      if (swApp?.Sw == null || components == null || components.Count == 0)
        return result;

      List<PartDxfWorkItem> parts = CollectUniqueParts(components);
      int total = parts.Count;
      for (int index = 0; index < parts.Count; index++)
      {
        if (isCancelled != null && isCancelled())
          break;

        PartDxfWorkItem part = parts[index];
        reportProgress?.Invoke(index + 1, total, "DXF: " + part.DisplayName);

        ModelDoc2 modelDoc = TryGetLoadedPartModel(swApp, part.PartPath);
        if (modelDoc == null)
        {
          result.Rows.Add(new VelumDxfBatchDiagnosticRow
          {
            PartPath = part.PartPath,
            PartDisplayName = part.DisplayName,
            ConfigName = string.Empty,
            Status = VelumDxfBatchRowStatus.Error,
            StatusText = "Ошибка: деталь не загружена в сборке",
            Selected = false,
            Quantity = part.Quantity
          });
          continue;
        }

        IReadOnlyList<string> exportable = VelumDxfNeedFlagResolver.CollectExportable(modelDoc);
        int prefixCount = exportable.Count;
        if (prefixCount == 0)
          continue;

        for (int c = 0; c < exportable.Count; c++)
        {
          string configName = exportable[c];
          string expectedBase = TryBuildExpectedDxfBaseName(modelDoc, configName, prefixCount);
          result.Rows.Add(new VelumDxfBatchDiagnosticRow
          {
            PartPath = part.PartPath,
            PartDisplayName = part.DisplayName,
            ConfigName = configName,
            Status = VelumDxfBatchRowStatus.Unchecked,
            StatusText = "Не проверено",
            Selected = true,
            Quantity = part.Quantity,
            ExpectedDxfBaseName = expectedBase,
            DxfPath = string.Empty
          });
        }
      }

      return result;
    }

    internal static DxfResult RunDxf(
        ISwApplication swApp,
        IReadOnlyList<VelumAssemblyRegistryComponent> components,
        string dxfCatalog,
        Func<bool> isCancelled = null,
        Action<int, int, string> reportProgress = null,
        int productQuantity = 1)
    {
      var result = new DxfResult();
      if (swApp?.Sw == null || components == null || components.Count == 0)
        return result;

      int productQty = productQuantity < 1 ? 1 : productQuantity;
      List<PartDxfWorkItem> parts = CollectUniqueParts(components);
      int total = parts.Count;
      for (int index = 0; index < parts.Count; index++)
      {
        if (isCancelled != null && isCancelled())
          break;

        PartDxfWorkItem part = parts[index];
        reportProgress?.Invoke(index + 1, total, "DXF: " + part.DisplayName);

        ModelDoc2 modelDoc = TryGetLoadedPartModel(swApp, part.PartPath);
        if (modelDoc == null)
        {
          result.Rows.Add(new VelumDxfBatchDiagnosticRow
          {
            PartPath = part.PartPath,
            PartDisplayName = part.DisplayName,
            ConfigName = string.Empty,
            Status = VelumDxfBatchRowStatus.Error,
            StatusText = "Ошибка: деталь не загружена в сборке",
            Selected = false,
            Quantity = part.Quantity
          });
          continue;
        }

        IReadOnlyList<string> allConfigs = VelumDxfArtifactResolver.TryGetConfigurationNames(modelDoc);
        int exportableCount = VelumDxfNeedFlagResolver.CountExportable(modelDoc);
        if (exportableCount == 0)
          continue;

        string partCatalog = VelumDxfBatchDocumentHelper.TryReadDxfCatalog(modelDoc);
        bool isFirstExport = string.IsNullOrWhiteSpace(partCatalog);
        int assemblyQty = part.Quantity;

        for (int c = 0; c < allConfigs.Count; c++)
        {
          string configName = allConfigs[c];
          bool hasNeedFlag = VelumDxfBatchDocumentHelper.TryReadNeedDxf(
              modelDoc,
              configName,
              out bool needDxf);
          VelumDxfBatchDiagnosticRow row = VelumDxfBatchScanner.ClassifyConfigurationPublic(
              modelDoc,
              part.PartPath,
              part.DisplayName,
              configName,
              partCatalog,
              hasNeedFlag,
              needDxf,
              isFirstExport,
              assemblyQty,
              needDxf ? exportableCount : allConfigs.Count);
          if (row == null)
            continue;

          int deliveryQty = assemblyQty > 0 ? assemblyQty * productQty : 0;
          VelumDxfDeliveryQuantityDiagnostics.ApplyIfNeeded(
              row,
              dxfCatalog,
              deliveryQty > 0 ? (int?)deliveryQty : null);
          result.Rows.Add(row);
        }
      }

      return result;
    }

    private sealed class PartDxfWorkItem
    {
      internal string PartPath;
      internal string DisplayName;
      internal int Quantity;
    }

    private static List<PartDxfWorkItem> CollectUniqueParts(
        IReadOnlyList<VelumAssemblyRegistryComponent> components)
    {
      var byPath = new Dictionary<string, PartDxfWorkItem>(StringComparer.OrdinalIgnoreCase);
      if (components == null)
        return new List<PartDxfWorkItem>();

      for (int i = 0; i < components.Count; i++)
      {
        VelumAssemblyRegistryComponent item = components[i];
        if (item == null || item.Kind != VelumAssemblyRegistryNodeKind.Part)
          continue;

        string partPath = (item.FilePath ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(partPath))
          continue;

        PartDxfWorkItem part;
        if (!byPath.TryGetValue(partPath, out part))
        {
          part = new PartDxfWorkItem
          {
            PartPath = partPath,
            DisplayName = item.FileTitle ?? Path.GetFileName(partPath),
            Quantity = 0
          };
          byPath[partPath] = part;
        }

        part.Quantity += Math.Max(0, item.Quantity);
      }

      return new List<PartDxfWorkItem>(byPath.Values);
    }

    private static string TryBuildExpectedDxfBaseName(
        ModelDoc2 modelDoc,
        string configName,
        int prefixConfigurationCount)
    {
      // Свойство «Имя файла dxf» — единственный источник правды для ожидаемого имени DXF.
      string name = VelumDxfArtifactResolver.TryReadPerConfigFileName(modelDoc, configName);
      return (name ?? string.Empty).Trim();
    }

    /// <summary>
    /// Строки по составу реестра (как в списке формы): с чертежом — «Не проверено»,
    /// без чертежа — «Нет чертежа». Без OpenDoc чертежей.
    /// </summary>
    internal static PdfResult BuildUncheckedPdf(
        ISwApplication swApp,
        IReadOnlyList<VelumAssemblyRegistryComponent> components,
        Func<bool> isCancelled = null,
        Action<int, int, string> reportProgress = null)
    {
      var result = new PdfResult();
      if (swApp?.Sw == null || components == null || components.Count == 0)
        return result;

      var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      int total = components.Count;
      for (int index = 0; index < components.Count; index++)
      {
        if (isCancelled != null && isCancelled())
          break;

        VelumAssemblyRegistryComponent item = components[index];
        if (item == null)
          continue;
        if (item.Kind != VelumAssemblyRegistryNodeKind.Part &&
            item.Kind != VelumAssemblyRegistryNodeKind.Assembly)
          continue;

        string modelPath = (item.FilePath ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(modelPath))
          continue;

        string baseName = Path.GetFileNameWithoutExtension(modelPath);
        if (string.IsNullOrWhiteSpace(baseName))
          continue;

        // Одна строка на позицию списка (path+config), а не только на существующий .slddrw.
        string rowKey = !string.IsNullOrWhiteSpace(item.Identity) ? item.Identity : modelPath;
        if (!seenKeys.Add(rowKey))
          continue;

        reportProgress?.Invoke(index + 1, total, "PDF: " + baseName);

        bool hasNeedDrawingFlag = TryReadBoolFromCache(
            item,
            VelumExportDocumentationProperties.NeedDrawing,
            out bool needDrawing);
        ModelDoc2 modelDoc = TryGetLoadedPartModel(swApp, modelPath);
        if (!hasNeedDrawingFlag && modelDoc != null)
          hasNeedDrawingFlag = VelumDrawingPathPropertyHelper.TryReadNeedDrawing(modelDoc, out needDrawing);
        // Явный «Нужен чертеж = Нет» — не включать в пакетный PDF.
        if (hasNeedDrawingFlag && !needDrawing)
          continue;

        bool hasNeedFlag = TryReadBoolFromCache(
            item,
            VelumExportDocumentationProperties.NeedPdf,
            out bool needPdf);
        if (!hasNeedFlag && modelDoc != null)
          hasNeedFlag = VelumPdfBatchDocumentHelper.TryReadNeedPdf(modelDoc, out needPdf);
        // Явный «Нужен pdf = Нет» — не включать. Отсутствие свойства не блокирует список
        // (флаг часто живёт на чертеже; метрики/Junk по-прежнему смотрят NeedPdf при диагностике).
        if (hasNeedFlag && !needPdf)
          continue;

        string drawingPath = TryReadStringFromCache(item, VelumExportDocumentationProperties.DrawingPath);
        if (string.IsNullOrWhiteSpace(drawingPath) && modelDoc != null)
          drawingPath = VelumDrawingPathPropertyHelper.TryRead(modelDoc);
        if ((string.IsNullOrWhiteSpace(drawingPath) || !File.Exists(drawingPath)) && modelDoc != null)
          drawingPath = VelumDrawingPathPropertyHelper.TryFindSiblingDrawingPath(modelDoc);

        if (string.IsNullOrWhiteSpace(drawingPath) || !File.Exists(drawingPath))
        {
          result.Rows.Add(new VelumPdfBatchDiagnosticRow
          {
            DrawingPath = string.Empty,
            DrawingDisplayName = baseName + ".slddrw",
            SourceModelPath = modelPath,
            Status = VelumPdfBatchRowStatus.Error,
            StatusText = "Нет чертежа",
            Selected = false,
            PdfPath = string.Empty
          });
          continue;
        }

        result.Rows.Add(new VelumPdfBatchDiagnosticRow
        {
          DrawingPath = drawingPath,
          DrawingDisplayName = Path.GetFileName(drawingPath),
          SourceModelPath = modelPath,
          Status = VelumPdfBatchRowStatus.Unchecked,
          StatusText = "Не проверено",
          Selected = true,
          PdfPath = string.Empty
        });
      }

      return result;
    }

    internal static PdfResult RunPdf(
        ISwApplication swApp,
        IReadOnlyList<VelumAssemblyRegistryComponent> components,
        string pdfCatalog,
        Func<bool> isCancelled = null,
        Action<int, int, string> reportProgress = null)
    {
      var result = new PdfResult();
      if (swApp?.Sw == null || components == null || components.Count == 0)
        return result;

      string pdfRoot = (pdfCatalog ?? string.Empty).Trim();
      // Каталог delivery не обязателен для диагностики: canonical PDF берётся из свойств.

      HashSet<string> keepOpen = VelumPdfBatchDocumentHelper.CollectOpenDrawingPaths(swApp);
      string activeTitleBefore = TryGetActiveDocumentTitle(swApp);
      var seenDrawings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

      int total = components.Count;
      for (int index = 0; index < components.Count; index++)
      {
        if (isCancelled != null && isCancelled())
          break;

        VelumAssemblyRegistryComponent item = components[index];
        if (item == null)
          continue;
        if (item.Kind != VelumAssemblyRegistryNodeKind.Part &&
            item.Kind != VelumAssemblyRegistryNodeKind.Assembly)
          continue;

        string modelPath = (item.FilePath ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(modelPath))
          continue;

        string baseName = Path.GetFileNameWithoutExtension(modelPath);
        if (string.IsNullOrWhiteSpace(baseName))
          continue;

        reportProgress?.Invoke(index + 1, total, "PDF: " + baseName);

        bool hasNeedDrawingFlag = TryReadBoolFromCache(
            item,
            VelumExportDocumentationProperties.NeedDrawing,
            out bool needDrawing);
        ModelDoc2 modelDoc = TryGetLoadedPartModel(swApp, modelPath);
        if (!hasNeedDrawingFlag && modelDoc != null)
          hasNeedDrawingFlag = VelumDrawingPathPropertyHelper.TryReadNeedDrawing(modelDoc, out needDrawing);
        if (hasNeedDrawingFlag && !needDrawing)
          continue;

        bool hasNeedFlag = TryReadBoolFromCache(
            item,
            VelumExportDocumentationProperties.NeedPdf,
            out bool needPdf);
        if (!hasNeedFlag && modelDoc != null)
          hasNeedFlag = VelumPdfBatchDocumentHelper.TryReadNeedPdf(modelDoc, out needPdf);
        if (hasNeedFlag && !needPdf)
          continue;

        string drawingPath = TryReadStringFromCache(item, VelumExportDocumentationProperties.DrawingPath);
        if (string.IsNullOrWhiteSpace(drawingPath) && modelDoc != null)
        {
          drawingPath = VelumDrawingPathPropertyHelper.TryRead(modelDoc);
        }
        if ((string.IsNullOrWhiteSpace(drawingPath) || !File.Exists(drawingPath)) && modelDoc != null)
          drawingPath = VelumDrawingPathPropertyHelper.TryFindSiblingDrawingPath(modelDoc);
        if (string.IsNullOrWhiteSpace(drawingPath) || !File.Exists(drawingPath))
        {
          result.Rows.Add(new VelumPdfBatchDiagnosticRow
          {
            DrawingPath = string.Empty,
            DrawingDisplayName = baseName + ".slddrw",
            SourceModelPath = modelPath,
            Status = VelumPdfBatchRowStatus.Error,
            StatusText = "Нет чертежа",
            Selected = false,
            PdfPath = string.Empty
          });
          continue;
        }

        if (!seenDrawings.Add(drawingPath))
          continue;

        ModelDoc2 existing = VelumPdfBatchDocumentHelper.TryFindOpenDrawingByPath(swApp, drawingPath);
        bool openedHere = false;
        ModelDoc2 drawingDoc = existing;
        if (drawingDoc == null)
        {
          drawingDoc = VelumPdfBatchDocumentHelper.TryOpenDrawingSilent(
              swApp,
              drawingPath,
              out _);
          openedHere = drawingDoc != null;
        }

        if (drawingDoc == null)
        {
          result.Rows.Add(new VelumPdfBatchDiagnosticRow
          {
            DrawingPath = drawingPath,
            DrawingDisplayName = Path.GetFileName(drawingPath),
            SourceModelPath = modelPath,
            Status = VelumPdfBatchRowStatus.Error,
            StatusText = "Ошибка: не удалось открыть чертёж",
            Selected = false
          });
          continue;
        }

        try
        {
          if (!hasNeedFlag)
            hasNeedFlag = VelumPdfBatchDocumentHelper.TryReadNeedPdf(drawingDoc, out needPdf);
          VelumPdfBatchDiagnosticRow row = VelumPdfBatchScanner.ClassifyDrawingPublic(
              swApp,
              drawingDoc,
              drawingPath,
              Path.GetFileName(drawingPath),
              pdfRoot,
              hasNeedFlag,
              needPdf,
              modelPath);
          if (row != null)
          {
            row.SourceModelPath = modelPath;
            result.Rows.Add(row);
          }
        }
        finally
        {
          if (openedHere)
          {
            VelumPdfBatchDocumentHelper.TryReleaseDrawingAfterBatch(
                swApp,
                drawingDoc,
                drawingPath,
                persistChanges: false,
                keepOpen);
          }
        }
      }

      TryRestoreActiveDocument(swApp, activeTitleBefore);
      return result;
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

    /// <summary>
    /// Модель детали/сборки уже в памяти (включая компонент активной сборки) — без OpenDoc.
    /// </summary>
    private static ModelDoc2 TryGetLoadedPartModel(ISwApplication swApp, string partPath)
    {
      if (swApp?.Sw == null || string.IsNullOrWhiteSpace(partPath))
        return null;

      string expected = NormalizePath(partPath);
      try
      {
        ModelDoc2 doc = swApp.Sw.GetFirstDocument() as ModelDoc2;
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

      return TryGetPartModelFromActiveAssembly(swApp, partPath);
    }

    private static ModelDoc2 TryGetPartModelFromActiveAssembly(ISwApplication swApp, string partPath)
    {
      if (swApp?.Sw == null || string.IsNullOrWhiteSpace(partPath))
        return null;

      try
      {
        ModelDoc2 active = swApp.Sw.IActiveDoc2 as ModelDoc2;
        if (active == null || active.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
          return null;

        var assembly = active as AssemblyDoc;
        if (assembly == null)
          return null;

        object[] components = assembly.GetComponents(true) as object[];
        if (components == null)
          return null;

        string expected = NormalizePath(partPath);
        if (string.IsNullOrWhiteSpace(expected))
          return null;

        foreach (object obj in components)
        {
          var comp = obj as Component2;
          if (comp == null)
            continue;

          string compPath;
          try
          {
            compPath = comp.GetPathName();
          }
          catch
          {
            continue;
          }

          if (!string.Equals(NormalizePath(compPath), expected, StringComparison.OrdinalIgnoreCase))
            continue;

          try
          {
            ModelDoc2 modelDoc = comp.GetModelDoc2() as ModelDoc2;
            if (modelDoc == null)
              return null;

            // Component2.GetModelDoc2 для компонента сборки (особенно в облегчённом режиме)
            // может возвращать документ с кэшированными per-config свойствами.
            // EditRebuild3 синхронизирует свойства конфигурации с файлом, что необходимо
            // для корректного чтения DxfGeometryPendingStamp / DxfGeometryUpdateStamp
            // через CustomPropertyManager.
            try
            {
              modelDoc.EditRebuild3();
            }
            catch
            {
              // Перестройка не критична — свойства могут быть частично загружены.
            }

            return modelDoc;
          }
          catch
          {
            return null;
          }
        }
      }
      catch
      {
      }

      return null;
    }

    private static string NormalizePath(string path)
    {
      if (string.IsNullOrWhiteSpace(path))
        return string.Empty;
      try
      {
        return Path.GetFullPath(path.Trim());
      }
      catch
      {
        return (path ?? string.Empty).Trim();
      }
    }

    private static bool TryReadBoolFromCache(
        VelumAssemblyRegistryComponent item,
        string propertyName,
        out bool value)
    {
      value = false;
      string raw = TryReadStringFromCache(item, propertyName);
      if (string.IsNullOrWhiteSpace(raw))
        return false;
      return VelumSolidCustomPropertyTypes.TryParseBooleanString(raw, out value);
    }

    private static string TryReadStringFromCache(VelumAssemblyRegistryComponent item, string propertyName)
    {
      if (item?.PropertyValues == null || string.IsNullOrWhiteSpace(propertyName))
        return string.Empty;

      string raw;
      if (!item.PropertyValues.TryGetValue(propertyName, out raw) || raw == null)
        return string.Empty;
      return raw.Trim();
    }
  }
}
