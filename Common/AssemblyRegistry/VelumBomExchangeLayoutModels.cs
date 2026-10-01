using System.Collections.Generic;
using Newtonsoft.Json;

namespace Velum.UI.AssemblyRegistry
{
  /// <summary>
  /// Источник значения колонки выгрузки BOM: структурное поле зеркала или отслеживаемое свойство.
  /// </summary>
  internal enum VelumBomExchangeFieldSource
  {
    /// <summary>Структурное поле записи зеркала BOM (TypeDocs, ExternalId, …).</summary>
    Structural = 0,

    /// <summary>Значение отслеживаемого свойства из снимка <c>TrackedValues</c>.</summary>
    Tracked = 1
  }

  /// <summary>
  /// Описание одной колонки CSV/списка экспорта BOM в 1С.
  /// </summary>
  internal sealed class VelumBomExchangeColumnDef
  {
    /// <summary>Ключ структурного поля или имя отслеживаемого свойства.</summary>
    public string Field { get; set; }

    /// <summary>Заголовок колонки в CSV и в списке формы.</summary>
    public string Header { get; set; }

    /// <summary>Источник значения колонки.</summary>
    public VelumBomExchangeFieldSource Source { get; set; }

    /// <summary>Включена ли колонка в выгрузку.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Порядковый номер колонки (1..N).</summary>
    public int Order { get; set; }

    /// <summary>Ширина колонки в списке формы, пикселей.</summary>
    public int Width { get; set; } = 120;
  }

  /// <summary>
  /// Корень файла <c>bomExchangeLayout.json</c> — упорядоченный список колонок выгрузки.
  /// </summary>
  internal sealed class VelumBomExchangeLayoutFile
  {
    /// <summary>
    /// Версия формата layout, записанная в файле. Отсутствие поля (0) означает
    /// набор до появления <see cref="VelumBomExchangeLayoutStore.CurrentLayoutFormatVersion"/>.
    /// </summary>
    [JsonProperty("layoutFormatVersion")]
    public int LayoutFormatVersion { get; set; }

    /// <summary>Колонки выгрузки в порядке следования.</summary>
    public List<VelumBomExchangeColumnDef> Columns { get; set; } =
        new List<VelumBomExchangeColumnDef>();
  }
}
