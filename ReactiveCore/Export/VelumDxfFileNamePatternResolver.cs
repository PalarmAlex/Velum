using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SolidWorks.Interop.sldworks;
using Velum.ReactiveCore;

namespace Velum.ReactiveCore.Export
{
  /// <summary>
  /// Разрешение текстового шаблона имени DXF: в строке — имена свойств детали,
  /// при подстановке — их значения (без учёта регистра).
  /// </summary>
  internal static class VelumDxfFileNamePatternResolver
  {
    private static readonly Regex LegacyPrpRegex = new Regex(
        @"\$PRP:\s*""([^""]+)""",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase);

    internal static string NormalizeSavedPattern(string saved)
    {
      if (string.IsNullOrWhiteSpace(saved))
        return string.Empty;

      string text = saved.Trim();
      if (text.IndexOf("$PRP:", StringComparison.OrdinalIgnoreCase) >= 0)
      {
        Match match = LegacyPrpRegex.Match(text);
        if (match.Success)
          text = match.Groups[1].Value.Trim();
      }

      return text;
    }

    internal static IReadOnlyList<string> CollectReferencedPropertyNames(
        string pattern,
        IEnumerable<string> candidatePropertyNames)
    {
      var found = new List<string>();
      if (string.IsNullOrEmpty(pattern) || candidatePropertyNames == null)
        return found;

      var ordered = candidatePropertyNames
          .Where(n => !string.IsNullOrWhiteSpace(n))
          .Select(n => n.Trim())
          .Distinct(StringComparer.OrdinalIgnoreCase)
          .OrderByDescending(n => n.Length)
          .ToList();
      if (ordered.Count == 0)
        return found;

      var foundSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      int index = 0;
      while (index < pattern.Length)
      {
        string matchedName = TryMatchPropertyNameAt(pattern, index, ordered);
        if (matchedName == null)
        {
          index++;
          continue;
        }

        if (foundSet.Add(matchedName))
          found.Add(matchedName);

        index += matchedName.Length;
      }

      return found;
    }

    internal static IReadOnlyList<string> CollectReferencedSuffixPropertyNames(string pattern)
    {
      IReadOnlyList<string> raw =
          CollectReferencedPropertyNames(pattern, VelumDxfFileNameSuffixRegistry.GetSuffixes());
      if (raw.Count == 0)
        return raw;

      var filtered = new List<string>(raw.Count);
      for (int i = 0; i < raw.Count; i++)
      {
        if (!VelumDxfQuantityToken.IsQuantityToken(raw[i]))
          filtered.Add(raw[i]);
      }

      return filtered;
    }

    internal static string ResolvePatternToDisplay(
        string pattern,
        ModelDoc2 modelDoc,
        IReadOnlyDictionary<string, string> context)
    {
      if (string.IsNullOrEmpty(pattern))
        return string.Empty;

      int quantity = VelumDxfQuantityToken.ResolveQuantity(context);
      string working = VelumDxfQuantityToken.SubstituteInPattern(pattern, quantity);

      if (VelumRecipeTemplateResolver.ContainsUnresolvedTemplateTokens(working))
      {
        return VelumRecipeTemplateResolver.ResolveForFileName(working, modelDoc, context) ?? string.Empty;
      }

      IList<string> propertyNames = VelumDxfFileNameHelper.CollectCustomPropertyNames(modelDoc);
      if (propertyNames.Count == 0)
        return working;

      var ordered = propertyNames
          .Where(n => !string.IsNullOrWhiteSpace(n))
          .OrderByDescending(n => n.Length)
          .ToList();

      var result = new StringBuilder(working.Length);
      int index = 0;
      while (index < working.Length)
      {
        string matchedName = TryMatchPropertyNameAt(working, index, ordered);
        if (matchedName == null)
        {
          result.Append(working[index]);
          index++;
          continue;
        }

        string value = ReadPropertyValue(modelDoc, context, matchedName);
        if (!string.IsNullOrEmpty(value))
          result.Append(value);

        index += matchedName.Length;
      }

      return result.ToString();
    }

    private static string TryMatchPropertyNameAt(string pattern, int index, IList<string> orderedPropertyNames)
    {
      foreach (string propertyName in orderedPropertyNames)
      {
        if (index + propertyName.Length > pattern.Length)
          continue;

        if (string.Compare(
                pattern,
                index,
                propertyName,
                0,
                propertyName.Length,
                StringComparison.OrdinalIgnoreCase) != 0)
          continue;

        return propertyName;
      }

      return null;
    }

    private static string ReadPropertyValue(
        ModelDoc2 modelDoc,
        IReadOnlyDictionary<string, string> context,
        string propertyName)
    {
      if (VelumRecipeTemplateResolver.TryResolvePropertyValueForFileName(
              propertyName,
              modelDoc,
              context,
              out string resolved) &&
          !string.IsNullOrWhiteSpace(resolved))
        return resolved.Trim();

      return string.Empty;
    }
  }
}
