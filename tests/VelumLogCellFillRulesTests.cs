using Velum.UI.Logs;
using Xunit;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>
  /// Регрессионные тесты чистой логики фоновой подсветки ячеек HTML-отчётов логов.
  /// Фиксируют семантику цветов по макетам AIStudio и ТЗ:
  /// «Состояние» (-1 красный / 0 жёлтый / 1 зелёный), «Среда» (положительное —
  /// красный, отрицательное — зелёный), «Опасно», «ОР/УМ», «Состояние параметров».
  /// </summary>
  public class VelumLogCellFillRulesTests
  {
    [Theory]
    [InlineData(-1, VelumLogCellFill.Red)]
    [InlineData(0, VelumLogCellFill.Yellow)]
    [InlineData(1, VelumLogCellFill.Green)]
    [InlineData(2, VelumLogCellFill.None)]
    public void StateFill_MapsBaseIdToColor(int baseId, VelumLogCellFill expected)
    {
      Assert.Equal(expected, VelumLogCellFillRules.StateFill(baseId));
    }

    [Fact]
    public void StateFill_Null_ReturnsNone()
    {
      Assert.Equal(VelumLogCellFill.None, VelumLogCellFillRules.StateFill(null));
    }

    [Theory]
    [InlineData("1:5", VelumLogCellFill.Red)]      // положительная величина → красный
    [InlineData("1:-5", VelumLogCellFill.Green)]   // отрицательная → зелёный
    [InlineData("1:+5", VelumLogCellFill.Red)]     // явный плюс
    [InlineData("1:0", VelumLogCellFill.None)]     // ноль — знака нет
    [InlineData("-", VelumLogCellFill.None)]
    [InlineData("", VelumLogCellFill.None)]
    [InlineData("   ", VelumLogCellFill.None)]
    [InlineData("abc", VelumLogCellFill.None)]     // нераспознанный сегмент
    public void EnvironmentFill_SingleSegment_BySign(string cell, VelumLogCellFill expected)
    {
      Assert.Equal(expected, VelumLogCellFillRules.EnvironmentFill(cell));
    }

    [Theory]
    [InlineData("1:5,2:-3", VelumLogCellFill.Red)]     // смешанные → приоритет положительному
    [InlineData("1:-3,2:5", VelumLogCellFill.Red)]     // порядок сегментов не важен
    [InlineData("1:-3,2:-7", VelumLogCellFill.Green)]  // только отрицательные
    public void EnvironmentFill_MixedSegments_PositiveWins(string cell, VelumLogCellFill expected)
    {
      Assert.Equal(expected, VelumLogCellFillRules.EnvironmentFill(cell));
    }

    [Theory]
    [InlineData(true, VelumLogCellFill.Red)]
    [InlineData(false, VelumLogCellFill.None)]
    public void DangerFill_RedOnlyWhenDanger(bool danger, VelumLogCellFill expected)
    {
      Assert.Equal(expected, VelumLogCellFillRules.DangerFill(danger));
    }

    [Theory]
    [InlineData("ОР", null, VelumLogCellFill.Red)]
    [InlineData("ОР1", null, VelumLogCellFill.Red)]
    [InlineData("ОР2", null, VelumLogCellFill.Yellow)]
    [InlineData("УМ1", true, VelumLogCellFill.Green)]
    [InlineData("УМ1", false, VelumLogCellFill.Red)]
    [InlineData("УМ2", true, VelumLogCellFill.Green)]
    [InlineData("УМ2", false, VelumLogCellFill.Red)]
    [InlineData("УМ1", null, VelumLogCellFill.None)] // успех неизвестен — без фона
    [InlineData("-", null, VelumLogCellFill.None)]
    public void OrUmFill_MapsLevelAndSuccess(string orUm, bool? success, VelumLogCellFill expected)
    {
      Assert.Equal(expected, VelumLogCellFillRules.OrUmFill(orUm, success));
    }

    [Theory]
    [InlineData(-1, VelumLogCellFill.Red)]
    [InlineData(0, VelumLogCellFill.None)]
    [InlineData(1, VelumLogCellFill.Green)]
    public void ParameterStateFill_MapsCodeToColor(int stateCode, VelumLogCellFill expected)
    {
      Assert.Equal(expected, VelumLogCellFillRules.ParameterStateFill(stateCode));
    }
  }
}
