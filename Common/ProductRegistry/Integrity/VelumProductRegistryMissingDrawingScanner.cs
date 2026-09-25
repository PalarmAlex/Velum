using System;
using System.Collections.Generic;
using System.IO;
using Velum.ReactiveCore.Export;

namespace Velum.UI.ProductRegistry
{
/// <summary>
  /// Скан отсутствия чертежа для деталей/сборок по всему реестру (без early-stop).
  /// Учитывает <see cref="VelumProductItem.NeedDrawing"/>: при false проблему не ставит.
  /// Чертёж проверяется по зеркалу <see cref="VelumProductItem.DrawingPath"/> + File.Exists.
  /// </summary>
  internal static class VelumProductRegistryMissingDrawingScanner
  {
    private const int FullRegistryBatchSize = 80;

    /// <summary>
    /// Выполняет квант полного прохода. Возвращает true, если за этот тик
    /// завершён полный проход реестра (с начала до конца) и pending закоммичен.
    /// </summary>
    /// <param name="store">Реестр изделий.</param>
    /// <param name="mappings">Маппинг имён папок (не используется, зарезервировано).</param>
    /// <param name="searchCursor">Курсор прохода (сохраняется между тиками).</param>
    /// <param name="passActive">Флаг активного прохода (begin/commit pending).</param>
    /// <param name="shouldStop">
    /// Возвращает true, когда тик нужно прервать (наступил следующий тяжёлый пульс
    /// либо открылся документ SW). При null — прежнее поведение с фиксированным квантом.
    /// </param>
    internal static bool Tick(
        VelumProductRegistryStore store,
        IList<VelumProductFolderAutoNameMapping> mappings,
        ref int searchCursor,
        ref bool passActive,
        Func<bool> shouldStop = null)
    {
      bool passCompleted = false;
      _ = mappings;
      if (store == null)
        return false;

      IReadOnlyList<VelumProductItem> items = store.GetAllItems();
      if (items.Count == 0)
      {
        if (passActive)
        {
          VelumProductRegistryProblemCache.CommitPendingPass(
              VelumProductRegistryProblemKind.MissingDrawing);
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
            VelumProductRegistryProblemKind.MissingDrawing);
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
        if (item == null || item.Id <= 0)
          continue;
        if (!VelumProductRegistryIntegrityRules.IsPartOrAssemblyPath(item.FilePath))
          continue;

        if (!TryEvaluateMissingDrawing(item, out VelumProductRegistryProblemEntry problem))
          continue;

        VelumProductRegistryProblemCache.UpsertPending(problem);
      }

      if (searchCursor >= items.Count)
      {
        VelumProductRegistryProblemCache.CommitPendingPass(
            VelumProductRegistryProblemKind.MissingDrawing);
        searchCursor = 0;
        passActive = false;
        passCompleted = true;
      }

      return passCompleted;
    }

    internal static bool TryEvaluateMissingDrawing(
        VelumProductRegistryStore store,
        IList<VelumProductFolderAutoNameMapping> mappings,
        VelumProductItem modelItem,
        out VelumProductRegistryProblemEntry problem)
    {
      _ = store;
      _ = mappings;
      return TryEvaluateMissingDrawing(modelItem, out problem);
    }

    internal static bool TryEvaluateMissingDrawing(
        VelumProductItem modelItem,
        out VelumProductRegistryProblemEntry problem)
    {
      problem = null;
      if (modelItem == null || modelItem.Id <= 0)
        return false;
      if (!VelumProductRegistryIntegrityRules.IsPartOrAssemblyPath(modelItem.FilePath))
        return false;

      // Нет файла модели — «нет чертежа» вторично относительно битой ссылки.
      string modelPath = modelItem.GetNormalizedPathKey();
      if (string.IsNullOrEmpty(modelPath)
          || VelumProductRegistryIntegrityRules.PathExistsOrTimedOutIsMissing(modelPath))
        return false;

      if (!modelItem.NeedDrawing)
        return false;

      // null — зеркало ещё не синхронизировалось (например после «Загрузить»).
      // Пустая строка — sync был, свойства «путь чертежа» нет или оно пустое.
      if (modelItem.DrawingPath == null)
        return false;

      string drawingPath = modelItem.DrawingPath.Trim();
      if (string.IsNullOrWhiteSpace(drawingPath))
      {
        problem = Build(modelItem, "Нет свойства «путь чертежа» в документе (зеркало пусто).");
        return true;
      }

      // Зеркало может хранить относительный путь — достраиваем префикс корневого каталога.
      drawingPath = Velum.ReactiveCore.Export.VelumRelativeDocumentPathResolver.ToFull(drawingPath);

      if (!VelumPathExists.FileExists(drawingPath))
      {
        problem = Build(
            modelItem,
            "Файл чертежа по «путь чертежа» отсутствует: " + drawingPath);
        return true;
      }

      return false;
    }

    private static VelumProductRegistryProblemEntry Build(VelumProductItem item, string detail)
    {
      return new VelumProductRegistryProblemEntry
      {
        ItemId = item.Id,
        Kind = VelumProductRegistryProblemKind.MissingDrawing,
        Designation = item.Designation,
        Name = item.Name,
        FilePath = item.FilePath,
        Detail = detail
      };
    }
  }
}
