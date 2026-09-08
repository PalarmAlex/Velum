using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Velum.ReactiveCore;

namespace Velum.ReactiveCore.Export
{
  /// <summary>Признак листового металла и feature FlatPattern (как в KmdEdit).</summary>
  internal static class VelumSolidSheetMetalHelper
  {
    /// <summary>Суффикс имени служебной конфигурации развёртки SolidWorks (fallback).</summary>
    private const string FlatPatternConfigNameMarker = "SM-FLAT-PATTERN";

    internal static bool IsSheetMetalPart(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return false;

      try
      {
        Feature feature = modelDoc.FirstFeature() as Feature;
        while (feature != null)
        {
          if (feature.GetTypeName() == "SheetMetal")
            return true;

          feature = feature.GetNextFeature() as Feature;
        }
      }
      catch
      {
      }

      return false;
    }

    internal static Feature TryFindFlatPatternFeature(ModelDoc2 modelDoc)
    {
      IReadOnlyList<Feature> all = CollectFlatPatternFeatures(modelDoc);
      return all.Count > 0 ? all[0] : null;
    }

    /// <summary>Все feature FlatPattern (мультитель — несколько развёрток).</summary>
    internal static IReadOnlyList<Feature> CollectFlatPatternFeatures(ModelDoc2 modelDoc)
    {
      var result = new List<Feature>();
      if (modelDoc == null)
        return result;

      var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

      try
      {
        FeatureManager featMgr = modelDoc.FeatureManager;
        object[] features = featMgr != null ? featMgr.GetFeatures(false) as object[] : null;
        if (features != null)
        {
          for (int i = 0; i < features.Length; i++)
          {
            Feature feature = features[i] as Feature;
            TryAddFlatPattern(feature, seen, result);
          }
        }
      }
      catch
      {
      }

      // Дополнительный обход дерева: FlatPattern иногда только под папкой.
      try
      {
        Feature feature = modelDoc.FirstFeature() as Feature;
        while (feature != null)
        {
          TryAddFlatPattern(feature, seen, result);
          feature = feature.GetNextFeature() as Feature;
        }
      }
      catch
      {
      }

      return result;
    }

    private static void TryAddFlatPattern(
        Feature feature,
        HashSet<string> seen,
        List<Feature> result)
    {
      if (feature == null)
        return;

      string typeName;
      try
      {
        typeName = feature.GetTypeName2() ?? feature.GetTypeName();
      }
      catch
      {
        try
        {
          typeName = feature.GetTypeName();
        }
        catch
        {
          return;
        }
      }

      if (!string.Equals(typeName, "FlatPattern", StringComparison.OrdinalIgnoreCase))
        return;

      string key;
      try
      {
        key = (feature.Name ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(key))
          key = "fp#" + result.Count;
      }
      catch
      {
        key = "fp#" + result.Count;
      }

      if (!seen.Add(key))
        return;

      result.Add(feature);
    }

    /// <summary>
    /// Снимает погашение FlatPattern (если нужно) и перестраивает модель,
    /// чтобы экспорт развёртки не отдал согнутый контур.
    /// </summary>
    internal static bool TryEnsureFlatPatternReady(
        ModelDoc2 modelDoc,
        Feature flatPattern,
        out bool wasSuppressed,
        out string error)
    {
      wasSuppressed = false;
      error = string.Empty;
      if (modelDoc == null || flatPattern == null)
      {
        error = "Нет feature развёртки.";
        return false;
      }

      try
      {
        bool ready = false;
        string ensureError = string.Empty;
        bool suppressed = false;

        // Unsuppress + rebuild не должны писать pending «устарел DXF».
        VelumExportDocumentationGeometryStampHelper.RunWithGeometryPendingStampSyncSuppressed(() =>
        {
          suppressed = IsFlatPatternSuppressed(flatPattern);
          if (!suppressed)
          {
            ready = true;
            return;
          }

          // Только текущая/родительская конфига: swAllConfiguration гасит и SM-FLAT-PATTERN,
          // а она нужна развёрнутой для чертежа.
          IReadOnlyList<string> parentConfigs = TryGetParentConfigurationNames(modelDoc);
          if (parentConfigs.Count == 0)
            parentConfigs = VelumDxfArtifactResolver.TryGetConfigurationNames(modelDoc);
          TrySetFlatPatternSuppression(flatPattern, suppress: false, parentConfigs);
          TryForceRebuild(modelDoc);

          Feature fresh = TryFindFlatPatternByName(modelDoc, flatPattern.Name) ?? flatPattern;
          if (IsFlatPatternSuppressed(fresh))
          {
            ensureError = "Развёртка осталась погашенной после перестроения. Проверьте конфигурацию листового металла.";
            ready = false;
            return;
          }

          ready = true;
        });

        wasSuppressed = suppressed;
        error = ensureError;
        return ready;
      }
      catch (Exception ex)
      {
        error = ex.Message;
        return false;
      }
    }

    /// <summary>Возвращает FlatPattern в погашенное состояние после экспорта DXF.</summary>
    internal static void TryRestoreFlatPatternSuppression(
        ModelDoc2 modelDoc,
        Feature flatPattern,
        bool wasSuppressed)
    {
      if (!wasSuppressed)
        return;

      TryRestoreFoldedDisplay(modelDoc, null);
    }

    /// <summary>
    /// После экспорта развёртки: вернуть родительскую конфигурацию и свернуть FlatPattern
    /// только в главных конфигурациях. Производные SM-FLAT-PATTERN оставляем развёрнутыми —
    /// именно они по умолчанию идут в чертёж.
    /// </summary>
    internal static void TryRestoreFoldedDisplay(
        ModelDoc2 modelDoc,
        string preferredConfigurationName)
    {
      if (modelDoc == null)
        return;

      try
      {
        string target = (preferredConfigurationName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(target))
          target = VelumDxfArtifactResolver.TryGetActiveConfigurationName(modelDoc);

        if (IsSheetMetalFlatPatternConfiguration(modelDoc, target))
          target = TryMapFlatPatternToParentConfigurationName(modelDoc, target);

        if (!string.IsNullOrWhiteSpace(target))
          VelumDxfFileNameHelper.TryActivateConfiguration(modelDoc, target);

        IReadOnlyList<string> parentConfigs = TryGetParentConfigurationNames(modelDoc);
        if (parentConfigs.Count == 0)
          parentConfigs = VelumDxfArtifactResolver.TryGetConfigurationNames(modelDoc);
        IReadOnlyList<string> flatPatternConfigs = TryGetSheetMetalFlatPatternConfigurationNames(modelDoc);
        IReadOnlyList<Feature> flatPatterns = CollectFlatPatternFeatures(modelDoc);
        ApplyFoldedDisplayToFeatures(flatPatterns, parentConfigs, flatPatternConfigs, selectFirst: false);
        TryForceRebuild(modelDoc);

        // Повтор, если SW оставил feature развёрнутой в родительской конфиге
        // (типично после первой выгрузки).
        flatPatterns = CollectFlatPatternFeatures(modelDoc);
        if (IsAnyFlatPatternUnsuppressedInThisConfiguration(flatPatterns))
        {
          ApplyFoldedDisplayToFeatures(flatPatterns, parentConfigs, flatPatternConfigs, selectFirst: true);
          if (!string.IsNullOrWhiteSpace(target))
            VelumDxfFileNameHelper.TryActivateConfiguration(modelDoc, target);
          TryForceRebuild(modelDoc);
        }

        try
        {
          modelDoc.GraphicsRedraw2();
        }
        catch
        {
        }
      }
      catch
      {
      }
    }

    private static void ApplyFoldedDisplayToFeatures(
        IReadOnlyList<Feature> flatPatterns,
        IReadOnlyList<string> parentConfigs,
        IReadOnlyList<string> flatPatternConfigs,
        bool selectFirst)
    {
      if (flatPatterns == null)
        return;

      for (int i = 0; i < flatPatterns.Count; i++)
      {
        Feature flatPattern = flatPatterns[i];
        if (flatPattern == null)
          continue;

        if (selectFirst)
        {
          try
          {
            flatPattern.Select2(false, 0);
          }
          catch
          {
          }
        }

        TrySetFlatPatternSuppression(flatPattern, suppress: true, parentConfigs);
        TrySetFlatPatternSuppression(flatPattern, suppress: false, flatPatternConfigs);
      }
    }

    private static bool IsAnyFlatPatternUnsuppressedInThisConfiguration(IReadOnlyList<Feature> flatPatterns)
    {
      if (flatPatterns == null)
        return false;

      for (int i = 0; i < flatPatterns.Count; i++)
      {
        if (!IsFlatPatternSuppressed(flatPatterns[i]))
          return true;
      }

      return false;
    }

    /// <summary>
    /// Главные (родительские) конфигурации: без родителя и не служебные SM-FLAT-PATTERN.
    /// </summary>
    internal static IReadOnlyList<string> TryGetParentConfigurationNames(ModelDoc2 modelDoc)
    {
      return CollectConfigurationNames(modelDoc, parentConfigsOnly: true, flatPatternConfigsOnly: false);
    }

    /// <summary>Служебные производные конфигурации развёртки (тип SheetMetal / *SM-FLAT-PATTERN).</summary>
    internal static IReadOnlyList<string> TryGetSheetMetalFlatPatternConfigurationNames(ModelDoc2 modelDoc)
    {
      return CollectConfigurationNames(modelDoc, parentConfigsOnly: false, flatPatternConfigsOnly: true);
    }

    /// <summary>
    /// Сигнатура погашения FlatPattern в активной конфигурации.
    /// Свернуть/развернуть развёртку меняет её, геометрия детали — нет.
    /// </summary>
    internal static string ComputeFlatPatternFoldSignature(ModelDoc2 modelDoc)
    {
      if (modelDoc == null || !IsSheetMetalPart(modelDoc))
        return string.Empty;

      string config = VelumDxfArtifactResolver.TryGetActiveConfigurationName(modelDoc) ?? string.Empty;
      IReadOnlyList<Feature> flatPatterns = CollectFlatPatternFeatures(modelDoc);
      var sb = new System.Text.StringBuilder(config);
      for (int i = 0; i < flatPatterns.Count; i++)
      {
        Feature feature = flatPatterns[i];
        if (feature == null)
          continue;

        string name;
        try
        {
          name = feature.Name ?? string.Empty;
        }
        catch
        {
          name = "#" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        sb.Append('|').Append(name).Append('=');
        sb.Append(IsFlatPatternSuppressed(feature) ? '1' : '0');
      }

      return sb.ToString();
    }

    private static IReadOnlyList<string> CollectConfigurationNames(
        ModelDoc2 modelDoc,
        bool parentConfigsOnly,
        bool flatPatternConfigsOnly)
    {
      var names = new List<string>();
      if (modelDoc == null)
        return names;

      try
      {
        string[] raw = modelDoc.GetConfigurationNames() as string[];
        if (raw == null)
          return names;

        for (int i = 0; i < raw.Length; i++)
        {
          string name = (raw[i] ?? string.Empty).Trim();
          if (string.IsNullOrWhiteSpace(name))
            continue;

          bool isFlat = IsSheetMetalFlatPatternConfiguration(modelDoc, name);
          if (flatPatternConfigsOnly)
          {
            if (isFlat)
              names.Add(name);
            continue;
          }

          if (isFlat)
            continue;

          if (parentConfigsOnly && !IsParentConfiguration(modelDoc, name))
            continue;

          names.Add(name);
        }
      }
      catch
      {
      }

      return names;
    }

    private static bool IsParentConfiguration(ModelDoc2 modelDoc, string configName)
    {
      string name = (configName ?? string.Empty).Trim();
      if (modelDoc == null || string.IsNullOrWhiteSpace(name))
        return false;

      try
      {
        SolidWorks.Interop.sldworks.Configuration conf =
            modelDoc.GetConfigurationByName(name) as SolidWorks.Interop.sldworks.Configuration;
        if (conf == null)
          return true;

        SolidWorks.Interop.sldworks.Configuration parent =
            conf.GetParent() as SolidWorks.Interop.sldworks.Configuration;
        return parent == null;
      }
      catch
      {
        return true;
      }
    }

    private static void TrySetFlatPatternSuppression(
        Feature flatPattern,
        bool suppress,
        IReadOnlyList<string> configNames)
    {
      if (flatPattern == null || configNames == null || configNames.Count == 0)
        return;

      var names = new string[configNames.Count];
      int count = 0;
      for (int i = 0; i < configNames.Count; i++)
      {
        string name = (configNames[i] ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
          continue;
        names[count++] = name;
      }

      if (count == 0)
        return;

      if (count != names.Length)
      {
        var trimmed = new string[count];
        Array.Copy(names, trimmed, count);
        names = trimmed;
      }

      int action = suppress
          ? (int)swFeatureSuppressionAction_e.swSuppressFeature
          : (int)swFeatureSuppressionAction_e.swUnSuppressFeature;

      try
      {
        flatPattern.SetSuppression2(
            action,
            (int)swInConfigurationOpts_e.swSpecifyConfiguration,
            names);
      }
      catch
      {
      }
    }

    /// <summary>Совместимость со старым вызовом по имени конфигурации + wasSuppressed.</summary>
    internal static void TryRestoreFlatPatternState(
        ModelDoc2 modelDoc,
        string configurationName,
        bool wasSuppressed)
    {
      if (wasSuppressed || IsSheetMetalFlatPatternConfiguration(
              modelDoc,
              VelumDxfArtifactResolver.TryGetActiveConfigurationName(modelDoc)))
      {
        TryRestoreFoldedDisplay(modelDoc, configurationName);
        return;
      }

      // Даже если IsSuppressed «врал» (часто на первой выгрузке) — сворачиваем,
      // если стартовали не из служебной SM-FLAT-PATTERN.
      if (!IsSheetMetalFlatPatternConfiguration(modelDoc, configurationName))
        TryRestoreFoldedDisplay(modelDoc, configurationName);
    }

    internal static bool IsFlatPatternSuppressed(Feature flatPattern)
    {
      if (flatPattern == null)
        return true;

      try
      {
        object raw = flatPattern.IsSuppressed2(
            (int)swInConfigurationOpts_e.swThisConfiguration,
            null);
        bool[] flags = raw as bool[];
        if (flags != null && flags.Length > 0)
          return flags[0];
      }
      catch
      {
      }

      try
      {
        return flatPattern.IsSuppressed();
      }
      catch
      {
        return true;
      }
    }

    private static Feature TryFindFlatPatternByName(ModelDoc2 modelDoc, string name)
    {
      string wanted = (name ?? string.Empty).Trim();
      if (modelDoc == null || string.IsNullOrWhiteSpace(wanted))
        return null;

      IReadOnlyList<Feature> all = CollectFlatPatternFeatures(modelDoc);
      for (int i = 0; i < all.Count; i++)
      {
        Feature feature = all[i];
        if (feature == null)
          continue;

        try
        {
          if (string.Equals(feature.Name, wanted, StringComparison.OrdinalIgnoreCase))
            return feature;
        }
        catch
        {
        }
      }

      return null;
    }

    private static void TryForceRebuild(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return;

      try
      {
        modelDoc.ForceRebuild3(false);
      }
      catch
      {
        try
        {
          modelDoc.EditRebuild3();
        }
        catch
        {
        }
      }
    }

    /// <summary>
    /// Служебная конфигурация развёртки SW (тип SheetMetal / имя *SM-FLAT-PATTERN).
    /// Не является пользовательской конфигурацией для DXF/материалов.
    /// </summary>
    internal static bool IsSheetMetalFlatPatternConfiguration(ModelDoc2 modelDoc, string configName)
    {
      string name = (configName ?? string.Empty).Trim();
      if (modelDoc == null || string.IsNullOrWhiteSpace(name))
        return false;

      try
      {
        SolidWorks.Interop.sldworks.Configuration conf =
            modelDoc.GetConfigurationByName(name) as SolidWorks.Interop.sldworks.Configuration;
        if (conf != null && conf.Type == (int)swConfigurationType_e.swConfiguration_SheetMetal)
          return true;
      }
      catch
      {
      }

      return name.IndexOf(FlatPatternConfigNameMarker, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Родительская (складная) конфигурация для служебной развёртки; иначе исходное имя.
    /// </summary>
    internal static string TryMapFlatPatternToParentConfigurationName(ModelDoc2 modelDoc, string configName)
    {
      string name = (configName ?? string.Empty).Trim();
      if (modelDoc == null || string.IsNullOrWhiteSpace(name))
        return name;

      if (!IsSheetMetalFlatPatternConfiguration(modelDoc, name))
        return name;

      try
      {
        SolidWorks.Interop.sldworks.Configuration conf =
            modelDoc.GetConfigurationByName(name) as SolidWorks.Interop.sldworks.Configuration;
        for (int depth = 0; conf != null && depth < 8; depth++)
        {
          SolidWorks.Interop.sldworks.Configuration parent =
              conf.GetParent() as SolidWorks.Interop.sldworks.Configuration;
          if (parent == null)
            break;

          string parentName = (parent.Name ?? string.Empty).Trim();
          if (string.IsNullOrWhiteSpace(parentName))
            break;

          if (!IsSheetMetalFlatPatternConfiguration(modelDoc, parentName))
            return parentName;

          conf = parent;
          name = parentName;
        }
      }
      catch
      {
      }

      return name;
    }
  }
}
