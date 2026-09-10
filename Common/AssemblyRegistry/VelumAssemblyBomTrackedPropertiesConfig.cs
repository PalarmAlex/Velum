using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ISIDA.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Velum.UI.AssemblyRegistry
{
  /// <summary>
  /// Конфигурация отслеживаемых свойств (tracked properties) для зеркалирования BOM.
  /// Хранится в отдельном JSON-файле в каталоге AssemblyRegistry.
  /// Список единый для сборок и деталей — он же задаёт столбцы выгрузки в 1С.
  /// </summary>
  internal static class VelumAssemblyBomTrackedPropertiesConfig
  {
    /// <summary>Значения по умолчанию.</summary>
    private static readonly TrackedProperty[] DefaultProperties = {
      new TrackedProperty { Name = "Материал", Precision = 0 },
      new TrackedProperty { Name = "Масса.кг.", Precision = 3 }
    };

    /// <summary>Ключ единого массива свойств в JSON-файле.</summary>
    private const string PropertiesKey = "properties";

    /// <summary>Минимально допустимая точность (округление до целого).</summary>
    public const int MinPrecision = 0;

    /// <summary>Максимально допустимая точность (Math.Round поддерживает не более 15 знаков).</summary>
    public const int MaxPrecision = 15;

    /// <summary>Старые ключи по типам документов (для миграции).</summary>
    private static readonly string[] LegacyKindKeys = { "Assembly", "Part" };

    /// <summary>Путь к файлу конфигурации.</summary>
    public static string ConfigFilePath => Path.Combine(
        Velum.Configuration.VelumAppConfig.AssemblyRegistryFolderPath,
        "bomTrackedProperties.json");

    /// <summary>Значения по умолчанию.</summary>
    private static IReadOnlyList<TrackedProperty> Defaults()
    {
      return new List<TrackedProperty>(DefaultProperties);
    }

    /// <summary>Загрузить единый список отслеживаемых свойств.</summary>
    public static IReadOnlyList<TrackedProperty> Load()
    {
      try
      {
        if (!File.Exists(ConfigFilePath))
          return Defaults();

        string json = File.ReadAllText(ConfigFilePath, Encoding.UTF8);
        if (string.IsNullOrWhiteSpace(json))
          return Defaults();

        JObject root = JObject.Parse(json);

        var result = ParseProperties(root[PropertiesKey]);

        // Миграция со старого формата: отдельные списки по типам документов.
        if (result.Count == 0)
        {
          foreach (string key in LegacyKindKeys)
            AppendUnique(result, ParseProperties(root[key]));
        }

        return result.Count > 0 ? (IReadOnlyList<TrackedProperty>)result : Defaults();
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum bomTrackedProperties load failed (" + ConfigFilePath + "): " + ex.Message);
        return Defaults();
      }
    }

    /// <summary>Сохранить единый список отслеживаемых свойств.</summary>
    public static void Save(IReadOnlyList<TrackedProperty> properties)
    {
      try
      {
        Directory.CreateDirectory(Velum.Configuration.VelumAppConfig.AssemblyRegistryFolderPath);

        var root = new JObject
        {
          // Единый массив свойств; старые ключи по типам документов больше не пишутся.
          [PropertiesKey] = new JArray(
              properties?.Select(p => new JObject(
                  new JProperty("name", p.Name),
                  new JProperty("precision", p.Precision))) ?? Array.Empty<JObject>())
        };

        string json = JsonConvert.SerializeObject(root, Formatting.Indented);
        File.WriteAllText(ConfigFilePath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
      }
      catch (Exception ex)
      {
        Logger.Error("Velum bomTrackedProperties save failed: " + ex.Message);
      }
    }

    /// <summary>
    /// Разобрать массив свойств из JSON (объекты {"name","precision"} или простые строки).
    /// </summary>
    private static List<TrackedProperty> ParseProperties(JToken token)
    {
      var result = new List<TrackedProperty>();
      if (token == null)
        return result;

      foreach (JToken item in token)
      {
        string name;
        int precision;

        if (item is JObject obj)
        {
          name = obj["name"]?.ToString() ?? string.Empty;
          JToken precisionToken = obj["precision"];
          precision = precisionToken != null && precisionToken.Type == JTokenType.Integer
              ? precisionToken.Value<int>()
              : 0;
        }
        else
        {
          name = item.ToString() ?? string.Empty;
          precision = 0;
        }

        string trimmed = name.Trim();
        if (!string.IsNullOrEmpty(trimmed) &&
            !result.Any(p => string.Equals(p.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
          result.Add(new TrackedProperty { Name = trimmed, Precision = ClampPrecision(precision) });
        }
      }

      return result;
    }

    /// <summary>
    /// Добавить в список свойства, отсутствующие в нём по имени (без учёта регистра).
    /// </summary>
    private static void AppendUnique(
        List<TrackedProperty> target, IReadOnlyList<TrackedProperty> additional)
    {
      foreach (TrackedProperty prop in additional)
      {
        if (!target.Any(p => string.Equals(p.Name, prop.Name, StringComparison.OrdinalIgnoreCase)))
          target.Add(prop);
      }
    }

    /// <summary>
    /// Вычислить SHA-256 хэш от нормализованных tracked properties + quantity.
    /// Формат: "Prop1=Value1;Prop2=Value2;Quantity=N" (сортировано по имени свойства).
    /// Числовые значения округляются до заданной точности.
    /// </summary>
    public static string ComputeHash(
        Dictionary<string, string> propertyValues,
        IReadOnlyList<TrackedProperty> trackedProperties,
        int quantity)
    {
      if (trackedProperties == null || trackedProperties.Count == 0)
        return string.Empty;

      var parts = new List<string>();

      foreach (var prop in trackedProperties.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
      {
        string value = string.Empty;
        if (propertyValues != null && propertyValues.TryGetValue(prop.Name, out string propValue))
        {
          value = (propValue ?? string.Empty).Trim();
        }
        // Значение «?» считаем пустым для хэша.
        if (string.Equals(value, "?", StringComparison.Ordinal))
          value = string.Empty;

        // Округляем числовые значения до заданной точности.
        value = RoundIfNumeric(value, prop.Precision);
        parts.Add(prop.Name + "=" + value);
      }

      parts.Add("Quantity=" + quantity);

      string normalized = string.Join(";", parts);
      return ComputeSha256(normalized);
    }

    /// <summary>
    /// Округлить числовое значение до заданного количества знаков.
    /// Если значение не является числом — возвращается без изменений.
    /// </summary>
    public static string RoundIfNumeric(string value, int precision)
    {
      if (string.IsNullOrEmpty(value))
        return value;

      if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
      {
        return Math.Round(number, ClampPrecision(precision), MidpointRounding.AwayFromZero)
                     .ToString(CultureInfo.InvariantCulture);
      }

      return value;
    }

    /// <summary>
    /// Ограничить точность допустимым диапазоном [<see cref="MinPrecision"/>; <see cref="MaxPrecision"/>].
    /// </summary>
    public static int ClampPrecision(int precision)
    {
      if (precision < MinPrecision)
        return MinPrecision;
      return precision > MaxPrecision ? MaxPrecision : precision;
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

  /// <summary>Отслеживаемое свойство с параметром точности.</summary>
  internal sealed class TrackedProperty
  {
    /// <summary>Имя свойства.</summary>
    public string Name { get; set; }

    /// <summary>Точность (количество знаков после запятой для числовых значений).</summary>
    public int Precision { get; set; }
  }
}
