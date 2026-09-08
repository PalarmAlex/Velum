using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using Velum.ReactiveCore;

namespace Velum.ReactiveCore.Export
{
  /// <summary>
  /// «Нужен dxf»: вкладка конфигурации перекрывает общую; иначе fallback на document.
  /// Свойство на конфигурации есть, но Yes/No не распознан — No (общая вкладка не читается).
  /// </summary>
  internal static class VelumDxfNeedFlagResolver
  {
    /// <summary>
    /// Читает флаг для конфигурации. <paramref name="exists"/> — свойство найдено
    /// на вкладке конфигурации или на общей.
    /// </summary>
    internal static bool TryRead(ModelDoc2 modelDoc, string configName, out bool needDxf, out bool exists)
    {
      needDxf = false;
      exists = false;
      if (modelDoc == null)
        return false;

      string value;
      if (!VelumRecipeSolidWorksCustomProperties.TryReadConfigThenDocument(
              modelDoc,
              configName,
              VelumExportDocumentationProperties.NeedDxf,
              out value,
              out exists) ||
          !exists)
        return false;

      if (VelumSolidCustomPropertyTypes.TryParseBooleanString(value, out bool parsed))
        needDxf = parsed;
      else
        needDxf = false;

      return true;
    }

    /// <summary>true, если для конфигурации «Нужен dxf = Yes» (с fallback на document).</summary>
    internal static bool IsExportable(ModelDoc2 modelDoc, string configName)
    {
      bool needDxf;
      bool exists;
      return TryRead(modelDoc, configName, out needDxf, out exists) && exists && needDxf;
    }

    /// <summary>Пользовательские конфигурации с «Нужен dxf = Yes» (без SM-FLAT-PATTERN).</summary>
    internal static IReadOnlyList<string> CollectExportable(ModelDoc2 modelDoc)
    {
      var result = new List<string>();
      IReadOnlyList<string> configs = VelumDxfArtifactResolver.TryGetConfigurationNames(modelDoc);
      for (int i = 0; i < configs.Count; i++)
      {
        string name = configs[i];
        if (IsExportable(modelDoc, name))
          result.Add(name);
      }

      return result;
    }

    /// <summary>Число конфигураций, которые идут в DXF. Суффикс имени — только если больше одной.</summary>
    internal static int CountExportable(ModelDoc2 modelDoc)
    {
      return CollectExportable(modelDoc).Count;
    }

    /// <summary>true, если хотя бы одна конфигурация требует DXF.</summary>
    internal static bool HasAnyExportable(ModelDoc2 modelDoc)
    {
      return CountExportable(modelDoc) > 0;
    }

    /// <summary>true, если активная конфигурация требует DXF.</summary>
    internal static bool HasActiveConfigExportable(ModelDoc2 modelDoc)
    {
      string configName = VelumDxfArtifactResolver.TryGetActiveConfigurationName(modelDoc);
      if (string.IsNullOrWhiteSpace(configName))
        return false;

      return IsExportable(modelDoc, configName);
    }

    /// <summary>
    /// Счётчик для префикса имени: экспортные конфигурации; если их нет —
    /// все пользовательские (поиск мусорных файлов со старыми именами).
    /// </summary>
    internal static int ResolvePrefixConfigurationCount(ModelDoc2 modelDoc)
    {
      int exportable = CountExportable(modelDoc);
      if (exportable > 0)
        return exportable;

      IReadOnlyList<string> all = VelumDxfArtifactResolver.TryGetConfigurationNames(modelDoc);
      return all.Count;
    }
  }
}
