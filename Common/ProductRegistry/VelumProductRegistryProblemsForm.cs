using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Velum.Configuration;
using Velum.UI.ProductRegistry;
using Xarial.XCad.SolidWorks;

namespace Velum.UI
{
  /// <summary>Список проблем целостности реестра + удалить / починить.</summary>
  internal sealed partial class VelumProductRegistryProblemsForm : Form
  {
    private readonly ISwApplication _swApp;
    private readonly bool _isAdmin = VelumAppConfig.IsProductRegistryAdmin;
    private VelumProductRegistryStore _store;
    private ContextMenuStrip _listMenu;
    private VelumListViewCellFilterMenu _listCellFilterMenu;
    private ToolStripMenuItem _menuSelectAll;
    private ToolStripMenuItem _menuOpen;
    private ToolStripMenuItem _menuDelete;
    private ToolStripMenuItem _menuFix;
    private int _sortColumn;
    private SortOrder _sortOrder = SortOrder.Ascending;
    private bool _opening;
    private bool _stopRequested;
    private const string FilterKindAll = "(все)";
    private string _activeFilterKind = string.Empty;
    private string _activeFilterDesignation = string.Empty;
    private string _activeFilterName = string.Empty;
    private string _activeFilterDetail = string.Empty;

    public VelumProductRegistryProblemsForm(ISwApplication swApp)
    {
      _swApp = swApp;
      InitializeComponent();
      VelumFormHelp.Bind(this, VelumHelpTopics.ProductProblems);
      Icon icon = TryLoadFormIcon();
      if (icon != null)
        Icon = icon;
      _list.ColumnClick += OnListColumnClick;
      SetupContextMenu();
      SetupFilters();
      ApplyAccess();
      var tip = new ToolTip();
      tip.SetToolTip(_btnClose, "Закрыть");
      tip.SetToolTip(_btnOpenRegistry, "Открыть реестр документов");
      tip.SetToolTip(_btnRefresh, "Обновить список проблем");
      tip.SetToolTip(_btnStop, "Прервать массовое открытие");
      tip.SetToolTip(_btnFilterApply, "Применить фильтры списка");
      tip.SetToolTip(_btnFilterReset, "Очистить фильтры списка");
      tip.SetToolTip(_btnFilterHelp, "Справка по маскам фильтра");
      _btnStop.Click += (s, e) => _stopRequested = true;
      RefreshList();
    }

    private void SetupContextMenu()
    {
      Image selectAllIcon = TryLoadMenuBitmap("editselectall.png");
      Image openIcon = TryLoadMenuBitmap("Yes.png");
      Image deleteIcon = TryLoadMenuBitmap("Delete.png");
      Image fixIcon = TryLoadMenuBitmap("Modify.png");

      _listMenu = new ContextMenuStrip();
      _menuSelectAll = new ToolStripMenuItem("Выбрать все", selectAllIcon, (s, e) => SelectAll());
      _menuOpen = new ToolStripMenuItem("Открыть", openIcon, (s, e) => OpenSelected());
      _menuDelete = new ToolStripMenuItem("Удалить", deleteIcon, (s, e) => DeleteSelected());
      _menuFix = new ToolStripMenuItem("Починить", fixIcon, (s, e) => FixSelected());
      _listMenu.Items.Add(_menuSelectAll);
      _listMenu.Items.Add(new ToolStripSeparator());
      Image filterIcon = TryLoadMenuBitmap("Thumbs up.png");
      Image excludeIcon = TryLoadMenuBitmap("Thumbs down.png");
      _listCellFilterMenu = new VelumListViewCellFilterMenu(
          _list,
          this,
          ResolveProblemListFilterBox,
          ApplyFiltersFromUi,
          () => !_opening,
          null);
      _listCellFilterMenu.InsertInto(_listMenu, _listMenu.Items.Count, filterIcon, excludeIcon);
      _listMenu.Items.Add(new ToolStripSeparator());
      _listMenu.Items.Add(_menuOpen);
      _listMenu.Items.Add(_menuDelete);
      _listMenu.Items.Add(_menuFix);
      _listMenu.Opening += OnListMenuOpening;
      _list.ContextMenuStrip = _listMenu;
    }

    private TextBox ResolveProblemListFilterBox(int column)
    {
      if (column == 2)
        return _filterDesignationBox;
      if (column == 3)
        return _filterNameBox;
      if (column == 5)
        return _filterDetailBox;
      return null;
    }

    private void ApplyAccess()
    {
      _menuDelete.Enabled = _isAdmin;
      _menuFix.Enabled = _isAdmin;
    }

    private void SetupFilters()
    {
      _filterKindBox.Items.Clear();
      _filterKindBox.Items.Add(FilterKindAll);
      foreach (VelumProductRegistryProblemKind kind in Enum.GetValues(typeof(VelumProductRegistryProblemKind)))
        _filterKindBox.Items.Add(KindText(kind));
      _filterKindBox.SelectedIndex = 0;

      _btnFilterApply.Click += (s, e) => ApplyFiltersFromUi();
      _btnFilterReset.Click += (s, e) =>
      {
        _filterKindBox.SelectedIndex = 0;
        _filterDesignationBox.Text = string.Empty;
        _filterNameBox.Text = string.Empty;
        _filterDetailBox.Text = string.Empty;
        _activeFilterKind = string.Empty;
        _activeFilterDesignation = string.Empty;
        _activeFilterName = string.Empty;
        _activeFilterDetail = string.Empty;
        RefreshList();
      };
      _btnFilterHelp.Click += (s, e) => VelumListFilterHelper.ShowHelp(this);

      KeyEventHandler enterApply = (s, e) =>
      {
        if (e.KeyCode == Keys.Enter)
        {
          ApplyFiltersFromUi();
          e.Handled = true;
          e.SuppressKeyPress = true;
        }
      };
      _filterDesignationBox.KeyDown += enterApply;
      _filterNameBox.KeyDown += enterApply;
      _filterDetailBox.KeyDown += enterApply;
    }

    private void ApplyFiltersFromUi()
    {
      string kindText = _filterKindBox.SelectedItem as string;
      _activeFilterKind = string.IsNullOrEmpty(kindText)
          || string.Equals(kindText, FilterKindAll, StringComparison.Ordinal)
          ? string.Empty
          : kindText;
      _activeFilterDesignation = (_filterDesignationBox.Text ?? string.Empty).Trim();
      _activeFilterName = (_filterNameBox.Text ?? string.Empty).Trim();
      _activeFilterDetail = (_filterDetailBox.Text ?? string.Empty).Trim();
      RefreshList();
    }

    private void OnListMenuOpening(object sender, System.ComponentModel.CancelEventArgs e)
    {
      if (_opening)
      {
        e.Cancel = true;
        return;
      }

      bool hasSelection = _list.SelectedItems.Count > 0;
      _menuSelectAll.Enabled = _list.Items.Count > 0;
      _menuOpen.Enabled = hasSelection;
      _menuDelete.Enabled = _isAdmin && hasSelection;
      _menuFix.Enabled = _isAdmin && hasSelection;
    }

    private void RefreshList()
    {
      _list.BeginUpdate();
      try
      {
        _list.Items.Clear();
        IReadOnlyList<VelumProductRegistryProblemEntry> snapshot =
            VelumProductRegistryProblemCache.SnapshotIncludingPending();
        var problems = new List<VelumProductRegistryProblemEntry>(snapshot.Count);
        foreach (VelumProductRegistryProblemEntry p in snapshot)
        {
          if (p != null)
            problems.Add(p);
        }

        problems = ApplyProblemFilters(problems);
        SortProblems(problems);

        foreach (VelumProductRegistryProblemEntry p in problems)
        {
          var item = new ListViewItem(KindText(p.Kind));
          item.SubItems.Add(p.ItemId > 0 ? p.ItemId.ToString() : "—");
          item.SubItems.Add(p.Designation ?? string.Empty);
          item.SubItems.Add(p.Name ?? string.Empty);
          item.SubItems.Add(p.FilePath ?? string.Empty);
          item.SubItems.Add(p.Detail ?? string.Empty);
          item.Tag = p;
          _list.Items.Add(item);
        }
      }
      finally
      {
        _list.EndUpdate();
      }

      _lblStatus.Text = BuildStatusText(_list.Items.Count);
    }

    private List<VelumProductRegistryProblemEntry> ApplyProblemFilters(
        List<VelumProductRegistryProblemEntry> problems)
    {
      if (string.IsNullOrEmpty(_activeFilterKind)
          && string.IsNullOrEmpty(_activeFilterDesignation)
          && string.IsNullOrEmpty(_activeFilterName)
          && string.IsNullOrEmpty(_activeFilterDetail))
        return problems;

      var filtered = new List<VelumProductRegistryProblemEntry>(problems.Count);
      foreach (VelumProductRegistryProblemEntry p in problems)
      {
        if (!string.IsNullOrEmpty(_activeFilterKind)
            && !string.Equals(KindText(p.Kind), _activeFilterKind, StringComparison.Ordinal))
          continue;
        if (!string.IsNullOrEmpty(_activeFilterDesignation)
            && !VelumListFilterHelper.Matches(p.Designation, _activeFilterDesignation))
          continue;
        if (!string.IsNullOrEmpty(_activeFilterName)
            && !VelumListFilterHelper.Matches(p.Name, _activeFilterName))
          continue;
        if (!string.IsNullOrEmpty(_activeFilterDetail)
            && !VelumListFilterHelper.Matches(p.Detail, _activeFilterDetail))
          continue;
        filtered.Add(p);
      }

      return filtered;
    }

    private void SortProblems(List<VelumProductRegistryProblemEntry> problems)
    {
      int direction = _sortOrder == SortOrder.Descending ? -1 : 1;
      problems.Sort((a, b) =>
      {
        int cmp;
        switch (_sortColumn)
        {
          case 1:
            cmp = a.ItemId.CompareTo(b.ItemId);
            break;
          case 2:
            cmp = string.Compare(a.Designation, b.Designation, StringComparison.CurrentCultureIgnoreCase);
            break;
          case 3:
            cmp = string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
            break;
          case 4:
            cmp = string.Compare(a.FilePath, b.FilePath, StringComparison.OrdinalIgnoreCase);
            break;
          case 5:
            cmp = string.Compare(a.Detail, b.Detail, StringComparison.CurrentCultureIgnoreCase);
            break;
          default:
            cmp = string.Compare(KindText(a.Kind), KindText(b.Kind), StringComparison.CurrentCultureIgnoreCase);
            break;
        }

        if (cmp == 0)
          cmp = a.ItemId.CompareTo(b.ItemId);
        if (cmp == 0)
          cmp = string.Compare(a.FilePath, b.FilePath, StringComparison.OrdinalIgnoreCase);
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

      RefreshList();
    }

    private void OpenSelected()
    {
      if (_opening)
        return;

      List<VelumProductRegistryProblemEntry> selected = GetSelectedProblems();
      if (selected.Count == 0)
        return;

      // Много документов подряд — оболочка/SW может заметно тормозить UI.
      const int bulkOpenWarnThreshold = 10;
      bool bulk = selected.Count > bulkOpenWarnThreshold;
      if (bulk)
      {
        if (MessageBox.Show(
                this,
                "Выбрано " + selected.Count + " документов.\n"
                    + "Открытие может занять продолжительное время.\n\n"
                    + "Продолжить?",
                Text,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
          return;

        _stopRequested = false;
        _opening = true;
        SetOpeningUi(true);
        UseWaitCursor = true;
      }

      var missing = new List<string>();
      var openedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      try
      {
        int total = selected.Count;
        for (int i = 0; i < total; i++)
        {
          if (bulk && _stopRequested)
            break;

          VelumProductRegistryProblemEntry p = selected[i];
          if (p == null)
            continue;

          string path = (p.FilePath ?? string.Empty).Trim();
          string label = !string.IsNullOrWhiteSpace(p.Designation)
              ? p.Designation
              : (p.ItemId > 0 ? ("Id=" + p.ItemId) : path);

          string pathKey = VelumProductRegistryStore.NormalizeFilePathKey(path);
          if (!string.IsNullOrEmpty(pathKey) && !openedPaths.Add(pathKey))
            continue;

          if (bulk)
            SetOpenProgress(i, total, "Открытие " + (i + 1) + " из " + total + ": " + label);

          if (string.IsNullOrEmpty(path) || !File.Exists(path))
          {
            missing.Add(
                string.IsNullOrEmpty(label)
                    ? (string.IsNullOrEmpty(path) ? "(без пути)" : path)
                    : (label + (string.IsNullOrEmpty(path) ? string.Empty : (": " + path))));
            continue;
          }

          string error;
          if (!TryOpenPath(path, out error))
          {
            missing.Add(
                label
                + (string.IsNullOrEmpty(path) ? string.Empty : (": " + path))
                + (string.IsNullOrEmpty(error) ? string.Empty : (" (" + error + ")")));
          }
        }

        if (bulk)
        {
          if (_stopRequested)
            _progressLabel.Text = "Прервано";
          else
            SetOpenProgress(total, total, "Открыто: " + (total - missing.Count) + " из " + total);
        }
      }
      finally
      {
        if (bulk)
        {
          UseWaitCursor = false;
          _opening = false;
          SetOpeningUi(false);
          _progressBar.Value = 0;
          if (!_stopRequested)
            _progressLabel.Text = string.Empty;
        }
      }

      if (missing.Count == 0)
        return;

      var text = new StringBuilder();
      text.AppendLine("Не удалось открыть:");
      foreach (string entry in missing)
        text.AppendLine(entry);

      MessageBox.Show(
          this,
          text.ToString().TrimEnd(),
          Text,
          MessageBoxButtons.OK,
          MessageBoxIcon.Warning);
    }

    private void SetOpenProgress(int current, int maximum, string status)
    {
      int max = Math.Max(maximum, 1);
      if (_progressBar.Maximum != max)
        _progressBar.Maximum = max;
      _progressBar.Value = Math.Max(0, Math.Min(current, _progressBar.Maximum));
      _progressLabel.Text = status ?? string.Empty;
      Application.DoEvents();
    }

    private void SetOpeningUi(bool opening)
    {
      _progressBar.Visible = opening;
      _layout.RowStyles[2].Height = opening ? 18F : 0F;
      if (opening)
      {
        _progressBar.Minimum = 0;
        _progressBar.Value = 0;
      }

      _btnStop.Enabled = opening;
      _btnRefresh.Enabled = !opening;
      _btnOpenRegistry.Enabled = !opening;
      _btnClose.Enabled = !opening;
      _list.Enabled = !opening;
      _filterKindBox.Enabled = !opening;
      _filterDesignationBox.Enabled = !opening;
      _filterNameBox.Enabled = !opening;
      _filterDetailBox.Enabled = !opening;
      _btnFilterApply.Enabled = !opening;
      _btnFilterReset.Enabled = !opening;
      _btnFilterHelp.Enabled = !opening;
    }

    private static string BuildStatusText(int listCount)
    {
      bool scanning = !VelumProductRegistryIntegrityScheduler.IsOpenDocumentsScopeActive
          && (VelumProductRegistryIntegrityScheduler.IsDiscoveryPassActive
              || VelumProductRegistryProblemCache.PendingCount > 0);

      if (VelumProductRegistryIntegrityScheduler.IsOpenDocumentsScopeActive)
      {
        return listCount == 0
            ? "Сканирование приостановлено (открыт документ SW)."
            : "Проблем: " + listCount + ". Сканирование приостановлено (открыт документ SW).";
      }

      if (scanning)
      {
        return listCount == 0
            ? "Сканирование реестра…"
            : "Проблем: " + listCount + ". Сканирование ещё идёт…";
      }

      return listCount == 0
          ? "Проблем в кэше нет."
          : "Проблем: " + listCount;
    }

    private static string KindText(VelumProductRegistryProblemKind kind)
    {
      switch (kind)
      {
        case VelumProductRegistryProblemKind.BrokenLink:
          return "Битая ссылка";
        case VelumProductRegistryProblemKind.MissingDrawing:
          return "Нет чертежа";
        case VelumProductRegistryProblemKind.MissingRegistryEntry:
          return "Нет в реестре";
        case VelumProductRegistryProblemKind.DuplicateDesignation:
          return "Дублирование обозначения";
        case VelumProductRegistryProblemKind.NeedDxfExport:
          return "Нужен DXF";
        case VelumProductRegistryProblemKind.OutdatedDxf:
          return "DXF устарел";
        case VelumProductRegistryProblemKind.MissingDxfProjection:
          return "Нет плоскости DXF";
        case VelumProductRegistryProblemKind.DxfCatalogUnavailable:
          return "Каталог DXF";
        case VelumProductRegistryProblemKind.DxfFirstExport:
          return "Первая выгрузка DXF";
        case VelumProductRegistryProblemKind.JunkDxf:
          return "Мусорный DXF";
        case VelumProductRegistryProblemKind.NeedPdfExport:
          return "Нужен PDF";
        case VelumProductRegistryProblemKind.OutdatedPdf:
          return "PDF устарел";
        case VelumProductRegistryProblemKind.JunkPdf:
          return "Мусорный PDF";
        default:
          return kind.ToString();
      }
    }

    private List<VelumProductRegistryProblemEntry> GetSelectedProblems()
    {
      var result = new List<VelumProductRegistryProblemEntry>();
      // Снимок индексов: SelectedItems живая коллекция, при потере фокуса SW
      // часть выделения может пропасть ещё до копирования Tag.
      int count = _list.SelectedIndices.Count;
      if (count == 0)
        return result;

      var indices = new int[count];
      _list.SelectedIndices.CopyTo(indices, 0);
      Array.Sort(indices);
      foreach (int index in indices)
      {
        if (index < 0 || index >= _list.Items.Count)
          continue;
        var p = _list.Items[index].Tag as VelumProductRegistryProblemEntry;
        if (p != null)
          result.Add(p);
      }

      return result;
    }

    /// <summary>
    /// Открывает файл в SW через OpenDoc6/ActivateDoc3.
    /// Process.Start (DDE) с модального диалога аддина теряет часть пакета:
    /// оболочка шлёт несколько open в уже занятый UI-поток SW.
    /// </summary>
    private bool TryOpenPath(string path, out string error)
    {
      error = string.Empty;
      if (_swApp != null && _swApp.Sw != null && TryResolveSwDocType(path, out int docType))
      {
        ModelDoc2 doc = VelumProductRegistryNameSyncHelper.TryFindOpenDocumentByPath(_swApp, path);
        if (doc == null)
        {
          try
          {
            int openErrors = 0;
            int warnings = 0;
            doc = _swApp.Sw.OpenDoc6(
                path,
                docType,
                0,
                string.Empty,
                ref openErrors,
                ref warnings) as ModelDoc2;
            if (doc == null)
              doc = VelumProductRegistryNameSyncHelper.TryFindOpenDocumentByPath(_swApp, path);
            if (doc == null)
            {
              error = "OpenDoc6 errors=" + openErrors + " warnings=" + warnings;
              return false;
            }
          }
          catch (Exception ex)
          {
            error = ex.Message;
            return false;
          }
        }

        try
        {
          try
          {
            doc.Visible = true;
          }
          catch
          {
          }

          string title = doc.GetTitle();
          if (string.IsNullOrWhiteSpace(title))
          {
            error = "Пустой заголовок документа";
            return false;
          }

          int activateErrors = 0;
          _swApp.Sw.ActivateDoc3(
              title,
              true,
              (int)swRebuildOnActivation_e.swDontRebuildActiveDoc,
              ref activateErrors);
          return true;
        }
        catch (Exception ex)
        {
          error = ex.Message;
          return false;
        }
      }

      try
      {
        Process.Start(new ProcessStartInfo
        {
          FileName = path,
          UseShellExecute = true
        });
        return true;
      }
      catch (Exception ex)
      {
        error = ex.Message;
        return false;
      }
    }

    private static bool TryResolveSwDocType(string path, out int docType)
    {
      docType = 0;
      string ext = VelumProductRegistryStore.GetDocumentTypeKey(path);
      if (string.Equals(ext, ".sldprt", StringComparison.OrdinalIgnoreCase))
      {
        docType = (int)swDocumentTypes_e.swDocPART;
        return true;
      }

      if (string.Equals(ext, ".sldasm", StringComparison.OrdinalIgnoreCase))
      {
        docType = (int)swDocumentTypes_e.swDocASSEMBLY;
        return true;
      }

      if (string.Equals(ext, ".slddrw", StringComparison.OrdinalIgnoreCase))
      {
        docType = (int)swDocumentTypes_e.swDocDRAWING;
        return true;
      }

      return false;
    }

    private void SelectAll()
    {
      foreach (ListViewItem row in _list.Items)
        row.Selected = true;
    }

    private VelumProductRegistryStore EnsureStore()
    {
      if (_store == null)
      {
        _store = new VelumProductRegistryStore();
        _store.Load();
      }

      return _store;
    }

    private void OnRefreshClick(object sender, EventArgs e)
    {
      if (_opening)
        return;

      // Только перечитать кэш (published ∪ pending). Reload реестра не нужен —
      // его делают действия меню через NotifyRegistryChanged + RevalidateItem.
      RefreshList();
    }

    private void DeleteSelected()
    {
      if (!_isAdmin)
        return;

      List<VelumProductRegistryProblemEntry> selected = GetSelectedProblems();
      if (selected.Count == 0)
      {
        MessageBox.Show(this, "Выберите одну или несколько записей.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        return;
      }

      int missingRegistryCount = 0;
      var deletable = new List<VelumProductRegistryProblemEntry>();
      foreach (VelumProductRegistryProblemEntry p in selected)
      {
        if (p == null)
          continue;
        if (p.Kind == VelumProductRegistryProblemKind.MissingRegistryEntry || p.ItemId <= 0)
          missingRegistryCount++;
        else
          deletable.Add(p);
      }

      if (deletable.Count == 0)
      {
        MessageBox.Show(
            this,
            "Строки «Нет в реестре» нельзя удалить из реестра — записи ещё нет.\nДобавьте документ через форму реестра.",
            Text,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      string confirm = deletable.Count == 1
          ? "Удалить запись Id=" + deletable[0].ItemId + " из реестра?"
          : "Удалить выделенные записи (" + deletable.Count + ") из реестра?";
      if (missingRegistryCount > 0)
        confirm += "\n\nСтроки «Нет в реестре» (" + missingRegistryCount + ") будут пропущены.";
      if (MessageBox.Show(this, confirm, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        return;

      try
      {
        VelumProductRegistryStore store = EnsureStore();
        var deletedIds = new List<int>();
        foreach (VelumProductRegistryProblemEntry problem in deletable)
        {
          if (problem == null || problem.ItemId <= 0)
            continue;

          if (store.GetItem(problem.ItemId) != null)
            store.DeleteItem(problem.ItemId);

          VelumProductRegistryProblemCache.RemoveAllForItem(problem.ItemId);
          deletedIds.Add(problem.ItemId);
        }

        VelumProductRegistryIntegrityScheduler.NotifyRegistryChanged();
        foreach (int id in deletedIds)
          VelumProductRegistryIntegrityScheduler.RevalidateItem(id);

        RefreshList();
      }
      catch (Exception ex)
      {
        MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
      }
    }

    private void FixSelected()
    {
      if (!_isAdmin)
        return;

      List<VelumProductRegistryProblemEntry> selected = GetSelectedProblems();
      if (selected.Count == 0)
      {
        MessageBox.Show(this, "Выберите одну или несколько записей.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        return;
      }

      var missingDrawing = new List<VelumProductRegistryProblemEntry>();
      var missingRegistry = new List<VelumProductRegistryProblemEntry>();
      // Починка через редактор записи: битая ссылка — исправить/очистить путь,
      // дубль обозначения — задать свободное обозначение (или отвязать файл).
      var editableProblems = new List<VelumProductRegistryProblemEntry>();
      foreach (VelumProductRegistryProblemEntry p in selected)
      {
        if (p.Kind == VelumProductRegistryProblemKind.MissingDrawing)
          missingDrawing.Add(p);
        else if (p.Kind == VelumProductRegistryProblemKind.MissingRegistryEntry)
          missingRegistry.Add(p);
        else if (p.Kind == VelumProductRegistryProblemKind.BrokenLink
            || p.Kind == VelumProductRegistryProblemKind.DuplicateDesignation)
          editableProblems.Add(p);
      }

      if (missingDrawing.Count > 0)
      {
        MessageBox.Show(
            this,
            BuildManualFixMessage(
                "Проблемы «Нет чертежа» действием «Починить» не решаются.",
                "Добавьте/привяжите чертёж в реестре или отметьте «Не нужен чертеж» в меню.",
                missingDrawing),
            Text,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
      }

      if (missingRegistry.Count > 0)
      {
        MessageBox.Show(
            this,
            BuildManualFixMessage(
                "Проблемы «Нет в реестре» действием «Починить» не решаются.",
                "Добавьте документ в реестр документов (кнопка «Реестр»).",
                missingRegistry),
            Text,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
      }

      if (editableProblems.Count == 0)
        return;

      try
      {
        VelumProductRegistryStore store = EnsureStore();
        foreach (VelumProductRegistryProblemEntry problem in editableProblems)
        {
          VelumProductItem item = store.GetItem(problem.ItemId);
          if (item == null)
          {
            VelumProductRegistryProblemCache.RemoveAllForItem(problem.ItemId);
            continue;
          }

          using (var edit = new VelumProductRegistryItemForm(item, false, store))
          {
            if (edit.ShowDialog(this) != DialogResult.OK || edit.ResultItem == null)
              continue;

            // Поэлементная защита: конфликт ключей у одной записи не должен
            // обрывать починку остальных выбранных проблем.
            try
            {
              store.UpdateItem(edit.ResultItem);
            }
            catch (InvalidOperationException ex)
            {
              MessageBox.Show(
                  this,
                  "Запись Id=" + problem.ItemId + " не обновлена:\n" + ex.Message,
                  Text,
                  MessageBoxButtons.OK,
                  MessageBoxIcon.Warning);
              continue;
            }
          }

          VelumProductRegistryIntegrityScheduler.NotifyRegistryChanged();
          VelumProductRegistryIntegrityScheduler.RevalidateItem(problem.ItemId);
        }

        VelumProductRegistryIntegrityScheduler.NotifyRegistryChanged();
        RefreshList();
      }
      catch (Exception ex)
      {
        MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
      }
    }

    private static string BuildManualFixMessage(
        string titleLine,
        string howLine,
        List<VelumProductRegistryProblemEntry> problems)
    {
      var sb = new StringBuilder();
      sb.AppendLine(titleLine);
      sb.AppendLine(howLine);
      sb.AppendLine();
      sb.AppendLine("Записи:");
      int shown = 0;
      foreach (VelumProductRegistryProblemEntry p in problems)
      {
        if (shown >= 12)
        {
          sb.AppendLine("… и ещё " + (problems.Count - shown));
          break;
        }

        if (p.ItemId > 0)
          sb.Append("Id=").Append(p.ItemId);
        else
          sb.Append("путь");
        if (!string.IsNullOrWhiteSpace(p.Designation))
          sb.Append(" / ").Append(p.Designation);
        else if (!string.IsNullOrWhiteSpace(p.FilePath))
          sb.Append(" / ").Append(p.FilePath);
        sb.AppendLine();
        shown++;
      }

      return sb.ToString();
    }

    private void OnOpenRegistryClick(object sender, EventArgs e)
    {
      if (_opening)
        return;

      int? selectFolderId = ResolveActiveDocumentFolderId();
      VelumProductRegistryFormHost.TryShow(_swApp, selectFolderId);
      VelumProductRegistryIntegrityScheduler.NotifyRegistryChanged();
      // Форма реестра могла менять JSON — свой store тоже перечитать.
      if (_store != null)
        _store.Load();
      RefreshList();
    }

    private int? ResolveActiveDocumentFolderId()
    {
      try
      {
        var sw = _swApp?.Sw;
        if (sw == null)
          return null;

        var activeDoc = sw.IActiveDoc2 as ModelDoc2;
        if (activeDoc == null)
          return null;

        string path = activeDoc.GetPathName();
        if (string.IsNullOrWhiteSpace(path))
          return null;

        var store = new VelumProductRegistryStore();
        store.Load();
        VelumProductItem item = store.FindItemByFilePath(path);
        if (item == null || item.FolderId <= 0)
          return null;

        return item.FolderId;
      }
      catch
      {
        return null;
      }
    }

    private void OnCloseClick(object sender, EventArgs e)
    {
      Close();
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
