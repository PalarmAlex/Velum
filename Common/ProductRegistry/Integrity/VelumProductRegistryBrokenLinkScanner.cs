using System;
using System.Collections.Generic;

namespace Velum.UI.ProductRegistry
{
  /// <summary>
  /// Скан битых ссылок реестра по всему каталогу (без early-stop).
  /// Discovery-проход копит в pending; по завершении — атомарный commit в кэш формы/метрик.
  /// Проходы циклически повторяются, пока флот активен (нет ActiveDoc).
  /// </summary>
  internal static class VelumProductRegistryBrokenLinkScanner
  {
    /// <summary>Квант File.Exists за тик при полном скане реестра (без открытых документов).</summary>
    private const int FullRegistryBatchSize = 120;

    /// <summary>
    /// Выполняет квант полного прохода. Возвращает true, если за этот тик
    /// завершён полный проход реестра (с начала до конца) и pending закоммичен.
    /// </summary>
    /// <param name="store">Реестр изделий.</param>
    /// <param name="searchCursor">Курсор прохода (сохраняется между тиками).</param>
    /// <param name="passActive">Флаг активного прохода (begin/commit pending).</param>
    /// <param name="scopedItemIds">Ограничение области (открытые документы) или null.</param>
    /// <param name="openDocumentCount">Число открытых документов для расчёта бюджета scoped-режима.</param>
    /// <param name="shouldStop">
    /// Возвращает true, когда тик нужно прервать (наступил следующий тяжёлый пульс
    /// либо открылся документ SW). При null — прежнее поведение с фиксированным квантом.
    /// </param>
    internal static bool Tick(
        VelumProductRegistryStore store,
        ref int searchCursor,
        ref bool passActive,
        HashSet<int> scopedItemIds,
        int openDocumentCount,
        Func<bool> shouldStop = null)
    {
      bool passCompleted = false;
      if (store == null)
        return false;

      IReadOnlyList<VelumProductItem> items = ResolveItems(store, scopedItemIds);
      if (items.Count == 0)
      {
        if (passActive)
        {
          VelumProductRegistryProblemCache.CommitPendingPass(
              VelumProductRegistryProblemKind.BrokenLink);
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
        VelumProductRegistryProblemCache.BeginPendingPass(
            VelumProductRegistryProblemKind.BrokenLink);
      }

      if (searchCursor < 0 || searchCursor > items.Count)
        searchCursor = 0;

      // Полный реестр с временным бюджетом: идём до следующего тяжёлого пульса
      // (shouldStop), а не фиксированным квантом количества. Scoped-режим —
      // прежнее поведение через ResolveBudget.
      int budget = shouldStop != null
          ? Math.Max(0, items.Count - searchCursor)
          : ResolveBudget(scopedItemIds == null, openDocumentCount, items.Count, searchCursor);
      int checkedCount = 0;
      while (checkedCount < budget && searchCursor < items.Count
          && (shouldStop == null || !shouldStop()))
      {
        VelumProductItem item = items[searchCursor++];
        checkedCount++;
        if (item == null || item.Id <= 0)
          continue;

        if (!TryEvaluateBroken(item, out VelumProductRegistryProblemEntry problem))
          continue;

        VelumProductRegistryProblemCache.UpsertPending(problem);
      }

      if (searchCursor >= items.Count)
      {
        VelumProductRegistryProblemCache.CommitPendingPass(
            VelumProductRegistryProblemKind.BrokenLink);
        searchCursor = 0;
        passActive = false;
        passCompleted = true;
      }

      return passCompleted;
    }

    /// <summary>
    /// Полный реестр: квант FullRegistryBatchSize до конца списка.
    /// Открытые документы: за тик — все позиции области (глубина ≈ числу открытых / размеру scope).
    /// </summary>
    private static int ResolveBudget(
        bool fullRegistry,
        int openDocumentCount,
        int itemCount,
        int cursor)
    {
      int remaining = Math.Max(0, itemCount - cursor);
      if (fullRegistry)
        return Math.Min(FullRegistryBatchSize, remaining);

      int depth = Math.Max(1, openDocumentCount);
      if (itemCount > 0)
        depth = Math.Max(depth, itemCount);
      return Math.Min(depth, remaining);
    }

    internal static bool TryEvaluateBroken(
        VelumProductItem item,
        out VelumProductRegistryProblemEntry problem)
    {
      problem = null;
      if (item == null || item.Id <= 0)
        return false;

      string path = VelumProductRegistryStore.NormalizeFilePathKey(item.FilePath);
      if (string.IsNullOrEmpty(path))
      {
        problem = Build(item, "Путь к файлу пуст.");
        return true;
      }

      if (VelumProductRegistryIntegrityRules.PathExistsOrTimedOutIsMissing(path))
      {
        problem = Build(item, "Файл по ссылке не найден или недоступен (в т.ч. timeout UNC).");
        return true;
      }

      return false;
    }

    private static IReadOnlyList<VelumProductItem> ResolveItems(
        VelumProductRegistryStore store,
        HashSet<int> scopedItemIds)
    {
      if (scopedItemIds == null)
        return store.GetAllItems();
      return VelumProductRegistryOpenDocumentsScope.FilterItems(store, scopedItemIds);
    }

    private static VelumProductRegistryProblemEntry Build(VelumProductItem item, string detail)
    {
      return new VelumProductRegistryProblemEntry
      {
        ItemId = item.Id,
        Kind = VelumProductRegistryProblemKind.BrokenLink,
        Designation = item.Designation,
        Name = item.Name,
        FilePath = item.FilePath,
        Detail = detail
      };
    }
  }
}
