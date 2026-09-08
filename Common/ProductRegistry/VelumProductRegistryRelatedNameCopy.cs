using System;
using System.Collections.Generic;

namespace Velum.UI.ProductRegistry
{
  /// <summary>
  /// Копирование поля <see cref="VelumProductItem.Name"/> с деталей/сборок
  /// на одноименные записи чертежей, DXF и PDF.
  /// </summary>
  internal static class VelumProductRegistryRelatedNameCopy
  {
    /// <summary>
    /// После sync «Наименование» деталей/сборок копирует Name в связанные документы.
    /// Цели: выделенные .slddrw/.dxf/.pdf и все такие записи реестра,
    /// чей ключ совпадает с выделенной деталью/сборкой.
    /// Для DXF ключ — первая часть обозначения до «:».
    /// Пустое имя источника не затирает цель.
    /// </summary>
    internal static VelumProductRegistryNameSyncBatchResult CopyFromModels(
        VelumProductRegistryStore store,
        IList<VelumProductItem> selectedItems)
    {
      var result = new VelumProductRegistryNameSyncBatchResult();
      if (store == null || selectedItems == null || selectedItems.Count == 0)
        return result;

      var nameByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      var modelKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      var selectedModelKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

      IReadOnlyList<VelumProductItem> allItems = store.GetAllItems();
      for (int i = 0; i < allItems.Count; i++)
      {
        VelumProductItem item = allItems[i];
        if (item == null || item.Id <= 0)
          continue;
        if (!VelumProductRegistryIntegrityRules.IsPartOrAssemblyPath(item.FilePath))
          continue;

        string key = VelumProductRegistryMatchKey.FromItem(item);
        if (key.Length == 0)
          continue;

        modelKeys.Add(key);
        string name = (item.Name ?? string.Empty).Trim();
        if (name.Length == 0)
          continue;

        if (!nameByKey.ContainsKey(key))
          nameByKey[key] = name;
      }

      for (int i = 0; i < selectedItems.Count; i++)
      {
        VelumProductItem item = selectedItems[i];
        if (item == null || item.Id <= 0)
          continue;
        if (!VelumProductRegistryIntegrityRules.IsPartOrAssemblyPath(item.FilePath))
          continue;

        string key = VelumProductRegistryMatchKey.FromItem(item);
        if (key.Length == 0)
          continue;

        string name = (item.Name ?? string.Empty).Trim();
        if (name.Length == 0)
          continue;

        nameByKey[key] = name;
        selectedModelKeys.Add(key);
      }

      var targetIds = new HashSet<int>();
      for (int i = 0; i < selectedItems.Count; i++)
      {
        VelumProductItem item = selectedItems[i];
        if (item == null || item.Id <= 0)
          continue;
        if (VelumProductRegistryIntegrityRules.IsRelatedDocumentPath(item.FilePath))
          targetIds.Add(item.Id);
      }

      if (selectedModelKeys.Count > 0)
      {
        for (int i = 0; i < allItems.Count; i++)
        {
          VelumProductItem item = allItems[i];
          if (item == null || item.Id <= 0)
            continue;
          if (!VelumProductRegistryIntegrityRules.IsRelatedDocumentPath(item.FilePath))
            continue;

          string key = VelumProductRegistryMatchKey.FromRelatedDocument(item);
          if (key.Length == 0 || !selectedModelKeys.Contains(key))
            continue;
          targetIds.Add(item.Id);
        }
      }

      if (targetIds.Count == 0)
        return result;

      foreach (int id in targetIds)
      {
        VelumProductItem target = store.GetItem(id);
        if (target == null)
        {
          result.Add(VelumProductRegistryNameSyncOutcome.Failed);
          continue;
        }

        string key = VelumProductRegistryMatchKey.FromRelatedDocument(target);
        if (key.Length == 0)
        {
          result.Add(VelumProductRegistryNameSyncOutcome.NotInRegistry);
          continue;
        }

        string sourceName;
        if (!nameByKey.TryGetValue(key, out sourceName) || string.IsNullOrWhiteSpace(sourceName))
        {
          result.Add(
              modelKeys.Contains(key)
                  ? VelumProductRegistryNameSyncOutcome.SkippedEmpty
                  : VelumProductRegistryNameSyncOutcome.NotInRegistry);
          continue;
        }

        result.Add(VelumProductRegistryNameSync.TryApplySwName(
            store,
            target,
            sourceName,
            persist: false));
      }

      return result;
    }
  }
}
