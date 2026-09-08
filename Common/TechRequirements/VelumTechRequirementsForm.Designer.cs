namespace Velum.UI
{
  internal sealed partial class VelumTechRequirementsForm
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
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VelumTechRequirementsForm));
      this._rootLayout = new System.Windows.Forms.TableLayoutPanel();
      this._filtersRow = new System.Windows.Forms.TableLayoutPanel();
      this._lblTemplates = new System.Windows.Forms.Label();
      this._btnImport = new System.Windows.Forms.Button();
      this._lblGroupFilter = new System.Windows.Forms.Label();
      this._groupFilterBox = new System.Windows.Forms.TextBox();
      this._lblItemFilter = new System.Windows.Forms.Label();
      this._itemFilterBox = new System.Windows.Forms.TextBox();
      this._btnFilterApply = new System.Windows.Forms.Button();
      this._btnFilterReset = new System.Windows.Forms.Button();
      this._btnFilterHelp = new System.Windows.Forms.Button();
      this._listView = new System.Windows.Forms.ListView();
      this._optionsRow = new System.Windows.Forms.TableLayoutPanel();
      this._lblFontSize = new System.Windows.Forms.Label();
      this._fontSizeBox = new System.Windows.Forms.ComboBox();
      this._fontDefaultCheck = new System.Windows.Forms.CheckBox();
      this._multiSelectCheck = new System.Windows.Forms.CheckBox();
      this._actionsRow = new System.Windows.Forms.TableLayoutPanel();
      this._btnApply = new System.Windows.Forms.Button();
      this._btnClose = new System.Windows.Forms.Button();
      this._rootLayout.SuspendLayout();
      this._filtersRow.SuspendLayout();
      this._optionsRow.SuspendLayout();
      this._actionsRow.SuspendLayout();
      this.SuspendLayout();
      // 
      // _rootLayout
      // 
      this._rootLayout.ColumnCount = 1;
      this._rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._rootLayout.Controls.Add(this._filtersRow, 0, 0);
      this._rootLayout.Controls.Add(this._listView, 0, 1);
      this._rootLayout.Controls.Add(this._optionsRow, 0, 2);
      this._rootLayout.Controls.Add(this._actionsRow, 0, 3);
      this._rootLayout.Dock = System.Windows.Forms.DockStyle.Fill;
      this._rootLayout.Location = new System.Drawing.Point(0, 0);
      this._rootLayout.Name = "_rootLayout";
      this._rootLayout.Padding = new System.Windows.Forms.Padding(10);
      this._rootLayout.RowCount = 4;
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 36F));
      this._rootLayout.Size = new System.Drawing.Size(700, 480);
      this._rootLayout.TabIndex = 0;
      // 
      // _filtersRow
      // 
      this._filtersRow.AutoSize = true;
      this._filtersRow.ColumnCount = 9;
      this._filtersRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._filtersRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._filtersRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._filtersRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
      this._filtersRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._filtersRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
      this._filtersRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 90F));
      this._filtersRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 70F));
      this._filtersRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 32F));
      this._filtersRow.Controls.Add(this._lblTemplates, 0, 0);
      this._filtersRow.Controls.Add(this._btnImport, 1, 0);
      this._filtersRow.Controls.Add(this._lblGroupFilter, 2, 0);
      this._filtersRow.Controls.Add(this._groupFilterBox, 3, 0);
      this._filtersRow.Controls.Add(this._lblItemFilter, 4, 0);
      this._filtersRow.Controls.Add(this._itemFilterBox, 5, 0);
      this._filtersRow.Controls.Add(this._btnFilterApply, 6, 0);
      this._filtersRow.Controls.Add(this._btnFilterReset, 7, 0);
      this._filtersRow.Controls.Add(this._btnFilterHelp, 8, 0);
      this._filtersRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._filtersRow.Location = new System.Drawing.Point(13, 13);
      this._filtersRow.Name = "_filtersRow";
      this._filtersRow.RowCount = 1;
      this._filtersRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._filtersRow.Size = new System.Drawing.Size(674, 28);
      this._filtersRow.TabIndex = 0;
      // 
      // _lblTemplates
      // 
      this._lblTemplates.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._lblTemplates.AutoSize = true;
      this._lblTemplates.Location = new System.Drawing.Point(3, 7);
      this._lblTemplates.Name = "_lblTemplates";
      this._lblTemplates.Size = new System.Drawing.Size(71, 13);
      this._lblTemplates.TabIndex = 0;
      this._lblTemplates.Text = "Шаблоны ТТ";
      // 
      // _btnImport
      // 
      this._btnImport.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._btnImport.Location = new System.Drawing.Point(80, 3);
      this._btnImport.Name = "_btnImport";
      this._btnImport.Size = new System.Drawing.Size(88, 22);
      this._btnImport.TabIndex = 1;
      this._btnImport.Text = "Импорт…";
      this._btnImport.UseVisualStyleBackColor = true;
      this._btnImport.Click += new System.EventHandler(this.OnImportClick);
      // 
      // _lblGroupFilter
      // 
      this._lblGroupFilter.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._lblGroupFilter.AutoSize = true;
      this._lblGroupFilter.Location = new System.Drawing.Point(174, 7);
      this._lblGroupFilter.Name = "_lblGroupFilter";
      this._lblGroupFilter.Size = new System.Drawing.Size(78, 13);
      this._lblGroupFilter.TabIndex = 2;
      this._lblGroupFilter.Text = "Фильтр групп";
      // 
      // _groupFilterBox
      // 
      this._groupFilterBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
      this._groupFilterBox.Location = new System.Drawing.Point(258, 4);
      this._groupFilterBox.Name = "_groupFilterBox";
      this._groupFilterBox.Size = new System.Drawing.Size(65, 20);
      this._groupFilterBox.TabIndex = 3;
      // 
      // _lblItemFilter
      // 
      this._lblItemFilter.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._lblItemFilter.AutoSize = true;
      this._lblItemFilter.Location = new System.Drawing.Point(329, 7);
      this._lblItemFilter.Name = "_lblItemFilter";
      this._lblItemFilter.Size = new System.Drawing.Size(79, 13);
      this._lblItemFilter.TabIndex = 4;
      this._lblItemFilter.Text = "Фильтр строк";
      // 
      // _itemFilterBox
      // 
      this._itemFilterBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
      this._itemFilterBox.Location = new System.Drawing.Point(414, 4);
      this._itemFilterBox.Name = "_itemFilterBox";
      this._itemFilterBox.Size = new System.Drawing.Size(65, 20);
      this._itemFilterBox.TabIndex = 5;
      // 
      // _btnFilterApply
      // 
      this._btnFilterApply.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._btnFilterApply.Location = new System.Drawing.Point(485, 3);
      this._btnFilterApply.Name = "_btnFilterApply";
      this._btnFilterApply.Size = new System.Drawing.Size(84, 22);
      this._btnFilterApply.TabIndex = 6;
      this._btnFilterApply.Text = "Применить";
      this._btnFilterApply.UseVisualStyleBackColor = true;
      // 
      // _btnFilterReset
      // 
      this._btnFilterReset.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._btnFilterReset.Location = new System.Drawing.Point(575, 3);
      this._btnFilterReset.Name = "_btnFilterReset";
      this._btnFilterReset.Size = new System.Drawing.Size(64, 22);
      this._btnFilterReset.TabIndex = 7;
      this._btnFilterReset.Text = "Сброс";
      this._btnFilterReset.UseVisualStyleBackColor = true;
      // 
      // _btnFilterHelp
      // 
      this._btnFilterHelp.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._btnFilterHelp.Location = new System.Drawing.Point(645, 3);
      this._btnFilterHelp.Name = "_btnFilterHelp";
      this._btnFilterHelp.Size = new System.Drawing.Size(26, 22);
      this._btnFilterHelp.TabIndex = 8;
      this._btnFilterHelp.Text = "?";
      this._btnFilterHelp.UseVisualStyleBackColor = true;
      // 
      // _listView
      // 
      this._listView.Dock = System.Windows.Forms.DockStyle.Fill;
      this._listView.FullRowSelect = true;
      this._listView.HideSelection = false;
      this._listView.Location = new System.Drawing.Point(13, 47);
      this._listView.Name = "_listView";
      this._listView.Size = new System.Drawing.Size(674, 351);
      this._listView.TabIndex = 1;
      this._listView.UseCompatibleStateImageBehavior = false;
      // 
      // _optionsRow
      // 
      this._optionsRow.AutoSize = true;
      this._optionsRow.ColumnCount = 4;
      this._optionsRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._optionsRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._optionsRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._optionsRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._optionsRow.Controls.Add(this._lblFontSize, 0, 0);
      this._optionsRow.Controls.Add(this._fontSizeBox, 1, 0);
      this._optionsRow.Controls.Add(this._fontDefaultCheck, 2, 0);
      this._optionsRow.Controls.Add(this._multiSelectCheck, 3, 0);
      this._optionsRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._optionsRow.Location = new System.Drawing.Point(13, 404);
      this._optionsRow.Name = "_optionsRow";
      this._optionsRow.RowCount = 1;
      this._optionsRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._optionsRow.Size = new System.Drawing.Size(674, 27);
      this._optionsRow.TabIndex = 2;
      // 
      // _lblFontSize
      // 
      this._lblFontSize.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._lblFontSize.AutoSize = true;
      this._lblFontSize.Location = new System.Drawing.Point(3, 7);
      this._lblFontSize.Name = "_lblFontSize";
      this._lblFontSize.Size = new System.Drawing.Size(88, 13);
      this._lblFontSize.TabIndex = 0;
      this._lblFontSize.Text = "Размер шрифта";
      // 
      // _fontSizeBox
      // 
      this._fontSizeBox.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._fontSizeBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
      this._fontSizeBox.FormattingEnabled = true;
      this._fontSizeBox.Items.AddRange(new object[] {
            "10",
            "12",
            "14",
            "16",
            "18",
            "20",
            "22",
            "24"});
      this._fontSizeBox.Location = new System.Drawing.Point(97, 3);
      this._fontSizeBox.Name = "_fontSizeBox";
      this._fontSizeBox.Size = new System.Drawing.Size(58, 21);
      this._fontSizeBox.TabIndex = 1;
      // 
      // _fontDefaultCheck
      // 
      this._fontDefaultCheck.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._fontDefaultCheck.AutoSize = true;
      this._fontDefaultCheck.Location = new System.Drawing.Point(161, 5);
      this._fontDefaultCheck.Name = "_fontDefaultCheck";
      this._fontDefaultCheck.Size = new System.Drawing.Size(125, 17);
      this._fontDefaultCheck.TabIndex = 2;
      this._fontDefaultCheck.Text = "Шрифт из настроек";
      this._fontDefaultCheck.UseVisualStyleBackColor = true;
      this._fontDefaultCheck.CheckedChanged += new System.EventHandler(this.OnFontDefaultChanged);
      // 
      // _multiSelectCheck
      // 
      this._multiSelectCheck.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._multiSelectCheck.AutoSize = true;
      this._multiSelectCheck.Location = new System.Drawing.Point(292, 5);
      this._multiSelectCheck.Name = "_multiSelectCheck";
      this._multiSelectCheck.Size = new System.Drawing.Size(146, 17);
      this._multiSelectCheck.TabIndex = 3;
      this._multiSelectCheck.Text = "Включить мультивыбор";
      this._multiSelectCheck.UseVisualStyleBackColor = true;
      this._multiSelectCheck.CheckedChanged += new System.EventHandler(this.OnMultiSelectChanged);
      // 
      // _actionsRow
      // 
      this._actionsRow.ColumnCount = 3;
      this._actionsRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._actionsRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._actionsRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._actionsRow.Controls.Add(this._btnApply, 1, 0);
      this._actionsRow.Controls.Add(this._btnClose, 2, 0);
      this._actionsRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._actionsRow.Location = new System.Drawing.Point(13, 437);
      this._actionsRow.Name = "_actionsRow";
      this._actionsRow.RowCount = 1;
      this._actionsRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._actionsRow.Size = new System.Drawing.Size(674, 30);
      this._actionsRow.TabIndex = 3;
      // 
      // _btnApply
      // 
      this._btnApply.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnApply.Location = new System.Drawing.Point(460, 3);
      this._btnApply.Name = "_btnApply";
      this._btnApply.Size = new System.Drawing.Size(120, 24);
      this._btnApply.TabIndex = 0;
      this._btnApply.Text = "Вставить";
      this._btnApply.UseVisualStyleBackColor = true;
      this._btnApply.Click += new System.EventHandler(this.OnApplyClick);
      // 
      // _btnClose
      // 
      this._btnClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
      this._btnClose.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnClose.Location = new System.Drawing.Point(586, 3);
      this._btnClose.Name = "_btnClose";
      this._btnClose.Size = new System.Drawing.Size(85, 24);
      this._btnClose.TabIndex = 1;
      this._btnClose.Text = "Закрыть";
      this._btnClose.UseVisualStyleBackColor = true;
      // 
      // VelumTechRequirementsForm
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.CancelButton = this._btnClose;
      this.ClientSize = new System.Drawing.Size(700, 480);
      this.Controls.Add(this._rootLayout);
      this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
      this.MinimizeBox = false;
      this.MinimumSize = new System.Drawing.Size(640, 420);
      this.Name = "VelumTechRequirementsForm";
      this.ShowInTaskbar = false;
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
      this.Text = "Технические требования";
      this._rootLayout.ResumeLayout(false);
      this._rootLayout.PerformLayout();
      this._filtersRow.ResumeLayout(false);
      this._filtersRow.PerformLayout();
      this._optionsRow.ResumeLayout(false);
      this._optionsRow.PerformLayout();
      this._actionsRow.ResumeLayout(false);
      this.ResumeLayout(false);

    }

    #endregion

    private System.Windows.Forms.TableLayoutPanel _rootLayout;
    private System.Windows.Forms.TableLayoutPanel _filtersRow;
    private System.Windows.Forms.Label _lblTemplates;
    private System.Windows.Forms.Button _btnImport;
    private System.Windows.Forms.Label _lblGroupFilter;
    private System.Windows.Forms.TextBox _groupFilterBox;
    private System.Windows.Forms.Label _lblItemFilter;
    private System.Windows.Forms.TextBox _itemFilterBox;
    private System.Windows.Forms.Button _btnFilterApply;
    private System.Windows.Forms.Button _btnFilterReset;
    private System.Windows.Forms.Button _btnFilterHelp;
    private System.Windows.Forms.ListView _listView;
    private System.Windows.Forms.TableLayoutPanel _optionsRow;
    private System.Windows.Forms.Label _lblFontSize;
    private System.Windows.Forms.ComboBox _fontSizeBox;
    private System.Windows.Forms.CheckBox _fontDefaultCheck;
    private System.Windows.Forms.CheckBox _multiSelectCheck;
    private System.Windows.Forms.TableLayoutPanel _actionsRow;
    private System.Windows.Forms.Button _btnApply;
    private System.Windows.Forms.Button _btnClose;
  }
}
