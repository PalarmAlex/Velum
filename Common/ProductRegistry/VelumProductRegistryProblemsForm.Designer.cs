namespace Velum.UI
{
  internal sealed partial class VelumProductRegistryProblemsForm
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
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VelumProductRegistryProblemsForm));
      this._layout = new System.Windows.Forms.TableLayoutPanel();
      this._filtersHost = new System.Windows.Forms.TableLayoutPanel();
      this._filtersTopRow = new System.Windows.Forms.TableLayoutPanel();
      this._lblFilterKind = new System.Windows.Forms.Label();
      this._filterKindBox = new System.Windows.Forms.ComboBox();
      this._lblFilterDesignation = new System.Windows.Forms.Label();
      this._filterDesignationBox = new System.Windows.Forms.TextBox();
      this._lblFilterName = new System.Windows.Forms.Label();
      this._filterNameBox = new System.Windows.Forms.TextBox();
      this._filtersBottomRow = new System.Windows.Forms.TableLayoutPanel();
      this._lblFilterDetail = new System.Windows.Forms.Label();
      this._filterDetailBox = new System.Windows.Forms.TextBox();
      this._btnFilterApply = new System.Windows.Forms.Button();
      this._btnFilterReset = new System.Windows.Forms.Button();
      this._btnFilterHelp = new System.Windows.Forms.Button();
      this._lblStatus = new System.Windows.Forms.Label();
      this._list = new System.Windows.Forms.ListView();
      this._colKind = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
      this._colId = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
      this._colDesignation = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
      this._colName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
      this._colPath = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
      this._colDetail = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
      this._progressBar = new System.Windows.Forms.ProgressBar();
      this._progressLabel = new System.Windows.Forms.Label();
      this._buttonsRow = new System.Windows.Forms.FlowLayoutPanel();
      this._btnClose = new System.Windows.Forms.Button();
      this._btnOpenRegistry = new System.Windows.Forms.Button();
      this._btnRefresh = new System.Windows.Forms.Button();
      this._btnStop = new System.Windows.Forms.Button();
      this._layout.SuspendLayout();
      this._filtersHost.SuspendLayout();
      this._filtersTopRow.SuspendLayout();
      this._filtersBottomRow.SuspendLayout();
      this._buttonsRow.SuspendLayout();
      this.SuspendLayout();
      // 
      // _layout
      // 
      this._layout.ColumnCount = 1;
      this._layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._layout.Controls.Add(this._filtersHost, 0, 0);
      this._layout.Controls.Add(this._list, 0, 1);
      this._layout.Controls.Add(this._progressBar, 0, 2);
      this._layout.Controls.Add(this._progressLabel, 0, 3);
      this._layout.Controls.Add(this._buttonsRow, 0, 4);
      this._layout.Dock = System.Windows.Forms.DockStyle.Fill;
      this._layout.Location = new System.Drawing.Point(0, 0);
      this._layout.Name = "_layout";
      this._layout.Padding = new System.Windows.Forms.Padding(12);
      this._layout.RowCount = 5;
      this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 60F));
      this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 0F));
      this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._layout.Size = new System.Drawing.Size(860, 480);
      this._layout.TabIndex = 0;
      // 
      // _filtersHost
      // 
      this._filtersHost.ColumnCount = 1;
      this._filtersHost.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._filtersHost.Controls.Add(this._filtersTopRow, 0, 0);
      this._filtersHost.Controls.Add(this._filtersBottomRow, 0, 1);
      this._filtersHost.Dock = System.Windows.Forms.DockStyle.Fill;
      this._filtersHost.Location = new System.Drawing.Point(15, 12);
      this._filtersHost.Margin = new System.Windows.Forms.Padding(3, 0, 3, 0);
      this._filtersHost.Name = "_filtersHost";
      this._filtersHost.RowCount = 2;
      this._filtersHost.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
      this._filtersHost.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
      this._filtersHost.Size = new System.Drawing.Size(830, 60);
      this._filtersHost.TabIndex = 0;
      // 
      // _filtersTopRow
      // 
      this._filtersTopRow.ColumnCount = 6;
      this._filtersTopRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._filtersTopRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 140F));
      this._filtersTopRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._filtersTopRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
      this._filtersTopRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._filtersTopRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
      this._filtersTopRow.Controls.Add(this._lblFilterKind, 0, 0);
      this._filtersTopRow.Controls.Add(this._filterKindBox, 1, 0);
      this._filtersTopRow.Controls.Add(this._lblFilterDesignation, 2, 0);
      this._filtersTopRow.Controls.Add(this._filterDesignationBox, 3, 0);
      this._filtersTopRow.Controls.Add(this._lblFilterName, 4, 0);
      this._filtersTopRow.Controls.Add(this._filterNameBox, 5, 0);
      this._filtersTopRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._filtersTopRow.Location = new System.Drawing.Point(0, 0);
      this._filtersTopRow.Margin = new System.Windows.Forms.Padding(0);
      this._filtersTopRow.Name = "_filtersTopRow";
      this._filtersTopRow.RowCount = 1;
      this._filtersTopRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._filtersTopRow.Size = new System.Drawing.Size(830, 30);
      this._filtersTopRow.TabIndex = 0;
      // 
      // _lblFilterKind
      // 
      this._lblFilterKind.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._lblFilterKind.AutoSize = true;
      this._lblFilterKind.Location = new System.Drawing.Point(3, 8);
      this._lblFilterKind.Name = "_lblFilterKind";
      this._lblFilterKind.Size = new System.Drawing.Size(29, 13);
      this._lblFilterKind.TabIndex = 0;
      this._lblFilterKind.Text = "Вид:";
      // 
      // _filterKindBox
      // 
      this._filterKindBox.Dock = System.Windows.Forms.DockStyle.Fill;
      this._filterKindBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
      this._filterKindBox.Location = new System.Drawing.Point(38, 3);
      this._filterKindBox.Name = "_filterKindBox";
      this._filterKindBox.Size = new System.Drawing.Size(134, 21);
      this._filterKindBox.TabIndex = 1;
      // 
      // _lblFilterDesignation
      // 
      this._lblFilterDesignation.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._lblFilterDesignation.AutoSize = true;
      this._lblFilterDesignation.Location = new System.Drawing.Point(178, 8);
      this._lblFilterDesignation.Name = "_lblFilterDesignation";
      this._lblFilterDesignation.Size = new System.Drawing.Size(77, 13);
      this._lblFilterDesignation.TabIndex = 2;
      this._lblFilterDesignation.Text = "Обозначение:";
      // 
      // _filterDesignationBox
      // 
      this._filterDesignationBox.Dock = System.Windows.Forms.DockStyle.Fill;
      this._filterDesignationBox.Location = new System.Drawing.Point(261, 3);
      this._filterDesignationBox.Name = "_filterDesignationBox";
      this._filterDesignationBox.Size = new System.Drawing.Size(234, 20);
      this._filterDesignationBox.TabIndex = 3;
      // 
      // _lblFilterName
      // 
      this._lblFilterName.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._lblFilterName.AutoSize = true;
      this._lblFilterName.Location = new System.Drawing.Point(501, 8);
      this._lblFilterName.Name = "_lblFilterName";
      this._lblFilterName.Size = new System.Drawing.Size(86, 13);
      this._lblFilterName.TabIndex = 4;
      this._lblFilterName.Text = "Наименование:";
      // 
      // _filterNameBox
      // 
      this._filterNameBox.Dock = System.Windows.Forms.DockStyle.Fill;
      this._filterNameBox.Location = new System.Drawing.Point(593, 3);
      this._filterNameBox.Name = "_filterNameBox";
      this._filterNameBox.Size = new System.Drawing.Size(234, 20);
      this._filterNameBox.TabIndex = 5;
      // 
      // _filtersBottomRow
      // 
      this._filtersBottomRow.ColumnCount = 6;
      this._filtersBottomRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._filtersBottomRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._filtersBottomRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 90F));
      this._filtersBottomRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 70F));
      this._filtersBottomRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 32F));
      this._filtersBottomRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._filtersBottomRow.Controls.Add(this._lblFilterDetail, 0, 0);
      this._filtersBottomRow.Controls.Add(this._filterDetailBox, 1, 0);
      this._filtersBottomRow.Controls.Add(this._btnFilterApply, 2, 0);
      this._filtersBottomRow.Controls.Add(this._btnFilterReset, 3, 0);
      this._filtersBottomRow.Controls.Add(this._btnFilterHelp, 4, 0);
      this._filtersBottomRow.Controls.Add(this._lblStatus, 5, 0);
      this._filtersBottomRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._filtersBottomRow.Location = new System.Drawing.Point(0, 30);
      this._filtersBottomRow.Margin = new System.Windows.Forms.Padding(0);
      this._filtersBottomRow.Name = "_filtersBottomRow";
      this._filtersBottomRow.RowCount = 1;
      this._filtersBottomRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._filtersBottomRow.Size = new System.Drawing.Size(830, 30);
      this._filtersBottomRow.TabIndex = 1;
      // 
      // _lblFilterDetail
      // 
      this._lblFilterDetail.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._lblFilterDetail.AutoSize = true;
      this._lblFilterDetail.Location = new System.Drawing.Point(3, 8);
      this._lblFilterDetail.Name = "_lblFilterDetail";
      this._lblFilterDetail.Size = new System.Drawing.Size(48, 13);
      this._lblFilterDetail.TabIndex = 0;
      this._lblFilterDetail.Text = "Детали:";
      // 
      // _filterDetailBox
      // 
      this._filterDetailBox.Dock = System.Windows.Forms.DockStyle.Fill;
      this._filterDetailBox.Location = new System.Drawing.Point(57, 3);
      this._filterDetailBox.Name = "_filterDetailBox";
      this._filterDetailBox.Size = new System.Drawing.Size(467, 20);
      this._filterDetailBox.TabIndex = 1;
      // 
      // _btnFilterApply
      // 
      this._btnFilterApply.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnFilterApply.Location = new System.Drawing.Point(530, 3);
      this._btnFilterApply.Name = "_btnFilterApply";
      this._btnFilterApply.Size = new System.Drawing.Size(84, 24);
      this._btnFilterApply.TabIndex = 2;
      this._btnFilterApply.Text = "Применить";
      this._btnFilterApply.UseVisualStyleBackColor = true;
      // 
      // _btnFilterReset
      // 
      this._btnFilterReset.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnFilterReset.Location = new System.Drawing.Point(620, 3);
      this._btnFilterReset.Name = "_btnFilterReset";
      this._btnFilterReset.Size = new System.Drawing.Size(64, 24);
      this._btnFilterReset.TabIndex = 3;
      this._btnFilterReset.Text = "Сброс";
      this._btnFilterReset.UseVisualStyleBackColor = true;
      // 
      // _btnFilterHelp
      // 
      this._btnFilterHelp.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnFilterHelp.Location = new System.Drawing.Point(690, 3);
      this._btnFilterHelp.Name = "_btnFilterHelp";
      this._btnFilterHelp.Size = new System.Drawing.Size(26, 24);
      this._btnFilterHelp.TabIndex = 4;
      this._btnFilterHelp.Text = "?";
      this._btnFilterHelp.UseVisualStyleBackColor = true;
      // 
      // _lblStatus
      // 
      this._lblStatus.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._lblStatus.AutoSize = true;
      this._lblStatus.Location = new System.Drawing.Point(722, 8);
      this._lblStatus.Name = "_lblStatus";
      this._lblStatus.Size = new System.Drawing.Size(105, 13);
      this._lblStatus.TabIndex = 5;
      this._lblStatus.Text = "Проблемы реестра";
      // 
      // _list
      // 
      this._list.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this._colKind,
            this._colId,
            this._colDesignation,
            this._colName,
            this._colPath,
            this._colDetail});
      this._list.Dock = System.Windows.Forms.DockStyle.Fill;
      this._list.FullRowSelect = true;
      this._list.HideSelection = false;
      this._list.Location = new System.Drawing.Point(15, 75);
      this._list.Name = "_list";
      this._list.Size = new System.Drawing.Size(830, 327);
      this._list.TabIndex = 1;
      this._list.UseCompatibleStateImageBehavior = false;
      this._list.View = System.Windows.Forms.View.Details;
      // 
      // _colKind
      // 
      this._colKind.Text = "Вид";
      this._colKind.Width = 110;
      // 
      // _colId
      // 
      this._colId.Text = "Id";
      this._colId.Width = 50;
      // 
      // _colDesignation
      // 
      this._colDesignation.Text = "Обозначение";
      this._colDesignation.Width = 120;
      // 
      // _colName
      // 
      this._colName.Text = "Наименование";
      this._colName.Width = 140;
      // 
      // _colPath
      // 
      this._colPath.Text = "Путь";
      this._colPath.Width = 220;
      // 
      // _colDetail
      // 
      this._colDetail.Text = "Детали";
      this._colDetail.Width = 180;
      // 
      // _progressBar
      // 
      this._progressBar.Dock = System.Windows.Forms.DockStyle.Fill;
      this._progressBar.Location = new System.Drawing.Point(15, 408);
      this._progressBar.Margin = new System.Windows.Forms.Padding(3, 3, 3, 0);
      this._progressBar.Name = "_progressBar";
      this._progressBar.Size = new System.Drawing.Size(830, 1);
      this._progressBar.TabIndex = 2;
      this._progressBar.Visible = false;
      // 
      // _progressLabel
      // 
      this._progressLabel.AutoSize = true;
      this._progressLabel.Dock = System.Windows.Forms.DockStyle.Fill;
      this._progressLabel.Location = new System.Drawing.Point(15, 405);
      this._progressLabel.Name = "_progressLabel";
      this._progressLabel.Size = new System.Drawing.Size(830, 13);
      this._progressLabel.TabIndex = 3;
      // 
      // _buttonsRow
      // 
      this._buttonsRow.Controls.Add(this._btnClose);
      this._buttonsRow.Controls.Add(this._btnOpenRegistry);
      this._buttonsRow.Controls.Add(this._btnRefresh);
      this._buttonsRow.Controls.Add(this._btnStop);
      this._buttonsRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._buttonsRow.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
      this._buttonsRow.Location = new System.Drawing.Point(15, 421);
      this._buttonsRow.Name = "_buttonsRow";
      this._buttonsRow.Padding = new System.Windows.Forms.Padding(0, 8, 0, 0);
      this._buttonsRow.Size = new System.Drawing.Size(830, 44);
      this._buttonsRow.TabIndex = 4;
      // 
      // _btnClose
      // 
      this._btnClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
      this._btnClose.Location = new System.Drawing.Point(722, 11);
      this._btnClose.Name = "_btnClose";
      this._btnClose.Size = new System.Drawing.Size(105, 28);
      this._btnClose.TabIndex = 3;
      this._btnClose.Text = "Закрыть";
      this._btnClose.UseVisualStyleBackColor = true;
      this._btnClose.Click += new System.EventHandler(this.OnCloseClick);
      // 
      // _btnOpenRegistry
      // 
      this._btnOpenRegistry.Location = new System.Drawing.Point(591, 11);
      this._btnOpenRegistry.Name = "_btnOpenRegistry";
      this._btnOpenRegistry.Size = new System.Drawing.Size(125, 28);
      this._btnOpenRegistry.TabIndex = 2;
      this._btnOpenRegistry.Text = "Открыть реестр";
      this._btnOpenRegistry.UseVisualStyleBackColor = true;
      this._btnOpenRegistry.Click += new System.EventHandler(this.OnOpenRegistryClick);
      // 
      // _btnRefresh
      // 
      this._btnRefresh.Location = new System.Drawing.Point(480, 11);
      this._btnRefresh.Name = "_btnRefresh";
      this._btnRefresh.Size = new System.Drawing.Size(105, 28);
      this._btnRefresh.TabIndex = 1;
      this._btnRefresh.Text = "Обновить";
      this._btnRefresh.UseVisualStyleBackColor = true;
      this._btnRefresh.Click += new System.EventHandler(this.OnRefreshClick);
      // 
      // _btnStop
      // 
      this._btnStop.Enabled = false;
      this._btnStop.Location = new System.Drawing.Point(399, 11);
      this._btnStop.Name = "_btnStop";
      this._btnStop.Size = new System.Drawing.Size(75, 28);
      this._btnStop.TabIndex = 0;
      this._btnStop.Text = "Стоп";
      this._btnStop.UseVisualStyleBackColor = true;
      // 
      // VelumProductRegistryProblemsForm
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.CancelButton = this._btnClose;
      this.ClientSize = new System.Drawing.Size(860, 480);
      this.Controls.Add(this._layout);
      this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
      this.MinimizeBox = false;
      this.Name = "VelumProductRegistryProblemsForm";
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
      this.Text = "Проблемы реестра изделий";
      this._layout.ResumeLayout(false);
      this._layout.PerformLayout();
      this._filtersHost.ResumeLayout(false);
      this._filtersTopRow.ResumeLayout(false);
      this._filtersTopRow.PerformLayout();
      this._filtersBottomRow.ResumeLayout(false);
      this._filtersBottomRow.PerformLayout();
      this._buttonsRow.ResumeLayout(false);
      this.ResumeLayout(false);

    }

    #endregion

    private System.Windows.Forms.TableLayoutPanel _layout;
    private System.Windows.Forms.TableLayoutPanel _filtersHost;
    private System.Windows.Forms.TableLayoutPanel _filtersTopRow;
    private System.Windows.Forms.TableLayoutPanel _filtersBottomRow;
    private System.Windows.Forms.Label _lblFilterKind;
    private System.Windows.Forms.ComboBox _filterKindBox;
    private System.Windows.Forms.Label _lblFilterDesignation;
    private System.Windows.Forms.TextBox _filterDesignationBox;
    private System.Windows.Forms.Label _lblFilterName;
    private System.Windows.Forms.TextBox _filterNameBox;
    private System.Windows.Forms.Label _lblFilterDetail;
    private System.Windows.Forms.TextBox _filterDetailBox;
    private System.Windows.Forms.Button _btnFilterApply;
    private System.Windows.Forms.Button _btnFilterReset;
    private System.Windows.Forms.Button _btnFilterHelp;
    private System.Windows.Forms.Label _lblStatus;
    private System.Windows.Forms.ListView _list;
    private System.Windows.Forms.ColumnHeader _colKind;
    private System.Windows.Forms.ColumnHeader _colId;
    private System.Windows.Forms.ColumnHeader _colDesignation;
    private System.Windows.Forms.ColumnHeader _colName;
    private System.Windows.Forms.ColumnHeader _colPath;
    private System.Windows.Forms.ColumnHeader _colDetail;
    private System.Windows.Forms.ProgressBar _progressBar;
    private System.Windows.Forms.Label _progressLabel;
    private System.Windows.Forms.FlowLayoutPanel _buttonsRow;
    private System.Windows.Forms.Button _btnClose;
    private System.Windows.Forms.Button _btnOpenRegistry;
    private System.Windows.Forms.Button _btnRefresh;
    private System.Windows.Forms.Button _btnStop;
  }
}
