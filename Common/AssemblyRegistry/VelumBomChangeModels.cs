using System;
using System.Collections.Generic;

namespace Velum.UI.AssemblyRegistry
{
  /// <summary>Операция над строкой состава для передачи в 1С.</summary>
  internal enum VelumBomChangeAction
  {
    /// <summary>Строка состава добавлена в родителя.</summary>
    Add = 0,

    /// <summary>Строка состава изменена (количество и/или ExternalId ребёнка).</summary>
    Update = 1,

    /// <summary>Строка состава удалена из родителя.</summary>
    Delete = 2
  }

  /// <summary>
  /// Одна запись журнала изменений состава: операция над строкой
  /// (родитель → ребёнок) между двумя зеркалированиями структуры.
  /// </summary>
  internal sealed class VelumBomChangeRecord
  {
    /// <summary>Уникальный идентификатор записи (Guid).</summary>
    public string Id { get; set; }

    /// <summary>Операция над строкой состава.</summary>
    public VelumBomChangeAction Action { get; set; }

    /// <summary>ExternalId родителя (ключ номенклатуры в 1С).</summary>
    public string ParentExternalId { get; set; }

    /// <summary>Конфигурация родителя (часть ключа номенклатуры в 1С).</summary>
    public string ParentConfiguration { get; set; }

    /// <summary>
    /// Identity дочернего компонента (FilePath|Config) — снимок на момент записи.
    /// Служебное поле: по нему при выгрузке берётся актуальный ExternalId
    /// из bomMirror.json. В CSV не выгружается.
    /// </summary>
    public string ChildIdentity { get; set; }

    /// <summary>ExternalId дочернего компонента — снимок на момент записи.</summary>
    public string ChildExternalId { get; set; }

    /// <summary>Конфигурация дочернего компонента (часть ключа номенклатуры в 1С).</summary>
    public string ChildConfiguration { get; set; }

    /// <summary>Количество в родителе (для Delete — количество до удаления).</summary>
    public int Quantity { get; set; }

    /// <summary>Момент записи (UTC).</summary>
    public DateTime TimestampUtc { get; set; }

    /// <summary>
    /// Хэш состояния структуры родителя, к которому относится запись:
    /// для Add/Update — CurrentHash (новое состояние),
    /// для Delete — PreviousHash (старое состояние, из которого строка удалена).
    /// </summary>
    public string SourceHash { get; set; }

    /// <summary>Выгружено в 1С (после успешной записи CSV).</summary>
    public bool Exported { get; set; }

    /// <summary>Момент выгрузки в 1С (UTC).</summary>
    public DateTime? ExportedUtc { get; set; }
  }

  /// <summary>Корень JSON-файла <c>bomChangeLog.json</c>.</summary>
  internal sealed class VelumBomChangeLogFile
  {
    /// <summary>Записи журнала изменений состава.</summary>
    public List<VelumBomChangeRecord> Records { get; set; } =
        new List<VelumBomChangeRecord>();
  }
}
