namespace Velum.UI
{
  internal sealed partial class VelumAssemblyRegistryForm
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
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VelumAssemblyRegistryForm));
      this._rootLayout = new System.Windows.Forms.TableLayoutPanel();
      this._splitContainer = new System.Windows.Forms.SplitContainer();
      this._treePanel = new System.Windows.Forms.TableLayoutPanel();
      this._lblTree = new System.Windows.Forms.Label();
      this._treeSearchPanel = new System.Windows.Forms.TableLayoutPanel();
      this._lblTreeSearch = new System.Windows.Forms.Label();
      this._treeSearchBox = new System.Windows.Forms.TextBox();
      this._btnTreeSearchPrev = new System.Windows.Forms.Button();
      this._btnTreeSearchNext = new System.Windows.Forms.Button();
      this._btnTreeSearchClear = new System.Windows.Forms.Button();
      this._btnStop = new System.Windows.Forms.Button();
      this._treeSearchStatusLabel = new System.Windows.Forms.Label();
      this._folderTreeView = new System.Windows.Forms.TreeView();
      this._progressBar = new System.Windows.Forms.ProgressBar();
      this._progressLabel = new System.Windows.Forms.Label();
      this._listPanel = new System.Windows.Forms.TableLayoutPanel();
      this._templateBar = new System.Windows.Forms.TableLayoutPanel();
      this._lblTemplate = new System.Windows.Forms.Label();
      this._cmbTemplate = new System.Windows.Forms.ComboBox();
      this._btnExportTemplates = new System.Windows.Forms.Button();
      this._btnImportTemplates = new System.Windows.Forms.Button();
      this._btnReport = new System.Windows.Forms.Button();
      this._btnReports = new System.Windows.Forms.Button();
      this._btnColumnSettings = new System.Windows.Forms.Button();
      this._filtersHost = new System.Windows.Forms.Panel();
      this._filterButtonsHost = new System.Windows.Forms.TableLayoutPanel();
      this._btnFilterHelp = new System.Windows.Forms.Button();
      this._btnFilterReset = new System.Windows.Forms.Button();
      this._btnFilterApply = new System.Windows.Forms.Button();
      this._btnAssemblyComposition = new System.Windows.Forms.Button();
      this.check_all_doc = new System.Windows.Forms.CheckBox();
      this._listStatusLabel = new System.Windows.Forms.Label();
      this._listView = new System.Windows.Forms.ListView();
      this._totalsHost = new System.Windows.Forms.Panel();
      this._rootLayout.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)(this._splitContainer)).BeginInit();
      this._splitContainer.Panel1.SuspendLayout();
      this._splitContainer.Panel2.SuspendLayout();
      this._splitContainer.SuspendLayout();
      this._treePanel.SuspendLayout();
      this._treeSearchPanel.SuspendLayout();
      this._listPanel.SuspendLayout();
      this._templateBar.SuspendLayout();
      this._filterButtonsHost.SuspendLayout();
      this.SuspendLayout();
      // 
      // _rootLayout
      // 
      this._rootLayout.ColumnCount = 1;
      this._rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._rootLayout.Controls.Add(this._splitContainer, 0, 0);
      this._rootLayout.Dock = System.Windows.Forms.DockStyle.Fill;
      this._rootLayout.Location = new System.Drawing.Point(0, 0);
      this._rootLayout.Name = "_rootLayout";
      this._rootLayout.Padding = new System.Windows.Forms.Padding(10);
      this._rootLayout.RowCount = 1;
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._rootLayout.Size = new System.Drawing.Size(1100, 640);
      this._rootLayout.TabIndex = 0;
      // 
      // _splitContainer
      // 
      this._splitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
      this._splitContainer.Location = new System.Drawing.Point(13, 13);
      this._splitContainer.Name = "_splitContainer";
      // 
      // _splitContainer.Panel1
      // 
      this._splitContainer.Panel1.Controls.Add(this._treePanel);
      // 
      // _splitContainer.Panel2
      // 
      this._splitContainer.Panel2.Controls.Add(this._listPanel);
      this._splitContainer.Size = new System.Drawing.Size(1074, 614);
      this._splitContainer.SplitterDistance = 340;
      this._splitContainer.TabIndex = 0;
      // 
      // _treePanel
      // 
      this._treePanel.ColumnCount = 1;
      this._treePanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._treePanel.Controls.Add(this._lblTree, 0, 0);
      this._treePanel.Controls.Add(this._treeSearchPanel, 0, 1);
      this._treePanel.Controls.Add(this._treeSearchStatusLabel, 0, 2);
      this._treePanel.Controls.Add(this._folderTreeView, 0, 3);
      this._treePanel.Controls.Add(this._progressBar, 0, 4);
      this._treePanel.Controls.Add(this._progressLabel, 0, 5);
      this._treePanel.Dock = System.Windows.Forms.DockStyle.Fill;
      this._treePanel.Location = new System.Drawing.Point(0, 0);
      this._treePanel.Name = "_treePanel";
      this._treePanel.RowCount = 6;
      this._treePanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._treePanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
      this._treePanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._treePanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._treePanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 0F));
      this._treePanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._treePanel.Size = new System.Drawing.Size(340, 614);
      this._treePanel.TabIndex = 0;
      // 
      // _lblTree
      // 
      this._lblTree.AutoSize = true;
      this._lblTree.Dock = System.Windows.Forms.DockStyle.Fill;
      this._lblTree.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold);
      this._lblTree.Location = new System.Drawing.Point(3, 0);
      this._lblTree.Margin = new System.Windows.Forms.Padding(3, 0, 3, 4);
      this._lblTree.Name = "_lblTree";
      this._lblTree.Size = new System.Drawing.Size(334, 13);
      this._lblTree.TabIndex = 0;
      this._lblTree.Text = "Состав изделия";
      // 
      // _treeSearchPanel
      // 
      this._treeSearchPanel.ColumnCount = 6;
      this._treeSearchPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._treeSearchPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._treeSearchPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 32F));
      this._treeSearchPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 32F));
      this._treeSearchPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 32F));
      this._treeSearchPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 70F));
      this._treeSearchPanel.Controls.Add(this._lblTreeSearch, 0, 0);
      this._treeSearchPanel.Controls.Add(this._treeSearchBox, 1, 0);
      this._treeSearchPanel.Controls.Add(this._btnTreeSearchPrev, 2, 0);
      this._treeSearchPanel.Controls.Add(this._btnTreeSearchNext, 3, 0);
      this._treeSearchPanel.Controls.Add(this._btnTreeSearchClear, 4, 0);
      this._treeSearchPanel.Controls.Add(this._btnStop, 5, 0);
      this._treeSearchPanel.Dock = System.Windows.Forms.DockStyle.Fill;
      this._treeSearchPanel.Location = new System.Drawing.Point(3, 20);
      this._treeSearchPanel.Name = "_treeSearchPanel";
      this._treeSearchPanel.RowCount = 1;
      this._treeSearchPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._treeSearchPanel.Size = new System.Drawing.Size(334, 28);
      this._treeSearchPanel.TabIndex = 1;
      // 
      // _lblTreeSearch
      // 
      this._lblTreeSearch.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._lblTreeSearch.AutoSize = true;
      this._lblTreeSearch.Location = new System.Drawing.Point(3, 8);
      this._lblTreeSearch.Name = "_lblTreeSearch";
      this._lblTreeSearch.Size = new System.Drawing.Size(32, 13);
      this._lblTreeSearch.TabIndex = 0;
      this._lblTreeSearch.Text = "Имя:";
      // 
      // _treeSearchBox
      // 
      this._treeSearchBox.Dock = System.Windows.Forms.DockStyle.Fill;
      this._treeSearchBox.Location = new System.Drawing.Point(41, 3);
      this._treeSearchBox.Name = "_treeSearchBox";
      this._treeSearchBox.Size = new System.Drawing.Size(124, 20);
      this._treeSearchBox.TabIndex = 1;
      // 
      // _btnTreeSearchPrev
      // 
      this._btnTreeSearchPrev.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnTreeSearchPrev.Location = new System.Drawing.Point(171, 3);
      this._btnTreeSearchPrev.Name = "_btnTreeSearchPrev";
      this._btnTreeSearchPrev.Size = new System.Drawing.Size(26, 23);
      this._btnTreeSearchPrev.TabIndex = 2;
      this._btnTreeSearchPrev.Text = "▲";
      this._btnTreeSearchPrev.UseVisualStyleBackColor = true;
      // 
      // _btnTreeSearchNext
      // 
      this._btnTreeSearchNext.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnTreeSearchNext.Location = new System.Drawing.Point(203, 3);
      this._btnTreeSearchNext.Name = "_btnTreeSearchNext";
      this._btnTreeSearchNext.Size = new System.Drawing.Size(26, 23);
      this._btnTreeSearchNext.TabIndex = 3;
      this._btnTreeSearchNext.Text = "▼";
      this._btnTreeSearchNext.UseVisualStyleBackColor = true;
      // 
      // _btnTreeSearchClear
      // 
      this._btnTreeSearchClear.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnTreeSearchClear.Location = new System.Drawing.Point(235, 3);
      this._btnTreeSearchClear.Name = "_btnTreeSearchClear";
      this._btnTreeSearchClear.Size = new System.Drawing.Size(26, 23);
      this._btnTreeSearchClear.TabIndex = 4;
      this._btnTreeSearchClear.Text = "×";
      this._btnTreeSearchClear.UseVisualStyleBackColor = true;
      // 
      // _btnStop
      // 
      this._btnStop.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnStop.Enabled = false;
      this._btnStop.Location = new System.Drawing.Point(267, 3);
      this._btnStop.Name = "_btnStop";
      this._btnStop.Size = new System.Drawing.Size(64, 23);
      this._btnStop.TabIndex = 5;
      this._btnStop.Text = "Стоп";
      this._btnStop.UseVisualStyleBackColor = true;
      // 
      // _treeSearchStatusLabel
      // 
      this._treeSearchStatusLabel.AutoSize = true;
      this._treeSearchStatusLabel.Dock = System.Windows.Forms.DockStyle.Fill;
      this._treeSearchStatusLabel.Location = new System.Drawing.Point(3, 51);
      this._treeSearchStatusLabel.Name = "_treeSearchStatusLabel";
      this._treeSearchStatusLabel.Size = new System.Drawing.Size(334, 13);
      this._treeSearchStatusLabel.TabIndex = 2;
      // 
      // _folderTreeView
      // 
      this._folderTreeView.AllowDrop = true;
      this._folderTreeView.Dock = System.Windows.Forms.DockStyle.Fill;
      this._folderTreeView.HideSelection = false;
      this._folderTreeView.LabelEdit = true;
      this._folderTreeView.Location = new System.Drawing.Point(3, 67);
      this._folderTreeView.Name = "_folderTreeView";
      this._folderTreeView.Size = new System.Drawing.Size(334, 531);
      this._folderTreeView.TabIndex = 3;
      // 
      // _progressBar
      // 
      this._progressBar.Dock = System.Windows.Forms.DockStyle.Fill;
      this._progressBar.Location = new System.Drawing.Point(3, 604);
      this._progressBar.Name = "_progressBar";
      this._progressBar.Size = new System.Drawing.Size(334, 1);
      this._progressBar.TabIndex = 4;
      this._progressBar.Visible = false;
      // 
      // _progressLabel
      // 
      this._progressLabel.AutoSize = true;
      this._progressLabel.Dock = System.Windows.Forms.DockStyle.Fill;
      this._progressLabel.Location = new System.Drawing.Point(3, 601);
      this._progressLabel.Name = "_progressLabel";
      this._progressLabel.Size = new System.Drawing.Size(334, 13);
      this._progressLabel.TabIndex = 5;
      // 
      // _listPanel
      // 
      this._listPanel.ColumnCount = 1;
      this._listPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._listPanel.Controls.Add(this._templateBar, 0, 0);
      this._listPanel.Controls.Add(this._filtersHost, 0, 1);
      this._listPanel.Controls.Add(this._filterButtonsHost, 0, 2);
      this._listPanel.Controls.Add(this._listStatusLabel, 0, 3);
      this._listPanel.Controls.Add(this._listView, 0, 4);
      this._listPanel.Controls.Add(this._totalsHost, 0, 5);
      this._listPanel.Dock = System.Windows.Forms.DockStyle.Fill;
      this._listPanel.Location = new System.Drawing.Point(0, 0);
      this._listPanel.Name = "_listPanel";
      this._listPanel.RowCount = 6;
      this._listPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._listPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 110F));
      this._listPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._listPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._listPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._listPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 0F));
      this._listPanel.Size = new System.Drawing.Size(730, 614);
      this._listPanel.TabIndex = 0;
      // 
      // _templateBar
      // 
      this._templateBar.AutoSize = true;
      this._templateBar.ColumnCount = 7;
      this._templateBar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._templateBar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._templateBar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 70F));
      this._templateBar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 70F));
      this._templateBar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 70F));
      this._templateBar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 32F));
      this._templateBar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 32F));
      this._templateBar.Controls.Add(this._lblTemplate, 0, 0);
      this._templateBar.Controls.Add(this._cmbTemplate, 1, 0);
      this._templateBar.Controls.Add(this._btnExportTemplates, 2, 0);
      this._templateBar.Controls.Add(this._btnImportTemplates, 3, 0);
      this._templateBar.Controls.Add(this._btnReport, 4, 0);
      this._templateBar.Controls.Add(this._btnReports, 5, 0);
      this._templateBar.Controls.Add(this._btnColumnSettings, 6, 0);
      this._templateBar.Dock = System.Windows.Forms.DockStyle.Fill;
      this._templateBar.Location = new System.Drawing.Point(3, 3);
      this._templateBar.Name = "_templateBar";
      this._templateBar.RowCount = 1;
      this._templateBar.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._templateBar.Size = new System.Drawing.Size(724, 29);
      this._templateBar.TabIndex = 0;
      // 
      // _lblTemplate
      // 
      this._lblTemplate.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._lblTemplate.AutoSize = true;
      this._lblTemplate.Location = new System.Drawing.Point(3, 8);
      this._lblTemplate.Name = "_lblTemplate";
      this._lblTemplate.Size = new System.Drawing.Size(49, 13);
      this._lblTemplate.TabIndex = 0;
      this._lblTemplate.Text = "Шаблон:";
      // 
      // _cmbTemplate
      // 
      this._cmbTemplate.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._cmbTemplate.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
      this._cmbTemplate.FormattingEnabled = true;
      this._cmbTemplate.Location = new System.Drawing.Point(58, 4);
      this._cmbTemplate.Name = "_cmbTemplate";
      this._cmbTemplate.Size = new System.Drawing.Size(389, 21);
      this._cmbTemplate.TabIndex = 1;
      // 
      // _btnExportTemplates
      // 
      this._btnExportTemplates.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._btnExportTemplates.Location = new System.Drawing.Point(453, 3);
      this._btnExportTemplates.Name = "_btnExportTemplates";
      this._btnExportTemplates.Size = new System.Drawing.Size(64, 23);
      this._btnExportTemplates.TabIndex = 5;
      this._btnExportTemplates.Text = "Экспорт";
      this._btnExportTemplates.UseVisualStyleBackColor = true;
      // 
      // _btnImportTemplates
      // 
      this._btnImportTemplates.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._btnImportTemplates.Location = new System.Drawing.Point(523, 3);
      this._btnImportTemplates.Name = "_btnImportTemplates";
      this._btnImportTemplates.Size = new System.Drawing.Size(64, 23);
      this._btnImportTemplates.TabIndex = 6;
      this._btnImportTemplates.Text = "Импорт";
      this._btnImportTemplates.UseVisualStyleBackColor = true;
      // 
      // _btnReport
      // 
      this._btnReport.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._btnReport.Location = new System.Drawing.Point(593, 3);
      this._btnReport.Name = "_btnReport";
      this._btnReport.Size = new System.Drawing.Size(64, 23);
      this._btnReport.TabIndex = 2;
      this._btnReport.Text = "Отчёт";
      this._btnReport.UseVisualStyleBackColor = true;
      // 
      // _btnReports
      // 
      this._btnReports.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._btnReports.Image = global::Velum.Properties.Resources.List;
      this._btnReports.Location = new System.Drawing.Point(663, 3);
      this._btnReports.Name = "_btnReports";
      this._btnReports.Size = new System.Drawing.Size(26, 23);
      this._btnReports.TabIndex = 3;
      this._btnReports.UseVisualStyleBackColor = true;
      // 
      // _btnColumnSettings
      // 
      this._btnColumnSettings.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._btnColumnSettings.Location = new System.Drawing.Point(695, 3);
      this._btnColumnSettings.Name = "_btnColumnSettings";
      this._btnColumnSettings.Size = new System.Drawing.Size(26, 23);
      this._btnColumnSettings.TabIndex = 4;
      this._btnColumnSettings.UseVisualStyleBackColor = true;
      // 
      // _filtersHost
      // 
      this._filtersHost.AutoScroll = true;
      this._filtersHost.Dock = System.Windows.Forms.DockStyle.Fill;
      this._filtersHost.Location = new System.Drawing.Point(3, 38);
      this._filtersHost.Name = "_filtersHost";
      this._filtersHost.Size = new System.Drawing.Size(724, 104);
      this._filtersHost.TabIndex = 1;
      // 
      // _filterButtonsHost
      // 
      this._filterButtonsHost.ColumnCount = 5;
      this._filterButtonsHost.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 86F));
      this._filterButtonsHost.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 88F));
      this._filterButtonsHost.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 34F));
      this._filterButtonsHost.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 412F));
      this._filterButtonsHost.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 104F));
      this._filterButtonsHost.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
      this._filterButtonsHost.Controls.Add(this._btnFilterHelp, 2, 0);
      this._filterButtonsHost.Controls.Add(this._btnFilterReset, 1, 0);
      this._filterButtonsHost.Controls.Add(this._btnFilterApply, 0, 0);
      this._filterButtonsHost.Controls.Add(this._btnAssemblyComposition, 4, 0);
      this._filterButtonsHost.Controls.Add(this.check_all_doc, 3, 0);
      this._filterButtonsHost.Dock = System.Windows.Forms.DockStyle.Fill;
      this._filterButtonsHost.Location = new System.Drawing.Point(3, 148);
      this._filterButtonsHost.Name = "_filterButtonsHost";
      this._filterButtonsHost.RowCount = 1;
      this._filterButtonsHost.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32F));
      this._filterButtonsHost.Size = new System.Drawing.Size(724, 29);
      this._filterButtonsHost.TabIndex = 2;
      // 
      // _btnFilterHelp
      // 
      this._btnFilterHelp.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._btnFilterHelp.Location = new System.Drawing.Point(177, 4);
      this._btnFilterHelp.Name = "_btnFilterHelp";
      this._btnFilterHelp.Size = new System.Drawing.Size(28, 23);
      this._btnFilterHelp.TabIndex = 2;
      this._btnFilterHelp.Text = "?";
      this._btnFilterHelp.UseVisualStyleBackColor = true;
      // 
      // _btnFilterReset
      // 
      this._btnFilterReset.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._btnFilterReset.Location = new System.Drawing.Point(89, 4);
      this._btnFilterReset.Name = "_btnFilterReset";
      this._btnFilterReset.Size = new System.Drawing.Size(80, 23);
      this._btnFilterReset.TabIndex = 3;
      this._btnFilterReset.Text = "Сброс";
      this._btnFilterReset.UseVisualStyleBackColor = true;
      // 
      // _btnFilterApply
      // 
      this._btnFilterApply.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._btnFilterApply.Location = new System.Drawing.Point(3, 4);
      this._btnFilterApply.Name = "_btnFilterApply";
      this._btnFilterApply.Size = new System.Drawing.Size(80, 23);
      this._btnFilterApply.TabIndex = 2;
      this._btnFilterApply.Text = "Применить";
      this._btnFilterApply.UseVisualStyleBackColor = true;
      // 
      // _btnAssemblyComposition
      // 
      this._btnAssemblyComposition.Anchor = System.Windows.Forms.AnchorStyles.Right;
      this._btnAssemblyComposition.Location = new System.Drawing.Point(625, 4);
      this._btnAssemblyComposition.Name = "_btnAssemblyComposition";
      this._btnAssemblyComposition.Size = new System.Drawing.Size(96, 23);
      this._btnAssemblyComposition.TabIndex = 1;
      this._btnAssemblyComposition.Text = "Состав сборки";
      this._btnAssemblyComposition.UseVisualStyleBackColor = true;
      // 
      // check_all_doc
      // 
      this.check_all_doc.Anchor = System.Windows.Forms.AnchorStyles.Right;
      this.check_all_doc.AutoSize = true;
      this.check_all_doc.Location = new System.Drawing.Point(506, 7);
      this.check_all_doc.Name = "check_all_doc";
      this.check_all_doc.Size = new System.Drawing.Size(111, 17);
      this.check_all_doc.TabIndex = 4;
      this.check_all_doc.Text = "Все компоненты";
      this.check_all_doc.UseVisualStyleBackColor = true;
      // 
      // _listStatusLabel
      // 
      this._listStatusLabel.AutoSize = true;
      this._listStatusLabel.Dock = System.Windows.Forms.DockStyle.Fill;
      this._listStatusLabel.Location = new System.Drawing.Point(3, 180);
      this._listStatusLabel.Name = "_listStatusLabel";
      this._listStatusLabel.Size = new System.Drawing.Size(724, 13);
      this._listStatusLabel.TabIndex = 3;
      // 
      // _listView
      // 
      this._listView.AllowDrop = true;
      this._listView.Dock = System.Windows.Forms.DockStyle.Fill;
      this._listView.FullRowSelect = true;
      this._listView.GridLines = true;
      this._listView.HideSelection = false;
      this._listView.Location = new System.Drawing.Point(3, 196);
      this._listView.Name = "_listView";
      this._listView.Size = new System.Drawing.Size(724, 415);
      this._listView.TabIndex = 4;
      this._listView.UseCompatibleStateImageBehavior = false;
      this._listView.View = System.Windows.Forms.View.Details;
      // 
      // _totalsHost
      // 
      this._totalsHost.AutoScroll = true;
      this._totalsHost.Dock = System.Windows.Forms.DockStyle.Fill;
      this._totalsHost.Location = new System.Drawing.Point(3, 617);
      this._totalsHost.Name = "_totalsHost";
      this._totalsHost.Size = new System.Drawing.Size(724, 1);
      this._totalsHost.TabIndex = 5;
      this._totalsHost.Visible = false;
      // 
      // VelumAssemblyRegistryForm
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.ClientSize = new System.Drawing.Size(1100, 640);
      this.Controls.Add(this._rootLayout);
      this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
      this.MinimizeBox = false;
      this.Name = "VelumAssemblyRegistryForm";
      this.ShowInTaskbar = false;
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
      this.Text = "Реестр изделия";
      this._rootLayout.ResumeLayout(false);
      this._splitContainer.Panel1.ResumeLayout(false);
      this._splitContainer.Panel2.ResumeLayout(false);
      ((System.ComponentModel.ISupportInitialize)(this._splitContainer)).EndInit();
      this._splitContainer.ResumeLayout(false);
      this._treePanel.ResumeLayout(false);
      this._treePanel.PerformLayout();
      this._treeSearchPanel.ResumeLayout(false);
      this._treeSearchPanel.PerformLayout();
      this._listPanel.ResumeLayout(false);
      this._listPanel.PerformLayout();
      this._templateBar.ResumeLayout(false);
      this._templateBar.PerformLayout();
      this._filterButtonsHost.ResumeLayout(false);
      this._filterButtonsHost.PerformLayout();
      this.ResumeLayout(false);

    }

    #endregion

    private System.Windows.Forms.TableLayoutPanel _rootLayout;
    private System.Windows.Forms.SplitContainer _splitContainer;
    private System.Windows.Forms.TableLayoutPanel _treePanel;
    private System.Windows.Forms.Label _lblTree;
    private System.Windows.Forms.TableLayoutPanel _treeSearchPanel;
    private System.Windows.Forms.Label _lblTreeSearch;
    private System.Windows.Forms.TextBox _treeSearchBox;
    private System.Windows.Forms.Button _btnTreeSearchPrev;
    private System.Windows.Forms.Button _btnTreeSearchNext;
    private System.Windows.Forms.Button _btnTreeSearchClear;
    private System.Windows.Forms.Button _btnStop;
    private System.Windows.Forms.Label _treeSearchStatusLabel;
    private System.Windows.Forms.TreeView _folderTreeView;
    private System.Windows.Forms.ProgressBar _progressBar;
    private System.Windows.Forms.Label _progressLabel;
    private System.Windows.Forms.TableLayoutPanel _listPanel;
    private System.Windows.Forms.TableLayoutPanel _templateBar;
    private System.Windows.Forms.Label _lblTemplate;
    private System.Windows.Forms.ComboBox _cmbTemplate;
    private System.Windows.Forms.Button _btnExportTemplates;
    private System.Windows.Forms.Button _btnImportTemplates;
    private System.Windows.Forms.Button _btnReport;
    private System.Windows.Forms.Button _btnReports;
    private System.Windows.Forms.Button _btnColumnSettings;
    private System.Windows.Forms.Panel _filtersHost;
    private System.Windows.Forms.TableLayoutPanel _filterButtonsHost;
    private System.Windows.Forms.Button _btnAssemblyComposition;
    private System.Windows.Forms.Label _listStatusLabel;
    private System.Windows.Forms.ListView _listView;
    private System.Windows.Forms.Panel _totalsHost;
    private System.Windows.Forms.Button _btnFilterHelp;
    private System.Windows.Forms.Button _btnFilterReset;
    private System.Windows.Forms.Button _btnFilterApply;
    private System.Windows.Forms.CheckBox check_all_doc;
  }
}
