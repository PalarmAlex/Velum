using System.IO;
using Velum.ReactiveCore.Export;
using Xunit;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>
  /// Тесты pure-правил нормализации корневого каталога документов.
  /// Защищают регрессии CASEBOOK-1 случай 5 (список через «;») и
  /// CASEBOOK-2 случай 23 (буква диска «Z:\» ошибочно считалась UNC — E45).
  /// </summary>
  public class VelumDocumentRootPathRulesTests
  {
    private static readonly string Sep = Path.DirectorySeparatorChar.ToString();
    private static readonly string Unc = new string(Path.DirectorySeparatorChar, 2);

    [Fact]
    public void FirstPart_EmptyOrWhitespace_ReturnsEmpty()
    {
      Assert.Equal(string.Empty, VelumDocumentRootPathRules.FirstPart(null));
      Assert.Equal(string.Empty, VelumDocumentRootPathRules.FirstPart(string.Empty));
      Assert.Equal(string.Empty, VelumDocumentRootPathRules.FirstPart("   "));
    }

    [Fact]
    public void FirstPart_SemicolonList_ReturnsFirstNonEmptyPart()
    {
      // Старые настройки с двумя префиксами через «;» — берётся только первая часть
      // (иначе путь собирался с обоими префиксами подряд, CASEBOOK-1 случай 5).
      Assert.Equal("Z:" + Sep, VelumDocumentRootPathRules.FirstPart("Z:" + Sep + ";" + Unc + "ip" + Sep + "dfs" + Sep));
    }

    [Fact]
    public void FirstPart_SkipsEmptySegments()
    {
      Assert.Equal("Z:", VelumDocumentRootPathRules.FirstPart("; ; Z: ;"));
    }

    [Fact]
    public void Normalize_DriveLetter_GainsTrailingSeparator()
    {
      Assert.Equal("Z:" + Sep, VelumDocumentRootPathRules.Normalize("Z:"));
      Assert.Equal("C:" + Sep, VelumDocumentRootPathRules.Normalize("C:"));
    }

    [Fact]
    public void Normalize_DriveLetterWithSeparator_Unchanged()
    {
      Assert.Equal("Z:" + Sep, VelumDocumentRootPathRules.Normalize("Z:" + Sep));
    }

    [Fact]
    public void Normalize_UncWithoutTrailing_GainsTrailingSeparator()
    {
      Assert.Equal(Unc + "server" + Sep + "dfs" + Sep,
          VelumDocumentRootPathRules.Normalize(Unc + "server" + Sep + "dfs"));
    }

    [Fact]
    public void Normalize_UncWithTrailing_Unchanged()
    {
      string uncWithTrailing = Unc + "server" + Sep + "dfs" + Sep;
      Assert.Equal(uncWithTrailing, VelumDocumentRootPathRules.Normalize(uncWithTrailing));
    }

    [Fact]
    public void Normalize_DriveLetterWithPath_Unchanged()
    {
      string path = "Z:" + Sep + "03_Производство";
      Assert.Equal(path, VelumDocumentRootPathRules.Normalize(path));
    }

    [Fact]
    public void Normalize_Empty_ReturnsEmpty()
    {
      Assert.Equal(string.Empty, VelumDocumentRootPathRules.Normalize(null));
      Assert.Equal(string.Empty, VelumDocumentRootPathRules.Normalize("  "));
    }

    [Fact]
    public void Resolve_ListWithSemicolon_UsesFirstNormalized()
    {
      Assert.Equal("Z:" + Sep, VelumDocumentRootPathRules.Resolve("Z:;" + Unc + "ip" + Sep + "dfs" + Sep));
    }

    [Fact]
    public void Resolve_Empty_ReturnsEmpty()
    {
      Assert.Equal(string.Empty, VelumDocumentRootPathRules.Resolve(""));
    }

    [Fact]
    public void IsUnc_DriveLetter_IsFalse()
    {
      // Ключевой кейс регрессии: буква диска не должна распознаваться как UNC.
      Assert.False(VelumDocumentRootPathRules.IsUnc("Z:" + Sep + "03_Производство"));
      Assert.False(VelumDocumentRootPathRules.IsUnc("Z:" + Sep));
    }

    [Fact]
    public void IsUnc_UncPath_IsTrue()
    {
      Assert.True(VelumDocumentRootPathRules.IsUnc(Unc + "server" + Sep + "dfs"));
    }
  }
}
