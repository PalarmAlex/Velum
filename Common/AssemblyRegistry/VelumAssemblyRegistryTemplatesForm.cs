using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Velum.UI.AssemblyRegistry;

namespace Velum.UI
{
  /// <summary>Справочник имён шаблонов столбцов реестра изделия.</summary>
  internal sealed partial class VelumAssemblyRegistryTemplatesForm : Form
  {
    private readonly VelumAssemblyRegistryColumnSettingsFile _data;
    private readonly bool _isAdmin;
    private readonly string _preferName;
    private readonly Dictionary<string, string> _renameMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private string _filterText;

    /// <summary>Имя шаблона после Apply с учётом переименования текущего.</summary>
    internal string ResolvedPreferTemplateName { get; private set; }

    internal VelumAssemblyRegistryTemplatesForm(
        VelumAssemblyRegistryColumnSettingsFile data,
        bool isAdmin,
        string preferName = null)
    {
      _data = data ?? new VelumAssemblyRegistryColumnSettingsFile();
      _isAdmin = isAdmin;
      _preferName = preferName;
      InitializeComponent();
      InitializeRuntime();
    }

    private void InitializeRuntime()
    {
      Icon icon = TryLoadFormIcon();
      if (icon != null)
        Icon = icon;

      var tip = new ToolTip();
      tip.SetToolTip(_btnApply, "Сохранить список шаблонов");
      tip.SetToolTip(_btnClose, "Закрыть");
      tip.SetToolTip(_txtFilter, "Фильтр по имени шаблона");

      _grid.AllowUserToAddRows = _isAdmin;
      _grid.AllowUserToDeleteRows = _isAdmin;
      _grid.ReadOnly = !_isAdmin;
      _btnApply.Enabled = _isAdmin;
      _btnApply.Visible = _isAdmin;

      _grid.Rows.Clear();
      foreach (string name in _data.TemplateNames ?? new List<string>())
      {
        if (string.IsNullOrWhiteSpace(name))
          continue;
        string trimmed = name.Trim();
        int index = _grid.Rows.Add(trimmed);
        _grid.Rows[index].Tag = trimmed;
      }

      _txtFilter.TextChanged += OnFilterTextChanged;
      _grid.UserDeletingRow += OnGridUserDeletingRow;
      _grid.CellDoubleClick += OnGridCellDoubleClick;

      _filterText = string.Empty;
    }

    private void OnFilterTextChanged(object sender, EventArgs e)
    {
      _filterText = (_txtFilter.Text ?? string.Empty).Trim().ToLowerInvariant();
      foreach (DataGridViewRow row in _grid.Rows)
      {
        if (row.IsNewRow)
          continue;
        string name = Convert.ToString(row.Cells[_colName.Index].Value) ?? string.Empty;
        row.Visible = string.IsNullOrEmpty(_filterText) ||
                      name.IndexOf(_filterText, StringComparison.OrdinalIgnoreCase) >= 0;
      }
    }

    private void OnGridUserDeletingRow(object sender, DataGridViewRowCancelEventArgs e)
    {
      if (!_isAdmin)
        return;

      string name = Convert.ToString(e.Row.Cells[_colName.Index].Value) ?? string.Empty;
      string msg = "Удалить шаблон «" + name + "»?\n\n" +
                   "Изменения вступят в силу при нажатии кнопки «Применить».";
      var result = MessageBox.Show(
          this,
          msg,
          Text,
          MessageBoxButtons.YesNo,
          MessageBoxIcon.Question);
      if (result != DialogResult.Yes)
      {
        e.Cancel = true;
      }
    }

    private void OnGridCellDoubleClick(object sender, DataGridViewCellEventArgs e)
    {
      if (e.RowIndex < 0 || _grid.Rows[e.RowIndex].IsNewRow)
        return;

      _grid.ClearSelection();
      _grid.Rows[e.RowIndex].Selected = true;
      ResolvedPreferTemplateName = Convert.ToString(_grid.Rows[e.RowIndex].Cells[_colName.Index].Value);
      DialogResult = DialogResult.OK;
      Close();
    }

    private void OnApply(object sender, EventArgs e)
    {
      if (!_isAdmin)
        return;

      _renameMap.Clear();
      var names = new List<string>();
      var kept = new List<VelumAssemblyRegistryColumnTemplate>();
      var claimed = new HashSet<VelumAssemblyRegistryColumnTemplate>();

      foreach (DataGridViewRow row in _grid.Rows)
      {
        if (row.IsNewRow)
          continue;

        string name = Convert.ToString(row.Cells[_colName.Index].Value) ?? string.Empty;
        name = name.Trim();
        if (name.Length == 0)
          continue;

        if (names.Exists(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)))
        {
          MessageBox.Show(this, "Имя шаблона повторяется: " + name, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
          return;
        }

        names.Add(name);

        string originalName = Convert.ToString(row.Tag) ?? string.Empty;
        originalName = originalName.Trim();

        VelumAssemblyRegistryColumnTemplate existing = null;
        if (originalName.Length > 0)
          existing = VelumAssemblyRegistryColumnStore.FindTemplate(_data, originalName);

        // На случай если Tag потерян, но имя не меняли.
        if (existing == null)
          existing = VelumAssemblyRegistryColumnStore.FindTemplate(_data, name);

        if (existing != null && claimed.Add(existing))
        {
          if (originalName.Length > 0)
            _renameMap[originalName] = name;
          existing.Name = name;
          kept.Add(existing);
        }
        else
        {
          kept.Add(new VelumAssemblyRegistryColumnTemplate
          {
            Name = name,
            Columns = new List<VelumAssemblyRegistryColumnDef>()
          });
        }
      }

      if (names.Count == 0)
      {
        MessageBox.Show(this, "Нужен хотя бы один шаблон.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return;
      }

      _data.TemplateNames = names;
      _data.Templates = kept;
      ResolvedPreferTemplateName = ResolvePreferName(_preferName);
      DialogResult = DialogResult.OK;
      Close();
    }

    private string ResolvePreferName(string previous)
    {
      if (string.IsNullOrWhiteSpace(previous))
        return _data.Templates.Count > 0 ? _data.Templates[0].Name : null;

      string mapped;
      if (_renameMap.TryGetValue(previous.Trim(), out mapped) && !string.IsNullOrWhiteSpace(mapped))
        return mapped;

      if (VelumAssemblyRegistryColumnStore.FindTemplate(_data, previous) != null)
        return VelumAssemblyRegistryColumnStore.FindTemplate(_data, previous).Name;

      return _data.Templates.Count > 0 ? _data.Templates[0].Name : null;
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
  }
}
