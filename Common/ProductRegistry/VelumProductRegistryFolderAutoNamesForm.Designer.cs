namespace Velum.UI
{
  internal sealed partial class VelumProductRegistryFolderAutoNamesForm
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
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VelumProductRegistryFolderAutoNamesForm));
      this._layout = new System.Windows.Forms.TableLayoutPanel();
      this._grid = new System.Windows.Forms.DataGridView();
      this._colExtension = new System.Windows.Forms.DataGridViewTextBoxColumn();
      this._colFolderName = new System.Windows.Forms.DataGridViewTextBoxColumn();
      this._buttonsRow = new System.Windows.Forms.FlowLayoutPanel();
      this._btnClose = new System.Windows.Forms.Button();
      this._btnApply = new System.Windows.Forms.Button();
      this._layout.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)(this._grid)).BeginInit();
      this._buttonsRow.SuspendLayout();
      this.SuspendLayout();
      // 
      // _layout
      // 
      this._layout.ColumnCount = 1;
      this._layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._layout.Controls.Add(this._grid, 0, 0);
      this._layout.Controls.Add(this._buttonsRow, 0, 1);
      this._layout.Dock = System.Windows.Forms.DockStyle.Fill;
      this._layout.Location = new System.Drawing.Point(0, 0);
      this._layout.Name = "_layout";
      this._layout.Padding = new System.Windows.Forms.Padding(12);
      this._layout.RowCount = 2;
      this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._layout.Size = new System.Drawing.Size(480, 420);
      this._layout.TabIndex = 0;
      // 
      // _grid
      // 
      this._grid.AllowUserToResizeRows = false;
      this._grid.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
      this._grid.BackgroundColor = System.Drawing.SystemColors.Window;
      this._grid.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
      this._grid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
      this._grid.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this._colExtension,
            this._colFolderName});
      this._grid.Dock = System.Windows.Forms.DockStyle.Fill;
      this._grid.Location = new System.Drawing.Point(15, 15);
      this._grid.MultiSelect = false;
      this._grid.Name = "_grid";
      this._grid.RowHeadersVisible = false;
      this._grid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
      this._grid.Size = new System.Drawing.Size(450, 344);
      this._grid.TabIndex = 0;
      // 
      // _colExtension
      // 
      this._colExtension.FillWeight = 40F;
      this._colExtension.HeaderText = "Расширение";
      this._colExtension.Name = "_colExtension";
      // 
      // _colFolderName
      // 
      this._colFolderName.FillWeight = 60F;
      this._colFolderName.HeaderText = "Имя каталога";
      this._colFolderName.Name = "_colFolderName";
      // 
      // _buttonsRow
      // 
      this._buttonsRow.Controls.Add(this._btnClose);
      this._buttonsRow.Controls.Add(this._btnApply);
      this._buttonsRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._buttonsRow.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
      this._buttonsRow.Location = new System.Drawing.Point(15, 365);
      this._buttonsRow.Name = "_buttonsRow";
      this._buttonsRow.Padding = new System.Windows.Forms.Padding(0, 8, 0, 0);
      this._buttonsRow.Size = new System.Drawing.Size(450, 40);
      this._buttonsRow.TabIndex = 1;
      // 
      // _btnClose
      // 
      this._btnClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
      this._btnClose.Location = new System.Drawing.Point(352, 11);
      this._btnClose.Name = "_btnClose";
      this._btnClose.Size = new System.Drawing.Size(95, 28);
      this._btnClose.TabIndex = 1;
      this._btnClose.Text = "Закрыть";
      this._btnClose.UseVisualStyleBackColor = true;
      this._btnClose.Click += new System.EventHandler(this.OnCloseClick);
      // 
      // _btnApply
      // 
      this._btnApply.Location = new System.Drawing.Point(251, 11);
      this._btnApply.Name = "_btnApply";
      this._btnApply.Size = new System.Drawing.Size(95, 28);
      this._btnApply.TabIndex = 0;
      this._btnApply.Text = "Применить";
      this._btnApply.UseVisualStyleBackColor = true;
      this._btnApply.Click += new System.EventHandler(this.OnApply);
      // 
      // VelumProductRegistryFolderAutoNamesForm
      // 
      this.AcceptButton = this._btnApply;
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.CancelButton = this._btnClose;
      this.ClientSize = new System.Drawing.Size(480, 420);
      this.Controls.Add(this._layout);
      this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
      this.MinimizeBox = false;
      this.Name = "VelumProductRegistryFolderAutoNamesForm";
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
      this.Text = "Автоимена каталогов";
      this._layout.ResumeLayout(false);
      ((System.ComponentModel.ISupportInitialize)(this._grid)).EndInit();
      this._buttonsRow.ResumeLayout(false);
      this.ResumeLayout(false);

    }

    #endregion

    private System.Windows.Forms.TableLayoutPanel _layout;
    private System.Windows.Forms.DataGridView _grid;
    private System.Windows.Forms.DataGridViewTextBoxColumn _colExtension;
    private System.Windows.Forms.DataGridViewTextBoxColumn _colFolderName;
    private System.Windows.Forms.FlowLayoutPanel _buttonsRow;
    private System.Windows.Forms.Button _btnClose;
    private System.Windows.Forms.Button _btnApply;
  }
}
