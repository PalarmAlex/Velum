using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Velum.Configuration;
using Xarial.XCad.SolidWorks;

namespace Velum.UI
{
  /// <summary>Загрузка баз материалов, подключённых в текущей сессии SolidWorks.</summary>
  internal sealed class VelumMaterialDatabaseManager
  {
    private readonly ISwApplication _swApp;

    internal VelumMaterialDatabaseManager(ISwApplication swApp)
    {
      _swApp = swApp ?? throw new ArgumentNullException(nameof(swApp));
    }

    internal string[] GetMaterialDatabasePaths()
    {
      var result = new List<string>();

      try
      {
        SldWorks sw = _swApp.Sw as SldWorks;
        object raw = sw?.GetMaterialDatabases();
        if (raw is Array databases)
        {
          foreach (object item in databases)
          {
            string path = item?.ToString();
            if (!string.IsNullOrWhiteSpace(path))
            {
              string trimmed = path.Trim();
              if (File.Exists(trimmed))
                result.Add(trimmed);
            }
          }
        }
      }
      catch (Exception ex)
      {
        if (VelumAppConfig.SolidHomeostasisDebugLog)
          Debug.WriteLine("Velum material DB paths error: " + ex.Message);
      }

      if (result.Count == 0)
        result.AddRange(GetUserPreferenceDatabasePaths());

      return result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private IEnumerable<string> GetUserPreferenceDatabasePaths()
    {
      var result = new List<string>();
      try
      {
        SldWorks sw = _swApp.Sw as SldWorks;
        string pathsStr = sw?.GetUserPreferenceStringValue(
            (int)swUserPreferenceStringValue_e.swFileLocationsMaterialDatabases);
        if (string.IsNullOrWhiteSpace(pathsStr))
          return GetDefaultMaterialPaths();

        string[] dirPaths = pathsStr.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < dirPaths.Length; i++)
        {
          string dirPath = (dirPaths[i] ?? string.Empty).Trim();
          if (dirPath.Length == 0 || !Directory.Exists(dirPath))
            continue;

          try
          {
            result.AddRange(Directory.GetFiles(dirPath, "*.sldmat"));
          }
          catch (Exception ex)
          {
            if (VelumAppConfig.SolidHomeostasisDebugLog)
              Debug.WriteLine("Velum material DB dir error: " + ex.Message);
          }
        }
      }
      catch (Exception ex)
      {
        if (VelumAppConfig.SolidHomeostasisDebugLog)
          Debug.WriteLine("Velum material DB preference error: " + ex.Message);
        return GetDefaultMaterialPaths();
      }

      if (result.Count > 0)
        return result.ToArray();

      return GetDefaultMaterialPaths();
    }

    private static string[] GetDefaultMaterialPaths()
    {
      string defaultDir = System.IO.Path.Combine(
          System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles),
          "SOLIDWORKS Corp",
          "SOLIDWORKS",
          "lang",
          "russian",
          "sldmaterials");

      try
      {
        if (Directory.Exists(defaultDir))
          return Directory.GetFiles(defaultDir, "*.sldmat");
      }
      catch
      {
      }

      return Array.Empty<string>();
    }

    internal Dictionary<string, List<string>> LoadMaterialsFromFiles(string specificDatabase = null)
    {
      var materialHierarchy = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
      string[] databasePaths = GetMaterialDatabasePaths();

      for (int i = 0; i < databasePaths.Length; i++)
      {
        string filePath = databasePaths[i];
        string fileName = Path.GetFileNameWithoutExtension(filePath);
        if (string.IsNullOrWhiteSpace(fileName))
          continue;

        if (!string.IsNullOrEmpty(specificDatabase) &&
            !string.Equals(fileName, specificDatabase, StringComparison.OrdinalIgnoreCase))
          continue;

        if (!File.Exists(filePath))
          continue;

        // Разобранный XML переиспользуется между вызовами и экземплярами формы
        // (см. VelumMaterialDatabaseCache): дерево раскрывает/ищет узлы, повторно
        // обращаясь к одной и той же базе, — без кэша файл парсился на каждый узел.
        List<string> entries = VelumMaterialDatabaseCache.GetOrLoad(filePath);
        if (entries == null || entries.Count == 0)
          continue;

        materialHierarchy[fileName] = entries;
      }

      return materialHierarchy;
    }
  }

  /// <summary>
  /// Разделяемый кэш разобранных баз материалов (.sldmat). Переживает закрытие формы,
  /// поэтому повторные открытия и поиск в дереве не перечитывают XML. Инвалидация — по
  /// дате изменения файла, чтобы отредактированная вручную база перечиталась автоматически.
  /// </summary>
  internal static class VelumMaterialDatabaseCache
  {
    private static readonly object _sync = new object();
    private static readonly Dictionary<string, CacheEntry> _entries =
        new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);

    private sealed class CacheEntry
    {
      internal DateTime LastWriteUtc;
      internal List<string> Values;
    }

    /// <summary>
    /// Возвращает плоский список записей базы (имена категорий и материалов с отступом «  »),
    /// либо null, если файл не удалось разобрать.
    /// </summary>
    internal static List<string> GetOrLoad(string filePath)
    {
      DateTime lastWriteUtc;
      try
      {
        lastWriteUtc = File.GetLastWriteTimeUtc(filePath);
      }
      catch
      {
        return null;
      }

      lock (_sync)
      {
        CacheEntry cached;
        if (_entries.TryGetValue(filePath, out cached) && cached.LastWriteUtc == lastWriteUtc)
          return cached.Values;
      }

      List<string> parsed = ParseFile(filePath);

      lock (_sync)
      {
        _entries[filePath] = new CacheEntry { LastWriteUtc = lastWriteUtc, Values = parsed };
      }

      return parsed;
    }

    private static List<string> ParseFile(string filePath)
    {
      var entries = new List<string>();
      try
      {
        var doc = new XmlDocument();
        doc.Load(filePath);

        XmlNodeList classifications = doc.SelectNodes("//*[local-name()='classification']") ??
                                      doc.SelectNodes("//classification");
        if (classifications == null || classifications.Count == 0)
        {
          classifications = doc.SelectNodes("//materials/classification") ??
                            doc.SelectNodes("/classification");
        }

        if (classifications == null || classifications.Count == 0)
          return entries;

        foreach (XmlNode classification in classifications)
        {
          string categoryName = classification.Attributes?["name"]?.Value;
          if (string.IsNullOrEmpty(categoryName))
            continue;

          entries.Add(categoryName);

          XmlNodeList materials = classification.SelectNodes("*[local-name()='material']") ??
                                  classification.SelectNodes("material");
          if (materials == null)
            continue;

          foreach (XmlNode material in materials)
          {
            string materialName = material.Attributes?["name"]?.Value;
            if (!string.IsNullOrEmpty(materialName))
              entries.Add("  " + materialName);
          }
        }
      }
      catch (Exception ex)
      {
        if (VelumAppConfig.SolidHomeostasisDebugLog)
          Debug.WriteLine("Velum material XML load error: " + ex.Message);
      }

      return entries;
    }
  }
}
