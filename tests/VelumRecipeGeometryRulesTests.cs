using System;
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

    #region TryInscribedDiameterToCircumRadius (многоугольник «под ключ»)

    [Theory]
    [InlineData(100, 6, 57.735026918962576)] // гексагон: 50 / cos(30°)
    [InlineData(100, 3, 100.0)]               // треугольник: 50 / cos(60°) = 100
    [InlineData(100, 4, 70.710678118654752)] // квадрат: 50 / cos(45°)
    [InlineData(80, 6, 46.188021535170061)]  // масштаб: 40 / cos(30°)
    public void TryInscribedDiameterToCircumRadius_KnownPolygons_ReturnsCircumRadius(
        double inscribedDiameterMm, int sides, double expectedCircumMm)
    {
      Assert.True(VelumRecipeGeometryRules.TryInscribedDiameterToCircumRadius(
          inscribedDiameterMm, sides, out double circumMm, out string error));
      Assert.True(string.IsNullOrEmpty(error));
      Assert.Equal(expectedCircumMm, circumMm, 9);
    }

    [Theory]
    [InlineData(100, 3)]
    [InlineData(100, 6)]
    [InlineData(100, 8)]
    [InlineData(37.5, 5)]
    public void TryInscribedDiameterToCircumRadius_Invariant_ApothemEqualsHalfDiameter(
        double inscribedDiameterMm, int sides)
    {
      // Регрессия на исходный баг: раньше вписанная Ø выходила diameter·cos(π/sides) (86.6 вместо 100).
      // Инвариант: circumRadius·cos(π/sides) == inscribedDiameter/2 (апофема = радиус вписанной).
      Assert.True(VelumRecipeGeometryRules.TryInscribedDiameterToCircumRadius(
          inscribedDiameterMm, sides, out double circumMm, out _));

      double apothem = circumMm * Math.Cos(Math.PI / sides);
      Assert.Equal(inscribedDiameterMm / 2.0, apothem, 9);
    }

    [Theory]
    [InlineData(0, 6)]
    [InlineData(-10, 6)]
    public void TryInscribedDiameterToCircumRadius_NonPositiveDiameter_Fails(double diameter, int sides)
    {
      Assert.False(VelumRecipeGeometryRules.TryInscribedDiameterToCircumRadius(
          diameter, sides, out _, out string error));
      Assert.StartsWith("non_positive_diameter:", error);
    }

    [Theory]
    [InlineData(100, 2)]
    [InlineData(100, 0)]
    [InlineData(100, -3)]
    public void TryInscribedDiameterToCircumRadius_TooFewSides_Fails(double diameter, int sides)
    {
      Assert.False(VelumRecipeGeometryRules.TryInscribedDiameterToCircumRadius(
          diameter, sides, out _, out string error));
      Assert.StartsWith("sides_min_3:", error);
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
