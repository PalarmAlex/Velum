using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ISIDA.Common;
using Newtonsoft.Json;
using Velum.Configuration;

namespace Velum.UI.ProductRegistry
{
  /// <summary>Правило автоимени каталога по расширению файла.</summary>
  internal sealed class VelumProductFolderAutoNameMapping
  {
    public string Extension { get; set; }

    public string FolderName { get; set; }
  }

  /// <summary>Контейнер JSON с правилами автоимён каталогов.</summary>
  internal sealed class VelumProductFolderAutoNameFile
  {
    public VelumProductFolderAutoNameMapping[] Mappings { get; set; } =
        Array.Empty<VelumProductFolderAutoNameMapping>();
  }

  /// <summary>
  /// Настройки автосоздания субкаталогов при индексации:
  /// <c>folderAutoNames.json</c> в каталоге <see cref="VelumAppConfig.ProductRegistryFolderPath"/>.
  /// </summary>
  internal static class VelumProductRegistryFolderAutoNames
  {
    private const string OtherFolderName = "Прочее"; // Удерживаем для обратной совместимости, но больше не используется

    private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
    {
      Formatting = Formatting.Indented,
      NullValueHandling = NullValueHandling.Ignore
    };

    public static string FilePath =>
        Path.Combine(VelumAppConfig.ProductRegistryFolderPath, "folderAutoNames.json");

    public static List<VelumProductFolderAutoNameMapping> CreateDefaultMappings()
    {
      var result = new List<VelumProductFolderAutoNameMapping>();
      AddMany(result, "Сборки", ".sldasm");
      AddMany(result, "Детали", ".sldprt");
      AddMany(result, "Чертежи", ".slddrw");
      AddMany(result, "DXF", ".dxf");
      AddMany(result, "PDF", ".pdf");
      AddMany(result,
          "Картинки",
          ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp", ".ico", ".svg", ".heic");
      AddMany(result,
          "Тексты",
          ".txt", ".log", ".csv", ".tsv", ".xml", ".json", ".md", ".rtf", ".ini", ".cfg", ".conf",
          ".doc", ".docx", ".xls", ".xlsx", ".xlsm", ".ppt", ".pptx", ".odt", ".ods", ".odp");
      AddMany(result,
          "Видео",
          ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".webm", ".mpeg", ".mpg", ".flv", ".m4v", ".3gp");
      AddMany(result, "Плазма", ".nif");
      AddMany(result, "Лазер", ".lxds");
      return result;
    }

    public static List<VelumProductFolderAutoNameMapping> LoadOrCreate()
    {
      Directory.CreateDirectory(Path.GetDirectoryName(FilePath) ?? VelumProductRegistryStore.RegistryFolderPath);

      VelumProductFolderAutoNameFile file = ReadJson(FilePath);
      if (file == null || file.Mappings == null || file.Mappings.Length == 0)
      {
        List<VelumProductFolderAutoNameMapping> defaults = CreateDefaultMappings();
        Save(defaults);
        return defaults;
      }

      var result = new List<VelumProductFolderAutoNameMapping>(file.Mappings.Length);
      foreach (VelumProductFolderAutoNameMapping mapping in file.Mappings)
      {
        if (mapping == null)
          continue;
        string extension = NormalizeExtension(mapping.Extension);
        string folderName = (mapping.FolderName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(extension) || string.IsNullOrEmpty(folderName))
          continue;
        result.Add(new VelumProductFolderAutoNameMapping
        {
          Extension = extension,
          FolderName = folderName
        });
      }

      if (result.Count == 0)
      {
        result = CreateDefaultMappings();
        Save(result);
      }

      return result;
    }

    public static void Save(IEnumerable<VelumProductFolderAutoNameMapping> mappings)
    {
      Directory.CreateDirectory(Path.GetDirectoryName(FilePath) ?? VelumProductRegistryStore.RegistryFolderPath);

      var normalized = new List<VelumProductFolderAutoNameMapping>();
      var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      if (mappings != null)
      {
        foreach (VelumProductFolderAutoNameMapping mapping in mappings)
        {
          if (mapping == null)
            continue;
          string extension = NormalizeExtension(mapping.Extension);
          string folderName = (mapping.FolderName ?? string.Empty).Trim();
          if (string.IsNullOrEmpty(extension) || string.IsNullOrEmpty(folderName))
            continue;
          if (!seen.Add(extension))
            continue;
          normalized.Add(new VelumProductFolderAutoNameMapping
          {
            Extension = extension,
            FolderName = folderName
          });
        }
      }

      var file = new VelumProductFolderAutoNameFile
      {
        Mappings = normalized.ToArray()
      };
      WriteJsonAtomic(FilePath, file);
    }

    public static Dictionary<string, string> BuildExtensionMap(IEnumerable<VelumProductFolderAutoNameMapping> mappings)
    {
      var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      if (mappings == null)
        return map;

      foreach (VelumProductFolderAutoNameMapping mapping in mappings)
      {
        if (mapping == null)
          continue;
        string extension = NormalizeExtension(mapping.Extension);
        string folderName = (mapping.FolderName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(extension) || string.IsNullOrEmpty(folderName))
          continue;
        map[extension] = folderName;
      }

      return map;
    }

    public static List<string> BuildFolderOrder(IEnumerable<VelumProductFolderAutoNameMapping> mappings)
    {
      var order = new List<string>();
      var seen = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
      if (mappings != null)
      {
        foreach (VelumProductFolderAutoNameMapping mapping in mappings)
        {
          if (mapping == null)
            continue;
          string folderName = (mapping.FolderName ?? string.Empty).Trim();
          if (string.IsNullOrEmpty(folderName))
            continue;
          if (seen.Add(folderName))
            order.Add(folderName);
        }
      }

      // Каталог «Прочее» больше не создаём — файлы без зарегистрированных расширений игнорируются.
      return order;
    }

    public static string ClassifyFileFolder(string filePath, IDictionary<string, string> extensionMap)
    {
      string ext = NormalizeExtension(Path.GetExtension(filePath));
      string folderName;
      if (!string.IsNullOrEmpty(ext)
          && extensionMap != null
          && extensionMap.TryGetValue(ext, out folderName)
          && !string.IsNullOrWhiteSpace(folderName))
        return folderName.Trim();
      // Файлы без зарегистрированных расширений больше не попадают в «Прочее» — возвращаем пустую строку.
      return string.Empty;
    }

    /// <summary>
    /// Если имя каталога совпадает с автоименем из folderAutoNames,
    /// возвращает каноническое имя категории; иначе null.
    /// </summary>
    public static string TryMatchReservedFolderName(
        string folderName,
        IEnumerable<VelumProductFolderAutoNameMapping> mappings)
    {
      string name = (folderName ?? string.Empty).Trim();
      if (string.IsNullOrEmpty(name))
        return null;

      foreach (string reserved in BuildFolderOrder(mappings))
      {
        if (string.Equals(reserved, name, StringComparison.CurrentCultureIgnoreCase))
          return reserved;
      }

      return null;
    }

    public static string NormalizeExtension(string extension)
    {
      string value = (extension ?? string.Empty).Trim();
      if (string.IsNullOrEmpty(value))
        return string.Empty;
      if (value[0] != '.')
        value = "." + value;
      return value.ToLowerInvariant();
    }

    private static void AddMany(
        List<VelumProductFolderAutoNameMapping> target,
        string folderName,
        params string[] extensions)
    {
      foreach (string extension in extensions)
      {
        target.Add(new VelumProductFolderAutoNameMapping
        {
          Extension = NormalizeExtension(extension),
          FolderName = folderName
        });
      }
    }

    private static VelumProductFolderAutoNameFile ReadJson(string path)
    {
      try
      {
        if (!File.Exists(path))
          return null;
        string json = File.ReadAllText(path, Encoding.UTF8);
        if (string.IsNullOrWhiteSpace(json))
          return null;
        return JsonConvert.DeserializeObject<VelumProductFolderAutoNameFile>(json, JsonSettings);
      }
      catch (Exception ex)
      {
        Logger.Error("ProductRegistry folderAutoNames load failed (" + path + "): " + ex.Message);
        return null;
      }
    }

    private static void WriteJsonAtomic(string path, object value)
    {
      string json = JsonConvert.SerializeObject(value, JsonSettings);
      string tempPath = path + ".tmp";
      File.WriteAllText(tempPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
      if (File.Exists(path))
        File.Replace(tempPath, path, null);
      else
        File.Move(tempPath, path);
    }
  }
}
