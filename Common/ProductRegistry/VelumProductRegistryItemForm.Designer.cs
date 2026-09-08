namespace Velum.UI
{
  internal sealed partial class VelumProductRegistryItemForm
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
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VelumProductRegistryItemForm));
      this._layout = new System.Windows.Forms.TableLayoutPanel();
      this._lblId = new System.Windows.Forms.Label();
      this._idBox = new System.Windows.Forms.TextBox();
      this._lblDesignation = new System.Windows.Forms.Label();
      this._designationBox = new System.Windows.Forms.TextBox();
      this._lblName = new System.Windows.Forms.Label();
      this._nameBox = new System.Windows.Forms.TextBox();
      this._lblFolder = new System.Windows.Forms.Label();
      this._folderRow = new System.Windows.Forms.TableLayoutPanel();
      this._folderPathBox = new System.Windows.Forms.TextBox();
      this._btnBrowseFolder = new System.Windows.Forms.Button();
      this._lblFile = new System.Windows.Forms.Label();
      this._fileRow = new System.Windows.Forms.TableLayoutPanel();
      this._filePathBox = new System.Windows.Forms.TextBox();
      this._btnBrowseFile = new System.Windows.Forms.Button();
      this._buttonsRow = new System.Windows.Forms.FlowLayoutPanel();
      this._btnClose = new System.Windows.Forms.Button();
      this._btnApply = new System.Windows.Forms.Button();
      this._layout.SuspendLayout();
      this._folderRow.SuspendLayout();
      this._fileRow.SuspendLayout();
      this._buttonsRow.SuspendLayout();
      this.SuspendLayout();
      // 
      // _layout
      // 
      this._layout.ColumnCount = 2;
      this._layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 100F));
      this._layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._layout.Controls.Add(this._lblId, 0, 0);
      this._layout.Controls.Add(this._idBox, 1, 0);
      this._layout.Controls.Add(this._lblDesignation, 0, 1);
      this._layout.Controls.Add(this._designationBox, 1, 1);
      this._layout.Controls.Add(this._lblName, 0, 2);
      this._layout.Controls.Add(this._nameBox, 1, 2);
      this._layout.Controls.Add(this._lblFolder, 0, 3);
      this._layout.Controls.Add(this._folderRow, 1, 3);
      this._layout.Controls.Add(this._lblFile, 0, 4);
      this._layout.Controls.Add(this._fileRow, 1, 4);
      this._layout.Controls.Add(this._buttonsRow, 0, 5);
      this._layout.Dock = System.Windows.Forms.DockStyle.Fill;
      this._layout.Location = new System.Drawing.Point(0, 0);
      this._layout.Name = "_layout";
      this._layout.Padding = new System.Windows.Forms.Padding(12);
      this._layout.RowCount = 6;
      this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._layout.Size = new System.Drawing.Size(520, 210);
      this._layout.TabIndex = 0;
      // 
      // _lblId
      // 
      this._lblId.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._lblId.AutoSize = true;
      this._lblId.Location = new System.Drawing.Point(15, 18);
      this._lblId.Name = "_lblId";
      this._lblId.Size = new System.Drawing.Size(21, 13);
      this._lblId.TabIndex = 0;
      this._lblId.Text = "ID:";
      // 
      // _idBox
      // 
      this._idBox.Dock = System.Windows.Forms.DockStyle.Fill;
      this._idBox.Location = new System.Drawing.Point(115, 15);
      this._idBox.Name = "_idBox";
      this._idBox.ReadOnly = true;
      this._idBox.Size = new System.Drawing.Size(390, 20);
      this._idBox.TabIndex = 1;
      // 
      // _lblDesignation
      // 
      this._lblDesignation.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._lblDesignation.AutoSize = true;
      this._lblDesignation.Location = new System.Drawing.Point(15, 44);
      this._lblDesignation.Name = "_lblDesignation";
      this._lblDesignation.Size = new System.Drawing.Size(77, 13);
      this._lblDesignation.TabIndex = 2;
      this._lblDesignation.Text = "Обозначение:";
      // 
      // _designationBox
      // 
      this._designationBox.Dock = System.Windows.Forms.DockStyle.Fill;
      this._designationBox.Location = new System.Drawing.Point(115, 41);
      this._designationBox.Name = "_designationBox";
      this._designationBox.Size = new System.Drawing.Size(390, 20);
      this._designationBox.TabIndex = 3;
      // 
      // _lblName
      // 
      this._lblName.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._lblName.AutoSize = true;
      this._lblName.Location = new System.Drawing.Point(15, 70);
      this._lblName.Name = "_lblName";
      this._lblName.Size = new System.Drawing.Size(86, 13);
      this._lblName.TabIndex = 4;
      this._lblName.Text = "Наименование:";
      // 
      // _nameBox
      // 
      this._nameBox.Dock = System.Windows.Forms.DockStyle.Fill;
      this._nameBox.Location = new System.Drawing.Point(115, 67);
      this._nameBox.Name = "_nameBox";
      this._nameBox.Size = new System.Drawing.Size(390, 20);
      this._nameBox.TabIndex = 5;
      // 
      // _lblFolder
      // 
      this._lblFolder.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._lblFolder.AutoSize = true;
      this._lblFolder.Location = new System.Drawing.Point(15, 97);
      this._lblFolder.Name = "_lblFolder";
      this._lblFolder.Size = new System.Drawing.Size(51, 13);
      this._lblFolder.TabIndex = 6;
      this._lblFolder.Text = "Каталог:";
      // 
      // _folderRow
      // 
      this._folderRow.ColumnCount = 2;
      this._folderRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._folderRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 90F));
      this._folderRow.Controls.Add(this._folderPathBox, 0, 0);
      this._folderRow.Controls.Add(this._btnBrowseFolder, 1, 0);
      this._folderRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._folderRow.Location = new System.Drawing.Point(112, 90);
      this._folderRow.Margin = new System.Windows.Forms.Padding(0);
      this._folderRow.Name = "_folderRow";
      this._folderRow.RowCount = 1;
      this._folderRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._folderRow.Size = new System.Drawing.Size(396, 28);
      this._folderRow.TabIndex = 7;
      // 
      // _folderPathBox
      // 
      this._folderPathBox.Dock = System.Windows.Forms.DockStyle.Fill;
      this._folderPathBox.Location = new System.Drawing.Point(3, 3);
      this._folderPathBox.Name = "_folderPathBox";
      this._folderPathBox.ReadOnly = true;
      this._folderPathBox.Size = new System.Drawing.Size(300, 20);
      this._folderPathBox.TabIndex = 0;
      // 
      // _btnBrowseFolder
      // 
      this._btnBrowseFolder.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnBrowseFolder.Location = new System.Drawing.Point(309, 3);
      this._btnBrowseFolder.Name = "_btnBrowseFolder";
      this._btnBrowseFolder.Size = new System.Drawing.Size(84, 22);
      this._btnBrowseFolder.TabIndex = 1;
      this._btnBrowseFolder.Text = "Обзор…";
      this._btnBrowseFolder.UseVisualStyleBackColor = true;
      this._btnBrowseFolder.Click += new System.EventHandler(this.OnBrowseFolder);
      // 
      // _lblFile
      // 
      this._lblFile.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._lblFile.AutoSize = true;
      this._lblFile.Location = new System.Drawing.Point(15, 125);
      this._lblFile.Name = "_lblFile";
      this._lblFile.Size = new System.Drawing.Size(39, 13);
      this._lblFile.TabIndex = 8;
      this._lblFile.Text = "Файл:";
      // 
      // _fileRow
      // 
      this._fileRow.ColumnCount = 2;
      this._fileRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._fileRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 90F));
      this._fileRow.Controls.Add(this._filePathBox, 0, 0);
      this._fileRow.Controls.Add(this._btnBrowseFile, 1, 0);
      this._fileRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._fileRow.Location = new System.Drawing.Point(112, 118);
      this._fileRow.Margin = new System.Windows.Forms.Padding(0);
      this._fileRow.Name = "_fileRow";
      this._fileRow.RowCount = 1;
      this._fileRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._fileRow.Size = new System.Drawing.Size(396, 28);
      this._fileRow.TabIndex = 9;
      // 
      // _filePathBox
      // 
      this._filePathBox.Dock = System.Windows.Forms.DockStyle.Fill;
      this._filePathBox.Location = new System.Drawing.Point(3, 3);
      this._filePathBox.Name = "_filePathBox";
      this._filePathBox.Size = new System.Drawing.Size(300, 20);
      this._filePathBox.TabIndex = 0;
      // 
      // _btnBrowseFile
      // 
      this._btnBrowseFile.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnBrowseFile.Location = new System.Drawing.Point(309, 3);
      this._btnBrowseFile.Name = "_btnBrowseFile";
      this._btnBrowseFile.Size = new System.Drawing.Size(84, 22);
      this._btnBrowseFile.TabIndex = 1;
      this._btnBrowseFile.Text = "Обзор…";
      this._btnBrowseFile.UseVisualStyleBackColor = true;
      this._btnBrowseFile.Click += new System.EventHandler(this.OnBrowseFile);
      // 
      // _buttonsRow
      // 
      this._buttonsRow.AutoSize = true;
      this._buttonsRow.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
      this._layout.SetColumnSpan(this._buttonsRow, 2);
      this._buttonsRow.Controls.Add(this._btnClose);
      this._buttonsRow.Controls.Add(this._btnApply);
      this._buttonsRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._buttonsRow.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
      this._buttonsRow.Location = new System.Drawing.Point(15, 152);
      this._buttonsRow.Margin = new System.Windows.Forms.Padding(3, 6, 3, 0);
      this._buttonsRow.Name = "_buttonsRow";
      this._buttonsRow.Size = new System.Drawing.Size(490, 46);
      this._buttonsRow.TabIndex = 10;
      // 
      // _btnClose
      // 
      this._btnClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
      this._btnClose.Location = new System.Drawing.Point(395, 0);
      this._btnClose.Margin = new System.Windows.Forms.Padding(3, 0, 0, 0);
      this._btnClose.Name = "_btnClose";
      this._btnClose.Size = new System.Drawing.Size(95, 28);
      this._btnClose.TabIndex = 1;
      this._btnClose.Text = "Закрыть";
      this._btnClose.UseVisualStyleBackColor = true;
      this._btnClose.Click += new System.EventHandler(this.OnCloseClick);
      // 
      // _btnApply
      // 
      this._btnApply.Location = new System.Drawing.Point(297, 0);
      this._btnApply.Margin = new System.Windows.Forms.Padding(3, 0, 0, 0);
      this._btnApply.Name = "_btnApply";
      this._btnApply.Size = new System.Drawing.Size(95, 28);
      this._btnApply.TabIndex = 0;
      this._btnApply.Text = "Применить";
      this._btnApply.UseVisualStyleBackColor = true;
      this._btnApply.Click += new System.EventHandler(this.OnApply);
      // 
      // VelumProductRegistryItemForm
      // 
      this.AcceptButton = this._btnApply;
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.CancelButton = this._btnClose;
      this.ClientSize = new System.Drawing.Size(520, 210);
      this.Controls.Add(this._layout);
      this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
      this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
      this.MaximizeBox = false;
      this.MinimizeBox = false;
      this.Name = "VelumProductRegistryItemForm";
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
      this.Text = "Узел дерева реестра документов";
      this._layout.ResumeLayout(false);
      this._layout.PerformLayout();
      this._folderRow.ResumeLayout(false);
      this._folderRow.PerformLayout();
      this._fileRow.ResumeLayout(false);
      this._fileRow.PerformLayout();
      this._buttonsRow.ResumeLayout(false);
      this.ResumeLayout(false);

    }

    #endregion

    private System.Windows.Forms.TableLayoutPanel _layout;
    private System.Windows.Forms.Label _lblId;
    private System.Windows.Forms.TextBox _idBox;
    private System.Windows.Forms.Label _lblDesignation;
    private System.Windows.Forms.TextBox _designationBox;
    private System.Windows.Forms.Label _lblName;
    private System.Windows.Forms.TextBox _nameBox;
    private System.Windows.Forms.Label _lblFolder;
    private System.Windows.Forms.TableLayoutPanel _folderRow;
    private System.Windows.Forms.TextBox _folderPathBox;
    private System.Windows.Forms.Button _btnBrowseFolder;
    private System.Windows.Forms.Label _lblFile;
    private System.Windows.Forms.TableLayoutPanel _fileRow;
    private System.Windows.Forms.TextBox _filePathBox;
    private System.Windows.Forms.Button _btnBrowseFile;
    private System.Windows.Forms.FlowLayoutPanel _buttonsRow;
    private System.Windows.Forms.Button _btnClose;
    private System.Windows.Forms.Button _btnApply;
  }
}
