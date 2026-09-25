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
      this._tabs = new System.Windows.Forms.TabControl();
      this._cardsTab = new System.Windows.Forms.TabPage();
      this._structureTab = new System.Windows.Forms.TabPage();
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
      this.root = new System.Windows.Forms.TableLayoutPanel();
      this.titleLabel = new System.Windows.Forms.Label();
      this.descLabel = new System.Windows.Forms.Label();
      this.folderRow = new System.Windows.Forms.TableLayoutPanel();
      this.folderLabel = new System.Windows.Forms.Label();
      this.noteLabel = new System.Windows.Forms.Label();
      this.buttonsRow = new System.Windows.Forms.FlowLayoutPanel();
      this.cancelButton = new System.Windows.Forms.Button();
      this._tabs.SuspendLayout();
      this._cardsTab.SuspendLayout();
      this._structureTab.SuspendLayout();
      this.root.SuspendLayout();
      this.folderRow.SuspendLayout();
      this.buttonsRow.SuspendLayout();
      this.SuspendLayout();
      // 
      // _folderBox
      // 
      this._folderBox.Dock = System.Windows.Forms.DockStyle.Fill;
      this._folderBox.Location = new System.Drawing.Point(100, 2);
      this._folderBox.Margin = new System.Windows.Forms.Padding(0, 2, 4, 2);
      this._folderBox.Name = "_folderBox";
      this._folderBox.ReadOnly = true;
      this._folderBox.Size = new System.Drawing.Size(313, 20);
      this._folderBox.TabIndex = 1;
      this._toolTip.SetToolTip(this._folderBox, "Каталог, в который сохраняются CSV-файлы обмена с 1C.");
      // 
      // _browseButton
      // 
      this._browseButton.AutoSize = true;
      this._browseButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
      this._browseButton.Location = new System.Drawing.Point(421, 2);
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
      this._exportButton.Location = new System.Drawing.Point(175, 3);
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
      this._settingsButton.Location = new System.Drawing.Point(337, 3);
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
      this._layoutButton.Location = new System.Drawing.Point(256, 3);
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
      this._registryFilterCheck.Location = new System.Drawing.Point(100, 39);
      this._registryFilterCheck.Name = "_registryFilterCheck";
      this._registryFilterCheck.Size = new System.Drawing.Size(240, 17);
      this._registryFilterCheck.TabIndex = 4;
      this._registryFilterCheck.Text = "Только зарегистрированные в реестре изделий";
      this._toolTip.SetToolTip(this._registryFilterCheck, "Показывать и выгружать в 1C только позиции, файлы которых есть в реестре изделий.");
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
      // _tabs
      // 
      this._tabs.Controls.Add(this._cardsTab);
      this._tabs.Controls.Add(this._structureTab);
      this._tabs.Dock = System.Windows.Forms.DockStyle.Fill;
      this._tabs.Location = new System.Drawing.Point(15, 99);
      this._tabs.Name = "_tabs";
      this._tabs.SelectedIndex = 0;
      this._tabs.Size = new System.Drawing.Size(490, 180);
      this._tabs.TabIndex = 5;
      // 
      // _cardsTab
      // 
      this._cardsTab.Controls.Add(this._listView);
      this._cardsTab.Location = new System.Drawing.Point(4, 22);
      this._cardsTab.Name = "_cardsTab";
      this._cardsTab.Padding = new System.Windows.Forms.Padding(3);
      this._cardsTab.Size = new System.Drawing.Size(482, 154);
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
      this._structureTab.Size = new System.Drawing.Size(482, 154);
      this._structureTab.TabIndex = 1;
      this._structureTab.Text = "Структура";
      this._structureTab.UseVisualStyleBackColor = true;
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
      this._listView.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
      this._listView.HideSelection = false;
      this._listView.Location = new System.Drawing.Point(3, 3);
      this._listView.Name = "_listView";
      this._listView.Size = new System.Drawing.Size(476, 148);
      this._listView.TabIndex = 0;
      this._toolTip.SetToolTip(this._listView, "Карточки, которые попадут в 1C_update_*.csv. Состав колонок настраивается кнопкой «Поля…».");
      this._listView.UseCompatibleStateImageBehavior = false;
      this._listView.View = System.Windows.Forms.View.Details;
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
            this._colStructAction});
      this._structureListView.Dock = System.Windows.Forms.DockStyle.Fill;
      this._structureListView.FullRowSelect = true;
      this._structureListView.GridLines = true;
      this._structureListView.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
      this._structureListView.HideSelection = false;
      this._structureListView.Location = new System.Drawing.Point(3, 3);
      this._structureListView.Name = "_structureListView";
      this._structureListView.Size = new System.Drawing.Size(476, 148);
      this._structureListView.TabIndex = 0;
      this._toolTip.SetToolTip(this._structureListView, "Строки состава, которые попадут в 1C_bom_*.csv (операции add/update/delete).");
      this._structureListView.UseCompatibleStateImageBehavior = false;
      this._structureListView.View = System.Windows.Forms.View.Details;
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
      // root
      // 
      this.root.ColumnCount = 1;
      this.root.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this.root.Controls.Add(this.titleLabel, 0, 0);
      this.root.Controls.Add(this.descLabel, 0, 1);
      this.root.Controls.Add(this.folderRow, 0, 2);
      this.root.Controls.Add(this._tabs, 0, 3);
      this.root.Controls.Add(this.noteLabel, 0, 4);
      this.root.Controls.Add(this.buttonsRow, 0, 5);
      this.root.Dock = System.Windows.Forms.DockStyle.Fill;
      this.root.Location = new System.Drawing.Point(0, 0);
      this.root.Name = "root";
      this.root.Padding = new System.Windows.Forms.Padding(12);
      this.root.RowCount = 6;
      this.root.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this.root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
      this.root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 42F));
      this.root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this.root.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this.root.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this.root.Size = new System.Drawing.Size(520, 340);
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
      this.titleLabel.Size = new System.Drawing.Size(496, 13);
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
      this.descLabel.Size = new System.Drawing.Size(496, 17);
      this.descLabel.TabIndex = 1;
      this.descLabel.Text = "Формирует CSV-файлы обмена с 1C: карточки номенклатуры и структура состава сборок.";
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
      this.folderRow.Controls.Add(this._registryFilterCheck, 0, 1);
      this.folderRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this.folderRow.Location = new System.Drawing.Point(12, 54);
      this.folderRow.Margin = new System.Windows.Forms.Padding(0, 0, 0, 6);
      this.folderRow.Name = "folderRow";
      this.folderRow.RowCount = 2;
      this.folderRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 36F));
      this.folderRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 18F));
      this.folderRow.Size = new System.Drawing.Size(496, 54);
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
      // noteLabel
      // 
      this.noteLabel.AutoSize = true;
      this.noteLabel.Dock = System.Windows.Forms.DockStyle.Fill;
      this.noteLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Italic);
      this.noteLabel.ForeColor = System.Drawing.Color.DimGray;
      this.noteLabel.Location = new System.Drawing.Point(12, 282);
      this.noteLabel.Margin = new System.Windows.Forms.Padding(0, 0, 0, 4);
      this.noteLabel.Name = "noteLabel";
      this.noteLabel.Size = new System.Drawing.Size(496, 13);
      this.noteLabel.TabIndex = 3;
      this.noteLabel.Text = "Компоненты без заполненного ExternalId будут пропущены при экспорте.";
      // 
      // buttonsRow
      // 
      this.buttonsRow.AutoSize = true;
      this.buttonsRow.Controls.Add(this.cancelButton);
      this.buttonsRow.Controls.Add(this._settingsButton);
      this.buttonsRow.Controls.Add(this._layoutButton);
      this.buttonsRow.Controls.Add(this._exportButton);
      this.buttonsRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this.buttonsRow.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
      this.buttonsRow.Location = new System.Drawing.Point(12, 299);
      this.buttonsRow.Margin = new System.Windows.Forms.Padding(0);
      this.buttonsRow.Name = "buttonsRow";
      this.buttonsRow.Size = new System.Drawing.Size(496, 29);
      this.buttonsRow.TabIndex = 4;
      this.buttonsRow.WrapContents = false;
      // 
      // cancelButton
      // 
      this.cancelButton.AutoSize = true;
      this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
      this.cancelButton.Location = new System.Drawing.Point(418, 3);
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
      this.ClientSize = new System.Drawing.Size(520, 340);
      this.Controls.Add(this.root);
      this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
      this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
      this.Name = "VelumBomExchangeForm";
      this.ShowInTaskbar = false;
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
      this.Text = "Экспорт BOM в 1C";
      this._tabs.ResumeLayout(false);
      this._cardsTab.ResumeLayout(false);
      this._structureTab.ResumeLayout(false);
      this.root.ResumeLayout(false);
      this.root.PerformLayout();
      this.folderRow.ResumeLayout(false);
      this.folderRow.PerformLayout();
      this.buttonsRow.ResumeLayout(false);
      this.buttonsRow.PerformLayout();
      this.ResumeLayout(false);

    }

    #endregion

    private TableLayoutPanel root;
    private Label titleLabel;
    private Label descLabel;
    private TableLayoutPanel folderRow;
    private Label folderLabel;
    private Label noteLabel;
    private FlowLayoutPanel buttonsRow;
    private Button cancelButton;
  }
}
