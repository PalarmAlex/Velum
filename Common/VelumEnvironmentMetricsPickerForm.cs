using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using ISIDA.Actions;
using Velum.SolidHomeostasis;

namespace Velum.UI
{
  /// <summary>
  /// Чек-лист метрик среды: включение/выключение с фильтрацией по имени и описанию.
  /// </summary>
  internal sealed partial class VelumEnvironmentMetricsPickerForm : Form
  {
    private enum EnabledFilter
    {
      All,
      On,
      Off,
    }

    private sealed class MetricRow
    {
      public int Id;
      public string Name;
      public string Description;
      public bool Enabled;
      public bool InitialEnabled;
    }

    private readonly List<MetricRow> _rows;
    private readonly List<int> _displayToRowIndex = new List<int>();
    private bool _suppressItemCheck;
    private string _activeTextFilter = string.Empty;
    private EnabledFilter _activeEnabledFilter = EnabledFilter.All;

    /// <summary>Число метрик, у которых изменилась активность после «Применить».</summary>
    public int AppliedChangeCount { get; private set; }

    /// <summary>Конструктор для конструктора форм Visual Studio.</summary>
    public VelumEnvironmentMetricsPickerForm()
    {
      _rows = new List<MetricRow>();
      InitializeComponent();
      VelumFormHelp.Bind(this, VelumHelpTopics.EnvironmentMetrics);
    }

    public VelumEnvironmentMetricsPickerForm(
        IReadOnlyList<InfluenceActionSystem.GomeostasisInfluenceAction> metrics)
        : this()
    {
      Icon icon = TryLoadVelumWindowIcon();
      if (icon != null)
        Icon = icon;

      var tip = new ToolTip();
      tip.SetToolTip(_btnFilterApply, "Применить фильтры");
      tip.SetToolTip(_btnFilterReset, "Очистить фильтры");
      tip.SetToolTip(_btnFilterHelp, "Справка по маскам фильтра");
      tip.SetToolTip(_btnApply, "Применить выбранные метрики");
      tip.SetToolTip(_btnClose, "Закрыть без применения");

      _rows.AddRange((metrics ?? Array.Empty<InfluenceActionSystem.GomeostasisInfluenceAction>())
          .Where(m => m != null)
          .OrderBy(m => m.Id)
          .Select(m => new MetricRow
          {
            Id = m.Id,
            Name = string.IsNullOrWhiteSpace(m.Name) ? ("ID " + m.Id) : m.Name.Trim(),
            Description = m.Description?.Trim() ?? string.Empty,
            Enabled = m.IsActive,
            InitialEnabled = m.IsActive,
          }));

      _clb.ItemHeight = Math.Max(34, _clb.Font.Height * 2 + 8);
      _btnFilterApply.Click += (s, e) => ApplyListFiltersFromUi();
      _btnFilterReset.Click += (s, e) =>
      {
        _txtFilter.Text = string.Empty;
        _rbStateAll.Checked = true;
        _activeTextFilter = string.Empty;
        _activeEnabledFilter = EnabledFilter.All;
        RebuildFilteredList();
      };
      _btnFilterHelp.Click += (s, e) => VelumListFilterHelper.ShowHelp(this);
      _txtFilter.KeyDown += (s, e) =>
      {
        if (e.KeyCode != Keys.Enter)
          return;
        ApplyListFiltersFromUi();
        e.Handled = true;
        e.SuppressKeyPress = true;
      };
      RebuildFilteredList();
    }

    private void ApplyListFiltersFromUi()
    {
      _activeTextFilter = (_txtFilter.Text ?? string.Empty).Trim();
      _activeEnabledFilter = GetEnabledFilter();
      RebuildFilteredList();
    }

    private void Form_Shown(object sender, EventArgs e)
    {
      if (_clb != null && !_clb.IsDisposed)
        _clb.Invalidate();
    }

    private void TxtFilter_TextChanged(object sender, EventArgs e)
    {
      // Фильтр применяется кнопкой «Применить».
    }

    private void RbStateAll_CheckedChanged(object sender, EventArgs e)
    {
      // Состояние учитывается при «Применить» фильтра.
    }

    private void RbStateOn_CheckedChanged(object sender, EventArgs e)
    {
    }

    private void RbStateOff_CheckedChanged(object sender, EventArgs e)
    {
    }

    private void RbSelectAll_CheckedChanged(object sender, EventArgs e)
    {
      if (!_rbSelectAll.Checked)
        return;
      SetAllRowsEnabled(true);
    }

    private void RbClearAll_CheckedChanged(object sender, EventArgs e)
    {
      if (!_rbClearAll.Checked)
        return;
      SetAllRowsEnabled(false);
    }

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

    private EnabledFilter GetEnabledFilter()
    {
      if (_rbStateOn != null && _rbStateOn.Checked)
        return EnabledFilter.On;
      if (_rbStateOff != null && _rbStateOff.Checked)
        return EnabledFilter.Off;
      return EnabledFilter.All;
    }

    private static bool PassesTextFilter(MetricRow row, string query)
    {
      if (string.IsNullOrWhiteSpace(query))
        return true;
      return VelumListFilterHelper.Matches(row.Name, query)
          || VelumListFilterHelper.Matches(row.Description, query);
    }

    private static bool PassesStateFilter(MetricRow row, EnabledFilter filter)
    {
      switch (filter)
      {
        case EnabledFilter.On:
          return row.Enabled;
        case EnabledFilter.Off:
          return !row.Enabled;
        default:
          return true;
      }
    }

    private void RebuildFilteredList()
    {
      if (_clb == null || _rows == null)
        return;

      string query = _activeTextFilter;
      EnabledFilter stateFilter = _activeEnabledFilter;

      _suppressItemCheck = true;
      try
      {
        _clb.Items.Clear();
        _displayToRowIndex.Clear();
        for (int i = 0; i < _rows.Count; i++)
        {
          MetricRow row = _rows[i];
          if (!PassesTextFilter(row, query) || !PassesStateFilter(row, stateFilter))
            continue;
          _displayToRowIndex.Add(i);
          _clb.Items.Add(row.Name, row.Enabled);
        }
      }
      finally
      {
        _suppressItemCheck = false;
      }

      _clb.Invalidate();
    }

    private void SetAllRowsEnabled(bool enabled)
    {
      for (int i = 0; i < _rows.Count; i++)
        _rows[i].Enabled = enabled;
      RebuildFilteredList();
    }

    private void OnItemCheck(object sender, ItemCheckEventArgs e)
    {
      if (_suppressItemCheck)
        return;
      if (e.Index < 0 || e.Index >= _displayToRowIndex.Count)
        return;

      int rowIndex = _displayToRowIndex[e.Index];
      bool newValue = e.NewValue == CheckState.Checked;
      _rows[rowIndex].Enabled = newValue;

      if (_clb.IsHandleCreated)
        _clb.BeginInvoke((MethodInvoker)AfterItemCheckCommitted);
    }

    private void AfterItemCheckCommitted()
    {
      if (_clb == null || _clb.IsDisposed)
        return;
      EnabledFilter stateFilter = _activeEnabledFilter;
      if (stateFilter == EnabledFilter.All)
      {
        _clb.Invalidate();
        return;
      }

      RebuildFilteredList();
    }

    private void OnDrawItem(object sender, DrawItemEventArgs e)
    {
      if (e.Index < 0 || e.Index >= _displayToRowIndex.Count)
        return;

      MetricRow row = _rows[_displayToRowIndex[e.Index]];
      bool isOn = _clb.GetItemChecked(e.Index);
      Rectangle bounds = e.Bounds;
      e.DrawBackground();

      int checkSize = Math.Min(13, Math.Max(10, bounds.Height - 8));
      int checkTop = bounds.Top + (bounds.Height - checkSize) / 2;
      var checkRect = new Rectangle(bounds.Left + 2, checkTop, checkSize, checkSize);
      ButtonState bs = isOn ? ButtonState.Checked : ButtonState.Normal;
      ControlPaint.DrawCheckBox(e.Graphics, checkRect, bs);

      int textLeft = checkRect.Right + 6;
      int textW = Math.Max(1, bounds.Width - textLeft - 4);
      var nameRect = new Rectangle(textLeft, bounds.Top + 2, textW, _clb.Font.Height + 2);
      var descRect = new Rectangle(textLeft, nameRect.Bottom, textW, bounds.Height - nameRect.Height - 4);

      TextRenderer.DrawText(
          e.Graphics,
          row.Name ?? string.Empty,
          new Font(_clb.Font, FontStyle.Bold),
          nameRect,
          e.ForeColor,
          TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);

      string desc = string.IsNullOrWhiteSpace(row.Description) ? "—" : row.Description;
      Color descColor = SystemInformation.HighContrast
          ? e.ForeColor
          : Color.FromArgb(90, 90, 95);
      TextRenderer.DrawText(
          e.Graphics,
          desc,
          _clb.Font,
          descRect,
          descColor,
          TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.WordEllipsis);

      if ((e.State & DrawItemState.Focus) != 0 && _clb.Focused)
        e.DrawFocusRectangle();
    }

    private void OnApplyClick(object sender, EventArgs e)
    {
      if (!InfluenceActionSystem.IsInitialized)
      {
        MessageBox.Show(
            this,
            "Система воздействий ISIDA не инициализирована.",
            Text,
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return;
      }

      int changed = 0;
      var errors = new List<string>();
      foreach (MetricRow row in _rows)
      {
        if (row.Enabled == row.InitialEnabled)
          continue;

        var (ok, err) = InfluenceActionSystem.Instance.TrySetEnvironmentMetricActive(row.Id, row.Enabled);
        if (!ok)
        {
          errors.Add((row.Name ?? ("ID " + row.Id)) + ": " + (err ?? "ошибка"));
          continue;
        }

        if (!row.Enabled)
        {
          InfluenceActionSystem.GomeostasisInfluenceAction ea =
              InfluenceActionSystem.Instance.GetAllInfluenceActions()
                  .FirstOrDefault(a => a.Id == row.Id);
          string probeKey = (ea?.ProbeKey ?? string.Empty).Trim();
          if (probeKey.Length > 0)
            VelumSolidMetricPressurePauseRegistry.ClearPausesForProbeKey(probeKey);
        }

        row.InitialEnabled = row.Enabled;
        changed++;
      }

      if (errors.Count > 0)
      {
        MessageBox.Show(
            this,
            "Не все изменения применены:\n" + string.Join("\n", errors),
            Text,
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        if (changed == 0)
          return;
      }

      AppliedChangeCount = changed;
      if (changed > 0)
      {
        MessageBox.Show(
            this,
            changed == 1
                ? "Изменена активность одной метрики."
                : ("Изменена активность метрик: " + changed + "."),
            Text,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
      }
      else
      {
        MessageBox.Show(
            this,
            "Изменений не было.",
            Text,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
      }

      DialogResult = DialogResult.OK;
      Close();
    }
  }
}
