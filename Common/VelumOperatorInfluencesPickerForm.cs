using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using ISIDA.Actions;

namespace Velum.UI
{
  /// <summary>
  /// Выбор операторских воздействий (без привязки к метрике среды) с учётом антагонистов.
  /// </summary>
  internal sealed partial class VelumOperatorInfluencesPickerForm : Form
  {
    /// <summary>Фон строки, заблокированной из‑за антагониста (контрастнее фона окна).</summary>
    private static readonly Color BlockedRowBackColor = Color.FromArgb(222, 224, 232);

    /// <summary>Текст недоступной строки — явнее, чем <see cref="SystemColors.GrayText"/> на части тем.</summary>
    private static readonly Color BlockedRowTextColor = Color.FromArgb(110, 110, 115);

    /// <summary>Акцент слева у заблокированной строки (заметнее серого фона на белом списке).</summary>
    private static readonly Color BlockedRowAccentColor = Color.FromArgb(130, 130, 155);

    /// <summary>Фон строки с подсказкой по текущему проблемному контексту.</summary>
    private static readonly Color SuggestedRowBackColor = Color.FromArgb(255, 236, 200);

    /// <summary>Акцент слева у строки-подсказки.</summary>
    private static readonly Color SuggestedRowAccentColor = Color.FromArgb(196, 120, 40);

    private readonly List<int> _idsInOrder;
    private readonly Dictionary<int, List<int>> _antagonistsMap;
    private int? _suggestedActionId;
    private bool[] _itemEnabled;
    private bool _suppressItemCheck;

    /// <summary>
    /// Идентификаторы выбранных воздействий после «Применить».
    /// </summary>
    public IReadOnlyList<int> SelectedActionIds { get; private set; } = Array.Empty<int>();

    /// <summary>Конструктор для конструктора форм Visual Studio.</summary>
    public VelumOperatorInfluencesPickerForm()
    {
      _idsInOrder = new List<int>();
      _antagonistsMap = new Dictionary<int, List<int>>();
      _suggestedActionId = null;
      InitializeComponent();
      VelumFormHelp.Bind(this, VelumHelpTopics.OperatorInfluences);
    }

    public VelumOperatorInfluencesPickerForm(
        IReadOnlyList<InfluenceActionSystem.GomeostasisInfluenceAction> actions,
        IReadOnlyList<int> initialSelection)
        : this(actions, initialSelection, null)
    {
    }

    public VelumOperatorInfluencesPickerForm(
        IReadOnlyList<InfluenceActionSystem.GomeostasisInfluenceAction> actions,
        IReadOnlyList<int> initialSelection,
        int? suggestedActionId)
        : this()
    {
      _suggestedActionId = suggestedActionId.HasValue && suggestedActionId.Value > 0
          ? suggestedActionId
          : null;

      Icon icon = TryLoadVelumWindowIcon();
      if (icon != null)
        Icon = icon;

      var tip = new ToolTip();
      tip.SetToolTip(_btnApply, "Применить выбранные воздействия");
      tip.SetToolTip(_btnCancel, "Отменить");

      var initial = new HashSet<int>(initialSelection ?? Array.Empty<int>());
      var byId = (actions ?? Array.Empty<InfluenceActionSystem.GomeostasisInfluenceAction>())
          .Where(x => x != null)
          .ToDictionary(x => x.Id, x => x);

      foreach (var a in byId.Values.OrderBy(x => x.Id))
      {
        _idsInOrder.Add(a.Id);
        _antagonistsMap[a.Id] = a.AntagonistInfluences != null
            ? new List<int>(a.AntagonistInfluences)
            : new List<int>();
      }

      _clb.ItemHeight = Math.Max(15, _clb.Font.Height + 4);

      _suppressItemCheck = true;
      try
      {
        for (int i = 0; i < _idsInOrder.Count; i++)
        {
          int id = _idsInOrder[i];
          byId.TryGetValue(id, out var act);
          string name = act != null && !string.IsNullOrWhiteSpace(act.Name) ? act.Name : ("ID " + id);
          if (_suggestedActionId.HasValue && id == _suggestedActionId.Value)
            name = name + "  · подсказка!";
          // Флажок только из текущего выбора оператора — подсказку не отмечаем.
          _clb.Items.Add(name, initial.Contains(id));
        }
      }
      finally
      {
        _suppressItemCheck = false;
      }

      _itemEnabled = new bool[_clb.Items.Count];
      RefreshItemEnabledStates();
    }

    private void Form_Shown(object sender, EventArgs e)
    {
      if (_clb != null && !_clb.IsDisposed)
      {
        if (_suggestedActionId.HasValue)
        {
          int idx = _idsInOrder.IndexOf(_suggestedActionId.Value);
          if (idx >= 0 && idx < _clb.Items.Count)
          {
            _clb.TopIndex = idx;
            // CheckOnClick иначе может отметить строку при программной селекции.
            bool checkOnClick = _clb.CheckOnClick;
            _clb.CheckOnClick = false;
            try
            {
              _clb.SelectedIndex = idx;
            }
            finally
            {
              _clb.CheckOnClick = checkOnClick;
            }
          }
        }

        _clb.Invalidate();
      }
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

    private void OnApplyClick(object sender, EventArgs e)
    {
      SelectedActionIds = CollectCheckedIds();
      DialogResult = DialogResult.OK;
      Close();
    }

    private List<int> CollectCheckedIds()
    {
      var r = new List<int>();
      for (int i = 0; i < _clb.Items.Count; i++)
      {
        if (_clb.GetItemChecked(i))
          r.Add(_idsInOrder[i]);
      }
      return r;
    }

    private void OnItemCheck(object sender, ItemCheckEventArgs e)
    {
      if (_suppressItemCheck)
        return;
      if (e.Index < 0 || e.Index >= _idsInOrder.Count)
        return;
      if (e.NewValue == CheckState.Checked && !CanCheckIndex(e.Index))
      {
        e.NewValue = e.CurrentValue;
        return;
      }

      if (_clb.IsHandleCreated)
        _clb.BeginInvoke((MethodInvoker)AfterItemCheckCommitted);
    }

    private void AfterItemCheckCommitted()
    {
      if (_clb.IsDisposed)
        return;
      RefreshItemEnabledStates();
      _clb.Invalidate();
      try
      {
        if (_clb.IsHandleCreated)
        {
          Point p = _clb.PointToClient(Control.MousePosition);
          if (_clb.ClientRectangle.Contains(p))
            UpdateListCursorForPoint(p);
        }
      }
      catch
      {
        _clb.Cursor = Cursors.Default;
      }
    }

    private void OnListMouseMove(object sender, MouseEventArgs e)
    {
      UpdateListCursorForPoint(e.Location);
    }

    private void OnListMouseLeave(object sender, EventArgs e)
    {
      _clb.Cursor = Cursors.Default;
    }

    private void UpdateListCursorForPoint(Point clientLocation)
    {
      if (_clb == null || _clb.IsDisposed || _itemEnabled == null || !_clb.IsHandleCreated)
        return;
      if (_clb.Items.Count == 0)
      {
        _clb.Cursor = Cursors.Default;
        return;
      }

      int idx;
      try
      {
        if (!_clb.ClientRectangle.Contains(clientLocation))
        {
          _clb.Cursor = Cursors.Default;
          return;
        }

        idx = _clb.IndexFromPoint(clientLocation);
      }
      catch (ArgumentException)
      {
        _clb.Cursor = Cursors.Default;
        return;
      }

      if (idx < 0 || idx >= _itemEnabled.Length)
      {
        _clb.Cursor = Cursors.Default;
        return;
      }

      bool rowBlocked = !_itemEnabled[idx];
      bool isOn = _clb.GetItemChecked(idx);
      _clb.Cursor = rowBlocked && !isOn ? Cursors.No : Cursors.Default;
    }

    /// <summary>
    /// Разрешить установить флажок: нет выбранного антагониста среди остальных отмеченных.
    /// </summary>
    private bool CanCheckIndex(int index)
    {
      int id = _idsInOrder[index];
      for (int j = 0; j < _idsInOrder.Count; j++)
      {
        if (j == index)
          continue;
        if (!_clb.GetItemChecked(j))
          continue;
        if (AreAntagonists(_idsInOrder[j], id))
          return false;
      }
      return true;
    }

    /// <summary>
    /// Доступность строки: антагонисты отмеченных недоступны, пока не снят конфликтующий выбор
    /// (аналогично привязке IsEnabled в диалогах выбора действий).
    /// </summary>
    private void RefreshItemEnabledStates()
    {
      if (_clb.Items.Count == 0)
        return;
      if (_itemEnabled == null || _itemEnabled.Length != _clb.Items.Count)
        _itemEnabled = new bool[_clb.Items.Count];

      for (int i = 0; i < _clb.Items.Count; i++)
      {
        bool isOn = _clb.GetItemChecked(i);
        bool blocked = false;
        for (int j = 0; j < _clb.Items.Count; j++)
        {
          if (j == i)
            continue;
          if (!_clb.GetItemChecked(j))
            continue;
          if (AreAntagonists(_idsInOrder[j], _idsInOrder[i]))
          {
            blocked = true;
            break;
          }
        }

        _itemEnabled[i] = isOn || !blocked;
      }
    }

    private void OnDrawItem(object sender, DrawItemEventArgs e)
    {
      if (e.Index < 0 || e.Index >= _clb.Items.Count)
        return;

      bool isOn = _clb.GetItemChecked(e.Index);
      bool enabled = _itemEnabled != null && e.Index < _itemEnabled.Length && _itemEnabled[e.Index];
      bool rowBlocked = !enabled;
      bool rowSuggested = !rowBlocked
          && _suggestedActionId.HasValue
          && e.Index < _idsInOrder.Count
          && _idsInOrder[e.Index] == _suggestedActionId.Value;
      Rectangle bounds = e.Bounds;

      if (rowBlocked)
      {
        using (var br = new SolidBrush(BlockedRowBackColor))
          e.Graphics.FillRectangle(br, bounds);
        using (var accent = new SolidBrush(BlockedRowAccentColor))
          e.Graphics.FillRectangle(accent, bounds.Left, bounds.Top, 3, bounds.Height);
        using (var pen = new Pen(Color.FromArgb(185, 185, 195)))
          e.Graphics.DrawLine(pen, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
      }
      else if (rowSuggested)
      {
        using (var br = new SolidBrush(SuggestedRowBackColor))
          e.Graphics.FillRectangle(br, bounds);
        using (var accent = new SolidBrush(SuggestedRowAccentColor))
          e.Graphics.FillRectangle(accent, bounds.Left, bounds.Top, 3, bounds.Height);
      }
      else
        e.DrawBackground();

      int checkSize = Math.Min(13, Math.Max(10, bounds.Height - 4));
      int checkTop = bounds.Top + (bounds.Height - checkSize) / 2;
      var checkRect = new Rectangle(bounds.Left + 2, checkTop, checkSize, checkSize);

      ButtonState bs;
      if (isOn)
        bs = enabled ? ButtonState.Checked : (ButtonState.Checked | ButtonState.Inactive);
      else
        bs = enabled ? ButtonState.Normal : ButtonState.Inactive;
      ControlPaint.DrawCheckBox(e.Graphics, checkRect, bs);

      string text = _clb.Items[e.Index]?.ToString() ?? string.Empty;
      Color fore;
      if (!enabled)
      {
        if (SystemInformation.HighContrast)
          fore = SystemColors.GrayText;
        else
          fore = BlockedRowTextColor;
      }
      else if (rowSuggested)
        fore = Color.FromArgb(80, 48, 8);
      else
        fore = e.ForeColor;

      var textRect = new Rectangle(
          checkRect.Right + 4,
          bounds.Top,
          Math.Max(1, bounds.Width - checkRect.Width - 8),
          bounds.Height);
      TextRenderer.DrawText(
          e.Graphics,
          text,
          e.Font,
          textRect,
          fore,
          TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

      if (enabled && (e.State & DrawItemState.Focus) != 0 && _clb.Focused)
        e.DrawFocusRectangle();
    }

    private bool AreAntagonists(int a, int b)
    {
      if (a == b)
        return false;
      var la = _antagonistsMap.TryGetValue(a, out var l1) ? l1 : null;
      var lb = _antagonistsMap.TryGetValue(b, out var l2) ? l2 : null;
      return (la != null && la.Contains(b)) || (lb != null && lb.Contains(a));
    }
  }
}
