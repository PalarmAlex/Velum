using System.Globalization;
using Velum.ReactiveCore;
using Xunit;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>
  /// Регресс-тест для защиты шага <c>run_sw_command</c> от задвоения команды оператора.
  /// <para>
  /// Сценарий: оператор жмёт «Сохранить» в SolidWorks → SolidWorks выполняет команду
  /// <c>sw.RunCommand(1234)</c> → токен <c>sw:1234</c> flush-ится в ISIDA → триггерит
  /// рефлекс/автоматизм → RecipeDispatcher запускает рецепт → рецепт содержит шаг
  /// <c>run_sw_command</c> с тем же <c>command_id=1234</c> → шаг должен быть пропущен.
  /// </para>
  /// Проверяется чистая логика правил (<see cref="VelumRunSwCommandEchoRules"/>),
  /// без ISIDA / SolidWorks / WinForms.
  /// </summary>
  public sealed class VelumRunSwCommandEchoRulesTests
  {
    #region TryParseSwCode

    [Fact]
    public void TryParseSwCode_ValidToken_ReturnsCommandId()
    {
      Assert.True(VelumRunSwCommandEchoRules.TryParseSwCode("sw:21", out int id));
      Assert.Equal(21, id);
    }

    [Fact]
    public void TryParseSwCode_ValidToken_CaseInsensitive()
    {
      Assert.True(VelumRunSwCommandEchoRules.TryParseSwCode("SW:4098", out int id));
      Assert.Equal(4098, id);
    }

    [Fact]
    public void TryParseSwCode_WhiteSpace_Trimmed()
    {
      Assert.True(VelumRunSwCommandEchoRules.TryParseSwCode("  sw:100  ", out int id));
      Assert.Equal(100, id);
    }

    [Fact]
    public void TryParseSwCode_ZeroId_False()
    {
      Assert.False(VelumRunSwCommandEchoRules.TryParseSwCode("sw:0", out _));
    }

    [Fact]
    public void TryParseSwCode_NegativeId_False()
    {
      Assert.False(VelumRunSwCommandEchoRules.TryParseSwCode("sw:-5", out _));
    }

    [Fact]
    public void TryParseSwCode_MissingPrefix_False()
    {
      Assert.False(VelumRunSwCommandEchoRules.TryParseSwCode("1234", out _));
    }

    [Fact]
    public void TryParseSwCode_Empty_False()
    {
      Assert.False(VelumRunSwCommandEchoRules.TryParseSwCode("", out _));
    }

    [Fact]
    public void TryParseSwCode_Null_False()
    {
      Assert.False(VelumRunSwCommandEchoRules.TryParseSwCode(null, out _));
    }

    [Fact]
    public void TryParseSwCode_OnlyPrefix_False()
    {
      Assert.False(VelumRunSwCommandEchoRules.TryParseSwCode("sw:", out _));
    }

    [Fact]
    public void TryParseSwCode_NonNumeric_False()
    {
      Assert.False(VelumRunSwCommandEchoRules.TryParseSwCode("sw:abc", out _));
    }

    #endregion

    #region ShouldSuppressAsEcho

    [Fact]
    public void ShouldSuppressAsEcho_SameIds_ReturnsTrue()
    {
      // Оператор выполнил sw:1234, рецепт пытается выполнить тот же sw:1234
      Assert.True(VelumRunSwCommandEchoRules.ShouldSuppressAsEcho(1234, 1234));
    }

    [Fact]
    public void ShouldSuppressAsEcho_DifferentIds_ReturnsFalse()
    {
      // Оператор выполнил sw:1234, рецепт пытается выполнить sw:5678 — разные команды
      Assert.False(VelumRunSwCommandEchoRules.ShouldSuppressAsEcho(1234, 5678));
    }

    [Fact]
    public void ShouldSuppressAsEcho_NoOperatorCommand_ReturnsFalse()
    {
      // commandPatternId=0 — рецепт запущен не командным триггером
      Assert.False(VelumRunSwCommandEchoRules.ShouldSuppressAsEcho(0, 1234));
    }

    [Fact]
    public void ShouldSuppressAsEcho_NoRecipeCommand_ReturnsFalse()
    {
      Assert.False(VelumRunSwCommandEchoRules.ShouldSuppressAsEcho(1234, 0));
    }

    [Fact]
    public void ShouldSuppressAsEcho_BothZero_ReturnsFalse()
    {
      Assert.False(VelumRunSwCommandEchoRules.ShouldSuppressAsEcho(0, 0));
    }

    #endregion

    #region End-to-End: паттерн → sw-код → сравнение

    [Fact]
    public void SwCodeParsing_RoundTrip()
    {
      // Проверяем, что любые допустимые sw-коды парсятся и сравниваются корректно
      for (int code = 1; code <= 100; code += 17)
      {
        string token = "sw:" + code.ToString(CultureInfo.InvariantCulture);
        Assert.True(VelumRunSwCommandEchoRules.TryParseSwCode(token, out int parsed));
        Assert.Equal(code, parsed);

        // Разные коды не должны совпадать
        Assert.False(VelumRunSwCommandEchoRules.ShouldSuppressAsEcho(code, code + 1));
        // Одни и те же — должны
        Assert.True(VelumRunSwCommandEchoRules.ShouldSuppressAsEcho(code, code));
      }
    }

    #endregion
  }
}
