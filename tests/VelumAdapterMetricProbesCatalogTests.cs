using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>
  /// Сверка метрик адаптера Velum со справочником воздействий среды
  /// <c>Data\Actions\InfluenceActions.dat</c> (поле ProbeKey, строки ID ≥ 50).
  /// <para>
  /// Метрики адаптера — объявленный им каталог <c>docs\adapter-package\schema\metric-probes.json</c>.
  /// Метрика без строки в справочнике нигде не давит на виталы и недоступна в редакторе
  /// «Давление среда на виталы»: проба опрашивается, но её результат никуда не уходит.
  /// </para>
  /// <para>
  /// Путь к справочнику не зашит абсолютным: как <c>VelumAppConfig.ResolveDataFolderPath()</c>,
  /// он берётся из настроек адаптера (<c>Settings.xml</c> → <c>DataFolderPath</c>, переменные
  /// окружения разворачиваются), при отсутствии настройки — <c>%ProgramData%\VELUM\Data</c>.
  /// Если пакет адаптера на машине не установлен, тест пропускается (ранний выход), а не падает.
  /// </para>
  /// </summary>
  public class VelumAdapterMetricProbesCatalogTests
  {
    private const string SchemaRelativePath =
        @"docs\adapter-package\schema\metric-probes.json";

    private const string InfluenceActionsFileName = "InfluenceActions.dat";

    /// <summary>Индекс поля ProbeKey в записи <c>ID|Имя|Описание|Воздействие|Антагонисты|ProbeKey|Active</c>.</summary>
    private const int ProbeKeyFieldIndex = 5;

    private const int IdFieldIndex = 0;

    [Fact]
    public void AllAdapterMetricProbes_AreDeclaredInInfluenceActions()
    {
      string influenceActionsPath = TryResolveInfluenceActionsPath();
      if (influenceActionsPath == null)
        return; // пакет адаптера не установлен — сверять не с чем

      List<string> declaredProbeKeys = ReadAdapterMetricProbeKeys();
      Dictionary<string, string> declaredByProbeKey =
          ReadInfluenceActionsProbeKeys(influenceActionsPath, out List<string> duplicatedProbeKeys);

      Assert.True(duplicatedProbeKeys.Count == 0,
          "ProbeKey объявлен в справочнике несколько раз (два воздействия давят на одну пробу): "
          + string.Join(", ", duplicatedProbeKeys));
      Assert.NotEmpty(declaredByProbeKey);

      List<string> missing = declaredProbeKeys
          .Where(key => !declaredByProbeKey.ContainsKey(key))
          .ToList();

      Assert.True(missing.Count == 0,
          "Метрики адаптера объявлены в " + SchemaRelativePath + ", но для них нет строки с ProbeKey в "
          + influenceActionsPath + ": " + string.Join(", ", missing)
          + ". Добавьте воздействия в справочник (ID ≥ 50) либо уберите ключи из каталога метрик.");
    }

    /// <summary>
    /// Обратная сторона: <c>metric-probes.json</c> обязан совпадать с метриками, заложенными
    /// в коде адаптера (константы <c>*ProbeKey*</c>). Ключ в коде, но не в каталоге — проба
    /// опрашивается, но недоступна в редакторе «Давление среды на виталы»; ключ в каталоге,
    /// но не в коде — мёртвая строка в справочнике и пустая плашка у оператора.
    /// </summary>
    [Fact]
    public void MetricProbesCatalog_MatchesProbeKeysInAdapterSource()
    {
      List<string> catalogProbeKeys = ReadAdapterMetricProbeKeys();
      List<string> sourceProbeKeys = ReadProbeKeysFromAdapterSource();

      Assert.NotEmpty(sourceProbeKeys);

      HashSet<string> catalog = new HashSet<string>(catalogProbeKeys, StringComparer.Ordinal);
      HashSet<string> source = new HashSet<string>(sourceProbeKeys, StringComparer.Ordinal);

      List<string> onlyInSource = source.Where(key => !catalog.Contains(key)).OrderBy(key => key, StringComparer.Ordinal).ToList();
      List<string> onlyInCatalog = catalog.Where(key => !source.Contains(key)).OrderBy(key => key, StringComparer.Ordinal).ToList();

      Assert.True(onlyInSource.Count == 0 && onlyInCatalog.Count == 0,
          SchemaRelativePath + " рассинхронизирован с константами ProbeKey в коде адаптера."
          + (onlyInSource.Count == 0 ? string.Empty
              : " Нет в каталоге (проба есть в коде): " + string.Join(", ", onlyInSource) + ".")
          + (onlyInCatalog.Count == 0 ? string.Empty
              : " Нет в коде (мёртвый ключ в каталоге): " + string.Join(", ", onlyInCatalog) + "."));
    }

    // ─────────────────────────── helpers ───────────────────────────

    /// <summary>
    /// Константы ProbeKey из исходников адаптера: строки вида
    /// <c>const string …ProbeKey… = "Velum.…"</c> во всех файлах кроме каталога тестов
    /// (в тестах дублируются ключи-строки для assert'ов — они не объявления метрик).
    /// </summary>
    private static List<string> ReadProbeKeysFromAdapterSource()
    {
      string repoRoot = FindRepositoryRoot();
      var regex = new Regex(
          "const\\s+string\\s+\\w*ProbeKey\\w*\\s*=\\s*\"(?<key>Velum\\.[\\w.]+)\"",
          RegexOptions.CultureInvariant);

      var keys = new HashSet<string>(StringComparer.Ordinal);
      foreach (string file in Directory.EnumerateFiles(repoRoot, "*.cs", SearchOption.AllDirectories))
      {
        if (IsUnderTestsFolder(file, repoRoot))
          continue;

        foreach (Match match in regex.Matches(File.ReadAllText(file)))
          keys.Add(match.Groups["key"].Value);
      }

      return keys.ToList();
    }

    /// <summary>Каталог проекта — родитель каталога <c>tests</c> (отсчёт от каталога тестовых сборок вверх).</summary>
    private static string FindRepositoryRoot()
    {
      string schemaPath = FindAdapterPackagePath(SchemaRelativePath);
      Assert.True(schemaPath != null,
          "Не найден каталог метрик " + SchemaRelativePath + " выше по дереву от "
          + AppContext.BaseDirectory);

      // …\docs\adapter-package\schema\metric-probes.json → корень проекта (Velum\).
      return new DirectoryInfo(Path.GetDirectoryName(Path.GetDirectoryName(
          Path.GetDirectoryName(schemaPath)))).Parent.FullName;
    }

    private static bool IsUnderTestsFolder(string filePath, string repoRoot)
    {
      string testsDir = Path.Combine(repoRoot, "tests") + Path.DirectorySeparatorChar;
      return filePath.StartsWith(testsDir, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Ключи метрик, объявленных адаптером (schema/metric-probes.json из дерева репозитория).</summary>
    private static List<string> ReadAdapterMetricProbeKeys()
    {
      string schemaPath = FindAdapterPackagePath(SchemaRelativePath);
      Assert.True(schemaPath != null,
          "Не найден каталог метрик " + SchemaRelativePath + " выше по дереву от "
          + AppContext.BaseDirectory);

      JArray probes = (JArray) JObject.Parse(File.ReadAllText(schemaPath))["probes"];
      Assert.True(probes != null, "В " + schemaPath + " нет массива probes[]");

      return probes
          .Select(p => ((string) p["key"] ?? string.Empty).Trim())
          .Where(key => key.Length > 0)
          .Distinct(StringComparer.Ordinal)
          .ToList();
    }

    /// <summary>
    /// Непустые ProbeKey справочника → ID строки. Дубли не заносятся в словарь,
    /// а собираются в <paramref name="duplicatedProbeKeys"/>.
    /// </summary>
    private static Dictionary<string, string> ReadInfluenceActionsProbeKeys(
        string influenceActionsPath,
        out List<string> duplicatedProbeKeys)
    {
      var byProbeKey = new Dictionary<string, string>(StringComparer.Ordinal);
      duplicatedProbeKeys = new List<string>();

      foreach (KeyValuePair<string, string> row in ReadProbeKeyRows(influenceActionsPath))
      {
        if (byProbeKey.ContainsKey(row.Key))
        {
          duplicatedProbeKeys.Add(row.Key + " (ID " + byProbeKey[row.Key] + " и " + row.Value + ")");
          continue;
        }

        byProbeKey[row.Key] = row.Value;
      }

      return byProbeKey;
    }

    /// <summary>Строки записей справочника: ProbeKey + ID (комментарии «#» и пустые строки пропускаются).</summary>
    private static List<KeyValuePair<string, string>> ReadProbeKeyRows(string influenceActionsPath)
    {
      var rows = new List<KeyValuePair<string, string>>();
      foreach (string raw in File.ReadAllLines(influenceActionsPath))
      {
        string line = (raw ?? string.Empty).Trim();
        if (line.Length == 0 || line[0] == '#')
          continue;

        string[] parts = line.Split('|');
        if (parts.Length <= ProbeKeyFieldIndex)
          continue;

        string probeKey = parts[ProbeKeyFieldIndex].Trim();
        if (probeKey.Length == 0)
          continue;

        rows.Add(new KeyValuePair<string, string>(probeKey, parts[IdFieldIndex].Trim()));
      }

      return rows;
    }

    /// <summary>
    /// Справочник из настроек адаптера: <c>Settings.xml</c> → <c>DataFolderPath</c>
    /// (переменные окружения разворачиваются), иначе <c>%ProgramData%\VELUM\Data</c>;
    /// далее <c>Actions\InfluenceActions.dat</c>. null — файла нет на этой машине.
    /// </summary>
    private static string TryResolveInfluenceActionsPath()
    {
      string dataFolder = ResolveDataFolderPathFromSettings();
      if (string.IsNullOrWhiteSpace(dataFolder))
        return null;

      string path = Path.Combine(dataFolder, "Actions", InfluenceActionsFileName);
      return File.Exists(path) ? path : null;
    }

    private static string ResolveDataFolderPathFromSettings()
    {
      string programData =
          Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

      string settingsFile = Path.Combine(programData, "VELUM", "Settings", "Settings.xml");
      string fromSettings = TryReadDataFolderPathSetting(settingsFile);
      if (!string.IsNullOrWhiteSpace(fromSettings))
        return fromSettings;

      // Как ResolveDataFolderPath в VelumAppConfig: нет настройки — корень по умолчанию.
      return Path.Combine(programData, "VELUM", "Data");
    }

    /// <summary>
    /// Значение <c>DataFolderPath</c> из Settings.xml с разворачиванием переменных окружения;
    /// null — файла/ключа нет или XML нечитаемый (то же молчание, что и у <c>VelumAppConfig</c>).
    /// </summary>
    private static string TryReadDataFolderPathSetting(string settingsFile)
    {
      if (!File.Exists(settingsFile))
        return null;

      try
      {
        XElement setting = XDocument.Load(settingsFile).Root?
            .Element("AppSettings")?
            .Element("DataFolderPath");
        if (setting == null || string.IsNullOrWhiteSpace(setting.Value))
          return null;

        return Environment.ExpandEnvironmentVariables(setting.Value.Trim());
      }
      catch (Exception)
      {
        return null;
      }
    }

    /// <summary>Поиск файла пакета адаптера от каталога тестов вверх до корня репозитория.</summary>
    private static string FindAdapterPackagePath(string relativePath)
    {
      for (DirectoryInfo dir = new DirectoryInfo(AppContext.BaseDirectory);
           dir != null;
           dir = dir.Parent)
      {
        string candidate = Path.Combine(dir.FullName, relativePath);
        if (File.Exists(candidate))
          return candidate;
      }

      return null;
    }
  }
}
