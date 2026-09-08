using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Xml;

namespace Velum.UI
{
  /// <summary>
  /// Загрузка/редактирование шаблона свойств CPTemplate (*.prtprp / *.asmprp) в DataGridView.
  /// </summary>
  internal sealed class VelumDocumentPropertyTemplateGridHelper
  {
    private readonly DataGridView _grid;
    private readonly string _nameColumn;
    private readonly string _valueColumn;
    private string _xmlFilePath;
    private ComboBox _editingComboBox;
    private ContextMenuStrip _contextMenu;
    private DataGridViewRow _currentRow;

    internal VelumDocumentPropertyTemplateGridHelper(
        DataGridView grid,
        string nameColumn,
        string valueColumn,
        string xmlFilePath,
        Image addImage,
        Image removeImage,
        Image defaultImage,
        Image removeDefaultImage,
        Image clearImage,
        Image deleteImage)
    {
      _grid = grid ?? throw new ArgumentNullException(nameof(grid));
      _nameColumn = nameColumn;
      _valueColumn = valueColumn;
      _xmlFilePath = xmlFilePath ?? string.Empty;

      _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
      _grid.AllowUserToDeleteRows = false;
      _grid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
      _grid.MultiSelect = true;

      _grid.CellClick += OnCellClick;
      _grid.CellMouseDown += OnCellMouseDown;
      _grid.CellEndEdit += OnCellEndEdit;
      _grid.CellLeave += (s, e) => RemoveComboBox();
      _grid.KeyDown += OnKeyDown;

      BuildContextMenu(addImage, removeImage, defaultImage, removeDefaultImage, clearImage, deleteImage);
    }

    /// <summary>
    /// Удаляет выделенные (или текущую) строки только из таблицы — без правки XML-шаблона.
    /// </summary>
    internal bool TryRemoveSelectedRowsFromGrid()
    {
      var rows = CollectRowsForUiRemoval();
      if (rows.Count == 0)
        return false;

      string confirm = rows.Count == 1
          ? "Удалить выбранную строку из списка?"
          : "Удалить " + rows.Count + " выделенных строк из списка?";

      if (MessageBox.Show(
              confirm,
              "Свойства документов",
              MessageBoxButtons.YesNo,
              MessageBoxIcon.Question,
              MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        return false;

      RemoveComboBox();
      _grid.SuspendLayout();
      try
      {
        for (int i = rows.Count - 1; i >= 0; i--)
        {
          if (!rows[i].IsNewRow)
            _grid.Rows.Remove(rows[i]);
        }
      }
      finally
      {
        _grid.ResumeLayout();
      }

      return true;
    }

    private List<DataGridViewRow> CollectRowsForUiRemoval()
    {
      var rows = new List<DataGridViewRow>();
      foreach (DataGridViewRow row in _grid.SelectedRows)
      {
        if (!row.IsNewRow)
          rows.Add(row);
      }

      if (rows.Count == 0 &&
          _currentRow != null &&
          !_currentRow.IsNewRow &&
          _grid.Rows.Contains(_currentRow))
        rows.Add(_currentRow);

      if (rows.Count == 0 &&
          _grid.CurrentRow != null &&
          !_grid.CurrentRow.IsNewRow)
        rows.Add(_grid.CurrentRow);

      return rows;
    }

    internal void UpdateXmlFilePath(string path)
    {
      _xmlFilePath = path ?? string.Empty;
    }

    internal void LoadDataFromXml()
    {
      _grid.Rows.Clear();
      if (string.IsNullOrWhiteSpace(_xmlFilePath) || !File.Exists(_xmlFilePath))
        return;

      var xmlDoc = new XmlDocument();
      try
      {
        xmlDoc.Load(_xmlFilePath);
      }
      catch (Exception ex)
      {
        MessageBox.Show(
            "Ошибка загрузки шаблона:\n" + ex.Message,
            "Свойства документов",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return;
      }

      XmlNodeList controlNodes = xmlDoc.SelectNodes("//Control");
      if (controlNodes == null)
        return;

      foreach (XmlNode controlNode in controlNodes)
      {
        string propName = controlNode.Attributes?["PropName"]?.Value;
        if (string.IsNullOrEmpty(propName))
          continue;

        int rowIndex = _grid.Rows.Add();
        DataGridViewRow row = _grid.Rows[rowIndex];
        row.Cells[_nameColumn].Value = propName;
        row.Cells[_valueColumn].Value = controlNode.Attributes?["DefaultValue"]?.Value;
        row.Tag = controlNode;
      }
    }

    internal IReadOnlyList<Velum.ReactiveCore.VelumDocumentPropertyBatchHelper.PropertyPair> CollectProperties()
    {
      var list = new List<Velum.ReactiveCore.VelumDocumentPropertyBatchHelper.PropertyPair>();
      foreach (DataGridViewRow row in _grid.Rows)
      {
        if (row.IsNewRow)
          continue;

        string name = row.Cells[_nameColumn].Value?.ToString();
        if (string.IsNullOrWhiteSpace(name))
          continue;

        list.Add(new Velum.ReactiveCore.VelumDocumentPropertyBatchHelper.PropertyPair
        {
          Name = name.Trim(),
          Value = row.Cells[_valueColumn].Value?.ToString() ?? string.Empty
        });
      }

      return list;
    }

    internal bool HasEmptyPropertyValues(out string firstEmptyName)
    {
      firstEmptyName = null;
      foreach (DataGridViewRow row in _grid.Rows)
      {
        if (row.IsNewRow)
          continue;

        string name = row.Cells[_nameColumn].Value?.ToString();
        if (string.IsNullOrWhiteSpace(name))
          continue;

        string value = row.Cells[_valueColumn].Value?.ToString();
        if (string.IsNullOrEmpty(value))
        {
          firstEmptyName = name;
          return true;
        }
      }

      return false;
    }

    internal void PersistNewRowsToXml()
    {
      foreach (DataGridViewRow row in _grid.Rows)
      {
        if (row.IsNewRow || row.Tag != null)
          continue;

        string propName = row.Cells[_nameColumn].Value?.ToString();
        if (string.IsNullOrWhiteSpace(propName))
          continue;

        string propValue = row.Cells[_valueColumn].Value?.ToString() ?? string.Empty;
        if (AddNewSettingToXml(propName.Trim(), propValue))
        {
          try
          {
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(_xmlFilePath);
            row.Tag = xmlDoc.SelectSingleNode("//Control[@PropName='" + EscapeXPath(propName.Trim()) + "']");
          }
          catch
          {
          }
        }
      }
    }

    internal bool AddNewSettingToXml(string propName, string defaultValue)
    {
      if (string.IsNullOrWhiteSpace(_xmlFilePath) || string.IsNullOrWhiteSpace(propName))
        return false;

      try
      {
        var xmlDoc = new XmlDocument();
        if (File.Exists(_xmlFilePath))
          xmlDoc.Load(_xmlFilePath);
        else
        {
          xmlDoc.AppendChild(xmlDoc.CreateXmlDeclaration("1.0", "UTF-8", null));
          xmlDoc.AppendChild(xmlDoc.CreateElement("CPTemplate"));
        }

        XmlNode existing = xmlDoc.SelectSingleNode("//Control[@PropName='" + EscapeXPath(propName) + "']");
        if (existing != null)
          return true;

        XmlNode group = xmlDoc.SelectSingleNode("//GroupBox") ??
                        EnsureGroupBox(xmlDoc);

        XmlElement control = xmlDoc.CreateElement("Control");
        control.SetAttribute("Label", propName);
        control.SetAttribute("PropName", propName);
        if (!string.IsNullOrEmpty(defaultValue))
          control.SetAttribute("DefaultValue", defaultValue);
        control.SetAttribute("ApplyTo", "Global");
        control.SetAttribute("Type", "ComboBox");
        control.SetAttribute("ReadOnly", "False");
        control.SetAttribute("UserDefineable", "True");

        XmlElement data = xmlDoc.CreateElement("Data");
        data.SetAttribute("Path", string.Empty);
        data.SetAttribute("SourceType", "List");
        control.AppendChild(data);
        group.AppendChild(control);

        xmlDoc.Save(_xmlFilePath);
        return true;
      }
      catch (Exception ex)
      {
        MessageBox.Show(
            "Не удалось сохранить настройку:\n" + ex.Message,
            "Свойства документов",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return false;
      }
    }

    private static XmlNode EnsureGroupBox(XmlDocument xmlDoc)
    {
      XmlElement root = xmlDoc.DocumentElement ?? xmlDoc.CreateElement("CPTemplate");
      if (xmlDoc.DocumentElement == null)
        xmlDoc.AppendChild(root);

      XmlElement sheet = root.SelectSingleNode("CPSheet") as XmlElement;
      if (sheet == null)
      {
        sheet = xmlDoc.CreateElement("CPSheet");
        root.AppendChild(sheet);
      }

      XmlElement group = sheet.SelectSingleNode("GroupBox") as XmlElement;
      if (group == null)
      {
        group = xmlDoc.CreateElement("GroupBox");
        group.SetAttribute("Label", "Свойства");
        group.SetAttribute("DefaultState", "Expanded");
        sheet.AppendChild(group);
      }

      return group;
    }

    private void BuildContextMenu(
        Image addImage,
        Image removeImage,
        Image defaultImage,
        Image removeDefaultImage,
        Image clearImage,
        Image deleteImage)
    {
      _contextMenu = new ContextMenuStrip();
      _contextMenu.Items.Add(new ToolStripMenuItem("Добавить в список выбора", addImage, OnAddChoice));
      _contextMenu.Items.Add(new ToolStripMenuItem("Удалить из списка выбора", removeImage, OnRemoveRowsFromList));
      _contextMenu.Items.Add(new ToolStripSeparator());
      _contextMenu.Items.Add(new ToolStripMenuItem("Назначить по умолчанию", defaultImage, OnSetDefault));
      _contextMenu.Items.Add(new ToolStripMenuItem("Удалить значение по умолчанию", removeDefaultImage, OnRemoveDefault));
      _contextMenu.Items.Add(new ToolStripSeparator());
      _contextMenu.Items.Add(new ToolStripMenuItem("Очистить", clearImage, OnClearValue));
      _contextMenu.Items.Add(new ToolStripSeparator());
      _contextMenu.Items.Add(new ToolStripMenuItem("Удалить настройку", deleteImage, OnDeleteSetting));
    }

    private void OnCellClick(object sender, DataGridViewCellEventArgs e)
    {
      if (e.RowIndex < 0 || e.ColumnIndex < 0)
        return;
      if (_grid.Columns[e.ColumnIndex].Name != _valueColumn)
        return;

      ShowComboBoxForCell(e.RowIndex, e.ColumnIndex);
    }

    private void OnCellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
    {
      if (e.Button != MouseButtons.Right || e.RowIndex < 0 || e.ColumnIndex < 0)
        return;

      _grid.CurrentCell = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
      _currentRow = _grid.Rows[e.RowIndex];
      if (_currentRow.IsNewRow)
        return;

      if (!_currentRow.Selected)
      {
        _grid.ClearSelection();
        _currentRow.Selected = true;
      }

      _contextMenu.Show(Cursor.Position);
    }

    private void OnCellEndEdit(object sender, DataGridViewCellEventArgs e)
    {
      RemoveComboBox();
      if (e.RowIndex < 0)
        return;

      DataGridViewRow row = _grid.Rows[e.RowIndex];
      if (row.IsNewRow || row.Tag != null)
        return;

      if (_grid.Columns[e.ColumnIndex].Name != _nameColumn)
        return;

      string propName = row.Cells[_nameColumn].Value?.ToString();
      string propValue = row.Cells[_valueColumn].Value?.ToString() ?? string.Empty;
      if (string.IsNullOrWhiteSpace(propName))
        return;

      if (AddNewSettingToXml(propName.Trim(), propValue))
      {
        try
        {
          var xmlDoc = new XmlDocument();
          xmlDoc.Load(_xmlFilePath);
          row.Tag = xmlDoc.SelectSingleNode("//Control[@PropName='" + EscapeXPath(propName.Trim()) + "']");
        }
        catch
        {
        }
      }
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
      if (e.KeyCode == Keys.Delete && e.Control)
      {
        OnDeleteSetting(sender, e);
        e.Handled = true;
        e.SuppressKeyPress = true;
        return;
      }

      if (e.KeyCode == Keys.Delete)
      {
        TryRemoveSelectedRowsFromGrid();
        e.Handled = true;
        e.SuppressKeyPress = true;
      }
    }

    private void ShowComboBoxForCell(int rowIndex, int columnIndex)
    {
      RemoveComboBox();
      DataGridViewRow row = _grid.Rows[rowIndex];
      var controlNode = row.Tag as XmlNode;

      Rectangle rect = _grid.GetCellDisplayRectangle(columnIndex, rowIndex, true);
      _editingComboBox = new ComboBox
      {
        DropDownStyle = ComboBoxStyle.DropDown,
        Location = rect.Location,
        Size = rect.Size
      };

      if (controlNode != null)
        FillComboBox(_editingComboBox, controlNode);

      _editingComboBox.Text = row.Cells[columnIndex].Value?.ToString() ?? string.Empty;
      _editingComboBox.KeyDown += (s, e) =>
      {
        if (e.KeyCode == Keys.Enter)
        {
          CommitComboBox();
          e.Handled = true;
        }
        else if (e.KeyCode == Keys.Escape)
        {
          RemoveComboBox();
          e.Handled = true;
        }
      };
      _editingComboBox.LostFocus += (s, e) => CommitComboBox();

      _grid.Controls.Add(_editingComboBox);
      _editingComboBox.Focus();
    }

    private void FillComboBox(ComboBox comboBox, XmlNode controlNode)
    {
      comboBox.Items.Clear();
      XmlNode dataNode = controlNode?.SelectSingleNode("Data");
      if (dataNode == null)
        return;

      foreach (XmlNode itemNode in dataNode.SelectNodes("Item"))
        comboBox.Items.Add(itemNode.InnerText);
    }

    private void CommitComboBox()
    {
      if (_editingComboBox == null)
        return;

      if (_grid.CurrentCell != null)
        _grid.CurrentCell.Value = _editingComboBox.Text;

      RemoveComboBox();
    }

    private void RemoveComboBox()
    {
      if (_editingComboBox == null)
        return;

      try
      {
        _grid.Controls.Remove(_editingComboBox);
        _editingComboBox.Dispose();
      }
      catch
      {
      }

      _editingComboBox = null;
    }

    private void OnAddChoice(object sender, EventArgs e)
    {
      if (_currentRow == null)
        return;

      string value = _currentRow.Cells[_valueColumn].Value?.ToString();
      var controlNode = _currentRow.Tag as XmlNode;
      if (controlNode == null || string.IsNullOrEmpty(value))
        return;

      XmlNode dataNode = controlNode.SelectSingleNode("Data");
      if (dataNode == null)
      {
        dataNode = controlNode.OwnerDocument.CreateElement("Data");
        controlNode.AppendChild(dataNode);
      }

      foreach (XmlNode item in dataNode.SelectNodes("Item"))
      {
        if (string.Equals(item.InnerText, value, StringComparison.Ordinal))
          return;
      }

      XmlElement newItem = controlNode.OwnerDocument.CreateElement("Item");
      newItem.InnerText = value;
      dataNode.AppendChild(newItem);
      SaveXml(controlNode.OwnerDocument);
    }

    private void OnRemoveRowsFromList(object sender, EventArgs e)
    {
      TryRemoveSelectedRowsFromGrid();
    }

    private void OnSetDefault(object sender, EventArgs e)
    {
      if (_currentRow == null)
        return;

      string value = _currentRow.Cells[_valueColumn].Value?.ToString();
      var controlNode = _currentRow.Tag as XmlNode;
      if (controlNode == null || string.IsNullOrEmpty(value))
        return;

      if (controlNode.Attributes["DefaultValue"] == null)
      {
        XmlAttribute attr = controlNode.OwnerDocument.CreateAttribute("DefaultValue");
        attr.Value = value;
        controlNode.Attributes.Append(attr);
      }
      else
      {
        controlNode.Attributes["DefaultValue"].Value = value;
      }

      SaveXml(controlNode.OwnerDocument);
    }

    private void OnRemoveDefault(object sender, EventArgs e)
    {
      if (_currentRow == null)
        return;

      var controlNode = _currentRow.Tag as XmlNode;
      if (controlNode?.Attributes?["DefaultValue"] == null)
        return;

      controlNode.Attributes.RemoveNamedItem("DefaultValue");
      _currentRow.Cells[_valueColumn].Value = null;
      SaveXml(controlNode.OwnerDocument);
    }

    private void OnClearValue(object sender, EventArgs e)
    {
      if (_currentRow != null)
        _currentRow.Cells[_valueColumn].Value = null;
      RemoveComboBox();
    }

    private void OnDeleteSetting(object sender, EventArgs e)
    {
      var rows = new List<DataGridViewRow>();
      foreach (DataGridViewRow row in _grid.SelectedRows)
      {
        if (!row.IsNewRow && row.Tag != null)
          rows.Add(row);
      }

      if (rows.Count == 0 && _currentRow != null && _currentRow.Tag != null)
        rows.Add(_currentRow);

      if (rows.Count == 0)
        return;

      if (MessageBox.Show(
              "Удалить выбранные настройки из шаблона?",
              "Свойства документов",
              MessageBoxButtons.YesNo,
              MessageBoxIcon.Warning,
              MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        return;

      XmlDocument xmlDoc = null;
      for (int i = rows.Count - 1; i >= 0; i--)
      {
        var node = rows[i].Tag as XmlNode;
        if (node?.ParentNode == null)
          continue;

        xmlDoc = node.OwnerDocument;
        node.ParentNode.RemoveChild(node);
        _grid.Rows.Remove(rows[i]);
      }

      if (xmlDoc != null)
        SaveXml(xmlDoc);
    }

    private bool SaveXml(XmlDocument xmlDoc)
    {
      try
      {
        xmlDoc.Save(_xmlFilePath);
        return true;
      }
      catch (Exception ex)
      {
        MessageBox.Show(
            "Ошибка сохранения шаблона:\n" + ex.Message,
            "Свойства документов",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return false;
      }
    }

    private static string EscapeXPath(string value)
    {
      if (string.IsNullOrEmpty(value))
        return string.Empty;

      return value.Replace("'", "&apos;");
    }
  }
}
