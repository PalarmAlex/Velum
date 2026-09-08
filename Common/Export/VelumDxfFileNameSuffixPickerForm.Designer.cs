namespace Velum.UI
{
  internal sealed partial class VelumDxfFileNameSuffixPickerForm
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
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VelumDxfFileNameSuffixPickerForm));
      this._layout = new System.Windows.Forms.TableLayoutPanel();
      this._filterRow = new System.Windows.Forms.TableLayoutPanel();
      this._txtFilter = new System.Windows.Forms.TextBox();
      this._btnFilterApply = new System.Windows.Forms.Button();
      this._btnFilterReset = new System.Windows.Forms.Button();
      this._btnFilterHelp = new System.Windows.Forms.Button();
      this._grid = new System.Windows.Forms.DataGridView();
      this.SuffixName = new System.Windows.Forms.DataGridViewTextBoxColumn();
      this._buttonsPanel = new System.Windows.Forms.FlowLayoutPanel();
      this._btnCancel = new System.Windows.Forms.Button();
      this._btnOk = new System.Windows.Forms.Button();
      this._layout.SuspendLayout();
      this._filterRow.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)(this._grid)).BeginInit();
      this._buttonsPanel.SuspendLayout();
      this.SuspendLayout();
      // 
      // _layout
      // 
      this._layout.ColumnCount = 1;
      this._layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._layout.Controls.Add(this._filterRow, 0, 0);
      this._layout.Controls.Add(this._grid, 0, 1);
      this._layout.Controls.Add(this._buttonsPanel, 0, 2);
      this._layout.Dock = System.Windows.Forms.DockStyle.Fill;
      this._layout.Location = new System.Drawing.Point(0, 0);
      this._layout.Name = "_layout";
      this._layout.Padding = new System.Windows.Forms.Padding(10);
      this._layout.RowCount = 3;
      this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32F));
      this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 44F));
      this._layout.Size = new System.Drawing.Size(420, 360);
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
      this._filterRow.Size = new System.Drawing.Size(394, 26);
      this._filterRow.TabIndex = 0;
      // 
      // _txtFilter
      // 
      this._txtFilter.Dock = System.Windows.Forms.DockStyle.Fill;
      this._txtFilter.Location = new System.Drawing.Point(3, 3);
      this._txtFilter.Name = "_txtFilter";
      this._txtFilter.Size = new System.Drawing.Size(196, 20);
      this._txtFilter.TabIndex = 0;
      // 
      // _btnFilterApply
      // 
      this._btnFilterApply.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnFilterApply.Location = new System.Drawing.Point(205, 3);
      this._btnFilterApply.Name = "_btnFilterApply";
      this._btnFilterApply.Size = new System.Drawing.Size(84, 20);
      this._btnFilterApply.TabIndex = 1;
      this._btnFilterApply.Text = "Применить";
      this._btnFilterApply.UseVisualStyleBackColor = true;
      // 
      // _btnFilterReset
      // 
      this._btnFilterReset.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnFilterReset.Location = new System.Drawing.Point(295, 3);
      this._btnFilterReset.Name = "_btnFilterReset";
      this._btnFilterReset.Size = new System.Drawing.Size(64, 20);
      this._btnFilterReset.TabIndex = 2;
      this._btnFilterReset.Text = "Сброс";
      this._btnFilterReset.UseVisualStyleBackColor = true;
      // 
      // _btnFilterHelp
      // 
      this._btnFilterHelp.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnFilterHelp.Location = new System.Drawing.Point(365, 3);
      this._btnFilterHelp.Name = "_btnFilterHelp";
      this._btnFilterHelp.Size = new System.Drawing.Size(26, 20);
      this._btnFilterHelp.TabIndex = 3;
      this._btnFilterHelp.Text = "?";
      this._btnFilterHelp.UseVisualStyleBackColor = true;
      // 
      // _grid
      // 
      this._grid.AllowUserToAddRows = false;
      this._grid.AllowUserToDeleteRows = false;
      this._grid.AllowUserToResizeRows = false;
      this._grid.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
      this._grid.BackgroundColor = System.Drawing.SystemColors.Window;
      this._grid.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
      this._grid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
      this._grid.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.SuffixName});
      this._grid.Dock = System.Windows.Forms.DockStyle.Fill;
      this._grid.Location = new System.Drawing.Point(13, 41);
      this._grid.MultiSelect = false;
      this._grid.Name = "_grid";
      this._grid.ReadOnly = true;
      this._grid.RowHeadersVisible = false;
      this._grid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
      this._grid.Size = new System.Drawing.Size(394, 262);
      this._grid.TabIndex = 1;
      this._grid.DoubleClick += new System.EventHandler(this.Grid_DoubleClick);
      this._grid.KeyDown += new System.Windows.Forms.KeyEventHandler(this.Grid_KeyDown);
      // 
      // SuffixName
      // 
      this.SuffixName.HeaderText = "Суффикс";
      this.SuffixName.Name = "SuffixName";
      this.SuffixName.ReadOnly = true;
      // 
      // _buttonsPanel
      // 
      this._buttonsPanel.Controls.Add(this._btnCancel);
      this._buttonsPanel.Controls.Add(this._btnOk);
      this._buttonsPanel.Dock = System.Windows.Forms.DockStyle.Fill;
      this._buttonsPanel.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
      this._buttonsPanel.Location = new System.Drawing.Point(10, 306);
      this._buttonsPanel.Margin = new System.Windows.Forms.Padding(0);
      this._buttonsPanel.Name = "_buttonsPanel";
      this._buttonsPanel.Padding = new System.Windows.Forms.Padding(0, 8, 0, 0);
      this._buttonsPanel.Size = new System.Drawing.Size(400, 44);
      this._buttonsPanel.TabIndex = 2;
      // 
      // _btnCancel
      // 
      this._btnCancel.AutoSize = true;
      this._btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
      this._btnCancel.Location = new System.Drawing.Point(322, 11);
      this._btnCancel.Name = "_btnCancel";
      this._btnCancel.Size = new System.Drawing.Size(75, 23);
      this._btnCancel.TabIndex = 1;
      this._btnCancel.Text = "Отмена";
      this._btnCancel.UseVisualStyleBackColor = true;
      // 
      // _btnOk
      // 
      this._btnOk.AutoSize = true;
      this._btnOk.Location = new System.Drawing.Point(241, 11);
      this._btnOk.Name = "_btnOk";
      this._btnOk.Size = new System.Drawing.Size(75, 23);
      this._btnOk.TabIndex = 0;
      this._btnOk.Text = "Вставить";
      this._btnOk.UseVisualStyleBackColor = true;
      this._btnOk.Click += new System.EventHandler(this.BtnOk_Click);
      // 
      // VelumDxfFileNameSuffixPickerForm
      // 
      this.AcceptButton = this._btnOk;
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.CancelButton = this._btnCancel;
      this.ClientSize = new System.Drawing.Size(420, 360);
      this.Controls.Add(this._layout);
      this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
      this.MaximizeBox = false;
      this.MinimizeBox = false;
      this.Name = "VelumDxfFileNameSuffixPickerForm";
      this.ShowInTaskbar = false;
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
      this.Text = "Суффиксы имени DXF";
      this._layout.ResumeLayout(false);
      this._layout.PerformLayout();
      this._filterRow.ResumeLayout(false);
      this._filterRow.PerformLayout();
      ((System.ComponentModel.ISupportInitialize)(this._grid)).EndInit();
      this._buttonsPanel.ResumeLayout(false);
      this._buttonsPanel.PerformLayout();
      this.ResumeLayout(false);

    }

    #endregion

    private System.Windows.Forms.TableLayoutPanel _layout;
    private System.Windows.Forms.TableLayoutPanel _filterRow;
    private System.Windows.Forms.TextBox _txtFilter;
    private System.Windows.Forms.Button _btnFilterApply;
    private System.Windows.Forms.Button _btnFilterReset;
    private System.Windows.Forms.Button _btnFilterHelp;
    private System.Windows.Forms.DataGridView _grid;
    private System.Windows.Forms.FlowLayoutPanel _buttonsPanel;
    private System.Windows.Forms.Button _btnCancel;
    private System.Windows.Forms.Button _btnOk;
    private System.Windows.Forms.DataGridViewTextBoxColumn SuffixName;
  }
}
