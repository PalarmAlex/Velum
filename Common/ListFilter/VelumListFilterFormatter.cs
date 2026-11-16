using System.Collections.Generic;
using System.Text;

namespace Velum.UI
{
  /// <summary>
  /// Форматирование DSL-строки фильтра (<see cref="VelumListFilterHelper"/>) в человекочитаемый вид
  /// для шапки отчётов: маска префикса + токены через запятую (например <c>=фильтр1, фильтр2</c>).
  /// Чистый класс без зависимостей от WinForms/SolidWorks.
  /// </summary>
  internal static class VelumListFilterFormatter
  {
    /// <summary>
    /// Преобразует выражение фильтра в строку «маска + токены через запятую».
    /// Пустое выражение даёт пустую строку.
    /// </summary>
    internal static string Format(string expression)
    {
      string expr = (expression ?? string.Empty).Trim();
      if (expr.Length == 0)
        return string.Empty;

      string prefix;
      string payload;
      SplitPrefix(expr, out prefix, out payload);

      List<string> tokens = SplitTokens(payload);
      if (tokens.Count == 0)
        return string.Empty;

      var sb = new StringBuilder();
      sb.Append(prefix);
      sb.Append(string.Join(", ", tokens.ToArray()));
      return sb.ToString();
    }

    /// <summary>
    /// Разбирает префикс-маску по тому же порядку, что и <see cref="VelumListFilterHelper"/>
    /// (двухсимвольные операторы раньше односимвольных).
    /// </summary>
    private static void SplitPrefix(string expr, out string prefix, out string payload)
    {
      if (StartsWith(expr, "!="))
      {
        prefix = "!=";
        payload = expr.Substring(2);
        return;
      }

      if (StartsWith(expr, ">="))
      {
        prefix = ">=";
        payload = expr.Substring(2);
        return;
      }

      if (StartsWith(expr, "<="))
      {
        prefix = "<=";
        payload = expr.Substring(2);
        return;
      }

      char first = expr[0];
      // Маска «*» (вхождение) эквивалентна отсутствию оператора — не выводим её,
      // чтобы «*сталь» и «сталь» отображались одинаково.
      if (first == '*')
      {
        prefix = string.Empty;
        payload = expr.Substring(1);
        return;
      }

      if (first == '!' || first == '=' || first == '>' || first == '<')
      {
        prefix = first.ToString();
        payload = expr.Substring(1);
        return;
      }

      prefix = string.Empty;
      payload = expr;
    }

    private static bool StartsWith(string text, string prefix)
    {
      return text.Length >= prefix.Length &&
          string.Compare(text, 0, prefix, 0, prefix.Length, System.StringComparison.Ordinal) == 0;
    }

    private static List<string> SplitTokens(string payload)
    {
      var tokens = new List<string>();
      if (string.IsNullOrEmpty(payload))
        return tokens;

      string[] parts = payload.Split('|');
      for (int i = 0; i < parts.Length; i++)
      {
        string token = (parts[i] ?? string.Empty).Trim();
        if (token.Length > 0)
          tokens.Add(token);
      }

      return tokens;
    }
  }
}
