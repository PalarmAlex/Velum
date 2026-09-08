namespace Velum.UI
{
  internal sealed partial class VelumEnvironmentMetricsPickerForm
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
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VelumEnvironmentMetricsPickerForm));
      this._layout = new System.Windows.Forms.TableLayoutPanel();
      this._filterRow = new System.Windows.Forms.TableLayoutPanel();
      this._txtFilter = new System.Windows.Forms.TextBox();
      this._btnFilterApply = new System.Windows.Forms.Button();
      this._btnFilterReset = new System.Windows.Forms.Button();
      this._btnFilterHelp = new System.Windows.Forms.Button();
      this._stateFilterRow = new System.Windows.Forms.FlowLayoutPanel();
      this._rbStateAll = new System.Windows.Forms.RadioButton();
      this._rbStateOn = new System.Windows.Forms.RadioButton();
      this._rbStateOff = new System.Windows.Forms.RadioButton();
      this._clb = new System.Windows.Forms.CheckedListBox();
      this._bottomRow = new System.Windows.Forms.TableLayoutPanel();
      this._bulkPanel = new System.Windows.Forms.FlowLayoutPanel();
      this._rbSelectAll = new System.Windows.Forms.RadioButton();
      this._rbClearAll = new System.Windows.Forms.RadioButton();
      this._btnPanel = new System.Windows.Forms.FlowLayoutPanel();
      this._btnClose = new System.Windows.Forms.Button();
      this._btnApply = new System.Windows.Forms.Button();
      this._layout.SuspendLayout();
      this._filterRow.SuspendLayout();
      this._stateFilterRow.SuspendLayout();
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
      this._layout.Controls.Add(this._stateFilterRow, 0, 1);
      this._layout.Controls.Add(this._clb, 0, 2);
      this._layout.Controls.Add(this._bottomRow, 0, 3);
      this._layout.Dock = System.Windows.Forms.DockStyle.Fill;
      this._layout.Location = new System.Drawing.Point(0, 0);
      this._layout.Name = "_layout";
      this._layout.Padding = new System.Windows.Forms.Padding(10);
      this._layout.RowCount = 4;
      this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32F));
      this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
      this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 44F));
      this._layout.Size = new System.Drawing.Size(480, 420);
      this._layout.TabIndex = 0;
      // 
      // _filterRow
      // 
      this._filterRow.ColumnCount = 4;
      this._filterRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._filterRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 90F));
      this._filterRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 70F));
      this._filterRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 32F));
      this._filterRow.Controls.Add(this._txtFilter, 0, 0);
      this._filterRow.Controls.Add(this._btnFilterApply, 1, 0);
      this._filterRow.Controls.Add(this._btnFilterReset, 2, 0);
      this._filterRow.Controls.Add(this._btnFilterHelp, 3, 0);
      this._filterRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._filterRow.Location = new System.Drawing.Point(13, 13);
      this._filterRow.Name = "_filterRow";
      this._filterRow.RowCount = 1;
      this._filterRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._filterRow.Size = new System.Drawing.Size(454, 26);
      this._filterRow.TabIndex = 0;
      // 
      // _txtFilter
      // 
      this._txtFilter.Dock = System.Windows.Forms.DockStyle.Fill;
      this._txtFilter.Font = new System.Drawing.Font("Segoe UI", 9F);
      this._txtFilter.Location = new System.Drawing.Point(3, 3);
      this._txtFilter.Name = "_txtFilter";
      this._txtFilter.Size = new System.Drawing.Size(256, 23);
      this._txtFilter.TabIndex = 0;
      // 
      // _btnFilterApply
      // 
      this._btnFilterApply.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnFilterApply.Location = new System.Drawing.Point(265, 3);
      this._btnFilterApply.Name = "_btnFilterApply";
      this._btnFilterApply.Size = new System.Drawing.Size(84, 20);
      this._btnFilterApply.TabIndex = 1;
      this._btnFilterApply.Text = "Применить";
      this._btnFilterApply.UseVisualStyleBackColor = true;
      // 
      // _btnFilterReset
      // 
      this._btnFilterReset.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnFilterReset.Location = new System.Drawing.Point(355, 3);
      this._btnFilterReset.Name = "_btnFilterReset";
      this._btnFilterReset.Size = new System.Drawing.Size(64, 20);
      this._btnFilterReset.TabIndex = 2;
      this._btnFilterReset.Text = "Сброс";
      this._btnFilterReset.UseVisualStyleBackColor = true;
      // 
      // _btnFilterHelp
      // 
      this._btnFilterHelp.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnFilterHelp.Location = new System.Drawing.Point(425, 3);
      this._btnFilterHelp.Name = "_btnFilterHelp";
      this._btnFilterHelp.Size = new System.Drawing.Size(26, 20);
      this._btnFilterHelp.TabIndex = 3;
      this._btnFilterHelp.Text = "?";
      this._btnFilterHelp.UseVisualStyleBackColor = true;
      // 
      // _stateFilterRow
      // 
      this._stateFilterRow.AutoSize = true;
      this._stateFilterRow.Controls.Add(this._rbStateAll);
      this._stateFilterRow.Controls.Add(this._rbStateOn);
      this._stateFilterRow.Controls.Add(this._rbStateOff);
      this._stateFilterRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._stateFilterRow.Location = new System.Drawing.Point(10, 46);
      this._stateFilterRow.Margin = new System.Windows.Forms.Padding(0, 4, 0, 0);
      this._stateFilterRow.Name = "_stateFilterRow";
      this._stateFilterRow.Size = new System.Drawing.Size(460, 26);
      this._stateFilterRow.TabIndex = 1;
      this._stateFilterRow.WrapContents = false;
      // 
      // _rbStateAll
      // 
      this._rbStateAll.AutoSize = true;
      this._rbStateAll.Checked = true;
      this._rbStateAll.Location = new System.Drawing.Point(3, 3);
      this._rbStateAll.Name = "_rbStateAll";
      this._rbStateAll.Size = new System.Drawing.Size(44, 17);
      this._rbStateAll.TabIndex = 0;
      this._rbStateAll.TabStop = true;
      this._rbStateAll.Text = "Все";
      this._rbStateAll.UseVisualStyleBackColor = true;
      this._rbStateAll.CheckedChanged += new System.EventHandler(this.RbStateAll_CheckedChanged);
      // 
      // _rbStateOn
      // 
      this._rbStateOn.AutoSize = true;
      this._rbStateOn.Location = new System.Drawing.Point(53, 3);
      this._rbStateOn.Name = "_rbStateOn";
      this._rbStateOn.Size = new System.Drawing.Size(44, 17);
      this._rbStateOn.TabIndex = 1;
      this._rbStateOn.Text = "Вкл";
      this._rbStateOn.UseVisualStyleBackColor = true;
      this._rbStateOn.CheckedChanged += new System.EventHandler(this.RbStateOn_CheckedChanged);
      // 
      // _rbStateOff
      // 
      this._rbStateOff.AutoSize = true;
      this._rbStateOff.Location = new System.Drawing.Point(103, 3);
      this._rbStateOff.Name = "_rbStateOff";
      this._rbStateOff.Size = new System.Drawing.Size(52, 17);
      this._rbStateOff.TabIndex = 2;
      this._rbStateOff.Text = "Выкл";
      this._rbStateOff.UseVisualStyleBackColor = true;
      this._rbStateOff.CheckedChanged += new System.EventHandler(this.RbStateOff_CheckedChanged);
      // 
      // _clb
      // 
      this._clb.CheckOnClick = true;
      this._clb.Dock = System.Windows.Forms.DockStyle.Fill;
      this._clb.Font = new System.Drawing.Font("Segoe UI", 9F);
      this._clb.FormattingEnabled = true;
      this._clb.IntegralHeight = false;
      this._clb.Location = new System.Drawing.Point(13, 75);
      this._clb.Name = "_clb";
      this._clb.Size = new System.Drawing.Size(454, 288);
      this._clb.TabIndex = 2;
      this._clb.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.OnItemCheck);
      this._clb.DrawItem += new System.Windows.Forms.DrawItemEventHandler(this.OnDrawItem);
      // 
      // _bottomRow
      // 
      this._bottomRow.ColumnCount = 2;
      this._bottomRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
      this._bottomRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
      this._bottomRow.Controls.Add(this._bulkPanel, 0, 0);
      this._bottomRow.Controls.Add(this._btnPanel, 1, 0);
      this._bottomRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._bottomRow.Location = new System.Drawing.Point(10, 374);
      this._bottomRow.Margin = new System.Windows.Forms.Padding(0, 8, 0, 0);
      this._bottomRow.Name = "_bottomRow";
      this._bottomRow.RowCount = 1;
      this._bottomRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._bottomRow.Size = new System.Drawing.Size(460, 36);
      this._bottomRow.TabIndex = 3;
      // 
      // _bulkPanel
      // 
      this._bulkPanel.AutoSize = true;
      this._bulkPanel.Controls.Add(this._rbSelectAll);
      this._bulkPanel.Controls.Add(this._rbClearAll);
      this._bulkPanel.Dock = System.Windows.Forms.DockStyle.Fill;
      this._bulkPanel.Location = new System.Drawing.Point(0, 0);
      this._bulkPanel.Margin = new System.Windows.Forms.Padding(0);
      this._bulkPanel.Name = "_bulkPanel";
      this._bulkPanel.Size = new System.Drawing.Size(230, 36);
      this._bulkPanel.TabIndex = 0;
      this._bulkPanel.WrapContents = false;
      // 
      // _rbSelectAll
      // 
      this._rbSelectAll.AutoSize = true;
      this._rbSelectAll.Location = new System.Drawing.Point(3, 3);
      this._rbSelectAll.Name = "_rbSelectAll";
      this._rbSelectAll.Size = new System.Drawing.Size(90, 17);
      this._rbSelectAll.TabIndex = 0;
      this._rbSelectAll.Text = "Выбрать все";
      this._rbSelectAll.UseVisualStyleBackColor = true;
      this._rbSelectAll.CheckedChanged += new System.EventHandler(this.RbSelectAll_CheckedChanged);
      // 
      // _rbClearAll
      // 
      this._rbClearAll.AutoSize = true;
      this._rbClearAll.Location = new System.Drawing.Point(99, 3);
      this._rbClearAll.Name = "_rbClearAll";
      this._rbClearAll.Size = new System.Drawing.Size(94, 17);
      this._rbClearAll.TabIndex = 1;
      this._rbClearAll.Text = "Сбросить все";
      this._rbClearAll.UseVisualStyleBackColor = true;
      this._rbClearAll.CheckedChanged += new System.EventHandler(this.RbClearAll_CheckedChanged);
      // 
      // _btnPanel
      // 
      this._btnPanel.Controls.Add(this._btnClose);
      this._btnPanel.Controls.Add(this._btnApply);
      this._btnPanel.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnPanel.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
      this._btnPanel.Location = new System.Drawing.Point(230, 0);
      this._btnPanel.Margin = new System.Windows.Forms.Padding(0);
      this._btnPanel.Name = "_btnPanel";
      this._btnPanel.Size = new System.Drawing.Size(230, 36);
      this._btnPanel.TabIndex = 1;
      this._btnPanel.WrapContents = false;
      // 
      // _btnClose
      // 
      this._btnClose.AutoSize = true;
      this._btnClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
      this._btnClose.Location = new System.Drawing.Point(152, 3);
      this._btnClose.Name = "_btnClose";
      this._btnClose.Size = new System.Drawing.Size(75, 23);
      this._btnClose.TabIndex = 1;
      this._btnClose.Text = "Закрыть";
      this._btnClose.UseVisualStyleBackColor = true;
      // 
      // _btnApply
      // 
      this._btnApply.AutoSize = true;
      this._btnApply.Location = new System.Drawing.Point(71, 3);
      this._btnApply.Name = "_btnApply";
      this._btnApply.Size = new System.Drawing.Size(75, 23);
      this._btnApply.TabIndex = 0;
      this._btnApply.Text = "Применить";
      this._btnApply.UseVisualStyleBackColor = true;
      this._btnApply.Click += new System.EventHandler(this.OnApplyClick);
      // 
      // VelumEnvironmentMetricsPickerForm
      // 
      this.AcceptButton = this._btnApply;
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.CancelButton = this._btnClose;
      this.ClientSize = new System.Drawing.Size(480, 420);
      this.Controls.Add(this._layout);
      this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
      this.MinimizeBox = false;
      this.MinimumSize = new System.Drawing.Size(420, 320);
      this.Name = "VelumEnvironmentMetricsPickerForm";
      this.ShowInTaskbar = false;
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
      this.Text = "Метрики среды";
      this.Shown += new System.EventHandler(this.Form_Shown);
      this._layout.ResumeLayout(false);
      this._layout.PerformLayout();
      this._filterRow.ResumeLayout(false);
      this._filterRow.PerformLayout();
      this._stateFilterRow.ResumeLayout(false);
      this._stateFilterRow.PerformLayout();
      this._bottomRow.ResumeLayout(false);
      this._bottomRow.PerformLayout();
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
    private System.Windows.Forms.Button _btnFilterApply;
    private System.Windows.Forms.Button _btnFilterReset;
    private System.Windows.Forms.Button _btnFilterHelp;
    private System.Windows.Forms.FlowLayoutPanel _stateFilterRow;
    private System.Windows.Forms.RadioButton _rbStateAll;
    private System.Windows.Forms.RadioButton _rbStateOn;
    private System.Windows.Forms.RadioButton _rbStateOff;
    private System.Windows.Forms.CheckedListBox _clb;
    private System.Windows.Forms.TableLayoutPanel _bottomRow;
    private System.Windows.Forms.FlowLayoutPanel _bulkPanel;
    private System.Windows.Forms.RadioButton _rbSelectAll;
    private System.Windows.Forms.RadioButton _rbClearAll;
    private System.Windows.Forms.FlowLayoutPanel _btnPanel;
    private System.Windows.Forms.Button _btnClose;
    private System.Windows.Forms.Button _btnApply;
  }
}
