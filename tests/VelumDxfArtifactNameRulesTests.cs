using Xunit;
using Velum.ReactiveCore.Export;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>
  /// Тесты VelumDxfArtifactNameRules — pure-правила имён DXF (prefix/суффикс маски).
  /// </summary>
  public class VelumDxfArtifactNameRulesTests
  {
    // ----- CombinePrefixAndSuffix -----

    [Fact]
    public void CombinePrefixAndSuffix_BothPresent_JoinsWithSpace()
    {
      Assert.Equal("Деталь_Конф1 суффикс",
          VelumDxfArtifactNameRules.CombinePrefixAndSuffix("Деталь_Конф1", "суффикс"));
    }

    [Theory]
    [InlineData(null, "суффикс", "суффикс")]
    [InlineData("  ", "суффикс", "суффикс")]
    [InlineData("префикс", null, "префикс")]
    [InlineData("префикс", "  ", "префикс")]
    public void CombinePrefixAndSuffix_EmptySide_ReturnsOther(string prefix, string suffix, string expected)
    {
      Assert.Equal(expected, VelumDxfArtifactNameRules.CombinePrefixAndSuffix(prefix, suffix));
    }

    [Fact]
    public void CombinePrefixAndSuffix_TrimsBothSides()
    {
      Assert.Equal("a b", VelumDxfArtifactNameRules.CombinePrefixAndSuffix("  a  ", "  b  "));
    }

    // ----- TryMergePrefixAndSuffix -----

    [Fact]
    public void TryMerge_AlreadyPrefixedWithSpace_ReturnsSuffixUnchanged()
    {
      Assert.Equal("префикс суффикс",
          VelumDxfArtifactNameRules.TryMergePrefixAndSuffix("префикс", "префикс суффикс", null));
    }

    [Fact]
    public void TryMerge_LegacyColonSpace_StripsColon()
    {
      Assert.Equal("префикс суффикс",
          VelumDxfArtifactNameRules.TryMergePrefixAndSuffix("префикс", "префикс: суффикс", null));
      Assert.Equal("префикс суффикс",
          VelumDxfArtifactNameRules.TryMergePrefixAndSuffix("префикс", "префикс:суффикс", null));
    }

    [Fact]
    public void TryMerge_PrefixGluedToSuffix_InsertsSeparator()
    {
      // суффикс начинается с префикса без разделителя — остаток склеивается через пробел
      Assert.Equal("префикс -001",
          VelumDxfArtifactNameRules.TryMergePrefixAndSuffix("префикс", "префикс-001", null));
    }

    [Fact]
    public void TryMerge_SuffixEqualsPartBaseName_ReturnsPrefixOnly()
    {
      Assert.Equal("префикс",
          VelumDxfArtifactNameRules.TryMergePrefixAndSuffix("префикс", "Деталь", "деталь"));
    }

    [Theory]
    [InlineData("префикс", null, "префикс")]
    [InlineData("префикс", "  ", "префикс")]
    [InlineData(null, "суффикс", "суффикс")]
    [InlineData("  ", "суффикс", "суффикс")]
    public void TryMerge_EmptySide_ReturnsOther(string prefix, string suffix, string expected)
    {
      Assert.Equal(expected, VelumDxfArtifactNameRules.TryMergePrefixAndSuffix(prefix, suffix, null));
    }

    [Fact]
    public void TryMerge_UnrelatedSides_CombinesBoth()
    {
      Assert.Equal("префикс суффикс",
          VelumDxfArtifactNameRules.TryMergePrefixAndSuffix("префикс", "суффикс", null));
    }

    [Fact]
    public void TryMerge_CaseInsensitivePrefixMatch_KeepsSuffixAsIs()
    {
      Assert.Equal("ПРЕФИКС суффикс",
          VelumDxfArtifactNameRules.TryMergePrefixAndSuffix("префикс", "ПРЕФИКС суффикс", null));
    }

    // ----- BuildRequiredPrefix -----

    [Theory]
    [InlineData("Деталь", "Конф1", 2, "Деталь_Конф1")]
    [InlineData("Деталь", "Конф1", 1, "Деталь")]      // одна конфигурация — без суффикса
    [InlineData("Деталь", "Конф1", 0, "Деталь")]
    [InlineData("Деталь", "", 3, "Деталь")]           // пустая конфигурация — без суффикса
    [InlineData(" Деталь ", " Конф1 ", 2, "Деталь_Конф1")]
    public void BuildRequiredPrefix_Variants(string baseName, string config, int count, string expected)
    {
      Assert.Equal(expected, VelumDxfArtifactNameRules.BuildRequiredPrefix(baseName, config, count));
    }

    // ----- NameContainsRequiredPrefix -----

    [Theory]
    [InlineData("Деталь", "Деталь", "", 1, true)]                 // точное совпадение
    [InlineData("Деталь суффикс", "Деталь", "", 1, true)]         // разделитель после префикса
    [InlineData("Деталь-001", "Деталь", "", 1, false)]            // «-» не разделитель и не буква/цифра
    [InlineData("Деталь001", "Деталь", "", 1, true)]              // цифра сразу после префикса
    [InlineData("Деталь_Конф1", "Деталь", "Конф1", 2, true)]      // префикс с конфигурацией
    [InlineData("Деталь,001", "Деталь", "", 1, false)]            // запятая — не разделитель и не буква
    [InlineData("другое", "Деталь", "", 1, false)]                // не начинается с префикса
    [InlineData("", "Деталь", "", 1, false)]
    [InlineData(null, "Деталь", "", 1, false)]
    [InlineData("Деталь2", "Деталь", "", 1, true)]                // цифра — допустимый продолжатель
    public void NameContainsRequiredPrefix_Variants(
        string fileName, string baseName, string config, int count, bool expected)
    {
      Assert.Equal(expected,
          VelumDxfArtifactNameRules.NameContainsRequiredPrefix(fileName, baseName, config, count));
    }

    [Fact]
    public void NameContainsRequiredPrefix_CaseInsensitive()
    {
      Assert.True(VelumDxfArtifactNameRules.NameContainsRequiredPrefix("ДЕТАЛЬ_КОНФ1 x", "деталь", "конф1", 2));
    }

    // ----- IsPrefixSuffixSeparatorChar -----

    [Theory]
    [InlineData(' ', true)]
    [InlineData(':', true)]
    [InlineData('\uFF1A', true)]  // полно宽ная «:» (legacy)
    [InlineData('-', false)]
    [InlineData(',', false)]
    [InlineData('0', false)]
    public void IsPrefixSuffixSeparatorChar_Variants(char c, bool expected)
    {
      Assert.Equal(expected, VelumDxfArtifactNameRules.IsPrefixSuffixSeparatorChar(c));
    }
  }
}
