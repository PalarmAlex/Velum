using System;
using System.Collections.Generic;
using Velum.UI.AssemblyRegistry;
using Xunit;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>
  /// Тесты единого предикта пригодности ExternalId к обмену с 1С.
  /// Регрессия: «0» приравнен к пустому — фактический запрет на экспорт позиции
  /// (карточки номенклатуры и строки состава). Проверяются граничные значения.
  /// </summary>
  public class VelumBomExportFilterRulesTests
  {
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t \r\n")]
    [InlineData("0")]
    [InlineData(" 0 ")]
    public void IsExportForbidden_ForbiddenValues_True(string externalId)
    {
      Assert.True(VelumBomExportFilterRules.IsExportForbidden(externalId));
      Assert.False(VelumBomExportFilterRules.IsExportable(externalId));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("10")]
    [InlineData("01")]
    [InlineData("00")]
    [InlineData("0.0")]
    [InlineData("007")]
    [InlineData("abc")]
    [InlineData("0x0")]
    [InlineData(" P1 ")]
    public void IsExportable_ValidValues_True(string externalId)
    {
      Assert.True(VelumBomExportFilterRules.IsExportable(externalId));
      Assert.False(VelumBomExportFilterRules.IsExportForbidden(externalId));
    }

    // Запрет — по ровно нулю после обрезки пробелов: «0» запрещён, а «10», «01»,
    // «00» (другие строки) — годные идентификаторы.
    [Fact]
    public void ZeroVsStringsContainingZero_Distinguished()
    {
      Assert.True(VelumBomExportFilterRules.IsExportForbidden("0"));
      Assert.True(VelumBomExportFilterRules.IsExportable("10"));
      Assert.True(VelumBomExportFilterRules.IsExportable("01"));
      Assert.True(VelumBomExportFilterRules.IsExportable("00"));
    }

    [Fact]
    public void PurgeUnexportablePending_RemovesPendingWithZeroParent_KeepsValid()
    {
      var records = new List<VelumBomChangeRecord>
      {
        Pending("", "child1"),      // пусто — невыгружаемая
        Pending("0", "child2"),     // «0» — невыгружаемая (новая регрессия)
        Pending(" 0 ", "child3"),   // «0» с пробелами — невыгружаемая
        Pending("P1", "child4"),    // годный — остаётся
        Pending("10", "child5")     // «10» ≠ «0» — остаётся
      };

      int removed = VelumBomChangeLogRules.PurgeUnexportablePending(records);

      Assert.Equal(3, removed);
      Assert.Equal(2, records.Count);
      Assert.All(records, r => Assert.True(VelumBomExportFilterRules.IsExportable(r.ParentExternalId)));
    }

    private static VelumBomChangeRecord Pending(string parentExternalId, string childIdentity)
    {
      return new VelumBomChangeRecord
      {
        Id = Guid.NewGuid().ToString(),
        Action = VelumBomChangeAction.Add,
        ParentExternalId = parentExternalId,
        ChildIdentity = childIdentity,
        TimestampUtc = DateTime.UtcNow,
        Exported = false
      };
    }
  }
}