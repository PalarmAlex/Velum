using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Velum.Configuration;
using Velum.SolidHomeostasis;
using Velum.UI.AssemblyRegistry;
using Velum.UI.ProductRegistry;
using Xarial.XCad.SolidWorks;

namespace Velum.UI
{
  /// <summary>
  /// Реестр документов: каталог файлов с виртуальными папками.
  /// Ключ учёта — нормализованный абсолютный путь; не путать с составом изделия.
  /// </summary>
  internal sealed partial class VelumProductRegistryForm : Form
  {
    private readonly ISwApplication _swApp;
    private readonly VelumProductRegistryStore _store = new VelumProductRegistryStore();
    private readonly Dictionary<int, VelumProductRegistryPathStatus> _pathStatuses =
        new Dictionary<int, VelumProductRegistryPathStatus>();
    private readonly List<TreeNode> _folderSearchResults = new List<TreeNode>();
    private int _folderSearchIndex = -1;
    private TreeNode _searchHighlightNode;
    private VelumProductRegistryPathStatus? _activeFilterStatus;
    private string _activeFilterDesignation = string.Empty;
    private string _activeFilterName = string.Empty;
    private int _sortColumn = 0;
    private SortOrder _sortOrder = SortOrder.Ascending;
    private bool _suppressTreeEvents;
    private bool _indexing;
    private bool _pathCheckRunning;
    private bool _updatingProperties;
    private bool _stopRequested;
    private BackgroundWorker _indexWorker;
    private int _indexTraceSession;
    private readonly bool _isAdmin = VelumAppConfig.IsProductRegistryAdmin;
    private ContextMenuStrip _treeMenu;
    private ContextMenuStrip _listMenu;
    private VelumListViewCellFilterMenu _listCellFilterMenu;
    private const string FolderImageClosedKey = "closed";
    private const string FolderImageOpenKey = "open";
    private const string FilterStatusAll = "(все)";

    public VelumProductRegistryForm(ISwApplication swApp)
    {
      _swApp = swApp;
      InitializeComponent();
      VelumFormHelp.Bind(this, VelumHelpTopics.ProductRegistry);
    }

    /// <summary>Для конструктора WinForms.</summary>
    public VelumProductRegistryForm()
        : this(null)
    {
    }

    /// <summary>
    /// Загрузка JSON, проверка путей и подготовка UI.
    /// false — пользователь прервал загрузку реестра.
    /// </summary>
    internal bool TryPrepare(int? selectFolderId = null)
    {
      if (VelumAppConfig.SolidHomeostasisDebugLog)
        VelumProductRegistryIndexTrace.Mark("form.prepare.begin");
      InitializeRuntime();
      _store.Load();
      var allItems = _store.GetAllItems();
      if (VelumAppConfig.SolidHomeostasisDebugLog)
        VelumProductRegistryIndexTrace.Mark(
            "form.prepare.loaded",
            "items=" + allItems.Count);

      VelumProductRegistryPathScanResult scan = RunPathVerification(
          allItems,
          allowAbortLoad: true,
          showSummary: false);
      if (scan == VelumProductRegistryPathScanResult.AbortLoad)
      {
        if (VelumAppConfig.SolidHomeostasisDebugLog)
          VelumProductRegistryIndexTrace.Mark("form.prepare.abort_path_check");
        return false;
      }

      // Если selectFolderId не передан — пытаемся найти папку активного документа SW.
      if (!selectFolderId.HasValue || selectFolderId.Value <= 0)
        selectFolderId = ResolveActiveDocumentFolderId();

      if (VelumAppConfig.SolidHomeostasisDebugLog)
        VelumProductRegistryIndexTrace.Mark("form.prepare.RebuildTree.begin");
      RebuildTree(selectFolderId);
      if (VelumAppConfig.SolidHomeostasisDebugLog)
        VelumProductRegistryIndexTrace.Mark("form.prepare.RebuildTree.end");
      BindList();
      if (VelumAppConfig.SolidHomeostasisDebugLog)
        VelumProductRegistryIndexTrace.Mark("form.prepare.done");
      return true;
    }

    private void InitializeRuntime()
    {
      Icon icon = TryLoadFormIcon();
      if (icon != null)
        Icon = icon;

      ApplySearchButtonIcons();
      ApplyFilterButtonIcons();
      SetupTree();
      SetupList();
      SetupFolderSearch();
      SetupFilters();
      ApplyTreeItemHeight();
      ApplyAccessLevel();
    }

    private void SetupTree()
    {
      SetupFolderTreeImages();
      _folderTreeView.AfterExpand += (s, e) => ApplyFolderNodeImage(e.Node);
      _folderTreeView.AfterCollapse += (s, e) => ApplyFolderNodeImage(e.Node);
      _folderTreeView.AfterSelect += (s, e) =>
      {
        if (!_suppressTreeEvents)
          BindList();
      };
      _folderTreeView.DrawNode += OnFolderTreeDrawNode;
      _folderTreeView.NodeMouseClick += (s, e) =>
      {
        if (e.Button == MouseButtons.Right)
          _folderTreeView.SelectedNode = e.Node;
      };
      _folderTreeView.KeyDown += OnFolderTreeKeyDown;

      if (!_isAdmin)
      {
        _folderTreeView.LabelEdit = false;
        _folderTreeView.AllowDrop = false;
        return;
      }

      _folderTreeView.AfterLabelEdit += OnFolderAfterLabelEdit;
      _folderTreeView.ItemDrag += OnFolderItemDrag;
      _folderTreeView.DragEnter += OnFolderDragEnter;
      _folderTreeView.DragOver += OnFolderDragOver;
      _folderTreeView.DragDrop += OnFolderDragDrop;

      Image addIcon = TryLoadMenuBitmap("Add.png");
      Image deleteIcon = TryLoadMenuBitmap("Delete.png");
      Image modifyIcon = TryLoadMenuBitmap("Modify.png");
      Image loadIcon = TryLoadMenuBitmap("Load.png");
      Image textIcon = TryLoadMenuBitmap("Text.png");
      Image moveUpIcon = TryLoadMenuBitmap("Up.png");
      Image moveDownIcon = TryLoadMenuBitmap("Down.png");

      _treeMenu = new ContextMenuStrip();
      var addItem = new ToolStripMenuItem("Добавить", addIcon, (s, e) => AddFolder());
      addItem.ShortcutKeys = Keys.Insert;
      addItem.ShowShortcutKeys = true;
      var deleteItem = new ToolStripMenuItem("Удалить", deleteIcon, (s, e) => DeleteSelectedFolder());
      deleteItem.ShortcutKeys = Keys.Delete;
      deleteItem.ShowShortcutKeys = true;
      var editItem = new ToolStripMenuItem("Редактировать", modifyIcon, (s, e) => BeginEditSelectedFolder());
      editItem.ShortcutKeys = Keys.F2;
      editItem.ShowShortcutKeys = true;
      var descriptionItem = new ToolStripMenuItem("Описание", textIcon, (s, e) => EditSelectedFolderDescription());
      // Перестановка узла в группе — только пунктами меню, без сочетаний клавиш.
      var moveUpItem = new ToolStripMenuItem("Вверх", moveUpIcon, (s, e) => MoveSelectedFolder(-1));
      var moveDownItem = new ToolStripMenuItem("Вниз", moveDownIcon, (s, e) => MoveSelectedFolder(1));
      var loadItem = new ToolStripMenuItem("Загрузить", loadIcon, (s, e) => LoadFolderIndex());
      // Смысловые группы: операции с каталогом | порядок в группе | индексация.
      _treeMenu.Items.Add(addItem);
      _treeMenu.Items.Add(deleteItem);
      _treeMenu.Items.Add(editItem);
      _treeMenu.Items.Add(descriptionItem);
      _treeMenu.Items.Add(new ToolStripSeparator());
      _treeMenu.Items.Add(moveUpItem);
      _treeMenu.Items.Add(moveDownItem);
      _treeMenu.Items.Add(new ToolStripSeparator());
      _treeMenu.Items.Add(loadItem);
      _folderTreeView.ContextMenuStrip = _treeMenu;
    }

    private void SetupFolderTreeImages()
    {
      Image closed = TryLoadMenuBitmap("Folder.png");
      Image open = TryLoadMenuBitmap("open_folder.png");
      if (closed == null && open == null)
        return;

      var images = new ImageList();
      images.ColorDepth = ColorDepth.Depth32Bit;
      images.ImageSize = new Size(16, 16);
      if (closed != null)
        images.Images.Add(FolderImageClosedKey, closed);
      if (open != null)
        images.Images.Add(FolderImageOpenKey, open);
      else if (closed != null)
        images.Images.Add(FolderImageOpenKey, closed);

      if (!images.Images.ContainsKey(FolderImageClosedKey) && open != null)
        images.Images.Add(FolderImageClosedKey, open);

      _folderTreeView.ImageList = images;
    }

    private void ApplyFolderNodeImage(TreeNode node)
    {
      if (node == null || _folderTreeView.ImageList == null)
        return;

      string key = node.IsExpanded ? FolderImageOpenKey : FolderImageClosedKey;
      if (!_folderTreeView.ImageList.Images.ContainsKey(key))
        return;

      node.ImageKey = key;
      node.SelectedImageKey = key;
    }

    private void SetupList()
    {
      _listView.ColumnClick += OnListColumnClick;
      _listView.DoubleClick += (s, e) =>
      {
        if (_isAdmin)
          EditSelectedItem();
        else
          OpenSelectedItems();
      };
      _listView.KeyDown += OnListKeyDown;

      Image addIcon = TryLoadMenuBitmap("Add.png");
      Image deleteIcon = TryLoadMenuBitmap("Delete.png");
      Image modifyIcon = TryLoadMenuBitmap("Modify.png");
      Image openIcon = TryLoadMenuBitmap("Yes.png");
      Image folderIcon = TryLoadMenuBitmap("Folder.png");
      Image selectAllIcon = TryLoadMenuBitmap("editselectall.png");

      _listMenu = new ContextMenuStrip();
      if (_isAdmin)
      {
        _listMenu.Items.Add(new ToolStripMenuItem("Добавить", addIcon, (s, e) => AddItem()));
        _listMenu.Items.Add(new ToolStripMenuItem("Удалить", deleteIcon, (s, e) => DeleteSelectedItem()));
        _listMenu.Items.Add(new ToolStripMenuItem("Редактировать", modifyIcon, (s, e) => EditSelectedItem()));
        _listMenu.Items.Add(new ToolStripMenuItem("Связать с узлом", folderIcon, (s, e) => LinkSelectedItemsToFolder()));
        _listMenu.Items.Add(new ToolStripSeparator());
      }

      _listMenu.Items.Add(new ToolStripMenuItem("Открыть", openIcon, (s, e) => OpenSelectedItems()));
      Image printIcon = TryLoadMenuBitmap("Print.png");
      _listMenu.Items.Add(new ToolStripMenuItem("Печать", printIcon, (s, e) => PrintSelectedDrawings()));
      Image copyIcon = TryLoadMenuBitmap("Copy.png");
      _listMenu.Items.Add(new ToolStripMenuItem("Скопировать в...", copyIcon, (s, e) => CopySelectedDxfFiles()));
      Image propsIcon = TryLoadMenuBitmap("Modify.png");
      var updatePropertiesItem = new ToolStripMenuItem("Обновить свойства", propsIcon, (s, e) => UpdateSelectedItemProperties());
      updatePropertiesItem.Enabled = _isAdmin;
      _listMenu.Items.Add(updatePropertiesItem);
      _listMenu.Items.Add(new ToolStripSeparator());
      Image filterIcon = TryLoadMenuBitmap("Thumbs up.png");
      Image excludeIcon = TryLoadMenuBitmap("Thumbs down.png");
      _listCellFilterMenu = new VelumListViewCellFilterMenu(
          _listView,
          this,
          ResolveProductListFilterBox,
          ApplyItemFiltersFromUi,
          () => !_indexing && !_updatingProperties,
          null);
      _listCellFilterMenu.InsertInto(_listMenu, _listMenu.Items.Count, filterIcon, excludeIcon);
      _listMenu.Items.Add(new ToolStripSeparator());
      _listMenu.Items.Add(new ToolStripMenuItem("Выбрать все", selectAllIcon, (s, e) => SelectAllListItems()));
      _listView.ContextMenuStrip = _listMenu;
    }

    private TextBox ResolveProductListFilterBox(int column)
    {
      if (column == 0)
        return _filterDesignationBox;
      if (column == 1)
        return _filterNameBox;
      return null;
    }

    private void ApplyAccessLevel()
    {
      if (_isAdmin)
        return;

      _folderTreeView.LabelEdit = false;
      _folderTreeView.AllowDrop = false;
      _folderTreeView.ContextMenuStrip = null;
    }

    private void SetupFolderSearch()
    {
      KeyEventHandler enterSearch = (s, e) =>
      {
        if (e.KeyCode == Keys.Enter)
        {
          SearchFolders();
          e.Handled = true;
          e.SuppressKeyPress = true;
        }
      };
      _folderSearchNameBox.KeyDown += enterSearch;
      _folderSearchDescriptionBox.KeyDown += enterSearch;
      EventHandler clearIfEmpty = (s, e) =>
      {
        if (string.IsNullOrEmpty(_folderSearchNameBox.Text)
            && string.IsNullOrEmpty(_folderSearchDescriptionBox.Text))
          ClearFolderSearch(keepText: true);
      };
      _folderSearchNameBox.TextChanged += clearIfEmpty;
      _folderSearchDescriptionBox.TextChanged += clearIfEmpty;
      _btnFolderSearchPrev.Click += (s, e) => ShowPreviousFolderResult();
      _btnFolderSearchNext.Click += (s, e) =>
      {
        if (_folderSearchResults.Count == 0)
          SearchFolders();
        else
          ShowNextFolderResult();
      };
      _btnFolderSearchClear.Click += (s, e) => ClearFolderSearch(keepText: false);
      _chkShowDescriptions.CheckedChanged += (s, e) =>
      {
        ApplyTreeItemHeight();
        _folderTreeView.Invalidate();
      };
      _btnFolderSearchPrev.Text = string.Empty;
      _btnFolderSearchNext.Text = string.Empty;
      _btnFolderSearchClear.Text = string.Empty;
    }

    private void SetupFilters()
    {
      _filterStatusBox.Items.Clear();
      _filterStatusBox.Items.Add(FilterStatusAll);
      _filterStatusBox.Items.Add(VelumProductRegistryPathStatusText.Ok);
      _filterStatusBox.Items.Add(VelumProductRegistryPathStatusText.No);
      _filterStatusBox.Items.Add(VelumProductRegistryPathStatusText.Unknown);
      _filterStatusBox.SelectedIndex = 0;

      _btnFilterApply.Click += (s, e) => ApplyItemFiltersFromUi();
      _btnFilterReset.Click += (s, e) =>
      {
        _filterStatusBox.SelectedIndex = 0;
        _filterDesignationBox.Text = string.Empty;
        _filterNameBox.Text = string.Empty;
        _activeFilterStatus = null;
        _activeFilterDesignation = string.Empty;
        _activeFilterName = string.Empty;
        BindList();
      };
      _btnFilterHelp.Click += (s, e) => VelumListFilterHelper.ShowHelp(this);
      KeyEventHandler enterApply = (s, e) =>
      {
        if (e.KeyCode == Keys.Enter)
        {
          ApplyItemFiltersFromUi();
          e.Handled = true;
          e.SuppressKeyPress = true;
        }
      };
      _filterDesignationBox.KeyDown += enterApply;
      _filterNameBox.KeyDown += enterApply;
      _btnVerifyPaths.Click += (s, e) => OnVerifyPathsClick();
      _btnFolderAutoNames.Click += (s, e) => OpenFolderAutoNamesSettings();
      _btnStop.Click += (s, e) => _stopRequested = true;
      _btnStop.Enabled = false;
      _btnReport.Click += (s, e) => ExportHtmlReport();
      _btnReports.Click += (s, e) => OpenExistingReports();
    }

    private void ApplyItemFiltersFromUi()
    {
      _activeFilterStatus = null;
      string statusText = _filterStatusBox.SelectedItem as string;
      if (!string.IsNullOrEmpty(statusText)
          && !string.Equals(statusText, FilterStatusAll, StringComparison.Ordinal)
          && VelumProductRegistryPathStatusText.TryParseFilter(statusText, out VelumProductRegistryPathStatus parsed))
      {
        _activeFilterStatus = parsed;
      }

      _activeFilterDesignation = (_filterDesignationBox.Text ?? string.Empty).Trim();
      _activeFilterName = (_filterNameBox.Text ?? string.Empty).Trim();
      BindList();
    }

    protected override bool ProcessDialogKey(Keys keyData)
    {
      if (keyData == Keys.Escape)
      {
        TreeNode editing = _folderTreeView != null ? _folderTreeView.SelectedNode : null;
        if (editing != null && editing.IsEditing)
          return base.ProcessDialogKey(keyData);

        Close();
        return true;
      }

      return base.ProcessDialogKey(keyData);
    }

    private void ApplyFilterButtonIcons()
    {
      Image settings = TryLoadMenuBitmap("settings.png");
      if (settings != null)
      {
        _btnFolderAutoNames.Image = settings;
        _btnFolderAutoNames.ImageAlign = ContentAlignment.MiddleCenter;
        _btnFolderAutoNames.Text = string.Empty;
      }
      else
        _btnFolderAutoNames.Text = "…";

      var tip = new ToolTip();
      tip.SetToolTip(_btnFolderAutoNames, "Автоимена каталогов");
      tip.SetToolTip(_btnVerifyPaths, "Проверить наличие файлов по путям реестра");
      tip.SetToolTip(_btnStop, "Прервать индексацию, обновление свойств или проверку путей");
      tip.SetToolTip(_btnFilterApply, "Применить фильтры списка");
      tip.SetToolTip(_btnFilterReset, "Очистить фильтры списка");
      tip.SetToolTip(_btnFilterHelp, "Справка по маскам фильтра");
      tip.SetToolTip(_chkShowDescriptions, "Показать описание узлов в дереве каталогов");
      tip.SetToolTip(_btnReport, "Сформировать HTML-отчёт по текущему списку");
      tip.SetToolTip(_btnReports, "Открыть список сохранённых отчётов");
    }

    private void OpenFolderAutoNamesSettings()
    {
      using (var form = new VelumProductRegistryFolderAutoNamesForm())
        form.ShowDialog(this);
    }

    private void ApplySearchButtonIcons()
    {
      Image up = TryLoadMenuBitmap("Up.png");
      Image down = TryLoadMenuBitmap("Down.png");
      Image erase = TryLoadMenuBitmap("Erase.png");
      if (up != null)
      {
        _btnFolderSearchPrev.Image = up;
        _btnFolderSearchPrev.ImageAlign = ContentAlignment.MiddleCenter;
      }
      else
        _btnFolderSearchPrev.Text = "▲";

      if (down != null)
      {
        _btnFolderSearchNext.Image = down;
        _btnFolderSearchNext.ImageAlign = ContentAlignment.MiddleCenter;
      }
      else
        _btnFolderSearchNext.Text = "▼";

      if (erase != null)
      {
        _btnFolderSearchClear.Image = erase;
        _btnFolderSearchClear.ImageAlign = ContentAlignment.MiddleCenter;
      }
      else
        _btnFolderSearchClear.Text = "×";

      var tip = new ToolTip();
      tip.SetToolTip(_btnFolderSearchPrev, "Предыдущее совпадение в дереве");
      tip.SetToolTip(_btnFolderSearchNext, "Следующее совпадение в дереве");
      tip.SetToolTip(_btnFolderSearchClear, "Сбросить поиск по дереву");
    }

    private void RebuildTree(int? selectFolderId = null)
    {
      HashSet<int> expandedIds = CaptureExpandedFolderIds();

      _suppressTreeEvents = true;
      _folderTreeView.BeginUpdate();
      try
      {
        _folderTreeView.Nodes.Clear();
        foreach (VelumProductFolder folder in _store.GetChildFolders(0))
          _folderTreeView.Nodes.Add(BuildFolderNode(folder));

        RestoreExpandedFolderIds(expandedIds);
      }
      finally
      {
        _folderTreeView.EndUpdate();
        _suppressTreeEvents = false;
      }

      // Разворачивание и выделение — строго после EndUpdate,
      // иначе BeginUpdate блокирует визуальное обновление дерева.
      TreeNode selected;
      if (selectFolderId.HasValue && selectFolderId.Value > 0)
        selected = FindNodeByFolderId(_folderTreeView.Nodes, selectFolderId.Value);
      else
        selected = null;

      if (selected == null && _folderTreeView.Nodes.Count > 0)
        selected = _folderTreeView.Nodes[0];

      if (selected != null)
      {
        ExpandParents(selected);
        selected.EnsureVisible();
        _folderTreeView.SelectedNode = selected;
      }
    }

    private HashSet<int> CaptureExpandedFolderIds()
    {
      var ids = new HashSet<int>();
      CollectExpandedFolderIds(_folderTreeView.Nodes, ids);
      return ids;
    }

    private static void CollectExpandedFolderIds(TreeNodeCollection nodes, HashSet<int> ids)
    {
      if (nodes == null || ids == null)
        return;
      foreach (TreeNode node in nodes)
      {
        if (node.IsExpanded && node.Tag is int)
          ids.Add((int)node.Tag);
        CollectExpandedFolderIds(node.Nodes, ids);
      }
    }

    private void RestoreExpandedFolderIds(HashSet<int> ids)
    {
      if (ids == null || ids.Count == 0)
        return;
      ExpandFoldersByIds(_folderTreeView.Nodes, ids);
    }

    private static void ExpandFoldersByIds(TreeNodeCollection nodes, HashSet<int> ids)
    {
      if (nodes == null || ids == null)
        return;
      foreach (TreeNode node in nodes)
      {
        if (node.Tag is int && ids.Contains((int)node.Tag))
          node.Expand();
        ExpandFoldersByIds(node.Nodes, ids);
      }
    }

    private TreeNode BuildFolderNode(VelumProductFolder folder)
    {
      var node = new TreeNode(folder.Name) { Tag = folder.Id };
      ApplyFolderNodeToolTip(node, folder);
      ApplyFolderNodeImage(node);
      foreach (VelumProductFolder child in _store.GetChildFolders(folder.Id))
        node.Nodes.Add(BuildFolderNode(child));
      return node;
    }

    private static void ApplyFolderNodeToolTip(TreeNode node, VelumProductFolder folder)
    {
      string description = folder == null ? string.Empty : (folder.Description ?? string.Empty).Trim();
      node.ToolTipText = description;
    }

    private void ApplyTreeItemHeight()
    {
      int line = Math.Max(13, _folderTreeView.Font.Height);
      _folderTreeView.ItemHeight = _chkShowDescriptions.Checked
          ? (line * 2) + 6
          : line + 4;
    }

    private void BindList()
    {
      int? folderId = GetSelectedFolderId();
      _listView.BeginUpdate();
      try
      {
        _listView.Items.Clear();
        if (folderId == null)
        {
          _listStatusLabel.Text = string.Empty;
          return;
        }

        List<VelumProductItem> items = new List<VelumProductItem>(_store.GetItemsInFolderTree(folderId.Value));
        items = ApplyItemFilters(items);
        SortItems(items);

        foreach (VelumProductItem item in items)
        {
          var row = new ListViewItem(item.Designation ?? string.Empty) { Tag = item.Id };
          row.SubItems.Add(item.Name ?? string.Empty);
          row.SubItems.Add(VelumProductRegistryPathStatusText.ToDisplay(GetPathStatus(item.Id)));
          _listView.Items.Add(row);
        }

        _listStatusLabel.Text = "Записей: " + _listView.Items.Count;
      }
      finally
      {
        _listView.EndUpdate();
      }
    }

    private List<VelumProductItem> ApplyItemFilters(List<VelumProductItem> items)
    {
      if (_activeFilterStatus == null
          && string.IsNullOrEmpty(_activeFilterDesignation)
          && string.IsNullOrEmpty(_activeFilterName))
        return items;

      var filtered = new List<VelumProductItem>(items.Count);
      foreach (VelumProductItem item in items)
      {
        if (_activeFilterStatus != null && GetPathStatus(item.Id) != _activeFilterStatus.Value)
          continue;
        if (!string.IsNullOrEmpty(_activeFilterDesignation)
            && !VelumListFilterHelper.Matches(item.Designation, _activeFilterDesignation))
          continue;
        if (!string.IsNullOrEmpty(_activeFilterName)
            && !VelumListFilterHelper.Matches(item.Name, _activeFilterName))
          continue;
        filtered.Add(item);
      }

      return filtered;
    }

    private void SortItems(List<VelumProductItem> items)
    {
      int direction = _sortOrder == SortOrder.Descending ? -1 : 1;
      items.Sort((a, b) =>
      {
        int cmp;
        switch (_sortColumn)
        {
          case 1:
            cmp = string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
            break;
          case 2:
            cmp = string.Compare(
                VelumProductRegistryPathStatusText.ToDisplay(GetPathStatus(a.Id)),
                VelumProductRegistryPathStatusText.ToDisplay(GetPathStatus(b.Id)),
                StringComparison.OrdinalIgnoreCase);
            break;
          default:
            cmp = string.Compare(a.Designation, b.Designation, StringComparison.CurrentCultureIgnoreCase);
            break;
        }

        if (cmp == 0)
          cmp = a.Id.CompareTo(b.Id);
        return cmp * direction;
      });
    }

    private void OnListColumnClick(object sender, ColumnClickEventArgs e)
    {
      if (e.Column == _sortColumn)
        _sortOrder = _sortOrder == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
      else
      {
        _sortColumn = e.Column;
        _sortOrder = SortOrder.Ascending;
      }

      BindList();
    }

    private VelumProductRegistryPathStatus GetPathStatus(int itemId)
    {
      VelumProductRegistryPathStatus status;
      if (_pathStatuses.TryGetValue(itemId, out status))
        return status;
      return VelumProductRegistryPathStatus.Unknown;
    }

    private void SetPathStatus(int itemId, VelumProductRegistryPathStatus status)
    {
      _pathStatuses[itemId] = status;
    }

    private void RefreshPathStatusForItem(VelumProductItem item)
    {
      if (item == null || item.Id <= 0)
        return;
      SetPathStatus(
          item.Id,
          VelumProductRegistryPathChecker.CheckOne(
              item.FilePath,
              VelumProductRegistryPathChecker.TimeoutMilliseconds));
    }

    private void OnVerifyPathsClick()
    {
      if (_indexing || _pathCheckRunning)
        return;

      VelumProductRegistryPathScanResult scan = RunPathVerification(
          _store.GetAllItems(),
          allowAbortLoad: false,
          showSummary: true);
      if (scan == VelumProductRegistryPathScanResult.AbortLoad)
        return;

      BindList();
    }

    private VelumProductRegistryPathScanResult RunPathVerification(
        IReadOnlyList<VelumProductItem> items,
        bool allowAbortLoad,
        bool showSummary)
    {
      if (_pathCheckRunning)
        return VelumProductRegistryPathScanResult.Completed;

      _pathCheckRunning = true;
      Cursor previous = Cursor;
      try
      {
        Cursor = Cursors.WaitCursor;
        _btnVerifyPaths.Enabled = false;

        VelumProductRegistryPathScanResult result = VelumProductRegistryPathChecker.Run(
            this,
            items,
            _pathStatuses,
            allowAbortLoad,
            out int checkedCount,
            out int okCount,
            out int noCount,
            out int unknownCount);

        if (showSummary && result != VelumProductRegistryPathScanResult.AbortLoad)
        {
          var text = new StringBuilder();
          text.AppendLine(
              result == VelumProductRegistryPathScanResult.StoppedEarly
                  ? "Проверка путей прервана."
                  : "Проверка путей завершена.");
          text.AppendLine("ok: " + okCount);
          text.AppendLine("NO: " + noCount);
          text.AppendLine("Не удалось проверить (?): " + unknownCount);
          MessageBox.Show(
              this,
              text.ToString().TrimEnd(),
              "Реестр документов",
              MessageBoxButtons.OK,
              unknownCount > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        return result;
      }
      finally
      {
        _btnVerifyPaths.Enabled = !_indexing;
        Cursor = previous;
        _pathCheckRunning = false;
      }
    }

    private void UpdateSelectedItemProperties()
    {
      if (_indexing || _pathCheckRunning || _updatingProperties)
        return;

      List<int> selectedIds = GetSelectedItemIds();
      if (selectedIds.Count == 0)
      {
        MessageBox.Show(
            this,
            "Выберите записи в списке.",
            "Реестр документов",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      if (_swApp?.Sw == null)
      {
        MessageBox.Show(
            this,
            "SolidWorks недоступен.",
            "Реестр документов",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return;
      }

      var solidItems = new List<VelumProductItem>();
      var selectedItems = new List<VelumProductItem>();
      int skippedOtherCount = 0;
      bool hasRelatedSelected = false;
      foreach (int id in selectedIds)
      {
        VelumProductItem item = _store.GetItem(id);
        if (item == null)
          continue;
        selectedItems.Add(item);
        bool isSolid = VelumProductRegistryExportMetaSync.IsExportMetaSyncSupported(item.FilePath)
            || VelumProductRegistryNameSyncHelper.IsNameSyncSupported(item.FilePath);
        if (isSolid)
          solidItems.Add(item);
        if (VelumProductRegistryIntegrityRules.IsRelatedDocumentPath(item.FilePath))
          hasRelatedSelected = true;
        else if (!isSolid)
          skippedOtherCount++;
      }

      if (skippedOtherCount > 0)
      {
        MessageBox.Show(
            this,
            "Пропущены неподдерживаемые файлы: " + skippedOtherCount
                + "." + System.Environment.NewLine
                + "Обновляются sldprt / sldasm / slddrw / dxf / pdf.",
            "Реестр документов",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
      }

      if (solidItems.Count == 0 && !hasRelatedSelected)
        return;

      if (hasRelatedSelected)
        AddMatchingModelsForRelatedNameCopy(solidItems, selectedItems);

      int updated = 0;
      int skippedEmpty = 0;
      int skippedMissing = 0;
      int unchanged = 0;
      int failed = 0;
      int relatedCopied = 0;
      int relatedUnchanged = 0;
      int relatedNoSource = 0;
      int relatedEmptySource = 0;
      var errors = new List<string>();
      bool stopped = false;
      bool showProgress = false;

      _stopRequested = false;
      _updatingProperties = true;
      Cursor previous = Cursor;
      try
      {
        SetPropertiesUpdateUi(true, false, 0);
        VelumProductRegistryOpenPropertyCache openCache =
            VelumProductRegistryOpenPropertyCache.Collect(_swApp);

        var remaining = new List<VelumProductItem>();
        for (int i = 0; i < solidItems.Count; i++)
        {
          if (_stopRequested)
          {
            stopped = true;
            break;
          }

          Application.DoEvents();
          if (_stopRequested)
          {
            stopped = true;
            break;
          }

          VelumProductItem item = solidItems[i];
          VelumProductRegistryNameSyncOutcome nameOutcome;
          VelumProductRegistryExportMetaSyncOutcome metaOutcome;
          string metaError;
          if (openCache.TryApplyToItem(
                  _store,
                  item,
                  persist: false,
                  out nameOutcome,
                  out metaOutcome,
                  out metaError))
          {
            SetPathStatus(item.Id, VelumProductRegistryPathStatus.Ok);
            AccumulatePropertySyncResults(
                item,
                nameOutcome,
                string.Empty,
                metaOutcome,
                metaError,
                ref updated,
                ref unchanged,
                ref skippedEmpty,
                ref failed,
                errors);
            continue;
          }

          if (openCache.IsSessionPath(item.FilePath))
          {
            SetPathStatus(item.Id, VelumProductRegistryPathStatus.Ok);
            continue;
          }

          if (!VelumProductRegistryNameSyncHelper.IsNameSyncSupported(item.FilePath))
            continue;

          remaining.Add(item);
        }

        showProgress = remaining.Count > 0 && !stopped;
        if (showProgress)
          SetPropertiesUpdateUi(true, true, remaining.Count);

        for (int i = 0; i < remaining.Count; i++)
        {
          if (_stopRequested)
          {
            stopped = true;
            break;
          }

          Application.DoEvents();
          if (_stopRequested)
          {
            stopped = true;
            break;
          }

          VelumProductItem item = remaining[i];
          VelumProductRegistryPathStatus pathStatus = VelumProductRegistryPathChecker.CheckOne(
              item.FilePath,
              VelumProductRegistryPathChecker.TimeoutMilliseconds);
          SetPathStatus(item.Id, pathStatus);
          if (pathStatus != VelumProductRegistryPathStatus.Ok)
          {
            skippedMissing++;
            if (showProgress)
              SetIndexProgressPercent(((i + 1) * 100) / remaining.Count);
            continue;
          }

          VelumProductRegistryNameSyncOutcome nameOutcome;
          string error;
          VelumProductRegistryExportMetaSyncOutcome metaOutcome;
          string metaError;
          VelumProductRegistryOpenPropertyCache.SyncAllowOpen(
              _store,
              _swApp,
              item,
              persist: false,
              out nameOutcome,
              out error,
              out metaOutcome,
              out metaError);

          AccumulatePropertySyncResults(
              item,
              nameOutcome,
              error,
              metaOutcome,
              metaError,
              ref updated,
              ref unchanged,
              ref skippedEmpty,
              ref failed,
              errors);

          if (showProgress)
            SetIndexProgressPercent(((i + 1) * 100) / remaining.Count);
        }

        VelumProductRegistryNameSyncBatchResult related =
            VelumProductRegistryRelatedNameCopy.CopyFromModels(_store, selectedItems);
        relatedCopied = related.Updated;
        relatedUnchanged = related.Unchanged;
        relatedNoSource = related.NotInRegistry;
        relatedEmptySource = related.SkippedEmpty;
        failed += related.Failed;
        updated += related.Updated;

        if (updated > 0)
          _store.Save();
      }
      finally
      {
        _updatingProperties = false;
        SetPropertiesUpdateUi(false, showProgress, 0);
        Cursor = previous;
      }

      BindList();

      var summary = new StringBuilder();
      summary.AppendLine(stopped
          ? "Обновление свойств прервано."
          : "Обновление свойств завершено.");
      summary.AppendLine("Обновлено: " + updated);
      if (relatedCopied > 0)
        summary.AppendLine("Наименование скопировано в чертежи/DXF/PDF: " + relatedCopied);
      if (unchanged > 0)
        summary.AppendLine("Без изменений: " + unchanged);
      if (relatedUnchanged > 0)
        summary.AppendLine("Чертежи/DXF/PDF без изменений: " + relatedUnchanged);
      if (skippedEmpty > 0)
        summary.AppendLine("Пустое «Наименование» в SW (Name не изменён): " + skippedEmpty);
      if (relatedEmptySource > 0)
        summary.AppendLine("Пустое «Наименование» у одноименной детали/сборки: " + relatedEmptySource);
      if (relatedNoSource > 0)
        summary.AppendLine("Нет одноименной детали/сборки: " + relatedNoSource);
      if (skippedMissing > 0)
        summary.AppendLine("Путь недоступен: " + skippedMissing);
      if (failed > 0)
        summary.AppendLine("Ошибки: " + failed);
      if (errors.Count > 0)
      {
        summary.AppendLine();
        foreach (string line in errors)
          summary.AppendLine("• " + line);
      }

      MessageBox.Show(
          this,
          summary.ToString().TrimEnd(),
          "Реестр документов",
          MessageBoxButtons.OK,
          failed > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
    }

    private static void AccumulatePropertySyncResults(
        VelumProductItem item,
        VelumProductRegistryNameSyncOutcome outcome,
        string error,
        VelumProductRegistryExportMetaSyncOutcome metaOutcome,
        string metaError,
        ref int updated,
        ref int unchanged,
        ref int skippedEmpty,
        ref int failed,
        List<string> errors)
    {
      if (metaOutcome == VelumProductRegistryExportMetaSyncOutcome.Updated)
        updated++;
      else if (metaOutcome == VelumProductRegistryExportMetaSyncOutcome.Failed
               && errors.Count < 15
               && !string.IsNullOrWhiteSpace(metaError))
      {
        errors.Add((item.Designation ?? item.FilePath) + " [export-meta]: " + metaError);
        failed++;
      }

      switch (outcome)
      {
        case VelumProductRegistryNameSyncOutcome.Updated:
          updated++;
          break;
        case VelumProductRegistryNameSyncOutcome.Unchanged:
          unchanged++;
          break;
        case VelumProductRegistryNameSyncOutcome.SkippedEmpty:
          skippedEmpty++;
          break;
        case VelumProductRegistryNameSyncOutcome.Unsupported:
          break;
        default:
          failed++;
          if (errors.Count < 15 && !string.IsNullOrWhiteSpace(error))
            errors.Add((item.Designation ?? item.FilePath) + ": " + error);
          break;
      }
    }

    /// <summary>
    /// Если выделены чертежи/DXF/PDF, добавляет одноименные детали/сборки в набор SW-sync,
    /// чтобы сначала обновить «Наименование», затем скопировать его в связанные записи.
    /// </summary>
    private void AddMatchingModelsForRelatedNameCopy(
        List<VelumProductItem> solidItems,
        List<VelumProductItem> selectedItems)
    {
      var relatedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      for (int i = 0; i < selectedItems.Count; i++)
      {
        VelumProductItem item = selectedItems[i];
        if (item == null || !VelumProductRegistryIntegrityRules.IsRelatedDocumentPath(item.FilePath))
          continue;
        string key = VelumProductRegistryMatchKey.FromRelatedDocument(item);
        if (key.Length > 0)
          relatedKeys.Add(key);
      }

      if (relatedKeys.Count == 0)
        return;

      var solidIds = new HashSet<int>();
      for (int i = 0; i < solidItems.Count; i++)
      {
        if (solidItems[i] != null && solidItems[i].Id > 0)
          solidIds.Add(solidItems[i].Id);
      }

      IReadOnlyList<VelumProductItem> allItems = _store.GetAllItems();
      for (int i = 0; i < allItems.Count; i++)
      {
        VelumProductItem item = allItems[i];
        if (item == null || item.Id <= 0 || solidIds.Contains(item.Id))
          continue;
        if (!VelumProductRegistryIntegrityRules.IsPartOrAssemblyPath(item.FilePath))
          continue;

        string key = VelumProductRegistryMatchKey.FromItem(item);
        if (key.Length == 0 || !relatedKeys.Contains(key))
          continue;

        solidItems.Add(item);
        solidIds.Add(item.Id);
      }
    }

    /// <summary>
    /// UI во время «Обновить свойства»: Стоп всегда; прогресс — если нет активной сборки.
    /// </summary>
    private void SetPropertiesUpdateUi(bool updating, bool showProgress, int totalItems)
    {
      if (updating)
        CommitOrCancelFolderLabelEdit();

      _btnStop.Enabled = updating;
      if (showProgress)
      {
        _indexProgressBar.Visible = updating;
        _treePanel.RowStyles[4].Height = updating ? 22F : 0F;
        if (updating)
        {
          ResetIndexProgressBar(100);
          _indexProgressBar.Style = ProgressBarStyle.Continuous;
          if (totalItems <= 0)
          {
            _indexProgressBar.Style = ProgressBarStyle.Marquee;
            _indexProgressBar.MarqueeAnimationSpeed = 30;
          }
        }
        else
        {
          _indexProgressBar.MarqueeAnimationSpeed = 0;
          ResetIndexProgressBar(100);
        }
      }

      _folderTreeView.Enabled = !updating;
      _folderTreeView.LabelEdit = _isAdmin && !updating;
      _listView.Enabled = !updating;
      _btnVerifyPaths.Enabled = !updating && !_pathCheckRunning && !_indexing;
      _btnFolderAutoNames.Enabled = !updating && !_indexing;
      _filterStatusBox.Enabled = !updating && !_indexing;
      _filterDesignationBox.Enabled = !updating && !_indexing;
      _filterNameBox.Enabled = !updating && !_indexing;
      _btnFilterApply.Enabled = !updating && !_indexing;
      _btnFilterReset.Enabled = !updating && !_indexing;
      _btnFilterHelp.Enabled = !updating && !_indexing;
      _btnReport.Enabled = !updating && !_indexing;
      _btnReports.Enabled = !updating && !_indexing;
      if (_treeMenu != null)
        _treeMenu.Enabled = !updating && !_indexing;
      if (_listMenu != null)
        _listMenu.Enabled = !updating && !_indexing;
      Cursor = updating ? Cursors.WaitCursor : Cursors.Default;
    }

    private int? GetSelectedFolderId()
    {
      TreeNode node = _folderTreeView.SelectedNode;
      if (node == null || !(node.Tag is int))
        return null;
      return (int)node.Tag;
    }

    private int? GetSelectedItemId()
    {
      if (_listView.SelectedItems.Count == 0)
        return null;
      object tag = _listView.SelectedItems[0].Tag;
      return tag is int ? (int)tag : (int?)null;
    }

    private List<int> GetSelectedItemIds()
    {
      var ids = new List<int>(_listView.SelectedItems.Count);
      foreach (ListViewItem row in _listView.SelectedItems)
      {
        if (row.Tag is int)
          ids.Add((int)row.Tag);
      }

      return ids;
    }

    private List<VelumProductItem> GetSelectedItems()
    {
      List<int> ids = GetSelectedItemIds();
      var items = new List<VelumProductItem>(ids.Count);
      for (int i = 0; i < ids.Count; i++)
      {
        VelumProductItem item = _store.GetItem(ids[i]);
        if (item != null)
          items.Add(item);
      }

      return items;
    }

    private static string FormatItemLabel(VelumProductItem item)
    {
      if (item == null)
        return string.Empty;
      if (!string.IsNullOrWhiteSpace(item.Designation))
        return item.Designation.Trim();
      string path = item.FilePath ?? string.Empty;
      if (path.Length > 0)
        return Path.GetFileName(path);
      return "ID " + item.Id;
    }

    private void ShowSkippedRecordsMessage(string actionPhrase, IList<string> skippedLabels)
    {
      if (skippedLabels == null || skippedLabels.Count == 0)
        return;

      var text = new StringBuilder();
      text.AppendLine("Следующие записи " + actionPhrase + ":");
      int limit = Math.Min(skippedLabels.Count, 40);
      for (int i = 0; i < limit; i++)
        text.AppendLine("• " + skippedLabels[i]);
      if (skippedLabels.Count > limit)
        text.AppendLine("… и ещё " + (skippedLabels.Count - limit));

      MessageBox.Show(
          this,
          text.ToString().TrimEnd(),
          "Реестр документов",
          MessageBoxButtons.OK,
          MessageBoxIcon.Information);
    }

    private void PrintSelectedDrawings()
    {
      if (_indexing || _pathCheckRunning || _updatingProperties)
        return;

      List<VelumProductItem> selected = GetSelectedItems();
      if (selected.Count == 0)
      {
        MessageBox.Show(
            this,
            "Выделите записи в списке.",
            "Реестр документов",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      if (_swApp?.Sw == null)
      {
        MessageBox.Show(
            this,
            "SolidWorks недоступен.",
            "Реестр документов",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return;
      }

      var printable = new List<VelumProductItem>();
      var skipped = new List<string>();
      for (int i = 0; i < selected.Count; i++)
      {
        VelumProductItem item = selected[i];
        if (VelumProductRegistryPrintDrawingsHelper.IsPrintablePath(item.FilePath))
          printable.Add(item);
        else
          skipped.Add(FormatItemLabel(item));
      }

      if (skipped.Count > 0)
      {
        if (printable.Count == 0)
        {
          MessageBox.Show(
              this,
              "Выделенные записи не являются чертежами (.slddrw) или PDF.",
              "Реестр документов",
              MessageBoxButtons.OK,
              MessageBoxIcon.Information);
          return;
        }

        ShowSkippedRecordsMessage("не будут распечатаны", skipped);
      }

      List<VelumAssemblyRegistryPrintDrawingsService.PrintRow> rows =
          VelumProductRegistryPrintDrawingsHelper.Resolve(printable);
      if (rows.Count == 0)
      {
        MessageBox.Show(
            this,
            "По выделенным записям нечего печатать.",
            "Реестр документов",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      using (var form = new VelumAssemblyRegistryPrintDrawingsForm(_swApp, rows))
        form.ShowDialog(this);
    }

    private void OpenExistingReports()
    {
      using (var form = new VelumAssemblyRegistryReportsForm(
          VelumProductRegistryReportHtmlBuilder.ReportsFolderPath,
          "Реестр_документов_"))
        form.ShowDialog(this);
    }

    private void ExportHtmlReport()
    {
      if (_indexing || _pathCheckRunning || _updatingProperties)
        return;

      int? folderId = GetSelectedFolderId();
      if (folderId == null)
      {
        MessageBox.Show(
            this,
            "Не выбран каталог в дереве.",
            "Реестр документов",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      List<VelumProductItem> items = new List<VelumProductItem>(_store.GetItemsInFolderTree(folderId.Value));
      items = ApplyItemFilters(items);

      // Collect all folders for path lookup
      var folderLookup = new Dictionary<int, VelumProductFolder>();
      foreach (var f in _store.Folders)
        folderLookup[f.Id] = f;

      var selectionLabel = new StringBuilder();
      selectionLabel.Append("Каталог: ").Append(GetSelectedFolderPathDisplay(folderId.Value));

      int filterCount = 0;
      if (_activeFilterStatus != null) filterCount++;
      if (!string.IsNullOrEmpty(_activeFilterDesignation)) filterCount++;
      if (!string.IsNullOrEmpty(_activeFilterName)) filterCount++;
      if (filterCount > 0)
        selectionLabel.Append("; фильтров: ").Append(filterCount);

      string html = VelumProductRegistryReportHtmlBuilder.BuildHtml(
          selectionLabel.ToString(),
          items,
          folderLookup);

      string folder = VelumProductRegistryReportHtmlBuilder.ReportsFolderPath;
      string path;
      try
      {
        Directory.CreateDirectory(folder);
        path = Path.Combine(folder, VelumProductRegistryReportHtmlBuilder.BuildFileName(DateTime.Now));
        File.WriteAllText(path, html, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
      }
      catch (Exception ex)
      {
        MessageBox.Show(
            this,
            "Не удалось сохранить отчёт:\n" + ex.Message,
            "Реестр документов",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
        return;
      }

      DialogResult open = MessageBox.Show(
          this,
          "Отчёт сохранён:\n" + path + "\n\nОткрыть отчёт в браузере?",
          "Реестр документов",
          MessageBoxButtons.YesNo,
          MessageBoxIcon.Information);
      if (open != DialogResult.Yes)
        return;

      try
      {
        Process.Start(new ProcessStartInfo
        {
          FileName = path,
          UseShellExecute = true
        });
      }
      catch (Exception ex)
      {
        MessageBox.Show(
            this,
            "Не удалось открыть отчёт:\n" + ex.Message,
            "Реестр документов",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
      }
    }

    private string GetSelectedFolderPathDisplay(int folderId)
    {
      var folder = _store.GetFolder(folderId);
      if (folder == null)
        return "(неизвестно)";

      var parts = new List<string>();
      int currentId = folderId;
      int depth = 0;
      while (currentId > 0 && depth < 50)
      {
        var f = _store.GetFolder(currentId);
        if (f == null)
          break;
        parts.Insert(0, f.Name ?? string.Empty);
        if (f.ParentId <= 0)
          break;
        currentId = f.ParentId;
        depth++;
      }
      return string.Join(@"\", parts);
    }

    private void CopySelectedDxfFiles()
    {
      if (_indexing || _pathCheckRunning || _updatingProperties)
        return;

      List<VelumProductItem> selected = GetSelectedItems();
      if (selected.Count == 0)
      {
        MessageBox.Show(
            this,
            "Выделите записи в списке.",
            "Реестр документов",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      var copyItems = new List<VelumProductItem>();
      var skipped = new List<string>();
      for (int i = 0; i < selected.Count; i++)
      {
        VelumProductItem item = selected[i];
        if (VelumProductRegistryIntegrityRules.IsDxfPath(item.FilePath))
          copyItems.Add(item);
        else
          skipped.Add(FormatItemLabel(item));
      }

      if (skipped.Count > 0)
      {
        if (copyItems.Count == 0)
        {
          MessageBox.Show(
              this,
              "Выделенные записи не являются DXF-файлами.",
              "Реестр документов",
              MessageBoxButtons.OK,
              MessageBoxIcon.Information);
          return;
        }

        ShowSkippedRecordsMessage("не будут скопированы", skipped);
      }

      string destination;
      if (!VelumFolderBrowser.TrySelect(
              this,
              "Укажите каталог для копирования DXF",
              null,
              out destination))
        return;

      if (string.IsNullOrWhiteSpace(destination) || !Directory.Exists(destination))
      {
        MessageBox.Show(
            this,
            "Выбранный каталог недоступен.",
            "Реестр документов",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return;
      }

      int copied = 0;
      int failed = 0;
      var errors = new List<string>();
      for (int i = 0; i < copyItems.Count; i++)
      {
        VelumProductItem item = copyItems[i];
        string sourcePath = VelumProductRegistryStore.NormalizeFilePathKey(item.FilePath);
        string label = FormatItemLabel(item);
        if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
        {
          failed++;
          errors.Add(label + ": файл не найден");
          continue;
        }

        string targetPath = Path.Combine(destination, Path.GetFileName(sourcePath));
        try
        {
          File.Copy(sourcePath, targetPath, overwrite: true);
          copied++;
        }
        catch (Exception ex)
        {
          failed++;
          if (errors.Count < 15)
            errors.Add(label + ": " + ex.Message);
        }
      }

      var summary = new StringBuilder();
      summary.AppendLine("Копирование завершено.");
      summary.AppendLine("Скопировано: " + copied);
      if (failed > 0)
        summary.AppendLine("Ошибок: " + failed);
      if (errors.Count > 0)
      {
        summary.AppendLine();
        foreach (string line in errors)
          summary.AppendLine("• " + line);
      }

      MessageBox.Show(
          this,
          summary.ToString().TrimEnd(),
          "Реестр документов",
          MessageBoxButtons.OK,
          failed > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
    }

    private void SelectAllListItems()
    {
      if (_listView.Items.Count == 0)
        return;

      _listView.BeginUpdate();
      try
      {
        foreach (ListViewItem row in _listView.Items)
          row.Selected = true;
      }
      finally
      {
        _listView.EndUpdate();
      }
    }

    private void AddFolder()
    {
      if (!_isAdmin)
        return;

      int parentId = GetSelectedFolderId() ?? 0;
      VelumProductFolder folder = _store.AddFolder(parentId, "Новый каталог");
      RebuildTree(folder.Id);
      TreeNode node = FindNodeByFolderId(_folderTreeView.Nodes, folder.Id);
      if (node != null)
      {
        _folderTreeView.SelectedNode = node;
        node.BeginEdit();
      }
    }

    private void DeleteSelectedFolder()
    {
      if (!_isAdmin)
        return;

      int? folderId = GetSelectedFolderId();
      if (folderId == null)
        return;

      VelumProductFolder folder = _store.GetFolder(folderId.Value);
      if (folder == null)
        return;

      DialogResult confirm = MessageBox.Show(
          this,
          "Удалить каталог «" + folder.Name + "» и все вложенные каталоги с записями?",
          "Реестр документов",
          MessageBoxButtons.YesNo,
          MessageBoxIcon.Warning,
          MessageBoxDefaultButton.Button2);
      if (confirm != DialogResult.Yes)
        return;

      int parentId = folder.ParentId;
      _store.DeleteFolderCascade(folderId.Value);
      ClearFolderSearch(keepText: true);
      RebuildTree(parentId > 0 ? parentId : (int?)null);
      BindList();
    }

    private void BeginEditSelectedFolder()
    {
      if (!_isAdmin)
        return;

      TreeNode node = _folderTreeView.SelectedNode;
      if (node == null)
        return;
      if (!node.IsEditing)
        node.BeginEdit();
    }

    private void EditSelectedFolderDescription()
    {
      if (!_isAdmin)
        return;

      int? folderId = GetSelectedFolderId();
      if (folderId == null)
        return;

      VelumProductFolder folder = _store.GetFolder(folderId.Value);
      if (folder == null)
        return;

      using (var form = new VelumProductRegistryFolderDescriptionForm(folder.Name, folder.Description))
      {
        if (form.ShowDialog(this) != DialogResult.OK)
          return;

        _store.SetFolderDescription(folderId.Value, form.DescriptionText);
        TreeNode node = FindNodeByFolderId(_folderTreeView.Nodes, folderId.Value);
        if (node != null)
          ApplyFolderNodeToolTip(node, _store.GetFolder(folderId.Value));
        _folderTreeView.Invalidate();
      }
    }

    private void OnFolderTreeDrawNode(object sender, DrawTreeNodeEventArgs e)
    {
      if (e.Node == null || e.Bounds.Width <= 0 || e.Bounds.Height <= 0)
        return;

      if (e.Node.IsEditing)
      {
        e.DrawDefault = true;
        return;
      }

      bool highlighted = e.Node == _searchHighlightNode;
      bool selected = (e.State & TreeNodeStates.Selected) != 0;
      bool showDescriptions = _chkShowDescriptions.Checked;

      Color backColor;
      if (highlighted)
        backColor = Color.Yellow;
      else if (selected)
        backColor = SystemColors.Highlight;
      else
        backColor = _folderTreeView.BackColor;

      using (var backBrush = new SolidBrush(backColor))
        e.Graphics.FillRectangle(backBrush, e.Bounds);

      VelumProductFolder folder = null;
      if (e.Node.Tag is int)
        folder = _store.GetFolder((int)e.Node.Tag);

      string name = folder != null ? (folder.Name ?? string.Empty) : (e.Node.Text ?? string.Empty);
      string description = folder != null ? (folder.Description ?? string.Empty) : string.Empty;

      Font nameFont = _folderTreeView.Font;
      Color nameColor = selected && !highlighted
          ? SystemColors.HighlightText
          : _folderTreeView.ForeColor;

      var nameBounds = new Rectangle(
          e.Bounds.X + 2,
          e.Bounds.Y + 1,
          Math.Max(1, e.Bounds.Width - 4),
          showDescriptions ? Math.Max(1, e.Bounds.Height / 2) : Math.Max(1, e.Bounds.Height - 2));

      TextRenderer.DrawText(
          e.Graphics,
          name,
          nameFont,
          nameBounds,
          nameColor,
          TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);

      if (showDescriptions)
      {
        string displayDescription = TruncateDescriptionForDisplay(
            description,
            e.Graphics,
            _folderTreeView.Font,
            nameBounds.Width);

        Color descColor = selected && !highlighted
            ? Color.FromArgb(200, 200, 200)
            : SystemColors.GrayText;

        var descBounds = new Rectangle(
            e.Bounds.X + 2,
            e.Bounds.Y + (e.Bounds.Height / 2),
            Math.Max(1, e.Bounds.Width - 4),
            Math.Max(1, e.Bounds.Height / 2));

        TextRenderer.DrawText(
            e.Graphics,
            displayDescription,
            _folderTreeView.Font,
            descBounds,
            descColor,
            TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
      }

      if ((e.State & TreeNodeStates.Focused) != 0 && selected)
        ControlPaint.DrawFocusRectangle(e.Graphics, e.Bounds);

      e.DrawDefault = false;
    }

    private static string TruncateDescriptionForDisplay(string description, Graphics graphics, Font font, int maxWidth)
    {
      string text = (description ?? string.Empty).Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ').Trim();
      if (text.Length == 0)
        return string.Empty;

      bool capped = false;
      const int maxDescriptionChars = 510;
      if (text.Length > maxDescriptionChars)
      {
        text = text.Substring(0, maxDescriptionChars);
        capped = true;
      }

      if (maxWidth <= 8)
        return capped ? text + "..." : text;

      Size measured = TextRenderer.MeasureText(graphics, text, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.SingleLine);
      if (measured.Width <= maxWidth)
        return capped ? text + "..." : text;

      string ellipsis = "...";
      int low = 0;
      int high = text.Length;
      string best = ellipsis;
      while (low <= high)
      {
        int mid = (low + high) / 2;
        string candidate = text.Substring(0, mid) + ellipsis;
        Size size = TextRenderer.MeasureText(graphics, candidate, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.SingleLine);
        if (size.Width <= maxWidth)
        {
          best = candidate;
          low = mid + 1;
        }
        else
          high = mid - 1;
      }

      return best;
    }

    private void LoadFolderIndex()
    {
      if (!_isAdmin || _indexing)
        return;

      // До любых модальных диалогов: LabelEdit + modal/message pump в host SolidWorks нестабилен.
      _folderTreeView.LabelEdit = false;
      CommitOrCancelFolderLabelEdit();

      // Если есть активная сборка — выделить её узел в дереве каталогов.
      int? activeFolderId = ResolveActiveDocumentFolderId();
      if (activeFolderId.HasValue && activeFolderId.Value > 0)
      {
        TreeNode activeNode = FindNodeByFolderId(_folderTreeView.Nodes, activeFolderId.Value);
        if (activeNode != null)
          _folderTreeView.SelectedNode = activeNode;
      }

      int? parentFolderId = GetSelectedFolderId();
      if (parentFolderId == null)
      {
        _folderTreeView.LabelEdit = _isAdmin;
        MessageBox.Show(this, "Выберите каталог в дереве.", "Реестр документов", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return;
      }

      // Только что созданный каталог должен быть уже на диске до индексации.
      VelumProductFolder parentFolder = _store.GetFolder(parentFolderId.Value);
      if (parentFolder == null)
      {
        _folderTreeView.LabelEdit = _isAdmin;
        MessageBox.Show(this, "Выбранный каталог не найден.", "Реестр документов", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return;
      }

      string sourcePath;
      string initialCatalogPath = ResolveActiveDocumentCatalogPath();
      if (!VelumFolderBrowser.TrySelect(
              this,
              "Выберите каталог для индексации файлов",
              initialCatalogPath,
              out sourcePath))
      {
        _folderTreeView.LabelEdit = _isAdmin;
        return;
      }

      if (string.IsNullOrWhiteSpace(sourcePath) || !Directory.Exists(sourcePath))
      {
        _folderTreeView.LabelEdit = _isAdmin;
        MessageBox.Show(this, "Выбранный каталог недоступен.", "Реестр документов", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return;
      }

      DialogResult confirm = MessageBox.Show(
          this,
          "Проиндексировать файлы из указанного каталога в выбранный узел реестра?\n\n"
              + "Уже учтённые файлы (тот же полный путь) будут пропущены.",
          "Реестр документов",
          MessageBoxButtons.YesNo,
          MessageBoxIcon.Question,
          MessageBoxDefaultButton.Button1);
      if (confirm != DialogResult.Yes)
      {
        _folderTreeView.LabelEdit = _isAdmin;
        return;
      }

      StartFolderIndexing(parentFolderId.Value, sourcePath);
    }

    /// <summary>
    /// Завершает/отменяет редактирование имени узла перед индексацией —
    /// иначе Application/DoEvents и смена дерева в режиме LabelEdit роняют SolidWorks.
    /// </summary>
    private void CommitOrCancelFolderLabelEdit()
    {
      TreeNode node = _folderTreeView.SelectedNode;
      if (node == null)
        return;

      try
      {
        if (node.IsEditing)
          node.EndEdit(false);
      }
      catch
      {
        try
        {
          node.EndEdit(true);
        }
        catch
        {
        }
      }
    }

    private void StartFolderIndexing(int parentFolderId, string sourcePath)
    {
      _indexing = true;
      _stopRequested = false;
      SetIndexingUi(true);

      string swExtra = null;
      try
      {
        if (_swApp?.Sw != null)
        {
          string rev = _swApp.Sw.RevisionNumber();
          swExtra = "swRevision=\"" + (rev ?? string.Empty) + "\"";
        }
      }
      catch
      {
        swExtra = "swRevision=?";
      }

      // Режим «догрузка в типовой каталог»: имя выделенного узла = автоимя из folderAutoNames
      // (Чертежи/Детали/…) → только файлы этого типа, без создания вложенных субкаталогов.
      VelumProductFolder parentFolder = _store.GetFolder(parentFolderId);
      List<VelumProductFolderAutoNameMapping> mappingsForMode =
          VelumProductRegistryFolderAutoNames.LoadOrCreate();
      string reservedCategory = VelumProductRegistryFolderAutoNames.TryMatchReservedFolderName(
          parentFolder != null ? parentFolder.Name : null,
          mappingsForMode);
      bool placeItemsDirectlyInParent = !string.IsNullOrEmpty(reservedCategory);

      int sessionId = VelumProductRegistryIndexTrace.BeginSession(sourcePath, parentFolderId, swExtra);
      _indexTraceSession = sessionId;
      if (VelumAppConfig.SolidHomeostasisDebugLog)
        VelumProductRegistryIndexTrace.Mark(
            "ui.after_dialogs",
            "logs=\"" + VelumProductRegistryIndexTrace.LogsDirectory + "\""
                + (placeItemsDirectlyInParent
                    ? " reservedCategory=\"" + reservedCategory + "\""
                    : " mode=classify"),
            sessionId);

      var worker = new BackgroundWorker
      {
        WorkerReportsProgress = true
      };
      _indexWorker = worker;

      worker.DoWork += (s, e) =>
      {
        if (VelumAppConfig.SolidHomeostasisDebugLog)
          VelumProductRegistryIndexTrace.Mark("scan.begin", null, sessionId);
        var errors = new List<string>();
        List<VelumProductFolderAutoNameMapping> mappings = VelumProductRegistryFolderAutoNames.LoadOrCreate();
        Dictionary<string, string> extensionMap = VelumProductRegistryFolderAutoNames.BuildExtensionMap(mappings);
        List<string> folderOrder = VelumProductRegistryFolderAutoNames.BuildFolderOrder(mappings);

        if (VelumAppConfig.SolidHomeostasisDebugLog)
          VelumProductRegistryIndexTrace.Mark("scan.collect.begin", null, sessionId);
        int skippedTempOrHidden;
        List<string> files = CollectFilesRecursive(
            sourcePath,
            errors,
            worker,
            () => _stopRequested,
            out skippedTempOrHidden);
        if (_stopRequested)
        {
          e.Result = new FolderIndexScanResult
          {
            Cancelled = true,
            TraceSessionId = sessionId
          };
          return;
        }
        if (VelumAppConfig.SolidHomeostasisDebugLog)
          VelumProductRegistryIndexTrace.Mark(
              "scan.collect.done",
              "files=" + files.Count + " skippedHidden=" + skippedTempOrHidden + " errors=" + errors.Count,
              sessionId);

        var groups = new Dictionary<string, List<string>>(StringComparer.CurrentCultureIgnoreCase);
        if (placeItemsDirectlyInParent)
        {
          folderOrder = new List<string> { reservedCategory };
          groups[reservedCategory] = new List<string>();
          foreach (string filePath in files)
          {
            string category = VelumProductRegistryFolderAutoNames.ClassifyFileFolder(filePath, extensionMap);
            if (!string.Equals(category, reservedCategory, StringComparison.CurrentCultureIgnoreCase))
              continue;
            groups[reservedCategory].Add(filePath);
          }
        }
        else
        {
          foreach (string category in folderOrder)
            groups[category] = new List<string>();

          foreach (string filePath in files)
          {
            string category = VelumProductRegistryFolderAutoNames.ClassifyFileFolder(filePath, extensionMap);
            // Пропускаем файлы без зарегистрированных расширений (не представлены в настройках автоимён каталогов).
            if (string.IsNullOrEmpty(category))
              continue;

            List<string> bucket;
            if (!groups.TryGetValue(category, out bucket))
            {
              bucket = new List<string>();
              groups[category] = bucket;
              folderOrder.Add(category);
            }

            bucket.Add(filePath);
          }
        }

        if (VelumAppConfig.SolidHomeostasisDebugLog)
          VelumProductRegistryIndexTrace.Mark(
              "scan.classify.done",
              "categories=" + folderOrder.Count
                  + (placeItemsDirectlyInParent ? " directIntoParent=1" : string.Empty),
              sessionId);

        e.Result = new FolderIndexScanResult
        {
          ParentFolderId = parentFolderId,
          PlaceItemsDirectlyInParent = placeItemsDirectlyInParent,
          FolderOrder = folderOrder,
          Groups = groups,
          Errors = errors,
          SkippedTempOrHidden = skippedTempOrHidden,
          TraceSessionId = sessionId
        };
      };

      worker.ProgressChanged += (s, e) =>
      {
        // В host SolidWorks SyncContext у BackgroundWorker иногда отсутствует —
        // ProgressChanged/Completed могут прийти не на UI-потоке формы.
        if (IsDisposed)
          return;
        if (InvokeRequired)
        {
          try
          {
            BeginInvoke(new Action(() =>
            {
              if (IsDisposed)
                return;
              if (e.ProgressPercentage >= 0 && e.ProgressPercentage <= 100)
                SetIndexProgressPercent(e.ProgressPercentage);
            }));
          }
          catch (ObjectDisposedException)
          {
          }
          return;
        }

        if (e.ProgressPercentage >= 0 && e.ProgressPercentage <= 100)
          SetIndexProgressPercent(e.ProgressPercentage);
      };

      worker.RunWorkerCompleted += (s, e) =>
      {
        // Всегда завершать на UI-потоке формы: иначе RebuildTree/BindList
        // дают InvalidOperationException (cross-thread) и при throw валят SolidWorks.
        // Данные к этому моменту уже могут быть на диске — как в ProductRegistryIndex.trace.log.
        if (IsDisposed)
          return;
        if (InvokeRequired)
        {
          try
          {
            if (VelumAppConfig.SolidHomeostasisDebugLog)
              VelumProductRegistryIndexTrace.Mark(
                  "ui.worker_completed.marshal",
                  "fromTid=" + System.Environment.CurrentManagedThreadId,
                  sessionId);
            BeginInvoke(new Action(() => FinishFolderIndexOnUiThread(e, sessionId)));
          }
          catch (ObjectDisposedException)
          {
            if (VelumAppConfig.SolidHomeostasisDebugLog)
              VelumProductRegistryIndexTrace.EndSessionFail(sessionId, null);
          }
          return;
        }

        FinishFolderIndexOnUiThread(e, sessionId);
      };

      if (VelumAppConfig.SolidHomeostasisDebugLog)
        VelumProductRegistryIndexTrace.Mark("scan.worker_start", null, sessionId);
      worker.RunWorkerAsync();
    }

    private void FinishFolderIndexOnUiThread(RunWorkerCompletedEventArgs e, int sessionId)
    {
      try
      {
        if (VelumAppConfig.SolidHomeostasisDebugLog)
          VelumProductRegistryIndexTrace.Mark(
              "ui.worker_completed",
              "tid=" + System.Environment.CurrentManagedThreadId
                  + " invokeRequired=" + (InvokeRequired ? "1" : "0"),
              sessionId);

        if (e.Error != null)
        {
          if (VelumAppConfig.SolidHomeostasisDebugLog)
            VelumProductRegistryIndexTrace.EndSessionFail(sessionId, e.Error);
          MessageBox.Show(
              this,
              "Ошибка индексации:\n" + e.Error.Message
                  + "\n\nДиагностика: " + VelumProductRegistryIndexTrace.LastFilePath,
              "Реестр документов",
              MessageBoxButtons.OK,
              MessageBoxIcon.Error);
          return;
        }

        var scan = e.Result as FolderIndexScanResult;
        if (scan == null)
        {
          if (VelumAppConfig.SolidHomeostasisDebugLog)
            VelumProductRegistryIndexTrace.Mark("ui.worker_completed.null_result", null, sessionId);
          if (VelumAppConfig.SolidHomeostasisDebugLog)
            VelumProductRegistryIndexTrace.EndSessionFail(sessionId, null);
          return;
        }

        ApplyFolderIndexResult(scan);
      }
      catch (Exception ex)
      {
        // Не пробрасывать: необработанное исключение на UI-потоке в add-in роняет SolidWorks.
        if (VelumAppConfig.SolidHomeostasisDebugLog)
          VelumProductRegistryIndexTrace.EndSessionFail(sessionId, ex);
        try
        {
          MessageBox.Show(
              this,
              "Ошибка после индексации:\n" + ex.Message
                  + "\n\nФайлы могли уже сохраниться в реестре."
                  + "\nДиагностика: " + VelumProductRegistryIndexTrace.LastFilePath,
              "Реестр документов",
              MessageBoxButtons.OK,
              MessageBoxIcon.Error);
        }
        catch
        {
        }
      }
      finally
      {
        _indexing = false;
        _indexWorker = null;
        _indexTraceSession = 0;
        try
        {
          if (!IsDisposed)
            SetIndexingUi(false);
        }
        catch
        {
        }
      }
    }

    private void ApplyFolderIndexResult(FolderIndexScanResult scan)
    {
      int sessionId = scan.TraceSessionId > 0 ? scan.TraceSessionId : _indexTraceSession;
      if (VelumAppConfig.SolidHomeostasisDebugLog)
        VelumProductRegistryIndexTrace.Mark("apply.begin", null, sessionId);

      if (scan.Cancelled)
      {
        if (VelumAppConfig.SolidHomeostasisDebugLog)
          VelumProductRegistryIndexTrace.Mark("apply.cancelled", null, sessionId);
        if (VelumAppConfig.SolidHomeostasisDebugLog)
          VelumProductRegistryIndexTrace.EndSessionOk(sessionId, "cancelled");
        MessageBox.Show(
            this,
            "Индексация прервана.",
            "Реестр документов",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      var errors = new List<string>(scan.Errors ?? new List<string>());
      int addedFolders = 0;
      int addedItems = 0;
      int skippedExisting = 0;
      int skippedTempOrHidden = scan.SkippedTempOrHidden;
      var addedNames = new List<string>();

      int totalFiles = 0;
      if (scan.FolderOrder != null && scan.Groups != null)
      {
        foreach (string category in scan.FolderOrder)
        {
          List<string> files;
          if (scan.Groups.TryGetValue(category, out files))
            totalFiles += files.Count;
        }
      }

      if (totalFiles == 0)
      {
        string emptyMessage;
        if (errors.Count > 0)
          emptyMessage = BuildErrorMessage("Файлы не найдены.", errors);
        else if (skippedTempOrHidden > 0)
          emptyMessage = "Подходящих файлов не найдено.\nПропущено скрытых/временных: " + skippedTempOrHidden;
        else
          emptyMessage = "В выбранном каталоге файлы не найдены.";

        if (VelumAppConfig.SolidHomeostasisDebugLog)
          VelumProductRegistryIndexTrace.Mark("apply.empty", "errors=" + errors.Count, sessionId);
        if (VelumAppConfig.SolidHomeostasisDebugLog)
          VelumProductRegistryIndexTrace.Mark("ui.RebuildTree.begin", "empty", sessionId);
        RebuildTree(scan.ParentFolderId);
        if (VelumAppConfig.SolidHomeostasisDebugLog)
          VelumProductRegistryIndexTrace.Mark("ui.RebuildTree.end", "empty", sessionId);
        if (VelumAppConfig.SolidHomeostasisDebugLog)
          VelumProductRegistryIndexTrace.Mark("ui.BindList.begin", "empty", sessionId);
        BindList();
        if (VelumAppConfig.SolidHomeostasisDebugLog)
          VelumProductRegistryIndexTrace.Mark("ui.BindList.end", "empty", sessionId);
        if (VelumAppConfig.SolidHomeostasisDebugLog)
          VelumProductRegistryIndexTrace.Mark("ui.MessageBox.begin", "empty", sessionId);
        MessageBox.Show(
            this,
            emptyMessage,
            "Реестр документов",
            MessageBoxButtons.OK,
            errors.Count == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        if (VelumAppConfig.SolidHomeostasisDebugLog)
          VelumProductRegistryIndexTrace.Mark("ui.MessageBox.end", "empty", sessionId);
        if (VelumAppConfig.SolidHomeostasisDebugLog)
          VelumProductRegistryIndexTrace.EndSessionOk(sessionId, "empty");
        return;
      }

      // Шкала всегда 0..100: иначе после индексации Maximum остаётся = числу файлов,
      // а ProgressChanged снова пишет процент 0..100 → ArgumentOutOfRangeException на Value.
      ResetIndexProgressBar(100);

      int processed = 0;
      bool stopped = false;
      var knownPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (string path in _store.GetAllFilePaths())
        knownPaths.Add(path);

      try
      {
        if (VelumAppConfig.SolidHomeostasisDebugLog)
          VelumProductRegistryIndexTrace.Mark(
              "apply.loop.begin",
              "totalFiles=" + totalFiles,
              sessionId);

        foreach (string category in scan.FolderOrder)
        {
          List<string> files;
          if (!scan.Groups.TryGetValue(category, out files) || files.Count == 0)
            continue;

          // Каталог категории создаём только когда реально добавляем хотя бы одну запись.
          // Иначе при повторной индексации появляются пустые PDF/DXF и т.п., а итог пишет «ничего не добавлено».
          // В режиме PlaceItemsDirectlyInParent записи идут прямо в выделенный каталог (без субпапок).
          VelumProductFolder categoryFolder = null;
          if (scan.PlaceItemsDirectlyInParent)
          {
            categoryFolder = _store.GetFolder(scan.ParentFolderId);
            if (categoryFolder == null)
            {
              errors.Add("Выбранный каталог не найден.");
              break;
            }
          }

          foreach (string filePath in files)
          {
            if (_stopRequested)
            {
              stopped = true;
              break;
            }

            if (processed > 0 && processed % 10 == 0)
            {
              Application.DoEvents();
              if (_stopRequested)
              {
                stopped = true;
                break;
              }
            }

            processed++;
            try
            {
              string pathKey = VelumProductRegistryStore.NormalizeFilePathKey(filePath);
              if (string.IsNullOrEmpty(pathKey) || knownPaths.Contains(pathKey))
              {
                skippedExisting++;
              }
              else
              {
                string designation = Path.GetFileNameWithoutExtension(filePath) ?? string.Empty;
                if (categoryFolder == null)
                {
                  try
                  {
                    VelumProductFolder before = _store.FindChildFolderByName(scan.ParentFolderId, category);
                    categoryFolder = _store.GetOrCreateChildFolder(scan.ParentFolderId, category, persist: false);
                    if (before == null)
                      addedFolders++;
                  }
                  catch (Exception ex)
                  {
                    errors.Add("Каталог «" + category + "»: " + ex.Message);
                    continue;
                  }
                }

                VelumProductItem created = _store.AddItem(
                    categoryFolder.Id,
                    designation,
                    string.Empty,
                    pathKey,
                    persist: false);
                knownPaths.Add(pathKey);
                SetPathStatus(created.Id, VelumProductRegistryPathStatus.Ok);
                addedItems++;
                addedNames.Add(string.IsNullOrEmpty(designation) ? Path.GetFileName(filePath) : designation);
              }
            }
            catch (Exception ex)
            {
              errors.Add(filePath + ": " + ex.Message);
            }

            if (VelumAppConfig.SolidHomeostasisDebugLog)
              VelumProductRegistryIndexTrace.MarkApplyProgress(processed, totalFiles, sessionId);

            if (processed % 25 == 0 || processed >= totalFiles)
            {
              SetIndexProgressPercent(totalFiles > 0 ? (processed * 100) / totalFiles : 100);
            }
          }

          if (stopped)
            break;
        }

        if (VelumAppConfig.SolidHomeostasisDebugLog)
          VelumProductRegistryIndexTrace.Mark(
              "apply.loop.done",
              "added=" + addedItems + " skippedExisting=" + skippedExisting,
              sessionId);
        if (VelumAppConfig.SolidHomeostasisDebugLog)
          VelumProductRegistryIndexTrace.Mark("store.Save.begin", null, sessionId);
        _store.Save();
        if (VelumAppConfig.SolidHomeostasisDebugLog)
          VelumProductRegistryIndexTrace.Mark("store.Save.end", null, sessionId);

        VelumProductRegistryIntegrityScheduler.RevalidateCachedItemProblems();
        if (VelumAppConfig.SolidHomeostasisDebugLog)
          VelumProductRegistryIndexTrace.Mark("integrity.revalidate_cached.done", null, sessionId);
      }
      catch (Exception ex)
      {
        errors.Add("Сохранение реестра: " + ex.Message);
        if (VelumAppConfig.SolidHomeostasisDebugLog)
          VelumProductRegistryIndexTrace.Mark(
              "apply.loop.exception",
              ex.GetType().Name + ": " + ex.Message,
              sessionId);
      }

      if (VelumAppConfig.SolidHomeostasisDebugLog)
        VelumProductRegistryIndexTrace.Mark("ui.RebuildTree.begin", null, sessionId);
      RebuildTree(scan.ParentFolderId);
      if (VelumAppConfig.SolidHomeostasisDebugLog)
        VelumProductRegistryIndexTrace.Mark("ui.RebuildTree.end", null, sessionId);
      if (VelumAppConfig.SolidHomeostasisDebugLog)
        VelumProductRegistryIndexTrace.Mark("ui.BindList.begin", null, sessionId);
      BindList();
      if (VelumAppConfig.SolidHomeostasisDebugLog)
        VelumProductRegistryIndexTrace.Mark("ui.BindList.end", null, sessionId);

      string summary = BuildIndexSummaryMessage(
          addedFolders,
          addedItems,
          skippedExisting,
          skippedTempOrHidden,
          addedNames);
      if (stopped)
      {
        summary = "Индексация прервана." + System.Environment.NewLine + System.Environment.NewLine + summary;
      }
      if (VelumAppConfig.SolidHomeostasisDebugLog)
        VelumProductRegistryIndexTrace.Mark("ui.MessageBox.begin", "summary", sessionId);
      if (errors.Count == 0)
      {
        MessageBox.Show(
            this,
            summary,
            "Реестр документов",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
      }
      else
      {
        MessageBox.Show(
            this,
            BuildErrorMessage(summary, errors),
            "Реестр документов",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
      }

      if (VelumAppConfig.SolidHomeostasisDebugLog)
        VelumProductRegistryIndexTrace.Mark("ui.MessageBox.end", "summary", sessionId);
      if (VelumAppConfig.SolidHomeostasisDebugLog)
        VelumProductRegistryIndexTrace.EndSessionOk(
            sessionId,
            "added=" + addedItems + " folders=" + addedFolders + " errors=" + errors.Count);
    }

    private static string BuildIndexSummaryMessage(
        int addedFolders,
        int addedItems,
        int skippedExisting,
        int skippedTempOrHidden,
        List<string> addedNames)
    {
      var text = new StringBuilder();
      if (addedItems == 0)
      {
        text.AppendLine("Новых файлов для добавления не найдено.");
        if (addedFolders > 0)
          text.AppendLine("Каталогов создано: " + addedFolders);
        if (skippedExisting > 0)
          text.AppendLine("Уже в реестре (тот же путь): " + skippedExisting);
        if (skippedTempOrHidden > 0)
          text.AppendLine("Пропущено скрытых/временных: " + skippedTempOrHidden);
        return text.ToString().TrimEnd();
      }

      text.AppendLine("Индексация завершена.");
      text.AppendLine("Каталогов создано: " + addedFolders);
      text.AppendLine("Записей добавлено: " + addedItems);
      if (skippedExisting > 0)
        text.AppendLine("Пропущено (уже в реестре, тот же путь): " + skippedExisting);
      if (skippedTempOrHidden > 0)
        text.AppendLine("Пропущено скрытых/временных: " + skippedTempOrHidden);

      text.AppendLine();
      text.AppendLine("Добавленные:");
      int limit = Math.Min(addedNames.Count, 40);
      for (int i = 0; i < limit; i++)
        text.AppendLine("• " + addedNames[i]);
      if (addedNames.Count > limit)
        text.AppendLine("… и ещё " + (addedNames.Count - limit));

      return text.ToString().TrimEnd();
    }

    private void SetIndexingUi(bool indexing)
    {
      if (indexing)
        CommitOrCancelFolderLabelEdit();

      _indexProgressBar.Visible = indexing;
      _treePanel.RowStyles[4].Height = indexing ? 22F : 0F;
      if (indexing)
      {
        ResetIndexProgressBar(100);
        _indexProgressBar.Style = ProgressBarStyle.Marquee;
        _indexProgressBar.MarqueeAnimationSpeed = 30;
      }
      else
      {
        _indexProgressBar.MarqueeAnimationSpeed = 0;
        ResetIndexProgressBar(100);
      }

      _folderTreeView.Enabled = !indexing;
      _folderTreeView.LabelEdit = _isAdmin && !indexing;
      _listView.Enabled = !indexing;
      _btnVerifyPaths.Enabled = !indexing && !_pathCheckRunning;
      _btnStop.Enabled = indexing;
      _filterStatusBox.Enabled = !indexing;
      _filterDesignationBox.Enabled = !indexing;
      _filterNameBox.Enabled = !indexing;
      _btnFilterApply.Enabled = !indexing;
      _btnFilterReset.Enabled = !indexing;
      _btnFilterHelp.Enabled = !indexing;
      if (_treeMenu != null)
        _treeMenu.Enabled = !indexing;
      if (_listMenu != null)
        _listMenu.Enabled = !indexing;
      Cursor = indexing ? Cursors.WaitCursor : Cursors.Default;
    }

    /// <summary>
    /// Сбрасывает ProgressBar в безопасное состояние (Value до Maximum),
    /// чтобы не получить ArgumentOutOfRangeException на Value.
    /// </summary>
    private void ResetIndexProgressBar(int maximum)
    {
      if (maximum < 1)
        maximum = 1;

      _indexProgressBar.Style = ProgressBarStyle.Continuous;
      // Сначала Value, потом Maximum: иначе при снижении Maximum при большом Value WinForms может бросить исключение.
      if (_indexProgressBar.Value != 0)
        _indexProgressBar.Value = 0;
      _indexProgressBar.Minimum = 0;
      _indexProgressBar.Maximum = maximum;
    }

    private void SetIndexProgressPercent(int percent)
    {
      if (percent < 0)
        percent = 0;
      if (percent > 100)
        percent = 100;

      if (_indexProgressBar.Style != ProgressBarStyle.Continuous)
        _indexProgressBar.Style = ProgressBarStyle.Continuous;

      if (_indexProgressBar.Minimum != 0 || _indexProgressBar.Maximum != 100)
        ResetIndexProgressBar(100);

      if (_indexProgressBar.Value != percent)
        _indexProgressBar.Value = percent;
    }

    private static List<string> CollectFilesRecursive(
        string rootPath,
        List<string> errors,
        BackgroundWorker worker,
        Func<bool> isCancelled,
        out int skippedTempOrHidden)
    {
      var files = new List<string>();
      var pending = new Stack<string>();
      pending.Push(rootPath);
      int scannedDirs = 0;
      skippedTempOrHidden = 0;

      while (pending.Count > 0)
      {
        if (isCancelled != null && isCancelled())
          break;

        string dir = pending.Pop();
        scannedDirs++;
        if (worker != null && scannedDirs % 5 == 0)
          worker.ReportProgress(Math.Min(95, scannedDirs % 100));

        try
        {
          foreach (string file in Directory.EnumerateFiles(dir))
          {
            if (ShouldSkipIndexedFile(file))
            {
              skippedTempOrHidden++;
              continue;
            }

            files.Add(file);
          }
        }
        catch (Exception ex)
        {
          errors.Add(dir + ": " + ex.Message);
        }

        try
        {
          foreach (string subDir in Directory.EnumerateDirectories(dir))
          {
            if (ShouldSkipIndexedDirectory(subDir))
              continue;
            pending.Push(subDir);
          }
        }
        catch (Exception ex)
        {
          errors.Add(dir + ": " + ex.Message);
        }
      }

      if (worker != null)
        worker.ReportProgress(100);

      return files;
    }

    private static bool ShouldSkipIndexedFile(string filePath)
    {
      string name = Path.GetFileName(filePath);
      if (string.IsNullOrEmpty(name))
        return true;

      // Временные/lock-файлы Office и подобные (~$001.010....).
      if (name.StartsWith("~$", StringComparison.Ordinal))
        return true;
      if (name.StartsWith(".~", StringComparison.Ordinal))
        return true;
      if (name.EndsWith("~", StringComparison.Ordinal) && name.Length > 1)
        return true;
      if (string.Equals(name, "Thumbs.db", StringComparison.OrdinalIgnoreCase)
          || string.Equals(name, "desktop.ini", StringComparison.OrdinalIgnoreCase))
        return true;

      try
      {
        FileAttributes attrs = File.GetAttributes(filePath);
        if ((attrs & FileAttributes.Directory) != 0)
          return true;
        if ((attrs & FileAttributes.Hidden) != 0)
          return true;
        if ((attrs & FileAttributes.System) != 0)
          return true;
        if ((attrs & FileAttributes.Temporary) != 0)
          return true;
      }
      catch
      {
      }

      return false;
    }

    private static bool ShouldSkipIndexedDirectory(string directoryPath)
    {
      string name = Path.GetFileName(directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
      if (string.IsNullOrEmpty(name))
        return false;
      if (name.StartsWith(".", StringComparison.Ordinal))
        return true;

      try
      {
        FileAttributes attrs = File.GetAttributes(directoryPath);
        if ((attrs & FileAttributes.Hidden) != 0)
          return true;
        if ((attrs & FileAttributes.System) != 0)
          return true;
      }
      catch
      {
      }

      return false;
    }

    private static string BuildErrorMessage(string header, List<string> errors)
    {
      var text = new StringBuilder();
      text.AppendLine(header);
      text.AppendLine();
      text.AppendLine("Ошибки:");
      int limit = Math.Min(errors.Count, 30);
      for (int i = 0; i < limit; i++)
        text.AppendLine(errors[i]);
      if (errors.Count > limit)
        text.AppendLine("… и ещё " + (errors.Count - limit));
      return text.ToString().TrimEnd();
    }

    private sealed class FolderIndexScanResult
    {
      public int ParentFolderId;
      /// <summary>
      /// true — выделенный каталог сам является типовым (Чертежи/…):
      /// записи добавлять в него, не создавая дочерние автокаталоги.
      /// </summary>
      public bool PlaceItemsDirectlyInParent;
      public List<string> FolderOrder;
      public Dictionary<string, List<string>> Groups;
      public List<string> Errors;
      public int SkippedTempOrHidden;
      public int TraceSessionId;
      /// <summary>Пользователь нажал «Стоп» во время сбора файлов.</summary>
      public bool Cancelled;
    }

    private void OnFolderAfterLabelEdit(object sender, NodeLabelEditEventArgs e)
    {
      if (e.Node == null || !(e.Node.Tag is int))
      {
        e.CancelEdit = true;
        return;
      }

      if (e.Label == null)
        return;

      string name = e.Label.Trim();
      if (string.IsNullOrEmpty(name))
      {
        e.CancelEdit = true;
        return;
      }

      int folderId = (int)e.Node.Tag;
      _store.RenameFolder(folderId, name);
      e.Node.Text = name;
    }

    private void OnFolderItemDrag(object sender, ItemDragEventArgs e)
    {
      if (e.Item is TreeNode)
        DoDragDrop(e.Item, DragDropEffects.Move);
    }

    private void OnFolderDragEnter(object sender, DragEventArgs e)
    {
      if (e.Data.GetDataPresent(typeof(TreeNode)))
        e.Effect = DragDropEffects.Move;
    }

    private void OnFolderDragOver(object sender, DragEventArgs e)
    {
      Point point = _folderTreeView.PointToClient(new Point(e.X, e.Y));
      TreeNode target = _folderTreeView.GetNodeAt(point);
      TreeNode source = e.Data.GetData(typeof(TreeNode)) as TreeNode;
      if (source == null || target == null || target == source || IsNodeAncestor(source, target))
      {
        e.Effect = DragDropEffects.None;
        return;
      }

      e.Effect = DragDropEffects.Move;
      _folderTreeView.SelectedNode = target;
    }

    private void OnFolderDragDrop(object sender, DragEventArgs e)
    {
      Point point = _folderTreeView.PointToClient(new Point(e.X, e.Y));
      TreeNode target = _folderTreeView.GetNodeAt(point);
      TreeNode source = e.Data.GetData(typeof(TreeNode)) as TreeNode;
      if (source == null || target == null || !(source.Tag is int) || !(target.Tag is int))
        return;
      if (source == target || IsNodeAncestor(source, target))
        return;

      string sourceName = source.Text ?? string.Empty;
      string targetName = target.Text ?? string.Empty;
      DialogResult confirm = MessageBox.Show(
          this,
          "Переместить каталог «" + sourceName + "» в «" + targetName + "»?",
          "Реестр документов",
          MessageBoxButtons.YesNo,
          MessageBoxIcon.Question,
          MessageBoxDefaultButton.Button2);
      if (confirm != DialogResult.Yes)
        return;

      int folderId = (int)source.Tag;
      int newParentId = (int)target.Tag;
      if (!_store.MoveFolder(folderId, newParentId))
        return;

      ClearFolderSearch(keepText: true);
      RebuildTree(folderId);
    }

    private static bool IsNodeAncestor(TreeNode ancestor, TreeNode node)
    {
      TreeNode current = node;
      while (current != null)
      {
        if (current == ancestor)
          return true;
        current = current.Parent;
      }

      return false;
    }

    private void OnFolderTreeKeyDown(object sender, KeyEventArgs e)
    {
      if (!_isAdmin)
        return;

      if (e.KeyCode == Keys.Insert)
      {
        AddFolder();
        e.Handled = true;
        e.SuppressKeyPress = true;
      }
      else if (e.KeyCode == Keys.Delete)
      {
        DeleteSelectedFolder();
        e.Handled = true;
        e.SuppressKeyPress = true;
      }
      else if (e.KeyCode == Keys.F2)
      {
        BeginEditSelectedFolder();
        e.Handled = true;
        e.SuppressKeyPress = true;
      }
    }

    /// <summary>
    /// Сдвигает выделенный каталог внутри своей дочерней группы:
    /// offset &lt; 0 — вверх, offset &gt; 0 — вниз.
    /// </summary>
    private void MoveSelectedFolder(int offset)
    {
      if (!_isAdmin)
        return;

      int? folderId = GetSelectedFolderId();
      if (folderId == null)
        return;

      if (!_store.MoveFolderRelative(folderId.Value, offset))
        return;

      RebuildTree(folderId);
    }

    private void SearchFolders()
    {
      string nameFilter = (_folderSearchNameBox.Text ?? string.Empty).Trim();
      string descriptionFilter = (_folderSearchDescriptionBox.Text ?? string.Empty).Trim();
      ClearFolderHighlights();
      _folderSearchResults.Clear();
      _folderSearchIndex = -1;

      if (string.IsNullOrEmpty(nameFilter) && string.IsNullOrEmpty(descriptionFilter))
      {
        _folderSearchStatusLabel.Text = string.Empty;
        return;
      }

      CollectFolderMatches(_folderTreeView.Nodes, nameFilter, descriptionFilter, _folderSearchResults);
      if (_folderSearchResults.Count == 0)
      {
        _folderSearchStatusLabel.Text = "Совпадений не найдено";
        return;
      }

      ShowNextFolderResult();
    }

    private void CollectFolderMatches(
        TreeNodeCollection nodes,
        string nameFilter,
        string descriptionFilter,
        List<TreeNode> matches)
    {
      foreach (TreeNode node in nodes)
      {
        VelumProductFolder folder = null;
        if (node.Tag is int)
          folder = _store.GetFolder((int)node.Tag);

        string name = folder != null ? (folder.Name ?? string.Empty) : (node.Text ?? string.Empty);
        string description = folder != null ? (folder.Description ?? string.Empty) : string.Empty;

        bool nameOk = string.IsNullOrEmpty(nameFilter)
            || name.IndexOf(nameFilter, StringComparison.OrdinalIgnoreCase) >= 0;
        bool descriptionOk = string.IsNullOrEmpty(descriptionFilter)
            || description.IndexOf(descriptionFilter, StringComparison.OrdinalIgnoreCase) >= 0;

        if (nameOk && descriptionOk)
          matches.Add(node);

        CollectFolderMatches(node.Nodes, nameFilter, descriptionFilter, matches);
      }
    }

    private void ShowNextFolderResult()
    {
      if (_folderSearchResults.Count == 0)
        return;
      _folderSearchIndex++;
      if (_folderSearchIndex >= _folderSearchResults.Count)
        _folderSearchIndex = 0;
      ShowCurrentFolderResult();
    }

    private void ShowPreviousFolderResult()
    {
      if (_folderSearchResults.Count == 0)
      {
        SearchFolders();
        if (_folderSearchResults.Count == 0)
          return;
        _folderSearchIndex = 0;
        ShowCurrentFolderResult();
        return;
      }

      _folderSearchIndex--;
      if (_folderSearchIndex < 0)
        _folderSearchIndex = _folderSearchResults.Count - 1;
      ShowCurrentFolderResult();
    }

    private void ShowCurrentFolderResult()
    {
      if (_folderSearchIndex < 0 || _folderSearchIndex >= _folderSearchResults.Count)
        return;

      ClearFolderHighlights();
      TreeNode current = _folderSearchResults[_folderSearchIndex];
      _searchHighlightNode = current;
      ExpandParents(current);
      _folderTreeView.SelectedNode = current;
      current.EnsureVisible();
      _folderTreeView.Invalidate();
      _folderSearchStatusLabel.Text = "Найдено: " + (_folderSearchIndex + 1) + " из " + _folderSearchResults.Count;
      BindList();
    }

    private void ClearFolderSearch(bool keepText)
    {
      ClearFolderHighlights();
      _folderSearchResults.Clear();
      _folderSearchIndex = -1;
      _folderSearchStatusLabel.Text = string.Empty;
      if (!keepText)
      {
        _folderSearchNameBox.Text = string.Empty;
        _folderSearchDescriptionBox.Text = string.Empty;
      }
    }

    private void ClearFolderHighlights()
    {
      _searchHighlightNode = null;
      _folderTreeView.Invalidate();
    }

    private static void ExpandParents(TreeNode node)
    {
      TreeNode parent = node.Parent;
      while (parent != null)
      {
        parent.Expand();
        parent = parent.Parent;
      }
    }

    /// <summary>Ищет папку реестра по активному документу SW.</summary>
    private int? ResolveActiveDocumentFolderId()
    {
      try
      {
        if (_swApp == null || _swApp.Sw == null)
          return null;

        var activeDoc = _swApp.Sw.IActiveDoc2 as ModelDoc2;
        if (activeDoc == null)
          return null;

        string path = activeDoc.GetPathName();
        if (string.IsNullOrWhiteSpace(path))
          return null;

        VelumProductItem item = _store.FindItemByFilePath(path);
        if (item == null || item.FolderId <= 0)
          return null;

        return item.FolderId;
      }
      catch
      {
        return null;
      }
    }

    /// <summary>
    /// Возвращает каталог, в котором лежит активный документ SW.
    /// Возвращает null, если документ не сохранён или SW недоступен.
    /// </summary>
    private string ResolveActiveDocumentCatalogPath()
    {
      try
      {
        if (_swApp == null || _swApp.Sw == null)
          return null;

        var activeDoc = _swApp.Sw.IActiveDoc2 as ModelDoc2;
        if (activeDoc == null)
          return null;

        string path = activeDoc.GetPathName();
        if (string.IsNullOrWhiteSpace(path))
          return null;

        return Path.GetDirectoryName(path);
      }
      catch
      {
        return null;
      }
    }

    private static TreeNode FindNodeByFolderId(TreeNodeCollection nodes, int folderId)
    {
      foreach (TreeNode node in nodes)
      {
        if (node.Tag is int && (int)node.Tag == folderId)
          return node;
        TreeNode found = FindNodeByFolderId(node.Nodes, folderId);
        if (found != null)
          return found;
      }

      return null;
    }

    private void AddItem()
    {
      if (!_isAdmin)
        return;

      int? folderId = GetSelectedFolderId();
      if (folderId == null)
      {
        MessageBox.Show(this, "Выберите каталог.", "Реестр документов", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return;
      }

      var draft = new VelumProductItem
      {
        Id = 0,
        FolderId = folderId.Value,
        Designation = string.Empty,
        Name = string.Empty,
        FilePath = string.Empty
      };

      using (var form = new VelumProductRegistryItemForm(draft, isNew: true, _store))
      {
        if (form.ShowDialog(this) != DialogResult.OK || form.ResultItem == null)
          return;

        int targetFolderId = form.ResultItem.FolderId > 0 ? form.ResultItem.FolderId : folderId.Value;
        VelumProductItem created;
        try
        {
          created = _store.AddItem(
              targetFolderId,
              form.ResultItem.Designation,
              form.ResultItem.Name,
              form.ResultItem.FilePath);
        }
        catch (InvalidOperationException ex)
        {
          MessageBox.Show(
              this,
              ex.Message,
              "Реестр документов",
              MessageBoxButtons.OK,
              MessageBoxIcon.Warning);
          return;
        }

        if (created != null
            && VelumProductRegistryExportMetaSync.IsExportMetaSyncSupported(created.FilePath))
        {
          VelumProductRegistryExportMetaSync.TrySyncItemAllowOpen(
              _store, _swApp, created, persist: true, out _);
        }

        if (targetFolderId != folderId.Value)
          RebuildTree(targetFolderId);
        RefreshPathStatusForItem(created);
        BindList();
        SelectItemInList(created.Id);
      }
    }

    private void EditSelectedItem()
    {
      if (!_isAdmin)
        return;

      int? itemId = GetSelectedItemId();
      if (itemId == null)
        return;

      VelumProductItem item = _store.GetItem(itemId.Value);
      if (item == null)
        return;

      var draft = new VelumProductItem
      {
        Id = item.Id,
        FolderId = item.FolderId,
        Designation = item.Designation,
        Name = item.Name,
        FilePath = item.FilePath,
        NeedDrawing = item.NeedDrawing
      };
      VelumProductRegistryExportMetaCopy.CopyMirrorFields(item, draft);

      using (var form = new VelumProductRegistryItemForm(draft, isNew: false, _store))
      {
        if (form.ShowDialog(this) != DialogResult.OK || form.ResultItem == null)
          return;

        int oldFolderId = item.FolderId;
        var updated = new VelumProductItem
        {
          Id = item.Id,
          FolderId = form.ResultItem.FolderId,
          Designation = form.ResultItem.Designation,
          Name = form.ResultItem.Name,
          FilePath = form.ResultItem.FilePath,
          NeedDrawing = item.NeedDrawing
        };
        VelumProductRegistryExportMetaCopy.CopyMirrorFields(item, updated);
        try
        {
          _store.UpdateItem(updated);
        }
        catch (InvalidOperationException ex)
        {
          MessageBox.Show(
              this,
              ex.Message,
              "Реестр документов",
              MessageBoxButtons.OK,
              MessageBoxIcon.Warning);
          return;
        }

        if (oldFolderId != updated.FolderId)
          RebuildTree(updated.FolderId);
        RefreshPathStatusForItem(updated);
        BindList();
        SelectItemInList(updated.Id);
      }
    }

    private void LinkSelectedItemsToFolder()
    {
      if (!_isAdmin)
        return;

      List<int> ids = GetSelectedItemIds();
      if (ids.Count == 0)
      {
        MessageBox.Show(
            this,
            "Выберите одну или несколько записей в списке.",
            "Реестр документов",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      int? initialFolderId = null;
      foreach (int itemId in ids)
      {
        VelumProductItem item = _store.GetItem(itemId);
        if (item == null)
          continue;
        if (initialFolderId == null)
          initialFolderId = item.FolderId;
        else if (initialFolderId.Value != item.FolderId)
        {
          initialFolderId = null;
          break;
        }
      }

      using (var form = new VelumProductRegistryFolderPickerForm(_store, initialFolderId))
      {
        if (form.ShowDialog(this) != DialogResult.OK || form.SelectedFolderId == null)
          return;

        int targetFolderId = form.SelectedFolderId.Value;
        bool allSame = true;
        foreach (int itemId in ids)
        {
          VelumProductItem item = _store.GetItem(itemId);
          if (item == null || item.FolderId != targetFolderId)
          {
            allSame = false;
            break;
          }
        }

        if (allSame)
        {
          MessageBox.Show(
              this,
              "Выделенные записи уже привязаны к выбранному каталогу.",
              "Реестр документов",
              MessageBoxButtons.OK,
              MessageBoxIcon.Information);
          return;
        }

        string path = _store.GetFolderPath(targetFolderId);
        string message = ids.Count == 1
            ? "Переназначить привязку выделенной записи к каталогу «" + path + "»?"
            : "Переназначить привязку выделенных записей (" + ids.Count + ") к каталогу «" + path + "»?";
        DialogResult confirm = MessageBox.Show(
            this,
            message,
            "Реестр документов",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);
        if (confirm != DialogResult.Yes)
          return;

        _store.MoveItems(ids, targetFolderId);
        RebuildTree(targetFolderId);
        BindList();
        foreach (ListViewItem row in _listView.Items)
          row.Selected = false;
        foreach (int itemId in ids)
          SelectItemInList(itemId);
      }
    }

    private void DeleteSelectedItem()
    {
      if (!_isAdmin)
        return;

      List<int> ids = GetSelectedItemIds();
      if (ids.Count == 0)
        return;

      string message = ids.Count == 1
          ? "Удалить выделенную запись?"
          : "Удалить выделенные записи (" + ids.Count + ")?";
      DialogResult confirm = MessageBox.Show(
          this,
          message,
          "Реестр документов",
          MessageBoxButtons.YesNo,
          MessageBoxIcon.Warning,
          MessageBoxDefaultButton.Button2);
      if (confirm != DialogResult.Yes)
        return;

      foreach (int itemId in ids)
      {
        _store.DeleteItem(itemId);
        _pathStatuses.Remove(itemId);
      }
      BindList();
    }

    /// <summary>
    /// Открывает выделенные документы: для файлов SolidWorks — через OpenDoc6
    /// (в текущем экземпляре SW), для остальных — через оболочку.
    /// </summary>
    private void OpenSelectedItems()
    {
      List<int> ids = GetSelectedItemIds();
      if (ids.Count == 0)
        return;

      var missing = new List<string>();
      int openedCount = 0;
      int swOpenedCount = 0;
      int shellOpenedCount = 0;
      var swErrors = new List<string>();
      var shellErrors = new List<string>();

      foreach (int itemId in ids)
      {
        VelumProductItem item = _store.GetItem(itemId);
        if (item == null)
          continue;

        string path = (item.FilePath ?? string.Empty).Trim();
        string label = string.IsNullOrWhiteSpace(item.Designation)
            ? ("ID " + item.Id)
            : item.Designation;

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
          missing.Add(label + (string.IsNullOrEmpty(path) ? string.Empty : (": " + path)));
          continue;
        }

        // Для файлов SolidWorks используем API (открывает в текущем экземпляре),
        // для остальных — оболочку Windows.
        if (_swApp?.Sw != null
            && VelumProductRegistryStore.IsSolidWorksFileExtension(path))
        {
          bool ok = TryOpenInSolidWorks(path, label, out string error);
          if (ok)
            swOpenedCount++;
          else
            swErrors.Add(label + (string.IsNullOrEmpty(error) ? string.Empty : (": " + error)));
        }
        else
        {
          try
          {
            Process.Start(new ProcessStartInfo
            {
              FileName = path,
              UseShellExecute = true
            });
            shellOpenedCount++;
          }
          catch (Exception ex)
          {
            shellErrors.Add(label + ": " + ex.Message);
          }
        }

        openedCount++;
      }

      bool hasErrors = missing.Count > 0 || swErrors.Count > 0 || shellErrors.Count > 0;
      var summary = new StringBuilder();

      if (openedCount > 0)
      {
        summary.AppendLine("Открыто: " + openedCount);
        if (swOpenedCount > 0)
          summary.AppendLine("  через SolidWorks: " + swOpenedCount);
        if (shellOpenedCount > 0)
          summary.AppendLine("  через оболочку: " + shellOpenedCount);
      }

      if (swErrors.Count > 0)
      {
        summary.AppendLine();
        summary.AppendLine("Ошибки SolidWorks:");
        foreach (string err in swErrors)
          summary.AppendLine("  • " + err);
      }

      if (shellErrors.Count > 0)
      {
        summary.AppendLine();
        summary.AppendLine("Ошибки оболочки:");
        foreach (string err in shellErrors)
          summary.AppendLine("  • " + err);
      }

      if (missing.Count > 0)
      {
        summary.AppendLine();
        summary.AppendLine(missing.Count == 1
            ? "Файл не найден:"
            : "Файлы не найдены:");
        foreach (string entry in missing)
          summary.AppendLine("  • " + entry);
      }

      if (hasErrors)
      {
        MessageBox.Show(
            this,
            summary.ToString().TrimEnd(),
            "Реестр документов",
            MessageBoxButtons.OK,
            swErrors.Count > 0 || shellErrors.Count > 0
                ? MessageBoxIcon.Warning
                : MessageBoxIcon.Information);
      }
    }

    /// <summary>
    /// Открывает файл в текущем экземпляре SolidWorks через API (OpenDoc6).
    /// Если документ уже открыт — активирует его.
    /// </summary>
    private bool TryOpenInSolidWorks(string path, string label, out string error)
    {
      error = string.Empty;

      if (_swApp?.Sw == null)
      {
        error = "SolidWorks недоступен";
        return false;
      }

      // Определяем тип документа по расширению
      int docType;
      string ext = Path.GetExtension(path).ToLowerInvariant();
      if (string.Equals(ext, ".sldprt", StringComparison.OrdinalIgnoreCase))
        docType = (int)SolidWorks.Interop.swconst.swDocumentTypes_e.swDocPART;
      else if (string.Equals(ext, ".sldasm", StringComparison.OrdinalIgnoreCase))
        docType = (int)SolidWorks.Interop.swconst.swDocumentTypes_e.swDocASSEMBLY;
      else if (string.Equals(ext, ".slddrw", StringComparison.OrdinalIgnoreCase))
        docType = (int)SolidWorks.Interop.swconst.swDocumentTypes_e.swDocDRAWING;
      else
      {
        error = "Не поддерживаемый тип файла SolidWorks: " + ext;
        return false;
      }

      // Сначала ищем уже открытый документ
      ModelDoc2 doc = VelumProductRegistryNameSyncHelper.TryFindOpenDocumentByPath(_swApp, path);
      if (doc != null)
      {
        // Документ уже открыт — активируем его
        try
        {
          string title = doc.GetTitle();
          if (!string.IsNullOrWhiteSpace(title))
          {
            int activateErrors = 0;
            _swApp.Sw.ActivateDoc3(
                title,
                true,
                (int)SolidWorks.Interop.swconst.swRebuildOnActivation_e.swDontRebuildActiveDoc,
                ref activateErrors);
          }
        }
        catch (Exception ex)
        {
          error = ex.Message;
          return false;
        }
        return true;
      }

      // Открываем через API
      try
      {
        int openErrors = 0;
        int warnings = 0;
        doc = _swApp.Sw.OpenDoc6(
            path,
            docType,
            (int)SolidWorks.Interop.swconst.swOpenDocOptions_e.swOpenDocOptions_Silent,
            string.Empty,
            ref openErrors,
            ref warnings) as ModelDoc2;

        if (doc == null)
        {
          // Повторная попытка найти документ (иногда OpenDoc6 возвращает null,
          // но документ всё же открывается)
          doc = VelumProductRegistryNameSyncHelper.TryFindOpenDocumentByPath(_swApp, path);
          if (doc == null)
          {
            error = "OpenDoc6: errors=" + openErrors + " warnings=" + warnings;
            return false;
          }
        }

        // Активируем открытый документ
        try
        {
          string title = doc.GetTitle();
          if (!string.IsNullOrWhiteSpace(title))
          {
            int activateErrors = 0;
            _swApp.Sw.ActivateDoc3(
                title,
                true,
                (int)SolidWorks.Interop.swconst.swRebuildOnActivation_e.swDontRebuildActiveDoc,
                ref activateErrors);
          }
        }
        catch
        {
          // Не критично, если активация не удалась
        }

        return true;
      }
      catch (Exception ex)
      {
        error = ex.Message;
        return false;
      }
    }

    private void SelectItemInList(int itemId)
    {
      foreach (ListViewItem row in _listView.Items)
      {
        if (row.Tag is int && (int)row.Tag == itemId)
        {
          row.Selected = true;
          row.Focused = true;
          row.EnsureVisible();
          break;
        }
      }
    }

    private void OnListKeyDown(object sender, KeyEventArgs e)
    {
      if (e.KeyCode == Keys.Insert)
      {
        if (_isAdmin)
          AddItem();
        e.Handled = true;
      }
      else if (e.KeyCode == Keys.Delete)
      {
        if (_isAdmin)
          DeleteSelectedItem();
        e.Handled = true;
      }
      else if (e.KeyCode == Keys.Enter)
      {
        if (_isAdmin)
          EditSelectedItem();
        else
          OpenSelectedItems();
        e.Handled = true;
      }
    }

    private static Icon TryLoadFormIcon()
    {
      return VelumFormIcon.TryLoad();
    }

    private static Image TryLoadMenuBitmap(string fileName)
    {
      try
      {
        string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (string.IsNullOrEmpty(dir))
          return null;
        string path = Path.Combine(dir, "icons", fileName);
        if (!File.Exists(path))
          return null;
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
