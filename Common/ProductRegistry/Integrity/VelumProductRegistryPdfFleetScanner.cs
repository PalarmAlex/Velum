using System;
using System.Collections.Generic;
using System.IO;
using ISIDA.Common;

namespace Velum.UI.ProductRegistry
{
  /// <summary>
  /// Флот-скан PDF по зеркалу реестра + FS (без OpenDoc).
  /// Только при полном проходе (нет активного документа).
  /// Весь реестр сканируется циклически без early-stop по уже найденным проблемам.
  /// </summary>
  internal static class VelumProductRegistryPdfFleetScanner
  {
    private const int FullRegistryBatchSize = 60;

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
        EvaluateItem(store, item);
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
      bool fileFound = pdfPath.Length > 0 && File.Exists(pdfPath);
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
    /// PDF устарел по геометрии модели: если <c>ModelGeometryStamp</c>
    /// связанной детали больше <c>PdfModelStampAtExport</c> чертежа — геометрия модели
    /// изменилась с момента экспорта PDF → PDF устарел.
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
      int modelStamp;

      // Для листовой детали: использовать максимальный DxfGeometryUpdateStamp вместо
      // ModelGeometryStamp, чтобы fold/unfold развёртки не вызывал ложных срабатываний.
      if (string.Equals(partExt, ".sldprt", StringComparison.OrdinalIgnoreCase) &&
          partItem.ExportMetaConfigs != null && partItem.ExportMetaConfigs.Length > 0)
      {
        int maxDxfStamp = 0;
        for (int i = 0; i < partItem.ExportMetaConfigs.Length; i++)
        {
          var cfg = partItem.ExportMetaConfigs[i];
          if (cfg != null && cfg.DxfGeometryUpdateStamp.HasValue &&
              cfg.DxfGeometryUpdateStamp.Value > maxDxfStamp)
          {
            maxDxfStamp = cfg.DxfGeometryUpdateStamp.Value;
          }
        }

        if (maxDxfStamp > 0)
        {
          modelStamp = maxDxfStamp;

          // Fallback: если DxfGeometryUpdateStamp отстаёт от ModelGeometryStamp > 1,
          // значит export-штамп не обновился из-за race condition.
          // Используем ModelGeometryStamp как эффективный штамп.
          if (partItem.ModelGeometryStamp.HasValue &&
              partItem.ModelGeometryStamp.Value - maxDxfStamp > 1)
          {
            modelStamp = partItem.ModelGeometryStamp.Value;
            Logger.Info(
                "Velum PDF fleet scanner: DxfGeometryUpdateStamp lags ModelGeometryStamp by " +
                (partItem.ModelGeometryStamp.Value - maxDxfStamp) + " — using ModelGeometryStamp as effective stamp" +
                " part=" + partPath + " modelStamp=" + modelStamp + " maxDxfStamp=" + maxDxfStamp);
          }
        }
        else
        {
          if (!partItem.ModelGeometryStamp.HasValue)
            return false;
          modelStamp = partItem.ModelGeometryStamp.Value;
        }
      }
      else
      {
        if (!partItem.ModelGeometryStamp.HasValue)
          return false;
        modelStamp = partItem.ModelGeometryStamp.Value;
      }

      // Вариант A: есть PdfModelStampAtExport — прямое сравнение.
      if (drawingItem.PdfModelStampAtExport.HasValue)
      {
        bool outdated = modelStamp > drawingItem.PdfModelStampAtExport.Value;
        return outdated;
      }

      // Вариант B: fallback для старых записей — сравниваем с PdfGeometryUpdateStamp чертежа.
      // ТОЛЬКО для деталей: для сборок stamp модели и stamp чертежа не связаны напрямую,
      // сравнение даёт ложные срабатывания. Для сборок должен быть записан PdfModelStampAtExport.
      if (string.Equals(partExt, ".sldprt", StringComparison.OrdinalIgnoreCase) &&
          drawingItem.PdfGeometryUpdateStamp.HasValue && drawingItem.PdfGeometryUpdateStamp.Value > 0)
      {
        return modelStamp > drawingItem.PdfGeometryUpdateStamp.Value;
      }

      return false;
    }

    /// <summary>
    /// Проверяет, что геометрия листовой детали не менялась с момента экспорта DXF.
    /// Для листовой детали рост ModelGeometryStamp может быть вызван fold/unfold
    /// развёртки — в этом случае DXF pending-штампы остаются равны export-штампам.
    /// Если все конфигурации DXF в норме (pending не больше export) — геометрия не менялась.
    /// </summary>
    private static bool IsDxfGeometryIntactForPartItem(VelumProductItem partItem)
    {
      if (partItem == null)
        return false;

      var configs = partItem.ExportMetaConfigs;
      if (configs == null || configs.Length == 0)
        return false;

      // Все конфигурации должны быть в норме: pending не больше export (или pending не записан).
      for (int i = 0; i < configs.Length; i++)
      {
        var cfg = configs[i];
        if (cfg == null)
          continue;

        int? exportStamp = cfg.DxfGeometryUpdateStamp;
        int? pendingStamp = cfg.DxfGeometryPendingStamp;

        // Если pending записан и больше export — геометрия менялась.
        if (pendingStamp.HasValue && exportStamp.HasValue && pendingStamp.Value > exportStamp.Value)
          return false;
      }

      // Дополнительная проверка: если ModelGeometryStamp детали больше, чем max(DxfGeometryUpdateStamp),
      // значит геометрия точно менялась, даже если pending-штамп не обновился
      // (например, из-за выключенной пульсации или race condition).
      // Допуск > 1 симметричен IsPdfOutdatedByModelGeometry: штатный fold/unfold
      // развёртки поднимает ModelGeometryStamp на 1 без изменения геометрии DXF.
      if (partItem.ModelGeometryStamp.HasValue && partItem.ModelGeometryStamp.Value > 0)
      {
        int maxDxfStamp = 0;
        for (int i = 0; i < configs.Length; i++)
        {
          var cfg = configs[i];
          if (cfg != null && cfg.DxfGeometryUpdateStamp.HasValue &&
              cfg.DxfGeometryUpdateStamp.Value > maxDxfStamp)
          {
            maxDxfStamp = cfg.DxfGeometryUpdateStamp.Value;
          }
        }

        // Если ModelGeometryStamp отстаёт от maxDxfStamp больше чем на 1, геометрия менялась.
        if (partItem.ModelGeometryStamp.Value - maxDxfStamp > 1)
          return false;
      }

      return true;
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
