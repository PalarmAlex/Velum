using System.Text;

namespace Velum.UI.Logs
{
  /// <summary>
  /// Чистые правила CSV-формата событийного лога (<c>AgentLogs_Events.csv</c>):
  /// заголовок блока-сессии и экранирование текста токена. Класс без зависимостей от
  /// WinForms / ISIDA / файловой системы — линкуется в тестовый проект
  /// <c>Velum.ReactiveCore.Tests</c>.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Текст токена сокращённого лога может содержать <c>;</c>, переносы строк и обратный
  /// слэш — всё это сломало бы CSV (строка делится по <c>;</c>, блок-сессия делится на
  /// строки). Поэтому текст кодируется в одну физическую строку без разделителей, а
  /// декодируется уже после чтения строки — <see cref="VelumCsvLogSessionReader"/>
  /// переиспользуется без изменений.
  /// </para>
  /// <para>
  /// Кодирование — последовательной заменой, <b>порядок важен</b>: сначала
  /// <c>\</c> → <c>\\</c>, иначе декодирование восстановит не то (экранирующие слэши,
  /// добавленные следующими шагами, слились бы с настоящими).
  /// </para>
  /// </remarks>
  internal static class VelumEventLogCsvRules
  {
    /// <summary>Имя файла событийного лога в каталоге логов.</summary>
    internal const string EventLogCsvFileName = "AgentLogs_Events.csv";

    /// <summary>Имя колонки времени (её читает <see cref="VelumCsvLogSessionReader"/>).</summary>
    internal const string TimeColumnName = "Time";

    /// <summary>
    /// Строка заголовка блока-сессии: <c>Time;Pulse;Repeat;Text</c>. Колонка <c>Text</c>
    /// — последняя, чтобы время и пульс читались по индексам независимо от текста.
    /// </summary>
    /// <returns>Строка заголовка без терминалятора.</returns>
    internal static string BuildHeaderLine()
    {
      return "Time;Pulse;Repeat;Text";
    }

    /// <summary>
    /// Кодирует текст токена в одну CSV-строку: <c>\</c> → <c>\\</c>, <c>;</c> → <c>\;</c>,
    /// CR → <c>\r</c> (две буквы), LF → <c>\n</c> (две буквы). После кодирования в тексте
    /// не остаётся ни <c>;</c>, ни переносов.
    /// </summary>
    /// <param name="text">Исходный текст токена (может быть <c>null</c>).</param>
    /// <returns>Экранированный текст; пустой/нулевой вход возвращается как пустая строка.</returns>
    internal static string Encode(string text)
    {
      const char Backslash = (char)92;

      if (string.IsNullOrEmpty(text))
        return string.Empty;

      // Порядок замен обязателен: сначала слэш, иначе он экранирует служебные пары следующих шагов.
      string result = text.Replace(Backslash.ToString(), new string(Backslash, 2));
      result = result.Replace(";", Backslash + ";");
      result = result.Replace("\r", Backslash + "r");
      result = result.Replace("\n", Backslash + "n");
      return result;
    }

    /// <summary>
    /// Декодирует строку CSV обратно в текст токена: <c>\\</c> → <c>\</c>, <c>\;</c> → <c>;</c>,
    /// <c>\r</c> → CR, <c>\n</c> → LF. Неизвестная escape-пара оставляется как есть
    /// (защита от ручных правок файла), висящий в конце слэш сохраняется.
    /// </summary>
    /// <param name="encoded">Экранированный текст (может быть <c>null</c>).</param>
    /// <returns>Исходный текст; пустой/нулевой вход возвращается как пустая строка.</returns>
    internal static string Decode(string encoded)
    {
      if (string.IsNullOrEmpty(encoded))
        return string.Empty;

      var sb = new StringBuilder(encoded.Length);
      for (int i = 0; i < encoded.Length; i++)
      {
        char c = encoded[i];
        if (c != '\\' || i + 1 >= encoded.Length)
        {
          // Обычный символ или висящий в конце слэш — пишем как есть.
          sb.Append(c);
          continue;
        }

        char next = encoded[++i];
        switch (next)
        {
          case '\\':
            sb.Append('\\');
            break;
          case ';':
            sb.Append(';');
            break;
          case 'r':
            sb.Append('\r');
            break;
          case 'n':
            sb.Append('\n');
            break;
          default:
            // Неизвестная пара — оставляем оба символа (защита).
            sb.Append('\\').Append(next);
            break;
        }
      }

      return sb.ToString();
    }
  }
}