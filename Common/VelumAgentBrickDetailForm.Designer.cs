namespace Velum.UI
{
  internal sealed partial class VelumAgentBrickDetailForm
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
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VelumAgentBrickDetailForm));
      this._layout = new System.Windows.Forms.TableLayoutPanel();
      this._txt = new System.Windows.Forms.TextBox();
      this._btnRow = new System.Windows.Forms.FlowLayoutPanel();
      this._btnClose = new System.Windows.Forms.Button();
      this._layout.SuspendLayout();
      this._btnRow.SuspendLayout();
      this.SuspendLayout();
      // 
      // _layout
      // 
      this._layout.ColumnCount = 1;
      this._layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._layout.Controls.Add(this._txt, 0, 0);
      this._layout.Controls.Add(this._btnRow, 0, 1);
      this._layout.Dock = System.Windows.Forms.DockStyle.Fill;
      this._layout.Location = new System.Drawing.Point(0, 0);
      this._layout.Name = "_layout";
      this._layout.Padding = new System.Windows.Forms.Padding(10);
      this._layout.RowCount = 2;
      this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
      this._layout.Size = new System.Drawing.Size(440, 320);
      this._layout.TabIndex = 0;
      // 
      // _txt
      // 
      this._txt.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
      this._txt.Dock = System.Windows.Forms.DockStyle.Fill;
      this._txt.Font = System.Drawing.SystemFonts.MessageBoxFont;
      this._txt.Location = new System.Drawing.Point(13, 13);
      this._txt.Multiline = true;
      this._txt.Name = "_txt";
      this._txt.ReadOnly = true;
      this._txt.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
      this._txt.Size = new System.Drawing.Size(414, 254);
      this._txt.TabIndex = 0;
      this._txt.TabStop = true;
      this._txt.WordWrap = true;
      // 
      // _btnRow
      // 
      this._btnRow.Controls.Add(this._btnClose);
      this._btnRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnRow.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
      this._btnRow.Location = new System.Drawing.Point(10, 270);
      this._btnRow.Margin = new System.Windows.Forms.Padding(0);
      this._btnRow.Name = "_btnRow";
      this._btnRow.Padding = new System.Windows.Forms.Padding(0, 6, 0, 0);
      this._btnRow.Size = new System.Drawing.Size(420, 40);
      this._btnRow.TabIndex = 1;
      this._btnRow.WrapContents = false;
      // 
      // _btnClose
      // 
      this._btnClose.AutoSize = true;
      this._btnClose.DialogResult = System.Windows.Forms.DialogResult.OK;
      this._btnClose.Location = new System.Drawing.Point(342, 9);
      this._btnClose.Name = "_btnClose";
      this._btnClose.Size = new System.Drawing.Size(75, 23);
      this._btnClose.TabIndex = 0;
      this._btnClose.Text = "Закрыть";
      this._btnClose.UseVisualStyleBackColor = true;
      this._btnClose.Click += new System.EventHandler(this.BtnClose_Click);
      // 
      // VelumAgentBrickDetailForm
      // 
      this.AcceptButton = this._btnClose;
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.CancelButton = this._btnClose;
      this.ClientSize = new System.Drawing.Size(440, 320);
      this.Controls.Add(this._layout);
      this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
      this.MaximizeBox = true;
      this.MinimizeBox = false;
      this.MinimumSize = new System.Drawing.Size(280, 180);
      this.Name = "VelumAgentBrickDetailForm";
      this.ShowInTaskbar = false;
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
      this.Text = "Детали метрики";
      this._layout.ResumeLayout(false);
      this._layout.PerformLayout();
      this._btnRow.ResumeLayout(false);
      this._btnRow.PerformLayout();
      this.ResumeLayout(false);

    }

    #endregion

    private System.Windows.Forms.TableLayoutPanel _layout;
    private System.Windows.Forms.TextBox _txt;
    private System.Windows.Forms.FlowLayoutPanel _btnRow;
    private System.Windows.Forms.Button _btnClose;
  }
}
