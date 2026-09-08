using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Velum.UI.AssemblyRegistry;

namespace Velum.UI
{
  /// <summary>Подстановка функций и reserved-ссылок в формулу столбца.</summary>
  internal sealed partial class VelumAssemblyRegistryFormulaHelperForm : Form
  {
    internal string SelectedInsertText { get; private set; }

    public VelumAssemblyRegistryFormulaHelperForm()
    {
      InitializeComponent();
      InitializeRuntime();
    }

    private void InitializeRuntime()
    {
      Icon icon = TryLoadFormIcon();
      if (icon != null)
        Icon = icon;

      var tip = new ToolTip();
      tip.SetToolTip(_btnInsert, "Вставить выбранную функцию в формулу");
      tip.SetToolTip(_btnClose, "Закрыть");

      _list.Items.Clear();
      foreach (VelumAssemblyRegistryColumnFormula.FormulaCatalogItem item in VelumAssemblyRegistryColumnFormula.Catalog)
      {
        var row = new ListViewItem(item.Display) { Tag = item };
        row.SubItems.Add(item.Description ?? string.Empty);
        _list.Items.Add(row);
      }

      if (_list.Items.Count > 0)
        _list.Items[0].Selected = true;
    }

    private void OnInsert(object sender, EventArgs e)
    {
      if (_list.SelectedItems.Count == 0)
        return;

      var item = _list.SelectedItems[0].Tag as VelumAssemblyRegistryColumnFormula.FormulaCatalogItem;
      if (item == null)
        return;

      SelectedInsertText = item.InsertText;
      DialogResult = DialogResult.OK;
      Close();
    }

    private void OnListDoubleClick(object sender, EventArgs e)
    {
      OnInsert(sender, e);
    }

    private void OnCloseClick(object sender, EventArgs e)
    {
      DialogResult = DialogResult.Cancel;
      Close();
    }

    private static Icon TryLoadFormIcon()
    {
      try
      {
        string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (string.IsNullOrEmpty(dir))
          return null;
        string path = Path.Combine(dir, "icons", "velum.ico");
        return File.Exists(path) ? new Icon(path) : null;
      }
      catch
      {
        return null;
      }
    }
  }
}
