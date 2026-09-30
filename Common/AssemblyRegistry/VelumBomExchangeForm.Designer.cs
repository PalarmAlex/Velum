using System.Drawing;
using System.Windows.Forms;

namespace Velum.UI.AssemblyRegistry
{
  /// <summary>
  /// Форма диалога экспорта BOM-данных в 1C: карточки номенклатуры
  /// и структура состава (вкладки), поле каталога обмена и кнопка экспорта.
  /// </summary>
  internal sealed partial class VelumBomExchangeForm
  {
    private System.ComponentModel.IContainer components = null;

    private System.Windows.Forms.TextBox _folderBox;
    private System.Windows.Forms.Button _browseButton;
    private System.Windows.Forms.Button _exportButton;
    private System.Windows.Forms.Button _settingsButton;
    private System.Windows.Forms.Button _layoutButton;
    private System.Windows.Forms.TabControl _tabs;
    private System.Windows.Forms.TabPage _cardsTab;
    private System.Windows.Forms.TabPage _structureTab;
    private System.Windows.Forms.TabPage _allTab;
    private System.Windows.Forms.ListView _listView;
    private System.Windows.Forms.ColumnHeader _colTypeDocs;
    private System.Windows.Forms.ColumnHeader _colExternalId;
    private System.Windows.Forms.ColumnHeader _colDesignation;
    private System.Windows.Forms.ColumnHeader _colName;
    private System.Windows.Forms.ColumnHeader _colQuantity;
    private System.Windows.Forms.ListView _structureListView;
    private System.Windows.Forms.ColumnHeader _colStructParent;
    private System.Windows.Forms.ColumnHeader _colStructChild;
    private System.Windows.Forms.ColumnHeader _colStructConfig;
    private System.Windows.Forms.ColumnHeader _colStructQty;
    private System.Windows.Forms.ColumnHeader _colStructAction;
    private System.Windows.Forms.ColumnHeader _colStructExternalId;
    private System.Windows.Forms.ColumnHeader _colStructTimestamp;
    private System.Windows.Forms.ListView _allListView;
    private System.Windows.Forms.ColumnHeader _colAllParent;
    private System.Windows.Forms.ColumnHeader _colAllChild;
    private System.Windows.Forms.ColumnHeader _colAllConfig;
    private System.Windows.Forms.ColumnHeader _colAllQty;
    private System.Windows.Forms.ColumnHeader _colAllAction;
    private System.Windows.Forms.ColumnHeader _colAllExternalId;
    private System.Windows.Forms.ColumnHeader _colAllTimestamp;
    private System.Windows.Forms.ColumnHeader _colAllState;
    private System.Windows.Forms.TableLayoutPanel _filterRow;
    private System.Windows.Forms.Label _filterParentLabel;
    private System.Windows.Forms.TextBox _filterParentBox;
    private System.Windows.Forms.Label _filterChildLabel;
    private System.Windows.Forms.TextBox _filterChildBox;
    private System.Windows.Forms.Label _filterActionLabel;
    private System.Windows.Forms.TextBox _filterActionBox;
    private System.Windows.Forms.Label _filterExternalIdLabel;
    private System.Windows.Forms.TextBox _filterExternalIdBox;
    private System.Windows.Forms.Label _filterDateLabel;
    private System.Windows.Forms.TextBox _filterDateBox;
    private System.Windows.Forms.Button _filterApplyButton;
    private System.Windows.Forms.Button _filterResetButton;
    private System.Windows.Forms.CheckBox _registryFilterCheck;
    private System.Windows.Forms.ToolTip _toolTip;

    protected override void Dispose(bool disposing)
    {
      if (disposing && (components != null))
      {
        components.Dispose();
      }
      base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// Метод, требуемый для конструктора форм.
    /// Не изменяйте содержимое этого метода с помощью редактора кода.
    /// </summary>
    private void InitializeComponent()
    {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VelumBomExchangeForm));
            this._folderBox = new System.Windows.Forms.TextBox();
            this._browseButton = new System.Windows.Forms.Button();
            this._exportButton = new System.Windows.Forms.Button();
            this._settingsButton = new System.Windows.Forms.Button();
            this._layoutButton = new System.Windows.Forms.Button();
            this._registryFilterCheck = new System.Windows.Forms.CheckBox();
            this._toolTip = new System.Windows.Forms.ToolTip(this.components);
            this._listView = new System.Windows.Forms.ListView();
            this._colTypeDocs = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this._colExternalId = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this._colDesignation = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this._colName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this._colQuantity = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this._structureListView = new System.Windows.Forms.ListView();
            this._colStructParent = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this._colStructChild = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this._colStructConfig = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this._colStructQty = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this._colStructAction = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this._colStructExternalId = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this._colStructTimestamp = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this._allListView = new System.Windows.Forms.ListView();
            this._colAllParent = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this._colAllChild = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this._colAllConfig = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this._colAllQty = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this._colAllAction = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this._colAllExternalId = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this._colAllTimestamp = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this._colAllState = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this._filterParentBox = new System.Windows.Forms.TextBox();
            this._filterChildBox = new System.Windows.Forms.TextBox();
            this._filterActionBox = new System.Windows.Forms.TextBox();
            this._filterExternalIdBox = new System.Windows.Forms.TextBox();
            this._filterDateBox = new System.Windows.Forms.TextBox();
            this._filterApplyButton = new System.Windows.Forms.Button();
            this._filterResetButton = new System.Windows.Forms.Button();
            this._filterRow = new System.Windows.Forms.TableLayoutPanel();
            this._filterParentLabel = new System.Windows.Forms.Label();
            this._filterChildLabel = new System.Windows.Forms.Label();
            this._filterActionLabel = new System.Windows.Forms.Label();
            this._filterExternalIdLabel = new System.Windows.Forms.Label();
            this._filterDateLabel = new System.Windows.Forms.Label();
            this._tabs = new System.Windows.Forms.TabControl();
            this._cardsTab = new System.Windows.Forms.TabPage();
            this._structureTab = new System.Windows.Forms.TabPage();
            this._allTab = new System.Windows.Forms.TabPage();
            this.shell = new System.Windows.Forms.TableLayoutPanel();
            this.root = new System.Windows.Forms.TableLayoutPanel();
            this.titleLabel = new System.Windows.Forms.Label();
            this.descLabel = new System.Windows.Forms.Label();
            this.folderRow = new System.Windows.Forms.TableLayoutPanel();
            this.folderLabel = new System.Windows.Forms.Label();
            this.notePanel = new System.Windows.Forms.Panel();
            this.noteLabel = new System.Windows.Forms.Label();
            this.buttonsPanel = new System.Windows.Forms.Panel();
            this._buttonsRow = new System.Windows.Forms.FlowLayoutPanel();
            this.cancelButton = new System.Windows.Forms.Button();
            this._filterRow.SuspendLayout();
            this._tabs.SuspendLayout();
            this._cardsTab.SuspendLayout();
            this._structureTab.SuspendLayout();
            this._allTab.SuspendLayout();
            this.shell.SuspendLayout();
            this.root.SuspendLayout();
            this.folderRow.SuspendLayout();
            this.notePanel.SuspendLayout();
            this.buttonsPanel.SuspendLayout();
            this._buttonsRow.SuspendLayout();
            this.SuspendLayout();
            // 
            // _folderBox
            // 
            this._folderBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._folderBox.Location = new System.Drawing.Point(100, 6);
            this._folderBox.Margin = new System.Windows.Forms.Padding(0, 2, 4, 2);
            this._folderBox.Name = "_folderBox";
            this._folderBox.ReadOnly = true;
            this._folderBox.Size = new System.Drawing.Size(867, 20);
            this._folderBox.TabIndex = 1;
            this._toolTip.SetToolTip(this._folderBox, "Каталог, в который сохраняются CSV-файлы обмена с 1C.");
            // 
            // _browseButton
            // 
            this._browseButton.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._browseButton.AutoSize = true;
            this._browseButton.Location = new System.Drawing.Point(975, 4);
            this._browseButton.Margin = new System.Windows.Forms.Padding(4, 2, 0, 2);
            this._browseButton.Name = "_browseButton";
            this._browseButton.Size = new System.Drawing.Size(75, 23);
            this._browseButton.TabIndex = 2;
            this._browseButton.Text = "Обзор";
            this._toolTip.SetToolTip(this._browseButton, "Выбрать каталог обмена с 1C.");
            this._browseButton.UseVisualStyleBackColor = true;
            this._browseButton.Click += new System.EventHandler(this.OnBrowseClick);
            // 
            // _exportButton
            // 
            this._exportButton.AutoSize = true;
            this._exportButton.Location = new System.Drawing.Point(729, 3);
            this._exportButton.Name = "_exportButton";
            this._exportButton.Size = new System.Drawing.Size(75, 23);
            this._exportButton.TabIndex = 2;
            this._exportButton.Text = "Экспорт";
            this._toolTip.SetToolTip(this._exportButton, "Сформировать CSV-файлы обмена с 1C (карточки и структура) в выбранном каталоге.");
            this._exportButton.UseVisualStyleBackColor = true;
            this._exportButton.Click += new System.EventHandler(this.OnExportClick);
            // 
            // _settingsButton
            // 
            this._settingsButton.AutoSize = true;
            this._settingsButton.Location = new System.Drawing.Point(891, 3);
            this._settingsButton.Name = "_settingsButton";
            this._settingsButton.Size = new System.Drawing.Size(75, 23);
            this._settingsButton.TabIndex = 1;
            this._settingsButton.Text = "Настройки";
            this._toolTip.SetToolTip(this._settingsButton, "Открыть редактор отслеживаемых свойств BOM.");
            this._settingsButton.UseVisualStyleBackColor = true;
            this._settingsButton.Click += new System.EventHandler(this.OnSettingsClick);
            // 
            // _layoutButton
            // 
            this._layoutButton.AutoSize = true;
            this._layoutButton.Location = new System.Drawing.Point(810, 3);
            this._layoutButton.Name = "_layoutButton";
            this._layoutButton.Size = new System.Drawing.Size(75, 23);
            this._layoutButton.TabIndex = 3;
            this._layoutButton.Text = "Поля…";
            this._toolTip.SetToolTip(this._layoutButton, "Настроить состав, порядок и заголовки полей выгрузки карточек");
            this._layoutButton.UseVisualStyleBackColor = true;
            this._layoutButton.Click += new System.EventHandler(this.OnLayoutSettingsClick);
            // 
            // _registryFilterCheck
            // 
            this._registryFilterCheck.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._registryFilterCheck.AutoSize = true;
            this._registryFilterCheck.Location = new System.Drawing.Point(15, 90);
            this._registryFilterCheck.Margin = new System.Windows.Forms.Padding(3, 4, 3, 3);
            this._registryFilterCheck.Name = "_registryFilterCheck";
            this._registryFilterCheck.Size = new System.Drawing.Size(272, 17);
            this._registryFilterCheck.TabIndex = 4;
            this._registryFilterCheck.Text = "Только зарегистрированные в реестре изделий";
            this._toolTip.SetToolTip(this._registryFilterCheck, "Показывать и выгружать в 1C только позиции, файлы которых есть в реестре изделий." +
        "");
            this._registryFilterCheck.UseVisualStyleBackColor = true;
            this._registryFilterCheck.CheckedChanged += new System.EventHandler(this.OnRegistryFilterChanged);
            // 
            // _toolTip
            // 
            this._toolTip.AutoPopDelay = 12000;
            this._toolTip.InitialDelay = 400;
            this._toolTip.ReshowDelay = 200;
            this._toolTip.ShowAlways = true;
            // 
            // _listView
            // 
            this._listView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this._colTypeDocs,
            this._colExternalId,
            this._colDesignation,
            this._colName,
            this._colQuantity});
            this._listView.Dock = System.Windows.Forms.DockStyle.Fill;
            this._listView.FullRowSelect = true;
            this._listView.GridLines = true;
            this._listView.HideSelection = false;
            this._listView.Location = new System.Drawing.Point(3, 3);
            this._listView.Name = "_listView";
            this._listView.Size = new System.Drawing.Size(1030, 182);
            this._listView.TabIndex = 0;
            this._toolTip.SetToolTip(this._listView, "Карточки, которые попадут в 1C_update_*.csv. Состав колонок настраивается кнопкой" +
        " «Поля…».");
            this._listView.UseCompatibleStateImageBehavior = false;
            this._listView.View = System.Windows.Forms.View.Details;
            this._listView.ColumnClick += new System.Windows.Forms.ColumnClickEventHandler(this.OnCardsColumnClick);
            // 
            // _colTypeDocs
            // 
            this._colTypeDocs.Text = "TypeDocs";
            this._colTypeDocs.Width = 80;
            // 
            // _colExternalId
            // 
            this._colExternalId.Text = "ExternalId";
            this._colExternalId.Width = 100;
            // 
            // _colDesignation
            // 
            this._colDesignation.Text = "Designation";
            this._colDesignation.Width = 120;
            // 
            // _colName
            // 
            this._colName.Text = "Name";
            this._colName.Width = 150;
            // 
            // _colQuantity
            // 
            this._colQuantity.Text = "Quantity";
            this._colQuantity.Width = 70;
            // 
            // _structureListView
            // 
            this._structureListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this._colStructParent,
            this._colStructChild,
            this._colStructConfig,
            this._colStructQty,
            this._colStructAction,
            this._colStructExternalId,
            this._colStructTimestamp});
            this._structureListView.Dock = System.Windows.Forms.DockStyle.Fill;
            this._structureListView.FullRowSelect = true;
            this._structureListView.GridLines = true;
            this._structureListView.HideSelection = false;
            this._structureListView.Location = new System.Drawing.Point(3, 3);
            this._structureListView.Name = "_structureListView";
            this._structureListView.Size = new System.Drawing.Size(1030, 182);
            this._structureListView.TabIndex = 0;
            this._toolTip.SetToolTip(this._structureListView, "Строки состава, которые попадут в 1C_bom_*.csv (операции add/update/delete).");
            this._structureListView.UseCompatibleStateImageBehavior = false;
            this._structureListView.View = System.Windows.Forms.View.Details;
            this._structureListView.ColumnClick += new System.Windows.Forms.ColumnClickEventHandler(this.OnStructureColumnClick);
            // 
            // _colStructParent
            // 
            this._colStructParent.Text = "Родитель";
            this._colStructParent.Width = 140;
            // 
            // _colStructChild
            // 
            this._colStructChild.Text = "Компонент";
            this._colStructChild.Width = 140;
            // 
            // _colStructConfig
            // 
            this._colStructConfig.Text = "Конфигурация";
            this._colStructConfig.Width = 90;
            // 
            // _colStructQty
            // 
            this._colStructQty.Text = "Кол-во";
            this._colStructQty.Width = 50;
            // 
            // _colStructAction
            // 
            this._colStructAction.Text = "Действие";
            this._colStructAction.Width = 70;
            // 
            // _colStructExternalId
            // 
            this._colStructExternalId.Text = "ExternalId";
            this._colStructExternalId.Width = 110;
            // 
            // _colStructTimestamp
            // 
            this._colStructTimestamp.Text = "Дата/время";
            this._colStructTimestamp.Width = 120;
            // 
            // _allListView
            // 
            this._allListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this._colAllParent,
            this._colAllChild,
            this._colAllConfig,
            this._colAllQty,
            this._colAllAction,
            this._colAllExternalId,
            this._colAllTimestamp,
            this._colAllState});
            this._allListView.Dock = System.Windows.Forms.DockStyle.Fill;
            this._allListView.FullRowSelect = true;
            this._allListView.GridLines = true;
            this._allListView.HideSelection = false;
            this._allListView.Location = new System.Drawing.Point(3, 3);
            this._allListView.Name = "_allListView";
            this._allListView.Size = new System.Drawing.Size(1030, 182);
            this._allListView.TabIndex = 0;
            this._toolTip.SetToolTip(this._allListView, "Все операции состава из журнала (и очередь на выгрузку, и уже выгруженные). Помет" +
        "ку «Выгружено» снять нельзя: в CSV попадают только невыгруженные.");
            this._allListView.UseCompatibleStateImageBehavior = false;
            this._allListView.View = System.Windows.Forms.View.Details;
            this._allListView.ColumnClick += new System.Windows.Forms.ColumnClickEventHandler(this.OnAllColumnClick);
            // 
            // _colAllParent
            // 
            this._colAllParent.Text = "Родитель";
            this._colAllParent.Width = 140;
            // 
            // _colAllChild
            // 
            this._colAllChild.Text = "Компонент";
            this._colAllChild.Width = 140;
            // 
            // _colAllConfig
            // 
            this._colAllConfig.Text = "Конфигурация";
            this._colAllConfig.Width = 90;
            // 
            // _colAllQty
            // 
            this._colAllQty.Text = "Кол-во";
            this._colAllQty.Width = 50;
            // 
            // _colAllAction
            // 
            this._colAllAction.Text = "Действие";
            this._colAllAction.Width = 70;
            // 
            // _colAllExternalId
            // 
            this._colAllExternalId.Text = "ExternalId";
            this._colAllExternalId.Width = 110;
            // 
            // _colAllTimestamp
            // 
            this._colAllTimestamp.Text = "Дата/время";
            this._colAllTimestamp.Width = 120;
            // 
            // _colAllState
            // 
            this._colAllState.Text = "Состояние";
            this._colAllState.Width = 90;
            // 
            // _filterParentBox
            // 
            this._filterParentBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._filterParentBox.Location = new System.Drawing.Point(62, 2);
            this._filterParentBox.Margin = new System.Windows.Forms.Padding(0, 2, 8, 2);
            this._filterParentBox.Name = "_filterParentBox";
            this._filterParentBox.Size = new System.Drawing.Size(139, 20);
            this._filterParentBox.TabIndex = 1;
            this._toolTip.SetToolTip(this._filterParentBox, "Фильтр по обозначению или ExternalId родителя. Подстрока без учёта регистра; неск" +
        "олько значений через | (ИЛИ).");
            // 
            // _filterChildBox
            // 
            this._filterChildBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._filterChildBox.Location = new System.Drawing.Point(279, 2);
            this._filterChildBox.Margin = new System.Windows.Forms.Padding(0, 2, 8, 2);
            this._filterChildBox.Name = "_filterChildBox";
            this._filterChildBox.Size = new System.Drawing.Size(139, 20);
            this._filterChildBox.TabIndex = 3;
            this._toolTip.SetToolTip(this._filterChildBox, "Фильтр по обозначению или конфигурации компонента. Подстрока без учёта регистра; " +
        "несколько значений через | (ИЛИ).");
            // 
            // _filterActionBox
            // 
            this._filterActionBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._filterActionBox.Location = new System.Drawing.Point(490, 2);
            this._filterActionBox.Margin = new System.Windows.Forms.Padding(0, 2, 8, 2);
            this._filterActionBox.Name = "_filterActionBox";
            this._filterActionBox.Size = new System.Drawing.Size(60, 20);
            this._filterActionBox.TabIndex = 5;
            this._toolTip.SetToolTip(this._filterActionBox, "Фильтр по операции: add, update, delete. Несколько значений через | (ИЛИ), !add —" +
        " исключить.");
            // 
            // _filterExternalIdBox
            // 
            this._filterExternalIdBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._filterExternalIdBox.Location = new System.Drawing.Point(619, 2);
            this._filterExternalIdBox.Margin = new System.Windows.Forms.Padding(0, 2, 8, 2);
            this._filterExternalIdBox.Name = "_filterExternalIdBox";
            this._filterExternalIdBox.Size = new System.Drawing.Size(119, 20);
            this._filterExternalIdBox.TabIndex = 7;
            this._toolTip.SetToolTip(this._filterExternalIdBox, "Фильтр по ExternalId ребёнка — идентификатору связи с 1С. =abc — точное совпадени" +
        "е, !abc — исключить.");
            // 
            // _filterDateBox
            // 
            this._filterDateBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._filterDateBox.Location = new System.Drawing.Point(786, 2);
            this._filterDateBox.Margin = new System.Windows.Forms.Padding(0, 2, 8, 2);
            this._filterDateBox.Name = "_filterDateBox";
            this._filterDateBox.Size = new System.Drawing.Size(80, 20);
            this._filterDateBox.TabIndex = 9;
            this._toolTip.SetToolTip(this._filterDateBox, "Фильтр по дате записи (местное время): >=2026-09-01, <2026-09-20, либо подстрока " +
        "2026-09.");
            // 
            // _filterApplyButton
            // 
            this._filterApplyButton.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._filterApplyButton.AutoSize = true;
            this._filterApplyButton.Location = new System.Drawing.Point(874, 0);
            this._filterApplyButton.Margin = new System.Windows.Forms.Padding(0, 0, 4, 0);
            this._filterApplyButton.Name = "_filterApplyButton";
            this._filterApplyButton.Size = new System.Drawing.Size(75, 22);
            this._filterApplyButton.TabIndex = 10;
            this._filterApplyButton.Text = "Применить";
            this._toolTip.SetToolTip(this._filterApplyButton, "Применить введённые условия к списку текущей вкладки.");
            this._filterApplyButton.UseVisualStyleBackColor = true;
            this._filterApplyButton.Click += new System.EventHandler(this.OnFilterApplyClick);
            // 
            // _filterResetButton
            // 
            this._filterResetButton.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._filterResetButton.AutoSize = true;
            this._filterResetButton.Location = new System.Drawing.Point(953, 0);
            this._filterResetButton.Margin = new System.Windows.Forms.Padding(0);
            this._filterResetButton.Name = "_filterResetButton";
            this._filterResetButton.Size = new System.Drawing.Size(94, 22);
            this._filterResetButton.TabIndex = 11;
            this._filterResetButton.Text = "Сброс фильтра";
            this._toolTip.SetToolTip(this._filterResetButton, "Очистить поля условий и показать все строки. Список не удаляется, только фильтр.");
            this._filterResetButton.UseVisualStyleBackColor = true;
            this._filterResetButton.Click += new System.EventHandler(this.OnFilterResetClick);
            // 
            // _filterRow
            // 
            this._filterRow.ColumnCount = 12;
            this._filterRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this._filterRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 30F));
            this._filterRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this._filterRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 30F));
            this._filterRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this._filterRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 14F));
            this._filterRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this._filterRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 26F));
            this._filterRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this._filterRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 18F));
            this._filterRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this._filterRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this._filterRow.Controls.Add(this._filterParentLabel, 0, 0);
            this._filterRow.Controls.Add(this._filterParentBox, 1, 0);
            this._filterRow.Controls.Add(this._filterChildLabel, 2, 0);
            this._filterRow.Controls.Add(this._filterChildBox, 3, 0);
            this._filterRow.Controls.Add(this._filterActionLabel, 4, 0);
            this._filterRow.Controls.Add(this._filterActionBox, 5, 0);
            this._filterRow.Controls.Add(this._filterExternalIdLabel, 6, 0);
            this._filterRow.Controls.Add(this._filterExternalIdBox, 7, 0);
            this._filterRow.Controls.Add(this._filterDateLabel, 8, 0);
            this._filterRow.Controls.Add(this._filterDateBox, 9, 0);
            this._filterRow.Controls.Add(this._filterApplyButton, 10, 0);
            this._filterRow.Controls.Add(this._filterResetButton, 11, 0);
            this._filterRow.Dock = System.Windows.Forms.DockStyle.Fill;
            this._filterRow.Location = new System.Drawing.Point(12, 116);
            this._filterRow.Margin = new System.Windows.Forms.Padding(0, 6, 0, 2);
            this._filterRow.Name = "_filterRow";
            this._filterRow.RowCount = 1;
            this._filterRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._filterRow.Size = new System.Drawing.Size(1050, 22);
            this._filterRow.TabIndex = 6;
            // 
            // _filterParentLabel
            // 
            this._filterParentLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._filterParentLabel.AutoSize = true;
            this._filterParentLabel.Location = new System.Drawing.Point(0, 4);
            this._filterParentLabel.Margin = new System.Windows.Forms.Padding(0, 0, 4, 0);
            this._filterParentLabel.Name = "_filterParentLabel";
            this._filterParentLabel.Size = new System.Drawing.Size(58, 13);
            this._filterParentLabel.TabIndex = 0;
            this._filterParentLabel.Text = "Родитель:";
            // 
            // _filterChildLabel
            // 
            this._filterChildLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._filterChildLabel.AutoSize = true;
            this._filterChildLabel.Location = new System.Drawing.Point(209, 4);
            this._filterChildLabel.Margin = new System.Windows.Forms.Padding(0, 0, 4, 0);
            this._filterChildLabel.Name = "_filterChildLabel";
            this._filterChildLabel.Size = new System.Drawing.Size(66, 13);
            this._filterChildLabel.TabIndex = 2;
            this._filterChildLabel.Text = "Компонент:";
            // 
            // _filterActionLabel
            // 
            this._filterActionLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._filterActionLabel.AutoSize = true;
            this._filterActionLabel.Location = new System.Drawing.Point(426, 4);
            this._filterActionLabel.Margin = new System.Windows.Forms.Padding(0, 0, 4, 0);
            this._filterActionLabel.Name = "_filterActionLabel";
            this._filterActionLabel.Size = new System.Drawing.Size(60, 13);
            this._filterActionLabel.TabIndex = 4;
            this._filterActionLabel.Text = "Операция:";
            // 
            // _filterExternalIdLabel
            // 
            this._filterExternalIdLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._filterExternalIdLabel.AutoSize = true;
            this._filterExternalIdLabel.Location = new System.Drawing.Point(558, 4);
            this._filterExternalIdLabel.Margin = new System.Windows.Forms.Padding(0, 0, 4, 0);
            this._filterExternalIdLabel.Name = "_filterExternalIdLabel";
            this._filterExternalIdLabel.Size = new System.Drawing.Size(57, 13);
            this._filterExternalIdLabel.TabIndex = 6;
            this._filterExternalIdLabel.Text = "ExternalId:";
            // 
            // _filterDateLabel
            // 
            this._filterDateLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._filterDateLabel.AutoSize = true;
            this._filterDateLabel.Location = new System.Drawing.Point(746, 4);
            this._filterDateLabel.Margin = new System.Windows.Forms.Padding(0, 0, 4, 0);
            this._filterDateLabel.Name = "_filterDateLabel";
            this._filterDateLabel.Size = new System.Drawing.Size(36, 13);
            this._filterDateLabel.TabIndex = 8;
            this._filterDateLabel.Text = "Дата:";
            // 
            // _tabs
            // 
            this._tabs.Controls.Add(this._cardsTab);
            this._tabs.Controls.Add(this._structureTab);
            this._tabs.Controls.Add(this._allTab);
            this._tabs.Dock = System.Windows.Forms.DockStyle.Fill;
            this._tabs.Location = new System.Drawing.Point(15, 143);
            this._tabs.Name = "_tabs";
            this._tabs.SelectedIndex = 0;
            this._tabs.Size = new System.Drawing.Size(1044, 214);
            this._tabs.TabIndex = 5;
            // 
            // _cardsTab
            // 
            this._cardsTab.Controls.Add(this._listView);
            this._cardsTab.Location = new System.Drawing.Point(4, 22);
            this._cardsTab.Name = "_cardsTab";
            this._cardsTab.Padding = new System.Windows.Forms.Padding(3);
            this._cardsTab.Size = new System.Drawing.Size(1036, 188);
            this._cardsTab.TabIndex = 0;
            this._cardsTab.Text = "Карточки";
            this._cardsTab.UseVisualStyleBackColor = true;
            // 
            // _structureTab
            // 
            this._structureTab.Controls.Add(this._structureListView);
            this._structureTab.Location = new System.Drawing.Point(4, 22);
            this._structureTab.Name = "_structureTab";
            this._structureTab.Padding = new System.Windows.Forms.Padding(3);
            this._structureTab.Size = new System.Drawing.Size(1036, 188);
            this._structureTab.TabIndex = 1;
            this._structureTab.Text = "Структура";
            this._structureTab.UseVisualStyleBackColor = true;
            // 
            // _allTab
            // 
            this._allTab.Controls.Add(this._allListView);
            this._allTab.Location = new System.Drawing.Point(4, 22);
            this._allTab.Name = "_allTab";
            this._allTab.Padding = new System.Windows.Forms.Padding(3);
            this._allTab.Size = new System.Drawing.Size(1036, 188);
            this._allTab.TabIndex = 2;
            this._allTab.Text = "Все";
            this._allTab.UseVisualStyleBackColor = true;
            // 
            // shell
            // 
            this.shell.ColumnCount = 1;
            this.shell.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.shell.Controls.Add(this.root, 0, 0);
            this.shell.Controls.Add(this.notePanel, 0, 1);
            this.shell.Controls.Add(this.buttonsPanel, 0, 2);
            this.shell.Dock = System.Windows.Forms.DockStyle.Fill;
            this.shell.Location = new System.Drawing.Point(0, 0);
            this.shell.Name = "shell";
            this.shell.RowCount = 3;
            this.shell.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.shell.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.shell.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.shell.Size = new System.Drawing.Size(1080, 430);
            this.shell.TabIndex = 0;
            // 
            // root
            // 
            this.root.ColumnCount = 1;
            this.root.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.root.Controls.Add(this.titleLabel, 0, 0);
            this.root.Controls.Add(this.descLabel, 0, 1);
            this.root.Controls.Add(this.folderRow, 0, 2);
            this.root.Controls.Add(this._registryFilterCheck, 0, 3);
            this.root.Controls.Add(this._filterRow, 0, 4);
            this.root.Controls.Add(this._tabs, 0, 5);
            this.root.Dock = System.Windows.Forms.DockStyle.Fill;
            this.root.Location = new System.Drawing.Point(3, 3);
            this.root.Name = "root";
            this.root.Padding = new System.Windows.Forms.Padding(12, 12, 12, 0);
            this.root.RowCount = 6;
            this.root.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32F));
            this.root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 24F));
            this.root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.root.Size = new System.Drawing.Size(1074, 360);
            this.root.TabIndex = 0;
            // 
            // titleLabel
            // 
            this.titleLabel.AutoSize = true;
            this.titleLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.titleLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold);
            this.titleLabel.Location = new System.Drawing.Point(12, 12);
            this.titleLabel.Margin = new System.Windows.Forms.Padding(0, 0, 0, 4);
            this.titleLabel.Name = "titleLabel";
            this.titleLabel.Size = new System.Drawing.Size(1050, 13);
            this.titleLabel.TabIndex = 0;
            this.titleLabel.Text = "Экспорт BOM-данных в 1C";
            // 
            // descLabel
            // 
            this.descLabel.AutoSize = true;
            this.descLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.descLabel.Location = new System.Drawing.Point(12, 29);
            this.descLabel.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
            this.descLabel.Name = "descLabel";
            this.descLabel.Size = new System.Drawing.Size(1050, 17);
            this.descLabel.TabIndex = 1;
            this.descLabel.Text = "Формирует CSV-файлы обмена с 1C: карточки номенклатуры и структура состава сборок" +
    ".";
            // 
            // folderRow
            // 
            this.folderRow.ColumnCount = 3;
            this.folderRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.folderRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.folderRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.folderRow.Controls.Add(this.folderLabel, 0, 0);
            this.folderRow.Controls.Add(this._folderBox, 1, 0);
            this.folderRow.Controls.Add(this._browseButton, 2, 0);
            this.folderRow.Dock = System.Windows.Forms.DockStyle.Fill;
            this.folderRow.Location = new System.Drawing.Point(12, 54);
            this.folderRow.Margin = new System.Windows.Forms.Padding(0, 0, 0, 6);
            this.folderRow.Name = "folderRow";
            this.folderRow.RowCount = 1;
            this.folderRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32F));
            this.folderRow.Size = new System.Drawing.Size(1050, 26);
            this.folderRow.TabIndex = 2;
            // 
            // folderLabel
            // 
            this.folderLabel.AutoSize = true;
            this.folderLabel.Location = new System.Drawing.Point(0, 2);
            this.folderLabel.Margin = new System.Windows.Forms.Padding(0, 2, 8, 0);
            this.folderLabel.Name = "folderLabel";
            this.folderLabel.Size = new System.Drawing.Size(92, 13);
            this.folderLabel.TabIndex = 0;
            this.folderLabel.Text = "Каталог обмена:";
            // 
            // notePanel
            // 
            this.notePanel.Controls.Add(this.noteLabel);
            this.notePanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.notePanel.Location = new System.Drawing.Point(3, 369);
            this.notePanel.Name = "notePanel";
            this.notePanel.Padding = new System.Windows.Forms.Padding(12, 0, 12, 4);
            this.notePanel.Size = new System.Drawing.Size(1074, 17);
            this.notePanel.TabIndex = 1;
            // 
            // noteLabel
            // 
            this.noteLabel.AutoSize = true;
            this.noteLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.noteLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Italic);
            this.noteLabel.ForeColor = System.Drawing.Color.DimGray;
            this.noteLabel.Location = new System.Drawing.Point(12, 0);
            this.noteLabel.Name = "noteLabel";
            this.noteLabel.Size = new System.Drawing.Size(1050, 13);
            this.noteLabel.TabIndex = 3;
            this.noteLabel.Text = "Компоненты без заполненного ExternalId будут пропущены при экспорте.";
            // 
            // buttonsPanel
            // 
            this.buttonsPanel.Controls.Add(this._buttonsRow);
            this.buttonsPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonsPanel.Location = new System.Drawing.Point(3, 392);
            this.buttonsPanel.Name = "buttonsPanel";
            this.buttonsPanel.Padding = new System.Windows.Forms.Padding(12, 0, 12, 6);
            this.buttonsPanel.Size = new System.Drawing.Size(1074, 35);
            this.buttonsPanel.TabIndex = 2;
            // 
            // _buttonsRow
            // 
            this._buttonsRow.Controls.Add(this.cancelButton);
            this._buttonsRow.Controls.Add(this._settingsButton);
            this._buttonsRow.Controls.Add(this._layoutButton);
            this._buttonsRow.Controls.Add(this._exportButton);
            this._buttonsRow.Dock = System.Windows.Forms.DockStyle.Fill;
            this._buttonsRow.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this._buttonsRow.Location = new System.Drawing.Point(12, 0);
            this._buttonsRow.Margin = new System.Windows.Forms.Padding(0);
            this._buttonsRow.Name = "_buttonsRow";
            this._buttonsRow.Size = new System.Drawing.Size(1050, 29);
            this._buttonsRow.TabIndex = 4;
            this._buttonsRow.WrapContents = false;
            // 
            // cancelButton
            // 
            this.cancelButton.AutoSize = true;
            this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancelButton.Location = new System.Drawing.Point(972, 3);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(75, 23);
            this.cancelButton.TabIndex = 0;
            this.cancelButton.Text = "Закрыть";
            this.cancelButton.UseVisualStyleBackColor = true;
            // 
            // VelumBomExchangeForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1080, 430);
            this.Controls.Add(this.shell);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MinimumSize = new System.Drawing.Size(840, 420);
            this.Name = "VelumBomExchangeForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Экспорт BOM в 1C";
            this._filterRow.ResumeLayout(false);
            this._filterRow.PerformLayout();
            this._tabs.ResumeLayout(false);
            this._cardsTab.ResumeLayout(false);
            this._structureTab.ResumeLayout(false);
            this._allTab.ResumeLayout(false);
            this.shell.ResumeLayout(false);
            this.root.ResumeLayout(false);
            this.root.PerformLayout();
            this.folderRow.ResumeLayout(false);
            this.folderRow.PerformLayout();
            this.notePanel.ResumeLayout(false);
            this.notePanel.PerformLayout();
            this.buttonsPanel.ResumeLayout(false);
            this._buttonsRow.ResumeLayout(false);
            this._buttonsRow.PerformLayout();
            this.ResumeLayout(false);

    }

    #endregion

    private TableLayoutPanel shell;
    private TableLayoutPanel root;
    private Label titleLabel;
    private Label descLabel;
    private TableLayoutPanel folderRow;
    private Label folderLabel;
    private Panel notePanel;
    private Label noteLabel;
    private Panel buttonsPanel;
    private FlowLayoutPanel _buttonsRow;
    private Button cancelButton;
  }
}
