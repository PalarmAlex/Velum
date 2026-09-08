using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Velum.ReactiveCore;

namespace Velum.UI.AssemblyRegistry
{
  /// <summary>
  /// Синтаксис и вычисление формул столбцов реестра изделия.
  /// Ссылки только в <c>[…]</c>; функции — белый список (<c>Round(expr; n)</c>).
  /// </summary>
  internal static class VelumAssemblyRegistryColumnFormula
  {
    internal const string EvaluationErrorMarker = "#ошибка вычислений!";

    private static readonly Regex RefRegex = new Regex(@"\[([^\[\]]*)\]", RegexOptions.Compiled);

    private static readonly HashSet<string> FunctionNames =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
          "Round"
        };

    /// <summary>Элементы белого списка для формы подстановок.</summary>
    internal static IReadOnlyList<FormulaCatalogItem> Catalog
    {
      get
      {
        return new[]
        {
          new FormulaCatalogItem("Round( ; )", "Round(expr; n)", "Округление expr до n знаков."),
          new FormulaCatalogItem("[FileName]", "[FileName]", "Имя файла без расширения."),
          new FormulaCatalogItem("[ИмяФайла]", "[ИмяФайла]", "Алиас FileName."),
          new FormulaCatalogItem("[Quantity]", "[Quantity]", "Количество вхождений в сборке."),
          new FormulaCatalogItem("[Кол-во]", "[Кол-во]", "Алиас Quantity.")
        };
      }
    }

    internal sealed class FormulaCatalogItem
    {
      internal FormulaCatalogItem(string display, string insertText, string description)
      {
        Display = display;
        InsertText = insertText;
        Description = description;
      }

      internal string Display { get; }

      internal string InsertText { get; }

      internal string Description { get; }
    }

    internal static bool IsKnownFunction(string name)
    {
      return !string.IsNullOrWhiteSpace(name) && FunctionNames.Contains(name.Trim());
    }

    /// <summary>Проверка синтаксиса формулы (без данных компонента).</summary>
    internal static bool TryValidateSyntax(string formula, out string error)
    {
      error = null;
      string text = (formula ?? string.Empty).Trim();
      if (text.Length == 0)
        return true;

      if (!AreBracketsBalanced(text, '[', ']', out error))
        return false;
      if (!AreBracketsBalanced(text, '(', ')', out error))
        return false;

      MatchCollection refs = RefRegex.Matches(text);
      foreach (Match match in refs)
      {
        if (string.IsNullOrWhiteSpace(match.Groups[1].Value))
        {
          error = "Пустая ссылка [].";
          return false;
        }
      }

      string stub = RefRegex.Replace(text, "1");
      try
      {
        ParseAndEvaluate(stub);
        return true;
      }
      catch (FormulaException ex)
      {
        error = ex.Message;
        return false;
      }
      catch (Exception ex)
      {
        error = "Ошибка разбора формулы: " + ex.Message;
        return false;
      }
    }

    /// <summary>
    /// Вычисляет формулу. <paramref name="resolveRef"/> возвращает сырое значение ссылки
    /// или null, если ссылка неизвестна/пустая/«?».
    /// </summary>
    internal static string Evaluate(string formula, Func<string, string> resolveRef)
    {
      string text = (formula ?? string.Empty).Trim();
      if (text.Length == 0)
        return string.Empty;

      if (resolveRef == null)
        return EvaluationErrorMarker;

      var replaced = new StringBuilder();
      int last = 0;
      foreach (Match match in RefRegex.Matches(text))
      {
        replaced.Append(text, last, match.Index - last);
        string key = match.Groups[1].Value.Trim();
        if (key.Length == 0)
          return EvaluationErrorMarker;

        string raw = resolveRef(key);
        if (!TryToInvariantNumber(raw, out string numberText))
          return EvaluationErrorMarker;

        replaced.Append(numberText);
        last = match.Index + match.Length;
      }

      replaced.Append(text, last, text.Length - last);

      try
      {
        double value = ParseAndEvaluate(replaced.ToString());
        return FormatResult(value);
      }
      catch
      {
        return EvaluationErrorMarker;
      }
    }

    internal static IEnumerable<string> ExtractRefs(string formula)
    {
      string text = formula ?? string.Empty;
      foreach (Match match in RefRegex.Matches(text))
      {
        string key = match.Groups[1].Value.Trim();
        if (key.Length > 0)
          yield return key;
      }
    }

    private static bool TryToInvariantNumber(string raw, out string numberText)
    {
      numberText = null;
      if (string.IsNullOrWhiteSpace(raw))
        return false;
      if (string.Equals(raw.Trim(), VelumAssemblyRegistryPropertyReader.MissingMarker, StringComparison.Ordinal))
        return false;

      double number;
      if (!VelumBlankSizeProperties.TryParseNumber(raw, out number))
        return false;

      numberText = number.ToString("0.#########", CultureInfo.InvariantCulture);
      return true;
    }

    private static string FormatResult(double value)
    {
      double rounded = Math.Round(value, 9, MidpointRounding.AwayFromZero);
      return VelumAssemblyRegistryPropertyReader.FormatListNumber(rounded);
    }

    private static bool AreBracketsBalanced(string text, char open, char close, out string error)
    {
      error = null;
      int depth = 0;
      foreach (char c in text)
      {
        if (c == open)
          depth++;
        else if (c == close)
        {
          depth--;
          if (depth < 0)
          {
            error = "Лишняя закрывающая скобка «" + close + "».";
            return false;
          }
        }
      }

      if (depth != 0)
      {
        error = "Незакрытая скобка «" + open + "».";
        return false;
      }

      return true;
    }

    private static double ParseAndEvaluate(string expression)
    {
      var parser = new Parser(expression);
      double value = parser.ParseExpression();
      parser.ExpectEnd();
      return value;
    }

    private sealed class FormulaException : Exception
    {
      internal FormulaException(string message)
          : base(message)
      {
      }
    }

    private enum TokenKind
    {
      End,
      Number,
      Ident,
      Plus,
      Minus,
      Star,
      Slash,
      LParen,
      RParen,
      Semicolon
    }

    private sealed class Parser
    {
      private readonly string _text;
      private int _index;
      private TokenKind _kind;
      private double _number;
      private string _ident;

      internal Parser(string text)
      {
        _text = text ?? string.Empty;
        Next();
      }

      internal double ParseExpression()
      {
        double value = ParseTerm();
        while (_kind == TokenKind.Plus || _kind == TokenKind.Minus)
        {
          TokenKind op = _kind;
          Next();
          double right = ParseTerm();
          value = op == TokenKind.Plus ? value + right : value - right;
        }

        return value;
      }

      private double ParseTerm()
      {
        double value = ParseUnary();
        while (_kind == TokenKind.Star || _kind == TokenKind.Slash)
        {
          TokenKind op = _kind;
          Next();
          double right = ParseUnary();
          if (op == TokenKind.Star)
            value *= right;
          else
          {
            if (Math.Abs(right) < 1e-15)
              throw new FormulaException("Деление на ноль.");
            value /= right;
          }
        }

        return value;
      }

      private double ParseUnary()
      {
        if (_kind == TokenKind.Plus)
        {
          Next();
          return ParseUnary();
        }

        if (_kind == TokenKind.Minus)
        {
          Next();
          return -ParseUnary();
        }

        return ParsePrimary();
      }

      private double ParsePrimary()
      {
        if (_kind == TokenKind.Number)
        {
          double value = _number;
          Next();
          return value;
        }

        if (_kind == TokenKind.Ident)
        {
          string name = _ident;
          Next();
          if (_kind != TokenKind.LParen)
            throw new FormulaException("Неизвестная константа «" + name + "».");

          if (!IsKnownFunction(name))
            throw new FormulaException("Неизвестная функция «" + name + "».");

          Next();
          if (string.Equals(name, "Round", StringComparison.OrdinalIgnoreCase))
          {
            double arg = ParseExpression();
            if (_kind != TokenKind.Semicolon)
              throw new FormulaException("Round: ожидается «;» между аргументами.");
            Next();
            double digitsRaw = ParseExpression();
            if (_kind != TokenKind.RParen)
              throw new FormulaException("Round: ожидается «)».");
            Next();
            int digits = (int)Math.Round(digitsRaw, MidpointRounding.AwayFromZero);
            if (digits < 0)
              digits = 0;
            if (digits > 15)
              digits = 15;
            return Math.Round(arg, digits, MidpointRounding.AwayFromZero);
          }

          throw new FormulaException("Неизвестная функция «" + name + "».");
        }

        if (_kind == TokenKind.LParen)
        {
          Next();
          double value = ParseExpression();
          if (_kind != TokenKind.RParen)
            throw new FormulaException("Ожидается «)».");
          Next();
          return value;
        }

        throw new FormulaException("Неожиданный фрагмент формулы.");
      }

      internal void ExpectEnd()
      {
        if (_kind != TokenKind.End)
          throw new FormulaException("Лишний текст после формулы.");
      }

      private void Next()
      {
        SkipWs();
        if (_index >= _text.Length)
        {
          _kind = TokenKind.End;
          return;
        }

        char c = _text[_index];
        switch (c)
        {
          case '+':
            _index++;
            _kind = TokenKind.Plus;
            return;
          case '-':
            _index++;
            _kind = TokenKind.Minus;
            return;
          case '*':
            _index++;
            _kind = TokenKind.Star;
            return;
          case '/':
            _index++;
            _kind = TokenKind.Slash;
            return;
          case '(':
            _index++;
            _kind = TokenKind.LParen;
            return;
          case ')':
            _index++;
            _kind = TokenKind.RParen;
            return;
          case ';':
            _index++;
            _kind = TokenKind.Semicolon;
            return;
        }

        if (char.IsDigit(c) || c == '.' || c == ',')
        {
          ReadNumber();
          return;
        }

        if (IsIdentStart(c))
        {
          ReadIdent();
          return;
        }

        throw new FormulaException("Недопустимый символ «" + c + "».");
      }

      private void ReadNumber()
      {
        int start = _index;
        bool seenDot = false;
        while (_index < _text.Length)
        {
          char c = _text[_index];
          if (c >= '0' && c <= '9')
          {
            _index++;
            continue;
          }

          if ((c == '.' || c == ',') && !seenDot)
          {
            seenDot = true;
            _index++;
            continue;
          }

          break;
        }

        string raw = _text.Substring(start, _index - start).Replace(',', '.');
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out _number))
          throw new FormulaException("Некорректное число «" + raw + "».");
        _kind = TokenKind.Number;
      }

      private void ReadIdent()
      {
        int start = _index;
        _index++;
        while (_index < _text.Length && IsIdentPart(_text[_index]))
          _index++;
        _ident = _text.Substring(start, _index - start);
        _kind = TokenKind.Ident;
      }

      private void SkipWs()
      {
        while (_index < _text.Length && char.IsWhiteSpace(_text[_index]))
          _index++;
      }

      private static bool IsIdentStart(char c)
      {
        return char.IsLetter(c) || c == '_';
      }

      private static bool IsIdentPart(char c)
      {
        return char.IsLetterOrDigit(c) || c == '_';
      }
    }
  }
}
