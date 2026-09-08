using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace Velum.UI
{
  /// <summary>
  /// Статичное окно с полным текстом по плашке метрики среды (снимок на момент открытия).
  /// </summary>
  internal sealed partial class VelumAgentBrickDetailForm : Form
  {
    /// <summary>Конструктор для конструктора форм Visual Studio.</summary>
    public VelumAgentBrickDetailForm()
    {
      InitializeComponent();
    }

    public VelumAgentBrickDetailForm(string caption, string body)
        : this()
    {
      Text = caption ?? string.Empty;
      _txt.Text = body ?? string.Empty;

      Icon icon = TryLoadVelumWindowIcon();
      if (icon != null)
        Icon = icon;

      var tip = new ToolTip();
      tip.SetToolTip(_btnClose, "Закрыть");
    }

    private void BtnClose_Click(object sender, EventArgs e)
    {
      Close();
    }

    /// <summary>
    /// Иконка окна: <c>velum.ico</c> рядом со сборкой или в подпапке <c>icons</c> (как при сборке проекта).
    /// </summary>
    private static Icon TryLoadVelumWindowIcon()
    {
      try
      {
        string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (string.IsNullOrEmpty(dir))
          return null;
        string[] candidates =
        {
          Path.Combine(dir, "velum.ico"),
          Path.Combine(dir, "icons", "velum.ico"),
        };
        foreach (string path in candidates)
        {
          if (File.Exists(path))
            return new Icon(path);
        }
      }
      catch
      {
      }

      return null;
    }

    protected override void OnShown(EventArgs e)
    {
      base.OnShown(e);
      ApplySelectNoneToTextBoxes(_layout);
    }

    private static void ApplySelectNoneToTextBoxes(Control root)
    {
      if (root is TextBox tb)
      {
        tb.SelectionStart = 0;
        tb.SelectionLength = 0;
        return;
      }

      for (int i = 0; i < root.Controls.Count; i++)
        ApplySelectNoneToTextBoxes(root.Controls[i]);
    }
  }
}
