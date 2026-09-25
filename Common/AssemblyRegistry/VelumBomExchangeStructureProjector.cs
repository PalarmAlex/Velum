using System;
using System.Collections.Generic;

namespace Velum.UI.AssemblyRegistry
{
  /// <summary>
  /// Отбор записей структуры состава для выгрузки в 1С.
  /// Единственный источник правды для формы экспорта и CSV-выгрузки —
  /// исключает расхождение между списком в форме и содержимым 1C_bom_*.csv.
  /// </summary>
  internal static class VelumBomExchangeStructureProjector
  {
    /// <summary>
    /// Результат отбора: записи журнала к выгрузке и структуры с расхождением
    /// (по ParentExternalId — для обновления previousHash после экспорта).
    /// </summary>
    internal sealed class Selection
    {
      /// <summary>
      /// Записи журнала к выгрузке — копии с актуализированными полями:
      /// ParentConfiguration — из структуры родителя,
      /// ChildExternalId — из зеркала карточек (источник правды).
      /// </summary>
      public List<VelumBomChangeRecord> Records { get; } =
          new List<VelumBomChangeRecord>();

      /// <summary>Структуры с расхождением (ParentExternalId → запись).</summary>
      public Dictionary<string, VelumBomStructureEntry> StructuresByParentExternalId { get; } =
          new Dictionary<string, VelumBomStructureEntry>(StringComparer.Ordinal);

      /// <summary>
      /// ParentExternalId структур, по которым остались невыгруженные строки,
      /// отложенные только из-за того, что у ребёнка ещё нет ExternalId.
      /// Выгрузив такие строки частично, нельзя сбрасывать previousHash структуры:
      /// иначе структура перестанет считаться расходящейся и её оставшиеся строки
      /// не попадут в обмен даже после появления ExternalId у ребёнка.
      /// </summary>
      public HashSet<string> DeferredParentExternalIds { get; } =
          new HashSet<string>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Отобрать записи структуры для выгрузки:
    /// pending-записи журнала по структурам с текущим расхождением,
    /// у которых актуальный ExternalId ребёнка (из зеркала карточек) непустой.
    /// ParentConfiguration и ChildExternalId актуализируются из источников правды
    /// (структура и зеркало карточек); снимки в записях журнала — только для истории.
    /// </summary>
    /// <param name="structureStore">Загруженный store структур состава.</param>
    /// <param name="mirrorStore">Загруженный store зеркала карточек.</param>
    /// <param name="changeStore">Загруженный store журнала изменений.</param>
    /// <returns>Отбор: записи к выгрузке + структуры с расхождением.</returns>
    internal static Selection Select(
        VelumBomStructureStore structureStore,
        VelumAssemblyBomMirrorStore mirrorStore,
        VelumBomChangeLogStore changeStore)
    {
      Selection selection = new Selection();

      // Структуры с расхождением — по ParentExternalId.
      IReadOnlyList<VelumBomStructureEntry> discrepancies =
          structureStore.GetEntriesWithDiscrepancy();
      foreach (VelumBomStructureEntry entry in discrepancies)
      {
        if (entry == null || string.IsNullOrWhiteSpace(entry.ParentExternalId))
          continue;
        if (!selection.StructuresByParentExternalId.ContainsKey(entry.ParentExternalId))
          selection.StructuresByParentExternalId[entry.ParentExternalId] = entry;
      }

      // Pending-записи журнала — только по структурам с текущим расхождением
      // и с непустым актуальным ExternalId ребёнка.
      foreach (VelumBomChangeRecord record in changeStore.GetPending())
      {
        if (record == null ||
            string.IsNullOrWhiteSpace(record.ParentExternalId) ||
            string.IsNullOrWhiteSpace(record.ChildIdentity))
          continue;

        VelumBomStructureEntry structureEntry;
        if (!selection.StructuresByParentExternalId.TryGetValue(
                record.ParentExternalId, out structureEntry))
          continue;

        // Актуальный ExternalId ребёнка — из зеркала карточек (источник правды).
        VelumAssemblyBomMirrorEntry childMirror = mirrorStore.GetEntry(record.ChildIdentity);
        if (childMirror == null || string.IsNullOrWhiteSpace(childMirror.ExternalId))
        {
          // Строка отложена до появления ExternalId у ребёнка — расхождение
          // структуры должно сохраняться, пока она не выгружена.
          selection.DeferredParentExternalIds.Add(record.ParentExternalId);
          continue;
        }

        // Копия записи с актуализированными полями (оригинал не мутируем).
        selection.Records.Add(new VelumBomChangeRecord
        {
          Id = record.Id,
          Action = record.Action,
          ParentExternalId = record.ParentExternalId,
          ParentConfiguration = structureEntry.ParentConfiguration ?? string.Empty,
          ChildIdentity = record.ChildIdentity,
          ChildExternalId = childMirror.ExternalId,
          ChildConfiguration = record.ChildConfiguration,
          Quantity = record.Quantity,
          TimestampUtc = record.TimestampUtc,
          SourceHash = record.SourceHash,
          Exported = record.Exported,
          ExportedUtc = record.ExportedUtc
        });
      }

      return selection;
    }
  }
}
