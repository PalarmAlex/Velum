using System.Collections.Generic;
using Velum.ReactiveCore.DocumentTemplates;
using Xunit;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>
  /// Тесты правил распознавания документа по отпечатку: гейт по типу и числу
  /// конфигураций, расчёт confidence, выбор кандидата и неоднозначность.
  /// </summary>
  public class VelumDocumentFingerprintRulesTests
  {
    private static VelumDocumentFingerprintRules.DocumentFacts Facts(
        string type = "part",
        int configs = 1,
        IEnumerable<string> equations = null,
        IEnumerable<string> properties = null,
        IEnumerable<string> features = null)
    {
      return new VelumDocumentFingerprintRules.DocumentFacts
      {
        DocumentType = type,
        ConfigurationCount = configs,
        Equations = new HashSet<string>(equations ?? new string[0], System.StringComparer.OrdinalIgnoreCase),
        Properties = new HashSet<string>(properties ?? new string[0], System.StringComparer.OrdinalIgnoreCase),
        Features = new HashSet<string>(features ?? new string[0], System.StringComparer.OrdinalIgnoreCase)
      };
    }

    private static VelumDocumentFingerprintRules.TemplateFingerprint Template(
        string id = "plate",
        string type = "part",
        int? configMin = null,
        IEnumerable<string> equations = null,
        IEnumerable<string> properties = null,
        IEnumerable<string> features = null)
    {
      return new VelumDocumentFingerprintRules.TemplateFingerprint
      {
        TemplateId = id,
        DocumentType = type,
        ConfigCountMin = configMin,
        RequiredEquations = new List<string>(equations ?? new string[0]),
        RequiredProperties = new List<string>(properties ?? new string[0]),
        RequiredFeatures = new List<string>(features ?? new string[0])
      };
    }

    [Fact]
    public void Match_NullArgs_ReturnsEmptyResult()
    {
      var result = VelumDocumentFingerprintRules.Match(null, null, null);
      Assert.False(result.PassedGate);
      Assert.Equal(0d, result.Confidence);
    }

    [Fact]
    public void Match_TypeMismatch_FailsGate()
    {
      var result = VelumDocumentFingerprintRules.Match(
          Template(type: "asm"),
          Facts(type: "part"),
          new VelumDocumentFingerprintRules.Rules());

      Assert.False(result.PassedGate);
      Assert.Equal(0d, result.Confidence);
    }

    [Fact]
    public void Match_ConfigCountBelowMin_FailsGate()
    {
      var result = VelumDocumentFingerprintRules.Match(
          Template(configMin: 3),
          Facts(configs: 2),
          new VelumDocumentFingerprintRules.Rules());

      Assert.False(result.PassedGate);
    }

    [Fact]
    public void Match_ConfigCountMinZero_NotChecked()
    {
      var result = VelumDocumentFingerprintRules.Match(
          Template(configMin: 0),
          Facts(configs: 0),
          new VelumDocumentFingerprintRules.Rules());

      Assert.True(result.PassedGate);
    }

    [Fact]
    public void Match_AllRatiosOne_MaxConfidence()
    {
      var result = VelumDocumentFingerprintRules.Match(
          Template(equations: new[] { "W", "H" }, properties: new[] { "Weight" }, features: new[] { "Cut1" }),
          Facts(equations: new[] { "W", "H" }, properties: new[] { "Weight" }, features: new[] { "Cut1" }),
          new VelumDocumentFingerprintRules.Rules());

      Assert.True(result.PassedGate);
      Assert.Equal(1d, result.EquationRatio);
      Assert.Equal(1d, result.PropertyRatio);
      Assert.Equal(1d, result.FeatureRatio);
      Assert.Equal(1d, result.Confidence);
    }

    [Fact]
    public void Match_PartialEquations_ComputesRatio()
    {
      var result = VelumDocumentFingerprintRules.Match(
          Template(equations: new[] { "W", "H" }),
          Facts(equations: new[] { "W" }),
          new VelumDocumentFingerprintRules.Rules());

      Assert.Equal(0.5d, result.EquationRatio);
      // 0.5*0.5 + 0.25*1 + 0.15*1 + 0.10 = 0.75
      Assert.Equal(0.75d, result.Confidence, 6);
    }

    [Fact]
    public void Match_EmptyRequirements_TreatedAsFullMatch()
    {
      var result = VelumDocumentFingerprintRules.Match(
          Template(),
          Facts(),
          new VelumDocumentFingerprintRules.Rules());

      Assert.Equal(1d, result.EquationRatio);
      Assert.Equal(1d, result.Confidence);
    }

    [Fact]
    public void Match_EmptyActual_FullMismatch()
    {
      var result = VelumDocumentFingerprintRules.Match(
          Template(equations: new[] { "W" }),
          Facts(),
          new VelumDocumentFingerprintRules.Rules());

      Assert.Equal(0d, result.EquationRatio);
    }

    [Fact]
    public void Match_CaseInsensitiveNames()
    {
      var result = VelumDocumentFingerprintRules.Match(
          Template(equations: new[] { "plate_w" }),
          Facts(equations: new[] { "PLATE_W" }),
          new VelumDocumentFingerprintRules.Rules());

      Assert.Equal(1d, result.EquationRatio);
    }

    [Fact]
    public void PickCandidate_NoMatches_ReturnsNull()
    {
      Assert.Null(VelumDocumentFingerprintRules.PickCandidate(
          new List<VelumDocumentFingerprintRules.MatchResult>(),
          new VelumDocumentFingerprintRules.Rules()));
      Assert.Null(VelumDocumentFingerprintRules.PickCandidate(null, null));
    }

    [Fact]
    public void PickCandidate_SingleAboveThreshold_ReturnsIt()
    {
      var matches = new List<VelumDocumentFingerprintRules.MatchResult>
      {
        new VelumDocumentFingerprintRules.MatchResult
        {
          TemplateId = "plate",
          PassedGate = true,
          Confidence = 0.9
        }
      };

      Assert.Equal("plate", VelumDocumentFingerprintRules.PickCandidate(
          matches, new VelumDocumentFingerprintRules.Rules()));
    }

    [Fact]
    public void PickCandidate_BelowThreshold_ReturnsNull()
    {
      var matches = new List<VelumDocumentFingerprintRules.MatchResult>
      {
        new VelumDocumentFingerprintRules.MatchResult
        {
          TemplateId = "plate",
          PassedGate = true,
          Confidence = 0.7
        }
      };

      Assert.Null(VelumDocumentFingerprintRules.PickCandidate(
          matches, new VelumDocumentFingerprintRules.Rules()));
    }

    [Fact]
    public void PickCandidate_GateFailed_Ignored()
    {
      var matches = new List<VelumDocumentFingerprintRules.MatchResult>
      {
        new VelumDocumentFingerprintRules.MatchResult
        {
          TemplateId = "plate",
          PassedGate = false,
          Confidence = 0.95
        }
      };

      Assert.Null(VelumDocumentFingerprintRules.PickCandidate(
          matches, new VelumDocumentFingerprintRules.Rules()));
    }

    [Fact]
    public void PickCandidate_TwoCandidatesAmbiguous_ReturnsNull()
    {
      var matches = new List<VelumDocumentFingerprintRules.MatchResult>
      {
        new VelumDocumentFingerprintRules.MatchResult { TemplateId = "a", PassedGate = true, Confidence = 0.90 },
        new VelumDocumentFingerprintRules.MatchResult { TemplateId = "b", PassedGate = true, Confidence = 0.88 }
      };

      Assert.Null(VelumDocumentFingerprintRules.PickCandidate(
          matches, new VelumDocumentFingerprintRules.Rules()));
    }

    [Fact]
    public void PickCandidate_TwoCandidatesClearWinner_ReturnsFirst()
    {
      var matches = new List<VelumDocumentFingerprintRules.MatchResult>
      {
        new VelumDocumentFingerprintRules.MatchResult { TemplateId = "b", PassedGate = true, Confidence = 0.85 },
        new VelumDocumentFingerprintRules.MatchResult { TemplateId = "a", PassedGate = true, Confidence = 0.95 }
      };

      Assert.Equal("a", VelumDocumentFingerprintRules.PickCandidate(
          matches, new VelumDocumentFingerprintRules.Rules()));
    }

    [Fact]
    public void PickCandidate_CustomRules_Respected()
    {
      var rules = new VelumDocumentFingerprintRules.Rules { MinConfidence = 0.5, AmbiguityDelta = 0.01 };
      var matches = new List<VelumDocumentFingerprintRules.MatchResult>
      {
        new VelumDocumentFingerprintRules.MatchResult { TemplateId = "a", PassedGate = true, Confidence = 0.6 }
      };

      Assert.Equal("a", VelumDocumentFingerprintRules.PickCandidate(matches, rules));
    }
  }
}
