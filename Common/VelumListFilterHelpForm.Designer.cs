namespace Velum.UI
{
  internal sealed partial class VelumListFilterHelpForm
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
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VelumListFilterHelpForm));
      this._layout = new System.Windows.Forms.TableLayoutPanel();
      this._txtHelp = new System.Windows.Forms.TextBox();
      this._btnClose = new System.Windows.Forms.Button();
      this._layout.SuspendLayout();
      this.SuspendLayout();
      // 
      // _layout
      // 
      this._layout.ColumnCount = 1;
      this._layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._layout.Controls.Add(this._txtHelp, 0, 0);
      this._layout.Controls.Add(this._btnClose, 0, 1);
      this._layout.Dock = System.Windows.Forms.DockStyle.Fill;
      this._layout.Location = new System.Drawing.Point(0, 0);
      this._layout.Name = "_layout";
      this._layout.Padding = new System.Windows.Forms.Padding(12);
      this._layout.RowCount = 2;
      this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._layout.Size = new System.Drawing.Size(520, 420);
      this._layout.TabIndex = 0;
      // 
      // _txtHelp
      // 
      this._txtHelp.Dock = System.Windows.Forms.DockStyle.Fill;
      this._txtHelp.Font = new System.Drawing.Font("Consolas", 9F);
      this._txtHelp.Location = new System.Drawing.Point(15, 15);
      this._txtHelp.Multiline = true;
      this._txtHelp.Name = "_txtHelp";
      this._txtHelp.ReadOnly = true;
      this._txtHelp.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
      this._txtHelp.Size = new System.Drawing.Size(490, 356);
      this._txtHelp.TabIndex = 0;
      // 
      // _btnClose
      // 
      this._btnClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
      this._btnClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
      this._btnClose.Location = new System.Drawing.Point(410, 377);
      this._btnClose.Name = "_btnClose";
      this._btnClose.Size = new System.Drawing.Size(95, 28);
      this._btnClose.TabIndex = 1;
      this._btnClose.Text = "Закрыть";
      this._btnClose.UseVisualStyleBackColor = true;
      this._btnClose.Click += new System.EventHandler(this.OnCloseClick);
      // 
      // VelumListFilterHelpForm
      // 
      this.AcceptButton = this._btnClose;
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.CancelButton = this._btnClose;
      this.ClientSize = new System.Drawing.Size(520, 420);
      this.Controls.Add(this._layout);
      this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
      this.MaximizeBox = false;
      this.MinimizeBox = false;
      this.MinimumSize = new System.Drawing.Size(420, 320);
      this.Name = "VelumListFilterHelpForm";
      this.ShowInTaskbar = false;
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
      this.Text = "Справка по фильтрам";
      this._layout.ResumeLayout(false);
      this._layout.PerformLayout();
      this.ResumeLayout(false);

    }

    #endregion

    private System.Windows.Forms.TableLayoutPanel _layout;
    private System.Windows.Forms.TextBox _txtHelp;
    private System.Windows.Forms.Button _btnClose;
  }
}
