using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ISIDA.Common;
using Newtonsoft.Json;
using Velum.Configuration;

namespace Velum.UI.AssemblyRegistry
{
  /// <summary>
  /// Одна строка состава: дочерний компонент и его количество в родителе.
  /// </summary>
  internal sealed class VelumBomStructureLine
  {
    /// <summary>Identity дочернего компонента (FilePath|Config).</summary>
    public string ChildIdentity { get; set; }

    /// <summary>
    /// ExternalId дочернего компонента — снимок на момент зеркалирования
    /// (для хэша и отображения в UI; при выгрузке в CSV берётся актуальный
    /// из bomMirror.json). Может быть пустым — строка сохраняется,
    /// но при выгрузке пропускается.
    /// </summary>
    public string ChildExternalId { get; set; }

    /// <summary>Обозначение дочернего компонента (информационно).</summary>
    public string ChildDesignation { get; set; }

    /// <summary>Наименование дочернего компонента (информационно).</summary>
    public string ChildName { get; set; }

    /// <summary>Количество вхождений в родителя.</summary>
    public int Quantity { get; set; }
  }

  /// <summary>
  /// Структура состава одной сборки-родителя (только прямые дети,
  /// без рекурсии во вложенные сборки).
  /// </summary>
  internal sealed class VelumBomStructureEntry
  {
    /// <summary>Identity родителя (FilePath|Config).</summary>
    public string ParentIdentity { get; set; }

    /// <summary>ExternalId родителя (связь с 1С).</summary>
    public string ParentExternalId { get; set; }

    /// <summary>Конфигурация родителя (активная при зеркалировании).</summary>
    public string ParentConfiguration { get; set; }

    /// <summary>Обозначение родителя (информационно).</summary>
    public string ParentDesignation { get; set; }

    /// <summary>Наименование родителя (информационно).</summary>
    public string ParentName { get; set; }

    /// <summary>Хэш текущего состояния структуры (см. <see cref="VelumBomStructureStore.ComputeStructureHash"/>).</summary>
    public string CurrentHash { get; set; }

    /// <summary>Хэш состояния, по которому последний раз успешно выгружали в 1С.</summary>
    public string PreviousHash { get; set; }

    /// <summary>Строки состава (прямые дети родителя).</summary>
    public List<VelumBomStructureLine> Lines { get; set; } =
        new List<VelumBomStructureLine>();

    /// <summary>Дата последнего зеркалирования (ISO 8601, UTC).</summary>
    public DateTime LastMirroredUtc { get; set; }
  }

  /// <summary>Корень JSON-файла <c>bomStructure.json</c>.</summary>
  internal sealed class VelumBomStructureFile
  {
    /// <summary>Структуры всех зеркалируемых сборок-родителей.</summary>
    public List<VelumBomStructureEntry> Structures { get; set; } =
        new List<VelumBomStructureEntry>();
  }

  /// <summary>
  /// JSON-хранилище структур состава сборок (<c>bomStructure.json</c>)
  /// в каталоге AssemblyRegistry. Ключ — Identity родителя (FilePath|Config).
  /// По образцу <see cref="VelumAssemblyBomMirrorStore"/>.
  /// </summary>
  internal sealed class VelumBomStructureStore
  {
    /// <summary>Настройки сериализации JSON.</summary>
    private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
    {
      Formatting = Formatting.Indented,
      NullValueHandling = NullValueHandling.Ignore
    };

    private readonly Dictionary<string, VelumBomStructureEntry> _entries =
        new Dictionary<string, VelumBomStructureEntry>(StringComparer.OrdinalIgnoreCase);

    private bool _dirty;

    /// <summary>Путь к файлу хранения.</summary>
    public static string StructureFilePath => Path.Combine(
        VelumAppConfig.AssemblyRegistryFolderPath,
        "bomStructure.json");

    /// <summary>Загрузка из JSON.</summary>
    public void Load()
    {
      Directory.CreateDirectory(VelumAppConfig.AssemblyRegistryFolderPath);

      _entries.Clear();
      _dirty = false;

      string json = ReadJsonFile(StructureFilePath);
      if (string.IsNullOrWhiteSpace(json))
        return;

      try
      {
        var wrapper = JsonConvert.DeserializeObject<VelumBomStructureFile>(json, JsonSettings);
        if (wrapper?.Structures != null)
        {
          foreach (VelumBomStructureEntry entry in wrapper.Structures)
          {
            if (entry == null || string.IsNullOrWhiteSpace(entry.ParentIdentity))
              continue;
            entry.ParentIdentity = entry.ParentIdentity.Trim();
            entry.ParentExternalId = (entry.ParentExternalId ?? string.Empty).Trim();
            entry.ParentConfiguration = (entry.ParentConfiguration ?? string.Empty).Trim();
            entry.ParentDesignation = (entry.ParentDesignation ?? string.Empty).Trim();
            entry.ParentName = (entry.ParentName ?? string.Empty).Trim();
            entry.CurrentHash = (entry.CurrentHash ?? string.Empty).Trim();
            entry.PreviousHash = (entry.PreviousHash ?? string.Empty).Trim();
            if (entry.Lines == null)
              entry.Lines = new List<VelumBomStructureLine>();
            foreach (VelumBomStructureLine line in entry.Lines)
            {
              if (line == null)
                continue;
              line.ChildIdentity = (line.ChildIdentity ?? string.Empty).Trim();
              line.ChildExternalId = (line.ChildExternalId ?? string.Empty).Trim();
              line.ChildDesignation = (line.ChildDesignation ?? string.Empty).Trim();
              line.ChildName = (line.ChildName ?? string.Empty).Trim();
            }
            _entries[entry.ParentIdentity] = entry;
          }
        }
      }
      catch (Exception ex)
      {
        Logger.Error("Velum bomStructureStore load failed (" + StructureFilePath + "): " + ex.Message);
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
        var wrapper = new VelumBomStructureFile
        {
          Structures = _entries.Values.OrderBy(e => e.ParentIdentity).ToList()
        };
        string json = JsonConvert.SerializeObject(wrapper, JsonSettings);
        WriteJsonAtomic(StructureFilePath, json);
        _dirty = false;
      }
      catch (Exception ex)
      {
        Logger.Error("Velum bomStructureStore save failed: " + ex.Message);
      }
    }

    /// <summary>Получить запись по Identity родителя.</summary>
    /// <param name="parentIdentity">Identity родителя (FilePath|Config).</param>
    /// <returns>Запись структуры или <c>null</c>, если нет.</returns>
    public VelumBomStructureEntry GetEntry(string parentIdentity)
    {
      if (string.IsNullOrWhiteSpace(parentIdentity))
        return null;
      string key = parentIdentity.Trim();
      _entries.TryGetValue(key, out VelumBomStructureEntry entry);
      return entry;
    }

    /// <summary>
    /// Обновить или создать запись структуры.
    /// Для существующей записи сохраняется её <c>PreviousHash</c>;
    /// для новой записи <c>PreviousHash</c> пуст (первое зеркалирование — расхождение,
    /// чтобы первая выгрузка отдала полный состав).
    /// </summary>
    /// <param name="entry">Запись структуры (ключ — ParentIdentity).</param>
    public void Upsert(VelumBomStructureEntry entry)
    {
      if (entry == null || string.IsNullOrWhiteSpace(entry.ParentIdentity))
        return;

      string key = entry.ParentIdentity.Trim();
      entry.ParentIdentity = key;

      if (_entries.TryGetValue(key, out VelumBomStructureEntry existing))
        entry.PreviousHash = (existing.PreviousHash ?? string.Empty).Trim();
      else
        entry.PreviousHash = string.Empty;

      _entries[key] = entry;
      _dirty = true;
    }

    /// <summary>
    /// Получить все записи с расхождением хэшей.
    /// Записи с пустым ParentExternalId исключены — они не участвуют в обмене с 1С.
    /// </summary>
    public IReadOnlyList<VelumBomStructureEntry> GetEntriesWithDiscrepancy()
    {
      var result = new List<VelumBomStructureEntry>();
      foreach (VelumBomStructureEntry entry in _entries.Values)
      {
        if (string.IsNullOrEmpty(entry.ParentExternalId))
          continue;
        if (!string.Equals(entry.CurrentHash, entry.PreviousHash, StringComparison.Ordinal))
          result.Add(entry);
      }
      result.Sort((a, b) => string.Compare(a.ParentIdentity, b.ParentIdentity, StringComparison.Ordinal));
      return result;
    }

    /// <summary>Обновить PreviousHash после успешного экспорта CSV.</summary>
    /// <param name="parentIdentity">Identity родителя.</param>
    /// <param name="currentHash">Хэш выгруженного состояния.</param>
    public void UpdatePreviousHash(string parentIdentity, string currentHash)
    {
      string key = (parentIdentity ?? string.Empty).Trim();
      if (string.IsNullOrEmpty(key))
        return;

      if (_entries.TryGetValue(key, out VelumBomStructureEntry entry))
      {
        entry.PreviousHash = currentHash;
        _dirty = true;
      }
    }

    /// <summary>Проверить, есть ли расхождение для записи.</summary>
    /// <param name="parentIdentity">Identity родителя.</param>
    /// <returns>true, если CurrentHash != PreviousHash.</returns>
    public bool HasDiscrepancy(string parentIdentity)
    {
      string key = (parentIdentity ?? string.Empty).Trim();
      if (string.IsNullOrEmpty(key))
        return false;

      if (!_entries.TryGetValue(key, out VelumBomStructureEntry entry))
        return false;

      return !string.Equals(entry.CurrentHash, entry.PreviousHash, StringComparison.Ordinal);
    }

    /// <summary>Все записи хранилища (стабильный порядок по ParentIdentity).</summary>
    public IReadOnlyList<VelumBomStructureEntry> GetAllEntries()
    {
      var list = new List<VelumBomStructureEntry>(_entries.Count);
      foreach (var kv in _entries)
        list.Add(kv.Value);
      list.Sort((a, b) => string.Compare(a.ParentIdentity, b.ParentIdentity, StringComparison.Ordinal));
      return list;
    }

    /// <summary>
    /// Вычислить SHA-256 хэш состояния структуры.
    /// Строка хэша: <c>parentExternalId|childIdentity|childExternalId|Qty</c>
    /// для каждой строки, сортировка по ChildIdentity (OrdinalIgnoreCase),
    /// строки объединяются через «;». Quantity — через InvariantCulture.
    /// Строки с пустым ChildIdentity пропускаются.
    /// Включение ExternalId гарантирует, что смена ExternalId родителя или
    /// ребёнка даёт новый хэш и попадает в выгрузку.
    /// </summary>
    /// <param name="parentExternalId">ExternalId родителя.</param>
    /// <param name="lines">Строки состава.</param>
    /// <returns>Хэш (hex, lowercase) или пустая строка, если строк нет.</returns>
    public static string ComputeStructureHash(
        string parentExternalId,
        IReadOnlyList<VelumBomStructureLine> lines)
    {
      if (lines == null || lines.Count == 0)
        return string.Empty;

      var parts = new List<string>();
      foreach (VelumBomStructureLine line in
          lines.OrderBy(l => (l?.ChildIdentity ?? string.Empty), StringComparer.OrdinalIgnoreCase))
      {
        if (line == null || string.IsNullOrWhiteSpace(line.ChildIdentity))
          continue;

        parts.Add(string.Join("|",
            parentExternalId ?? string.Empty,
            line.ChildIdentity,
            line.ChildExternalId ?? string.Empty,
            line.Quantity.ToString(CultureInfo.InvariantCulture)));
      }

      if (parts.Count == 0)
        return string.Empty;

      return ComputeSha256(string.Join(";", parts));
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

    /// <summary>Вычислить SHA-256 хэш строки (UTF-8 без BOM).</summary>
    private static string ComputeSha256(string text)
    {
      if (string.IsNullOrEmpty(text))
        return string.Empty;

      using (SHA256 sha256 = SHA256.Create())
      {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        byte[] hash = sha256.ComputeHash(bytes);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
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
