using System;
using System.Collections.Generic;
using System.Linq;

namespace Velum.UI.Logs
{
  /// <summary>
  /// Pure-правила работы с сессиями CSV-лога (без файлов, путей и конфигурации):
  /// отбор строк, попадающих в удаляемые сессии, и построение имени отчёта, связанного
  /// с сессией. Линкуется в тестовый проект Velum.ReactiveCore.Tests.
  /// </summary>
  /// <remarks>
  /// Регрессии, которые защищают правила: удаление выделенных сессий должно сохранять
  /// все остальные строки файла (включая чужие сессии и заголовки), а имя отчёта —
  /// совпадать с именем лога в списке, чтобы «Очистить» могло найти связанный отчёт.
  /// </remarks>
  internal static class VelumLogSessionRules
  {
    /// <summary>
    /// Оставляет из исходных строк файла только те, что НЕ принадлежат удаляемым сессиям.
    /// Заголовок открывает новый блок (сессию); строки блока с индексом из
    /// <paramref name="sessionIndicesToDelete"/> отбрасываются вместе со своим заголовком.
    /// Строки до первого заголовка и пустые строки сохраняются.
    /// </summary>
    /// <param name="lines">Строки файла в исходном порядке.</param>
    /// <param name="isHeaderRow">Предикат «строка является заголовком блока-сессии».</param>
    /// <param name="sessionIndicesToDelete">Индексы удаляемых блоков (по порядку в файле).</param>
    /// <returns>Список строк, которые нужно оставить.</returns>
    internal static List<string> KeepLinesExceptSessions(
        IEnumerable<string> lines,
        Func<string, bool> isHeaderRow,
        IEnumerable<int> sessionIndicesToDelete)
    {
      var kept = new List<string>();
      if (lines == null)
        return kept;

      var toDelete = new HashSet<int>(sessionIndicesToDelete ?? Enumerable.Empty<int>());
      if (isHeaderRow == null || toDelete.Count == 0)
      {
        kept.AddRange(lines);
        return kept;
      }

      bool anyHeader = false;
      bool insideDeleted = false;
      int blockIndex = -1;

      foreach (string line in lines)
      {
        if (isHeaderRow(line))
        {
          blockIndex++;
          anyHeader = true;
          insideDeleted = toDelete.Contains(blockIndex);
          // Заголовок удаляемой сессии отбрасывается вместе с её строками;
          // заголовок оставшейся сессии сохраняется.
          if (!insideDeleted)
            kept.Add(line);
          continue;
        }

        // Строки до первого заголовка сохраняются; строки удаляемых блоков — нет.
        if (anyHeader && insideDeleted)
          continue;

        kept.Add(line);
      }

      return kept;
    }

    /// <summary>
    /// Имя файла HTML-отчёта, связанного с сессией: <c>{префикс}_{yyyyMMdd_HHmmss}.html</c>,
    /// где штамп — время начала сессии. По этому имени «Очистить» находит связанный отчёт.
    /// </summary>
    /// <param name="prefix">Префикс вкладки (различает системные/стилевые/параметрические логи).</param>
    /// <param name="sessionStart">Время начала сессии.</param>
    internal static string BuildSessionReportFileName(string prefix, DateTime sessionStart)
    {
      return (prefix ?? "Log") + "_"
             + sessionStart.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture)
             + ".html";
    }
  }
}
