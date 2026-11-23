using System.Collections.Generic;
using Velum.ReactiveCore;
using Xunit;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>
  /// Регресс-тесты чистого резолвера переменных психики <c>{V:имя}</c>
  /// (<see cref="VelumRecipeVariableResolver"/>), без ISIDA / SolidWorks / WinForms.
  /// </summary>
  public sealed class VelumRecipeVariableResolverTests
  {
    #region Resolve

    [Fact]
    public void Resolve_KnownVariable_SubstitutesValue()
    {
      var vars = new Dictionary<string, string> { ["ГабаритX"] = "120" };
      Assert.Equal("120", VelumRecipeVariableResolver.Resolve("{V:ГабаритX}", vars));
    }

    [Fact]
    public void Resolve_MixedTextAndVariable_SubstitutesInPlace()
    {
      var vars = new Dictionary<string, string> { ["Толщина"] = "8" };
      Assert.Equal(
          "plate-8-mm",
          VelumRecipeVariableResolver.Resolve("plate-{V:Толщина}-mm", vars));
    }

    [Fact]
    public void Resolve_MultipleVariables_SubstitutesAll()
    {
      var vars = new Dictionary<string, string> { ["X"] = "10", ["Y"] = "20" };
      Assert.Equal(
          "10x20",
          VelumRecipeVariableResolver.Resolve("{V:X}x{V:Y}", vars));
    }

    [Fact]
    public void Resolve_WhitespaceAroundColon_Tolerated()
    {
      var vars = new Dictionary<string, string> { ["Имя"] = "7" };
      Assert.Equal("7", VelumRecipeVariableResolver.Resolve("{ V : Имя }", vars));
    }

    [Fact]
    public void Resolve_UnknownVariable_KeepsToken()
    {
      var vars = new Dictionary<string, string> { ["X"] = "10" };
      // Неизвестная переменная остаётся неразрешённой (токен целиком).
      Assert.Equal("{V:Нет}", VelumRecipeVariableResolver.Resolve("{V:Нет}", vars));
    }

    [Fact]
    public void Resolve_EmptyValue_KeepsToken()
    {
      var vars = new Dictionary<string, string> { ["X"] = "" };
      Assert.Equal("{V:X}", VelumRecipeVariableResolver.Resolve("{V:X}", vars));
    }

    [Fact]
    public void Resolve_NullVariables_KeepsToken()
    {
      Assert.Equal("{V:X}", VelumRecipeVariableResolver.Resolve("{V:X}", null));
    }

    [Fact]
    public void Resolve_CaseInsensitiveKey()
    {
      // Ключи словаря регистронезависимы (как и в VelumRecipePsychicVariables).
      var vars = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
      {
        ["Габарит"] = "50"
      };
      Assert.Equal("50", VelumRecipeVariableResolver.Resolve("{V:габарит}", vars));
    }

    [Fact]
    public void Resolve_NoVariableTokens_ReturnsUnchanged()
    {
      var vars = new Dictionary<string, string> { ["X"] = "10" };
      Assert.Equal("120", VelumRecipeVariableResolver.Resolve("120", vars));
      Assert.Equal("{DOCUMENT_PATH}", VelumRecipeVariableResolver.Resolve("{DOCUMENT_PATH}", vars));
    }

    [Fact]
    public void Resolve_NullOrEmptyInput_ReturnsEmpty()
    {
      Assert.Equal(string.Empty, VelumRecipeVariableResolver.Resolve(null, new Dictionary<string, string>()));
      Assert.Equal(string.Empty, VelumRecipeVariableResolver.Resolve(string.Empty, new Dictionary<string, string>()));
    }

    #endregion

    #region ContainsVariableTokens

    [Fact]
    public void ContainsVariableTokens_Present_ReturnsTrue()
    {
      Assert.True(VelumRecipeVariableResolver.ContainsVariableTokens("a{V:X}b"));
    }

    [Fact]
    public void ContainsVariableTokens_Absent_ReturnsFalse()
    {
      Assert.False(VelumRecipeVariableResolver.ContainsVariableTokens("{DOCUMENT_PATH}"));
      Assert.False(VelumRecipeVariableResolver.ContainsVariableTokens("120"));
      Assert.False(VelumRecipeVariableResolver.ContainsVariableTokens(null));
    }

    #endregion

    #region CollectVariableNames

    [Fact]
    public void CollectVariableNames_Duplicates_Deduplicated()
    {
      IReadOnlyList<string> names = VelumRecipeVariableResolver.CollectVariableNames(
          "{V:X}-{V:Y}-{V:X}");
      Assert.Equal(new[] { "X", "Y" }, names);
    }

    [Fact]
    public void CollectVariableNames_None_Empty()
    {
      Assert.Empty(VelumRecipeVariableResolver.CollectVariableNames("{DOCUMENT_PATH}"));
    }

    #endregion
  }
}
