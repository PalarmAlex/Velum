using Velum.ReactiveCore;
using Xunit;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>
  /// Регресс-тесты чистой логики разбора числовых параметров геометрии
  /// (<see cref="VelumRecipeGeometryRules"/>): мм→м, проверка чисел и неразрешённых токенов.
  /// </summary>
  public sealed class VelumRecipeGeometryRulesTests
  {
    #region TryResolvedToMeters

    [Theory]
    [InlineData("100", 0.1)]
    [InlineData("12.5", 0.0125)]
    [InlineData("12,5", 0.0125)]   // запятая как десятичный разделитель
    [InlineData(" 80 ", 0.08)]     // обрамляющие пробелы
    public void TryResolvedToMeters_PositiveNumber_ConvertsToMeters(string raw, double expectedMeters)
    {
      Assert.True(VelumRecipeGeometryRules.TryResolvedToMeters(raw, out double meters, out string error));
      Assert.True(string.IsNullOrEmpty(error));
      Assert.Equal(expectedMeters, meters, 9);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TryResolvedToMeters_Empty_MissingValue(string raw)
    {
      Assert.False(VelumRecipeGeometryRules.TryResolvedToMeters(raw, out _, out string error));
      Assert.Equal("missing_value", error);
    }

    [Theory]
    [InlineData("{V:Толщина}")]     // неразрешённая переменная психики
    [InlineData("{DOCUMENT_PATH}")] // неразрешённый плейсхолдер окружения
    [InlineData("$PRP:\"SW-Foo\"")] // неразрешённая ссылка на свойство
    public void TryResolvedToMeters_UnresolvedToken_ReportsUnresolved(string raw)
    {
      Assert.False(VelumRecipeGeometryRules.TryResolvedToMeters(raw, out _, out string error));
      Assert.StartsWith("unresolved_token:", error);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("10 мм")]
    public void TryResolvedToMeters_NonNumeric_ReportsInvalidNumber(string raw)
    {
      Assert.False(VelumRecipeGeometryRules.TryResolvedToMeters(raw, out _, out string error));
      Assert.StartsWith("invalid_number:", error);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("0.0")]
    public void TryResolvedToMeters_NonPositive_ReportsNonPositive(string raw)
    {
      Assert.False(VelumRecipeGeometryRules.TryResolvedToMeters(raw, out _, out string error));
      Assert.StartsWith("non_positive:", error);
    }

    #endregion

    #region TryParseMeters

    [Theory]
    [InlineData("1", 1)]
    [InlineData("1000", 1000)]
    [InlineData("0.5", 0.5)]
    [InlineData("-3", -3)] // парс допускает отрицательное (знак проверяет вызывающий)
    public void TryParseMeters_ValidNumbers_Parsed(string raw, double expected)
    {
      Assert.True(VelumRecipeGeometryRules.TryParseMeters(raw, out double mm, out string error));
        Assert.True(string.IsNullOrEmpty(error));
      Assert.Equal(expected, mm, 9);
    }

    [Fact]
    public void TryParseMeters_Empty_ReportsEmptyNumber()
    {
      Assert.False(VelumRecipeGeometryRules.TryParseMeters(null, out _, out string error));
      Assert.Equal("empty_number", error);
    }

    #endregion

    #region ContainsUnresolvedToken

    [Theory]
    [InlineData("{X}", true)]
    [InlineData("$PRP:\"A\"", true)]
    [InlineData("100", false)]
    [InlineData("plain", false)]
    [InlineData("", false)]
    public void ContainsUnresolvedToken_DetectsPlaceholders(string value, bool expected)
    {
      Assert.Equal(expected, VelumRecipeGeometryRules.ContainsUnresolvedToken(value));
    }

    #endregion

    #region Коэффициент единиц

    [Fact]
    public void MillimetersToMeters_IsOneThousandth()
    {
      // 1 мм = 0.001 м (COM SolidWorks работает в метрах).
      Assert.Equal(0.001, VelumRecipeGeometryRules.MillimetersToMeters, 9);
    }

    #endregion
  }
}
