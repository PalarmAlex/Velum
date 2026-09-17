using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Velum.UI;

namespace Velum.UI.AssemblyRegistry
{
  /// <summary>
  /// Форма настройки состава, порядка и заголовков полей выгрузки BOM в 1С
  /// (<c>bomExchangeLayout.json</c>). Вкладка «Поля выгрузки» редактирует колонки,
  /// вкладка «Отслеживаемые свойства» открывает редактор свойств и показывает предпросмотр.
  /// </summary>
  internal sealed partial class VelumBomExchangeLayoutForm : Form
  {
    /// <summary>Минимальная ширина колонки выгрузки, пикселей.</summary>
    private const int MinColumnWidth = 40;

    /// <summary>Максимальная ширина колонки выгрузки, пикселей.</summary>
    private const int MaxColumnWidth = 1000;

    /// <summary>Редактируемый layout (рабочая копия).</summary>
    private VelumBomExchangeLayoutFile _data;

    /// <summary>Признак программной загрузки грида (отключает обработчики).</summary>
    private bool _loading;

    /// <summary>Отложенные правки точности отслеживаемых свойств (имя → точность).</summary>
    private readonly Dictionary<string, int> _precisionEdits =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Текущая точность свойств (из конфига с наложенными правками) для отображения.</summary>
    private Dictionary<string, int> _precisionMap =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    public VelumBomExchangeLayoutForm()
    {
      InitializeComponent();
      VelumFormIcon.Apply(this);
    }

    /// <summary>Загрузить layout и заполнить интерфейс.</summary>
    /// <param name="e">Аргументы события.</param>
    protected override void OnLoad(EventArgs e)
    {
      base.OnLoad(e);
      _data = VelumBomExchangeLayoutStore.LoadOrCreate();
      ReloadFieldsGrid();
      ReloadTrackedPreview();
    }

    /// <summary>Получить описание колонки из строки грида.</summary>
    /// <param name="row">Строка грида.</param>
    /// <returns>Описание колонки или <c>null</c>.</returns>
    private static VelumBomExchangeColumnDef ColOf(DataGridViewRow row)
    {
      return row?.Tag as VelumBomExchangeColumnDef;
    }

    /// <summary>
    /// Пересобрать грид колонок из <see cref="_data"/> (в порядке следования).
    /// </summary>
    private void ReloadFieldsGrid()
    {
      RebuildPrecisionMap();

      _loading = true;
      try
      {
        _gridFields.Rows.Clear();
        foreach (VelumBomExchangeColumnDef col in _data.Columns)
        {
          int idx = _gridFields.Rows.Add();
          DataGridViewRow row = _gridFields.Rows[idx];

          row.Cells[_colEnabled.Index].Value = col.Enabled;
          row.Cells[_colField.Index].Value = col.Field;
          row.Cells[_colSource.Index].Value =
              col.Source == VelumBomExchangeFieldSource.Structural ? "Системное" : "Свойство";
          row.Cells[_colHeader.Index].Value = col.Header;
          row.Cells[_colPrecision.Index].Value = ResolvePrecisionText(col);
          row.Cells[_colWidth.Index].Value = col.Width;

          bool structural = col.Source == VelumBomExchangeFieldSource.Structural;
          bool mandatory = structural && VelumBomExchangeLayoutStore.IsMandatory(col.Field);

          // Поле и Источник — всегда только для чтения.
          row.Cells[_colField.Index].ReadOnly = true;
          row.Cells[_colSource.Index].ReadOnly = true;

          // Заголовок в CSV: правится только у отслеживаемых свойств.
          row.Cells[_colHeader.Index].ReadOnly = structural;

          // Точность и ширина — только для чтения (точность правится в редакторе свойств).
          row.Cells[_colPrecision.Index].ReadOnly = true;
          row.Cells[_colWidth.Index].ReadOnly = true;

          // Чекбокс «Вкл»: у обязательных structural всегда включён и не редактируется.
          if (mandatory)
            row.Cells[_colEnabled.Index].ReadOnly = true;

          // Визуально затемняем structural-строки.
          if (structural)
            row.DefaultCellStyle.BackColor = SystemColors.ControlLight;

          row.Tag = col;
        }
      }
      finally
      {
        _loading = false;
      }
    }

    /// <summary>
    /// Собрать карту точности: значения из конфига свойств + наложенные правки.
    /// </summary>
    private void RebuildPrecisionMap()
    {
      _precisionMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
      foreach (TrackedProperty p in VelumAssemblyBomTrackedPropertiesConfig.Load())
      {
        if (string.IsNullOrWhiteSpace(p?.Name)) continue;
        _precisionMap[p.Name.Trim()] = p.Precision;
      }
      foreach (KeyValuePair<string, int> kv in _precisionEdits)
        _precisionMap[kv.Key] = kv.Value;
    }

    /// <summary>Текст для колонки «Точность»: пусто для структурных, иначе число.</summary>
    /// <param name="col">Описание колонки.</param>
    /// <returns>Текстовое значение точности.</returns>
    private string ResolvePrecisionText(VelumBomExchangeColumnDef col)
    {
      if (col == null || col.Source == VelumBomExchangeFieldSource.Structural)
        return string.Empty;
      int p;
      if (_precisionMap.TryGetValue((col.Field ?? string.Empty).Trim(), out p))
        return p.ToString(CultureInfo.InvariantCulture);
      return string.Empty;
    }

    /// <summary>Запрет редактирования недоступных ячеек.</summary>
    /// <param name="sender">Источник события.</param>
    /// <param name="e">Аргументы отмены правки.</param>
    private void OnGridCellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
    {
      if (_loading || e.RowIndex < 0)
        return;

      VelumBomExchangeColumnDef col = ColOf(_gridFields.Rows[e.RowIndex]);
      if (col == null)
        return;

      // Чекбокс — переключаем кликом, без режима редактирования.
      if (e.ColumnIndex == _colEnabled.Index)
      {
        e.Cancel = true;
        return;
      }

      // Поле, Источник, Точность, Ширина — read-only всегда.
      if (e.ColumnIndex == _colField.Index ||
          e.ColumnIndex == _colSource.Index ||
          e.ColumnIndex == _colPrecision.Index ||
          e.ColumnIndex == _colWidth.Index)
      {
        e.Cancel = true;
        return;
      }

      // Заголовок — редактируется только у tracked.
      if (e.ColumnIndex == _colHeader.Index &&
          col.Source == VelumBomExchangeFieldSource.Structural)
      {
        e.Cancel = true;
      }
    }

    /// <summary>Переключение флага «Вкл» щелчком по чекбоксу.</summary>
    /// <param name="sender">Источник события.</param>
    /// <param name="e">Аргументы щелчка.</param>
    private void OnGridCellContentClick(object sender, DataGridViewCellEventArgs e)
    {
      if (_loading || e.RowIndex < 0 || e.ColumnIndex != _colEnabled.Index)
        return;

      DataGridViewRow row = _gridFields.Rows[e.RowIndex];
      VelumBomExchangeColumnDef col = ColOf(row);
      if (col == null)
        return;

      bool mandatory = col.Source == VelumBomExchangeFieldSource.Structural &&
          VelumBomExchangeLayoutStore.IsMandatory(col.Field);

      bool newValue = !col.Enabled;
      if (mandatory)
        newValue = true;

      col.Enabled = newValue;
      row.Cells[_colEnabled.Index].Value = newValue;
    }

    /// <summary>Проверка числовых значений «Ширина» и «Точность» при выходе из ячейки.</summary>
    /// <param name="sender">Источник события.</param>
    /// <param name="e">Аргументы валидации.</param>
    private void OnGridCellValidating(object sender, DataGridViewCellValidatingEventArgs e)
    {
      if (_loading || e.RowIndex < 0)
        return;

      DataGridViewRow row = _gridFields.Rows[e.RowIndex];
      if (row.IsNewRow)
        return;

      VelumBomExchangeColumnDef col = ColOf(row);
      if (col == null)
        return;

      string text = (e.FormattedValue == null ? string.Empty : e.FormattedValue.ToString()).Trim();

      if (e.ColumnIndex == _colWidth.Index)
      {
        int width;
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out width) ||
            width < MinColumnWidth || width > MaxColumnWidth)
        {
          MessageBox.Show(
              this,
              "Ширина колонки — целое число от " + MinColumnWidth + " до " + MaxColumnWidth + ".",
              Text,
              MessageBoxButtons.OK,
              MessageBoxIcon.Warning);
          e.Cancel = true;
        }
      }
      else if (e.ColumnIndex == _colPrecision.Index)
      {
        if (col.Source == VelumBomExchangeFieldSource.Structural)
          return;

        // Пустое значение допустимо — трактуется как 0.
        if (text.Length == 0)
          return;

        int precision;
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out precision) ||
            precision < VelumAssemblyBomTrackedPropertiesConfig.MinPrecision ||
            precision > VelumAssemblyBomTrackedPropertiesConfig.MaxPrecision)
        {
          MessageBox.Show(
              this,
              "Точность — целое число от " + VelumAssemblyBomTrackedPropertiesConfig.MinPrecision +
                  " до " + VelumAssemblyBomTrackedPropertiesConfig.MaxPrecision + ".",
              Text,
              MessageBoxButtons.OK,
              MessageBoxIcon.Warning);
          e.Cancel = true;
        }
      }
    }

    /// <summary>Завершение ввода: сохранить значение в модель.</summary>
    /// <param name="sender">Источник события.</param>
    /// <param name="e">Аргументы окончания правки.</param>
    private void OnGridCellEndEdit(object sender, DataGridViewCellEventArgs e)
    {
      if (_loading || e.RowIndex < 0)
        return;

      DataGridViewRow row = _gridFields.Rows[e.RowIndex];
      if (row.IsNewRow)
        return;

      VelumBomExchangeColumnDef col = ColOf(row);
      if (col == null)
        return;

      if (e.ColumnIndex == _colHeader.Index)
      {
        string header = (row.Cells[_colHeader.Index].Value?.ToString() ?? string.Empty).Trim();
        if (header.Length == 0)
          header = col.Field ?? string.Empty;
        col.Header = header;
        row.Cells[_colHeader.Index].Value = header;
      }
      else if (e.ColumnIndex == _colWidth.Index)
      {
        string text = (row.Cells[_colWidth.Index].Value?.ToString() ?? string.Empty).Trim();
        int width;
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out width))
        {
          if (width < MinColumnWidth) width = MinColumnWidth;
          if (width > MaxColumnWidth) width = MaxColumnWidth;
          col.Width = width;
        }
        row.Cells[_colWidth.Index].Value = col.Width;
      }
      else if (e.ColumnIndex == _colPrecision.Index)
      {
        if (col.Source == VelumBomExchangeFieldSource.Structural)
          return;

        string text = (row.Cells[_colPrecision.Index].Value?.ToString() ?? string.Empty).Trim();
        int precision = VelumAssemblyBomTrackedPropertiesConfig.MinPrecision;
        if (text.Length != 0)
          int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out precision);
        precision = VelumAssemblyBomTrackedPropertiesConfig.ClampPrecision(precision);

        string key = (col.Field ?? string.Empty).Trim();
        if (key.Length != 0)
        {
          _precisionEdits[key] = precision;
          _precisionMap[key] = precision;
        }
        row.Cells[_colPrecision.Index].Value = precision;
      }
    }

    /// <summary>Переместить выбранную колонку вверх.</summary>
    /// <param name="sender">Источник события.</param>
    /// <param name="e">Аргументы события.</param>
    private void OnUpClick(object sender, EventArgs e)
    {
      MoveSelectedColumn(-1);
    }

    /// <summary>Переместить выбранную колонку вниз.</summary>
    /// <param name="sender">Источник события.</param>
    /// <param name="e">Аргументы события.</param>
    private void OnDownClick(object sender, EventArgs e)
    {
      MoveSelectedColumn(1);
    }

    /// <summary>
    /// Переместить выбранную колонку на заданное смещение и обновить грид.
    /// </summary>
    /// <param name="delta">Смещение (-1 вверх, +1 вниз).</param>
    private void MoveSelectedColumn(int delta)
    {
      if (_loading)
        return;

      DataGridViewRow current = _gridFields.CurrentRow;
      if (current == null || current.Index < 0)
        return;

      int index = current.Index;
      int target = index + delta;
      List<VelumBomExchangeColumnDef> list = _data.Columns;
      if (target < 0 || target >= list.Count)
        return;

      VelumBomExchangeColumnDef moved = list[index];
      list.RemoveAt(index);
      list.Insert(target, moved);
      RenumberOrder(list);

      ReloadFieldsGrid();
      SelectRow(target);
    }

    /// <summary>Синхронизировать layout со списком отслеживаемых свойств.</summary>
    /// <param name="sender">Источник события.</param>
    /// <param name="e">Аргументы события.</param>
    private void OnSyncTrackedClick(object sender, EventArgs e)
    {
      VelumBomExchangeLayoutStore.Normalize(_data);
      ReloadFieldsGrid();
      ReloadTrackedPreview();
    }

    /// <summary>Сбросить состав и порядок полей к значениям по умолчанию.</summary>
    /// <param name="sender">Источник события.</param>
    /// <param name="e">Аргументы события.</param>
    private void OnResetClick(object sender, EventArgs e)
    {
      DialogResult answer = MessageBox.Show(
          this,
          "Сбросить состав, порядок и заголовки полей выгрузки к значениям по умолчанию?\n" +
              "Изменения сохранятся только после нажатия OK.",
          Text,
          MessageBoxButtons.OKCancel,
          MessageBoxIcon.Question);
      if (answer != DialogResult.OK)
        return;

      _precisionEdits.Clear();
      _data = VelumBomExchangeLayoutStore.CreateDefault();
      ReloadFieldsGrid();
    }

    /// <summary>Открыть редактор отслеживаемых свойств, затем обновить layout.</summary>
    /// <param name="sender">Источник события.</param>
    /// <param name="e">Аргументы события.</param>
    private void OnOpenTrackedEditorClick(object sender, EventArgs e)
    {
      using (var form = new VelumBomTrackedPropertiesEditForm())
      {
        form.ShowDialog(this);
      }

      // Свойства могли измениться — пересобираем layout, отложенные правки точности сбрасываем.
      VelumBomExchangeLayoutStore.Normalize(_data);
      _precisionEdits.Clear();
      ReloadFieldsGrid();
      ReloadTrackedPreview();
    }

    /// <summary>Сохранить layout и применить правки точности.</summary>
    /// <param name="sender">Источник события.</param>
    /// <param name="e">Аргументы события.</param>
    private void OnOkClick(object sender, EventArgs e)
    {
      _gridFields.EndEdit();

      // Порядок колонок берём из визуального порядка строк грида.
      var ordered = new List<VelumBomExchangeColumnDef>();
      foreach (DataGridViewRow row in _gridFields.Rows)
      {
        if (row.IsNewRow)
          continue;
        VelumBomExchangeColumnDef col = row.Tag as VelumBomExchangeColumnDef;
        if (col != null && !ordered.Contains(col))
          ordered.Add(col);
      }
      for (int i = 0; i < ordered.Count; i++)
        ordered[i].Order = i + 1;
      _data.Columns = ordered;

      // Правки точности — в bomTrackedProperties.json.
      ApplyPrecisionEdits();

      VelumBomExchangeLayoutStore.Save(_data);
      DialogResult = DialogResult.OK;
      Close();
    }

    /// <summary>Закрыть без сохранения.</summary>
    /// <param name="sender">Источник события.</param>
    /// <param name="e">Аргументы события.</param>
    private void OnCancelClick(object sender, EventArgs e)
    {
      DialogResult = DialogResult.Cancel;
      Close();
    }

    /// <summary>
    /// Применить отложенные правки точности к настройкам отслеживаемых свойств.
    /// </summary>
    private void ApplyPrecisionEdits()
    {
      if (_precisionEdits.Count == 0)
        return;

      var props = new List<TrackedProperty>(VelumAssemblyBomTrackedPropertiesConfig.Load());
      bool changed = false;
      foreach (KeyValuePair<string, int> kv in _precisionEdits)
      {
        TrackedProperty prop = props.FirstOrDefault(p =>
            p != null && string.Equals(p.Name, kv.Key, StringComparison.OrdinalIgnoreCase));
        if (prop == null)
          continue;
        int precision = VelumAssemblyBomTrackedPropertiesConfig.ClampPrecision(kv.Value);
        if (prop.Precision != precision)
        {
          prop.Precision = precision;
          changed = true;
        }
      }

      if (changed)
        VelumAssemblyBomTrackedPropertiesConfig.Save(props);
    }

    /// <summary>Обновить предпросмотр списка отслеживаемых свойств на второй вкладке.</summary>
    private void ReloadTrackedPreview()
    {
      _listTrackedPreview.BeginUpdate();
      try
      {
        _listTrackedPreview.Items.Clear();
        foreach (TrackedProperty p in VelumAssemblyBomTrackedPropertiesConfig.Load())
        {
          if (p == null || string.IsNullOrWhiteSpace(p.Name))
            continue;
          var item = new ListViewItem(p.Name);
          item.SubItems.Add(p.Precision.ToString(CultureInfo.InvariantCulture));
          _listTrackedPreview.Items.Add(item);
        }
      }
      finally
      {
        _listTrackedPreview.EndUpdate();
      }
    }

    /// <summary>Переуплотнить Order по порядку списка.</summary>
    /// <param name="list">Список колонок.</param>
    private static void RenumberOrder(List<VelumBomExchangeColumnDef> list)
    {
      for (int i = 0; i < list.Count; i++)
        list[i].Order = i + 1;
    }

    /// <summary>Выделить строку грида по индексу.</summary>
    /// <param name="index">Индекс строки.</param>
    private void SelectRow(int index)
    {
      if (index < 0 || index >= _gridFields.Rows.Count)
        return;
      _gridFields.ClearSelection();
      _gridFields.Rows[index].Selected = true;
      _gridFields.CurrentCell = _gridFields.Rows[index].Cells[_colEnabled.Index];
    }
  }
}