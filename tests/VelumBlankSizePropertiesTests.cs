using Xunit;
using Velum.ReactiveCore;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>
  /// Тесты VelumBlankSizeProperties — свойства габарита/проката заготовки.
  /// </summary>
  public class VelumBlankSizePropertiesTests
  {
    // ----- IsBlankSizePropertyName -----

    [Fact]
    public void IsBlankSizePropertyName_RecognizesThreeProps()
    {
      Assert.True(VelumBlankSizeProperties.IsBlankSizePropertyName("Толщина"));
      Assert.True(VelumBlankSizeProperties.IsBlankSizePropertyName(" Длина "));
      Assert.True(VelumBlankSizeProperties.IsBlankSizePropertyName("Ширина"));
    }

    [Fact]
    public void IsBlankSizePropertyName_RejectsOthers()
    {
      Assert.False(VelumBlankSizeProperties.IsBlankSizePropertyName(null));
      Assert.False(VelumBlankSizeProperties.IsBlankSizePropertyName(""));
      Assert.False(VelumBlankSizeProperties.IsBlankSizePropertyName("Прокат"));
      Assert.False(VelumBlankSizeProperties.IsBlankSizePropertyName("толщина")); // регистр значим
    }

    // ----- IsBlankSizeProbeKey -----

    [Fact]
    public void IsBlankSizeProbeKey_MatchesExactKey()
    {
      Assert.True(VelumBlankSizeProperties.IsBlankSizeProbeKey(VelumBlankSizeProperties.ProbeKeyLinksOk));
      Assert.True(VelumBlankSizeProperties.IsBlankSizeProbeKey(" " + VelumBlankSizeProperties.ProbeKeyLinksOk + " "));
      Assert.False(VelumBlankSizeProperties.IsBlankSizeProbeKey(null));
      Assert.False(VelumBlankSizeProperties.IsBlankSizeProbeKey("другой"));
    }

    // ----- TryParseNumber -----

    [Theory]
    [InlineData("5", 5.0)]
    [InlineData("5.000", 5.0)]
    [InlineData("5,25", 5.25)]
    [InlineData("-3.5", -3.5)]
    [InlineData("+2", 2.0)]
    [InlineData("10 mm", 10.0)]
    [InlineData("12мм", 12.0)]
    public void TryParseNumber_ValidValues(string text, double expected)
    {
      Assert.True(VelumBlankSizeProperties.TryParseNumber(text, out double value));
      Assert.Equal(expected, value, 9);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Ст3сп5")]     // ведущий текст — не число
    [InlineData("Швеллер 10П")]
    [InlineData("abc")]
    public void TryParseNumber_InvalidValues(string text)
    {
      Assert.False(VelumBlankSizeProperties.TryParseNumber(text, out _));
    }

    // ----- FormatValueForFileName -----

    [Theory]
    [InlineData("5.000", "5")]
    [InlineData("5", "5")]
    [InlineData("5.24", "5.2")]
    [InlineData("5.25", "5.3")]   // MidpointRounding.AwayFromZero
    [InlineData("0.05", "0.1")]
    [InlineData("10 mm", "10")]
    public void FormatValueForFileName_Normalizes(string raw, string expected)
    {
      Assert.Equal(expected, VelumBlankSizeProperties.FormatValueForFileName(raw));
    }

    [Fact]
    public void FormatValueForFileName_NonNumericPassthrough()
    {
      Assert.Equal("Лист", VelumBlankSizeProperties.FormatValueForFileName("Лист"));
      Assert.Equal(string.Empty, VelumBlankSizeProperties.FormatValueForFileName(null));
      Assert.Equal(string.Empty, VelumBlankSizeProperties.FormatValueForFileName("   "));
    }
  }
}