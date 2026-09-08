using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Velum.ReactiveCore;

namespace Velum.UI
{
  /// <summary>
  /// Унифицированная фильтрация списков по expression-строке.
  /// OR внутри столбца (токены через <c>|</c>), AND между столбцами — на стороне вызывающего.
  /// </summary>
  /// <remarks>
  /// Операторы (префикс всей строки; порядок разбора):
  /// <c>!=</c> — не равно (exact);
  /// <c>!</c> — исключить вхождение (NOT LIKE);
  /// <c>&gt;=</c> / <c>&lt;=</c> / <c>&gt;</c> / <c>&lt;</c> — числовое сравнение;
  /// <c>=</c> — точное совпадение;
  /// <c>*</c> или без оператора — поиск по вхождению.
  /// </remarks>
  internal static class VelumListFilterHelper
  {
    internal const string SelectionIncludePrefix = "=";
    internal const string SelectionExcludePrefix = "!=";

    private enum FilterOperator
    {
      Contains,
      Equals,
      NotEquals,
      NotContains,
      Greater,
      GreaterOrEqual,
      Less,
      LessOrEqual
    }

    /// <summary>Текстовое сопоставление (без отдельного raw для чисел).</summary>
    internal static bool Matches(string value, string expression)
    {
      return Matches(value, expression, null);
    }

    /// <param name="value">Значение для текстовых операторов (обычно display).</param>
    /// <param name="expression">Строка фильтра из UI.</param>
    /// <param name="numericRaw">
    /// Сырое значение для <c>&gt;</c>/<c>&lt;</c>/<c>&gt;=</c>/<c>&lt;=</c>;
    /// если null — парсится <paramref name="value"/>.
    /// </param>
    internal static bool Matches(string value, string expression, string numericRaw)
    {
      string expr = (expression ?? string.Empty).Trim();
      if (expr.Length == 0)
        return true;

      FilterOperator op;
      string payload;
      string unusedPrefix;
      ParseOperator(expr, out op, out payload, out unusedPrefix);

      List<string> tokens = SplitTokens(payload);
      if (tokens.Count == 0)
        return true;

      string text = value ?? string.Empty;

      switch (op)
      {
        case FilterOperator.Equals:
          return MatchAnyExact(text, tokens);
        case FilterOperator.NotEquals:
          return !MatchAnyExact(text, tokens);
        case FilterOperator.NotContains:
          return !MatchAnyContains(text, tokens);
        case FilterOperator.Contains:
          return MatchAnyContains(text, tokens);
        case FilterOperator.Greater:
        case FilterOperator.GreaterOrEqual:
        case FilterOperator.Less:
        case FilterOperator.LessOrEqual:
          return MatchNumeric(op, text, numericRaw, tokens);
        default:
          return MatchAnyContains(text, tokens);
      }
    }

    /// <summary>
    /// Объединяет значения выделения с текущим выражением фильтра (<c>=</c> / <c>!=</c>).
    /// При совпадении маски — молча добавляет токены через <c>|</c> (без дубликатов).
    /// При другой маске — диалог Yes/No/Cancel:
    /// Yes — новая маска на старые токены + выделенное;
    /// No — заменить поле только новым фильтром по выделенному;
    /// Cancel — без изменений.
    /// Пустое поле — сразу <c>=values</c> / <c>!=values</c>.
    /// </summary>
    /// <returns><c>true</c>, если выражение нужно записать в поле.</returns>
    internal static bool TryMergeSelectionFilter(
        IWin32Window owner,
        string currentExpression,
        IList<string> selectedValues,
        bool exclude,
        out string newExpression)
    {
      newExpression = currentExpression ?? string.Empty;

      List<string> incoming = NormalizeUniqueValues(selectedValues);
      if (incoming.Count == 0)
        return false;

      string desiredPrefix = exclude ? SelectionExcludePrefix : SelectionIncludePrefix;
      string current = (currentExpression ?? string.Empty).Trim();
      if (current.Length == 0)
      {
        newExpression = BuildExpression(desiredPrefix, incoming);
        return true;
      }

      FilterOperator unusedOp;
      string payload;
      string existingPrefix;
      ParseOperator(current, out unusedOp, out payload, out existingPrefix);

      List<string> existingTokens = SplitTokens(payload);
      if (string.Equals(existingPrefix, desiredPrefix, StringComparison.Ordinal))
      {
        newExpression = BuildExpression(desiredPrefix, MergeUnique(existingTokens, incoming));
        return true;
      }

      string newMask = FormatMaskLabel(desiredPrefix);
      string message =
          "В поле фильтра уже стоит другая маска («" + FormatMaskLabel(existingPrefix) + "»)." +
          Environment.NewLine + Environment.NewLine +
          "Да — добавить выделенные значения и поставить всем маску «" + newMask + "»." +
          Environment.NewLine +
          "Нет — заменить содержимое поля новым фильтром по выделенному (маска «" + newMask + "»)." +
          Environment.NewLine +
          "Отмена — ничего не менять.";

      DialogResult answer = MessageBox.Show(
          owner,
          message,
          exclude ? "Исключить выделенное" : "Фильтр по выделенному",
          MessageBoxButtons.YesNoCancel,
          MessageBoxIcon.Question,
          MessageBoxDefaultButton.Button1);

      if (answer == DialogResult.Yes)
      {
        newExpression = BuildExpression(desiredPrefix, MergeUnique(existingTokens, incoming));
        return true;
      }

      if (answer == DialogResult.No)
      {
        newExpression = BuildExpression(desiredPrefix, incoming);
        return true;
      }

      return false;
    }

    /// <summary>
    /// Собирает уникальные значения столбца у выделенных строк ListView.
    /// </summary>
    /// <param name="list">Список с выделением.</param>
    /// <param name="columnIndex">Индекс столбца.</param>
    /// <param name="valueSelector">
    /// Если задан — значение для фильтра (например сырое имя материала);
    /// иначе текст <c>SubItems[column]</c>.
    /// </param>
    internal static List<string> CollectSelectedColumnValues(
        ListView list,
        int columnIndex,
        Func<ListViewItem, int, string> valueSelector)
    {
      var result = new List<string>();
      if (list == null || columnIndex < 0)
        return result;

      var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (ListViewItem item in list.SelectedItems)
      {
        if (item == null)
          continue;

        string value;
        if (valueSelector != null)
          value = valueSelector(item, columnIndex) ?? string.Empty;
        else if (columnIndex < item.SubItems.Count)
          value = item.SubItems[columnIndex].Text ?? string.Empty;
        else
          value = string.Empty;

        if (string.IsNullOrEmpty(value))
          continue;

        if (seen.Add(value))
          result.Add(value);
      }

      return result;
    }

    internal static void ShowHelp(IWin32Window owner)
    {
      VelumHelp.Show(owner, VelumHelpTopics.ListFilters);
    }

    private static List<string> NormalizeUniqueValues(IList<string> values)
    {
      var result = new List<string>();
      if (values == null || values.Count == 0)
        return result;

      var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      for (int i = 0; i < values.Count; i++)
      {
        string value = values[i] ?? string.Empty;
        if (string.IsNullOrEmpty(value))
          continue;
        if (seen.Add(value))
          result.Add(value);
      }

      return result;
    }

    private static List<string> MergeUnique(List<string> existing, List<string> incoming)
    {
      var result = new List<string>();
      var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

      if (existing != null)
      {
        for (int i = 0; i < existing.Count; i++)
        {
          string token = existing[i];
          if (string.IsNullOrEmpty(token))
            continue;
          if (seen.Add(token))
            result.Add(token);
        }
      }

      if (incoming != null)
      {
        for (int i = 0; i < incoming.Count; i++)
        {
          string token = incoming[i];
          if (string.IsNullOrEmpty(token))
            continue;
          if (seen.Add(token))
            result.Add(token);
        }
      }

      return result;
    }

    private static string BuildExpression(string prefix, List<string> tokens)
    {
      if (tokens == null || tokens.Count == 0)
        return string.Empty;

      string joined = string.Join("|", tokens.ToArray());
      return (prefix ?? string.Empty) + joined;
    }

    private static string FormatMaskLabel(string prefix)
    {
      if (string.IsNullOrEmpty(prefix))
        return "вхождение";
      return prefix;
    }

    private static void ParseOperator(
        string expr,
        out FilterOperator op,
        out string payload,
        out string prefix)
    {
      if (StartsWith(expr, "!="))
      {
        op = FilterOperator.NotEquals;
        payload = expr.Substring(2);
        prefix = "!=";
        return;
      }

      if (StartsWith(expr, ">="))
      {
        op = FilterOperator.GreaterOrEqual;
        payload = expr.Substring(2);
        prefix = ">=";
        return;
      }

      if (StartsWith(expr, "<="))
      {
        op = FilterOperator.LessOrEqual;
        payload = expr.Substring(2);
        prefix = "<=";
        return;
      }

      if (expr[0] == '!')
      {
        op = FilterOperator.NotContains;
        payload = expr.Substring(1);
        prefix = "!";
        return;
      }

      if (expr[0] == '=')
      {
        op = FilterOperator.Equals;
        payload = expr.Substring(1);
        prefix = "=";
        return;
      }

      if (expr[0] == '*')
      {
        op = FilterOperator.Contains;
        payload = expr.Substring(1);
        prefix = "*";
        return;
      }

      if (expr[0] == '>')
      {
        op = FilterOperator.Greater;
        payload = expr.Substring(1);
        prefix = ">";
        return;
      }

      if (expr[0] == '<')
      {
        op = FilterOperator.Less;
        payload = expr.Substring(1);
        prefix = "<";
        return;
      }

      op = FilterOperator.Contains;
      payload = expr;
      prefix = string.Empty;
    }

    private static bool StartsWith(string text, string prefix)
    {
      return text.Length >= prefix.Length &&
          string.Compare(text, 0, prefix, 0, prefix.Length, StringComparison.Ordinal) == 0;
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

    private static bool MatchAnyExact(string value, List<string> tokens)
    {
      for (int i = 0; i < tokens.Count; i++)
      {
        if (string.Equals(value, tokens[i], StringComparison.OrdinalIgnoreCase))
          return true;
      }

      return false;
    }

    private static bool MatchAnyContains(string value, List<string> tokens)
    {
      for (int i = 0; i < tokens.Count; i++)
      {
        if (value.IndexOf(tokens[i], StringComparison.OrdinalIgnoreCase) >= 0)
          return true;
      }

      return false;
    }

    private static bool MatchNumeric(
        FilterOperator op,
        string displayValue,
        string numericRaw,
        List<string> tokens)
    {
      double value;
      string source = !string.IsNullOrWhiteSpace(numericRaw) ? numericRaw : displayValue;
      if (!TryParseFilterNumber(source, out value))
        return false;

      for (int i = 0; i < tokens.Count; i++)
      {
        double tokenValue;
        if (!TryParseFilterNumber(tokens[i], out tokenValue))
          continue;

        bool hit;
        switch (op)
        {
          case FilterOperator.Greater:
            hit = value > tokenValue;
            break;
          case FilterOperator.GreaterOrEqual:
            hit = value >= tokenValue;
            break;
          case FilterOperator.Less:
            hit = value < tokenValue;
            break;
          case FilterOperator.LessOrEqual:
            hit = value <= tokenValue;
            break;
          default:
            hit = false;
            break;
        }

        if (hit)
          return true;
      }

      return false;
    }

    private static bool TryParseFilterNumber(string text, out double value)
    {
      value = 0;
      if (string.IsNullOrWhiteSpace(text))
        return false;
      return VelumBlankSizeProperties.TryParseNumber(text.Trim(), out value);
    }
  }
}
