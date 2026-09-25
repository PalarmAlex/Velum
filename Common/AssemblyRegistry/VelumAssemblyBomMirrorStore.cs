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
  /// <summary>
  /// Модель зеркальной записи BOM-хэшей для одного документа (вхождения).
  /// </summary>
  internal sealed class VelumAssemblyBomMirrorEntry
  {
    /// <summary>Значение <see cref="DocType"/> для сборки.</summary>
    public const string DocTypeAssembly = "Assembly";

    /// <summary>Значение <see cref="DocType"/> для детали.</summary>
    public const string DocTypePart = "Part";

    /// <summary>Ключ — нормализованный путь + "|" + имя конфигурации.</summary>
    public string Identity { get; set; }

    /// <summary>Путь к файлу (нормализованный).</summary>
    public string FilePath { get; set; }

    /// <summary>Имя конфигурации.</summary>
    public string ConfigurationName { get; set; }

    /// <summary>Внешний ID записи в 1C (из свойства SW). Пусто, если не задан.</summary>
    public string ExternalId { get; set; }

    /// <summary>Обозначение (из свойства SW).</summary>
    public string Designation { get; set; }

    /// <summary>Наименование (из свойства SW).</summary>
    public string Name { get; set; }

    /// <summary>
    /// Фактический тип SOLIDWORKS-документа: <see cref="DocTypeAssembly"/> или
    /// <see cref="DocTypePart"/>. Пусто у записей, созданных до появления поля.
    /// </summary>
    public string DocType { get; set; }

    /// <summary>Количество вхождений в головной сборке (0 для деталей).</summary>
    public int Quantity { get; set; }

    /// <summary>Внутренний ID записи в реестре изделий (присваивается Velum).</summary>
    public int? RegistryId { get; set; }

    /// <summary>Хэш текущего состояния tracked properties + количество.</summary>
    public string CurrentHash { get; set; }

    /// <summary>Хэш предыдущего состояния (сравнивается при сканировании).</summary>
    public string PreviousHash { get; set; }

    /// <summary>Снимок значений отслеживаемых свойств (для CSV-экспорта без COM).</summary>
    public Dictionary<string, string> TrackedValues { get; set; }

    /// <summary>
    /// Момент последнего подтверждения существования файла (UTC, ISO 8601).
    /// Заполняется при каждом <see cref="VelumAssemblyBomMirrorStore.Upsert"/>.
    /// Запись, у которой файл исчез (диагностика зеркала), считается устаревшей
    /// (<c>Stale</c>) и не участвует в выгрузке, пока не появится вновь.
    /// </summary>
    public DateTime? LastSeenUtc { get; set; }

    /// <summary>
    /// Признак устаревшей записи: файл не найден при последней диагностике зеркала.
    /// Устаревшие записи не выгружаются в 1С и подсвечиваются на форме экспорта.
    /// Сбрасывается при следующем <see cref="VelumAssemblyBomMirrorStore.Upsert"/>.
    /// </summary>
    public bool Stale { get; set; }
  }

  /// <summary>
  /// JSON-хранилище хэшей BOM-состояний компонентов в каталоге AssemblyRegistry.
  /// Ключ — нормализованный путь + "|" + конфигурация.
  /// </summary>
  internal sealed class VelumAssemblyBomMirrorStore
  {
    private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
    {
      Formatting = Formatting.Indented,
      NullValueHandling = NullValueHandling.Ignore
    };

    private readonly Dictionary<string, VelumAssemblyBomMirrorEntry> _entries =
        new Dictionary<string, VelumAssemblyBomMirrorEntry>(StringComparer.OrdinalIgnoreCase);

    private bool _dirty;

    /// <summary>
    /// Версия формата хэша, записанная в файле. По умолчанию равна текущей —
    /// так новый (ещё не существующий на диске) файл не запускает миграцию.
    /// </summary>
    private int _hashFormatVersion = VelumAssemblyBomTrackedPropertiesConfig.HashFormatVersion;

    /// <summary>Путь к файлу хранения.</summary>
    public static string MirrorFilePath => Path.Combine(
        VelumAppConfig.AssemblyRegistryFolderPath,
        "bomMirror.json");

    /// <summary>Загрузка из JSON.</summary>
    public void Load()
    {
      Directory.CreateDirectory(VelumAppConfig.AssemblyRegistryFolderPath);

      _entries.Clear();
      _dirty = false;

      string json = ReadJsonFile(MirrorFilePath);
      if (string.IsNullOrWhiteSpace(json))
        return;

      try
      {
        var wrapper = JsonConvert.DeserializeObject<BomMirrorWrapper>(json, JsonSettings);
        if (wrapper == null)
          return;

        // 0 — поле отсутствовало в файле, то есть формат 1 («свойства + Quantity»).
        _hashFormatVersion = wrapper.HashFormatVersion;

        if (wrapper.Entries != null)
        {
          foreach (var entry in wrapper.Entries)
          {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Identity))
              continue;
            entry.Identity = entry.Identity.Trim();
            entry.FilePath = (entry.FilePath ?? string.Empty).Trim();
            entry.ConfigurationName = (entry.ConfigurationName ?? string.Empty).Trim();
            entry.ExternalId = (entry.ExternalId ?? string.Empty).Trim();
            entry.Designation = (entry.Designation ?? string.Empty).Trim();
            entry.Name = (entry.Name ?? string.Empty).Trim();
            entry.DocType = (entry.DocType ?? string.Empty).Trim();
            entry.CurrentHash = (entry.CurrentHash ?? string.Empty).Trim();
            entry.PreviousHash = (entry.PreviousHash ?? string.Empty).Trim();
            _entries[entry.Identity] = entry;
          }
        }

        MigrateHashFormatIfNeeded();
      }
      catch (Exception ex)
      {
        Logger.Error("Velum bomMirrorStore load failed (" + MirrorFilePath + "): " + ex.Message);
      }
    }

    /// <summary>Сохранение в JSON (атомарно).</summary>
    public void Save()
    {
      if (!_dirty)
        return;

      try
      {
        Directory.CreateDirectory(VelumAppConfig.AssemblyRegistryFolderPath);
        var wrapper = new BomMirrorWrapper
        {
          HashFormatVersion = _hashFormatVersion,
          Entries = _entries.Values.OrderBy(e => e.Identity).ToArray()
        };
        string json = JsonConvert.SerializeObject(wrapper, JsonSettings);
        WriteJsonAtomic(MirrorFilePath, json);
        _dirty = false;
      }
      catch (Exception ex)
      {
        Logger.Error("Velum bomMirrorStore save failed: " + ex.Message);
      }
    }

    /// <summary>Получить запись по ключу.</summary>
    public VelumAssemblyBomMirrorEntry GetEntry(string identity)
    {
      if (string.IsNullOrWhiteSpace(identity))
        return null;
      string key = identity.Trim();
      _entries.TryGetValue(key, out var entry);
      return entry;
    }

    /// <summary>
    /// Обновить или создать запись.
    /// newHash — текущий хэш; у <b>новой</b> записи previousHash остаётся пустым:
    /// пустой previousHash означает «карточка ещё ни разу не выгружалась в 1С»,
    /// поэтому первая же выгрузка отдаёт её в 1C_update. Иначе новая позиция
    /// не попала бы в обмен никогда (выгружаются только расхождения).
    /// trackedValues — снимок значений отслеживаемых свойств (для CSV-экспорта).
    /// docType — фактический тип документа (<see cref="VelumAssemblyBomMirrorEntry.DocTypeAssembly"/>
    /// или <see cref="VelumAssemblyBomMirrorEntry.DocTypePart"/>); пустое значение означает
    /// «неизвестно» и не затирает уже сохранённый тип.
    /// </summary>
    public void Upsert(
        string identity,
        string filePath,
        string configurationName,
        string externalId,
        string designation,
        string name,
        int quantity,
        string newHash,
        Dictionary<string, string> trackedValues = null,
        string docType = null)
    {
      string key = (identity ?? string.Empty).Trim();
      if (string.IsNullOrEmpty(key))
        return;

      string normalizedDocType = (docType ?? string.Empty).Trim();

      VelumAssemblyBomMirrorEntry entry;
      if (!_entries.TryGetValue(key, out entry))
      {
        entry = new VelumAssemblyBomMirrorEntry
        {
          Identity = key,
          FilePath = (filePath ?? string.Empty).Trim(),
          ConfigurationName = (configurationName ?? string.Empty).Trim(),
          ExternalId = (externalId ?? string.Empty).Trim(),
          Designation = (designation ?? string.Empty).Trim(),
          Name = (name ?? string.Empty).Trim(),
          DocType = normalizedDocType,
          Quantity = quantity,
          CurrentHash = newHash,
          PreviousHash = string.Empty,
          LastSeenUtc = DateTime.UtcNow
        };
        _entries[key] = entry;
      }
      else
      {
        entry.CurrentHash = newHash;
        entry.ExternalId = (externalId ?? string.Empty).Trim();
        entry.Designation = (designation ?? string.Empty).Trim();
        entry.Name = (name ?? string.Empty).Trim();
        entry.Quantity = quantity;
        if (!string.IsNullOrEmpty(normalizedDocType))
          entry.DocType = normalizedDocType;
        // Файл снова подтверждён — устаревшая пометка снимается.
        entry.Stale = false;
        entry.LastSeenUtc = DateTime.UtcNow;
      }

      if (trackedValues != null)
      {
        entry.TrackedValues = new Dictionary<string, string>(
            trackedValues, StringComparer.OrdinalIgnoreCase);
      }

      _dirty = true;
    }

    /// <summary>
    /// Обновить ExternalId, Designation, Name для существующей записи.
    /// </summary>
    public void UpdateMetadata(string identity, string externalId, string designation, string name)
    {
      string key = (identity ?? string.Empty).Trim();
      if (string.IsNullOrEmpty(key))
        return;

      if (_entries.TryGetValue(key, out var entry))
      {
        entry.ExternalId = (externalId ?? string.Empty).Trim();
        entry.Designation = (designation ?? string.Empty).Trim();
        entry.Name = (name ?? string.Empty).Trim();
        _dirty = true;
      }
    }

    /// <summary>Обновить previousHash после успешного экспорта CSV.</summary>
    public void UpdatePreviousHash(string identity, string currentHash)
    {
      string key = (identity ?? string.Empty).Trim();
      if (string.IsNullOrEmpty(key))
        return;

      if (_entries.TryGetValue(key, out var entry))
      {
        entry.PreviousHash = currentHash;
        _dirty = true;
      }
    }

    /// <summary>Все записи хранилища (стабильный порядок по Identity).</summary>
    public IReadOnlyList<VelumAssemblyBomMirrorEntry> GetAllEntries()
    {
      var list = new List<VelumAssemblyBomMirrorEntry>(_entries.Count);
      foreach (var kv in _entries)
        list.Add(kv.Value);
      list.Sort((a, b) => string.Compare(a.Identity, b.Identity, StringComparison.Ordinal));
      return list;
    }

    /// <summary>
    /// Проверить, есть ли расхождение для записи.
    /// Пустой previousHash при непустом currentHash тоже считается расхождением —
    /// это новая карточка, которую ещё ни разу не отдавали в 1С.
    /// </summary>
    public bool HasDiscrepancy(string identity)
    {
      string key = (identity ?? string.Empty).Trim();
      if (string.IsNullOrEmpty(key))
        return false;

      if (!_entries.TryGetValue(key, out var entry))
        return false;

      return IsDiscrepant(entry);
    }

    /// <summary>
    /// Есть ли у записи неотправленное расхождение: хэш изменился либо карточка
    /// ещё не выгружалась (пустой previousHash).
    /// </summary>
    internal static bool IsDiscrepant(VelumAssemblyBomMirrorEntry entry)
    {
      if (entry == null || string.IsNullOrEmpty(entry.CurrentHash))
        return false;

      return !string.Equals(entry.CurrentHash, entry.PreviousHash, StringComparison.Ordinal);
    }

    /// <summary>
    /// Запись устарела: файл исчез с диска при последней диагностике зеркала
    /// (FileExists=false в результатах диагностики) и с тех пор не появлялся.
    /// Такие записи не участвуют в выгрузке в 1С (см. <see cref="GetDiscrepancyEntries"/>).
    /// </summary>
    /// <param name="entry">Запись зеркала.</param>
    /// <returns>true, если запись помечена устаревшей.</returns>
    internal static bool IsStale(VelumAssemblyBomMirrorEntry entry)
    {
      if (entry == null)
        return false;

      // Пометка ставится только при явной диагностике (FilePath исчез).
      return entry.Stale;
    }

    /// <summary>
    /// Получить все записи с расхождением хэшей.
    /// Записи с пустым ExternalId исключены — они не участвуют в обмене с 1С.
    /// Устаревшие записи (<see cref="IsStale"/>) тоже исключены — их файл исчез,
    /// и выгрузка в 1С отложена до появления файла вновь.
    /// </summary>
    public IReadOnlyList<VelumAssemblyBomMirrorEntry> GetDiscrepancyEntries()
    {
      var result = new List<VelumAssemblyBomMirrorEntry>();
      foreach (var entry in _entries.Values)
      {
        if (string.IsNullOrEmpty(entry.ExternalId))
          continue;
        if (IsStale(entry))
          continue;
        if (IsDiscrepant(entry))
          result.Add(entry);
      }
      result.Sort((a, b) => string.Compare(a.Identity, b.Identity, StringComparison.Ordinal));
      return result;
    }

    /// <summary>Сбросить все previousHash = currentHash.</summary>
    public void ResetAllPreviousHashes()
    {
      bool anyChanged = false;
      foreach (var entry in _entries.Values)
      {
        if (!string.IsNullOrEmpty(entry.CurrentHash))
        {
          entry.PreviousHash = entry.CurrentHash;
          anyChanged = true;
        }
      }
      if (anyChanged)
        _dirty = true;
    }

    /// <summary>Удалить запись по ключу.</summary>
    public void Remove(string identity)
    {
      string key = (identity ?? string.Empty).Trim();
      if (string.IsNullOrEmpty(key))
        return;

      if (_entries.ContainsKey(key))
      {
        _entries.Remove(key);
        _dirty = true;
      }
    }

    /// <summary>
    /// Всего записей в зеркале (для статус-строки формы экспорта, без обращения к диску).
    /// </summary>
    public int CountAllEntries()
    {
      return _entries.Count;
    }

    /// <summary>
    /// Число записей с невыгруженным расхождением (без учёта Stale — они исключены
    /// из выгрузки; счётчик показывает, сколько карточек реально уедет в 1C_update).
    /// </summary>
    public int CountDiscrepancyEntries()
    {
      int count = 0;
      foreach (var entry in _entries.Values)
      {
        if (string.IsNullOrEmpty(entry.ExternalId))
          continue;
        if (IsStale(entry))
          continue;
        if (IsDiscrepant(entry))
          count++;
      }
      return count;
    }

    /// <summary>
    /// Число устаревших записей (файл исчез, <see cref="VelumAssemblyBomMirrorEntry.Stale"/>).
    /// </summary>
    public int CountStaleEntries()
    {
      int count = 0;
      foreach (var entry in _entries.Values)
      {
        if (IsStale(entry))
          count++;
      }
      return count;
    }

    /// <summary>
    /// Одноразово привести хэши записей к текущему формату
    /// (<see cref="VelumAssemblyBomTrackedPropertiesConfig.HashFormatVersion"/>).
    /// <para>
    /// Нужен потому, что при смене формата <c>PreviousHash</c> из старого файла
    /// никогда не совпадёт с <c>CurrentHash</c>, посчитанным по новому правилу, —
    /// и каждая позиция дала бы ложное «расхождение карточки» с массовой
    /// выгрузкой в <c>1C_update</c>.
    /// </para>
    /// <para>
    /// <c>CurrentHash</c> пересчитывается из сохранённого снимка
    /// <c>TrackedValues</c> — те же значения и та же нормализация, что и при
    /// живом подсчёте, поэтому обращение к SOLIDWORKS не нужно.
    /// </para>
    /// <para>
    /// <c>PreviousHash</c> в новом формате восстановить неоткуда (в снимке лежит
    /// только текущее состояние), поэтому он сравнивается со <b>старым</b>
    /// <c>CurrentHash</c> до пересчёта: расхождения не было → приравниваем
    /// previous к новому хэшу; расхождение было → обнуляем previous, то есть
    /// неотправленное изменение сохраняется и уедет в 1С как обычно.
    /// </para>
    /// </summary>
    private void MigrateHashFormatIfNeeded()
    {
      int current = VelumAssemblyBomTrackedPropertiesConfig.HashFormatVersion;
      if (_hashFormatVersion == current)
        return;

      if (_hashFormatVersion > current)
      {
        // Файл из более новой версии (откат плагина) — хэши уже нового формата,
        // пересчёт только испортит снимок. Приводим записанный номер и уходим.
        Logger.Warning(
            "Velum bomMirrorStore: hash format version " + _hashFormatVersion +
            " is newer than " + current + ", hashes kept as is");
        _hashFormatVersion = current;
        _dirty = true;
        return;
      }

      int fromVersion = _hashFormatVersion;

      int migrated = 0;
      int keptDiscrepancy = 0;
      foreach (var entry in _entries.Values)
      {
        if (entry == null || string.IsNullOrEmpty(entry.CurrentHash))
          continue;

        bool hadDiscrepancy = !string.Equals(
            entry.CurrentHash, entry.PreviousHash, StringComparison.Ordinal);

        string newHash = VelumAssemblyBomTrackedPropertiesConfig
            .ComputeHashFromTrackedValues(entry.TrackedValues);

        if (string.IsNullOrEmpty(newHash))
        {
          // Снимка свойств нет (запись старше появления TrackedValues) —
          // пересчитать нечем. Прежний хэш может быть в старом формате, поэтому
          // считаем карточку не выгруженной: реальное расхождение потеряно не будет,
          // а ложное появится максимум один раз и снимется после первой выгрузки.
          entry.PreviousHash = string.Empty;
          keptDiscrepancy++;
          continue;
        }

        entry.CurrentHash = newHash;
        entry.PreviousHash = hadDiscrepancy ? string.Empty : newHash;
        migrated++;

        if (hadDiscrepancy)
          keptDiscrepancy++;
      }

      _hashFormatVersion = current;
      _dirty = true;

      Logger.Info(
          "Velum bomMirrorStore: hash format migrated from version " + fromVersion +
          " to " + current + " entries=" + _entries.Count +
          " recomputed=" + migrated + " discrepanciesKept=" + keptDiscrepancy);
    }

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

  /// <summary>Обёртка JSON-файла bomMirror.json.</summary>
  internal sealed class BomMirrorWrapper
  {
    /// <summary>
    /// Версия формата хэша записей. Отсутствует (0) в файлах, созданных до
    /// появления поля, — это формат 1 («свойства + Quantity»).
    /// </summary>
    [JsonProperty("hashFormatVersion")]
    public int HashFormatVersion { get; set; }

    [JsonProperty("entries")]
    public VelumAssemblyBomMirrorEntry[] Entries { get; set; }
  }
}
