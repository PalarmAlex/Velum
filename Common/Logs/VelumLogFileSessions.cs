using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using Velum.Isida.Logs;

namespace Velum.UI.Logs
{
  /// <summary>
  /// Описание сессии в CSV-логе (блок между повторяющимися строками заголовка).
  /// </summary>
  internal sealed class VelumLogFileSessionInfo
  {
    /// <summary>Ключ «текущая сессия в памяти» (в Velum — только файловые сессии).</summary>
    public const string CurrentSessionKey = "__current__";

    /// <summary>Ключ сессии (индекс блока в файле).</summary>
    public string SessionKey { get; set; }

    /// <summary>Индекс блока в файле.</summary>
    public int SessionIndex { get; set; }

    /// <summary>Время начала сессии.</summary>
    public DateTime StartedLocal { get; set; }

    /// <summary>Время окончания сессии.</summary>
    public DateTime EndedLocal { get; set; }

    /// <summary>Количество строк данных в сессии.</summary>
    public int EntryCount { get; set; }

    /// <summary>Подпись сессии вида <c>dd.MM.yyyy HH:mm–HH:mm (count)</c>.</summary>
    public string BuildDisplayLabel()
    {
      if (StartedLocal.Date == EndedLocal.Date)
      {
        return StartedLocal.ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture)
               + "–" + EndedLocal.ToString("HH:mm", CultureInfo.CurrentCulture)
               + " (" + EntryCount.ToString(CultureInfo.InvariantCulture) + ")";
      }

      return StartedLocal.ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture)
             + " – " + EndedLocal.ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture)
             + " (" + EntryCount.ToString(CultureInfo.InvariantCulture) + ")";
    }
  }

  /// <summary>
  /// Чтение сессий из CSV-логов с повторяющимися строками заголовка.
  /// </summary>
  internal static class VelumCsvLogSessionReader
  {
    private static readonly string[] TimeFormats =
    {
      "yyyy-MM-dd HH:mm:ss",
      "dd.MM.yyyy HH:mm:ss"
    };

    /// <summary>Список сессий файла, от новых к старым.</summary>
    public static IReadOnlyList<VelumLogFileSessionInfo> ListSessions(
        string csvFileName,
        Func<string, bool> isHeaderRow,
        string timeColumnName)
    {
      string path = VelumLogPaths.ResolveLogFile(csvFileName);
      if (!File.Exists(path))
        return Array.Empty<VelumLogFileSessionInfo>();

      try
      {
        var blocks = ReadBlocks(path, isHeaderRow, timeColumnName);
        var list = new List<VelumLogFileSessionInfo>();
        for (int i = 0; i < blocks.Count; i++)
        {
          if (blocks[i].RowCount == 0)
            continue;
          list.Add(new VelumLogFileSessionInfo
          {
            SessionKey = i.ToString(CultureInfo.InvariantCulture),
            SessionIndex = i,
            StartedLocal = blocks[i].StartedLocal,
            EndedLocal = blocks[i].EndedLocal,
            EntryCount = blocks[i].RowCount
          });
        }

        return list.OrderByDescending(s => s.StartedLocal).ToList();
      }
      catch (Exception ex)
      {
        ISIDA.Common.Logger.Error(csvFileName + " сессии: " + ex.Message);
        return Array.Empty<VelumLogFileSessionInfo>();
      }
    }

    /// <summary>Строки данных указанной сессии (индекс — по порядку блоков в файле).</summary>
    public static List<Dictionary<string, string>> ReadSessionRows(
        string csvFileName,
        int sessionIndex,
        Func<string, bool> isHeaderRow,
        string timeColumnName)
    {
      string path = VelumLogPaths.ResolveLogFile(csvFileName);
      if (!File.Exists(path))
        return new List<Dictionary<string, string>>();

      var blocks = ReadBlocks(path, isHeaderRow, timeColumnName);
      if (sessionIndex < 0 || sessionIndex >= blocks.Count)
        return new List<Dictionary<string, string>>();
      return blocks[sessionIndex].Rows;
    }

    /// <summary>
    /// Удаляет из CSV-файла блоки указанных сессий (индексы — по порядку блоков в файле),
    /// сохраняя все остальные строки (включая чужие сессии, заголовки и пустые строки).
    /// Возвращает число фактически удалённых сессий.
    /// </summary>
    public static int DeleteSessions(
        string csvFileName,
        IEnumerable<int> sessionIndices,
        Func<string, bool> isHeaderRow)
    {
      string path = VelumLogPaths.ResolveLogFile(csvFileName);
      if (!File.Exists(path))
        return 0;

      var toDelete = new HashSet<int>(sessionIndices ?? Enumerable.Empty<int>());
      if (toDelete.Count == 0)
        return 0;

      List<string> lines;
      using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
      using (var reader = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
      {
        lines = new List<string>();
        string line;
        while ((line = reader.ReadLine()) != null)
          lines.Add(line);
      }

      var kept = VelumLogSessionRules.KeepLinesExceptSessions(lines, isHeaderRow, toDelete);

      using (var writer = new StreamWriter(path, append: false, encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)))
      {
        foreach (string line in kept)
          writer.WriteLine(line);
      }

      return toDelete.Count;
    }

    /// <summary>Разбор временной метки (ISO или <c>dd.MM.yyyy</c>).</summary>
    public static bool TryParseTimestamp(string raw, out DateTime timestamp)
    {
      timestamp = default(DateTime);
      if (string.IsNullOrWhiteSpace(raw))
        return false;
      if (DateTime.TryParseExact(raw, TimeFormats, CultureInfo.InvariantCulture,
              DateTimeStyles.AssumeLocal, out timestamp))
        return true;
      return DateTime.TryParse(raw, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out timestamp);
    }

    /// <summary>Разбор целого с значением по умолчанию.</summary>
    public static int ParseInt(string raw, int defaultValue = 0)
    {
      if (string.IsNullOrWhiteSpace(raw))
        return defaultValue;
      return int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)
          ? v
          : defaultValue;
    }

    /// <summary>Разбор вещественного (запятая или точка как разделитель).</summary>
    public static float ParseFloat(string raw)
    {
      if (string.IsNullOrWhiteSpace(raw))
        return 0f;
      raw = raw.Trim().Replace(',', '.');
      return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;
    }

    /// <summary>Чтение строк файла с общим доступом и снятием BOM.</summary>
    public static IEnumerable<string> ReadLinesShared(string path)
    {
      using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
      using (var reader = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
      {
        string line;
        while ((line = reader.ReadLine()) != null)
          yield return StripBom(line);
      }
    }

    /// <summary>Снятие символа BOM в начале строки.</summary>
    public static string StripBom(string line)
    {
      if (string.IsNullOrEmpty(line))
        return line;
      return line[0] == '\uFEFF' ? line.Substring(1) : line;
    }

    private sealed class Block
    {
      public List<Dictionary<string, string>> Rows { get; } = new List<Dictionary<string, string>>();
      public int RowCount { get; set; }
      public DateTime StartedLocal { get; set; }
      public DateTime EndedLocal { get; set; }
    }

    private static List<Block> ReadBlocks(string path, Func<string, bool> isHeaderRow, string timeColumnName)
    {
      var blocks = new List<Block>();
      Block current = null;
      Dictionary<string, int> columns = null;
      foreach (string line in ReadLinesShared(path))
      {
        if (string.IsNullOrWhiteSpace(line))
          continue;
        if (isHeaderRow(line))
        {
          current = new Block();
          blocks.Add(current);
          columns = ParseHeaderColumns(line);
          continue;
        }
        if (current == null || columns == null)
          continue;
        var row = ParseDataRow(line, columns, timeColumnName);
        if (row == null)
          continue;
        current.Rows.Add(row);
        current.RowCount++;
        if (row.TryGetValue(timeColumnName, out string tsRaw) && TryParseTimestamp(tsRaw, out DateTime ts))
        {
          if (current.RowCount == 1)
            current.StartedLocal = ts;
          current.EndedLocal = ts;
        }
      }
      return blocks;
    }

    private static Dictionary<string, int> ParseHeaderColumns(string headerLine)
    {
      var parts = StripBom(headerLine ?? string.Empty).Split(';');
      var map = new Dictionary<string, int>(StringComparer.Ordinal);
      for (int i = 0; i < parts.Length; i++)
      {
        string name = parts[i].Trim();
        if (string.IsNullOrEmpty(name) || map.ContainsKey(name))
          continue;
        map[name] = i;
      }
      return map;
    }

    private static Dictionary<string, string> ParseDataRow(
        string line,
        Dictionary<string, int> columns,
        string timeColumnName)
    {
      var parts = line.Split(';');
      if (parts.Length < 3)
        return null;
      if (!columns.TryGetValue(timeColumnName, out int timeIx) || timeIx >= parts.Length)
        return null;
      if (!TryParseTimestamp(parts[timeIx]?.Trim(), out _))
        return null;

      var row = new Dictionary<string, string>(StringComparer.Ordinal);
      foreach (var kv in columns)
        row[kv.Key] = kv.Value < parts.Length ? (parts[kv.Value]?.Trim() ?? string.Empty) : string.Empty;
      return row;
    }
  }

  /// <summary>
  /// Сессии системного лога агента (<c>AgentLogs.csv</c>, при пустом CSV — <c>AgentLogs.jsonl</c>).
  /// </summary>
  internal static class VelumAgentLogFileSessions
  {
    private static readonly string[] TimeFormats =
    {
      "yyyy-MM-dd HH:mm:ss",
      "dd.MM.yyyy HH:mm:ss"
    };

    /// <summary>Список сессий, от новых к старым.</summary>
    public static IReadOnlyList<VelumLogFileSessionInfo> ListFileSessions()
    {
      var csv = VelumCsvLogSessionReader.ListSessions(
          VelumLogPaths.AgentLogCsvFileName, IsHeaderRow, "Время");
      if (csv.Count > 0)
        return csv;

      string jsonlPath = Path.ChangeExtension(
          VelumLogPaths.ResolveLogFile(VelumLogPaths.AgentLogCsvFileName), ".jsonl");
      if (File.Exists(jsonlPath))
        return ListSessionsFromJsonl(jsonlPath);
      return csv;
    }

    /// <summary>Записи указанной сессии.</summary>
    public static List<VelumAgentLogEntry> LoadSessionEntries(int sessionIndex)
    {
      var rows = VelumCsvLogSessionReader.ReadSessionRows(
          VelumLogPaths.AgentLogCsvFileName, sessionIndex, IsHeaderRow, "Время");
      if (rows.Count > 0)
        return ParseRows(rows);

      string jsonlPath = Path.ChangeExtension(
          VelumLogPaths.ResolveLogFile(VelumLogPaths.AgentLogCsvFileName), ".jsonl");
      if (File.Exists(jsonlPath))
        return LoadSessionEntriesFromJsonl(jsonlPath, sessionIndex);
      return new List<VelumAgentLogEntry>();
    }

    /// <summary>Объединение нескольких сессий.</summary>
    public static List<VelumAgentLogEntry> LoadMergedSessions(IEnumerable<int> sessionIndices)
    {
      var list = new List<VelumAgentLogEntry>();
      if (sessionIndices == null)
        return list;
      foreach (int ix in sessionIndices)
        list.AddRange(LoadSessionEntries(ix));
      return list.OrderByDescending(e => e.Timestamp).ToList();
    }

    /// <summary>
    /// Удаляет указанные сессии из системного лога. Если данные берутся из CSV —
    /// удаляются соответствующие блоки; иначе (только JSONL) файл не трогается.
    /// Возвращает удалённые сессии (для поиска связанных отчётов).
    /// </summary>
    public static List<VelumLogFileSessionInfo> DeleteSessions(IEnumerable<int> sessionIndices)
    {
      var indices = new List<int>(sessionIndices ?? Enumerable.Empty<int>());
      if (indices.Count == 0)
        return new List<VelumLogFileSessionInfo>();

      var all = ListFileSessions();
      var removed = all.Where(s => indices.Contains(s.SessionIndex)).ToList();

      if (File.Exists(VelumLogPaths.ResolveLogFile(VelumLogPaths.AgentLogCsvFileName)))
        VelumCsvLogSessionReader.DeleteSessions(VelumLogPaths.AgentLogCsvFileName, indices, IsHeaderRow);

      return removed;
    }

    private static bool IsHeaderRow(string line)
    {
      line = line ?? string.Empty;
      return line.IndexOf("Автоматизм", StringComparison.Ordinal) >= 0
             && line.IndexOf("Время", StringComparison.Ordinal) >= 0
             && line.IndexOf("Пульс", StringComparison.Ordinal) >= 0
             && line.IndexOf(';') >= 0;
    }

    private static List<VelumAgentLogEntry> ParseRows(List<Dictionary<string, string>> rows)
    {
      var list = new List<VelumAgentLogEntry>();
      foreach (var row in rows)
      {
        if (!VelumCsvLogSessionReader.TryParseTimestamp(Get(row, "Время"), out DateTime ts))
          continue;
        list.Add(new VelumAgentLogEntry
        {
          Timestamp = ts,
          Pulse = ParseNullableInt(Get(row, "Пульс")),
          BaseId = ParseNullableInt(Get(row, "Состояние")),
          BaseStyleId = ParseNullableInt(Get(row, "Стили")),
          ThinkingThemeTypeId = ParseNullableInt(Get(row, "Тема")),
          TriggerStimulusId = ParseNullableInt(Get(row, "Триггер")),
          InformationEnvironmentDanger = Get(row, "Опасно") == "1",
          InformationEnvironmentVeryActual = Get(row, "Актуально") == "1",
          GeneticReflexId = ParseNullableInt(Get(row, "Б/у рефлекс")),
          ConditionReflexId = ParseNullableInt(Get(row, "Усл. рефлекс")),
          AutomatizmId = ParseNullableInt(Get(row, "Автоматизм")),
          AutomatizmUsefulnessAtSnapshot = ParseNullableInt(Get(row, "Полезность")),
          ReflexChainInfo = Get(row, "Цепочка РФ"),
          AutomatizmChainInfo = Get(row, "Цепочка АВ"),
          MainThinkingCycleId = ParseNullableInt(Get(row, "Цикл М")),
          BackgroundThinkingCyclesJson = NullIfEmpty(Get(row, "Циклы Ф")),
          EnvironmentPressureCell = NullIfEmpty(Get(row, "Среда")),
          EnvironmentPressureTooltip = NullIfEmpty(Get(row, "Среда_подсказка"))
        });
        ParseOrUm(Get(row, "ОР"), Get(row, "УМ"), Get(row, "УМ_успех"),
            out int? orientation, out int? thinking, out bool? thinkingOk);
        var entry = list[list.Count - 1];
        entry.OrientationReflexType = orientation;
        entry.ThinkingLevel = thinking;
        entry.ThinkingLevelSuccess = thinkingOk;
      }
      return list;
    }

    private static IReadOnlyList<VelumLogFileSessionInfo> ListSessionsFromJsonl(string jsonlPath)
    {
      var blocks = ReadJsonlSessionBlocks(jsonlPath);
      var list = new List<VelumLogFileSessionInfo>();
      for (int i = 0; i < blocks.Count; i++)
      {
        if (blocks[i].Entries.Count == 0)
          continue;
        list.Add(new VelumLogFileSessionInfo
        {
          SessionKey = i.ToString(CultureInfo.InvariantCulture),
          SessionIndex = i,
          StartedLocal = blocks[i].StartedLocal,
          EndedLocal = blocks[i].EndedLocal,
          EntryCount = blocks[i].Entries.Count
        });
      }
      return list.OrderByDescending(s => s.StartedLocal).ToList();
    }

    private static List<VelumAgentLogEntry> LoadSessionEntriesFromJsonl(string jsonlPath, int sessionIndex)
    {
      var blocks = ReadJsonlSessionBlocks(jsonlPath);
      if (sessionIndex < 0 || sessionIndex >= blocks.Count)
        return new List<VelumAgentLogEntry>();
      return blocks[sessionIndex].Entries;
    }

    private sealed class JsonlSessionBlock
    {
      public List<VelumAgentLogEntry> Entries { get; } = new List<VelumAgentLogEntry>();
      public DateTime StartedLocal { get; set; }
      public DateTime EndedLocal { get; set; }
    }

    private static List<JsonlSessionBlock> ReadJsonlSessionBlocks(string jsonlPath)
    {
      var blocks = new List<JsonlSessionBlock>();
      JsonlSessionBlock current = null;
      int? lastPulse = null;
      foreach (string line in VelumCsvLogSessionReader.ReadLinesShared(jsonlPath))
      {
        if (string.IsNullOrWhiteSpace(line) || line[0] != '{')
          continue;
        var entry = TryParseJsonlRow(line);
        if (entry == null)
          continue;
        int pulse = entry.Pulse ?? 0;
        bool newSession = current == null
                          || (pulse == 1 && lastPulse.HasValue && lastPulse.Value > 1);
        if (newSession)
        {
          current = new JsonlSessionBlock();
          blocks.Add(current);
          lastPulse = null;
        }
        if (current.Entries.Count == 0)
          current.StartedLocal = entry.Timestamp;
        current.Entries.Add(entry);
        current.EndedLocal = entry.Timestamp;
        lastPulse = pulse;
      }
      return blocks;
    }

    private static VelumAgentLogEntry TryParseJsonlRow(string line)
    {
      try
      {
        var jo = JObject.Parse(line);
        string timeRaw = (string)jo["Время"];
        if (!TryParseTimestamp(timeRaw, out DateTime timestamp))
          return null;
        string um = (string)jo["УМ"] ?? "";
        string or = (string)jo["ОР"] ?? "";
        string umOk = (string)jo["УМ_успех"];
        ParseOrUm(or, um, umOk, out int? orientation, out int? thinking, out bool? thinkingOk);
        return new VelumAgentLogEntry
        {
          Timestamp = timestamp,
          Pulse = ParseNullableInt((string)jo["Пульс"]),
          BaseId = ParseNullableInt((string)jo["Состояние"]),
          BaseStyleId = ParseNullableInt((string)jo["Стили"]),
          ThinkingThemeTypeId = ParseNullableInt((string)jo["Тема"]),
          TriggerStimulusId = ParseNullableInt((string)jo["Триггер"]),
          InformationEnvironmentDanger = (string)jo["Опасно"] == "1",
          InformationEnvironmentVeryActual = (string)jo["Актуально"] == "1",
          GeneticReflexId = ParseNullableInt((string)jo["Б/у рефлекс"]),
          ConditionReflexId = ParseNullableInt((string)jo["Усл. рефлекс"]),
          AutomatizmId = ParseNullableInt((string)jo["Автоматизм"]),
          AutomatizmUsefulnessAtSnapshot = ParseNullableInt((string)jo["Полезность"]),
          ReflexChainInfo = (string)jo["Цепочка РФ"] ?? string.Empty,
          AutomatizmChainInfo = (string)jo["Цепочка АВ"] ?? string.Empty,
          OrientationReflexType = orientation,
          ThinkingLevel = thinking,
          ThinkingLevelSuccess = thinkingOk,
          MainThinkingCycleId = ParseNullableInt((string)jo["Цикл М"]),
          BackgroundThinkingCyclesJson = NullIfEmpty((string)jo["Циклы Ф"]),
          EnvironmentPressureCell = NullIfEmpty((string)jo["Среда"]),
          EnvironmentPressureTooltip = NullIfEmpty((string)jo["Среда_подсказка"])
        };
      }
      catch
      {
        return null;
      }
    }

    private static void ParseOrUm(
        string orCol,
        string umCol,
        string umSuccessCol,
        out int? orientationReflexType,
        out int? thinkingLevel,
        out bool? thinkingLevelSuccess)
    {
      orientationReflexType = null;
      thinkingLevel = null;
      thinkingLevelSuccess = null;
      string um = (umCol ?? string.Empty).Trim();
      if (um == "УМ1" || um == "1")
      {
        thinkingLevel = 1;
        thinkingLevelSuccess = ParseNullableBool(umSuccessCol);
        return;
      }
      if (um == "УМ2" || um == "2")
      {
        thinkingLevel = 2;
        thinkingLevelSuccess = ParseNullableBool(umSuccessCol);
        return;
      }
      string or = (orCol ?? string.Empty).Trim();
      if (or == "ОР1" || or == "1")
        orientationReflexType = 1;
      else if (or == "ОР2" || or == "2")
        orientationReflexType = 2;
      else if (int.TryParse(or, NumberStyles.Integer, CultureInfo.InvariantCulture, out int orNum) && orNum > 0)
        orientationReflexType = orNum;
    }

    private static bool TryParseTimestamp(string raw, out DateTime timestamp)
    {
      timestamp = default(DateTime);
      if (string.IsNullOrWhiteSpace(raw))
        return false;
      if (DateTime.TryParseExact(raw, TimeFormats, CultureInfo.InvariantCulture,
              DateTimeStyles.AssumeLocal, out timestamp))
        return true;
      return DateTime.TryParse(raw, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out timestamp);
    }

    private static string Get(Dictionary<string, string> row, string key)
    {
      return row.TryGetValue(key, out string v) ? v : string.Empty;
    }

    private static int? ParseNullableInt(string raw)
    {
      if (string.IsNullOrWhiteSpace(raw))
        return null;
      return int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)
          ? (int?)v
          : null;
    }

    private static bool? ParseNullableBool(string raw)
    {
      if (string.IsNullOrWhiteSpace(raw))
        return null;
      raw = raw.Trim();
      if (raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase))
        return true;
      if (raw == "0" || string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase))
        return false;
      return null;
    }

    private static string NullIfEmpty(string s)
    {
      return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
  }

  /// <summary>
  /// Сессии лога стилей поведения (<c>AgentLogs_Styles.csv</c>).
  /// </summary>
  internal static class VelumStyleLogFileSessions
  {
    /// <summary>Список сессий, от новых к старым.</summary>
    public static IReadOnlyList<VelumLogFileSessionInfo> ListFileSessions()
    {
      return VelumCsvLogSessionReader.ListSessions(
          VelumLogPaths.StylesLogCsvFileName, IsHeaderRow, "Time");
    }

    /// <summary>Данные указанной сессии.</summary>
    public static VelumStyleLogSessionData LoadSessionData(int sessionIndex)
    {
      var rows = VelumCsvLogSessionReader.ReadSessionRows(
          VelumLogPaths.StylesLogCsvFileName, sessionIndex, IsHeaderRow, "Time");
      return ParseRows(rows);
    }

    /// <summary>Объединение нескольких сессий.</summary>
    public static VelumStyleLogSessionData LoadMergedSessions(IEnumerable<int> sessionIndices)
    {
      var data = new VelumStyleLogSessionData();
      if (sessionIndices == null)
        return data;
      foreach (int ix in sessionIndices)
      {
        var part = LoadSessionData(ix);
        data.StyleEntries.AddRange(part.StyleEntries);
        data.Activations.AddRange(part.Activations);
      }
      return data;
    }

    /// <summary>
    /// Удаляет указанные сессии из лога стилей. Возвращает удалённые сессии
    /// (для поиска связанных отчётов).
    /// </summary>
    public static List<VelumLogFileSessionInfo> DeleteSessions(IEnumerable<int> sessionIndices)
    {
      var indices = new List<int>(sessionIndices ?? Enumerable.Empty<int>());
      if (indices.Count == 0)
        return new List<VelumLogFileSessionInfo>();

      var removed = ListFileSessions().Where(s => indices.Contains(s.SessionIndex)).ToList();
      VelumCsvLogSessionReader.DeleteSessions(VelumLogPaths.StylesLogCsvFileName, indices, IsHeaderRow);
      return removed;
    }

    private static bool IsHeaderRow(string line)
    {
      line = line ?? string.Empty;
      return line.IndexOf("ActivationDetails", StringComparison.Ordinal) >= 0
             && line.IndexOf("Time", StringComparison.Ordinal) >= 0
             && line.IndexOf("Pulse", StringComparison.Ordinal) >= 0
             && line.IndexOf(';') >= 0;
    }

    private static VelumStyleLogSessionData ParseRows(List<Dictionary<string, string>> rows)
    {
      var data = new VelumStyleLogSessionData();
      foreach (var row in rows)
      {
        if (!VelumCsvLogSessionReader.TryParseTimestamp(Get(row, "Time"), out DateTime ts))
          continue;
        int pulse = VelumCsvLogSessionReader.ParseInt(Get(row, "Pulse"));
        string stage = Get(row, "Stage");
        if (string.Equals(stage, "Final", StringComparison.OrdinalIgnoreCase))
        {
          data.StyleEntries.Add(new VelumStyleLogEntry
          {
            Timestamp = ts,
            Pulse = pulse,
            StyleId = VelumCsvLogSessionReader.ParseInt(Get(row, "StyleId")),
            StyleName = Get(row, "StyleName")
          });
          continue;
        }
        if (string.Equals(stage, "ParameterActivation", StringComparison.OrdinalIgnoreCase))
        {
          data.Activations.Add(new VelumStyleParameterActivationEntry
          {
            Timestamp = ts,
            Pulse = pulse,
            ParameterId = VelumCsvLogSessionReader.ParseInt(Get(row, "ParameterId")),
            ParameterName = Get(row, "ParameterName"),
            ZoneId = VelumCsvLogSessionReader.ParseInt(Get(row, "ZoneId")),
            ZoneDescription = Get(row, "ZoneDescription"),
            StyleId = VelumCsvLogSessionReader.ParseInt(Get(row, "StyleId")),
            StyleName = Get(row, "StyleName")
          });
        }
      }
      return data;
    }

    private static string Get(Dictionary<string, string> row, string key)
    {
      return row.TryGetValue(key, out string v) ? v : string.Empty;
    }
  }

  /// <summary>
  /// Сессии лога параметров гомеостаза (<c>AgentLogs_Parameters.csv</c>).
  /// </summary>
  internal static class VelumParameterLogFileSessions
  {
    /// <summary>Список сессий, от новых к старым.</summary>
    public static IReadOnlyList<VelumLogFileSessionInfo> ListFileSessions()
    {
      return VelumCsvLogSessionReader.ListSessions(
          VelumLogPaths.ParametersLogCsvFileName, IsHeaderRow, "Time");
    }

    /// <summary>Записи указанной сессии.</summary>
    public static List<VelumParameterLogEntry> LoadSessionEntries(int sessionIndex)
    {
      var rows = VelumCsvLogSessionReader.ReadSessionRows(
          VelumLogPaths.ParametersLogCsvFileName, sessionIndex, IsHeaderRow, "Time");
      return ParseRows(rows);
    }

    /// <summary>Объединение нескольких сессий.</summary>
    public static List<VelumParameterLogEntry> LoadMergedSessions(IEnumerable<int> sessionIndices)
    {
      var list = new List<VelumParameterLogEntry>();
      if (sessionIndices == null)
        return list;
      foreach (int ix in sessionIndices)
        list.AddRange(LoadSessionEntries(ix));
      return list;
    }

    /// <summary>
    /// Удаляет указанные сессии из лога параметров. Возвращает удалённые сессии
    /// (для поиска связанных отчётов).
    /// </summary>
    public static List<VelumLogFileSessionInfo> DeleteSessions(IEnumerable<int> sessionIndices)
    {
      var indices = new List<int>(sessionIndices ?? Enumerable.Empty<int>());
      if (indices.Count == 0)
        return new List<VelumLogFileSessionInfo>();

      var removed = ListFileSessions().Where(s => indices.Contains(s.SessionIndex)).ToList();
      VelumCsvLogSessionReader.DeleteSessions(VelumLogPaths.ParametersLogCsvFileName, indices, IsHeaderRow);
      return removed;
    }

    private static bool IsHeaderRow(string line)
    {
      line = line ?? string.Empty;
      return line.IndexOf("ActivationZone", StringComparison.Ordinal) >= 0
             && line.IndexOf("Time", StringComparison.Ordinal) >= 0
             && line.IndexOf("Pulse", StringComparison.Ordinal) >= 0
             && line.IndexOf(';') >= 0;
    }

    private static List<VelumParameterLogEntry> ParseRows(List<Dictionary<string, string>> rows)
    {
      var list = new List<VelumParameterLogEntry>();
      foreach (var row in rows)
      {
        if (!VelumCsvLogSessionReader.TryParseTimestamp(Get(row, "Time"), out DateTime ts))
          continue;
        list.Add(new VelumParameterLogEntry
        {
          Timestamp = ts,
          Pulse = VelumCsvLogSessionReader.ParseInt(Get(row, "Pulse")),
          ParamId = VelumCsvLogSessionReader.ParseInt(Get(row, "ParamId")),
          ParamName = Get(row, "ParamName"),
          Weight = VelumCsvLogSessionReader.ParseInt(Get(row, "Weight")),
          NormaWell = VelumCsvLogSessionReader.ParseInt(Get(row, "NormaWell")),
          Speed = VelumCsvLogSessionReader.ParseInt(Get(row, "Speed")),
          Value = VelumCsvLogSessionReader.ParseFloat(Get(row, "Value")),
          UrgencyFunction = VelumCsvLogSessionReader.ParseFloat(Get(row, "UrgencyFunction")),
          ParameterState = Get(row, "ParameterState"),
          ActivationZone = Get(row, "ActivationZone")
        });
      }
      return list;
    }

    private static string Get(Dictionary<string, string> row, string key)
    {
      return row.TryGetValue(key, out string v) ? v : string.Empty;
    }
  }
}
