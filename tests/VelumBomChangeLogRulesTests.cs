using System;
using System.Collections.Generic;
using Velum.UI.AssemblyRegistry;
using Xunit;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>
  /// Тесты pure-правил журнала изменений состава BOM.
  /// Защищают регрессии CASEBOOK-2 случай 19: E39 (невыгружаемая pending-запись
  /// без ParentExternalId) и E40 (регистронезависимое сравнение ExternalId).
  /// </summary>
  public class VelumBomChangeLogRulesTests
  {
    private static VelumBomChangeRecord Record(
        VelumBomChangeAction action,
        string parentExternalId,
        string childIdentity,
        DateTime timestamp,
        bool exported = false,
        DateTime? exportedUtc = null,
        string id = null)
    {
      return new VelumBomChangeRecord
      {
        Id = id ?? Guid.NewGuid().ToString(),
        Action = action,
        ParentExternalId = parentExternalId,
        ChildIdentity = childIdentity,
        TimestampUtc = timestamp,
        Exported = exported,
        ExportedUtc = exportedUtc
      };
    }

    [Theory]
    [InlineData((int)VelumBomChangeAction.Add, "add")]
    [InlineData((int)VelumBomChangeAction.Update, "update")]
    [InlineData((int)VelumBomChangeAction.Delete, "delete")]
    public void RenderAction_Lowercase(int action, string expected)
    {
      Assert.Equal(expected, VelumBomChangeLogRules.RenderAction((VelumBomChangeAction)action));
    }

    [Fact]
    public void RenderAction_Unknown_ReturnsEmpty()
    {
      Assert.Equal(string.Empty, VelumBomChangeLogRules.RenderAction((VelumBomChangeAction)999));
    }

    [Fact]
    public void PurgeUnexportablePending_RemovesPendingWithoutParent_KeepsOthers()
    {
      var records = new List<VelumBomChangeRecord>
      {
        Record(VelumBomChangeAction.Add, "", "child1", DateTime.UtcNow),            // невыгружаемая
        Record(VelumBomChangeAction.Update, "  ", "child2", DateTime.UtcNow),      // невыгружаемая
        Record(VelumBomChangeAction.Add, "P1", "child3", DateTime.UtcNow),         // остаётся
        Record(VelumBomChangeAction.Delete, "", "child4", DateTime.UtcNow, true)   // выгружена — не трогаем
      };

      int removed = VelumBomChangeLogRules.PurgeUnexportablePending(records);

      Assert.Equal(2, removed);
      Assert.Equal(2, records.Count);
      Assert.All(records, r => Assert.False(string.IsNullOrWhiteSpace(r.ParentExternalId)
          && !r.Exported));
    }

    [Fact]
    public void PurgeExportedOlderThan_RemovesOnlyOldExported()
    {
      DateTime now = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
      var records = new List<VelumBomChangeRecord>
      {
        Record(VelumBomChangeAction.Add, "P1", "c1", now.AddDays(-20), true, now.AddDays(-15)), // старее 10 дней
        Record(VelumBomChangeAction.Add, "P1", "c2", now.AddDays(-20), true, now.AddDays(-2)),  // свежая выгрузка
        Record(VelumBomChangeAction.Add, "P1", "c3", now.AddDays(-30))                          // невыгруженная — вечная
      };

      int removed = VelumBomChangeLogRules.PurgeExportedOlderThan(
          records, TimeSpan.FromDays(10), now);

      Assert.Equal(1, removed);
      Assert.Equal(2, records.Count);
    }

    [Fact]
    public void Filter_PendingOnly_ExcludesExported()
    {
      var records = new List<VelumBomChangeRecord>
      {
        Record(VelumBomChangeAction.Add, "P1", "c1", DateTime.UtcNow),
        Record(VelumBomChangeAction.Add, "P1", "c2", DateTime.UtcNow, true, DateTime.UtcNow)
      };

      List<VelumBomChangeRecord> pending = VelumBomChangeLogRules.Filter(records, includeExported: false);

      Assert.Single(pending);
      Assert.Equal("c1", pending[0].ChildIdentity);
    }

    [Fact]
    public void Filter_SortsByTimestampThenId()
    {
      DateTime t0 = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
      var records = new List<VelumBomChangeRecord>
      {
        Record(VelumBomChangeAction.Add, "P1", "b", t0.AddMinutes(1), id: "B"),
        Record(VelumBomChangeAction.Add, "P1", "a", t0.AddMinutes(1), id: "A"),
        Record(VelumBomChangeAction.Add, "P1", "first", t0, id: "Z")
      };

      List<VelumBomChangeRecord> sorted = VelumBomChangeLogRules.Filter(records, includeExported: true);

      Assert.Equal("first", sorted[0].ChildIdentity);
      Assert.Equal("A", sorted[1].Id);
      Assert.Equal("B", sorted[2].Id);
    }

    [Fact]
    public void Filter_NullInput_ReturnsEmpty()
    {
      Assert.Empty(VelumBomChangeLogRules.Filter(null, includeExported: true));
    }

    [Fact]
    public void HasPending_SameOperationCaseInsensitive_True()
    {
      var records = new List<VelumBomChangeRecord>
      {
        Record(VelumBomChangeAction.Update, "abc123", "path|config", DateTime.UtcNow)
      };

      // Смена регистра ExternalId не должна порождать новую запись (E40).
      Assert.True(VelumBomChangeLogRules.HasPending(
          records, VelumBomChangeAction.Update, "ABC123", "PATH|CONFIG"));
    }

    [Fact]
    public void HasPending_DifferentActionOrChild_False()
    {
      var records = new List<VelumBomChangeRecord>
      {
        Record(VelumBomChangeAction.Add, "P1", "c1", DateTime.UtcNow)
      };

      Assert.False(VelumBomChangeLogRules.HasPending(records, VelumBomChangeAction.Update, "P1", "c1"));
      Assert.False(VelumBomChangeLogRules.HasPending(records, VelumBomChangeAction.Add, "P1", "c2"));
      Assert.False(VelumBomChangeLogRules.HasPending(records, VelumBomChangeAction.Add, "P2", "c1"));
    }

    [Fact]
    public void HasPending_ExportedRecord_Ignored()
    {
      var records = new List<VelumBomChangeRecord>
      {
        Record(VelumBomChangeAction.Add, "P1", "c1", DateTime.UtcNow, true, DateTime.UtcNow)
      };

      Assert.False(VelumBomChangeLogRules.HasPending(records, VelumBomChangeAction.Add, "P1", "c1"));
    }

    [Fact]
    public void HasPending_NullInput_False()
    {
      Assert.False(VelumBomChangeLogRules.HasPending(null, VelumBomChangeAction.Add, "P1", "c1"));
    }
  }
}
