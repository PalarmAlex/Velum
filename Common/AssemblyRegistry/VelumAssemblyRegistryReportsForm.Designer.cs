namespace Velum.UI
{
  internal sealed partial class VelumAssemblyRegistryReportsForm
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
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VelumAssemblyRegistryReportsForm));
      this._layout = new System.Windows.Forms.TableLayoutPanel();
      this._lblFilter = new System.Windows.Forms.Label();
      this._txtFilter = new System.Windows.Forms.TextBox();
      this._list = new System.Windows.Forms.ListBox();
      this._buttonsRow = new System.Windows.Forms.FlowLayoutPanel();
      this._btnClose = new System.Windows.Forms.Button();
      this._layout.SuspendLayout();
      this._buttonsRow.SuspendLayout();
      this.SuspendLayout();
      // 
      // _layout
      // 
      this._layout.ColumnCount = 2;
      this._layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._layout.Controls.Add(this._lblFilter, 0, 0);
      this._layout.Controls.Add(this._txtFilter, 1, 0);
      this._layout.Controls.Add(this._list, 0, 1);
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
      // _lblFilter
      // 
      this._lblFilter.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._lblFilter.AutoSize = true;
      this._lblFilter.Location = new System.Drawing.Point(15, 18);
      this._lblFilter.Name = "_lblFilter";
      this._lblFilter.Size = new System.Drawing.Size(85, 13);
      this._lblFilter.TabIndex = 0;
      this._lblFilter.Text = "Фильтр имени:";
      // 
      // _txtFilter
      // 
      this._txtFilter.Dock = System.Windows.Forms.DockStyle.Fill;
      this._txtFilter.Location = new System.Drawing.Point(106, 15);
      this._txtFilter.Name = "_txtFilter";
      this._txtFilter.Size = new System.Drawing.Size(399, 20);
      this._txtFilter.TabIndex = 1;
      this._txtFilter.TextChanged += new System.EventHandler(this.OnFilterChanged);
      // 
      // _list
      // 
      this._layout.SetColumnSpan(this._list, 2);
      this._list.Dock = System.Windows.Forms.DockStyle.Fill;
      this._list.FormattingEnabled = true;
      this._list.IntegralHeight = false;
      this._list.Location = new System.Drawing.Point(15, 41);
      this._list.Name = "_list";
      this._list.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
      this._list.Size = new System.Drawing.Size(490, 318);
      this._list.TabIndex = 2;
      this._list.DoubleClick += new System.EventHandler(this.OnListDoubleClick);
      this._list.KeyDown += new System.Windows.Forms.KeyEventHandler(this.OnListKeyDown);
      // 
      // _buttonsRow
      // 
      this._layout.SetColumnSpan(this._buttonsRow, 2);
      this._buttonsRow.Controls.Add(this._btnClose);
      this._buttonsRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._buttonsRow.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
      this._buttonsRow.Location = new System.Drawing.Point(15, 365);
      this._buttonsRow.Name = "_buttonsRow";
      this._buttonsRow.Padding = new System.Windows.Forms.Padding(0, 8, 0, 0);
      this._buttonsRow.Size = new System.Drawing.Size(490, 40);
      this._buttonsRow.TabIndex = 3;
      // 
      // _btnClose
      // 
      this._btnClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
      this._btnClose.Location = new System.Drawing.Point(392, 11);
      this._btnClose.Name = "_btnClose";
      this._btnClose.Size = new System.Drawing.Size(95, 28);
      this._btnClose.TabIndex = 0;
      this._btnClose.Text = "Закрыть";
      this._btnClose.UseVisualStyleBackColor = true;
      this._btnClose.Click += new System.EventHandler(this.OnCloseClick);
      // 
      // VelumAssemblyRegistryReportsForm
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.CancelButton = this._btnClose;
      this.ClientSize = new System.Drawing.Size(520, 420);
      this.Controls.Add(this._layout);
      this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
      this.MaximizeBox = false;
      this.MinimizeBox = false;
      this.MinimumSize = new System.Drawing.Size(420, 320);
      this.Name = "VelumAssemblyRegistryReportsForm";
      this.ShowInTaskbar = false;
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
      this.Text = "Отчёты реестра изделия";
      this._layout.ResumeLayout(false);
      this._layout.PerformLayout();
      this._buttonsRow.ResumeLayout(false);
      this.ResumeLayout(false);

    }

    #endregion

    private System.Windows.Forms.TableLayoutPanel _layout;
    private System.Windows.Forms.Label _lblFilter;
    private System.Windows.Forms.TextBox _txtFilter;
    private System.Windows.Forms.ListBox _list;
    private System.Windows.Forms.FlowLayoutPanel _buttonsRow;
    private System.Windows.Forms.Button _btnClose;
  }
}
