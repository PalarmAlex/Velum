using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Velum.UI.AssemblyRegistry;

namespace Velum.UI
{
  /// <summary>Настройки столбцов реестра изделия: шаблоны и список колонок.</summary>
  internal sealed partial class VelumAssemblyRegistryColumnSettingsForm : Form
  {
    private readonly bool _isAdmin;
    private readonly IList<VelumAssemblyRegistryComponent> _validationComponents;
    private VelumAssemblyRegistryColumnSettingsFile _data;
    private string _currentTemplateName;
    private bool _suppressTemplateEvents;
    private bool _suppressGroupByEvents;
    private bool _suppressSortEvents;
    private bool _suppressOrderEvents;
    private ContextMenuStrip _orderMenu;
    private ToolStripMenuItem _miMoveUp;
    private ToolStripMenuItem _miMoveDown;

    /// <summary>Имя шаблона, которое нужно выбрать на основной форме после Apply.</summary>
    internal string AppliedTemplateName { get; private set; }

    internal VelumAssemblyRegistryColumnSettingsForm(
        string selectedTemplateName,
        IList<VelumAssemblyRegistryComponent> validationComponents,
        bool isAdmin)
    {
      _isAdmin = isAdmin;
      _validationComponents = validationComponents;
      _currentTemplateName = selectedTemplateName;
      InitializeComponent();
      VelumFormHelp.Bind(this, VelumHelpTopics.AssemblyColumns);
      InitializeRuntime();
    }

    private void InitializeRuntime()
    {
      Icon icon = TryLoadFormIcon();
      if (icon != null)
        Icon = icon;

      _data = VelumAssemblyRegistryColumnStore.LoadOrCreate();
      _grid.AllowUserToAddRows = _isAdmin;
      _grid.AllowUserToDeleteRows = _isAdmin;
      _grid.ReadOnly = !_isAdmin;
      _btnApply.Enabled = _isAdmin;
      _btnApply.Visible = _isAdmin;
      _btnCopy.Enabled = _isAdmin;
      _btnCopy.Visible = _isAdmin;
      _btnTemplates.Enabled = true;
      _cmbTemplate.Enabled = true;

      var tip = new ToolTip();
      tip.SetToolTip(_btnCopy, "Копировать текущий шаблон столбцов");
      tip.SetToolTip(_btnTemplates, "Редактировать список шаблонов");
      tip.SetToolTip(_btnValidate, "Проверить формулы столбцов");
      tip.SetToolTip(_btnApply, "Сохранить настройки столбцов");
      tip.SetToolTip(_btnClose, "Закрыть");

      SetupOrderContextMenu();
      _colOrder.ReadOnly = !_isAdmin;

      _grid.CellValueChanged += OnGridCellValueChanged;
      _grid.CellBeginEdit += OnGridCellBeginEdit;
      _grid.CellEndEdit += OnGridCellEndEdit;
      _grid.UserDeletedRow += OnGridUserDeletedRow;
      _grid.DefaultValuesNeeded += OnGridDefaultValuesNeeded;
      _grid.RowsAdded += OnGridRowsAdded;
      _grid.CellDoubleClick += OnGridCellDoubleClick;
      _grid.CellContextMenuStripNeeded += OnGridCellContextMenuStripNeeded;
      _grid.CurrentCellDirtyStateChanged += (s, e) =>
      {
        // Для «№ пп» коммитим только при выходе из ячейки (CellEndEdit), иначе сработает на первом символе.
        if (_grid.IsCurrentCellDirty &&
            (_grid.CurrentCell == null || _grid.CurrentCell.ColumnIndex != _colOrder.Index))
          _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
      };
      _cmbTemplate.SelectedIndexChanged += OnTemplateSelected;
      _btnTemplates.Click += OnEditTemplates;
      _btnCopy.Click += OnCopyTemplate;
      _btnValidate.Click += OnValidate;
      _btnApply.Click += OnApply;
      _btnClose.Click += OnCloseClick;

      ReloadTemplateCombo(_currentTemplateName);
    }

    private void SetupOrderContextMenu()
    {
      _orderMenu = new ContextMenuStrip();
      _miMoveUp = new ToolStripMenuItem("Вверх", TryLoadMenuBitmap("Up.png"), (s, e) => MoveSelectedRow(-1));
      _miMoveDown = new ToolStripMenuItem("Вниз", TryLoadMenuBitmap("Down.png"), (s, e) => MoveSelectedRow(1));
      _orderMenu.Items.Add(_miMoveUp);
      _orderMenu.Items.Add(_miMoveDown);
      _orderMenu.Opening += (s, e) =>
      {
        int index = GetSelectedDataRowIndex();
        _miMoveUp.Enabled = _isAdmin && index > 0;
        _miMoveDown.Enabled = _isAdmin && index >= 0 && index < CountDataRows() - 1;
        if (!_miMoveUp.Enabled && !_miMoveDown.Enabled)
          e.Cancel = true;
      };
    }

    private void OnGridCellContextMenuStripNeeded(object sender, DataGridViewCellContextMenuStripNeededEventArgs e)
    {
      e.ContextMenuStrip = null;
      if (!_isAdmin || e.RowIndex < 0 || e.ColumnIndex != _colOrder.Index)
        return;
      if (_grid.Rows[e.RowIndex].IsNewRow)
        return;
      e.ContextMenuStrip = _orderMenu;
    }

    private int GetSelectedDataRowIndex()
    {
      if (_grid.CurrentCell == null)
        return -1;
      int index = _grid.CurrentCell.RowIndex;
      if (index < 0 || index >= _grid.Rows.Count || _grid.Rows[index].IsNewRow)
        return -1;
      return index;
    }

    private int CountDataRows()
    {
      int count = 0;
      foreach (DataGridViewRow row in _grid.Rows)
      {
        if (!row.IsNewRow)
          count++;
      }

      return count;
    }

    private void MoveSelectedRow(int delta)
    {
      if (!_isAdmin)
        return;

      int index = GetSelectedDataRowIndex();
      if (index < 0)
        return;

      int target = index + delta;
      if (target < 0 || target >= _grid.Rows.Count || _grid.Rows[target].IsNewRow)
        return;

      MoveRowTo(index, target);
    }

    /// <summary>Переносит data-строку на индекс target и нормализует № пп в 1…N.</summary>
    private void MoveRowTo(int sourceIndex, int targetIndex)
    {
      if (!_isAdmin || sourceIndex == targetIndex)
        return;
      if (sourceIndex < 0 || sourceIndex >= _grid.Rows.Count || _grid.Rows[sourceIndex].IsNewRow)
        return;
      if (targetIndex < 0 || targetIndex >= _grid.Rows.Count || _grid.Rows[targetIndex].IsNewRow)
        return;

      DataGridViewRow source = _grid.Rows[sourceIndex];
      object[] values =
      {
        source.Cells[_colOrder.Index].Value,
        source.Cells[_colName.Index].Value,
        source.Cells[_colFormula.Index].Value,
        source.Cells[_colFilter.Index].Value,
        source.Cells[_colTotals.Index].Value,
        source.Cells[_colGroupBy.Index].Value,
        source.Cells[_colSort.Index].Value,
        source.Cells[_colShowInList.Index].Value,
        source.Cells[_colDescription.Index].Value
      };
      string formulaError = source.Cells[_colFormula.Index].ErrorText;
      string totalsError = source.Cells[_colTotals.Index].ErrorText;

      _suppressOrderEvents = true;
      try
      {
        _grid.Rows.RemoveAt(sourceIndex);
        _grid.Rows.Insert(targetIndex, values);
        _grid.Rows[targetIndex].Cells[_colFormula.Index].ErrorText = formulaError;
        _grid.Rows[targetIndex].Cells[_colTotals.Index].ErrorText = totalsError;
        _grid.CurrentCell = _grid.Rows[targetIndex].Cells[_colOrder.Index];
        RenumberOrders();
      }
      finally
      {
        _suppressOrderEvents = false;
      }
    }

    private void RenumberOrders()
    {
      int order = 1;
      foreach (DataGridViewRow row in _grid.Rows)
      {
        if (row.IsNewRow)
          continue;
        row.Cells[_colOrder.Index].Value = order++;
      }
    }

    private void OnGridCellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
    {
      if (!_isAdmin || e.ColumnIndex != _colOrder.Index || e.RowIndex < 0)
        return;
      if (_grid.Rows[e.RowIndex].IsNewRow)
        e.Cancel = true;
    }

    private void OnGridCellEndEdit(object sender, DataGridViewCellEventArgs e)
    {
      if (_suppressOrderEvents || !_isAdmin || e.ColumnIndex != _colOrder.Index || e.RowIndex < 0)
        return;
      if (e.RowIndex >= _grid.Rows.Count || _grid.Rows[e.RowIndex].IsNewRow)
        return;

      // При уходе кликом на другую ячейку DGV ещё меняет CurrentCell — перестановка строк
      // внутри CellEndEdit даёт reentrant SetCurrentCellAddressCore. Enter часто «прокатывает»,
      // клик — нет. Откладываем до завершения смены ячейки.
      int rowIndex = e.RowIndex;
      BeginInvoke(new Action(() =>
      {
        if (IsDisposed || _suppressOrderEvents)
          return;
        if (rowIndex < 0 || rowIndex >= _grid.Rows.Count || _grid.Rows[rowIndex].IsNewRow)
          return;
        ApplyOrderEdit(rowIndex);
      }));
    }

    /// <summary>
    /// Ручной ввод № пп: clamp в [1..N], перенос строки на позицию, затем плотная перенумерация.
    /// </summary>
    private void ApplyOrderEdit(int rowIndex)
    {
      int count = CountDataRows();
      if (count <= 0 || rowIndex < 0 || rowIndex >= _grid.Rows.Count || _grid.Rows[rowIndex].IsNewRow)
        return;

      int oldPos = rowIndex + 1;
      int newPos;
      if (!int.TryParse(Convert.ToString(_grid.Rows[rowIndex].Cells[_colOrder.Index].Value), out newPos))
      {
        RenumberOrders();
        return;
      }

      if (newPos < 1)
        newPos = 1;
      if (newPos > count)
        newPos = count;

      if (newPos == oldPos)
      {
        _suppressOrderEvents = true;
        try
        {
          _grid.Rows[rowIndex].Cells[_colOrder.Index].Value = oldPos;
        }
        finally
        {
          _suppressOrderEvents = false;
        }

        return;
      }

      MoveRowTo(rowIndex, newPos - 1);
    }

    private void OnGridUserDeletedRow(object sender, DataGridViewRowEventArgs e)
    {
      if (!_isAdmin || _suppressOrderEvents)
        return;
      RenumberOrders();
    }

    private void ReloadTemplateCombo(string preferName)
    {
      _suppressTemplateEvents = true;
      try
      {
        _cmbTemplate.Items.Clear();
        foreach (string name in _data.TemplateNames)
          _cmbTemplate.Items.Add(name);

        string select = preferName;
        if (string.IsNullOrWhiteSpace(select) ||
            VelumAssemblyRegistryColumnStore.FindTemplate(_data, select) == null)
          select = VelumAssemblyRegistryColumnStore.ResolveActiveTemplateName(_data);

        int index = -1;
        for (int i = 0; i < _cmbTemplate.Items.Count; i++)
        {
          if (string.Equals(Convert.ToString(_cmbTemplate.Items[i]), select, StringComparison.OrdinalIgnoreCase))
          {
            index = i;
            break;
          }
        }

        if (index < 0 && _cmbTemplate.Items.Count > 0)
          index = 0;

        if (index >= 0)
        {
          _cmbTemplate.SelectedIndex = index;
          _currentTemplateName = Convert.ToString(_cmbTemplate.Items[index]);
        }
        else
          _currentTemplateName = null;
      }
      finally
      {
        _suppressTemplateEvents = false;
      }

      LoadColumnsForCurrentTemplate();
    }

    private void OnTemplateSelected(object sender, EventArgs e)
    {
      if (_suppressTemplateEvents)
        return;

      if (_isAdmin)
        CaptureCurrentTemplateColumns();

      _currentTemplateName = Convert.ToString(_cmbTemplate.SelectedItem);
      LoadColumnsForCurrentTemplate();
    }

    private void LoadColumnsForCurrentTemplate()
    {
      _grid.Rows.Clear();
      VelumAssemblyRegistryColumnTemplate template =
          VelumAssemblyRegistryColumnStore.FindTemplate(_data, _currentTemplateName);
      if (template == null)
        return;

      foreach (VelumAssemblyRegistryColumnDef col in VelumAssemblyRegistryColumnStore.GetOrderedColumns(template))
      {
        _grid.Rows.Add(
            col.Order,
            col.Name ?? string.Empty,
            col.Formula ?? string.Empty,
            col.Filter ?? string.Empty,
            VelumAssemblyRegistryColumnTotals.TotalsKindDisplayName(col.TotalsKind),
            col.GroupBy,
            VelumAssemblyRegistryColumnStore.SortKindDisplayName(col.SortKind),
            col.ShowInList,
            col.Description ?? string.Empty);
      }
    }

    private void CaptureCurrentTemplateColumns()
    {
      if (string.IsNullOrWhiteSpace(_currentTemplateName))
        return;

      VelumAssemblyRegistryColumnTemplate template =
          VelumAssemblyRegistryColumnStore.FindTemplate(_data, _currentTemplateName);
      if (template == null)
      {
        template = new VelumAssemblyRegistryColumnTemplate
        {
          Name = _currentTemplateName.Trim(),
          Columns = new List<VelumAssemblyRegistryColumnDef>()
        };
        _data.Templates.Add(template);
      }

      template.Columns = ReadColumnsFromGrid();
    }

    private List<VelumAssemblyRegistryColumnDef> ReadColumnsFromGrid()
    {
      var result = new List<VelumAssemblyRegistryColumnDef>();
      foreach (DataGridViewRow row in _grid.Rows)
      {
        if (row.IsNewRow)
          continue;

        string name = Convert.ToString(row.Cells[_colName.Index].Value) ?? string.Empty;
        string formula = Convert.ToString(row.Cells[_colFormula.Index].Value) ?? string.Empty;
        string filter = Convert.ToString(row.Cells[_colFilter.Index].Value) ?? string.Empty;
        string totalsText = Convert.ToString(row.Cells[_colTotals.Index].Value) ?? string.Empty;
        bool groupBy = Convert.ToBoolean(row.Cells[_colGroupBy.Index].Value ?? false);
        string sortText = Convert.ToString(row.Cells[_colSort.Index].Value) ?? string.Empty;
        bool showInList = Convert.ToBoolean(row.Cells[_colShowInList.Index].Value ?? true);
        string description = Convert.ToString(row.Cells[_colDescription.Index].Value) ?? string.Empty;
        name = name.Trim();
        formula = formula.Trim();
        filter = filter.Trim();
        description = description.Trim();

        VelumAssemblyRegistryColumnSortKind sortKind;
        if (!VelumAssemblyRegistryColumnStore.TryParseSortKindDisplay(sortText, out sortKind))
          sortKind = VelumAssemblyRegistryColumnSortKind.None;

        if (name.Length == 0 && formula.Length == 0 && filter.Length == 0 && description.Length == 0 &&
            !groupBy && sortKind == VelumAssemblyRegistryColumnSortKind.None)
          continue;

        int order;
        if (!int.TryParse(Convert.ToString(row.Cells[_colOrder.Index].Value), out order))
          order = result.Count + 1;

        VelumAssemblyRegistryColumnTotalsKind totalsKind;
        if (!VelumAssemblyRegistryColumnTotals.TryParseTotalsKindDisplay(totalsText, out totalsKind))
          totalsKind = VelumAssemblyRegistryColumnTotalsKind.None;

        result.Add(new VelumAssemblyRegistryColumnDef
        {
          Order = order,
          Name = name,
          Formula = formula,
          Filter = filter,
          TotalsKind = totalsKind,
          GroupBy = groupBy,
          SortKind = sortKind,
          ShowInList = showInList,
          Description = description
        });
      }

      // Не более одного GroupBy и одной сортировки в шаблоне.
      bool groupSeen = false;
      bool sortSeen = false;
      for (int i = 0; i < result.Count; i++)
      {
        if (result[i].GroupBy)
        {
          if (groupSeen)
            result[i].GroupBy = false;
          else
            groupSeen = true;
        }

        if (result[i].SortKind != VelumAssemblyRegistryColumnSortKind.None)
        {
          if (sortSeen)
            result[i].SortKind = VelumAssemblyRegistryColumnSortKind.None;
          else
            sortSeen = true;
        }
      }

      return result;
    }

    private void OnGridDefaultValuesNeeded(object sender, DataGridViewRowEventArgs e)
    {
      int next = 1;
      foreach (DataGridViewRow row in _grid.Rows)
      {
        if (row.IsNewRow || ReferenceEquals(row, e.Row))
          continue;
        int order;
        if (int.TryParse(Convert.ToString(row.Cells[_colOrder.Index].Value), out order) && order >= next)
          next = order + 1;
      }

      e.Row.Cells[_colOrder.Index].Value = next;
      e.Row.Cells[_colName.Index].Value = string.Empty;
      e.Row.Cells[_colFormula.Index].Value = string.Empty;
      e.Row.Cells[_colFilter.Index].Value = string.Empty;
      e.Row.Cells[_colTotals.Index].Value = "Без итогов";
      e.Row.Cells[_colGroupBy.Index].Value = false;
      e.Row.Cells[_colSort.Index].Value = "Нет";
      e.Row.Cells[_colShowInList.Index].Value = true;
      e.Row.Cells[_colDescription.Index].Value = string.Empty;
    }

    private void OnGridRowsAdded(object sender, DataGridViewRowsAddedEventArgs e)
    {
      // № уже выставляется в DefaultValuesNeeded.
    }

    private void OnGridCellValueChanged(object sender, DataGridViewCellEventArgs e)
    {
      if (!_isAdmin || e.RowIndex < 0)
        return;

      DataGridViewRow row = _grid.Rows[e.RowIndex];
      if (row.IsNewRow)
        return;

      if (e.ColumnIndex == _colFormula.Index)
      {
        string formula = Convert.ToString(row.Cells[_colFormula.Index].Value) ?? string.Empty;
        string error;
        if (!VelumAssemblyRegistryColumnFormula.TryValidateSyntax(formula, out error))
          row.Cells[_colFormula.Index].ErrorText = error ?? "Ошибка формулы";
        else
          row.Cells[_colFormula.Index].ErrorText = string.Empty;
      }

      if (e.ColumnIndex == _colTotals.Index)
        ValidateTotalsCell(row);

      if (e.ColumnIndex == _colGroupBy.Index)
        EnforceSingleGroupBy(e.RowIndex);

      if (e.ColumnIndex == _colSort.Index)
        EnforceSingleSort(e.RowIndex);
    }

    private void EnforceSingleGroupBy(int changedRowIndex)
    {
      if (_suppressGroupByEvents || changedRowIndex < 0 || changedRowIndex >= _grid.Rows.Count)
        return;

      DataGridViewRow changed = _grid.Rows[changedRowIndex];
      if (changed.IsNewRow)
        return;

      bool isChecked = Convert.ToBoolean(changed.Cells[_colGroupBy.Index].Value ?? false);
      if (!isChecked)
        return;

      _suppressGroupByEvents = true;
      try
      {
        foreach (DataGridViewRow row in _grid.Rows)
        {
          if (row.IsNewRow || row.Index == changedRowIndex)
            continue;
          if (Convert.ToBoolean(row.Cells[_colGroupBy.Index].Value ?? false))
            row.Cells[_colGroupBy.Index].Value = false;
        }
      }
      finally
      {
        _suppressGroupByEvents = false;
      }
    }

    private void EnforceSingleSort(int changedRowIndex)
    {
      if (_suppressSortEvents || changedRowIndex < 0 || changedRowIndex >= _grid.Rows.Count)
        return;

      DataGridViewRow changed = _grid.Rows[changedRowIndex];
      if (changed.IsNewRow)
        return;

      VelumAssemblyRegistryColumnSortKind kind;
      string text = Convert.ToString(changed.Cells[_colSort.Index].Value) ?? string.Empty;
      if (!VelumAssemblyRegistryColumnStore.TryParseSortKindDisplay(text, out kind) ||
          kind == VelumAssemblyRegistryColumnSortKind.None)
        return;

      _suppressSortEvents = true;
      try
      {
        foreach (DataGridViewRow row in _grid.Rows)
        {
          if (row.IsNewRow || row.Index == changedRowIndex)
            continue;
          row.Cells[_colSort.Index].Value = "Нет";
        }
      }
      finally
      {
        _suppressSortEvents = false;
      }
    }

    private void ValidateTotalsCell(DataGridViewRow row)
    {
      if (row == null || row.IsNewRow)
        return;

      string totalsText = Convert.ToString(row.Cells[_colTotals.Index].Value) ?? string.Empty;
      VelumAssemblyRegistryColumnTotalsKind kind;
      if (!VelumAssemblyRegistryColumnTotals.TryParseTotalsKindDisplay(totalsText, out kind) ||
          kind == VelumAssemblyRegistryColumnTotalsKind.None)
      {
        row.Cells[_colTotals.Index].ErrorText = string.Empty;
        return;
      }

      var probe = new VelumAssemblyRegistryColumnDef
      {
        Name = Convert.ToString(row.Cells[_colName.Index].Value) ?? string.Empty,
        Formula = Convert.ToString(row.Cells[_colFormula.Index].Value) ?? string.Empty,
        TotalsKind = kind
      };

      if (!VelumAssemblyRegistryColumnTotals.LooksNumericForTotals(probe, _validationComponents))
      {
        row.Cells[_colTotals.Index].ErrorText =
            "Столбец не выглядит числовым — итог на форме/в отчёте будет пустым";
        row.Cells[_colTotals.Index].Value = "Без итогов";
        row.Cells[_colTotals.Index].ErrorText = string.Empty;
        MessageBox.Show(
            this,
            "Итоги можно задать только для числовых столбцов.\nВыбор сброшен на «Без итогов».",
            Text,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
      }
      else
        row.Cells[_colTotals.Index].ErrorText = string.Empty;
    }

    private void OnGridCellDoubleClick(object sender, DataGridViewCellEventArgs e)
    {
      if (!_isAdmin || e.RowIndex < 0 || e.ColumnIndex != _colFormula.Index)
        return;

      DataGridViewRow row = _grid.Rows[e.RowIndex];
      if (row.IsNewRow)
        return;

      using (var helper = new VelumAssemblyRegistryFormulaHelperForm())
      {
        if (helper.ShowDialog(this) != DialogResult.OK)
          return;
        if (string.IsNullOrEmpty(helper.SelectedInsertText))
          return;

        string current = Convert.ToString(row.Cells[_colFormula.Index].Value) ?? string.Empty;
        if (current.Length == 0)
          row.Cells[_colFormula.Index].Value = helper.SelectedInsertText;
        else
          row.Cells[_colFormula.Index].Value = current + helper.SelectedInsertText;
      }
    }

    private void OnEditTemplates(object sender, EventArgs e)
    {
      if (_isAdmin)
        CaptureCurrentTemplateColumns();

      using (var form = new VelumAssemblyRegistryTemplatesForm(_data, _isAdmin, _currentTemplateName))
      {
        if (form.ShowDialog(this) != DialogResult.OK)
          return;
        _currentTemplateName = form.ResolvedPreferTemplateName ?? _currentTemplateName;
      }

      ReloadTemplateCombo(_currentTemplateName);
    }

    private void OnCopyTemplate(object sender, EventArgs e)
    {
      if (!_isAdmin)
        return;

      CaptureCurrentTemplateColumns();
      VelumAssemblyRegistryColumnTemplate source =
          VelumAssemblyRegistryColumnStore.FindTemplate(_data, _currentTemplateName);
      if (source == null)
      {
        MessageBox.Show(
            this,
            "Нет текущего шаблона для копирования.",
            Text,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      string newName = VelumAssemblyRegistryColumnStore.AllocateUniqueTemplateName(_data, "Новая сводка");
      var copy = new VelumAssemblyRegistryColumnTemplate
      {
        Name = newName,
        Columns = VelumAssemblyRegistryColumnStore.CloneColumns(source.Columns)
      };
      _data.Templates.Add(copy);
      if (_data.TemplateNames == null)
        _data.TemplateNames = new List<string>();
      if (!_data.TemplateNames.Exists(n => string.Equals(n, newName, StringComparison.OrdinalIgnoreCase)))
        _data.TemplateNames.Add(newName);

      ReloadTemplateCombo(newName);
    }

    private void OnValidate(object sender, EventArgs e)
    {
      if (_isAdmin)
        CaptureCurrentTemplateColumns();

      List<VelumAssemblyRegistryColumnDef> columns = ReadColumnsFromGrid();
      foreach (VelumAssemblyRegistryColumnDef col in columns)
      {
        string formula = (col.Formula ?? string.Empty).Trim();
        if (formula.Length == 0)
          continue;
        string error;
        if (!VelumAssemblyRegistryColumnFormula.TryValidateSyntax(formula, out error))
        {
          MessageBox.Show(
              this,
              "Синтаксис столбца «" + VelumAssemblyRegistryColumnStore.CaptionFromName(col.Name) + "»:\n" +
              (error ?? "ошибка"),
              Text,
              MessageBoxButtons.OK,
              MessageBoxIcon.Warning);
          return;
        }
      }

      if (_validationComponents == null || _validationComponents.Count == 0)
      {
        MessageBox.Show(
            this,
            "Нет кэша состава для проверки. Откройте реестр изделия и дождитесь загрузки сборки.",
            Text,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      List<VelumAssemblyRegistryColumnResolver.ValidationHit> hits =
          VelumAssemblyRegistryColumnResolver.ValidateAgainstCache(columns, _validationComponents, 80);

      if (hits.Count == 0)
      {
        MessageBox.Show(this, "Все ОК.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        return;
      }

      var sb = new StringBuilder();
      sb.AppendLine("Найдено ошибок: " + hits.Count + (hits.Count >= 80 ? " (показаны первые 80)" : ""));
      sb.AppendLine();
      foreach (VelumAssemblyRegistryColumnResolver.ValidationHit hit in hits)
      {
        sb.Append(hit.ComponentLabel);
        sb.Append(" | ");
        sb.Append(hit.ColumnCaption);
        sb.Append(" | ");
        sb.AppendLine(hit.Detail);
      }

      MessageBox.Show(this, sb.ToString(), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void OnApply(object sender, EventArgs e)
    {
      if (!_isAdmin)
        return;

      CaptureCurrentTemplateColumns();

      foreach (VelumAssemblyRegistryColumnTemplate template in _data.Templates)
      {
        if (template?.Columns == null)
          continue;

        var orders = new HashSet<int>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (VelumAssemblyRegistryColumnDef col in template.Columns)
        {
          if (string.IsNullOrWhiteSpace(col.Name))
          {
            MessageBox.Show(
                this,
                "В шаблоне «" + template.Name + "» есть строка без имени.",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
          }

          if (!orders.Add(col.Order))
          {
            MessageBox.Show(
                this,
                "В шаблоне «" + template.Name + "» повторяется № пп: " + col.Order,
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
          }

          if (!names.Add(col.Name.Trim()))
          {
            MessageBox.Show(
                this,
                "В шаблоне «" + template.Name + "» повторяется имя: " + col.Name,
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
          }

          string formula = (col.Formula ?? string.Empty).Trim();
          if (formula.Length > 0)
          {
            string error;
            if (!VelumAssemblyRegistryColumnFormula.TryValidateSyntax(formula, out error))
            {
              MessageBox.Show(
                  this,
                  "Шаблон «" + template.Name + "», столбец «" + col.Name + "»:\n" + (error ?? "ошибка формулы"),
                  Text,
                  MessageBoxButtons.OK,
                  MessageBoxIcon.Warning);
              return;
            }
          }

          if (col.TotalsKind != VelumAssemblyRegistryColumnTotalsKind.None &&
              !VelumAssemblyRegistryColumnTotals.LooksNumericForTotals(col, _validationComponents))
          {
            MessageBox.Show(
                this,
                "Шаблон «" + template.Name + "»: итоги для столбца «" +
                VelumAssemblyRegistryColumnStore.CaptionFromName(col.Name) +
                "» недоступны — столбец не выглядит числовым.",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
          }
        }
      }

      try
      {
        VelumAssemblyRegistryColumnStore.Save(_data);
      }
      catch (Exception ex)
      {
        MessageBox.Show(
            this,
            "Не удалось сохранить настройки:\n" + ex.Message,
            Text,
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
        return;
      }

      AppliedTemplateName = _currentTemplateName;
      if (!string.IsNullOrWhiteSpace(AppliedTemplateName))
        VelumAssemblyRegistryColumnStore.SetLastTemplateName(AppliedTemplateName);

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
      try
      {
        string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (string.IsNullOrEmpty(dir))
          return null;
        string path = Path.Combine(dir, "icons", "velum.ico");
        return File.Exists(path) ? new Icon(path) : null;
      }
      catch
      {
        return null;
      }
    }

    private static Image TryLoadMenuBitmap(string fileName)
    {
      try
      {
        string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string path = Path.Combine(dir ?? string.Empty, "icons", fileName);
        if (!File.Exists(path))
          return null;
        return Image.FromFile(path);
      }
      catch
      {
        return null;
      }
    }
  }
}
