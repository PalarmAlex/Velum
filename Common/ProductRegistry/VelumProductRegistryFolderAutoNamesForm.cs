using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Velum.Configuration;
using Velum.UI.ProductRegistry;

namespace Velum.UI
{
  /// <summary>Настройки автоимён каталогов по расширениям файлов.</summary>
  internal sealed partial class VelumProductRegistryFolderAutoNamesForm : Form
  {
    private readonly bool _isAdmin = VelumAppConfig.IsProductRegistryAdmin;

    public VelumProductRegistryFolderAutoNamesForm()
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
      tip.SetToolTip(_btnApply, "Сохранить автоимена каталогов");
      tip.SetToolTip(_btnClose, "Закрыть");

      _grid.AllowUserToAddRows = _isAdmin;
      _grid.AllowUserToDeleteRows = _isAdmin;
      _grid.ReadOnly = !_isAdmin;
      _colExtension.ReadOnly = !_isAdmin;
      _colFolderName.ReadOnly = !_isAdmin;
      _btnApply.Enabled = _isAdmin;
      _btnApply.Visible = _isAdmin;

      foreach (VelumProductFolderAutoNameMapping mapping in VelumProductRegistryFolderAutoNames.LoadOrCreate())
        _grid.Rows.Add(mapping.Extension, mapping.FolderName);
    }

    private void OnApply(object sender, EventArgs e)
    {
      if (!_isAdmin)
        return;

      var mappings = new List<VelumProductFolderAutoNameMapping>();
      foreach (DataGridViewRow row in _grid.Rows)
      {
        if (row.IsNewRow)
          continue;

        string extension = Convert.ToString(row.Cells[_colExtension.Index].Value) ?? string.Empty;
        string folderName = Convert.ToString(row.Cells[_colFolderName.Index].Value) ?? string.Empty;
        extension = VelumProductRegistryFolderAutoNames.NormalizeExtension(extension);
        folderName = folderName.Trim();
        if (string.IsNullOrEmpty(extension) && string.IsNullOrEmpty(folderName))
          continue;
        if (string.IsNullOrEmpty(extension) || string.IsNullOrEmpty(folderName))
        {
          MessageBox.Show(
              this,
              "Заполните оба поля: «Расширение» и «Имя каталога».",
              "Автоимена каталогов",
              MessageBoxButtons.OK,
              MessageBoxIcon.Warning);
          return;
        }

        mappings.Add(new VelumProductFolderAutoNameMapping
        {
          Extension = extension,
          FolderName = folderName
        });
      }

      try
      {
        VelumProductRegistryFolderAutoNames.Save(mappings);
      }
      catch (Exception ex)
      {
        MessageBox.Show(
            this,
            "Не удалось сохранить настройки:\n" + ex.Message,
            "Автоимена каталогов",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
        return;
      }

      DialogResult = DialogResult.OK;
      Close();
    }

    private void OnCloseClick(object sender, EventArgs e)
    {
      DialogResult = DialogResult.Cancel;
      Close();
    }

    private static Icon TryLoadFormIcon()
    {
      return VelumFormIcon.TryLoad();
    }
  }
}
