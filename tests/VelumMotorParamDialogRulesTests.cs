using System.Collections.Generic;
using Velum.ReactiveCore;
using Xunit;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>
  /// Тесты чистых правил диалога подтверждения параметров мотор-действия: разбор флага
  /// confirm, гейт по источнику активации, валидация/нормализация значений и слияние
  /// правок оператора с базовыми аргументами шага.
  /// </summary>
  public class VelumMotorParamDialogRulesTests
  {
    [Theory]
    [InlineData("always", VelumMotorParamDialogRules.ConfirmMode.Always)]
    [InlineData("ДА", VelumMotorParamDialogRules.ConfirmMode.Always)]
    [InlineData("true", VelumMotorParamDialogRules.ConfirmMode.Always)]
    [InlineData("1", VelumMotorParamDialogRules.ConfirmMode.Always)]
    [InlineData("yes", VelumMotorParamDialogRules.ConfirmMode.Always)]
    [InlineData("auto", VelumMotorParamDialogRules.ConfirmMode.Auto)]
    [InlineData("  AUTO  ", VelumMotorParamDialogRules.ConfirmMode.Auto)]
    [InlineData("never", VelumMotorParamDialogRules.ConfirmMode.Never)]
    [InlineData("", VelumMotorParamDialogRules.ConfirmMode.Never)]
    [InlineData(null, VelumMotorParamDialogRules.ConfirmMode.Never)]
    [InlineData("что-то", VelumMotorParamDialogRules.ConfirmMode.Never)]
    public void ParseConfirmMode_MapsKnownValues(string raw, VelumMotorParamDialogRules.ConfirmMode expected)
    {
      Assert.Equal(expected, VelumMotorParamDialogRules.ParseConfirmMode(raw));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(VelumMotorParamDialogRules.ActivationSourceCode.GeneticReflex, true)]
    [InlineData(VelumMotorParamDialogRules.ActivationSourceCode.ConditionedReflex, false)]
    [InlineData(VelumMotorParamDialogRules.ActivationSourceCode.Automatizm, false)]
    [InlineData(VelumMotorParamDialogRules.ActivationSourceCode.AutomatizmVerbalResponse, false)]
    [InlineData(VelumMotorParamDialogRules.ActivationSourceCode.AutomatizmEchoCommentary, false)]
    public void IsManualSource_OnlyGeneticOrZero(int source, bool expected)
    {
      Assert.Equal(expected, VelumMotorParamDialogRules.IsManualSource(source));
    }

    [Fact]
    public void ShouldAskDialog_NeverMode_NeverAsks()
    {
      Assert.False(VelumMotorParamDialogRules.ShouldAskDialog(
          VelumMotorParamDialogRules.ConfirmMode.Never,
          VelumMotorParamDialogRules.ActivationSourceCode.GeneticReflex));
    }

    [Fact]
    public void ShouldAskDialog_ManualSourceAndConfirm_Ask()
    {
      Assert.True(VelumMotorParamDialogRules.ShouldAskDialog(
          VelumMotorParamDialogRules.ConfirmMode.Auto,
          VelumMotorParamDialogRules.ActivationSourceCode.GeneticReflex));
      Assert.True(VelumMotorParamDialogRules.ShouldAskDialog(
          VelumMotorParamDialogRules.ConfirmMode.Always,
          0));
    }

    [Fact]
    public void ShouldAskDialog_AutonomousPsychicSource_DoesNotAsk()
    {
      Assert.False(VelumMotorParamDialogRules.ShouldAskDialog(
          VelumMotorParamDialogRules.ConfirmMode.Always,
          VelumMotorParamDialogRules.ActivationSourceCode.Automatizm));
      Assert.False(VelumMotorParamDialogRules.ShouldAskDialog(
          VelumMotorParamDialogRules.ConfirmMode.Auto,
          VelumMotorParamDialogRules.ActivationSourceCode.AutomatizmEchoCommentary));
    }

    [Fact]
    public void TryValidateValue_Number_NormalizesInvariant()
    {
      Assert.True(VelumMotorParamDialogRules.TryValidateValue(
          "number", " 3.50 ", false, out string normalized, out _));
      Assert.Equal("3.5", normalized);
    }

    [Fact]
    public void TryValidateValue_Number_InvalidFails()
    {
      Assert.False(VelumMotorParamDialogRules.TryValidateValue(
          "number", "abc", false, out _, out string error));
      Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void TryValidateValue_Int_TrimsAndKeepsInteger()
    {
      Assert.True(VelumMotorParamDialogRules.TryValidateValue(
          "int", " 42 ", false, out string normalized, out _));
      Assert.Equal("42", normalized);
    }

    [Theory]
    [InlineData("да", true)]
    [InlineData("true", true)]
    [InlineData("0", true)]
    [InlineData("maybe", false)]
    public void TryValidateValue_Bool_AcceptsKnownTokens(string raw, bool ok)
    {
      Assert.Equal(ok, VelumMotorParamDialogRules.TryValidateValue("bool", raw, false, out _, out _));
    }

    [Fact]
    public void TryValidateValue_RequiredEmpty_Fails()
    {
      Assert.False(VelumMotorParamDialogRules.TryValidateValue(
          "text", "   ", true, out _, out string error));
      Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void TryValidateValue_OptionalEmpty_ReturnsEmptyAndOk()
    {
      Assert.True(VelumMotorParamDialogRules.TryValidateValue(
          "number", "", false, out string normalized, out _));
      Assert.Equal(string.Empty, normalized);
    }

    [Fact]
    public void TryValidateValue_TextEnumDate_AcceptsAsIs()
    {
      Assert.True(VelumMotorParamDialogRules.TryValidateValue("text", "любой текст", false, out string t, out _));
      Assert.Equal("любой текст", t);
      Assert.True(VelumMotorParamDialogRules.TryValidateValue("enum", "alpha", false, out _, out _));
      Assert.True(VelumMotorParamDialogRules.TryValidateValue("date", "2026-11-24", false, out _, out _));
    }

    [Fact]
    public void MergeOverrides_DialogValueOverlaysBase()
    {
      var baseArgs = new Dictionary<string, string> { { "width", "10" }, { "height", "20" } };
      var dialog = new Dictionary<string, string> { { "width", "15" } };

      Dictionary<string, string> merged = VelumMotorParamDialogRules.MergeOverrides(baseArgs, dialog);

      Assert.Equal("15", merged["width"]);
      Assert.Equal("20", merged["height"]);
    }

    [Fact]
    public void MergeOverrides_EmptyDialogValue_RemovesKey()
    {
      var baseArgs = new Dictionary<string, string> { { "width", "10" }, { "note", "x" } };
      var dialog = new Dictionary<string, string> { { "note", "" } };

      Dictionary<string, string> merged = VelumMotorParamDialogRules.MergeOverrides(baseArgs, dialog);

      Assert.True(merged.ContainsKey("width"));
      Assert.False(merged.ContainsKey("note"));
    }

    [Fact]
    public void MergeOverrides_NullInputs_ProduceEmptyOrBase()
    {
      Assert.Empty(VelumMotorParamDialogRules.MergeOverrides(null, null));

      var baseArgs = new Dictionary<string, string> { { "a", "1" } };
      Assert.Equal("1", VelumMotorParamDialogRules.MergeOverrides(baseArgs, null)["a"]);
    }

    [Fact]
    public void MergeOverrides_KeysCaseInsensitive()
    {
      var baseArgs = new Dictionary<string, string> { { "Width", "10" } };
      var dialog = new Dictionary<string, string> { { "width", "15" } };

      Dictionary<string, string> merged = VelumMotorParamDialogRules.MergeOverrides(baseArgs, dialog);

      Assert.Single(merged);
      Assert.Equal("15", merged["WIDTH"]);
    }

    [Fact]
    public void Field_DefaultsFillEmptyLabelAndType()
    {
      var field = new VelumMotorParamField("width", null, null, null, false, null);

      Assert.Equal("width", field.Key);
      Assert.Equal("width", field.Label);
      Assert.Equal("text", field.Type);
      Assert.Equal(string.Empty, field.InitialValue);
      Assert.Empty(field.EnumValues);
    }

    [Fact]
    public void Field_KeepsProvidedValues()
    {
      var field = new VelumMotorParamField(
          "kind", "Тип", "enum", "a", true, new[] { "a", "b" });

      Assert.Equal("Тип", field.Label);
      Assert.Equal("enum", field.Type);
      Assert.Equal("a", field.InitialValue);
      Assert.True(field.Required);
      Assert.Equal(2, field.EnumValues.Count);
    }
  }
}
