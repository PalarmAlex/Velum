using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Velum.Configuration;
using Velum.ReactiveCore.Export;

namespace Velum.ReactiveCore.DocumentTemplates
{
  /// <summary>
  /// Хранилище реестра экземпляров документов: <c>instances.json</c>
  /// в <see cref="VelumAppConfig.DocumentsRegistryFolderPath"/>.
  /// По образцу <c>VelumProductRegistryStore</c>.
  /// </summary>
  /// <remarks>
  /// Ключ экземпляра — <c>doc.key</c> (path|type|config).
  /// </remarks>
  internal sealed class VelumDocumentInstanceRegistry
  {
    private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
    {
      Formatting = Formatting.Indented,
      NullValueHandling = NullValueHandling.Ignore
    };

    private readonly Dictionary<string, VelumDocumentInstanceData> _instances =
        new Dictionary<string, VelumDocumentInstanceData>(StringComparer.OrdinalIgnoreCase);
    private bool _dirty;

    /// <summary>Путь к файлу реестра экземпляров.</summary>
    public static string RegistryFilePath => Path.Combine(
        VelumAppConfig.DocumentsRegistryFolderPath, "instances.json");

    /// <summary>
    /// Загружает реестр из файла.
    /// </summary>
    public void Load()
    {
      VelumPathExists.TryCreateDirectory(VelumAppConfig.DocumentsRegistryFolderPath);

      ReadTextResult read = VelumPathExists.ReadAllTextWithStatus(RegistryFilePath);

      if (read.Outcome == ReadTextOutcome.Failed)
        throw new IOException(
            "Не удалось прочитать реестр экземпляров документов (" + RegistryFilePath + "): " +
            "сетевой каталог недоступен или чтение не уложилось в таймаут.");

      if (read.Success && string.IsNullOrWhiteSpace(read.Content))
        throw new IOException(
            "instances.json прочитан пустым (файл обрезан или повреждён). Реестр в памяти не обновлён.");

      if (read.Success)
      {
        try
        {
          var file = JsonConvert.DeserializeObject<InstancesFile>(read.Content, JsonSettings);
          if (file != null && file.Instances != null)
          {
            foreach (VelumDocumentInstanceData inst in file.Instances)
            {
              if (inst == null || string.IsNullOrWhiteSpace(inst.DocKey))
                continue;
              _instances[inst.DocKey] = inst;
            }
          }
        }
        catch (Exception ex)
        {
          throw new IOException(
              "Не удалось разобрать " + RegistryFilePath + ": " + ex.Message, ex);
        }
      }
    }

    /// <summary>
    /// Сохраняет реестр в файл (атомарно через temp).
    /// </summary>
    public void Save()
    {
      if (!_dirty)
        return;

      VelumPathExists.TryCreateDirectory(VelumAppConfig.DocumentsRegistryFolderPath);

      var file = new InstancesFile
      {
        Instances = _instances.Values.OrderBy(i => i.DocKey).ToArray()
      };
      string json = JsonConvert.SerializeObject(file, JsonSettings);
      string tempPath = RegistryFilePath + ".tmp";

      if (!VelumPathExists.WriteAllTextWithTimeout(tempPath, json))
        throw new IOException(
            "Не удалось записать временный файл реестра: " + tempPath);

      // Проверка записи.
      ReadTextResult verify = VelumPathExists.ReadAllTextWithStatus(tempPath);
      if (verify.Outcome != ReadTextOutcome.Success ||
          !string.Equals(verify.Content, json, StringComparison.Ordinal))
      {
        throw new IOException(
            "Временный файл реестра записан неполностью: " + tempPath + ". Живой файл не изменён.");
      }

      if (VelumPathExists.FileExists(RegistryFilePath))
        File.Replace(tempPath, RegistryFilePath, null);
      else
        File.Move(tempPath, RegistryFilePath);

      _dirty = false;
    }

    /// <summary>
    /// Найти экземпляр по <c>doc.key</c>.
    /// </summary>
    /// <param name="docKey">Ключ документа.</param>
    /// <returns>Запись экземпляра либо null.</returns>
    public VelumDocumentInstanceData FindByDocKey(string docKey)
    {
      if (string.IsNullOrWhiteSpace(docKey))
        return null;

      return _instances.TryGetValue(docKey.Trim(), out VelumDocumentInstanceData result)
          ? result : null;
    }

    /// <summary>
    /// Получить или создать экземпляр.
    /// </summary>
    /// <param name="docKey">Ключ документа.</param>
    /// <param name="templateId">ID шаблона.</param>
    /// <returns>Запись экземпляра.</returns>
    public VelumDocumentInstanceData GetOrCreate(string docKey, string templateId)
    {
      string key = (docKey ?? string.Empty).Trim();
      if (key.Length == 0)
        return null;

      VelumDocumentInstanceData inst;
      if (!_instances.TryGetValue(key, out inst))
      {
        inst = new VelumDocumentInstanceData
        {
          DocKey = key,
          TemplateId = templateId ?? string.Empty,
          CreatedAt = DateTime.UtcNow.ToString("o"),
          LastModifiedAt = DateTime.UtcNow.ToString("o"),
          Values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };
        _instances[key] = inst;
        _dirty = true;
      }

      return inst;
    }

    /// <summary>
    /// Обновить значения параметра в экземпляре.
    /// </summary>
    /// <param name="docKey">Ключ документа.</param>
    /// <param name="paramId">ID параметра.</param>
    /// <param name="value">Значение.</param>
    /// <param name="persist">Сохранить на диск.</param>
    public void UpdateParameterValue(string docKey, string paramId, string value, bool persist = true)
    {
      string key = (docKey ?? string.Empty).Trim();
      if (key.Length == 0)
        return;

      VelumDocumentInstanceData inst;
      if (!_instances.TryGetValue(key, out inst))
        return;

      inst.LastModifiedAt = DateTime.UtcNow.ToString("o");
      if (inst.Values == null)
        inst.Values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

      string id = (paramId ?? string.Empty).Trim();
      if (id.Length > 0)
        inst.Values[id] = value ?? string.Empty;

      if (persist)
        Save();
    }

    /// <summary>
    /// Удалить экземпляр по <c>doc.key</c>.
    /// </summary>
    public void Remove(string docKey, bool persist = true)
    {
      string key = (docKey ?? string.Empty).Trim();
      if (key.Length == 0)
        return;

      if (_instances.Remove(key))
      {
        _dirty = true;
        if (persist)
          Save();
      }
    }

    /// <summary>Все экземпляры.</summary>
    public IReadOnlyList<VelumDocumentInstanceData> GetAll()
    {
      return _instances.Values.ToList();
    }

    /// <summary>
    /// Проверить, существует ли экземпляр.
    /// </summary>
    public bool Contains(string docKey)
    {
      return !string.IsNullOrWhiteSpace(docKey) && _instances.ContainsKey(docKey.Trim());
    }

    /// <summary>Сбросить кэш в памяти (не на диске).</summary>
    public void Clear()
    {
      _instances.Clear();
      _dirty = true;
    }
  }

  /// <summary>
  /// Данные экземпляра: doc.key → шаблон, значения параметров, время.
  /// </summary>
  internal sealed class VelumDocumentInstanceData
  {
    /// <summary>Ключ документа (doc.key).</summary>
    public string DocKey { get; set; }

    /// <summary>ID шаблона, из которого создан документ.</summary>
    public string TemplateId { get; set; }

    /// <summary>Время создания (UTC, ISO 8601).</summary>
    public string CreatedAt { get; set; }

    /// <summary>Время последнего изменения (UTC, ISO 8601).</summary>
    public string LastModifiedAt { get; set; }

    /// <summary>Значения параметров: id → value.</summary>
    public Dictionary<string, string> Values { get; set; }
  }

  /// <summary>
  /// Сериализуемый файл реестра экземпляров.
  /// </summary>
  internal sealed class InstancesFile
  {
    /// <summary>Список экземпляров.</summary>
    public VelumDocumentInstanceData[] Instances { get; set; }
  }
}
