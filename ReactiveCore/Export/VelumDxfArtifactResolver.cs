using System;
using System.Collections.Generic;
using System.IO;
using SolidWorks.Interop.sldworks;
using Velum.ReactiveCore;

namespace Velum.ReactiveCore.Export
{
  /// <summary>Результат поиска DXF-артефакта в каталоге «Путь dxf».</summary>
  internal sealed class ResolvedDxfArtifact
  {
    internal bool Found { get; set; }

    internal string FullPath { get; set; }

    internal string BaseName { get; set; }

    internal string ConfigName { get; set; }

    internal string Reason { get; set; }

    /// <summary>Найден файл с тем же префиксом, но другим кол-вом в имени.</summary>
    internal bool QuantityMismatch { get; set; }

    internal int ExpectedQuantity { get; set; } = VelumDxfQuantityToken.DefaultQuantity;
  }

  /// <summary>
  /// Единый резолвер DXF: имя берётся из свойства детали «Имя файла dxf» — единственный
  /// источник правды. Если свойство пустое — файл не найден. Если свойство задано, ищется
  /// файл с этим именем в каталоге «Путь dxf»; при наличии токена кол-ва проверяется
  /// совпадение количества.
  /// </summary>
  internal static class VelumDxfArtifactResolver
  {
    /// <summary>
    /// Разделитель обязательного prefix и суффикса маски.
    /// Пробел — совместимо с E-drawing и другими приложениями, которые не поддерживают «∶» (U+2236).
    /// </summary>
    internal const char PrefixSuffixSeparatorChar = ' ';

    internal const string PrefixSuffixSeparator = " ";

    private const char LegacyFullwidthColon = '\uFF1A';

    internal static bool IsPrefixSuffixSeparatorChar(char c)
    {
      return c == PrefixSuffixSeparatorChar || c == LegacyFullwidthColon || c == ':';
    }

    internal static string CombinePrefixAndSuffix(string prefix, string suffix)
    {
      string p = (prefix ?? string.Empty).Trim();
      string s = (suffix ?? string.Empty).Trim();
      if (string.IsNullOrWhiteSpace(p))
        return s;
      if (string.IsNullOrWhiteSpace(s))
        return p;

      return p + PrefixSuffixSeparator + s;
    }

    internal static string TryMergePrefixAndSuffix(string prefix, string suffix, string partBaseName)
    {
      string p = (prefix ?? string.Empty).Trim();
      string s = (suffix ?? string.Empty).Trim();
      if (string.IsNullOrWhiteSpace(s))
        return p;
      if (string.IsNullOrWhiteSpace(p))
        return s;

      if (s.StartsWith(p + PrefixSuffixSeparator, StringComparison.OrdinalIgnoreCase))
        return s;

      if (s.StartsWith(p + ": ", StringComparison.OrdinalIgnoreCase))
        return CombinePrefixAndSuffix(p, s.Substring(p.Length + 2).TrimStart());

      if (s.StartsWith(p + ":", StringComparison.OrdinalIgnoreCase))
        return CombinePrefixAndSuffix(p, s.Substring(p.Length + 1).TrimStart());

      if (s.StartsWith(p, StringComparison.OrdinalIgnoreCase) &&
          !string.Equals(s, p, StringComparison.OrdinalIgnoreCase))
      {
        string remainder = s.Substring(p.Length).TrimStart(PrefixSuffixSeparatorChar, LegacyFullwidthColon, ':', ' ');
        if (!string.IsNullOrWhiteSpace(remainder))
          return CombinePrefixAndSuffix(p, remainder);
        return p;
      }

      if (!string.IsNullOrWhiteSpace(partBaseName) &&
          string.Equals(s, partBaseName.Trim(), StringComparison.OrdinalIgnoreCase))
        return p;

      return CombinePrefixAndSuffix(p, s);
    }
    internal static ResolvedDxfArtifact Resolve(
        ModelDoc2 modelDoc,
        string catalogPath,
        string configName)
    {
      return Resolve(modelDoc, catalogPath, configName, VelumDxfQuantityToken.DefaultQuantity);
    }

    internal static ResolvedDxfArtifact Resolve(
        ModelDoc2 modelDoc,
        string catalogPath,
        string configName,
        int quantity)
    {
      return Resolve(modelDoc, catalogPath, configName, quantity, -1);
    }

    /// <param name="modelDoc">Деталь SOLIDWORKS.</param>
    /// <param name="catalogPath">Каталог DXF («Путь dxf»).</param>
    /// <param name="configName">Имя конфигурации.</param>
    /// <param name="quantity">Ожидаемое количество в имени файла.</param>
    /// <param name="prefixConfigurationCount">
    /// Число конфигураций для суффикса имени; меньше 0 — взять экспортные (или все, если экспортных нет).
    /// </param>
    internal static ResolvedDxfArtifact Resolve(
        ModelDoc2 modelDoc,
        string catalogPath,
        string configName,
        int quantity,
        int prefixConfigurationCount)
    {
      var result = new ResolvedDxfArtifact
      {
        ConfigName = (configName ?? string.Empty).Trim(),
        Reason = "not_resolved",
        ExpectedQuantity = quantity < 0 ? 0 : quantity
      };

      if (modelDoc == null)
      {
        result.Reason = "model_null";
        return result;
      }

      string catalog = NormalizeCatalogPath(catalogPath, modelDoc);
      if (string.IsNullOrWhiteSpace(catalog) || !VelumPathExists.DirectoryExists(catalog))
      {
        result.Reason = "catalog_missing";
        return result;
      }

      IReadOnlyList<string> configNames = TryGetConfigurationNames(modelDoc);
      string config = TryResolveDxfConfigurationName(modelDoc, configName);
      if (string.IsNullOrWhiteSpace(config) && configNames.Count > 0)
        config = configNames[0] ?? string.Empty;
      result.ConfigName = config;

      // Свойство «Имя файла dxf» — единственный источник правды для ожидаемого имени DXF.
      string expectedName = TryReadPerConfigFileName(modelDoc, config);
      if (string.IsNullOrWhiteSpace(expectedName))
      {
        result.Reason = "dxf_name_property_empty";
        return result;
      }

      result.BaseName = expectedName;
      if (TryFindExactFile(catalog, expectedName, out string patternPath))
      {
        result.Found = true;
        result.FullPath = patternPath;
        result.Reason = "pattern_exact";
        return result;
      }

      if (VelumDxfQuantityToken.PatternReferencesQuantity(
              VelumDxfFileNameHelper.GetInitialNamePattern()) &&
          TryFindQuantityMismatchFile(catalog, expectedName, out string mismatchPath))
      {
        result.Found = false;
        result.QuantityMismatch = true;
        result.FullPath = mismatchPath;
        result.Reason = "quantity_mismatch";
        return result;
      }

      // Свойство задано, но файл не найден — имя устарело (файл переименован/удалён).
      result.Reason = "stored_name_stale";
      return result;
    }

    internal static string BuildRequiredPrefix(
        string partBaseName,
        string configName,
        int configurationCount)
    {
      string baseName = (partBaseName ?? string.Empty).Trim();
      string config = (configName ?? string.Empty).Trim();
      if (configurationCount <= 1 || string.IsNullOrWhiteSpace(config))
        return baseName;

      return baseName + "_" + config;
    }

    internal static bool NameContainsRequiredPrefix(
        string fileName,
        string partBaseName,
        string configName,
        int configurationCount)
    {
      string name = (fileName ?? string.Empty).Trim();
      if (string.IsNullOrWhiteSpace(name))
        return false;

      string prefix = BuildRequiredPrefix(partBaseName, configName, configurationCount);
      if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        return false;

      if (name.Length == prefix.Length)
        return true;

      char next = name[prefix.Length];
      return IsPrefixSuffixSeparatorChar(next) || char.IsLetterOrDigit(next);
    }

    internal static string NormalizeCatalogPath(string rawPath, ModelDoc2 modelDoc)
    {
      string trimmed = (rawPath ?? string.Empty).Trim();
      if (string.IsNullOrWhiteSpace(trimmed))
        return string.Empty;

      try
      {
        string expanded = System.Environment.ExpandEnvironmentVariables(trimmed);
        if (!Path.IsPathRooted(expanded))
        {
          // Относительный путь из свойства — сначала достройка префикса корневого каталога
          // (мигрированные значения срезаны по корню). Подкаталог детали («DXF» рядом с ней)
          // не резолвится по корню и уходит в прежнюю логику от каталога документа.
          string byRoot = VelumRelativeDocumentPathResolver.ToFull(expanded);
          if (Path.IsPathRooted(byRoot) && VelumPathExists.DirectoryExists(byRoot))
            expanded = byRoot;
        }

        if (!Path.IsPathRooted(expanded) && modelDoc != null)
        {
          string docPath = TryGetPartPath(modelDoc);
          if (!string.IsNullOrWhiteSpace(docPath))
          {
            string docDir = Path.GetDirectoryName(docPath);
            if (!string.IsNullOrWhiteSpace(docDir))
              expanded = Path.GetFullPath(Path.Combine(docDir, expanded));
          }
        }
        else if (!Path.IsPathRooted(expanded))
        {
          expanded = Path.GetFullPath(expanded);
        }

        if (VelumPathExists.DirectoryExists(expanded))
          return expanded;

        if (VelumPathExists.FileExists(expanded))
          return Path.GetDirectoryName(expanded);
      }
      catch
      {
      }

      return trimmed;
    }

    /// <summary>
    /// Пользовательские конфигурации детали для DXF/материалов.
    /// Служебные развёртки SheetMetal (SM-FLAT-PATTERN) исключаются.
    /// </summary>
    internal static IReadOnlyList<string> TryGetConfigurationNames(ModelDoc2 modelDoc)
    {
      var names = new List<string>();
      if (modelDoc == null)
        return names;

      try
      {
        string[] raw = modelDoc.GetConfigurationNames() as string[];
        if (raw == null || raw.Length == 0)
        {
          names.Add(string.Empty);
          return names;
        }

        for (int i = 0; i < raw.Length; i++)
        {
          string n = (raw[i] ?? string.Empty).Trim();
          if (string.IsNullOrWhiteSpace(n))
            continue;
          if (VelumSolidSheetMetalHelper.IsSheetMetalFlatPatternConfiguration(modelDoc, n))
            continue;
          names.Add(n);
        }

        if (names.Count == 0)
          names.Add(string.Empty);
      }
      catch
      {
        names.Add(string.Empty);
      }

      return names;
    }

    internal static string TryGetActiveConfigurationName(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return string.Empty;

      try
      {
        ConfigurationManager cm = modelDoc.ConfigurationManager;
        SolidWorks.Interop.sldworks.Configuration active = cm?.ActiveConfiguration;
        string name = active?.Name;
        return string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();
      }
      catch
      {
        return string.Empty;
      }
    }

    /// <summary>
    /// Конфигурация для DXF: служебную развёртку сводит к родителю;
    /// пустое имя — первая пользовательская конфигурация.
    /// </summary>
    internal static string TryResolveDxfConfigurationName(ModelDoc2 modelDoc, string configName)
    {
      string wanted = (configName ?? string.Empty).Trim();
      if (!string.IsNullOrWhiteSpace(wanted))
      {
        wanted = VelumSolidSheetMetalHelper.TryMapFlatPatternToParentConfigurationName(modelDoc, wanted);
        if (!VelumSolidSheetMetalHelper.IsSheetMetalFlatPatternConfiguration(modelDoc, wanted))
          return wanted;
      }

      IReadOnlyList<string> configs = TryGetConfigurationNames(modelDoc);
      return configs.Count > 0 ? configs[0] : string.Empty;
    }

    /// <summary>
    /// Имя конфигурации для одиночного экспорта: активная (с маппингом развёртки), иначе первая пользовательская.
    /// </summary>
    internal static string TryResolveDefaultExportConfigurationName(ModelDoc2 modelDoc)
    {
      return TryResolveDxfConfigurationName(modelDoc, TryGetActiveConfigurationName(modelDoc));
    }

    internal static string TryResolveKnownConfigurationName(ModelDoc2 modelDoc, string configName)
    {
      string wanted = (configName ?? string.Empty).Trim();
      if (string.IsNullOrWhiteSpace(wanted))
        return string.Empty;

      try
      {
        string[] raw = modelDoc?.GetConfigurationNames() as string[];
        if (raw != null)
        {
          for (int i = 0; i < raw.Length; i++)
          {
            string name = (raw[i] ?? string.Empty).Trim();
            if (string.Equals(name, wanted, StringComparison.OrdinalIgnoreCase))
              return name;
          }
        }
      }
      catch
      {
      }

      return wanted;
    }

    internal static string TryGetPartBaseName(ModelDoc2 modelDoc)
    {
      string path = TryGetPartPath(modelDoc);
      if (string.IsNullOrWhiteSpace(path))
        return string.Empty;

      try
      {
        return Path.GetFileNameWithoutExtension(path);
      }
      catch
      {
        return string.Empty;
      }
    }

    internal static string TryReadPerConfigFileName(ModelDoc2 modelDoc, string configName)
    {
      CustomPropertyManager cpm = TryGetConfigManager(modelDoc, configName);
      if (cpm == null)
        return string.Empty;

      if (!VelumRecipeSolidWorksCustomProperties.TryGetValue(
              cpm,
              VelumExportDocumentationProperties.DxfFileName,
              out string raw))
        return string.Empty;

      return (raw ?? string.Empty).Trim();
    }

    internal static bool TryWritePerConfigFileName(
        ModelDoc2 modelDoc,
        string configName,
        string baseName,
        out string message)
    {
      message = string.Empty;
      CustomPropertyManager cpm = TryGetConfigManager(modelDoc, configName);
      if (cpm == null)
      {
        message = "property_manager_unavailable";
        return false;
      }

      return VelumRecipeSolidWorksCustomProperties.TrySetValue(
          cpm,
          VelumExportDocumentationProperties.DxfFileName,
          baseName ?? string.Empty,
          "always",
          VelumSolidCustomPropertyTypes.TypeKeyText,
          out bool skipped,
          out message) && !skipped;
    }

    internal static bool TryClearPerConfigExportMetadata(
        ModelDoc2 modelDoc,
        string configName,
        out string message)
    {
      message = string.Empty;
      bool ok = true;
      string localMessage = string.Empty;
      VelumExportDocumentationGeometryStampHelper.RunWithGeometryPendingStampSyncSuppressed(() =>
      {
        CustomPropertyManager cpm = TryGetConfigManager(modelDoc, configName);
        if (cpm == null)
        {
          localMessage = "property_manager_unavailable";
          ok = false;
          return;
        }

        ok = TryClearProperty(cpm, VelumExportDocumentationProperties.DxfFileName, out string m1);
        ok &= TryClearProperty(
            cpm,
            VelumExportDocumentationProperties.DxfGeometryUpdateStamp,
            VelumSolidCustomPropertyTypes.TypeKeyNumber,
            out string m2);
        ok &= TryClearProperty(
            cpm,
            VelumExportDocumentationProperties.DxfGeometryPendingStamp,
            VelumSolidCustomPropertyTypes.TypeKeyNumber,
            out string m3);
        TryClearProperty(cpm, VelumExportDocumentationProperties.DxfProjectionView, out _);
        TryClearProperty(cpm, VelumExportDocumentationProperties.DxfFileFingerprint, out _);
        if (!ok)
          localMessage = string.IsNullOrWhiteSpace(m1) ? (string.IsNullOrWhiteSpace(m2) ? m3 : m2) : m1;
      });

      if (!string.IsNullOrWhiteSpace(localMessage))
        message = localMessage;

      return ok;
    }

    private static bool TryClearProperty(CustomPropertyManager cpm, string propertyName, out string message)
    {
      return TryClearProperty(
          cpm,
          propertyName,
          VelumSolidCustomPropertyTypes.TypeKeyText,
          out message);
    }

    private static bool TryClearProperty(
        CustomPropertyManager cpm,
        string propertyName,
        string propertyTypeKey,
        out string message)
    {
      message = string.Empty;
      if (cpm == null || string.IsNullOrWhiteSpace(propertyName))
        return false;

      return VelumRecipeSolidWorksCustomProperties.TrySetValue(
          cpm,
          propertyName,
          string.Empty,
          "always",
          propertyTypeKey,
          out bool skipped,
          out message) && !skipped;
    }

    internal static CustomPropertyManager TryGetConfigManager(ModelDoc2 modelDoc, string configName)
    {
      if (modelDoc?.Extension == null)
        return null;

      try
      {
        string key = (configName ?? string.Empty).Trim();
        return modelDoc.Extension.CustomPropertyManager[key];
      }
      catch
      {
        return null;
      }
    }

    private static bool TryFindExactFile(string catalog, string baseName, out string fullPath)
    {
      fullPath = null;
      if (string.IsNullOrWhiteSpace(catalog) || string.IsNullOrWhiteSpace(baseName))
        return false;

      string candidate = Path.Combine(catalog, baseName + ".dxf");
      if (VelumPathExists.FileExists(candidate))
      {
        fullPath = candidate;
        return true;
      }

      try
      {
        foreach (string file in Directory.EnumerateFiles(catalog, "*.dxf", SearchOption.TopDirectoryOnly))
        {
          string name = Path.GetFileNameWithoutExtension(file);
          if (string.Equals(name, baseName, StringComparison.OrdinalIgnoreCase))
          {
            fullPath = file;
            return true;
          }
        }
      }
      catch
      {
      }

      return false;
    }

    /// <summary>
    /// Ищет DXF с тем же префиксом (без «- Nшт»), но другим кол-вом в имени.
    /// </summary>
    private static bool TryFindQuantityMismatchFile(
        string catalog,
        string expectedBaseName,
        out string fullPath)
    {
      fullPath = null;
      if (string.IsNullOrWhiteSpace(catalog) || string.IsNullOrWhiteSpace(expectedBaseName))
        return false;

      string expectedStem = VelumDxfQuantityToken.StripQuantitySuffix(expectedBaseName);
      if (string.IsNullOrWhiteSpace(expectedStem))
        return false;

      if (!VelumDxfQuantityToken.TryParseQuantityFromFileName(expectedBaseName, out int expectedQty))
        return false;

      try
      {
        foreach (string file in Directory.EnumerateFiles(catalog, "*.dxf", SearchOption.TopDirectoryOnly))
        {
          string name = Path.GetFileNameWithoutExtension(file);
          if (string.IsNullOrWhiteSpace(name))
            continue;

          if (string.Equals(name, expectedBaseName, StringComparison.OrdinalIgnoreCase))
            continue;

          string stem = VelumDxfQuantityToken.StripQuantitySuffix(name);
          if (!string.Equals(stem, expectedStem, StringComparison.OrdinalIgnoreCase))
            continue;

          if (!VelumDxfQuantityToken.TryParseQuantityFromFileName(name, out int fileQty))
            continue;

          if (fileQty == expectedQty)
            continue;

          fullPath = file;
          return true;
        }
      }
      catch
      {
      }

      return false;
    }

    private static string TryGetPartPath(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return string.Empty;

      try
      {
        return (modelDoc.GetPathName() ?? string.Empty).Trim();
      }
      catch
      {
        return string.Empty;
      }
    }
  }
}
