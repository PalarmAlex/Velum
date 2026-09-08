namespace Velum.UI
{
  internal sealed partial class VelumProductRegistryFolderDescriptionForm
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
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VelumProductRegistryFolderDescriptionForm));
      this._layout = new System.Windows.Forms.TableLayoutPanel();
      this._lblFolder = new System.Windows.Forms.Label();
      this._descriptionBox = new System.Windows.Forms.TextBox();
      this._buttonsRow = new System.Windows.Forms.FlowLayoutPanel();
      this._btnClose = new System.Windows.Forms.Button();
      this._btnApply = new System.Windows.Forms.Button();
      this._layout.SuspendLayout();
      this._buttonsRow.SuspendLayout();
      this.SuspendLayout();
      // 
      // _layout
      // 
      this._layout.ColumnCount = 1;
      this._layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._layout.Controls.Add(this._lblFolder, 0, 0);
      this._layout.Controls.Add(this._descriptionBox, 0, 1);
      this._layout.Controls.Add(this._buttonsRow, 0, 2);
      this._layout.Dock = System.Windows.Forms.DockStyle.Fill;
      this._layout.Location = new System.Drawing.Point(0, 0);
      this._layout.Name = "_layout";
      this._layout.Padding = new System.Windows.Forms.Padding(12);
      this._layout.RowCount = 3;
      this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._layout.Size = new System.Drawing.Size(480, 280);
      this._layout.TabIndex = 0;
      // 
      // _lblFolder
      // 
      this._lblFolder.AutoSize = true;
      this._lblFolder.Dock = System.Windows.Forms.DockStyle.Fill;
      this._lblFolder.Location = new System.Drawing.Point(15, 12);
      this._lblFolder.Margin = new System.Windows.Forms.Padding(3, 0, 3, 6);
      this._lblFolder.Name = "_lblFolder";
      this._lblFolder.Size = new System.Drawing.Size(450, 13);
      this._lblFolder.TabIndex = 0;
      this._lblFolder.Text = "Каталог:";
      // 
      // _descriptionBox
      // 
      this._descriptionBox.AcceptsReturn = true;
      this._descriptionBox.Dock = System.Windows.Forms.DockStyle.Fill;
      this._descriptionBox.Location = new System.Drawing.Point(15, 34);
      this._descriptionBox.Multiline = true;
      this._descriptionBox.Name = "_descriptionBox";
      this._descriptionBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
      this._descriptionBox.Size = new System.Drawing.Size(450, 185);
      this._descriptionBox.TabIndex = 1;
      // 
      // _buttonsRow
      // 
      this._buttonsRow.Controls.Add(this._btnClose);
      this._buttonsRow.Controls.Add(this._btnApply);
      this._buttonsRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._buttonsRow.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
      this._buttonsRow.Location = new System.Drawing.Point(15, 225);
      this._buttonsRow.Name = "_buttonsRow";
      this._buttonsRow.Padding = new System.Windows.Forms.Padding(0, 8, 0, 0);
      this._buttonsRow.Size = new System.Drawing.Size(450, 40);
      this._buttonsRow.TabIndex = 2;
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
      // VelumProductRegistryFolderDescriptionForm
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.CancelButton = this._btnClose;
      this.ClientSize = new System.Drawing.Size(480, 280);
      this.Controls.Add(this._layout);
      this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
      this.MinimizeBox = false;
      this.Name = "VelumProductRegistryFolderDescriptionForm";
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
      this.Text = "Описание каталога";
      this._layout.ResumeLayout(false);
      this._layout.PerformLayout();
      this._buttonsRow.ResumeLayout(false);
      this.ResumeLayout(false);

    }

    #endregion

    private System.Windows.Forms.TableLayoutPanel _layout;
    private System.Windows.Forms.Label _lblFolder;
    private System.Windows.Forms.TextBox _descriptionBox;
    private System.Windows.Forms.FlowLayoutPanel _buttonsRow;
    private System.Windows.Forms.Button _btnClose;
    private System.Windows.Forms.Button _btnApply;
  }
}
