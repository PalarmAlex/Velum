using System;

namespace Velum.ReactiveCore.Export
{
  /// <summary>
  /// Pure-правила имён DXF: операции над строками (prefix/суффикс маски, обязательный
  /// префикс конфигурации) без COM, файлов и внешних зависимостей.
  /// Линкуется в тестовый проект Velum.ReactiveCore.Tests без основного резолвера.
  /// </summary>
  internal static class VelumDxfArtifactNameRules
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
  }
}
