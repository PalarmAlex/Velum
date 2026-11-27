using System;
using Velum.ReactiveCore.DocumentTemplates;
using Xunit;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>
  /// Тесты <c>VelumDocumentContractRules</c>: разбор/формат адреса, политика записи,
  /// валидация имени уравнения, разбор уравнения, clamp, известные типы/политики.
  /// </summary>
  public class VelumDocumentContractRulesTests
  {
    [Theory]
    [InlineData("equation:PLATE_W", "equation", "PLATE_W", "")]
    [InlineData("dimension:Dim1", "dimension", "Dim1", "")]
    [InlineData("configProperty:Default/MyProp", "configProperty", "MyProp", "Default")]
    [InlineData("configProperty:MyProp", "configProperty", "MyProp", "")]
    [InlineData("property:Weight", "property", "Weight", "")]
    [InlineData("feature:Cut1", "feature", "Cut1", "")]
    public void ParseAddress_Valid_ReturnsCorrectFields(string address, string expectedType, string expectedName, string expectedContext)
    {
      var parsed = VelumDocumentContractRules.ParseAddress(address);
      Assert.Equal(expectedType, parsed.Type);
      Assert.Equal(expectedName, parsed.Name);
      Assert.Equal(expectedContext, parsed.Context);
    }

    [Theory]
    [InlineData("equation", "PLATE_W", "", "equation:PLATE_W")]
    [InlineData("dimension", "Dim1", "", "dimension:Dim1")]
    [InlineData("configProperty", "MyProp", "Default", "configProperty:Default/MyProp")]
    [InlineData("configProperty", "MyProp", "", "configProperty:MyProp")]
    public void FormatAddress_RoundTrip(string type, string name, string context, string expected)
    {
      var addr = new VelumDocumentAddress { Type = type, Name = name, Context = context };
      Assert.Equal(expected, VelumDocumentContractRules.FormatAddress(addr));
    }

    [Fact]
    public void FormatAddress_Null_ReturnsEmpty()
    {
      Assert.Equal(string.Empty, VelumDocumentContractRules.FormatAddress(null));
    }

    [Theory]
    [InlineData("equation", true)]
    [InlineData("dimension", true)]
    [InlineData("property", false)]
    [InlineData("configProperty", false)]
    [InlineData("feature", true)]
    public void IsWritableAddressType_ReturnsExpected(string type, bool expected)
    {
      Assert.Equal(expected, VelumDocumentContractRules.IsWritableAddressType(type));
    }

    [Fact]
    public void IsWritableAddressType_Empty_ReturnsFalse()
    {
      Assert.False(VelumDocumentContractRules.IsWritableAddressType(""));
    }

    [Theory]
    [InlineData("equation", "always", true)]
    [InlineData("equation", "if_empty", true)]
    [InlineData("equation", "repair", true)]
    [InlineData("equation", "never", true)]
    [InlineData("property", "never", true)]
    [InlineData("property", "always", false)]
    [InlineData("property", "if_empty", false)]
    [InlineData("property", "repair", false)]
    [InlineData("configProperty", "never", true)]
    [InlineData("configProperty", "always", false)]
    public void IsAddressPolicyCombinationValid(string addressType, string writePolicy, bool expected)
    {
      Assert.Equal(expected, VelumDocumentContractRules.IsAddressPolicyCombinationValid(addressType, writePolicy));
    }

    [Theory]
    [InlineData("PLATE_W", true)]
    [InlineData("_private", true)]
    [InlineData("A", true)]
    [InlineData("a1", true)]
    [InlineData("1num", false)]
    [InlineData("", false)]
    [InlineData("a b", false)]
    public void IsValidEquationName_ReturnsExpected(string name, bool expected)
    {
      Assert.Equal(expected, VelumDocumentContractRules.IsValidEquationName(name));
    }

    [Theory]
    [InlineData("\"PLATE_W\" = 100", "PLATE_W", "100")]
    [InlineData("\"W\" = 50.5", "W", "50.5")]
    [InlineData("\"A\" = \"text\"", "A", "text")]
    [InlineData("  \"X\" = 1  ", "X", "1")]
    public void TryParseEquation_Valid_ReturnsTrue(string raw, string expectedName, string expectedValue)
    {
      Assert.True(VelumDocumentContractRules.TryParseEquation(raw, out string name, out string valueText));
      Assert.Equal(expectedName, name);
      Assert.Equal(expectedValue, valueText);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no_eq")]
    [InlineData("\"\" = 1")]
    [InlineData("= 1")]
    public void TryParseEquation_Invalid_ReturnsFalse(string raw)
    {
      Assert.False(VelumDocumentContractRules.TryParseEquation(raw, out _, out _));
    }

    [Theory]
    [InlineData(100, 0, 200, 1, 100)]
    [InlineData(-10, 0, 200, 1, 1)]
    [InlineData(300, 0, 200, 1, 199)]
    [InlineData(100, 0, 200, 5, 100)]
    [InlineData(3, 0, 10, 5, 5)]
    [InlineData(12, 0, 10, 5, 5)]
    [InlineData(100, 0, 10, 1, 9)]
    public void ClampValue_ReturnsClamped(double value, double min, double max, double step, double expected)
    {
      Assert.Equal(expected, VelumDocumentContractRules.ClampValue(value, min, max, step));
    }

    [Theory]
    [InlineData("equation", true)]
    [InlineData("dimension", true)]
    [InlineData("property", true)]
    [InlineData("configProperty", true)]
    [InlineData("feature", true)]
    [InlineData("unknown", false)]
    [InlineData("", false)]
    public void IsKnownAddressType_ReturnsExpected(string type, bool expected)
    {
      Assert.Equal(expected, VelumDocumentContractRules.IsKnownAddressType(type));
    }

    [Theory]
    [InlineData("always", true)]
    [InlineData("if_empty", true)]
    [InlineData("repair", true)]
    [InlineData("never", true)]
    [InlineData("write", false)]
    [InlineData("", false)]
    public void IsKnownWritePolicy_ReturnsExpected(string policy, bool expected)
    {
      Assert.Equal(expected, VelumDocumentContractRules.IsKnownWritePolicy(policy));
    }

    [Theory]
    [InlineData("skip", true)]
    [InlineData("default", true)]
    [InlineData("clamp", true)]
    [InlineData("ignore", false)]
    [InlineData("", false)]
    public void IsKnownOnInvalid_ReturnsExpected(string onInvalid, bool expected)
    {
      Assert.Equal(expected, VelumDocumentContractRules.IsKnownOnInvalid(onInvalid));
    }
  }
}