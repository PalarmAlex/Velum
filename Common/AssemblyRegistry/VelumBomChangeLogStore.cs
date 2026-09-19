using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ISIDA.Common;
using Newtonsoft.Json;
using Velum.Configuration;

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

  /// <summary>
  /// JSON-хранилище журнала изменений состава (<c>bomChangeLog.json</c>)
  /// в каталоге AssemblyRegistry. Невыгруженные записи (!Exported) —
  /// очередь на выгрузку; выгруженные хранятся RetentionDays дней.
  /// </summary>
  internal sealed class VelumBomChangeLogStore
  {
    /// <summary>Настройки сериализации JSON.</summary>
    private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
    {
      Formatting = Formatting.Indented,
      NullValueHandling = NullValueHandling.Ignore
    };

    /// <summary>Срок хранения выгруженных записей (Exported=true), дней.</summary>
    public const int RetentionDays = 10;

    private readonly List<VelumBomChangeRecord> _records =
        new List<VelumBomChangeRecord>();

    private bool _dirty;

    /// <summary>Путь к файлу хранения.</summary>
    public static string ChangeLogFilePath => Path.Combine(
        VelumAppConfig.AssemblyRegistryFolderPath,
        "bomChangeLog.json");

    /// <summary>Загрузка из JSON.</summary>
    public void Load()
    {
      Directory.CreateDirectory(VelumAppConfig.AssemblyRegistryFolderPath);

      _records.Clear();
      _dirty = false;

      string json = ReadJsonFile(ChangeLogFilePath);
      if (string.IsNullOrWhiteSpace(json))
        return;

      try
      {
        var wrapper = JsonConvert.DeserializeObject<VelumBomChangeLogFile>(json, JsonSettings);
        if (wrapper?.Records != null)
        {
          foreach (VelumBomChangeRecord record in wrapper.Records)
          {
            if (record == null || string.IsNullOrWhiteSpace(record.Id))
              continue;
            record.Id = record.Id.Trim();
            record.ParentExternalId = (record.ParentExternalId ?? string.Empty).Trim();
            record.ParentConfiguration = (record.ParentConfiguration ?? string.Empty).Trim();
            record.ChildIdentity = (record.ChildIdentity ?? string.Empty).Trim();
            record.ChildExternalId = (record.ChildExternalId ?? string.Empty).Trim();
            record.ChildConfiguration = (record.ChildConfiguration ?? string.Empty).Trim();
            record.SourceHash = (record.SourceHash ?? string.Empty).Trim();
            _records.Add(record);
          }
        }
      }
      catch (Exception ex)
      {
        Logger.Error("Velum bomChangeLogStore load failed (" + ChangeLogFilePath + "): " + ex.Message);
      }
    }

    /// <summary>
    /// Сохранение в JSON (атомарно). Перед записью автоматически чистит
    /// выгруженные записи старше <see cref="RetentionDays"/> дней
    /// (невыгруженные !Exported — очередь — не удаляются никогда).
    /// </summary>
    public void Save()
    {
      if (!_dirty)
        return;

      try
      {
        PurgeExportedOlderThan(TimeSpan.FromDays(RetentionDays));

        Directory.CreateDirectory(VelumAppConfig.AssemblyRegistryFolderPath);
        var wrapper = new VelumBomChangeLogFile
        {
          Records = _records
              .OrderBy(r => r.TimestampUtc)
              .ThenBy(r => r.Id, StringComparer.Ordinal)
              .ToList()
        };
        string json = JsonConvert.SerializeObject(wrapper, JsonSettings);
        WriteJsonAtomic(ChangeLogFilePath, json);
        _dirty = false;
      }
      catch (Exception ex)
      {
        Logger.Error("Velum bomChangeLogStore save failed: " + ex.Message);
      }
    }

    /// <summary>Добавить запись в журнал.</summary>
    /// <param name="record">Запись об изменении строки состава.</param>
    public void Append(VelumBomChangeRecord record)
    {
      if (record == null || string.IsNullOrWhiteSpace(record.Id))
        return;

      _records.Add(record);
      _dirty = true;
    }

    /// <summary>
    /// Получить все невыгруженные записи (Exported=false) — очередь на выгрузку.
    /// Порядок: TimestampUtc, затем Id (стабильная хронология внутри пары).
    /// </summary>
    public IReadOnlyList<VelumBomChangeRecord> GetPending()
    {
      var result = new List<VelumBomChangeRecord>();
      foreach (VelumBomChangeRecord record in _records)
      {
        if (record != null && !record.Exported)
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

    /// <summary>Пометить записи выгруженными (после успешной записи CSV).</summary>
    /// <param name="ids">Идентификаторы записей.</param>
    /// <param name="exportedUtc">Момент выгрузки (UTC).</param>
    public void MarkExported(IEnumerable<string> ids, DateTime exportedUtc)
    {
      if (ids == null)
        return;

      HashSet<string> idSet = new HashSet<string>(ids, StringComparer.Ordinal);
      foreach (VelumBomChangeRecord record in _records)
      {
        if (record != null && !record.Exported && idSet.Contains(record.Id))
        {
          record.Exported = true;
          record.ExportedUtc = exportedUtc;
          _dirty = true;
        }
      }
    }

    /// <summary>
    /// Удалить выгруженные записи (Exported=true) старше заданного срока.
    /// Невыгруженные записи не удаляются никогда.
    /// </summary>
    /// <param name="age">Максимальный возраст выгруженной записи.</param>
    /// <returns>Число удалённых записей.</returns>
    public int PurgeExportedOlderThan(TimeSpan age)
    {
      DateTime cutoff = DateTime.UtcNow - age;
      int removed = _records.RemoveAll(r =>
          r != null && r.Exported && r.ExportedUtc.HasValue && r.ExportedUtc.Value < cutoff);
      if (removed > 0)
        _dirty = true;
      return removed;
    }

    /// <summary>
    /// Рендер операции для обмена с 1С (lowercase). Центральная точка:
    /// при смене требований 1С к формату операции менять только здесь.
    /// </summary>
    /// <param name="action">Операция над строкой состава.</param>
    /// <returns>Строковое представление для CSV/UI.</returns>
    public static string RenderAction(VelumBomChangeAction action)
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

    /// <summary>Прочитать файл; при ошибке — <c>null</c>.</summary>
    private static string ReadJsonFile(string path)
    {
      try
      {
        if (!File.Exists(path))
          return null;
        return File.ReadAllText(path, Encoding.UTF8);
      }
      catch
      {
        return null;
      }
    }

    /// <summary>Атомарная запись файла (как в <c>VelumAssemblyBomMirrorStore</c>).</summary>
    private static void WriteJsonAtomic(string path, string json)
    {
      string tempPath = path + ".tmp";
      File.WriteAllText(tempPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
      if (File.Exists(path))
        File.Replace(tempPath, path, null);
      else
        File.Move(tempPath, path);
    }
  }
}
