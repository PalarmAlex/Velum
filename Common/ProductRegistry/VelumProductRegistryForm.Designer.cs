namespace Velum.UI
{
  internal sealed partial class VelumProductRegistryForm
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
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VelumProductRegistryForm));
      this._rootLayout = new System.Windows.Forms.TableLayoutPanel();
      this._splitContainer = new System.Windows.Forms.SplitContainer();
      this._treePanel = new System.Windows.Forms.TableLayoutPanel();
      this._lblTree = new System.Windows.Forms.Label();
      this._folderSearchPanel = new System.Windows.Forms.TableLayoutPanel();
      this._lblFolderSearchName = new System.Windows.Forms.Label();
      this._folderSearchNameBox = new System.Windows.Forms.TextBox();
      this._lblFolderSearchDescription = new System.Windows.Forms.Label();
      this._folderSearchDescriptionBox = new System.Windows.Forms.TextBox();
      this._folderSearchButtonsRow = new System.Windows.Forms.TableLayoutPanel();
      this._chkShowDescriptions = new System.Windows.Forms.CheckBox();
      this._btnFolderSearchPrev = new System.Windows.Forms.Button();
      this._btnFolderSearchNext = new System.Windows.Forms.Button();
      this._btnFolderSearchClear = new System.Windows.Forms.Button();
      this._folderSearchStatusLabel = new System.Windows.Forms.Label();
      this._folderTreeView = new System.Windows.Forms.TreeView();
      this._indexProgressBar = new System.Windows.Forms.ProgressBar();
      this._listPanel = new System.Windows.Forms.TableLayoutPanel();
      this._filtersHost = new System.Windows.Forms.TableLayoutPanel();
      this._filtersTopRow = new System.Windows.Forms.TableLayoutPanel();
      this._lblFilterStatus = new System.Windows.Forms.Label();
      this._filterStatusBox = new System.Windows.Forms.ComboBox();
      this._btnFilterApply = new System.Windows.Forms.Button();
      this._btnFilterReset = new System.Windows.Forms.Button();
      this._btnFilterHelp = new System.Windows.Forms.Button();
      this._btnStop = new System.Windows.Forms.Button();
      this._btnVerifyPaths = new System.Windows.Forms.Button();
      this._btnFolderAutoNames = new System.Windows.Forms.Button();
      this._filtersBottomRow = new System.Windows.Forms.TableLayoutPanel();
      this._btnReports = new System.Windows.Forms.Button();
      this._lblFilterDesignation = new System.Windows.Forms.Label();
      this._filterDesignationBox = new System.Windows.Forms.TextBox();
      this._lblFilterName = new System.Windows.Forms.Label();
      this._filterNameBox = new System.Windows.Forms.TextBox();
      this._btnReport = new System.Windows.Forms.Button();
      this._btnConvertRelativePaths = new System.Windows.Forms.Button();
      this._listStatusLabel = new System.Windows.Forms.Label();
      this._listView = new System.Windows.Forms.ListView();
      this._colDesignation = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
      this._colName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
      this._colStatus = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
      this._rootLayout.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)(this._splitContainer)).BeginInit();
      this._splitContainer.Panel1.SuspendLayout();
      this._splitContainer.Panel2.SuspendLayout();
      this._splitContainer.SuspendLayout();
      this._treePanel.SuspendLayout();
      this._folderSearchPanel.SuspendLayout();
      this._folderSearchButtonsRow.SuspendLayout();
      this._listPanel.SuspendLayout();
      this._filtersHost.SuspendLayout();
      this._filtersTopRow.SuspendLayout();
      this._filtersBottomRow.SuspendLayout();
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
      this._rootLayout.Size = new System.Drawing.Size(980, 560);
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
      this._splitContainer.Size = new System.Drawing.Size(954, 534);
      this._splitContainer.SplitterDistance = 344;
      this._splitContainer.TabIndex = 0;
      // 
      // _treePanel
      // 
      this._treePanel.ColumnCount = 1;
      this._treePanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._treePanel.Controls.Add(this._lblTree, 0, 0);
      this._treePanel.Controls.Add(this._folderSearchPanel, 0, 1);
      this._treePanel.Controls.Add(this._folderSearchStatusLabel, 0, 2);
      this._treePanel.Controls.Add(this._folderTreeView, 0, 3);
      this._treePanel.Controls.Add(this._indexProgressBar, 0, 4);
      this._treePanel.Dock = System.Windows.Forms.DockStyle.Fill;
      this._treePanel.Location = new System.Drawing.Point(0, 0);
      this._treePanel.Name = "_treePanel";
      this._treePanel.RowCount = 5;
      this._treePanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._treePanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._treePanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._treePanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._treePanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 0F));
      this._treePanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
      this._treePanel.Size = new System.Drawing.Size(344, 534);
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
      this._lblTree.Size = new System.Drawing.Size(338, 13);
      this._lblTree.TabIndex = 0;
      this._lblTree.Text = "Каталоги";
      // 
      // _folderSearchPanel
      // 
      this._folderSearchPanel.ColumnCount = 4;
      this._folderSearchPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._folderSearchPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
      this._folderSearchPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._folderSearchPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
      this._folderSearchPanel.Controls.Add(this._lblFolderSearchName, 0, 0);
      this._folderSearchPanel.Controls.Add(this._folderSearchNameBox, 1, 0);
      this._folderSearchPanel.Controls.Add(this._lblFolderSearchDescription, 2, 0);
      this._folderSearchPanel.Controls.Add(this._folderSearchDescriptionBox, 3, 0);
      this._folderSearchPanel.Controls.Add(this._folderSearchButtonsRow, 0, 1);
      this._folderSearchPanel.Dock = System.Windows.Forms.DockStyle.Fill;
      this._folderSearchPanel.Location = new System.Drawing.Point(3, 20);
      this._folderSearchPanel.Name = "_folderSearchPanel";
      this._folderSearchPanel.RowCount = 2;
      this._folderSearchPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._folderSearchPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._folderSearchPanel.Size = new System.Drawing.Size(338, 56);
      this._folderSearchPanel.TabIndex = 1;
      // 
      // _lblFolderSearchName
      // 
      this._lblFolderSearchName.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._lblFolderSearchName.AutoSize = true;
      this._lblFolderSearchName.Location = new System.Drawing.Point(3, 6);
      this._lblFolderSearchName.Name = "_lblFolderSearchName";
      this._lblFolderSearchName.Size = new System.Drawing.Size(32, 13);
      this._lblFolderSearchName.TabIndex = 0;
      this._lblFolderSearchName.Text = "Имя:";
      // 
      // _folderSearchNameBox
      // 
      this._folderSearchNameBox.Dock = System.Windows.Forms.DockStyle.Fill;
      this._folderSearchNameBox.Location = new System.Drawing.Point(41, 3);
      this._folderSearchNameBox.Name = "_folderSearchNameBox";
      this._folderSearchNameBox.Size = new System.Drawing.Size(111, 20);
      this._folderSearchNameBox.TabIndex = 1;
      // 
      // _lblFolderSearchDescription
      // 
      this._lblFolderSearchDescription.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._lblFolderSearchDescription.AutoSize = true;
      this._lblFolderSearchDescription.Location = new System.Drawing.Point(158, 6);
      this._lblFolderSearchDescription.Name = "_lblFolderSearchDescription";
      this._lblFolderSearchDescription.Size = new System.Drawing.Size(60, 13);
      this._lblFolderSearchDescription.TabIndex = 2;
      this._lblFolderSearchDescription.Text = "Описание:";
      // 
      // _folderSearchDescriptionBox
      // 
      this._folderSearchDescriptionBox.Dock = System.Windows.Forms.DockStyle.Fill;
      this._folderSearchDescriptionBox.Location = new System.Drawing.Point(224, 3);
      this._folderSearchDescriptionBox.Name = "_folderSearchDescriptionBox";
      this._folderSearchDescriptionBox.Size = new System.Drawing.Size(111, 20);
      this._folderSearchDescriptionBox.TabIndex = 3;
      // 
      // _folderSearchButtonsRow
      // 
      this._folderSearchButtonsRow.ColumnCount = 4;
      this._folderSearchPanel.SetColumnSpan(this._folderSearchButtonsRow, 4);
      this._folderSearchButtonsRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._folderSearchButtonsRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 32F));
      this._folderSearchButtonsRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 32F));
      this._folderSearchButtonsRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 32F));
      this._folderSearchButtonsRow.Controls.Add(this._chkShowDescriptions, 0, 0);
      this._folderSearchButtonsRow.Controls.Add(this._btnFolderSearchPrev, 1, 0);
      this._folderSearchButtonsRow.Controls.Add(this._btnFolderSearchNext, 2, 0);
      this._folderSearchButtonsRow.Controls.Add(this._btnFolderSearchClear, 3, 0);
      this._folderSearchButtonsRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._folderSearchButtonsRow.Location = new System.Drawing.Point(0, 26);
      this._folderSearchButtonsRow.Margin = new System.Windows.Forms.Padding(0);
      this._folderSearchButtonsRow.Name = "_folderSearchButtonsRow";
      this._folderSearchButtonsRow.RowCount = 1;
      this._folderSearchButtonsRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._folderSearchButtonsRow.Size = new System.Drawing.Size(338, 30);
      this._folderSearchButtonsRow.TabIndex = 4;
      // 
      // _chkShowDescriptions
      // 
      this._chkShowDescriptions.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._chkShowDescriptions.AutoSize = true;
      this._chkShowDescriptions.Location = new System.Drawing.Point(3, 6);
      this._chkShowDescriptions.Name = "_chkShowDescriptions";
      this._chkShowDescriptions.Size = new System.Drawing.Size(126, 17);
      this._chkShowDescriptions.TabIndex = 0;
      this._chkShowDescriptions.Text = "Показать описание";
      this._chkShowDescriptions.UseVisualStyleBackColor = true;
      // 
      // _btnFolderSearchPrev
      // 
      this._btnFolderSearchPrev.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnFolderSearchPrev.Location = new System.Drawing.Point(245, 3);
      this._btnFolderSearchPrev.Name = "_btnFolderSearchPrev";
      this._btnFolderSearchPrev.Size = new System.Drawing.Size(26, 24);
      this._btnFolderSearchPrev.TabIndex = 1;
      this._btnFolderSearchPrev.UseVisualStyleBackColor = true;
      // 
      // _btnFolderSearchNext
      // 
      this._btnFolderSearchNext.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnFolderSearchNext.Location = new System.Drawing.Point(277, 3);
      this._btnFolderSearchNext.Name = "_btnFolderSearchNext";
      this._btnFolderSearchNext.Size = new System.Drawing.Size(26, 24);
      this._btnFolderSearchNext.TabIndex = 2;
      this._btnFolderSearchNext.UseVisualStyleBackColor = true;
      // 
      // _btnFolderSearchClear
      // 
      this._btnFolderSearchClear.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnFolderSearchClear.Location = new System.Drawing.Point(309, 3);
      this._btnFolderSearchClear.Name = "_btnFolderSearchClear";
      this._btnFolderSearchClear.Size = new System.Drawing.Size(26, 24);
      this._btnFolderSearchClear.TabIndex = 3;
      this._btnFolderSearchClear.UseVisualStyleBackColor = true;
      // 
      // _folderSearchStatusLabel
      // 
      this._folderSearchStatusLabel.AutoSize = true;
      this._folderSearchStatusLabel.Dock = System.Windows.Forms.DockStyle.Fill;
      this._folderSearchStatusLabel.ForeColor = System.Drawing.SystemColors.GrayText;
      this._folderSearchStatusLabel.Location = new System.Drawing.Point(3, 79);
      this._folderSearchStatusLabel.Margin = new System.Windows.Forms.Padding(3, 0, 3, 4);
      this._folderSearchStatusLabel.Name = "_folderSearchStatusLabel";
      this._folderSearchStatusLabel.Size = new System.Drawing.Size(338, 13);
      this._folderSearchStatusLabel.TabIndex = 2;
      // 
      // _folderTreeView
      // 
      this._folderTreeView.AllowDrop = true;
      this._folderTreeView.Dock = System.Windows.Forms.DockStyle.Fill;
      this._folderTreeView.DrawMode = System.Windows.Forms.TreeViewDrawMode.OwnerDrawText;
      this._folderTreeView.HideSelection = false;
      this._folderTreeView.LabelEdit = true;
      this._folderTreeView.Location = new System.Drawing.Point(3, 99);
      this._folderTreeView.Name = "_folderTreeView";
      this._folderTreeView.ShowNodeToolTips = true;
      this._folderTreeView.Size = new System.Drawing.Size(338, 432);
      this._folderTreeView.TabIndex = 3;
      // 
      // _indexProgressBar
      // 
      this._indexProgressBar.Dock = System.Windows.Forms.DockStyle.Fill;
      this._indexProgressBar.Location = new System.Drawing.Point(3, 537);
      this._indexProgressBar.Margin = new System.Windows.Forms.Padding(3, 3, 3, 0);
      this._indexProgressBar.Name = "_indexProgressBar";
      this._indexProgressBar.Size = new System.Drawing.Size(338, 1);
      this._indexProgressBar.TabIndex = 4;
      this._indexProgressBar.Visible = false;
      // 
      // _listPanel
      // 
      this._listPanel.ColumnCount = 1;
      this._listPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._listPanel.Controls.Add(this._filtersHost, 0, 0);
      this._listPanel.Controls.Add(this._listView, 0, 1);
      this._listPanel.Dock = System.Windows.Forms.DockStyle.Fill;
      this._listPanel.Location = new System.Drawing.Point(0, 0);
      this._listPanel.Name = "_listPanel";
      this._listPanel.RowCount = 2;
      this._listPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 60F));
      this._listPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._listPanel.Size = new System.Drawing.Size(606, 534);
      this._listPanel.TabIndex = 0;
      // 
      // _filtersHost
      // 
      this._filtersHost.ColumnCount = 1;
      this._filtersHost.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._filtersHost.Controls.Add(this._filtersTopRow, 0, 0);
      this._filtersHost.Controls.Add(this._filtersBottomRow, 0, 1);
      this._filtersHost.Dock = System.Windows.Forms.DockStyle.Fill;
      this._filtersHost.Location = new System.Drawing.Point(0, 0);
      this._filtersHost.Margin = new System.Windows.Forms.Padding(0);
      this._filtersHost.Name = "_filtersHost";
      this._filtersHost.RowCount = 2;
      this._filtersHost.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
      this._filtersHost.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
      this._filtersHost.Size = new System.Drawing.Size(606, 60);
      this._filtersHost.TabIndex = 0;
      // 
      // _filtersTopRow
      // 
      this._filtersTopRow.ColumnCount = 8;
      this._filtersTopRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._filtersTopRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 100F));
      this._filtersTopRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 84F));
      this._filtersTopRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 64F));
      this._filtersTopRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 26F));
      this._filtersTopRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 64F));
      this._filtersTopRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._filtersTopRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._filtersTopRow.Controls.Add(this._lblFilterStatus, 0, 0);
      this._filtersTopRow.Controls.Add(this._filterStatusBox, 1, 0);
      this._filtersTopRow.Controls.Add(this._btnFilterApply, 2, 0);
      this._filtersTopRow.Controls.Add(this._btnFilterReset, 3, 0);
      this._filtersTopRow.Controls.Add(this._btnFilterHelp, 4, 0);
      this._filtersTopRow.Controls.Add(this._btnStop, 5, 0);
      this._filtersTopRow.Controls.Add(this._btnVerifyPaths, 6, 0);
      this._filtersTopRow.Controls.Add(this._btnFolderAutoNames, 7, 0);
      this._filtersTopRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._filtersTopRow.Location = new System.Drawing.Point(0, 0);
      this._filtersTopRow.Margin = new System.Windows.Forms.Padding(0);
      this._filtersTopRow.Name = "_filtersTopRow";
      this._filtersTopRow.RowCount = 1;
      this._filtersTopRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._filtersTopRow.Size = new System.Drawing.Size(606, 30);
      this._filtersTopRow.TabIndex = 0;
      // 
      // _lblFilterStatus
      // 
      this._lblFilterStatus.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._lblFilterStatus.AutoSize = true;
      this._lblFilterStatus.Location = new System.Drawing.Point(3, 8);
      this._lblFilterStatus.Name = "_lblFilterStatus";
      this._lblFilterStatus.Size = new System.Drawing.Size(44, 13);
      this._lblFilterStatus.TabIndex = 0;
      this._lblFilterStatus.Text = "Статус:";
      // 
      // _filterStatusBox
      // 
      this._filterStatusBox.Dock = System.Windows.Forms.DockStyle.Fill;
      this._filterStatusBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
      this._filterStatusBox.Location = new System.Drawing.Point(53, 3);
      this._filterStatusBox.Name = "_filterStatusBox";
      this._filterStatusBox.Size = new System.Drawing.Size(94, 21);
      this._filterStatusBox.TabIndex = 1;
      // 
      // _btnFilterApply
      // 
      this._btnFilterApply.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnFilterApply.Location = new System.Drawing.Point(153, 3);
      this._btnFilterApply.Name = "_btnFilterApply";
      this._btnFilterApply.Size = new System.Drawing.Size(78, 24);
      this._btnFilterApply.TabIndex = 4;
      this._btnFilterApply.Text = "Применить";
      this._btnFilterApply.UseVisualStyleBackColor = true;
      // 
      // _btnFilterReset
      // 
      this._btnFilterReset.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnFilterReset.Location = new System.Drawing.Point(237, 3);
      this._btnFilterReset.Name = "_btnFilterReset";
      this._btnFilterReset.Size = new System.Drawing.Size(58, 24);
      this._btnFilterReset.TabIndex = 5;
      this._btnFilterReset.Text = "Сброс";
      this._btnFilterReset.UseVisualStyleBackColor = true;
      // 
      // _btnFilterHelp
      // 
      this._btnFilterHelp.Location = new System.Drawing.Point(301, 3);
      this._btnFilterHelp.Name = "_btnFilterHelp";
      this._btnFilterHelp.Size = new System.Drawing.Size(20, 24);
      this._btnFilterHelp.TabIndex = 6;
      this._btnFilterHelp.Text = "?";
      this._btnFilterHelp.UseVisualStyleBackColor = true;
      // 
      // _btnStop
      // 
      this._btnStop.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnStop.Enabled = false;
      this._btnStop.Location = new System.Drawing.Point(327, 3);
      this._btnStop.Name = "_btnStop";
      this._btnStop.Size = new System.Drawing.Size(58, 24);
      this._btnStop.TabIndex = 2;
      this._btnStop.Text = "Стоп";
      this._btnStop.UseVisualStyleBackColor = true;
      // 
      // _btnVerifyPaths
      // 
      this._btnVerifyPaths.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnVerifyPaths.Location = new System.Drawing.Point(391, 3);
      this._btnVerifyPaths.Name = "_btnVerifyPaths";
      this._btnVerifyPaths.Size = new System.Drawing.Size(100, 24);
      this._btnVerifyPaths.TabIndex = 3;
      this._btnVerifyPaths.Text = "Проверка путей";
      this._btnVerifyPaths.UseVisualStyleBackColor = true;
      // 
      // _btnFolderAutoNames
      // 
      this._btnFolderAutoNames.Location = new System.Drawing.Point(497, 3);
      this._btnFolderAutoNames.Name = "_btnFolderAutoNames";
      this._btnFolderAutoNames.Size = new System.Drawing.Size(26, 24);
      this._btnFolderAutoNames.TabIndex = 4;
      this._btnFolderAutoNames.UseVisualStyleBackColor = true;
      // 
      // _filtersBottomRow
      // 
      this._filtersBottomRow.ColumnCount = 9;
      this._filtersBottomRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._filtersBottomRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
      this._filtersBottomRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._filtersBottomRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
      this._filtersBottomRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 58F));
      this._filtersBottomRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 70F));
      this._filtersBottomRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 26F));
      this._filtersBottomRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._filtersBottomRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._filtersBottomRow.Controls.Add(this._btnReports, 6, 0);
      this._filtersBottomRow.Controls.Add(this._lblFilterDesignation, 0, 0);
      this._filtersBottomRow.Controls.Add(this._filterDesignationBox, 1, 0);
      this._filtersBottomRow.Controls.Add(this._lblFilterName, 2, 0);
      this._filtersBottomRow.Controls.Add(this._filterNameBox, 3, 0);
      this._filtersBottomRow.Controls.Add(this._btnReport, 5, 0);
      this._filtersBottomRow.Controls.Add(this._listStatusLabel, 7, 0);
      this._filtersBottomRow.Controls.Add(this._btnConvertRelativePaths, 8, 0);
      this._filtersBottomRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._filtersBottomRow.Location = new System.Drawing.Point(0, 30);
      this._filtersBottomRow.Margin = new System.Windows.Forms.Padding(0);
      this._filtersBottomRow.Name = "_filtersBottomRow";
      this._filtersBottomRow.RowCount = 1;
      this._filtersBottomRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._filtersBottomRow.Size = new System.Drawing.Size(606, 30);
      this._filtersBottomRow.TabIndex = 1;
      // 
      // _btnReports
      // 
      this._btnReports.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnReports.Image = global::Velum.Properties.Resources.List;
      this._btnReports.Location = new System.Drawing.Point(510, 3);
      this._btnReports.Name = "_btnReports";
      this._btnReports.Size = new System.Drawing.Size(20, 24);
      this._btnReports.TabIndex = 10;
      this._btnReports.UseVisualStyleBackColor = true;
      // 
      // _lblFilterDesignation
      // 
      this._lblFilterDesignation.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._lblFilterDesignation.AutoSize = true;
      this._lblFilterDesignation.Location = new System.Drawing.Point(3, 8);
      this._lblFilterDesignation.Name = "_lblFilterDesignation";
      this._lblFilterDesignation.Size = new System.Drawing.Size(77, 13);
      this._lblFilterDesignation.TabIndex = 0;
      this._lblFilterDesignation.Text = "Обозначение:";
      // 
      // _filterDesignationBox
      // 
      this._filterDesignationBox.Dock = System.Windows.Forms.DockStyle.Fill;
      this._filterDesignationBox.Location = new System.Drawing.Point(86, 3);
      this._filterDesignationBox.Name = "_filterDesignationBox";
      this._filterDesignationBox.Size = new System.Drawing.Size(96, 20);
      this._filterDesignationBox.TabIndex = 1;
      // 
      // _lblFilterName
      // 
      this._lblFilterName.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._lblFilterName.AutoSize = true;
      this._lblFilterName.Location = new System.Drawing.Point(188, 8);
      this._lblFilterName.Name = "_lblFilterName";
      this._lblFilterName.Size = new System.Drawing.Size(86, 13);
      this._lblFilterName.TabIndex = 2;
      this._lblFilterName.Text = "Наименование:";
      // 
      // _filterNameBox
      // 
      this._filterNameBox.Dock = System.Windows.Forms.DockStyle.Fill;
      this._filterNameBox.Location = new System.Drawing.Point(280, 3);
      this._filterNameBox.Name = "_filterNameBox";
      this._filterNameBox.Size = new System.Drawing.Size(96, 20);
      this._filterNameBox.TabIndex = 3;
      // 
      // _btnReport
      // 
      this._btnReport.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnReport.Location = new System.Drawing.Point(440, 3);
      this._btnReport.Name = "_btnReport";
      this._btnReport.Size = new System.Drawing.Size(64, 24);
      this._btnReport.TabIndex = 8;
      this._btnReport.Text = "Отчёт";
      this._btnReport.UseVisualStyleBackColor = true;
      // 
      // _listStatusLabel
      // 
      this._listStatusLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._listStatusLabel.AutoSize = true;
      this._listStatusLabel.ForeColor = System.Drawing.SystemColors.GrayText;
      this._listStatusLabel.Location = new System.Drawing.Point(541, 8);
      this._listStatusLabel.Margin = new System.Windows.Forms.Padding(8, 0, 3, 0);
      this._listStatusLabel.Name = "_listStatusLabel";
      this._listStatusLabel.Size = new System.Drawing.Size(62, 13);
      this._listStatusLabel.TabIndex = 7;
      this._listStatusLabel.Text = "Записей: 0";
      // 
      // _btnConvertRelativePaths
      // 
      this._btnConvertRelativePaths.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._btnConvertRelativePaths.AutoSize = true;
      this._btnConvertRelativePaths.Location = new System.Drawing.Point(541, 3);
      this._btnConvertRelativePaths.Margin = new System.Windows.Forms.Padding(8, 3, 3, 3);
      this._btnConvertRelativePaths.Name = "_btnConvertRelativePaths";
      this._btnConvertRelativePaths.Size = new System.Drawing.Size(206, 24);
      this._btnConvertRelativePaths.TabIndex = 11;
      this._btnConvertRelativePaths.Text = "Перевести пути в относительные";
      this._btnConvertRelativePaths.UseVisualStyleBackColor = true;
      // 
      // _listView
      // 
      this._listView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this._colDesignation,
            this._colName,
            this._colStatus});
      this._listView.Dock = System.Windows.Forms.DockStyle.Fill;
      this._listView.FullRowSelect = true;
      this._listView.HideSelection = false;
      this._listView.Location = new System.Drawing.Point(3, 63);
      this._listView.Name = "_listView";
      this._listView.Size = new System.Drawing.Size(600, 468);
      this._listView.TabIndex = 1;
      this._listView.UseCompatibleStateImageBehavior = false;
      this._listView.View = System.Windows.Forms.View.Details;
      // 
      // _colDesignation
      // 
      this._colDesignation.Text = "Обозначение";
      this._colDesignation.Width = 180;
      // 
      // _colName
      // 
      this._colName.Text = "Наименование";
      this._colName.Width = 320;
      // 
      // _colStatus
      // 
      this._colStatus.Text = "Статус";
      this._colStatus.Width = 70;
      // 
      // VelumProductRegistryForm
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.ClientSize = new System.Drawing.Size(1160, 560);
      this.Controls.Add(this._rootLayout);
      this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
      this.MinimizeBox = false;
      this.Name = "VelumProductRegistryForm";
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
      this.Text = "Реестр документов";
      this._rootLayout.ResumeLayout(false);
      this._splitContainer.Panel1.ResumeLayout(false);
      this._splitContainer.Panel2.ResumeLayout(false);
      ((System.ComponentModel.ISupportInitialize)(this._splitContainer)).EndInit();
      this._splitContainer.ResumeLayout(false);
      this._treePanel.ResumeLayout(false);
      this._treePanel.PerformLayout();
      this._folderSearchPanel.ResumeLayout(false);
      this._folderSearchPanel.PerformLayout();
      this._folderSearchButtonsRow.ResumeLayout(false);
      this._folderSearchButtonsRow.PerformLayout();
      this._listPanel.ResumeLayout(false);
      this._filtersHost.ResumeLayout(false);
      this._filtersTopRow.ResumeLayout(false);
      this._filtersTopRow.PerformLayout();
      this._filtersBottomRow.ResumeLayout(false);
      this._filtersBottomRow.PerformLayout();
      this.ResumeLayout(false);

    }

    #endregion

    private System.Windows.Forms.TableLayoutPanel _rootLayout;
    private System.Windows.Forms.SplitContainer _splitContainer;
    private System.Windows.Forms.TableLayoutPanel _treePanel;
    private System.Windows.Forms.Label _lblTree;
    private System.Windows.Forms.TableLayoutPanel _folderSearchPanel;
    private System.Windows.Forms.Label _lblFolderSearchName;
    private System.Windows.Forms.TextBox _folderSearchNameBox;
    private System.Windows.Forms.Label _lblFolderSearchDescription;
    private System.Windows.Forms.TextBox _folderSearchDescriptionBox;
    private System.Windows.Forms.TableLayoutPanel _folderSearchButtonsRow;
    private System.Windows.Forms.CheckBox _chkShowDescriptions;
    private System.Windows.Forms.Button _btnFolderSearchPrev;
    private System.Windows.Forms.Button _btnFolderSearchNext;
    private System.Windows.Forms.Button _btnFolderSearchClear;
    private System.Windows.Forms.Label _folderSearchStatusLabel;
    private System.Windows.Forms.TreeView _folderTreeView;
    private System.Windows.Forms.ProgressBar _indexProgressBar;
    private System.Windows.Forms.TableLayoutPanel _listPanel;
    private System.Windows.Forms.TableLayoutPanel _filtersHost;
    private System.Windows.Forms.TableLayoutPanel _filtersTopRow;
    private System.Windows.Forms.TableLayoutPanel _filtersBottomRow;
    private System.Windows.Forms.Label _lblFilterStatus;
    private System.Windows.Forms.ComboBox _filterStatusBox;
    private System.Windows.Forms.Label _lblFilterDesignation;
    private System.Windows.Forms.TextBox _filterDesignationBox;
    private System.Windows.Forms.Label _lblFilterName;
    private System.Windows.Forms.TextBox _filterNameBox;
    private System.Windows.Forms.Button _btnFilterApply;
    private System.Windows.Forms.Button _btnFilterReset;
    private System.Windows.Forms.Button _btnFilterHelp;
    private System.Windows.Forms.Button _btnStop;
    private System.Windows.Forms.Button _btnVerifyPaths;
    private System.Windows.Forms.Button _btnFolderAutoNames;
    private System.Windows.Forms.Button _btnReport;
    private System.Windows.Forms.Label _listStatusLabel;
    private System.Windows.Forms.ListView _listView;
    private System.Windows.Forms.ColumnHeader _colDesignation;
    private System.Windows.Forms.ColumnHeader _colName;
    private System.Windows.Forms.ColumnHeader _colStatus;
    private System.Windows.Forms.Button _btnReports;
    private System.Windows.Forms.Button _btnConvertRelativePaths;
  }
}
