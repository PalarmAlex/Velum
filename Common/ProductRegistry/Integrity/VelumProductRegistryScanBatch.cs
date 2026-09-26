using System;
using System.Collections.Generic;
using Velum.ReactiveCore.Export;

namespace Velum.UI.ProductRegistry
{
  /// <summary>
  /// Общий помощник пакетной проверки путей для сканеров целостности реестра.
  /// <para>
  /// Сканеры ранее проверяли путь каждой записи последовательно в один поток; на сетевом
  /// корне это давало сетевой раундтрип на запись, и полный проход растягивался на десятки
  /// минут. Помощник берёт очередной квант записей и проверяет его пути параллельно через
  /// <see cref="VelumPathExists.CheckBatch"/>, а результаты по путям передаёт дальше,
  /// чтобы связанные сканеры не проверяли тот же файл повторно.
  /// </para>
  /// </summary>
  internal static class VelumRegistryScanBatch
  {
    /// <summary>Один элемент кванта прохода сканера.</summary>
    internal struct Entry
    {
      /// <summary>Индекс записи в списке реестра.</summary>
      internal int Index;

      /// <summary>Сама запись реестра.</summary>
      internal VelumProductItem Item;

      /// <summary>Нормализованный путь файла записи (пусто — путь не задан).</summary>
      internal string Path;
    }

    /// <summary>Результат параллельной проверки файлов кванта.</summary>
    internal sealed class FileResults
    {
      /// <summary>Индекс кванта → результат проверки файла записи.</summary>
      internal readonly Dictionary<int, VelumPathExists.Result> ByEntry =
          new Dictionary<int, VelumPathExists.Result>();

      /// <summary>
      /// Нормализованный путь → результат: позволяет связанным сканерам переиспользовать
      /// уже полученный ответ и не ходить на сеть за тем же файлом второй раз.
      /// </summary>
      internal readonly Dictionary<string, VelumPathExists.Result> ByPath =
          new Dictionary<string, VelumPathExists.Result>();
    }

    /// <summary>
    /// Берёт следующий квант записей, начиная с курсора (за пределами бюджета — короче).
    /// <paramref name="filter"/> может отбирать записи по типу документа; <c>null</c> —
    /// без отбора. Курсор записи в реестре не меняется: он остаётся позицией в списке,
    /// поэтому отфильтрованные записи не «съедают» квант целиком.
    /// </summary>
    /// <param name="store">Хранилище записей реестра.</param>
    /// <param name="searchCursor">Позиция начала кванта в списке записей.</param>
    /// <param name="budget">Максимум записей в кванте.</param>
    /// <param name="filter">Отбор записей (может быть <c>null</c>).</param>
    internal static List<Entry> TakeQuant(
        VelumProductRegistryStore store,
        int searchCursor,
        int budget,
        Func<VelumProductItem, bool> filter)
    {
      var quant = new List<Entry>();
      if (store == null || budget <= 0)
        return quant;

      VelumProductItem[] items = store.GetAllItems();
      if (items == null)
        return quant;

      return TakeQuant(items, searchCursor, budget, filter);
    }

    /// <summary>
    /// Берёт следующий квант из произвольного списка записей (например, из области
    /// открытых документов, где позиция в списке не совпадает с индексом в реестре).
    /// </summary>
    /// <param name="items">Список записей.</param>
    /// <param name="searchCursor">Позиция начала кванта в списке.</param>
    /// <param name="budget">Максимум записей в кванте.</param>
    /// <param name="filter">Отбор записей (может быть <c>null</c>).</param>
    internal static List<Entry> TakeQuant(
        IList<VelumProductItem> items,
        int searchCursor,
        int budget,
        Func<VelumProductItem, bool> filter)
    {
      var quant = new List<Entry>();
      if (items == null || budget <= 0)
        return quant;

      for (int i = Math.Max(0, searchCursor); i < items.Count && quant.Count < budget; i++)
      {
        VelumProductItem item = items[i];
        if (item == null || item.Id <= 0)
          continue;

        if (filter != null && !filter(item))
          continue;

        string path = VelumProductRegistryStore.NormalizeFilePathKey(item.FilePath);
        var entry = new Entry();
        entry.Index = i;
        entry.Item = item;
        entry.Path = path ?? string.Empty;
        quant.Add(entry);
      }

      return quant;
    }

    /// <summary>
    /// Индекс позиции сразу за последним разобранным элементом кванта (новый курсор).
    /// Учитывает и пропущенные позиции (не прошедшие отбор), иначе курсор стоял бы
    /// на месте и бюджет тика уходил бы на повторный разбор начала списка.
    /// </summary>
    /// <param name="quant">Разобранные позиции (пусто — конец списка).</param>
    /// <param name="lastScannedIndex">Индекс последней разобранной позиции.</param>
    /// <param name="searchCursor">Курсор до разбора (возвращается без изменений, если
    /// позиций разобрать не удалось).</param>
    internal static int NextCursor(
        List<Entry> quant,
        int lastScannedIndex,
        int searchCursor)
    {
      if (lastScannedIndex < searchCursor)
        return searchCursor;
      return lastScannedIndex + 1;
    }

    /// <summary>
    /// Строит синтетическую запись с указанным нормализованным путём.
    /// <para>
    /// Нужна для пакетной проверки путей, которые не являются путями самих записей
    /// (чертёж модели, каталог DXF/PDF): Id синтетической записи равен нулю, поэтому
    /// такая запись ни в какой кэш проблем не попадёт — используется только путь.
    /// </para>
    /// </summary>
    /// <param name="normalizedPath">Нормализованный путь для проверки.</param>
    internal static Entry SyntheticEntry(string normalizedPath)
    {
      var entry = new Entry();
      entry.Index = 0;
      entry.Item = new VelumProductItem();
      entry.Item.Id = 0;
      entry.Item.FilePath = normalizedPath ?? string.Empty;
      entry.Path = normalizedPath ?? string.Empty;
      return entry;
    }

    /// <summary>
    /// Параллельно проверяет существование файлов записей кванта.
    /// <para>
    /// На сетевом корне одиночная проверка стоит сетевой раундтрип, и последовательный
    /// обход кванта из сотен записей превращает его в десятки секунд; пакет выполняет
    /// проверки одновременно, поэтому квант стоит времени самой медленной из них.
    /// </para>
    /// </summary>
    /// <param name="quant">Элементы кванта.</param>
    /// <param name="timeoutMs">Таймаут одной проверки, мс.</param>
    /// <param name="known">Ранее полученные результаты по путям (может быть <c>null</c>).</param>
    internal static FileResults ProbeFiles(
        List<Entry> quant,
        int timeoutMs,
        IDictionary<string, VelumPathExists.Result> known = null)
    {
      var res = new FileResults();
      if (quant == null || quant.Count == 0)
        return res;

      var requests = new List<VelumPathExists.PathCheckRequest>(quant.Count);
      var requestToEntry = new List<int>(quant.Count);
      for (int i = 0; i < quant.Count; i++)
      {
        if (quant[i].Path.Length == 0)
          continue;

        var req = new VelumPathExists.PathCheckRequest(false, quant[i].Path);
        requests.Add(req);
        requestToEntry.Add(i);
      }

      VelumPathExists.Result[] results =
          VelumPathExists.CheckBatch(requests, timeoutMs, known);

      for (int i = 0; i < requestToEntry.Count; i++)
      {
        int entryIndex = requestToEntry[i];
        VelumPathExists.Result r =
            i < results.Length ? results[i] : VelumPathExists.Result.Unknown;
        res.ByEntry[entryIndex] = r;
        res.ByPath[requests[i].Path] = r;
      }

      return res;
    }

    /// <summary>
    /// Приводит известные результаты проверки путей к словарю трёхзначных результатов,
    /// чтобы связанные проверки переиспользовали уже полученный ответ по тому же пути.
    /// </summary>
    /// <param name="known">Известные статусы или результаты (может быть <c>null</c>).</param>
    internal static IDictionary<string, VelumPathExists.Result> AsResults(object known)
    {
      if (known == null)
        return null;

      var results = known as IDictionary<string, VelumPathExists.Result>;
      if (results != null)
        return results;

      var statuses = known as IDictionary<string, VelumProductRegistryPathStatus>;
      if (statuses == null)
        return null;

      var mapped = new Dictionary<string, VelumPathExists.Result>(statuses.Count);
      foreach (KeyValuePair<string, VelumProductRegistryPathStatus> kv in statuses)
      {
        switch (kv.Value)
        {
          case VelumProductRegistryPathStatus.Ok:
            mapped[kv.Key] = VelumPathExists.Result.Yes;
            break;
          case VelumProductRegistryPathStatus.No:
            mapped[kv.Key] = VelumPathExists.Result.No;
            break;
          default:
            mapped[kv.Key] = VelumPathExists.Result.Unknown;
            break;
        }
      }

      return mapped;
    }

    /// <summary>
    /// Добавляет путь в пакетную проверку, пропуская пустые и уже добавленные.
    /// <para>
    /// Дубли внутри кванта (модель, чертёж, каталог) проверять повторно незачем:
    /// они съели бы слоты семафора <see cref="VelumPathExists"/> и время ожидания
    /// пакета, а результат был бы тот же.
    /// </para>
    /// </summary>
    /// <param name="requests">Собираемые запросы пакета.</param>
    /// <param name="seen">Ключи уже добавленных путей (для дедупликации, может быть <c>null</c>).</param>
    /// <param name="isDirectory">true — проверяется каталог, false — файл.</param>
    /// <param name="path">Проверяемый путь.</param>
    internal static void AddRequest(
        List<VelumPathExists.PathCheckRequest> requests,
        HashSet<string> seen,
        bool isDirectory,
        string path)
    {
      if (requests == null || string.IsNullOrEmpty(path))
        return;

      string key = (isDirectory ? "D:" : "F:") + path;
      if (seen != null && !seen.Add(key))
        return;

      requests.Add(new VelumPathExists.PathCheckRequest(isDirectory, path));
    }
  }
}
