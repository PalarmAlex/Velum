using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Преобразование строки буфера панели Velum в формат сенсорных каналов ISIDA.
  /// Речь и команды разделяются на две независимые строки для VerbalChannel и CommandChannel.
  /// </summary>
  internal static class VelumOperatorStimulusCodec
  {
    private static readonly Regex CommandTokenPattern = new Regex(
        @"\b(sw:\d+|pt:[^\s]+)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string SanitizeOperatorMessage(string raw)
    {
      if (string.IsNullOrEmpty(raw))
        return string.Empty;
      var sb = new StringBuilder(raw.Length);
      for (int i = 0; i < raw.Length; i++)
      {
        if (raw[i] == '"')
          continue;
        sb.Append(raw[i]);
      }

      return CollapseWhitespace(sb.ToString()).Trim();
    }

    public static string NormalizeForVerbalChannel(string raw)
    {
      if (string.IsNullOrWhiteSpace(raw))
        return string.Empty;
      raw = raw.Trim();
      if (raw.IndexOf('"') < 0 && raw.IndexOf('(') < 0)
        return CollapseWhitespace(raw).Trim();

      var tokens = new List<string>();
      int i = 0;
      while (i < raw.Length)
      {
        if (char.IsWhiteSpace(raw[i]))
        {
          i++;
          continue;
        }

        if (raw[i] == '"')
        {
          int end = raw.IndexOf('"', i + 1);
          if (end < 0)
          {
            tokens.AddRange(SplitWords(raw.Substring(i + 1)));
            break;
          }

          tokens.AddRange(SplitWords(raw.Substring(i + 1, end - i - 1)));
          i = end + 1;
          continue;
        }

        if (raw[i] == '(')
        {
          int depth = 1;
          int j = i + 1;
          for (; j < raw.Length; j++)
          {
            if (raw[j] == '(')
              depth++;
            else if (raw[j] == ')')
            {
              depth--;
              if (depth == 0)
                break;
            }
          }

          if (depth != 0)
          {
            tokens.AddRange(SplitWords(raw.Substring(i)));
            break;
          }

          foreach (string t in SplitWords(raw.Substring(i + 1, j - i - 1)))
          {
            if (!string.IsNullOrEmpty(t))
              tokens.Add(t);
          }

          i = j + 1;
          continue;
        }

        int start = i;
        while (i < raw.Length && !char.IsWhiteSpace(raw[i]) && raw[i] != '"' && raw[i] != '(')
          i++;
        string tok = raw.Substring(start, i - start);
        if (!string.IsNullOrEmpty(tok))
          tokens.Add(tok);
      }

      return string.Join(" ", tokens);
    }

    /// <summary>
    /// Разделяет нормализованную строку на речь и командные токены.
    /// </summary>
    public static (string VerbalLine, string CommandLine) SplitVerbalAndCommand(string normalizedLine)
    {
      if (string.IsNullOrWhiteSpace(normalizedLine))
        return (string.Empty, string.Empty);

      var verbalTokens = new List<string>();
      var commandTokens = new List<string>();

      foreach (string token in SplitWords(normalizedLine))
      {
        if (IsCommandToken(token))
          commandTokens.Add(token);
        else
          verbalTokens.Add(token);
      }

      return (string.Join(" ", verbalTokens), string.Join(" ", commandTokens));
    }

    /// <summary>
    /// Разбирает ввод оператора: текст в кавычках/скобках и буфер команд SolidWorks.
    /// </summary>
    public static (string VerbalLine, string CommandLine) ParseOperatorInput(string sanitizedMessage, string commandTokenLine)
    {
      string normalized = NormalizeForVerbalChannel(sanitizedMessage ?? string.Empty);
      var (verbalFromMessage, commandFromMessage) = SplitVerbalAndCommand(normalized);

      string commandBuffer = CollapseWhitespace(commandTokenLine ?? string.Empty).Trim();
      string commandLine = commandBuffer.Length == 0
          ? commandFromMessage
          : (commandFromMessage.Length == 0 ? commandBuffer : commandFromMessage + " " + commandBuffer);

      return (verbalFromMessage, commandLine.Trim());
    }

    public static bool ShouldForceAuthoritativeVerbalWrite(string normalizedVerbalLine)
    {
      if (string.IsNullOrWhiteSpace(normalizedVerbalLine))
        return false;
      return false;
    }

    public static bool IsSwCommandToken(string token)
    {
      if (string.IsNullOrEmpty(token) || token.Length < 4)
        return false;
      if (!token.StartsWith("sw:", StringComparison.OrdinalIgnoreCase))
        return false;
      return int.TryParse(
          token.Substring(3),
          NumberStyles.Integer,
          CultureInfo.InvariantCulture,
          out _);
    }

    public static bool IsPointToken(string token)
    {
      if (string.IsNullOrEmpty(token) || token.Length < 4)
        return false;
      return token.StartsWith("pt:", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsCommandToken(string token)
    {
      return IsSwCommandToken(token) || IsPointToken(token);
    }

    /// <summary>
    /// Командные токены (<c>sw:*</c>, <c>pt:*</c>) из строки буфера в порядке следования.
    /// </summary>
    public static IEnumerable<string> EnumerateCommandTokens(string commandLine)
    {
      if (string.IsNullOrWhiteSpace(commandLine))
        yield break;

      foreach (string part in commandLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
      {
        string tok = part.Trim();
        if (IsCommandToken(tok))
          yield return tok;
      }
    }

    private static IEnumerable<string> SplitWords(string segment)
    {
      if (string.IsNullOrWhiteSpace(segment))
        yield break;
      var parts = segment.Split(
          new[] { ' ', '\t', '\r', '\n' },
          StringSplitOptions.RemoveEmptyEntries);
      for (int p = 0; p < parts.Length; p++)
      {
        string w = parts[p].Trim();
        if (w.Length > 0)
          yield return w;
      }
    }

    private static string CollapseWhitespace(string s)
    {
      if (string.IsNullOrEmpty(s))
        return string.Empty;
      var sb = new StringBuilder(s.Length);
      bool pending = false;
      for (int i = 0; i < s.Length; i++)
      {
        if (char.IsWhiteSpace(s[i]))
        {
          pending = true;
          continue;
        }

        if (pending && sb.Length > 0)
          sb.Append(' ');
        pending = false;
        sb.Append(s[i]);
      }

      return sb.ToString();
    }
  }
}
