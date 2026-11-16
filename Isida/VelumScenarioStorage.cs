using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ISIDA.Common;
using ISIDA.Reflexes;
using ISIDA.Scenarios;
using Velum.Configuration;

namespace Velum.Isida
{
  /// <summary>
  /// Чтение реестра и строк сценариев оператора из каталога <c>{корень ISIDA}\Scenarios</c>
  /// (тот же формат <c>.dat</c> с разделителем «|», что создаёт AIStudio). Velum использует
  /// сценарии только для прогонов (редактирование — в AIStudio), поэтому реализовано чтение.
  /// </summary>
  internal static class VelumScenarioStorage
  {
    private const string RegistryFormatHeader = "# SCENARIO_REGISTRY_FORMAT|";
    private const string LinesFormatHeader = "# SCENARIO_LINES_FORMAT|";
    private const string RegistryFileName = "ScenarioRegistry.dat";
    private const string ScenarioLinesFilePrefix = "Scenario_";
    private const string ScenarioLinesFileSuffix = ".dat";

    /// <summary>Каталог сценариев: корень проекта данных Velum (или ISIDA по умолчанию) + «Scenarios».</summary>
    internal static string RootFolder => IsidaDataPaths.ResolveScenariosFolder(ResolveIsidaRoot());

    internal static string RegistryPath => Path.Combine(RootFolder, RegistryFileName);

    internal static string LinesPath(int scenarioId) =>
        Path.Combine(RootFolder, ScenarioLinesFilePrefix + scenarioId.ToString(CultureInfo.InvariantCulture) + ScenarioLinesFileSuffix);

    /// <summary>Загружает записи реестра сценариев (пустой список, если файла нет).</summary>
    internal static List<ScenarioHeader> LoadRegistry()
    {
      var list = new List<ScenarioHeader>();
      string path = RegistryPath;
      if (!File.Exists(path))
        return list;

      foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
      {
        string t = line?.Trim();
        if (string.IsNullOrEmpty(t) || t.StartsWith("#", StringComparison.Ordinal))
          continue;
        string[] p = t.Split('|');
        if (p.Length < 4)
          continue;
        if (!int.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
          continue;
        int pst = -1;
        if (int.TryParse(p[3].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedPst))
          pst = parsedPst;
        list.Add(new ScenarioHeader
        {
          Id = id,
          Title = Unescape(p[1]),
          Description = Unescape(p[2]),
          PreRunTargetStage = pst
        });
      }

      return list.OrderBy(h => h.Id).ToList();
    }

    /// <summary>Загружает полный документ сценария (шапка + строки + ожидания логов).</summary>
    internal static ScenarioDocument LoadScenario(int scenarioId)
    {
      string path = LinesPath(scenarioId);
      if (!File.Exists(path))
        throw new FileNotFoundException("Файл строк сценария не найден", path);

      var doc = new ScenarioDocument();
      doc.Header.Id = scenarioId;
      bool expectationMode = false;

      foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
      {
        string t = line?.Trim();
        if (string.IsNullOrEmpty(t))
          continue;
        if (t.StartsWith(LinesFormatHeader, StringComparison.Ordinal))
          continue;

        if (expectationMode)
        {
          if (t.StartsWith("# COL_SKIP|", StringComparison.Ordinal))
          {
            ParseLogExpectationColSkip(t, doc);
            continue;
          }
          if (t.StartsWith("#", StringComparison.Ordinal))
            continue;
          TryParseLogExpectationRow(t, doc);
          continue;
        }

        if (t.StartsWith("# SCENARIO_LOG_EXPECTATIONS", StringComparison.Ordinal))
        {
          doc.LogExpectationColumnSkips = doc.LogExpectationColumnSkips ?? new ScenarioLogExpectationColumnSkips();
          doc.LogExpectations = doc.LogExpectations ?? new List<ScenarioLogExpectationRow>();
          continue;
        }

        if (t.StartsWith("#", StringComparison.Ordinal))
        {
          if (t.StartsWith("# SCENARIO_META|", StringComparison.Ordinal))
            ApplyScenarioMeta(doc.Header, t.Substring("# SCENARIO_META|".Length).Split('|'));
          continue;
        }

        ScenarioLineRow row = ParseLineRow(t);
        if (row != null)
          doc.Lines.Add(row);
      }

      return doc;
    }

    private static string ResolveIsidaRoot()
    {
      if (IsidaDataPaths.TryGetProjectRootFromDataFolderPath(VelumAppConfig.DataFolderPath, out string projectRoot))
        return projectRoot;
      return IsidaDataPaths.GetDefaultIsidaRoot();
    }

    private static void ApplyScenarioMeta(ScenarioHeader header, string[] meta)
    {
      if (header == null || meta == null)
        return;
      if (meta.Length >= 2)
      {
        header.Title = Unescape(meta[0]);
        header.Description = Unescape(meta[1]);
      }
      if (meta.Length < 3)
        return;
      const int homeoValuesIndex = 2;
      header.InitialHomeostasisValues = Unescape(meta[homeoValuesIndex]);
      if (meta.Length < homeoValuesIndex + 8)
        return;
      int i = homeoValuesIndex;
      if (int.TryParse(meta[i + 1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int pst))
        header.PreRunTargetStage = pst;
      header.PreRunClearAgentData =
          int.TryParse(meta[i + 2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int cl) && cl != 0;
      header.ScenarioObservationMode =
          int.TryParse(meta[i + 3].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int om) && om != 0;
      header.ScenarioAuthoritativeRecording =
          int.TryParse(meta[i + 4].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int ar) && ar != 0;
      header.PreRunNormalHomeostasisState =
          int.TryParse(meta[i + 5].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int nh) && nh != 0;
      if (int.TryParse(meta[i + 6].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int psi)
          && (psi == 1 || psi == 2 || psi == 3 || psi == 4))
        header.PulseStepIncrement = psi;
      if (int.TryParse(meta[i + 7].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int ptc)
          && (ptc == 1 || ptc == 5 || ptc == 10 || ptc == 20 || ptc == 50 || ptc == 100))
        header.RunPulseTimingCoefficient = ptc;
      if (meta.Length > i + 8
          && int.TryParse(meta[i + 8].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int rEmpty))
        header.ReportHideEmptyComparisonColumns = rEmpty != 0;
      if (meta.Length > i + 9
          && int.TryParse(meta[i + 9].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int rExp))
        header.ReportHideExpectedWhenNoMismatch = rExp != 0;
    }

    private static ScenarioLineRow ParseLineRow(string line)
    {
      string[] p = line.Split('|');
      if (p.Length < 8)
        return null;
      if (!int.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int step))
        return null;
      if (!int.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int pulse))
        return null;
      ScenarioLineKind kind = p[2].Trim().Equals("W", StringComparison.OrdinalIgnoreCase)
          ? ScenarioLineKind.WaitClick
          : ScenarioLineKind.Pult;
      if (!int.TryParse(p[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int tone))
        return null;
      if (!int.TryParse(p[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int mood))
        return null;
      var actions = string.IsNullOrWhiteSpace(p[5])
          ? new List<int>()
          : p[5].Split(',').Select(s => s.Trim()).Where(s => s.Length > 0)
              .Select(s => int.Parse(s, CultureInfo.InvariantCulture)).ToList();
      string phrase = Unescape(p[6]);
      if (!int.TryParse(p[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out int rw))
        return null;
      int visualColor = AgentVisualColor.White;
      if (p.Length > 8
          && int.TryParse(p[8].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int vc)
          && AgentVisualColor.IsValidCode(vc))
        visualColor = vc;
      var environmentProbes = p.Length > 9
          ? ScenarioEnvironmentProbeFormat.Parse(p[9])
          : new List<ScenarioEnvironmentProbeEntry>();
      var commandPatternIds = p.Length > 10 && !string.IsNullOrWhiteSpace(p[10])
          ? p[10].Split(',').Select(s => s.Trim()).Where(s => s.Length > 0)
              .Select(s => int.Parse(s, CultureInfo.InvariantCulture)).ToList()
          : new List<int>();
      int chainStepResultCode = p.Length > 11
          && int.TryParse(p[11].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int csr)
          ? csr
          : 0;

      return new ScenarioLineRow
      {
        StepIndex = step,
        PulseWithinScenario = pulse,
        Kind = kind,
        ToneId = tone,
        MoodId = mood,
        VisualColorId = visualColor,
        ActionIds = actions,
        EnvironmentProbes = environmentProbes,
        CommandPatternIds = commandPatternIds,
        Phrase = phrase,
        ResetWaitingPeriod = rw != 0,
        ChainStepResultCode = chainStepResultCode
      };
    }

    private static void ParseLogExpectationColSkip(string line, ScenarioDocument doc)
    {
      var sk = doc.LogExpectationColumnSkips ?? new ScenarioLogExpectationColumnSkips();
      doc.LogExpectationColumnSkips = sk;
      string[] p = line.Substring("# COL_SKIP|".Length).Split('|');
      if (p.Length < 11)
        return;
      bool Skip(int idx) =>
          int.TryParse(p[idx].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) && v != 0;
      sk.SkipState = Skip(0);
      sk.SkipStyle = Skip(1);
      sk.SkipTheme = Skip(2);
      sk.SkipTrigger = Skip(3);
      sk.SkipOrUm = Skip(4);
      sk.SkipGeneticReflex = Skip(5);
      sk.SkipConditionReflex = Skip(6);
      sk.SkipAutomatizm = Skip(7);
      sk.SkipReflexChain = Skip(8);
      sk.SkipAutomatizmChain = Skip(9);
      sk.SkipMainCycle = Skip(10);
      if (p.Length >= 12) sk.SkipDanger = Skip(11);
      if (p.Length >= 13) sk.SkipVeryActual = Skip(12);
      if (p.Length >= 14) sk.SkipAutomatizmUsefulness = Skip(13);
      if (p.Length >= 15) sk.SkipBackgroundCycles = Skip(14);
      if (p.Length >= 16) sk.SkipEnvironmentProbes = Skip(15);
    }

    private static void TryParseLogExpectationRow(string line, ScenarioDocument doc)
    {
      if (doc.LogExpectations == null)
        doc.LogExpectations = new List<ScenarioLogExpectationRow>();
      List<string> p = SplitLogExpectationLine(line);
      if (p.Count < 15)
        return;
      if (!int.TryParse(p[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int step))
        return;
      if (!int.TryParse(p[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int pulse))
        return;
      doc.LogExpectations.Add(new ScenarioLogExpectationRow
      {
        StepIndex = step,
        PulseWithinScenario = pulse,
        StateText = Unescape(p[2]),
        StyleText = Unescape(p[3]),
        ThemeText = Unescape(p[4]),
        TriggerText = Unescape(p[5]),
        OrUmText = Unescape(p[6]),
        DangerText = Unescape(p[7]),
        VeryActualText = Unescape(p[8]),
        GeneticReflexText = Unescape(p[9]),
        ConditionReflexText = Unescape(p[10]),
        AutomatizmText = Unescape(p[11]),
        ReflexChainText = Unescape(p[12]),
        AutomatizmChainText = Unescape(p[13]),
        MainCycleText = Unescape(p[14]),
        AutomatizmUsefulnessText = p.Count >= 16 ? Unescape(p[15]) : "-",
        BackgroundCyclesText = p.Count >= 17 ? Unescape(p[16]) : "-",
        EnvironmentProbesText = p.Count >= 18 ? Unescape(p[17]) : "-",
        CommandPatternsText = p.Count >= 19 ? Unescape(p[18]) : "-"
      });
    }

    /// <summary>Делит строку ожиданий по неэкранированному «|».</summary>
    private static List<string> SplitLogExpectationLine(string line)
    {
      var parts = new List<string>();
      var cur = new StringBuilder(line.Length);
      for (int i = 0; i < line.Length; i++)
      {
        char c = line[i];
        if (c != '|')
        {
          cur.Append(c);
          continue;
        }
        int backslashes = 0;
        for (int j = i - 1; j >= 0 && line[j] == '\\'; j--)
          backslashes++;
        if ((backslashes % 2) == 1)
        {
          cur.Append('|');
          continue;
        }
        parts.Add(cur.ToString());
        cur.Clear();
      }
      parts.Add(cur.ToString());
      return parts;
    }

    private static string Unescape(string s)
    {
      if (string.IsNullOrEmpty(s))
        return "";
      var sb = new StringBuilder(s.Length);
      for (int i = 0; i < s.Length; i++)
      {
        if (s[i] == '\\' && i + 1 < s.Length)
        {
          i++;
          if (s[i] == 'n')
            sb.Append('\n');
          else
            sb.Append(s[i]);
        }
        else
          sb.Append(s[i]);
      }
      return sb.ToString();
    }
  }
}