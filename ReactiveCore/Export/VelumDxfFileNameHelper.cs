using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Velum.Configuration;
using Velum.ReactiveCore;

namespace Velum.ReactiveCore.Export
{
  /// <summary>Имя свойства детали и его значение для диалога экспорта DXF.</summary>
  internal sealed class VelumDxfPropertyEntry
  {
    internal VelumDxfPropertyEntry(string name, string value)
    {
      Name = (name ?? string.Empty).Trim();
      Value = value ?? string.Empty;
    }

    internal string Name { get; }

    internal string Value { get; }
  }

  /// <summary>Шаблон имени DXF, каталог вывода и запись свойств DXF.</summary>
  internal static class VelumDxfFileNameHelper
  {
    internal static string GetInitialNamePattern()
    {
      string lastUsed = VelumAppConfig.DxfFileNameTemplateLastUsed;
      if (!string.IsNullOrWhiteSpace(lastUsed))
        return VelumDxfFileNamePatternResolver.NormalizeSavedPattern(lastUsed);

      string saved = VelumAppConfig.DxfFileNameTemplate;
      if (!string.IsNullOrWhiteSpace(saved))
        return VelumDxfFileNamePatternResolver.NormalizeSavedPattern(saved);

      return string.Empty;
    }

    internal static string ResolvePatternToDisplay(
        string pattern,
        ModelDoc2 modelDoc,
        IReadOnlyDictionary<string, string> templateContext,
        string configName = null)
    {
      return VelumDxfFileNamePatternResolver.ResolvePatternToDisplay(
          pattern ?? string.Empty,
          modelDoc,
          templateContext);
    }

    /// <summary>
    /// Превью итогового имени для пакетного экспорта: префикс-заглушка и маска суффиксов без подстановки значений.
    /// </summary>
    internal static string BuildBatchSuffixPreview(string suffixPattern)
    {
      string pattern = (suffixPattern ?? string.Empty).Trim();
      if (pattern.Length == 0)
        return string.Empty;

      const string prefixPlaceholder = "имя_детали";
      string preview = VelumDxfArtifactResolver.CombinePrefixAndSuffix(prefixPlaceholder, pattern);
      return preview;
    }

    internal static string BuildFullFileName(
        string suffixPattern,
        ModelDoc2 modelDoc,
        string configName,
        IReadOnlyDictionary<string, string> templateContext)
    {
      return BuildFullFileName(suffixPattern, modelDoc, configName, templateContext, -1);
    }

    internal static string BuildFullFileName(
        string suffixPattern,
        ModelDoc2 modelDoc,
        string configName,
        IReadOnlyDictionary<string, string> templateContext,
        int prefixConfigurationCount)
    {
      string suffix = ResolvePatternToFileName(suffixPattern, modelDoc, templateContext, configName);
      string partBase = VelumDxfArtifactResolver.TryGetPartBaseName(modelDoc);
      int prefixCount = prefixConfigurationCount >= 0
          ? prefixConfigurationCount
          : VelumDxfNeedFlagResolver.ResolvePrefixConfigurationCount(modelDoc);
      string prefix = VelumDxfArtifactResolver.BuildRequiredPrefix(
          partBase,
          configName,
          prefixCount);

      string combined = VelumDxfArtifactResolver.TryMergePrefixAndSuffix(prefix, suffix, partBase);
      return PrepareResolvedFileName(combined);
    }

    internal static string ResolvePatternToFileName(
        string pattern,
        ModelDoc2 modelDoc,
        IReadOnlyDictionary<string, string> templateContext,
        string configName = null)
    {
      string display = ResolvePatternToDisplay(pattern, modelDoc, templateContext, configName);
      return PrepareResolvedFileName(display);
    }

    internal static string PrepareResolvedFileName(string resolvedDisplay)
    {
      string display = (resolvedDisplay ?? string.Empty).Trim();
      if (display.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase))
        display = Path.GetFileNameWithoutExtension(display);

      return SanitizeFileName(display);
    }

    internal static bool TryValidateBaseFileName(
        string baseName,
        ModelDoc2 modelDoc,
        string configName,
        out string message)
    {
      message = string.Empty;
      if (string.IsNullOrWhiteSpace(baseName))
      {
        message = "Укажите имя файла DXF (составьте имена свойств в поле выше или проверьте значения под полем).";
        return false;
      }

      if (baseName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
      {
        message = "Имя файла содержит недопустимые символы.";
        return false;
      }

      if (VelumRecipeTemplateResolver.ContainsUnresolvedTemplateTokens(baseName))
      {
        message = "Имя файла содержит неразрешённые шаблоны.";
        return false;
      }

      if (modelDoc != null)
      {
        string partBase = VelumDxfArtifactResolver.TryGetPartBaseName(modelDoc);
        int prefixCount = VelumDxfNeedFlagResolver.ResolvePrefixConfigurationCount(modelDoc);
        if (!VelumDxfArtifactResolver.NameContainsRequiredPrefix(
                baseName,
                partBase,
                configName,
                prefixCount))
        {
          message = "Имя файла должно начинаться с " +
                    VelumDxfArtifactResolver.BuildRequiredPrefix(partBase, configName, prefixCount) +
                    VelumDxfArtifactResolver.PrefixSuffixSeparator + "…";
          return false;
        }
      }

      return true;
    }

    internal static bool TryValidateBaseFileName(string baseName, out string message)
    {
      return TryValidateBaseFileName(baseName, null, null, out message);
    }

    internal static string TryReadPropertyValue(ModelDoc2 modelDoc, string propertyName)
    {
      return TryReadPropertyValue(modelDoc, propertyName, null);
    }

    internal static string TryReadPropertyValue(
        ModelDoc2 modelDoc,
        string propertyName,
        IReadOnlyDictionary<string, string> templateContext)
    {
      if (modelDoc == null || string.IsNullOrWhiteSpace(propertyName))
        return string.Empty;

      IReadOnlyDictionary<string, string> context = templateContext ?? BuildTemplateContext(modelDoc);
      if (VelumRecipeTemplateResolver.TryResolvePropertyValueForFileName(
              propertyName.Trim(),
              modelDoc,
              context,
              out string resolved) &&
          !string.IsNullOrWhiteSpace(resolved))
        return resolved.Trim();

      return string.Empty;
    }

    internal static bool TryGetEmptyReferencedSuffixProperties(
        string pattern,
        ModelDoc2 modelDoc,
        IReadOnlyDictionary<string, string> templateContext,
        out IReadOnlyList<string> emptyPropertyNames)
    {
      emptyPropertyNames = Array.Empty<string>();
      if (modelDoc == null || string.IsNullOrWhiteSpace(pattern))
        return false;

      IReadOnlyList<string> referenced =
          VelumDxfFileNamePatternResolver.CollectReferencedSuffixPropertyNames(pattern);
      if (referenced.Count == 0)
        return false;

      IReadOnlyDictionary<string, string> context = templateContext ?? BuildTemplateContext(modelDoc);
      var empty = new List<string>();
      for (int i = 0; i < referenced.Count; i++)
      {
        string propertyName = referenced[i];
        if (string.IsNullOrWhiteSpace(TryReadPropertyValue(modelDoc, propertyName, context)))
          empty.Add(propertyName);
      }

      if (empty.Count == 0)
        return false;

      emptyPropertyNames = empty;
      return true;
    }

    internal static string FormatEmptySuffixPropertiesError(
        IReadOnlyList<string> emptyPropertyNames,
        string partTitle,
        string configName)
    {
      string properties = emptyPropertyNames == null || emptyPropertyNames.Count == 0
          ? string.Empty
          : string.Join(", ", emptyPropertyNames);

      string part = string.IsNullOrWhiteSpace(partTitle) ? "(деталь)" : partTitle.Trim();
      string config = string.IsNullOrWhiteSpace(configName) ? null : configName.Trim();
      if (config == null)
        return "Пустые значения свойств суффикса: " + properties + " (" + part + ").";

      return "Пустые значения свойств суффикса: " + properties + " (" + part + ", конфигурация: " + config + ").";
    }

    internal static string FormatEmptySuffixPropertiesErrorList(IEnumerable<string> lines)
    {
      if (lines == null)
        return string.Empty;

      var items = lines
          .Where(line => !string.IsNullOrWhiteSpace(line))
          .ToList();
      if (items.Count == 0)
        return string.Empty;

      return "Пропущено из-за пустых свойств суффикса:" + System.Environment.NewLine +
             string.Join(System.Environment.NewLine, items);
    }

    internal static List<VelumDxfPropertyEntry> CollectCustomPropertyEntries(ModelDoc2 modelDoc)
    {
      var entries = new List<VelumDxfPropertyEntry>();
      if (modelDoc == null)
        return entries;

      IReadOnlyDictionary<string, string> context = BuildTemplateContext(modelDoc);
      foreach (string name in CollectCustomPropertyNames(modelDoc))
      {
        string value = string.Empty;
        VelumRecipeTemplateResolver.TryResolvePropertyValueForFileName(
            name,
            modelDoc,
            context,
            out string resolved);
        if (!string.IsNullOrWhiteSpace(resolved))
          value = resolved.Trim();

        entries.Add(new VelumDxfPropertyEntry(name, value));
      }

      return entries;
    }

    internal static IReadOnlyDictionary<string, string> BuildTemplateContext(ModelDoc2 modelDoc)
    {
      return BuildTemplateContext(modelDoc, VelumDxfQuantityToken.DefaultQuantity);
    }

    /// <summary>Контекст шаблона имени DXF с кол-вом для токенов [Quantity]/[Кол-во].</summary>
    /// <param name="modelDoc">Деталь SolidWorks (может быть null).</param>
    /// <param name="quantity">Кол-во для токенов; вне изделия обычно 1.</param>
    internal static IReadOnlyDictionary<string, string> BuildTemplateContext(
        ModelDoc2 modelDoc,
        int quantity)
    {
      string path = string.Empty;
      string title = string.Empty;
      bool dirty = false;
      if (modelDoc != null)
      {
        try
        {
          path = modelDoc.GetPathName() ?? string.Empty;
          title = modelDoc.GetTitle() ?? string.Empty;
          dirty = modelDoc.GetSaveFlag();
        }
        catch
        {
        }
      }

      var snapshot = new SolidWorksSessionSnapshot(
          hasActiveDocument: modelDoc != null,
          documentKind: VelumSolidDocumentKind.Part,
          documentPath: path,
          documentTitle: title,
          isSketchEditMode: false,
          isReadOnly: false,
          isDirty: dirty,
          pdmCheckedOut: null,
          capturedUtc: DateTime.UtcNow);

      var context = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      IReadOnlyDictionary<string, string> baseContext =
          VelumRecipeTemplateResolver.BuildContext(snapshot, modelDoc);
      if (baseContext != null)
      {
        foreach (KeyValuePair<string, string> kv in baseContext)
          context[kv.Key] = kv.Value;
      }

      context[VelumDxfQuantityToken.ContextKey] = VelumDxfQuantityToken.FormatQuantity(quantity);
      return context;
    }

    internal static string TryResolveDefaultOutputFolder(ModelDoc2 modelDoc)
    {
      if (modelDoc != null)
      {
        string fromDocument = TryResolveOutputFolderFromSavedDocument(modelDoc);
        if (!string.IsNullOrWhiteSpace(fromDocument))
        {
          VelumAppConfig.SetDxfDefaultOutputFolder(fromDocument);
          return fromDocument;
        }
      }

      string savedFolder = (VelumAppConfig.DxfDefaultOutputFolder ?? string.Empty).Trim();
      if (!string.IsNullOrWhiteSpace(savedFolder) && Directory.Exists(savedFolder))
        return savedFolder;

      return string.Empty;
    }

    private static string TryResolveOutputFolderFromSavedDocument(ModelDoc2 modelDoc)
    {
      if (modelDoc == null || !TryHasSavedDocumentPath(modelDoc))
        return string.Empty;

      CustomPropertyManager cpm =
          VelumRecipeSolidWorksCustomProperties.TryGetManager(modelDoc, "document");
      if (cpm != null &&
          VelumRecipeSolidWorksCustomProperties.TryGetValue(
              cpm,
              VelumExportDocumentationProperties.DxfPath,
              out string pathValue) &&
          !string.IsNullOrWhiteSpace(pathValue))
      {
        string catalog = VelumDxfArtifactResolver.NormalizeCatalogPath(pathValue, modelDoc);
        if (!string.IsNullOrWhiteSpace(catalog) && Directory.Exists(catalog))
          return catalog;
      }

      try
      {
        string partPath = modelDoc.GetPathName();
        if (!string.IsNullOrWhiteSpace(partPath))
        {
          string dir = Path.GetDirectoryName(partPath);
          if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
            return dir;
        }
      }
      catch
      {
      }

      return string.Empty;
    }

    private static bool TryHasSavedDocumentPath(ModelDoc2 modelDoc)
    {
      try
      {
        return !string.IsNullOrWhiteSpace(modelDoc?.GetPathName());
      }
      catch
      {
        return false;
      }
    }

    internal static bool TryWriteDxfCatalogProperty(ModelDoc2 modelDoc, string catalogPath, out string message)
    {
      message = string.Empty;
      if (modelDoc == null || string.IsNullOrWhiteSpace(catalogPath))
      {
        message = "model_or_path_missing";
        return false;
      }

      try
      {
        if (modelDoc.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY)
        {
          message = "Свойство «Путь dxf» не записывается для сборок";
          return false;
        }
      }
      catch
      {
        message = "document_type_unavailable";
        return false;
      }

      CustomPropertyManager cpm =
          VelumRecipeSolidWorksCustomProperties.TryGetManager(modelDoc, "document");
      if (cpm == null)
      {
        message = "property_manager_unavailable";
        return false;
      }

      string normalized = VelumDxfArtifactResolver.NormalizeCatalogPath(catalogPath, modelDoc);
      return VelumRecipeSolidWorksCustomProperties.TrySetValue(
          cpm,
          VelumExportDocumentationProperties.DxfPath,
          normalized,
          "always",
          VelumSolidCustomPropertyTypes.TypeKeyText,
          out bool skipped,
          out message) && !skipped;
    }

    internal static bool TryWriteDxfPathProperty(ModelDoc2 modelDoc, string fullPath, out string message)
    {
      if (string.IsNullOrWhiteSpace(fullPath))
        return TryWriteDxfCatalogProperty(modelDoc, string.Empty, out message);

      string catalog = string.Empty;
      try
      {
        catalog = Path.GetDirectoryName(fullPath);
      }
      catch
      {
      }

      return TryWriteDxfCatalogProperty(modelDoc, catalog, out message);
    }

    internal static bool TryWriteProjectionViewProperty(
        ModelDoc2 modelDoc,
        string configName,
        VelumDxfProjectionView projectionView,
        out string message)
    {
      message = string.Empty;
      CustomPropertyManager cpm = VelumDxfArtifactResolver.TryGetConfigManager(modelDoc, configName);
      if (cpm == null)
      {
        message = "property_manager_unavailable";
        return false;
      }

      return VelumRecipeSolidWorksCustomProperties.TrySetValue(
          cpm,
          VelumExportDocumentationProperties.DxfProjectionView,
          projectionView.ToString(),
          "always",
          VelumSolidCustomPropertyTypes.TypeKeyText,
          out bool skipped,
          out message) && !skipped;
    }

    /// <summary>
    /// Читает «Вид проекции dxf» из свойств конфигурации.
    /// Возвращает true только если свойство задано и распознано.
    /// </summary>
    internal static bool TryGetProjectionViewProperty(
        ModelDoc2 modelDoc,
        string configName,
        out VelumDxfProjectionView projectionView)
    {
      projectionView = VelumDxfProjectionView.Front;
      CustomPropertyManager cpm = VelumDxfArtifactResolver.TryGetConfigManager(modelDoc, configName);
      if (cpm == null ||
          !VelumRecipeSolidWorksCustomProperties.TryGetValue(
              cpm,
              VelumExportDocumentationProperties.DxfProjectionView,
              out string raw) ||
          string.IsNullOrWhiteSpace(raw))
        return false;

      if (!Enum.TryParse(raw.Trim(), true, out VelumDxfProjectionView parsed))
        return false;

      projectionView = parsed;
      return true;
    }

    internal static VelumDxfProjectionView TryReadProjectionViewProperty(
        ModelDoc2 modelDoc,
        string configName)
    {
      if (TryGetProjectionViewProperty(modelDoc, configName, out VelumDxfProjectionView fromProperty))
        return fromProperty;

      string defaultView = VelumAppConfig.DxfDefaultProjectionView;
      if (Enum.TryParse((defaultView ?? string.Empty).Trim(), true, out VelumDxfProjectionView fromSettings))
        return fromSettings;

      return VelumDxfProjectionView.Front;
    }

    internal static bool TryActivateConfiguration(ModelDoc2 modelDoc, string configName)
    {
      if (modelDoc == null || string.IsNullOrWhiteSpace(configName))
        return true;

      string canonical = VelumDxfArtifactResolver.TryResolveDxfConfigurationName(modelDoc, configName);
      if (string.IsNullOrWhiteSpace(canonical))
        return true;

      canonical = VelumDxfArtifactResolver.TryResolveKnownConfigurationName(modelDoc, canonical);
      if (string.IsNullOrWhiteSpace(canonical))
        return true;

      string active = VelumDxfArtifactResolver.TryGetActiveConfigurationName(modelDoc);
      if (string.Equals(active, canonical, StringComparison.OrdinalIgnoreCase))
        return true;

      try
      {
        if (modelDoc.ShowConfiguration2(canonical))
          return true;

        // ShowConfiguration2 иногда возвращает false, хотя конфигурация уже активна.
        active = VelumDxfArtifactResolver.TryGetActiveConfigurationName(modelDoc);
        return string.Equals(active, canonical, StringComparison.OrdinalIgnoreCase);
      }
      catch
      {
        return false;
      }
    }

    internal static List<string> CollectCustomPropertyNames(ModelDoc2 modelDoc)
    {
      var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      if (modelDoc == null)
        return new List<string>();

      // Свойства документа (file-level).
      TryAddPropertyNames(modelDoc, string.Empty, names);

      // Имена из всех конфигураций — новые свойства любой конфигурации попадают в список суффиксов.
      foreach (string configName in VelumDxfArtifactResolver.TryGetConfigurationNames(modelDoc))
        TryAddPropertyNames(modelDoc, configName, names);

      return names.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }

    internal static void PersistNamePatternSettings(string pattern)
    {
      string trimmed = (pattern ?? string.Empty).Trim();
      if (string.IsNullOrWhiteSpace(trimmed))
        return;

      VelumAppConfig.SetDxfFileNameTemplateLastUsed(trimmed);
      VelumAppConfig.SetDxfFileNameTemplate(trimmed);
    }

    internal static void PersistExportSettings(
        string pattern,
        string outputFolder,
        VelumDxfProjectionView projectionView)
    {
      PersistNamePatternSettings(pattern);

      string folder = (outputFolder ?? string.Empty).Trim();
      if (!string.IsNullOrWhiteSpace(folder))
        VelumAppConfig.SetDxfDefaultOutputFolder(folder);

      VelumAppConfig.SetDxfDefaultProjectionView(projectionView.ToString());
    }

    private static void TryAddPropertyNames(
        ModelDoc2 modelDoc,
        string configKey,
        ISet<string> names)
    {
      try
      {
        CustomPropertyManager cpm = modelDoc.Extension?.CustomPropertyManager[configKey ?? string.Empty];
        string[] propertyNames = cpm?.GetNames() as string[];
        if (propertyNames == null)
          return;

        for (int i = 0; i < propertyNames.Length; i++)
        {
          if (!string.IsNullOrWhiteSpace(propertyNames[i]))
            names.Add(propertyNames[i].Trim());
        }
      }
      catch
      {
      }
    }

    private static string SanitizeFileName(string name)
    {
      if (string.IsNullOrWhiteSpace(name))
        return string.Empty;

      char[] invalid = Path.GetInvalidFileNameChars();
      var chars = name.ToCharArray();
      for (int i = 0; i < chars.Length; i++)
      {
        if (invalid.Contains(chars[i]))
          chars[i] = '_';
      }

      return new string(chars).Trim();
    }
  }
}
