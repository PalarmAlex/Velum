using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Velum.UI.ProductRegistry;

namespace Velum.UI
{
  /// <summary>Редактор записи реестра изделий.</summary>
  internal sealed partial class VelumProductRegistryItemForm : Form
  {
    private readonly VelumProductItem _item;
    private readonly VelumProductRegistryStore _store;
    private readonly ToolTip _pathToolTip;
    private int _folderId;

    public VelumProductItem ResultItem { get; private set; }

    public VelumProductRegistryItemForm()
    {
      InitializeComponent();
      VelumFormHelp.Bind(this, VelumHelpTopics.ProductItem);
      _pathToolTip = new ToolTip();
      VelumBatchFormFolderBootstrap.BindFolderPathTooltip(_folderPathBox, _pathToolTip);
      VelumBatchFormFolderBootstrap.BindFolderPathTooltip(_filePathBox, _pathToolTip);
      _pathToolTip.SetToolTip(_btnBrowseFolder, "Выбрать каталог реестра для записи");
      _pathToolTip.SetToolTip(_btnBrowseFile, "Выбрать файл изделия на диске");
      _pathToolTip.SetToolTip(_btnClose, "Закрыть без сохранения");
      _pathToolTip.SetToolTip(_btnApply, "Сохранить изменения записи");
    }

    public VelumProductRegistryItemForm(VelumProductItem item, bool isNew, VelumProductRegistryStore store)
        : this()
    {
      _item = item ?? throw new ArgumentNullException(nameof(item));
      _store = store ?? throw new ArgumentNullException(nameof(store));
      _folderId = item.FolderId;

      Icon icon = TryLoadFormIcon();
      if (icon != null)
        Icon = icon;

      Text = isNew ? "Новое изделие" : "Редактирование изделия";
      _idBox.Text = item.Id > 0 ? item.Id.ToString() : "(авто)";
      _designationBox.Text = item.Designation ?? string.Empty;
      _nameBox.Text = item.Name ?? string.Empty;
      _filePathBox.Text = item.FilePath ?? string.Empty;
      RefreshFolderPath();
    }

    private void RefreshFolderPath()
    {
      _folderPathBox.Text = _store.GetFolderPath(_folderId);
    }

    private void OnBrowseFolder(object sender, EventArgs e)
    {
      using (var form = new VelumProductRegistryFolderPickerForm(_store, _folderId))
      {
        if (form.ShowDialog(this) != DialogResult.OK || form.SelectedFolderId == null)
          return;

        int selectedId = form.SelectedFolderId.Value;
        if (selectedId == _folderId)
        {
          MessageBox.Show(
              this,
              "Запись уже привязана к выбранному каталогу.",
              "Реестр документов",
              MessageBoxButtons.OK,
              MessageBoxIcon.Information);
          return;
        }

        string path = _store.GetFolderPath(selectedId);
        DialogResult confirm = MessageBox.Show(
            this,
            "Переназначить привязку записи к каталогу «" + path + "»?",
            "Реестр документов",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);
        if (confirm != DialogResult.Yes)
          return;

        _folderId = selectedId;
        RefreshFolderPath();
      }
    }

    private void OnBrowseFile(object sender, EventArgs e)
    {
      using (var dialog = new OpenFileDialog())
      {
        dialog.Title = "Выбор файла изделия";
        dialog.Filter = "Все файлы (*.*)|*.*|SolidWorks (*.sldprt;*.sldasm;*.slddrw)|*.sldprt;*.sldasm;*.slddrw";
        dialog.CheckFileExists = true;
        dialog.Multiselect = false;

        // Ключ FilePath хранится относительным корню документов — достраиваем до полного.
        string current = Velum.ReactiveCore.Export.VelumRelativeDocumentPathResolver.ToFull(
            (_filePathBox.Text ?? string.Empty).Trim());
        if (!string.IsNullOrEmpty(current))
        {
          try
          {
            string dir = Path.GetDirectoryName(current);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
              dialog.InitialDirectory = dir;
            if (File.Exists(current))
              dialog.FileName = current;
          }
          catch
          {
          }
        }

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
          _filePathBox.Text = dialog.FileName;
          if (string.IsNullOrWhiteSpace(_designationBox.Text))
            _designationBox.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
        }
      }
    }

    private void OnApply(object sender, EventArgs e)
    {
      string designation = (_designationBox.Text ?? string.Empty).Trim();
      string filePath = (_filePathBox.Text ?? string.Empty).Trim();

      string pathKey = VelumProductRegistryStore.NormalizeFilePathKey(filePath);
      if (!string.IsNullOrEmpty(pathKey))
      {
        VelumProductItem pathConflict = _store.FindItemByFilePath(pathKey);
        if (pathConflict != null && pathConflict.Id != _item.Id)
        {
          MessageBox.Show(
              this,
              "Файл уже есть в реестре документов (Id=" + pathConflict.Id + "):\n" + pathKey
              + "\n\nКлюч учёта — полный путь к файлу; одна запись на файл.",
              "Реестр документов",
              MessageBoxButtons.OK,
              MessageBoxIcon.Warning);
          _filePathBox.Focus();
          return;
        }
      }

      // Конфликт ключа уникальности «обозначение + расширение» — предпроверка до
      // сохранения; исключаем собственную редактируемую запись (_item.Id).
      if (!string.IsNullOrEmpty(designation))
      {
        VelumProductItem desConflict = _store.FindItemByDesignationKey(
            designation, filePath, excludeItemId: _item.Id);
        if (desConflict != null)
        {
          MessageBox.Show(
              this,
              VelumProductRegistryStore.BuildDesignationKeyConflictMessage(desConflict),
              "Реестр документов",
              MessageBoxButtons.OK,
              MessageBoxIcon.Warning);
          _designationBox.Focus();
          return;
        }
      }

      ResultItem = new VelumProductItem
      {
        Id = _item.Id,
        FolderId = _folderId,
        Designation = designation,
        Name = (_nameBox.Text ?? string.Empty).Trim(),
        FilePath = filePath,
        NeedDrawing = _item.NeedDrawing
      };
      VelumProductRegistryExportMetaCopy.CopyMirrorFields(_item, ResultItem);
      DialogResult = DialogResult.OK;
      Close();
    }

    private void OnCloseClick(object sender, EventArgs e)
    {
      DialogResult = DialogResult.Cancel;
      Close();
    }

    private static Icon TryLoadFormIcon()
    {
      return VelumFormIcon.TryLoad();
    }
  }
}
