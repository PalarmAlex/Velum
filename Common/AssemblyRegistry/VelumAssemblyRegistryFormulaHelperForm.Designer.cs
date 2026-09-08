namespace Velum.UI
{
  internal sealed partial class VelumAssemblyRegistryFormulaHelperForm
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
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VelumAssemblyRegistryFormulaHelperForm));
      this._layout = new System.Windows.Forms.TableLayoutPanel();
      this._list = new System.Windows.Forms.ListView();
      this._colItem = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
      this._colDesc = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
      this._buttonsRow = new System.Windows.Forms.FlowLayoutPanel();
      this._btnClose = new System.Windows.Forms.Button();
      this._btnInsert = new System.Windows.Forms.Button();
      this._layout.SuspendLayout();
      this._buttonsRow.SuspendLayout();
      this.SuspendLayout();
      // 
      // _layout
      // 
      this._layout.ColumnCount = 1;
      this._layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._layout.Controls.Add(this._list, 0, 0);
      this._layout.Controls.Add(this._buttonsRow, 0, 1);
      this._layout.Dock = System.Windows.Forms.DockStyle.Fill;
      this._layout.Location = new System.Drawing.Point(0, 0);
      this._layout.Name = "_layout";
      this._layout.Padding = new System.Windows.Forms.Padding(12);
      this._layout.RowCount = 2;
      this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._layout.Size = new System.Drawing.Size(480, 360);
      this._layout.TabIndex = 0;
      // 
      // _list
      // 
      this._list.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this._colItem,
            this._colDesc});
      this._list.Dock = System.Windows.Forms.DockStyle.Fill;
      this._list.FullRowSelect = true;
      this._list.GridLines = true;
      this._list.HideSelection = false;
      this._list.Location = new System.Drawing.Point(15, 15);
      this._list.MultiSelect = false;
      this._list.Name = "_list";
      this._list.Size = new System.Drawing.Size(450, 284);
      this._list.TabIndex = 0;
      this._list.UseCompatibleStateImageBehavior = false;
      this._list.View = System.Windows.Forms.View.Details;
      this._list.DoubleClick += new System.EventHandler(this.OnListDoubleClick);
      // 
      // _colItem
      // 
      this._colItem.Text = "Подстановка";
      this._colItem.Width = 160;
      // 
      // _colDesc
      // 
      this._colDesc.Text = "Описание";
      this._colDesc.Width = 260;
      // 
      // _buttonsRow
      // 
      this._buttonsRow.Controls.Add(this._btnClose);
      this._buttonsRow.Controls.Add(this._btnInsert);
      this._buttonsRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._buttonsRow.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
      this._buttonsRow.Location = new System.Drawing.Point(15, 305);
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
      // _btnInsert
      // 
      this._btnInsert.Location = new System.Drawing.Point(251, 11);
      this._btnInsert.Name = "_btnInsert";
      this._btnInsert.Size = new System.Drawing.Size(95, 28);
      this._btnInsert.TabIndex = 0;
      this._btnInsert.Text = "Вставить";
      this._btnInsert.UseVisualStyleBackColor = true;
      this._btnInsert.Click += new System.EventHandler(this.OnInsert);
      // 
      // VelumAssemblyRegistryFormulaHelperForm
      // 
      this.AcceptButton = this._btnInsert;
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.CancelButton = this._btnClose;
      this.ClientSize = new System.Drawing.Size(480, 360);
      this.Controls.Add(this._layout);
      this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
      this.MinimizeBox = false;
      this.Name = "VelumAssemblyRegistryFormulaHelperForm";
      this.ShowInTaskbar = false;
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
      this.Text = "Подстановки формулы";
      this._layout.ResumeLayout(false);
      this._buttonsRow.ResumeLayout(false);
      this.ResumeLayout(false);

    }

    #endregion

    private System.Windows.Forms.TableLayoutPanel _layout;
    private System.Windows.Forms.ListView _list;
    private System.Windows.Forms.ColumnHeader _colItem;
    private System.Windows.Forms.ColumnHeader _colDesc;
    private System.Windows.Forms.FlowLayoutPanel _buttonsRow;
    private System.Windows.Forms.Button _btnClose;
    private System.Windows.Forms.Button _btnInsert;
  }
}
