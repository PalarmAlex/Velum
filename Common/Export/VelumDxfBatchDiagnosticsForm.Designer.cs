namespace Velum.UI
{
  internal sealed partial class VelumDxfBatchDiagnosticsForm
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
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VelumDxfBatchDiagnosticsForm));
      this._rootLayout = new System.Windows.Forms.TableLayoutPanel();
      this._dxfFolderCaptionRow = new System.Windows.Forms.TableLayoutPanel();
      this._lblDxfFolder = new System.Windows.Forms.Label();
      this._dxfFolderRow = new System.Windows.Forms.TableLayoutPanel();
      this._dxfFolderBox = new System.Windows.Forms.TextBox();
      this._btnBrowseDxf = new System.Windows.Forms.Button();
      this._exportOptionsRow = new System.Windows.Forms.FlowLayoutPanel();
      this._chkThicknessSubfolders = new System.Windows.Forms.CheckBox();
      this._chkAddAssemblyQuantity = new System.Windows.Forms.CheckBox();
      this._lblProductQuantity = new System.Windows.Forms.Label();
      this._nudProductQuantity = new System.Windows.Forms.NumericUpDown();
      this._patternRow = new System.Windows.Forms.TableLayoutPanel();
      this._patternBox = new System.Windows.Forms.TextBox();
      this._btnSuffixes = new System.Windows.Forms.Button();
      this._resolvedSuffixesRow = new System.Windows.Forms.TableLayoutPanel();
      this._lblResolvedFileName = new System.Windows.Forms.Label();
      this._resolvedSuffixesLabel = new System.Windows.Forms.Label();
      this._statusLabel = new System.Windows.Forms.Label();
      this._filterRow = new System.Windows.Forms.TableLayoutPanel();
      this._lblStatusFilter = new System.Windows.Forms.Label();
      this._cmbStatusFilter = new System.Windows.Forms.ComboBox();
      this._lblRecordCount = new System.Windows.Forms.Label();
      this._listView = new System.Windows.Forms.ListView();
      this._colPart = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
      this._colConfig = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
      this._colDxf = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
      this._colStatus = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
      this._lblProgress = new System.Windows.Forms.Label();
      this._progressBar = new System.Windows.Forms.ProgressBar();
      this._actionsRow = new System.Windows.Forms.TableLayoutPanel();
      this._btnExport = new System.Windows.Forms.Button();
      this._btnDeleteOrphans = new System.Windows.Forms.Button();
      this._btnStop = new System.Windows.Forms.Button();
      this._btnClose = new System.Windows.Forms.Button();
      this._rootLayout.SuspendLayout();
      this._dxfFolderCaptionRow.SuspendLayout();
      this._dxfFolderRow.SuspendLayout();
      this._exportOptionsRow.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)(this._nudProductQuantity)).BeginInit();
      this._patternRow.SuspendLayout();
      this._resolvedSuffixesRow.SuspendLayout();
      this._filterRow.SuspendLayout();
      this._actionsRow.SuspendLayout();
      this.SuspendLayout();
      // 
      // _rootLayout
      // 
      this._rootLayout.ColumnCount = 1;
      this._rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._rootLayout.Controls.Add(this._dxfFolderCaptionRow, 0, 0);
      this._rootLayout.Controls.Add(this._dxfFolderRow, 0, 1);
      this._rootLayout.Controls.Add(this._exportOptionsRow, 0, 2);
      this._rootLayout.Controls.Add(this._patternRow, 0, 3);
      this._rootLayout.Controls.Add(this._resolvedSuffixesRow, 0, 4);
      this._rootLayout.Controls.Add(this._statusLabel, 0, 5);
      this._rootLayout.Controls.Add(this._filterRow, 0, 6);
      this._rootLayout.Controls.Add(this._listView, 0, 7);
      this._rootLayout.Controls.Add(this._lblProgress, 0, 8);
      this._rootLayout.Controls.Add(this._progressBar, 0, 9);
      this._rootLayout.Controls.Add(this._actionsRow, 0, 10);
      this._rootLayout.Dock = System.Windows.Forms.DockStyle.Fill;
      this._rootLayout.Location = new System.Drawing.Point(0, 0);
      this._rootLayout.Name = "_rootLayout";
      this._rootLayout.Padding = new System.Windows.Forms.Padding(10);
      this._rootLayout.RowCount = 11;
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 36F));
      this._rootLayout.Size = new System.Drawing.Size(744, 561);
      this._rootLayout.TabIndex = 0;
      // 
      // _dxfFolderCaptionRow
      // 
      this._dxfFolderCaptionRow.ColumnCount = 1;
      this._dxfFolderCaptionRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._dxfFolderCaptionRow.Controls.Add(this._lblDxfFolder, 0, 0);
      this._dxfFolderCaptionRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._dxfFolderCaptionRow.Location = new System.Drawing.Point(13, 10);
      this._dxfFolderCaptionRow.Margin = new System.Windows.Forms.Padding(3, 0, 3, 0);
      this._dxfFolderCaptionRow.Name = "_dxfFolderCaptionRow";
      this._dxfFolderCaptionRow.RowCount = 1;
      this._dxfFolderCaptionRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._dxfFolderCaptionRow.Size = new System.Drawing.Size(718, 22);
      this._dxfFolderCaptionRow.TabIndex = 0;
      // 
      // _lblDxfFolder
      // 
      this._lblDxfFolder.AutoSize = true;
      this._lblDxfFolder.Dock = System.Windows.Forms.DockStyle.Left;
      this._lblDxfFolder.Location = new System.Drawing.Point(0, 0);
      this._lblDxfFolder.Margin = new System.Windows.Forms.Padding(0);
      this._lblDxfFolder.Name = "_lblDxfFolder";
      this._lblDxfFolder.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
      this._lblDxfFolder.Size = new System.Drawing.Size(126, 22);
      this._lblDxfFolder.TabIndex = 0;
      this._lblDxfFolder.Text = "Каталог выгрузки DXF:";
      // 
      // _dxfFolderRow
      // 
      this._dxfFolderRow.ColumnCount = 2;
      this._dxfFolderRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._dxfFolderRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 110F));
      this._dxfFolderRow.Controls.Add(this._dxfFolderBox, 0, 0);
      this._dxfFolderRow.Controls.Add(this._btnBrowseDxf, 1, 0);
      this._dxfFolderRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._dxfFolderRow.Location = new System.Drawing.Point(13, 35);
      this._dxfFolderRow.Name = "_dxfFolderRow";
      this._dxfFolderRow.RowCount = 1;
      this._dxfFolderRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._dxfFolderRow.Size = new System.Drawing.Size(718, 28);
      this._dxfFolderRow.TabIndex = 1;
      // 
      // _dxfFolderBox
      // 
      this._dxfFolderBox.Dock = System.Windows.Forms.DockStyle.Fill;
      this._dxfFolderBox.Location = new System.Drawing.Point(3, 3);
      this._dxfFolderBox.Name = "_dxfFolderBox";
      this._dxfFolderBox.Size = new System.Drawing.Size(602, 20);
      this._dxfFolderBox.TabIndex = 0;
      this._dxfFolderBox.TextChanged += new System.EventHandler(this.OnDxfFolderTextChanged);
      // 
      // _btnBrowseDxf
      // 
      this._btnBrowseDxf.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnBrowseDxf.Location = new System.Drawing.Point(611, 3);
      this._btnBrowseDxf.Name = "_btnBrowseDxf";
      this._btnBrowseDxf.Size = new System.Drawing.Size(104, 22);
      this._btnBrowseDxf.TabIndex = 1;
      this._btnBrowseDxf.Text = "Обзор…";
      this._btnBrowseDxf.UseVisualStyleBackColor = true;
      this._btnBrowseDxf.Click += new System.EventHandler(this.OnBrowseDxfFolder);
      // 
      // _exportOptionsRow
      // 
      this._exportOptionsRow.AutoSize = true;
      this._exportOptionsRow.Controls.Add(this._chkThicknessSubfolders);
      this._exportOptionsRow.Controls.Add(this._chkAddAssemblyQuantity);
      this._exportOptionsRow.Controls.Add(this._lblProductQuantity);
      this._exportOptionsRow.Controls.Add(this._nudProductQuantity);
      this._exportOptionsRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._exportOptionsRow.Location = new System.Drawing.Point(13, 66);
      this._exportOptionsRow.Margin = new System.Windows.Forms.Padding(3, 0, 3, 0);
      this._exportOptionsRow.Name = "_exportOptionsRow";
      this._exportOptionsRow.Size = new System.Drawing.Size(718, 26);
      this._exportOptionsRow.TabIndex = 2;
      this._exportOptionsRow.WrapContents = false;
      // 
      // _chkThicknessSubfolders
      // 
      this._chkThicknessSubfolders.AutoSize = true;
      this._chkThicknessSubfolders.Checked = true;
      this._chkThicknessSubfolders.CheckState = System.Windows.Forms.CheckState.Checked;
      this._chkThicknessSubfolders.Enabled = false;
      this._chkThicknessSubfolders.Location = new System.Drawing.Point(3, 3);
      this._chkThicknessSubfolders.Margin = new System.Windows.Forms.Padding(3, 3, 16, 3);
      this._chkThicknessSubfolders.Name = "_chkThicknessSubfolders";
      this._chkThicknessSubfolders.Size = new System.Drawing.Size(260, 17);
      this._chkThicknessSubfolders.TabIndex = 0;
      this._chkThicknessSubfolders.Text = "Создать суб. каталоги по толщине материала";
      this._chkThicknessSubfolders.UseVisualStyleBackColor = true;
      // 
      // _chkAddAssemblyQuantity
      // 
      this._chkAddAssemblyQuantity.AutoSize = true;
      this._chkAddAssemblyQuantity.Checked = true;
      this._chkAddAssemblyQuantity.CheckState = System.Windows.Forms.CheckState.Checked;
      this._chkAddAssemblyQuantity.Enabled = false;
      this._chkAddAssemblyQuantity.Location = new System.Drawing.Point(282, 3);
      this._chkAddAssemblyQuantity.Margin = new System.Windows.Forms.Padding(3, 3, 16, 3);
      this._chkAddAssemblyQuantity.Name = "_chkAddAssemblyQuantity";
      this._chkAddAssemblyQuantity.Size = new System.Drawing.Size(166, 17);
      this._chkAddAssemblyQuantity.TabIndex = 1;
      this._chkAddAssemblyQuantity.Text = "Добавить кол-во по сборке";
      this._chkAddAssemblyQuantity.UseVisualStyleBackColor = true;
      this._chkAddAssemblyQuantity.Visible = false;
      // 
      // _lblProductQuantity
      // 
      this._lblProductQuantity.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._lblProductQuantity.AutoSize = true;
      this._lblProductQuantity.Location = new System.Drawing.Point(467, 9);
      this._lblProductQuantity.Margin = new System.Windows.Forms.Padding(3, 5, 4, 0);
      this._lblProductQuantity.Name = "_lblProductQuantity";
      this._lblProductQuantity.Size = new System.Drawing.Size(89, 13);
      this._lblProductQuantity.TabIndex = 2;
      this._lblProductQuantity.Text = "Кол-во изделия:";
      this._lblProductQuantity.Visible = false;
      // 
      // _nudProductQuantity
      // 
      this._nudProductQuantity.Location = new System.Drawing.Point(563, 3);
      this._nudProductQuantity.Maximum = new decimal(new int[] {
            999999,
            0,
            0,
            0});
      this._nudProductQuantity.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
      this._nudProductQuantity.Name = "_nudProductQuantity";
      this._nudProductQuantity.Size = new System.Drawing.Size(60, 20);
      this._nudProductQuantity.TabIndex = 3;
      this._nudProductQuantity.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
      this._nudProductQuantity.Visible = false;
      // 
      // _patternRow
      // 
      this._patternRow.ColumnCount = 2;
      this._patternRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._patternRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 110F));
      this._patternRow.Controls.Add(this._patternBox, 0, 0);
      this._patternRow.Controls.Add(this._btnSuffixes, 1, 0);
      this._patternRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._patternRow.Location = new System.Drawing.Point(13, 95);
      this._patternRow.Margin = new System.Windows.Forms.Padding(3, 3, 3, 0);
      this._patternRow.Name = "_patternRow";
      this._patternRow.RowCount = 1;
      this._patternRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._patternRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
      this._patternRow.Size = new System.Drawing.Size(718, 28);
      this._patternRow.TabIndex = 5;
      // 
      // _patternBox
      // 
      this._patternBox.Dock = System.Windows.Forms.DockStyle.Fill;
      this._patternBox.Location = new System.Drawing.Point(3, 3);
      this._patternBox.Name = "_patternBox";
      this._patternBox.Size = new System.Drawing.Size(602, 20);
      this._patternBox.TabIndex = 0;
      // 
      // _btnSuffixes
      // 
      this._btnSuffixes.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnSuffixes.Location = new System.Drawing.Point(611, 3);
      this._btnSuffixes.Name = "_btnSuffixes";
      this._btnSuffixes.Size = new System.Drawing.Size(104, 22);
      this._btnSuffixes.TabIndex = 1;
      this._btnSuffixes.Text = "Суффиксы…";
      this._btnSuffixes.UseVisualStyleBackColor = true;
      // 
      // _resolvedSuffixesRow
      // 
      this._resolvedSuffixesRow.ColumnCount = 2;
      this._resolvedSuffixesRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._resolvedSuffixesRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._resolvedSuffixesRow.Controls.Add(this._lblResolvedFileName, 0, 0);
      this._resolvedSuffixesRow.Controls.Add(this._resolvedSuffixesLabel, 1, 0);
      this._resolvedSuffixesRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._resolvedSuffixesRow.Location = new System.Drawing.Point(13, 126);
      this._resolvedSuffixesRow.Margin = new System.Windows.Forms.Padding(3, 3, 3, 8);
      this._resolvedSuffixesRow.Name = "_resolvedSuffixesRow";
      this._resolvedSuffixesRow.RowCount = 1;
      this._resolvedSuffixesRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._resolvedSuffixesRow.Size = new System.Drawing.Size(718, 22);
      this._resolvedSuffixesRow.TabIndex = 6;
      // 
      // _lblResolvedFileName
      // 
      this._lblResolvedFileName.AutoSize = true;
      this._lblResolvedFileName.Dock = System.Windows.Forms.DockStyle.Fill;
      this._lblResolvedFileName.Location = new System.Drawing.Point(3, 0);
      this._lblResolvedFileName.Margin = new System.Windows.Forms.Padding(3, 0, 8, 0);
      this._lblResolvedFileName.Name = "_lblResolvedFileName";
      this._lblResolvedFileName.Size = new System.Drawing.Size(116, 22);
      this._lblResolvedFileName.TabIndex = 0;
      this._lblResolvedFileName.Text = "Итоговое имя файла:";
      this._lblResolvedFileName.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // _resolvedSuffixesLabel
      // 
      this._resolvedSuffixesLabel.AutoEllipsis = true;
      this._resolvedSuffixesLabel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
      this._resolvedSuffixesLabel.Dock = System.Windows.Forms.DockStyle.Fill;
      this._resolvedSuffixesLabel.ForeColor = System.Drawing.SystemColors.GrayText;
      this._resolvedSuffixesLabel.Location = new System.Drawing.Point(130, 0);
      this._resolvedSuffixesLabel.MinimumSize = new System.Drawing.Size(2, 22);
      this._resolvedSuffixesLabel.Name = "_resolvedSuffixesLabel";
      this._resolvedSuffixesLabel.Padding = new System.Windows.Forms.Padding(4, 3, 4, 3);
      this._resolvedSuffixesLabel.Size = new System.Drawing.Size(585, 22);
      this._resolvedSuffixesLabel.TabIndex = 1;
      this._resolvedSuffixesLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // _statusLabel
      // 
      this._statusLabel.AutoSize = true;
      this._statusLabel.Dock = System.Windows.Forms.DockStyle.Fill;
      this._statusLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold);
      this._statusLabel.Location = new System.Drawing.Point(13, 159);
      this._statusLabel.Margin = new System.Windows.Forms.Padding(3, 3, 3, 6);
      this._statusLabel.Name = "_statusLabel";
      this._statusLabel.Size = new System.Drawing.Size(718, 15);
      this._statusLabel.TabIndex = 4;
      this._statusLabel.Text = "Нет проблемных деталей";
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
      this._filterRow.Location = new System.Drawing.Point(13, 180);
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
            this._colPart,
            this._colConfig,
            this._colDxf,
            this._colStatus});
      this._listView.Dock = System.Windows.Forms.DockStyle.Fill;
      this._listView.FullRowSelect = true;
      this._listView.HideSelection = false;
      this._listView.Location = new System.Drawing.Point(13, 214);
      this._listView.Name = "_listView";
      this._listView.Size = new System.Drawing.Size(718, 261);
      this._listView.TabIndex = 6;
      this._listView.UseCompatibleStateImageBehavior = false;
      this._listView.View = System.Windows.Forms.View.Details;
      this._listView.ItemSelectionChanged += new System.Windows.Forms.ListViewItemSelectionChangedEventHandler(this.OnListViewItemSelectionChanged);
      // 
      // _colPart
      // 
      this._colPart.Text = "Деталь";
      this._colPart.Width = 200;
      // 
      // _colConfig
      // 
      this._colConfig.Text = "Конфигурация";
      this._colConfig.Width = 120;
      // 
      // _colDxf
      // 
      this._colDxf.Text = "Файл dxf";
      this._colDxf.Width = 200;
      // 
      // _colStatus
      // 
      this._colStatus.Text = "Статус";
      this._colStatus.Width = 180;
      // 
      // _lblProgress
      // 
      this._lblProgress.AutoSize = true;
      this._lblProgress.Dock = System.Windows.Forms.DockStyle.Fill;
      this._lblProgress.Location = new System.Drawing.Point(13, 478);
      this._lblProgress.Name = "_lblProgress";
      this._lblProgress.Size = new System.Drawing.Size(718, 13);
      this._lblProgress.TabIndex = 6;
      this._lblProgress.Text = "Экспорт:";
      this._lblProgress.Visible = false;
      // 
      // _progressBar
      // 
      this._progressBar.Dock = System.Windows.Forms.DockStyle.Fill;
      this._progressBar.Location = new System.Drawing.Point(13, 494);
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
      this._actionsRow.Location = new System.Drawing.Point(13, 518);
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
      this._btnDeleteOrphans.Text = "Удалить мусорные DXF";
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
      // VelumDxfBatchDiagnosticsForm
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.CancelButton = this._btnClose;
      this.ClientSize = new System.Drawing.Size(744, 561);
      this.Controls.Add(this._rootLayout);
      this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
      this.MinimumSize = new System.Drawing.Size(760, 600);
      this.Name = "VelumDxfBatchDiagnosticsForm";
      this.ShowInTaskbar = false;
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
      this.Text = "Пакетная диагностика DXF";
      this._rootLayout.ResumeLayout(false);
      this._rootLayout.PerformLayout();
      this._dxfFolderCaptionRow.ResumeLayout(false);
      this._dxfFolderCaptionRow.PerformLayout();
      this._dxfFolderRow.ResumeLayout(false);
      this._dxfFolderRow.PerformLayout();
      this._exportOptionsRow.ResumeLayout(false);
      this._exportOptionsRow.PerformLayout();
      ((System.ComponentModel.ISupportInitialize)(this._nudProductQuantity)).EndInit();
      this._patternRow.ResumeLayout(false);
      this._patternRow.PerformLayout();
      this._resolvedSuffixesRow.ResumeLayout(false);
      this._resolvedSuffixesRow.PerformLayout();
      this._filterRow.ResumeLayout(false);
      this._filterRow.PerformLayout();
      this._actionsRow.ResumeLayout(false);
      this.ResumeLayout(false);

    }

    #endregion

    private System.Windows.Forms.TableLayoutPanel _rootLayout;
    private System.Windows.Forms.TableLayoutPanel _dxfFolderCaptionRow;
    private System.Windows.Forms.Label _lblDxfFolder;
    private System.Windows.Forms.TableLayoutPanel _dxfFolderRow;
    private System.Windows.Forms.TextBox _dxfFolderBox;
    private System.Windows.Forms.Button _btnBrowseDxf;
    private System.Windows.Forms.FlowLayoutPanel _exportOptionsRow;
    private System.Windows.Forms.CheckBox _chkThicknessSubfolders;
    private System.Windows.Forms.CheckBox _chkAddAssemblyQuantity;
    private System.Windows.Forms.Label _lblProductQuantity;
    private System.Windows.Forms.NumericUpDown _nudProductQuantity;
    private System.Windows.Forms.TableLayoutPanel _patternRow;
    private System.Windows.Forms.TextBox _patternBox;
    private System.Windows.Forms.Button _btnSuffixes;
    private System.Windows.Forms.TableLayoutPanel _resolvedSuffixesRow;
    private System.Windows.Forms.Label _lblResolvedFileName;
    private System.Windows.Forms.Label _resolvedSuffixesLabel;
    private System.Windows.Forms.Label _statusLabel;
    private System.Windows.Forms.TableLayoutPanel _filterRow;
    private System.Windows.Forms.Label _lblStatusFilter;
    private System.Windows.Forms.ComboBox _cmbStatusFilter;
    private System.Windows.Forms.Label _lblRecordCount;
    private System.Windows.Forms.ListView _listView;
    private System.Windows.Forms.ColumnHeader _colPart;
    private System.Windows.Forms.ColumnHeader _colConfig;
    private System.Windows.Forms.ColumnHeader _colDxf;
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
