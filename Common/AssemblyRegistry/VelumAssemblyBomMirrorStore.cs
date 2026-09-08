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
        if (wrapper?.Entries != null)
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
            entry.CurrentHash = (entry.CurrentHash ?? string.Empty).Trim();
            entry.PreviousHash = (entry.PreviousHash ?? string.Empty).Trim();
            _entries[entry.Identity] = entry;
          }
        }
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
    /// newHash — текущий хэш; если запись новая, previousHash = currentHash.
    /// trackedValues — снимок значений отслеживаемых свойств (для CSV-экспорта).
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
        Dictionary<string, string> trackedValues = null)
    {
      string key = (identity ?? string.Empty).Trim();
      if (string.IsNullOrEmpty(key))
        return;

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
          Quantity = quantity,
          CurrentHash = newHash,
          PreviousHash = newHash
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

    /// <summary>Проверить, есть ли расхождение для записи.</summary>
    public bool HasDiscrepancy(string identity)
    {
      string key = (identity ?? string.Empty).Trim();
      if (string.IsNullOrEmpty(key))
        return false;

      if (!_entries.TryGetValue(key, out var entry))
        return false;

      if (string.IsNullOrEmpty(entry.CurrentHash) || string.IsNullOrEmpty(entry.PreviousHash))
        return false;

      return !string.Equals(entry.CurrentHash, entry.PreviousHash, StringComparison.Ordinal);
    }

    /// <summary>
    /// Получить все записи с расхождением хэшей.
    /// Записи с пустым ExternalId исключены — они не участвуют в обмене с 1С.
    /// </summary>
    public IReadOnlyList<VelumAssemblyBomMirrorEntry> GetDiscrepancyEntries()
    {
      var result = new List<VelumAssemblyBomMirrorEntry>();
      foreach (var entry in _entries.Values)
      {
        if (string.IsNullOrEmpty(entry.CurrentHash) || string.IsNullOrEmpty(entry.PreviousHash))
          continue;
        if (string.IsNullOrEmpty(entry.ExternalId))
          continue;
        if (!string.Equals(entry.CurrentHash, entry.PreviousHash, StringComparison.Ordinal))
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
    [JsonProperty("entries")]
    public VelumAssemblyBomMirrorEntry[] Entries { get; set; }
  }
}
