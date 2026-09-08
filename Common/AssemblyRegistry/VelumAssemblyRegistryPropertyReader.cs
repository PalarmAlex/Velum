using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using SolidWorks.Interop.sldworks;
using Velum.ReactiveCore;
using Velum.SolidHomeostasis;

namespace Velum.UI.AssemblyRegistry
{
  /// <summary>
  /// Чтение свойств узла реестра изделия: конфигурация вхождения → document;
  /// Обозначение без свойства → имя файла; полный кэш для настраиваемых столбцов.
  /// </summary>
  internal static class VelumAssemblyRegistryPropertyReader
  {
    internal const string PropDesignation = "Обозначение";
    internal const string PropName = "Наименование";
    internal const string PropMaterial = "Материал";
    internal const string PropMass = "Масса";
    internal const string PropSection = VelumAssemblyRegistrySectionPath.PropSection;
    internal const string MissingMarker = "?";

    internal static void FillProperties(VelumAssemblyRegistryComponent item, ModelDoc2 modelDoc)
    {
      if (item == null)
        return;

      var cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

      if (modelDoc == null)
      {
        PutReserved(cache, item);
        item.Designation = item.FileTitle ?? string.Empty;
        item.Name = MissingMarker;
        item.Material = MissingMarker;
        item.Mass = MissingMarker;
        item.RolledStock = MissingMarker;
        item.Thickness = MissingMarker;
        item.Width = MissingMarker;
        item.Length = MissingMarker;
        item.TotalMass = string.Empty;
        item.TotalLength = string.Empty;
        cache[PropDesignation] = item.Designation ?? string.Empty;
        item.PropertyValues = cache;
        item.PropertiesLoaded = true;
        return;
      }

      string config = item.ConfigurationName ?? string.Empty;
      FillAllCustomProperties(cache, modelDoc, config);

      // Обозначение: как раньше — fallback на имя файла.
      string designation = ReadDesignation(modelDoc, config, item.FileTitle);
      cache[PropDesignation] = designation ?? string.Empty;

      // Reserved поверх custom props (Quantity/FileName не должны перекрываться одноимёнными свойствами SW).
      PutReserved(cache, item);

      item.Designation = designation ?? string.Empty;
      item.Name = GetCacheOrMissing(cache, PropName);
      item.Material = ResolveMaterialDisplay(modelDoc, config, cache);
      item.Mass = GetCacheOrMissing(cache, PropMass);
      item.RolledStock = GetCacheOrMissing(cache, VelumBlankSizeProperties.RolledStock);
      item.Thickness = GetCacheOrMissing(cache, VelumBlankSizeProperties.Thickness);
      item.Width = GetCacheOrMissing(cache, VelumBlankSizeProperties.Width);
      item.Length = GetCacheOrMissing(cache, VelumBlankSizeProperties.Length);
      item.TotalMass = MultiplyQuantity(item.Quantity, item.Mass);
      item.TotalLength = MultiplyQuantity(item.Quantity, item.Length);
      item.PropertyValues = cache;
      item.PropertiesLoaded = true;
    }

    /// <summary>
    /// Материал: произвольный текст свойства — как есть; ссылка SW-Material / нерезолвленная формула —
    /// имя из материала детали; иначе «?».
    /// </summary>
    private static string ResolveMaterialDisplay(
        ModelDoc2 modelDoc,
        string configurationName,
        Dictionary<string, string> cache)
    {
      string prop;
      if (cache == null || !cache.TryGetValue(PropMaterial, out prop) || prop == null)
        prop = string.Empty;
      else
        prop = prop.Trim();

      string display;
      if (prop.Length > 0 && !LooksLikeMaterialLink(prop))
      {
        // Ручной текст в свойстве «Материал».
        display = prop;
      }
      else
      {
        string materialName;
        string databaseName;
        if (VelumSolidWorksMaterialComHelper.TryReadPartMaterialForConfig(
                modelDoc,
                configurationName,
                out materialName,
                out databaseName) &&
            !string.IsNullOrWhiteSpace(materialName))
        {
          display = materialName.Trim();
        }
        else if (prop.Length > 0 && !LooksLikeMaterialLink(prop))
        {
          display = prop;
        }
        else
        {
          display = MissingMarker;
        }
      }

      if (cache != null)
        cache[PropMaterial] = display;
      return display;
    }

    /// <summary>Ссылка/формула материала SW, а не произвольный текст.</summary>
    private static bool LooksLikeMaterialLink(string value)
    {
      if (string.IsNullOrWhiteSpace(value))
        return false;

      string t = value.Trim();
      if (t.IndexOf("$PRP:", StringComparison.OrdinalIgnoreCase) >= 0)
        return true;
      if (t.IndexOf("SW-Material", StringComparison.OrdinalIgnoreCase) >= 0)
        return true;
      if (t.IndexOf("@@@", StringComparison.Ordinal) >= 0)
        return true;
      return false;
    }

    internal static string ReadSectionRaw(ModelDoc2 modelDoc, string configurationName)
    {
      if (modelDoc == null)
        return string.Empty;

      string value;
      bool exists;
      if (!VelumRecipeSolidWorksCustomProperties.TryReadConfigThenDocument(
              modelDoc,
              configurationName,
              PropSection,
              out value,
              out exists) ||
          !exists)
        return string.Empty;

      return (value ?? string.Empty).Trim();
    }

    /// <summary>
    /// Читает «Раздел» только с вкладки конфигурации вхождения (без fallback на document).
    /// </summary>
    internal static bool TryReadSectionOnConfiguration(
        ModelDoc2 modelDoc,
        string configurationName,
        out string value,
        out bool exists)
    {
      value = string.Empty;
      exists = false;
      if (modelDoc == null)
        return false;

      string config = (configurationName ?? string.Empty).Trim();
      if (config.Length == 0)
        return false;

      CustomPropertyManager cpm = VelumRecipeSolidWorksCustomProperties.TryGetManager(modelDoc, config);
      if (cpm == null)
        return false;

      if (!VelumRecipeSolidWorksCustomProperties.TryPropertyExists(cpm, PropSection))
        return true;

      exists = true;
      string raw;
      if (VelumRecipeSolidWorksCustomProperties.TryGetValue(cpm, PropSection, out raw))
        value = (raw ?? string.Empty).Trim();
      return true;
    }

    internal static string BuildIdentity(string filePath, string configurationName)
    {
      string path = NormalizePath(filePath);
      string config = (configurationName ?? string.Empty).Trim();
      return path + "|" + config;
    }

    internal static string FileTitleFromPath(string filePath)
    {
      if (string.IsNullOrWhiteSpace(filePath))
        return string.Empty;
      try
      {
        return Path.GetFileNameWithoutExtension(filePath) ?? string.Empty;
      }
      catch
      {
        return string.Empty;
      }
    }

    internal static string NormalizePath(string filePath)
    {
      if (string.IsNullOrWhiteSpace(filePath))
        return string.Empty;
      try
      {
        return Path.GetFullPath(filePath.Trim()).ToUpperInvariant();
      }
      catch
      {
        return filePath.Trim().ToUpperInvariant();
      }
    }

    private static void PutReserved(Dictionary<string, string> cache, VelumAssemblyRegistryComponent item)
    {
      string fileTitle = item.FileTitle ?? string.Empty;
      string qty = item.Quantity.ToString(CultureInfo.InvariantCulture);
      cache["FileName"] = fileTitle;
      cache["ИмяФайла"] = fileTitle;
      cache["Quantity"] = qty;
      cache["Кол-во"] = qty;
      if (!string.IsNullOrEmpty(item.ConfigurationName))
        cache["Configuration"] = item.ConfigurationName;
      if (!string.IsNullOrEmpty(item.FilePath))
        cache["FilePath"] = item.FilePath;
    }

    private static void FillAllCustomProperties(
        Dictionary<string, string> cache,
        ModelDoc2 modelDoc,
        string configurationName)
    {
      var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      CollectPropertyNames(modelDoc, configurationName, names);
      CollectPropertyNames(modelDoc, string.Empty, names);

      foreach (string name in names)
      {
        if (string.IsNullOrWhiteSpace(name))
          continue;

        string value;
        bool exists;
        if (!VelumRecipeSolidWorksCustomProperties.TryReadConfigThenDocument(
                modelDoc,
                configurationName,
                name,
                out value,
                out exists) ||
            !exists)
          continue;

        cache[name] = value ?? string.Empty;
      }
    }

    private static void CollectPropertyNames(
        ModelDoc2 modelDoc,
        string configurationName,
        HashSet<string> names)
    {
      string configKey = string.IsNullOrWhiteSpace(configurationName) ? "document" : configurationName.Trim();
      CustomPropertyManager cpm = VelumRecipeSolidWorksCustomProperties.TryGetManager(modelDoc, configKey);
      if (cpm == null)
        return;

      try
      {
        string[] list = cpm.GetNames() as string[];
        if (list == null)
          return;
        foreach (string name in list)
        {
          if (!string.IsNullOrWhiteSpace(name))
            names.Add(name.Trim());
        }
      }
      catch
      {
      }
    }

    private static string GetCacheOrMissing(Dictionary<string, string> cache, string key)
    {
      string value;
      if (cache != null && cache.TryGetValue(key, out value) && value != null)
        return value;
      return MissingMarker;
    }

    private static string ReadDesignation(ModelDoc2 modelDoc, string configurationName, string fileTitle)
    {
      string value;
      bool exists;
      if (VelumRecipeSolidWorksCustomProperties.TryReadConfigThenDocument(
              modelDoc,
              configurationName,
              PropDesignation,
              out value,
              out exists) &&
          exists)
        return value ?? string.Empty;

      return fileTitle ?? string.Empty;
    }

    private static string MultiplyQuantity(int quantity, string rawValue)
    {
      if (quantity <= 0)
        return string.Empty;
      if (string.IsNullOrWhiteSpace(rawValue) ||
          string.Equals(rawValue, MissingMarker, StringComparison.Ordinal))
        return string.Empty;

      double number;
      if (!VelumBlankSizeProperties.TryParseNumber(rawValue, out number))
        return string.Empty;

      double total = number * quantity;
      return FormatStorageNumber(total);
    }

    /// <summary>
    /// Отображение числа в списке: разделители разрядов;
    /// дробные — ровно 3 знака после запятой, целые — без дробной части.
    /// </summary>
    internal static string FormatListNumber(double value)
    {
      double rounded = Math.Round(value, 3, MidpointRounding.AwayFromZero);
      long asInt = (long)Math.Round(rounded, MidpointRounding.AwayFromZero);
      CultureInfo culture = CultureInfo.GetCultureInfo("ru-RU");
      if (Math.Abs(rounded - asInt) < 1e-9)
        return asInt.ToString("N0", culture);

      return rounded.ToString("N3", culture);
    }

    /// <summary>Отображение целого (кол-во) с разделителями разрядов.</summary>
    internal static string FormatListNumber(int value)
    {
      return value.ToString("N0", CultureInfo.GetCultureInfo("ru-RU"));
    }

    /// <summary>
    /// Проверяет, похоже ли значение на обозначение (начинается с «0» и содержит
    /// разделитель «.» или «,» после первого символа). Обозначения не форматируются
    /// как числа, чтобы не терять ведущие нули и не превращать «022.1012» в «22,101».
    /// </summary>
    private static bool LooksLikeDesignation(string text)
    {
      if (text.Length < 2 || text[0] != '0')
        return false;

      for (int i = 1; i < text.Length; i++)
      {
        char c = text[i];
        if (c == '.' || c == ',')
          return true;
      }

      return false;
    }

    /// <summary>
    /// Число для списка/сортировки: чистое значение или число + хвост единиц (мм, kg…).
    /// Обозначения («70К20.01.01», «004.004.Л01.07») и габариты («50х50») — не числа.
    /// </summary>
    internal static bool TryParseListNumber(string raw, out double value)
    {
      value = 0;
      if (string.IsNullOrWhiteSpace(raw))
        return false;

      string trimmed = raw.Trim();
      if (string.Equals(trimmed, MissingMarker, StringComparison.Ordinal))
        return false;
      if (string.Equals(trimmed, VelumAssemblyRegistryColumnFormula.EvaluationErrorMarker, StringComparison.Ordinal))
        return false;

      if (!VelumBlankSizeProperties.TryParseNumber(trimmed, out value))
        return false;

      int prefixEnd = MeasureNumericPrefixLength(trimmed);
      if (prefixEnd <= 0)
        return false;

      string suffix = prefixEnd < trimmed.Length
          ? trimmed.Substring(prefixEnd).TrimStart()
          : string.Empty;

      // Ещё один числовой сегмент (004.004.…) или хвост с цифрами (50х50) — это текст, не число+единицы.
      if (suffix.Length > 0 && !IsUnitSuffix(suffix))
        return false;

      return true;
    }

    /// <summary>
    /// Форматирует числовую ячейку списка; «?» и нечисловой текст без изменений.
    /// Хвост единиц (мм, kg…) сохраняется после отформатированного числа.
    /// Обозначения (начинаются с «0» и содержат «.»/«,») — не форматируются,
    /// чтобы не терять ведущие нули и не превращать «022.1012» в «22,101».
    /// </summary>
    internal static string FormatListNumberText(string raw)
    {
      if (string.IsNullOrWhiteSpace(raw))
        return string.Empty;

      string trimmed = raw.Trim();
      if (string.Equals(trimmed, MissingMarker, StringComparison.Ordinal))
        return MissingMarker;
      if (string.Equals(trimmed, VelumAssemblyRegistryColumnFormula.EvaluationErrorMarker, StringComparison.Ordinal))
        return trimmed;

      // Обозначения: не форматируем, чтобы сохранить ведущие нули и разделители компонентов.
      if (LooksLikeDesignation(trimmed))
        return raw;

      double number;
      if (!TryParseListNumber(trimmed, out number))
        return raw;

      int prefixEnd = MeasureNumericPrefixLength(trimmed);
      string suffix = prefixEnd > 0 && prefixEnd < trimmed.Length
          ? trimmed.Substring(prefixEnd).TrimStart()
          : string.Empty;

      string formatted = FormatListNumber(number);
      return suffix.Length == 0 ? formatted : formatted + " " + suffix;
    }

    /// <summary>Хвост единиц измерения: без цифр и без новой числовой точки/запятой в начале.</summary>
    private static bool IsUnitSuffix(string suffix)
    {
      if (string.IsNullOrEmpty(suffix))
        return true;

      char first = suffix[0];
      if (first == '.' || first == ',')
        return false;

      for (int i = 0; i < suffix.Length; i++)
      {
        if (char.IsDigit(suffix[i]))
          return false;
      }

      return true;
    }

    private static int MeasureNumericPrefixLength(string text)
    {
      int i = 0;
      bool seenDigit = false;
      bool seenDot = false;
      if (i < text.Length && (text[i] == '-' || text[i] == '+'))
        i++;

      for (; i < text.Length; i++)
      {
        char c = text[i];
        if (c >= '0' && c <= '9')
        {
          seenDigit = true;
          continue;
        }

        if ((c == '.' || c == ',') && !seenDot)
        {
          seenDot = true;
          continue;
        }

        break;
      }

      return seenDigit ? i : 0;
    }

    private static string FormatStorageNumber(double value)
    {
      double rounded = Math.Round(value, 3, MidpointRounding.AwayFromZero);
      long asInt = (long)Math.Round(rounded, MidpointRounding.AwayFromZero);
      if (Math.Abs(rounded - asInt) < 1e-9)
        return asInt.ToString(CultureInfo.InvariantCulture);

      return rounded.ToString("0.###", CultureInfo.InvariantCulture);
    }
  }
}
