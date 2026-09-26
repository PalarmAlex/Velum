using System;
using System.Collections.Generic;

namespace Velum.UI.ProductRegistry
{
  /// <summary>
  /// Скан битых ссылок реестра по всему каталогу (без early-stop).
  /// Discovery-проход копит в pending; по завершении — атомарный commit в кэш формы/метрик.
  /// Проходы циклически повторяются, пока флот активен (нет ActiveDoc).
  /// <para>
  /// Пути очередного кванта проверяются пакетом параллельно (см. <see cref="VelumRegistryScanBatch"/>):
  /// на сетевой шаре последовательная проверка каждой записи стоила раундтрип, и полный
  /// проход реестра растягивался на десятки минут.
  /// </para>
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
    /// либо открылся документ SW).
    /// </param>
    /// <param name="known">
    /// Статусы, уже полученные другими сканами прохода (ключ — нормализованный путь):
    /// повторная проверка того же пути на сетевой шаре стоила бы ещё одного раундтрипа.
    /// </param>
    /// <param name="produced">
    /// Результаты проверки путей кванта (ключ — нормализованный путь), передаются
    /// последующим сканерам прохода, чтобы те не перепроверяли те же модели на шаре.
    /// </param>
    internal static bool Tick(
        VelumProductRegistryStore store,
        ref int searchCursor,
        ref bool passActive,
        HashSet<int> scopedItemIds,
        int openDocumentCount,
        Func<bool> shouldStop,
        object known,
        out Dictionary<string, VelumProductRegistryPathStatus> produced)
    {
      produced = null;
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

      // Бюджет тика — число позиций списка: для полного реестра это квант, для
      // области открытых документов — весь список области за тик.
      // Временной бюджет (shouldStop) сверху не ограничивает: квант и так не больше
      // FullRegistryBatchSize, поэтому ожидание ответа пакета не переносится на
      // следующий тяжёлый пульс.
      int budget = ResolveBudget(scopedItemIds == null, openDocumentCount, items.Count, searchCursor);
      // Квант проверяется пакетом: за тик — один параллельный заход на ФС, а не
      // по одному сетевому раундтрипу на каждую запись.
      List<VelumRegistryScanBatch.Entry> quant =
          TakeQuant(items, searchCursor, budget, scopedItemIds != null);
      if (quant.Count == 0)
        return false;

      searchCursor = VelumRegistryScanBatch.NextCursor(
          quant, quant[quant.Count - 1].Index, searchCursor);
      Dictionary<string, VelumProductRegistryPathStatus> statuses =
          VelumProductRegistryPathChecker.CheckEntryPaths(
              quant,
              VelumProductRegistryPathChecker.TimeoutMilliseconds,
              known);

      // Результаты путей кванта передаются дальше (MissingDrawing/DXF/PDF), чтобы
      // модели, уже проверенные этим сканером, не проверялись на сетевой шаре повторно.
      produced = statuses;

      for (int i = 0; i < quant.Count; i++)
      {
        if (!TryEvaluateBroken(quant[i].Item, StatusOf(statuses, quant[i]),
                out VelumProductRegistryProblemEntry problem))
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

    /// <summary>
    /// Одиночная проверка записи (реакция на её изменение в реестре): путь проверяется
    /// тем же пакетным механизмом, что и обход кванта.
    /// </summary>
    /// <param name="item">Запись реестра.</param>
    /// <param name="problem">Найденная проблема (или <c>null</c>).</param>
    internal static bool TryEvaluateBroken(
        VelumProductItem item,
        out VelumProductRegistryProblemEntry problem)
    {
      return TryEvaluateBroken(item, CheckItemFile(item), out problem);
    }

    /// <summary>
    /// Тот же критерий, но для уже полученного статуса: сканер проверяет квант пакетом
    /// и не должен ходить на сеть за каждой записью отдельно.
    /// </summary>
    /// <param name="item">Запись реестра.</param>
    /// <param name="status">Статус проверки её файла (или <c>null</c>, если ответа нет).</param>
    /// <param name="problem">Найденная проблема (или <c>null</c>).</param>
    internal static bool TryEvaluateBroken(
        VelumProductItem item,
        VelumProductRegistryPathStatus? status,
        out VelumProductRegistryProblemEntry problem)
    {
      problem = null;
      if (item == null || item.Id <= 0)
        return false;

      string path = item.GetNormalizedPathKey();
      if (string.IsNullOrEmpty(path))
      {
        problem = Build(item, "Путь к файлу пуст.");
        return true;
      }

      // Нет ответа по пути (запись не попала в пакет) — о существовании файла
      // не судим: ложный BrokenLink опаснее пропущенного за тик.
      if (!status.HasValue)
        return false;

      if (VelumProductRegistryIntegrityRules.PathExistsOrTimedOutIsMissing(status.Value))
      {
        problem = Build(item, "Файл по ссылке не найден или недоступен (в т.ч. timeout UNC).");
        return true;
      }

      return false;
    }

    /// <summary>
    /// Берёт квант записей, обработав не больше бюджета позиций списка.
    /// <para>
    /// Область открытых документов маленькая (обычно десятки записей), поэтому её
    /// проверяем целиком за тик: иначе проблема в последней записи области ждала бы
    /// несколько тиков. Полный реестр идёт квантом не больше <see cref="FullRegistryBatchSize"/>
    /// записей — один пакет проверок на тик.
    /// </para>
    /// </summary>
    /// <param name="items">Записи области или всего реестра.</param>
    /// <param name="searchCursor">Позиция начала кванта в списке.</param>
    /// <param name="budget">Бюджет — число позиций списка за тик.</param>
    /// <param name="wholeScope">true — область открытых документов (берём целиком).</param>
    private static List<VelumRegistryScanBatch.Entry> TakeQuant(
        IReadOnlyList<VelumProductItem> items,
        int searchCursor,
        int budget,
        bool wholeScope)
    {
      int remaining = Math.Max(0, items.Count - searchCursor);
      int quantSize = wholeScope
          ? remaining
          : Math.Min(Math.Max(0, budget), Math.Min(FullRegistryBatchSize, remaining));

      var quant = new List<VelumRegistryScanBatch.Entry>();
      int consumed = 0;
      for (int i = Math.Max(0, searchCursor);
          i < items.Count && consumed < quantSize;
          i++, consumed++)
      {
        VelumProductItem item = items[i];
        if (item == null || item.Id <= 0)
          continue;

        string path = VelumProductRegistryStore.NormalizeFilePathKey(item.FilePath);
        var entry = new VelumRegistryScanBatch.Entry();
        entry.Index = i;
        entry.Item = item;
        entry.Path = path ?? string.Empty;
        quant.Add(entry);
      }

      return quant;
    }

    /// <summary>Статус проверки файла записи кванта (null — ответа по пути нет).</summary>
    private static VelumProductRegistryPathStatus? StatusOf(
        Dictionary<string, VelumProductRegistryPathStatus> statuses,
        VelumRegistryScanBatch.Entry entry)
    {
      if (entry.Path.Length == 0 || statuses == null)
        return null;

      VelumProductRegistryPathStatus status;
      return statuses.TryGetValue(entry.Path, out status)
          ? status
          : (VelumProductRegistryPathStatus?)null;
    }

    private static IReadOnlyList<VelumProductItem> ResolveItems(
        VelumProductRegistryStore store,
        HashSet<int> scopedItemIds)
    {
      if (scopedItemIds == null)
        return store.GetAllItems();
      return VelumProductRegistryOpenDocumentsScope.FilterItems(store, scopedItemIds);
    }

    /// <summary>
    /// Проверяет путь одиночной записи тем же пакетным механизмом, что и обход кванта:
    /// реакция на изменение записи не должна отличаться от сканера ни таймаутом,
    /// ни способом обращения к кэшу.
    /// </summary>
    /// <param name="item">Запись реестра.</param>
    private static VelumProductRegistryPathStatus CheckItemFile(VelumProductItem item)
    {
      var items = new List<VelumProductItem> { item };
      Dictionary<string, VelumProductRegistryPathStatus> statuses =
          VelumProductRegistryPathChecker.CheckFiles(
              items,
              VelumProductRegistryPathChecker.TimeoutMilliseconds);

      VelumProductRegistryPathStatus status;
      if (statuses.TryGetValue(item.GetNormalizedPathKey(), out status))
        return status;

      // Путь пуст — проверять нечего, это то же «нет файла».
      return VelumProductRegistryPathStatus.No;
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
