using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ISIDA.Common;
using Newtonsoft.Json;
using Velum.Configuration;

namespace Velum.UI.AssemblyRegistry
{
  /// <summary>
  /// JSON-хранилище журнала изменений состава (<c>bomChangeLog.json</c>)
  /// в каталоге AssemblyRegistry. Невыгруженные записи (!Exported) —
  /// очередь на выгрузку; выгруженные хранятся RetentionDays дней.
  /// Файловый ввод-вывод — здесь; чистые правила отбора/очистки/рендера —
  /// в <see cref="VelumBomChangeLogRules"/>, DTO — в <see cref="VelumBomChangeRecord"/>.
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
    public const int RetentionDays = VelumBomChangeLogRules.RetentionDays;

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
    /// (невыгруженные !Exported — очередь — не удаляются никогда,
    /// кроме заведомо невыгружаемых — см. <see cref="PurgeUnexportablePending"/>).
    /// </summary>
    public void Save()
    {
      if (!_dirty)
        return;

      try
      {
        PurgeExportedOlderThan(TimeSpan.FromDays(RetentionDays));
        PurgeUnexportablePending();

        Directory.CreateDirectory(VelumAppConfig.AssemblyRegistryFolderPath);
        var wrapper = new VelumBomChangeLogFile
        {
          Records = VelumBomChangeLogRules.Filter(_records, includeExported: true)
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
      return VelumBomChangeLogRules.Filter(_records, includeExported: false);
    }

    /// <summary>
    /// Все записи журнала (и очередь, и выгруженные из RetentionDays-окна) —
    /// для просмотра истории на вкладке «Структура» формы обмена.
    /// CSV-выгрузка по-прежнему берёт только <see cref="GetPending"/>.
    /// </summary>
    public IReadOnlyList<VelumBomChangeRecord> GetAll()
    {
      return VelumBomChangeLogRules.Filter(_records, includeExported: true);
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
      int removed = VelumBomChangeLogRules.PurgeExportedOlderThan(_records, age, DateTime.UtcNow);
      if (removed > 0)
        _dirty = true;
      return removed;
    }

    /// <summary>
    /// Удалить невыгружаемые pending-записи: с пустым ParentExternalId
    /// они не попадут в обмен никогда, а срок хранения !Exported не истекает —
    /// без очистки такие записи зависают в журнале навсегда (CASEBOOK-2, случай 19 / E39).
    /// </summary>
    /// <returns>Число удалённых записей.</returns>
    public int PurgeUnexportablePending()
    {
      int removed = VelumBomChangeLogRules.PurgeUnexportablePending(_records);
      if (removed > 0)
        _dirty = true;
      return removed;
    }

    /// <summary>
    /// Проверить, есть ли невыгруженная запись с тем же действием, родителем
    /// и ребёнком (дедупликация повторного сохранения: идентичная операция
    /// не должна плодить новые записи до первой успешной выгрузки).
    /// </summary>
    /// <param name="action">Операция над строкой состава.</param>
    /// <param name="parentExternalId">ExternalId родителя.</param>
    /// <param name="childIdentity">Identity ребёнка.</param>
    /// <returns>true, если идентичная невыгруженная запись уже есть.</returns>
    public bool HasPending(VelumBomChangeAction action, string parentExternalId, string childIdentity)
    {
      return VelumBomChangeLogRules.HasPending(_records, action, parentExternalId, childIdentity);
    }

    /// <summary>
    /// Рендер операции для обмена с 1С (lowercase). Центральная точка:
    /// при смене требований 1С к формату операции менять только здесь.
    /// </summary>
    /// <param name="action">Операция над строкой состава.</param>
    /// <returns>Строковое представление для CSV/UI.</returns>
    public static string RenderAction(VelumBomChangeAction action)
    {
      return VelumBomChangeLogRules.RenderAction(action);
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