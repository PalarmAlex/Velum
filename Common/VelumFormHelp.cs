using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace Velum.UI
{
  /// <summary>
  /// Привязка контекстной справки: Maximize включён, кнопка справки с иконкой внизу у действий.
  /// F1 не перехватывается (штатная справка SOLIDWORKS).
  /// </summary>
  internal static class VelumFormHelp
  {
    private static readonly Size HelpButtonSize = new Size(28, 28);

    private static readonly string[] ActionHostNames =
    {
      // Реестр изделия: строка шаблона (справа от настроек столбцов).
      "_templateBar",
      // Реестр документов: верхняя строка фильтров (справа от настроек/проверки путей).
      "_filtersTopRow",
      "_filterButtonsHost",
      "_exportHubButtons",
      "_actionsRow",
      "_buttonsRow",
      "_btnRow",
      "_buttonsPanel",
      "_flowButtons",
      "_btnPanel",
      "_bottomRow"
    };

    /// <summary>
    /// Привязать раздел справки: разворот окна + кнопка справки в блоке действий (или полоса внизу).
    /// </summary>
    internal static void Bind(Form form, string topicId)
    {
      Bind(form, topicId, null);
    }

    /// <summary>
    /// Привязать к уже существующей кнопке справки (иконка Help.png).
    /// Если кнопки нет — создаётся в блоке действий / внизу формы.
    /// </summary>
    internal static void Bind(Form form, string topicId, Button existingHelpButton)
    {
      if (form == null || string.IsNullOrEmpty(topicId))
        return;

      form.HelpButton = false;
      form.MaximizeBox = true;
      if (form.FormBorderStyle == FormBorderStyle.FixedDialog
          || form.FormBorderStyle == FormBorderStyle.FixedSingle)
        form.FormBorderStyle = FormBorderStyle.Sizable;

      // Shown — после OnLoad формы (хаб экспорта и др. создаются в InitializeRuntime).
      // Один раз: иначе при повторном Show кнопка дублируется.
      EventHandler onShown = null;
      onShown = (s, e) =>
      {
        form.Shown -= onShown;
        if (existingHelpButton != null && !existingHelpButton.IsDisposed)
        {
          StyleAndWireHelpButton(existingHelpButton, form, topicId);
          if (!IsInActionHost(existingHelpButton))
            RelocateToActionHostOrBottomBar(form, existingHelpButton);
          return;
        }
        EnsureBottomHelpButton(form, topicId);
      };
      form.Shown += onShown;
    }

    /// <summary>Кнопка справки для task pane / тулбара (иконка Help.png).</summary>
    internal static Button CreateToolbarHelpButton(string topicId, IWin32Window owner)
    {
      Button btn = CreateHelpButtonCore();
      StyleAndWireHelpButton(btn, owner, topicId);
      return btn;
    }

    private static void EnsureBottomHelpButton(Form form, string topicId)
    {
      if (form.IsDisposed)
        return;

      // Убрать ошибочную нижнюю полосу, если она уже наложилась поверх контента.
      RemoveOrphanHelpBar(form);

      Control existing = FindNamed(form, "velumFormHelpButton")
          ?? FindNamed(form, "_btnFormHelp");
      if (existing is Button existingBtn)
      {
        StyleAndWireHelpButton(existingBtn, form, topicId);
        if (!IsInActionHost(existingBtn))
          RelocateToActionHostOrBottomBar(form, existingBtn);
        return;
      }

      Button help = CreateHelpButtonCore();
      help.Name = "velumFormHelpButton";
      StyleAndWireHelpButton(help, form, topicId);
      if (!TryInsertIntoActionHost(form, help))
        AttachBottomHelpBar(form, help);
    }

    private static void RemoveOrphanHelpBar(Form form)
    {
      Control bar = FindNamed(form, "velumFormHelpBar");
      if (bar == null || bar.IsDisposed)
        return;
      if (bar.Parent != null)
        bar.Parent.Controls.Remove(bar);
      bar.Dispose();
    }

    private static bool IsInActionHost(Control btn)
    {
      for (Control p = btn.Parent; p != null; p = p.Parent)
      {
        if (string.IsNullOrEmpty(p.Name))
          continue;
        for (int i = 0; i < ActionHostNames.Length; i++)
        {
          if (string.Equals(p.Name, ActionHostNames[i], StringComparison.Ordinal))
            return true;
        }
        if (string.Equals(p.Name, "velumFormHelpBar", StringComparison.Ordinal))
          return true;
      }
      return false;
    }

    private static void RelocateToActionHostOrBottomBar(Form form, Button help)
    {
      RemoveOrphanHelpBar(form);
      if (help.Parent != null)
        help.Parent.Controls.Remove(help);
      if (!TryInsertIntoActionHost(form, help))
        AttachBottomHelpBar(form, help);
    }

    private static bool TryInsertIntoActionHost(Form form, Button help)
    {
      for (int i = 0; i < ActionHostNames.Length; i++)
      {
        Control host = FindNamed(form, ActionHostNames[i]);
        if (host == null)
          continue;

        var flp = host as FlowLayoutPanel;
        if (flp != null)
        {
          flp.Controls.Add(help);
          // LTR: слева = индекс 0. RTL: последний = визуально слева.
          if (flp.FlowDirection != FlowDirection.RightToLeft)
            flp.Controls.SetChildIndex(help, 0);
          return true;
        }

        var tlp = host as TableLayoutPanel;
        if (tlp != null)
        {
          InsertHelpIntoTableRow(tlp, help);
          return true;
        }
      }

      return false;
    }

    private static void InsertHelpIntoTableRow(TableLayoutPanel tlp, Button help)
    {
      help.Anchor = AnchorStyles.Left;
      help.Dock = DockStyle.None;
      help.Margin = new Padding(0, 2, 8, 2);
      help.Size = HelpButtonSize;

      // Пустая первая колонка (spacer у действий) — кладём туда слева.
      if (tlp.ColumnCount > 0 && tlp.GetControlFromPosition(0, 0) == null)
      {
        tlp.Controls.Add(help, 0, 0);
        return;
      }

      // Иначе — в конец той же строки (рядом с Применить / Проверка путей и т.п.).
      int col = tlp.ColumnCount;
      tlp.ColumnCount = col + 1;
      tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36F));
      tlp.Controls.Add(help, col, 0);
    }

    private static void AttachBottomHelpBar(Form form, Button help)
    {
      Control existingBar = FindNamed(form, "velumFormHelpBar");
      if (existingBar != null)
      {
        existingBar.Controls.Add(help);
        return;
      }

      var bar = new Panel
      {
        Name = "velumFormHelpBar",
        Dock = DockStyle.Bottom,
        Height = 40,
        Padding = new Padding(8, 6, 8, 6)
      };
      help.Dock = DockStyle.Left;
      help.Size = HelpButtonSize;
      bar.Controls.Add(help);
      form.Controls.Add(bar);
      bar.BringToFront();
    }

    private static Button CreateHelpButtonCore()
    {
      return new Button
      {
        Size = HelpButtonSize,
        Margin = new Padding(0, 0, 8, 0),
        FlatStyle = FlatStyle.Flat,
        TabStop = false,
        Text = string.Empty,
        UseVisualStyleBackColor = true,
      };
    }

    private static void StyleAndWireHelpButton(Button btn, IWin32Window owner, string topicId)
    {
      if (btn == null)
        return;

      Image img = TryLoadHelpImage();
      if (img != null)
      {
        btn.Image = img;
        btn.ImageAlign = ContentAlignment.MiddleCenter;
        btn.Text = string.Empty;
      }
      else if (string.IsNullOrEmpty(btn.Text))
        btn.Text = "?";

      btn.FlatStyle = FlatStyle.Flat;
      btn.FlatAppearance.BorderSize = 0;
      btn.Cursor = Cursors.Hand;
      btn.TabStop = false;
      btn.Size = HelpButtonSize;

      var tip = new ToolTip();
      tip.SetToolTip(btn, "Справка Velum");

      btn.Click -= HelpClickHandler;
      btn.Tag = new HelpClickTag(owner, topicId);
      btn.Click += HelpClickHandler;
    }

    private sealed class HelpClickTag
    {
      public readonly IWin32Window Owner;
      public readonly string TopicId;

      public HelpClickTag(IWin32Window owner, string topicId)
      {
        Owner = owner;
        TopicId = topicId;
      }
    }

    private static void HelpClickHandler(object sender, EventArgs e)
    {
      var btn = sender as Button;
      var tag = btn != null ? btn.Tag as HelpClickTag : null;
      if (tag == null)
        return;
      VelumHelp.Show(tag.Owner, tag.TopicId);
    }

    private static Image TryLoadHelpImage()
    {
      try
      {
        string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (string.IsNullOrEmpty(dir))
          return null;
        string path = Path.Combine(dir, "icons", "Help.png");
        if (!File.Exists(path))
          return null;
        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var tmp = Image.FromStream(fs))
          return new Bitmap(tmp);
      }
      catch
      {
        return null;
      }
    }

    private static Control FindNamed(Control root, string name)
    {
      if (root == null || string.IsNullOrEmpty(name))
        return null;
      if (string.Equals(root.Name, name, StringComparison.Ordinal))
        return root;
      foreach (Control c in root.Controls)
      {
        Control found = FindNamed(c, name);
        if (found != null)
          return found;
      }
      return null;
    }
  }
}
