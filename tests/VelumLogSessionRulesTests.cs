using System;
using System.Collections.Generic;
using System.Linq;
using Velum.UI.Logs;
using Xunit;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>
  /// Тесты pure-правил сессий CSV-лога: удаление выделенных сессий без потери остальных
  /// строк и построение имени связанного отчёта. Защищают поведение кнопки «Очистить»
  /// формы логов (удаление выделенных записей и связанных отчётов).
  /// </summary>
  public class VelumLogSessionRulesTests
  {
    // Заголовок блока-сессии: распознаём по маркеру «Pulse» и разделителю «;».
    private static bool IsHeader(string line) =>
        line != null && line.Contains("Pulse") && line.Contains(';');

    private static List<string> SampleLog()
    {
      return new List<string>
      {
        "Time;Pulse;Value",          // блок 0
        "10:00;1;a",
        "10:01;2;b",
        "Time;Pulse;Value",          // блок 1
        "10:05;3;c",
        "Time;Pulse;Value",          // блок 2
        "10:09;4;d",
        "10:10;5;e",
      };
    }

    [Fact]
    public void KeepLinesExceptSessions_DeletesSelectedBlockOnly()
    {
      List<string> kept = VelumLogSessionRules.KeepLinesExceptSessions(
          SampleLog(), IsHeader, new[] { 1 });

      Assert.Equal(new[]
      {
        "Time;Pulse;Value",
        "10:00;1;a",
        "10:01;2;b",
        "Time;Pulse;Value",
        "10:09;4;d",
        "10:10;5;e",
      }, kept);
    }

    [Fact]
    public void KeepLinesExceptSessions_DeletesMultipleBlocks()
    {
      List<string> kept = VelumLogSessionRules.KeepLinesExceptSessions(
          SampleLog(), IsHeader, new[] { 0, 2 });

      Assert.Equal(new[]
      {
        "Time;Pulse;Value",
        "10:05;3;c",
      }, kept);
    }

    [Fact]
    public void KeepLinesExceptSessions_DeletesAllBlocks_LeavesNothing()
    {
      List<string> kept = VelumLogSessionRules.KeepLinesExceptSessions(
          SampleLog(), IsHeader, new[] { 0, 1, 2 });

      Assert.Empty(kept);
    }

    [Fact]
    public void KeepLinesExceptSessions_NoSelection_KeepsEverything()
    {
      List<string> lines = SampleLog();
      List<string> kept = VelumLogSessionRules.KeepLinesExceptSessions(
          lines, IsHeader, Array.Empty<int>());

      Assert.Equal(lines, kept);
    }

    [Fact]
    public void KeepLinesExceptSessions_UnknownIndex_KeepsEverything()
    {
      List<string> lines = SampleLog();
      List<string> kept = VelumLogSessionRules.KeepLinesExceptSessions(
          lines, IsHeader, new[] { 99 });

      Assert.Equal(lines, kept);
    }

    [Fact]
    public void KeepLinesExceptSessions_LinesBeforeFirstHeader_AreKept()
    {
      // Мусорные строки до первого заголовка не относятся ни к одной сессии и сохраняются.
      var lines = new List<string>
      {
        "# комментарий",
        "",
        "Time;Pulse;Value",
        "10:00;1;a",
      };

      List<string> kept = VelumLogSessionRules.KeepLinesExceptSessions(
          lines, IsHeader, new[] { 0 });

      Assert.Equal(new[] { "# комментарий", "" }, kept);
    }

    [Fact]
    public void KeepLinesExceptSessions_NullInput_ReturnsEmpty()
    {
      Assert.Empty(VelumLogSessionRules.KeepLinesExceptSessions(null, IsHeader, new[] { 0 }));
    }

    [Fact]
    public void BuildSessionReportFileName_UsesPrefixAndSessionStart()
    {
      string name = VelumLogSessionRules.BuildSessionReportFileName(
          "Логи_системы", new DateTime(2026, 10, 3, 14, 5, 33));

      Assert.Equal("Логи_системы_20261003_140533.html", name);
    }

    [Fact]
    public void BuildSessionReportFileName_DifferentPrefixes_SameStart_DifferByName()
    {
      // Отчёты разных вкладок с одинаковым временем не путаются: различает префикс.
      var start = new DateTime(2026, 10, 3, 14, 5, 0);
      string system = VelumLogSessionRules.BuildSessionReportFileName("Логи_системы", start);
      string styles = VelumLogSessionRules.BuildSessionReportFileName("Логи_стилей", start);

      Assert.NotEqual(system, styles);
    }

    [Fact]
    public void BuildSessionReportFileName_NullPrefix_FallsBackToLog()
    {
      string name = VelumLogSessionRules.BuildSessionReportFileName(
          null, new DateTime(2026, 1, 2, 3, 4, 5));

      Assert.Equal("Log_20260102_030405.html", name);
    }
  }
}
