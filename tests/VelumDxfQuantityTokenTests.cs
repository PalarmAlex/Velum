using System;
using System.Collections.Generic;
using Xunit;
using Velum.ReactiveCore.Export;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>
  /// Тесты VelumDxfQuantityToken — плейсхолдеры кол-ва в маске имени DXF.
  /// </summary>
  public class VelumDxfQuantityTokenTests
  {
    // ----- IsQuantityToken -----

    [Xunit.Fact]
    public void IsQuantityToken_RecognizesBothTokensCaseInsensitive()
    {
      Xunit.Assert.True(VelumDxfQuantityToken.IsQuantityToken("[Quantity]"));
      Xunit.Assert.True(VelumDxfQuantityToken.IsQuantityToken("[quantity]"));
      Xunit.Assert.True(VelumDxfQuantityToken.IsQuantityToken("[Кол-во]"));
      Xunit.Assert.True(VelumDxfQuantityToken.IsQuantityToken("  [КОЛ-ВО]  "));
    }

    [Xunit.Fact]
    public void IsQuantityToken_RejectsNonTokens()
    {
      Xunit.Assert.False(VelumDxfQuantityToken.IsQuantityToken(null));
      Xunit.Assert.False(VelumDxfQuantityToken.IsQuantityToken(""));
      Xunit.Assert.False(VelumDxfQuantityToken.IsQuantityToken("Quantity"));
      Xunit.Assert.False(VelumDxfQuantityToken.IsQuantityToken("Detail-[Quantity]"));
    }

    // ----- PatternReferencesQuantity -----

    [Xunit.Fact]
    public void PatternReferencesQuantity_FindsEitherToken()
    {
      Xunit.Assert.True(VelumDxfQuantityToken.PatternReferencesQuantity("Деталь-[Quantity].dxf"));
      Xunit.Assert.True(VelumDxfQuantityToken.PatternReferencesQuantity("Деталь-[Кол-во]"));
      Xunit.Assert.False(VelumDxfQuantityToken.PatternReferencesQuantity("Деталь-001"));
      Xunit.Assert.False(VelumDxfQuantityToken.PatternReferencesQuantity(null));
    }

    // ----- StripQuantityTokens -----

    [Xunit.Fact]
    public void StripQuantityTokens_RemovesBothTokens()
    {
      string stripped = VelumDxfQuantityToken.StripQuantityTokens("Деталь-[Quantity]-[Кол-во]");
      Xunit.Assert.Equal("Деталь--", stripped);
    }

    [Xunit.Fact]
    public void StripQuantityTokens_NullReturnsEmpty()
    {
      Xunit.Assert.Equal(string.Empty, VelumDxfQuantityToken.StripQuantityTokens(null));
    }

    // ----- ResolveQuantity / TryResolveQuantity -----

    [Xunit.Fact]
    public void ResolveQuantity_MissingContext_ReturnsDefault()
    {
      Xunit.Assert.Equal(VelumDxfQuantityToken.DefaultQuantity, VelumDxfQuantityToken.ResolveQuantity(null));
      Xunit.Assert.Equal(VelumDxfQuantityToken.DefaultQuantity,
          VelumDxfQuantityToken.ResolveQuantity(new Dictionary<string, string>()));
    }

    [Xunit.Fact]
    public void ResolveQuantity_ValidValue_ReturnsIt()
    {
      var ctx = new Dictionary<string, string> { [VelumDxfQuantityToken.ContextKey] = " 7 " };
      Xunit.Assert.Equal(7, VelumDxfQuantityToken.ResolveQuantity(ctx));
    }

    [Xunit.Fact]
    public void ResolveQuantity_InvalidOrNegative_ReturnsDefault()
    {
      Xunit.Assert.Equal(VelumDxfQuantityToken.DefaultQuantity,
          VelumDxfQuantityToken.ResolveQuantity(new Dictionary<string, string> { [VelumDxfQuantityToken.ContextKey] = "abc" }));
      Xunit.Assert.Equal(VelumDxfQuantityToken.DefaultQuantity,
          VelumDxfQuantityToken.ResolveQuantity(new Dictionary<string, string> { [VelumDxfQuantityToken.ContextKey] = "-2" }));
    }

    [Xunit.Fact]
    public void TryResolveQuantity_NullWhenMissing()
    {
      Xunit.Assert.Null(VelumDxfQuantityToken.TryResolveQuantity(null));
      Xunit.Assert.Equal(5, VelumDxfQuantityToken.TryResolveQuantity(
          new Dictionary<string, string> { [VelumDxfQuantityToken.ContextKey] = "5" }));
    }

    // ----- FormatQuantity -----

    [Xunit.Fact]
    public void FormatQuantity_NegativeClampedToZero()
    {
      Xunit.Assert.Equal("0", VelumDxfQuantityToken.FormatQuantity(-3));
      Xunit.Assert.Equal("12", VelumDxfQuantityToken.FormatQuantity(12));
    }

    // ----- SubstituteInPattern -----

    [Xunit.Fact]
    public void SubstituteInPattern_ReplacesBothTokens()
    {
      Assert.Equal("Деталь-3-3",
          VelumDxfQuantityToken.SubstituteInPattern("Деталь-[quantity]-[Кол-во]", 3));
    }

    [Xunit.Fact]
    public void SubstituteInPattern_WithoutTokens_PatternUnchanged()
    {
      Xunit.Assert.Equal("Деталь-001", VelumDxfQuantityToken.SubstituteInPattern("Деталь-001", 3));
      Xunit.Assert.Equal(string.Empty, VelumDxfQuantityToken.SubstituteInPattern(null, 3));
    }

    // ----- TryParseQuantityFromFileName -----

    [Theory]
    [InlineData("Деталь- 12шт", 12)]
    [InlineData("Деталь-3 шт", 3)]
    [InlineData("Деталь-7ШТ", 7)]
    public void TryParseQuantityFromFileName_ValidSuffixes(string name, int expected)
    {
      Xunit.Assert.True(VelumDxfQuantityToken.TryParseQuantityFromFileName(name, out int qty));
      Xunit.Assert.Equal(expected, qty);
    }

    [Xunit.Fact]
    public void TryParseQuantityFromFileName_InvalidNames()
    {
      Xunit.Assert.False(VelumDxfQuantityToken.TryParseQuantityFromFileName(null, out _));
      Xunit.Assert.False(VelumDxfQuantityToken.TryParseQuantityFromFileName("Деталь-001", out _));
      Xunit.Assert.False(VelumDxfQuantityToken.TryParseQuantityFromFileName("Деталь-шт", out _));
    }

    // ----- StripQuantitySuffix -----

    [Xunit.Fact]
    public void StripQuantitySuffix_RemovesQtyTail()
    {
      Xunit.Assert.Equal("Деталь", VelumDxfQuantityToken.StripQuantitySuffix("Деталь- 12шт"));
      Xunit.Assert.Equal(string.Empty, VelumDxfQuantityToken.StripQuantitySuffix(null));
    }

    // ----- EnumerateTokenNames -----

    [Xunit.Fact]
    public void EnumerateTokenNames_ReturnsBothTokens()
    {
      var names = new List<string>(VelumDxfQuantityToken.EnumerateTokenNames());
      Xunit.Assert.Equal(2, names.Count);
      Xunit.Assert.Contains("[Quantity]", names);
      Xunit.Assert.Contains("[Кол-во]", names);
    }
  }
}
