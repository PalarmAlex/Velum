using System.IO;
using Velum.ReactiveCore.DocumentTemplates;
using Xunit;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>
  /// Тесты правил библиотеки шаблонов: разрешение относительного пути шаблона
  /// (защита от выхода за корень) и выбор активного корня (проект → общий).
  /// </summary>
  public class VelumDocumentTemplateCatalogRulesTests
  {
    [Fact]
    public void ResolveTemplateFile_ValidRelative_ReturnsFullPath()
    {
      string root = @"C:\proj\Templates";
      string result = VelumDocumentTemplateCatalogRules.ResolveTemplateFile(root, @"parts\plate.sldprt");

      Assert.Equal(Path.GetFullPath(Path.Combine(root, @"parts\plate.sldprt")), result);
    }

    [Fact]
    public void ResolveTemplateFile_ForwardSlashes_Normalized()
    {
      string root = @"C:\proj\Templates";
      string result = VelumDocumentTemplateCatalogRules.ResolveTemplateFile(root, "parts/plate.sldprt");

      Assert.Equal(Path.GetFullPath(Path.Combine(root, @"parts\plate.sldprt")), result);
    }

    [Theory]
    [InlineData(null, "a.sldprt")]
    [InlineData("C:\\root", null)]
    [InlineData("", "a.sldprt")]
    [InlineData("C:\\root", "")]
    public void ResolveTemplateFile_EmptyArgs_ReturnsEmpty(string root, string file)
    {
      Assert.Equal(string.Empty, VelumDocumentTemplateCatalogRules.ResolveTemplateFile(root, file));
    }

    [Theory]
    [InlineData(@"C:\other\plate.sldprt")]
    [InlineData(@"\\server\share\plate.sldprt")]
    [InlineData(@"\absolute\plate.sldprt")]
    [InlineData("/absolute/plate.sldprt")]
    public void ResolveTemplateFile_AbsolutePath_Rejected(string file)
    {
      Assert.Equal(string.Empty,
          VelumDocumentTemplateCatalogRules.ResolveTemplateFile(@"C:\root", file));
    }

    [Theory]
    [InlineData(@"..\evil.sldprt")]
    [InlineData(@"parts\..\..\evil.sldprt")]
    [InlineData("parts/../../evil.sldprt")]
    public void ResolveTemplateFile_Traversal_Rejected(string file)
    {
      Assert.Equal(string.Empty,
          VelumDocumentTemplateCatalogRules.ResolveTemplateFile(@"C:\root", file));
    }

    [Fact]
    public void ResolveLibraryRoot_ProjectFirst()
    {
      string project = @"C:\proj\Templates";
      string shared = @"C:\ProgramData\VELUM\Templates";
      string projectManifest = Path.Combine(project, "document-templates.json");
      string sharedManifest = Path.Combine(shared, "document-templates.json");

      string result = VelumDocumentTemplateCatalogRules.ResolveLibraryRoot(
          project, shared,
          path => path == projectManifest || path == sharedManifest);

      Assert.Equal(project, result);
    }

    [Fact]
    public void ResolveLibraryRoot_ProjectMissing_FallsBackToShared()
    {
      string project = @"C:\proj\Templates";
      string shared = @"C:\ProgramData\VELUM\Templates";
      string sharedManifest = Path.Combine(shared, "document-templates.json");

      string result = VelumDocumentTemplateCatalogRules.ResolveLibraryRoot(
          project, shared,
          path => path == sharedManifest);

      Assert.Equal(shared, result);
    }

    [Fact]
    public void ResolveLibraryRoot_NeitherExists_ReturnsEmpty()
    {
      Assert.Equal(string.Empty, VelumDocumentTemplateCatalogRules.ResolveLibraryRoot(
          @"C:\proj\Templates", @"C:\ProgramData\VELUM\Templates", _ => false));
    }

    [Fact]
    public void ResolveLibraryRoot_EmptyRoots_ReturnsEmpty()
    {
      Assert.Equal(string.Empty, VelumDocumentTemplateCatalogRules.ResolveLibraryRoot(null, null, _ => true));
    }

    [Fact]
    public void GetLibraryFilePath_ReturnsCombinedPath()
    {
      Assert.Equal(
          Path.Combine(@"C:\root", "document-templates.json"),
          VelumDocumentTemplateCatalogRules.GetLibraryFilePath(@"C:\root"));
    }

    [Fact]
    public void GetLibraryFilePath_Empty_ReturnsEmpty()
    {
      Assert.Equal(string.Empty, VelumDocumentTemplateCatalogRules.GetLibraryFilePath(null));
      Assert.Equal(string.Empty, VelumDocumentTemplateCatalogRules.GetLibraryFilePath(" "));
    }
  }
}
