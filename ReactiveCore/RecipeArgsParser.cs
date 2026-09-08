using System;
using System.Collections.Generic;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Разбор строки аргументов шага рецепта (<c>key=value; key2=value2</c>).
  /// </summary>
  public static class RecipeArgsParser
  {
    /// <summary>
    /// Парсит строку аргументов в словарь (регистр ключей не учитывается).
    /// </summary>
    public static Dictionary<string, string> Parse(string argsText)
    {
      var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      if (string.IsNullOrWhiteSpace(argsText))
        return dict;

      foreach (string segment in argsText.Split(new[] { ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
      {
        string line = segment.Trim();
        if (line.Length == 0)
          continue;

        int eq = line.IndexOf('=');
        if (eq <= 0)
          continue;

        string key = line.Substring(0, eq).Trim();
        string value = line.Substring(eq + 1).Trim();
        if (key.Length > 0)
          dict[key] = value;
      }

      return dict;
    }

    /// <summary>
    /// Возвращает значение аргумента или пустую строку.
    /// </summary>
    public static string Get(IReadOnlyDictionary<string, string> args, string key)
    {
      if (args == null || string.IsNullOrWhiteSpace(key))
        return string.Empty;

      foreach (KeyValuePair<string, string> kv in args)
      {
        if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
          return kv.Value ?? string.Empty;
      }

      return string.Empty;
    }
  }
}
