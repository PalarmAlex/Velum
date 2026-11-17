using System.Collections.Generic;
using System.Xml;
using Velum.UI;
using Xunit;

namespace Velum.ReactiveCore.Tests;

/// <summary>
/// Регрессионные тесты чистых правил шаблона свойств документов:
/// добавление/удаление значения из виртуального списка подстановок и
/// перестановка узлов <c>Control</c>.
/// </summary>
public sealed class VelumDocumentPropertyChoiceRulesTests
{
  // XML вида CPTemplate/GroupBox с одним Control и списком подстановок.
  private static XmlDocument CreateDoc(out XmlNode controlNode, params string[] items)
  {
    var doc = new XmlDocument();
    doc.LoadXml("<CPTemplate><GroupBox></GroupBox></CPTemplate>");
    XmlElement group = doc.DocumentElement!.FirstChild as XmlElement;
    XmlElement control = doc.CreateElement("Control");
    control.SetAttribute("PropName", "Материал");
    XmlElement data = doc.CreateElement("Data");
    foreach (string item in items)
    {
      XmlElement el = doc.CreateElement("Item");
      el.InnerText = item;
      data.AppendChild(el);
    }

    control.AppendChild(data);
    group!.AppendChild(control);
    controlNode = control;
    return doc;
  }

  private static List<string> ReadItems(XmlNode controlNode)
  {
    var result = new List<string>();
    XmlNode data = controlNode.SelectSingleNode("Data");
    if (data == null)
      return result;

    foreach (XmlNode item in data.SelectNodes("Item"))
      result.Add(item.InnerText);
    return result;
  }

  [Fact]
  public void AddChoice_СоздаётData_ЕслиОтсутствует()
  {
    var doc = new XmlDocument();
    doc.LoadXml("<CPTemplate><GroupBox><Control PropName=\"П\"/></GroupBox></CPTemplate>");
    XmlNode control = doc.SelectSingleNode("//Control");

    Assert.True(VelumDocumentPropertyChoiceRules.AddChoice(control, "Сталь"));
    Assert.Equal(new List<string> { "Сталь" }, ReadItems(control));
  }

  [Fact]
  public void AddChoice_НеДублируетСуществующее()
  {
    CreateDoc(out XmlNode control, "Сталь");

    Assert.False(VelumDocumentPropertyChoiceRules.AddChoice(control, "Сталь"));
    Assert.Equal(new List<string> { "Сталь" }, ReadItems(control));
  }

  [Fact]
  public void AddChoice_ПустоеЗначение_Игнорируется()
  {
    CreateDoc(out XmlNode control, "Сталь");

    Assert.False(VelumDocumentPropertyChoiceRules.AddChoice(control, ""));
    Assert.Equal(new List<string> { "Сталь" }, ReadItems(control));
  }

  [Fact]
  public void RemoveChoice_УдаляетРавныйЭлемент_СтрокуНеТрогает()
  {
    CreateDoc(out XmlNode control, "Сталь", "Латунь", "Алюминий");

    Assert.True(VelumDocumentPropertyChoiceRules.RemoveChoice(control, "Латунь"));
    // Строка (узел Control) осталась на месте.
    Assert.NotNull(control.SelectSingleNode("Data"));
    Assert.Equal(new List<string> { "Сталь", "Алюминий" }, ReadItems(control));
  }

  [Fact]
  public void RemoveChoice_РегистрУчитывается()
  {
    CreateDoc(out XmlNode control, "Сталь");

    // Регресс: раньше «Удалить из списка выбора» удалял всю строку вместо значения.
    Assert.False(VelumDocumentPropertyChoiceRules.RemoveChoice(control, "сталь"));
    Assert.Equal(new List<string> { "Сталь" }, ReadItems(control));
  }

  [Fact]
  public void RemoveChoice_НетData_ВозвращаетFalse()
  {
    var doc = new XmlDocument();
    doc.LoadXml("<CPTemplate><GroupBox><Control PropName=\"П\"/></GroupBox></CPTemplate>");
    XmlNode control = doc.SelectSingleNode("//Control");

    Assert.False(VelumDocumentPropertyChoiceRules.RemoveChoice(control, "Сталь"));
  }

  [Fact]
  public void RemoveChoice_ЗначениеОтсутствует_ВозвращаетFalse()
  {
    CreateDoc(out XmlNode control, "Сталь");

    Assert.False(VelumDocumentPropertyChoiceRules.RemoveChoice(control, "Медь"));
    Assert.Equal(new List<string> { "Сталь" }, ReadItems(control));
  }

  [Fact]
  public void ReorderControls_МеняетПорядок()
  {
    var doc = new XmlDocument();
    doc.LoadXml("<CPTemplate><GroupBox></GroupBox></CPTemplate>");
    XmlElement group = doc.DocumentElement!.FirstChild as XmlElement;
    var nodes = new List<XmlNode>();
    foreach (string name in new[] { "A", "B", "C" })
    {
      XmlElement c = doc.CreateElement("Control");
      c.SetAttribute("PropName", name);
      group!.AppendChild(c);
      nodes.Add(c);
    }

    // Желаемый порядок C, A, B.
    var desired = new List<XmlNode> { nodes[2], nodes[0], nodes[1] };
    Assert.True(VelumDocumentPropertyChoiceRules.ReorderControls(group, desired));

    var actual = new List<string>();
    foreach (XmlNode child in group.ChildNodes)
      actual.Add(child.Attributes?["PropName"]?.Value);
    Assert.Equal(new List<string> { "C", "A", "B" }, actual);
  }

  [Fact]
  public void ReorderControls_УжеВерныйПорядок_НеМеняет()
  {
    var doc = new XmlDocument();
    doc.LoadXml("<CPTemplate><GroupBox></GroupBox></CPTemplate>");
    XmlElement group = doc.DocumentElement!.FirstChild as XmlElement;
    var nodes = new List<XmlNode>();
    foreach (string name in new[] { "A", "B", "C" })
    {
      XmlElement c = doc.CreateElement("Control");
      c.SetAttribute("PropName", name);
      group!.AppendChild(c);
      nodes.Add(c);
    }

    Assert.False(VelumDocumentPropertyChoiceRules.ReorderControls(group, nodes));
  }
}
