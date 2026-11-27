using System.Collections.Generic;
using ISIDA.SymbiontEnv.Contract;
using Velum.ReactiveCore.DocumentTemplates;
using Xunit;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>
  /// Тесты распознавания активного документа среди библиотеки шаблонов:
  /// уверенное совпадение, unknown, «вероятно X» и выбор наиболее близкого.
  /// </summary>
  public class VelumDocumentSnapshotRulesTests
  {
    private static VelumDocumentFingerprintRules.DocumentFacts Facts(
        string type = "part",
        int configs = 1,
        IEnumerable<string> equations = null,
        IEnumerable<string> properties = null)
    {
      return new VelumDocumentFingerprintRules.DocumentFacts
      {
        DocumentType = type,
        ConfigurationCount = configs,
        Equations = new HashSet<string>(equations ?? new string[0], System.StringComparer.OrdinalIgnoreCase),
        Properties = new HashSet<string>(properties ?? new string[0], System.StringComparer.OrdinalIgnoreCase),
        Features = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
      };
    }

    private static EnvironmentDocumentTemplateData Template(
        string id,
        string type = "part",
        IEnumerable<string> equations = null,
        IEnumerable<string> properties = null)
    {
      return new EnvironmentDocumentTemplateData
      {
        TemplateId = id,
        DocumentType = type,
        Fingerprint = new EnvironmentDocumentFingerprintData
        {
          RequiredEquations = new List<string>(equations ?? new string[0]).ToArray(),
          RequiredProperties = new List<string>(properties ?? new string[0]).ToArray()
        }
      };
    }

    [Fact]
    public void Recognize_EmptyLibrary_IsUnknown()
    {
      var result = VelumDocumentSnapshotRules.Recognize(
          new List<EnvironmentDocumentTemplateData>(),
          Facts(),
          new VelumDocumentFingerprintRules.Rules());

      Assert.False(result.Recognized);
      Assert.Null(result.TemplateId);
    }

    [Fact]
    public void Recognize_FullMatch_Recognized()
    {
      var templates = new List<EnvironmentDocumentTemplateData>
      {
        Template("plate", equations: new[] { "L", "W", "T" }, properties: new[] { "Velum.TemplateId" })
      };

      var facts = Facts(
          equations: new[] { "L", "W", "T" },
          properties: new[] { "Velum.TemplateId" });

      var result = VelumDocumentSnapshotRules.Recognize(templates, facts, null);

      Assert.True(result.Recognized);
      Assert.Equal("plate", result.TemplateId);
      Assert.Equal(1d, result.Confidence, 3);
    }

    [Fact]
    public void Recognize_ForeignDoc_IsUnknown()
    {
      var templates = new List<EnvironmentDocumentTemplateData>
      {
        Template("plate", equations: new[] { "L", "W", "T" })
      };

      var facts = Facts(equations: new[] { "D1", "D2" });

      var result = VelumDocumentSnapshotRules.Recognize(templates, facts, null);
      Assert.False(result.Recognized);
    }

    [Fact]
    public void FindBestGuess_WhenUnknown_ReturnsClosestPassedGate()
    {
      var templates = new List<EnvironmentDocumentTemplateData>
      {
        Template("plate", equations: new[] { "L", "W", "T" }, properties: new[] { "Velum.TemplateId" })
      };

      // 2 уравнения из 3 + отсутствие обязательного свойства → confidence ниже порога 0.8,
      // но гейт (тип part, конфиги) пройден → «вероятно plate».
      var facts = Facts(equations: new[] { "L", "W" });
      var result = VelumDocumentSnapshotRules.Recognize(templates, facts, null);

      Assert.False(result.Recognized);
      VelumDocumentFingerprintRules.MatchResult guess = VelumDocumentSnapshotRules.FindBestGuess(result);
      Assert.NotNull(guess);
      Assert.Equal("plate", guess.TemplateId);
    }

    [Fact]
    public void FindBestGuess_TypeMismatch_NotGuessed()
    {
      var templates = new List<EnvironmentDocumentTemplateData>
      {
        Template("plate", type: "asm", equations: new[] { "L" })
      };

      var facts = Facts(type: "part", equations: new[] { "L" });
      var result = VelumDocumentSnapshotRules.Recognize(templates, facts, null);

      Assert.Null(VelumDocumentSnapshotRules.FindBestGuess(result));
    }
  }
}
