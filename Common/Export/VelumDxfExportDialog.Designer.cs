namespace Velum.UI
{
  internal sealed partial class VelumDxfExportDialog
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
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VelumDxfExportDialog));
      this._rootLayout = new System.Windows.Forms.TableLayoutPanel();
      this._lblDocument = new System.Windows.Forms.Label();
      this._lblMode = new System.Windows.Forms.Label();
      this._viewGroup = new System.Windows.Forms.GroupBox();
      this._radioLeft = new System.Windows.Forms.RadioButton();
      this._radioBottom = new System.Windows.Forms.RadioButton();
      this._radioBack = new System.Windows.Forms.RadioButton();
      this._radioRight = new System.Windows.Forms.RadioButton();
      this._radioTop = new System.Windows.Forms.RadioButton();
      this._radioFront = new System.Windows.Forms.RadioButton();
      this._patternRow = new System.Windows.Forms.TableLayoutPanel();
      this._lblFileName = new System.Windows.Forms.Label();
      this._patternBox = new System.Windows.Forms.TextBox();
      this._btnSuffixes = new System.Windows.Forms.Button();
      this._resolvedValuesRow = new System.Windows.Forms.TableLayoutPanel();
      this._lblResolvedFileName = new System.Windows.Forms.Label();
      this._resolvedValuesLabel = new System.Windows.Forms.Label();
      this._lblFolder = new System.Windows.Forms.Label();
      this._folderRow = new System.Windows.Forms.TableLayoutPanel();
      this._folderBox = new System.Windows.Forms.TextBox();
      this._btnBrowse = new System.Windows.Forms.Button();
      this._lblProgress = new System.Windows.Forms.Label();
      this._progressBar = new System.Windows.Forms.ProgressBar();
      this._actionsRow = new System.Windows.Forms.TableLayoutPanel();
      this._btnForbid = new System.Windows.Forms.Button();
      this._btnExport = new System.Windows.Forms.Button();
      this._btnCancel = new System.Windows.Forms.Button();
      this._rootLayout.SuspendLayout();
      this._viewGroup.SuspendLayout();
      this._patternRow.SuspendLayout();
      this._resolvedValuesRow.SuspendLayout();
      this._folderRow.SuspendLayout();
      this._actionsRow.SuspendLayout();
      this.SuspendLayout();
      // 
      // _rootLayout
      // 
      this._rootLayout.ColumnCount = 1;
      this._rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._rootLayout.Controls.Add(this._lblDocument, 0, 0);
      this._rootLayout.Controls.Add(this._lblMode, 0, 1);
      this._rootLayout.Controls.Add(this._viewGroup, 0, 2);
      this._rootLayout.Controls.Add(this._patternRow, 0, 3);
      this._rootLayout.Controls.Add(this._resolvedValuesRow, 0, 4);
      this._rootLayout.Controls.Add(this._lblFolder, 0, 5);
      this._rootLayout.Controls.Add(this._folderRow, 0, 6);
      this._rootLayout.Controls.Add(this._lblProgress, 0, 7);
      this._rootLayout.Controls.Add(this._progressBar, 0, 8);
      this._rootLayout.Controls.Add(this._actionsRow, 0, 9);
      this._rootLayout.Dock = System.Windows.Forms.DockStyle.Fill;
      this._rootLayout.Location = new System.Drawing.Point(0, 0);
      this._rootLayout.Name = "_rootLayout";
      this._rootLayout.Padding = new System.Windows.Forms.Padding(10);
      this._rootLayout.RowCount = 10;
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 17F));
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 22F));
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 84F));
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 31F));
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 33F));
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 16F));
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 31F));
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 16F));
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 27F));
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32F));
      this._rootLayout.Size = new System.Drawing.Size(504, 329);
      this._rootLayout.TabIndex = 0;
      // 
      // _lblDocument
      // 
      this._lblDocument.AutoEllipsis = true;
      this._lblDocument.Dock = System.Windows.Forms.DockStyle.Fill;
      this._lblDocument.Location = new System.Drawing.Point(13, 10);
      this._lblDocument.Margin = new System.Windows.Forms.Padding(3, 0, 3, 4);
      this._lblDocument.Name = "_lblDocument";
      this._lblDocument.Size = new System.Drawing.Size(478, 13);
      this._lblDocument.TabIndex = 0;
      this._lblDocument.Text = "Деталь:";
      // 
      // _lblMode
      // 
      this._lblMode.AutoEllipsis = true;
      this._lblMode.Dock = System.Windows.Forms.DockStyle.Fill;
      this._lblMode.Location = new System.Drawing.Point(13, 27);
      this._lblMode.Margin = new System.Windows.Forms.Padding(3, 0, 3, 6);
      this._lblMode.Name = "_lblMode";
      this._lblMode.Size = new System.Drawing.Size(478, 16);
      this._lblMode.TabIndex = 1;
      this._lblMode.Text = "Режим:";
      // 
      // _viewGroup
      // 
      this._viewGroup.Controls.Add(this._radioLeft);
      this._viewGroup.Controls.Add(this._radioBottom);
      this._viewGroup.Controls.Add(this._radioBack);
      this._viewGroup.Controls.Add(this._radioRight);
      this._viewGroup.Controls.Add(this._radioTop);
      this._viewGroup.Controls.Add(this._radioFront);
      this._viewGroup.Dock = System.Windows.Forms.DockStyle.Fill;
      this._viewGroup.Location = new System.Drawing.Point(13, 52);
      this._viewGroup.Margin = new System.Windows.Forms.Padding(3, 3, 3, 6);
      this._viewGroup.Name = "_viewGroup";
      this._viewGroup.Padding = new System.Windows.Forms.Padding(8, 4, 8, 4);
      this._viewGroup.Size = new System.Drawing.Size(478, 75);
      this._viewGroup.TabIndex = 2;
      this._viewGroup.TabStop = false;
      this._viewGroup.Text = "Вид проекции";
      // 
      // _radioLeft
      // 
      this._radioLeft.AutoSize = true;
      this._radioLeft.Location = new System.Drawing.Point(180, 43);
      this._radioLeft.Name = "_radioLeft";
      this._radioLeft.Size = new System.Drawing.Size(43, 17);
      this._radioLeft.TabIndex = 5;
      this._radioLeft.Text = "Left";
      this._radioLeft.UseVisualStyleBackColor = true;
      // 
      // _radioBottom
      // 
      this._radioBottom.AutoSize = true;
      this._radioBottom.Location = new System.Drawing.Point(100, 43);
      this._radioBottom.Name = "_radioBottom";
      this._radioBottom.Size = new System.Drawing.Size(58, 17);
      this._radioBottom.TabIndex = 4;
      this._radioBottom.Text = "Bottom";
      this._radioBottom.UseVisualStyleBackColor = true;
      // 
      // _radioBack
      // 
      this._radioBack.AutoSize = true;
      this._radioBack.Location = new System.Drawing.Point(12, 43);
      this._radioBack.Name = "_radioBack";
      this._radioBack.Size = new System.Drawing.Size(50, 17);
      this._radioBack.TabIndex = 3;
      this._radioBack.Text = "Back";
      this._radioBack.UseVisualStyleBackColor = true;
      // 
      // _radioRight
      // 
      this._radioRight.AutoSize = true;
      this._radioRight.Location = new System.Drawing.Point(180, 20);
      this._radioRight.Name = "_radioRight";
      this._radioRight.Size = new System.Drawing.Size(50, 17);
      this._radioRight.TabIndex = 2;
      this._radioRight.Text = "Right";
      this._radioRight.UseVisualStyleBackColor = true;
      // 
      // _radioTop
      // 
      this._radioTop.AutoSize = true;
      this._radioTop.Location = new System.Drawing.Point(100, 20);
      this._radioTop.Name = "_radioTop";
      this._radioTop.Size = new System.Drawing.Size(44, 17);
      this._radioTop.TabIndex = 1;
      this._radioTop.Text = "Top";
      this._radioTop.UseVisualStyleBackColor = true;
      // 
      // _radioFront
      // 
      this._radioFront.AutoSize = true;
      this._radioFront.Checked = true;
      this._radioFront.Location = new System.Drawing.Point(12, 20);
      this._radioFront.Name = "_radioFront";
      this._radioFront.Size = new System.Drawing.Size(49, 17);
      this._radioFront.TabIndex = 0;
      this._radioFront.TabStop = true;
      this._radioFront.Text = "Front";
      this._radioFront.UseVisualStyleBackColor = true;
      // 
      // _patternRow
      // 
      this._patternRow.ColumnCount = 3;
      this._patternRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 100F));
      this._patternRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._patternRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 110F));
      this._patternRow.Controls.Add(this._lblFileName, 0, 0);
      this._patternRow.Controls.Add(this._patternBox, 1, 0);
      this._patternRow.Controls.Add(this._btnSuffixes, 2, 0);
      this._patternRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._patternRow.Location = new System.Drawing.Point(13, 136);
      this._patternRow.Margin = new System.Windows.Forms.Padding(3, 3, 3, 0);
      this._patternRow.Name = "_patternRow";
      this._patternRow.RowCount = 1;
      this._patternRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._patternRow.Size = new System.Drawing.Size(478, 28);
      this._patternRow.TabIndex = 3;
      // 
      // _lblFileName
      // 
      this._lblFileName.AutoSize = true;
      this._lblFileName.Dock = System.Windows.Forms.DockStyle.Fill;
      this._lblFileName.Location = new System.Drawing.Point(3, 0);
      this._lblFileName.Margin = new System.Windows.Forms.Padding(3, 0, 8, 0);
      this._lblFileName.Name = "_lblFileName";
      this._lblFileName.Size = new System.Drawing.Size(89, 28);
      this._lblFileName.TabIndex = 0;
      this._lblFileName.Text = "Имя файла:";
      this._lblFileName.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // _patternBox
      // 
      this._patternBox.Dock = System.Windows.Forms.DockStyle.Fill;
      this._patternBox.Location = new System.Drawing.Point(103, 3);
      this._patternBox.Name = "_patternBox";
      this._patternBox.Size = new System.Drawing.Size(262, 20);
      this._patternBox.TabIndex = 1;
      // 
      // _btnSuffixes
      // 
      this._btnSuffixes.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnSuffixes.Location = new System.Drawing.Point(371, 3);
      this._btnSuffixes.Name = "_btnSuffixes";
      this._btnSuffixes.Size = new System.Drawing.Size(104, 22);
      this._btnSuffixes.TabIndex = 2;
      this._btnSuffixes.Text = "Суффиксы…";
      this._btnSuffixes.UseVisualStyleBackColor = true;
      this._btnSuffixes.Click += new System.EventHandler(this.OnPickSuffix);
      // 
      // _resolvedValuesRow
      // 
      this._resolvedValuesRow.ColumnCount = 2;
      this._resolvedValuesRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 100F));
      this._resolvedValuesRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._resolvedValuesRow.Controls.Add(this._lblResolvedFileName, 0, 0);
      this._resolvedValuesRow.Controls.Add(this._resolvedValuesLabel, 1, 0);
      this._resolvedValuesRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._resolvedValuesRow.Location = new System.Drawing.Point(13, 167);
      this._resolvedValuesRow.Margin = new System.Windows.Forms.Padding(3, 3, 3, 8);
      this._resolvedValuesRow.Name = "_resolvedValuesRow";
      this._resolvedValuesRow.RowCount = 1;
      this._resolvedValuesRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._resolvedValuesRow.Size = new System.Drawing.Size(478, 22);
      this._resolvedValuesRow.TabIndex = 4;
      // 
      // _lblResolvedFileName
      // 
      this._lblResolvedFileName.AutoSize = true;
      this._lblResolvedFileName.Dock = System.Windows.Forms.DockStyle.Fill;
      this._lblResolvedFileName.Location = new System.Drawing.Point(3, 0);
      this._lblResolvedFileName.Margin = new System.Windows.Forms.Padding(3, 0, 8, 0);
      this._lblResolvedFileName.Name = "_lblResolvedFileName";
      this._lblResolvedFileName.Size = new System.Drawing.Size(89, 22);
      this._lblResolvedFileName.TabIndex = 0;
      this._lblResolvedFileName.Text = "Итоговое имя:";
      this._lblResolvedFileName.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // _resolvedValuesLabel
      // 
      this._resolvedValuesLabel.AutoEllipsis = true;
      this._resolvedValuesLabel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
      this._resolvedValuesLabel.Dock = System.Windows.Forms.DockStyle.Fill;
      this._resolvedValuesLabel.ForeColor = System.Drawing.SystemColors.GrayText;
      this._resolvedValuesLabel.Location = new System.Drawing.Point(103, 0);
      this._resolvedValuesLabel.MinimumSize = new System.Drawing.Size(2, 22);
      this._resolvedValuesLabel.Name = "_resolvedValuesLabel";
      this._resolvedValuesLabel.Padding = new System.Windows.Forms.Padding(4, 3, 4, 3);
      this._resolvedValuesLabel.Size = new System.Drawing.Size(372, 22);
      this._resolvedValuesLabel.TabIndex = 1;
      this._resolvedValuesLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // _lblFolder
      // 
      this._lblFolder.AutoSize = true;
      this._lblFolder.Dock = System.Windows.Forms.DockStyle.Fill;
      this._lblFolder.Location = new System.Drawing.Point(13, 200);
      this._lblFolder.Margin = new System.Windows.Forms.Padding(3, 3, 3, 0);
      this._lblFolder.Name = "_lblFolder";
      this._lblFolder.Size = new System.Drawing.Size(478, 13);
      this._lblFolder.TabIndex = 6;
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
      this._folderRow.Location = new System.Drawing.Point(13, 216);
      this._folderRow.Margin = new System.Windows.Forms.Padding(3, 3, 3, 0);
      this._folderRow.Name = "_folderRow";
      this._folderRow.RowCount = 1;
      this._folderRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._folderRow.Size = new System.Drawing.Size(478, 28);
      this._folderRow.TabIndex = 7;
      // 
      // _folderBox
      // 
      this._folderBox.Dock = System.Windows.Forms.DockStyle.Fill;
      this._folderBox.Location = new System.Drawing.Point(3, 3);
      this._folderBox.Name = "_folderBox";
      this._folderBox.Size = new System.Drawing.Size(362, 20);
      this._folderBox.TabIndex = 0;
      // 
      // _btnBrowse
      // 
      this._btnBrowse.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnBrowse.Location = new System.Drawing.Point(371, 3);
      this._btnBrowse.Name = "_btnBrowse";
      this._btnBrowse.Size = new System.Drawing.Size(104, 22);
      this._btnBrowse.TabIndex = 1;
      this._btnBrowse.Text = "Обзор…";
      this._btnBrowse.UseVisualStyleBackColor = true;
      this._btnBrowse.Click += new System.EventHandler(this.OnBrowseFolder);
      // 
      // _lblProgress
      // 
      this._lblProgress.AutoSize = true;
      this._lblProgress.Dock = System.Windows.Forms.DockStyle.Fill;
      this._lblProgress.Location = new System.Drawing.Point(13, 244);
      this._lblProgress.Name = "_lblProgress";
      this._lblProgress.Size = new System.Drawing.Size(478, 16);
      this._lblProgress.TabIndex = 9;
      this._lblProgress.Text = "Конфигурация:";
      this._lblProgress.Visible = false;
      // 
      // _progressBar
      // 
      this._progressBar.Dock = System.Windows.Forms.DockStyle.Fill;
      this._progressBar.Location = new System.Drawing.Point(13, 263);
      this._progressBar.Margin = new System.Windows.Forms.Padding(3, 3, 3, 6);
      this._progressBar.Name = "_progressBar";
      this._progressBar.Size = new System.Drawing.Size(478, 18);
      this._progressBar.TabIndex = 10;
      this._progressBar.Visible = false;
      // 
      // _actionsRow
      // 
      this._actionsRow.ColumnCount = 5;
      this._actionsRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._actionsRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 160F));
      this._actionsRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 90F));
      this._actionsRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 90F));
      this._actionsRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 90F));
      this._actionsRow.Controls.Add(this._btnForbid, 2, 0);
      this._actionsRow.Controls.Add(this._btnExport, 3, 0);
      this._actionsRow.Controls.Add(this._btnCancel, 4, 0);
      this._actionsRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._actionsRow.Location = new System.Drawing.Point(13, 287);
      this._actionsRow.Margin = new System.Windows.Forms.Padding(3, 0, 3, 0);
      this._actionsRow.Name = "_actionsRow";
      this._actionsRow.RowCount = 1;
      this._actionsRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32F));
      this._actionsRow.Size = new System.Drawing.Size(478, 32);
      this._actionsRow.TabIndex = 9;
      // 
      // _btnForbid
      // 
      this._btnForbid.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnForbid.Enabled = false;
      this._btnForbid.Location = new System.Drawing.Point(211, 3);
      this._btnForbid.Name = "_btnForbid";
      this._btnForbid.Size = new System.Drawing.Size(84, 26);
      this._btnForbid.TabIndex = 2;
      this._btnForbid.Text = "Запрет";
      this._btnForbid.UseVisualStyleBackColor = true;
      // 
      // _btnExport
      // 
      this._btnExport.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnExport.Location = new System.Drawing.Point(301, 3);
      this._btnExport.Name = "_btnExport";
      this._btnExport.Size = new System.Drawing.Size(84, 26);
      this._btnExport.TabIndex = 0;
      this._btnExport.Text = "Экспорт";
      this._btnExport.UseVisualStyleBackColor = true;
      this._btnExport.Click += new System.EventHandler(this.OnExport);
      // 
      // _btnCancel
      // 
      this._btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
      this._btnCancel.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnCancel.Location = new System.Drawing.Point(391, 3);
      this._btnCancel.Name = "_btnCancel";
      this._btnCancel.Size = new System.Drawing.Size(84, 26);
      this._btnCancel.TabIndex = 1;
      this._btnCancel.Text = "Закрыть";
      this._btnCancel.UseVisualStyleBackColor = true;
      // 
      // VelumDxfExportDialog
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.CancelButton = this._btnCancel;
      this.ClientSize = new System.Drawing.Size(504, 329);
      this.Controls.Add(this._rootLayout);
      this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
      this.MaximizeBox = false;
      this.MinimizeBox = false;
      this.MinimumSize = new System.Drawing.Size(504, 329);
      this.Name = "VelumDxfExportDialog";
      this.ShowInTaskbar = false;
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
      this.Text = "Экспорт DXF";
      this._rootLayout.ResumeLayout(false);
      this._rootLayout.PerformLayout();
      this._viewGroup.ResumeLayout(false);
      this._viewGroup.PerformLayout();
      this._patternRow.ResumeLayout(false);
      this._patternRow.PerformLayout();
      this._resolvedValuesRow.ResumeLayout(false);
      this._resolvedValuesRow.PerformLayout();
      this._folderRow.ResumeLayout(false);
      this._folderRow.PerformLayout();
      this._actionsRow.ResumeLayout(false);
      this.ResumeLayout(false);

    }

    #endregion

    private System.Windows.Forms.TableLayoutPanel _rootLayout;
    private System.Windows.Forms.Label _lblDocument;
    private System.Windows.Forms.Label _lblMode;
    private System.Windows.Forms.GroupBox _viewGroup;
    private System.Windows.Forms.RadioButton _radioFront;
    private System.Windows.Forms.RadioButton _radioTop;
    private System.Windows.Forms.RadioButton _radioRight;
    private System.Windows.Forms.RadioButton _radioBack;
    private System.Windows.Forms.RadioButton _radioBottom;
    private System.Windows.Forms.RadioButton _radioLeft;
    private System.Windows.Forms.TableLayoutPanel _patternRow;
    private System.Windows.Forms.Label _lblFileName;
    private System.Windows.Forms.TextBox _patternBox;
    private System.Windows.Forms.Button _btnSuffixes;
    private System.Windows.Forms.TableLayoutPanel _resolvedValuesRow;
    private System.Windows.Forms.Label _lblResolvedFileName;
    private System.Windows.Forms.Label _resolvedValuesLabel;
    private System.Windows.Forms.Label _lblFolder;
    private System.Windows.Forms.TableLayoutPanel _folderRow;
    private System.Windows.Forms.TextBox _folderBox;
    private System.Windows.Forms.Button _btnBrowse;
    private System.Windows.Forms.Label _lblProgress;
    private System.Windows.Forms.ProgressBar _progressBar;
    private System.Windows.Forms.TableLayoutPanel _actionsRow;
    private System.Windows.Forms.Button _btnForbid;
    private System.Windows.Forms.Button _btnExport;
    private System.Windows.Forms.Button _btnCancel;
  }
}
