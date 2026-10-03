namespace Velum.UI.Logs
{
  internal sealed partial class VelumLogsForm
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
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VelumLogsForm));
      this._layout = new System.Windows.Forms.TableLayoutPanel();
      this._filterRow = new System.Windows.Forms.TableLayoutPanel();
      this._txtFilter = new System.Windows.Forms.TextBox();
      this._btnFilterClear = new System.Windows.Forms.Button();
      this._tabs = new System.Windows.Forms.TabControl();
      this._tabSystem = new System.Windows.Forms.TabPage();
      this._clbSystem = new System.Windows.Forms.CheckedListBox();
      this._tabStyles = new System.Windows.Forms.TabPage();
      this._clbStyles = new System.Windows.Forms.CheckedListBox();
      this._tabParameters = new System.Windows.Forms.TabPage();
      this._clbParameters = new System.Windows.Forms.CheckedListBox();
      this._bottomRow = new System.Windows.Forms.TableLayoutPanel();
      this._bulkPanel = new System.Windows.Forms.FlowLayoutPanel();
      this._btnSelectAll = new System.Windows.Forms.Button();
      this._btnClearAll = new System.Windows.Forms.Button();
      this._btnRefresh = new System.Windows.Forms.Button();
      this._btnClear = new System.Windows.Forms.Button();
      this._btnOpenFolder = new System.Windows.Forms.Button();
      this._btnPanel = new System.Windows.Forms.FlowLayoutPanel();
      this._btnClose = new System.Windows.Forms.Button();
      this._btnView = new System.Windows.Forms.Button();
      this._layout.SuspendLayout();
      this._filterRow.SuspendLayout();
      this._tabs.SuspendLayout();
      this._tabSystem.SuspendLayout();
      this._tabStyles.SuspendLayout();
      this._tabParameters.SuspendLayout();
      this._bottomRow.SuspendLayout();
      this._bulkPanel.SuspendLayout();
      this._btnPanel.SuspendLayout();
      this.SuspendLayout();
      // 
      // _layout
      // 
      this._layout.ColumnCount = 1;
      this._layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._layout.Controls.Add(this._filterRow, 0, 0);
      this._layout.Controls.Add(this._tabs, 0, 1);
      this._layout.Controls.Add(this._bottomRow, 0, 2);
      this._layout.Dock = System.Windows.Forms.DockStyle.Fill;
      this._layout.Location = new System.Drawing.Point(0, 0);
      this._layout.Name = "_layout";
      this._layout.Padding = new System.Windows.Forms.Padding(10);
      this._layout.RowCount = 3;
      this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
      this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
      this._layout.Size = new System.Drawing.Size(560, 460);
      this._layout.TabIndex = 0;
      // 
      // _filterRow
      // 
      this._filterRow.ColumnCount = 2;
      this._filterRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._filterRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 32F));
      this._filterRow.Controls.Add(this._txtFilter, 0, 0);
      this._filterRow.Controls.Add(this._btnFilterClear, 1, 0);
      this._filterRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._filterRow.Location = new System.Drawing.Point(13, 13);
      this._filterRow.Name = "_filterRow";
      this._filterRow.RowCount = 1;
      this._filterRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._filterRow.Size = new System.Drawing.Size(534, 28);
      this._filterRow.TabIndex = 0;
      // 
      // _txtFilter
      // 
      this._txtFilter.Dock = System.Windows.Forms.DockStyle.Fill;
      this._txtFilter.Font = new System.Drawing.Font("Segoe UI", 9F);
      this._txtFilter.Location = new System.Drawing.Point(3, 3);
      this._txtFilter.Name = "_txtFilter";
      this._txtFilter.Size = new System.Drawing.Size(496, 23);
      this._txtFilter.TabIndex = 0;
      // 
      // _btnFilterClear
      // 
      this._btnFilterClear.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnFilterClear.Font = new System.Drawing.Font("Segoe UI", 9F);
      this._btnFilterClear.Location = new System.Drawing.Point(505, 3);
      this._btnFilterClear.Name = "_btnFilterClear";
      this._btnFilterClear.Size = new System.Drawing.Size(26, 22);
      this._btnFilterClear.TabIndex = 1;
      this._btnFilterClear.Text = "×";
      this._btnFilterClear.UseVisualStyleBackColor = true;
      // 
      // _tabs
      // 
      this._tabs.Controls.Add(this._tabSystem);
      this._tabs.Controls.Add(this._tabStyles);
      this._tabs.Controls.Add(this._tabParameters);
      this._tabs.Dock = System.Windows.Forms.DockStyle.Fill;
      this._tabs.Location = new System.Drawing.Point(13, 47);
      this._tabs.Name = "_tabs";
      this._tabs.SelectedIndex = 0;
      this._tabs.Size = new System.Drawing.Size(534, 360);
      this._tabs.TabIndex = 0;
      this._tabs.SelectedIndexChanged += new System.EventHandler(this.OnTabChanged);
      // 
      // _tabSystem
      // 
      this._tabSystem.Controls.Add(this._clbSystem);
      this._tabSystem.Location = new System.Drawing.Point(4, 22);
      this._tabSystem.Name = "_tabSystem";
      this._tabSystem.Padding = new System.Windows.Forms.Padding(6);
      this._tabSystem.Size = new System.Drawing.Size(526, 334);
      this._tabSystem.TabIndex = 0;
      this._tabSystem.Text = "Логи системы";
      this._tabSystem.UseVisualStyleBackColor = true;
      // 
      // _clbSystem
      // 
      this._clbSystem.CheckOnClick = true;
      this._clbSystem.Dock = System.Windows.Forms.DockStyle.Fill;
      this._clbSystem.Font = new System.Drawing.Font("Segoe UI", 9F);
      this._clbSystem.FormattingEnabled = true;
      this._clbSystem.IntegralHeight = false;
      this._clbSystem.Location = new System.Drawing.Point(6, 6);
      this._clbSystem.Name = "_clbSystem";
      this._clbSystem.Size = new System.Drawing.Size(514, 322);
      this._clbSystem.TabIndex = 0;
      this._clbSystem.SelectedIndexChanged += new System.EventHandler(this.OnListSelectedIndexChanged);
      this._clbSystem.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.OnListMouseDoubleClick);
      // 
      // _tabStyles
      // 
      this._tabStyles.Controls.Add(this._clbStyles);
      this._tabStyles.Location = new System.Drawing.Point(4, 22);
      this._tabStyles.Name = "_tabStyles";
      this._tabStyles.Padding = new System.Windows.Forms.Padding(6);
      this._tabStyles.Size = new System.Drawing.Size(526, 334);
      this._tabStyles.TabIndex = 1;
      this._tabStyles.Text = "Логи стилей";
      this._tabStyles.UseVisualStyleBackColor = true;
      // 
      // _clbStyles
      // 
      this._clbStyles.CheckOnClick = true;
      this._clbStyles.Dock = System.Windows.Forms.DockStyle.Fill;
      this._clbStyles.Font = new System.Drawing.Font("Segoe UI", 9F);
      this._clbStyles.FormattingEnabled = true;
      this._clbStyles.IntegralHeight = false;
      this._clbStyles.Location = new System.Drawing.Point(6, 6);
      this._clbStyles.Name = "_clbStyles";
      this._clbStyles.Size = new System.Drawing.Size(514, 322);
      this._clbStyles.TabIndex = 0;
      this._clbStyles.SelectedIndexChanged += new System.EventHandler(this.OnListSelectedIndexChanged);
      this._clbStyles.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.OnListMouseDoubleClick);
      // 
      // _tabParameters
      // 
      this._tabParameters.Controls.Add(this._clbParameters);
      this._tabParameters.Location = new System.Drawing.Point(4, 22);
      this._tabParameters.Name = "_tabParameters";
      this._tabParameters.Padding = new System.Windows.Forms.Padding(6);
      this._tabParameters.Size = new System.Drawing.Size(526, 334);
      this._tabParameters.TabIndex = 2;
      this._tabParameters.Text = "Логи параметров";
      this._tabParameters.UseVisualStyleBackColor = true;
      // 
      // _clbParameters
      // 
      this._clbParameters.CheckOnClick = true;
      this._clbParameters.Dock = System.Windows.Forms.DockStyle.Fill;
      this._clbParameters.Font = new System.Drawing.Font("Segoe UI", 9F);
      this._clbParameters.FormattingEnabled = true;
      this._clbParameters.IntegralHeight = false;
      this._clbParameters.Location = new System.Drawing.Point(6, 6);
      this._clbParameters.Name = "_clbParameters";
      this._clbParameters.Size = new System.Drawing.Size(514, 322);
      this._clbParameters.TabIndex = 0;
      this._clbParameters.SelectedIndexChanged += new System.EventHandler(this.OnListSelectedIndexChanged);
      this._clbParameters.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.OnListMouseDoubleClick);
      // 
      // _bottomRow
      // 
      this._bottomRow.ColumnCount = 2;
      this._bottomRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
      this._bottomRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
      this._bottomRow.Controls.Add(this._bulkPanel, 0, 0);
      this._bottomRow.Controls.Add(this._btnPanel, 1, 0);
      this._bottomRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._bottomRow.Location = new System.Drawing.Point(10, 418);
      this._bottomRow.Margin = new System.Windows.Forms.Padding(0, 8, 0, 0);
      this._bottomRow.Name = "_bottomRow";
      this._bottomRow.RowCount = 1;
      this._bottomRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._bottomRow.Size = new System.Drawing.Size(540, 32);
      this._bottomRow.TabIndex = 1;
      // 
      // _bulkPanel
      // 
      this._bulkPanel.Controls.Add(this._btnSelectAll);
      this._bulkPanel.Controls.Add(this._btnClearAll);
      this._bulkPanel.Controls.Add(this._btnRefresh);
      this._bulkPanel.Controls.Add(this._btnClear);
      this._bulkPanel.Controls.Add(this._btnOpenFolder);
      this._bulkPanel.Dock = System.Windows.Forms.DockStyle.Fill;
      this._bulkPanel.Location = new System.Drawing.Point(0, 0);
      this._bulkPanel.Margin = new System.Windows.Forms.Padding(0);
      this._bulkPanel.Name = "_bulkPanel";
      this._bulkPanel.Size = new System.Drawing.Size(270, 32);
      this._bulkPanel.TabIndex = 0;
      this._bulkPanel.WrapContents = false;
      // 
      // _btnSelectAll
      // 
      this._btnSelectAll.AutoSize = true;
      this._btnSelectAll.Location = new System.Drawing.Point(3, 3);
      this._btnSelectAll.Name = "_btnSelectAll";
      this._btnSelectAll.Size = new System.Drawing.Size(90, 23);
      this._btnSelectAll.TabIndex = 0;
      this._btnSelectAll.Text = "Выделить все";
      this._btnSelectAll.UseVisualStyleBackColor = true;
      this._btnSelectAll.Click += new System.EventHandler(this.OnSelectAllClick);
      // 
      // _btnClearAll
      // 
      this._btnClearAll.AutoSize = true;
      this._btnClearAll.Location = new System.Drawing.Point(99, 3);
      this._btnClearAll.Name = "_btnClearAll";
      this._btnClearAll.Size = new System.Drawing.Size(90, 23);
      this._btnClearAll.TabIndex = 1;
      this._btnClearAll.Text = "Снять все";
      this._btnClearAll.UseVisualStyleBackColor = true;
      this._btnClearAll.Click += new System.EventHandler(this.OnClearAllClick);
      // 
      // _btnRefresh
      // 
      this._btnRefresh.AutoSize = true;
      this._btnRefresh.Location = new System.Drawing.Point(195, 3);
      this._btnRefresh.Name = "_btnRefresh";
      this._btnRefresh.Size = new System.Drawing.Size(75, 23);
      this._btnRefresh.TabIndex = 2;
      this._btnRefresh.Text = "Обновить";
      this._btnRefresh.UseVisualStyleBackColor = true;
      this._btnRefresh.Click += new System.EventHandler(this.OnRefreshClick);
      // 
      // _btnClear
      // 
      this._btnClear.AutoSize = true;
      this._btnClear.Location = new System.Drawing.Point(276, 3);
      this._btnClear.Name = "_btnClear";
      this._btnClear.Size = new System.Drawing.Size(75, 23);
      this._btnClear.TabIndex = 3;
      this._btnClear.Text = "Очистить";
      this._btnClear.UseVisualStyleBackColor = true;
      this._btnClear.Click += new System.EventHandler(this.OnClearClick);
      // 
      // _btnOpenFolder
      // 
      this._btnOpenFolder.AutoSize = true;
      this._btnOpenFolder.Location = new System.Drawing.Point(357, 3);
      this._btnOpenFolder.Name = "_btnOpenFolder";
      this._btnOpenFolder.Size = new System.Drawing.Size(75, 23);
      this._btnOpenFolder.TabIndex = 4;
      this._btnOpenFolder.Text = "Каталог";
      this._btnOpenFolder.UseVisualStyleBackColor = true;
      this._btnOpenFolder.Click += new System.EventHandler(this.OnOpenFolderClick);
      // 
      // _btnPanel
      // 
      this._btnPanel.Controls.Add(this._btnClose);
      this._btnPanel.Controls.Add(this._btnView);
      this._btnPanel.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnPanel.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
      this._btnPanel.Location = new System.Drawing.Point(270, 0);
      this._btnPanel.Margin = new System.Windows.Forms.Padding(0);
      this._btnPanel.Name = "_btnPanel";
      this._btnPanel.Size = new System.Drawing.Size(270, 32);
      this._btnPanel.TabIndex = 1;
      this._btnPanel.WrapContents = false;
      // 
      // _btnClose
      // 
      this._btnClose.AutoSize = true;
      this._btnClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
      this._btnClose.Location = new System.Drawing.Point(192, 3);
      this._btnClose.Name = "_btnClose";
      this._btnClose.Size = new System.Drawing.Size(75, 23);
      this._btnClose.TabIndex = 1;
      this._btnClose.Text = "Закрыть";
      this._btnClose.UseVisualStyleBackColor = true;
      // 
      // _btnView
      // 
      this._btnView.AutoSize = true;
      this._btnView.Location = new System.Drawing.Point(111, 3);
      this._btnView.Name = "_btnView";
      this._btnView.Size = new System.Drawing.Size(75, 23);
      this._btnView.TabIndex = 0;
      this._btnView.Text = "Просмотр";
      this._btnView.UseVisualStyleBackColor = true;
      this._btnView.Click += new System.EventHandler(this.OnViewClick);
      // 
      // VelumLogsForm
      // 
      this.AcceptButton = this._btnView;
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.CancelButton = this._btnClose;
      this.ClientSize = new System.Drawing.Size(560, 460);
      this.Controls.Add(this._layout);
      this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
      this.MinimizeBox = false;
      this.MinimumSize = new System.Drawing.Size(460, 360);
      this.Name = "VelumLogsForm";
      this.ShowInTaskbar = false;
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
      this.Text = "Логи агента";
      this.Shown += new System.EventHandler(this.OnFormShown);
      this._layout.ResumeLayout(false);
      this._filterRow.ResumeLayout(false);
      this._filterRow.PerformLayout();
      this._tabs.ResumeLayout(false);
      this._tabSystem.ResumeLayout(false);
      this._tabStyles.ResumeLayout(false);
      this._tabParameters.ResumeLayout(false);
      this._bottomRow.ResumeLayout(false);
      this._bulkPanel.ResumeLayout(false);
      this._bulkPanel.PerformLayout();
      this._btnPanel.ResumeLayout(false);
      this._btnPanel.PerformLayout();
      this.ResumeLayout(false);

    }

    #endregion

    private System.Windows.Forms.TableLayoutPanel _layout;
    private System.Windows.Forms.TableLayoutPanel _filterRow;
    private System.Windows.Forms.TextBox _txtFilter;
    private System.Windows.Forms.Button _btnFilterClear;
    private System.Windows.Forms.TabControl _tabs;
    private System.Windows.Forms.TabPage _tabSystem;
    private System.Windows.Forms.CheckedListBox _clbSystem;
    private System.Windows.Forms.TabPage _tabStyles;
    private System.Windows.Forms.CheckedListBox _clbStyles;
    private System.Windows.Forms.TabPage _tabParameters;
    private System.Windows.Forms.CheckedListBox _clbParameters;
    private System.Windows.Forms.TableLayoutPanel _bottomRow;
    private System.Windows.Forms.FlowLayoutPanel _bulkPanel;
    private System.Windows.Forms.Button _btnSelectAll;
    private System.Windows.Forms.Button _btnClearAll;
    private System.Windows.Forms.Button _btnRefresh;
    private System.Windows.Forms.Button _btnClear;
    private System.Windows.Forms.Button _btnOpenFolder;
    private System.Windows.Forms.FlowLayoutPanel _btnPanel;
    private System.Windows.Forms.Button _btnClose;
    private System.Windows.Forms.Button _btnView;
  }
}
