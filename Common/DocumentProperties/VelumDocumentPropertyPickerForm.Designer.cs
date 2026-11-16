namespace Velum.UI
{
  internal sealed partial class VelumDocumentPropertyPickerForm
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
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VelumDocumentPropertyPickerForm));
      this._layout = new System.Windows.Forms.TableLayoutPanel();
      this._grid = new System.Windows.Forms.DataGridView();
      this._colPropertyName = new System.Windows.Forms.DataGridViewTextBoxColumn();
      this._colPropertyValue = new System.Windows.Forms.DataGridViewTextBoxColumn();
      this._buttonsPanel = new System.Windows.Forms.FlowLayoutPanel();
      this._btnCancel = new System.Windows.Forms.Button();
      this._btnOk = new System.Windows.Forms.Button();
      this._layout.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)(this._grid)).BeginInit();
      this._buttonsPanel.SuspendLayout();
      this.SuspendLayout();
      // 
      // _layout
      // 
      this._layout.ColumnCount = 1;
      this._layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._layout.Controls.Add(this._grid, 0, 0);
      this._layout.Controls.Add(this._buttonsPanel, 0, 1);
      this._layout.Dock = System.Windows.Forms.DockStyle.Fill;
      this._layout.Location = new System.Drawing.Point(0, 0);
      this._layout.Name = "_layout";
      this._layout.Padding = new System.Windows.Forms.Padding(10);
      this._layout.RowCount = 2;
      this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 44F));
      this._layout.Size = new System.Drawing.Size(520, 360);
      this._layout.TabIndex = 0;
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
            this._colPropertyName,
            this._colPropertyValue});
      this._grid.Dock = System.Windows.Forms.DockStyle.Fill;
      this._grid.Location = new System.Drawing.Point(13, 13);
      this._grid.MultiSelect = false;
      this._grid.Name = "_grid";
      this._grid.ReadOnly = true;
      this._grid.RowHeadersVisible = false;
      this._grid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
      this._grid.Size = new System.Drawing.Size(494, 290);
      this._grid.TabIndex = 0;
      this._grid.DoubleClick += new System.EventHandler(this.Grid_DoubleClick);
      this._grid.KeyDown += new System.Windows.Forms.KeyEventHandler(this.Grid_KeyDown);
      // 
      // _colPropertyName
      // 
      this._colPropertyName.FillWeight = 42F;
      this._colPropertyName.HeaderText = "Свойство";
      this._colPropertyName.Name = "PropertyName";
      this._colPropertyName.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
      // 
      // _colPropertyValue
      // 
      this._colPropertyValue.FillWeight = 58F;
      this._colPropertyValue.HeaderText = "Значение";
      this._colPropertyValue.Name = "PropertyValue";
      this._colPropertyValue.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
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
      this._buttonsPanel.Size = new System.Drawing.Size(500, 44);
      this._buttonsPanel.TabIndex = 1;
      // 
      // _btnCancel
      // 
      this._btnCancel.AutoSize = true;
      this._btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
      this._btnCancel.Location = new System.Drawing.Point(422, 11);
      this._btnCancel.Name = "_btnCancel";
      this._btnCancel.Size = new System.Drawing.Size(75, 23);
      this._btnCancel.TabIndex = 1;
      this._btnCancel.Text = "Отмена";
      this._btnCancel.UseVisualStyleBackColor = true;
      // 
      // _btnOk
      // 
      this._btnOk.AutoSize = true;
      this._btnOk.Location = new System.Drawing.Point(341, 11);
      this._btnOk.Name = "_btnOk";
      this._btnOk.Size = new System.Drawing.Size(75, 23);
      this._btnOk.TabIndex = 0;
      this._btnOk.Text = "Вставить";
      this._btnOk.UseVisualStyleBackColor = true;
      this._btnOk.Click += new System.EventHandler(this.BtnOk_Click);
      // 
      // VelumDocumentPropertyPickerForm
      // 
      this.AcceptButton = this._btnOk;
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.CancelButton = this._btnCancel;
      this.ClientSize = new System.Drawing.Size(520, 360);
      this.Controls.Add(this._layout);
      this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
      this.MaximizeBox = false;
      this.MinimizeBox = false;
      this.Name = "VelumDocumentPropertyPickerForm";
      this.ShowInTaskbar = false;
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
      this.Text = "Свойства детали";
      this._layout.ResumeLayout(false);
      ((System.ComponentModel.ISupportInitialize)(this._grid)).EndInit();
      this._buttonsPanel.ResumeLayout(false);
      this._buttonsPanel.PerformLayout();
      this.ResumeLayout(false);

    }

    #endregion

    private System.Windows.Forms.TableLayoutPanel _layout;
    private System.Windows.Forms.DataGridView _grid;
    private System.Windows.Forms.DataGridViewTextBoxColumn _colPropertyName;
    private System.Windows.Forms.DataGridViewTextBoxColumn _colPropertyValue;
    private System.Windows.Forms.FlowLayoutPanel _buttonsPanel;
    private System.Windows.Forms.Button _btnCancel;
    private System.Windows.Forms.Button _btnOk;
  }
}
