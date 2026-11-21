using System.Collections.Generic;
using Velum.UI;
using Xunit;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>
  /// Регрессия правил кнопок «Поощрить»/«Наказать»: сопоставление кнопки с ID воздействия
  /// и сбор скрытых ID для фильтра пикера. Ловит риск «в пикере дублируется воздействие
  /// кнопки» и «кнопка не настроена, но пытается отправить пустой стимул».
  /// </summary>
  public class VelumInfluenceButtonRulesTests
  {
    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    public void IsConfigured_PositiveId_True(int influenceId)
    {
      Assert.True(VelumInfluenceButtonRules.IsConfigured(influenceId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void IsConfigured_NonPositive_False(int influenceId)
    {
      Assert.False(VelumInfluenceButtonRules.IsConfigured(influenceId));
    }

    [Fact]
    public void ResolveTriggerIds_Configured_ReturnsSingleId()
    {
      List<int> ids = VelumInfluenceButtonRules.ResolveTriggerIds(7);
      Assert.Equal(new List<int> { 7 }, ids);
    }

    [Fact]
    public void ResolveTriggerIds_NotConfigured_ReturnsEmpty()
    {
        Assert.Empty(VelumInfluenceButtonRules.ResolveTriggerIds(0));
    }

    [Fact]
    public void BuildHiddenInfluenceIds_UnionsBothButtons()
    {
      HashSet<int> hidden = VelumInfluenceButtonRules.BuildHiddenInfluenceIds(3, 5);
      Assert.Contains(3, hidden);
      Assert.Contains(5, hidden);
      Assert.Equal(2, hidden.Count);
    }

    [Fact]
    public void BuildHiddenInfluenceIds_SameId_Collapsed()
    {
      HashSet<int> hidden = VelumInfluenceButtonRules.BuildHiddenInfluenceIds(4, 4);
      Assert.Single(hidden);
      Assert.Contains(4, hidden);
    }

    [Fact]
    public void BuildHiddenInfluenceIds_NonPositive_Ignored()
    {
      HashSet<int> hidden = VelumInfluenceButtonRules.BuildHiddenInfluenceIds(0, -7);
      Assert.Empty(hidden);
    }

    [Fact]
    public void ConfiguredTooltip_MentionsInfluenceId()
    {
      string text = VelumInfluenceButtonRules.BuildConfiguredTooltip("Поощрить", 11);
      Assert.Contains("Поощрить", text);
      Assert.Contains("11", text);
    }
  }
}
