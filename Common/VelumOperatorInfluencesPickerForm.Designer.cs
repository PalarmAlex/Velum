namespace Velum.UI
{
  internal sealed partial class VelumOperatorInfluencesPickerForm
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
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VelumOperatorInfluencesPickerForm));
      this._layout = new System.Windows.Forms.TableLayoutPanel();
      this._clb = new System.Windows.Forms.CheckedListBox();
      this._btnRow = new System.Windows.Forms.FlowLayoutPanel();
      this._btnCancel = new System.Windows.Forms.Button();
      this._btnApply = new System.Windows.Forms.Button();
      this._layout.SuspendLayout();
      this._btnRow.SuspendLayout();
      this.SuspendLayout();
      // 
      // _layout
      // 
      this._layout.ColumnCount = 1;
      this._layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._layout.Controls.Add(this._clb, 0, 0);
      this._layout.Controls.Add(this._btnRow, 0, 1);
      this._layout.Dock = System.Windows.Forms.DockStyle.Fill;
      this._layout.Location = new System.Drawing.Point(0, 0);
      this._layout.Name = "_layout";
      this._layout.Padding = new System.Windows.Forms.Padding(10);
      this._layout.RowCount = 2;
      this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 44F));
      this._layout.Size = new System.Drawing.Size(420, 360);
      this._layout.TabIndex = 0;
      // 
      // _clb
      // 
      this._clb.CheckOnClick = true;
      this._clb.Dock = System.Windows.Forms.DockStyle.Fill;
      this._clb.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
      this._clb.Font = System.Drawing.SystemFonts.MessageBoxFont;
      this._clb.FormattingEnabled = true;
      this._clb.IntegralHeight = false;
      this._clb.ItemHeight = 19;
      this._clb.Location = new System.Drawing.Point(13, 13);
      this._clb.Name = "_clb";
      this._clb.Size = new System.Drawing.Size(394, 290);
      this._clb.TabIndex = 0;
      this._clb.DrawItem += new System.Windows.Forms.DrawItemEventHandler(this.OnDrawItem);
      this._clb.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.OnItemCheck);
      this._clb.MouseLeave += new System.EventHandler(this.OnListMouseLeave);
      this._clb.MouseMove += new System.Windows.Forms.MouseEventHandler(this.OnListMouseMove);
      // 
      // _btnRow
      // 
      this._btnRow.Controls.Add(this._btnCancel);
      this._btnRow.Controls.Add(this._btnApply);
      this._btnRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnRow.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
      this._btnRow.Location = new System.Drawing.Point(10, 306);
      this._btnRow.Margin = new System.Windows.Forms.Padding(0);
      this._btnRow.Name = "_btnRow";
      this._btnRow.Padding = new System.Windows.Forms.Padding(0, 8, 0, 0);
      this._btnRow.Size = new System.Drawing.Size(400, 44);
      this._btnRow.TabIndex = 1;
      this._btnRow.WrapContents = false;
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
      // _btnApply
      // 
      this._btnApply.AutoSize = true;
      this._btnApply.Location = new System.Drawing.Point(241, 11);
      this._btnApply.Name = "_btnApply";
      this._btnApply.Size = new System.Drawing.Size(75, 23);
      this._btnApply.TabIndex = 0;
      this._btnApply.Text = "Применить";
      this._btnApply.UseVisualStyleBackColor = true;
      this._btnApply.Click += new System.EventHandler(this.OnApplyClick);
      // 
      // VelumOperatorInfluencesPickerForm
      // 
      this.AcceptButton = this._btnApply;
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.CancelButton = this._btnCancel;
      this.ClientSize = new System.Drawing.Size(420, 360);
      this.Controls.Add(this._layout);
      this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
      this.MaximizeBox = true;
      this.MinimizeBox = false;
      this.MinimumSize = new System.Drawing.Size(360, 280);
      this.Name = "VelumOperatorInfluencesPickerForm";
      this.ShowInTaskbar = false;
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
      this.Text = "Воздействия на параметры агента";
      this.Shown += new System.EventHandler(this.Form_Shown);
      this._layout.ResumeLayout(false);
      this._btnRow.ResumeLayout(false);
      this._btnRow.PerformLayout();
      this.ResumeLayout(false);

    }

    #endregion

    private System.Windows.Forms.TableLayoutPanel _layout;
    private System.Windows.Forms.CheckedListBox _clb;
    private System.Windows.Forms.FlowLayoutPanel _btnRow;
    private System.Windows.Forms.Button _btnCancel;
    private System.Windows.Forms.Button _btnApply;
  }
}
