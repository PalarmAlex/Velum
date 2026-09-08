namespace Velum.UI
{
  internal sealed partial class VelumPdfExportDialog
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
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VelumPdfExportDialog));
      this._rootLayout = new System.Windows.Forms.TableLayoutPanel();
      this._lblDocument = new System.Windows.Forms.Label();
      this._lblPreviewPath = new System.Windows.Forms.Label();
      this._lblCurrentArtifact = new System.Windows.Forms.Label();
      this._lblVersionStatus = new System.Windows.Forms.Label();
      this._lblSaveHint = new System.Windows.Forms.Label();
      this._lblFolder = new System.Windows.Forms.Label();
      this._folderRow = new System.Windows.Forms.TableLayoutPanel();
      this._folderBox = new System.Windows.Forms.TextBox();
      this._btnBrowse = new System.Windows.Forms.Button();
      this._actionsRow = new System.Windows.Forms.TableLayoutPanel();
      this._btnForbid = new System.Windows.Forms.Button();
      this._btnExport = new System.Windows.Forms.Button();
      this._btnCancel = new System.Windows.Forms.Button();
      this._rootLayout.SuspendLayout();
      this._folderRow.SuspendLayout();
      this._actionsRow.SuspendLayout();
      this.SuspendLayout();
      // 
      // _rootLayout
      // 
      this._rootLayout.ColumnCount = 1;
      this._rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._rootLayout.Controls.Add(this._lblDocument, 0, 0);
      this._rootLayout.Controls.Add(this._lblPreviewPath, 0, 1);
      this._rootLayout.Controls.Add(this._lblCurrentArtifact, 0, 2);
      this._rootLayout.Controls.Add(this._lblVersionStatus, 0, 3);
      this._rootLayout.Controls.Add(this._lblSaveHint, 0, 4);
      this._rootLayout.Controls.Add(this._lblFolder, 0, 5);
      this._rootLayout.Controls.Add(this._folderRow, 0, 6);
      this._rootLayout.Controls.Add(this._actionsRow, 0, 7);
      this._rootLayout.Dock = System.Windows.Forms.DockStyle.Fill;
      this._rootLayout.Location = new System.Drawing.Point(0, 0);
      this._rootLayout.Name = "_rootLayout";
      this._rootLayout.Padding = new System.Windows.Forms.Padding(10);
      this._rootLayout.RowCount = 8;
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._rootLayout.Size = new System.Drawing.Size(584, 203);
      this._rootLayout.TabIndex = 0;
      // 
      // _lblDocument
      // 
      this._lblDocument.AutoSize = true;
      this._lblDocument.Dock = System.Windows.Forms.DockStyle.Top;
      this._lblDocument.Location = new System.Drawing.Point(13, 10);
      this._lblDocument.Margin = new System.Windows.Forms.Padding(3, 0, 3, 6);
      this._lblDocument.Name = "_lblDocument";
      this._lblDocument.Size = new System.Drawing.Size(558, 13);
      this._lblDocument.TabIndex = 0;
      this._lblDocument.Text = "Чертёж:";
      // 
      // _lblPreviewPath
      // 
      this._lblPreviewPath.AutoSize = true;
      this._lblPreviewPath.Dock = System.Windows.Forms.DockStyle.Top;
      this._lblPreviewPath.Location = new System.Drawing.Point(13, 29);
      this._lblPreviewPath.Margin = new System.Windows.Forms.Padding(3, 0, 3, 4);
      this._lblPreviewPath.Name = "_lblPreviewPath";
      this._lblPreviewPath.Size = new System.Drawing.Size(558, 13);
      this._lblPreviewPath.TabIndex = 1;
      this._lblPreviewPath.Text = "Итоговый файл:";
      // 
      // _lblCurrentArtifact
      // 
      this._lblCurrentArtifact.AutoSize = true;
      this._lblCurrentArtifact.Dock = System.Windows.Forms.DockStyle.Top;
      this._lblCurrentArtifact.Location = new System.Drawing.Point(13, 46);
      this._lblCurrentArtifact.Margin = new System.Windows.Forms.Padding(3, 0, 3, 4);
      this._lblCurrentArtifact.Name = "_lblCurrentArtifact";
      this._lblCurrentArtifact.Size = new System.Drawing.Size(558, 13);
      this._lblCurrentArtifact.TabIndex = 2;
      this._lblCurrentArtifact.Text = "Текущий артефакт:";
      // 
      // _lblVersionStatus
      // 
      this._lblVersionStatus.AutoSize = true;
      this._lblVersionStatus.Dock = System.Windows.Forms.DockStyle.Top;
      this._lblVersionStatus.Location = new System.Drawing.Point(13, 63);
      this._lblVersionStatus.Margin = new System.Windows.Forms.Padding(3, 0, 3, 8);
      this._lblVersionStatus.Name = "_lblVersionStatus";
      this._lblVersionStatus.Size = new System.Drawing.Size(558, 13);
      this._lblVersionStatus.TabIndex = 3;
      this._lblVersionStatus.Text = "Статус версии:";
      // 
      // _lblSaveHint
      // 
      this._lblSaveHint.AutoSize = true;
      this._lblSaveHint.Dock = System.Windows.Forms.DockStyle.Top;
      this._lblSaveHint.ForeColor = System.Drawing.Color.DarkRed;
      this._lblSaveHint.Location = new System.Drawing.Point(13, 84);
      this._lblSaveHint.Margin = new System.Windows.Forms.Padding(3, 0, 3, 6);
      this._lblSaveHint.Name = "_lblSaveHint";
      this._lblSaveHint.Size = new System.Drawing.Size(558, 13);
      this._lblSaveHint.TabIndex = 4;
      // 
      // _lblFolder
      // 
      this._lblFolder.AutoSize = true;
      this._lblFolder.Dock = System.Windows.Forms.DockStyle.Top;
      this._lblFolder.Location = new System.Drawing.Point(13, 103);
      this._lblFolder.Name = "_lblFolder";
      this._lblFolder.Size = new System.Drawing.Size(558, 13);
      this._lblFolder.TabIndex = 5;
      this._lblFolder.Text = "Каталог выгрузки:";
      // 
      // _folderRow
      // 
      this._folderRow.ColumnCount = 2;
      this._folderRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._folderRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 110F));
      this._folderRow.Controls.Add(this._folderBox, 0, 0);
      this._folderRow.Controls.Add(this._btnBrowse, 1, 0);
      this._folderRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._folderRow.Location = new System.Drawing.Point(13, 119);
      this._folderRow.Margin = new System.Windows.Forms.Padding(3, 3, 3, 8);
      this._folderRow.Name = "_folderRow";
      this._folderRow.RowCount = 1;
      this._folderRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._folderRow.Size = new System.Drawing.Size(558, 28);
      this._folderRow.TabIndex = 6;
      // 
      // _folderBox
      // 
      this._folderBox.Dock = System.Windows.Forms.DockStyle.Fill;
      this._folderBox.Location = new System.Drawing.Point(3, 3);
      this._folderBox.Name = "_folderBox";
      this._folderBox.Size = new System.Drawing.Size(442, 20);
      this._folderBox.TabIndex = 0;
      // 
      // _btnBrowse
      // 
      this._btnBrowse.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnBrowse.Location = new System.Drawing.Point(451, 3);
      this._btnBrowse.Name = "_btnBrowse";
      this._btnBrowse.Size = new System.Drawing.Size(104, 22);
      this._btnBrowse.TabIndex = 1;
      this._btnBrowse.Text = "Обзор…";
      this._btnBrowse.UseVisualStyleBackColor = true;
      this._btnBrowse.Click += new System.EventHandler(this.OnBrowseFolder);
      // 
      // _actionsRow
      // 
      this._actionsRow.AutoSize = true;
      this._actionsRow.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
      this._actionsRow.ColumnCount = 4;
      this._actionsRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._actionsRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 110F));
      this._actionsRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 110F));
      this._actionsRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 110F));
      this._actionsRow.Controls.Add(this._btnForbid, 1, 0);
      this._actionsRow.Controls.Add(this._btnExport, 2, 0);
      this._actionsRow.Controls.Add(this._btnCancel, 3, 0);
      this._actionsRow.Dock = System.Windows.Forms.DockStyle.Top;
      this._actionsRow.Location = new System.Drawing.Point(13, 161);
      this._actionsRow.Margin = new System.Windows.Forms.Padding(3, 6, 3, 0);
      this._actionsRow.Name = "_actionsRow";
      this._actionsRow.RowCount = 1;
      this._actionsRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
      this._actionsRow.Size = new System.Drawing.Size(558, 28);
      this._actionsRow.TabIndex = 7;
      // 
      // _btnForbid
      // 
      this._btnForbid.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnForbid.Enabled = false;
      this._btnForbid.Location = new System.Drawing.Point(231, 0);
      this._btnForbid.Margin = new System.Windows.Forms.Padding(3, 0, 0, 0);
      this._btnForbid.Name = "_btnForbid";
      this._btnForbid.Size = new System.Drawing.Size(107, 28);
      this._btnForbid.TabIndex = 2;
      this._btnForbid.Text = "Запрет";
      this._btnForbid.UseVisualStyleBackColor = true;
      // 
      // _btnExport
      // 
      this._btnExport.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnExport.Location = new System.Drawing.Point(341, 0);
      this._btnExport.Margin = new System.Windows.Forms.Padding(3, 0, 0, 0);
      this._btnExport.Name = "_btnExport";
      this._btnExport.Size = new System.Drawing.Size(107, 28);
      this._btnExport.TabIndex = 0;
      this._btnExport.Text = "Экспорт";
      this._btnExport.UseVisualStyleBackColor = true;
      this._btnExport.Click += new System.EventHandler(this.OnExport);
      // 
      // _btnCancel
      // 
      this._btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
      this._btnCancel.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnCancel.Location = new System.Drawing.Point(451, 0);
      this._btnCancel.Margin = new System.Windows.Forms.Padding(3, 0, 0, 0);
      this._btnCancel.Name = "_btnCancel";
      this._btnCancel.Size = new System.Drawing.Size(107, 28);
      this._btnCancel.TabIndex = 1;
      this._btnCancel.Text = "Закрыть";
      this._btnCancel.UseVisualStyleBackColor = true;
      // 
      // VelumPdfExportDialog
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.CancelButton = this._btnCancel;
      this.ClientSize = new System.Drawing.Size(584, 203);
      this.Controls.Add(this._rootLayout);
      this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
      this.MaximizeBox = false;
      this.MinimizeBox = false;
      this.MinimumSize = new System.Drawing.Size(600, 240);
      this.Name = "VelumPdfExportDialog";
      this.ShowInTaskbar = false;
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
      this.Text = "Экспорт PDF";
      this.Shown += new System.EventHandler(this.OnFormShown);
      this._rootLayout.ResumeLayout(false);
      this._rootLayout.PerformLayout();
      this._folderRow.ResumeLayout(false);
      this._folderRow.PerformLayout();
      this._actionsRow.ResumeLayout(false);
      this.ResumeLayout(false);

    }

    #endregion

    private System.Windows.Forms.TableLayoutPanel _rootLayout;
    private System.Windows.Forms.Label _lblDocument;
    private System.Windows.Forms.Label _lblPreviewPath;
    private System.Windows.Forms.Label _lblCurrentArtifact;
    private System.Windows.Forms.Label _lblVersionStatus;
    private System.Windows.Forms.Label _lblSaveHint;
    private System.Windows.Forms.Label _lblFolder;
    private System.Windows.Forms.TableLayoutPanel _folderRow;
    private System.Windows.Forms.TextBox _folderBox;
    private System.Windows.Forms.Button _btnBrowse;
    private System.Windows.Forms.TableLayoutPanel _actionsRow;
    private System.Windows.Forms.Button _btnForbid;
    private System.Windows.Forms.Button _btnExport;
    private System.Windows.Forms.Button _btnCancel;
  }
}
