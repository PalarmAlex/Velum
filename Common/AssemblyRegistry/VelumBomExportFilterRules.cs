using System;

namespace Velum.UI.AssemblyRegistry
{
  /// <summary>
  /// Единый чистый предикт пригодности <c>ExternalId</c> к обмену с 1С.
  /// Работает только со строкой, без COM / файлов / конфигурации — линкуется
  /// в тестовый проект Velum.ReactiveCore.Tests (см. AGENTS.md, эвристики E9/E64:
  /// предикт обязан жить в одном месте, а не копироваться по конвейеру).
  /// </summary>
  /// <remarks>
  /// Правила «не экспортируется»: значение пустое, состоит только из пробелов,
  /// либо после обрезки пробелов равно «0». Ноль приравнен к пустому: это
  /// фактический запрет на выгрузку позиции (карточки номенклатуры и строки
  /// состава), а не валидный идентификатор.
  /// </remarks>
  internal static class VelumBomExportFilterRules
  {
    /// <summary>
    /// Признак «ExternalId запрещает экспорт»: пусто, только пробелы или «0»
    /// (после обрезки пробелов). Обратная сторона — <see cref="IsExportable"/>.
    /// </summary>
    /// <param name="externalId">Значение свойства ExternalId (может быть <c>null</c>).</param>
    /// <returns>true, если позиция не должна попадать в обмен с 1С.</returns>
    internal static bool IsExportForbidden(string externalId)
    {
      string trimmed = (externalId ?? string.Empty).Trim();
      return trimmed.Length == 0 ||
          string.Equals(trimmed, "0", StringComparison.Ordinal);
    }

    /// <summary>
    /// Признак «ExternalId разрешает экспорт»: непустой и не равен «0»
    /// (после обрезки пробелов). Единственная точка истины для всего конвейера
    /// обмена с 1С (зеркало карточек, структура состава, выгрузка, предпросмотр).
    /// </summary>
    /// <param name="externalId">Значение свойства ExternalId (может быть <c>null</c>).</param>
    /// <returns>true, если позиция участвует в обмене с 1С.</returns>
    internal static bool IsExportable(string externalId)
    {
      return !IsExportForbidden(externalId);
    }
  }
}
