using System;
using System.IO;

namespace Velum.Isida.Logs
{
  /// <summary>
  /// Пути к CSV-логам симбионта ISIDA в <c>%ProgramData%\VELUM\Logs</c>
  /// (их пишет <c>isida.dll</c>: AgentLogs.csv, AgentLogs_Styles.csv, AgentLogs_Parameters.csv).
  /// </summary>
  internal static class VelumLogPaths
  {
    /// <summary>Имя главного файла логов.</summary>
    internal const string AgentLogCsvFileName = "AgentLogs.csv";

    /// <summary>Имя файла логов стилей поведения.</summary>
    internal const string StylesLogCsvFileName = "AgentLogs_Styles.csv";

    /// <summary>Имя файла логов параметров гомеостаза.</summary>
    internal const string ParametersLogCsvFileName = "AgentLogs_Parameters.csv";

    /// <summary>Каталог логов: <c>%ProgramData%\VELUM\Logs</c>.</summary>
    internal static string LogsFolderPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "VELUM", "Logs");

    /// <summary>Полный путь к файлу логов по имени.</summary>
    internal static string ResolveLogFile(string fileName)
    {
      return Path.Combine(LogsFolderPath, fileName);
    }
  }
}
