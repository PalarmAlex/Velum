using System;
using System.Collections.Generic;
using ISIDA.Common;
using Velum.Configuration;
using Velum.UI.ProductRegistry;

namespace Velum.UI.AssemblyRegistry
{
  /// <summary>
  /// Глобальное сканирование расхождений BOM-хэшей.
  /// Работает <b>только с данными</b> VelumAssemblyBomMirrorStore — без COM,
  /// без открытия документов SolidWorks.
  /// </summary>
  internal static class VelumAssemblyBomDiffProbe
  {
    /// <summary>Ключ метрики расхождения BOM-хэшей.</summary>
    public const string ProbeKey = "Velum.Assembly.BomDiff";

    /// <summary>
    /// Выполнить сканирование: сравнить currentHash vs previousHash.
    /// Результаты пишутся в VelumProductRegistryProblemCache.
    /// </summary>
    public static void RunScan()
    {
      try
      {
        // Load the mirror store.
        VelumAssemblyBomMirrorStore store = new VelumAssemblyBomMirrorStore();
        store.Load();

        // Обновить пометку stale по фактическому наличию файлов на диске и сохранить,
        // чтобы форма экспорта читала счётчики без обращения к диску.
        store.MarkMissingFilesStale();
        store.Save();

        // Get all discrepancy entries.
        IReadOnlyList<VelumAssemblyBomMirrorEntry> discrepancies = store.GetDiscrepancyEntries();

        // Позиции вне реестра изделий не участвуют в обмене с 1С, если оператор включил
        // фильтр «только зарегистрированные» (см. форму экспорта и bom_exchange_export).
        // Без этой же фильтрации метрика BomDiff давила бы вечно: экспорт сбрасывает
        // previousHash только у выгруженных карточек, а незарегистрированные остались бы
        // в списке расхождений навсегда.
        if (VelumAppConfig.BomExchangeOnlyRegisteredInProductRegistry)
        {
          VelumBomExchangeRegistryIndex registryIndex = VelumBomExchangeRegistryIndex.Load();
          if (registryIndex.Available)
          {
            var registered = new List<VelumAssemblyBomMirrorEntry>(discrepancies.Count);
            foreach (VelumAssemblyBomMirrorEntry mirror in discrepancies)
            {
              if (registryIndex.IsRegistered(mirror))
                registered.Add(mirror);
            }
            discrepancies = registered;
          }
        }

        // Проход по образцу остальных сканеров: pending → commit. Без commit-фазы
        // проблема, у которой расхождение исчезло (экспорт сбросил previousHash,
        // файл удалён из зеркала), жила бы в кэше до следующего полностью пустого
        // прохода — то есть метрика не отпускала бы, пока в реестре есть хоть одна
        // расходящаяся карточка.
        VelumProductRegistryProblemCache.BeginPendingPass(
            VelumProductRegistryProblemKind.BomDiff);

        try
        {
          if (discrepancies.Count == 0)
            return;

          PublishProblems(discrepancies);
        }
        finally
        {
          VelumProductRegistryProblemCache.CommitPendingPass(
              VelumProductRegistryProblemKind.BomDiff);
        }
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum bomDiff probe: " + ex.Message);
      }
    }

    /// <summary>
    /// Поставить находки прохода в pending кэша проблем реестра.
    /// </summary>
    /// <param name="discrepancies">Записи зеркала с неотправленным расхождением.</param>
    private static void PublishProblems(
        IReadOnlyList<VelumAssemblyBomMirrorEntry> discrepancies)
    {
      // Load the product registry to resolve ItemId → Designation/Name.
      VelumProductRegistryStore regStore = new VelumProductRegistryStore();
      regStore.Load();

      // Map discrepancy entries to registry ItemId.
      var entriesByItemId = new Dictionary<int, List<VelumAssemblyBomMirrorEntry>>();

      foreach (VelumAssemblyBomMirrorEntry mirror in discrepancies)
      {
        int itemId = FindItemId(regStore, mirror.Identity, mirror.FilePath);
        if (itemId <= 0)
        {
          // No registry entry — the BomDiff problem cannot be keyed (the cache
          // ignores ItemId <= 0 for all kinds except MissingRegistryEntry),
          // such positions do not press on the metric.
          continue;
        }

        if (!entriesByItemId.TryGetValue(itemId, out var list))
        {
          list = new List<VelumAssemblyBomMirrorEntry>();
          entriesByItemId[itemId] = list;
        }
        list.Add(mirror);
      }

      // Upsert problems for each item with discrepancies.
      foreach (KeyValuePair<int, List<VelumAssemblyBomMirrorEntry>> kv in entriesByItemId)
      {
        int itemId = kv.Key;
        List<VelumAssemblyBomMirrorEntry> mirrors = kv.Value;

        VelumProductItem item = regStore.GetItem(itemId);
        string designation = item?.Designation ?? mirrors[0].Designation ?? string.Empty;
        string name = item?.Name ?? mirrors[0].Name ?? string.Empty;

        var details = new List<string>(mirrors.Count);
        foreach (VelumAssemblyBomMirrorEntry mirror in mirrors)
        {
          details.Add(mirror.ConfigurationName ?? string.Empty);
        }

        VelumProductRegistryProblemEntry entry = new VelumProductRegistryProblemEntry
        {
          ItemId = itemId,
          Kind = VelumProductRegistryProblemKind.BomDiff,
          Designation = designation,
          Name = name,
          FilePath = item?.FilePath ?? mirrors[0].FilePath,
          Detail = "Расхождение BOM-хэша: " + string.Join(", ", details)
        };
        VelumProductRegistryProblemCache.UpsertPending(entry);
      }
    }

    /// <summary>
    /// Найти ItemId в реестре по нормализованному пути файла.
    /// </summary>
    private static int FindItemId(
        VelumProductRegistryStore store,
        string identity,
        string filePath)
    {
      if (string.IsNullOrWhiteSpace(filePath))
        return 0;

      VelumProductItem item = store.FindItemByFilePath(filePath);
      return item?.Id ?? 0;
    }
  }
}
