using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Velum.Configuration;

namespace Velum.UI.ProductRegistry
{
  /// <summary>
  /// Диагностические «хлебные крошки» индексации реестра.
  /// Пишет редко (фазы + редкий прогресс), сразу сбрасывает на диск —
  /// после silent crash SolidWorks последняя строка указывает фазу падения.
  /// </summary>
  /// <remarks>
  /// Файлы:
  /// <list type="bullet">
  /// <item><description><c>ProductRegistryIndex.last.txt</c> — одна строка, смотреть первой после вылета;</description></item>
  /// <item><description><c>ProductRegistryIndex.trace.log</c> — полный след сессии (append).</description></item>
  /// </list>
  /// Каталог: <see cref="VelumAppConfig.LogsFolderPath"/> или %ProgramData%\VELUM\Logs.
  /// </remarks>
  internal static class VelumProductRegistryIndexTrace
  {
    private const string LastFileName = "ProductRegistryIndex.last.txt";
    private const string TraceFileName = "ProductRegistryIndex.trace.log";

    /// <summary>Интервал прогресса в Apply (не на каждый файл).</summary>
    internal const int ApplyProgressEvery = 250;

    private static readonly object Sync = new object();
    private static int _sessionId;
    private static int _activeSession;
    private static string _logsDir;

    /// <summary>Каталог, куда пишутся last/trace (для подсказки пользователю).</summary>
    internal static string LogsDirectory
    {
      get
      {
        EnsureLogsDir();
        return _logsDir;
      }
    }

    /// <summary>Полный путь к файлу «последняя точка».</summary>
    internal static string LastFilePath
    {
      get
      {
        EnsureLogsDir();
        return Path.Combine(_logsDir, LastFileName);
      }
    }

    /// <summary>Полный путь к полному следу.</summary>
    internal static string TraceFilePath
    {
      get
      {
        EnsureLogsDir();
        return Path.Combine(_logsDir, TraceFileName);
      }
    }

    /// <summary>Начало сессии индексации (новый id, заголовок с окружением).</summary>
    internal static int BeginSession(string sourcePath, int parentFolderId, string extra = null)
    {
      int id = Interlocked.Increment(ref _sessionId);
      Interlocked.Exchange(ref _activeSession, id);

      var header = new StringBuilder();
      header.Append("SESSION START id=").Append(id);
      header.Append(" parentFolderId=").Append(parentFolderId);
      header.Append(" pid=").Append(Process.GetCurrentProcess().Id);
      header.Append(" machine=").Append(Environment.MachineName);
      header.Append(" user=").Append(Environment.UserName);
      header.Append(" os=").Append(Environment.OSVersion);
      header.Append(" clr=").Append(Environment.Version);
      header.Append(" bitness=").Append(Environment.Is64BitProcess ? "x64" : "x86");
      if (!string.IsNullOrWhiteSpace(sourcePath))
        header.Append(" source=\"").Append(sourcePath).Append('"');
      if (!string.IsNullOrWhiteSpace(extra))
        header.Append(' ').Append(extra);

      Write(id, header.ToString());
      return id;
    }

    /// <summary>Фаза / событие внутри активной или указанной сессии.</summary>
    internal static void Mark(string phase, string detail = null, int sessionId = 0)
    {
      int id = sessionId > 0 ? sessionId : Volatile.Read(ref _activeSession);
      if (id <= 0)
        id = Volatile.Read(ref _sessionId);

      if (string.IsNullOrEmpty(detail))
        Write(id, phase);
      else
        Write(id, phase + " " + detail);
    }

    /// <summary>Прогресс Apply не чаще чем раз в <see cref="ApplyProgressEvery"/> файлов.</summary>
    internal static void MarkApplyProgress(int processed, int total, int sessionId = 0)
    {
      if (processed <= 0)
        return;
      if (processed < total
          && (processed % ApplyProgressEvery) != 0)
        return;

      Mark("apply.progress", "processed=" + processed + "/" + total, sessionId);
    }

    /// <summary>Успешное завершение сессии.</summary>
    internal static void EndSessionOk(int sessionId, string summary = null)
    {
      Mark("SESSION END ok", summary, sessionId);
      if (Volatile.Read(ref _activeSession) == sessionId)
        Interlocked.Exchange(ref _activeSession, 0);
    }

    /// <summary>Завершение с ошибкой managed (не native kill).</summary>
    internal static void EndSessionFail(int sessionId, Exception ex)
    {
      string detail = ex == null
          ? null
          : (ex.GetType().Name + ": " + ex.Message);
      Mark("SESSION END fail", detail, sessionId);
      if (Volatile.Read(ref _activeSession) == sessionId)
        Interlocked.Exchange(ref _activeSession, 0);
    }

    private static void EnsureLogsDir()
    {
      if (_logsDir != null)
        return;

      lock (Sync)
      {
        if (_logsDir != null)
          return;

        string dir = null;
        try
        {
          dir = VelumAppConfig.LogsFolderPath;
        }
        catch
        {
        }

        if (string.IsNullOrWhiteSpace(dir))
          dir = Path.Combine(VelumAppConfig.BaseDataPath, "Logs");

        try
        {
          if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        }
        catch
        {
          dir = Path.GetTempPath();
        }

        _logsDir = dir;
      }
    }

    private static void Write(int sessionId, string message)
    {
      if (string.IsNullOrEmpty(message))
        return;

      string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
          + " [idx#" + sessionId + "] "
          + message
          + Environment.NewLine;

      lock (Sync)
      {
        try
        {
          EnsureLogsDir();
          // last — одна актуальная строка: после вылета открыть этот файл.
          AtomicWriteAllText(Path.Combine(_logsDir, LastFileName), line);
          AppendAllTextFlushed(Path.Combine(_logsDir, TraceFileName), line);
        }
        catch
        {
          // Диагностика не должна ронять индексацию.
        }
      }
    }

    private static void AtomicWriteAllText(string path, string contents)
    {
      string temp = path + "." + Process.GetCurrentProcess().Id + ".tmp";
      try
      {
        using (var fs = new FileStream(
            temp,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            4096,
            FileOptions.SequentialScan))
        using (var writer = new StreamWriter(fs, Encoding.UTF8))
        {
          writer.Write(contents);
          writer.Flush();
          fs.Flush(true);
        }

        if (File.Exists(path))
          File.Delete(path);
        File.Move(temp, path);
      }
      catch
      {
        try
        {
          if (File.Exists(temp))
            File.Delete(temp);
        }
        catch
        {
        }

        // Fallback без атомарности.
        using (var fs = new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            4096,
            FileOptions.SequentialScan))
        using (var writer = new StreamWriter(fs, Encoding.UTF8))
        {
          writer.Write(contents);
          writer.Flush();
          fs.Flush(true);
        }
      }
    }

    private static void AppendAllTextFlushed(string path, string contents)
    {
      using (var fs = new FileStream(
          path,
          FileMode.Append,
          FileAccess.Write,
          FileShare.ReadWrite,
          4096,
          FileOptions.SequentialScan))
      using (var writer = new StreamWriter(fs, Encoding.UTF8))
      {
        writer.Write(contents);
        writer.Flush();
        fs.Flush(true);
      }
    }
  }
}
