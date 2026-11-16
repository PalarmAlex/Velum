using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;
using Velum.ReactiveCore.Export;

namespace Velum.UI
{
  /// <summary>Выбор свойства детали: имя и значение для вставки в имя файла DXF.</summary>
  internal sealed partial class VelumDocumentPropertyPickerForm : Form
  {
    /// <summary>Имя выбранного свойства.</summary>
    public string SelectedPropertyName { get; private set; }

    /// <summary>Значение выбранного свойства (может быть пустым).</summary>
    public string SelectedPropertyValue { get; private set; }

    /// <summary>Конструктор для конструктора форм Visual Studio.</summary>
    public VelumDocumentPropertyPickerForm()
    {
      InitializeComponent();
    }

    public VelumDocumentPropertyPickerForm(ModelDoc2 modelDoc)
        : this()
    {
      Icon icon = TryLoadVelumWindowIcon();
      if (icon != null)
        Icon = icon;

      var tip = new ToolTip();
      tip.SetToolTip(_btnOk, "Вставить выбранное свойство");
      tip.SetToolTip(_btnCancel, "Отменить");

      foreach (VelumDxfPropertyEntry entry in VelumDxfFileNameHelper.CollectCustomPropertyEntries(modelDoc))
        _grid.Rows.Add(entry.Name, entry.Value);

      if (_grid.Rows.Count == 0)
        _grid.Rows.Add("(нет свойств)", string.Empty);
    }

    private void Grid_DoubleClick(object sender, EventArgs e)
    {
      AcceptSelection();
    }

    private void Grid_KeyDown(object sender, KeyEventArgs e)
    {
      if (e.KeyCode == Keys.Enter)
        AcceptSelection();
    }

    private void BtnOk_Click(object sender, EventArgs e)
    {
      AcceptSelection();
    }

    private void AcceptSelection()
    {
      if (_grid.CurrentRow == null || _grid.CurrentRow.IsNewRow)
        return;

      string name = Convert.ToString(_grid.CurrentRow.Cells[0].Value) ?? string.Empty;
      if (string.IsNullOrWhiteSpace(name) || name.StartsWith("(", StringComparison.Ordinal))
        return;

      SelectedPropertyName = name.Trim();
      SelectedPropertyValue = (Convert.ToString(_grid.CurrentRow.Cells[1].Value) ?? string.Empty).Trim();
      DialogResult = DialogResult.OK;
      Close();
    }

    private static Icon TryLoadVelumWindowIcon()
    {
      return VelumFormIcon.TryLoad();
    }
  }
}
