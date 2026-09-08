using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace Velum.UI
{
  /// <summary>Простой диалог ввода текста ТТ со спецсимволами SolidWorks.</summary>
  internal sealed class VelumTechRequirementsInputForm : Form
  {
    private readonly TextBox _textBox;

    internal string InputText { get; private set; }

    internal VelumTechRequirementsInputForm(string title, string prompt, string defaultValue = "")
    {
      Text = title;
      Width = 540;
      Height = 150;
      FormBorderStyle = FormBorderStyle.FixedDialog;
      MaximizeBox = false;
      MinimizeBox = false;
      ShowInTaskbar = false;
      StartPosition = FormStartPosition.CenterParent;
      KeyPreview = true;
      VelumFormIcon.Apply(this);

      var label = new Label
      {
        Text = prompt,
        AutoSize = true,
        Location = new Point(12, 12)
      };

      _textBox = new TextBox
      {
        Text = defaultValue ?? string.Empty,
        Location = new Point(12, 36),
        Width = 500,
        Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
      };

      var okButton = new Button
      {
        Text = "OK",
        DialogResult = DialogResult.OK,
        Location = new Point(356, 72),
        Width = 75
      };
      var cancelButton = new Button
      {
        Text = "Отмена",
        DialogResult = DialogResult.Cancel,
        Location = new Point(437, 72),
        Width = 75
      };
      var tip = new ToolTip();
      tip.SetToolTip(okButton, "Подтвердить ввод");
      tip.SetToolTip(cancelButton, "Отменить и закрыть");

      okButton.Click += (s, e) =>
      {
        InputText = _textBox.Text;
        DialogResult = DialogResult.OK;
        Close();
      };
      cancelButton.Click += (s, e) =>
      {
        DialogResult = DialogResult.Cancel;
        Close();
      };

      AcceptButton = okButton;
      CancelButton = cancelButton;

      Controls.Add(label);
      Controls.Add(_textBox);
      Controls.Add(okButton);
      Controls.Add(cancelButton);

      BindSpecialCharactersMenu();

      KeyDown += (s, e) =>
      {
        if (e.KeyCode == Keys.Escape)
        {
          DialogResult = DialogResult.Cancel;
          Close();
        }
      };
    }

    private void BindSpecialCharactersMenu()
    {
      var menu = new ContextMenuStrip();
      AddSpecial(menu, "Знак диаметра", "<MOD-DIAM>", "Diam.png");
      AddSpecial(menu, "Градус", "<MOD-DEG>", "Grad.png");
      AddSpecial(menu, "Катет сварки", "<AWLD-FILL>", "Kat.png");
      AddSpecial(menu, "Квадрат", "<MOD-BOX>", "Kwad.png");
      AddSpecial(menu, "Развертка", "<MOD-FLT>", "Razw.png");
      AddSpecial(menu, "Плюс/Минус", "<MOD-PM>", "PlusMin.png");
      _textBox.ContextMenuStrip = menu;
    }

    private void AddSpecial(ContextMenuStrip menu, string text, string token, string iconFile)
    {
      var item = new ToolStripMenuItem(text, TryLoadMenuBitmap(iconFile));
      item.Click += (s, e) => InsertToken(token);
      menu.Items.Add(item);
    }

    private void InsertToken(string token)
    {
      int index = _textBox.SelectionStart;
      _textBox.Text = _textBox.Text.Insert(index, token);
      _textBox.SelectionStart = index + token.Length;
      _textBox.Focus();
    }

    private static Image TryLoadMenuBitmap(string fileName)
    {
      try
      {
        string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (string.IsNullOrEmpty(dir))
          return null;

        string path = Path.Combine(dir, "icons", fileName);
        if (!File.Exists(path))
          return null;

        using (var loaded = new Bitmap(path))
          return new Bitmap(loaded);
      }
      catch
      {
        return null;
      }
    }

    private void InitializeComponent()
    {
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VelumTechRequirementsInputForm));
      this.SuspendLayout();
      // 
      // VelumTechRequirementsInputForm
      // 
      this.ClientSize = new System.Drawing.Size(284, 261);
      this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
      this.Name = "VelumTechRequirementsInputForm";
      this.ResumeLayout(false);

    }
  }
}
