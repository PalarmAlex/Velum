using System.Collections.Generic;
using Velum.ReactiveCore.DocumentTemplates;
using Xunit;
using Zone = Velum.ReactiveCore.DocumentTemplates.VelumDocumentParameterProbeRules.Zone;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>
  /// Тесты зон и свёртки группы параметров: good/warn/bad, линейная шкала 0…100,
  /// режимы all/any.
  /// </summary>
  public class VelumDocumentParameterProbeRulesTests
  {
    [Fact]
    public void Classify_InsideRange_IsGood()
    {
      Assert.Equal(Zone.Good, VelumDocumentParameterProbeRules.Classify(50, 10, 100, 5));
    }

    [Fact]
    public void Classify_NoBounds_IsGood()
    {
      Assert.Equal(Zone.Good, VelumDocumentParameterProbeRules.Classify(1000, null, null, 5));
    }

    [Fact]
    public void Classify_SlightlyBelowMin_IsWarn()
    {
      // Ниже min на 3 при полосе warn 5 — warn.
      Assert.Equal(Zone.Warn, VelumDocumentParameterProbeRules.Classify(7, 10, 100, 5));
    }

    [Fact]
    public void Classify_FarAboveMax_IsBad()
    {
      // Выше max на 50 при полосе warn 5 — bad.
      Assert.Equal(Zone.Bad, VelumDocumentParameterProbeRules.Classify(150, 10, 100, 5));
    }

    [Fact]
    public void ScoreValue_InsideRange_IsHundred()
    {
      Assert.Equal(100f, VelumDocumentParameterProbeRules.ScoreValue(50, 10, 100, 5));
    }

    [Fact]
    public void ScoreValue_AtWarnBoundary_IsFifty()
    {
      // Ровно на границе полосы warn (excess == band) → нижняя граница warn = 50.
      Assert.Equal(50f, VelumDocumentParameterProbeRules.ScoreValue(105, 10, 100, 5), 3);
    }

    [Fact]
    public void ScoreValue_MidWarn_IsBetweenHundredAndFifty()
    {
      float score = VelumDocumentParameterProbeRules.ScoreValue(102.5, 10, 100, 5);
      Assert.True(score > 50f && score < 100f, "score=" + score);
    }

    [Fact]
    public void ScoreValue_FarBeyondBand_IsZero()
    {
      // excess = 50, band = 5 → badRatio = 9 ≥ 1 → 0.
      Assert.Equal(0f, VelumDocumentParameterProbeRules.ScoreValue(150, 10, 100, 5), 3);
    }

    [Fact]
    public void ScoreValue_OnlyMin_BelowIsPenalized()
    {
      float score = VelumDocumentParameterProbeRules.ScoreValue(0, 10, null, 5);
      Assert.True(score < 100f, "score=" + score);
    }

    [Fact]
    public void ScoreGroup_Empty_IsHundred()
    {
      Assert.Equal(100f, VelumDocumentParameterProbeRules.ScoreGroup(new List<float>(), "all"));
      Assert.Equal(100f, VelumDocumentParameterProbeRules.ScoreGroup(null, "any"));
    }

    [Fact]
    public void ScoreGroup_AllMode_TakesMinimum()
    {
      var scores = new List<float> { 100f, 40f, 80f };
      Assert.Equal(40f, VelumDocumentParameterProbeRules.ScoreGroup(scores, "all"));
    }

    [Fact]
    public void ScoreGroup_AnyMode_TakesMaximum()
    {
      var scores = new List<float> { 10f, 40f, 80f };
      Assert.Equal(80f, VelumDocumentParameterProbeRules.ScoreGroup(scores, "any"));
    }

    [Fact]
    public void ScoreGroup_UnknownMode_DefaultsToAll()
    {
      var scores = new List<float> { 100f, 30f };
      Assert.Equal(30f, VelumDocumentParameterProbeRules.ScoreGroup(scores, "weird"));
    }
  }
}
