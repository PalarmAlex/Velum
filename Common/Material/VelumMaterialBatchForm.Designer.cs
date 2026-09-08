namespace Velum.UI
{
  internal sealed partial class VelumMaterialBatchForm
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
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VelumMaterialBatchForm));
      this._rootLayout = new System.Windows.Forms.TableLayoutPanel();
      this._partsFolderCaptionRow = new System.Windows.Forms.TableLayoutPanel();
      this._partsFolderCaptionHost = new System.Windows.Forms.Panel();
      this._lblPartsFolder = new System.Windows.Forms.Label();
      this._partsFolderRow = new System.Windows.Forms.TableLayoutPanel();
      this._partsFolderBox = new System.Windows.Forms.TextBox();
      this._btnBrowseParts = new System.Windows.Forms.Button();
      this._btnLoadParts = new System.Windows.Forms.Button();
      this._splitContainer = new System.Windows.Forms.SplitContainer();
      this._materialPanel = new System.Windows.Forms.TableLayoutPanel();
      this._lblMaterialTree = new System.Windows.Forms.Label();
      this._materialSearchRow = new System.Windows.Forms.TableLayoutPanel();
      this._materialFilterBox = new System.Windows.Forms.TextBox();
      this._btnMaterialSearch = new System.Windows.Forms.Button();
      this._btnMaterialPrev = new System.Windows.Forms.Button();
      this._btnMaterialNext = new System.Windows.Forms.Button();
      this._materialSearchStatusLabel = new System.Windows.Forms.Label();
      this._materialTreeViewControl = new System.Windows.Forms.TreeView();
      this._partsPanel = new System.Windows.Forms.TableLayoutPanel();
      this._filtersRow = new System.Windows.Forms.TableLayoutPanel();
      this._lblPartFilter = new System.Windows.Forms.Label();
      this._partFilterBox = new System.Windows.Forms.TextBox();
      this._lblConfigFilter = new System.Windows.Forms.Label();
      this._configFilterBox = new System.Windows.Forms.TextBox();
      this._lblMaterialListFilter = new System.Windows.Forms.Label();
      this._materialListFilterBox = new System.Windows.Forms.TextBox();
      this._btnFilterApply = new System.Windows.Forms.Button();
      this._btnFilterReset = new System.Windows.Forms.Button();
      this._btnFilterHelp = new System.Windows.Forms.Button();
      this._statusLabel = new System.Windows.Forms.Label();
      this._listView = new System.Windows.Forms.ListView();
      this._colPart = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
      this._colConfig = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
      this._colMaterial = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
      this._lblProgress = new System.Windows.Forms.Label();
      this._progressBar = new System.Windows.Forms.ProgressBar();
      this._actionsRow = new System.Windows.Forms.TableLayoutPanel();
      this._btnApply = new System.Windows.Forms.Button();
      this._btnStop = new System.Windows.Forms.Button();
      this._btnClose = new System.Windows.Forms.Button();
      this._rootLayout.SuspendLayout();
      this._partsFolderCaptionRow.SuspendLayout();
      this._partsFolderCaptionHost.SuspendLayout();
      this._partsFolderRow.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)(this._splitContainer)).BeginInit();
      this._splitContainer.Panel1.SuspendLayout();
      this._splitContainer.Panel2.SuspendLayout();
      this._splitContainer.SuspendLayout();
      this._materialPanel.SuspendLayout();
      this._materialSearchRow.SuspendLayout();
      this._partsPanel.SuspendLayout();
      this._filtersRow.SuspendLayout();
      this._actionsRow.SuspendLayout();
      this.SuspendLayout();
      // 
      // _rootLayout
      // 
      this._rootLayout.ColumnCount = 1;
      this._rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._rootLayout.Controls.Add(this._partsFolderCaptionRow, 0, 0);
      this._rootLayout.Controls.Add(this._partsFolderRow, 0, 1);
      this._rootLayout.Controls.Add(this._splitContainer, 0, 2);
      this._rootLayout.Controls.Add(this._lblProgress, 0, 3);
      this._rootLayout.Controls.Add(this._progressBar, 0, 4);
      this._rootLayout.Controls.Add(this._actionsRow, 0, 5);
      this._rootLayout.Dock = System.Windows.Forms.DockStyle.Fill;
      this._rootLayout.Location = new System.Drawing.Point(0, 0);
      this._rootLayout.Name = "_rootLayout";
      this._rootLayout.Padding = new System.Windows.Forms.Padding(10);
      this._rootLayout.RowCount = 6;
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 36F));
      this._rootLayout.Size = new System.Drawing.Size(984, 581);
      this._rootLayout.TabIndex = 0;
      // 
      // _partsFolderCaptionRow
      // 
      this._partsFolderCaptionRow.ColumnCount = 1;
      this._partsFolderCaptionRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._partsFolderCaptionRow.Controls.Add(this._partsFolderCaptionHost, 0, 0);
      this._partsFolderCaptionRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._partsFolderCaptionRow.Location = new System.Drawing.Point(13, 10);
      this._partsFolderCaptionRow.Margin = new System.Windows.Forms.Padding(3, 0, 3, 0);
      this._partsFolderCaptionRow.Name = "_partsFolderCaptionRow";
      this._partsFolderCaptionRow.RowCount = 1;
      this._partsFolderCaptionRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._partsFolderCaptionRow.Size = new System.Drawing.Size(958, 22);
      this._partsFolderCaptionRow.TabIndex = 0;
      // 
      // _partsFolderCaptionHost
      // 
      this._partsFolderCaptionHost.Controls.Add(this._lblPartsFolder);
      this._partsFolderCaptionHost.Dock = System.Windows.Forms.DockStyle.Fill;
      this._partsFolderCaptionHost.Location = new System.Drawing.Point(0, 0);
      this._partsFolderCaptionHost.Margin = new System.Windows.Forms.Padding(0);
      this._partsFolderCaptionHost.Name = "_partsFolderCaptionHost";
      this._partsFolderCaptionHost.Size = new System.Drawing.Size(958, 22);
      this._partsFolderCaptionHost.TabIndex = 0;
      // 
      // _lblPartsFolder
      // 
      this._lblPartsFolder.AutoSize = true;
      this._lblPartsFolder.Dock = System.Windows.Forms.DockStyle.Left;
      this._lblPartsFolder.Location = new System.Drawing.Point(0, 0);
      this._lblPartsFolder.Margin = new System.Windows.Forms.Padding(0);
      this._lblPartsFolder.Name = "_lblPartsFolder";
      this._lblPartsFolder.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
      this._lblPartsFolder.Size = new System.Drawing.Size(95, 17);
      this._lblPartsFolder.TabIndex = 0;
      this._lblPartsFolder.Text = "Каталог деталей:";
      // 
      // _partsFolderRow
      // 
      this._partsFolderRow.ColumnCount = 3;
      this._partsFolderRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._partsFolderRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 110F));
      this._partsFolderRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 110F));
      this._partsFolderRow.Controls.Add(this._partsFolderBox, 0, 0);
      this._partsFolderRow.Controls.Add(this._btnBrowseParts, 1, 0);
      this._partsFolderRow.Controls.Add(this._btnLoadParts, 2, 0);
      this._partsFolderRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._partsFolderRow.Location = new System.Drawing.Point(13, 35);
      this._partsFolderRow.Name = "_partsFolderRow";
      this._partsFolderRow.RowCount = 1;
      this._partsFolderRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._partsFolderRow.Size = new System.Drawing.Size(958, 28);
      this._partsFolderRow.TabIndex = 1;
      // 
      // _partsFolderBox
      // 
      this._partsFolderBox.Dock = System.Windows.Forms.DockStyle.Fill;
      this._partsFolderBox.Location = new System.Drawing.Point(3, 3);
      this._partsFolderBox.Name = "_partsFolderBox";
      this._partsFolderBox.Size = new System.Drawing.Size(732, 20);
      this._partsFolderBox.TabIndex = 0;
      // 
      // _btnBrowseParts
      // 
      this._btnBrowseParts.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnBrowseParts.Location = new System.Drawing.Point(741, 3);
      this._btnBrowseParts.Name = "_btnBrowseParts";
      this._btnBrowseParts.Size = new System.Drawing.Size(104, 22);
      this._btnBrowseParts.TabIndex = 1;
      this._btnBrowseParts.Text = "Обзор…";
      this._btnBrowseParts.UseVisualStyleBackColor = true;
      this._btnBrowseParts.Click += new System.EventHandler(this.OnBrowsePartsFolder);
      // 
      // _btnLoadParts
      // 
      this._btnLoadParts.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnLoadParts.Location = new System.Drawing.Point(851, 3);
      this._btnLoadParts.Name = "_btnLoadParts";
      this._btnLoadParts.Size = new System.Drawing.Size(104, 22);
      this._btnLoadParts.TabIndex = 2;
      this._btnLoadParts.Text = "Загрузить";
      this._btnLoadParts.UseVisualStyleBackColor = true;
      this._btnLoadParts.Click += new System.EventHandler(this.OnLoadParts);
      // 
      // _splitContainer
      // 
      this._splitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
      this._splitContainer.Location = new System.Drawing.Point(13, 69);
      this._splitContainer.Name = "_splitContainer";
      // 
      // _splitContainer.Panel1
      // 
      this._splitContainer.Panel1.Controls.Add(this._materialPanel);
      // 
      // _splitContainer.Panel2
      // 
      this._splitContainer.Panel2.Controls.Add(this._partsPanel);
      this._splitContainer.Size = new System.Drawing.Size(958, 426);
      this._splitContainer.SplitterDistance = 360;
      this._splitContainer.TabIndex = 2;
      // 
      // _materialPanel
      // 
      this._materialPanel.ColumnCount = 1;
      this._materialPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._materialPanel.Controls.Add(this._lblMaterialTree, 0, 0);
      this._materialPanel.Controls.Add(this._materialSearchRow, 0, 1);
      this._materialPanel.Controls.Add(this._materialSearchStatusLabel, 0, 2);
      this._materialPanel.Controls.Add(this._materialTreeViewControl, 0, 3);
      this._materialPanel.Dock = System.Windows.Forms.DockStyle.Fill;
      this._materialPanel.Location = new System.Drawing.Point(0, 0);
      this._materialPanel.Name = "_materialPanel";
      this._materialPanel.RowCount = 4;
      this._materialPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._materialPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._materialPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._materialPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._materialPanel.Size = new System.Drawing.Size(360, 426);
      this._materialPanel.TabIndex = 0;
      // 
      // _lblMaterialTree
      // 
      this._lblMaterialTree.AutoSize = true;
      this._lblMaterialTree.Dock = System.Windows.Forms.DockStyle.Fill;
      this._lblMaterialTree.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold);
      this._lblMaterialTree.Location = new System.Drawing.Point(3, 0);
      this._lblMaterialTree.Margin = new System.Windows.Forms.Padding(3, 0, 3, 4);
      this._lblMaterialTree.Name = "_lblMaterialTree";
      this._lblMaterialTree.Size = new System.Drawing.Size(354, 13);
      this._lblMaterialTree.TabIndex = 0;
      this._lblMaterialTree.Text = "Материалы SOLIDWORKS";
      // 
      // _materialSearchRow
      // 
      this._materialSearchRow.ColumnCount = 4;
      this._materialSearchRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._materialSearchRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 60F));
      this._materialSearchRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 32F));
      this._materialSearchRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 32F));
      this._materialSearchRow.Controls.Add(this._materialFilterBox, 0, 0);
      this._materialSearchRow.Controls.Add(this._btnMaterialSearch, 1, 0);
      this._materialSearchRow.Controls.Add(this._btnMaterialPrev, 2, 0);
      this._materialSearchRow.Controls.Add(this._btnMaterialNext, 3, 0);
      this._materialSearchRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._materialSearchRow.Location = new System.Drawing.Point(3, 20);
      this._materialSearchRow.Name = "_materialSearchRow";
      this._materialSearchRow.RowCount = 1;
      this._materialSearchRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._materialSearchRow.Size = new System.Drawing.Size(354, 28);
      this._materialSearchRow.TabIndex = 1;
      // 
      // _materialFilterBox
      // 
      this._materialFilterBox.Dock = System.Windows.Forms.DockStyle.Fill;
      this._materialFilterBox.Location = new System.Drawing.Point(3, 3);
      this._materialFilterBox.Name = "_materialFilterBox";
      this._materialFilterBox.Size = new System.Drawing.Size(224, 20);
      this._materialFilterBox.TabIndex = 0;
      // 
      // _btnMaterialSearch
      // 
      this._btnMaterialSearch.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnMaterialSearch.Location = new System.Drawing.Point(233, 3);
      this._btnMaterialSearch.Name = "_btnMaterialSearch";
      this._btnMaterialSearch.Size = new System.Drawing.Size(54, 22);
      this._btnMaterialSearch.TabIndex = 1;
      this._btnMaterialSearch.Text = "Поиск";
      this._btnMaterialSearch.UseVisualStyleBackColor = true;
      // 
      // _btnMaterialPrev
      // 
      this._btnMaterialPrev.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnMaterialPrev.Location = new System.Drawing.Point(293, 3);
      this._btnMaterialPrev.Name = "_btnMaterialPrev";
      this._btnMaterialPrev.Size = new System.Drawing.Size(26, 22);
      this._btnMaterialPrev.TabIndex = 2;
      this._btnMaterialPrev.Text = "▲";
      this._btnMaterialPrev.UseVisualStyleBackColor = true;
      // 
      // _btnMaterialNext
      // 
      this._btnMaterialNext.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnMaterialNext.Location = new System.Drawing.Point(325, 3);
      this._btnMaterialNext.Name = "_btnMaterialNext";
      this._btnMaterialNext.Size = new System.Drawing.Size(26, 22);
      this._btnMaterialNext.TabIndex = 3;
      this._btnMaterialNext.Text = "▼";
      this._btnMaterialNext.UseVisualStyleBackColor = true;
      // 
      // _materialSearchStatusLabel
      // 
      this._materialSearchStatusLabel.AutoSize = true;
      this._materialSearchStatusLabel.Dock = System.Windows.Forms.DockStyle.Fill;
      this._materialSearchStatusLabel.ForeColor = System.Drawing.SystemColors.GrayText;
      this._materialSearchStatusLabel.Location = new System.Drawing.Point(3, 51);
      this._materialSearchStatusLabel.Margin = new System.Windows.Forms.Padding(3, 0, 3, 4);
      this._materialSearchStatusLabel.Name = "_materialSearchStatusLabel";
      this._materialSearchStatusLabel.Size = new System.Drawing.Size(354, 13);
      this._materialSearchStatusLabel.TabIndex = 2;
      // 
      // _materialTreeViewControl
      // 
      this._materialTreeViewControl.Dock = System.Windows.Forms.DockStyle.Fill;
      this._materialTreeViewControl.Location = new System.Drawing.Point(3, 71);
      this._materialTreeViewControl.Name = "_materialTreeViewControl";
      this._materialTreeViewControl.Size = new System.Drawing.Size(354, 352);
      this._materialTreeViewControl.TabIndex = 3;
      // 
      // _partsPanel
      // 
      this._partsPanel.ColumnCount = 1;
      this._partsPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._partsPanel.Controls.Add(this._filtersRow, 0, 0);
      this._partsPanel.Controls.Add(this._statusLabel, 0, 1);
      this._partsPanel.Controls.Add(this._listView, 0, 2);
      this._partsPanel.Dock = System.Windows.Forms.DockStyle.Fill;
      this._partsPanel.Location = new System.Drawing.Point(0, 0);
      this._partsPanel.Name = "_partsPanel";
      this._partsPanel.RowCount = 3;
      this._partsPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._partsPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._partsPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._partsPanel.Size = new System.Drawing.Size(594, 426);
      this._partsPanel.TabIndex = 0;
      // 
      // _filtersRow
      // 
      this._filtersRow.ColumnCount = 9;
      this._filtersRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._filtersRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
      this._filtersRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._filtersRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
      this._filtersRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._filtersRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33334F));
      this._filtersRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 90F));
      this._filtersRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 70F));
      this._filtersRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 32F));
      this._filtersRow.Controls.Add(this._lblPartFilter, 0, 0);
      this._filtersRow.Controls.Add(this._partFilterBox, 1, 0);
      this._filtersRow.Controls.Add(this._lblConfigFilter, 2, 0);
      this._filtersRow.Controls.Add(this._configFilterBox, 3, 0);
      this._filtersRow.Controls.Add(this._lblMaterialListFilter, 4, 0);
      this._filtersRow.Controls.Add(this._materialListFilterBox, 5, 0);
      this._filtersRow.Controls.Add(this._btnFilterApply, 6, 0);
      this._filtersRow.Controls.Add(this._btnFilterReset, 7, 0);
      this._filtersRow.Controls.Add(this._btnFilterHelp, 8, 0);
      this._filtersRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._filtersRow.Location = new System.Drawing.Point(3, 3);
      this._filtersRow.Name = "_filtersRow";
      this._filtersRow.RowCount = 1;
      this._filtersRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._filtersRow.Size = new System.Drawing.Size(588, 28);
      this._filtersRow.TabIndex = 0;
      // 
      // _lblPartFilter
      // 
      this._lblPartFilter.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._lblPartFilter.AutoSize = true;
      this._lblPartFilter.Location = new System.Drawing.Point(3, 7);
      this._lblPartFilter.Name = "_lblPartFilter";
      this._lblPartFilter.Size = new System.Drawing.Size(48, 13);
      this._lblPartFilter.TabIndex = 0;
      this._lblPartFilter.Text = "Деталь:";
      // 
      // _partFilterBox
      // 
      this._partFilterBox.Dock = System.Windows.Forms.DockStyle.Fill;
      this._partFilterBox.Location = new System.Drawing.Point(57, 3);
      this._partFilterBox.Name = "_partFilterBox";
      this._partFilterBox.Size = new System.Drawing.Size(120, 20);
      this._partFilterBox.TabIndex = 1;
      // 
      // _lblConfigFilter
      // 
      this._lblConfigFilter.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._lblConfigFilter.AutoSize = true;
      this._lblConfigFilter.Location = new System.Drawing.Point(183, 7);
      this._lblConfigFilter.Name = "_lblConfigFilter";
      this._lblConfigFilter.Size = new System.Drawing.Size(83, 13);
      this._lblConfigFilter.TabIndex = 2;
      this._lblConfigFilter.Text = "Конфигурация:";
      // 
      // _configFilterBox
      // 
      this._configFilterBox.Dock = System.Windows.Forms.DockStyle.Fill;
      this._configFilterBox.Location = new System.Drawing.Point(272, 3);
      this._configFilterBox.Name = "_configFilterBox";
      this._configFilterBox.Size = new System.Drawing.Size(120, 20);
      this._configFilterBox.TabIndex = 3;
      // 
      // _lblMaterialListFilter
      // 
      this._lblMaterialListFilter.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._lblMaterialListFilter.AutoSize = true;
      this._lblMaterialListFilter.Location = new System.Drawing.Point(398, 7);
      this._lblMaterialListFilter.Name = "_lblMaterialListFilter";
      this._lblMaterialListFilter.Size = new System.Drawing.Size(60, 13);
      this._lblMaterialListFilter.TabIndex = 4;
      this._lblMaterialListFilter.Text = "Материал:";
      // 
      // _materialListFilterBox
      // 
      this._materialListFilterBox.Dock = System.Windows.Forms.DockStyle.Fill;
      this._materialListFilterBox.Location = new System.Drawing.Point(464, 3);
      this._materialListFilterBox.Name = "_materialListFilterBox";
      this._materialListFilterBox.Size = new System.Drawing.Size(121, 20);
      this._materialListFilterBox.TabIndex = 5;
      // 
      // _btnFilterApply
      // 
      this._btnFilterApply.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnFilterApply.Location = new System.Drawing.Point(591, 3);
      this._btnFilterApply.Name = "_btnFilterApply";
      this._btnFilterApply.Size = new System.Drawing.Size(84, 22);
      this._btnFilterApply.TabIndex = 6;
      this._btnFilterApply.Text = "Применить";
      this._btnFilterApply.UseVisualStyleBackColor = true;
      // 
      // _btnFilterReset
      // 
      this._btnFilterReset.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnFilterReset.Location = new System.Drawing.Point(681, 3);
      this._btnFilterReset.Name = "_btnFilterReset";
      this._btnFilterReset.Size = new System.Drawing.Size(64, 22);
      this._btnFilterReset.TabIndex = 7;
      this._btnFilterReset.Text = "Сброс";
      this._btnFilterReset.UseVisualStyleBackColor = true;
      // 
      // _btnFilterHelp
      // 
      this._btnFilterHelp.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnFilterHelp.Location = new System.Drawing.Point(751, 3);
      this._btnFilterHelp.Name = "_btnFilterHelp";
      this._btnFilterHelp.Size = new System.Drawing.Size(26, 22);
      this._btnFilterHelp.TabIndex = 8;
      this._btnFilterHelp.Text = "?";
      this._btnFilterHelp.UseVisualStyleBackColor = true;
      // 
      // _statusLabel
      // 
      this._statusLabel.AutoSize = true;
      this._statusLabel.Dock = System.Windows.Forms.DockStyle.Fill;
      this._statusLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold);
      this._statusLabel.Location = new System.Drawing.Point(3, 37);
      this._statusLabel.Margin = new System.Windows.Forms.Padding(3, 3, 3, 4);
      this._statusLabel.Name = "_statusLabel";
      this._statusLabel.Size = new System.Drawing.Size(588, 13);
      this._statusLabel.TabIndex = 1;
      this._statusLabel.Text = "Список пуст — укажите каталог и нажмите «Загрузить»";
      // 
      // _listView
      // 
      this._listView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this._colPart,
            this._colConfig,
            this._colMaterial});
      this._listView.Dock = System.Windows.Forms.DockStyle.Fill;
      this._listView.FullRowSelect = true;
      this._listView.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Clickable;
      this._listView.ColumnClick += new System.Windows.Forms.ColumnClickEventHandler(this.OnListViewColumnClick);
      this._listView.HideSelection = false;
      this._listView.Location = new System.Drawing.Point(3, 57);
      this._listView.MultiSelect = true;
      this._listView.Name = "_listView";
      this._listView.Size = new System.Drawing.Size(588, 366);
      this._listView.TabIndex = 2;
      this._listView.UseCompatibleStateImageBehavior = false;
      this._listView.View = System.Windows.Forms.View.Details;
      // 
      // _colPart
      // 
      this._colPart.Text = "Деталь";
      this._colPart.Width = 200;
      // 
      // _colConfig
      // 
      this._colConfig.Text = "Конфигурация";
      this._colConfig.Width = 140;
      // 
      // _colMaterial
      // 
      this._colMaterial.Text = "Материал";
      this._colMaterial.Width = 220;
      // 
      // _lblProgress
      // 
      this._lblProgress.AutoSize = true;
      this._lblProgress.Dock = System.Windows.Forms.DockStyle.Fill;
      this._lblProgress.Location = new System.Drawing.Point(13, 498);
      this._lblProgress.Name = "_lblProgress";
      this._lblProgress.Size = new System.Drawing.Size(958, 13);
      this._lblProgress.TabIndex = 3;
      this._lblProgress.Text = "Применение:";
      this._lblProgress.Visible = false;
      // 
      // _progressBar
      // 
      this._progressBar.Dock = System.Windows.Forms.DockStyle.Fill;
      this._progressBar.Location = new System.Drawing.Point(13, 514);
      this._progressBar.Name = "_progressBar";
      this._progressBar.Size = new System.Drawing.Size(958, 18);
      this._progressBar.TabIndex = 4;
      this._progressBar.Visible = false;
      // 
      // _actionsRow
      // 
      this._actionsRow.ColumnCount = 4;
      this._actionsRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._actionsRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._actionsRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._actionsRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._actionsRow.Controls.Add(this._btnApply, 1, 0);
      this._actionsRow.Controls.Add(this._btnStop, 2, 0);
      this._actionsRow.Controls.Add(this._btnClose, 3, 0);
      this._actionsRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._actionsRow.Location = new System.Drawing.Point(13, 538);
      this._actionsRow.Name = "_actionsRow";
      this._actionsRow.RowCount = 1;
      this._actionsRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._actionsRow.Size = new System.Drawing.Size(958, 30);
      this._actionsRow.TabIndex = 5;
      // 
      // _btnApply
      // 
      this._btnApply.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnApply.Location = new System.Drawing.Point(703, 3);
      this._btnApply.Name = "_btnApply";
      this._btnApply.Size = new System.Drawing.Size(88, 24);
      this._btnApply.TabIndex = 0;
      this._btnApply.Text = "Применить";
      this._btnApply.UseVisualStyleBackColor = true;
      this._btnApply.Click += new System.EventHandler(this.OnApply);
      // 
      // _btnStop
      // 
      this._btnStop.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnStop.Enabled = false;
      this._btnStop.Location = new System.Drawing.Point(797, 3);
      this._btnStop.Name = "_btnStop";
      this._btnStop.Size = new System.Drawing.Size(64, 24);
      this._btnStop.TabIndex = 1;
      this._btnStop.Text = "Стоп";
      this._btnStop.UseVisualStyleBackColor = true;
      this._btnStop.Click += new System.EventHandler(this.OnStopOperation);
      // 
      // _btnClose
      // 
      this._btnClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
      this._btnClose.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnClose.Location = new System.Drawing.Point(867, 3);
      this._btnClose.Name = "_btnClose";
      this._btnClose.Size = new System.Drawing.Size(88, 24);
      this._btnClose.TabIndex = 1;
      this._btnClose.Text = "Закрыть";
      this._btnClose.UseVisualStyleBackColor = true;
      // 
      // VelumMaterialBatchForm
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.CancelButton = this._btnClose;
      this.ClientSize = new System.Drawing.Size(984, 581);
      this.Controls.Add(this._rootLayout);
      this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
      this.MinimumSize = new System.Drawing.Size(1000, 620);
      this.Name = "VelumMaterialBatchForm";
      this.ShowInTaskbar = false;
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
      this.Text = "Пакетное присвоение материалов";
      this._rootLayout.ResumeLayout(false);
      this._rootLayout.PerformLayout();
      this._partsFolderCaptionRow.ResumeLayout(false);
      this._partsFolderCaptionHost.ResumeLayout(false);
      this._partsFolderCaptionHost.PerformLayout();
      this._partsFolderRow.ResumeLayout(false);
      this._partsFolderRow.PerformLayout();
      this._splitContainer.Panel1.ResumeLayout(false);
      this._splitContainer.Panel2.ResumeLayout(false);
      ((System.ComponentModel.ISupportInitialize)(this._splitContainer)).EndInit();
      this._splitContainer.ResumeLayout(false);
      this._materialPanel.ResumeLayout(false);
      this._materialPanel.PerformLayout();
      this._materialSearchRow.ResumeLayout(false);
      this._materialSearchRow.PerformLayout();
      this._partsPanel.ResumeLayout(false);
      this._partsPanel.PerformLayout();
      this._filtersRow.ResumeLayout(false);
      this._filtersRow.PerformLayout();
      this._actionsRow.ResumeLayout(false);
      this.ResumeLayout(false);

    }

    #endregion

    private System.Windows.Forms.TableLayoutPanel _rootLayout;
    private System.Windows.Forms.TableLayoutPanel _partsFolderCaptionRow;
    private System.Windows.Forms.Panel _partsFolderCaptionHost;
    private System.Windows.Forms.Label _lblPartsFolder;
    private System.Windows.Forms.TableLayoutPanel _partsFolderRow;
    private System.Windows.Forms.TextBox _partsFolderBox;
    private System.Windows.Forms.Button _btnBrowseParts;
    private System.Windows.Forms.Button _btnLoadParts;
    private System.Windows.Forms.SplitContainer _splitContainer;
    private System.Windows.Forms.TableLayoutPanel _materialPanel;
    private System.Windows.Forms.Label _lblMaterialTree;
    private System.Windows.Forms.TableLayoutPanel _materialSearchRow;
    private System.Windows.Forms.TextBox _materialFilterBox;
    private System.Windows.Forms.Button _btnMaterialSearch;
    private System.Windows.Forms.Button _btnMaterialPrev;
    private System.Windows.Forms.Button _btnMaterialNext;
    private System.Windows.Forms.Label _materialSearchStatusLabel;
    private System.Windows.Forms.TreeView _materialTreeViewControl;
    private System.Windows.Forms.TableLayoutPanel _partsPanel;
    private System.Windows.Forms.TableLayoutPanel _filtersRow;
    private System.Windows.Forms.Label _lblPartFilter;
    private System.Windows.Forms.TextBox _partFilterBox;
    private System.Windows.Forms.Label _lblConfigFilter;
    private System.Windows.Forms.TextBox _configFilterBox;
    private System.Windows.Forms.Label _lblMaterialListFilter;
    private System.Windows.Forms.TextBox _materialListFilterBox;
    private System.Windows.Forms.Button _btnFilterApply;
    private System.Windows.Forms.Button _btnFilterReset;
    private System.Windows.Forms.Button _btnFilterHelp;
    private System.Windows.Forms.Label _statusLabel;
    private System.Windows.Forms.ListView _listView;
    private System.Windows.Forms.ColumnHeader _colPart;
    private System.Windows.Forms.ColumnHeader _colConfig;
    private System.Windows.Forms.ColumnHeader _colMaterial;
    private System.Windows.Forms.Label _lblProgress;
    private System.Windows.Forms.ProgressBar _progressBar;
    private System.Windows.Forms.TableLayoutPanel _actionsRow;
    private System.Windows.Forms.Button _btnApply;
    private System.Windows.Forms.Button _btnStop;
    private System.Windows.Forms.Button _btnClose;
  }
}
