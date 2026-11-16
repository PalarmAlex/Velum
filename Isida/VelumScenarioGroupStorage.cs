using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ISIDA.Common;
using ISIDA.Scenarios;
using Velum.Configuration;

namespace Velum.Isida
{
  /// <summary>
  /// Чтение реестра и строк групп сценариев оператора из каталога <c>{корень ISIDA}\Scenarios</c>
  /// (тот же формат <c>.dat</c>, что создаёт AIStudio).
  /// </summary>
  internal static class VelumScenarioGroupStorage
  {
    private const string GroupRegistryFormatHeader = "# SCENARIO_GROUP_REGISTRY_FORMAT|";
    private const string GroupLinesFormatHeader = "# SCENARIO_GROUP_LINES_FORMAT|";
    private const string GroupRegistryFileName = "ScenarioGroupRegistry.dat";
    private const string GroupLinesFilePrefix = "ScenarioGroup_";
    private const string GroupLinesFileSuffix = ".dat";

    /// <summary>Каталог групп: корень проекта данных Velum (или ISIDA по умолчанию) + «Scenarios».</summary>
    internal static string RootFolder => VelumScenarioStorage.RootFolder;

    internal static string RegistryPath => Path.Combine(RootFolder, GroupRegistryFileName);

    internal static string LinesPath(int groupId) =>
        Path.Combine(RootFolder, GroupLinesFilePrefix + groupId.ToString(CultureInfo.InvariantCulture) + GroupLinesFileSuffix);

    /// <summary>Загружает записи реестра групп (пустой список, если файла нет).</summary>
    internal static List<VelumScenarioGroupHeader> LoadGroupRegistry()
    {
      var list = new List<VelumScenarioGroupHeader>();
      string path = RegistryPath;
      if (!File.Exists(path))
        return list;

      foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
      {
        string t = line?.Trim();
        if (string.IsNullOrEmpty(t) || t.StartsWith("#", StringComparison.Ordinal))
          continue;
        string[] p = t.Split('|');
        if (p.Length < 3)
          continue;
        if (!int.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
          continue;
        list.Add(new VelumScenarioGroupHeader
        {
          Id = id,
          Title = Unescape(p[1]),
          Description = Unescape(p[2])
        });
      }

      return list.OrderBy(h => h.Id).ToList();
    }

    /// <summary>Загружает полный документ группы (шапка + состав).</summary>
    internal static VelumScenarioGroupDocument LoadGroup(int groupId)
    {
      string path = LinesPath(groupId);
      if (!File.Exists(path))
        throw new FileNotFoundException("Файл группы сценариев не найден", path);

      return ParseGroupFromLines(File.ReadAllLines(path, Encoding.UTF8), groupId);
    }

    /// <summary>Парсинг содержимого файла группы из строк.</summary>
    internal static VelumScenarioGroupDocument ParseGroupFromLines(string[] lines, int provisionalId)
    {
      var doc = new VelumScenarioGroupDocument { Id = provisionalId };

      foreach (string line in lines)
      {
        string t = line?.Trim();
        if (string.IsNullOrEmpty(t) || !t.StartsWith("#", StringComparison.Ordinal))
          continue;

        if (t.StartsWith("# SCENARIO_GROUP_META|", StringComparison.Ordinal))
        {
          string[] meta = t.Substring("# SCENARIO_GROUP_META|".Length).Split('|');
          if (meta.Length >= 1)
            doc.Title = Unescape(meta[0]);
          if (meta.Length >= 2)
            doc.Description = Unescape(meta[1]);
          if (meta.Length >= 3
              && int.TryParse(meta[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int coeff))
          {
            if (coeff == 1 || coeff == 5 || coeff == 10 || coeff == 20 || coeff == 50 || coeff == 100)
              doc.RunPulseTimingCoefficient = coeff;
          }
          continue;
        }

        if (t.StartsWith("# SCENARIO_GROUP_MEMBER|", StringComparison.Ordinal))
        {
          string[] p = t.Substring("# SCENARIO_GROUP_MEMBER|".Length).Split('|');
          if (p.Length < 7)
            continue;
          if (!int.TryParse(p[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int sort))
            continue;
          if (!int.TryParse(p[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int sid))
            continue;

          int.TryParse(p[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int pst);

          var row = new VelumScenarioGroupMember
          {
            SortOrderInGroup = sort,
            ScenarioId = sid,
            PreRunTargetStage = pst,
            PreRunClearAgentData = Parse01(p[3]),
            PreRunNormalHomeostasisState = Parse01(p[4]),
            ScenarioObservationMode = Parse01(p[5]),
            ScenarioAuthoritativeRecording = Parse01(p[6])
          };
          doc.Members.Add(row);
        }
      }

      return doc;
    }

    private static bool Parse01(string s)
    {
      return int.TryParse(s?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) && v != 0;
    }

    /// <summary>Следующий свободный ID группы.</summary>
    internal static int NextGroupId()
    {
      var list = LoadGroupRegistry();
      if (list.Count == 0)
        return 1;
      return list.Max(h => h.Id) + 1;
    }

    /// <summary>Загружает составленный документ для прогона: подставляет параметры слота в одиночный сценарий.</summary>
    internal static ScenarioDocument LoadMemberForRun(int groupId, int memberIndex)
    {
      VelumScenarioGroupDocument groupDoc = LoadGroup(groupId);
      if (memberIndex < 0 || memberIndex >= groupDoc.Members.Count)
        throw new ArgumentOutOfRangeException(nameof(memberIndex));

      VelumScenarioGroupMember member = groupDoc.Members[memberIndex];
      ScenarioDocument source = VelumScenarioStorage.LoadScenario(member.ScenarioId);
      return VelumScenarioGroupDocument.ApplyMemberToScenario(source, member, groupDoc.RunPulseTimingCoefficient);
    }

    /// <summary>Количество членов в группе.</summary>
    internal static int GetMemberCount(int groupId)
    {
      try
      {
        return LoadGroup(groupId).Members.Count;
      }
      catch
      {
        return 0;
      }
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