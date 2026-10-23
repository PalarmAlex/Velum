using Velum.UI;
using Xunit;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>
  /// Тесты форматирования DSL-фильтра в человекочитаемый вид для шапки отчёта.
  /// Проверяются маска-префикс, разбиение токенов по «|» и пустые значения.
  /// </summary>
  public class VelumListFilterFormatterTests
  {
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("=")]
    [InlineData("!=")]
    [InlineData("|")]
    [InlineData("||")]
    public void Format_EmptyOrMaskOnly_ReturnsEmpty(string expression)
    {
      Assert.Equal(string.Empty, VelumListFilterFormatter.Format(expression));
    }

    [Theory]
    [InlineData("сталь", "сталь")]
    [InlineData("*сталь", "сталь")]
    [InlineData("=сталь", "=сталь")]
    [InlineData("!=сталь", "!=сталь")]
    [InlineData("!сталь", "!сталь")]
    [InlineData(">10", ">10")]
    [InlineData(">=10", ">=10")]
    [InlineData("<10", "<10")]
    [InlineData("<=10", "<=10")]
    public void Format_SingleToken_PrefixPreserved(string expression, string expected)
    {
      Assert.Equal(expected, VelumListFilterFormatter.Format(expression));
    }

    [Theory]
    [InlineData("сталь|чугун", "сталь, чугун")]
    [InlineData("=сталь|чугун", "=сталь, чугун")]
    [InlineData("!=сталь|чугун|титан", "!=сталь, чугун, титан")]
    [InlineData("=сталь| чугун ", "=сталь, чугун")]
    [InlineData("=сталь||чугун", "=сталь, чугун")]
    public void Format_MultipleTokens_JoinedWithComma(string expression, string expected)
    {
      Assert.Equal(expected, VelumListFilterFormatter.Format(expression));
    }

    [Fact]
    public void Format_TrimsOuterWhitespace()
    {
      Assert.Equal("=сталь, чугун", VelumListFilterFormatter.Format("  =сталь|чугун  "));
    }
  }
}
