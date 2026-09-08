using System;
using System.Collections.Generic;
using ISIDA.Common;
using SolidWorks.Interop.sldworks;
using Velum.UI;
using Xarial.XCad.SolidWorks;

namespace Velum.UI.ProductRegistry
{
  /// <summary>Исход синхронизации «Наименование» SW → <see cref="VelumProductItem.Name"/>.</summary>
  internal enum VelumProductRegistryNameSyncOutcome
  {
    Updated,
    Unchanged,
    SkippedEmpty,
    NotInRegistry,
    Unsupported,
    DocumentNotOpen,
    Failed
  }

  /// <summary>Сводка пакетной синхронизации наименований.</summary>
  internal sealed class VelumProductRegistryNameSyncBatchResult
  {
    public int Updated { get; set; }

    public int Unchanged { get; set; }

    public int SkippedEmpty { get; set; }

    public int NotInRegistry { get; set; }

    public int Unsupported { get; set; }

    public int DocumentNotOpen { get; set; }

    public int Failed { get; set; }

    public void Add(VelumProductRegistryNameSyncOutcome outcome)
    {
      switch (outcome)
      {
        case VelumProductRegistryNameSyncOutcome.Updated:
          Updated++;
          break;
        case VelumProductRegistryNameSyncOutcome.Unchanged:
          Unchanged++;
          break;
        case VelumProductRegistryNameSyncOutcome.SkippedEmpty:
          SkippedEmpty++;
          break;
        case VelumProductRegistryNameSyncOutcome.NotInRegistry:
          NotInRegistry++;
          break;
        case VelumProductRegistryNameSyncOutcome.Unsupported:
          Unsupported++;
          break;
        case VelumProductRegistryNameSyncOutcome.DocumentNotOpen:
          DocumentNotOpen++;
          break;
        default:
          Failed++;
          break;
      }
    }
  }

  /// <summary>
  /// Фоновая и пакетная синхронизация поля <c>Name</c> реестра из свойства «Наименование».
  /// Ключ сопоставления — нормализованный путь файла. Без OpenDoc в фоновых сценариях.
  /// </summary>
  internal static class VelumProductRegistryNameSync
  {
    private static readonly object DiskSyncGate = new object();
    private static string _lastActiveSyncedPath = string.Empty;
    private static string _lastActiveSyncedSwName = string.Empty;

    /// <summary>
    /// Применить уже прочитанное «Наименование» к записи (без чтения SW).
    /// Пустое значение не затирает <see cref="VelumProductItem.Name"/>.
    /// </summary>
    internal static VelumProductRegistryNameSyncOutcome TryApplySwName(
        VelumProductRegistryStore store,
        VelumProductItem item,
        string swName,
        bool persist)
    {
      if (store == null || item == null)
        return VelumProductRegistryNameSyncOutcome.Failed;

      if (string.IsNullOrWhiteSpace(swName))
        return VelumProductRegistryNameSyncOutcome.SkippedEmpty;

      string trimmed = swName.Trim();
      if (string.Equals(item.Name ?? string.Empty, trimmed, StringComparison.Ordinal))
        return VelumProductRegistryNameSyncOutcome.Unchanged;

      item.Name = trimmed;
      try
      {
        store.UpdateItem(item, persist);
        return VelumProductRegistryNameSyncOutcome.Updated;
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum registry name sync apply: " + ex.Message);
        return VelumProductRegistryNameSyncOutcome.Failed;
      }
    }

    /// <summary>
    /// Sync по пути для уже открытого документа (без <c>OpenDoc6</c>).
    /// </summary>
    internal static VelumProductRegistryNameSyncOutcome TrySyncOpenDocument(
        VelumProductRegistryStore store,
        ISwApplication swApp,
        string filePath,
        ModelDoc2 modelDocOrNull,
        bool persist,
        out string error)
    {
      error = string.Empty;
      if (store == null)
      {
        error = "store null";
        return VelumProductRegistryNameSyncOutcome.Failed;
      }

      string path = VelumProductRegistryStore.NormalizeFilePathKey(filePath);
      if (string.IsNullOrEmpty(path))
      {
        error = "Путь пуст";
        return VelumProductRegistryNameSyncOutcome.Failed;
      }

      if (!VelumProductRegistryNameSyncHelper.IsNameSyncSupported(path))
        return VelumProductRegistryNameSyncOutcome.Unsupported;

      VelumProductItem item = store.FindItemByFilePath(path);
      if (item == null)
        return VelumProductRegistryNameSyncOutcome.NotInRegistry;

      if (!VelumProductRegistryNameSyncHelper.TryReadNameFromOpenDocumentOnly(
              swApp,
              path,
              modelDocOrNull,
              out string swName,
              out error))
      {
        if (string.Equals(error, "Документ не открыт", StringComparison.Ordinal))
          return VelumProductRegistryNameSyncOutcome.DocumentNotOpen;
        return VelumProductRegistryNameSyncOutcome.Failed;
      }

      return TryApplySwName(store, item, swName, persist);
    }

    /// <summary>
    /// Sync выбранной записи с возможным <c>OpenDoc6</c> (команда «Обновить свойства»).
    /// </summary>
    internal static VelumProductRegistryNameSyncOutcome TrySyncItemAllowOpen(
        VelumProductRegistryStore store,
        ISwApplication swApp,
        VelumProductItem item,
        bool persist,
        out string error)
    {
      error = string.Empty;
      if (store == null || item == null)
      {
        error = "item/store null";
        return VelumProductRegistryNameSyncOutcome.Failed;
      }

      if (!VelumProductRegistryNameSyncHelper.IsNameSyncSupported(item.FilePath))
        return VelumProductRegistryNameSyncOutcome.Unsupported;

      if (!VelumProductRegistryNameSyncHelper.TryReadDesignationName(
              swApp,
              item.FilePath,
              out string swName,
              out error))
        return VelumProductRegistryNameSyncOutcome.Failed;

      return TryApplySwName(store, item, swName, persist);
    }

    /// <summary>
    /// Пакетный sync открытых путей. Один <see cref="VelumProductRegistryStore.Save"/> в конце при updates.
    /// </summary>
    internal static VelumProductRegistryNameSyncBatchResult SyncOpenPaths(
        VelumProductRegistryStore store,
        ISwApplication swApp,
        IEnumerable<string> filePaths,
        bool saveIfUpdated)
    {
      var result = new VelumProductRegistryNameSyncBatchResult();
      if (store == null || filePaths == null)
        return result;

      var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (string raw in filePaths)
      {
        string path = VelumProductRegistryStore.NormalizeFilePathKey(raw);
        if (string.IsNullOrEmpty(path) || !seen.Add(path))
          continue;

        VelumProductRegistryNameSyncOutcome outcome = TrySyncOpenDocument(
            store,
            swApp,
            path,
            modelDocOrNull: null,
            persist: false,
            out _);
        result.Add(outcome);
      }

      if (saveIfUpdated && result.Updated > 0)
      {
        try
        {
          store.Save();
        }
        catch (Exception ex)
        {
          Logger.Warning("Velum registry name sync save: " + ex.Message);
        }
      }

      return result;
    }

    /// <summary>
    /// Load с диска → sync открытых → Save. Пропуск, пока открыта форма реестра.
    /// </summary>
    internal static VelumProductRegistryNameSyncBatchResult SyncOpenPathsFromDisk(
        ISwApplication swApp,
        IEnumerable<string> filePaths)
    {
      var empty = new VelumProductRegistryNameSyncBatchResult();
      if (swApp?.Sw == null || filePaths == null)
        return empty;

      if (VelumProductRegistryFormHost.IsOpen)
        return empty;

      lock (DiskSyncGate)
      {
        try
        {
          var store = new VelumProductRegistryStore();
          store.Load();
          VelumProductRegistryNameSyncBatchResult result = SyncOpenPaths(
              store,
              swApp,
              filePaths,
              saveIfUpdated: true);
          if (result.Updated > 0)
          {
            Logger.Info(
                "Velum registry name sync: updated=" + result.Updated
                + " unchanged=" + result.Unchanged
                + " empty=" + result.SkippedEmpty
                + " missing=" + result.NotInRegistry);
          }

          return result;
        }
        catch (Exception ex)
        {
          Logger.Warning("Velum registry name sync from disk: " + ex.Message);
          return empty;
        }
      }
    }

    /// <summary>Sync активного документа SW по FilePath (без OpenDoc).</summary>
    internal static void TrySyncActiveDocument(ISwApplication swApp)
    {
      if (swApp?.Sw == null)
        return;
      if (VelumProductRegistryFormHost.IsOpen)
        return;

      ModelDoc2 active = null;
      string path = string.Empty;
      try
      {
        active = swApp.Sw.IActiveDoc2 as ModelDoc2;
        if (active != null)
          path = active.GetPathName();
      }
      catch
      {
        return;
      }

      path = VelumProductRegistryStore.NormalizeFilePathKey(path);
      if (string.IsNullOrEmpty(path))
        return;
      if (!VelumProductRegistryNameSyncHelper.IsNameSyncSupported(path))
        return;

      string swName;
      try
      {
        swName = VelumProductRegistryNameSyncHelper.ReadNameProperty(active);
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum registry name sync active read: " + ex.Message);
        return;
      }

      string trimmed = (swName ?? string.Empty).Trim();
      if (string.Equals(_lastActiveSyncedPath, path, StringComparison.OrdinalIgnoreCase)
          && string.Equals(_lastActiveSyncedSwName, trimmed, StringComparison.Ordinal))
        return;

      if (string.IsNullOrEmpty(trimmed))
      {
        _lastActiveSyncedPath = path;
        _lastActiveSyncedSwName = string.Empty;
        return;
      }

      lock (DiskSyncGate)
      {
        try
        {
          var store = new VelumProductRegistryStore();
          store.Load();
          VelumProductRegistryNameSyncOutcome outcome = TrySyncOpenDocument(
              store,
              swApp,
              path,
              active,
              persist: true,
              out _);
          if (outcome == VelumProductRegistryNameSyncOutcome.Updated
              || outcome == VelumProductRegistryNameSyncOutcome.Unchanged
              || outcome == VelumProductRegistryNameSyncOutcome.NotInRegistry
              || outcome == VelumProductRegistryNameSyncOutcome.SkippedEmpty)
          {
            _lastActiveSyncedPath = path;
            _lastActiveSyncedSwName = trimmed;
          }

          if (outcome == VelumProductRegistryNameSyncOutcome.Updated)
            Logger.Info("Velum registry name sync active: updated \"" + path + "\"");
        }
        catch (Exception ex)
        {
          Logger.Warning("Velum registry name sync active: " + ex.Message);
        }
      }
    }
  }
}
