using System.Drawing;
using System.Windows.Forms;

namespace Velum.UI.AssemblyRegistry
{
  /// <summary>
  /// Форма редактирования списка отслеживаемых свойств (tracked properties)
  /// для зеркалирования BOM. Сохраняет список в bomTrackedProperties.json.
  /// </summary>
  internal sealed partial class VelumBomTrackedPropertiesEditForm
  {
    private System.ComponentModel.IContainer components = null;

    private System.Windows.Forms.DataGridView _dataGridView;
    private System.Windows.Forms.DataGridViewTextBoxColumn _colPropertyName;
    private System.Windows.Forms.ContextMenuStrip _contextMenuStrip;
    private System.Windows.Forms.ToolStripMenuItem _deleteMenuItem;
    private System.Windows.Forms.Button _okButton;
    private System.Windows.Forms.Button _cancelButton;

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
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VelumBomTrackedPropertiesEditForm));
      this._dataGridView = new System.Windows.Forms.DataGridView();
      this._colPropertyName = new System.Windows.Forms.DataGridViewTextBoxColumn();
      this._contextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
      this._deleteMenuItem = new System.Windows.Forms.ToolStripMenuItem();
      this._okButton = new System.Windows.Forms.Button();
      this._cancelButton = new System.Windows.Forms.Button();
      this.root = new System.Windows.Forms.TableLayoutPanel();
      this.descLabel = new System.Windows.Forms.Label();
      this.buttonsRow = new System.Windows.Forms.FlowLayoutPanel();
      this._contextMenuStrip.SuspendLayout();
      this.root.SuspendLayout();
      this.buttonsRow.SuspendLayout();
      this.SuspendLayout();
      // 
      // _dataGridView
      // 
      this._dataGridView.AllowUserToAddRows = false;
      this._dataGridView.AllowUserToDeleteRows = false;
      this._dataGridView.AllowUserToResizeRows = false;
      this._dataGridView.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
      this._dataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
      this._dataGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { this._colPropertyName });
      this._dataGridView.ContextMenuStrip = this._contextMenuStrip;
      this._dataGridView.Dock = System.Windows.Forms.DockStyle.Fill;
      this._dataGridView.Location = new System.Drawing.Point(12, 33);
      this._dataGridView.MultiSelect = false;
      this._dataGridView.Name = "_dataGridView";
      this._dataGridView.RowHeadersVisible = false;
      this._dataGridView.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
      this._dataGridView.Size = new System.Drawing.Size(425, 293);
      this._dataGridView.TabIndex = 1;
      this._dataGridView.KeyDown += new System.Windows.Forms.KeyEventHandler(this.OnDataGridViewKeyDown);
      // 
      // _colPropertyName
      // 
      this._colPropertyName.HeaderText = "Свойство";
      this._colPropertyName.Name = "_colPropertyName";
      this._colPropertyName.ReadOnly = false;
      this._colPropertyName.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
      // 
      // _contextMenuStrip
      // 
      this._contextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { this._deleteMenuItem });
      this._contextMenuStrip.Name = "contextMenuStrip";
      this._contextMenuStrip.Size = new System.Drawing.Size(106, 26);
      // 
      // _deleteMenuItem
      // 
      this._deleteMenuItem.Name = "_deleteMenuItem";
      this._deleteMenuItem.Size = new System.Drawing.Size(105, 22);
      this._deleteMenuItem.Text = "Удалить";
      this._deleteMenuItem.Click += new System.EventHandler(this.OnDeleteClick);
      // 
      // _okButton
      // 
      this._okButton.AutoSize = true;
      this._okButton.Location = new System.Drawing.Point(266, 3);
      this._okButton.Name = "_okButton";
      this._okButton.Size = new System.Drawing.Size(75, 23);
      this._okButton.TabIndex = 1;
      this._okButton.Text = "OK";
      this._okButton.UseVisualStyleBackColor = true;
      this._okButton.Click += new System.EventHandler(this.OnOkClick);
      // 
      // _cancelButton
      // 
      this._cancelButton.AutoSize = true;
      this._cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
      this._cancelButton.Location = new System.Drawing.Point(347, 3);
      this._cancelButton.Name = "_cancelButton";
      this._cancelButton.Size = new System.Drawing.Size(75, 23);
      this._cancelButton.TabIndex = 0;
      this._cancelButton.Text = "Отмена";
      this._cancelButton.UseVisualStyleBackColor = true;
      // 
      // root
      // 
      this.root.ColumnCount = 1;
      this.root.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this.root.Controls.Add(this.descLabel, 0, 0);
      this.root.Controls.Add(this._dataGridView, 0, 1);
      this.root.Controls.Add(this.buttonsRow, 0, 2);
      this.root.Dock = System.Windows.Forms.DockStyle.Fill;
      this.root.Location = new System.Drawing.Point(0, 0);
      this.root.Name = "root";
      this.root.Padding = new System.Windows.Forms.Padding(12);
      this.root.RowCount = 3;
      this.root.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this.root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
      this.root.RowStyles.Add(new System.Windows.Forms.RowStyle());
      this.root.Size = new System.Drawing.Size(449, 380);
      this.root.TabIndex = 0;
      // 
      // descLabel
      // 
      this.descLabel.AutoSize = true;
      this.descLabel.Dock = System.Windows.Forms.DockStyle.Fill;
      this.descLabel.Location = new System.Drawing.Point(12, 12);
      this.descLabel.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
      this.descLabel.Name = "descLabel";
      this.descLabel.Size = new System.Drawing.Size(425, 13);
      this.descLabel.TabIndex = 0;
      this.descLabel.Text = "Свойства компонентов, изменения которых отслеживаются и выгружаются в 1C:";
      // 
      // buttonsRow
      // 
      this.buttonsRow.AutoSize = true;
      this.buttonsRow.Controls.Add(this._cancelButton);
      this.buttonsRow.Controls.Add(this._okButton);
      this.buttonsRow.Dock = System.Windows.Forms.DockStyle.Fill;
      this.buttonsRow.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
      this.buttonsRow.Location = new System.Drawing.Point(12, 336);
      this.buttonsRow.Margin = new System.Windows.Forms.Padding(0);
      this.buttonsRow.Name = "buttonsRow";
      this.buttonsRow.Size = new System.Drawing.Size(425, 29);
      this.buttonsRow.TabIndex = 4;
      this.buttonsRow.WrapContents = false;
      // 
      // VelumBomTrackedPropertiesEditForm
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.ClientSize = new System.Drawing.Size(449, 380);
      this.Controls.Add(this.root);
      this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
      this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
      this.MaximizeBox = false;
      this.MinimizeBox = false;
      this.Name = "VelumBomTrackedPropertiesEditForm";
      this.ShowInTaskbar = false;
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
      this.Text = "Отслеживаемые свойства BOM";
      this._contextMenuStrip.ResumeLayout(false);
      this.root.ResumeLayout(false);
      this.root.PerformLayout();
      this.buttonsRow.ResumeLayout(false);
      this.buttonsRow.PerformLayout();
      this.ResumeLayout(false);

    }

    #endregion

    private TableLayoutPanel root;
    private Label descLabel;
    private FlowLayoutPanel buttonsRow;
  }
}
