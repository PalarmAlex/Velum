using System;
using System.Collections.Generic;

namespace Velum.UI.ProductRegistry
{
  /// <summary>
  /// Скан дублей ключа уникальности «обозначение + расширение файла» по всему реестру.
  /// Группа из двух и более записей с одним ключом даёт проблему на КАЖДУЮ запись
  /// группы (пользователь видит оба конфликта списка).
  /// <para>
  /// Проверке не нужна файловая система - группировка идёт по ключам в памяти за O(n),
  /// поэтому полный проход выполняется целиком внутри одного кванта тяжёлого тика
  /// (<see cref="VelumProductRegistryIntegrityScheduler.RunTick"/>), без курсора между
  /// тиками. Как и остальные сканеры, работает только при отсутствии открытых
  /// документов SW; при открытии документа проход прерывается через
  /// <see cref="DiscardPending"/>.
  /// </para>
  /// <para>
  /// Записи с пустым обозначением в проверке не участвуют (ключ пуст - пропуск),
  /// исторические дубли из JSON не удаляются: их разрешает загрузка стора.
  /// </para>
  /// </summary>
  internal static class VelumProductRegistryDuplicateDesignationScanner
  {
    /// <summary>
    /// Выполняет проход реестра. Весь проход укладывается в один вызов, поэтому
    /// возвращает true при каждом завершённом кванте (включая пустой реестр).
    /// </summary>
    /// <param name="store">Реестр изделий.</param>
    /// <param name="passActive">Флаг активного прохода (begin/commit pending).</param>
    /// <returns>true - полный проход завершён и pending закоммичен.</returns>
    internal static bool Tick(
        VelumProductRegistryStore store,
        ref bool passActive)
    {
      if (store == null)
        return false;

      IReadOnlyList<VelumProductItem> items = store.GetAllItems();
      if (items.Count == 0)
      {
        if (passActive)
        {
          VelumProductRegistryProblemCache.CommitPendingPass(
              VelumProductRegistryProblemKind.DuplicateDesignation);
          passActive = false;
        }

        return true;
      }

      if (!passActive)
      {
        passActive = true;
        VelumProductRegistryProblemCache.BeginPendingPass(
            VelumProductRegistryProblemKind.DuplicateDesignation);
      }

      // Группировка по ключу: ключ - записи (исторические дубли допускаются).
      var groups = new Dictionary<string, List<VelumProductItem>>(StringComparer.OrdinalIgnoreCase);
      foreach (VelumProductItem item in items)
      {
        if (item == null || item.Id <= 0)
          continue;
        string key = VelumProductRegistryStore.BuildDesignationKey(item);
        if (string.IsNullOrEmpty(key))
          continue;

        List<VelumProductItem> group;
        if (!groups.TryGetValue(key, out group))
        {
          group = new List<VelumProductItem>();
          groups[key] = group;
        }

        group.Add(item);
      }

      foreach (KeyValuePair<string, List<VelumProductItem>> kv in groups)
      {
        if (kv.Value.Count < 2)
          continue;

        foreach (VelumProductItem item in kv.Value)
        {
          VelumProductItem conflict = FindOther(item, kv.Value);
          if (conflict == null)
            continue;

          VelumProductRegistryProblemCache.UpsertPending(BuildProblem(item, conflict));
        }
      }

      VelumProductRegistryProblemCache.CommitPendingPass(
          VelumProductRegistryProblemKind.DuplicateDesignation);
      passActive = false;
      return true;
    }

    /// <summary>
    /// Точечная перепроверка одной записи (после правки/удаления): проблема
    /// обновляется в опубликованном кэше или снимается, если конфликт исчез.
    /// </summary>
    /// <param name="store">Реестр изделий.</param>
    /// <param name="item">Проверяемая запись.</param>
    /// <param name="writePending">true - писать в pending незавершённого прохода.</param>
    internal static void RevalidateItem(
        VelumProductRegistryStore store,
        VelumProductItem item,
        bool writePending)
    {
      if (store == null || item == null || item.Id <= 0)
        return;

      if (TryEvaluate(store, item, out VelumProductRegistryProblemEntry problem))
      {
        if (writePending)
          VelumProductRegistryProblemCache.UpsertPending(problem);
        else
          VelumProductRegistryProblemCache.Upsert(problem);
      }
      else
      {
        VelumProductRegistryProblemCache.Remove(
            item.Id, VelumProductRegistryProblemKind.DuplicateDesignation);
      }
    }

    /// <summary>
    /// Перепроверяет только те записи, у которых уже есть опубликованная проблема
    /// этого вида. Применяется после удаления записи: её прежний ключ недоступен,
    /// а устаревшая строка гарантированно лежит в кэше у оставшегося партнёра.
    /// Дешевле полного прохода, т.к. вызывается из UI-потока по каждой удалённой записи.
    /// </summary>
    /// <param name="store">Реестр изделий.</param>
    internal static void RevalidateCachedDuplicates(VelumProductRegistryStore store)
    {
      if (store == null)
        return;

      IReadOnlyList<VelumProductRegistryProblemEntry> cached =
          VelumProductRegistryProblemCache.SnapshotKind(
              VelumProductRegistryProblemKind.DuplicateDesignation);

      for (int i = 0; i < cached.Count; i++)
      {
        VelumProductRegistryProblemEntry entry = cached[i];
        if (entry == null || entry.ItemId <= 0)
          continue;

        VelumProductItem other = store.GetItem(entry.ItemId);
        if (other != null)
          RevalidateItem(store, other, writePending: false);
      }
    }

    /// <summary>
    /// Перепроверяет ВСЕ записи группы указанного ключа (кроме уже обработанных в
    /// <paramref name="visited"/>). Используется точечной перепроверкой: после правки
    /// или удаления одной записи проблема должна сняться со всех остальных участников
    /// группы сразу, а не дождаться следующего полного прохода.
    /// Вызов не рекурсивен - <see cref="RevalidateItem"/> внутри не обходит группы.
    /// </summary>
    /// <param name="store">Реестр изделий.</param>
    /// <param name="designation">Обозначение (например прежний реквизит из проблемы).</param>
    /// <param name="filePath">Путь связанного файла.</param>
    /// <param name="visited">Множество уже перепроверенных Id (пополняется).</param>
    /// <param name="writePending">true - писать в pending незавершённого прохода.</param>
    internal static void RevalidateKeyGroup(
        VelumProductRegistryStore store,
        string designation,
        string filePath,
        HashSet<int> visited,
        bool writePending)
    {
      if (store == null || visited == null)
        return;

      IReadOnlyList<int> ids = store.GetItemIdsByDesignationKey(
          designation, filePath, excludeItemId: 0);
      for (int i = 0; i < ids.Count; i++)
      {
        int id = ids[i];
        if (id <= 0 || !visited.Add(id))
          continue;

        VelumProductItem other = store.GetItem(id);
        if (other != null)
          RevalidateItem(store, other, writePending);
      }
    }

    /// <summary>
    /// Проверяет запись на конфликт ключа с другой записью реестра.
    /// </summary>
    /// <param name="store">Реестр изделий.</param>
    /// <param name="item">Проверяемая запись.</param>
    /// <param name="problem">Найденная проблема (иначе null).</param>
    /// <returns>true - есть другая запись с тем же ключом.</returns>
    internal static bool TryEvaluate(
        VelumProductRegistryStore store,
        VelumProductItem item,
        out VelumProductRegistryProblemEntry problem)
    {
      problem = null;
      if (store == null || item == null || item.Id <= 0)
        return false;

      string key = VelumProductRegistryStore.BuildDesignationKey(item);
      if (string.IsNullOrEmpty(key))
        return false;

      VelumProductItem conflict = store.FindItemByDesignationKey(
          item.Designation, item.FilePath, excludeItemId: item.Id);
      if (conflict == null)
        return false;

      problem = BuildProblem(item, conflict);
      return true;
    }

    /// <summary>Сброс незавершённого прохода (открытие документа / смена области).</summary>
    internal static void DiscardPending()
    {
      VelumProductRegistryProblemCache.DiscardPendingPass(
          VelumProductRegistryProblemKind.DuplicateDesignation);
    }

    /// <summary>Другая запись той же группы (не сама запись), иначе null.</summary>
    private static VelumProductItem FindOther(
        VelumProductItem item,
        List<VelumProductItem> group)
    {
      foreach (VelumProductItem other in group)
      {
        if (other != null && other.Id != item.Id)
          return other;
      }

      return null;
    }

    /// <summary>Проблема дубля для <paramref name="item"/> с указанием конфликтующего пути.</summary>
    private static VelumProductRegistryProblemEntry BuildProblem(
        VelumProductItem item,
        VelumProductItem conflict)
    {
      string conflictPath = (conflict.FilePath ?? string.Empty).Trim();
      if (conflictPath.Length == 0)
        conflictPath = "(без файла)";

      string detail = "Ключ «" + (item.Designation ?? string.Empty).Trim()
          + "» + расширение уже занимает запись Id=" + conflict.Id + ":\\n" + conflictPath
          + "\\n\\nОбозначение с таким расширением должно быть одно на документ.";

      return new VelumProductRegistryProblemEntry
      {
        ItemId = item.Id,
        Kind = VelumProductRegistryProblemKind.DuplicateDesignation,
        Designation = item.Designation,
        Name = item.Name,
        FilePath = item.FilePath,
        Detail = detail
      };
    }
  }
}
