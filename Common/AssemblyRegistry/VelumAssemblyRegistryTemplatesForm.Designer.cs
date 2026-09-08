namespace Velum.UI
{
  internal sealed partial class VelumAssemblyRegistryTemplatesForm
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
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VelumAssemblyRegistryTemplatesForm));
      this._layout = new System.Windows.Forms.TableLayoutPanel();
      this._filterRow = new System.Windows.Forms.TableLayoutPanel();
      this._lblFilter = new System.Windows.Forms.Label();
      this._txtFilter = new System.Windows.Forms.TextBox();
      this._grid = new System.Windows.Forms.DataGridView();
      this._colName = new System.Windows.Forms.DataGridViewTextBoxColumn();
      this._buttonsRow = new System.Windows.Forms.FlowLayoutPanel();
      this._btnClose = new System.Windows.Forms.Button();
      this._btnApply = new System.Windows.Forms.Button();
      this._layout.SuspendLayout();
      this._filterRow.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)(this._grid)).BeginInit();
      this._buttonsRow.SuspendLayout();
      this.SuspendLayout();
      // 
      // _layout
      // 
      this._layout.ColumnCount = 1;
      this._layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._layout.Controls.Add(this._filterRow, 0, 0);
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
      this._layout.Size = new System.Drawing.Size(520, 420);
      this._layout.TabIndex = 0;
      // 
      // _filterRow
      // 
      this._filterRow.AutoSize = true;
      this._filterRow.ColumnCount = 2;
      this._filterRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._filterRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._filterRow.Controls.Add(this._lblFilter, 0, 0);
      this._filterRow.Controls.Add(this._txtFilter, 1, 0);
      this._filterRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._filterRow.Location = new System.Drawing.Point(15, 15);
      this._filterRow.Name = "_filterRow";
      this._filterRow.RowCount = 1;
      this._filterRow.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._filterRow.Size = new System.Drawing.Size(490, 26);
      this._filterRow.TabIndex = 0;
      // 
      // _lblFilter
      // 
      this._lblFilter.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._lblFilter.AutoSize = true;
      this._lblFilter.Location = new System.Drawing.Point(3, 6);
      this._lblFilter.Name = "_lblFilter";
      this._lblFilter.Size = new System.Drawing.Size(50, 13);
      this._lblFilter.TabIndex = 0;
      this._lblFilter.Text = "Фильтр:";
      // 
      // _txtFilter
      // 
      this._txtFilter.Dock = System.Windows.Forms.DockStyle.Fill;
      this._txtFilter.Location = new System.Drawing.Point(59, 3);
      this._txtFilter.Name = "_txtFilter";
      this._txtFilter.Size = new System.Drawing.Size(428, 20);
      this._txtFilter.TabIndex = 1;
      // 
      // _grid
      // 
      this._grid.AllowUserToResizeRows = false;
      this._grid.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
      this._grid.BackgroundColor = System.Drawing.SystemColors.Window;
      this._grid.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
      this._grid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
      this._grid.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this._colName});
      this._grid.Dock = System.Windows.Forms.DockStyle.Fill;
      this._grid.Location = new System.Drawing.Point(15, 47);
      this._grid.MultiSelect = false;
      this._grid.Name = "_grid";
      this._grid.RowHeadersVisible = false;
      this._grid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
      this._grid.Size = new System.Drawing.Size(490, 312);
      this._grid.TabIndex = 1;
      // 
      // _colName
      // 
      this._colName.HeaderText = "Имя шаблона";
      this._colName.Name = "_colName";
      // 
      // _buttonsRow
      // 
      this._buttonsRow.Controls.Add(this._btnClose);
      this._buttonsRow.Controls.Add(this._btnApply);
      this._buttonsRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._buttonsRow.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
      this._buttonsRow.Location = new System.Drawing.Point(15, 365);
      this._buttonsRow.Name = "_buttonsRow";
      this._buttonsRow.Padding = new System.Windows.Forms.Padding(0, 2, 0, 0);
      this._buttonsRow.Size = new System.Drawing.Size(490, 40);
      this._buttonsRow.TabIndex = 2;
      // 
      // _btnClose
      // 
      this._btnClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
      this._btnClose.Location = new System.Drawing.Point(392, 5);
      this._btnClose.Name = "_btnClose";
      this._btnClose.Size = new System.Drawing.Size(95, 28);
      this._btnClose.TabIndex = 1;
      this._btnClose.Text = "Закрыть";
      this._btnClose.UseVisualStyleBackColor = true;
      this._btnClose.Click += new System.EventHandler(this.OnCloseClick);
      // 
      // _btnApply
      // 
      this._btnApply.Location = new System.Drawing.Point(291, 5);
      this._btnApply.Name = "_btnApply";
      this._btnApply.Size = new System.Drawing.Size(95, 28);
      this._btnApply.TabIndex = 0;
      this._btnApply.Text = "Применить";
      this._btnApply.UseVisualStyleBackColor = true;
      this._btnApply.Click += new System.EventHandler(this.OnApply);
      // 
      // VelumAssemblyRegistryTemplatesForm
      // 
      this.AcceptButton = this._btnApply;
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.CancelButton = this._btnClose;
      this.ClientSize = new System.Drawing.Size(520, 420);
      this.Controls.Add(this._layout);
      this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
      this.MinimizeBox = false;
      this.Name = "VelumAssemblyRegistryTemplatesForm";
      this.ShowInTaskbar = false;
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
      this.Text = "Шаблоны столбцов";
      this._layout.ResumeLayout(false);
      this._layout.PerformLayout();
      this._filterRow.ResumeLayout(false);
      this._filterRow.PerformLayout();
      ((System.ComponentModel.ISupportInitialize)(this._grid)).EndInit();
      this._buttonsRow.ResumeLayout(false);
      this.ResumeLayout(false);

    }

    #endregion

    private System.Windows.Forms.TableLayoutPanel _layout;
    private System.Windows.Forms.TableLayoutPanel _filterRow;
    private System.Windows.Forms.Label _lblFilter;
    private System.Windows.Forms.TextBox _txtFilter;
    private System.Windows.Forms.DataGridView _grid;
    private System.Windows.Forms.DataGridViewTextBoxColumn _colName;
    private System.Windows.Forms.FlowLayoutPanel _buttonsRow;
    private System.Windows.Forms.Button _btnClose;
    private System.Windows.Forms.Button _btnApply;
  }
}
