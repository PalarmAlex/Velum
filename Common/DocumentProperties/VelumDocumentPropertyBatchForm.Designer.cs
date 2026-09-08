namespace Velum.UI
{
  partial class VelumDocumentPropertyBatchForm
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
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VelumDocumentPropertyBatchForm));
      this._rootLayout = new System.Windows.Forms.TableLayoutPanel();
      this._catalogCaptionLabel = new System.Windows.Forms.Label();
      this._catalogRow = new System.Windows.Forms.TableLayoutPanel();
      this._catalogFolderBox = new System.Windows.Forms.TextBox();
      this._btnBrowseCatalog = new System.Windows.Forms.Button();
      this._btnLoadCatalog = new System.Windows.Forms.Button();
      this._tabs = new System.Windows.Forms.TabControl();
      this._tabParts = new System.Windows.Forms.TabPage();
      this._partsSplit = new System.Windows.Forms.SplitContainer();
      this._partsLeft = new System.Windows.Forms.TableLayoutPanel();
      this._lblPartsProps = new System.Windows.Forms.Label();
      this._partsGrid = new System.Windows.Forms.DataGridView();
      this.colPartPropName = new System.Windows.Forms.DataGridViewTextBoxColumn();
      this.colPartPropValue = new System.Windows.Forms.DataGridViewTextBoxColumn();
      this._partTemplateRow = new System.Windows.Forms.TableLayoutPanel();
      this._lblPartTemplate = new System.Windows.Forms.Label();
      this._partTemplateBox = new System.Windows.Forms.TextBox();
      this._btnBrowsePartTemplate = new System.Windows.Forms.Button();
      this._partsRight = new System.Windows.Forms.TableLayoutPanel();
      this._partsFilterRow = new System.Windows.Forms.TableLayoutPanel();
      this._lblPartsDocs = new System.Windows.Forms.Label();
      this._partsFilterBox = new System.Windows.Forms.TextBox();
      this._btnPartsFilterReset = new System.Windows.Forms.Button();
      this._btnPartsFilterHelp = new System.Windows.Forms.Button();
      this._partsList = new System.Windows.Forms.ListView();
      this._colPartFile = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
      this._colPartConfig = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
      this._partsStatusLabel = new System.Windows.Forms.Label();
      this._tabAssemblies = new System.Windows.Forms.TabPage();
      this._asmSplit = new System.Windows.Forms.SplitContainer();
      this._asmLeft = new System.Windows.Forms.TableLayoutPanel();
      this._lblAsmProps = new System.Windows.Forms.Label();
      this._asmGrid = new System.Windows.Forms.DataGridView();
      this.colAsmPropName = new System.Windows.Forms.DataGridViewTextBoxColumn();
      this.colAsmPropValue = new System.Windows.Forms.DataGridViewTextBoxColumn();
      this._asmTemplateRow = new System.Windows.Forms.TableLayoutPanel();
      this._lblAsmTemplate = new System.Windows.Forms.Label();
      this._asmTemplateBox = new System.Windows.Forms.TextBox();
      this._btnBrowseAsmTemplate = new System.Windows.Forms.Button();
      this._asmRight = new System.Windows.Forms.TableLayoutPanel();
      this._asmFilterRow = new System.Windows.Forms.TableLayoutPanel();
      this._lblAsmDocs = new System.Windows.Forms.Label();
      this._asmFilterBox = new System.Windows.Forms.TextBox();
      this._btnAsmFilterReset = new System.Windows.Forms.Button();
      this._btnAsmFilterHelp = new System.Windows.Forms.Button();
      this._asmList = new System.Windows.Forms.ListView();
      this._colAsmFile = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
      this._colAsmConfig = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
      this._asmStatusLabel = new System.Windows.Forms.Label();
      this._lblProgress = new System.Windows.Forms.Label();
      this._progressBar = new System.Windows.Forms.ProgressBar();
      this._actionsRow = new System.Windows.Forms.TableLayoutPanel();
      this._createMissingPanel = new System.Windows.Forms.FlowLayoutPanel();
      this._lblCreateMissing = new System.Windows.Forms.Label();
      this._rbCreateInSettings = new System.Windows.Forms.RadioButton();
      this._rbCreateInConfigs = new System.Windows.Forms.RadioButton();
      this._btnApply = new System.Windows.Forms.Button();
      this._btnStop = new System.Windows.Forms.Button();
      this._btnClose = new System.Windows.Forms.Button();
      this._rootLayout.SuspendLayout();
      this._catalogRow.SuspendLayout();
      this._tabs.SuspendLayout();
      this._tabParts.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)(this._partsSplit)).BeginInit();
      this._partsSplit.Panel1.SuspendLayout();
      this._partsSplit.Panel2.SuspendLayout();
      this._partsSplit.SuspendLayout();
      this._partsLeft.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)(this._partsGrid)).BeginInit();
      this._partTemplateRow.SuspendLayout();
      this._partsRight.SuspendLayout();
      this._partsFilterRow.SuspendLayout();
      this._tabAssemblies.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)(this._asmSplit)).BeginInit();
      this._asmSplit.Panel1.SuspendLayout();
      this._asmSplit.Panel2.SuspendLayout();
      this._asmSplit.SuspendLayout();
      this._asmLeft.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)(this._asmGrid)).BeginInit();
      this._asmTemplateRow.SuspendLayout();
      this._asmRight.SuspendLayout();
      this._asmFilterRow.SuspendLayout();
      this._actionsRow.SuspendLayout();
      this._createMissingPanel.SuspendLayout();
      this.SuspendLayout();
      // 
      // _rootLayout
      // 
      this._rootLayout.ColumnCount = 1;
      this._rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._rootLayout.Controls.Add(this._catalogCaptionLabel, 0, 0);
      this._rootLayout.Controls.Add(this._catalogRow, 0, 1);
      this._rootLayout.Controls.Add(this._tabs, 0, 2);
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
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 44F));
      this._rootLayout.Size = new System.Drawing.Size(984, 640);
      this._rootLayout.TabIndex = 0;
      // 
      // _catalogCaptionLabel
      // 
      this._catalogCaptionLabel.AutoSize = true;
      this._catalogCaptionLabel.Location = new System.Drawing.Point(10, 10);
      this._catalogCaptionLabel.Margin = new System.Windows.Forms.Padding(0, 0, 0, 4);
      this._catalogCaptionLabel.Name = "_catalogCaptionLabel";
      this._catalogCaptionLabel.Size = new System.Drawing.Size(93, 13);
      this._catalogCaptionLabel.TabIndex = 0;
      this._catalogCaptionLabel.Text = "Каталог изделия";
      // 
      // _catalogRow
      // 
      this._catalogRow.ColumnCount = 3;
      this._catalogRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._catalogRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._catalogRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._catalogRow.Controls.Add(this._catalogFolderBox, 0, 0);
      this._catalogRow.Controls.Add(this._btnBrowseCatalog, 1, 0);
      this._catalogRow.Controls.Add(this._btnLoadCatalog, 2, 0);
      this._catalogRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._catalogRow.Location = new System.Drawing.Point(10, 27);
      this._catalogRow.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
      this._catalogRow.Name = "_catalogRow";
      this._catalogRow.RowCount = 1;
      this._catalogRow.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._catalogRow.Size = new System.Drawing.Size(964, 28);
      this._catalogRow.TabIndex = 1;
      // 
      // _catalogFolderBox
      // 
      this._catalogFolderBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
      this._catalogFolderBox.Location = new System.Drawing.Point(0, 4);
      this._catalogFolderBox.Margin = new System.Windows.Forms.Padding(0, 0, 6, 0);
      this._catalogFolderBox.Name = "_catalogFolderBox";
      this._catalogFolderBox.Size = new System.Drawing.Size(766, 20);
      this._catalogFolderBox.TabIndex = 0;
      // 
      // _btnBrowseCatalog
      // 
      this._btnBrowseCatalog.AutoSize = true;
      this._btnBrowseCatalog.Location = new System.Drawing.Point(772, 0);
      this._btnBrowseCatalog.Margin = new System.Windows.Forms.Padding(0, 0, 6, 0);
      this._btnBrowseCatalog.Name = "_btnBrowseCatalog";
      this._btnBrowseCatalog.Size = new System.Drawing.Size(75, 28);
      this._btnBrowseCatalog.TabIndex = 1;
      this._btnBrowseCatalog.Text = "Обзор…";
      this._btnBrowseCatalog.UseVisualStyleBackColor = true;
      this._btnBrowseCatalog.Click += new System.EventHandler(this.OnBrowseCatalog);
      // 
      // _btnLoadCatalog
      // 
      this._btnLoadCatalog.AutoSize = true;
      this._btnLoadCatalog.Location = new System.Drawing.Point(853, 0);
      this._btnLoadCatalog.Margin = new System.Windows.Forms.Padding(0);
      this._btnLoadCatalog.Name = "_btnLoadCatalog";
      this._btnLoadCatalog.Size = new System.Drawing.Size(111, 28);
      this._btnLoadCatalog.TabIndex = 2;
      this._btnLoadCatalog.Text = "Загрузить";
      this._btnLoadCatalog.UseVisualStyleBackColor = true;
      this._btnLoadCatalog.Click += new System.EventHandler(this.OnLoadCatalog);
      // 
      // _tabs
      // 
      this._tabs.Controls.Add(this._tabParts);
      this._tabs.Controls.Add(this._tabAssemblies);
      this._tabs.Dock = System.Windows.Forms.DockStyle.Fill;
      this._tabs.Location = new System.Drawing.Point(13, 66);
      this._tabs.Name = "_tabs";
      this._tabs.SelectedIndex = 0;
      this._tabs.Size = new System.Drawing.Size(958, 472);
      this._tabs.TabIndex = 2;
      // 
      // _tabParts
      // 
      this._tabParts.Controls.Add(this._partsSplit);
      this._tabParts.Location = new System.Drawing.Point(4, 22);
      this._tabParts.Name = "_tabParts";
      this._tabParts.Padding = new System.Windows.Forms.Padding(6);
      this._tabParts.Size = new System.Drawing.Size(950, 446);
      this._tabParts.TabIndex = 0;
      this._tabParts.Text = "Детали";
      this._tabParts.UseVisualStyleBackColor = true;
      // 
      // _partsSplit
      // 
      this._partsSplit.Dock = System.Windows.Forms.DockStyle.Fill;
      this._partsSplit.Location = new System.Drawing.Point(6, 6);
      this._partsSplit.Name = "_partsSplit";
      // 
      // _partsSplit.Panel1
      // 
      this._partsSplit.Panel1.Controls.Add(this._partsLeft);
      // 
      // _partsSplit.Panel2
      // 
      this._partsSplit.Panel2.Controls.Add(this._partsRight);
      this._partsSplit.Size = new System.Drawing.Size(938, 434);
      this._partsSplit.SplitterDistance = 520;
      this._partsSplit.TabIndex = 0;
      // 
      // _partsLeft
      // 
      this._partsLeft.ColumnCount = 1;
      this._partsLeft.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._partsLeft.Controls.Add(this._lblPartsProps, 0, 0);
      this._partsLeft.Controls.Add(this._partsGrid, 0, 1);
      this._partsLeft.Controls.Add(this._partTemplateRow, 0, 2);
      this._partsLeft.Dock = System.Windows.Forms.DockStyle.Fill;
      this._partsLeft.Location = new System.Drawing.Point(0, 0);
      this._partsLeft.Name = "_partsLeft";
      this._partsLeft.RowCount = 3;
      this._partsLeft.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._partsLeft.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._partsLeft.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._partsLeft.Size = new System.Drawing.Size(520, 434);
      this._partsLeft.TabIndex = 0;
      // 
      // _lblPartsProps
      // 
      this._lblPartsProps.AutoSize = true;
      this._lblPartsProps.Location = new System.Drawing.Point(0, 0);
      this._lblPartsProps.Margin = new System.Windows.Forms.Padding(0, 0, 0, 4);
      this._lblPartsProps.Name = "_lblPartsProps";
      this._lblPartsProps.Size = new System.Drawing.Size(120, 13);
      this._lblPartsProps.TabIndex = 0;
      this._lblPartsProps.Text = "Свойства для деталей";
      // 
      // _partsGrid
      // 
      this._partsGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
      this._partsGrid.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colPartPropName,
            this.colPartPropValue});
      this._partsGrid.Dock = System.Windows.Forms.DockStyle.Fill;
      this._partsGrid.Location = new System.Drawing.Point(0, 17);
      this._partsGrid.Margin = new System.Windows.Forms.Padding(0, 0, 0, 6);
      this._partsGrid.Name = "_partsGrid";
      this._partsGrid.RowHeadersVisible = false;
      this._partsGrid.Size = new System.Drawing.Size(520, 377);
      this._partsGrid.TabIndex = 1;
      // 
      // colPartPropName
      // 
      this.colPartPropName.HeaderText = "Свойство";
      this.colPartPropName.Name = "colPartPropName";
      this.colPartPropName.Width = 180;
      // 
      // colPartPropValue
      // 
      this.colPartPropValue.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
      this.colPartPropValue.HeaderText = "Значение";
      this.colPartPropValue.Name = "colPartPropValue";
      // 
      // _partTemplateRow
      // 
      this._partTemplateRow.ColumnCount = 3;
      this._partTemplateRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._partTemplateRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._partTemplateRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._partTemplateRow.Controls.Add(this._lblPartTemplate, 0, 0);
      this._partTemplateRow.Controls.Add(this._partTemplateBox, 1, 0);
      this._partTemplateRow.Controls.Add(this._btnBrowsePartTemplate, 2, 0);
      this._partTemplateRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._partTemplateRow.Location = new System.Drawing.Point(0, 400);
      this._partTemplateRow.Margin = new System.Windows.Forms.Padding(0);
      this._partTemplateRow.Name = "_partTemplateRow";
      this._partTemplateRow.RowCount = 1;
      this._partTemplateRow.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._partTemplateRow.Size = new System.Drawing.Size(520, 34);
      this._partTemplateRow.TabIndex = 2;
      // 
      // _lblPartTemplate
      // 
      this._lblPartTemplate.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._lblPartTemplate.AutoSize = true;
      this._lblPartTemplate.Location = new System.Drawing.Point(0, 10);
      this._lblPartTemplate.Margin = new System.Windows.Forms.Padding(0, 0, 6, 0);
      this._lblPartTemplate.Name = "_lblPartTemplate";
      this._lblPartTemplate.Size = new System.Drawing.Size(90, 13);
      this._lblPartTemplate.TabIndex = 0;
      this._lblPartTemplate.Text = "Шаблон деталей";
      // 
      // _partTemplateBox
      // 
      this._partTemplateBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
      this._partTemplateBox.Location = new System.Drawing.Point(96, 7);
      this._partTemplateBox.Margin = new System.Windows.Forms.Padding(0, 0, 6, 0);
      this._partTemplateBox.Name = "_partTemplateBox";
      this._partTemplateBox.ReadOnly = true;
      this._partTemplateBox.Size = new System.Drawing.Size(343, 20);
      this._partTemplateBox.TabIndex = 1;
      // 
      // _btnBrowsePartTemplate
      // 
      this._btnBrowsePartTemplate.AutoSize = true;
      this._btnBrowsePartTemplate.Location = new System.Drawing.Point(445, 0);
      this._btnBrowsePartTemplate.Margin = new System.Windows.Forms.Padding(0);
      this._btnBrowsePartTemplate.Name = "_btnBrowsePartTemplate";
      this._btnBrowsePartTemplate.Size = new System.Drawing.Size(75, 28);
      this._btnBrowsePartTemplate.TabIndex = 2;
      this._btnBrowsePartTemplate.Text = "Обзор…";
      this._btnBrowsePartTemplate.UseVisualStyleBackColor = true;
      this._btnBrowsePartTemplate.Click += new System.EventHandler(this.OnBrowsePartTemplate);
      // 
      // _partsRight
      // 
      this._partsRight.ColumnCount = 1;
      this._partsRight.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._partsRight.Controls.Add(this._partsFilterRow, 0, 0);
      this._partsRight.Controls.Add(this._partsList, 0, 1);
      this._partsRight.Controls.Add(this._partsStatusLabel, 0, 2);
      this._partsRight.Dock = System.Windows.Forms.DockStyle.Fill;
      this._partsRight.Location = new System.Drawing.Point(0, 0);
      this._partsRight.Name = "_partsRight";
      this._partsRight.Padding = new System.Windows.Forms.Padding(6, 0, 0, 0);
      this._partsRight.RowCount = 3;
      this._partsRight.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._partsRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._partsRight.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._partsRight.Size = new System.Drawing.Size(414, 434);
      this._partsRight.TabIndex = 0;
      // 
      // _partsFilterRow
      // 
      this._partsFilterRow.ColumnCount = 4;
      this._partsFilterRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._partsFilterRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._partsFilterRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 64F));
      this._partsFilterRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 28F));
      this._partsFilterRow.Controls.Add(this._lblPartsDocs, 0, 0);
      this._partsFilterRow.Controls.Add(this._partsFilterBox, 1, 0);
      this._partsFilterRow.Controls.Add(this._btnPartsFilterReset, 2, 0);
      this._partsFilterRow.Controls.Add(this._btnPartsFilterHelp, 3, 0);
      this._partsFilterRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._partsFilterRow.Location = new System.Drawing.Point(6, 0);
      this._partsFilterRow.Margin = new System.Windows.Forms.Padding(0, 0, 0, 4);
      this._partsFilterRow.Name = "_partsFilterRow";
      this._partsFilterRow.RowCount = 1;
      this._partsFilterRow.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._partsFilterRow.Size = new System.Drawing.Size(408, 28);
      this._partsFilterRow.TabIndex = 0;
      // 
      // _lblPartsDocs
      // 
      this._lblPartsDocs.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._lblPartsDocs.AutoSize = true;
      this._lblPartsDocs.Location = new System.Drawing.Point(0, 7);
      this._lblPartsDocs.Margin = new System.Windows.Forms.Padding(0, 0, 6, 0);
      this._lblPartsDocs.Name = "_lblPartsDocs";
      this._lblPartsDocs.Size = new System.Drawing.Size(48, 13);
      this._lblPartsDocs.TabIndex = 0;
      this._lblPartsDocs.Text = "Детали:";
      // 
      // _partsFilterBox
      // 
      this._partsFilterBox.Dock = System.Windows.Forms.DockStyle.Fill;
      this._partsFilterBox.Location = new System.Drawing.Point(54, 4);
      this._partsFilterBox.Margin = new System.Windows.Forms.Padding(0, 4, 4, 0);
      this._partsFilterBox.Name = "_partsFilterBox";
      this._partsFilterBox.Size = new System.Drawing.Size(258, 20);
      this._partsFilterBox.TabIndex = 1;
      // 
      // _btnPartsFilterReset
      // 
      this._btnPartsFilterReset.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnPartsFilterReset.Location = new System.Drawing.Point(316, 3);
      this._btnPartsFilterReset.Margin = new System.Windows.Forms.Padding(0, 3, 4, 0);
      this._btnPartsFilterReset.Name = "_btnPartsFilterReset";
      this._btnPartsFilterReset.Size = new System.Drawing.Size(60, 25);
      this._btnPartsFilterReset.TabIndex = 2;
      this._btnPartsFilterReset.Text = "Сброс";
      this._btnPartsFilterReset.UseVisualStyleBackColor = true;
      // 
      // _btnPartsFilterHelp
      // 
      this._btnPartsFilterHelp.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnPartsFilterHelp.Location = new System.Drawing.Point(380, 3);
      this._btnPartsFilterHelp.Margin = new System.Windows.Forms.Padding(0, 3, 0, 0);
      this._btnPartsFilterHelp.Name = "_btnPartsFilterHelp";
      this._btnPartsFilterHelp.Size = new System.Drawing.Size(28, 25);
      this._btnPartsFilterHelp.TabIndex = 3;
      this._btnPartsFilterHelp.Text = "?";
      this._btnPartsFilterHelp.UseVisualStyleBackColor = true;
      // 
      // _partsList
      // 
      this._partsList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this._colPartFile,
            this._colPartConfig});
      this._partsList.Dock = System.Windows.Forms.DockStyle.Fill;
      this._partsList.FullRowSelect = true;
      this._partsList.HideSelection = false;
      this._partsList.Location = new System.Drawing.Point(6, 32);
      this._partsList.Margin = new System.Windows.Forms.Padding(0, 0, 0, 4);
      this._partsList.Name = "_partsList";
      this._partsList.Size = new System.Drawing.Size(408, 385);
      this._partsList.TabIndex = 1;
      this._partsList.UseCompatibleStateImageBehavior = false;
      this._partsList.View = System.Windows.Forms.View.Details;
      // 
      // _colPartFile
      // 
      this._colPartFile.Text = "Документ";
      this._colPartFile.Width = 220;
      // 
      // _colPartConfig
      // 
      this._colPartConfig.Text = "Конфигурация";
      this._colPartConfig.Width = 160;
      // 
      // _partsStatusLabel
      // 
      this._partsStatusLabel.AutoSize = true;
      this._partsStatusLabel.Location = new System.Drawing.Point(6, 421);
      this._partsStatusLabel.Margin = new System.Windows.Forms.Padding(0);
      this._partsStatusLabel.Name = "_partsStatusLabel";
      this._partsStatusLabel.Size = new System.Drawing.Size(49, 13);
      this._partsStatusLabel.TabIndex = 2;
      this._partsStatusLabel.Text = "Строк: 0";
      // 
      // _tabAssemblies
      // 
      this._tabAssemblies.Controls.Add(this._asmSplit);
      this._tabAssemblies.Location = new System.Drawing.Point(4, 22);
      this._tabAssemblies.Name = "_tabAssemblies";
      this._tabAssemblies.Padding = new System.Windows.Forms.Padding(6);
      this._tabAssemblies.Size = new System.Drawing.Size(950, 446);
      this._tabAssemblies.TabIndex = 1;
      this._tabAssemblies.Text = "Сборки";
      this._tabAssemblies.UseVisualStyleBackColor = true;
      // 
      // _asmSplit
      // 
      this._asmSplit.Dock = System.Windows.Forms.DockStyle.Fill;
      this._asmSplit.Location = new System.Drawing.Point(6, 6);
      this._asmSplit.Name = "_asmSplit";
      // 
      // _asmSplit.Panel1
      // 
      this._asmSplit.Panel1.Controls.Add(this._asmLeft);
      // 
      // _asmSplit.Panel2
      // 
      this._asmSplit.Panel2.Controls.Add(this._asmRight);
      this._asmSplit.Size = new System.Drawing.Size(938, 434);
      this._asmSplit.SplitterDistance = 520;
      this._asmSplit.TabIndex = 0;
      // 
      // _asmLeft
      // 
      this._asmLeft.ColumnCount = 1;
      this._asmLeft.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._asmLeft.Controls.Add(this._lblAsmProps, 0, 0);
      this._asmLeft.Controls.Add(this._asmGrid, 0, 1);
      this._asmLeft.Controls.Add(this._asmTemplateRow, 0, 2);
      this._asmLeft.Dock = System.Windows.Forms.DockStyle.Fill;
      this._asmLeft.Location = new System.Drawing.Point(0, 0);
      this._asmLeft.Name = "_asmLeft";
      this._asmLeft.RowCount = 3;
      this._asmLeft.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._asmLeft.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._asmLeft.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._asmLeft.Size = new System.Drawing.Size(520, 434);
      this._asmLeft.TabIndex = 0;
      // 
      // _lblAsmProps
      // 
      this._lblAsmProps.AutoSize = true;
      this._lblAsmProps.Location = new System.Drawing.Point(0, 0);
      this._lblAsmProps.Margin = new System.Windows.Forms.Padding(0, 0, 0, 4);
      this._lblAsmProps.Name = "_lblAsmProps";
      this._lblAsmProps.Size = new System.Drawing.Size(115, 13);
      this._lblAsmProps.TabIndex = 0;
      this._lblAsmProps.Text = "Свойства для сборок";
      // 
      // _asmGrid
      // 
      this._asmGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
      this._asmGrid.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colAsmPropName,
            this.colAsmPropValue});
      this._asmGrid.Dock = System.Windows.Forms.DockStyle.Fill;
      this._asmGrid.Location = new System.Drawing.Point(0, 17);
      this._asmGrid.Margin = new System.Windows.Forms.Padding(0, 0, 0, 6);
      this._asmGrid.Name = "_asmGrid";
      this._asmGrid.RowHeadersVisible = false;
      this._asmGrid.Size = new System.Drawing.Size(520, 377);
      this._asmGrid.TabIndex = 1;
      // 
      // colAsmPropName
      // 
      this.colAsmPropName.HeaderText = "Свойство";
      this.colAsmPropName.Name = "colAsmPropName";
      this.colAsmPropName.Width = 180;
      // 
      // colAsmPropValue
      // 
      this.colAsmPropValue.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
      this.colAsmPropValue.HeaderText = "Значение";
      this.colAsmPropValue.Name = "colAsmPropValue";
      // 
      // _asmTemplateRow
      // 
      this._asmTemplateRow.ColumnCount = 3;
      this._asmTemplateRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._asmTemplateRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._asmTemplateRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._asmTemplateRow.Controls.Add(this._lblAsmTemplate, 0, 0);
      this._asmTemplateRow.Controls.Add(this._asmTemplateBox, 1, 0);
      this._asmTemplateRow.Controls.Add(this._btnBrowseAsmTemplate, 2, 0);
      this._asmTemplateRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._asmTemplateRow.Location = new System.Drawing.Point(0, 400);
      this._asmTemplateRow.Margin = new System.Windows.Forms.Padding(0);
      this._asmTemplateRow.Name = "_asmTemplateRow";
      this._asmTemplateRow.RowCount = 1;
      this._asmTemplateRow.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._asmTemplateRow.Size = new System.Drawing.Size(520, 34);
      this._asmTemplateRow.TabIndex = 2;
      // 
      // _lblAsmTemplate
      // 
      this._lblAsmTemplate.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._lblAsmTemplate.AutoSize = true;
      this._lblAsmTemplate.Location = new System.Drawing.Point(0, 10);
      this._lblAsmTemplate.Margin = new System.Windows.Forms.Padding(0, 0, 6, 0);
      this._lblAsmTemplate.Name = "_lblAsmTemplate";
      this._lblAsmTemplate.Size = new System.Drawing.Size(85, 13);
      this._lblAsmTemplate.TabIndex = 0;
      this._lblAsmTemplate.Text = "Шаблон сборок";
      // 
      // _asmTemplateBox
      // 
      this._asmTemplateBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
      this._asmTemplateBox.Location = new System.Drawing.Point(91, 7);
      this._asmTemplateBox.Margin = new System.Windows.Forms.Padding(0, 0, 6, 0);
      this._asmTemplateBox.Name = "_asmTemplateBox";
      this._asmTemplateBox.ReadOnly = true;
      this._asmTemplateBox.Size = new System.Drawing.Size(348, 20);
      this._asmTemplateBox.TabIndex = 1;
      // 
      // _btnBrowseAsmTemplate
      // 
      this._btnBrowseAsmTemplate.AutoSize = true;
      this._btnBrowseAsmTemplate.Location = new System.Drawing.Point(445, 0);
      this._btnBrowseAsmTemplate.Margin = new System.Windows.Forms.Padding(0);
      this._btnBrowseAsmTemplate.Name = "_btnBrowseAsmTemplate";
      this._btnBrowseAsmTemplate.Size = new System.Drawing.Size(75, 28);
      this._btnBrowseAsmTemplate.TabIndex = 2;
      this._btnBrowseAsmTemplate.Text = "Обзор…";
      this._btnBrowseAsmTemplate.UseVisualStyleBackColor = true;
      this._btnBrowseAsmTemplate.Click += new System.EventHandler(this.OnBrowseAsmTemplate);
      // 
      // _asmRight
      // 
      this._asmRight.ColumnCount = 1;
      this._asmRight.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._asmRight.Controls.Add(this._asmFilterRow, 0, 0);
      this._asmRight.Controls.Add(this._asmList, 0, 1);
      this._asmRight.Controls.Add(this._asmStatusLabel, 0, 2);
      this._asmRight.Dock = System.Windows.Forms.DockStyle.Fill;
      this._asmRight.Location = new System.Drawing.Point(0, 0);
      this._asmRight.Name = "_asmRight";
      this._asmRight.Padding = new System.Windows.Forms.Padding(6, 0, 0, 0);
      this._asmRight.RowCount = 3;
      this._asmRight.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._asmRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._asmRight.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._asmRight.Size = new System.Drawing.Size(414, 434);
      this._asmRight.TabIndex = 0;
      // 
      // _asmFilterRow
      // 
      this._asmFilterRow.ColumnCount = 4;
      this._asmFilterRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._asmFilterRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._asmFilterRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 64F));
      this._asmFilterRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 28F));
      this._asmFilterRow.Controls.Add(this._lblAsmDocs, 0, 0);
      this._asmFilterRow.Controls.Add(this._asmFilterBox, 1, 0);
      this._asmFilterRow.Controls.Add(this._btnAsmFilterReset, 2, 0);
      this._asmFilterRow.Controls.Add(this._btnAsmFilterHelp, 3, 0);
      this._asmFilterRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._asmFilterRow.Location = new System.Drawing.Point(6, 0);
      this._asmFilterRow.Margin = new System.Windows.Forms.Padding(0, 0, 0, 4);
      this._asmFilterRow.Name = "_asmFilterRow";
      this._asmFilterRow.RowCount = 1;
      this._asmFilterRow.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._asmFilterRow.Size = new System.Drawing.Size(408, 28);
      this._asmFilterRow.TabIndex = 0;
      // 
      // _lblAsmDocs
      // 
      this._lblAsmDocs.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._lblAsmDocs.AutoSize = true;
      this._lblAsmDocs.Location = new System.Drawing.Point(0, 7);
      this._lblAsmDocs.Margin = new System.Windows.Forms.Padding(0, 0, 6, 0);
      this._lblAsmDocs.Name = "_lblAsmDocs";
      this._lblAsmDocs.Size = new System.Drawing.Size(47, 13);
      this._lblAsmDocs.TabIndex = 0;
      this._lblAsmDocs.Text = "Сборки:";
      // 
      // _asmFilterBox
      // 
      this._asmFilterBox.Dock = System.Windows.Forms.DockStyle.Fill;
      this._asmFilterBox.Location = new System.Drawing.Point(53, 4);
      this._asmFilterBox.Margin = new System.Windows.Forms.Padding(0, 4, 4, 0);
      this._asmFilterBox.Name = "_asmFilterBox";
      this._asmFilterBox.Size = new System.Drawing.Size(259, 20);
      this._asmFilterBox.TabIndex = 1;
      // 
      // _btnAsmFilterReset
      // 
      this._btnAsmFilterReset.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnAsmFilterReset.Location = new System.Drawing.Point(316, 3);
      this._btnAsmFilterReset.Margin = new System.Windows.Forms.Padding(0, 3, 4, 0);
      this._btnAsmFilterReset.Name = "_btnAsmFilterReset";
      this._btnAsmFilterReset.Size = new System.Drawing.Size(60, 25);
      this._btnAsmFilterReset.TabIndex = 2;
      this._btnAsmFilterReset.Text = "Сброс";
      this._btnAsmFilterReset.UseVisualStyleBackColor = true;
      // 
      // _btnAsmFilterHelp
      // 
      this._btnAsmFilterHelp.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnAsmFilterHelp.Location = new System.Drawing.Point(380, 3);
      this._btnAsmFilterHelp.Margin = new System.Windows.Forms.Padding(0, 3, 0, 0);
      this._btnAsmFilterHelp.Name = "_btnAsmFilterHelp";
      this._btnAsmFilterHelp.Size = new System.Drawing.Size(28, 25);
      this._btnAsmFilterHelp.TabIndex = 3;
      this._btnAsmFilterHelp.Text = "?";
      this._btnAsmFilterHelp.UseVisualStyleBackColor = true;
      // 
      // _asmList
      // 
      this._asmList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this._colAsmFile,
            this._colAsmConfig});
      this._asmList.Dock = System.Windows.Forms.DockStyle.Fill;
      this._asmList.FullRowSelect = true;
      this._asmList.HideSelection = false;
      this._asmList.Location = new System.Drawing.Point(6, 32);
      this._asmList.Margin = new System.Windows.Forms.Padding(0, 0, 0, 4);
      this._asmList.Name = "_asmList";
      this._asmList.Size = new System.Drawing.Size(408, 385);
      this._asmList.TabIndex = 1;
      this._asmList.UseCompatibleStateImageBehavior = false;
      this._asmList.View = System.Windows.Forms.View.Details;
      // 
      // _colAsmFile
      // 
      this._colAsmFile.Text = "Документ";
      this._colAsmFile.Width = 220;
      // 
      // _colAsmConfig
      // 
      this._colAsmConfig.Text = "Конфигурация";
      this._colAsmConfig.Width = 160;
      // 
      // _asmStatusLabel
      // 
      this._asmStatusLabel.AutoSize = true;
      this._asmStatusLabel.Location = new System.Drawing.Point(6, 421);
      this._asmStatusLabel.Margin = new System.Windows.Forms.Padding(0);
      this._asmStatusLabel.Name = "_asmStatusLabel";
      this._asmStatusLabel.Size = new System.Drawing.Size(49, 13);
      this._asmStatusLabel.TabIndex = 2;
      this._asmStatusLabel.Text = "Строк: 0";
      // 
      // _lblProgress
      // 
      this._lblProgress.AutoSize = true;
      this._lblProgress.Location = new System.Drawing.Point(10, 547);
      this._lblProgress.Margin = new System.Windows.Forms.Padding(0, 6, 0, 2);
      this._lblProgress.Name = "_lblProgress";
      this._lblProgress.Size = new System.Drawing.Size(0, 13);
      this._lblProgress.TabIndex = 3;
      this._lblProgress.Visible = false;
      // 
      // _progressBar
      // 
      this._progressBar.Dock = System.Windows.Forms.DockStyle.Fill;
      this._progressBar.Location = new System.Drawing.Point(10, 562);
      this._progressBar.Margin = new System.Windows.Forms.Padding(0, 0, 0, 6);
      this._progressBar.Name = "_progressBar";
      this._progressBar.Size = new System.Drawing.Size(964, 18);
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
      this._actionsRow.Controls.Add(this._createMissingPanel, 0, 0);
      this._actionsRow.Controls.Add(this._btnApply, 1, 0);
      this._actionsRow.Controls.Add(this._btnStop, 2, 0);
      this._actionsRow.Controls.Add(this._btnClose, 3, 0);
      this._actionsRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._actionsRow.Location = new System.Drawing.Point(10, 586);
      this._actionsRow.Margin = new System.Windows.Forms.Padding(0);
      this._actionsRow.Name = "_actionsRow";
      this._actionsRow.RowCount = 1;
      this._actionsRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._actionsRow.Size = new System.Drawing.Size(964, 44);
      this._actionsRow.TabIndex = 5;
      // 
      // _createMissingPanel
      // 
      this._createMissingPanel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
      this._createMissingPanel.AutoSize = true;
      this._createMissingPanel.Controls.Add(this._lblCreateMissing);
      this._createMissingPanel.Controls.Add(this._rbCreateInSettings);
      this._createMissingPanel.Controls.Add(this._rbCreateInConfigs);
      this._createMissingPanel.Location = new System.Drawing.Point(0, 13);
      this._createMissingPanel.Margin = new System.Windows.Forms.Padding(0, 0, 8, 0);
      this._createMissingPanel.Name = "_createMissingPanel";
      this._createMissingPanel.Size = new System.Drawing.Size(696, 17);
      this._createMissingPanel.TabIndex = 0;
      this._createMissingPanel.WrapContents = false;
      // 
      // _lblCreateMissing
      // 
      this._lblCreateMissing.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._lblCreateMissing.AutoSize = true;
      this._lblCreateMissing.Location = new System.Drawing.Point(0, 3);
      this._lblCreateMissing.Margin = new System.Windows.Forms.Padding(0, 3, 6, 0);
      this._lblCreateMissing.Name = "_lblCreateMissing";
      this._lblCreateMissing.Size = new System.Drawing.Size(195, 13);
      this._lblCreateMissing.TabIndex = 0;
      this._lblCreateMissing.Text = "При отсутствии свойства создавать:";
      // 
      // _rbCreateInSettings
      // 
      this._rbCreateInSettings.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._rbCreateInSettings.AutoSize = true;
      this._rbCreateInSettings.Checked = true;
      this._rbCreateInSettings.Location = new System.Drawing.Point(201, 0);
      this._rbCreateInSettings.Margin = new System.Windows.Forms.Padding(0, 0, 8, 0);
      this._rbCreateInSettings.Name = "_rbCreateInSettings";
      this._rbCreateInSettings.Size = new System.Drawing.Size(92, 17);
      this._rbCreateInSettings.TabIndex = 1;
      this._rbCreateInSettings.TabStop = true;
      this._rbCreateInSettings.Text = "в настройках";
      this._rbCreateInSettings.UseVisualStyleBackColor = true;
      // 
      // _rbCreateInConfigs
      // 
      this._rbCreateInConfigs.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._rbCreateInConfigs.AutoSize = true;
      this._rbCreateInConfigs.Location = new System.Drawing.Point(301, 0);
      this._rbCreateInConfigs.Margin = new System.Windows.Forms.Padding(0);
      this._rbCreateInConfigs.Name = "_rbCreateInConfigs";
      this._rbCreateInConfigs.Size = new System.Drawing.Size(111, 17);
      this._rbCreateInConfigs.TabIndex = 2;
      this._rbCreateInConfigs.Text = "в конфигурациях";
      this._rbCreateInConfigs.UseVisualStyleBackColor = true;
      // 
      // _btnApply
      // 
      this._btnApply.Anchor = System.Windows.Forms.AnchorStyles.Right;
      this._btnApply.AutoSize = true;
      this._btnApply.Location = new System.Drawing.Point(704, 8);
      this._btnApply.Margin = new System.Windows.Forms.Padding(0, 0, 6, 0);
      this._btnApply.Name = "_btnApply";
      this._btnApply.Size = new System.Drawing.Size(90, 28);
      this._btnApply.TabIndex = 1;
      this._btnApply.Text = "Применить";
      this._btnApply.UseVisualStyleBackColor = true;
      this._btnApply.Click += new System.EventHandler(this.OnApply);
      // 
      // _btnStop
      // 
      this._btnStop.Anchor = System.Windows.Forms.AnchorStyles.Right;
      this._btnStop.AutoSize = true;
      this._btnStop.Enabled = false;
      this._btnStop.Location = new System.Drawing.Point(800, 8);
      this._btnStop.Margin = new System.Windows.Forms.Padding(0, 0, 6, 0);
      this._btnStop.Name = "_btnStop";
      this._btnStop.Size = new System.Drawing.Size(75, 28);
      this._btnStop.TabIndex = 2;
      this._btnStop.Text = "Стоп";
      this._btnStop.UseVisualStyleBackColor = true;
      this._btnStop.Click += new System.EventHandler(this.OnStop);
      // 
      // _btnClose
      // 
      this._btnClose.Anchor = System.Windows.Forms.AnchorStyles.Right;
      this._btnClose.AutoSize = true;
      this._btnClose.Location = new System.Drawing.Point(881, 8);
      this._btnClose.Margin = new System.Windows.Forms.Padding(0);
      this._btnClose.Name = "_btnClose";
      this._btnClose.Size = new System.Drawing.Size(83, 28);
      this._btnClose.TabIndex = 3;
      this._btnClose.Text = "Закрыть";
      this._btnClose.UseVisualStyleBackColor = true;
      this._btnClose.Click += new System.EventHandler(this.OnCloseClick);
      // 
      // VelumDocumentPropertyBatchForm
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.ClientSize = new System.Drawing.Size(984, 640);
      this.Controls.Add(this._rootLayout);
      this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
      this.MinimizeBox = false;
      this.MinimumSize = new System.Drawing.Size(800, 520);
      this.Name = "VelumDocumentPropertyBatchForm";
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
      this.Text = "Обновление свойств документов";
      this._rootLayout.ResumeLayout(false);
      this._rootLayout.PerformLayout();
      this._catalogRow.ResumeLayout(false);
      this._catalogRow.PerformLayout();
      this._tabs.ResumeLayout(false);
      this._tabParts.ResumeLayout(false);
      this._partsSplit.Panel1.ResumeLayout(false);
      this._partsSplit.Panel2.ResumeLayout(false);
      ((System.ComponentModel.ISupportInitialize)(this._partsSplit)).EndInit();
      this._partsSplit.ResumeLayout(false);
      this._partsLeft.ResumeLayout(false);
      this._partsLeft.PerformLayout();
      ((System.ComponentModel.ISupportInitialize)(this._partsGrid)).EndInit();
      this._partTemplateRow.ResumeLayout(false);
      this._partTemplateRow.PerformLayout();
      this._partsRight.ResumeLayout(false);
      this._partsRight.PerformLayout();
      this._partsFilterRow.ResumeLayout(false);
      this._partsFilterRow.PerformLayout();
      this._tabAssemblies.ResumeLayout(false);
      this._asmSplit.Panel1.ResumeLayout(false);
      this._asmSplit.Panel2.ResumeLayout(false);
      ((System.ComponentModel.ISupportInitialize)(this._asmSplit)).EndInit();
      this._asmSplit.ResumeLayout(false);
      this._asmLeft.ResumeLayout(false);
      this._asmLeft.PerformLayout();
      ((System.ComponentModel.ISupportInitialize)(this._asmGrid)).EndInit();
      this._asmTemplateRow.ResumeLayout(false);
      this._asmTemplateRow.PerformLayout();
      this._asmRight.ResumeLayout(false);
      this._asmRight.PerformLayout();
      this._asmFilterRow.ResumeLayout(false);
      this._asmFilterRow.PerformLayout();
      this._actionsRow.ResumeLayout(false);
      this._actionsRow.PerformLayout();
      this._createMissingPanel.ResumeLayout(false);
      this._createMissingPanel.PerformLayout();
      this.ResumeLayout(false);

    }

    #endregion

    private System.Windows.Forms.TableLayoutPanel _rootLayout;
    private System.Windows.Forms.Label _catalogCaptionLabel;
    private System.Windows.Forms.TableLayoutPanel _catalogRow;
    private System.Windows.Forms.TextBox _catalogFolderBox;
    private System.Windows.Forms.Button _btnBrowseCatalog;
    private System.Windows.Forms.Button _btnLoadCatalog;
    private System.Windows.Forms.TabControl _tabs;
    private System.Windows.Forms.TabPage _tabParts;
    private System.Windows.Forms.SplitContainer _partsSplit;
    private System.Windows.Forms.TableLayoutPanel _partsLeft;
    private System.Windows.Forms.Label _lblPartsProps;
    private System.Windows.Forms.DataGridView _partsGrid;
    private System.Windows.Forms.DataGridViewTextBoxColumn colPartPropName;
    private System.Windows.Forms.DataGridViewTextBoxColumn colPartPropValue;
    private System.Windows.Forms.TableLayoutPanel _partTemplateRow;
    private System.Windows.Forms.Label _lblPartTemplate;
    private System.Windows.Forms.TextBox _partTemplateBox;
    private System.Windows.Forms.Button _btnBrowsePartTemplate;
    private System.Windows.Forms.TableLayoutPanel _partsRight;
    private System.Windows.Forms.TableLayoutPanel _partsFilterRow;
    private System.Windows.Forms.Label _lblPartsDocs;
    private System.Windows.Forms.TextBox _partsFilterBox;
    private System.Windows.Forms.Button _btnPartsFilterReset;
    private System.Windows.Forms.Button _btnPartsFilterHelp;
    private System.Windows.Forms.ListView _partsList;
    private System.Windows.Forms.ColumnHeader _colPartFile;
    private System.Windows.Forms.ColumnHeader _colPartConfig;
    private System.Windows.Forms.Label _partsStatusLabel;
    private System.Windows.Forms.TabPage _tabAssemblies;
    private System.Windows.Forms.SplitContainer _asmSplit;
    private System.Windows.Forms.TableLayoutPanel _asmLeft;
    private System.Windows.Forms.Label _lblAsmProps;
    private System.Windows.Forms.DataGridView _asmGrid;
    private System.Windows.Forms.DataGridViewTextBoxColumn colAsmPropName;
    private System.Windows.Forms.DataGridViewTextBoxColumn colAsmPropValue;
    private System.Windows.Forms.TableLayoutPanel _asmTemplateRow;
    private System.Windows.Forms.Label _lblAsmTemplate;
    private System.Windows.Forms.TextBox _asmTemplateBox;
    private System.Windows.Forms.Button _btnBrowseAsmTemplate;
    private System.Windows.Forms.TableLayoutPanel _asmRight;
    private System.Windows.Forms.TableLayoutPanel _asmFilterRow;
    private System.Windows.Forms.Label _lblAsmDocs;
    private System.Windows.Forms.TextBox _asmFilterBox;
    private System.Windows.Forms.Button _btnAsmFilterReset;
    private System.Windows.Forms.Button _btnAsmFilterHelp;
    private System.Windows.Forms.ListView _asmList;
    private System.Windows.Forms.ColumnHeader _colAsmFile;
    private System.Windows.Forms.ColumnHeader _colAsmConfig;
    private System.Windows.Forms.Label _asmStatusLabel;
    private System.Windows.Forms.Label _lblProgress;
    private System.Windows.Forms.ProgressBar _progressBar;
    private System.Windows.Forms.TableLayoutPanel _actionsRow;
    private System.Windows.Forms.FlowLayoutPanel _createMissingPanel;
    private System.Windows.Forms.Label _lblCreateMissing;
    private System.Windows.Forms.RadioButton _rbCreateInSettings;
    private System.Windows.Forms.RadioButton _rbCreateInConfigs;
    private System.Windows.Forms.Button _btnApply;
    private System.Windows.Forms.Button _btnStop;
    private System.Windows.Forms.Button _btnClose;
  }
}
