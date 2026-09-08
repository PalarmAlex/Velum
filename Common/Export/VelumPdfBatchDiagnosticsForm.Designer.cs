namespace Velum.UI
{
  internal sealed partial class VelumPdfBatchDiagnosticsForm
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
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VelumPdfBatchDiagnosticsForm));
      this._rootLayout = new System.Windows.Forms.TableLayoutPanel();
      this._pdfFolderCaptionRow = new System.Windows.Forms.TableLayoutPanel();
      this._lblPdfFolder = new System.Windows.Forms.Label();
      this._pdfFolderRow = new System.Windows.Forms.TableLayoutPanel();
      this._pdfFolderBox = new System.Windows.Forms.TextBox();
      this._btnBrowsePdf = new System.Windows.Forms.Button();
      this._statusLabel = new System.Windows.Forms.Label();
      this._filterRow = new System.Windows.Forms.TableLayoutPanel();
      this._lblStatusFilter = new System.Windows.Forms.Label();
      this._cmbStatusFilter = new System.Windows.Forms.ComboBox();
      this._lblRecordCount = new System.Windows.Forms.Label();
      this._listView = new System.Windows.Forms.ListView();
      this._colDrawing = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
      this._colPdf = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
      this._colStatus = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
      this._lblProgress = new System.Windows.Forms.Label();
      this._progressBar = new System.Windows.Forms.ProgressBar();
      this._actionsRow = new System.Windows.Forms.TableLayoutPanel();
      this._btnExport = new System.Windows.Forms.Button();
      this._btnDeleteOrphans = new System.Windows.Forms.Button();
      this._btnStop = new System.Windows.Forms.Button();
      this._btnClose = new System.Windows.Forms.Button();
      this._rootLayout.SuspendLayout();
      this._pdfFolderCaptionRow.SuspendLayout();
      this._pdfFolderRow.SuspendLayout();
      this._filterRow.SuspendLayout();
      this._actionsRow.SuspendLayout();
      this.SuspendLayout();
      // 
      // _rootLayout
      // 
      this._rootLayout.ColumnCount = 1;
      this._rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._rootLayout.Controls.Add(this._pdfFolderCaptionRow, 0, 0);
      this._rootLayout.Controls.Add(this._pdfFolderRow, 0, 1);
      this._rootLayout.Controls.Add(this._statusLabel, 0, 2);
      this._rootLayout.Controls.Add(this._filterRow, 0, 3);
      this._rootLayout.Controls.Add(this._listView, 0, 4);
      this._rootLayout.Controls.Add(this._lblProgress, 0, 5);
      this._rootLayout.Controls.Add(this._progressBar, 0, 6);
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
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 36F));
      this._rootLayout.Size = new System.Drawing.Size(744, 481);
      this._rootLayout.TabIndex = 0;
      // 
      // _pdfFolderCaptionRow
      // 
      this._pdfFolderCaptionRow.ColumnCount = 1;
      this._pdfFolderCaptionRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._pdfFolderCaptionRow.Controls.Add(this._lblPdfFolder, 0, 0);
      this._pdfFolderCaptionRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._pdfFolderCaptionRow.Location = new System.Drawing.Point(13, 10);
      this._pdfFolderCaptionRow.Margin = new System.Windows.Forms.Padding(3, 0, 3, 0);
      this._pdfFolderCaptionRow.Name = "_pdfFolderCaptionRow";
      this._pdfFolderCaptionRow.RowCount = 1;
      this._pdfFolderCaptionRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._pdfFolderCaptionRow.Size = new System.Drawing.Size(718, 22);
      this._pdfFolderCaptionRow.TabIndex = 0;
      // 
      // _lblPdfFolder
      // 
      this._lblPdfFolder.AutoSize = true;
      this._lblPdfFolder.Dock = System.Windows.Forms.DockStyle.Left;
      this._lblPdfFolder.Location = new System.Drawing.Point(0, 0);
      this._lblPdfFolder.Margin = new System.Windows.Forms.Padding(0);
      this._lblPdfFolder.Name = "_lblPdfFolder";
      this._lblPdfFolder.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
      this._lblPdfFolder.Size = new System.Drawing.Size(126, 22);
      this._lblPdfFolder.TabIndex = 0;
      this._lblPdfFolder.Text = "Каталог выгрузки PDF:";
      // 
      // _pdfFolderRow
      // 
      this._pdfFolderRow.ColumnCount = 2;
      this._pdfFolderRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._pdfFolderRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 110F));
      this._pdfFolderRow.Controls.Add(this._pdfFolderBox, 0, 0);
      this._pdfFolderRow.Controls.Add(this._btnBrowsePdf, 1, 0);
      this._pdfFolderRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._pdfFolderRow.Location = new System.Drawing.Point(13, 35);
      this._pdfFolderRow.Name = "_pdfFolderRow";
      this._pdfFolderRow.RowCount = 1;
      this._pdfFolderRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._pdfFolderRow.Size = new System.Drawing.Size(718, 28);
      this._pdfFolderRow.TabIndex = 1;
      // 
      // _pdfFolderBox
      // 
      this._pdfFolderBox.Dock = System.Windows.Forms.DockStyle.Fill;
      this._pdfFolderBox.Location = new System.Drawing.Point(3, 3);
      this._pdfFolderBox.Name = "_pdfFolderBox";
      this._pdfFolderBox.Size = new System.Drawing.Size(602, 20);
      this._pdfFolderBox.TabIndex = 0;
      // 
      // _btnBrowsePdf
      // 
      this._btnBrowsePdf.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnBrowsePdf.Location = new System.Drawing.Point(611, 3);
      this._btnBrowsePdf.Name = "_btnBrowsePdf";
      this._btnBrowsePdf.Size = new System.Drawing.Size(104, 22);
      this._btnBrowsePdf.TabIndex = 1;
      this._btnBrowsePdf.Text = "Обзор…";
      this._btnBrowsePdf.UseVisualStyleBackColor = true;
      this._btnBrowsePdf.Click += new System.EventHandler(this.OnBrowsePdfFolder);
      // 
      // _statusLabel
      // 
      this._statusLabel.AutoSize = true;
      this._statusLabel.Dock = System.Windows.Forms.DockStyle.Fill;
      this._statusLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold);
      this._statusLabel.Location = new System.Drawing.Point(13, 69);
      this._statusLabel.Margin = new System.Windows.Forms.Padding(3, 3, 3, 6);
      this._statusLabel.Name = "_statusLabel";
      this._statusLabel.Size = new System.Drawing.Size(718, 15);
      this._statusLabel.TabIndex = 4;
      this._statusLabel.Text = "Нет проблемных чертежей";
      // 
      // _filterRow
      // 
      this._filterRow.ColumnCount = 3;
      this._filterRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._filterRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._filterRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._filterRow.Controls.Add(this._lblStatusFilter, 0, 0);
      this._filterRow.Controls.Add(this._cmbStatusFilter, 1, 0);
      this._filterRow.Controls.Add(this._lblRecordCount, 2, 0);
      this._filterRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._filterRow.Location = new System.Drawing.Point(13, 90);
      this._filterRow.Margin = new System.Windows.Forms.Padding(3, 0, 3, 3);
      this._filterRow.Name = "_filterRow";
      this._filterRow.RowCount = 1;
      this._filterRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._filterRow.Size = new System.Drawing.Size(718, 28);
      this._filterRow.TabIndex = 5;
      // 
      // _lblStatusFilter
      // 
      this._lblStatusFilter.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._lblStatusFilter.AutoSize = true;
      this._lblStatusFilter.Location = new System.Drawing.Point(0, 7);
      this._lblStatusFilter.Margin = new System.Windows.Forms.Padding(0, 0, 8, 0);
      this._lblStatusFilter.Name = "_lblStatusFilter";
      this._lblStatusFilter.Size = new System.Drawing.Size(44, 13);
      this._lblStatusFilter.TabIndex = 0;
      this._lblStatusFilter.Text = "Статус:";
      // 
      // _cmbStatusFilter
      // 
      this._cmbStatusFilter.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
      this._cmbStatusFilter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
      this._cmbStatusFilter.FormattingEnabled = true;
      this._cmbStatusFilter.Location = new System.Drawing.Point(55, 3);
      this._cmbStatusFilter.Name = "_cmbStatusFilter";
      this._cmbStatusFilter.Size = new System.Drawing.Size(603, 21);
      this._cmbStatusFilter.TabIndex = 1;
      // 
      // _lblRecordCount
      // 
      this._lblRecordCount.Anchor = System.Windows.Forms.AnchorStyles.Right;
      this._lblRecordCount.AutoSize = true;
      this._lblRecordCount.Location = new System.Drawing.Point(669, 7);
      this._lblRecordCount.Margin = new System.Windows.Forms.Padding(8, 0, 0, 0);
      this._lblRecordCount.Name = "_lblRecordCount";
      this._lblRecordCount.Size = new System.Drawing.Size(49, 13);
      this._lblRecordCount.TabIndex = 2;
      this._lblRecordCount.Text = "Строк: 0";
      this._lblRecordCount.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      // 
      // _listView
      // 
      this._listView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this._colDrawing,
            this._colPdf,
            this._colStatus});
      this._listView.Dock = System.Windows.Forms.DockStyle.Fill;
      this._listView.FullRowSelect = true;
      this._listView.HideSelection = false;
      this._listView.Location = new System.Drawing.Point(13, 124);
      this._listView.Name = "_listView";
      this._listView.Size = new System.Drawing.Size(718, 271);
      this._listView.TabIndex = 6;
      this._listView.UseCompatibleStateImageBehavior = false;
      this._listView.View = System.Windows.Forms.View.Details;
      this._listView.ItemSelectionChanged += new System.Windows.Forms.ListViewItemSelectionChangedEventHandler(this.OnListViewItemSelectionChanged);
      // 
      // _colDrawing
      // 
      this._colDrawing.Text = "Чертёж";
      this._colDrawing.Width = 240;
      // 
      // _colPdf
      // 
      this._colPdf.Text = "PDF-файл";
      this._colPdf.Width = 240;
      // 
      // _colStatus
      // 
      this._colStatus.Text = "Статус";
      this._colStatus.Width = 220;
      // 
      // _lblProgress
      // 
      this._lblProgress.AutoSize = true;
      this._lblProgress.Dock = System.Windows.Forms.DockStyle.Fill;
      this._lblProgress.Location = new System.Drawing.Point(13, 398);
      this._lblProgress.Name = "_lblProgress";
      this._lblProgress.Size = new System.Drawing.Size(718, 13);
      this._lblProgress.TabIndex = 6;
      this._lblProgress.Text = "Экспорт:";
      this._lblProgress.Visible = false;
      // 
      // _progressBar
      // 
      this._progressBar.Dock = System.Windows.Forms.DockStyle.Fill;
      this._progressBar.Location = new System.Drawing.Point(13, 414);
      this._progressBar.Name = "_progressBar";
      this._progressBar.Size = new System.Drawing.Size(718, 18);
      this._progressBar.TabIndex = 7;
      this._progressBar.Visible = false;
      // 
      // _actionsRow
      // 
      this._actionsRow.ColumnCount = 5;
      this._actionsRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._actionsRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._actionsRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._actionsRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._actionsRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._actionsRow.Controls.Add(this._btnExport, 1, 0);
      this._actionsRow.Controls.Add(this._btnDeleteOrphans, 2, 0);
      this._actionsRow.Controls.Add(this._btnStop, 3, 0);
      this._actionsRow.Controls.Add(this._btnClose, 4, 0);
      this._actionsRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._actionsRow.Location = new System.Drawing.Point(13, 438);
      this._actionsRow.Name = "_actionsRow";
      this._actionsRow.RowCount = 1;
      this._actionsRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._actionsRow.Size = new System.Drawing.Size(718, 30);
      this._actionsRow.TabIndex = 8;
      // 
      // _btnExport
      // 
      this._btnExport.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnExport.Location = new System.Drawing.Point(236, 3);
      this._btnExport.Name = "_btnExport";
      this._btnExport.Size = new System.Drawing.Size(169, 24);
      this._btnExport.TabIndex = 1;
      this._btnExport.Text = "Экспортировать выделенные";
      this._btnExport.UseVisualStyleBackColor = true;
      this._btnExport.Click += new System.EventHandler(this.OnExportSelected);
      // 
      // _btnDeleteOrphans
      // 
      this._btnDeleteOrphans.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnDeleteOrphans.Location = new System.Drawing.Point(411, 3);
      this._btnDeleteOrphans.Name = "_btnDeleteOrphans";
      this._btnDeleteOrphans.Size = new System.Drawing.Size(144, 24);
      this._btnDeleteOrphans.TabIndex = 2;
      this._btnDeleteOrphans.Text = "Удалить мусорные PDF";
      this._btnDeleteOrphans.UseVisualStyleBackColor = true;
      this._btnDeleteOrphans.Click += new System.EventHandler(this.OnDeleteOrphans);
      // 
      // _btnStop
      // 
      this._btnStop.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnStop.Enabled = false;
      this._btnStop.Location = new System.Drawing.Point(561, 3);
      this._btnStop.Name = "_btnStop";
      this._btnStop.Size = new System.Drawing.Size(64, 24);
      this._btnStop.TabIndex = 3;
      this._btnStop.Text = "Стоп";
      this._btnStop.UseVisualStyleBackColor = true;
      this._btnStop.Click += new System.EventHandler(this.OnStopOperation);
      // 
      // _btnClose
      // 
      this._btnClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
      this._btnClose.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnClose.Location = new System.Drawing.Point(631, 3);
      this._btnClose.Name = "_btnClose";
      this._btnClose.Size = new System.Drawing.Size(84, 24);
      this._btnClose.TabIndex = 3;
      this._btnClose.Text = "Закрыть";
      this._btnClose.UseVisualStyleBackColor = true;
      // 
      // VelumPdfBatchDiagnosticsForm
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.CancelButton = this._btnClose;
      this.ClientSize = new System.Drawing.Size(744, 481);
      this.Controls.Add(this._rootLayout);
      this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
      this.MinimumSize = new System.Drawing.Size(760, 520);
      this.Name = "VelumPdfBatchDiagnosticsForm";
      this.ShowInTaskbar = false;
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
      this.Text = "Пакетная диагностика PDF";
      this._rootLayout.ResumeLayout(false);
      this._rootLayout.PerformLayout();
      this._pdfFolderCaptionRow.ResumeLayout(false);
      this._pdfFolderCaptionRow.PerformLayout();
      this._pdfFolderRow.ResumeLayout(false);
      this._pdfFolderRow.PerformLayout();
      this._filterRow.ResumeLayout(false);
      this._filterRow.PerformLayout();
      this._actionsRow.ResumeLayout(false);
      this.ResumeLayout(false);

    }

    #endregion

    private System.Windows.Forms.TableLayoutPanel _rootLayout;
    private System.Windows.Forms.TableLayoutPanel _pdfFolderCaptionRow;
    private System.Windows.Forms.Label _lblPdfFolder;
    private System.Windows.Forms.TableLayoutPanel _pdfFolderRow;
    private System.Windows.Forms.TextBox _pdfFolderBox;
    private System.Windows.Forms.Button _btnBrowsePdf;
    private System.Windows.Forms.Label _statusLabel;
    private System.Windows.Forms.TableLayoutPanel _filterRow;
    private System.Windows.Forms.Label _lblStatusFilter;
    private System.Windows.Forms.ComboBox _cmbStatusFilter;
    private System.Windows.Forms.Label _lblRecordCount;
    private System.Windows.Forms.ListView _listView;
    private System.Windows.Forms.ColumnHeader _colDrawing;
    private System.Windows.Forms.ColumnHeader _colPdf;
    private System.Windows.Forms.ColumnHeader _colStatus;
    private System.Windows.Forms.Label _lblProgress;
    private System.Windows.Forms.ProgressBar _progressBar;
    private System.Windows.Forms.TableLayoutPanel _actionsRow;
    private System.Windows.Forms.Button _btnExport;
    private System.Windows.Forms.Button _btnDeleteOrphans;
    private System.Windows.Forms.Button _btnStop;
    private System.Windows.Forms.Button _btnClose;
  }
}
