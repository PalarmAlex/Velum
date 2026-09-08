using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace Velum.UI
{
  /// <summary>
  /// Идентификаторы разделов пользовательской справки
  /// (%ProgramData%\VELUM\help, см. docs\help\topics.json).
  /// </summary>
  internal static class VelumHelpTopics
  {
    /// <summary>Оглавление / стартовая страница (index.html).</summary>
    public const string Index = "index";
    public const string Overview = "overview";
    public const string Platform = "platform";
    public const string Ribbon = "ribbon";
    public const string ListFilters = "list-filters";
    public const string ProjectSettings = "project-settings";
    public const string AssemblyRegistry = "assembly-registry";
    public const string AssemblyColumns = "assembly-columns";
    public const string AssemblyReports = "assembly-reports";
    public const string ProductRegistry = "product-registry";
    public const string ProductItem = "product-item";
    public const string ProductProblems = "product-problems";
    public const string ProductReports = "product-reports";
    public const string TechRequirements = "tech-requirements";
    public const string MaterialBatch = "material-batch";
    public const string DocumentProperties = "document-properties";
    public const string DxfBatch = "dxf-batch";
    public const string DxfExport = "dxf-export";
    public const string DxfSuffixes = "dxf-suffixes";
    public const string PdfBatch = "pdf-batch";
    public const string PdfExport = "pdf-export";
    public const string AgentTaskPane = "agent-taskpane";
    public const string EnvironmentMetrics = "environment-metrics";
    public const string OperatorInfluences = "operator-influences";
    public const string SensorBuffer = "sensor-buffer";

    private static readonly Dictionary<string, string> RelativePaths =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
          { Index, "index.html" },
          { Overview, "overview.html" },
          { Platform, "platform.html" },
          { Ribbon, "ribbon.html" },
          { ListFilters, Path.Combine("shared", "list-filters.html") },
          { ProjectSettings, Path.Combine("forms", "project-settings.html") },
          { AssemblyRegistry, Path.Combine("forms", "assembly-registry.html") },
          { AssemblyColumns, Path.Combine("forms", "assembly-columns.html") },
          { AssemblyReports, Path.Combine("forms", "assembly-reports.html") },
          { ProductRegistry, Path.Combine("forms", "product-registry.html") },
          { ProductItem, Path.Combine("forms", "product-item.html") },
          { ProductProblems, Path.Combine("forms", "product-problems.html") },
          { ProductReports, Path.Combine("forms", "product-reports.html") },
          { TechRequirements, Path.Combine("forms", "tech-requirements.html") },
          { MaterialBatch, Path.Combine("forms", "material-batch.html") },
          { DocumentProperties, Path.Combine("forms", "document-properties.html") },
          { DxfBatch, Path.Combine("forms", "dxf-batch.html") },
          { DxfExport, Path.Combine("forms", "dxf-export.html") },
          { DxfSuffixes, Path.Combine("forms", "dxf-suffixes.html") },
          { PdfBatch, Path.Combine("forms", "pdf-batch.html") },
          { PdfExport, Path.Combine("forms", "pdf-export.html") },
          { AgentTaskPane, Path.Combine("forms", "agent-taskpane.html") },
          { EnvironmentMetrics, Path.Combine("forms", "environment-metrics.html") },
          { OperatorInfluences, Path.Combine("forms", "operator-influences.html") },
          { SensorBuffer, Path.Combine("forms", "sensor-buffer.html") },
        };

    internal static bool TryGetRelativePath(string topicId, out string relativePath)
    {
      return RelativePaths.TryGetValue(topicId ?? string.Empty, out relativePath);
    }
  }

  /// <summary>Открытие HTML-справки из %ProgramData%\VELUM\help.</summary>
  internal static class VelumHelp
  {
    /// <summary>Показать раздел справки в модальном просмотрщике.</summary>
    internal static void Show(IWin32Window owner, string topicId)
    {
      string path = TryResolveTopicPath(topicId);
      if (string.IsNullOrEmpty(path))
      {
        MessageBox.Show(
            owner,
            "Не найден файл справки." + Environment.NewLine + Environment.NewLine +
            "Ожидаемый каталог: " + GetHelpRoot() + Environment.NewLine +
            "Раздел: " + (topicId ?? string.Empty),
            "Справка Velum",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      using (var form = new VelumHelpViewerForm(path, topicId))
        form.ShowDialog(owner);
    }

    /// <summary>
    /// Каталог справки: %ProgramData%\VELUM\help (установка),
    /// иначе {asm}\help / docs\help при отладке.
    /// </summary>
    internal static string GetHelpRoot()
    {
      string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
      if (!string.IsNullOrEmpty(programData))
      {
        string installed = Path.Combine(programData, "VELUM", "help");
        if (Directory.Exists(installed) && File.Exists(Path.Combine(installed, "index.html")))
          return installed;
      }

      string asmDir = null;
      try
      {
        asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
      }
      catch
      {
        asmDir = null;
      }

      if (!string.IsNullOrEmpty(asmDir))
      {
        string besideDll = Path.Combine(asmDir, "help");
        if (Directory.Exists(besideDll) && File.Exists(Path.Combine(besideDll, "index.html")))
          return besideDll;

        string dev = Path.GetFullPath(Path.Combine(asmDir, "..", "..", "docs", "help"));
        if (Directory.Exists(dev) && File.Exists(Path.Combine(dev, "index.html")))
          return dev;
      }

      if (!string.IsNullOrEmpty(programData))
        return Path.Combine(programData, "VELUM", "help");

      return string.IsNullOrEmpty(asmDir) ? "help" : Path.Combine(asmDir, "help");
    }

    internal static string TryResolveTopicPath(string topicId)
    {
      if (!VelumHelpTopics.TryGetRelativePath(topicId, out string relative))
        return null;

      string root = GetHelpRoot();
      string full = Path.GetFullPath(Path.Combine(root, relative));
      return File.Exists(full) ? full : null;
    }
  }
}
