using System;
using System.Collections.Generic;
using System.IO;
using SolidWorks.Interop.sldworks;
using ISIDA.Common;
using Velum.ReactiveCore;
using Velum.ReactiveCore.Export;
using Velum.SolidHomeostasis;
using Velum.UI;
using Velum.UI.ProductRegistry;
using Xarial.XCad.SolidWorks;

namespace Velum.ReactiveCore.Export
{
  /// <summary>Пакетная диагностика DXF: фаза 1 (ФС) + фаза 2 (SW API).</summary>
  internal static class VelumDxfBatchScanner
  {
    internal const SearchOption PartSearchOption = SearchOption.AllDirectories;
    internal const SearchOption DxfSearchOption = SearchOption.AllDirectories;

    internal sealed class ScanRequest
    {
      public string PartsRoot { get; set; }

      public string DxfRoot { get; set; }

      public ISwApplication SwApp { get; set; }

      /// <summary>Пути SLDPRT, уже открытые до операции — не закрывать после неё.</summary>
      public ISet<string> KeepOpenPartPaths { get; set; }

      /// <summary>При возврате true сканирование прерывается.</summary>
      public Func<bool> IsCancelled { get; set; }
    }

    internal sealed class ScanResult
    {
      public List<VelumDxfBatchDiagnosticRow> Rows { get; } = new List<VelumDxfBatchDiagnosticRow>();

      public List<string> OrphanDxfFiles { get; } = new List<string>();

      /// <summary>Сколько записей реестра обновили поле Name при диагностике.</summary>
      public int RegistryNamesUpdated { get; set; }
    }

    internal static ScanResult Run(ScanRequest request)
    {
      var result = new ScanResult();
      string partsRoot = (request?.PartsRoot ?? string.Empty).Trim();
      string dxfRoot = (request?.DxfRoot ?? string.Empty).Trim();
      if (string.IsNullOrWhiteSpace(partsRoot) || !Directory.Exists(partsRoot))
        return result;
      if (string.IsNullOrWhiteSpace(dxfRoot) || !Directory.Exists(dxfRoot))
        return result;

      List<string> partFiles = CollectPartFiles(partsRoot);
      List<string> dxfFiles = CollectDxfFiles(dxfRoot);

      VelumProductRegistryStore registryStore = null;
      if (!VelumProductRegistryFormHost.IsOpen)
      {
        try
        {
          registryStore = new VelumProductRegistryStore();
          registryStore.Load();
        }
        catch (Exception ex)
        {
          Logger.Warning("Velum DXF diagnostics registry load: " + ex.Message);
          registryStore = null;
        }
      }

      for (int i = 0; i < partFiles.Count; i++)
      {
        if (request?.IsCancelled != null && request.IsCancelled())
          break;

        RunPhase2ForPart(request, partFiles[i], dxfRoot, result.Rows, registryStore, result);
      }

      if (registryStore != null && result.RegistryNamesUpdated > 0)
      {
        try
        {
          registryStore.Save();
          Logger.Info(
              "Velum DXF diagnostics registry name sync: updated=" + result.RegistryNamesUpdated);
        }
        catch (Exception ex)
        {
          Logger.Warning("Velum DXF diagnostics registry save: " + ex.Message);
        }
      }

      CollectOrphanDxfFiles(dxfFiles, result.Rows, result.OrphanDxfFiles);

      VelumDxfBatchDocumentHelper.TryCloseOpenPartsByPaths(
          request.SwApp,
          partFiles,
          request.KeepOpenPartPaths);

      return result;
    }

    private static void RunPhase2ForPart(
        ScanRequest request,
        string partPath,
        string dxfRoot,
        List<VelumDxfBatchDiagnosticRow> rows,
        VelumProductRegistryStore registryStore,
        ScanResult scanResult)
    {
      ISwApplication swApp = request?.SwApp;
      string displayName = Path.GetFileName(partPath);
      ModelDoc2 modelDoc = VelumDxfBatchDocumentHelper.TryOpenPartSilent(swApp, partPath, out _);
      if (modelDoc == null)
        return;

      try
      {
        if (registryStore != null && scanResult != null)
        {
          VelumProductRegistryNameSyncOutcome outcome =
              VelumProductRegistryNameSync.TrySyncOpenDocument(
                  registryStore,
                  swApp,
                  partPath,
                  modelDoc,
                  persist: false,
                  out _);
          if (outcome == VelumProductRegistryNameSyncOutcome.Updated)
            scanResult.RegistryNamesUpdated++;
        }

        string partCatalog = VelumDxfBatchDocumentHelper.TryReadDxfCatalog(modelDoc);
        bool isFirstExport = string.IsNullOrWhiteSpace(partCatalog);
        string catalog = partCatalog;

        IReadOnlyList<string> configs = VelumDxfArtifactResolver.TryGetConfigurationNames(modelDoc);
        int exportableCount = VelumDxfNeedFlagResolver.CountExportable(modelDoc);
        for (int i = 0; i < configs.Count; i++)
        {
          string configName = configs[i];
          bool hasNeedFlag = VelumDxfBatchDocumentHelper.TryReadNeedDxf(modelDoc, configName, out bool needDxf);
          VelumDxfBatchDiagnosticRow row = ClassifyConfiguration(
              modelDoc,
              partPath,
              displayName,
              configName,
              catalog,
              hasNeedFlag,
              needDxf,
              isFirstExport,
              exportableCount);
          if (row != null)
            rows.Add(row);
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

    internal static IReadOnlyList<string> CollectConfigsWithDxfFiles(
        ModelDoc2 modelDoc,
        string catalogPath)
    {
      var names = new List<string>();
      if (modelDoc == null)
        return names;

      IReadOnlyList<string> configs = VelumDxfArtifactResolver.TryGetConfigurationNames(modelDoc);
      for (int i = 0; i < configs.Count; i++)
      {
        string configName = configs[i];
        if (VelumDxfArtifactResolver.Resolve(modelDoc, catalogPath, configName).Found)
          names.Add(configName);
      }

      return names;
    }

    internal static void RefreshPartRows(
        ModelDoc2 modelDoc,
        string partPath,
        string dxfRoot,
        IList<VelumDxfBatchDiagnosticRow> rows)
    {
      if (modelDoc == null || rows == null || rows.Count == 0)
        return;

      string normalizedPartPath = (partPath ?? string.Empty).Trim();
      if (normalizedPartPath.Length == 0)
        return;

      string partCatalog = VelumDxfBatchDocumentHelper.TryReadDxfCatalog(modelDoc);
      bool isFirstExport = string.IsNullOrWhiteSpace(partCatalog);
      string catalog = partCatalog;
      int exportableCount = VelumDxfNeedFlagResolver.CountExportable(modelDoc);

      for (int i = rows.Count - 1; i >= 0; i--)
      {
        VelumDxfBatchDiagnosticRow row = rows[i];
        if (row == null ||
            !string.Equals(row.PartPath, normalizedPartPath, StringComparison.OrdinalIgnoreCase))
          continue;

        bool hasNeedFlag = VelumDxfBatchDocumentHelper.TryReadNeedDxf(
            modelDoc,
            row.ConfigName,
            out bool needDxf);
        VelumDxfBatchDiagnosticRow refreshed = ClassifyConfiguration(
            modelDoc,
            row.PartPath,
            row.PartDisplayName,
            row.ConfigName,
            catalog,
            hasNeedFlag,
            needDxf,
            isFirstExport,
            exportableCount,
            row.Quantity);
        if (refreshed == null)
        {
          rows.RemoveAt(i);
          continue;
        }

        refreshed.Selected = row.Selected;
        rows[i] = refreshed;
      }
    }

    private static VelumDxfBatchDiagnosticRow CreateEmptyDocumentRow(
        string partPath,
        string displayName,
        string configName)
    {
      return new VelumDxfBatchDiagnosticRow
      {
        PartPath = partPath,
        PartDisplayName = displayName,
        ConfigName = configName,
        Status = VelumDxfBatchRowStatus.EmptyDocument,
        StatusText = "Пустой документ",
        Selected = true,
        DxfPath = string.Empty
      };
    }

    /// <summary>Публичная обёртка классификации для диагностики из реестра изделия.</summary>
    internal static VelumDxfBatchDiagnosticRow ClassifyConfigurationPublic(
        ModelDoc2 modelDoc,
        string partPath,
        string displayName,
        string configName,
        string catalog,
        bool hasNeedFlag,
        bool needDxf,
        bool isFirstExport,
        int? quantity = null,
        int prefixConfigurationCount = -1)
    {
      return ClassifyConfiguration(
          modelDoc,
          partPath,
          displayName,
          configName,
          catalog,
          hasNeedFlag,
          needDxf,
          isFirstExport,
          prefixConfigurationCount,
          quantity);
    }

    private static VelumDxfBatchDiagnosticRow ClassifyConfiguration(
        ModelDoc2 modelDoc,
        string partPath,
        string displayName,
        string configName,
        string catalog,
        bool hasNeedFlag,
        bool needDxf,
        bool isFirstExport,
        int prefixConfigurationCount,
        int? quantity = null)
    {
      if (VelumDxfPartGeometryHelper.IsEmptyDocument(modelDoc))
      {
        if (!needDxf)
          return null;
        VelumDxfBatchDiagnosticRow empty = CreateEmptyDocumentRow(partPath, displayName, configName);
        empty.Quantity = quantity;
        return empty;
      }

      if (!needDxf)
      {
        if (string.IsNullOrWhiteSpace(catalog) || !Directory.Exists(catalog))
          return null;

        int junkPrefixCount = VelumDxfArtifactResolver.TryGetConfigurationNames(modelDoc).Count;
        ResolvedDxfArtifact junkArtifact = VelumDxfArtifactResolver.Resolve(
            modelDoc,
            catalog,
            configName,
            VelumDxfQuantityToken.DefaultQuantity,
            junkPrefixCount);
        if (!junkArtifact.Found)
          return null;

        return new VelumDxfBatchDiagnosticRow
        {
          PartPath = partPath,
          PartDisplayName = displayName,
          ConfigName = configName,
          Status = VelumDxfBatchRowStatus.Junk,
          StatusText = "Мусорный DXF",
          Selected = true,
          Quantity = quantity,
          DxfPath = junkArtifact.FullPath,
          ExpectedDxfBaseName = junkArtifact.BaseName
        };
      }

      if (isFirstExport)
      {
        return new VelumDxfBatchDiagnosticRow
        {
          PartPath = partPath,
          PartDisplayName = displayName,
          ConfigName = configName,
          Status = VelumDxfBatchRowStatus.FirstExport,
          StatusText = "Первая выгрузка",
          Selected = false,
          Quantity = quantity,
          DxfPath = string.Empty
        };
      }

      if (string.IsNullOrWhiteSpace(catalog) || !Directory.Exists(catalog))
      {
        return new VelumDxfBatchDiagnosticRow
        {
          PartPath = partPath,
          PartDisplayName = displayName,
          ConfigName = configName,
          Status = VelumDxfBatchRowStatus.CatalogUnavailable,
          StatusText = "Каталог из свойства «Путь dxf» недоступен",
          Selected = false,
          Quantity = quantity,
          DxfPath = string.Empty
        };
      }

      int exportPrefixCount = prefixConfigurationCount >= 0
          ? prefixConfigurationCount
          : VelumDxfNeedFlagResolver.CountExportable(modelDoc);
      ResolvedDxfArtifact artifact = VelumDxfArtifactResolver.Resolve(
          modelDoc,
          catalog,
          configName,
          quantity ?? VelumDxfQuantityToken.DefaultQuantity,
          exportPrefixCount);
      bool fileFound = artifact.Found;
      bool outdated = false;

      if (fileFound)
      {
        int currentStamp = 0;
        bool hasCurrentStamp =
            VelumExportDocumentationGeometryStampHelper.TryGetCurrentUpdateStamp(modelDoc, out currentStamp);

        if (!VelumExportDocumentationGeometryStampHelper.TryReadStoredUpdateStamp(
                modelDoc,
                VelumExportDocumentationProperties.DxfGeometryUpdateStamp,
                configName,
                out int storedStamp))
        {
          outdated = true;
        }
        else if (hasCurrentStamp &&
                 VelumExportDocumentationGeometryStampHelper.TryIsPerConfigDxfOutdated(
                     modelDoc,
                     configName,
                     currentStamp,
                     storedStamp))
        {
          outdated = true;
        }
      }

      VelumDxfBatchRowStatus status;
      string statusText;
      bool selected;

      bool hasProjection = VelumDxfFileNameHelper.TryGetProjectionViewProperty(
          modelDoc,
          configName,
          out _);
      if (!hasProjection &&
          VelumSolidSheetMetalHelper.IsSheetMetalPart(modelDoc) &&
          VelumSolidSheetMetalHelper.TryFindFlatPatternFeature(modelDoc) != null)
      {
        // Листовые выгружаются по развёртке — плоскость проекции не требуется.
        hasProjection = true;
      }

      if (artifact.QuantityMismatch && !fileFound)
      {
        status = VelumDxfBatchRowStatus.QuantityOutdated;
        statusText = "Устарело кол-во в имени";
        selected = true;
      }
      else if (!fileFound &&
               string.Equals(artifact.Reason, "dxf_name_property_empty", StringComparison.Ordinal))
      {
        // Свойство «Имя файла dxf» пустое: имя ещё не задано — только ручная первая выгрузка.
        status = VelumDxfBatchRowStatus.FirstExport;
        statusText = "Первая выгрузка";
        selected = false;
      }
      else if (!fileFound)
      {
        if (!hasProjection)
        {
          status = VelumDxfBatchRowStatus.MissingProjection;
          statusText = "Нет плоскости";
          selected = false;
        }
        else
        {
          status = VelumDxfBatchRowStatus.NeedExport;
          statusText = "Нужен экспорт";
          selected = true;
        }
      }
      else if (outdated)
      {
        if (!hasProjection)
        {
          status = VelumDxfBatchRowStatus.MissingProjection;
          statusText = "Нет плоскости";
          selected = false;
        }
        else
        {
          status = VelumDxfBatchRowStatus.Outdated;
          statusText = "Устарел";
          selected = true;
        }
      }
      else
      {
        status = VelumDxfBatchRowStatus.Ok;
        statusText = "OK";
        selected = false;
      }

      return new VelumDxfBatchDiagnosticRow
      {
        PartPath = partPath,
        PartDisplayName = displayName,
        ConfigName = configName,
        Status = status,
        StatusText = statusText,
        Selected = selected,
        DxfPath = artifact.FullPath,
        Quantity = quantity,
        ExpectedDxfBaseName = artifact.BaseName
      };
    }

    private static List<string> CollectPartFiles(string partsRoot)
    {
      var files = new List<string>();
      try
      {
        files.AddRange(Directory.EnumerateFiles(partsRoot, "*.sldprt", PartSearchOption));
      }
      catch
      {
      }

      return files;
    }

    private static List<string> CollectDxfFiles(string dxfRoot)
    {
      var files = new List<string>();
      try
      {
        files.AddRange(Directory.EnumerateFiles(dxfRoot, "*.dxf", DxfSearchOption));
      }
      catch
      {
      }

      return files;
    }

    private static void CollectOrphanDxfFiles(
        IReadOnlyList<string> dxfFiles,
        IReadOnlyList<VelumDxfBatchDiagnosticRow> rows,
        List<string> orphanDxfFiles)
    {
      if (dxfFiles == null || dxfFiles.Count == 0 || orphanDxfFiles == null)
        return;

      var claimedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      if (rows != null)
      {
        for (int i = 0; i < rows.Count; i++)
        {
          string path = rows[i]?.DxfPath;
          if (!string.IsNullOrWhiteSpace(path))
            claimedPaths.Add(path);
        }
      }

      for (int i = 0; i < dxfFiles.Count; i++)
      {
        string path = dxfFiles[i];
        if (!string.IsNullOrWhiteSpace(path) && !claimedPaths.Contains(path))
          orphanDxfFiles.Add(path);
      }
    }
  }
}
