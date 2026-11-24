using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Velum.ReactiveCore;

namespace Velum.UI
{
  /// <summary>
  /// Общая модальная форма подтверждения/изменения параметров мотор-действия.
  /// Рендерит только те параметры шага рецепта, что реально в нём заданы
  /// (значения из рецепта/переменных — как стартовые). ОК подтверждает и отдаёт
  /// введённые значения; «Отмена»/закрытие — <see cref="DialogResult.Cancel"/>.
  /// </summary>
  internal sealed class VelumMotorParamsDialog : Form
  {
    private readonly List<VelumMotorParamField> _fields;
    private readonly Dictionary<string, Control> _editors =
        new Dictionary<string, Control>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Значения, введённые оператором (ключи как в аргументах шага).</summary>
    public Dictionary<string, string> Values { get; private set; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Создаёт форму по описанию полей.</summary>
    /// <param name="title">Заголовок окна.</param>
    /// <param name="fields">Поля для отображения (не пустой).</param>
    public VelumMotorParamsDialog(string title, IReadOnlyList<VelumMotorParamField> fields)
    {
      _fields = fields != null ? new List<VelumMotorParamField>(fields) : new List<VelumMotorParamField>();

      Text = string.IsNullOrWhiteSpace(title) ? "Параметры действия" : title;
      FormBorderStyle = FormBorderStyle.FixedDialog;
      MaximizeBox = false;
      MinimizeBox = false;
      ShowInTaskbar = false;
      StartPosition = FormStartPosition.CenterParent;
      ClientSize = new Size(460, 60 + _fields.Count * 34);

      BuildControls();
    }

    private void BuildControls()
    {
      var layout = new TableLayoutPanel
      {
        ColumnCount = 2,
        RowCount = _fields.Count + 1,
        Dock = DockStyle.Fill,
        Padding = new Padding(12),
        AutoSize = false
      };
      layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180F));
      layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
      for (int i = 0; i < _fields.Count; i++)
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
      layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

      for (int i = 0; i < _fields.Count; i++)
      {
        VelumMotorParamField f = _fields[i];

        var label = new Label
        {
          Text = f.Required ? f.Label + " *" : f.Label,
          AutoSize = false,
          Dock = DockStyle.Fill,
          TextAlign = ContentAlignment.MiddleLeft
        };

        Control editor = CreateEditor(f);
        editor.Dock = DockStyle.Fill;
        _editors[f.Key] = editor;

        layout.Controls.Add(label, 0, i);
        layout.Controls.Add(editor, 1, i);
      }

      var buttons = new FlowLayoutPanel
      {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.RightToLeft,
        WrapContents = false,
        Padding = new Padding(0, 8, 0, 0)
      };

      var btnOk = new Button { Text = "ОК", DialogResult = DialogResult.OK, Width = 100 };
      var btnCancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, Width = 100 };
      buttons.Controls.Add(btnOk);
      buttons.Controls.Add(btnCancel);
      layout.Controls.Add(buttons, 0, _fields.Count);
      layout.SetColumnSpan(buttons, 2);

      Controls.Add(layout);

      AcceptButton = btnOk;
      CancelButton = btnCancel;
      btnOk.Click += (s, e) => CollectAndValidate();
    }

    private static Control CreateEditor(VelumMotorParamField f)
    {
      string t = (f.Type ?? string.Empty).Trim().ToLowerInvariant();

      if (t == "enum" && f.EnumValues != null && f.EnumValues.Count > 0)
      {
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (string v in f.EnumValues)
          combo.Items.Add(v);
        int idx = combo.Items.IndexOf(f.InitialValue);
        combo.SelectedIndex = idx >= 0 ? idx : (combo.Items.Count > 0 ? 0 : -1);
        return combo;
      }

      if (t == "bool")
      {
        string b = (f.InitialValue ?? string.Empty).Trim().ToLowerInvariant();
        return new CheckBox { Checked = b == "true" || b == "да" || b == "1", Text = string.Empty };
      }

      return new TextBox { Text = f.InitialValue };
    }

    private void CollectAndValidate()
    {
      var collected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

      foreach (VelumMotorParamField f in _fields)
      {
        string raw = ReadEditorValue(f, _editors[f.Key]);
        if (!VelumMotorParamDialogRules.TryValidateValue(
                f.Type, raw, f.Required, out string normalized, out string error))
        {
          MessageBox.Show(
              "Поле «" + f.Label + "»: " + error,
              Text,
              MessageBoxButtons.OK,
              MessageBoxIcon.Warning);
          DialogResult = DialogResult.None; // Отменяем закрытие, остаёмся в форме.
          return;
        }

        collected[f.Key] = normalized;
      }

      Values = collected;
    }

    private static string ReadEditorValue(VelumMotorParamField f, Control editor)
    {
      string t = (f.Type ?? string.Empty).Trim().ToLowerInvariant();

      if (t == "bool" && editor is CheckBox cb)
        return cb.Checked ? "true" : "false";

      if (editor is ComboBox combo)
        return combo.SelectedItem != null ? combo.SelectedItem.ToString() : string.Empty;

      if (editor is TextBox tb)
        return tb.Text;

      return string.Empty;
    }
  }
}
