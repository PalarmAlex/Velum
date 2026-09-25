using System;
using System.Globalization;

namespace Velum.UI.AssemblyRegistry
{
  /// <summary>
  /// Извлекает значение колонки из записи зеркала BOM.
  /// Единственный источник правды для формы экспорта и CSV-выгрузки.
  /// </summary>
  internal static class VelumBomExchangeRowProjector
  {
    /// <summary>
    /// Получить текстовое значение ячейки для записи зеркала и описания колонки.
    /// Структурные поля берутся из полей записи; отслеживаемые свойства — из снимка
    /// <see cref="VelumAssemblyBomMirrorEntry.TrackedValues"/> (значения уже округлены
    /// при зеркалировании до заданной точности).
    /// </summary>
    /// <param name="entry">Запись зеркала BOM.</param>
    /// <param name="col">Описание колонки выгрузки.</param>
    /// <returns>Текстовое значение (пустая строка, если значение недоступно).</returns>
    internal static string GetValue(
        VelumAssemblyBomMirrorEntry entry,
        VelumBomExchangeColumnDef col)
    {
      if (entry == null || col == null)
        return string.Empty;

      if (col.Source == VelumBomExchangeFieldSource.Structural)
      {
        switch ((col.Field ?? string.Empty).Trim())
        {
          case "TypeDocs":      return GetTypeDocs(entry);
          case "ExternalId":    return entry.ExternalId ?? string.Empty;
          case "Designation":   return entry.Designation ?? string.Empty;
          case "Name":          return entry.Name ?? string.Empty;
          case "Quantity":      return entry.Quantity.ToString(CultureInfo.InvariantCulture);
          case "FilePath":      return entry.FilePath ?? string.Empty;
          case "Configuration": return entry.ConfigurationName ?? string.Empty;
          default:              return string.Empty;
        }
      }

      // Отслеживаемое свойство — берём из снимка значений.
      string key = (col.Field ?? string.Empty).Trim();
      if (key.Length == 0 || entry.TrackedValues == null)
        return string.Empty;

      string raw;
      return entry.TrackedValues.TryGetValue(key, out raw) ? (raw ?? string.Empty) : string.Empty;
    }

    /// <summary>
    /// Тип документа для колонки <c>TypeDocs</c>.
    /// Берётся фактический тип SOLIDWORKS-документа из записи зеркала. Для записей,
    /// созданных до появления поля <see cref="VelumAssemblyBomMirrorEntry.DocType"/>,
    /// сохраняется прежнее определение по количеству (иначе тип всех старых позиций
    /// стал бы пустым до их ближайшего пересохранения).
    /// </summary>
    private static string GetTypeDocs(VelumAssemblyBomMirrorEntry entry)
    {
      string docType = (entry.DocType ?? string.Empty).Trim();
      if (string.Equals(docType, VelumAssemblyBomMirrorEntry.DocTypeAssembly,
              StringComparison.OrdinalIgnoreCase))
        return VelumAssemblyBomMirrorEntry.DocTypeAssembly;
      if (string.Equals(docType, VelumAssemblyBomMirrorEntry.DocTypePart,
              StringComparison.OrdinalIgnoreCase))
        return VelumAssemblyBomMirrorEntry.DocTypePart;

      return entry.Quantity > 0
          ? VelumAssemblyBomMirrorEntry.DocTypeAssembly
          : VelumAssemblyBomMirrorEntry.DocTypePart;
    }
  }
}