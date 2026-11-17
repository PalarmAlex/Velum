using System;
using System.Collections.Generic;
using System.Xml;

namespace Velum.UI
{
  /// <summary>
  /// Чистые правила редактирования XML-шаблона свойств документа (CPTemplate):
  /// добавление/удаление значения в виртуальный список подстановок ячейки «Значение»
  /// (<c>Control/Data/Item</c>) и перестановка узлов <c>Control</c> в заданном порядке.
  /// Работают над <see cref="XmlNode"/> без файлов, COM и WinForms — линкуется в тестовый
  /// проект Velum.ReactiveCore.Tests.
  /// </summary>
  internal static class VelumDocumentPropertyChoiceRules
  {
    /// <summary>
    /// Добавляет значение в список подстановок свойства (узел <c>Data/Item</c>).
    /// Пустое значение или уже присутствующий элемент не дублируются.
    /// </summary>
    /// <param name="controlNode">Узел свойства <c>Control</c>.</param>
    /// <param name="value">Добавляемое значение.</param>
    /// <returns><c>true</c>, если элемент был добавлен.</returns>
    internal static bool AddChoice(XmlNode controlNode, string value)
    {
      if (controlNode == null || string.IsNullOrEmpty(value))
        return false;

      XmlNode dataNode = controlNode.SelectSingleNode("Data");
      if (dataNode == null)
      {
        dataNode = controlNode.OwnerDocument.CreateElement("Data");
        controlNode.AppendChild(dataNode);
      }

      foreach (XmlNode item in dataNode.SelectNodes("Item"))
      {
        if (string.Equals(item.InnerText, value, StringComparison.Ordinal))
          return false;
      }

      XmlElement newItem = controlNode.OwnerDocument.CreateElement("Item");
      newItem.InnerText = value;
      dataNode.AppendChild(newItem);
      return true;
    }

    /// <summary>
    /// Удаляет из списка подстановок свойства первый элемент, равный значению.
    /// Сама строка/узел <c>Control</c> не удаляется.
    /// </summary>
    /// <param name="controlNode">Узел свойства <c>Control</c>.</param>
    /// <param name="value">Удаляемое значение.</param>
    /// <returns><c>true</c>, если элемент был удалён.</returns>
    internal static bool RemoveChoice(XmlNode controlNode, string value)
    {
      if (controlNode == null || string.IsNullOrEmpty(value))
        return false;

      XmlNode dataNode = controlNode.SelectSingleNode("Data");
      if (dataNode == null)
        return false;

      foreach (XmlNode item in dataNode.SelectNodes("Item"))
      {
        if (!string.Equals(item.InnerText, value, StringComparison.Ordinal))
          continue;

        dataNode.RemoveChild(item);
        return true;
      }

      return false;
    }

    /// <summary>
    /// Переставляет дочерние узлы <c>Control</c> родителя так, чтобы их порядок
    /// соответствовал переданному списку (список должен содержать узлы этого же родителя).
    /// </summary>
    /// <param name="parent">Родительский узел (обычно <c>GroupBox</c>).</param>
    /// <param name="orderedNodes">Узлы в желаемом порядке.</param>
    /// <returns><c>true</c>, если порядок изменён и документ нужно сохранить.</returns>
    internal static bool ReorderControls(XmlNode parent, IList<XmlNode> orderedNodes)
    {
      if (parent == null || orderedNodes == null || orderedNodes.Count == 0)
        return false;

      // Относительный порядок уже верный — ничего не делаем.
      int cursor = -1;
      bool ordered = true;
      for (int i = 0; i < orderedNodes.Count; i++)
      {
        int pos = IndexOfChild(parent, orderedNodes[i]);
        if (pos < cursor)
        {
          ordered = false;
          break;
        }

        cursor = pos;
      }

      if (ordered)
        return false;

      foreach (XmlNode node in orderedNodes)
        parent.RemoveChild(node);
      foreach (XmlNode node in orderedNodes)
        parent.AppendChild(node);

      return true;
    }

    private static int IndexOfChild(XmlNode parent, XmlNode node)
    {
      int index = 0;
      foreach (XmlNode child in parent.ChildNodes)
      {
        if (ReferenceEquals(child, node))
          return index;
        index++;
      }

      return int.MaxValue;
    }
  }
}
