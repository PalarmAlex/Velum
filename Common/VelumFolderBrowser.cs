using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Velum.UI
{
  /// <summary>
  /// Выбор существующего каталога. Заменяет <see cref="FolderBrowserDialog"/>,
  /// у которого оболочка Windows всё равно даёт New Folder / Rename / Cut через
  /// дерево и контекстное меню. Предоставляет встроенную кнопку создания каталога.
  /// </summary>
  internal static class VelumFolderBrowser
  {
    /// <summary>
    /// Показывает диалог выбора каталога. Возвращает true при подтверждении.
    /// </summary>
    internal static bool TrySelect(
        IWin32Window owner,
        string description,
        string initialPath,
        out string selectedPath)
    {
      using (var form = new SelectOnlyFolderForm(description, initialPath))
      {
        if (form.ShowDialog(owner) == DialogResult.OK)
        {
          selectedPath = form.SelectedPath;
          return true;
        }
      }

      selectedPath = null;
      return false;
    }

    private sealed class SelectOnlyFolderForm : Form
    {
      private readonly TreeView _tree;
      private readonly TextBox _pathBox;
      private readonly Button _okButton;
      private readonly string _initialPath;
      private string _selectedPath;
      private bool _initialSelectionApplied;

      internal string SelectedPath
      {
        get { return _selectedPath; }
      }

      internal SelectOnlyFolderForm(string description, string initialPath)
      {
        Text = "Выбор каталога";
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(480, 320);
        ClientSize = new Size(480, 320);
        Font = SystemFonts.MessageBoxFont;
        VelumFormIcon.Apply(this);
        _initialPath = initialPath;

        var root = new TableLayoutPanel
        {
          Dock = DockStyle.Fill,
          ColumnCount = 1,
          RowCount = 4,
          Padding = new Padding(10),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var descriptionLabel = new Label
        {
          AutoSize = true,
          Dock = DockStyle.Fill,
          Margin = new Padding(0, 0, 0, 8),
          Text = string.IsNullOrWhiteSpace(description) ? "Выберите каталог:" : description.Trim(),
        };

        _tree = new TreeView
        {
          Dock = DockStyle.Fill,
          HideSelection = false,
          LabelEdit = false,
          AllowDrop = false,
          HotTracking = false,
          ShowLines = true,
          ShowPlusMinus = true,
          ShowRootLines = true,
          PathSeparator = Path.DirectorySeparatorChar.ToString(),
        };
        _tree.BeforeExpand += OnBeforeExpand;
        _tree.AfterSelect += OnAfterSelect;
        _tree.NodeMouseDoubleClick += OnNodeMouseDoubleClick;
        _tree.KeyDown += OnTreeKeyDown;

        _pathBox = new TextBox
        {
          Dock = DockStyle.Fill,
          Margin = new Padding(0, 8, 0, 8),
        };
        _pathBox.TextChanged += OnPathBoxTextChanged;
        _pathBox.KeyDown += OnPathBoxKeyDown;

        var createButton = new Button
        {
          Text = "Создать каталог",
          AutoSize = true,
          Margin = new Padding(0),
          FlatStyle = FlatStyle.Standard,
        };
        createButton.Click += OnCreateFolderClick;

        var cancelButton = new Button
        {
          Text = "Отмена",
          DialogResult = DialogResult.Cancel,
          AutoSize = true,
          Margin = new Padding(8, 0, 0, 0),
        };

        _okButton = new Button
        {
          Text = "OK",
          DialogResult = DialogResult.None,
          AutoSize = true,
          Enabled = false,
          Margin = new Padding(0),
        };
        _okButton.Click += OnOkClick;

        var tip = new ToolTip();
        tip.SetToolTip(_okButton, "Выбрать каталог");
        tip.SetToolTip(cancelButton, "Отменить выбор");
        tip.SetToolTip(createButton, "Создать новый каталог в выбранной папке");

        var buttonsLeft = new FlowLayoutPanel
        {
          FlowDirection = FlowDirection.LeftToRight,
          WrapContents = false,
          AutoSize = true,
          Margin = new Padding(0),
          Padding = new Padding(0),
          Anchor = AnchorStyles.Left,
        };
        buttonsLeft.Controls.Add(createButton);

        var buttonsRight = new FlowLayoutPanel
        {
          FlowDirection = FlowDirection.RightToLeft,
          WrapContents = false,
          AutoSize = true,
          Margin = new Padding(0),
          Padding = new Padding(0),
          Anchor = AnchorStyles.Right,
        };
        buttonsRight.Controls.Add(cancelButton);
        buttonsRight.Controls.Add(_okButton);

        var buttonsRow = new TableLayoutPanel
        {
          Dock = DockStyle.Fill,
          ColumnCount = 2,
          RowCount = 1,
          AutoSize = true,
          Margin = new Padding(0),
        };
        buttonsRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        buttonsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        buttonsRow.Controls.Add(buttonsLeft, 0, 0);
        buttonsRow.Controls.Add(buttonsRight, 1, 0);

        root.Controls.Add(descriptionLabel, 0, 0);
        root.Controls.Add(_tree, 0, 1);
        root.Controls.Add(_pathBox, 0, 2);
        root.Controls.Add(buttonsRow, 0, 3);

        Controls.Add(root);
        AcceptButton = _okButton;
        CancelButton = cancelButton;

        PopulateRoots();
        // Выбор начального каталога — после показа формы и повторно через BeginInvoke:
        // до полной раскладки Expand/EnsureVisible часто не срабатывают.
        Shown += OnShownSelectInitialPath;
      }

      private void OnShownSelectInitialPath(object sender, EventArgs e)
      {
        if (_initialSelectionApplied)
          return;

        _initialSelectionApplied = true;
        TrySelectInitialPath(_initialPath);
        BeginInvoke(new Action(() => TrySelectInitialPath(_initialPath)));
      }

      private void PopulateRoots()
      {
        _tree.BeginUpdate();
        try
        {
          _tree.Nodes.Clear();
          TryAddDesktopRootNode();
          foreach (DriveInfo drive in DriveInfo.GetDrives())
          {
            if (!drive.IsReady)
              continue;

            string rootPath = drive.RootDirectory.FullName;
            TreeNode node = CreateFolderNode(rootPath, FormatDriveCaption(drive));
            _tree.Nodes.Add(node);
          }
        }
        catch
        {
          // ignore inaccessible drives enumeration
        }
        finally
        {
          _tree.EndUpdate();
        }
      }

      private void TryAddDesktopRootNode()
      {
        try
        {
          string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
          if (string.IsNullOrWhiteSpace(desktopPath) || !Directory.Exists(desktopPath))
            return;

          TreeNode node = CreateFolderNode(desktopPath, "Рабочий стол");
          _tree.Nodes.Add(node);
        }
        catch
        {
          // ignore
        }
      }

      private static string FormatDriveCaption(DriveInfo drive)
      {
        string root = drive.RootDirectory.FullName.TrimEnd('\\', '/');
        string label = null;
        try
        {
          label = drive.VolumeLabel;
        }
        catch
        {
          // ignore
        }

        if (string.IsNullOrWhiteSpace(label))
          return root;
        return label.Trim() + " (" + root + ")";
      }

      private static TreeNode CreateFolderNode(string fullPath, string caption)
      {
        var node = new TreeNode(caption ?? GetFolderCaption(fullPath))
        {
          Tag = fullPath,
          Name = fullPath,
        };

        // Заглушка: раскрытие подгрузит дочерние каталоги.
        try
        {
          if (Directory.Exists(fullPath) && HasAnySubdirectory(fullPath))
            node.Nodes.Add(new TreeNode());
        }
        catch
        {
          // ignore access errors
        }

        return node;
      }

      private static string GetFolderCaption(string fullPath)
      {
        if (string.IsNullOrEmpty(fullPath))
          return fullPath;

        string name = Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.IsNullOrEmpty(name) ? fullPath : name;
      }

      private static bool HasAnySubdirectory(string fullPath)
      {
        try
        {
          using (var e = Directory.EnumerateDirectories(fullPath).GetEnumerator())
            return e.MoveNext();
        }
        catch
        {
          return false;
        }
      }

      private void OnBeforeExpand(object sender, TreeViewCancelEventArgs e)
      {
        TreeNode node = e.Node;
        if (node == null)
          return;

        string path = node.Tag as string;
        if (string.IsNullOrEmpty(path))
          return;

        // Уже загружено (нет единственной пустой заглушки).
        if (node.Nodes.Count != 1 || node.Nodes[0].Tag != null)
          return;

        node.Nodes.Clear();
        try
        {
          foreach (string dir in Directory.EnumerateDirectories(path))
          {
            try
            {
              var attr = File.GetAttributes(dir);
              if ((attr & FileAttributes.Hidden) != 0 || (attr & FileAttributes.System) != 0)
                continue;
            }
            catch
            {
              // include if attributes unavailable
            }

            node.Nodes.Add(CreateFolderNode(dir, null));
          }
        }
        catch
        {
          // leave empty on access denied
        }
      }

      private void OnAfterSelect(object sender, TreeViewEventArgs e)
      {
        string path = e.Node != null ? e.Node.Tag as string : null;
        SetSelectedPath(path, updatePathBox: true);
      }

      private void OnPathBoxTextChanged(object sender, EventArgs e)
      {
        string typed = (_pathBox.Text ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(typed) && Directory.Exists(typed))
        {
          _selectedPath = typed;
          _okButton.Enabled = true;
        }
        else
        {
          _selectedPath = null;
          _okButton.Enabled = false;
        }
      }

      private void OnPathBoxKeyDown(object sender, KeyEventArgs e)
      {
        if (e.KeyCode != Keys.Enter)
          return;

        e.SuppressKeyPress = true;
        e.Handled = true;
        if (_okButton.Enabled)
          OnOkClick(sender, EventArgs.Empty);
      }

      private void OnNodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
      {
        if (e.Node == null || e.Button != MouseButtons.Left)
          return;

        if (e.Node.Nodes.Count > 0 && !e.Node.IsExpanded)
        {
          e.Node.Expand();
          return;
        }

        if (_okButton.Enabled)
          OnOkClick(sender, EventArgs.Empty);
      }

      private void OnTreeKeyDown(object sender, KeyEventArgs e)
      {
        // Блокируем типичные действия оболочки/редактирования.
        if (e.KeyCode == Keys.F2 ||
            (e.Control && (e.KeyCode == Keys.X || e.KeyCode == Keys.V || e.KeyCode == Keys.C)) ||
            e.KeyCode == Keys.Delete)
        {
          e.SuppressKeyPress = true;
          e.Handled = true;
        }
      }

      /// <summary>
      /// Обработчик нажатия кнопки «Создать каталог».
      /// </summary>
      private void OnCreateFolderClick(object sender, EventArgs e)
      {
        string parentPath = _tree.SelectedNode?.Tag as string;
        if (string.IsNullOrEmpty(parentPath) || !Directory.Exists(parentPath))
        {
          MessageBox.Show(
              this,
              "Сначала выберите каталог, в котором будет создан новый каталог.",
              Text,
              MessageBoxButtons.OK,
              MessageBoxIcon.Warning);
          return;
        }

        using (var dlg = new FolderNameDialog())
        {
          dlg.InitialName = "Новая папка";
          if (dlg.ShowDialog(this) != DialogResult.OK)
            return;

          string newName = dlg.FolderName;
          string fullPath = Path.Combine(parentPath, newName);

          try
          {
            Directory.CreateDirectory(fullPath);
          }
          catch (UnauthorizedAccessException)
          {
            MessageBox.Show(
                this,
                "Отказано в доступе при создании каталога «" + newName + "».",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
          }
          catch (IOException ex)
          {
            MessageBox.Show(
                this,
                "Не удалось создать каталог «" + newName + "»: " + ex.Message,
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
          }

          // Обновляем дерево — добавляем узел и выделяем его.
          EnsureChildrenLoaded(_tree.SelectedNode);
          TreeNode newNode = AddNodeToTree(_tree.SelectedNode, fullPath, newName);
          if (newNode != null)
          {
            _tree.SelectedNode = newNode;
            newNode.EnsureVisible();
          }
        }
      }

      /// <summary>
      /// Добавляет узел в дерево, раскрывает родителя и возвращает созданный узел.
      /// </summary>
      private TreeNode AddNodeToTree(TreeNode parentNode, string fullPath, string caption)
      {
        if (parentNode == null)
          return null;

        TreeNode newNode = CreateFolderNode(fullPath, caption ?? GetFolderCaption(fullPath));
        parentNode.Nodes.Add(newNode);
        parentNode.Expand();
        return newNode;
      }

      /// <summary>
      /// Диалог ввода имени нового каталога.
      /// </summary>
      private sealed class FolderNameDialog : Form
      {
        private readonly TextBox _nameBox;

        /// <summary>
        /// Введённое имя каталога.
        /// </summary>
        internal string FolderName
        {
          get { return (_nameBox.Text ?? string.Empty).Trim(); }
        }

        /// <summary>
        /// Имя по умолчанию при открытии диалога.
        /// </summary>
        internal string InitialName
        {
          set { _nameBox.Text = value; }
        }

        internal FolderNameDialog()
        {
          Text = "Создание каталога";
          FormBorderStyle = FormBorderStyle.FixedDialog;
          MinimizeBox = false;
          MaximizeBox = false;
          ShowInTaskbar = false;
          StartPosition = FormStartPosition.CenterParent;
          SizeGripStyle = SizeGripStyle.Hide;
          Font = SystemFonts.MessageBoxFont;
          VelumFormIcon.Apply(this);
          ClientSize = new Size(320, 100);

          var root = new TableLayoutPanel
          {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12),
          };
          root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
          root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
          root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

          var label = new Label
          {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 6),
            Text = "Введите имя нового каталога:",
          };

          _nameBox = new TextBox
          {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 8),
          };
          _nameBox.KeyDown += OnNameBoxKeyDown;

          var btnRow = new FlowLayoutPanel
          {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0),
          };

          var cancelBtn = new Button
          {
            Text = "Отмена",
            DialogResult = DialogResult.Cancel,
            AutoSize = true,
            Margin = new Padding(8, 0, 0, 0),
          };

          var okBtn = new Button
          {
            Text = "OK",
            DialogResult = DialogResult.OK,
            AutoSize = true,
            Margin = new Padding(0),
          };
          okBtn.Click += OnOkClick;

          btnRow.Controls.Add(cancelBtn);
          btnRow.Controls.Add(okBtn);

          root.Controls.Add(label, 0, 0);
          root.Controls.Add(_nameBox, 0, 1);
          root.Controls.Add(btnRow, 0, 2);

          Controls.Add(root);
          AcceptButton = okBtn;
          CancelButton = cancelBtn;
        }

        private void OnNameBoxKeyDown(object sender, KeyEventArgs e)
        {
          if (e.KeyCode == Keys.Enter)
          {
            e.SuppressKeyPress = true;
            e.Handled = true;
          }
        }

        private void OnOkClick(object sender, EventArgs e)
        {
          string name = FolderName;
          if (string.IsNullOrEmpty(name))
          {
            MessageBox.Show(
                this,
                "Введите имя каталога.",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            _nameBox.Focus();
            return;
          }

          // Проверка допустимых символов.
          char[] invalid = Path.GetInvalidPathChars();
          if (name.IndexOfAny(invalid) >= 0)
          {
            MessageBox.Show(
                this,
                "Имя каталога содержит недопустимые символы.",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            _nameBox.Focus();
            return;
          }

          // Проверка на зарезервированные имена.
          string[] reserved = { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4",
                               "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2",
                               "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
          string baseName = Path.GetFileName(name.TrimEnd('.', ' '));
          if (reserved.Contains(baseName.ToUpperInvariant()))
          {
            MessageBox.Show(
                this,
                "Имя «" + name + "» является зарезервированным для файловой системы.",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            _nameBox.Focus();
            return;
          }

          // Закрываем форму — результат будет в FolderName.
          Close();
        }
      }

      private void SetSelectedPath(string path, bool updatePathBox)
      {
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
        {
          _selectedPath = path;
          if (updatePathBox && !string.Equals(_pathBox.Text, path, StringComparison.OrdinalIgnoreCase))
            _pathBox.Text = path;
          _okButton.Enabled = true;
        }
        else
        {
          _selectedPath = null;
          if (updatePathBox)
            _pathBox.Text = string.Empty;
          _okButton.Enabled = false;
        }
      }

      private void OnOkClick(object sender, EventArgs e)
      {
        if (string.IsNullOrEmpty(_selectedPath) || !Directory.Exists(_selectedPath))
        {
          MessageBox.Show(
              this,
              "Выберите существующий каталог.",
              Text,
              MessageBoxButtons.OK,
              MessageBoxIcon.Warning);
          return;
        }

        DialogResult = DialogResult.OK;
        Close();
      }

      private void TrySelectInitialPath(string initialPath)
      {
        if (string.IsNullOrWhiteSpace(initialPath))
          return;

        string path = ResolveExistingDirectoryOrAncestor(initialPath);
        if (string.IsNullOrWhiteSpace(path))
          return;

        // Путь показывается и доступен для OK даже для UNC вне дерева дисков.
        SetSelectedPath(path, updatePathBox: true);

        TreeNode match = ExpandPath(path);
        if (match == null)
          return;

        _tree.SelectedNode = match;
        match.EnsureVisible();
        try
        {
          _tree.Focus();
        }
        catch
        {
          // ignore focus errors before handle is ready
        }

        // Повторный EnsureVisible после обновления раскладки.
        try
        {
          _tree.Update();
          match.EnsureVisible();
        }
        catch
        {
          // ignore
        }
      }

      /// <summary>
      /// Если указанный путь не существует — ближайший существующий родитель
      /// (чтобы «Обзор» стартовал рядом со старым путём с поля формы).
      /// </summary>
      private static string ResolveExistingDirectoryOrAncestor(string initialPath)
      {
        string path;
        try
        {
          path = Path.GetFullPath(initialPath.Trim());
        }
        catch
        {
          path = (initialPath ?? string.Empty).Trim();
        }

        if (string.IsNullOrWhiteSpace(path))
          return null;

        // Если указали файл — берём его каталог.
        try
        {
          if (File.Exists(path))
            path = Path.GetDirectoryName(path) ?? path;
        }
        catch
        {
          // ignore
        }

        while (!string.IsNullOrWhiteSpace(path))
        {
          try
          {
            if (Directory.Exists(path))
              return path;
          }
          catch
          {
            return null;
          }

          string parent;
          try
          {
            parent = Path.GetDirectoryName(
                path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
          }
          catch
          {
            return null;
          }

          if (string.IsNullOrWhiteSpace(parent) ||
              string.Equals(parent, path, StringComparison.OrdinalIgnoreCase))
            return null;

          path = parent;
        }

        return null;
      }

      private TreeNode ExpandPath(string fullPath)
      {
        string normalized = NormalizePath(fullPath);
        if (string.IsNullOrEmpty(normalized))
          return null;

        TreeNode current = null;
        foreach (TreeNode root in _tree.Nodes)
        {
          string rootPath = NormalizePath(root.Tag as string);
          if (string.IsNullOrEmpty(rootPath))
            continue;

          if (normalized.Equals(rootPath, StringComparison.OrdinalIgnoreCase) ||
              normalized.StartsWith(EnsureTrailingSlash(rootPath), StringComparison.OrdinalIgnoreCase))
          {
            current = root;
            break;
          }
        }

        if (current == null)
          return null;

        string currentPath = NormalizePath(current.Tag as string);
        if (string.IsNullOrEmpty(currentPath))
          return null;

        if (currentPath.Equals(normalized, StringComparison.OrdinalIgnoreCase))
          return current;

        string relative = normalized.Substring(currentPath.Length)
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.IsNullOrEmpty(relative))
          return current;

        string[] parts = relative.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);

        string walk = EnsureTrailingSlash(currentPath).TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        // Для корня диска оставляем завершающий '\', иначе Path.Combine("C:", "Users") → "C:Users".
        if (walk.Length == 2 && walk[1] == ':')
          walk = walk + Path.DirectorySeparatorChar;

        foreach (string part in parts)
        {
          // Принудительно подгружаем детей до поиска (Expand может не вызвать BeforeExpand
          // повторно, если узел уже помечен expanded без дочерних).
          EnsureChildrenLoaded(current);
          current.Expand();
          walk = Path.Combine(walk, part);
          string walkNorm = NormalizePath(walk);
          TreeNode next = null;
          foreach (TreeNode child in current.Nodes)
          {
            if (string.Equals(
                    NormalizePath(child.Tag as string),
                    walkNorm,
                    StringComparison.OrdinalIgnoreCase))
            {
              next = child;
              break;
            }
          }

          if (next == null)
            return current;

          current = next;
        }

        return current;
      }

      private void EnsureChildrenLoaded(TreeNode node)
      {
        if (node == null)
          return;

        string path = node.Tag as string;
        if (string.IsNullOrEmpty(path))
          return;

        if (node.Nodes.Count == 1 && node.Nodes[0].Tag == null)
        {
          OnBeforeExpand(this, new TreeViewCancelEventArgs(node, false, TreeViewAction.Expand));
          return;
        }

        if (node.Nodes.Count == 0)
        {
          try
          {
            if (Directory.Exists(path) && HasAnySubdirectory(path))
            {
              node.Nodes.Add(new TreeNode());
              OnBeforeExpand(this, new TreeViewCancelEventArgs(node, false, TreeViewAction.Expand));
            }
          }
          catch
          {
            // ignore
          }
        }
      }

      private static string NormalizePath(string path)
      {
        if (string.IsNullOrWhiteSpace(path))
          return null;

        try
        {
          return Path.GetFullPath(path)
              .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
          return null;
        }
      }

      private static string EnsureTrailingSlash(string path)
      {
        if (string.IsNullOrEmpty(path))
          return path;
        char sep = Path.DirectorySeparatorChar;
        if (path[path.Length - 1] == sep || path[path.Length - 1] == Path.AltDirectorySeparatorChar)
          return path;
        return path + sep;
      }
    }
  }
}
