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

    // --- Выборка выбранных сессий (регресс бага «не выбрана ни одна сессия») ---

    // Аналог SessionListItem формы логов: модель строки со своим флагом Checked.
    private sealed class Row
    {
      public int SessionIndex;
      public bool Checked;
      public bool HasInfo = true;
    }

    [Fact]
    public void CollectSelectedSessionIndices_ReturnsCheckedSorted()
    {
      // Выборка читает модель (Checked), а не контрол: отмечены индексы 2 и 0.
      var items = new List<Row>
      {
        new Row { SessionIndex = 2, Checked = true },
        new Row { SessionIndex = 1, Checked = false },
        new Row { SessionIndex = 0, Checked = true },
      };

      List<int> selected = VelumLogSessionRules.CollectSelectedSessionIndices(
          items, r => r.Checked, r => r.SessionIndex);

      Assert.Equal(new[] { 0, 2 }, selected);
    }

    [Fact]
    public void CollectSelectedSessionIndices_NoneChecked_ReturnsEmpty()
    {
      // Сценарий «до/после»: флажки в модели не проставлены (рассинхрон модели и
      // чекбокса) — выборка пуста, и форма корректно сообщает «не выбрано ни одной».
      var items = new List<Row>
      {
        new Row { SessionIndex = 0, Checked = false },
        new Row { SessionIndex = 1, Checked = false },
      };

      List<int> selected = VelumLogSessionRules.CollectSelectedSessionIndices(
          items, r => r.Checked, r => r.SessionIndex);

      Assert.Empty(selected);
    }

    [Fact]
    public void CollectSelectedSessionIndices_AfterCheckSync_ReturnsSelection()
    {
      // Сценарий «после» правки: OnSessionItemCheck проставил Checked в модель —
      // «Просмотр» видит выбор.
      var items = new List<Row>
      {
        new Row { SessionIndex = 0, Checked = false },
        new Row { SessionIndex = 1, Checked = false },
      };
      items[1].Checked = true; // имитация ItemCheck → модель

      List<int> selected = VelumLogSessionRules.CollectSelectedSessionIndices(
          items, r => r.Checked && r.HasInfo, r => r.SessionIndex);

      Assert.Equal(new[] { 1 }, selected);
    }

    [Fact]
    public void CollectSelectedSessionIndices_SkipsRowsWithoutInfo()
    {
      var items = new List<Row>
      {
        new Row { SessionIndex = 0, Checked = true, HasInfo = false },
        new Row { SessionIndex = 1, Checked = true, HasInfo = true },
      };

      List<int> selected = VelumLogSessionRules.CollectSelectedSessionIndices(
          items, r => r.Checked && r.HasInfo, r => r.SessionIndex);

      Assert.Equal(new[] { 1 }, selected);
    }

    [Fact]
    public void CollectSelectedSessionIndices_NullInput_ReturnsEmpty()
    {
      Assert.Empty(VelumLogSessionRules.CollectSelectedSessionIndices<Row>(
          null, r => r.Checked, r => r.SessionIndex));
    }

    // ── JSONL: удаление сессий (случай 48) ──────────────────────────────
    // Экстрактор пульса для тестов: строка вида {"Пульс":N,...} → N; иначе null.
    private static int? Pulse(string line)
    {
      if (line == null) return null;
      int i = line.IndexOf("\"Пульс\":", StringComparison.Ordinal);
      if (i < 0) return null;
      i += "\"Пульс\":".Length;
      int j = i;
      while (j < line.Length && (char.IsDigit(line[j]) || line[j] == '-')) j++;
      return int.TryParse(line.Substring(i, j - i), out int v) ? (int?)v : null;
    }

    private static List<string> SampleJsonl()
    {
      return new List<string>
      {
        "{\"Пульс\":1,\"a\":1}", // сессия 0
        "{\"Пульс\":2,\"a\":2}",
        "{\"Пульс\":1,\"b\":1}", // сессия 1 (pulse 1 после pulse 2)
        "{\"Пульс\":2,\"b\":2}",
        "{\"Пульс\":1,\"c\":1}", // сессия 2
        "{\"Пульс\":3,\"c\":3}",
      };
    }

    [Fact]
    public void KeepJsonlLinesExceptSessions_DeletesSelectedSessionOnly()
    {
      List<string> kept = VelumLogSessionRules.KeepJsonlLinesExceptSessions(
          SampleJsonl(), Pulse, new[] { 1 });

      Assert.DoesNotContain(kept, l => l.Contains("\"b\""));
      Assert.Contains(kept, l => l.Contains("\"a\""));
      Assert.Contains(kept, l => l.Contains("\"c\""));
    }

    [Fact]
    public void KeepJsonlLinesExceptSessions_DeleteAll_LeavesEmpty()
    {
      List<string> kept = VelumLogSessionRules.KeepJsonlLinesExceptSessions(
          SampleJsonl(), Pulse, new[] { 0, 1, 2 });

      Assert.Empty(kept);
    }

    [Fact]
    public void KeepJsonlLinesExceptSessions_DropsNonEntryLines()
    {
      var lines = new List<string>
      {
        "{\"Пульс\":1,\"a\":1}",
        "не json строка",
        "{\"Пульс\":2,\"a\":2}",
        "{\"Пульс\":1,\"b\":1}", // сессия 1
      };

      List<string> kept = VelumLogSessionRules.KeepJsonlLinesExceptSessions(
          lines, Pulse, new[] { 0 });

      // Блок 0 (обе a-строки) удалён; мусорная строка отброшена; сессия 1 цела.
      Assert.DoesNotContain(kept, l => l.Contains("не json"));
      Assert.DoesNotContain(kept, l => l.Contains("\"a\""));
      Assert.Contains(kept, l => l.Contains("\"b\""));
    }

    [Fact]
    public void KeepJsonlLinesExceptSessions_EmptyDelete_KeepsValidEntries()
    {
      List<string> kept = VelumLogSessionRules.KeepJsonlLinesExceptSessions(
          SampleJsonl(), Pulse, new int[0]);

      Assert.Equal(6, kept.Count);
    }
  }
}
