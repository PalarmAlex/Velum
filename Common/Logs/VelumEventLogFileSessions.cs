using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ISIDA.Common;
using Velum.Configuration;

namespace Velum.UI.Logs
{
  /// <summary>
  /// Сессии событийного лога (<c>AgentLogs_Events.csv</c>) — токенов сокращённого
  /// (семантического) лога движка, которые панель «Агент» показывает в окне «Ответ агента».
  /// В отличие от системного/сталевого/параметрического логов, этот файл пишет сам Velum
  /// (append при завершении сессии пульсации), а не <c>ResearchLogger</c>, поэтому:
  /// каталог берётся из настройки проекта <see cref="VelumAppConfig.LogsFolderPath"/>,
  /// парного JSONL нет, и освобождение writers логгера при удалении не требуется.
  /// </summary>
  internal static class VelumEventLogFileSessions
  {
    /// <summary>Полный путь к файлу событийного лога (каталог — из настройки проекта).</summary>
    internal static string EventLogCsvPath =>
        Path.Combine(VelumAppConfig.LogsFolderPath ?? string.Empty,
            VelumEventLogCsvRules.EventLogCsvFileName);

    /// <summary>
    /// Список сессий, от новых к старым. Сортировка — по <b>индексу блока</b> в файле
    /// (файл дописывается append'ом, блоки уже хронологичны), а не по времени: колонка
    /// <c>Time</c> хранит только время дня без даты, и между сессиями разных дней
    /// сортировка по времени перепутала бы порядок.
    /// </summary>
    public static IReadOnlyList<VelumLogFileSessionInfo> ListFileSessions()
    {
      var sessions = VelumCsvLogSessionReader.ListSessionsByPath(
          EventLogCsvPath, IsHeaderRow, VelumEventLogCsvRules.TimeColumnName);
      return sessions.OrderByDescending(s => s.SessionIndex).ToList();
    }

    /// <summary>Записи указанной сессии (текст декодирован).</summary>
    public static List<VelumEventLogEntry> LoadSessionEntries(int sessionIndex)
    {
      // maxColumns: 4 — текст токена забирает остаток строки, иначе экранированные ';' внутри
      // текста разрезали бы строку на лишние колонки (см. VelumEventLogCsvRules.Encode).
      var rows = VelumCsvLogSessionReader.ReadSessionRowsByPath(
          EventLogCsvPath, sessionIndex, IsHeaderRow, VelumEventLogCsvRules.TimeColumnName,
          maxColumns: 4);
      return ParseRows(rows);
    }

    /// <summary>
    /// Объединение нескольких сессий в <b>хронологическом</b> порядке токенов
    /// (в порядке записи, как в отчёте сценариев; окно панели показывает обратный порядок).
    /// </summary>
    public static List<VelumEventLogEntry> LoadMergedSessions(IEnumerable<int> sessionIndices)
    {
      var list = new List<VelumEventLogEntry>();
      if (sessionIndices == null)
        return list;
      // Индексы идут по порядку блоков в файле (старые — раньше), обходим по возрастанию,
      // чтобы склейка сессий осталась хронологической.
      foreach (int ix in sessionIndices.OrderBy(x => x))
        list.AddRange(LoadSessionEntries(ix));
      return list;
    }

    /// <summary>
    /// Удаляет указанные сессии из событийного лога. Возвращает удалённые сессии
    /// (для поиска связанных отчётов).
    /// </summary>
    public static List<VelumLogFileSessionInfo> DeleteSessions(IEnumerable<int> sessionIndices)
    {
      var indices = new List<int>(sessionIndices ?? Enumerable.Empty<int>());
      if (indices.Count == 0)
        return new List<VelumLogFileSessionInfo>();

      var removed = ListFileSessions().Where(s => indices.Contains(s.SessionIndex)).ToList();
      VelumCsvLogSessionReader.DeleteSessionsByPath(EventLogCsvPath, indices, IsHeaderRow);
      return removed;
    }

    /// <summary>
    /// Дописывает завершённую сессию в файл событийного лога: строку-заголовок блока,
    /// затем по строке на токен в порядке записи. Файл открывается в append-режиме с общим
    /// доступом (<see cref="FileShare.ReadWrite"/>), чтобы не конфликтовать с формой логов,
    /// которая может читать его параллельно. При отсутствии файла создаётся новый с BOM.
    /// </summary>
    /// <remarks>
    /// Вызывается на потоке пульса из <c>OnSessionCompleted</c>: ввод-вывод держим коротким,
    /// исключения не выпускаем наружу (пишем в <see cref="Logger.Warning"/>), чтобы сбой
    /// записи не останавливал пульсацию.
    /// </remarks>
    /// <param name="tokens">Все токены сессии в порядке записи (из <c>OnSessionCompleted</c>).</param>
    public static void AppendSession(ShortLogToken[] tokens)
    {
      if (tokens == null || tokens.Length == 0)
        return;

      try
      {
        string folder = VelumAppConfig.LogsFolderPath;
        if (string.IsNullOrWhiteSpace(folder))
          return;
        Directory.CreateDirectory(folder);

        string path = EventLogCsvPath;
        bool writeBom = !File.Exists(path);

        // BOM добавляем только при создании файла; append идёт без него (иначе BOM в середине).
        using (var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
        {
          if (writeBom)
          {
            byte[] bom = new UTF8Encoding(true).GetPreamble();
            fs.Write(bom, 0, bom.Length);
          }

          using (var writer = new StreamWriter(fs, new UTF8Encoding(false)))
          {
            writer.WriteLine(VelumEventLogCsvRules.BuildHeaderLine());
            foreach (ShortLogToken token in tokens)
            {
              if (token == null)
                continue;
              writer.WriteLine(BuildDataLine(token));
            }
            writer.Flush();
          }
        }
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum: запись событийного лога: " + ex.Message);
      }
    }

    /// <summary>Строка данных токена: <c>HH:mm:ss;пульс;repeat;текст</c> (текст экранирован).</summary>
    private static string BuildDataLine(ShortLogToken token)
    {
      string time = token.Time.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
      return time
             + ";" + token.Pulse.ToString(CultureInfo.InvariantCulture)
             + ";" + token.RepeatCount.ToString(CultureInfo.InvariantCulture)
             + ";" + VelumEventLogCsvRules.Encode(token.Text);
    }

    private static bool IsHeaderRow(string line)
    {
      line = line ?? string.Empty;
      return line.IndexOf("Time", StringComparison.Ordinal) >= 0
             && line.IndexOf("Pulse", StringComparison.Ordinal) >= 0
             && line.IndexOf("Repeat", StringComparison.Ordinal) >= 0
             && line.IndexOf(';') >= 0;
    }

    private static List<VelumEventLogEntry> ParseRows(List<Dictionary<string, string>> rows)
    {
      var list = new List<VelumEventLogEntry>();
      foreach (var row in rows)
      {
        string timeRaw = Get(row, "Time");
        if (!TryParseTime(timeRaw, out TimeSpan time))
          continue;
        list.Add(new VelumEventLogEntry
        {
          Time = time,
          Pulse = VelumCsvLogSessionReader.ParseInt(Get(row, "Pulse")),
          RepeatCount = ParseRepeatOrDefault(Get(row, "Repeat")),
          Text = VelumEventLogCsvRules.Decode(Get(row, "Text"))
        });
      }
      return list;
    }

    /// <summary>Разбор времени колонки <c>Time</c> (<c>HH:mm:ss</c>).</summary>
    private static bool TryParseTime(string raw, out TimeSpan time)
    {
      time = TimeSpan.Zero;
      if (string.IsNullOrWhiteSpace(raw))
        return false;
      return TimeSpan.TryParseExact(raw.Trim(), @"hh\:mm\:ss", CultureInfo.InvariantCulture, out time)
             || TimeSpan.TryParse(raw.Trim(), CultureInfo.InvariantCulture, out time);
    }

    /// <summary>Repeat по умолчанию 1 (пустое/битое значение — «без повторов», как в токене).</summary>
    private static int ParseRepeatOrDefault(string raw)
    {
      int v = VelumCsvLogSessionReader.ParseInt(raw, 1);
      return v < 1 ? 1 : v;
    }

    private static string Get(Dictionary<string, string> row, string key)
    {
      return row.TryGetValue(key, out string v) ? v : string.Empty;
    }
  }
}
