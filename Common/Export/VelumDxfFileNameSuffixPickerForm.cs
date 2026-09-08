using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;
using Velum.ReactiveCore.Export;

namespace Velum.UI
{
  /// <summary>
  /// Выбор суффикса имени DXF из списка в Settings.xml.
  /// При открытии с деталью новые имена свойств добавляются в настройки (без удаления старых).
  /// </summary>
  internal sealed partial class VelumDxfFileNameSuffixPickerForm : Form
  {
    private readonly List<string> _allSuffixes = new List<string>();
    private string _activeFilter = string.Empty;

    public string SelectedSuffixName { get; private set; }

    /// <summary>Конструктор для конструктора форм Visual Studio.</summary>
    public VelumDxfFileNameSuffixPickerForm()
    {
      InitializeComponent();
      VelumFormHelp.Bind(this, VelumHelpTopics.DxfSuffixes);
      InitializeRuntime(null);
    }

    public VelumDxfFileNameSuffixPickerForm(ModelDoc2 modelDoc)
    {
      InitializeComponent();
      VelumFormHelp.Bind(this, VelumHelpTopics.DxfSuffixes);
      InitializeRuntime(modelDoc);
    }

    private void InitializeRuntime(ModelDoc2 modelDoc)
    {
      Icon icon = TryLoadVelumWindowIcon();
      if (icon != null)
        Icon = icon;

      var tip = new ToolTip();
      tip.SetToolTip(_btnFilterApply, "Применить фильтр");
      tip.SetToolTip(_btnFilterReset, "Очистить фильтр");
      tip.SetToolTip(_btnFilterHelp, "Справка по маскам фильтра");
      tip.SetToolTip(_btnOk, "Вставить выбранный суффикс");
      tip.SetToolTip(_btnCancel, "Отменить");

      if (modelDoc != null)
        VelumDxfFileNameSuffixRegistry.TryMergeFromPartProperties(modelDoc);

      // [Quantity]/[Кол-во] не в маске: кол-во добавляется флажком на batch-форме.
      foreach (string suffix in VelumDxfFileNameSuffixRegistry.GetSuffixes())
      {
        if (VelumDxfQuantityToken.IsQuantityToken(suffix))
          continue;
        _allSuffixes.Add(suffix);
      }

      _btnFilterApply.Click += (s, e) => ApplyFilterFromUi();
      _btnFilterReset.Click += (s, e) =>
      {
        _txtFilter.Text = string.Empty;
        _activeFilter = string.Empty;
        RebuildFilteredList();
      };
      _btnFilterHelp.Click += (s, e) => VelumListFilterHelper.ShowHelp(this);
      _txtFilter.KeyDown += (s, e) =>
      {
        if (e.KeyCode != Keys.Enter)
          return;
        ApplyFilterFromUi();
        e.Handled = true;
        e.SuppressKeyPress = true;
      };

      RebuildFilteredList();
    }

    private void ApplyFilterFromUi()
    {
      _activeFilter = (_txtFilter.Text ?? string.Empty).Trim();
      RebuildFilteredList();
    }

    private void RebuildFilteredList()
    {
      string filter = _activeFilter;
      string selected = GetCurrentRowSuffix();

      _grid.Rows.Clear();

      if (_allSuffixes.Count == 0)
      {
        _grid.Rows.Add("(нет суффиксов)");
        return;
      }

      int selectIndex = -1;
      for (int i = 0; i < _allSuffixes.Count; i++)
      {
        string suffix = _allSuffixes[i];
        if (!VelumListFilterHelper.Matches(suffix, filter))
          continue;

        int rowIndex = _grid.Rows.Add(suffix);
        if (selectIndex < 0 &&
            !string.IsNullOrEmpty(selected) &&
            string.Equals(suffix, selected, StringComparison.OrdinalIgnoreCase))
        {
          selectIndex = rowIndex;
        }
      }

      if (_grid.Rows.Count == 0)
      {
        _grid.Rows.Add("(нет совпадений)");
        return;
      }

      if (selectIndex >= 0 && selectIndex < _grid.Rows.Count)
        _grid.CurrentCell = _grid.Rows[selectIndex].Cells[0];
      else if (_grid.Rows.Count > 0)
        _grid.CurrentCell = _grid.Rows[0].Cells[0];
    }

    private string GetCurrentRowSuffix()
    {
      if (_grid.CurrentRow == null || _grid.CurrentRow.IsNewRow)
        return string.Empty;

      string name = Convert.ToString(_grid.CurrentRow.Cells[0].Value) ?? string.Empty;
      if (string.IsNullOrWhiteSpace(name) || name.StartsWith("(", StringComparison.Ordinal))
        return string.Empty;

      return name.Trim();
    }

    private void Grid_DoubleClick(object sender, EventArgs e)
    {
      AcceptSelection();
    }

    private void Grid_KeyDown(object sender, KeyEventArgs e)
    {
      if (e.KeyCode == Keys.Enter)
        AcceptSelection();
    }

    private void BtnOk_Click(object sender, EventArgs e)
    {
      AcceptSelection();
    }

    private void AcceptSelection()
    {
      string name = GetCurrentRowSuffix();
      if (string.IsNullOrWhiteSpace(name))
        return;

      SelectedSuffixName = name;
      DialogResult = DialogResult.OK;
      Close();
    }

    private static Icon TryLoadVelumWindowIcon()
    {
      return VelumFormIcon.TryLoad();
    }
  }
}
