using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security;
using ISIDA.Common;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Чтение и запись пользовательских свойств SolidWorks для шагов рецепта.
  /// </summary>
  internal static class VelumRecipeSolidWorksCustomProperties
  {
    /// <summary>
    /// Разрешает ключ конфигурации из параметра шага (<c>active</c>, <c>document</c>, имя конфигурации).
    /// </summary>
    public static string ResolveConfigurationKey(ModelDoc2 modelDoc, string configParameter)
    {
      string p = (configParameter ?? string.Empty).Trim();
      if (p.Length == 0 ||
          string.Equals(p, "document", StringComparison.OrdinalIgnoreCase))
        return string.Empty;

      if (string.Equals(p, "default", StringComparison.OrdinalIgnoreCase))
        return TryGetDefaultConfigurationName(modelDoc) ?? string.Empty;

      if (!string.Equals(p, "active", StringComparison.OrdinalIgnoreCase))
        return p;

      try
      {
        ConfigurationManager cm = modelDoc?.ConfigurationManager;
        SolidWorks.Interop.sldworks.Configuration active = cm?.ActiveConfiguration;
        string name = active?.Name;
        return string.IsNullOrWhiteSpace(name) ? string.Empty : name;
      }
      catch
      {
        return string.Empty;
      }
    }

    /// <summary>
    /// Возвращает менеджер свойств для конфигурации или <c>null</c>.
    /// </summary>
    public static CustomPropertyManager TryGetManager(ModelDoc2 modelDoc, string configParameter)
    {
      if (modelDoc?.Extension == null)
        return null;

      try
      {
        string configKey = ResolveConfigurationKey(modelDoc, configParameter);
        return modelDoc.Extension.CustomPropertyManager[configKey];
      }
      catch
      {
        return null;
      }
    }

    /// <summary>
    /// Читает сырое и вычисленное значение через <c>Get2</c> (без <c>Get6</c>).
    /// </summary>
    [HandleProcessCorruptedStateExceptions]
    [SecurityCritical]
    public static bool TryGetRawAndResolved(
        CustomPropertyManager cpm,
        string propertyName,
        out string rawValue,
        out string resolvedValue)
    {
      rawValue = string.Empty;
      resolvedValue = string.Empty;
      if (cpm == null || string.IsNullOrWhiteSpace(propertyName))
        return false;

      if (!TryResolvePropertyName(cpm, propertyName, out string canonicalName))
        return false;

      try
      {
        string valOut;
        string resolvedValOut;
        cpm.Get2(canonicalName, out valOut, out resolvedValOut);
        rawValue = valOut ?? string.Empty;
        resolvedValue = resolvedValOut ?? string.Empty;
        if (string.IsNullOrEmpty(resolvedValue) && !string.IsNullOrEmpty(rawValue))
          resolvedValue = rawValue;
        return true;
      }
      catch (AccessViolationException ex)
      {
        Logger.Warning(
            "Velum CPM TryGetRawAndResolved AV name=\"" + canonicalName + "\": " + ex.Message);
        if (TryGetValueViaGet(cpm, canonicalName, out string legacy))
        {
          rawValue = legacy;
          resolvedValue = legacy;
          return true;
        }

        return false;
      }
      catch (SEHException ex)
      {
        Logger.Warning(
            "Velum CPM TryGetRawAndResolved SEH name=\"" + canonicalName + "\": " + ex.Message);
        if (TryGetValueViaGet(cpm, canonicalName, out string legacy))
        {
          rawValue = legacy;
          resolvedValue = legacy;
          return true;
        }

        return false;
      }
      catch
      {
        return false;
      }
    }

    /// <summary>
    /// Читает значение свойства (разрешённое, если доступно).
    /// </summary>
    /// <remarks>
    /// Не используем <c>Get6</c>: PIA XCad 32.x поверх SW 2019 на части установок даёт
    /// <see cref="AccessViolationException"/> при резолве <c>$PRP:</c> (особенно unsaved-документ).
    /// В .NET 4+ такой AV — CSE и не ловится обычным <c>catch</c> без
    /// <see cref="HandleProcessCorruptedStateExceptionsAttribute"/>.
    /// </remarks>
    [HandleProcessCorruptedStateExceptions]
    [SecurityCritical]
    public static bool TryGetValue(CustomPropertyManager cpm, string propertyName, out string value)
    {
      value = string.Empty;
      if (cpm == null || string.IsNullOrWhiteSpace(propertyName))
        return false;

      if (!TryResolvePropertyName(cpm, propertyName, out string canonicalName))
        return false;

      try
      {
        if (TryGetValueViaGet2(cpm, canonicalName, out value))
        {
          if (string.IsNullOrEmpty(value) &&
              VelumSolidCustomPropertyTypes.IsYesOrNoType(TryGetPropertyType(cpm, canonicalName)) &&
              TryGetValueViaGet(cpm, canonicalName, out string legacyValue) &&
              !string.IsNullOrWhiteSpace(legacyValue))
          {
            value = legacyValue;
          }

          return true;
        }

        return TryGetValueViaGet(cpm, canonicalName, out value);
      }
      catch (AccessViolationException ex)
      {
        Logger.Warning(
            "Velum CPM TryGetValue AV name=\"" + canonicalName + "\": " + ex.Message);
        return TryGetValueViaGet(cpm, canonicalName, out value);
      }
      catch (SEHException ex)
      {
        Logger.Warning(
            "Velum CPM TryGetValue SEH name=\"" + canonicalName + "\": " + ex.Message);
        return TryGetValueViaGet(cpm, canonicalName, out value);
      }
      catch
      {
        return false;
      }
    }

    /// <summary>
    /// Читает логическое свойство (SW Yes/No); для текстового legacy — Да/Нет, Yes/No.
    /// </summary>
    public static bool TryGetBooleanValue(
        CustomPropertyManager cpm,
        string propertyName,
        out bool? value)
    {
      value = null;
      if (cpm == null || string.IsNullOrWhiteSpace(propertyName))
        return false;

      if (!TryResolvePropertyName(cpm, propertyName, out string canonicalName))
        return false;

      if (!TryGetValue(cpm, canonicalName, out string raw))
        return false;

      if (VelumSolidCustomPropertyTypes.TryParseBooleanString(raw, out bool parsed))
      {
        value = parsed;
        return true;
      }

      return false;
    }

    /// <summary>Тип свойства по <c>GetType2</c>; при ошибке — текст.</summary>
    public static swCustomInfoType_e TryGetPropertyType(CustomPropertyManager cpm, string canonicalName)
    {
      if (cpm == null || string.IsNullOrWhiteSpace(canonicalName))
        return swCustomInfoType_e.swCustomInfoText;

      try
      {
        int typeCode = cpm.GetType2(canonicalName);
        if (Enum.IsDefined(typeof(swCustomInfoType_e), typeCode))
          return (swCustomInfoType_e)typeCode;
      }
      catch
      {
      }

      return swCustomInfoType_e.swCustomInfoText;
    }

    /// <summary>
    /// Ключи конфигурации для записи: <c>document</c> — уровень документа;
    /// <c>active</c> — только активная конфигурация.
    /// </summary>
    public static IEnumerable<string> EnumerateWriteConfigurationKeys(
        ModelDoc2 modelDoc,
        string configParameter)
    {
      string p = (configParameter ?? string.Empty).Trim();
      if (p.Length == 0 ||
          string.Equals(p, "document", StringComparison.OrdinalIgnoreCase))
      {
        yield return string.Empty;
        yield break;
      }

      if (string.Equals(p, "default", StringComparison.OrdinalIgnoreCase))
      {
        string defaultName = TryGetDefaultConfigurationName(modelDoc);
        if (!string.IsNullOrWhiteSpace(defaultName))
          yield return defaultName;
        yield break;
      }

      if (string.Equals(p, "active", StringComparison.OrdinalIgnoreCase))
      {
        string active = ResolveConfigurationKey(modelDoc, "active");
        if (!string.IsNullOrWhiteSpace(active))
          yield return active;
        yield break;
      }

      yield return ResolveConfigurationKey(modelDoc, p);
    }

    /// <summary>
    /// Проверяет наличие свойства в менеджере (через <c>GetNames</c>, без <c>Get6</c>).
    /// </summary>
    public static bool TryPropertyExists(CustomPropertyManager cpm, string propertyName)
    {
      return TryResolvePropertyName(cpm, propertyName, out _);
    }

    /// <summary>
    /// Читает свойство: сначала вкладка конфигурации — если свойство там есть
    /// (даже пустое), document не используется; иначе главная вкладка документа.
    /// </summary>
    /// <param name="modelDoc">Документ SolidWorks.</param>
    /// <param name="configurationName">Имя конфигурации вхождения; пусто — только document.</param>
    /// <param name="propertyName">Имя пользовательского свойства.</param>
    /// <param name="value">Значение (может быть пустым при exists=true).</param>
    /// <param name="exists">true, если свойство найдено на выбранной вкладке.</param>
    public static bool TryReadConfigThenDocument(
        ModelDoc2 modelDoc,
        string configurationName,
        string propertyName,
        out string value,
        out bool exists)
    {
      value = string.Empty;
      exists = false;
      if (modelDoc == null || string.IsNullOrWhiteSpace(propertyName))
        return false;

      string config = (configurationName ?? string.Empty).Trim();
      if (config.Length > 0)
      {
        CustomPropertyManager configCpm = TryGetManager(modelDoc, config);
        if (configCpm != null && TryPropertyExists(configCpm, propertyName))
        {
          exists = true;
          TryGetValue(configCpm, propertyName, out value);
          value = value ?? string.Empty;
          return true;
        }
      }

      CustomPropertyManager docCpm = TryGetManager(modelDoc, "document");
      if (docCpm != null && TryPropertyExists(docCpm, propertyName))
      {
        exists = true;
        TryGetValue(docCpm, propertyName, out value);
        value = value ?? string.Empty;
        return true;
      }

      return false;
    }

    /// <summary>
    /// Записывает свойство с учётом политики <paramref name="overwrite"/> и типа SW.
    /// Отсутствующее свойство создаётся через <c>Add3</c>, существующее обновляется через <c>Set2</c>/<c>Add3</c>.
    /// </summary>
    public static bool TrySetValue(
        CustomPropertyManager cpm,
        string propertyName,
        string newValue,
        string overwrite,
        string propertyTypeKey,
        out bool skipped,
        out string message)
    {
      skipped = false;
      message = string.Empty;

      if (cpm == null || string.IsNullOrWhiteSpace(propertyName))
      {
        message = "property_manager_or_name_missing";
        return false;
      }

      swCustomInfoType_e propertyType = VelumSolidCustomPropertyTypes.ResolveType(propertyTypeKey);
      int propertyTypeInt = (int)propertyType;

      string mode = NormalizeOverwriteMode(overwrite);
      string valueToWrite = VelumSolidCustomPropertyTypes.NormalizeValue(propertyType, newValue);
      bool exists = TryResolvePropertyName(cpm, propertyName, out string canonicalName);
      string writeName = exists ? canonicalName : propertyName.Trim();
      swCustomInfoType_e currentType = exists
          ? TryGetPropertyType(cpm, canonicalName)
          : propertyType;

      if (exists)
      {
        if (!TryGetValue(cpm, canonicalName, out string current))
        {
          if (!string.Equals(mode, "always", StringComparison.OrdinalIgnoreCase))
          {
            skipped = true;
            message = "overwrite_skipped:read_failed:" + mode;
            return true;
          }
        }
        else if (!ShouldOverwrite(current, valueToWrite, mode, currentType))
        {
          skipped = true;
          message = "overwrite_skipped:" + mode;
          return true;
        }
      }
      else if (string.Equals(mode, "never", StringComparison.OrdinalIgnoreCase))
      {
        skipped = true;
        message = "overwrite_skipped:never";
        return true;
      }

      try
      {
        if (exists)
        {
          if (currentType != propertyType)
          {
            int typeReplaceResult = cpm.Add3(
                writeName,
                propertyTypeInt,
                valueToWrite,
                (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);

            if (typeReplaceResult == 0)
            {
              message = "type_replace_ok";
              return true;
            }

            message = "type_replace_failed:" + typeReplaceResult;
            return false;
          }

          int setResult = cpm.Set2(writeName, valueToWrite);
          if (setResult == 0)
          {
            message = "set_ok";
            return true;
          }

          int replaceResult = cpm.Add3(
              writeName,
              propertyTypeInt,
              valueToWrite,
              (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);

          if (replaceResult == 0)
          {
            message = "replace_ok";
            return true;
          }

          message = "set_failed:" + setResult + ",replace:" + replaceResult;
          return false;
        }

        int addResult = cpm.Add3(
            writeName,
            propertyTypeInt,
            valueToWrite,
            (int)swCustomPropertyAddOption_e.swCustomPropertyOnlyIfNew);

        if (addResult == 0)
        {
          message = "add_ok";
          return true;
        }

        int addReplaceResult = cpm.Add3(
            writeName,
            propertyTypeInt,
            valueToWrite,
            (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);

        if (addReplaceResult == 0)
        {
          message = "add_replace_ok";
          return true;
        }

        message = "add_failed:" + addResult + ",replace:" + addReplaceResult;
        return false;
      }
      catch (Exception ex)
      {
        message = ex.Message;
        return false;
      }
    }

    private static bool ShouldOverwrite(
        string currentValue,
        string newValue,
        string mode,
        swCustomInfoType_e propertyType)
    {
      bool hasCurrent = !IsEffectivelyEmptyPropertyValue(currentValue, propertyType);

      if (string.Equals(mode, "always", StringComparison.OrdinalIgnoreCase))
        return true;

      if (string.Equals(mode, "if_empty", StringComparison.OrdinalIgnoreCase))
        return !hasCurrent;

      if (string.Equals(mode, "never", StringComparison.OrdinalIgnoreCase))
        return false;

      return !hasCurrent;
    }

    /// <summary>
    /// Пустое значение свойства: пробелы, «-», нерезолвленная SW-формула (<c>$PRP:</c>).
    /// Для логического типа No — не пусто.
    /// </summary>
    private static bool IsEffectivelyEmptyPropertyValue(string value, swCustomInfoType_e propertyType)
    {
      if (VelumSolidCustomPropertyTypes.IsYesOrNoType(propertyType))
      {
        if (VelumSolidCustomPropertyTypes.TryParseBooleanString(value, out _))
          return false;
      }

      return IsEffectivelyEmptyPropertyValue(value);
    }

    /// <summary>
    /// Пустое значение текстового/числового/дата свойства.
    /// </summary>
    private static bool IsEffectivelyEmptyPropertyValue(string value)
    {
      if (string.IsNullOrWhiteSpace(value))
        return true;

      string trimmed = value.Trim();
      if (string.Equals(trimmed, "-", StringComparison.Ordinal) ||
          string.Equals(trimmed, "—", StringComparison.Ordinal))
        return true;

      return VelumRecipeTemplateResolver.ContainsUnresolvedTemplateTokens(trimmed);
    }

    /// <summary>Чтение через <c>Get2</c> (сырое + вычисленное значение).</summary>
    private static bool TryGetValueViaGet2(CustomPropertyManager cpm, string canonicalName, out string value)
    {
      value = string.Empty;
      if (cpm == null || string.IsNullOrWhiteSpace(canonicalName))
        return false;

      try
      {
        string valOut;
        string resolvedValOut;
        cpm.Get2(canonicalName, out valOut, out resolvedValOut);

        value = !string.IsNullOrEmpty(resolvedValOut) ? resolvedValOut : valOut;
        if (string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(valOut))
          value = valOut;

        value = value ?? string.Empty;
        return true;
      }
      catch
      {
        return false;
      }
    }

    /// <summary>
    /// Чтение только сырого значения через <c>Get</c> — без резолва формул (безопаснее на unsaved).
    /// </summary>
    [HandleProcessCorruptedStateExceptions]
    [SecurityCritical]
    private static bool TryGetValueViaGet(CustomPropertyManager cpm, string canonicalName, out string value)
    {
      value = string.Empty;
      if (cpm == null || string.IsNullOrWhiteSpace(canonicalName))
        return false;

      try
      {
        string raw = cpm.Get(canonicalName);
        if (raw == null)
          return false;

        value = raw;
        return true;
      }
      catch (AccessViolationException)
      {
        return false;
      }
      catch (SEHException)
      {
        return false;
      }
      catch
      {
        return false;
      }
    }

    /// <summary>
    /// Возвращает точное имя свойства из <c>GetNames</c> (для <c>Get6</c>/<c>Set2</c>).
    /// </summary>
    internal static bool TryResolvePropertyName(
        CustomPropertyManager cpm,
        string propertyName,
        out string canonicalName)
    {
      canonicalName = null;
      if (cpm == null || string.IsNullOrWhiteSpace(propertyName))
        return false;

      try
      {
        string[] names = cpm.GetNames() as string[];
        if (names == null || names.Length == 0)
          return false;

        foreach (string name in names)
        {
          if (string.Equals(name, propertyName, StringComparison.OrdinalIgnoreCase))
          {
            canonicalName = name;
            return true;
          }
        }

        return false;
      }
      catch
      {
        return false;
      }
    }

    private static string TryGetDefaultConfigurationName(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return null;

      try
      {
        string[] names = modelDoc.GetConfigurationNames() as string[];
        if (names == null || names.Length == 0)
          return null;

        foreach (string name in names)
        {
          if (string.Equals(name, "Default", StringComparison.OrdinalIgnoreCase))
            return name;
        }

        return names[0];
      }
      catch
      {
        return null;
      }
    }

    /// <summary>
    /// Устаревшее <c>never_if_filled</c> трактуется как <c>if_empty</c> (старые рецепты в ProgramData).
    /// </summary>
    private static string NormalizeOverwriteMode(string overwrite)
    {
      string mode = (overwrite ?? "always").Trim();
      if (string.Equals(mode, "never_if_filled", StringComparison.OrdinalIgnoreCase))
        return "if_empty";
      return mode;
    }
  }
}
