using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ISIDA.Common;
using ISIDA.Scenarios;
using Velum.Isida.Logs;
using Newtonsoft.Json;

namespace Velum.Isida
{
  /// <summary>
  /// Один номер цикла мышления на пульсе с последним известным статусом (для отчёта).
  /// </summary>
  internal sealed class MainCyclePulseSegment
  {
    internal int Id { get; set; }
    internal string TaskStatus { get; set; }
    internal string Tooltip { get; set; }
    internal int Weight { get; set; }
  }

  /// <summary>
  /// Слияние записей лога по глобальному пульсу и сравнение с ожиданиями сценария.
  /// Дублирует логику AIStudio.Common.ScenarioLogComparer, недоступного из Velum.
  /// </summary>
  internal static class VelumScenarioLogComparer
  {
    /// <summary>
    /// Агрегированный снимок лога на один глобальный пульс.
    /// </summary>
    internal sealed class AggregatedLogSnapshot
    {
      internal string State { get; set; } = "-";
      internal string Style { get; set; } = "-";
      internal string Theme { get; set; } = "-";
      internal string Trigger { get; set; } = "-";
      internal string OrUm { get; set; } = "-";
      internal bool? OrUmThinkingSuccess { get; set; }
      internal string Danger { get; set; } = "-";
      internal string VeryActual { get; set; } = "-";
      internal string GeneticReflex { get; set; } = "-";
      internal string ConditionReflex { get; set; } = "-";
      internal string Automatizm { get; set; } = "-";
      internal int? AutomatizmUsefulnessLogged { get; set; }
      internal string ReflexChain { get; set; } = "-";
      internal string AutomatizmChain { get; set; } = "-";
      internal string MainCycle { get; set; } = "-";
      internal List<MainCyclePulseSegment> MainCycleSegments { get; } = new List<MainCyclePulseSegment>();
      internal string BackgroundCycles { get; set; } = "-";
      internal List<MainCyclePulseSegment> BackgroundCycleSegments { get; } = new List<MainCyclePulseSegment>();
      internal string ThemeTooltip { get; set; }
      internal string MainCycleTooltip { get; set; }
      internal string BackgroundCyclesTooltip { get; set; }
    }

    /// <summary>
    /// Результат сравнения одного шага сценария с фактическим логом.
    /// </summary>
    internal sealed class StepCompareResult
    {
      internal int StepIndex { get; set; }
      internal int PulseWithinScenario { get; set; }
      internal int GlobalPulse { get; set; }
      internal bool Ok { get; set; }
      internal string Details { get; set; } = "";
    }

    // ===== Парсинг AgentLogs.csv =====

    /// <summary>
    /// Запись сырой строки CSV AgentLogs.csv (без отображений).
    /// </summary>
    internal sealed class RawCsvLogEntry
    {
      internal DateTime Timestamp { get; set; }
      internal int? Pulse { get; set; }
      internal int? BaseID { get; set; }
      internal int? BaseStyleID { get; set; }
      internal int? TriggerStimulusID { get; set; }
      internal int? OrientationReflexType { get; set; }
      internal int? ThinkingLevel { get; set; }
      internal bool? ThinkingLevelSuccess { get; set; }
      internal int? GeneticReflexID { get; set; }
      internal int? ConditionReflexID { get; set; }
      internal int? AutomatizmID { get; set; }
      internal int? AutomatizmUsefulnessAtSnapshot { get; set; }
      internal string ReflexChainInfo { get; set; }
      internal string AutomatizmChainInfo { get; set; }
      internal int? ThinkingThemeTypeId { get; set; }
      internal string ThinkingThemeTooltip { get; set; }
      internal int? MainThinkingCycleId { get; set; }
      internal string MainThinkingCycleTaskStatus { get; set; }
      internal string MainThinkingCycleTooltip { get; set; }
      internal string BackgroundThinkingCyclesJson { get; set; }
      internal bool? InformationEnvironmentDanger { get; set; }
      internal bool? InformationEnvironmentVeryActual { get; set; }
    }

    /// <summary>
    /// Загрузка сырых записей из AgentLogs.csv, разделённых по сессиям (границы по пульсу 1).
    /// </summary>
    internal static List<List<RawCsvLogEntry>> LoadSessionBlocks()
    {
      var path = VelumLogPaths.ResolveLogFile(VelumLogPaths.AgentLogCsvFileName);
      if (!File.Exists(path))
        return new List<List<RawCsvLogEntry>>();

      try
      {
        var blocks = new List<List<RawCsvLogEntry>>();
        List<RawCsvLogEntry> current = null;
        Dictionary<string, int> columns = null;
        int? lastPulse = null;

        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var reader = new StreamReader(fs, Encoding.UTF8, true))
        {
          string line;
          while ((line = reader.ReadLine()) != null)
          {
            if (string.IsNullOrWhiteSpace(line))
              continue;
            line = StripBom(line);

            if (IsHeaderRow(line))
            {
              // Новая сессия (повторяющийся заголовок)
              current = new List<RawCsvLogEntry>();
              blocks.Add(current);
              columns = ParseHeaderColumns(line);
              lastPulse = null;
              continue;
            }
            if (current == null || columns == null)
              continue;

            var entry = TryParseDataRow(line, columns);
            if (entry == null)
              continue;

            int pulse = entry.Pulse ?? 0;
            // Граница сессии: пульс 1 после того, как был > 1
            if (pulse == 1 && lastPulse.HasValue && lastPulse.Value > 1)
            {
              current = new List<RawCsvLogEntry>();
              blocks.Add(current);
              lastPulse = null;
            }

            current.Add(entry);
            lastPulse = pulse;
          }
        }
        return blocks;
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum: ошибка чтения AgentLogs.csv: " + ex.Message);
        return new List<List<RawCsvLogEntry>>();
      }
    }

    private static string StripBom(string line)
    {
      if (string.IsNullOrEmpty(line)) return line;
      if (line[0] == '\uFEFF') return line.Substring(1);
      return line;
    }

    private static bool IsHeaderRow(string line)
    {
      return line.IndexOf("Автоматизм", StringComparison.Ordinal) >= 0
             && line.IndexOf("Время", StringComparison.Ordinal) >= 0
             && line.IndexOf("Пульс", StringComparison.Ordinal) >= 0
             && line.IndexOf(';') >= 0;
    }

    private static Dictionary<string, int> ParseHeaderColumns(string headerLine)
    {
      var parts = StripBom(headerLine ?? string.Empty).Split(';');
      var map = new Dictionary<string, int>(StringComparer.Ordinal);
      for (int i = 0; i < parts.Length; i++)
      {
        var name = parts[i].Trim();
        if (string.IsNullOrEmpty(name) || map.ContainsKey(name))
          continue;
        map[name] = i;
      }
      return map;
    }

    private static RawCsvLogEntry TryParseDataRow(string line, Dictionary<string, int> columns)
    {
      var parts = line.Split(';');
      string Get(string name)
      {
        if (!columns.TryGetValue(name, out int ix) || ix >= parts.Length)
          return string.Empty;
        return parts[ix]?.Trim() ?? string.Empty;
      }

      if (!TryParseTimestamp(Get("Время"), out DateTime timestamp))
        return null;

      ParseOrUm(Get("ОР"), Get("УМ"), Get("УМ_успех"),
          out int? orientationReflexType, out int? thinkingLevel, out bool? thinkingLevelSuccess);

      return new RawCsvLogEntry
      {
        Timestamp = timestamp,
        Pulse = ParseNullableInt(Get("Пульс")),
        BaseID = ParseNullableInt(Get("Состояние")),
        BaseStyleID = ParseNullableInt(Get("Стили")),
        TriggerStimulusID = ParseNullableInt(Get("Триггер")),
        OrientationReflexType = orientationReflexType,
        ThinkingLevel = thinkingLevel,
        ThinkingLevelSuccess = thinkingLevelSuccess,
        GeneticReflexID = ParseNullableInt(Get("Б/у рефлекс")),
        ConditionReflexID = ParseNullableInt(Get("Усл. рефлекс")),
        AutomatizmID = ParseNullableInt(Get("Автоматизм")),
        AutomatizmUsefulnessAtSnapshot = ParseNullableInt(Get("Полезность")),
        ReflexChainInfo = Get("Цепочка РФ"),
        AutomatizmChainInfo = Get("Цепочка АВ"),
        ThinkingThemeTypeId = ParseNullableInt(Get("Тема")),
        ThinkingThemeTooltip = string.IsNullOrWhiteSpace(Get("Тема_подсказка")) ? null : Get("Тема_подсказка").Trim(),
        MainThinkingCycleId = ParseNullableInt(Get("Цикл М")),
        MainThinkingCycleTaskStatus = string.IsNullOrWhiteSpace(Get("ЦиклМ_задача")) ? null : Get("ЦиклМ_задача").Trim(),
        MainThinkingCycleTooltip = string.IsNullOrWhiteSpace(Get("ЦиклМ_тема")) ? null : Get("ЦиклМ_тема").Trim(),
        BackgroundThinkingCyclesJson = string.IsNullOrWhiteSpace(Get("ЦиклыФ_json")) ? null : Get("ЦиклыФ_json"),
        InformationEnvironmentDanger = Get("Опасно") == "1",
        InformationEnvironmentVeryActual = Get("Актуально") == "1"
      };
    }

    private static void ParseOrUm(string orCol, string umCol, string umSuccessCol,
        out int? orientationReflexType, out int? thinkingLevel, out bool? thinkingLevelSuccess)
    {
      orientationReflexType = null;
      thinkingLevel = null;
      thinkingLevelSuccess = null;
      var um = (umCol ?? string.Empty).Trim();
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
      var or = (orCol ?? string.Empty).Trim();
      if (or == "ОР1" || or == "1")
        orientationReflexType = 1;
      else if (or == "ОР2" || or == "2")
        orientationReflexType = 2;
      else if (int.TryParse(or, NumberStyles.Integer, CultureInfo.InvariantCulture, out int orNum) && orNum > 0)
        orientationReflexType = orNum;
    }

    private static bool TryParseTimestamp(string raw, out DateTime timestamp)
    {
      timestamp = default;
      if (string.IsNullOrWhiteSpace(raw))
        return false;
      return DateTime.TryParseExact(raw,
          new[] { "yyyy-MM-dd HH:mm:ss", "dd.MM.yyyy HH:mm:ss" },
          CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out timestamp)
             || DateTime.TryParse(raw, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out timestamp);
    }

    private static int? ParseNullableInt(string raw)
    {
      if (string.IsNullOrWhiteSpace(raw)) return null;
      if (int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
        return v;
      return null;
    }

    private static bool? ParseNullableBool(string raw)
    {
      if (string.IsNullOrWhiteSpace(raw)) return null;
      raw = raw.Trim();
      if (raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)) return true;
      if (raw == "0" || string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase)) return false;
      return null;
    }

    // ===== Отображение сырой записи (Display-свойства) =====

    private static string DisplayBaseID(int? baseId) => baseId?.ToString() ?? "-";
    private static string DisplayBaseStyleID(int? baseStyleId) => baseStyleId?.ToString() ?? "-";
    private static string DisplayTriggerStimulusID(int? triggerId) => triggerId?.ToString() ?? "-";
    private static string DisplayGeneticReflexID(int? geneticReflexId) => geneticReflexId?.ToString() ?? "-";
    private static string DisplayConditionReflexID(int? condReflexId)
    {
      if (!condReflexId.HasValue) return "-";
      return condReflexId.Value < 0 ? "-" : condReflexId.Value.ToString(CultureInfo.InvariantCulture);
    }
    private static string DisplayAutomatizmID(int? automatizmId) => automatizmId?.ToString() ?? "-";
    private static string DisplayDanger(bool? danger) => danger == true ? "1" : "0";
    private static string DisplayVeryActual(bool? veryActual) => veryActual == true ? "1" : "0";
    private static string DisplayOrUm(RawCsvLogEntry e)
    {
      if (e.ThinkingLevel.HasValue && (e.ThinkingLevel.Value == 1 || e.ThinkingLevel.Value == 2))
        return e.ThinkingLevel.Value == 1 ? "УМ1" : "УМ2";
      if (e.OrientationReflexType.HasValue)
      {
        int t = e.OrientationReflexType.Value;
        if (t == 1) return "ОР1";
        if (t == 2) return "ОР2";
        return "ОР" + t.ToString(CultureInfo.InvariantCulture);
      }
      return "-";
    }
    private static string DisplayReflexChainInfo(string chain) => !string.IsNullOrEmpty(chain) ? chain : "-";
    private static string DisplayAutomatizmChainInfo(string chain) => !string.IsNullOrEmpty(chain) ? chain : "-";
    private static string DisplayThinkingThemeId(int? themeId) => themeId.HasValue && themeId.Value > 0 ? themeId.Value.ToString(CultureInfo.InvariantCulture) : "-";
    private static string DisplayMainThinkingCycle(int? cycleId) => cycleId.HasValue && cycleId.Value > 0 ? cycleId.Value.ToString(CultureInfo.InvariantCulture) : "-";

    // ===== Агрегация по пульсу =====

    /// <summary>
    /// Агрегация записей, попавших во временное окно прогона <c>[from; to]</c>.
    /// Нужна потому, что предзапуск обнуляет глобальный счётчик пульсов (<c>GlobalTimer.Reset</c>):
    /// без временной границы записи старого и нового прогонов получают одинаковые номера пульсов
    /// и склеиваются в один снапшот (фантомные автоматизмы/ОР в отчёте).
    /// При <c>from == default</c> или <c>to == default</c> возвращает весь буфер (обратная совместимость).
    /// </summary>
    internal static Dictionary<int, AggregatedLogSnapshot> AggregateByPulseInWindow(DateTime from, DateTime to)
    {
      var blocks = LoadSessionBlocks();
      var all = blocks.SelectMany(b => b).ToList();
      IEnumerable<RawCsvLogEntry> window = all;
      if (from != default && to != default)
        window = all.Where(e => e.Timestamp >= from && e.Timestamp <= to);
      return AggregateByPulse(window);
    }

    /// <summary>
    /// Группирует записи по пульсу: для каждого поля берётся последнее не «-» значение по времени.
    /// </summary>
    internal static Dictionary<int, AggregatedLogSnapshot> AggregateByPulse(
        IEnumerable<RawCsvLogEntry> entries)
    {
      var result = new Dictionary<int, AggregatedLogSnapshot>();
      if (entries == null)
        return result;

      foreach (var g in entries.Where(e => e.Pulse.HasValue).GroupBy(e => e.Pulse.Value))
      {
        var ordered = g.OrderBy(e => e.Timestamp).ToList();
        var snap = new AggregatedLogSnapshot();

        foreach (var e in ordered)
        {
          snap.State = MergeField(snap.State, DisplayBaseID(e.BaseID));
          snap.Style = MergeField(snap.Style, DisplayBaseStyleID(e.BaseStyleID));

          var themeCand = NormalizeDisplay(DisplayThinkingThemeId(e.ThinkingThemeTypeId));
          if (themeCand != "-")
          {
            snap.Theme = themeCand;
            if (!string.IsNullOrWhiteSpace(e.ThinkingThemeTooltip))
              snap.ThemeTooltip = e.ThinkingThemeTooltip;
          }

          snap.Trigger = MergeField(snap.Trigger, DisplayTriggerStimulusID(e.TriggerStimulusID));
          MergeOrUm(snap, e);
          snap.Danger = MergeField(snap.Danger, DisplayDanger(e.InformationEnvironmentDanger));
          snap.VeryActual = MergeField(snap.VeryActual, DisplayVeryActual(e.InformationEnvironmentVeryActual));
          snap.GeneticReflex = MergeField(snap.GeneticReflex, DisplayGeneticReflexID(e.GeneticReflexID));
          snap.ConditionReflex = MergeField(snap.ConditionReflex, DisplayConditionReflexID(e.ConditionReflexID));
          snap.Automatizm = MergeField(snap.Automatizm, DisplayAutomatizmID(e.AutomatizmID));

          if (NormalizeDisplay(DisplayAutomatizmID(e.AutomatizmID)) != "-")
            snap.AutomatizmUsefulnessLogged = e.AutomatizmUsefulnessAtSnapshot;

          snap.ReflexChain = MergeField(snap.ReflexChain, DisplayReflexChainInfo(e.ReflexChainInfo));
          snap.AutomatizmChain = MergeField(snap.AutomatizmChain, DisplayAutomatizmChainInfo(e.AutomatizmChainInfo));

          var mainCand = NormalizeDisplay(DisplayMainThinkingCycle(e.MainThinkingCycleId));
          if (mainCand != "-")
          {
            snap.MainCycle = mainCand;
            if (!string.IsNullOrWhiteSpace(e.MainThinkingCycleTooltip))
              snap.MainCycleTooltip = e.MainThinkingCycleTooltip;
          }

          if (e.MainThinkingCycleId.HasValue && e.MainThinkingCycleId.Value > 0)
          {
            var st = string.IsNullOrWhiteSpace(e.MainThinkingCycleTaskStatus)
                ? "NoSolution"
                : e.MainThinkingCycleTaskStatus.Trim();
            int idx = snap.MainCycleSegments.FindIndex(s => s.Id == e.MainThinkingCycleId.Value);
            if (idx >= 0)
            {
              snap.MainCycleSegments[idx].TaskStatus = st;
            }
            else
            {
              snap.MainCycleSegments.Add(new MainCyclePulseSegment
              {
                Id = e.MainThinkingCycleId.Value,
                TaskStatus = st,
                Tooltip = string.IsNullOrWhiteSpace(e.MainThinkingCycleTooltip) ? null : e.MainThinkingCycleTooltip.Trim()
              });
            }
            snap.MainCycle = string.Join(", ", snap.MainCycleSegments.Select(s => s.Id.ToString(CultureInfo.InvariantCulture)));
          }

          if (!string.IsNullOrWhiteSpace(e.BackgroundThinkingCyclesJson))
          {
            snap.BackgroundCycleSegments.Clear();
            foreach (var it in TryParseBackgroundCyclesJson(e.BackgroundThinkingCyclesJson))
            {
              if (it.Id <= 0) continue;
              var st = string.IsNullOrWhiteSpace(it.TaskStatus) ? "NoSolution" : it.TaskStatus.Trim();
              snap.BackgroundCycleSegments.Add(new MainCyclePulseSegment
              {
                Id = it.Id,
                TaskStatus = st,
                Tooltip = string.IsNullOrWhiteSpace(it.Tooltip) ? null : it.Tooltip.Trim(),
                Weight = it.Weight
              });
            }
            if (snap.BackgroundCycleSegments.Count > 0)
            {
              var orderedBg = snap.BackgroundCycleSegments.OrderByDescending(s => s.Weight).ThenBy(s => s.Id).ToList();
              snap.BackgroundCycles = string.Join(", ", orderedBg.Select(s => s.Id.ToString(CultureInfo.InvariantCulture)));
              var bgTipParts = orderedBg
                  .Select(s => string.IsNullOrEmpty(s.Tooltip) ? null : $"Цикл {s.Id}: {s.Tooltip}")
                  .Where(x => !string.IsNullOrEmpty(x))
                  .ToList();
              snap.BackgroundCyclesTooltip = bgTipParts.Count > 0 ? string.Join("\n\n", bgTipParts) : null;
            }
            else
            {
              snap.BackgroundCycles = "-";
              snap.BackgroundCyclesTooltip = null;
            }
          }
        }

        // Подавление рефлексов при выбранном автоматизме
        if (int.TryParse(NormalizeDisplay(snap.Automatizm), NumberStyles.Integer, CultureInfo.InvariantCulture, out int automatizmId)
            && automatizmId > 0)
        {
          snap.GeneticReflex = "-";
          snap.ConditionReflex = "-";
          snap.ReflexChain = "-";
        }

        result[g.Key] = snap;
      }
      return result;
    }

    private static void MergeOrUm(AggregatedLogSnapshot snap, RawCsvLogEntry e)
    {
      var c = NormalizeDisplay(DisplayOrUm(e));
      if (c == "-") return;
      snap.OrUm = c;
      snap.OrUmThinkingSuccess = c == "УМ1" || c == "УМ2" ? e.ThinkingLevelSuccess : null;
    }

    private static string MergeField(string current, string candidate)
    {
      var c = NormalizeDisplay(candidate);
      return c != "-" ? c : current;
    }

    // ===== ResolveSnapshot =====

    /// <summary>
    /// Для пульса без записи в логе подтягивает непрерывные поля с ближайшего предшествующего пульса.
    /// </summary>
    internal static AggregatedLogSnapshot ResolveSnapshot(
        int globalPulse,
        IReadOnlyDictionary<int, AggregatedLogSnapshot> byGlobalPulse)
    {
      if (byGlobalPulse.TryGetValue(globalPulse, out var exact))
        return exact;

      var snap = new AggregatedLogSnapshot();
      int nearest = -1;
      foreach (var key in byGlobalPulse.Keys)
      {
        if (key < globalPulse && key > nearest)
          nearest = key;
      }
      if (nearest >= 0 && byGlobalPulse.TryGetValue(nearest, out var prev))
      {
        snap.State = prev.State;
        snap.Style = prev.Style;
        snap.Danger = prev.Danger;
        snap.VeryActual = prev.VeryActual;
      }
      return snap;
    }

    // ===== Утилиты сравнения =====

    internal static string NormalizeDisplay(string raw)
    {
      if (string.IsNullOrWhiteSpace(raw)) return "-";
      var t = raw.Trim();
      return t.Length == 0 ? "-" : t;
    }

    internal static bool ExpectationCellMatches(string expectedRaw, string actualDisplayNormalized)
    {
      if (string.IsNullOrWhiteSpace(expectedRaw))
        return true;
      var e = expectedRaw.Trim();
      var a = actualDisplayNormalized ?? "-";
      if (e.IndexOf('|') < 0)
        return string.Equals(e, a, StringComparison.Ordinal);
      foreach (var part in e.Split('|'))
      {
        var t = part.Trim();
        if (t.Length > 0 && string.Equals(t, a, StringComparison.Ordinal))
          return true;
      }
      return false;
    }

    internal static bool MainCycleExpectationMatches(string expectedRaw, string actualDisplayNormalized)
    {
      if (string.IsNullOrWhiteSpace(expectedRaw))
        return true;
      var a = actualDisplayNormalized ?? "-";
      if (ExpectationCellMatches(expectedRaw, a))
        return true;
      if (a == "-")
        return false;
      var actualTokens = new HashSet<string>(StringComparer.Ordinal);
      foreach (var tok in a.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0))
        actualTokens.Add(tok);
      if (actualTokens.Count == 0)
        return false;
      foreach (var part in expectedRaw.Split('|'))
      {
        var p = part.Trim();
        if (p.Length == 0) continue;
        if (p.IndexOf(',') >= 0)
        {
          var need = p.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).ToList();
          if (need.Count > 0 && need.All(t => actualTokens.Contains(t)))
            return true;
        }
        else if (actualTokens.Contains(p))
          return true;
      }
      return false;
    }

    internal static bool DangerExpectationMatches(string expectedRaw, string actualFromLog)
    {
      if (string.IsNullOrWhiteSpace(expectedRaw))
        return true;
      var a = NormalizeDisplay(actualFromLog);
      var aCanon = a == "1" ? "1" : "0";
      return DangerExpectationMatchesCanon(expectedRaw.Trim(), aCanon);
    }

    private static bool DangerExpectationMatchesCanon(string expectedTrimmed, string actualCanon01)
    {
      if (expectedTrimmed.IndexOf('|') < 0)
        return DangerTokenToCanon(expectedTrimmed) == actualCanon01;
      foreach (var part in expectedTrimmed.Split('|'))
      {
        var t = part.Trim();
        if (t.Length > 0 && DangerTokenToCanon(t) == actualCanon01)
          return true;
      }
      return false;
    }

    private static string DangerTokenToCanon(string token) => token == "1" ? "1" : "0";

    internal static string FormatAutomatizmUsefulnessCell(int? usefulnessLogged) =>
        usefulnessLogged.HasValue
            ? usefulnessLogged.Value.ToString(CultureInfo.InvariantCulture)
            : "-";

    // ===== Сравнение =====

    /// <summary>
    /// Сравнение шагов сценария с фактическими записями логов.
    /// </summary>
    internal static List<StepCompareResult> Compare(
        ScenarioDocument doc,
        int anchorGlobalPulse,
        IReadOnlyDictionary<int, AggregatedLogSnapshot> byGlobalPulse)
    {
      var list = new List<StepCompareResult>();
      if (doc?.Lines == null)
        return list;

      var expectations = doc.LogExpectations ?? new List<ScenarioLogExpectationRow>();

      foreach (var line in doc.Lines.OrderBy(l => l.StepIndex))
      {
        var step = line.StepIndex;
        var pulseWithin = line.PulseWithinScenario;
        var globalPulse = anchorGlobalPulse + pulseWithin;
        var exp = expectations.FirstOrDefault(e => e.StepIndex == step && e.PulseWithinScenario == pulseWithin)
            ?? expectations.FirstOrDefault(e => e.StepIndex == step);

        if (exp == null)
        {
          list.Add(new StepCompareResult
          {
            StepIndex = step,
            PulseWithinScenario = pulseWithin,
            GlobalPulse = globalPulse,
            Ok = true,
            Details = "Нет строки ожиданий — нечего сравнивать."
          });
          continue;
        }

        var actual = ResolveSnapshot(globalPulse, byGlobalPulse);
        var mismatches = new List<string>();

        void Check(string label, string expectedRaw, string actualVal)
        {
          if (string.IsNullOrWhiteSpace(expectedRaw))
            return;
          var e = expectedRaw.Trim();
          var a = NormalizeDisplay(actualVal ?? "");

          if (label == "Опасно" || label == "Актуально")
          {
            var aCanon = a == "1" ? "1" : "0";
            if (!DangerExpectationMatchesCanon(e, aCanon))
            {
              mismatches.Add($"{label}: ожид. «{FormatDangerComparisonCell(e)}», факт «{FormatDangerComparisonCell(aCanon)}»");
            }
            return;
          }

          if (label == "Цикл М" || label == "Циклы Ф")
          {
            if (!MainCycleExpectationMatches(expectedRaw, a))
              mismatches.Add($"{label}: ожид. «{e}», факт «{a}»");
            return;
          }

          if (!ExpectationCellMatches(expectedRaw, a))
          {
            var expPhrase = e;
            var factPhrase = a;
            if (label == "Состояние")
            {
              expPhrase = FormatStateCell(e);
              factPhrase = FormatStateCell(actualVal ?? "");
            }
            mismatches.Add($"{label}: ожид. «{expPhrase}», факт «{factPhrase}»");
          }
        }

        Check("Состояние", exp.StateText, actual.State);
        Check("Стиль", exp.StyleText, actual.Style);
        Check("Тема", exp.ThemeText, actual.Theme);
        Check("Триггер", exp.TriggerText, actual.Trigger);
        Check("ОР/УМ", exp.OrUmText, actual.OrUm);
        Check("Опасно", exp.DangerText, actual.Danger);
        Check("Актуально", exp.VeryActualText, actual.VeryActual);
        Check("Б/у рефлекс", exp.GeneticReflexText, actual.GeneticReflex);
        Check("Усл. рефлекс", exp.ConditionReflexText, actual.ConditionReflex);
        Check("Автоматизм", exp.AutomatizmText, actual.Automatizm);
        Check("Успешность", exp.AutomatizmUsefulnessText, FormatAutomatizmUsefulnessCell(actual.AutomatizmUsefulnessLogged));
        Check("Цепочка РФ", exp.ReflexChainText, actual.ReflexChain);
        Check("Цепочка АВ", exp.AutomatizmChainText, actual.AutomatizmChain);
        Check("Цикл М", exp.MainCycleText, actual.MainCycle);
        Check("Циклы Ф", exp.BackgroundCyclesText, actual.BackgroundCycles);

        bool ok = mismatches.Count == 0;
        var sb = new StringBuilder();
        sb.Append(ok ? "Все проверяемые поля совпали." : string.Join("; ", mismatches));

        list.Add(new StepCompareResult
        {
          StepIndex = step,
          PulseWithinScenario = pulseWithin,
          GlobalPulse = globalPulse,
          Ok = ok,
          Details = sb.ToString()
        });
      }

      return list;
    }

    // ===== Форматирование ячеек =====

    internal static string FormatStateCell(string raw)
    {
      var a = NormalizeDisplay(raw ?? "");
      switch (a)
      {
        case "-1": return "Плохо";
        case "0": return "Норма";
        case "1": return "Хорошо";
        default: return a;
      }
    }

    internal static string FormatDangerComparisonCell(string raw)
    {
      var a = NormalizeDisplay(raw ?? "");
      return a == "1" ? "1" : "-";
    }

    // ===== JSON фоновых циклов =====

    private sealed class BackgroundCycleJsonItem
    {
      internal int Id { get; set; }
      internal int Weight { get; set; }
      internal string TaskStatus { get; set; }
      internal string Tooltip { get; set; }
    }

    private static IEnumerable<BackgroundCycleJsonItem> TryParseBackgroundCyclesJson(string json)
    {
      if (string.IsNullOrWhiteSpace(json)) yield break;
      List<BackgroundCycleJsonItem> list;
      try { list = JsonConvert.DeserializeObject<List<BackgroundCycleJsonItem>>(json); }
      catch { yield break; }
      if (list == null) yield break;
      foreach (var x in list)
      {
        if (x != null && x.Id > 0)
          yield return x;
      }
    }
  }
}