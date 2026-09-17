using System.Drawing;
using System.Windows.Forms;

namespace Velum.UI.AssemblyRegistry
{
  /// <summary>
  /// Форма настройки состава, порядка и заголовков полей выгрузки BOM в 1С
  /// (bomExchangeLayout.json). Содержит вкладки «Поля выгрузки» и «Отслеживаемые свойства».
  /// </summary>
  internal sealed partial class VelumBomExchangeLayoutForm
  {
    private System.ComponentModel.IContainer components = null;

    private System.Windows.Forms.TabControl tabs;
    private System.Windows.Forms.TabPage tabFields;
    private System.Windows.Forms.TabPage tabTracked;

    private System.Windows.Forms.TableLayoutPanel fieldsLayout;
    private System.Windows.Forms.DataGridView _gridFields;
    private System.Windows.Forms.DataGridViewCheckBoxColumn _colEnabled;
    private System.Windows.Forms.DataGridViewTextBoxColumn _colField;
    private System.Windows.Forms.DataGridViewTextBoxColumn _colSource;
    private System.Windows.Forms.DataGridViewTextBoxColumn _colHeader;
    private System.Windows.Forms.DataGridViewTextBoxColumn _colPrecision;
    private System.Windows.Forms.DataGridViewTextBoxColumn _colWidth;
    private System.Windows.Forms.FlowLayoutPanel fieldsButtons;
    private System.Windows.Forms.Button _btnUp;
    private System.Windows.Forms.Button _btnDown;
    private System.Windows.Forms.Button _btnSyncTracked;
    private System.Windows.Forms.Button _btnReset;

    private System.Windows.Forms.TableLayoutPanel trackedLayout;
    private System.Windows.Forms.Label trackedDescLabel;
    private System.Windows.Forms.ListView _listTrackedPreview;
    private System.Windows.Forms.ColumnHeader _colTrackedName;
    private System.Windows.Forms.ColumnHeader _colTrackedPrecision;
    private System.Windows.Forms.Button _btnOpenTrackedEditor;

    private System.Windows.Forms.TableLayoutPanel root;
    private System.Windows.Forms.FlowLayoutPanel buttonsRow;
    private System.Windows.Forms.Button _btnCancel;
    private System.Windows.Forms.Button _btnOk;
    private System.Windows.Forms.ToolTip _toolTip;

    protected override void Dispose(bool disposing)
    {
      if (disposing && (components != null))
      {
        components.Dispose();
      }
      base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// Метод, требуемый для конструктора форм.
    /// Не изменяйте содержимое этого метода с помощью редактора кода.
    /// </summary>
    private void InitializeComponent()
    {
      this.components = new System.ComponentModel.Container();
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VelumBomExchangeLayoutForm));
      this.tabs = new System.Windows.Forms.TabControl();
      this.tabFields = new System.Windows.Forms.TabPage();
      this.fieldsLayout = new System.Windows.Forms.TableLayoutPanel();
      this._gridFields = new System.Windows.Forms.DataGridView();
      this._colEnabled = new System.Windows.Forms.DataGridViewCheckBoxColumn();
      this._colField = new System.Windows.Forms.DataGridViewTextBoxColumn();
      this._colSource = new System.Windows.Forms.DataGridViewTextBoxColumn();
      this._colHeader = new System.Windows.Forms.DataGridViewTextBoxColumn();
      this._colPrecision = new System.Windows.Forms.DataGridViewTextBoxColumn();
      this._colWidth = new System.Windows.Forms.DataGridViewTextBoxColumn();
      this.fieldsButtons = new System.Windows.Forms.FlowLayoutPanel();
      this._btnUp = new System.Windows.Forms.Button();
      this._btnDown = new System.Windows.Forms.Button();
      this._btnSyncTracked = new System.Windows.Forms.Button();
      this._btnReset = new System.Windows.Forms.Button();
      this.tabTracked = new System.Windows.Forms.TabPage();
      this.trackedLayout = new System.Windows.Forms.TableLayoutPanel();
      this.trackedDescLabel = new System.Windows.Forms.Label();
      this._listTrackedPreview = new System.Windows.Forms.ListView();
      this._colTrackedName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
      this._colTrackedPrecision = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
      this._btnOpenTrackedEditor = new System.Windows.Forms.Button();
      this.root = new System.Windows.Forms.TableLayoutPanel();
      this.buttonsRow = new System.Windows.Forms.FlowLayoutPanel();
      this._btnCancel = new System.Windows.Forms.Button();
      this._btnOk = new System.Windows.Forms.Button();
      this._toolTip = new System.Windows.Forms.ToolTip(this.components);

      this.tabs.SuspendLayout();
      this.tabFields.SuspendLayout();
      this.fieldsLayout.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)(this._gridFields)).BeginInit();
      this.fieldsButtons.SuspendLayout();
      this.tabTracked.SuspendLayout();
      this.trackedLayout.SuspendLayout();
      this.root.SuspendLayout();
      this.buttonsRow.SuspendLayout();
      this.SuspendLayout();
      // 
      // tabs
      // 
      this.tabs.Controls.Add(this.tabFields);
      this.tabs.Controls.Add(this.tabTracked);
      this.tabs.Dock = System.Windows.Forms.DockStyle.Fill;
      this.tabs.Location = new System.Drawing.Point(12, 12);
      this.tabs.Margin = new System.Windows.Forms.Padding(0, 0, 0, 6);
      this.tabs.Name = "tabs";
      this.tabs.SelectedIndex = 0;
      this.tabs.Size = new System.Drawing.Size(700, 432);
      this.tabs.TabIndex = 0;
      // 
      // tabFields
      // 
      this.tabFields.Controls.Add(this.fieldsLayout);
      this.tabFields.Location = new System.Drawing.Point(4, 22);
      this.tabFields.Name = "tabFields";
      this.tabFields.Padding = new System.Windows.Forms.Padding(6);
      this.tabFields.Size = new System.Drawing.Size(692, 406);
      this.tabFields.TabIndex = 0;
      this.tabFields.Text = "Поля выгрузки";
      this.tabFields.UseVisualStyleBackColor = true;
      // 
      // fieldsLayout
      // 
      this.fieldsLayout.ColumnCount = 2;
      this.fieldsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this.fieldsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this.fieldsLayout.Controls.Add(this._gridFields, 0, 0);
      this.fieldsLayout.Controls.Add(this.fieldsButtons, 1, 0);
      this.fieldsLayout.Dock = System.Windows.Forms.DockStyle.Fill;
      this.fieldsLayout.Location = new System.Drawing.Point(6, 6);
      this.fieldsLayout.Name = "fieldsLayout";
      this.fieldsLayout.RowCount = 1;
      this.fieldsLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this.fieldsLayout.Size = new System.Drawing.Size(680, 394);
      this.fieldsLayout.TabIndex = 0;
      // 
      // _gridFields
      // 
      this._gridFields.AllowUserToAddRows = false;
      this._gridFields.AllowUserToDeleteRows = false;
      this._gridFields.AllowUserToResizeRows = false;
      this._gridFields.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
      this._gridFields.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this._colEnabled,
            this._colField,
            this._colSource,
            this._colHeader,
            this._colPrecision,
            this._colWidth});
      this._gridFields.Dock = System.Windows.Forms.DockStyle.Fill;
      this._gridFields.Location = new System.Drawing.Point(0, 0);
      this._gridFields.Margin = new System.Windows.Forms.Padding(0, 0, 6, 0);
      this._gridFields.MultiSelect = false;
      this._gridFields.Name = "_gridFields";
      this._gridFields.RowHeadersVisible = false;
      this._gridFields.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
      this._gridFields.Size = new System.Drawing.Size(528, 394);
      this._gridFields.TabIndex = 0;
      this._gridFields.CellBeginEdit += new System.Windows.Forms.DataGridViewCellCancelEventHandler(this.OnGridCellBeginEdit);
      this._gridFields.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.OnGridCellContentClick);
      this._gridFields.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler(this.OnGridCellEndEdit);
      this._gridFields.CellValidating += new System.Windows.Forms.DataGridViewCellValidatingEventHandler(this.OnGridCellValidating);
      // 
      // _colEnabled
      // 
      this._colEnabled.HeaderText = "Вкл";
      this._colEnabled.Name = "_colEnabled";
      this._colEnabled.Resizable = System.Windows.Forms.DataGridViewTriState.False;
      this._colEnabled.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
      this._colEnabled.ToolTipText = "Включить колонку в выгрузку. Обязательные системные поля отключить нельзя.";
      this._colEnabled.Width = 40;
      // 
      // _colField
      // 
      this._colField.HeaderText = "Поле";
      this._colField.Name = "_colField";
      this._colField.ReadOnly = true;
      this._colField.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
      this._colField.ToolTipText = "Ключ системного поля или имя отслеживаемого свойства. Только для чтения.";
      this._colField.Width = 160;
      // 
      // _colSource
      // 
      this._colSource.HeaderText = "Источник";
      this._colSource.Name = "_colSource";
      this._colSource.ReadOnly = true;
      this._colSource.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
      this._colSource.ToolTipText = "Системное — поле записи зеркала BOM; Свойство — значение из снимка отслеж" +
    "иваемых свойств.";
      this._colSource.Width = 90;
      // 
      // _colHeader
      // 
      this._colHeader.HeaderText = "Заголовок в CSV";
      this._colHeader.Name = "_colHeader";
      this._colHeader.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
      this._colHeader.ToolTipText = "Заголовок колонки в CSV. Для системных полей фиксирован, для свойств — пр" +
    "авится.";
      this._colHeader.Width = 180;
      // 
      // _colPrecision
      // 
      this._colPrecision.HeaderText = "Точность";
      this._colPrecision.Name = "_colPrecision";
      this._colPrecision.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
      this._colPrecision.ToolTipText = "Количество знаков после запятой. Правится в редакторе отслеживаемых св" +
    "ойств.";
      this._colPrecision.Width = 70;
      // 
      // _colWidth
      // 
      this._colWidth.HeaderText = "Ширина";
      this._colWidth.Name = "_colWidth";
      this._colWidth.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
      this._colWidth.ToolTipText = "Ширина колонки в окне экспорта, пикселей. Только для чтения.";
      this._colWidth.Width = 70;
      // 
      // fieldsButtons
      // 
      this.fieldsButtons.AutoSize = true;
      this.fieldsButtons.Controls.Add(this._btnUp);
      this.fieldsButtons.Controls.Add(this._btnDown);
      this.fieldsButtons.Controls.Add(this._btnSyncTracked);
      this.fieldsButtons.Controls.Add(this._btnReset);
      this.fieldsButtons.Dock = System.Windows.Forms.DockStyle.Fill;
      this.fieldsButtons.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
      this.fieldsButtons.Location = new System.Drawing.Point(534, 0);
      this.fieldsButtons.Margin = new System.Windows.Forms.Padding(0);
      this.fieldsButtons.Name = "fieldsButtons";
      this.fieldsButtons.Size = new System.Drawing.Size(146, 394);
      this.fieldsButtons.TabIndex = 1;
      this.fieldsButtons.WrapContents = false;
      // 
      // _btnUp
      // 
      this._btnUp.AutoSize = true;
      this._btnUp.Location = new System.Drawing.Point(3, 3);
      this._btnUp.Name = "_btnUp";
      this._btnUp.Size = new System.Drawing.Size(140, 25);
      this._btnUp.TabIndex = 0;
      this._btnUp.Text = "Вверх";
      this._btnUp.UseVisualStyleBackColor = true;
      this._btnUp.Click += new System.EventHandler(this.OnUpClick);
      // 
      // _btnDown
      // 
      this._btnDown.AutoSize = true;
      this._btnDown.Location = new System.Drawing.Point(3, 34);
      this._btnDown.Name = "_btnDown";
      this._btnDown.Size = new System.Drawing.Size(140, 25);
      this._btnDown.TabIndex = 1;
      this._btnDown.Text = "Вниз";
      this._btnDown.UseVisualStyleBackColor = true;
      this._btnDown.Click += new System.EventHandler(this.OnDownClick);
      // 
      // _btnSyncTracked
      // 
      this._btnSyncTracked.AutoSize = true;
      this._btnSyncTracked.Location = new System.Drawing.Point(3, 65);
      this._btnSyncTracked.Name = "_btnSyncTracked";
      this._btnSyncTracked.Size = new System.Drawing.Size(140, 25);
      this._btnSyncTracked.TabIndex = 2;
      this._btnSyncTracked.Text = "Синхронизировать";
      this._btnSyncTracked.UseVisualStyleBackColor = true;
      this._btnSyncTracked.Click += new System.EventHandler(this.OnSyncTrackedClick);
      // 
      // _btnReset
      // 
      this._btnReset.AutoSize = true;
      this._btnReset.Location = new System.Drawing.Point(3, 96);
      this._btnReset.Name = "_btnReset";
      this._btnReset.Size = new System.Drawing.Size(140, 25);
      this._btnReset.TabIndex = 3;
      this._btnReset.Text = "Сбросить…";
      this._btnReset.UseVisualStyleBackColor = true;
      this._btnReset.Click += new System.EventHandler(this.OnResetClick);
      // 
      // tabTracked
      // 
      this.tabTracked.Controls.Add(this.trackedLayout);
      this.tabTracked.Location = new System.Drawing.Point(4, 22);
      this.tabTracked.Name = "tabTracked";
      this.tabTracked.Padding = new System.Windows.Forms.Padding(6);
      this.tabTracked.Size = new System.Drawing.Size(692, 406);
      this.tabTracked.TabIndex = 1;
      this.tabTracked.Text = "Отслеживаемые свойства";
      this.tabTracked.UseVisualStyleBackColor = true;
      // 
      // trackedLayout
      // 
      this.trackedLayout.ColumnCount = 1;
      this.trackedLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this.trackedLayout.Controls.Add(this.trackedDescLabel, 0, 0);
      this.trackedLayout.Controls.Add(this._listTrackedPreview, 0, 1);
      this.trackedLayout.Controls.Add(this._btnOpenTrackedEditor, 0, 2);
      this.trackedLayout.Dock = System.Windows.Forms.DockStyle.Fill;
      this.trackedLayout.Location = new System.Drawing.Point(6, 6);
      this.trackedLayout.Name = "trackedLayout";
      this.trackedLayout.RowCount = 3;
      this.trackedLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this.trackedLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this.trackedLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this.trackedLayout.Size = new System.Drawing.Size(680, 394);
      this.trackedLayout.TabIndex = 0;
      // 
      // trackedDescLabel
      // 
      this.trackedDescLabel.AutoSize = true;
      this.trackedDescLabel.Dock = System.Windows.Forms.DockStyle.Fill;
      this.trackedDescLabel.Location = new System.Drawing.Point(3, 0);
      this.trackedDescLabel.Margin = new System.Windows.Forms.Padding(3, 0, 3, 6);
      this.trackedDescLabel.Name = "trackedDescLabel";
      this.trackedDescLabel.Size = new System.Drawing.Size(674, 26);
      this.trackedDescLabel.TabIndex = 0;
      this.trackedDescLabel.Text = "Свойства, значения которых выгружаются в 1С. Изменяются в отдельном окне; после з" +
    "акрытия список полей выгрузки обновляется автоматически.";
      // 
      // _listTrackedPreview
      // 
      this._listTrackedPreview.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this._colTrackedName,
            this._colTrackedPrecision});
      this._listTrackedPreview.Dock = System.Windows.Forms.DockStyle.Fill;
      this._listTrackedPreview.FullRowSelect = true;
      this._listTrackedPreview.GridLines = true;
      this._listTrackedPreview.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
      this._listTrackedPreview.HideSelection = false;
      this._listTrackedPreview.Location = new System.Drawing.Point(3, 35);
      this._listTrackedPreview.MultiSelect = false;
      this._listTrackedPreview.Name = "_listTrackedPreview";
      this._listTrackedPreview.Size = new System.Drawing.Size(674, 328);
      this._listTrackedPreview.TabIndex = 1;
      this._listTrackedPreview.UseCompatibleStateImageBehavior = false;
      this._listTrackedPreview.View = System.Windows.Forms.View.Details;
      // 
      // _colTrackedName
      // 
      this._colTrackedName.Text = "Свойство";
      this._colTrackedName.Width = 420;
      // 
      // _colTrackedPrecision
      // 
      this._colTrackedPrecision.Text = "Точность";
      this._colTrackedPrecision.Width = 120;
      // 
      // _btnOpenTrackedEditor
      // 
      this._btnOpenTrackedEditor.AutoSize = true;
      this._btnOpenTrackedEditor.Location = new System.Drawing.Point(3, 369);
      this._btnOpenTrackedEditor.Margin = new System.Windows.Forms.Padding(3, 3, 3, 0);
      this._btnOpenTrackedEditor.Name = "_btnOpenTrackedEditor";
      this._btnOpenTrackedEditor.Size = new System.Drawing.Size(240, 25);
      this._btnOpenTrackedEditor.TabIndex = 2;
      this._btnOpenTrackedEditor.Text = "Редактировать отслеживаемые свойства…";
      this._btnOpenTrackedEditor.UseVisualStyleBackColor = true;
      this._btnOpenTrackedEditor.Click += new System.EventHandler(this.OnOpenTrackedEditorClick);
      // 
      // root
      // 
      this.root.ColumnCount = 1;
      this.root.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this.root.Controls.Add(this.tabs, 0, 0);
      this.root.Controls.Add(this.buttonsRow, 0, 1);
      this.root.Dock = System.Windows.Forms.DockStyle.Fill;
      this.root.Location = new System.Drawing.Point(0, 0);
      this.root.Name = "root";
      this.root.Padding = new System.Windows.Forms.Padding(12);
      this.root.RowCount = 2;
      this.root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this.root.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this.root.Size = new System.Drawing.Size(724, 491);
      this.root.TabIndex = 0;
      // 
      // buttonsRow
      // 
      this.buttonsRow.AutoSize = true;
      this.buttonsRow.Controls.Add(this._btnCancel);
      this.buttonsRow.Controls.Add(this._btnOk);
      this.buttonsRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this.buttonsRow.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
      this.buttonsRow.Location = new System.Drawing.Point(12, 450);
      this.buttonsRow.Margin = new System.Windows.Forms.Padding(0);
      this.buttonsRow.Name = "buttonsRow";
      this.buttonsRow.Size = new System.Drawing.Size(700, 29);
      this.buttonsRow.TabIndex = 1;
      this.buttonsRow.WrapContents = false;
      // 
      // _btnCancel
      // 
      this._btnCancel.AutoSize = true;
      this._btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
      this._btnCancel.Location = new System.Drawing.Point(622, 3);
      this._btnCancel.Name = "_btnCancel";
      this._btnCancel.Size = new System.Drawing.Size(75, 23);
      this._btnCancel.TabIndex = 1;
      this._btnCancel.Text = "Отмена";
      this._btnCancel.UseVisualStyleBackColor = true;
      this._btnCancel.Click += new System.EventHandler(this.OnCancelClick);
      // 
      // _btnOk
      // 
      this._btnOk.AutoSize = true;
      this._btnOk.Location = new System.Drawing.Point(541, 3);
      this._btnOk.Name = "_btnOk";
      this._btnOk.Size = new System.Drawing.Size(75, 23);
      this._btnOk.TabIndex = 0;
      this._btnOk.Text = "OK";
      this._btnOk.UseVisualStyleBackColor = true;
      this._btnOk.Click += new System.EventHandler(this.OnOkClick);
      // 
      // VelumBomExchangeLayoutForm
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.ClientSize = new System.Drawing.Size(724, 491);
      this.Controls.Add(this.root);
      this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
      this.MinimumSize = new System.Drawing.Size(560, 360);
      this.Name = "VelumBomExchangeLayoutForm";
      this.ShowInTaskbar = false;
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
      this.Text = "Поля выгрузки BOM в 1C";
      this.tabs.ResumeLayout(false);
      this.tabFields.ResumeLayout(false);
      this.fieldsLayout.ResumeLayout(false);
      this.fieldsLayout.PerformLayout();
      ((System.ComponentModel.ISupportInitialize)(this._gridFields)).EndInit();
      this.fieldsButtons.ResumeLayout(false);
      this.fieldsButtons.PerformLayout();
      this.tabTracked.ResumeLayout(false);
      this.trackedLayout.ResumeLayout(false);
      this.trackedLayout.PerformLayout();
      this.root.ResumeLayout(false);
      this.root.PerformLayout();
      this.buttonsRow.ResumeLayout(false);
      this.buttonsRow.PerformLayout();

      // 
      // _toolTip
      // 
      // Подсказки колонок таблицы задаются через DataGridViewColumn.ToolTipText
      // (у ToolTip нет перегрузки SetToolTip для колонок DataGridView).
      this._toolTip.AutoPopDelay = 12000;
      this._toolTip.InitialDelay = 400;
      this._toolTip.ReshowDelay = 200;
      this._toolTip.ShowAlways = true;
      this._toolTip.SetToolTip(this._gridFields,
          "Состав и порядок колонок CSV. Системные поля (серые) фиксированы; " +
          "у отслеживаемых свойств можно менять заголовок в CSV и включать/выключать колонку.");
      this._toolTip.SetToolTip(this._btnUp,
          "Переместить выделенную колонку на одну позицию вверх в порядке выгрузки.");
      this._toolTip.SetToolTip(this._btnDown,
          "Переместить выделенную колонку на одну позицию вниз в порядке выгрузки.");
      this._toolTip.SetToolTip(this._btnSyncTracked,
          "Подтянуть в список колонок новые отслеживаемые свойства и убрать те, что больше не отслеживаются.");
      this._toolTip.SetToolTip(this._btnReset,
          "Сбросить состав, порядок и заголовки колонок к значениям по умолчанию. " +
          "Изменения сохранятся только после нажатия OK.");
      this._toolTip.SetToolTip(this._btnOpenTrackedEditor,
          "Открыть редактор отслеживаемых свойств. После закрытия список колонок обновится автоматически.");
      this._toolTip.SetToolTip(this._btnOk, "Сохранить настройки полей выгрузки.");
      this._toolTip.SetToolTip(this._btnCancel, "Закрыть без сохранения.");

      this.ResumeLayout(false);

    }

    #endregion
  }
}