using System;
using System.Collections.Generic;
using System.IO;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Velum.ReactiveCore.Export;
using Velum.SolidHomeostasis;
using Xarial.XCad.SolidWorks;

namespace Velum.ReactiveCore
{
  /// <summary>Сканирование деталей в каталоге для пакетного присвоения материалов.</summary>
  internal static class VelumMaterialBatchScanner
  {
    internal const SearchOption PartSearchOption = SearchOption.AllDirectories;
    internal const string PropSection = "Раздел";

    internal sealed class ScanRequest
    {
      public string PartsRoot { get; set; }

      public ISwApplication SwApp { get; set; }

      public ISet<string> KeepOpenPartPaths { get; set; }

      /// <summary>При возврате true сканирование прерывается.</summary>
      public Func<bool> IsCancelled { get; set; }

      /// <summary>Прогресс сканирования: текущий индекс (1-based), всего файлов, имя файла.</summary>
      public Action<int, int, string> OnProgress { get; set; }
    }

    internal sealed class ScanResult
    {
      public List<VelumMaterialBatchRow> Rows { get; } = new List<VelumMaterialBatchRow>();
    }

    internal static ScanResult Run(ScanRequest request)
    {
      var result = new ScanResult();
      string partsRoot = (request?.PartsRoot ?? string.Empty).Trim();
      if (string.IsNullOrWhiteSpace(partsRoot) || !Directory.Exists(partsRoot))
        return result;

      List<string> partFiles = CollectPartFiles(partsRoot);
      for (int i = 0; i < partFiles.Count; i++)
      {
        if (request?.IsCancelled != null && request.IsCancelled())
          break;

        string partPath = partFiles[i];
        if (request.OnProgress != null)
          request.OnProgress(i + 1, partFiles.Count, Path.GetFileName(partPath));

        ScanPart(request, partPath, result.Rows);
      }

      VelumDxfBatchDocumentHelper.TryCloseOpenPartsByPaths(
          request.SwApp,
          partFiles,
          request.KeepOpenPartPaths);

      return result;
    }

    /// <summary>Строки по всем деталям активной сборки (уникальный путь), без обхода каталога на диске.</summary>
    internal static ScanResult RunFromAssembly(ScanRequest request, AssemblyDoc assembly)
    {
      var result = new ScanResult();
      if (request?.SwApp == null || assembly == null)
        return result;

      List<string> partFiles = CollectAssemblyPartPaths(request, assembly);
      for (int i = 0; i < partFiles.Count; i++)
      {
        if (request.IsCancelled != null && request.IsCancelled())
          break;

        string partPath = partFiles[i];
        if (request.OnProgress != null)
          request.OnProgress(i + 1, partFiles.Count, Path.GetFileName(partPath));

        ScanPart(request, partPath, result.Rows);
      }

      VelumDxfBatchDocumentHelper.TryCloseOpenPartsByPaths(
          request.SwApp,
          partFiles,
          request.KeepOpenPartPaths);

      return result;
    }

    /// <summary>Строки по конфигурациям активной детали (без обхода каталога).</summary>
    internal static ScanResult RunFromPart(ScanRequest request, ModelDoc2 part)
    {
      var result = new ScanResult();
      if (request?.SwApp == null || part == null)
        return result;

      try
      {
        if (part.GetType() != (int)swDocumentTypes_e.swDocPART)
          return result;
      }
      catch
      {
        return result;
      }

      string partPath;
      try
      {
        partPath = (part.GetPathName() ?? string.Empty).Trim();
      }
      catch
      {
        return result;
      }

      if (string.IsNullOrWhiteSpace(partPath))
        return result;

      if (request.OnProgress != null)
        request.OnProgress(1, 1, Path.GetFileName(partPath));

      ScanPart(request, partPath, result.Rows);
      return result;
    }

    /// <summary>
    /// Рекурсивный обход всех компонентов сборки через GetChildren.
    /// Возвращает уникальные пути только к деталям (swDocPART).
    /// </summary>
    private static List<string> CollectAssemblyPartPaths(ScanRequest request, AssemblyDoc assembly)
    {
      var paths = new List<string>();
      var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

      // Получаем компоненты первого уровня.
      object[] topLevel = assembly?.GetComponents(true) as object[];
      if (topLevel == null)
        return paths;

      foreach (object item in topLevel)
      {
        if (request?.IsCancelled != null && request.IsCancelled())
          break;

        Component2 comp = item as Component2;
        if (comp == null)
          continue;

        CollectPartPathsRecursive(request, comp, paths, seen);
      }

      return paths;
    }

    /// <summary>
    /// Рекурсивный сбор путей к деталям из компонента и его дочерних компонентов.
    /// </summary>
    private static void CollectPartPathsRecursive(
        ScanRequest request,
        Component2 comp,
        List<string> paths,
        HashSet<string> seen)
    {
      if (comp == null)
        return;

      try
      {
        if (comp.IsSuppressed())
          return;
      }
      catch
      {
      }

      ModelDoc2 modelDoc;
      try
      {
        modelDoc = comp.GetModelDoc2() as ModelDoc2;
      }
      catch
      {
        return;
      }

      if (modelDoc == null)
        return;

      string path;
      try
      {
        path = (modelDoc.GetPathName() ?? string.Empty).Trim();
      }
      catch
      {
        return;
      }

      if (!string.IsNullOrWhiteSpace(path) && seen.Add(path))
      {
        if (modelDoc.GetType() == (int)swDocumentTypes_e.swDocPART)
          paths.Add(path);
      }

      // Если это сборка — рекурсивно обходим дочерние компоненты.
      if (modelDoc.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY)
      {
        object[] children;
        try
        {
          children = comp.GetChildren() as object[];
        }
        catch
        {
          children = null;
        }

        if (children != null)
        {
          foreach (object childObj in children)
          {
            if (request?.IsCancelled != null && request.IsCancelled())
              break;

            CollectPartPathsRecursive(request, childObj as Component2, paths, seen);
          }
        }
      }
    }

    private static void ScanPart(
        ScanRequest request,
        string partPath,
        List<VelumMaterialBatchRow> rows)
    {
      ISwApplication swApp = request?.SwApp;
      string displayName = Path.GetFileName(partPath);
      ModelDoc2 modelDoc = VelumDxfBatchDocumentHelper.TryOpenPartSilent(swApp, partPath, out _);
      if (modelDoc == null)
        return;

      string section = string.Empty;
      try
      {
        section = ReadSectionFromPart(modelDoc);
      }
      catch
      {
        section = string.Empty;
      }

      try
      {
        IReadOnlyList<string> configs = VelumDxfArtifactResolver.TryGetConfigurationNames(modelDoc);
        for (int i = 0; i < configs.Count; i++)
        {
          string configName = configs[i];
          string materialName = string.Empty;
          string databaseName = string.Empty;
          if (VelumSolidWorksMaterialComHelper.TryReadPartMaterialForConfig(
                  modelDoc,
                  configName,
                  out string readName,
                  out string readDatabase))
          {
            materialName = NormalizeDisplayMaterial(readName);
            databaseName = NormalizeDisplayDatabase(readDatabase);
          }

          rows.Add(new VelumMaterialBatchRow
          {
            PartPath = partPath,
            PartDisplayName = displayName,
            ConfigName = configName,
            MaterialName = materialName,
            MaterialDatabase = databaseName,
            Selected = false,
            Section = section
          });
        }
      }
      finally
      {
        VelumDxfBatchDocumentHelper.TryReleasePartAfterBatch(
            swApp,
            modelDoc,
            partPath,
            persistChanges: false,
            request?.KeepOpenPartPaths);
      }
    }

    /// <summary>
    /// Читает свойство «Раздел» из детали (через конфигурацию и документ).
    /// </summary>
    private static string ReadSectionFromPart(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return string.Empty;

      string value;
      bool exists;
      if (!VelumRecipeSolidWorksCustomProperties.TryReadConfigThenDocument(
              modelDoc,
              string.Empty,
              PropSection,
              out value,
              out exists) ||
          !exists)
        return string.Empty;

      return (value ?? string.Empty).Trim();
    }

    internal static void RefreshPartRows(
        ModelDoc2 modelDoc,
        string partPath,
        IList<VelumMaterialBatchRow> rows)
    {
      if (modelDoc == null || rows == null)
        return;

      string normalizedPartPath = (partPath ?? string.Empty).Trim();
      if (normalizedPartPath.Length == 0)
        return;

      for (int i = 0; i < rows.Count; i++)
      {
        VelumMaterialBatchRow row = rows[i];
        if (row == null ||
            !string.Equals(row.PartPath, normalizedPartPath, StringComparison.OrdinalIgnoreCase))
          continue;

        if (!VelumSolidWorksMaterialComHelper.TryReadPartMaterialForConfig(
                modelDoc,
                row.ConfigName,
                out string materialName,
                out string databaseName))
          continue;

        row.MaterialName = NormalizeDisplayMaterial(materialName);
        row.MaterialDatabase = NormalizeDisplayDatabase(databaseName);
      }
    }

    private static List<string> CollectPartFiles(string partsRoot)
    {
      var files = new List<string>();
      try
      {
        string[] raw = Directory.GetFiles(partsRoot, "*.sldprt", PartSearchOption);
        for (int i = 0; i < raw.Length; i++)
        {
          string path = (raw[i] ?? string.Empty).Trim();
          if (path.Length > 0)
            files.Add(path);
        }
      }
      catch
      {
      }

      files.Sort(StringComparer.OrdinalIgnoreCase);
      return files;
    }

    private static string NormalizeDisplayMaterial(string materialName)
    {
      if (string.IsNullOrWhiteSpace(materialName))
        return string.Empty;

      string trimmed = materialName.Trim();
      if (string.Equals(trimmed, "Unassigned", StringComparison.OrdinalIgnoreCase))
        return string.Empty;

      return trimmed;
    }

    private static string NormalizeDisplayDatabase(string databaseName)
    {
      if (string.IsNullOrWhiteSpace(databaseName))
        return string.Empty;

      return Path.GetFileNameWithoutExtension(databaseName.Trim());
    }
  }
}
