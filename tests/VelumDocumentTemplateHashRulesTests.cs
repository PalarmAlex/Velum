using System.Collections.Generic;
using Velum.ReactiveCore.DocumentTemplates;
using Xunit;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>
  /// Тесты <c>ContractHash</c>: хэш покрывает только структуру контракта
  /// (id/type/unit/address), порядок параметров не влияет на результат.
  /// </summary>
  public class VelumDocumentTemplateHashRulesTests
  {
    private static VelumDocumentParameterDescriptor Param(string id, string type, string unit, string address)
    {
      return new VelumDocumentParameterDescriptor
      {
        Id = id,
        Type = type,
        Unit = unit,
        Address = address
      };
    }

    [Fact]
    public void ComputeContractHash_EmptyTemplateId_ReturnsEmpty()
    {
      Assert.Equal(string.Empty, VelumDocumentTemplateHashRules.ComputeContractHash(
          null, "1", new List<VelumDocumentParameterDescriptor>()));
    }

    [Fact]
    public void ComputeContractHash_Returns16HexChars()
    {
      string hash = VelumDocumentTemplateHashRules.ComputeContractHash(
          "plate", "1",
          new List<VelumDocumentParameterDescriptor> { Param("W", "number", "mm", "equation:PLATE_W") });

      Assert.Equal(VelumDocumentTemplateHashRules.ContractHashLength, hash.Length);
      Assert.Matches("^[0-9a-f]+$", hash);
    }

    [Fact]
    public void ComputeContractHash_ParameterOrderIrrelevant()
    {
      var a = new List<VelumDocumentParameterDescriptor>
      {
        Param("W", "number", "mm", "equation:PLATE_W"),
        Param("H", "number", "mm", "equation:PLATE_H")
      };
      var b = new List<VelumDocumentParameterDescriptor>
      {
        Param("H", "number", "mm", "equation:PLATE_H"),
        Param("W", "number", "mm", "equation:PLATE_W")
      };

      Assert.Equal(
          VelumDocumentTemplateHashRules.ComputeContractHash("plate", "1", a),
          VelumDocumentTemplateHashRules.ComputeContractHash("plate", "1", b));
    }

    [Fact]
    public void ComputeContractHash_AddressChange_ChangesHash()
    {
      string before = VelumDocumentTemplateHashRules.ComputeContractHash(
          "plate", "1",
          new List<VelumDocumentParameterDescriptor> { Param("W", "number", "mm", "equation:PLATE_W") });

      string after = VelumDocumentTemplateHashRules.ComputeContractHash(
          "plate", "1",
          new List<VelumDocumentParameterDescriptor> { Param("W", "number", "mm", "equation:OTHER_W") });

      Assert.NotEqual(before, after);
    }

    [Fact]
    public void ComputeContractHash_TypeCaseInsensitive()
    {
      string lower = VelumDocumentTemplateHashRules.ComputeContractHash(
          "plate", "1",
          new List<VelumDocumentParameterDescriptor> { Param("W", "number", "mm", "equation:PLATE_W") });

      string upper = VelumDocumentTemplateHashRules.ComputeContractHash(
          "plate", "1",
          new List<VelumDocumentParameterDescriptor> { Param("W", "NUMBER", "mm", "equation:PLATE_W") });

      Assert.Equal(lower, upper);
    }

    [Fact]
    public void BuildCanonicalString_StableFormat()
    {
      string canonical = VelumDocumentTemplateHashRules.BuildCanonicalString(
          " plate ", "1",
          new List<VelumDocumentParameterDescriptor> { Param("W", "Number", "mm", "equation:PLATE_W") });

      Assert.Equal("plate|1|W:number:mm:equation:PLATE_W", canonical);
    }

    [Fact]
    public void BuildCanonicalString_NullParameters_KeepsHeader()
    {
      Assert.Equal("plate|1|", VelumDocumentTemplateHashRules.BuildCanonicalString("plate", "1", null));
    }

    [Fact]
    public void Sha256HexPrefix_ShortLength_ReturnsPrefix()
    {
      string hash = VelumDocumentTemplateHashRules.Sha256HexPrefix("abc", 8);
      Assert.Equal(8, hash.Length);
      Assert.Equal(hash, VelumDocumentTemplateHashRules.Sha256HexPrefix("abc", 8));
    }

    [Fact]
    public void Sha256HexPrefix_EmptyInput_ReturnsEmpty()
    {
      Assert.Equal(string.Empty, VelumDocumentTemplateHashRules.Sha256HexPrefix(null, 8));
      Assert.Equal(string.Empty, VelumDocumentTemplateHashRules.Sha256HexPrefix("abc", 0));
    }
  }
}
