using System;
using System.Collections.Generic;
using System.IO;
using ISIDA.Common;
using Velum.Configuration;
using Velum.ReactiveCore.Export;

namespace Velum.UI.ProductRegistry
{
  /// <summary>
  /// Флот-скан PDF по зеркалу реестра + FS (без OpenDoc).
  /// Только при полном проходе (нет активного документа).
  /// Весь реестр сканируется циклически без early-stop по уже найденным проблемам.
  /// </summary>
  internal static class VelumProductRegistryPdfFleetScanner
  {
    /// <summary>Квант полного прохода реестра (настраивается в Settings.xml: ScannerBatchSize).</summary>
    private static int FullRegistryBatchSize => VelumAppConfig.ScannerBatchSize;

    internal static readonly VelumProductRegistryProblemKind[] PdfKinds =
    {
      VelumProductRegistryProblemKind.NeedPdfExport,
      VelumProductRegistryProblemKind.OutdatedPdf,
      VelumProductRegistryProblemKind.JunkPdf
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
    /// <param name="known">
    /// Результаты проверок путей от BrokenLink (статусы по ключу),
    /// чтобы модель, уже проверенная сканером битых ссылок, не перепроверялась на сетевой шаре.
    /// </param>
    internal static bool Tick(
        VelumProductRegistryStore store,
        ref int searchCursor,
        ref bool passActive,
        Func<bool> shouldStop = null,
        object known = null)
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
      // Бюджет кванта всегда ограничен: пакет проверок не должен превышать разумный
      // размер, иначе хвост большого пакета гарантированно не успевает в общий
      // таймаут ожидания (MaxBatchWaitMs) и получает Unknown на живой шаре.
      // shouldStop прерывает тик между пакетами, а не раздувает пакет.
      int budget = Math.Min(FullRegistryBatchSize, remaining);

      // Квант отбирается по чертежам; пути (чертёж, PDF-файл) проверяются пакетом
      // в thread-static scope — TryClassify читает ответы из scope.
      List<VelumRegistryScanBatch.Entry> quant =
          VelumRegistryScanBatch.TakeQuant(store, searchCursor, budget,
              (VelumProductItem it) =>
                  VelumProductRegistryIntegrityRules.IsDrawingPath(it.FilePath));
      if (quant.Count == 0)
      {
        // Пустой квант — от курсора до конца списка не осталось записей по фильтру:
        // полный проход завершён, pending коммитится (иначе passCompleted никогда
        // не станет true и счётчик итераций в шедулере не сбросится).
        if (passActive)
        {
          CommitAllPending();
          passCompleted = true;
        }

        searchCursor = 0;
        passActive = false;
        return passCompleted;
      }

      searchCursor = VelumRegistryScanBatch.NextCursor(
          quant, quant[quant.Count - 1].Index, searchCursor);

      using (IDisposable scope = VelumPathExists.EnterScope())
      {
        var requests = new List<VelumPathExists.PathCheckRequest>();
        var seen = new HashSet<string>();
        for (int i = 0; i < quant.Count; i++)
        {
          VelumRegistryScanBatch.Entry e = quant[i];
          VelumRegistryScanBatch.AddRequest(requests, seen, false, e.Path);
          VelumRegistryScanBatch.AddRequest(requests, seen, false,
              Velum.ReactiveCore.Export.VelumRelativeDocumentPathResolver.ToFull(
                  (e.Item.PdfPath ?? string.Empty).Trim()));
        }

        VelumPathExists.Result[] results =
            VelumPathExists.CheckBatch(requests,
                VelumProductRegistryPathChecker.TimeoutMilliseconds,
                VelumRegistryScanBatch.AsResults(known));
        for (int i = 0; i < requests.Count; i++)
          VelumPathExists.ScopeResult(
              requests[i].IsDirectory, requests[i].Path, results[i]);

        for (int i = 0; i < quant.Count; i++)
          EvaluateItem(store, quant[i].Item);
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
      for (int i = 0; i < PdfKinds.Length; i++)
        VelumProductRegistryProblemCache.RemoveAllOfKind(PdfKinds[i]);
    }

    internal static void DiscardPending()
    {
      for (int i = 0; i < PdfKinds.Length; i++)
        VelumProductRegistryProblemCache.DiscardPendingPass(PdfKinds[i]);
    }

internal static void RevalidateItem(VelumProductRegistryStore store, VelumProductItem item, bool writePending)
    {
      if (item == null || item.Id <= 0)
        return;

      RemoveItemKinds(item.Id);
      VelumProductRegistryProblemEntry problem;
      if (!TryClassify(store, item, out problem))
        return;

      if (writePending)
        VelumProductRegistryProblemCache.UpsertPending(problem);
      else
        VelumProductRegistryProblemCache.Upsert(problem);
    }

    private static void BeginAllPending()
    {
      for (int i = 0; i < PdfKinds.Length; i++)
        VelumProductRegistryProblemCache.BeginPendingPass(PdfKinds[i]);
    }

    private static void CommitAllPending()
    {
      for (int i = 0; i < PdfKinds.Length; i++)
        VelumProductRegistryProblemCache.CommitPendingPass(PdfKinds[i]);
    }

private static void EvaluateItem(VelumProductRegistryStore store, VelumProductItem item)
    {
      if (item == null || item.Id <= 0)
        return;
      if (!VelumProductRegistryIntegrityRules.IsDrawingPath(item.FilePath))
        return;

      VelumProductRegistryProblemEntry problem;
      if (TryClassify(store, item, out problem))
        VelumProductRegistryProblemCache.UpsertPending(problem);
    }

    private static void RemoveItemKinds(int itemId)
    {
      for (int i = 0; i < PdfKinds.Length; i++)
        VelumProductRegistryProblemCache.Remove(itemId, PdfKinds[i]);
    }

internal static bool TryClassify(
        VelumProductRegistryStore store,
        VelumProductItem item,
        out VelumProductRegistryProblemEntry problem)
    {
      problem = null;
      if (item == null)
        return false;

      // Нет файла чертежа — флот PDF бессмысленен (выше приоритетом будет BrokenLink).
      string drawingPath = item.GetNormalizedPathKey();
      if (string.IsNullOrEmpty(drawingPath)
          || VelumProductRegistryIntegrityRules.PathExistsOrTimedOutIsMissing(drawingPath))
        return false;

      // Нет признаков sync — не классифицируем. Частичное зеркало (PdfPath/штампы) — сканируем.
      if (!HasPdfMirrorSyncSignal(item))
        return false;

      // NeedPdf не записан, но зеркало есть — как batch: нужен PDF.
      bool needPdf = item.NeedPdf ?? true;
      // Зеркало может хранить относительный путь — достраиваем префикс корневого каталога.
      string pdfPath = Velum.ReactiveCore.Export.VelumRelativeDocumentPathResolver.ToFull(
          (item.PdfPath ?? string.Empty).Trim());
      // Проверка трёхзначная: Unknown (таймаут сетевой шары) не считается
      // «файл не найден» — по таймауту нельзя судить о существовании файла,
      // иначе недоступный по VPN корень помечал бы весь реестр «Нужен экспорт PDF».
      VelumPathExists.Result pdfState = pdfPath.Length > 0
          ? VelumPathExists.Check(false, pdfPath)
          : VelumPathExists.Result.No;
      if (pdfState == VelumPathExists.Result.Unknown)
        return false;

      bool fileFound = pdfState == VelumPathExists.Result.Yes;
      bool outdated = IsPdfOutdated(item)
          || IsPdfOutdatedByModelGeometry(store, item);

      if (!needPdf)
      {
        if (!fileFound)
          return false;

        problem = Build(item, VelumProductRegistryProblemKind.JunkPdf, "Мусорный PDF");
        return true;
      }

      if (!fileFound)
      {
        problem = Build(item, VelumProductRegistryProblemKind.NeedPdfExport, "Нужен экспорт PDF");
        return true;
      }

      if (outdated)
      {
        problem = Build(item, VelumProductRegistryProblemKind.OutdatedPdf, "PDF устарел");
        return true;
      }

      return false;
    }

/// <summary>
    /// Признак, что PDF-зеркало уже писали в реестр (полная или частичная миграция).
    /// PdfModelStampAtExport также считается сигналом sync.
    /// </summary>
    private static bool HasPdfMirrorSyncSignal(VelumProductItem item)
    {
      if (item == null)
        return false;
      if (item.NeedPdf.HasValue)
        return true;
      if (item.PdfPath != null)
        return true;
      if (item.PdfGeometryUpdateStamp.HasValue || item.PdfGeometryPendingStamp.HasValue)
        return true;
      return item.PdfModelStampAtExport.HasValue;
    }

    private static bool IsPdfOutdated(VelumProductItem item)
    {
      if (!item.PdfGeometryUpdateStamp.HasValue || item.PdfGeometryUpdateStamp.Value <= 0)
        return true;
      if (item.PdfGeometryPendingStamp.HasValue
          && item.PdfGeometryPendingStamp.Value > item.PdfGeometryUpdateStamp.Value)
        return true;
      return false;
    }

    /// <summary>
    /// PDF устарел по геометрии модели. Резолвинг связанной детали — здесь; чистый вердикт
    /// (genuine-правка vs regen-всплеск) отдаёт <see cref="Velum.ReactiveCore.VelumPdfOutdatedRules"/>
    /// (E9: единый предикат вместо расходящихся копий).
    /// </summary>
    private static bool IsPdfOutdatedByModelGeometry(VelumProductRegistryStore store, VelumProductItem drawingItem)
    {
      if (store == null || drawingItem == null)
        return false;

      // Пытаемся найти связанную деталь.
      // Вариант 1: DrawingPath чертежа (обратная связь, может быть пустой).
      string partPath = (drawingItem.DrawingPath ?? string.Empty).Trim();

      // Вариант 2: ищем по базовому имени файла чертежа среди моделей — через
      // индекс базовых имён стора (линейный обход реестра на каждый чертёж
      // давал O(N²) на полном проходе).
      if (string.IsNullOrEmpty(partPath))
      {
        string drawingBaseName;
        try
        {
          drawingBaseName = System.IO.Path.GetFileNameWithoutExtension(
              drawingItem.FilePath ?? string.Empty);
        }
        catch
        {
          drawingBaseName = string.Empty;
        }

        if (!string.IsNullOrEmpty(drawingBaseName))
        {
          IReadOnlyList<VelumProductItem> models =
              store.FindModelItemsByBaseName(drawingBaseName);
          if (models.Count > 0)
            partPath = models[0].FilePath;
        }
      }

      if (string.IsNullOrEmpty(partPath))
        return false;

      string normalizedPartPath = VelumProductRegistryStore.NormalizeFilePathKey(partPath);
      if (string.IsNullOrEmpty(normalizedPartPath))
        return false;

      VelumProductItem partItem = store.FindItemByFilePath(normalizedPartPath);
      if (partItem == null)
        return false;

      string partExt = System.IO.Path.GetExtension(partItem.FilePath ?? string.Empty).ToLowerInvariant();
      bool partIsSldprt = string.Equals(partExt, ".sldprt", StringComparison.OrdinalIgnoreCase);

      return Velum.ReactiveCore.VelumPdfOutdatedRules.IsPdfOutdatedByModelGeometry(
          partItem.ModelGeometryStamp,
          Velum.ReactiveCore.VelumExportDocumentationGeometryStampHelper.ToConfigStamps(
              partItem.ExportMetaConfigs),
          partIsSldprt,
          drawingItem.PdfModelStampAtExport,
          drawingItem.PdfGeometryUpdateStamp);
    }

    private static VelumProductRegistryProblemEntry Build(
        VelumProductItem item,
        VelumProductRegistryProblemKind kind,
        string detail)
    {
      return new VelumProductRegistryProblemEntry
      {
        ItemId = item.Id,
        Kind = kind,
        Designation = item.Designation,
        Name = item.Name,
        FilePath = item.FilePath,
        Detail = detail
      };
    }
  }
}
