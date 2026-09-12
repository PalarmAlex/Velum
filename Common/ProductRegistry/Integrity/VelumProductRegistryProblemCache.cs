using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Velum.UI.ProductRegistry
{
  /// <summary>
  /// Потокобезопасный кэш проблем реестра. Метрики читают его за O(1);
  /// фоновые сканеры пишут/чистят записи после проверки среды.
  /// <para>
  /// Кэш заполняется только при отсутствии открытых документов SW
  /// (<see cref="VelumProductRegistryIntegrityScheduler"/>). При открытии документа
  /// кэш полностью очищается — не пытайтесь обновлять его точечно при активном документе.
  /// </para>
  /// <para>
  /// Поиск (discovery) пишет в pending; в опубликованный кэш попадает только
  /// атомарный <see cref="CommitPendingPass"/> по завершении прохода.
  /// Метрики (<see cref="HasKind"/>) и форма (<see cref="SnapshotIncludingPending"/>)
  /// учитывают pending, чтобы боль была видна до конца прохода.
  /// </para>
  /// </summary>
  internal static class VelumProductRegistryProblemCache
  {
    private static readonly object Gate = new object();
    private static readonly Dictionary<string, VelumProductRegistryProblemEntry> Problems =
        new Dictionary<string, VelumProductRegistryProblemEntry>(StringComparer.Ordinal);
    private static readonly Dictionary<string, VelumProductRegistryProblemEntry> Pending =
        new Dictionary<string, VelumProductRegistryProblemEntry>(StringComparer.Ordinal);

    internal static string MakeKey(int itemId, VelumProductRegistryProblemKind kind)
    {
      return MakeKey(itemId, kind, null);
    }

    internal static string MakeKey(int itemId, VelumProductRegistryProblemKind kind, string filePath)
    {
      if (kind == VelumProductRegistryProblemKind.MissingRegistryEntry)
      {
        string path = VelumProductRegistryStore.NormalizeFilePathKey(filePath);
        return ((int)kind).ToString(System.Globalization.CultureInfo.InvariantCulture) + ":path:" + path;
      }

      return ((int)kind).ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" +
             itemId.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    internal static bool HasAny
    {
      get
      {
        lock (Gate)
          return Problems.Count > 0 || Pending.Count > 0;
      }
    }

    internal static bool HasKind(VelumProductRegistryProblemKind kind)
    {
      lock (Gate)
      {
        foreach (VelumProductRegistryProblemEntry e in Problems.Values)
        {
          if (e != null && e.Kind == kind)
            return true;
        }

        foreach (VelumProductRegistryProblemEntry e in Pending.Values)
        {
          if (e != null && e.Kind == kind)
            return true;
        }

        return false;
      }
    }

    /// <summary>
    /// true, если в опубликованном кэше или pending есть хотя бы один из указанных видов.
    /// Учитывает оба словаря — как <see cref="HasKind"/>, но без аллокаций на каждый вид.
    /// </summary>
    internal static bool HasAnyKind(IReadOnlyList<VelumProductRegistryProblemKind> kinds)
    {
      if (kinds == null || kinds.Count == 0)
        return false;

      lock (Gate)
      {
        foreach (VelumProductRegistryProblemEntry e in Problems.Values)
        {
          if (e != null && ContainsKind(kinds, e.Kind))
            return true;
        }

        foreach (VelumProductRegistryProblemEntry e in Pending.Values)
        {
          if (e != null && ContainsKind(kinds, e.Kind))
            return true;
        }

        return false;
      }
    }

    private static bool ContainsKind(
        IReadOnlyList<VelumProductRegistryProblemKind> kinds,
        VelumProductRegistryProblemKind kind)
    {
      for (int i = 0; i < kinds.Count; i++)
      {
        if (kinds[i] == kind)
          return true;
      }

      return false;
    }

    internal static int Count
    {
      get
      {
        lock (Gate)
          return Problems.Count;
      }
    }

    internal static int PendingCount
    {
      get
      {
        lock (Gate)
          return Pending.Count;
      }
    }

    /// <summary>Опубликованный снимок (без pending discovery).</summary>
    internal static IReadOnlyList<VelumProductRegistryProblemEntry> Snapshot()
    {
      lock (Gate)
      {
        return SelectPrimaryProblems(
            Problems.Values.Where(e => e != null).Select(Clone));
      }
    }

    /// <summary>
    /// Опубликованный кэш ∪ pending discovery (для формы проблем и tip метрик).
    /// При совпадении ключа берётся pending.
    /// Для одной записи реестра (ItemId &gt; 0) оставляется только наиболее важный вид.
    /// </summary>
    internal static IReadOnlyList<VelumProductRegistryProblemEntry> SnapshotIncludingPending()
    {
      lock (Gate)
      {
        var map = new Dictionary<string, VelumProductRegistryProblemEntry>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, VelumProductRegistryProblemEntry> kv in Problems)
        {
          if (kv.Value != null)
            map[kv.Key] = kv.Value;
        }

        foreach (KeyValuePair<string, VelumProductRegistryProblemEntry> kv in Pending)
        {
          if (kv.Value != null)
            map[kv.Key] = kv.Value;
        }

        return SelectPrimaryProblems(map.Values.Select(Clone));
      }
    }

    internal static IReadOnlyList<VelumProductRegistryProblemEntry> SnapshotKind(
        VelumProductRegistryProblemKind kind)
    {
      lock (Gate)
      {
        return Problems.Values
            .Where(e => e != null && e.Kind == kind)
            .Select(Clone)
            .OrderBy(e => e.ItemId)
            .ThenBy(e => e.FilePath ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
      }
    }

    /// <summary>Число проблем kind в опубликованном кэше ∪ pending (для tip метрики).</summary>
    internal static int CountKindIncludingPending(VelumProductRegistryProblemKind kind)
    {
      lock (Gate)
      {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, VelumProductRegistryProblemEntry> kv in Problems)
        {
          if (kv.Value != null && kv.Value.Kind == kind)
            keys.Add(kv.Key);
        }

        foreach (KeyValuePair<string, VelumProductRegistryProblemEntry> kv in Pending)
        {
          if (kv.Value != null && kv.Value.Kind == kind)
            keys.Add(kv.Key);
        }

        return keys.Count;
      }
    }

    /// <summary>
    /// Проблема конкретного вида для одной записи (pending имеет приоритет над
    /// опубликованным кэшем). Нужна точечной перепроверке, чтобы прочитать
    /// реквизиты записи на момент находки (например прежний ключ дубля).
    /// Для <see cref="VelumProductRegistryProblemKind.MissingRegistryEntry"/> не работает
    /// (такие проблемы ключуются путём, а не Id).
    /// </summary>
    /// <param name="itemId">Id записи реестра.</param>
    /// <param name="kind">Вид проблемы.</param>
    /// <param name="entry">Найденная проблема (копия), иначе null.</param>
    /// <returns>true - проблема есть в опубликованном кэше или pending.</returns>
    internal static bool TryGet(
        int itemId,
        VelumProductRegistryProblemKind kind,
        out VelumProductRegistryProblemEntry entry)
    {
      entry = null;
      if (itemId <= 0 || kind == VelumProductRegistryProblemKind.MissingRegistryEntry)
        return false;

      lock (Gate)
      {
        string key = MakeKey(itemId, kind);
        VelumProductRegistryProblemEntry found;
        if (Pending.TryGetValue(key, out found) && found != null)
        {
          entry = Clone(found);
          return true;
        }

        if (Problems.TryGetValue(key, out found) && found != null)
        {
          entry = Clone(found);
          return true;
        }

        return false;
      }
    }

    internal static void Upsert(VelumProductRegistryProblemEntry entry)
    {
      if (entry == null)
        return;

      if (entry.Kind == VelumProductRegistryProblemKind.MissingRegistryEntry)
      {
        string path = VelumProductRegistryStore.NormalizeFilePathKey(entry.FilePath);
        if (string.IsNullOrEmpty(path))
          return;
        entry.FilePath = path;
        entry.ItemId = 0;
        lock (Gate)
          Problems[MakeKey(0, entry.Kind, path)] = Clone(entry);
        return;
      }

      if (entry.ItemId <= 0)
        return;

      lock (Gate)
      {
        string key = MakeKey(entry.ItemId, entry.Kind);
        Problems[key] = Clone(entry);
        Pending.Remove(key);
      }

      Debug.WriteLine("[Velum.ProblemCache] Upsert itemId=" + entry.ItemId + " kind=" + entry.Kind + " count=" + Problems.Count);
    }

    /// <summary>Начать discovery-проход: очистить pending этого kind.</summary>
    internal static void BeginPendingPass(VelumProductRegistryProblemKind kind)
    {
      if (kind == VelumProductRegistryProblemKind.MissingRegistryEntry)
        return;

      lock (Gate)
        RemoveKindUnlocked(Pending, kind);
    }

    /// <summary>Находка текущего прохода (учитывается метриками и формой до commit).</summary>
    internal static void UpsertPending(VelumProductRegistryProblemEntry entry)
    {
      if (entry == null)
        return;
      if (entry.Kind == VelumProductRegistryProblemKind.MissingRegistryEntry)
        return;
      if (entry.ItemId <= 0)
        return;

      lock (Gate)
        Pending[MakeKey(entry.ItemId, entry.Kind)] = Clone(entry);
    }

    /// <summary>
    /// Завершить проход: заменить опубликованный kind целиком содержимым pending.
    /// </summary>
    internal static void CommitPendingPass(VelumProductRegistryProblemKind kind)
    {
      if (kind == VelumProductRegistryProblemKind.MissingRegistryEntry)
        return;

      lock (Gate)
      {
        RemoveKindUnlocked(Problems, kind);
        var keys = Pending
            .Where(kv => kv.Value != null && kv.Value.Kind == kind)
            .Select(kv => kv.Key)
            .ToList();
        foreach (string key in keys)
        {
          Problems[key] = Pending[key];
          Pending.Remove(key);
        }
      }
    }

    /// <summary>Сброс незавершённого прохода (reload / смена области).</summary>
    internal static void DiscardPendingPass(VelumProductRegistryProblemKind kind)
    {
      if (kind == VelumProductRegistryProblemKind.MissingRegistryEntry)
        return;

      lock (Gate)
        RemoveKindUnlocked(Pending, kind);
    }

    internal static void Remove(int itemId, VelumProductRegistryProblemKind kind)
    {
      if (kind == VelumProductRegistryProblemKind.MissingRegistryEntry)
        return;

      lock (Gate)
      {
        string key = MakeKey(itemId, kind);
        Problems.Remove(key);
        Pending.Remove(key);
      }
    }

    internal static void RemoveMissingRegistryEntry(string filePath)
    {
      string path = VelumProductRegistryStore.NormalizeFilePathKey(filePath);
      if (string.IsNullOrEmpty(path))
        return;

      lock (Gate)
        Problems.Remove(MakeKey(0, VelumProductRegistryProblemKind.MissingRegistryEntry, path));
    }

    internal static void RemoveAllOfKind(VelumProductRegistryProblemKind kind)
    {
      lock (Gate)
      {
        RemoveKindUnlocked(Problems, kind);
        RemoveKindUnlocked(Pending, kind);
      }
    }

    internal static void RemoveAllForItem(int itemId)
    {
      if (itemId <= 0)
        return;

      string suffix = ":" + itemId.ToString(System.Globalization.CultureInfo.InvariantCulture);
      lock (Gate)
      {
        RemoveKeysEndingWith(Problems, suffix);
        RemoveKeysEndingWith(Pending, suffix);
      }
    }

    internal static void Clear()
    {
      lock (Gate)
      {
        int count = Problems.Count + Pending.Count;
        Problems.Clear();
        Pending.Clear();
      }
    }

    /// <summary>
    /// Удаляет BrokenLink/MissingDrawing вне разрешённых Id.
    /// MissingRegistryEntry не трогает (синхронизируется отдельно).
    /// </summary>
    internal static void RemoveItemProblemsWhereItemNotIn(HashSet<int> allowedItemIds)
    {
      if (allowedItemIds == null)
        return;

      lock (Gate)
      {
        PruneNotInUnlocked(Problems, allowedItemIds);
        PruneNotInUnlocked(Pending, allowedItemIds);
      }
    }

    private static void RemoveKindUnlocked(
        Dictionary<string, VelumProductRegistryProblemEntry> map,
        VelumProductRegistryProblemKind kind)
    {
      var keys = map
          .Where(kv => kv.Value != null && kv.Value.Kind == kind)
          .Select(kv => kv.Key)
          .ToList();
      foreach (string key in keys)
        map.Remove(key);
    }

    private static void RemoveKeysEndingWith(
        Dictionary<string, VelumProductRegistryProblemEntry> map,
        string suffix)
    {
      var keys = map.Keys
          .Where(k => k.EndsWith(suffix, StringComparison.Ordinal) &&
                      !k.Contains(":path:"))
          .ToList();
      foreach (string key in keys)
        map.Remove(key);
    }

    private static void PruneNotInUnlocked(
        Dictionary<string, VelumProductRegistryProblemEntry> map,
        HashSet<int> allowedItemIds)
    {
      var keys = map
          .Where(kv =>
          {
            VelumProductRegistryProblemEntry e = kv.Value;
            if (e == null)
              return true;
            if (e.Kind == VelumProductRegistryProblemKind.MissingRegistryEntry)
              return false;
            return allowedItemIds.Count == 0 || !allowedItemIds.Contains(e.ItemId);
          })
          .Select(kv => kv.Key)
          .ToList();
      foreach (string key in keys)
        map.Remove(key);
    }

    /// <summary>
    /// Одна запись реестра → один вид (наивысший приоритет).
    /// <see cref="VelumProductRegistryProblemKind.MissingRegistryEntry"/> (ItemId = 0) не схлопываются.
    /// </summary>
    private static IReadOnlyList<VelumProductRegistryProblemEntry> SelectPrimaryProblems(
        IEnumerable<VelumProductRegistryProblemEntry> entries)
    {
      var pathScoped = new List<VelumProductRegistryProblemEntry>();
      var bestByItem = new Dictionary<int, VelumProductRegistryProblemEntry>();

      foreach (VelumProductRegistryProblemEntry entry in entries)
      {
        if (entry == null)
          continue;

        if (entry.ItemId <= 0
            || entry.Kind == VelumProductRegistryProblemKind.MissingRegistryEntry)
        {
          pathScoped.Add(entry);
          continue;
        }

        VelumProductRegistryProblemEntry existing;
        if (!bestByItem.TryGetValue(entry.ItemId, out existing)
            || VelumProductRegistryProblemKindPriority.Compare(entry.Kind, existing.Kind) < 0)
        {
          bestByItem[entry.ItemId] = entry;
        }
      }

      var result = new List<VelumProductRegistryProblemEntry>(pathScoped.Count + bestByItem.Count);
      result.AddRange(pathScoped);
      result.AddRange(bestByItem.Values);
      result.Sort((a, b) =>
      {
        int cmp = VelumProductRegistryProblemKindPriority.Compare(a.Kind, b.Kind);
        if (cmp != 0)
          return cmp;
        cmp = a.ItemId.CompareTo(b.ItemId);
        if (cmp != 0)
          return cmp;
        return string.Compare(a.FilePath, b.FilePath, StringComparison.OrdinalIgnoreCase);
      });
      return result;
    }

    private static VelumProductRegistryProblemEntry Clone(VelumProductRegistryProblemEntry e)
    {
      return new VelumProductRegistryProblemEntry
      {
        ItemId = e.ItemId,
        Kind = e.Kind,
        Designation = e.Designation,
        Name = e.Name,
        FilePath = e.FilePath,
        Detail = e.Detail
      };
    }
  }
}
