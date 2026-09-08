namespace Velum.UI
{
  internal sealed partial class VelumAssemblyRegistryColumnSettingsForm
  {
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
      if (disposing && (components != null))
        components.Dispose();
      base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VelumAssemblyRegistryColumnSettingsForm));
      this._layout = new System.Windows.Forms.TableLayoutPanel();
      this._templateRow = new System.Windows.Forms.TableLayoutPanel();
      this._lblTemplate = new System.Windows.Forms.Label();
      this._cmbTemplate = new System.Windows.Forms.ComboBox();
      this._btnCopy = new System.Windows.Forms.Button();
      this._btnTemplates = new System.Windows.Forms.Button();
      this._grid = new System.Windows.Forms.DataGridView();
      this._buttonsRow = new System.Windows.Forms.FlowLayoutPanel();
      this._btnClose = new System.Windows.Forms.Button();
      this._btnApply = new System.Windows.Forms.Button();
      this._btnValidate = new System.Windows.Forms.Button();
      this._colOrder = new System.Windows.Forms.DataGridViewTextBoxColumn();
      this._colName = new System.Windows.Forms.DataGridViewTextBoxColumn();
      this._colFormula = new System.Windows.Forms.DataGridViewTextBoxColumn();
      this._colFilter = new System.Windows.Forms.DataGridViewTextBoxColumn();
      this._colTotals = new System.Windows.Forms.DataGridViewComboBoxColumn();
      this._colGroupBy = new System.Windows.Forms.DataGridViewCheckBoxColumn();
      this._colSort = new System.Windows.Forms.DataGridViewComboBoxColumn();
      this._colShowInList = new System.Windows.Forms.DataGridViewCheckBoxColumn();
      this._colDescription = new System.Windows.Forms.DataGridViewTextBoxColumn();
      this._layout.SuspendLayout();
      this._templateRow.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)(this._grid)).BeginInit();
      this._buttonsRow.SuspendLayout();
      this.SuspendLayout();
      // 
      // _layout
      // 
      this._layout.ColumnCount = 1;
      this._layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._layout.Controls.Add(this._templateRow, 0, 0);
      this._layout.Controls.Add(this._grid, 0, 1);
      this._layout.Controls.Add(this._buttonsRow, 0, 2);
      this._layout.Dock = System.Windows.Forms.DockStyle.Fill;
      this._layout.Location = new System.Drawing.Point(0, 0);
      this._layout.Name = "_layout";
      this._layout.Padding = new System.Windows.Forms.Padding(12);
      this._layout.RowCount = 3;
      this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._layout.Size = new System.Drawing.Size(1020, 520);
      this._layout.TabIndex = 0;
      // 
      // _templateRow
      // 
      this._templateRow.AutoSize = true;
      this._templateRow.ColumnCount = 4;
      this._templateRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._templateRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._templateRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._templateRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 36F));
      this._templateRow.Controls.Add(this._lblTemplate, 0, 0);
      this._templateRow.Controls.Add(this._cmbTemplate, 1, 0);
      this._templateRow.Controls.Add(this._btnCopy, 2, 0);
      this._templateRow.Controls.Add(this._btnTemplates, 3, 0);
      this._templateRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._templateRow.Location = new System.Drawing.Point(15, 15);
      this._templateRow.Name = "_templateRow";
      this._templateRow.RowCount = 1;
      this._templateRow.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._templateRow.Size = new System.Drawing.Size(990, 29);
      this._templateRow.TabIndex = 0;
      // 
      // _lblTemplate
      // 
      this._lblTemplate.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._lblTemplate.AutoSize = true;
      this._lblTemplate.Location = new System.Drawing.Point(3, 8);
      this._lblTemplate.Name = "_lblTemplate";
      this._lblTemplate.Size = new System.Drawing.Size(49, 13);
      this._lblTemplate.TabIndex = 0;
      this._lblTemplate.Text = "Шаблон:";
      // 
      // _cmbTemplate
      // 
      this._cmbTemplate.Dock = System.Windows.Forms.DockStyle.Fill;
      this._cmbTemplate.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
      this._cmbTemplate.FormattingEnabled = true;
      this._cmbTemplate.Location = new System.Drawing.Point(58, 3);
      this._cmbTemplate.Name = "_cmbTemplate";
      this._cmbTemplate.Size = new System.Drawing.Size(802, 21);
      this._cmbTemplate.TabIndex = 1;
      // 
      // _btnCopy
      // 
      this._btnCopy.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._btnCopy.AutoSize = true;
      this._btnCopy.Location = new System.Drawing.Point(866, 3);
      this._btnCopy.Name = "_btnCopy";
      this._btnCopy.Size = new System.Drawing.Size(85, 23);
      this._btnCopy.TabIndex = 2;
      this._btnCopy.Text = "Копировать";
      this._btnCopy.UseVisualStyleBackColor = true;
      // 
      // _btnTemplates
      // 
      this._btnTemplates.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnTemplates.Location = new System.Drawing.Point(957, 3);
      this._btnTemplates.Name = "_btnTemplates";
      this._btnTemplates.Size = new System.Drawing.Size(30, 23);
      this._btnTemplates.TabIndex = 3;
      this._btnTemplates.Text = "…";
      this._btnTemplates.UseVisualStyleBackColor = true;
      // 
      // _grid
      // 
      this._grid.AllowUserToResizeRows = false;
      this._grid.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
      this._grid.BackgroundColor = System.Drawing.SystemColors.Window;
      this._grid.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
      this._grid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
      this._grid.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this._colOrder,
            this._colName,
            this._colFormula,
            this._colFilter,
            this._colTotals,
            this._colGroupBy,
            this._colSort,
            this._colShowInList,
            this._colDescription});
      this._grid.Dock = System.Windows.Forms.DockStyle.Fill;
      this._grid.Location = new System.Drawing.Point(15, 50);
      this._grid.MultiSelect = false;
      this._grid.Name = "_grid";
      this._grid.RowHeadersVisible = false;
      this._grid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
      this._grid.Size = new System.Drawing.Size(990, 407);
      this._grid.TabIndex = 1;
      // 
      // _buttonsRow
      // 
      this._buttonsRow.Controls.Add(this._btnClose);
      this._buttonsRow.Controls.Add(this._btnApply);
      this._buttonsRow.Controls.Add(this._btnValidate);
      this._buttonsRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._buttonsRow.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
      this._buttonsRow.Location = new System.Drawing.Point(15, 463);
      this._buttonsRow.Name = "_buttonsRow";
      this._buttonsRow.Padding = new System.Windows.Forms.Padding(0, 8, 0, 0);
      this._buttonsRow.Size = new System.Drawing.Size(990, 42);
      this._buttonsRow.TabIndex = 2;
      // 
      // _btnClose
      // 
      this._btnClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
      this._btnClose.Location = new System.Drawing.Point(892, 11);
      this._btnClose.Name = "_btnClose";
      this._btnClose.Size = new System.Drawing.Size(95, 28);
      this._btnClose.TabIndex = 2;
      this._btnClose.Text = "Закрыть";
      this._btnClose.UseVisualStyleBackColor = true;
      // 
      // _btnApply
      // 
      this._btnApply.Location = new System.Drawing.Point(791, 11);
      this._btnApply.Name = "_btnApply";
      this._btnApply.Size = new System.Drawing.Size(95, 28);
      this._btnApply.TabIndex = 1;
      this._btnApply.Text = "Применить";
      this._btnApply.UseVisualStyleBackColor = true;
      // 
      // _btnValidate
      // 
      this._btnValidate.Location = new System.Drawing.Point(690, 11);
      this._btnValidate.Name = "_btnValidate";
      this._btnValidate.Size = new System.Drawing.Size(95, 28);
      this._btnValidate.TabIndex = 0;
      this._btnValidate.Text = "Проверить";
      this._btnValidate.UseVisualStyleBackColor = true;
      // 
      // _colOrder
      // 
      this._colOrder.FillWeight = 12F;
      this._colOrder.HeaderText = "№ пп";
      this._colOrder.Name = "_colOrder";
      this._colOrder.ToolTipText = "Порядок столбца в таблице и отчёте. Введите номер на нужную позицию — строка пере" +
    "местится, номера пересчитаются.";
      // 
      // _colName
      // 
      this._colName.FillWeight = 24F;
      this._colName.HeaderText = "Имя";
      this._colName.Name = "_colName";
      this._colName.ToolTipText = "Имя свойства документа или зарезервированное поле в квадратных скобках, например " +
    "[Кол-во].";
      // 
      // _colFormula
      // 
      this._colFormula.FillWeight = 34F;
      this._colFormula.HeaderText = "Формула";
      this._colFormula.Name = "_colFormula";
      this._colFormula.ToolTipText = "Выражение ячейки. Пусто — берётся значение свойства по имени. Двойной клик — помо" +
    "щник вставки.";
      // 
      // _colFilter
      // 
      this._colFilter.FillWeight = 26F;
      this._colFilter.HeaderText = "Фильтр";
      this._colFilter.Name = "_colFilter";
      this._colFilter.ToolTipText = "Префилл фильтра списка при выборе этого шаблона.";
      // 
      // _colTotals
      // 
      this._colTotals.FillWeight = 18F;
      this._colTotals.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
      this._colTotals.HeaderText = "Показать итоги";
      this._colTotals.Items.AddRange(new object[] {
            "Без итогов",
            "Min",
            "Max",
            "Avg",
            "Sum"});
      this._colTotals.Name = "_colTotals";
      this._colTotals.ToolTipText = "Итог по числовому столбцу в списке и HTML-отчёте.";
      // 
      // _colGroupBy
      // 
      this._colGroupBy.FalseValue = false;
      this._colGroupBy.FillWeight = 16F;
      this._colGroupBy.HeaderText = "Группировка";
      this._colGroupBy.Name = "_colGroupBy";
      this._colGroupBy.ToolTipText = "Только для HTML-отчёта: группировка строк по значению столбца. На список формы не" +
    " влияет. Можно включить только у одного столбца.";
      this._colGroupBy.TrueValue = true;
      // 
      // _colSort
      // 
      this._colSort.FillWeight = 16F;
      this._colSort.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
      this._colSort.HeaderText = "Сортировка";
      this._colSort.Items.AddRange(new object[] {
            "Нет",
            "По возрастанию",
            "По убыванию"});
      this._colSort.Name = "_colSort";
      this._colSort.ToolTipText = "Начальная сортировка списка и отчёта по этому столбцу. Можно задать только у одно" +
    "го столбца.";
      // 
      // _colShowInList
      // 
      this._colShowInList.FalseValue = false;
      this._colShowInList.FillWeight = 16F;
      this._colShowInList.HeaderText = "Показать в списке";
      this._colShowInList.Name = "_colShowInList";
      this._colShowInList.Resizable = System.Windows.Forms.DataGridViewTriState.True;
      this._colShowInList.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
      this._colShowInList.ToolTipText = "Скрыть столбец из списка и отчёта, оставив фильтр по нему рабочим.";
      this._colShowInList.TrueValue = true;
      // 
      // _colDescription
      // 
      this._colDescription.FillWeight = 34F;
      this._colDescription.HeaderText = "Описание";
      this._colDescription.Name = "_colDescription";
      this._colDescription.ToolTipText = "Подсказка (tooltip) для заголовка столбца в списке реестра.";
      // 
      // VelumAssemblyRegistryColumnSettingsForm
      // 
      this.AcceptButton = this._btnApply;
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.CancelButton = this._btnClose;
      this.ClientSize = new System.Drawing.Size(1020, 520);
      this.Controls.Add(this._layout);
      this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
      this.MinimizeBox = false;
      this.Name = "VelumAssemblyRegistryColumnSettingsForm";
      this.ShowInTaskbar = false;
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
      this.Text = "Настройки столбцов реестра изделия";
      this._layout.ResumeLayout(false);
      this._layout.PerformLayout();
      this._templateRow.ResumeLayout(false);
      this._templateRow.PerformLayout();
      ((System.ComponentModel.ISupportInitialize)(this._grid)).EndInit();
      this._buttonsRow.ResumeLayout(false);
      this.ResumeLayout(false);

    }

    #endregion

    private System.Windows.Forms.TableLayoutPanel _layout;
    private System.Windows.Forms.TableLayoutPanel _templateRow;
    private System.Windows.Forms.Label _lblTemplate;
    private System.Windows.Forms.ComboBox _cmbTemplate;
    private System.Windows.Forms.Button _btnCopy;
    private System.Windows.Forms.Button _btnTemplates;
    private System.Windows.Forms.DataGridView _grid;
    private System.Windows.Forms.FlowLayoutPanel _buttonsRow;
    private System.Windows.Forms.Button _btnClose;
    private System.Windows.Forms.Button _btnApply;
    private System.Windows.Forms.Button _btnValidate;
    private System.Windows.Forms.DataGridViewTextBoxColumn _colOrder;
    private System.Windows.Forms.DataGridViewTextBoxColumn _colName;
    private System.Windows.Forms.DataGridViewTextBoxColumn _colFormula;
    private System.Windows.Forms.DataGridViewTextBoxColumn _colFilter;
    private System.Windows.Forms.DataGridViewComboBoxColumn _colTotals;
    private System.Windows.Forms.DataGridViewCheckBoxColumn _colGroupBy;
    private System.Windows.Forms.DataGridViewComboBoxColumn _colSort;
    private System.Windows.Forms.DataGridViewCheckBoxColumn _colShowInList;
    private System.Windows.Forms.DataGridViewTextBoxColumn _colDescription;
  }
}
