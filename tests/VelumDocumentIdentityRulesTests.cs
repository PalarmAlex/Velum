using System;
using Velum.ReactiveCore.DocumentTemplates;
using Xunit;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>
  /// Тесты pure-правил идентичности документа SolidWorks (<c>doc.key</c>):
  /// тип по расширению, нормализация пути, ключ сохранённого/несохранённого документа.
  /// </summary>
  public class VelumDocumentIdentityRulesTests
  {
    [Theory]
    [InlineData("C:\\work\\plate.sldprt", "part")]
    [InlineData("C:\\work\\plate.SLDPRT", "part")]
    [InlineData("C:\\work\\unit.sldasm", "asm")]
    [InlineData("C:\\work\\unit.slddrw", "drw")]
    [InlineData(".sldprt", "part")]
    [InlineData(".SLDASM", "asm")]
    public void ResolveDocType_KnownExtensions_ReturnsType(string path, string expected)
    {
      Assert.Equal(expected, VelumDocumentIdentityRules.ResolveDocType(path));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("C:\\work\\plate.txt")]
    [InlineData("C:\\work\\plate")]
    public void ResolveDocType_Unknown_ReturnsEmpty(string path)
    {
      Assert.Equal(string.Empty, VelumDocumentIdentityRules.ResolveDocType(path));
    }

    [Fact]
    public void NormalizePath_Relative_BecomesAbsolute()
    {
      string normalized = VelumDocumentIdentityRules.NormalizePath("plate.sldprt");
      Assert.True(System.IO.Path.IsPathRooted(normalized));
      Assert.EndsWith("plate.sldprt", normalized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NormalizePath_Empty_ReturnsEmpty()
    {
      Assert.Equal(string.Empty, VelumDocumentIdentityRules.NormalizePath(null));
      Assert.Equal(string.Empty, VelumDocumentIdentityRules.NormalizePath("  "));
    }

    [Fact]
    public void NormalizePath_LongPathPrefix_Removed()
    {
      string path = @"\\?\C:\work\plate.sldprt";
      string normalized = VelumDocumentIdentityRules.NormalizePath(path);
      Assert.Equal(@"C:\work\plate.sldprt", normalized);
    }

    [Fact]
    public void BuildDocKey_IncludesTypeAndConfig()
    {
      string key = VelumDocumentIdentityRules.BuildDocKey(
          @"C:\work\plate.sldprt", "part", "Default");

      Assert.Equal(@"C:\work\plate.sldprt|part|Default", key);
    }

    [Fact]
    public void BuildDocKey_EmptyConfig_StillBuildsKey()
    {
      string key = VelumDocumentIdentityRules.BuildDocKey(@"C:\work\plate.sldprt", "PART", null);
      Assert.Equal(@"C:\work\plate.sldprt|part|", key);
    }

    [Fact]
    public void BuildDocKey_MissingPathOrType_ReturnsEmpty()
    {
      Assert.Equal(string.Empty, VelumDocumentIdentityRules.BuildDocKey(null, "part", null));
      Assert.Equal(string.Empty, VelumDocumentIdentityRules.BuildDocKey(@"C:\work\plate.sldprt", "", null));
    }

    [Fact]
    public void BuildUnsavedDocKey_HasPrefix()
    {
      string key = VelumDocumentIdentityRules.BuildUnsavedDocKey("guid-1", "Деталь1");
      Assert.Equal("unsaved:guid-1:Деталь1", key);
      Assert.True(VelumDocumentIdentityRules.IsUnsavedDocKey(key));
    }

    [Fact]
    public void BuildUnsavedDocKey_MissingParts_ReturnsEmpty()
    {
      Assert.Equal(string.Empty, VelumDocumentIdentityRules.BuildUnsavedDocKey(null, "x"));
      Assert.Equal(string.Empty, VelumDocumentIdentityRules.BuildUnsavedDocKey("g", " "));
    }

    [Fact]
    public void IsUnsavedDocKey_NormalKey_IsFalse()
    {
      Assert.False(VelumDocumentIdentityRules.IsUnsavedDocKey(@"C:\work\plate.sldprt|part|"));
      Assert.False(VelumDocumentIdentityRules.IsUnsavedDocKey(null));
    }

    [Fact]
    public void DocKeysEqual_IgnoresCaseAndWhitespace()
    {
      Assert.True(VelumDocumentIdentityRules.DocKeysEqual(
          @"C:\Work\Plate.sldprt|part|",
          @"c:\work\plate.SLDPRT|part| "));
    }

    [Fact]
    public void DocKeysEqual_DifferentConfig_IsFalse()
    {
      Assert.False(VelumDocumentIdentityRules.DocKeysEqual(
          @"C:\work\plate.sldprt|part|Default",
          @"C:\work\plate.sldprt|part|Short"));
    }
  }
}
