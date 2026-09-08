using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Velum.UI.AssemblyRegistry
{
  /// <summary>
  /// Форма редактирования списка отслеживаемых свойств (tracked properties)
  /// для зеркалирования BOM. Сохраняет список в bomTrackedProperties.json.
  /// </summary>
  internal sealed partial class VelumBomTrackedPropertiesEditForm : Form
  {
    public VelumBomTrackedPropertiesEditForm()
    {
      InitializeComponent();
      VelumFormIcon.Apply(this);
      LoadList();
    }

    /// <summary>
    /// Загрузить список свойств в DataGridView.
    /// </summary>
    private void LoadList()
    {
      _dataGridView.Rows.Clear();
      IReadOnlyList<string> props = VelumAssemblyBomTrackedPropertiesConfig.Load();
      foreach (string prop in props)
      {
        int rowIdx = _dataGridView.Rows.Add();
        _dataGridView.Rows[rowIdx].Cells[0].Value = prop;
      }
    }

    /// <summary>
    /// Обработка нажатия клавиши Delete для удаления выбранных строк.
    /// </summary>
    private void OnDataGridViewKeyDown(object sender, KeyEventArgs e)
    {
      if (e.KeyCode == Keys.Delete && _dataGridView.SelectedRows.Count > 0)
      {
        OnDeleteClick(sender, e);
        e.SuppressKeyPress = true;
      }
    }

    /// <summary>
    /// Удалить выбранные строки после подтверждения.
    /// </summary>
    private void OnDeleteClick(object sender, EventArgs e)
    {
      if (_dataGridView.SelectedRows.Count == 0)
        return;

      var result = MessageBox.Show(
          this,
          "Удалить выбранное свойство?",
          Text,
          MessageBoxButtons.OKCancel,
          MessageBoxIcon.Question);

      if (result != DialogResult.OK)
        return;

      // Собрать индексы для удаления (в обратном порядке, чтобы не сбить индексы).
      var indices = _dataGridView.SelectedRows.Cast<System.Windows.Forms.DataGridViewRow>()
          .Select(r => r.Index)
          .OrderByDescending(i => i)
          .ToList();

      foreach (int index in indices)
      {
        _dataGridView.Rows.RemoveAt(index);
      }
    }

    /// <summary>
    /// Сохранить список свойств и закрыть форму.
    /// </summary>
    private void OnOkClick(object sender, EventArgs e)
    {
      var props = new List<string>();
      foreach (System.Windows.Forms.DataGridViewRow row in _dataGridView.Rows)
      {
        if (row.IsNewRow)
          continue;
        string name = (row.Cells[0].Value as string ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(name) && !props.Contains(name))
          props.Add(name);
      }

      VelumAssemblyBomTrackedPropertiesConfig.Save(props);
      DialogResult = DialogResult.OK;
      Close();
    }
  }
}
