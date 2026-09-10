using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace Velum.UI.AssemblyRegistry
{
  /// <summary>
  /// Форма редактирования единого списка отслеживаемых свойств (tracked properties),
  /// значения которых выгружаются в 1С. Сохраняет список в bomTrackedProperties.json.
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
    /// Загрузить единый список свойств в таблицу.
    /// </summary>
    private void LoadList()
    {
      _grid.Rows.Clear();
      IReadOnlyList<TrackedProperty> props = VelumAssemblyBomTrackedPropertiesConfig.Load();
      foreach (var prop in props)
      {
        int rowIdx = _grid.Rows.Add();
        _grid.Rows[rowIdx].Cells[0].Value = prop.Name;
        _grid.Rows[rowIdx].Cells[1].Value = prop.Precision;
      }
    }

    /// <summary>
    /// Фильтр ввода редактора ячейки: в «Точность» пропускаем только цифры.
    /// </summary>
    private void OnGridEditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
    {
      TextBox editor = e.Control as TextBox;
      if (editor == null)
        return;

      // Редактор переиспользуется таблицей — сначала отписываемся, чтобы обработчик не накапливался.
      editor.KeyPress -= OnPrecisionKeyPress;

      DataGridViewCell current = _grid.CurrentCell;
      if (current != null && current.ColumnIndex == _colPrecision.Index)
        editor.KeyPress += OnPrecisionKeyPress;
    }

    /// <summary>
    /// Клавиши столбца «Точность»: цифры и управляющие клавиши (Backspace, вставка), остальное отсекаем.
    /// </summary>
    private static void OnPrecisionKeyPress(object sender, KeyPressEventArgs e)
    {
      if (char.IsControl(e.KeyChar) || char.IsDigit(e.KeyChar))
        return;

      e.Handled = true;
    }

    /// <summary>
    /// Проверка значения «Точность» при выходе из ячейки: только целые числа допустимого диапазона.
    /// </summary>
    private void OnGridCellValidating(object sender, DataGridViewCellValidatingEventArgs e)
    {
      if (e.RowIndex < 0 || e.ColumnIndex != _colPrecision.Index)
        return;

      if (_grid.Rows[e.RowIndex].IsNewRow)
        return;

      string text = (e.FormattedValue == null ? string.Empty : e.FormattedValue.ToString()).Trim();

      // Пустое значение допустимо — означает «без округления» (0), подставляется при выходе из ячейки.
      if (text.Length == 0)
        return;

      int precision;
      if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out precision) &&
          precision >= VelumAssemblyBomTrackedPropertiesConfig.MinPrecision &&
          precision <= VelumAssemblyBomTrackedPropertiesConfig.MaxPrecision)
        return;

      MessageBox.Show(
          this,
          "В столбце «Точность» допускаются только целые числа от " +
              VelumAssemblyBomTrackedPropertiesConfig.MinPrecision + " до " +
              VelumAssemblyBomTrackedPropertiesConfig.MaxPrecision +
              ".\n\nВведите количество знаков после запятой или оставьте поле пустым.",
          Text,
          MessageBoxButtons.OK,
          MessageBoxIcon.Warning);

      e.Cancel = true;
    }

    /// <summary>
    /// Завершение ввода: пустое значение точности заменяем на 0.
    /// </summary>
    private void OnGridCellEndEdit(object sender, DataGridViewCellEventArgs e)
    {
      if (e.RowIndex < 0 || e.ColumnIndex != _colPrecision.Index)
        return;

      DataGridViewRow row = _grid.Rows[e.RowIndex];
      if (row.IsNewRow)
        return;

      object value = row.Cells[e.ColumnIndex].Value;
      string text = (value == null ? string.Empty : value.ToString()).Trim();
      if (text.Length == 0)
        row.Cells[e.ColumnIndex].Value = VelumAssemblyBomTrackedPropertiesConfig.MinPrecision;
    }

    /// <summary>
    /// Обработка нажатия клавиши Delete для удаления выбранных строк.
    /// </summary>
    private void OnDataGridViewKeyDown(object sender, KeyEventArgs e)
    {
      if (e.KeyCode != Keys.Delete)
        return;

      if (_grid.SelectedRows.Count > 0)
      {
        DeleteSelectedRows();
        e.SuppressKeyPress = true;
      }
    }

    /// <summary>
    /// Открытие контекстного меню: разрешаем только при выделенной строке.
    /// </summary>
    private void OnContextMenuStripOpening(object sender, System.ComponentModel.CancelEventArgs e)
    {
      if (_grid.SelectedRows.Count == 0)
        e.Cancel = true;
    }

    /// <summary>
    /// Удалить выбранную строку таблицы.
    /// </summary>
    private void OnDeleteClick(object sender, EventArgs e)
    {
      DeleteSelectedRows();
    }

    /// <summary>
    /// Удалить выделенные строки таблицы после подтверждения.
    /// </summary>
    private void DeleteSelectedRows()
    {
      if (_grid.SelectedRows.Count == 0)
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
      var indices = _grid.SelectedRows.Cast<DataGridViewRow>()
          .Select(r => r.Index)
          .OrderByDescending(i => i)
          .ToList();

      foreach (int index in indices)
      {
        _grid.Rows.RemoveAt(index);
      }
    }

    /// <summary>
    /// Сохранить список свойств и закрыть форму.
    /// </summary>
    private void OnOkClick(object sender, EventArgs e)
    {
      VelumAssemblyBomTrackedPropertiesConfig.Save(CollectProperties());
      DialogResult = DialogResult.OK;
      Close();
    }

    /// <summary>
    /// Собрать список свойств из таблицы.
    /// </summary>
    private List<TrackedProperty> CollectProperties()
    {
      var props = new List<TrackedProperty>();
      foreach (DataGridViewRow row in _grid.Rows)
      {
        if (row.IsNewRow)
          continue;

        // Значение ячейки может быть числом (после загрузки из конфига) — приводим через ToString.
        string name = row.Cells[0].Value?.ToString()?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name))
          continue;

        int precision = VelumAssemblyBomTrackedPropertiesConfig.MinPrecision;
        string precisionText = row.Cells[1].Value?.ToString()?.Trim() ?? string.Empty;
        if (int.TryParse(precisionText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
          precision = VelumAssemblyBomTrackedPropertiesConfig.ClampPrecision(parsed);

        if (!props.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
          props.Add(new TrackedProperty { Name = name, Precision = precision });
      }
      return props;
    }
  }
}
