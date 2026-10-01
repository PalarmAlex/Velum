using System.IO;
using Velum.ReactiveCore.Export;
using Xunit;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>
  /// Тесты pure-правил конверсии путей (срез корня / достройка по корню).
  /// Защищают регрессии CASEBOOK-1 случай 5 (двойной разделитель после среза корня,
  /// дубль сегмента пути) и случай 6 (относительные пути как ключ хранения).
  /// </summary>
  public class VelumRelativePathRulesTests
  {
    private static readonly string Sep = Path.DirectorySeparatorChar.ToString();
    private static readonly string Root = "Z:" + Sep;

    [Fact]
    public void ToStored_UnderRoot_StripsPrefixCaseInsensitive()
    {
      string full = "z:" + Sep + "03_Производство" + Sep + "деталь.SLDPRT";
      Assert.Equal("03_Производство" + Sep + "деталь.SLDPRT",
          VelumRelativePathRules.ToStored(full, Root));
    }

    [Fact]
    public void ToStored_RootEndsWithSeparatorAndValueStartsWithOne_NoDoubleSeparator()
    {
      // «Z:\» + «\03_Производство\...»: после среза ведущий разделитель снимается —
      // иначе получался бы двойной разделитель и путь не собирался (CASEBOOK-1 случай 5).
      string full = "Z:" + Sep + Sep + "03_Производство" + Sep + "деталь.SLDPRT";
      Assert.Equal("03_Производство" + Sep + "деталь.SLDPRT",
          VelumRelativePathRules.ToStored(full, Root));
    }

    [Fact]
    public void ToStored_AbsoluteWithoutMatch_Unchanged()
    {
      string full = "C:" + Sep + "Temp" + Sep + "деталь.SLDPRT";
      Assert.Equal(full, VelumRelativePathRules.ToStored(full, Root));
    }

    [Fact]
    public void ToStored_AlreadyRelative_Unchanged()
    {
      string relative = "03_Производство" + Sep + "деталь.SLDPRT";
      Assert.Equal(relative, VelumRelativePathRules.ToStored(relative, Root));
    }

    [Fact]
    public void ToStored_EmptyRoot_Passthrough()
    {
      string full = "C:" + Sep + "Temp" + Sep + "деталь.SLDPRT";
      Assert.Equal(full, VelumRelativePathRules.ToStored(full, string.Empty));
    }

    [Fact]
    public void ToStored_EmptyValue_ReturnsEmpty()
    {
      Assert.Equal(string.Empty, VelumRelativePathRules.ToStored(null, Root));
      Assert.Equal(string.Empty, VelumRelativePathRules.ToStored("   ", Root));
    }

    [Fact]
    public void ToFull_Relative_CombinesWithRoot()
    {
      Assert.Equal(Root + "03_Производство" + Sep + "деталь.SLDPRT",
          VelumRelativePathRules.ToFull("03_Производство" + Sep + "деталь.SLDPRT", Root));
    }

    [Fact]
    public void ToFull_Rooted_Unchanged()
    {
      string full = "C:" + Sep + "Temp" + Sep + "деталь.SLDPRT";
      Assert.Equal(full, VelumRelativePathRules.ToFull(full, Root));
    }

    [Fact]
    public void ToFull_EmptyRoot_RelativeUnchanged()
    {
      string relative = "03_Производство" + Sep + "деталь.SLDPRT";
      Assert.Equal(relative, VelumRelativePathRules.ToFull(relative, string.Empty));
    }

    [Fact]
    public void ToFull_EmptyValue_ReturnsEmpty()
    {
      Assert.Equal(string.Empty, VelumRelativePathRules.ToFull(null, Root));
    }

    [Fact]
    public void TryStripPrefix_Match_ReturnsRelative()
    {
      bool ok = VelumRelativePathRules.TryStripPrefix(
          "Z:" + Sep + "a" + Sep + "b", Root, out string relative);

      Assert.True(ok);
      Assert.Equal("a" + Sep + "b", relative);
    }

    [Fact]
    public void TryStripPrefix_NoMatch_ReturnsFalseAndEmpty()
    {
      bool ok = VelumRelativePathRules.TryStripPrefix(
          "C:" + Sep + "a", Root, out string relative);

      Assert.False(ok);
      Assert.Equal(string.Empty, relative);
    }

    [Fact]
    public void TryStripPrefix_EmptyPrefix_ReturnsFalse()
    {
      Assert.False(VelumRelativePathRules.TryStripPrefix("a", string.Empty, out _));
    }

    [Fact]
    public void IsRelativeStored_Relative_True()
    {
      Assert.True(VelumRelativePathRules.IsRelativeStored("a" + Sep + "b"));
      Assert.True(VelumRelativePathRules.IsRelativeStored("деталь.SLDPRT"));
    }

    [Fact]
    public void IsRelativeStored_Absolute_False()
    {
      Assert.False(VelumRelativePathRules.IsRelativeStored("Z:" + Sep + "a"));
      Assert.False(VelumRelativePathRules.IsRelativeStored("C:" + Sep + "a"));
    }

    [Fact]
    public void IsRelativeStored_Empty_False()
    {
      Assert.False(VelumRelativePathRules.IsRelativeStored(null));
      Assert.False(VelumRelativePathRules.IsRelativeStored("  "));
    }

    [Fact]
    public void NormalizeForCompare_RelativeResolvedAgainstRoot_NotProcessCwd()
    {
      // Без ToFull относительный путь развернулся бы от текущего каталога процесса —
      // ключ сравнения был бы неверным (CASEBOOK-1 случай 6).
      string key = VelumRelativePathRules.NormalizeForCompare("деталь.SLDPRT", Root);

      Assert.StartsWith("Z:" + Sep, key);
      Assert.EndsWith("деталь.SLDPRT", key);
    }

    [Fact]
    public void NormalizeForCompare_Absolute_Unchanged()
    {
      string full = "C:" + Sep + "Temp" + Sep + "деталь.SLDPRT";
      Assert.Equal(full, VelumRelativePathRules.NormalizeForCompare(full, Root));
    }

    [Fact]
    public void NormalizeForCompare_Empty_ReturnsEmpty()
    {
      Assert.Equal(string.Empty, VelumRelativePathRules.NormalizeForCompare(null, Root));
    }
  }
}
