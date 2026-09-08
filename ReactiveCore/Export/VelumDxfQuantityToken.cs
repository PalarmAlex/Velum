using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Velum.ReactiveCore.Export
{
  /// <summary>
  /// Плейсхолдеры кол-ва в маске имени DXF: <c>[Quantity]</c> и алиас <c>[Кол-во]</c>.
  /// Вне контекста изделия по умолчанию подставляется 1.
  /// </summary>
  internal static class VelumDxfQuantityToken
  {
    internal const string TokenQuantity = "[Quantity]";
    internal const string TokenQuantityRu = "[Кол-во]";
    internal const string ContextKey = "VELUM_DXF_QUANTITY";
    internal const int DefaultQuantity = 1;

    private static readonly Regex QtyInFileNameRegex = new Regex(
        @"-\s*(\d+)\s*шт\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase);

    internal static bool IsQuantityToken(string name)
    {
      if (string.IsNullOrWhiteSpace(name))
        return false;

      string trimmed = name.Trim();
      return string.Equals(trimmed, TokenQuantity, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(trimmed, TokenQuantityRu, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool PatternReferencesQuantity(string pattern)
    {
      if (string.IsNullOrEmpty(pattern))
        return false;

      return IndexOfToken(pattern, TokenQuantity) >= 0 ||
             IndexOfToken(pattern, TokenQuantityRu) >= 0;
    }

    /// <summary>Удаляет qty-токены из имени канонического DXF.</summary>
    internal static string StripQuantityTokens(string pattern)
    {
      string result = pattern ?? string.Empty;
      result = ReplaceToken(result, TokenQuantity, string.Empty);
      result = ReplaceToken(result, TokenQuantityRu, string.Empty);
      return result.Trim();
    }

    internal static int ResolveQuantity(IReadOnlyDictionary<string, string> context)
    {
      if (context != null &&
          context.TryGetValue(ContextKey, out string raw) &&
          int.TryParse((raw ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int qty) &&
          qty >= 0)
        return qty;

      return DefaultQuantity;
    }

    internal static int? TryResolveQuantity(IReadOnlyDictionary<string, string> context)
    {
      if (context != null &&
          context.TryGetValue(ContextKey, out string raw) &&
          int.TryParse((raw ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int qty) &&
          qty >= 0)
        return qty;
      return null;
    }

    internal static string FormatQuantity(int quantity)
    {
      if (quantity < 0)
        quantity = 0;
      return quantity.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Подставляет оба токена кол-ва в маску до резолва свойств.</summary>
    internal static string SubstituteInPattern(string pattern, int quantity)
    {
      if (string.IsNullOrEmpty(pattern) || !PatternReferencesQuantity(pattern))
        return pattern ?? string.Empty;

      string value = FormatQuantity(quantity);
      string result = ReplaceToken(pattern, TokenQuantity, value);
      result = ReplaceToken(result, TokenQuantityRu, value);
      return result;
    }

    internal static bool TryParseQuantityFromFileName(string fileNameWithoutExtension, out int quantity)
    {
      quantity = 0;
      if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
        return false;

      Match match = QtyInFileNameRegex.Match(fileNameWithoutExtension.Trim());
      if (!match.Success)
        return false;

      return int.TryParse(
          match.Groups[1].Value,
          NumberStyles.Integer,
          CultureInfo.InvariantCulture,
          out quantity);
    }

    /// <summary>
    /// Имя без хвоста «- Nшт» (для сопоставления префикса при устаревшем кол-ве).
    /// </summary>
    internal static string StripQuantitySuffix(string fileNameWithoutExtension)
    {
      if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
        return string.Empty;

      return QtyInFileNameRegex.Replace(fileNameWithoutExtension.Trim(), string.Empty).TrimEnd(' ', '-', '—', ',');
    }

    internal static IEnumerable<string> EnumerateTokenNames()
    {
      yield return TokenQuantity;
      yield return TokenQuantityRu;
    }

    private static int IndexOfToken(string text, string token)
    {
      return text.IndexOf(token, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReplaceToken(string text, string token, string value)
    {
      if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(token))
        return text ?? string.Empty;

      int index = 0;
      while (index < text.Length)
      {
        int found = text.IndexOf(token, index, StringComparison.OrdinalIgnoreCase);
        if (found < 0)
          break;

        text = text.Substring(0, found) + value + text.Substring(found + token.Length);
        index = found + value.Length;
      }

      return text;
    }
  }
}
