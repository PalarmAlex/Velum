namespace Velum.UI
{
  internal sealed partial class VelumAssemblyRegistryPrintDrawingsForm
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
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VelumAssemblyRegistryPrintDrawingsForm));
      this._root = new System.Windows.Forms.TableLayoutPanel();
      this._chkSelectAll = new System.Windows.Forms.CheckBox();
      this._filtersRow = new System.Windows.Forms.TableLayoutPanel();
      this._lblPositionFilter = new System.Windows.Forms.Label();
      this._positionFilterBox = new System.Windows.Forms.TextBox();
      this._lblDrawingFilter = new System.Windows.Forms.Label();
      this._drawingFilterBox = new System.Windows.Forms.TextBox();
      this._btnFilterApply = new System.Windows.Forms.Button();
      this._btnFilterReset = new System.Windows.Forms.Button();
      this._btnFilterHelp = new System.Windows.Forms.Button();
      this._list = new System.Windows.Forms.ListView();
      this._colPosition = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
      this._colDrawing = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
      this._colPath = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
      this._options = new System.Windows.Forms.TableLayoutPanel();
      this._lblCopies = new System.Windows.Forms.Label();
      this._copiesBox = new System.Windows.Forms.NumericUpDown();
      this._lblPrinter = new System.Windows.Forms.Label();
      this._printerBox = new System.Windows.Forms.ComboBox();
      this._bottom = new System.Windows.Forms.TableLayoutPanel();
      this._statusLabel = new System.Windows.Forms.Label();
      this._progressBar = new System.Windows.Forms.ProgressBar();
      this._buttonsPanel = new System.Windows.Forms.FlowLayoutPanel();
      this._btnClose = new System.Windows.Forms.Button();
      this._btnPrint = new System.Windows.Forms.Button();
      this._root.SuspendLayout();
      this._filtersRow.SuspendLayout();
      this._options.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)(this._copiesBox)).BeginInit();
      this._bottom.SuspendLayout();
      this._buttonsPanel.SuspendLayout();
      this.SuspendLayout();
      // 
      // _root
      // 
      this._root.ColumnCount = 1;
      this._root.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._root.Controls.Add(this._chkSelectAll, 0, 0);
      this._root.Controls.Add(this._filtersRow, 0, 1);
      this._root.Controls.Add(this._list, 0, 2);
      this._root.Controls.Add(this._options, 0, 3);
      this._root.Controls.Add(this._bottom, 0, 4);
      this._root.Dock = System.Windows.Forms.DockStyle.Fill;
      this._root.Location = new System.Drawing.Point(0, 0);
      this._root.Name = "_root";
      this._root.Padding = new System.Windows.Forms.Padding(10);
      this._root.RowCount = 5;
      this._root.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._root.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._root.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._root.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._root.Size = new System.Drawing.Size(820, 476);
      this._root.TabIndex = 0;
      // 
      // _chkSelectAll
      // 
      this._chkSelectAll.AutoSize = true;
      this._chkSelectAll.Checked = true;
      this._chkSelectAll.CheckState = System.Windows.Forms.CheckState.Checked;
      this._chkSelectAll.Location = new System.Drawing.Point(10, 10);
      this._chkSelectAll.Margin = new System.Windows.Forms.Padding(0, 0, 0, 4);
      this._chkSelectAll.Name = "_chkSelectAll";
      this._chkSelectAll.Size = new System.Drawing.Size(97, 17);
      this._chkSelectAll.TabIndex = 0;
      this._chkSelectAll.Text = "Выделить все";
      this._chkSelectAll.UseVisualStyleBackColor = true;
      this._chkSelectAll.CheckedChanged += new System.EventHandler(this.OnSelectAllCheckedChanged);
      // 
      // _filtersRow
      // 
      this._filtersRow.AutoSize = true;
      this._filtersRow.ColumnCount = 7;
      this._filtersRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._filtersRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
      this._filtersRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._filtersRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
      this._filtersRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 90F));
      this._filtersRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 70F));
      this._filtersRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 33F));
      this._filtersRow.Controls.Add(this._lblPositionFilter, 0, 0);
      this._filtersRow.Controls.Add(this._positionFilterBox, 1, 0);
      this._filtersRow.Controls.Add(this._lblDrawingFilter, 2, 0);
      this._filtersRow.Controls.Add(this._drawingFilterBox, 3, 0);
      this._filtersRow.Controls.Add(this._btnFilterApply, 4, 0);
      this._filtersRow.Controls.Add(this._btnFilterReset, 5, 0);
      this._filtersRow.Controls.Add(this._btnFilterHelp, 6, 0);
      this._filtersRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this._filtersRow.Location = new System.Drawing.Point(10, 31);
      this._filtersRow.Margin = new System.Windows.Forms.Padding(0, 0, 0, 4);
      this._filtersRow.Name = "_filtersRow";
      this._filtersRow.RowCount = 1;
      this._filtersRow.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._filtersRow.Size = new System.Drawing.Size(800, 28);
      this._filtersRow.TabIndex = 1;
      // 
      // _lblPositionFilter
      // 
      this._lblPositionFilter.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._lblPositionFilter.AutoSize = true;
      this._lblPositionFilter.Location = new System.Drawing.Point(0, 7);
      this._lblPositionFilter.Margin = new System.Windows.Forms.Padding(0, 0, 4, 0);
      this._lblPositionFilter.Name = "_lblPositionFilter";
      this._lblPositionFilter.Size = new System.Drawing.Size(54, 13);
      this._lblPositionFilter.TabIndex = 0;
      this._lblPositionFilter.Text = "Позиция:";
      // 
      // _positionFilterBox
      // 
      this._positionFilterBox.Dock = System.Windows.Forms.DockStyle.Fill;
      this._positionFilterBox.Location = new System.Drawing.Point(61, 3);
      this._positionFilterBox.Name = "_positionFilterBox";
      this._positionFilterBox.Size = new System.Drawing.Size(220, 20);
      this._positionFilterBox.TabIndex = 1;
      // 
      // _lblDrawingFilter
      // 
      this._lblDrawingFilter.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._lblDrawingFilter.AutoSize = true;
      this._lblDrawingFilter.Location = new System.Drawing.Point(290, 7);
      this._lblDrawingFilter.Margin = new System.Windows.Forms.Padding(6, 0, 4, 0);
      this._lblDrawingFilter.Name = "_lblDrawingFilter";
      this._lblDrawingFilter.Size = new System.Drawing.Size(87, 13);
      this._lblDrawingFilter.TabIndex = 2;
      this._lblDrawingFilter.Text = "Чертёж/статус:";
      // 
      // _drawingFilterBox
      // 
      this._drawingFilterBox.Dock = System.Windows.Forms.DockStyle.Fill;
      this._drawingFilterBox.Location = new System.Drawing.Point(384, 3);
      this._drawingFilterBox.Name = "_drawingFilterBox";
      this._drawingFilterBox.Size = new System.Drawing.Size(220, 20);
      this._drawingFilterBox.TabIndex = 3;
      // 
      // _btnFilterApply
      // 
      this._btnFilterApply.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnFilterApply.Location = new System.Drawing.Point(610, 3);
      this._btnFilterApply.Name = "_btnFilterApply";
      this._btnFilterApply.Size = new System.Drawing.Size(84, 22);
      this._btnFilterApply.TabIndex = 4;
      this._btnFilterApply.Text = "Применить";
      this._btnFilterApply.UseVisualStyleBackColor = true;
      // 
      // _btnFilterReset
      // 
      this._btnFilterReset.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnFilterReset.Location = new System.Drawing.Point(700, 3);
      this._btnFilterReset.Name = "_btnFilterReset";
      this._btnFilterReset.Size = new System.Drawing.Size(64, 22);
      this._btnFilterReset.TabIndex = 5;
      this._btnFilterReset.Text = "Сброс";
      this._btnFilterReset.UseVisualStyleBackColor = true;
      // 
      // _btnFilterHelp
      // 
      this._btnFilterHelp.Dock = System.Windows.Forms.DockStyle.Fill;
      this._btnFilterHelp.Location = new System.Drawing.Point(770, 3);
      this._btnFilterHelp.Name = "_btnFilterHelp";
      this._btnFilterHelp.Size = new System.Drawing.Size(27, 22);
      this._btnFilterHelp.TabIndex = 6;
      this._btnFilterHelp.Text = "?";
      this._btnFilterHelp.UseVisualStyleBackColor = true;
      // 
      // _list
      // 
      this._list.CheckBoxes = true;
      this._list.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this._colPosition,
            this._colDrawing,
            this._colPath});
      this._list.Dock = System.Windows.Forms.DockStyle.Fill;
      this._list.FullRowSelect = true;
      this._list.GridLines = true;
      this._list.HideSelection = false;
      this._list.Location = new System.Drawing.Point(13, 66);
      this._list.Name = "_list";
      this._list.Size = new System.Drawing.Size(794, 314);
      this._list.TabIndex = 2;
      this._list.UseCompatibleStateImageBehavior = false;
      this._list.View = System.Windows.Forms.View.Details;
      this._list.ColumnClick += new System.Windows.Forms.ColumnClickEventHandler(this.OnListColumnClick);
      this._list.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.OnListItemCheck);
      this._list.ItemChecked += new System.Windows.Forms.ItemCheckedEventHandler(this.OnListItemChecked);
      // 
      // _colPosition
      // 
      this._colPosition.Text = "Позиция";
      this._colPosition.Width = 180;
      // 
      // _colDrawing
      // 
      this._colDrawing.Text = "Чертёж / статус";
      this._colDrawing.Width = 280;
      // 
      // _colPath
      // 
      this._colPath.Text = "Путь";
      this._colPath.Width = 300;
      // 
      // _options
      // 
      this._options.AutoSize = true;
      this._options.ColumnCount = 4;
      this._options.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._options.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 70F));
      this._options.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
      this._options.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._options.Controls.Add(this._lblCopies, 0, 0);
      this._options.Controls.Add(this._copiesBox, 1, 0);
      this._options.Controls.Add(this._lblPrinter, 2, 0);
      this._options.Controls.Add(this._printerBox, 3, 0);
      this._options.Dock = System.Windows.Forms.DockStyle.Fill;
      this._options.Location = new System.Drawing.Point(10, 391);
      this._options.Margin = new System.Windows.Forms.Padding(0, 8, 0, 4);
      this._options.Name = "_options";
      this._options.RowCount = 1;
      this._options.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._options.Size = new System.Drawing.Size(800, 27);
      this._options.TabIndex = 3;
      // 
      // _lblCopies
      // 
      this._lblCopies.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._lblCopies.AutoSize = true;
      this._lblCopies.Location = new System.Drawing.Point(0, 10);
      this._lblCopies.Margin = new System.Windows.Forms.Padding(0, 6, 6, 0);
      this._lblCopies.Name = "_lblCopies";
      this._lblCopies.Size = new System.Drawing.Size(41, 13);
      this._lblCopies.TabIndex = 0;
      this._lblCopies.Text = "Копии:";
      // 
      // _copiesBox
      // 
      this._copiesBox.Dock = System.Windows.Forms.DockStyle.Fill;
      this._copiesBox.Location = new System.Drawing.Point(50, 3);
      this._copiesBox.Maximum = new decimal(new int[] {
            99,
            0,
            0,
            0});
      this._copiesBox.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
      this._copiesBox.Name = "_copiesBox";
      this._copiesBox.Size = new System.Drawing.Size(64, 20);
      this._copiesBox.TabIndex = 1;
      this._copiesBox.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
      // 
      // _lblPrinter
      // 
      this._lblPrinter.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._lblPrinter.AutoSize = true;
      this._lblPrinter.Location = new System.Drawing.Point(129, 10);
      this._lblPrinter.Margin = new System.Windows.Forms.Padding(12, 6, 6, 0);
      this._lblPrinter.Name = "_lblPrinter";
      this._lblPrinter.Size = new System.Drawing.Size(53, 13);
      this._lblPrinter.TabIndex = 2;
      this._lblPrinter.Text = "Принтер:";
      // 
      // _printerBox
      // 
      this._printerBox.Dock = System.Windows.Forms.DockStyle.Fill;
      this._printerBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
      this._printerBox.FormattingEnabled = true;
      this._printerBox.Location = new System.Drawing.Point(191, 3);
      this._printerBox.Name = "_printerBox";
      this._printerBox.Size = new System.Drawing.Size(606, 21);
      this._printerBox.TabIndex = 3;
      // 
      // _bottom
      // 
      this._bottom.AutoSize = true;
      this._bottom.ColumnCount = 1;
      this._bottom.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this._bottom.Controls.Add(this._statusLabel, 0, 0);
      this._bottom.Controls.Add(this._progressBar, 0, 1);
      this._bottom.Dock = System.Windows.Forms.DockStyle.Fill;
      this._bottom.Location = new System.Drawing.Point(10, 422);
      this._bottom.Margin = new System.Windows.Forms.Padding(0);
      this._bottom.Name = "_bottom";
      this._bottom.RowCount = 2;
      this._bottom.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._bottom.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this._bottom.Size = new System.Drawing.Size(800, 44);
      this._bottom.TabIndex = 4;
      // 
      // _statusLabel
      // 
      this._statusLabel.Dock = System.Windows.Forms.DockStyle.Fill;
      this._statusLabel.Location = new System.Drawing.Point(0, 2);
      this._statusLabel.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
      this._statusLabel.Name = "_statusLabel";
      this._statusLabel.Size = new System.Drawing.Size(800, 18);
      this._statusLabel.TabIndex = 0;
      this._statusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // _progressBar
      // 
      this._progressBar.Dock = System.Windows.Forms.DockStyle.Fill;
      this._progressBar.Location = new System.Drawing.Point(0, 24);
      this._progressBar.Margin = new System.Windows.Forms.Padding(0, 2, 0, 4);
      this._progressBar.Name = "_progressBar";
      this._progressBar.Size = new System.Drawing.Size(800, 16);
      this._progressBar.TabIndex = 1;
      this._progressBar.Visible = false;
      // 
      // _buttonsPanel
      // 
      this._buttonsPanel.AutoSize = true;
      this._buttonsPanel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
      this._buttonsPanel.Controls.Add(this._btnClose);
      this._buttonsPanel.Controls.Add(this._btnPrint);
      this._buttonsPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
      this._buttonsPanel.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
      this._buttonsPanel.Location = new System.Drawing.Point(0, 476);
      this._buttonsPanel.Name = "_buttonsPanel";
      this._buttonsPanel.Padding = new System.Windows.Forms.Padding(10, 6, 10, 10);
      this._buttonsPanel.Size = new System.Drawing.Size(820, 44);
      this._buttonsPanel.TabIndex = 1;
      this._buttonsPanel.WrapContents = false;
      // 
      // _btnClose
      // 
      this._btnClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
      this._btnClose.Location = new System.Drawing.Point(700, 6);
      this._btnClose.Margin = new System.Windows.Forms.Padding(0);
      this._btnClose.Name = "_btnClose";
      this._btnClose.Size = new System.Drawing.Size(100, 28);
      this._btnClose.TabIndex = 1;
      this._btnClose.Text = "Закрыть";
      this._btnClose.UseVisualStyleBackColor = true;
      this._btnClose.Click += new System.EventHandler(this.OnCloseClick);
      // 
      // _btnPrint
      // 
      this._btnPrint.Location = new System.Drawing.Point(592, 6);
      this._btnPrint.Margin = new System.Windows.Forms.Padding(0, 0, 8, 0);
      this._btnPrint.Name = "_btnPrint";
      this._btnPrint.Size = new System.Drawing.Size(100, 28);
      this._btnPrint.TabIndex = 0;
      this._btnPrint.Text = "Печать";
      this._btnPrint.UseVisualStyleBackColor = true;
      this._btnPrint.Click += new System.EventHandler(this.OnPrintClick);
      // 
      // VelumAssemblyRegistryPrintDrawingsForm
      // 
      this.AcceptButton = this._btnPrint;
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.CancelButton = this._btnClose;
      this.ClientSize = new System.Drawing.Size(820, 520);
      this.Controls.Add(this._root);
      this.Controls.Add(this._buttonsPanel);
      this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
      this.MinimizeBox = false;
      this.MinimumSize = new System.Drawing.Size(640, 400);
      this.Name = "VelumAssemblyRegistryPrintDrawingsForm";
      this.ShowInTaskbar = false;
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
      this.Text = "Печать чертежей";
      this.Load += new System.EventHandler(this.OnFormLoadEnableListEvents);
      this._root.ResumeLayout(false);
      this._root.PerformLayout();
      this._filtersRow.ResumeLayout(false);
      this._filtersRow.PerformLayout();
      this._options.ResumeLayout(false);
      this._options.PerformLayout();
      ((System.ComponentModel.ISupportInitialize)(this._copiesBox)).EndInit();
      this._bottom.ResumeLayout(false);
      this._buttonsPanel.ResumeLayout(false);
      this.ResumeLayout(false);
      this.PerformLayout();

    }

    #endregion

    private System.Windows.Forms.TableLayoutPanel _root;
    private System.Windows.Forms.CheckBox _chkSelectAll;
    private System.Windows.Forms.TableLayoutPanel _filtersRow;
    private System.Windows.Forms.Label _lblPositionFilter;
    private System.Windows.Forms.TextBox _positionFilterBox;
    private System.Windows.Forms.Label _lblDrawingFilter;
    private System.Windows.Forms.TextBox _drawingFilterBox;
    private System.Windows.Forms.Button _btnFilterApply;
    private System.Windows.Forms.Button _btnFilterReset;
    private System.Windows.Forms.Button _btnFilterHelp;
    private System.Windows.Forms.ListView _list;
    private System.Windows.Forms.ColumnHeader _colPosition;
    private System.Windows.Forms.ColumnHeader _colDrawing;
    private System.Windows.Forms.ColumnHeader _colPath;
    private System.Windows.Forms.TableLayoutPanel _options;
    private System.Windows.Forms.Label _lblCopies;
    private System.Windows.Forms.NumericUpDown _copiesBox;
    private System.Windows.Forms.Label _lblPrinter;
    private System.Windows.Forms.ComboBox _printerBox;
    private System.Windows.Forms.TableLayoutPanel _bottom;
    private System.Windows.Forms.Label _statusLabel;
    private System.Windows.Forms.ProgressBar _progressBar;
    private System.Windows.Forms.FlowLayoutPanel _buttonsPanel;
    private System.Windows.Forms.Button _btnClose;
    private System.Windows.Forms.Button _btnPrint;
  }
}
