using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Velum.ReactiveCore.Export;

namespace Velum.UI.ProductRegistry
{
    /// <summary>
    /// Флот-скан DXF по зеркалу реестра + FS (без OpenDoc).
    /// Только при полном проходе (нет активного документа).
    /// Весь реестр сканируется циклически без early-stop по уже найденным проблемам.
    /// На item — один DXF-вид (наивысший приоритет); детали конфигураций в Detail.
    /// </summary>
  internal static class VelumProductRegistryDxfFleetScanner
  {
    private const int FullRegistryBatchSize = 40;

    internal static readonly VelumProductRegistryProblemKind[] DxfKinds =
    {
      VelumProductRegistryProblemKind.NeedDxfExport,
      VelumProductRegistryProblemKind.OutdatedDxf,
      VelumProductRegistryProblemKind.MissingDxfProjection,
      VelumProductRegistryProblemKind.DxfCatalogUnavailable,
      VelumProductRegistryProblemKind.DxfFirstExport,
      VelumProductRegistryProblemKind.JunkDxf
    };

    /// <summary>
    /// Выполняет квант полного прохода. Возвращает true, если за этот тик
    /// завершён полный проход реестра (с начала до конца) и pending закоммичен.
    /// </summary>
    /// <param name="store">Реестр изделий.</param>
    /// <param name="searchCursor">Курсор прохода (сохраняется между тиками).</param>
    /// <param name="passActive">Флаг активного прохода (begin/commit pending).</param>
    /// <param name="shouldStop">
    /// Возвращает true, когда тик нужно прервать (наступил следующий тяжёлый пульс
    /// либо открылся документ SW). При null — прежнее поведение с фиксированным квантом.
    /// </param>
    internal static bool Tick(
        VelumProductRegistryStore store,
        ref int searchCursor,
        ref bool passActive,
        Func<bool> shouldStop = null)
    {
      bool passCompleted = false;
      if (store == null)
        return false;

      IReadOnlyList<VelumProductItem> items = store.GetAllItems();
      if (items.Count == 0)
      {
        if (passActive)
        {
          CommitAllPending();
          passCompleted = true;
        }
        searchCursor = 0;
        passActive = false;
        return passCompleted;
      }

      if (!passActive)
      {
        passActive = true;
        searchCursor = 0;
        BeginAllPending();
      }

      if (searchCursor < 0 || searchCursor > items.Count)
        searchCursor = 0;

      int remaining = Math.Max(0, items.Count - searchCursor);
      int budget = shouldStop != null ? remaining : Math.Min(FullRegistryBatchSize, remaining);
      int checkedCount = 0;
      while (checkedCount < budget && searchCursor < items.Count
          && (shouldStop == null || !shouldStop()))
      {
        VelumProductItem item = items[searchCursor++];
        checkedCount++;
        EvaluateItem(item);
      }

      if (searchCursor >= items.Count)
      {
        CommitAllPending();
        searchCursor = 0;
        passActive = false;
        passCompleted = true;
      }

      return passCompleted;
    }

    internal static void ClearAllKinds()
    {
      for (int i = 0; i < DxfKinds.Length; i++)
        VelumProductRegistryProblemCache.RemoveAllOfKind(DxfKinds[i]);
    }

    internal static void DiscardPending()
    {
      for (int i = 0; i < DxfKinds.Length; i++)
        VelumProductRegistryProblemCache.DiscardPendingPass(DxfKinds[i]);
    }

    internal static void RevalidateItem(VelumProductItem item, bool writePending)
    {
      if (item == null || item.Id <= 0)
        return;

      RemoveItemKinds(item.Id);
      IReadOnlyList<VelumProductRegistryProblemEntry> problems = Classify(item);
      for (int i = 0; i < problems.Count; i++)
      {
        if (writePending)
          VelumProductRegistryProblemCache.UpsertPending(problems[i]);
        else
          VelumProductRegistryProblemCache.Upsert(problems[i]);
      }
    }

    private static void BeginAllPending()
    {
      for (int i = 0; i < DxfKinds.Length; i++)
        VelumProductRegistryProblemCache.BeginPendingPass(DxfKinds[i]);
    }

    private static void CommitAllPending()
    {
      for (int i = 0; i < DxfKinds.Length; i++)
        VelumProductRegistryProblemCache.CommitPendingPass(DxfKinds[i]);
    }

    private static void EvaluateItem(VelumProductItem item)
    {
      if (item == null || item.Id <= 0)
        return;
      if (!VelumProductRegistryIntegrityRules.IsPartPath(item.FilePath))
        return;

      IReadOnlyList<VelumProductRegistryProblemEntry> problems = Classify(item);
      for (int i = 0; i < problems.Count; i++)
        VelumProductRegistryProblemCache.UpsertPending(problems[i]);
    }

    private static void RemoveItemKinds(int itemId)
    {
      for (int i = 0; i < DxfKinds.Length; i++)
        VelumProductRegistryProblemCache.Remove(itemId, DxfKinds[i]);
    }

    internal static IReadOnlyList<VelumProductRegistryProblemEntry> Classify(VelumProductItem item)
    {
      var byKind = new Dictionary<VelumProductRegistryProblemKind, StringBuilder>();
      if (item == null)
        return Array.Empty<VelumProductRegistryProblemEntry>();

      // Нет файла модели — флот DXF бессмысленен (выше приоритетом будет BrokenLink).
      string modelPath = VelumProductRegistryStore.NormalizeFilePathKey(item.FilePath);
      if (string.IsNullOrEmpty(modelPath)
          || VelumProductRegistryIntegrityRules.PathExistsOrTimedOutIsMissing(modelPath))
        return Array.Empty<VelumProductRegistryProblemEntry>();

      // Нет признаков sync export-meta — не классифицируем (шум после «Загрузить»).
      // После миграции в зеркало достаточно любого DXF-поля (не только NeedDxf).
      if (!HasDxfMirrorSyncSignal(item))
        return Array.Empty<VelumProductRegistryProblemEntry>();

      string ext = VelumProductRegistryStore.GetDocumentTypeKey(item.FilePath);
      if (string.Equals(ext, ".sldasm", StringComparison.OrdinalIgnoreCase))
        return Array.Empty<VelumProductRegistryProblemEntry>();

      // NeedDxf не записан — по умолчанию DXF не нужен.
      bool itemNeedDxf = item.NeedDxf ?? false;
      // Зеркало может хранить относительный каталог — достраиваем префикс корневого каталога.
      string catalog = VelumRelativeDocumentPathResolver.ToFull((item.DxfPath ?? string.Empty).Trim());
      bool firstExport = string.IsNullOrWhiteSpace(catalog);
      VelumProductExportMetaConfig[] configs = item.ExportMetaConfigs
          ?? Array.Empty<VelumProductExportMetaConfig>();

      if (configs.Length == 0)
      {
        if (itemNeedDxf && firstExport)
          Append(byKind, VelumProductRegistryProblemKind.DxfFirstExport, "Нет «Путь dxf» (первая выгрузка)");
        return ToEntries(item, byKind);
      }

      for (int i = 0; i < configs.Length; i++)
      {
        VelumProductExportMetaConfig cfg = configs[i];
        if (cfg == null)
          continue;

        string configLabel = string.IsNullOrWhiteSpace(cfg.ConfigName)
            ? "(default)"
            : cfg.ConfigName;

        bool needDxf = cfg.NeedDxf ?? itemNeedDxf;

        if (!needDxf)
        {
          string junkPath = TryResolveDxfFullPath(catalog, cfg.DxfFileName);
          if (!string.IsNullOrEmpty(junkPath) && File.Exists(junkPath))
          {
            Append(byKind, VelumProductRegistryProblemKind.JunkDxf,
                "Мусорный DXF [" + configLabel + "]");
          }

          continue;
        }

        if (firstExport)
        {
          Append(byKind, VelumProductRegistryProblemKind.DxfFirstExport,
              "Первая выгрузка [" + configLabel + "]");
          continue;
        }

        if (!Directory.Exists(catalog))
        {
          Append(byKind, VelumProductRegistryProblemKind.DxfCatalogUnavailable,
              "Каталог недоступен [" + configLabel + "]");
          continue;
        }

        string fullPath = TryResolveDxfFullPath(catalog, cfg.DxfFileName);
        bool fileFound = !string.IsNullOrEmpty(fullPath) && File.Exists(fullPath);
        bool hasProjection = !string.IsNullOrWhiteSpace(cfg.DxfProjectionView);
        bool outdated = IsDxfOutdated(cfg);
        bool fingerprintMismatch = false;
        if (fileFound
            && !string.IsNullOrWhiteSpace(cfg.DxfFileFingerprint)
            && VelumDxfFinalizeService.TryBuildFileFingerprint(fullPath, out string currentFp, out _)
            && !string.Equals(cfg.DxfFileFingerprint, currentFp, StringComparison.Ordinal))
        {
          fingerprintMismatch = true;
        }

        if (fingerprintMismatch)
        {
          Append(byKind, VelumProductRegistryProblemKind.JunkDxf, "Подменён [" + configLabel + "]");
          continue;
        }

        if (!fileFound)
        {
          if (!hasProjection)
            Append(byKind, VelumProductRegistryProblemKind.MissingDxfProjection,
                "Нет плоскости [" + configLabel + "]");
          else
            Append(byKind, VelumProductRegistryProblemKind.NeedDxfExport,
                "Нужен экспорт [" + configLabel + "]");
          continue;
        }

        if (outdated)
        {
          if (!hasProjection)
            Append(byKind, VelumProductRegistryProblemKind.MissingDxfProjection,
                "Нет плоскости (устарел) [" + configLabel + "]");
          else
            Append(byKind, VelumProductRegistryProblemKind.OutdatedDxf,
                "Устарел [" + configLabel + "]");
        }
      }

      return ToEntries(item, byKind);
    }

    /// <summary>
    /// Признак, что DXF-зеркало уже писали в реестр (полная или частичная миграция).
    /// </summary>
    private static bool HasDxfMirrorSyncSignal(VelumProductItem item)
    {
      if (item == null)
        return false;
      if (item.NeedDxf.HasValue)
        return true;
      if (item.DxfPath != null)
        return true;
      return item.ExportMetaConfigs != null && item.ExportMetaConfigs.Length > 0;
    }

    private static void Append(
        Dictionary<VelumProductRegistryProblemKind, StringBuilder> byKind,
        VelumProductRegistryProblemKind kind,
        string line)
    {
      StringBuilder sb;
      if (!byKind.TryGetValue(kind, out sb))
      {
        sb = new StringBuilder();
        byKind[kind] = sb;
      }
      else if (sb.Length > 0)
      {
        sb.Append("; ");
      }

      sb.Append(line);
    }

    private static IReadOnlyList<VelumProductRegistryProblemEntry> ToEntries(
        VelumProductItem item,
        Dictionary<VelumProductRegistryProblemKind, StringBuilder> byKind)
    {
      if (byKind == null || byKind.Count == 0)
        return Array.Empty<VelumProductRegistryProblemEntry>();

      // Несколько DXF-видов на item (разные конфигурации) → один наиболее важный.
      bool hasPrimary = false;
      VelumProductRegistryProblemKind primary = VelumProductRegistryProblemKind.NeedDxfExport;
      foreach (VelumProductRegistryProblemKind kind in byKind.Keys)
      {
        if (!hasPrimary
            || VelumProductRegistryProblemKindPriority.Compare(kind, primary) < 0)
        {
          primary = kind;
          hasPrimary = true;
        }
      }

      if (!hasPrimary)
        return Array.Empty<VelumProductRegistryProblemEntry>();

      return new[]
      {
        new VelumProductRegistryProblemEntry
        {
          ItemId = item.Id,
          Kind = primary,
          Designation = item.Designation,
          Name = item.Name,
          FilePath = item.FilePath,
          Detail = byKind[primary].ToString()
        }
      };
    }

    private static bool IsDxfOutdated(VelumProductExportMetaConfig cfg)
    {
      if (cfg == null)
        return false;
      if (!cfg.DxfGeometryUpdateStamp.HasValue || cfg.DxfGeometryUpdateStamp.Value <= 0)
        return true;
      if (cfg.DxfGeometryPendingStamp.HasValue
          && cfg.DxfGeometryPendingStamp.Value > cfg.DxfGeometryUpdateStamp.Value)
        return true;
      return false;
    }

    private static string TryResolveDxfFullPath(string catalog, string baseName)
    {
      string folder = (catalog ?? string.Empty).Trim();
      string name = (baseName ?? string.Empty).Trim();
      if (folder.Length == 0 || name.Length == 0)
        return string.Empty;

      if (name.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase))
        return Path.Combine(folder, name);
      return Path.Combine(folder, name + ".dxf");
    }
  }
}
