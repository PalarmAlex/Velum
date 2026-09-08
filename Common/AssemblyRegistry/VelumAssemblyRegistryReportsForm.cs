using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Velum.UI.AssemblyRegistry;

namespace Velum.UI
{
  /// <summary>Просмотр и удаление сохранённых HTML-отчётов реестра изделия.</summary>
  internal sealed partial class VelumAssemblyRegistryReportsForm : Form
  {
    private readonly List<string> _allFiles = new List<string>();
    private readonly string _folderPath;
    private readonly string _filePrefix;
    private ContextMenuStrip _listMenu;
    private ToolStripMenuItem _menuOpen;
    private ToolStripMenuItem _menuDelete;
    private ToolStripMenuItem _menuSelectAll;

    /// <summary>Конструктор по умолчанию (реестр изделия).</summary>
    public VelumAssemblyRegistryReportsForm()
        : this(null, null)
    {
    }

    /// <summary>Конструктор с указанием каталога и префикса имён файлов для фильтрации.</summary>
    /// <param name="folderPath">Каталог отчётов. Если null — используется путь по умолчанию.</param>
    /// <param name="filePrefix">Префикс имён файлов для фильтрации. Если null — фильтры нет.</param>
    public VelumAssemblyRegistryReportsForm(string folderPath, string filePrefix)
    {
      _folderPath = folderPath;
      _filePrefix = filePrefix;
      InitializeComponent();
      VelumFormHelp.Bind(this, VelumHelpTopics.AssemblyReports);
      Icon icon = TryLoadFormIcon();
      if (icon != null)
        Icon = icon;
      SetupContextMenu();
      var tip = new ToolTip();
      tip.SetToolTip(_btnClose, "Закрыть");

      // Установка заголовка формы в зависимости от контекста.
      Text = string.IsNullOrEmpty(_filePrefix)
          ? "Отчёты реестра изделия"
          : "Отчёты реестра документов";

      ReloadFileList();
    }

    private void SetupContextMenu()
    {
      _listMenu = new ContextMenuStrip();
      _menuOpen = AddMenuItem(_listMenu, "Открыть", "Yes.png", OnOpenSelected);
      _menuDelete = AddMenuItem(_listMenu, "Удалить", "Delete.png", OnDeleteSelected);
      _menuSelectAll = AddMenuItem(_listMenu, "Выбрать все", "editselectall.png", OnSelectAll);
      _listMenu.Opening += OnMenuOpening;
      _list.ContextMenuStrip = _listMenu;
    }

    private static ToolStripMenuItem AddMenuItem(
        ContextMenuStrip menu,
        string text,
        string iconFile,
        EventHandler click)
    {
      var item = new ToolStripMenuItem(text, TryLoadMenuBitmap(iconFile));
      item.Click += click;
      menu.Items.Add(item);
      return item;
    }

    private void OnMenuOpening(object sender, System.ComponentModel.CancelEventArgs e)
    {
      bool hasItems = _list.Items.Count > 0;
      bool hasSelection = _list.SelectedItems.Count > 0;
      _menuOpen.Enabled = hasSelection;
      _menuDelete.Enabled = hasSelection;
      _menuSelectAll.Enabled = hasItems;
    }

    private void ReloadFileList()
    {
      var keepNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (object item in _list.SelectedItems)
      {
        string name = item as string;
        if (!string.IsNullOrEmpty(name))
          keepNames.Add(name);
      }

      _allFiles.Clear();
      _list.Items.Clear();

      string folder = _folderPath ?? VelumAssemblyRegistryReportHtmlBuilder.ReportsFolderPath;
      try
      {
        Directory.CreateDirectory(folder);
        if (Directory.Exists(folder))
        {
          string[] files = Directory.GetFiles(folder, "*.html");
          Array.Sort(files, (a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
          for (int i = 0; i < files.Length; i++)
            _allFiles.Add(files[i]);
        }
      }
      catch (Exception ex)
      {
        MessageBox.Show(
            this,
            "Не удалось прочитать каталог отчётов:\n" + ex.Message,
            Text,
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
      }

      ApplyNameFilter(keepNames);
    }

    private void ApplyNameFilter(HashSet<string> preferSelectNames)
    {
      string filter = (_txtFilter.Text ?? string.Empty).Trim();
      _list.BeginUpdate();
      try
      {
        _list.Items.Clear();
        for (int i = 0; i < _allFiles.Count; i++)
        {
          string path = _allFiles[i];
          string name = Path.GetFileName(path) ?? path;

          // Фильтр по префиксу имён файлов (для реестра документов).
          if (!string.IsNullOrEmpty(_filePrefix) && !name.StartsWith(_filePrefix, StringComparison.OrdinalIgnoreCase))
            continue;

          if (!string.IsNullOrEmpty(filter) &&
              name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
            continue;

          int index = _list.Items.Add(name);
          if (preferSelectNames != null && preferSelectNames.Contains(name))
            _list.SetSelected(index, true);
        }
      }
      finally
      {
        _list.EndUpdate();
      }
    }

    private List<string> ResolveSelectedPaths()
    {
      var paths = new List<string>();
      foreach (object item in _list.SelectedItems)
      {
        string name = item as string;
        string path = ResolveFullPath(name);
        if (!string.IsNullOrEmpty(path))
          paths.Add(path);
      }

      return paths;
    }

    private string ResolveFullPath(string fileName)
    {
      if (string.IsNullOrWhiteSpace(fileName))
        return null;

      for (int i = 0; i < _allFiles.Count; i++)
      {
        string path = _allFiles[i];
        if (string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase))
          return path;
      }

      return null;
    }

    private void OnFilterChanged(object sender, EventArgs e)
    {
      var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (object item in _list.SelectedItems)
      {
        string name = item as string;
        if (!string.IsNullOrEmpty(name))
          keep.Add(name);
      }

      ApplyNameFilter(keep);
    }

    private void OnOpenSelected(object sender, EventArgs e)
    {
      OpenPaths(ResolveSelectedPaths());
    }

    private void OnDeleteSelected(object sender, EventArgs e)
    {
      DeletePaths(ResolveSelectedPaths());
    }

    private void OnSelectAll(object sender, EventArgs e)
    {
      if (_list.Items.Count == 0)
        return;

      _list.BeginUpdate();
      try
      {
        for (int i = 0; i < _list.Items.Count; i++)
          _list.SetSelected(i, true);
      }
      finally
      {
        _list.EndUpdate();
      }
    }

    private void OnListDoubleClick(object sender, EventArgs e)
    {
      Point client = _list.PointToClient(Cursor.Position);
      int index = _list.IndexFromPoint(client);
      if (index < 0 || index >= _list.Items.Count)
        return;

      string name = _list.Items[index] as string;
      string path = ResolveFullPath(name);
      if (string.IsNullOrEmpty(path))
        return;

      OpenPaths(new List<string> { path });
    }

    private void OnListKeyDown(object sender, KeyEventArgs e)
    {
      if (e.KeyCode != Keys.Delete)
        return;

      // Delete — для одной выделенной строки; несколько — через контекстное меню.
      if (_list.SelectedItems.Count != 1)
        return;

      DeletePaths(ResolveSelectedPaths());
      e.Handled = true;
      e.SuppressKeyPress = true;
    }

    private void OpenPaths(IList<string> paths)
    {
      if (paths == null || paths.Count == 0)
        return;

      var errors = new StringBuilder();
      for (int i = 0; i < paths.Count; i++)
      {
        try
        {
          Process.Start(new ProcessStartInfo
          {
            FileName = paths[i],
            UseShellExecute = true
          });
        }
        catch (Exception ex)
        {
          if (errors.Length > 0)
            errors.AppendLine();
          errors.Append(Path.GetFileName(paths[i])).Append(": ").Append(ex.Message);
        }
      }

      if (errors.Length > 0)
      {
        MessageBox.Show(
            this,
            "Не удалось открыть:\n" + errors,
            Text,
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
      }
    }

    private void DeletePaths(IList<string> paths)
    {
      if (paths == null || paths.Count == 0)
        return;

      string message;
      if (paths.Count == 1)
        message = "Удалить отчёт?\n" + Path.GetFileName(paths[0]);
      else
        message = "Удалить выбранные отчёты (" + paths.Count + ")?";

      DialogResult confirm = MessageBox.Show(
          this,
          message,
          Text,
          MessageBoxButtons.YesNo,
          MessageBoxIcon.Question,
          MessageBoxDefaultButton.Button2);
      if (confirm != DialogResult.Yes)
        return;

      var errors = new StringBuilder();
      for (int i = 0; i < paths.Count; i++)
      {
        try
        {
          File.Delete(paths[i]);
        }
        catch (Exception ex)
        {
          if (errors.Length > 0)
            errors.AppendLine();
          errors.Append(Path.GetFileName(paths[i])).Append(": ").Append(ex.Message);
        }
      }

      if (errors.Length > 0)
      {
        MessageBox.Show(
            this,
            "Не удалось удалить (возможно, файл открыт в браузере):\n" + errors,
            Text,
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
      }

      ReloadFileList();
    }

    private void OnCloseClick(object sender, EventArgs e)
    {
      Close();
    }

    private static Icon TryLoadFormIcon()
    {
      try
      {
        string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (string.IsNullOrEmpty(dir))
          return null;
        string path = Path.Combine(dir, "icons", "velum.ico");
        return File.Exists(path) ? new Icon(path) : null;
      }
      catch
      {
        return null;
      }
    }

    private static Image TryLoadMenuBitmap(string fileName)
    {
      try
      {
        string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string path = Path.Combine(dir ?? string.Empty, "icons", fileName);
        if (!File.Exists(path))
          return null;

        if (fileName != null &&
            fileName.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
        {
          using (var icon = new Icon(path))
            return icon.ToBitmap();
        }

        using (var loaded = new Bitmap(path))
          return new Bitmap(loaded);
      }
      catch
      {
        return null;
      }
    }
  }
}
