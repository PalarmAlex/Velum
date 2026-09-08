using System;
using SolidWorks.Interop.swconst;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Типы пользовательских свойств SolidWorks (SW 2019: текст, число, дата, логическое).
  /// </summary>
  internal static class VelumSolidCustomPropertyTypes
  {
    internal const string TypeKeyText = "text";
    internal const string TypeKeyNumber = "number";
    internal const string TypeKeyDate = "date";
    internal const string TypeKeyBoolean = "boolean";

    /// <summary>Значение логического свойства SW (Yes/No).</summary>
    internal const string BooleanYes = "Yes";

    /// <summary>Значение логического свойства SW (Yes/No).</summary>
    internal const string BooleanNo = "No";

    internal static swCustomInfoType_e ResolveType(string propertyTypeKey)
    {
      string key = (propertyTypeKey ?? string.Empty).Trim();
      if (key.Length == 0)
        return swCustomInfoType_e.swCustomInfoText;

      if (string.Equals(key, TypeKeyText, StringComparison.OrdinalIgnoreCase))
        return swCustomInfoType_e.swCustomInfoText;

      if (string.Equals(key, TypeKeyNumber, StringComparison.OrdinalIgnoreCase))
        return swCustomInfoType_e.swCustomInfoNumber;

      if (string.Equals(key, TypeKeyDate, StringComparison.OrdinalIgnoreCase))
        return swCustomInfoType_e.swCustomInfoDate;

      if (string.Equals(key, TypeKeyBoolean, StringComparison.OrdinalIgnoreCase) ||
          string.Equals(key, "yes_no", StringComparison.OrdinalIgnoreCase) ||
          string.Equals(key, "logical", StringComparison.OrdinalIgnoreCase) ||
          string.Equals(key, "bool", StringComparison.OrdinalIgnoreCase))
        return swCustomInfoType_e.swCustomInfoYesOrNo;

      return swCustomInfoType_e.swCustomInfoText;
    }

    internal static string NormalizeValue(swCustomInfoType_e propertyType, string rawValue)
    {
      string value = rawValue ?? string.Empty;
      if (propertyType != swCustomInfoType_e.swCustomInfoYesOrNo)
        return value;

      if (TryParseBooleanString(value, out bool parsed))
        return parsed ? BooleanYes : BooleanNo;

      return value.Trim();
    }

    /// <summary>
    /// Разбирает строку логического свойства (SW Yes/No и устаревшие Да/Нет).
    /// </summary>
    internal static bool TryParseBooleanString(string rawValue, out bool parsed)
    {
      parsed = false;
      string v = (rawValue ?? string.Empty).Trim();
      if (v.Length == 0)
        return false;

      if (string.Equals(v, BooleanYes, StringComparison.OrdinalIgnoreCase) ||
          string.Equals(v, "Y", StringComparison.OrdinalIgnoreCase) ||
          string.Equals(v, "1", StringComparison.Ordinal) ||
          string.Equals(v, "true", StringComparison.OrdinalIgnoreCase) ||
          string.Equals(v, "Да", StringComparison.OrdinalIgnoreCase))
      {
        parsed = true;
        return true;
      }

      if (string.Equals(v, BooleanNo, StringComparison.OrdinalIgnoreCase) ||
          string.Equals(v, "N", StringComparison.OrdinalIgnoreCase) ||
          string.Equals(v, "0", StringComparison.Ordinal) ||
          string.Equals(v, "false", StringComparison.OrdinalIgnoreCase) ||
          string.Equals(v, "Нет", StringComparison.OrdinalIgnoreCase))
      {
        parsed = false;
        return true;
      }

      return false;
    }

    internal static bool IsYesOrNoType(swCustomInfoType_e propertyType) =>
        propertyType == swCustomInfoType_e.swCustomInfoYesOrNo;
  }
}
