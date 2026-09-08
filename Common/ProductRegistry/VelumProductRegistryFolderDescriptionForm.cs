using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace Velum.UI
{
  /// <summary>Редактор описания каталога реестра изделий.</summary>
  internal sealed partial class VelumProductRegistryFolderDescriptionForm : Form
  {
    public string DescriptionText { get; private set; }

    public VelumProductRegistryFolderDescriptionForm()
    {
      InitializeComponent();
    }

    public VelumProductRegistryFolderDescriptionForm(string folderName, string description)
        : this()
    {
      Icon icon = TryLoadFormIcon();
      if (icon != null)
        Icon = icon;

      Text = "Описание каталога";
      _lblFolder.Text = string.IsNullOrWhiteSpace(folderName)
          ? "Каталог:"
          : ("Каталог: " + folderName.Trim());
      _descriptionBox.Text = description ?? string.Empty;
      _descriptionBox.Select(0, 0);

      var tip = new ToolTip();
      tip.SetToolTip(_btnApply, "Сохранить описание каталога");
      tip.SetToolTip(_btnClose, "Закрыть без сохранения");
    }

    private void OnApply(object sender, EventArgs e)
    {
      DescriptionText = _descriptionBox.Text ?? string.Empty;
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
