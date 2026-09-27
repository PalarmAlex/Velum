using System;
using System.Collections.Generic;
using Velum.ReactiveCore;
using Xunit;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>Тесты разбора строки аргументов шага рецепта.</summary>
  public class RecipeArgsParserTests
  {
    [Fact]
    public void Parse_EmptyOrWhitespace_ReturnsEmptyDictionary()
    {
      Assert.Empty(RecipeArgsParser.Parse(null));
      Assert.Empty(RecipeArgsParser.Parse(string.Empty));
      Assert.Empty(RecipeArgsParser.Parse("   \r\n  "));
    }

    [Fact]
    public void Parse_KeyValuePairs_SplitsBySemicolonAndEquals()
    {
      Dictionary<string, string> args = RecipeArgsParser.Parse("a=1; b=2");

      Assert.Equal(2, args.Count);
      Assert.Equal("1", args["a"]);
      Assert.Equal("2", args["b"]);
    }

    [Fact]
    public void Parse_KeysAreCaseInsensitive()
    {
      Dictionary<string, string> args = RecipeArgsParser.Parse("Name=КББ");

      Assert.Equal("КББ", args["NAME"]);
      Assert.Equal("КББ", args["name"]);
    }

    [Fact]
    public void Parse_ValueMayContainEquals_OnlyFirstDelimiterSplits()
    {
      Dictionary<string, string> args = RecipeArgsParser.Parse("formula=a=b");

      Assert.Equal("a=b", args["formula"]);
    }

    [Fact]
    public void Parse_LinesWithMissingKeyOrEmptySegments_AreIgnored()
    {
      Dictionary<string, string> args = RecipeArgsParser.Parse(";=5; key2=6; ;novalue");

      Assert.Single(args);
      Assert.Equal("6", args["key2"]);
    }

    [Fact]
    public void Parse_DuplicateKeys_LastValueWins()
    {
      Dictionary<string, string> args = RecipeArgsParser.Parse("k=1; k=2");

      Assert.Equal("2", args["k"]);
    }

    [Fact]
    public void Get_FoundKey_ReturnsValueIgnoringCase()
    {
      Dictionary<string, string> args = RecipeArgsParser.Parse("Path=d:\\tmp");

      Assert.Equal("d:\\tmp", RecipeArgsParser.Get(args, "PATH"));
    }

    [Fact]
    public void Get_MissingKeyNullOrArgsOrEmptyKey_ReturnsEmptyString()
    {
      Dictionary<string, string> args = RecipeArgsParser.Parse("a=1");

      Assert.Equal(string.Empty, RecipeArgsParser.Get(args, "b"));
      Assert.Equal(string.Empty, RecipeArgsParser.Get(null, "a"));
      Assert.Equal(string.Empty, RecipeArgsParser.Get(args, "  "));
    }

    [Fact]
    public void Get_NullValueInDictionary_ReturnsEmptyString()
    {
      var args = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["x"] = null };

      Assert.Equal(string.Empty, RecipeArgsParser.Get(args, "x"));
    }
  }
}