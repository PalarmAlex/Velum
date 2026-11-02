using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Velum.UI.Logs;
using Xunit;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>
  /// Регрессия случая 48: форма логов не давала удалить сессии, потому что файл
  /// <c>AgentLogs*.csv</c> одновременно удерживает открытым <c>ResearchLogger</c>
  /// (<c>StreamWriter</c> без явного <c>FileShare</c> → по умолчанию <c>FileShare.Read</c>),
  /// а Velum пытался перезаписать его вторым <c>StreamWriter(append:false)</c> (запрос на запись).
  ///
  /// Сами тесты не линкуют <c>ResearchLogger</c>/isida.dll (COM-зависимость), поэтому
  /// воспроизводят предпосылку бага напрямую: дескриптор, открытый как у писателя логов
  /// (<c>FileShare.Read</c>), блокирует перезапись; после его освобождения (<c>RunLogFileMaintenance</c>
  /// в рантайме закрывает writers) — перезапись проходит, а <see cref="VelumLogSessionRules"/>
  /// сохраняет остальные сессии (заголовки целы — регрессия случая 31 не сломана).
  /// </summary>
  public sealed class VelumLogSessionDeletionTests : IDisposable
  {
    private readonly string _dir;
    private readonly string _path;

    public VelumLogSessionDeletionTests()
    {
      _dir = Path.Combine(Path.GetTempPath(), "VelumLogDel_" + Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(_dir);
      _path = Path.Combine(_dir, "AgentLogs.csv");
    }

    public void Dispose()
    {
      try { Directory.Delete(_dir, recursive: true); } catch { /* temp-мусор не критичен */ }
    }

    private static bool IsHeader(string line) =>
        line != null && line.Contains("Pulse") && line.Contains(';');

    private static List<string> SampleLog()
    {
      return new List<string>
      {
        "Time;Pulse;Value",   // блок 0
        "10:00;1;a",
        "10:01;2;b",
        "Time;Pulse;Value",   // блок 1 (удаляем)
        "10:05;3;c",
        "Time;Pulse;Value",   // блок 2
        "10:09;4;d",
      };
    }

    private void WriteSampleLog()
    {
      using (var w = new StreamWriter(_path, append: false, new UTF8Encoding(true)))
        foreach (string line in SampleLog())
          w.WriteLine(line);
    }

    /// <summary>Открывает файл ровно так, как его держит писатель логов: доступ Read, отдача на чтение.</summary>
    private static FileStream OpenLikeLoggerWriter(string path)
    {
      // Эквивалент StreamWriter(path, append:true, UTF8) без явного FileShare:
      // FileAccess.Write + FileShare.Read — чужая запись невозможна.
      return new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.Read);
    }

    private static void RewriteDeletingBlock(string path, IEnumerable<int> blocks)
    {
      List<string> lines;
      using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
      using (var r = new StreamReader(fs, Encoding.UTF8, true))
      {
        lines = new List<string>();
        string line;
        while ((line = r.ReadLine()) != null)
          lines.Add(line);
      }
      List<string> kept = VelumLogSessionRules.KeepLinesExceptSessions(lines, IsHeader, blocks);
      using (var w = new StreamWriter(path, append: false, new UTF8Encoding(true)))
        foreach (string line in kept)
          w.WriteLine(line);
    }

    [Fact]
    public void Rewrite_WhileFileHeldByLoggerLikeHandle_Throws()
    {
      WriteSampleLog();

      using (OpenLikeLoggerWriter(_path))
      {
        // Предпосылка бага: пока дескриптор держат как писатель логов,
        // прямая перезапись (StreamWriter append:false) не проходит.
        Assert.Throws<IOException>(() => RewriteDeletingBlock(_path, new[] { 1 }));
      }
    }

    [Fact]
    public void Rewrite_AfterHandleReleased_Succeeds()
    {
      WriteSampleLog();

      // Имитация контура RunLogFileMaintenance: дескриптор писателя был, затем закрыт —
      // на свободном файле перезапись проходит без ошибки.
      FileStream held = OpenLikeLoggerWriter(_path);
      held.Dispose();

      var ex = Record.Exception(() => RewriteDeletingBlock(_path, new[] { 1 }));
      Assert.Null(ex);
    }

    [Fact]
    public void Rewrite_DeletesSelectedBlockAndKeepsOtherSessionsIntact()
    {
      WriteSampleLog();

      RewriteDeletingBlock(_path, new[] { 1 });

      List<string> after = File.ReadAllLines(_path).ToList();

      // Блок 1 удалён (его строка данных).
      Assert.DoesNotContain(after, l => l.Contains(";3;c"));
      // Оставшиеся сессии целы вместе со своими заголовками (регрессия случая 31).
      Assert.Contains(after, l => l.Contains(";1;a"));
      Assert.Contains(after, l => l.Contains(";2;b"));
      Assert.Contains(after, l => l.Contains(";4;d"));
      // Заголовки оставшихся блоков сохранены: два блока → ровно две строки-заголовка.
      Assert.Equal(2, after.Count(IsHeader));
    }

    [Fact]
    public void Rewrite_DeleteAllSelected_RemovesAllBlocks()
    {
      WriteSampleLog();

      RewriteDeletingBlock(_path, new[] { 0, 1, 2 });

      List<string> after = File.ReadAllLines(_path).ToList();
      Assert.DoesNotContain(after, l => IsHeader(l));
      Assert.DoesNotContain(after, l => l.Contains(";a"));
      Assert.DoesNotContain(after, l => l.Contains(";c"));
    }
  }
}
