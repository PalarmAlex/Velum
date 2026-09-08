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
  /// <summary>
  /// Пакетное обновление пользовательских свойств деталей и сборок (вкладки раздельно).
  /// </summary>
  internal sealed partial class VelumDocumentPropertyBatchForm : Form
  {
    private const string FormTitle = "Обновление свойств документов";

    private readonly ISwApplication _swApp;
    private readonly List<VelumDocumentPropertyBatchRow> _partRows =
        new List<VelumDocumentPropertyBatchRow>();
    private readonly List<VelumDocumentPropertyBatchRow> _assemblyRows =
        new List<VelumDocumentPropertyBatchRow>();

    private VelumDocumentPropertyTemplateGridHelper _partGridHelper;
    private VelumDocumentPropertyTemplateGridHelper _asmGridHelper;
    private string _partTemplatePath = string.Empty;
    private string _asmTemplatePath = string.Empty;
    private bool _cancelRequested;
    private ContextMenuStrip _partsListContextMenu;
    private ContextMenuStrip _asmListContextMenu;
    private VelumListViewCellFilterMenu _partsCellFilterMenu;
    private VelumListViewCellFilterMenu _asmCellFilterMenu;
    private string _partsFilter = string.Empty;
    private string _asmFilter = string.Empty;

    // Сортировка по столбцам.
    private int _partsSortColumn = -1;
    private ListSortDirection _partsSortDirection = ListSortDirection.Ascending;
    private int _asmSortColumn = -1;
    private ListSortDirection _asmSortDirection = ListSortDirection.Ascending;

    public VelumDocumentPropertyBatchForm(ISwApplication swApp)
    {
      _swApp = swApp;
      InitializeComponent();
      VelumFormHelp.Bind(this, VelumHelpTopics.DocumentProperties);
      InitializeRuntime();
    }

    private void InitializeRuntime()
    {
      Icon icon = TryLoadFormIcon();
      if (icon != null)
        Icon = icon;

      KeyPreview = true;
      KeyDown += (s, e) =>
      {
        if (e.KeyCode == Keys.Escape)
        {
          Close();
          e.Handled = true;
        }
      };

      InitializeTemplatePaths();
      InitializeGrids();
      InitializeListInteractions();
      BindFolderPathTooltip();

      VelumBatchFormFolderBootstrap.DocumentPropertyFolders folders =
          VelumBatchFormFolderBootstrap.ResolveDocumentPropertyFolders(_swApp);
      ApplyResolvedFolders(folders);
      AutoLoadFromContext(folders);
      UpdateStatusLabels();
    }

    private void InitializeTemplatePaths()
    {
      _partTemplatePath = VelumAppConfig.DocumentPropertyPartTemplatePath ?? string.Empty;
      _asmTemplatePath = VelumAppConfig.DocumentPropertyAsmTemplatePath ?? string.Empty;
      _partTemplateBox.Text = _partTemplatePath;
      _asmTemplateBox.Text = _asmTemplatePath;
    }

    private void InitializeGrids()
    {
      Image addImg = TryLoadMenuBitmap("Add.png");
      Image removeImg = TryLoadMenuBitmap("Remove.png");
      Image defaultImg = TryLoadMenuBitmap("Anchor.png");
      Image removeDefaultImg = TryLoadMenuBitmap("Fall.png");
      Image eraseImg = TryLoadMenuBitmap("Erase.png");
      Image deleteImg = TryLoadMenuBitmap("Delete.png");

      _partGridHelper = new VelumDocumentPropertyTemplateGridHelper(
          _partsGrid,
          "colPartPropName",
          "colPartPropValue",
          _partTemplatePath,
          addImg,
          removeImg,
          defaultImg,
          removeDefaultImg,
          eraseImg,
          deleteImg);
      _partGridHelper.LoadDataFromXml();

      _asmGridHelper = new VelumDocumentPropertyTemplateGridHelper(
          _asmGrid,
          "colAsmPropName",
          "colAsmPropValue",
          _asmTemplatePath,
          addImg,
          removeImg,
          defaultImg,
          removeDefaultImg,
          eraseImg,
          deleteImg);
      _asmGridHelper.LoadDataFromXml();
    }

    private void InitializeListInteractions()
    {
      _partsList.ColumnClick += OnPartsListColumnClick;
      _asmList.ColumnClick += OnAsmListColumnClick;

      Image openIcon = TryLoadMenuBitmap("Yes.png");
      Image selectAllIcon = TryLoadMenuBitmap("editselectall.png");

      _partsListContextMenu = new ContextMenuStrip();
      _partsListContextMenu.Items.Add(
          new ToolStripMenuItem("Открыть", openIcon, (s, e) => OnOpenFromList(_partsList)));
      Image filterIcon = TryLoadMenuBitmap("Thumbs up.png");
      Image excludeIcon = TryLoadMenuBitmap("Thumbs down.png");
      _partsCellFilterMenu = new VelumListViewCellFilterMenu(
          _partsList,
          this,
          column => column == 0 || column == 1 ? _partsFilterBox : null,
          ApplyPartsFilterFromUi,
          null,
          null);
      _partsCellFilterMenu.AppendTo(_partsListContextMenu, filterIcon, excludeIcon);
      _partsListContextMenu.Items.Add(new ToolStripSeparator());
      _partsListContextMenu.Items.Add(
          new ToolStripMenuItem("Выделить все", selectAllIcon, (s, e) => SelectAll(_partsList)));

      _asmListContextMenu = new ContextMenuStrip();
      _asmListContextMenu.Items.Add(
          new ToolStripMenuItem("Открыть", openIcon, (s, e) => OnOpenFromList(_asmList)));
      _asmCellFilterMenu = new VelumListViewCellFilterMenu(
          _asmList,
          this,
          column => column == 0 || column == 1 ? _asmFilterBox : null,
          ApplyAsmFilterFromUi,
          null,
          null);
      _asmCellFilterMenu.AppendTo(_asmListContextMenu, filterIcon, excludeIcon);
      _asmListContextMenu.Items.Add(new ToolStripSeparator());
      _asmListContextMenu.Items.Add(
          new ToolStripMenuItem("Выделить все", selectAllIcon, (s, e) => SelectAll(_asmList)));

      _partsList.DoubleClick += (s, e) => OnOpenFromList(_partsList);
      _asmList.DoubleClick += (s, e) => OnOpenFromList(_asmList);
      _partsList.MouseUp += (s, e) => OnListMouseUp(_partsList, _partsListContextMenu, e);
      _asmList.MouseUp += (s, e) => OnListMouseUp(_asmList, _asmListContextMenu, e);

      KeyEventHandler partsEnter = (s, e) =>
      {
        if (e.KeyCode != Keys.Enter)
          return;
        ApplyPartsFilterFromUi();
        e.Handled = true;
        e.SuppressKeyPress = true;
      };
      KeyEventHandler asmEnter = (s, e) =>
      {
        if (e.KeyCode != Keys.Enter)
          return;
        ApplyAsmFilterFromUi();
        e.Handled = true;
        e.SuppressKeyPress = true;
      };

      _partsFilterBox.KeyDown += partsEnter;
      _asmFilterBox.KeyDown += asmEnter;
      _btnPartsFilterReset.Click += (s, e) => ResetPartsFilter();
      _btnAsmFilterReset.Click += (s, e) => ResetAsmFilter();
      _btnPartsFilterHelp.Click += (s, e) => VelumListFilterHelper.ShowHelp(this);
      _btnAsmFilterHelp.Click += (s, e) => VelumListFilterHelper.ShowHelp(this);
    }

    private void OnPartsListColumnClick(object sender, ColumnClickEventArgs e)
    {
      if (e.Column == _partsSortColumn)
      {
        _partsSortDirection = _partsSortDirection == ListSortDirection.Ascending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;
      }
      else
      {
        _partsSortColumn = e.Column;
        _partsSortDirection = ListSortDirection.Ascending;
      }

      BindPartsList();
    }

    private void OnAsmListColumnClick(object sender, ColumnClickEventArgs e)
    {
      if (e.Column == _asmSortColumn)
      {
        _asmSortDirection = _asmSortDirection == ListSortDirection.Ascending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;
      }
      else
      {
        _asmSortColumn = e.Column;
        _asmSortDirection = ListSortDirection.Ascending;
      }

      BindAssembliesList();
    }

    private void ApplyPartsFilterFromUi()
    {
      _partsFilter = (_partsFilterBox.Text ?? string.Empty).Trim();
      BindPartsList();
    }

    private void ApplyAsmFilterFromUi()
    {
      _asmFilter = (_asmFilterBox.Text ?? string.Empty).Trim();
      BindAssembliesList();
    }

    private void ResetPartsFilter()
    {
      _partsFilterBox.Text = string.Empty;
      _partsFilter = string.Empty;
      BindPartsList();
    }

    private void ResetAsmFilter()
    {
      _asmFilterBox.Text = string.Empty;
      _asmFilter = string.Empty;
      BindAssembliesList();
    }

    private void BindFolderPathTooltip()
    {
      var tip = new ToolTip();
      VelumBatchFormFolderBootstrap.BindFolderPathTooltip(_catalogFolderBox, tip);
      tip.SetToolTip(_btnBrowseCatalog, "Выбрать каталог изделия");
      tip.SetToolTip(_btnLoadCatalog, "Загрузить детали и сборки из каталога");
      tip.SetToolTip(_btnBrowsePartTemplate, "Выбрать шаблон свойств для деталей");
      tip.SetToolTip(_btnBrowseAsmTemplate, "Выбрать шаблон свойств для сборок");
      tip.SetToolTip(_btnPartsFilterHelp, "Справка по маскам фильтра");
      tip.SetToolTip(_btnAsmFilterHelp, "Справка по маскам фильтра");
      tip.SetToolTip(_btnPartsFilterReset, "Сбросить фильтр");
      tip.SetToolTip(_btnAsmFilterReset, "Сбросить фильтр");
      tip.SetToolTip(_btnApply, "Применить свойства к выделенным записям текущей вкладки");
      tip.SetToolTip(_btnStop, "Остановить обработку");
      tip.SetToolTip(_btnClose, "Закрыть");
      tip.SetToolTip(
          _rbCreateInSettings,
          "Если свойства нет в конфигурации и на главной вкладке — создать на главной вкладке («Настройка»)");
      tip.SetToolTip(
          _rbCreateInConfigs,
          "Если свойства нет в конфигурации и на главной вкладке — создать во всех конфигурациях документа");
    }

    private void ApplyResolvedFolders(VelumBatchFormFolderBootstrap.DocumentPropertyFolders folders)
    {
      VelumBatchFormFolderBootstrap.ApplyFolderOrHint(
          _catalogFolderBox,
          folders?.CatalogFolder,
          null);
    }

    private void AutoLoadFromContext(VelumBatchFormFolderBootstrap.DocumentPropertyFolders folders)
    {
      if (folders == null || !folders.FromActiveDocument)
        return;

      if (folders.FromActiveAssembly)
      {
        VelumDocumentPropertyBatchHelper.CollectFromActiveAssembly(
            _swApp,
            _partRows,
            _assemblyRows);
      }
      else if (folders.FromActivePart)
      {
        VelumDocumentPropertyBatchHelper.CollectFromActivePart(
            _swApp,
            _partRows,
            _assemblyRows);
      }

      BindPartsList();
      BindAssembliesList();
      SelectAll(_partsList);
      SelectAll(_asmList);
    }

    private void OnBrowseCatalog(object sender, EventArgs e)
    {
      string initial = VelumBatchFormFolderBootstrap.ReadFolderPath(_catalogFolderBox);
      string selected;
      if (!VelumFolderBrowser.TrySelect(this, "Каталог изделия", initial, out selected))
        return;

      _catalogFolderBox.ForeColor = SystemColors.WindowText;
      _catalogFolderBox.Text = selected;
      VelumAppConfig.SetDocumentPropertyBatchDefaultCatalogFolder(selected);
    }

    private void OnLoadCatalog(object sender, EventArgs e)
    {
      string catalog = VelumBatchFormFolderBootstrap.ReadFolderPath(_catalogFolderBox);
      if (!Directory.Exists(catalog))
      {
        MessageBox.Show(
            "Укажите существующий каталог изделия.",
            FormTitle,
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return;
      }

      VelumAppConfig.SetDocumentPropertyBatchDefaultCatalogFolder(catalog);
      BeginOperation();
      Cursor prev = Cursor;
      Cursor = Cursors.WaitCursor;
      ShowProgress("Загрузка…", 0, 1);
      try
      {
        VelumDocumentPropertyBatchHelper.CollectFromCatalogFolder(
            _swApp,
            catalog,
            _partRows,
            _assemblyRows,
            () => _cancelRequested,
            (current, total, fileName) =>
            {
              ShowProgress(
                  "Загрузка: " + fileName + " (" + current + "/" + total + ")",
                  current,
                  total);
              Application.DoEvents();
            });
        BindPartsList();
        BindAssembliesList();
        SelectAll(_partsList);
        SelectAll(_asmList);
        UpdateStatusLabels();
      }
      finally
      {
        Cursor = prev;
        EndOperation();
        ResetProgressUi();
      }
    }

    private void OnBrowsePartTemplate(object sender, EventArgs e)
    {
      string selected = BrowseTemplateFile(
          "Файлы свойств деталей (*.prtprp)|*.prtprp",
          "Шаблон свойств деталей",
          _partTemplatePath);
      if (selected == null)
        return;

      _partTemplatePath = selected;
      _partTemplateBox.Text = selected;
      VelumAppConfig.SetDocumentPropertyPartTemplatePath(selected);
      _partGridHelper.UpdateXmlFilePath(selected);
      _partGridHelper.LoadDataFromXml();
    }

    private void OnBrowseAsmTemplate(object sender, EventArgs e)
    {
      string selected = BrowseTemplateFile(
          "Файлы свойств сборок (*.asmprp)|*.asmprp",
          "Шаблон свойств сборок",
          _asmTemplatePath);
      if (selected == null)
        return;

      _asmTemplatePath = selected;
      _asmTemplateBox.Text = selected;
      VelumAppConfig.SetDocumentPropertyAsmTemplatePath(selected);
      _asmGridHelper.UpdateXmlFilePath(selected);
      _asmGridHelper.LoadDataFromXml();
    }

    private string BrowseTemplateFile(string filter, string title, string initialPath)
    {
      using (var dialog = new OpenFileDialog())
      {
        dialog.Filter = filter;
        dialog.Title = title;
        dialog.CheckFileExists = true;
        string initialDir = null;
        try
        {
          if (!string.IsNullOrWhiteSpace(initialPath))
            initialDir = Path.GetDirectoryName(initialPath);
        }
        catch
        {
          initialDir = null;
        }

        if (string.IsNullOrWhiteSpace(initialDir) || !Directory.Exists(initialDir))
          initialDir = VelumAppConfig.SettingsPath;

        if (!string.IsNullOrWhiteSpace(initialDir) && Directory.Exists(initialDir))
          dialog.InitialDirectory = initialDir;

        if (!string.IsNullOrWhiteSpace(initialPath) && File.Exists(initialPath))
          dialog.FileName = Path.GetFileName(initialPath);

        return dialog.ShowDialog(this) == DialogResult.OK ? dialog.FileName : null;
      }
    }

    private void OnApply(object sender, EventArgs e)
    {
      bool partsTab = _tabs.SelectedTab == _tabParts;
      ListView list = partsTab ? _partsList : _asmList;
      VelumDocumentPropertyTemplateGridHelper gridHelper = partsTab ? _partGridHelper : _asmGridHelper;
      string entityName = partsTab ? "деталей" : "сборок";

      var selected = new List<VelumDocumentPropertyBatchRow>();
      foreach (ListViewItem item in list.SelectedItems)
      {
        var row = item.Tag as VelumDocumentPropertyBatchRow;
        if (row != null)
          selected.Add(row);
      }

      if (selected.Count == 0)
      {
        MessageBox.Show(
            "Выделите записи в списке " + entityName + ".",
            FormTitle,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      IReadOnlyList<VelumDocumentPropertyBatchHelper.PropertyPair> properties =
          gridHelper.CollectProperties();
      if (properties.Count == 0)
      {
        MessageBox.Show(
            "В шаблоне нет свойств для записи.",
            FormTitle,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      if (gridHelper.HasEmptyPropertyValues(out string emptyName))
      {
        if (MessageBox.Show(
                "Значение для свойства «" + emptyName +
                "» пустое. Очистить/записать пустые значения в документах?",
                FormTitle,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
          return;
      }

      if (MessageBox.Show(
              "Обновить свойства в " + selected.Count + " выделенных записях " + entityName + "?",
              FormTitle,
              MessageBoxButtons.YesNo,
              MessageBoxIcon.Question) != DialogResult.Yes)
        return;

      // Группируем по файлу: одна открытие/сохранение на документ, только выделенные конфигурации.
      var groups = new List<List<VelumDocumentPropertyBatchRow>>();
      var groupIndexByPath = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
      for (int i = 0; i < selected.Count; i++)
      {
        VelumDocumentPropertyBatchRow row = selected[i];
        string path = row.FilePath ?? string.Empty;
        int index;
        if (!groupIndexByPath.TryGetValue(path, out index))
        {
          index = groups.Count;
          groupIndexByPath[path] = index;
          groups.Add(new List<VelumDocumentPropertyBatchRow>());
        }

        groups[index].Add(row);
      }

      HashSet<string> keepOpen = VelumDocumentPropertyBatchHelper.CollectOpenDocumentPaths(_swApp);
      VelumDocumentPropertyCreateTarget createTarget = _rbCreateInConfigs.Checked
          ? VelumDocumentPropertyCreateTarget.AllConfigurations
          : VelumDocumentPropertyCreateTarget.DocumentSettings;

      BeginOperation();
      ShowProgress("Применение…", 0, selected.Count);
      int applied = 0;
      int failed = 0;
      int processed = 0;

      try
      {
        for (int g = 0; g < groups.Count; g++)
        {
          if (_cancelRequested)
            break;

          List<VelumDocumentPropertyBatchRow> group = groups[g];
          VelumDocumentPropertyBatchRow first = group[0];

          ModelDoc2 doc = VelumDocumentPropertyBatchHelper.TryOpenSilent(
              _swApp,
              first.FilePath,
              first.IsAssembly,
              out _);
          if (doc == null)
          {
            failed += group.Count;
            processed += group.Count;
            _progressBar.Value = Math.Min(processed, _progressBar.Maximum);
            Application.DoEvents();
            continue;
          }

          bool changed = false;
          try
          {
            for (int i = 0; i < group.Count; i++)
            {
              if (_cancelRequested)
                break;

              VelumDocumentPropertyBatchRow row = group[i];
              processed++;
              string configLabel = string.IsNullOrWhiteSpace(row.ConfigurationName)
                  ? string.Empty
                  : " [" + row.ConfigurationName + "]";
              _lblProgress.Text = "Применение: " + row.DisplayName + configLabel +
                                  " (" + processed + "/" + selected.Count + ")";
              _progressBar.Value = Math.Min(processed, _progressBar.Maximum);
              Application.DoEvents();

              bool rowChanged = VelumDocumentPropertyBatchHelper.TryApplyDocumentProperties(
                  doc,
                  row.ConfigurationName,
                  properties,
                  createTarget);
              if (rowChanged)
              {
                changed = true;
                applied++;
              }
              else
              {
                failed++;
              }
            }
          }
          finally
          {
            VelumDocumentPropertyBatchHelper.TryReleaseAfterBatch(
                _swApp,
                doc,
                first.FilePath,
                persistChanges: changed,
                keepOpenPaths: keepOpen);
          }
        }
      }
      finally
      {
        EndOperation();
        ResetProgressUi();
      }

      UpdateStatusLabels();

      string message = _cancelRequested
          ? "Обработка остановлена. Обновлено: " + applied
          : "Обновлено: " + applied;
      if (failed > 0)
        message += "\nБез изменений / ошибок: " + failed;

      MessageBox.Show(
          message,
          FormTitle,
          MessageBoxButtons.OK,
          failed > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
    }

    private void OnStop(object sender, EventArgs e)
    {
      _cancelRequested = true;
    }

    private void OnCloseClick(object sender, EventArgs e)
    {
      Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
      try
      {
        _partGridHelper?.PersistNewRowsToXml();
        _asmGridHelper?.PersistNewRowsToXml();
      }
      catch
      {
      }

      base.OnFormClosing(e);
    }

    private void BindPartsList()
    {
      var rows = new List<VelumDocumentPropertyBatchRow>(_partRows);
      if (_partsSortColumn >= 0)
        SortBatchRows(rows, _partsSortColumn, _partsSortDirection);
      BindList(_partsList, rows, _partsFilter, _partsStatusLabel);
    }

    private void BindAssembliesList()
    {
      var rows = new List<VelumDocumentPropertyBatchRow>(_assemblyRows);
      if (_asmSortColumn >= 0)
        SortBatchRows(rows, _asmSortColumn, _asmSortDirection);
      BindList(_asmList, rows, _asmFilter, _asmStatusLabel);
    }

    private void BindList(
        ListView list,
        List<VelumDocumentPropertyBatchRow> rows,
        string filter,
        Label statusLabel)
    {
      list.BeginUpdate();
      list.Items.Clear();
      int visible = 0;
      for (int i = 0; i < rows.Count; i++)
      {
        VelumDocumentPropertyBatchRow row = rows[i];
        if (!VelumListFilterHelper.Matches(row.DisplayName, filter) &&
            !VelumListFilterHelper.Matches(row.ConfigurationName, filter))
          continue;

        visible++;
        var item = new ListViewItem(row.DisplayName ?? string.Empty)
        {
          Tag = row,
          ToolTipText = row.FilePath
        };
        item.SubItems.Add(row.ConfigurationName ?? string.Empty);
        list.Items.Add(item);
      }

      list.EndUpdate();
      statusLabel.Text = "Строк: " + visible + " из " + rows.Count;
    }

    /// <summary>
    /// Сортировка списка строк по номеру столбца.
    /// </summary>
    private static void SortBatchRows(
        List<VelumDocumentPropertyBatchRow> rows,
        int column,
        ListSortDirection direction)
    {
      bool ascending = direction == ListSortDirection.Ascending;
      rows.Sort((a, b) =>
      {
        string va = GetBatchRowCellValue(a, column);
        string vb = GetBatchRowCellValue(b, column);
        return ascending ? CompareValues(va, vb) : CompareValues(vb, va);
      });
    }

    /// <summary>
    /// Получает текстовое значение ячейки по индексу столбца.
    /// </summary>
    private static string GetBatchRowCellValue(VelumDocumentPropertyBatchRow row, int column)
    {
      switch (column)
      {
        case 0: return row.DisplayName;
        case 1: return row.ConfigurationName;
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

    private void UpdateStatusLabels()
    {
      if (_partRows.Count == 0 && _assemblyRows.Count == 0)
      {
        _partsStatusLabel.Text = "Список пуст — укажите каталог и нажмите «Загрузить»";
        _asmStatusLabel.Text = "Список пуст — укажите каталог и нажмите «Загрузить»";
      }
      else
      {
        BindPartsList();
        BindAssembliesList();
      }
    }

    private static void SelectAll(ListView list)
    {
      if (list.Items.Count == 0)
        return;

      list.BeginUpdate();
      try
      {
        foreach (ListViewItem item in list.Items)
          item.Selected = true;
      }
      finally
      {
        list.EndUpdate();
      }
    }

    private void OnOpenFromList(ListView list)
    {
      if (list.SelectedItems.Count == 0)
        return;

      var row = list.SelectedItems[0].Tag as VelumDocumentPropertyBatchRow;
      if (row == null || string.IsNullOrWhiteSpace(row.FilePath))
        return;

      if (!VelumDocumentPropertyBatchHelper.TryActivateOrOpenVisible(
              _swApp,
              row.FilePath,
              row.IsAssembly,
              out string error))
      {
        MessageBox.Show(
            string.IsNullOrWhiteSpace(error) ? "Не удалось открыть документ." : error,
            FormTitle,
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
      }
    }

    private static void OnListMouseUp(ListView list, ContextMenuStrip menu, MouseEventArgs e)
    {
      if (e.Button != MouseButtons.Right)
        return;

      ListViewItem hit = list.GetItemAt(e.X, e.Y);
      if (hit != null)
      {
        // Уже выделенную строку не трогаем — сохраняем мультивыбор для фильтра.
        if (!hit.Selected)
        {
          list.SelectedIndices.Clear();
          hit.Selected = true;
        }
      }
      else if (list.Items.Count == 0)
        return;

      menu.Show(list, e.Location);
    }

    /// <summary>
    /// Блокирует все элементы управления формы, кроме кнопки «Стоп», при запуске длительной операции.
    /// </summary>
    private void BeginOperation()
    {
      _cancelRequested = false;
      _btnStop.Enabled = true;
      _btnBrowseCatalog.Enabled = false;
      _btnLoadCatalog.Enabled = false;
      _btnBrowsePartTemplate.Enabled = false;
      _btnBrowseAsmTemplate.Enabled = false;
      _btnPartsFilterReset.Enabled = false;
      _btnPartsFilterHelp.Enabled = false;
      _btnAsmFilterReset.Enabled = false;
      _btnAsmFilterHelp.Enabled = false;
      _btnApply.Enabled = false;
      _btnClose.Enabled = false;
      _catalogFolderBox.Enabled = false;
      _rbCreateInSettings.Enabled = false;
      _rbCreateInConfigs.Enabled = false;
    }

    /// <summary>
    /// Разблокирует все элементы управления формы после завершения длительной операции.
    /// </summary>
    private void EndOperation()
    {
      _btnStop.Enabled = false;
      _btnBrowseCatalog.Enabled = true;
      _btnLoadCatalog.Enabled = true;
      _btnBrowsePartTemplate.Enabled = true;
      _btnBrowseAsmTemplate.Enabled = true;
      _btnPartsFilterReset.Enabled = true;
      _btnPartsFilterHelp.Enabled = true;
      _btnAsmFilterReset.Enabled = true;
      _btnAsmFilterHelp.Enabled = true;
      _btnApply.Enabled = true;
      _btnClose.Enabled = true;
      _catalogFolderBox.Enabled = true;
      _rbCreateInSettings.Enabled = true;
      _rbCreateInConfigs.Enabled = true;
    }

    private void ShowProgress(string text, int value, int maximum)
    {
      _progressBar.Minimum = 0;
      _progressBar.Maximum = Math.Max(maximum, 1);
      _progressBar.Value = Math.Min(value, _progressBar.Maximum);
      _progressBar.Visible = true;
      _lblProgress.Visible = true;
      _lblProgress.Text = text;
    }

    private void ResetProgressUi()
    {
      _progressBar.Value = 0;
      _progressBar.Visible = false;
      _lblProgress.Visible = false;
      _lblProgress.Text = string.Empty;
    }

    private static Icon TryLoadFormIcon()
    {
      try
      {
        string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (string.IsNullOrEmpty(dir))
          return null;

        string pngPath = Path.Combine(dir, "icons", "EditPage.png");
        if (File.Exists(pngPath))
        {
          using (var bmp = new Bitmap(pngPath))
          {
            IntPtr handle = bmp.GetHicon();
            using (Icon temp = Icon.FromHandle(handle))
              return (Icon)temp.Clone();
          }
        }

        string icoPath = Path.Combine(dir, "icons", "velum.ico");
        return File.Exists(icoPath) ? new Icon(icoPath) : null;
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
  }
}
