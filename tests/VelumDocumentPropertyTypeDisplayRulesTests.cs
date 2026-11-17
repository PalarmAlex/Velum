using Xunit;

namespace Velum.ReactiveCore.Tests;

/// <summary>
/// Тесты маппинга «отображаемое имя ↔ ключ» типов пользовательских свойств документов.
/// </summary>
public sealed class VelumDocumentPropertyTypeDisplayRulesTests
{
  [Theory]
  [InlineData("Текст", "text")]
  [InlineData("Дата", "date")]
  [InlineData("Номер", "number")]
  [InlineData("Да или Нет", "boolean")]
  public void DisplayToKey_ВозвращаетПравильныйКлюч(string display, string expected)
  {
    Assert.Equal(expected, VelumDocumentPropertyTypeDisplayRules.DisplayToKey(display));
  }

  [Theory]
  [InlineData("text", "Текст")]
  [InlineData("date", "Дата")]
  [InlineData("number", "Номер")]
  [InlineData("boolean", "Да или Нет")]
  public void KeyToDisplay_ВозвращаетПравильноеОтображаемоеИмя(string key, string expected)
  {
    Assert.Equal(expected, VelumDocumentPropertyTypeDisplayRules.KeyToDisplay(key));
  }

  [Fact]
  public void DisplayToKey_НеизвестноеЗначение_ВозвращаетText()
  {
    Assert.Equal("text", VelumDocumentPropertyTypeDisplayRules.DisplayToKey("НеизвестныйТип"));
  }

  [Fact]
  public void DisplayToKey_ПустоеЗначение_ВозвращаетText()
  {
    Assert.Equal("text", VelumDocumentPropertyTypeDisplayRules.DisplayToKey(""));
  }

  [Fact]
  public void DisplayToKey_Null_ВозвращаетText()
  {
    Assert.Equal("text", VelumDocumentPropertyTypeDisplayRules.DisplayToKey(null));
  }

  [Fact]
  public void KeyToDisplay_НеизвестноеЗначение_ВозвращаетТекст()
  {
    Assert.Equal("Текст", VelumDocumentPropertyTypeDisplayRules.KeyToDisplay("unknown"));
  }

  [Fact]
  public void KeyToDisplay_ПустоеЗначение_ВозвращаетТекст()
  {
    Assert.Equal("Текст", VelumDocumentPropertyTypeDisplayRules.KeyToDisplay(""));
  }

  [Fact]
  public void KeyToDisplay_Null_ВозвращаетТекст()
  {
    Assert.Equal("Текст", VelumDocumentPropertyTypeDisplayRules.KeyToDisplay(null));
  }

  [Fact]
  public void DisplayToKey_БезУчётаРегистра()
  {
    Assert.Equal("text", VelumDocumentPropertyTypeDisplayRules.DisplayToKey("ТЕКСТ"));
    Assert.Equal("boolean", VelumDocumentPropertyTypeDisplayRules.DisplayToKey("ДА ИЛИ НЕТ"));
  }

  [Fact]
  public void KeyToDisplay_БезУчётаРегистра()
  {
    Assert.Equal("Текст", VelumDocumentPropertyTypeDisplayRules.KeyToDisplay("TEXT"));
    Assert.Equal("Да или Нет", VelumDocumentPropertyTypeDisplayRules.KeyToDisplay("BOOLEAN"));
  }

  [Fact]
  public void DisplayToKey_ПробелыОтбрасываются()
  {
    Assert.Equal("date", VelumDocumentPropertyTypeDisplayRules.DisplayToKey("  Дата  "));
  }

  [Fact]
  public void DisplayNames_СодержатЧетыреТипаВПорядке()
  {
    var names = VelumDocumentPropertyTypeDisplayRules.DisplayNames;
    Assert.Equal(4, names.Count);
    Assert.Equal("Текст", names[0]);
    Assert.Equal("Дата", names[1]);
    Assert.Equal("Номер", names[2]);
    Assert.Equal("Да или Нет", names[3]);
  }
}