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
    /// Индексы выбранных (отмеченных флажком) сессий по списку элементов вкладки.
    /// Выборка читает <b>модель</b> элемента (флаг <c>Checked</c>), а не состояние
    /// контрола: именно рассинхрон модели и чекбокса CheckedListBox приводил к тому,
    /// что «Просмотр»/«Очистить» сообщали «не выбрана ни одна сессия» при отмеченных
    /// флажках. Элементы без привязанной сессии отбрасываются; результат сортируется.
    /// </summary>
    /// <typeparam name="T">Тип строки списка сессий.</typeparam>
    /// <param name="items">Элементы вкладки (видимые и скрытые фильтром).</param>
    /// <param name="isChecked">Предикат «строка отмечена флажком» (по модели).</param>
    /// <param name="sessionIndex">Индекс сессии строки.</param>
    /// <returns>Отсортированные индексы выбранных сессий.</returns>
    internal static List<int> CollectSelectedSessionIndices<T>(
        IEnumerable<T> items,
        Func<T, bool> isChecked,
        Func<T, int> sessionIndex)
    {
      var result = new List<int>();
      if (items == null || isChecked == null || sessionIndex == null)
        return result;

      foreach (T item in items)
      {
        if (item != null && isChecked(item))
          result.Add(sessionIndex(item));
      }

      result.Sort();
      return result;
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

    /// <summary>
    /// Оставляет из строк JSONL-лога только те, что НЕ принадлежат удаляемым сессиям.
    /// Граница сессии в JSONL — запись с пульсом 1, идущая после записи с пульсом &gt; 1
    /// (первый пульс новой сессии после её развития); это тот же критерий, что при чтении
    /// сессий (<c>ReadJsonlSessionBlocks</c>). Непарсящиеся строки (не запись) отбрасываются,
    /// как и при чтении — файл после перезаписи остаётся валидным JSONL.
    /// </summary>
    /// <param name="lines">Строки JSONL в исходном порядке.</param>
    /// <param name="tryGetPulse">
    /// Извлекает пульс записи; возвращает <c>null</c>, если строка не является записью
    /// (не JSON или нет разбора времени) — такая строка выбрасывается.
    /// </param>
    /// <param name="sessionIndicesToDelete">Индексы удаляемых сессий (по порядку блоков).</param>
    internal static List<string> KeepJsonlLinesExceptSessions(
        IEnumerable<string> lines,
        Func<string, int?> tryGetPulse,
        IEnumerable<int> sessionIndicesToDelete)
    {
      var kept = new List<string>();
      if (lines == null || tryGetPulse == null)
        return kept;

      var toDelete = new HashSet<int>(sessionIndicesToDelete ?? Enumerable.Empty<int>());
      if (toDelete.Count == 0)
      {
        // Нечего удалять — сохраняем только валидные записи (мусор отбрасываем, как при чтении).
        foreach (string line in lines)
          if (tryGetPulse(line).HasValue)
            kept.Add(line);
        return kept;
      }

      int blockIndex = -1;
      bool insideDeleted = false;
      bool anySession = false;
      int? lastPulse = null;

      foreach (string line in lines)
      {
        int? pulse = tryGetPulse(line);
        if (!pulse.HasValue)
          continue; // не запись — пропускаем (как ReadJsonlSessionBlocks)

        bool newSession = !anySession
                          || (pulse.Value == 1 && lastPulse.HasValue && lastPulse.Value > 1);
        if (newSession)
        {
          blockIndex++;
          anySession = true;
          insideDeleted = toDelete.Contains(blockIndex);
          lastPulse = null;
        }

        if (!insideDeleted)
          kept.Add(line);
        lastPulse = pulse;
      }

      return kept;
    }
  }
}
