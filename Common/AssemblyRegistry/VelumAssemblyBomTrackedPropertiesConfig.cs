using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ISIDA.Common;
using Newtonsoft.Json;

namespace Velum.UI.AssemblyRegistry
{
  /// <summary>
  /// Конфигурация отслеживаемых свойств (tracked properties) для зеркалирования BOM.
  /// Хранится в отдельном JSON-файле в каталоге AssemblyRegistry.
  /// </summary>
  internal static class VelumAssemblyBomTrackedPropertiesConfig
  {
    /// <summary>Значение по умолчанию: "Материал", "Масса".</summary>
    private static readonly string[] DefaultProperties = { "Материал", "Масса" };

    /// <summary>Путь к файлу конфигурации.</summary>
    public static string ConfigFilePath => Path.Combine(
        Velum.Configuration.VelumAppConfig.AssemblyRegistryFolderPath,
        "bomTrackedProperties.json");

    /// <summary>Список имён отслеживаемых свойств (без пустых).</summary>
    public static IReadOnlyList<string> Load()
    {
      try
      {
        if (!File.Exists(ConfigFilePath))
          return new List<string>(DefaultProperties);

        string json = File.ReadAllText(ConfigFilePath, Encoding.UTF8);
        if (string.IsNullOrWhiteSpace(json))
          return new List<string>(DefaultProperties);

        var wrapper = JsonConvert.DeserializeObject<TrackedPropertiesWrapper>(json);
        if (wrapper?.Properties == null || wrapper.Properties.Length == 0)
          return new List<string>(DefaultProperties);

        var result = new List<string>();
        foreach (string prop in wrapper.Properties)
        {
          string trimmed = (prop ?? string.Empty).Trim();
          if (!string.IsNullOrEmpty(trimmed) && !result.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
            result.Add(trimmed);
        }

        return result.Count > 0 ? (IReadOnlyList<string>)result : new List<string>(DefaultProperties);
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum bomTrackedProperties load failed (" + ConfigFilePath + "): " + ex.Message);
        return new List<string>(DefaultProperties);
      }
    }

    /// <summary>Сохранение списка свойств в JSON.</summary>
    public static void Save(IReadOnlyList<string> properties)
    {
      try
      {
        Directory.CreateDirectory(Velum.Configuration.VelumAppConfig.AssemblyRegistryFolderPath);
        var wrapper = new TrackedPropertiesWrapper
        {
          Properties = properties?.ToArray() ?? Array.Empty<string>()
        };
        string json = JsonConvert.SerializeObject(wrapper, Formatting.Indented);
        File.WriteAllText(ConfigFilePath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
      }
      catch (Exception ex)
      {
        Logger.Error("Velum bomTrackedProperties save failed: " + ex.Message);
      }
    }

    /// <summary>
    /// Вычислить SHA-256 хэш от нормализованных tracked properties + quantity.
    /// Формат: "Prop1=Value1;Prop2=Value2;Quantity=N" (сортировано по имени свойства).
    /// </summary>
    public static string ComputeHash(
        Dictionary<string, string> propertyValues,
        IReadOnlyList<string> trackedProperties,
        int quantity)
    {
      if (trackedProperties == null || trackedProperties.Count == 0)
        return string.Empty;

      var parts = new List<string>();

      foreach (string propName in trackedProperties.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
      {
        string value = string.Empty;
        if (propertyValues != null && propertyValues.TryGetValue(propName, out string propValue))
        {
          value = (propValue ?? string.Empty).Trim();
        }
        // Значение «?» считаем пустым для хэша.
        if (string.Equals(value, "?", StringComparison.Ordinal))
          value = string.Empty;
        parts.Add(propName + "=" + value);
      }

      parts.Add("Quantity=" + quantity);

      string normalized = string.Join(";", parts);
      return ComputeSha256(normalized);
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
  }

  /// <summary>Обёртка JSON-файла bomTrackedProperties.json.</summary>
  internal sealed class TrackedPropertiesWrapper
  {
    [JsonProperty("properties")]
    public string[] Properties { get; set; }
  }
}
