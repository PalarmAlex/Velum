using Velum.ReactiveCore.DocumentTemplates;
using Xunit;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>
  /// Тесты префиксных предикатов и разбора ключей проб восприятия документа.
  /// </summary>
  public class VelumDocumentProbeKeysTests
  {
    [Fact]
    public void FormatParameterProbeKey_BuildsPrefixedKey()
    {
      Assert.Equal(
          "Velum.Solid.Parameter.plate.standard.L",
          VelumDocumentProbeKeys.FormatParameterProbeKey("plate.standard", "L"));
    }

    [Fact]
    public void FormatParameterProbeKey_EmptyPart_ReturnsEmpty()
    {
      Assert.Equal(string.Empty, VelumDocumentProbeKeys.FormatParameterProbeKey("plate", ""));
      Assert.Equal(string.Empty, VelumDocumentProbeKeys.FormatParameterProbeKey(null, "L"));
    }

    [Fact]
    public void IsParameterProbeKey_MatchesPrefixOnly()
    {
      Assert.True(VelumDocumentProbeKeys.IsParameterProbeKey("Velum.Solid.Parameter.plate.L"));
      Assert.False(VelumDocumentProbeKeys.IsParameterProbeKey("Velum.Solid.Property.plate.L"));
      Assert.False(VelumDocumentProbeKeys.IsParameterProbeKey("Velum.Solid.Parameter."));
      Assert.False(VelumDocumentProbeKeys.IsParameterProbeKey(null));
    }

    [Fact]
    public void IsPropertyProbeKey_MatchesPrefix()
    {
      Assert.True(VelumDocumentProbeKeys.IsPropertyProbeKey("Velum.Solid.Property.plate.Marker"));
      Assert.False(VelumDocumentProbeKeys.IsPropertyProbeKey("Velum.Solid.Parameter.plate.L"));
    }

    [Fact]
    public void IsDocumentDataProbeKey_CoversBothPrefixes()
    {
      Assert.True(VelumDocumentProbeKeys.IsDocumentDataProbeKey("Velum.Solid.Parameter.t.p"));
      Assert.True(VelumDocumentProbeKeys.IsDocumentDataProbeKey("Velum.Solid.Property.t.p"));
      Assert.False(VelumDocumentProbeKeys.IsDocumentDataProbeKey("Velum.Solid.Material"));
    }

    [Fact]
    public void TryParseParameterProbeKey_SplitsTemplateAndParam()
    {
      Assert.True(VelumDocumentProbeKeys.TryParseParameterProbeKey(
          "Velum.Solid.Parameter.plate.standard.L", out string templateId, out string paramId));
      Assert.Equal("plate.standard", templateId);
      Assert.Equal("L", paramId);
    }

    [Fact]
    public void TryParseParameterProbeKey_DottedTemplateId_SplitsAtLastDot()
    {
      // Идентификатор параметра не содержит точек, шаблон может — граница на последней точке.
      Assert.True(VelumDocumentProbeKeys.TryParseParameterProbeKey(
          "Velum.Solid.Parameter.plate.standard.L", out string templateId, out string paramId));
      Assert.Equal("plate.standard", templateId);
      Assert.Equal("L", paramId);
    }

    [Fact]
    public void TryParseParameterProbeKey_MissingParam_Fails()
    {
      Assert.False(VelumDocumentProbeKeys.TryParseParameterProbeKey(
          "Velum.Solid.Parameter.onlytemplate", out _, out _));
    }

    [Fact]
    public void TryParsePropertyProbeKey_SplitsCorrectly()
    {
      Assert.True(VelumDocumentProbeKeys.TryParsePropertyProbeKey(
          "Velum.Solid.Property.plate.Marker", out string templateId, out string paramId));
      Assert.Equal("plate", templateId);
      Assert.Equal("Marker", paramId);
    }
  }
}
