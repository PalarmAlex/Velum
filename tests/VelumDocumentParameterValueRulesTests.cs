using System.Collections.Generic;
using Velum.ReactiveCore.DocumentTemplates;
using Xunit;
using ValueOutcome = Velum.ReactiveCore.DocumentTemplates.VelumDocumentParameterValueRules.ValueOutcome;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>
  /// Тесты <c>VelumDocumentParameterValueRules</c>: валидация number/int/bool/enum,
  /// onInvalid (skip/default/clamp), step mismatch.
  /// </summary>
  public class VelumDocumentParameterValueRulesTests
  {
    private static VelumDocumentParameterRuleDescriptor NumberParam(
        double? min = null, double? max = null, double? step = null, object defaultValue = null, string onInvalid = "skip")
    {
      return new VelumDocumentParameterRuleDescriptor
      {
        Id = "W",
        Type = "number",
        Min = min,
        Max = max,
        Step = step,
        Default = defaultValue,
        OnInvalid = onInvalid
      };
    }

    private static VelumDocumentParameterRuleDescriptor IntParam(
        double? min = null, double? max = null, double? step = null, object defaultValue = null, string onInvalid = "skip")
    {
      return new VelumDocumentParameterRuleDescriptor
      {
        Id = "N",
        Type = "int",
        Min = min,
        Max = max,
        Step = step,
        Default = defaultValue,
        OnInvalid = onInvalid
      };
    }

    [Theory]
    [InlineData("100", 100d)]
    [InlineData("50.5", 50.5)]
    [InlineData("-10", -10d)]
    public void ValidateNumber_Valid_ReturnsValid(string input, double expected)
    {
      var param = NumberParam();
      var result = VelumDocumentParameterValueRules.Validate(param, input);

      Assert.Equal(ValueOutcome.Valid, result.Outcome);
      Assert.Equal(expected, result.Value);
      Assert.Equal(string.Empty, result.Reason);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData(null)]
    public void ValidateNumber_InvalidNumber_ReturnsSkip(string input)
    {
      var result = VelumDocumentParameterValueRules.Validate(NumberParam(), input);
      Assert.Equal(ValueOutcome.Skip, result.Outcome);
      Assert.Equal("not_a_number", result.Reason);
      Assert.Null(result.Value);
    }

    [Theory]
    [InlineData("100", 100L)]
    [InlineData("50", 50L)]
    [InlineData("-10", -10L)]
    public void ValidateInt_Valid_ReturnsValid(string input, long expected)
    {
      var param = IntParam();
      var result = VelumDocumentParameterValueRules.Validate(param, input);

      Assert.Equal(ValueOutcome.Valid, result.Outcome);
      Assert.Equal(expected, result.Value);
    }

    [Theory]
    [InlineData("1.5")]
    [InlineData("3.14")]
    public void ValidateInt_Fractional_ReturnsSkip(string input)
    {
      var result = VelumDocumentParameterValueRules.Validate(IntParam(), input);
      Assert.Equal(ValueOutcome.Skip, result.Outcome);
      Assert.Equal("not_an_integer", result.Reason);
    }

    [Theory]
    [InlineData("100", 0, 200, "Valid")]
    [InlineData("0", 0, 200, "Valid")]
    [InlineData("200", 0, 200, "Valid")]
    [InlineData("-1", 0, 200, "Skip")]
    [InlineData("201", 0, 200, "Skip")]
    public void ValidateNumber_Range(string input, double min, double max, string expectedName)
    {
      var result = VelumDocumentParameterValueRules.Validate(NumberParam(min, max), input);
      Assert.Equal(expectedName, result.Outcome.ToString());
    }

    [Theory]
    [InlineData("100", 0, 200, 10, "Valid")]
    [InlineData("105", 0, 200, 10, "Skip")]
    [InlineData("103", 0, 200, 10, "Skip")]
    [InlineData("100.5", 0, 200, 10, "Skip")]
    public void ValidateNumber_Step(string input, double min, double max, double step, string expectedName)
    {
      var result = VelumDocumentParameterValueRules.Validate(NumberParam(min, max, step), input);
      Assert.Equal(expectedName, result.Outcome.ToString());
    }

    [Fact]
    public void ValidateNumber_DefaultOnInvalid_Repaired()
    {
      var param = NumberParam(0, 200, 1, 50d, "default");
      var result = VelumDocumentParameterValueRules.Validate(param, "abc");

      Assert.Equal(ValueOutcome.Repaired, result.Outcome);
      Assert.Equal(50d, result.Value);
    }

    [Fact]
    public void ValidateNumber_ClampOnInvalid_Repaired()
    {
      var param = NumberParam(0, 200, 1, null, "clamp");
      var result = VelumDocumentParameterValueRules.Validate(param, "300");

      Assert.Equal(ValueOutcome.Repaired, result.Outcome);
      Assert.Equal(199d, result.Value);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("True", true)]
    [InlineData("FALSE", false)]
    public void ValidateBool_Valid_ReturnsValid(string input, bool expected)
    {
      var param = new VelumDocumentParameterRuleDescriptor { Id = "flag", Type = "bool" };
      var result = VelumDocumentParameterValueRules.Validate(param, input);

      Assert.Equal(ValueOutcome.Valid, result.Outcome);
      Assert.Equal(expected, result.Value);
    }

    [Theory]
    [InlineData("yes")]
    [InlineData("2")]
    [InlineData("no")]
    [InlineData("")]
    public void ValidateBool_Invalid_ReturnsSkip(string input)
    {
      var param = new VelumDocumentParameterRuleDescriptor { Id = "flag", Type = "bool" };
      var result = VelumDocumentParameterValueRules.Validate(param, input);
      Assert.Equal(ValueOutcome.Skip, result.Outcome);
      Assert.Equal("not_a_bool", result.Reason);
    }

    [Theory]
    [InlineData("A", true)]
    [InlineData("B", true)]
    [InlineData("C", true)]
    [InlineData("X", false)]
    [InlineData("a", false)]
    public void ValidateEnum_Valid_ReturnsExpected(string input, bool shouldPass)
    {
      var param = new VelumDocumentParameterRuleDescriptor
      {
        Id = "mode",
        Type = "enum",
        Values = new List<string> { "A", "B", "C" }
      };
      var result = VelumDocumentParameterValueRules.Validate(param, input);

      Assert.Equal(shouldPass, result.Outcome == ValueOutcome.Valid);
      if (!shouldPass)
        Assert.Equal("not_in_enum", result.Reason);
    }

    [Fact]
    public void ValidateText_AcceptAnyValue()
    {
      var param = new VelumDocumentParameterRuleDescriptor { Id = "note", Type = "text" };
      var result = VelumDocumentParameterValueRules.Validate(param, "любые данные 123!");
      Assert.Equal(ValueOutcome.Valid, result.Outcome);
    }

    [Fact]
    public void ValidateNullParameter_ReturnsSkip()
    {
      var result = VelumDocumentParameterValueRules.Validate(null, "100");
      Assert.Equal(ValueOutcome.Skip, result.Outcome);
      Assert.Equal("parameter_missing", result.Reason);
    }

    [Fact]
    public void ValidateNumber_ClampWithStep()
    {
      var param = NumberParam(0, 200, 5, null, "clamp");
      var result = VelumDocumentParameterValueRules.Validate(param, "503");
      // clamp в [5, 195]
      Assert.Equal(195d, result.Value);
      Assert.Equal(ValueOutcome.Repaired, result.Outcome);
    }
  }
}
