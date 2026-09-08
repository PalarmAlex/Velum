using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Velum.UI.AssemblyRegistry;
using Xarial.XCad.SolidWorks;

namespace Velum.UI
{
  /// <summary>Очередь печати чертежей по выделенным позициям реестра изделия.</summary>
  internal sealed partial class VelumAssemblyRegistryPrintDrawingsForm : Form
  {
    private readonly ISwApplication _swApp;
    private readonly List<VelumAssemblyRegistryPrintDrawingsService.PrintRow> _rows;
    private readonly HashSet<VelumAssemblyRegistryPrintDrawingsService.PrintRow> _checkedRows =
        new HashSet<VelumAssemblyRegistryPrintDrawingsService.PrintRow>();
    private bool _checkedDefaultsApplied;
    private string _activePositionFilter = string.Empty;
    private string _activeDrawingFilter = string.Empty;
    private int _sortColumn;
    private SortOrder _sortOrder = SortOrder.Ascending;
    private bool _printing;
    private bool _cancelRequested;
    private bool _suppressSelectAllSync;
    private bool _suppressItemCheck;
    private VelumListViewCellFilterMenu _listCellFilterMenu;
    /// <summary>
    /// Пока ListView создаёт HWND, WinForms шлёт ItemChecked по уже отмеченным
    /// строкам — в этот момент Items могут быть «битыми» (NRE в UpdateStatusLabel).
    /// </summary>
    private bool _listEventsReady;

    /// <summary>Конструктор для WinForms Designer.</summary>
    public VelumAssemblyRegistryPrintDrawingsForm()
    {
      InitializeComponent();
      _rows = new List<VelumAssemblyRegistryPrintDrawingsService.PrintRow>();
    }

    internal VelumAssemblyRegistryPrintDrawingsForm(
        ISwApplication swApp,
        IReadOnlyList<VelumAssemblyRegistryPrintDrawingsService.PrintRow> rows)
        : this()
    {
      _swApp = swApp;
      _rows = new List<VelumAssemblyRegistryPrintDrawingsService.PrintRow>();
      if (rows != null)
      {
        for (int i = 0; i < rows.Count; i++)
        {
          if (rows[i] != null)
            _rows.Add(rows[i]);
        }
      }

      InitializeRuntime();
    }

    private void InitializeRuntime()
    {
      Font = SystemFonts.MessageBoxFont;
      Icon icon = TryLoadFormIcon();
      if (icon != null)
        Icon = icon;

      VelumFormHelp.Bind(this, VelumHelpTopics.AssemblyRegistry);
      BindFilterEvents();
      ApplyFilterToolTips();
      SetupListContextMenu();

      LoadPrinters();
      EnsureCheckedDefaults();
      BindRows();
      UpdateStatusLabel();
    }

    private void SetupListContextMenu()
    {
      Image filterIcon = TryLoadMenuBitmap("Thumbs up.png");
      Image excludeIcon = TryLoadMenuBitmap("Thumbs down.png");
      var menu = new ContextMenuStrip();
      _listCellFilterMenu = new VelumListViewCellFilterMenu(
          _list,
          this,
          ResolvePrintListFilterBox,
          ApplyFiltersFromUi,
          () => !_printing,
          null);
      _listCellFilterMenu.InsertInto(menu, 0, filterIcon, excludeIcon);
      _list.ContextMenuStrip = menu;
    }

    private TextBox ResolvePrintListFilterBox(int column)
    {
      if (column == 0)
        return _positionFilterBox;
      if (column == 1)
        return _drawingFilterBox;
      return null;
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
        return Image.FromFile(path);
      }
      catch
      {
        return null;
      }
    }

    private void BindFilterEvents()
    {
      KeyEventHandler enterApply = (s, e) =>
      {
        if (e.KeyCode != Keys.Enter)
          return;
        ApplyFiltersFromUi();
        e.Handled = true;
        e.SuppressKeyPress = true;
      };
      _positionFilterBox.KeyDown += enterApply;
      _drawingFilterBox.KeyDown += enterApply;
      _btnFilterApply.Click += (s, e) => ApplyFiltersFromUi();
      _btnFilterReset.Click += (s, e) => ResetFilters();
      _btnFilterHelp.Click += (s, e) => VelumListFilterHelper.ShowHelp(this);
    }

    private void ApplyFilterToolTips()
    {
      var tip = new ToolTip();
      tip.SetToolTip(_positionFilterBox, "Маска фильтра позиций (см. «?»)");
      tip.SetToolTip(_drawingFilterBox, "Маска фильтра чертежа/статуса (см. «?»)");
      tip.SetToolTip(_btnFilterApply, "Применить фильтры");
      tip.SetToolTip(_btnFilterReset, "Очистить фильтры");
      tip.SetToolTip(_btnFilterHelp, "Справка по маскам фильтра");
      tip.SetToolTip(_chkSelectAll, "Выделить или снять выделение со всех строк");
      tip.SetToolTip(_btnClose, "Закрыть");
      tip.SetToolTip(_btnPrint, "Печать чертежей выделенных позиций");
    }

    private void ApplyFiltersFromUi()
    {
      _activePositionFilter = (_positionFilterBox.Text ?? string.Empty).Trim();
      _activeDrawingFilter = (_drawingFilterBox.Text ?? string.Empty).Trim();
      BindRows();
      UpdateStatusLabel();
    }

    private void ResetFilters()
    {
      _positionFilterBox.Text = string.Empty;
      _drawingFilterBox.Text = string.Empty;
      _activePositionFilter = string.Empty;
      _activeDrawingFilter = string.Empty;
      BindRows();
      UpdateStatusLabel();
    }

    private void OnFormLoadEnableListEvents(object sender, EventArgs e)
    {
      if (DesignMode)
        return;

      Load -= OnFormLoadEnableListEvents;
      _listEventsReady = true;
      UpdateStatusLabel();
    }

    private void LoadPrinters()
    {
      _printerBox.Items.Clear();
      string defaultPrinter = string.Empty;
      try
      {
        defaultPrinter = new PrinterSettings().PrinterName ?? string.Empty;
      }
      catch
      {
        defaultPrinter = string.Empty;
      }

      try
      {
        foreach (string name in PrinterSettings.InstalledPrinters)
        {
          if (!string.IsNullOrWhiteSpace(name))
            _printerBox.Items.Add(name);
        }
      }
      catch
      {
      }

      if (_printerBox.Items.Count == 0)
      {
        _printerBox.Enabled = false;
        return;
      }

      int defaultIndex = -1;
      if (!string.IsNullOrWhiteSpace(defaultPrinter))
      {
        for (int i = 0; i < _printerBox.Items.Count; i++)
        {
          if (string.Equals(
                  Convert.ToString(_printerBox.Items[i]),
                  defaultPrinter,
                  StringComparison.OrdinalIgnoreCase))
          {
            defaultIndex = i;
            break;
          }
        }
      }

      _printerBox.SelectedIndex = defaultIndex >= 0 ? defaultIndex : 0;
    }

    private void EnsureCheckedDefaults()
    {
      if (_checkedDefaultsApplied)
        return;

      _checkedRows.Clear();
      for (int i = 0; i < _rows.Count; i++)
      {
        VelumAssemblyRegistryPrintDrawingsService.PrintRow row = _rows[i];
        if (row != null &&
            row.Kind == VelumAssemblyRegistryPrintDrawingsService.PrintRowKind.Ready)
          _checkedRows.Add(row);
      }

      _checkedDefaultsApplied = true;
    }

    private void BindRows()
    {
      EnsureCheckedDefaults();

      var visible = new List<VelumAssemblyRegistryPrintDrawingsService.PrintRow>();
      for (int i = 0; i < _rows.Count; i++)
      {
        VelumAssemblyRegistryPrintDrawingsService.PrintRow row = _rows[i];
        if (row == null)
          continue;
        if (!MatchesFilters(row))
          continue;
        visible.Add(row);
      }

      SortRows(visible);

      _suppressItemCheck = true;
      _suppressSelectAllSync = true;
      _list.BeginUpdate();
      try
      {
        _list.Items.Clear();
        for (int i = 0; i < visible.Count; i++)
        {
          VelumAssemblyRegistryPrintDrawingsService.PrintRow row = visible[i];
          var item = new ListViewItem(row.ComponentDisplayName ?? string.Empty);
          item.SubItems.Add(row.StatusText ?? string.Empty);
          item.SubItems.Add(row.DrawingPath ?? string.Empty);
          item.Tag = row;
          if (!row.CanPrint)
            item.ForeColor = SystemColors.GrayText;
          _list.Items.Add(item);
          item.Checked = _checkedRows.Contains(row);
        }

        ApplySelectAllCheckboxState();
      }
      finally
      {
        _list.EndUpdate();
        _suppressItemCheck = false;
        _suppressSelectAllSync = false;
      }
    }

    private bool MatchesFilters(VelumAssemblyRegistryPrintDrawingsService.PrintRow row)
    {
      if (!VelumListFilterHelper.Matches(row.ComponentDisplayName, _activePositionFilter))
        return false;
      if (!VelumListFilterHelper.Matches(row.StatusText, _activeDrawingFilter))
        return false;
      return true;
    }

    private void SortRows(List<VelumAssemblyRegistryPrintDrawingsService.PrintRow> rows)
    {
      int direction = _sortOrder == SortOrder.Descending ? -1 : 1;
      rows.Sort((a, b) =>
      {
        int cmp = CompareColumn(a, b, _sortColumn);
        if (cmp == 0)
        {
          cmp = string.Compare(
              a.ComponentDisplayName,
              b.ComponentDisplayName,
              StringComparison.CurrentCultureIgnoreCase);
        }

        if (cmp == 0)
        {
          cmp = string.Compare(
              a.DrawingPath,
              b.DrawingPath,
              StringComparison.OrdinalIgnoreCase);
        }

        return cmp * direction;
      });
    }

    private static int CompareColumn(
        VelumAssemblyRegistryPrintDrawingsService.PrintRow a,
        VelumAssemblyRegistryPrintDrawingsService.PrintRow b,
        int column)
    {
      string left = GetColumnText(a, column);
      string right = GetColumnText(b, column);
      return string.Compare(left, right, StringComparison.CurrentCultureIgnoreCase);
    }

    private static string GetColumnText(
        VelumAssemblyRegistryPrintDrawingsService.PrintRow row,
        int column)
    {
      if (row == null)
        return string.Empty;
      switch (column)
      {
        case 1:
          return row.StatusText ?? string.Empty;
        case 2:
          return row.DrawingPath ?? string.Empty;
        default:
          return row.ComponentDisplayName ?? string.Empty;
      }
    }

    private void OnListColumnClick(object sender, ColumnClickEventArgs e)
    {
      if (_printing)
        return;

      if (e.Column == _sortColumn)
      {
        _sortOrder = _sortOrder == SortOrder.Ascending
            ? SortOrder.Descending
            : SortOrder.Ascending;
      }
      else
      {
        _sortColumn = e.Column;
        _sortOrder = SortOrder.Ascending;
      }

      BindRows();
      UpdateStatusLabel();
    }

    private void OnSelectAllCheckedChanged(object sender, EventArgs e)
    {
      if (_suppressSelectAllSync || _printing || _list == null || _list.IsDisposed)
        return;

      _suppressSelectAllSync = true;
      _suppressItemCheck = true;
      _list.ItemCheck -= OnListItemCheck;
      _list.ItemChecked -= OnListItemChecked;
      try
      {
        bool check = _chkSelectAll.Checked;
        for (int i = 0; i < _list.Items.Count; i++)
        {
          ListViewItem item = _list.Items[i];
          if (item == null)
            continue;

          var row = item.Tag as VelumAssemblyRegistryPrintDrawingsService.PrintRow;
          if (row == null ||
              row.Kind != VelumAssemblyRegistryPrintDrawingsService.PrintRowKind.Ready)
          {
            if (row == null || !row.CanPrint)
            {
              item.Checked = false;
              if (row != null)
                _checkedRows.Remove(row);
            }

            continue;
          }

          item.Checked = check;
          if (check)
            _checkedRows.Add(row);
          else
            _checkedRows.Remove(row);
        }
      }
      finally
      {
        _list.ItemCheck += OnListItemCheck;
        _list.ItemChecked += OnListItemChecked;
        _suppressItemCheck = false;
        _suppressSelectAllSync = false;
      }

      UpdateStatusLabel();
    }

    private void OnListItemCheck(object sender, ItemCheckEventArgs e)
    {
      if (!_listEventsReady || _suppressItemCheck || _printing)
      {
        if (_printing)
          e.NewValue = e.CurrentValue;
        return;
      }

      if (_list == null || _list.IsDisposed)
        return;
      if (e.Index < 0 || e.Index >= _list.Items.Count)
        return;

      ListViewItem item = _list.Items[e.Index];
      if (item == null)
      {
        e.NewValue = CheckState.Unchecked;
        return;
      }

      var row = item.Tag as VelumAssemblyRegistryPrintDrawingsService.PrintRow;
      if (row == null || !row.CanPrint)
        e.NewValue = CheckState.Unchecked;
    }

    private void OnListItemChecked(object sender, ItemCheckedEventArgs e)
    {
      if (!_listEventsReady || _suppressSelectAllSync || _printing)
        return;

      if (e != null && e.Item != null)
      {
        var row = e.Item.Tag as VelumAssemblyRegistryPrintDrawingsService.PrintRow;
        if (row != null)
        {
          if (e.Item.Checked && row.CanPrint)
            _checkedRows.Add(row);
          else
            _checkedRows.Remove(row);
        }
      }

      SyncSelectAllFromItems();
      UpdateStatusLabel();
    }

    private void SyncSelectAllFromItems()
    {
      if (_list == null || _list.IsDisposed || _chkSelectAll == null || _chkSelectAll.IsDisposed)
        return;

      ApplySelectAllCheckboxState();
    }

    private void ApplySelectAllCheckboxState()
    {
      if (_list == null || _list.IsDisposed || _chkSelectAll == null || _chkSelectAll.IsDisposed)
        return;

      int ready = 0;
      int checkedReady = 0;
      for (int i = 0; i < _list.Items.Count; i++)
      {
        ListViewItem item = _list.Items[i];
        if (item == null)
          continue;

        var row = item.Tag as VelumAssemblyRegistryPrintDrawingsService.PrintRow;
        if (row == null ||
            row.Kind != VelumAssemblyRegistryPrintDrawingsService.PrintRowKind.Ready)
          continue;
        ready++;
        if (item.Checked)
          checkedReady++;
      }

      bool shouldCheck = ready > 0 && checkedReady == ready;
      if (_chkSelectAll.Checked == shouldCheck)
        return;

      _suppressSelectAllSync = true;
      try
      {
        _chkSelectAll.Checked = shouldCheck;
      }
      finally
      {
        _suppressSelectAllSync = false;
      }
    }

    private void UpdateStatusLabel()
    {
      if (_statusLabel == null || _statusLabel.IsDisposed)
        return;
      if (_rows == null || _list == null || _list.IsDisposed)
        return;

      int ready = 0;
      int missing = 0;
      int ambiguous = 0;
      int skipped = 0;
      int selected = 0;
      for (int i = 0; i < _rows.Count; i++)
      {
        VelumAssemblyRegistryPrintDrawingsService.PrintRow row = _rows[i];
        if (row == null)
          continue;

        switch (row.Kind)
        {
          case VelumAssemblyRegistryPrintDrawingsService.PrintRowKind.Ready:
            ready++;
            break;
          case VelumAssemblyRegistryPrintDrawingsService.PrintRowKind.Missing:
            missing++;
            break;
          case VelumAssemblyRegistryPrintDrawingsService.PrintRowKind.Ambiguous:
            ambiguous++;
            break;
          default:
            skipped++;
            break;
        }

        if (_checkedRows.Contains(row) && row.CanPrint)
          selected++;
      }

      _statusLabel.Text =
          "К печати: " + selected +
          " | В списке: " + _list.Items.Count +
          " | Найдено: " + ready +
          " | Нет чертежа: " + missing +
          (ambiguous > 0 ? " | Несколько файлов: " + ambiguous : string.Empty) +
          (skipped > 0 ? " | Пропуск: " + skipped : string.Empty);
    }

    private void OnPrintClick(object sender, EventArgs e)
    {
      if (_printing)
      {
        _cancelRequested = true;
        return;
      }

      var selected = new List<VelumAssemblyRegistryPrintDrawingsService.PrintRow>();
      for (int i = 0; i < _rows.Count; i++)
      {
        VelumAssemblyRegistryPrintDrawingsService.PrintRow row = _rows[i];
        if (row != null && row.CanPrint && _checkedRows.Contains(row))
          selected.Add(row);
      }

      if (selected.Count == 0)
      {
        MessageBox.Show(
            this,
            "Отметьте хотя бы один чертёж для печати.",
            Text,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      if (_printerBox.SelectedItem == null)
      {
        MessageBox.Show(
            this,
            "Выберите принтер.",
            Text,
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return;
      }

      string printer = Convert.ToString(_printerBox.SelectedItem);
      int copies = (int)_copiesBox.Value;

      _cancelRequested = false;
      _printing = true;
      SetPrintingUi(true);
      Cursor prev = Cursor;
      Cursor = Cursors.WaitCursor;

      VelumAssemblyRegistryPrintDrawingsService.PrintResult result;
      try
      {
        result = VelumAssemblyRegistryPrintDrawingsService.PrintSelected(
            _swApp,
            selected,
            printer,
            copies,
            () => _cancelRequested,
            (current, total, name) =>
            {
              if (_progressBar.Maximum != total)
                _progressBar.Maximum = Math.Max(1, total);
              _progressBar.Value = Math.Min(current, _progressBar.Maximum);
              _statusLabel.Text = "Печать " + current + " / " + total + ": " + name;
              Application.DoEvents();
            });
      }
      finally
      {
        Cursor = prev;
        _printing = false;
        SetPrintingUi(false);
        UpdateStatusLabel();
      }

      string summary =
          "Напечатано: " + result.Printed +
          "\nОшибок: " + result.Failed +
          (result.Cancelled ? "\nПрервано пользователем." : string.Empty);
      if (result.Errors.Count > 0)
      {
        int show = Math.Min(8, result.Errors.Count);
        summary += "\n\n" + string.Join("\n", result.Errors.GetRange(0, show).ToArray());
        if (result.Errors.Count > show)
          summary += "\n… ещё " + (result.Errors.Count - show);
      }

      MessageBox.Show(
          this,
          summary,
          Text,
          MessageBoxButtons.OK,
          result.Failed > 0 || result.Cancelled ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
    }

    private void OnCloseClick(object sender, EventArgs e)
    {
      if (_printing)
      {
        _cancelRequested = true;
        return;
      }

      DialogResult = DialogResult.Cancel;
      Close();
    }

    private void SetPrintingUi(bool printing)
    {
      _btnPrint.Text = printing ? "Прервать" : "Печать";
      _btnClose.Enabled = !printing;
      _chkSelectAll.Enabled = !printing;
      _copiesBox.Enabled = !printing;
      _printerBox.Enabled = !printing && _printerBox.Items.Count > 0;
      _list.Enabled = !printing;
      _positionFilterBox.Enabled = !printing;
      _drawingFilterBox.Enabled = !printing;
      _btnFilterApply.Enabled = !printing;
      _btnFilterReset.Enabled = !printing;
      _btnFilterHelp.Enabled = !printing;
      _progressBar.Visible = printing;
      if (printing)
      {
        _progressBar.Value = 0;
        int count = 0;
        for (int i = 0; i < _rows.Count; i++)
        {
          VelumAssemblyRegistryPrintDrawingsService.PrintRow row = _rows[i];
          if (row != null && row.CanPrint && _checkedRows.Contains(row))
            count++;
        }

        _progressBar.Maximum = Math.Max(1, count);
      }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
      if (_printing)
      {
        _cancelRequested = true;
        e.Cancel = true;
        return;
      }

      base.OnFormClosing(e);
    }

    private static Icon TryLoadFormIcon()
    {
      try
      {
        string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string path = Path.Combine(dir ?? string.Empty, "icons", "Print.png");
        if (!File.Exists(path))
          path = Path.Combine(dir ?? string.Empty, "icons", "Graf.png");
        if (!File.Exists(path))
          return null;

        using (var bitmap = new Bitmap(path))
        {
          IntPtr handle = bitmap.GetHicon();
          using (Icon icon = Icon.FromHandle(handle))
            return (Icon)icon.Clone();
        }
      }
      catch
      {
        return null;
      }
    }
  }
}
