using System.Collections.Generic;
using System.Linq;
using Velum.UI.AssemblyRegistry;
using Xunit;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>
  /// Тесты pure-правил состава колонок выгрузки BOM.
  /// Защищают регрессию CASEBOOK-1 случай 14 / E33: снятое с поддержки поле
  /// (Quantity) не должно возвращаться при нормализации.
  /// </summary>
  public class VelumBomExchangeLayoutRulesTests
  {
    private static readonly string[] NoTracked = new string[0];

    private static VelumBomExchangeColumnDef Structural(string field, int order, bool enabled = true)
    {
      return new VelumBomExchangeColumnDef
      {
        Field = field,
        Header = field,
        Source = VelumBomExchangeFieldSource.Structural,
        Enabled = enabled,
        Order = order
      };
    }

    private static VelumBomExchangeColumnDef Tracked(string field, int order)
    {
      return new VelumBomExchangeColumnDef
      {
        Field = field,
        Header = field,
        Source = VelumBomExchangeFieldSource.Tracked,
        Enabled = true,
        Order = order
      };
    }

    [Fact]
    public void StructuralKeys_DoesNotContainQuantity()
    {
      // Ключевой инвариант случая 14: Quantity убран из эталона структурных полей.
      Assert.DoesNotContain("Quantity", VelumBomExchangeLayoutRules.StructuralKeys);
    }

    [Theory]
    [InlineData("TypeDocs", true)]
    [InlineData("typedocs", true)]
    [InlineData("Quantity", false)]
    [InlineData("SomeTracked", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsStructural_MatchesReferenceKeysCaseInsensitive(string field, bool expected)
    {
      Assert.Equal(expected, VelumBomExchangeLayoutRules.IsStructural(field));
    }

    [Theory]
    [InlineData("TypeDocs", true)]
    [InlineData("name", true)]
    [InlineData("FilePath", false)]
    [InlineData("Configuration", false)]
    [InlineData("Quantity", false)]
    public void IsMandatory_OnlyCoreFields(string field, bool expected)
    {
      Assert.Equal(expected, VelumBomExchangeLayoutRules.IsMandatory(field));
    }

    [Fact]
    public void Normalize_RemovesOrphanStructuralColumn_Quantity()
    {
      // Старый layout с колонкой Quantity (снята с поддержки).
      var data = new VelumBomExchangeLayoutFile
      {
        Columns = new List<VelumBomExchangeColumnDef>
        {
          Structural("Quantity", 1),
          Structural("TypeDocs", 2),
          Structural("ExternalId", 3),
          Structural("Designation", 4),
          Structural("Name", 5),
          Structural("FilePath", 6),
          Structural("Configuration", 7)
        }
      };

      VelumBomExchangeLayoutRules.Normalize(data, NoTracked);

      Assert.DoesNotContain(data.Columns, c => c.Field == "Quantity");
      Assert.Contains(data.Columns, c => c.Field == "TypeDocs");
    }

    [Fact]
    public void Normalize_MandatoryFieldDisabled_ForcedEnabled()
    {
      var data = new VelumBomExchangeLayoutFile
      {
        Columns = new List<VelumBomExchangeColumnDef>
        {
          Structural("TypeDocs", 1, enabled: false),
          Structural("ExternalId", 2, enabled: false),
          Structural("Designation", 3),
          Structural("Name", 4),
          Structural("FilePath", 5),
          Structural("Configuration", 6)
        }
      };

      VelumBomExchangeLayoutRules.Normalize(data, NoTracked);

      Assert.True(data.Columns.Single(c => c.Field == "TypeDocs").Enabled);
      Assert.True(data.Columns.Single(c => c.Field == "ExternalId").Enabled);
    }

    [Fact]
    public void Normalize_OrdersAreCompactedAndStable()
    {
      var data = new VelumBomExchangeLayoutFile
      {
        Columns = new List<VelumBomExchangeColumnDef>
        {
          Structural("Configuration", 100),
          Structural("TypeDocs", 5),
          Structural("ExternalId", 5),
          Structural("Designation", 6),
          Structural("Name", 7),
          Structural("FilePath", 8)
        }
      };

      VelumBomExchangeLayoutRules.Normalize(data, NoTracked);

      int[] orders = data.Columns.Select(c => c.Order).ToArray();
      Assert.Equal(Enumerable.Range(1, data.Columns.Count).ToArray(), orders);
      // При равном Order сохраняется исходный порядок: TypeDocs перед ExternalId.
      Assert.True(data.Columns.FindIndex(c => c.Field == "TypeDocs")
                  < data.Columns.FindIndex(c => c.Field == "ExternalId"));
    }

    [Fact]
    public void Normalize_AddsMissingStructuralKeys()
    {
      var data = new VelumBomExchangeLayoutFile
      {
        Columns = new List<VelumBomExchangeColumnDef> { Structural("TypeDocs", 1) }
      };

      VelumBomExchangeLayoutRules.Normalize(data, NoTracked);

      foreach (string key in VelumBomExchangeLayoutRules.StructuralKeys)
        Assert.Contains(data.Columns, c => c.Field == key);
    }

    [Fact]
    public void Normalize_RemovesStaleTracked_AddsCurrentTracked()
    {
      var data = new VelumBomExchangeLayoutFile
      {
        Columns = new List<VelumBomExchangeColumnDef>
        {
          Structural("TypeDocs", 1),
          Tracked("Obsolete", 2)
        }
      };

      VelumBomExchangeLayoutRules.Normalize(data, new[] { "НовоеСвойство" });

      Assert.DoesNotContain(data.Columns, c => c.Field == "Obsolete");
      Assert.Contains(data.Columns, c => c.Field == "НовоеСвойство"
          && c.Source == VelumBomExchangeFieldSource.Tracked);
    }

    [Fact]
    public void Normalize_FixesEmptyHeaderAndSmallWidth()
    {
      var data = new VelumBomExchangeLayoutFile
      {
        Columns = new List<VelumBomExchangeColumnDef>
        {
          new VelumBomExchangeColumnDef
          {
            Field = "TypeDocs", Header = "  ",
            Source = VelumBomExchangeFieldSource.Structural, Order = 1, Width = 10
          }
        }
      };

      VelumBomExchangeLayoutRules.Normalize(data, NoTracked);

      VelumBomExchangeColumnDef typeDocs = data.Columns.Single(c => c.Field == "TypeDocs");
      Assert.Equal("TypeDocs", typeDocs.Header);
      Assert.Equal(40, typeDocs.Width);
    }

    [Fact]
    public void CreateDefault_HasAllStructuralKeysAndNoQuantity()
    {
      VelumBomExchangeLayoutFile data = VelumBomExchangeLayoutRules.CreateDefault(NoTracked);

      Assert.Equal(VelumBomExchangeLayoutRules.CurrentLayoutFormatVersion, data.LayoutFormatVersion);
      Assert.DoesNotContain(data.Columns, c => c.Field == "Quantity");
      foreach (string key in VelumBomExchangeLayoutRules.StructuralKeys)
        Assert.Contains(data.Columns, c => c.Field == key);
      Assert.Equal(Enumerable.Range(1, data.Columns.Count).ToArray(),
          data.Columns.Select(c => c.Order).ToArray());
    }

    [Fact]
    public void CreateDefault_IncludesTrackedPropertiesAfterStructural()
    {
      VelumBomExchangeLayoutFile data = VelumBomExchangeLayoutRules.CreateDefault(new[] { "Масса" });

      VelumBomExchangeColumnDef tracked = data.Columns.Single(c => c.Field == "Масса");
      Assert.Equal(VelumBomExchangeFieldSource.Tracked, tracked.Source);
      Assert.Equal(data.Columns.Count, tracked.Order);
    }

    [Fact]
    public void MigrateFormatVersion_OlderVersion_UpgradesAndRequestsRewrite()
    {
      var data = new VelumBomExchangeLayoutFile { LayoutFormatVersion = 0 };
      Assert.True(VelumBomExchangeLayoutRules.MigrateFormatVersion(data));
      Assert.Equal(VelumBomExchangeLayoutRules.CurrentLayoutFormatVersion, data.LayoutFormatVersion);
    }

    [Fact]
    public void MigrateFormatVersion_CurrentVersion_NoChange()
    {
      var data = new VelumBomExchangeLayoutFile
      {
        LayoutFormatVersion = VelumBomExchangeLayoutRules.CurrentLayoutFormatVersion
      };
      Assert.False(VelumBomExchangeLayoutRules.MigrateFormatVersion(data));
    }

    [Fact]
    public void MigrateFormatVersion_NewerVersion_NotDowngraded()
    {
      var data = new VelumBomExchangeLayoutFile
      {
        LayoutFormatVersion = VelumBomExchangeLayoutRules.CurrentLayoutFormatVersion + 1
      };
      Assert.False(VelumBomExchangeLayoutRules.MigrateFormatVersion(data));
      Assert.Equal(VelumBomExchangeLayoutRules.CurrentLayoutFormatVersion + 1, data.LayoutFormatVersion);
    }

    [Fact]
    public void DefaultWidthFor_KnownAndUnknownKeys()
    {
      Assert.Equal(80, VelumBomExchangeLayoutRules.DefaultWidthFor("TypeDocs"));
      Assert.Equal(120, VelumBomExchangeLayoutRules.DefaultWidthFor("Неизвестное"));
    }
  }
}
