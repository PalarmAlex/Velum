using System;
using System.Collections.Generic;
using System.Linq;

namespace Velum.UI.AssemblyRegistry
{
  /// <summary>
  /// Pure-правила журнала изменений состава BOM: отбор/сортировка записей, дедупликация
  /// невыгруженных операций, очистка невыгружаемых pending-записей и рендер операции для 1С.
  /// Работают над списком записей, без файлов, COM и конфигурации — линкуется в тестовый
  /// проект Velum.ReactiveCore.Tests.
  /// </summary>
  /// <remarks>
  /// Регрессии, которые защищают правила: CASEBOOK-2 случай 19 — E39 (невыгружаемая
  /// pending-запись без ParentExternalId зависает навсегда) и E40 (регистр ExternalId
  /// сравнивался по-разному в разных точках конвейера).
  /// </remarks>
  internal static class VelumBomChangeLogRules
  {
    /// <summary>Срок хранения выгруженных записей (Exported=true), дней.</summary>
    public const int RetentionDays = 10;

    /// <summary>
    /// Собирает список записей в стабильном хронологическом порядке
    /// (TimestampUtc, затем Id по Ordinal).
    /// </summary>
    /// <param name="records">Исходный список записей.</param>
    /// <param name="includeExported">Включать ли выгруженные записи.</param>
    /// <returns>Отфильтрованный и отсортированный список.</returns>
    internal static List<VelumBomChangeRecord> Filter(
        IEnumerable<VelumBomChangeRecord> records, bool includeExported)
    {
      var result = new List<VelumBomChangeRecord>();
      if (records == null)
        return result;

      foreach (VelumBomChangeRecord record in records)
      {
        if (record == null)
          continue;
        if (!includeExported && record.Exported)
          continue;
        result.Add(record);
      }
      result.Sort((a, b) =>
      {
        int cmp = a.TimestampUtc.CompareTo(b.TimestampUtc);
        if (cmp != 0)
          return cmp;
        return string.Compare(a.Id, b.Id, StringComparison.Ordinal);
      });
      return result;
    }

    /// <summary>
    /// Удаляет выгруженные записи (Exported=true) старше заданного срока.
    /// Невыгруженные записи не удаляются никогда.
    /// </summary>
    /// <param name="records">Список записей (изменяется на месте).</param>
    /// <param name="age">Максимальный возраст выгруженной записи.</param>
    /// <param name="nowUtc">Текущий момент (UTC).</param>
    /// <returns>Число удалённых записей.</returns>
    internal static int PurgeExportedOlderThan(
        List<VelumBomChangeRecord> records, TimeSpan age, DateTime nowUtc)
    {
      if (records == null)
        return 0;

      DateTime cutoff = nowUtc - age;
      return records.RemoveAll(r =>
          r != null && r.Exported && r.ExportedUtc.HasValue && r.ExportedUtc.Value < cutoff);
    }

    /// <summary>
    /// Удаляет невыгружаемые pending-записи: с пустым ParentExternalId они не попадут
    /// в обмен никогда (отбор в <c>VelumBomExchangeStructureProjector.Select</c> их
    /// пропускает), а срок хранения !Exported не истекает — без очистки такие записи
    /// зависают в журнале навсегда (E39).
    /// </summary>
    /// <param name="records">Список записей (изменяется на месте).</param>
    /// <returns>Число удалённых записей.</returns>
    internal static int PurgeUnexportablePending(List<VelumBomChangeRecord> records)
    {
      if (records == null)
        return 0;

      return records.RemoveAll(r =>
          r != null && !r.Exported && string.IsNullOrWhiteSpace(r.ParentExternalId));
    }

    /// <summary>
    /// Проверяет, есть ли невыгруженная запись с тем же действием, родителем и ребёнком
    /// (дедупликация повторного сохранения: идентичная операция не должна плодить новые
    /// записи до первой успешной выгрузки). Сравнение — регистронезависимо (E40).
    /// </summary>
    /// <param name="records">Список записей.</param>
    /// <param name="action">Операция над строкой состава.</param>
    /// <param name="parentExternalId">ExternalId родителя.</param>
    /// <param name="childIdentity">Identity ребёнка.</param>
    /// <returns>true, если идентичная невыгруженная запись уже есть.</returns>
    internal static bool HasPending(
        IEnumerable<VelumBomChangeRecord> records,
        VelumBomChangeAction action,
        string parentExternalId,
        string childIdentity)
    {
      if (records == null)
        return false;

      string parent = (parentExternalId ?? string.Empty).Trim();
      string child = (childIdentity ?? string.Empty).Trim();
      foreach (VelumBomChangeRecord record in records)
      {
        if (record == null || record.Exported || record.Action != action)
          continue;
        if (string.Equals((record.ParentExternalId ?? string.Empty).Trim(), parent,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals((record.ChildIdentity ?? string.Empty).Trim(), child,
                StringComparison.OrdinalIgnoreCase))
          return true;
      }
      return false;
    }

    /// <summary>
    /// Рендер операции для обмена с 1С (lowercase). Центральная точка:
    /// при смене требований 1С к формату операции менять только здесь.
    /// </summary>
    /// <param name="action">Операция над строкой состава.</param>
    /// <returns>Строковое представление для CSV/UI.</returns>
    internal static string RenderAction(VelumBomChangeAction action)
    {
      switch (action)
      {
        case VelumBomChangeAction.Add:
          return "add";
        case VelumBomChangeAction.Update:
          return "update";
        case VelumBomChangeAction.Delete:
          return "delete";
        default:
          return string.Empty;
      }
    }
  }
}
