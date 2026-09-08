namespace Velum.UI
{
  partial class VelumSensorPrimariesBufferForm
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
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VelumSensorPrimariesBufferForm));
      this._rootLayout = new System.Windows.Forms.TableLayoutPanel();
      this._listEntries = new System.Windows.Forms.ListBox();
      this._flowButtons = new System.Windows.Forms.FlowLayoutPanel();
      this._btnClose = new System.Windows.Forms.Button();
      this._btnAdd = new System.Windows.Forms.Button();
      this._btnClearAll = new System.Windows.Forms.Button();
      this._btnRemoveSelected = new System.Windows.Forms.Button();
      this._rootLayout.SuspendLayout();
      this._flowButtons.SuspendLayout();
      this.SuspendLayout();
      // 
      // _rootLayout
      // 
      this._rootLayout.ColumnCount = 1;
      this._rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._rootLayout.Controls.Add(this._listEntries, 0, 0);
      this._rootLayout.Controls.Add(this._flowButtons, 0, 1);
      this._rootLayout.Dock = System.Windows.Forms.DockStyle.Fill;
      this._rootLayout.Location = new System.Drawing.Point(0, 0);
      this._rootLayout.Margin = new System.Windows.Forms.Padding(0);
      this._rootLayout.Name = "_rootLayout";
      this._rootLayout.Padding = new System.Windows.Forms.Padding(10);
      this._rootLayout.RowCount = 2;
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 44F));
      this._rootLayout.Size = new System.Drawing.Size(380, 300);
      this._rootLayout.TabIndex = 0;
      // 
      // _listEntries
      // 
      this._listEntries.Dock = System.Windows.Forms.DockStyle.Fill;
      this._listEntries.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
      this._listEntries.FormattingEnabled = true;
      this._listEntries.IntegralHeight = false;
      this._listEntries.ItemHeight = 14;
      this._listEntries.Location = new System.Drawing.Point(13, 13);
      this._listEntries.Margin = new System.Windows.Forms.Padding(3, 3, 3, 6);
      this._listEntries.Name = "_listEntries";
      this._listEntries.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
      this._listEntries.Size = new System.Drawing.Size(354, 227);
      this._listEntries.TabIndex = 0;
      this._listEntries.KeyDown += new System.Windows.Forms.KeyEventHandler(this.ListEntries_KeyDown);
      // 
      // _flowButtons
      // 
      this._flowButtons.AutoSize = true;
      this._flowButtons.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
      this._flowButtons.Controls.Add(this._btnClose);
      this._flowButtons.Controls.Add(this._btnAdd);
      this._flowButtons.Controls.Add(this._btnClearAll);
      this._flowButtons.Controls.Add(this._btnRemoveSelected);
      this._flowButtons.Dock = System.Windows.Forms.DockStyle.Fill;
      this._flowButtons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
      this._flowButtons.Location = new System.Drawing.Point(10, 246);
      this._flowButtons.Margin = new System.Windows.Forms.Padding(0);
      this._flowButtons.Name = "_flowButtons";
      this._flowButtons.Padding = new System.Windows.Forms.Padding(0, 8, 0, 0);
      this._flowButtons.Size = new System.Drawing.Size(360, 44);
      this._flowButtons.TabIndex = 1;
      this._flowButtons.WrapContents = false;
      // 
      // _btnClose
      // 
      this._btnClose.AutoSize = true;
      this._btnClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
      this._btnClose.Location = new System.Drawing.Point(287, 11);
      this._btnClose.Margin = new System.Windows.Forms.Padding(3, 3, 0, 3);
      this._btnClose.Name = "_btnClose";
      this._btnClose.Size = new System.Drawing.Size(73, 23);
      this._btnClose.TabIndex = 3;
      this._btnClose.Text = "Закрыть";
      this._btnClose.UseVisualStyleBackColor = true;
      // 
      // _btnAdd
      // 
      this._btnAdd.AutoSize = true;
      this._btnAdd.Location = new System.Drawing.Point(152, 11);
      this._btnAdd.Name = "_btnAdd";
      this._btnAdd.Size = new System.Drawing.Size(129, 23);
      this._btnAdd.TabIndex = 2;
      this._btnAdd.Text = "Добавить сенсоры";
      this._btnAdd.UseVisualStyleBackColor = true;
      this._btnAdd.Click += new System.EventHandler(this.BtnAdd_Click);
      // 
      // _btnClearAll
      // 
      this._btnClearAll.AutoSize = true;
      this._btnClearAll.Location = new System.Drawing.Point(73, 11);
      this._btnClearAll.Name = "_btnClearAll";
      this._btnClearAll.Size = new System.Drawing.Size(73, 23);
      this._btnClearAll.TabIndex = 1;
      this._btnClearAll.Text = "Очистить";
      this._btnClearAll.UseVisualStyleBackColor = true;
      this._btnClearAll.Click += new System.EventHandler(this.BtnClearAll_Click);
      // 
      // _btnRemoveSelected
      // 
      this._btnRemoveSelected.AutoSize = true;
      this._btnRemoveSelected.Location = new System.Drawing.Point(0, 11);
      this._btnRemoveSelected.Margin = new System.Windows.Forms.Padding(0, 3, 3, 3);
      this._btnRemoveSelected.Name = "_btnRemoveSelected";
      this._btnRemoveSelected.Size = new System.Drawing.Size(67, 23);
      this._btnRemoveSelected.TabIndex = 0;
      this._btnRemoveSelected.Text = "Удалить";
      this._btnRemoveSelected.UseVisualStyleBackColor = true;
      this._btnRemoveSelected.Click += new System.EventHandler(this.BtnRemoveSelected_Click);
      // 
      // VelumSensorPrimariesBufferForm
      // 
      this.AcceptButton = this._btnAdd;
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.CancelButton = this._btnClose;
      this.ClientSize = new System.Drawing.Size(380, 300);
      this.Controls.Add(this._rootLayout);
      this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
      this.MaximizeBox = false;
      this.MinimizeBox = false;
      this.MinimumSize = new System.Drawing.Size(320, 260);
      this.Name = "VelumSensorPrimariesBufferForm";
      this.ShowInTaskbar = false;
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
      this.Text = "Новые первичники командного канала";
      this._rootLayout.ResumeLayout(false);
      this._rootLayout.PerformLayout();
      this._flowButtons.ResumeLayout(false);
      this._flowButtons.PerformLayout();
      this.ResumeLayout(false);

    }

    #endregion

    private System.Windows.Forms.TableLayoutPanel _rootLayout;
    private System.Windows.Forms.ListBox _listEntries;
    private System.Windows.Forms.FlowLayoutPanel _flowButtons;
    private System.Windows.Forms.Button _btnRemoveSelected;
    private System.Windows.Forms.Button _btnClearAll;
    private System.Windows.Forms.Button _btnAdd;
    private System.Windows.Forms.Button _btnClose;
  }
}
