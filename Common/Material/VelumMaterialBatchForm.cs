using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;
using Velum.Configuration;
using Velum.ReactiveCore;
using Velum.SolidHomeostasis;
using Xarial.XCad.SolidWorks;

namespace Velum.UI
{
  /// <summary>Пакетное присвоение материалов деталям по конфигурациям.</summary>
  internal sealed partial class VelumMaterialBatchForm : Form
  {
    private readonly ISwApplication _swApp;
    private readonly List<VelumMaterialBatchRow> _rows = new List<VelumMaterialBatchRow>();
    private readonly bool _closeOnSuccessfulApply;
    private VelumMaterialTreeView _materialTreeView;
    private bool _cancelRequested;
    private ContextMenuStrip _listContextMenu;
    private VelumListViewCellFilterMenu _listCellFilterMenu;
    private string _assemblyFolder = string.Empty;
    private bool _fromActiveAssembly;
    private bool _fromActivePart;

    private string _activePartFilter = string.Empty;
    private string _activeConfigFilter = string.Empty;
    private string _activeMaterialFilter = string.Empty;

    // Сортировка по столбцам.
    private int _sortColumn = -1;
    private ListSortDirection _sortDirection = ListSortDirection.Ascending;

    /// <summary>Запуск с панели/команды плагина (каталог → загрузка).</summary>
    public VelumMaterialBatchForm(ISwApplication swApp)
      : this(swApp, null, closeOnSuccessfulApply: false)
    {
    }

    /// <summary>
    /// Запуск из реестра изделия: строки уже заданы, после успешного Apply форма закрывается с OK.
    /// </summary>
    public VelumMaterialBatchForm(
        ISwApplication swApp,
        IReadOnlyList<VelumMaterialBatchRow> seedRows,
        bool closeOnSuccessfulApply)
    {
      _swApp = swApp;
      _closeOnSuccessfulApply = closeOnSuccessfulApply;
      InitializeComponent();
      VelumFormHelp.Bind(this, VelumHelpTopics.MaterialBatch);

      // Resolve folders BEFORE checking _fromActiveAssembly / _fromActivePart,
      // because the Shown handler registration depends on these flags being set.
      VelumBatchFormFolderBootstrap.MaterialFolders folders =
          VelumBatchFormFolderBootstrap.ResolveMaterialFolders(_swApp);
      ApplyResolvedFolders(folders);

      if (seedRows != null && seedRows.Count > 0)
        ApplySeedRows(seedRows);
      else if (_fromActiveAssembly || _fromActivePart)
      {
        // BeginInvoke in constructor crashes: handle is not yet created.
        // Load after first form show (ShowDialog creates the handle).
        Shown += OnFirstShownLoadFromActiveDocument;
      }

      InitializeRuntime();
    }

    private void OnFirstShownLoadFromActiveDocument(object sender, EventArgs e)
    {
      Shown -= OnFirstShownLoadFromActiveDocument;
      if (_fromActiveAssembly)
        LoadPartsFromActiveAssembly();
      else if (_fromActivePart)
        LoadPartsFromActivePart();
    }

    private void InitializeRuntime()
    {
      Icon icon = TryLoadFormIcon();
      if (icon != null)
        Icon = icon;

      InitializeMaterialTree();
      InitializeListViewInteractions();
      BindFilterEvents();
      BindActionToolTips();
      UpdateStatusLabel();
    }

    private void BindActionToolTips()
    {
      var tip = new ToolTip();
      tip.SetToolTip(_btnBrowseParts, "Выбрать каталог деталей");
      tip.SetToolTip(_btnLoadParts, "Загрузить детали из каталога");
      tip.SetToolTip(_btnMaterialSearch, "Найти материал в дереве");
      tip.SetToolTip(_btnMaterialPrev, "Предыдущее совпадение в дереве материалов");
      tip.SetToolTip(_btnMaterialNext, "Следующее совпадение в дереве материалов");
      tip.SetToolTip(_btnFilterApply, "Применить фильтры списка");
      tip.SetToolTip(_btnFilterReset, "Очистить фильтры списка");
      tip.SetToolTip(_btnFilterHelp, "Справка по маскам фильтра");
      tip.SetToolTip(_btnApply, "Присвоить выбранный материал выделенным деталям");
      tip.SetToolTip(_btnStop, "Остановить обработку");
      tip.SetToolTip(_btnClose, "Закрыть");
    }

    private void ApplySeedRows(IReadOnlyList<VelumMaterialBatchRow> seedRows)
    {
      _rows.Clear();
      for (int i = 0; i < seedRows.Count; i++)
      {
        VelumMaterialBatchRow src = seedRows[i];
        if (src == null || string.IsNullOrWhiteSpace(src.PartPath))
          continue;

        _rows.Add(new VelumMaterialBatchRow
        {
          PartPath = src.PartPath,
          PartDisplayName = string.IsNullOrWhiteSpace(src.PartDisplayName)
              ? Path.GetFileName(src.PartPath)
              : src.PartDisplayName,
          ConfigName = src.ConfigName ?? string.Empty,
          MaterialName = src.MaterialName ?? string.Empty,
          MaterialDatabase = src.MaterialDatabase ?? string.Empty,
          Selected = true
        });
      }

      BindListView();
      SelectAllListItems();
      UpdateStatusLabel();
    }

    private void ApplyResolvedFolders(VelumBatchFormFolderBootstrap.MaterialFolders folders)
    {
      _fromActiveAssembly = folders != null && folders.FromActiveAssembly;
      _fromActivePart = folders != null && folders.FromActivePart;
      _assemblyFolder = folders?.AssemblyFolder ?? string.Empty;

      string folder = folders?.PartsFolder ?? string.Empty;
      if (string.IsNullOrWhiteSpace(folder) &&
          (_fromActiveAssembly || _fromActivePart))
        folder = _assemblyFolder;

      VelumBatchFormFolderBootstrap.ApplyFolderOrHint(
          _partsFolderBox,
          folder,
          null);
    }

    private void InitializeListViewInteractions()
    {
      _listView.DoubleClick += OnListViewDoubleClick;
      _listView.MouseUp += OnListViewMouseUp;

      Image openIcon = TryLoadMenuBitmap("Yes.png");
      Image selectAllIcon = TryLoadMenuBitmap("editselectall.png");

      _listContextMenu = new ContextMenuStrip();
      _listContextMenu.Items.Add(new ToolStripMenuItem("Открыть деталь", openIcon, (s, e) => OnOpenPartFromList()));
      Image filterIcon = TryLoadMenuBitmap("Thumbs up.png");
      Image excludeIcon = TryLoadMenuBitmap("Thumbs down.png");
      _listCellFilterMenu = new VelumListViewCellFilterMenu(
          _listView,
          this,
          ResolveMaterialListFilterBox,
          ApplyListFiltersFromUi,
          null,
          ResolveMaterialListFilterValue);
      _listCellFilterMenu.AppendTo(_listContextMenu, filterIcon, excludeIcon);
      _listContextMenu.Items.Add(new ToolStripSeparator());
      _listContextMenu.Items.Add(new ToolStripMenuItem("Выделить все", selectAllIcon, (s, e) => SelectAllListItems()));
    }

    /// <summary>
    /// Сортировка при клике на заголовок столбца.
    /// </summary>
    private void OnListViewColumnClick(object sender, ColumnClickEventArgs e)
    {
      if (e.Column == _sortColumn)
      {
        _sortDirection = _sortDirection == ListSortDirection.Ascending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;
      }
      else
      {
        _sortColumn = e.Column;
        _sortDirection = ListSortDirection.Ascending;
      }

      BindListView();
    }

    private TextBox ResolveMaterialListFilterBox(int column)
    {
      if (column == 0)
        return _partFilterBox;
      if (column == 1)
        return _configFilterBox;
      if (column == 2)
        return _materialListFilterBox;
      return null;
    }

    private static string ResolveMaterialListFilterValue(ListViewItem item, int column)
    {
      if (column == 2)
      {
        var row = item != null ? item.Tag as VelumMaterialBatchRow : null;
        if (row != null)
          return row.MaterialName ?? string.Empty;
      }

      if (item == null || column < 0 || column >= item.SubItems.Count)
        return string.Empty;
      return item.SubItems[column].Text ?? string.Empty;
    }

    private void InitializeMaterialTree()
    {
      var materialManager = new VelumMaterialDatabaseManager(_swApp);
      _materialTreeView = new VelumMaterialTreeView(
          _materialTreeViewControl,
          materialManager,
          CreateTreeIcon(Color.FromArgb(255, 196, 89)),
          TryLoadTreeBitmap("Database.ico") ?? CreateTreeIcon(Color.FromArgb(70, 130, 180)),
          CreateTreeIcon(Color.FromArgb(120, 170, 90)),
          CreateTreeIcon(Color.FromArgb(180, 120, 60)));

      _materialTreeView.BindSearchControls(
          _materialFilterBox,
          _btnMaterialSearch,
          _btnMaterialNext,
          _btnMaterialPrev,
          _materialSearchStatusLabel);

      _materialTreeView.LoadMaterialDatabases();
    }

    private void BindFilterEvents()
    {
      KeyEventHandler enterApply = (s, e) =>
      {
        if (e.KeyCode != Keys.Enter)
          return;
        ApplyListFiltersFromUi();
        e.Handled = true;
        e.SuppressKeyPress = true;
      };
      _partFilterBox.KeyDown += enterApply;
      _configFilterBox.KeyDown += enterApply;
      _materialListFilterBox.KeyDown += enterApply;
      _btnFilterApply.Click += (s, e) => ApplyListFiltersFromUi();
      _btnFilterReset.Click += (s, e) => ResetListFilters();
      _btnFilterHelp.Click += (s, e) => VelumListFilterHelper.ShowHelp(this);
    }

    private void ApplyListFiltersFromUi()
    {
      _activePartFilter = (_partFilterBox.Text ?? string.Empty).Trim();
      _activeConfigFilter = (_configFilterBox.Text ?? string.Empty).Trim();
      _activeMaterialFilter = (_materialListFilterBox.Text ?? string.Empty).Trim();
      BindListView();
    }

    private void ResetListFilters()
    {
      _partFilterBox.Text = string.Empty;
      _configFilterBox.Text = string.Empty;
      _materialListFilterBox.Text = string.Empty;
      _activePartFilter = string.Empty;
      _activeConfigFilter = string.Empty;
      _activeMaterialFilter = string.Empty;
      BindListView();
    }

    private void OnBrowsePartsFolder(object sender, EventArgs e)
    {
      BrowseFolder(_partsFolderBox, "Каталог деталей (SLDPRT)");
    }

    private void OnLoadParts(object sender, EventArgs e)
    {
      if (_fromActiveAssembly)
      {
        LoadPartsFromActiveAssembly();
        return;
      }

      if (_fromActivePart)
      {
        LoadPartsFromActivePart();
        return;
      }

      LoadPartsFromFolder();
    }

    private void LoadPartsFromActiveAssembly()
    {
      ModelDoc2 active = null;
      try
      {
        active = _swApp?.Sw?.IActiveDoc2 as ModelDoc2;
      }
      catch
      {
      }

      var assembly = active as AssemblyDoc;
      if (assembly == null ||
          active.GetType() != (int)SolidWorks.Interop.swconst.swDocumentTypes_e.swDocASSEMBLY)
      {
        MessageBox.Show(
            "Активный документ не является сборкой.",
            "Пакетное присвоение материалов",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      HashSet<string> keepOpenPartPaths = VelumDxfBatchDocumentHelper.CollectOpenPartPaths(_swApp);
      var request = new VelumMaterialBatchScanner.ScanRequest
      {
        SwApp = _swApp,
        KeepOpenPartPaths = keepOpenPartPaths,
        IsCancelled = () => _cancelRequested,
        OnProgress = (current, total, fileName) =>
        {
          if (total > 0 && _progressBar.Maximum != total)
            _progressBar.Maximum = total;
          _progressBar.Value = current > _progressBar.Maximum ? _progressBar.Maximum : current;
          _lblProgress.Text = "Загрузка: " + (fileName ?? string.Empty) +
                              " (" + current + "/" + total + ")";
          Application.DoEvents();
        }
      };

      BeginOperation();
      Cursor prev = Cursor;
      Cursor = Cursors.WaitCursor;
      _progressBar.Minimum = 0;
      _progressBar.Maximum = 1;
      _progressBar.Value = 0;
      _progressBar.Visible = true;
      _lblProgress.Visible = true;
      _lblProgress.Text = "Загрузка состава сборки…";
      try
      {
        VelumMaterialBatchScanner.ScanResult scan =
            VelumMaterialBatchScanner.RunFromAssembly(request, assembly);
        _rows.Clear();
        _rows.AddRange(scan.Rows);
        BindListView();
        SelectAllListItems();
        UpdateStatusLabel();
      }
      finally
      {
        Cursor = prev;
        EndOperation();
        ResetProgressUi();
      }
    }

    private void LoadPartsFromActivePart()
    {
      ModelDoc2 active = null;
      try
      {
        active = _swApp?.Sw?.IActiveDoc2 as ModelDoc2;
      }
      catch
      {
      }

      if (active == null ||
          active.GetType() != (int)SolidWorks.Interop.swconst.swDocumentTypes_e.swDocPART)
      {
        MessageBox.Show(
            "Активный документ не является деталью.",
            "Пакетное присвоение материалов",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      HashSet<string> keepOpenPartPaths = VelumDxfBatchDocumentHelper.CollectOpenPartPaths(_swApp);
      var request = new VelumMaterialBatchScanner.ScanRequest
      {
        SwApp = _swApp,
        KeepOpenPartPaths = keepOpenPartPaths,
        IsCancelled = () => _cancelRequested,
        OnProgress = (current, total, fileName) =>
        {
          if (total > 0 && _progressBar.Maximum != total)
            _progressBar.Maximum = total;
          _progressBar.Value = current > _progressBar.Maximum ? _progressBar.Maximum : current;
          _lblProgress.Text = "Загрузка: " + (fileName ?? string.Empty) +
                              " (" + current + "/" + total + ")";
          Application.DoEvents();
        }
      };

      BeginOperation();
      Cursor prev = Cursor;
      Cursor = Cursors.WaitCursor;
      _progressBar.Minimum = 0;
      _progressBar.Maximum = 1;
      _progressBar.Value = 0;
      _progressBar.Visible = true;
      _lblProgress.Visible = true;
      _lblProgress.Text = "Загрузка детали…";
      try
      {
        VelumMaterialBatchScanner.ScanResult scan =
            VelumMaterialBatchScanner.RunFromPart(request, active);
        _rows.Clear();
        _rows.AddRange(scan.Rows);
        BindListView();
        SelectAllListItems();
        UpdateStatusLabel();
      }
      finally
      {
        Cursor = prev;
        EndOperation();
        ResetProgressUi();
      }
    }

    private void LoadPartsFromFolder()
    {
      string partsRoot = VelumBatchFormFolderBootstrap.ReadFolderPath(_partsFolderBox);
      if (!Directory.Exists(partsRoot))
      {
        MessageBox.Show(
            "Укажите существующий каталог деталей.",
            "Пакетное присвоение материалов",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return;
      }

      VelumAppConfig.SetMaterialBatchDefaultPartsFolder(partsRoot);
      HashSet<string> keepOpenPartPaths = VelumDxfBatchDocumentHelper.CollectOpenPartPaths(_swApp);

      var request = new VelumMaterialBatchScanner.ScanRequest
      {
        PartsRoot = partsRoot,
        SwApp = _swApp,
        KeepOpenPartPaths = keepOpenPartPaths,
        IsCancelled = () => _cancelRequested,
        OnProgress = (current, total, fileName) =>
        {
          if (total > 0 && _progressBar.Maximum != total)
            _progressBar.Maximum = total;
          _progressBar.Value = current > _progressBar.Maximum ? _progressBar.Maximum : current;
          _lblProgress.Text = "Загрузка: " + (fileName ?? string.Empty) +
                              " (" + current + "/" + total + ")";
          Application.DoEvents();
        }
      };

      BeginOperation();
      Cursor prev = Cursor;
      Cursor = Cursors.WaitCursor;
      _progressBar.Minimum = 0;
      _progressBar.Maximum = 1;
      _progressBar.Value = 0;
      _progressBar.Visible = true;
      _lblProgress.Visible = true;
      _lblProgress.Text = "Загрузка…";
      try
      {
        VelumMaterialBatchScanner.ScanResult scan = VelumMaterialBatchScanner.Run(request);
        _rows.Clear();
        _rows.AddRange(scan.Rows);
        BindListView();
        UpdateStatusLabel();
      }
      finally
      {
        Cursor = prev;
        EndOperation();
        ResetProgressUi();
      }
    }

    private void OnApply(object sender, EventArgs e)
    {
      VelumMaterialInfo materialInfo = _materialTreeView.GetSelectedMaterial();
      if (materialInfo == null)
      {
        MessageBox.Show(
            "Выберите материал в дереве.",
            "Пакетное присвоение материалов",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      var selected = new List<VelumMaterialBatchRow>();
      foreach (ListViewItem listItem in _listView.SelectedItems)
      {
        var row = listItem.Tag as VelumMaterialBatchRow;
        if (row != null)
          selected.Add(row);
      }

      if (selected.Count == 0)
      {
        MessageBox.Show(
            "Выделите строки в списке деталей.",
            "Пакетное присвоение материалов",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      string databasePath = materialInfo.DatabasePath;
      if (string.IsNullOrWhiteSpace(databasePath))
        databasePath = materialInfo.DatabaseName;

      _progressBar.Minimum = 0;
      _progressBar.Maximum = selected.Count;
      _progressBar.Value = 0;
      _progressBar.Visible = true;
      _lblProgress.Visible = true;

      int applied = 0;
      int failed = 0;
      int progress = 0;
      HashSet<string> keepOpenPartPaths = VelumDxfBatchDocumentHelper.CollectOpenPartPaths(_swApp);
      var rowsByPart = new Dictionary<string, List<VelumMaterialBatchRow>>(StringComparer.OrdinalIgnoreCase);

      for (int i = 0; i < selected.Count; i++)
      {
        VelumMaterialBatchRow row = selected[i];
        List<VelumMaterialBatchRow> bucket;
        if (!rowsByPart.TryGetValue(row.PartPath, out bucket))
        {
          bucket = new List<VelumMaterialBatchRow>();
          rowsByPart[row.PartPath] = bucket;
        }

        bucket.Add(row);
      }

      try
      {
        BeginOperation();
        foreach (KeyValuePair<string, List<VelumMaterialBatchRow>> partGroup in rowsByPart)
        {
          if (_cancelRequested)
            break;

          List<VelumMaterialBatchRow> partRows = partGroup.Value;
          if (partRows.Count == 0)
            continue;

          ModelDoc2 modelDoc = VelumDxfBatchDocumentHelper.TryOpenPartSilent(
              _swApp,
              partGroup.Key,
              out _);
          if (modelDoc == null)
          {
            failed += partRows.Count;
            progress += partRows.Count;
            continue;
          }

          bool partChanged = false;
          try
          {
            for (int i = 0; i < partRows.Count; i++)
            {
              if (_cancelRequested)
                break;

              VelumMaterialBatchRow row = partRows[i];
              progress++;
              _lblProgress.Text = "Применение: " + row.PartDisplayName + " / " + row.ConfigName +
                                  " (" + progress + "/" + selected.Count + ")";
              _progressBar.Value = progress;
              Application.DoEvents();

              bool success = VelumSolidWorksMaterialComHelper.TryAssignPartMaterial(
                  modelDoc,
                  row.ConfigName,
                  materialInfo.MaterialName,
                  databasePath);

              if (success)
              {
                applied++;
                partChanged = true;
              }
              else
              {
                failed++;
              }
            }

            if (partChanged)
              VelumMaterialBatchScanner.RefreshPartRows(modelDoc, partGroup.Key, _rows);
          }
          finally
          {
            VelumDxfBatchDocumentHelper.TryReleasePartAfterBatch(
                _swApp,
                modelDoc,
                partGroup.Key,
                persistChanges: partChanged,
                keepOpenPartPaths);
          }
        }
      }
      finally
      {
        EndOperation();
        ResetProgressUi();
      }

      BindListView();
      UpdateStatusLabel();
      VelumSolidProbeRefreshPlanner.MarkExportDocumentationStale();

      string completionMessage = _cancelRequested
          ? "Применение остановлено. Присвоено: " + applied + (failed > 0 ? "\nОшибок: " + failed : string.Empty)
          : "Присвоено: " + applied + (failed > 0 ? "\nОшибок: " + failed : string.Empty);
      MessageBox.Show(
          completionMessage,
          "Пакетное присвоение материалов",
          MessageBoxButtons.OK,
          failed > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);

      // Из реестра изделия: после успешной замены закрываем с OK (родитель обновит список).
      if (_closeOnSuccessfulApply &&
          !_cancelRequested &&
          failed == 0 &&
          applied > 0)
      {
        DialogResult = DialogResult.OK;
        Close();
      }
    }

    private void BindListView()
    {
      string partFilter = _activePartFilter;
      string configFilter = _activeConfigFilter;
      string materialFilter = _activeMaterialFilter;

      // Фильтрация.
      var filtered = new List<VelumMaterialBatchRow>();
      for (int i = 0; i < _rows.Count; i++)
      {
        VelumMaterialBatchRow row = _rows[i];
        if (!VelumListFilterHelper.Matches(row.PartDisplayName, partFilter) ||
            !VelumListFilterHelper.Matches(row.ConfigName, configFilter) ||
            !VelumListFilterHelper.Matches(row.MaterialName, materialFilter))
          continue;
        filtered.Add(row);
      }

      // Сортировка по активному столбцу.
      if (_sortColumn >= 0)
      {
        SortMaterialBatchRows(filtered, _sortColumn, _sortDirection);
      }

      // Группировка по свойству «Раздел».
      var groups = new Dictionary<string, List<ListViewItem>>();
      const string defaultGroupKey = "__Default__";

      _listView.BeginUpdate();
      _listView.Items.Clear();
      _listView.Groups.Clear();

      int visibleCount = 0;

      for (int i = 0; i < filtered.Count; i++)
      {
        VelumMaterialBatchRow row = filtered[i];
        string groupKey = string.IsNullOrWhiteSpace(row.Section) ? defaultGroupKey : row.Section;

        List<ListViewItem> groupItems;
        if (!groups.TryGetValue(groupKey, out groupItems))
        {
          groupItems = new List<ListViewItem>();
          groups[groupKey] = groupItems;
        }

        visibleCount++;
        string materialText = FormatMaterialCell(row.MaterialName, row.MaterialDatabase);
        var listItem = new ListViewItem(row.PartDisplayName)
        {
          Tag = row
        };
        listItem.SubItems.Add(row.ConfigName);
        listItem.SubItems.Add(materialText);
        groupItems.Add(listItem);
      }

      // Создание групп и добавление элементов.
      foreach (KeyValuePair<string, List<ListViewItem>> kvp in groups)
      {
        string groupTitle = kvp.Key == defaultGroupKey ? "Детали" : kvp.Key;
        var group = new ListViewGroup(groupTitle,HorizontalAlignment.Left);
        _listView.Groups.Add(group);

        foreach (ListViewItem item in kvp.Value)
        {
          item.Group = group;
          _listView.Items.Add(item);
        }
      }

      _listView.EndUpdate();
      _statusLabel.Text = "Строк: " + visibleCount + " из " + _rows.Count;
    }

    /// <summary>
    /// Сортировка списка строк по номеру столбца.
    /// </summary>
    private static void SortMaterialBatchRows(
        List<VelumMaterialBatchRow> rows,
        int column,
        ListSortDirection direction)
    {
      bool ascending = direction == ListSortDirection.Ascending;
      rows.Sort((a, b) =>
      {
        string va = GetCellValue(a, column);
        string vb = GetCellValue(b, column);
        return ascending ? CompareValues(va, vb) : CompareValues(vb, va);
      });
    }

    /// <summary>
    /// Получает текстовое значение ячейки по индексу столбца.
    /// </summary>
    private static string GetCellValue(VelumMaterialBatchRow row, int column)
    {
      switch (column)
      {
        case 0: return row.PartDisplayName;
        case 1: return row.ConfigName;
        case 2: return FormatMaterialCell(row.MaterialName, row.MaterialDatabase);
        case 3: return row.Section;
        default: return string.Empty;
      }
    }

    /// <summary>
    /// Сравнение значений: сначала числовое (если оба числа), иначе строковое.
    /// </summary>
    private static int CompareValues(string a, string b)
    {
      if (string.Equals(a, b, StringComparison.Ordinal))
        return 0;

      if (string.IsNullOrEmpty(a))
        return -1;
      if (string.IsNullOrEmpty(b))
        return 1;

      // Пробуем числовое сравнение.
      if (double.TryParse(a, System.Globalization.NumberStyles.Any,
              System.Globalization.CultureInfo.InvariantCulture, out double na) &&
          double.TryParse(b, System.Globalization.NumberStyles.Any,
              System.Globalization.CultureInfo.InvariantCulture, out double nb))
      {
        return na.CompareTo(nb);
      }

      return string.Compare(a, b, StringComparison.Ordinal);
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

    private void UpdateStatusLabel()
    {
      _statusLabel.ForeColor = _rows.Count > 0 ? Color.DarkGreen : SystemColors.ControlText;
      if (_rows.Count == 0)
        _statusLabel.Text = "Список пуст — укажите каталог и нажмите «Загрузить»";
    }

    private static string FormatMaterialCell(string materialName, string databaseName)
    {
      if (string.IsNullOrWhiteSpace(materialName))
        return string.Empty;

      if (string.IsNullOrWhiteSpace(databaseName))
        return materialName;

      return materialName + " (" + databaseName + ")";
    }

    private void OnListViewDoubleClick(object sender, EventArgs e)
    {
      OnOpenPartFromList();
    }

    private void OnListViewMouseUp(object sender, MouseEventArgs e)
    {
      if (e.Button != MouseButtons.Right)
        return;

      ListViewItem hit = _listView.GetItemAt(e.X, e.Y);
      if (hit != null)
      {
        if (!hit.Selected)
        {
          _listView.SelectedIndices.Clear();
          hit.Selected = true;
        }
      }
      else if (_listView.Items.Count == 0)
        return;

      _listContextMenu.Show(_listView, e.Location);
    }

    private void OnOpenPartFromList()
    {
      VelumMaterialBatchRow row = TryGetSelectedDataRow();
      if (row == null || string.IsNullOrWhiteSpace(row.PartPath))
        return;

      if (!VelumDxfBatchDocumentHelper.TryActivateOrOpenPartVisible(_swApp, row.PartPath, out string error))
      {
        MessageBox.Show(
            string.IsNullOrWhiteSpace(error) ? "Не удалось открыть деталь." : error,
            "Пакетное присвоение материалов",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
      }
    }

    private VelumMaterialBatchRow TryGetSelectedDataRow()
    {
      if (_listView.SelectedItems.Count == 0)
        return null;

      return _listView.SelectedItems[0].Tag as VelumMaterialBatchRow;
    }

    private void OnStopOperation(object sender, EventArgs e)
    {
      _cancelRequested = true;
    }

    /// <summary>
    /// Блокирует все элементы управления формы, кроме кнопки «Стоп», при запуске длительной операции.
    /// </summary>
    private void BeginOperation()
    {
      _cancelRequested = false;
      _btnStop.Enabled = true;
      _btnBrowseParts.Enabled = false;
      _btnLoadParts.Enabled = false;
      _btnApply.Enabled = false;
      _btnMaterialSearch.Enabled = false;
      _btnMaterialNext.Enabled = false;
      _btnMaterialPrev.Enabled = false;
      _btnFilterApply.Enabled = false;
      _btnFilterReset.Enabled = false;
      _btnFilterHelp.Enabled = false;
      _btnClose.Enabled = false;
      _partsFolderBox.Enabled = false;
      _materialFilterBox.Enabled = false;
      _partFilterBox.Enabled = false;
      _configFilterBox.Enabled = false;
      _materialListFilterBox.Enabled = false;
      _materialTreeViewControl.Enabled = false;
      _listView.Enabled = false;
    }

    /// <summary>
    /// Разблокирует все элементы управления формы после завершения длительной операции.
    /// </summary>
    private void EndOperation()
    {
      _btnStop.Enabled = false;
      _btnBrowseParts.Enabled = true;
      _btnLoadParts.Enabled = true;
      _btnApply.Enabled = true;
      _btnMaterialSearch.Enabled = true;
      _btnMaterialNext.Enabled = true;
      _btnMaterialPrev.Enabled = true;
      _btnFilterApply.Enabled = true;
      _btnFilterReset.Enabled = true;
      _btnFilterHelp.Enabled = true;
      _btnClose.Enabled = true;
      _partsFolderBox.Enabled = true;
      _materialFilterBox.Enabled = true;
      _partFilterBox.Enabled = true;
      _configFilterBox.Enabled = true;
      _materialListFilterBox.Enabled = true;
      _materialTreeViewControl.Enabled = true;
      _listView.Enabled = true;
    }

    private void ResetProgressUi()
    {
      _progressBar.Value = 0;
      _progressBar.Visible = false;
      _lblProgress.Visible = false;
      _lblProgress.Text = "Применение:";
    }

    private void BrowseFolder(TextBox target, string description)
    {
      string initial = _assemblyFolder ?? string.Empty;
      if (string.IsNullOrWhiteSpace(initial))
        initial = VelumBatchFormFolderBootstrap.ReadFolderPath(target);
      string selected;
      if (!VelumFolderBrowser.TrySelect(this, description, initial, out selected))
        return;

      target.ForeColor = SystemColors.WindowText;
      target.Text = selected;
    }

    private static Icon TryLoadFormIcon()
    {
      try
      {
        string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (string.IsNullOrEmpty(dir))
          return null;

        string path = Path.Combine(dir, "icons", "Database.ico");
        return File.Exists(path) ? new Icon(path) : null;
      }
      catch
      {
        return null;
      }
    }

    private static Bitmap TryLoadTreeBitmap(string fileName)
    {
      try
      {
        string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (string.IsNullOrEmpty(dir))
          return null;

        string path = Path.Combine(dir, "icons", fileName);
        if (!File.Exists(path))
          return null;

        using (Icon icon = new Icon(path))
          return icon.ToBitmap();
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

    private static Bitmap CreateTreeIcon(Color color)
    {
      var bitmap = new Bitmap(16, 16);
      using (Graphics g = Graphics.FromImage(bitmap))
      {
        g.Clear(Color.Transparent);
        using (var brush = new SolidBrush(color))
          g.FillRectangle(brush, 2, 2, 12, 12);
      }

      return bitmap;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
      _materialTreeView?.Dispose();
      base.OnFormClosing(e);
    }
  }
}
