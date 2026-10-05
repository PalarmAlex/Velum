using Velum.UI;
using Xunit;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>
  /// Регрессия: кнопка «Запретить» должна штрафовать сенсорную связь CS₁→CS₂,
  /// когда форма открыта активацией через сенсорный гейт («муха»), и только тогда —
  /// иначе прежний сброс крепости у-рефлекса. Ловит баг «Запретить наказывает не того».
  /// </summary>
  public class VelumReflexForbidTargetRulesTests
  {
    [Fact]
    public void GateEpisode_TargetsSensoryLink()
    {
      // Полноценный снимок гейт-эпизода (бедный CS₁ открыл богатый CS₂).
      Assert.True(VelumReflexForbidTargetRules.TargetsSensoryLink(21, 210));
    }

    [Theory]
    [InlineData(0, 0)]   // обычный путь (exact match) — штраф идёт на УР
    [InlineData(21, 0)]  // CS₂ не записан — путь неизвестен, на связь не претендуем
    [InlineData(0, 210)] // CS₁ не записан
    [InlineData(-1, 210)]
    [InlineData(210, -5)]
    [InlineData(7, 7)]   // CS₁ == CS₂ — вырожденная пара, связи не существует
    public void NonGateEpisode_TargetsReflex(int cs1, int cs2)
    {
      Assert.False(VelumReflexForbidTargetRules.TargetsSensoryLink(cs1, cs2));
    }

    [Fact]
    public void LinkConfirmText_MentionsBothEndpoints()
    {
      string text = VelumReflexForbidTargetRules.BuildLinkConfirmText(21, 210);
      Assert.Contains("21", text);
      Assert.Contains("210", text);
      // Текст обязан обещать сохранность у-рефлекса — это суть адресного штрафа.
      Assert.Contains("У-рефлекс", text);
    }

    [Fact]
    public void ReflexConfirmText_MentionsReflexId()
    {
      string text = VelumReflexForbidTargetRules.BuildReflexConfirmText(42);
      Assert.Contains("42", text);
      Assert.Contains("крепост", text);
    }
  }
}
