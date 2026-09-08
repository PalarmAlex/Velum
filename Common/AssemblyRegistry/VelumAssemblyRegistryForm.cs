using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Velum.SolidHomeostasis;
using Velum.UI.AssemblyRegistry;
using Xarial.XCad.SolidWorks;

namespace Velum.UI
{
  /// <summary>Реестр состава активной сборки и её каталогов.</summary>
  internal sealed partial class VelumAssemblyRegistryForm : Form
  {
    private const string ImgFolderClosed = "folder_closed";
    private const string ImgFolderOpen = "folder_open";
    private const string ImgAssembly = "assembly";
    private const string ImgPart = "part";
    private const string ImgStandard = "standard";

    private readonly ISwApplication _swApp;
    private readonly HashSet<string> _ephemeralFolderKeys =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<TreeNode> _treeMultiSelected = new HashSet<TreeNode>();
    private TreeNode _treePendingSingleSelect;
    private bool _treeDragInProgress;
    private readonly List<TreeNode> _treeSearchResults = new List<TreeNode>();
    private List<VelumAssemblyRegistryColumnDef> _activeColumns =
        new List<VelumAssemblyRegistryColumnDef>();
    private int[] _visibleToActualIndex = Array.Empty<int>();
    private TextBox[] _filterBoxes = Array.Empty<TextBox>();
    private string[] _activeFilters = Array.Empty<string>();
    private VelumAssemblyRegistryGraph _graph = new VelumAssemblyRegistryGraph();
    private ToolTip _toolTip;
    private ContextMenuStrip _treeMenu;
    private ContextMenuStrip _listMenu;
    private ToolStripMenuItem _menuListOpen;
    private ToolStripMenuItem _menuListSelectAll;
    private ToolStripMenuItem _menuListPrintDrawings;
    private ToolStripMenuItem _menuListChangeCatalog;
    private ToolStripMenuItem _menuListChangeMaterial;
    private VelumListViewCellFilterMenu _listCellFilterMenu;
    private TreeNode _treeShiftAnchor;
    private int _idxFolderClosed = -1, _idxFolderOpen = -1;
    private int _idxAssembly = -1, _idxPart = -1, _idxStandard = -1;
    private int _treeSearchIndex = -1;
    private int _sortColumn;
    private int _listTooltipColumn = -1;
    private string _listTooltipText = string.Empty;
    private ListViewHeaderTooltipHook _listHeaderTooltipHook;
    private SortOrder _sortOrder = SortOrder.Ascending;
    private bool _suppressTreeEvents;
    private bool _suppressTemplateEvents;
    private bool _loading;
    private bool _stopRequested;

    public VelumAssemblyRegistryForm(ISwApplication swApp)
    {
      _swApp = swApp;
      InitializeComponent();
      VelumFormHelp.Bind(this, VelumHelpTopics.AssemblyRegistry);
    }

    public VelumAssemblyRegistryForm()
      : this(null)
    {
    }

    protected override void OnLoad(EventArgs e)
    {
      base.OnLoad(e);
      InitializeRuntime();
      RebuildFromSolidWorks();
      // После хаба экспорта и шаблона столбцов — пересчитать высоту полосы фильтров.
      if (_activeColumns != null && _activeColumns.Count > 0)
        BuildFilterEditors();

      // Разрешить экспорт DXF/PDF только для администратора.
      if (IsAdmin)
      {
        if (_btnExportDxf != null) _btnExportDxf.Enabled = true;
        if (_btnExportPdf != null) _btnExportPdf.Enabled = true;
      }
    }

    private bool IsAdmin
    {
      get { return VelumAdminAccess.IsAdmin; }
    }

    private void InitializeRuntime()
    {
      Icon icon = TryLoadFormIcon();
      if (icon != null)
        Icon = icon;

      _toolTip = new ToolTip();
      _toolTip.AutoPopDelay = 12000;
      _toolTip.InitialDelay = 400;
      _toolTip.ReshowDelay = 200;
      _toolTip.ShowAlways = true;
      _toolTip.SetToolTip(_btnTreeSearchPrev, "Предыдущий результат поиска в дереве");
      _toolTip.SetToolTip(_btnTreeSearchNext, "Найти в дереве / следующий результат");
      _toolTip.SetToolTip(_btnTreeSearchClear, "Очистить поиск в дереве");
      _toolTip.SetToolTip(_btnStop, "Прервать операцию");
      _toolTip.SetToolTip(_btnColumnSettings, "Настройки столбцов");
      _toolTip.SetToolTip(_btnReport, "Сформировать HTML-отчёт по текущему списку");
      _toolTip.SetToolTip(_btnReports, "Открыть список сохранённых отчётов");
      _toolTip.SetToolTip(_btnFilterApply, "Применить фильтры списка");
      _toolTip.SetToolTip(_btnFilterReset, "Очистить фильтры списка");
      _toolTip.SetToolTip(_btnFilterHelp, "Справка по маскам фильтра");
      _toolTip.SetToolTip(_btnAssemblyComposition, "Открыть отчет Состав сборки");
      _toolTip.SetToolTip(_btnExportTemplates, "Экспорт шаблонов");
      _toolTip.SetToolTip(_btnImportTemplates, "Импорт шаблонов");
      _toolTip.SetToolTip(check_all_doc, "Показать в отчете все компоненты сборки. Иначе: только те, что в списке");

      check_all_doc.Checked = true;
      KeyPreview = true;

      Image settings = TryLoadMenuBitmap("settings.png");
      if (settings != null)
      {
        _btnColumnSettings.Image = settings;
        _btnColumnSettings.ImageAlign = ContentAlignment.MiddleCenter;
        _btnColumnSettings.Text = string.Empty;
      }
      else
        _btnColumnSettings.Text = "…";

      LoadColumnTemplatesUi(null);
      SetupTreeImages();
      ApplySearchButtonIcons();
      SetupContextMenus();

      _folderTreeView.LabelEdit = IsAdmin;
      _folderTreeView.AllowDrop = IsAdmin;
      _folderTreeView.HideSelection = false;
      _folderTreeView.AfterSelect += OnTreeAfterSelect;
      _folderTreeView.AfterExpand += (s, e) => ApplyFolderNodeImage(e.Node);
      _folderTreeView.AfterCollapse += (s, e) => ApplyFolderNodeImage(e.Node);
      _folderTreeView.AfterLabelEdit += OnFolderAfterLabelEdit;
      _folderTreeView.ItemDrag += OnTreeItemDrag;
      _folderTreeView.DragEnter += OnTreeDragEnter;
      _folderTreeView.DragOver += OnTreeDragOver;
      _folderTreeView.DragDrop += OnTreeDragDrop;
      _folderTreeView.MouseDown += OnTreeMouseDown;
      _folderTreeView.MouseUp += OnTreeMouseUp;
      _folderTreeView.KeyDown += OnFolderTreeKeyDown;
      _listView.ColumnClick += OnListColumnClick;
      _listView.ItemDrag += OnListItemDrag;
      _listView.MouseMove += OnListMouseMove;
      _listView.MouseLeave += (s, e) => ClearListColumnTooltip();
      _listView.HandleCreated += (s, e) => AttachListHeaderTooltipHook();
      _listView.HandleDestroyed += (s, e) => ReleaseListHeaderTooltipHook();
      _listView.DoubleClick += (s, e) => OpenSelectedListItems();
      if (_listView.IsHandleCreated)
      AttachListHeaderTooltipHook();

      _btnFilterApply.Click += (s, e) => ApplyFiltersFromUi();
      _btnFilterReset.Click += (s, e) => ResetFilters();
      _btnFilterHelp.Click += (s, e) => VelumListFilterHelper.ShowHelp(this);
      _btnAssemblyComposition.Click += (s, e) => ExportAssemblyComposition();
      _btnStop.Click += (s, e) => { if (!_loading) RebuildFromSolidWorks(); else _stopRequested = true; };
      _btnColumnSettings.Click += (s, e) => OpenColumnSettings();
      _btnReport.Click += (s, e) => ExportHtmlReport();
      _btnReports.Click += (s, e) => OpenExistingReports();
      _btnExportTemplates.Click += (s, e) => ExportTemplates();
      _btnImportTemplates.Click += (s, e) => ImportTemplates();
      _cmbTemplate.SelectedIndexChanged += OnTemplateComboChanged;
      _treeSearchBox.KeyDown += OnTreeSearchKeyDown;
      _btnTreeSearchPrev.Click += (s, e) => ShowPreviousTreeResult();
      _btnTreeSearchNext.Click += (s, e) =>
      {
        if (_treeSearchResults.Count == 0) SearchTree(); else ShowNextTreeResult();
      };
      _btnTreeSearchClear.Click += (s, e) => ClearTreeSearch(false);

      InitializeExportHubUi();
      if (_btnExportDxf != null)
        _toolTip.SetToolTip(_btnExportDxf, "Экспорт DXF по позициям текущего списка");
      if (_btnExportPdf != null)
        _toolTip.SetToolTip(_btnExportPdf, "Экспорт PDF по позициям текущего списка");
    }

    private void LoadColumnTemplatesUi(string preferName)
    {
      VelumAssemblyRegistryColumnSettingsFile data = VelumAssemblyRegistryColumnStore.LoadOrCreate();
      string select = preferName;
      if (string.IsNullOrWhiteSpace(select))
        select = VelumAssemblyRegistryColumnStore.ResolveActiveTemplateName(data);

      _suppressTemplateEvents = true;
      try
      {
        _cmbTemplate.Items.Clear();
        foreach (string name in data.TemplateNames)
          _cmbTemplate.Items.Add(name);

        int index = -1;
        for (int i = 0; i < _cmbTemplate.Items.Count; i++)
        {
          if (string.Equals(Convert.ToString(_cmbTemplate.Items[i]), select, StringComparison.OrdinalIgnoreCase))
          {
            index = i;
            break;
          }
        }

        if (index < 0 && _cmbTemplate.Items.Count > 0)
          index = 0;
        if (index >= 0)
          _cmbTemplate.SelectedIndex = index;
      }
      finally
      {
        _suppressTemplateEvents = false;
      }

      ApplySelectedTemplate(false);
    }

    private void OnTemplateComboChanged(object sender, EventArgs e)
    {
      if (_suppressTemplateEvents)
        return;
      ApplySelectedTemplate(true);
    }

    private void ApplySelectedTemplate(bool persistLast)
    {
      string name = Convert.ToString(_cmbTemplate.SelectedItem);
      VelumAssemblyRegistryColumnSettingsFile data = VelumAssemblyRegistryColumnStore.LoadOrCreate();
      VelumAssemblyRegistryColumnTemplate template =
          VelumAssemblyRegistryColumnStore.FindTemplate(data, name);
      if (template == null && data.Templates.Count > 0)
        template = data.Templates[0];

      _activeColumns = template == null
          ? new List<VelumAssemblyRegistryColumnDef>()
          : VelumAssemblyRegistryColumnStore.GetOrderedColumns(template);

      if (persistLast && !string.IsNullOrWhiteSpace(name))
        VelumAssemblyRegistryColumnStore.SetLastTemplateName(name);

      ApplyTemplateSortDefaults();
      RebuildListColumnsAndFilters();
      BindList();
    }

    private void ApplyTemplateSortDefaults()
    {
      int sortIndex = VelumAssemblyRegistryColumnStore.IndexOfSortColumn(_activeColumns);
      if (sortIndex < 0)
      {
        _sortColumn = 0;
        _sortOrder = SortOrder.Ascending;
        return;
      }

      _sortColumn = sortIndex;
      VelumAssemblyRegistryColumnDef sortCol = _activeColumns[sortIndex];
      _sortOrder = sortCol.SortKind == VelumAssemblyRegistryColumnSortKind.Descending
          ? SortOrder.Descending
          : SortOrder.Ascending;
    }

    private void OpenExistingReports()
    {
      using (var form = new VelumAssemblyRegistryReportsForm())
        form.ShowDialog(this);
    }

    private void OpenColumnSettings()
    {
      var components = new List<VelumAssemblyRegistryComponent>(_graph.Components.Values);
      string selected = Convert.ToString(_cmbTemplate.SelectedItem);
      using (var form = new VelumAssemblyRegistryColumnSettingsForm(selected, components, IsAdmin))
      {
        if (form.ShowDialog(this) != DialogResult.OK)
          return;
        LoadColumnTemplatesUi(form.AppliedTemplateName ?? selected);
      }
    }

    private void ExportTemplates()
    {
      string src = VelumAssemblyRegistryColumnStore.FilePath;
      if (!File.Exists(src))
      {
        ShowMessage("Файл шаблонов не найден.", MessageBoxIcon.Information);
        return;
      }

      if (!VelumFolderBrowser.TrySelect(this, "Выберите каталог для сохранения файла шаблонов",
          Path.GetDirectoryName(src), out string destDir))
        return;

      string dest = Path.Combine(destDir, Path.GetFileName(src));
      try
      {
        File.Copy(src, dest, true);
        ShowMessage("Шаблоны экспортированы:\n" + dest, MessageBoxIcon.Information);
      }
      catch (Exception ex)
      {
        ShowMessage("Не удалось сохранить:\n" + ex.Message, MessageBoxIcon.Error);
      }
    }

    private void ImportTemplates()
    {
      if (!IsAdmin)
      {
        ShowMessage("Импорт доступен только администратору.", MessageBoxIcon.Information);
        return;
      }

      using (var ofd = new OpenFileDialog())
      {
        ofd.Filter = "Файлы шаблонов|columnTemplates.json|Все файлы|*.*";
        ofd.Title = "Импорт шаблонов столбцов";
        if (ofd.ShowDialog(this) != DialogResult.OK)
          return;

        string src = ofd.FileName;
        if (!File.Exists(src))
        {
          ShowMessage("Файл не найден.", MessageBoxIcon.Error);
          return;
        }

        // Попытка десериализации для валидации
        VelumAssemblyRegistryColumnSettingsFile imported;
        try
        {
          string json = File.ReadAllText(src);
          imported = JsonConvert.DeserializeObject<VelumAssemblyRegistryColumnSettingsFile>(
              json,
              new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore })
              ?? new VelumAssemblyRegistryColumnSettingsFile();
        }
        catch (Exception ex)
        {
          ShowMessage("Файл не является корректным файлом шаблонов:\n" + ex.Message, MessageBoxIcon.Error);
          return;
        }

        if (imported.Templates == null || imported.Templates.Count == 0)
        {
          ShowMessage("Импортируемый файл не содержит шаблонов.", MessageBoxIcon.Information);
          return;
        }

        // Загружаем текущие данные
        VelumAssemblyRegistryColumnSettingsFile current = VelumAssemblyRegistryColumnStore.LoadOrCreate();
        if (current.Templates == null)
          current.Templates = new List<VelumAssemblyRegistryColumnTemplate>();
        if (current.TemplateNames == null)
          current.TemplateNames = new List<string>();

        // Определяем, какие шаблоны новые, какие с дублями
        var existingNames = new HashSet<string>(current.TemplateNames ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        var newTemplates = new List<VelumAssemblyRegistryColumnTemplate>();
        var replaceCandidates = new List<VelumAssemblyRegistryColumnTemplate>();
        var skipCandidates = new List<VelumAssemblyRegistryColumnTemplate>();

        foreach (VelumAssemblyRegistryColumnTemplate tmpl in imported.Templates)
        {
          if (tmpl == null || string.IsNullOrWhiteSpace(tmpl.Name))
            continue;

          string name = tmpl.Name.Trim();
          if (existingNames.Contains(name))
          {
            replaceCandidates.Add(tmpl);
          }
          else
          {
            newTemplates.Add(tmpl);
          }
        }

        if (replaceCandidates.Count > 0)
        {
          var sb = new System.Text.StringBuilder();
          sb.AppendLine("В импортируемом файле найдены шаблоны с именами, которые уже существуют:");
          foreach (var t in replaceCandidates)
            sb.AppendLine("  - " + t.Name);
          sb.AppendLine();
          sb.AppendLine("Выберите действие:");
          sb.AppendLine("Да — заменить существующие шаблоны на новые");
          sb.AppendLine("Нет — оставить текущие, пропустить дубликаты");

          var result = MessageBox.Show(
              this,
              sb.ToString(),
              "Дубликаты шаблонов",
              MessageBoxButtons.YesNoCancel,
              MessageBoxIcon.Warning);

          if (result == DialogResult.Cancel)
            return;

          if (result == DialogResult.Yes)
          {
            foreach (var tmpl in replaceCandidates)
            {
              current.Templates.RemoveAll(t => string.Equals(t.Name, tmpl.Name, StringComparison.OrdinalIgnoreCase));
              current.TemplateNames.RemoveAll(n => string.Equals(n, tmpl.Name, StringComparison.OrdinalIgnoreCase));
              current.Templates.Add(tmpl);
              current.TemplateNames.Add(tmpl.Name);
            }
          }
          else
          {
            // No — skip duplicates
            foreach (var tmpl in replaceCandidates)
              skipCandidates.Add(tmpl);
          }
        }

        // Добавляем новые
        foreach (var tmpl in newTemplates)
        {
          current.Templates.Add(tmpl);
          current.TemplateNames.Add(tmpl.Name);
        }

        // Собираем итоговую статистику
        var sb2 = new System.Text.StringBuilder();
        sb2.AppendLine("Импорт завершён.");
        sb2.AppendLine("Добавлено новых: " + newTemplates.Count);
        int replaced = replaceCandidates.Count - skipCandidates.Count;
        sb2.AppendLine("Заменено: " + replaced);
        sb2.AppendLine("Пропущено (дубликаты): " + skipCandidates.Count);

        // Пересобираем TemplateNames без дублей
        current.TemplateNames = current.Templates
            .Select(t => (t.Name ?? string.Empty).Trim())
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        try
        {
          VelumAssemblyRegistryColumnStore.Save(current);
        }
        catch (Exception ex)
        {
          ShowMessage("Не удалось сохранить:\n" + ex.Message, MessageBoxIcon.Error);
          return;
        }

        LoadColumnTemplatesUi(null);
        ShowMessage(sb2.ToString(), MessageBoxIcon.Information);
      }
    }

    private void ExportHtmlReport()
    {
      if (_loading)
        return;

      if (_activeColumns == null || _activeColumns.Count == 0)
      {
        ShowMessage("Нет столбцов в текущем шаблоне списка.", MessageBoxIcon.Information);
        return;
      }

      string templateName = Convert.ToString(_cmbTemplate.SelectedItem);
      if (string.IsNullOrWhiteSpace(templateName))
        templateName = VelumAssemblyRegistryColumnStore.DefaultTemplateName;

      List<VelumAssemblyRegistryComponent> rows = ApplyItemFilters(ResolveRowsForSelection());
      SortItems(rows);

      int productQty = ReadProductQuantity();
      string html = VelumAssemblyRegistryReportHtmlBuilder.BuildHtml(
          templateName,
          _graph.RootAssemblyPath ?? string.Empty,
          _graph.RootConfigurationName ?? string.Empty,
          BuildReportSelectionLabel(),
          _activeColumns,
          rows,
          productQty);

      string folder = VelumAssemblyRegistryReportHtmlBuilder.ReportsFolderPath;
      string path;
      try
      {
        Directory.CreateDirectory(folder);
        path = Path.Combine(folder, VelumAssemblyRegistryReportHtmlBuilder.BuildFileName(templateName, DateTime.Now));
        File.WriteAllText(path, html, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
      }
      catch (Exception ex)
      {
        ShowMessage("Не удалось сохранить отчёт:\n" + ex.Message, MessageBoxIcon.Error);
        return;
      }

      DialogResult open = MessageBox.Show(
          this,
          "Отчёт сохранён:\n" + path + "\n\nОткрыть отчёт в браузере?",
          "Реестр изделия",
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
        ShowMessage("Не удалось открыть отчёт:\n" + ex.Message, MessageBoxIcon.Warning);
      }
    }

    private void ExportAssemblyComposition()
    {
      if (_loading)
        return;

      if (_graph == null || _graph.Components.Count == 0)
      {
        ShowMessage("Нет данных о составе сборки.", MessageBoxIcon.Information);
        return;
      }

      string html;

      if (check_all_doc.Checked)
      {
        html = VelumAssemblyRegistryReportHtmlBuilder.BuildAssemblyCompositionHtml(_graph, _activeColumns);
      }
      else
      {
        List<VelumAssemblyRegistryComponent> rows = ResolveRowsForSelection();
        html = VelumAssemblyRegistryReportHtmlBuilder.BuildAssemblyCompositionHtml(_graph, rows, _activeColumns);
      }

      string folder = VelumAssemblyRegistryReportHtmlBuilder.ReportsFolderPath;
      string path;
      try
      {
        Directory.CreateDirectory(folder);
        string fileName = "Состав_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".html";
        path = Path.Combine(folder, fileName);
        File.WriteAllText(path, html, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
      }
      catch (Exception ex)
      {
        ShowMessage("Не удалось сохранить отчёт:\n" + ex.Message, MessageBoxIcon.Error);
        return;
      }

      DialogResult open = MessageBox.Show(
          this,
          "Отчёт сохранён:\n" + path + "\n\nОткрыть отчёт в браузере?",
          "Состав сборки",
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
        ShowMessage("Не удалось открыть отчёт:\n" + ex.Message, MessageBoxIcon.Warning);
      }
    }

    private string BuildReportSelectionLabel()
    {
      var text = new StringBuilder();
      if (_treeMultiSelected.Count > 1)
      {
        text.Append("Выбрано узлов: ").Append(_treeMultiSelected.Count);
      }
      else
      {
        TreeNode selected = _folderTreeView.SelectedNode;
        VelumAssemblyRegistryTreeTag tag = selected == null ? null : selected.Tag as VelumAssemblyRegistryTreeTag;
        if (tag == null)
          text.Append("Не выбран узел состава");
        else if (tag.Kind == VelumAssemblyRegistryTreeTag.TagKind.Folder && tag.FolderPath != null)
          text.Append("Каталог: ").Append(tag.FolderPath.ToDisplayPath());
        else if (tag.Kind == VelumAssemblyRegistryTreeTag.TagKind.Component)
          text.Append("Компонент: ").Append(selected.Text ?? tag.DocumentKey ?? string.Empty);
        else
          text.Append(selected.Text ?? "Узел состава");
      }

      int filterCount = 0;
      if (_activeFilters != null)
      {
        for (int i = 0; i < _activeFilters.Length; i++)
        {
          if (!string.IsNullOrWhiteSpace(_activeFilters[i]))
            filterCount++;
        }
      }

      if (filterCount > 0)
        text.Append("; фильтров: ").Append(filterCount);

      return text.ToString();
    }

    private void RebuildListColumnsAndFilters()
    {
      _listView.BeginUpdate();
      try
      {
        _listView.Items.Clear();
        _listView.Columns.Clear();

        // Собираем отображаемые колонки и маппинг видимых индексов в реальные.
        var visibleToActual = new List<int>();
        for (int i = 0; i < _activeColumns.Count; i++)
        {
          VelumAssemblyRegistryColumnDef col = _activeColumns[i];
          if (col != null && col.ShowInList)
            visibleToActual.Add(i);
        }
        _visibleToActualIndex = visibleToActual.ToArray();

        foreach (int actualIndex in _visibleToActualIndex)
        {
          VelumAssemblyRegistryColumnDef col = _activeColumns[actualIndex];
          string caption = VelumAssemblyRegistryColumnStore.CaptionFromName(col.Name);
          if (string.IsNullOrEmpty(caption))
            caption = " ";
          ColumnHeader header = _listView.Columns.Add(
              caption,
              Math.Max(60, Math.Min(160, caption.Length * 9)));
          header.Tag = col.Description ?? string.Empty;
        }
      }
      finally
      {
        _listView.EndUpdate();
      }

      ClearListColumnTooltip();
      AttachListHeaderTooltipHook();
      BuildFilterEditors();
      BuildTotalsEditors();
    }

    private void BuildTotalsEditors()
    {
      _totalsHost.Controls.Clear();
      var totalsColumns = new List<VelumAssemblyRegistryColumnDef>();
      for (int v = 0; v < _visibleToActualIndex.Length; v++)
      {
        int actualIndex = _visibleToActualIndex[v];
        VelumAssemblyRegistryColumnDef col = _activeColumns[actualIndex];
        if (col != null && col.TotalsKind != VelumAssemblyRegistryColumnTotalsKind.None)
          totalsColumns.Add(col);
      }

      // После InitializeExportHubUi: [2]=фильтры, [5]=список %, [6]=итоги.
      const int totalsRow = 6;
      if (totalsColumns.Count == 0)
      {
        _totalsHost.Visible = false;
        if (_listPanel.RowStyles.Count > totalsRow)
        {
          _listPanel.RowStyles[totalsRow].SizeType = SizeType.Absolute;
          _listPanel.RowStyles[totalsRow].Height = 0F;
        }
        return;
      }

      int rowCount = (totalsColumns.Count + 1) / 2;
      float height = Math.Min(120, Math.Max(36, rowCount * 26 + 8));
      if (_listPanel.RowStyles.Count > totalsRow)
      {
        _listPanel.RowStyles[totalsRow].SizeType = SizeType.Absolute;
        _listPanel.RowStyles[totalsRow].Height = height;
      }
      _totalsHost.Visible = true;

      var layout = new TableLayoutPanel
      {
        ColumnCount = 4,
        RowCount = Math.Max(1, rowCount),
        Dock = DockStyle.Fill,
        AutoSize = true
      };
      for (int c = 0; c < 4; c++)
        layout.ColumnStyles.Add(new ColumnStyle(c % 2 == 0 ? SizeType.AutoSize : SizeType.Percent, c % 2 == 0 ? 0 : 50F));

      for (int i = 0; i < totalsColumns.Count; i++)
      {
        VelumAssemblyRegistryColumnDef col = totalsColumns[i];
        int row = i / 2, colIndex = (i % 2) * 2;
        string caption = VelumAssemblyRegistryColumnStore.CaptionFromName(col.Name);
        string kind = VelumAssemblyRegistryColumnTotals.TotalsKindDisplayName(col.TotalsKind);
        var label = new Label
        {
          AutoSize = true,
          Anchor = AnchorStyles.Left,
          Text = caption + " (" + kind + "):",
          Margin = new Padding(3, 4, 3, 0),
          Font = new Font(Font.FontFamily, 8f)
        };
        var box = new TextBox
        {
          Dock = DockStyle.Fill,
          ReadOnly = true,
          Margin = new Padding(3, 2, 12, 2),
          BackColor = Color.FromArgb(255, 248, 225),
          TabStop = false,
          Tag = col,
          Font = new Font(Font.FontFamily, 8f)
        };
        layout.Controls.Add(label, colIndex, row);
        layout.Controls.Add(box, colIndex + 1, row);
      }

      _totalsHost.Controls.Add(layout);
    }

    private void UpdateTotalsValues(List<VelumAssemblyRegistryComponent> rows)
    {
      if (_totalsHost.Controls.Count == 0)
        return;

      TableLayoutPanel layout = _totalsHost.Controls[0] as TableLayoutPanel;
      if (layout == null)
        return;

      foreach (Control control in layout.Controls)
      {
        var box = control as TextBox;
        if (box == null)
          continue;
        var col = box.Tag as VelumAssemblyRegistryColumnDef;
        box.Text = VelumAssemblyRegistryColumnTotals.ComputeDisplay(col, rows, ReadProductQuantity()) ?? string.Empty;
      }
    }

    private void SetupContextMenus()
    {
      if (IsAdmin)
      {
        _treeMenu = new ContextMenuStrip();
        AddMenuItem(_treeMenu, "Добавить каталог", "Add.png", Keys.Insert, (s, e) => AddFolder());
        AddMenuItem(_treeMenu, "Переименовать", "Modify.png", Keys.F2, (s, e) => BeginRenameFolder());
        AddMenuItem(_treeMenu, "Удалить", "Delete.png", Keys.Delete, (s, e) => DeleteSelectedFolder());
        _treeMenu.Opening += OnTreeMenuOpening;
        _folderTreeView.ContextMenuStrip = _treeMenu;
      }

      _listMenu = new ContextMenuStrip();
      _menuListOpen = AddMenuItem(_listMenu, "Открыть", "Yes.png", Keys.None, (s, e) => OpenSelectedListItems());
      _menuListPrintDrawings = AddMenuItem(
          _listMenu,
          "Печать чертежей",
          "Print.png",
          Keys.None,
          (s, e) => PrintSelectedDrawings());
      _listCellFilterMenu = new VelumListViewCellFilterMenu(
          _listView,
          this,
          column => column >= 0 && column < _filterBoxes.Length ? _filterBoxes[column] : null,
          ApplyFiltersFromUi,
          () => !_loading,
          null);
      _listCellFilterMenu.AppendTo(
          _listMenu,
          TryLoadMenuBitmap("Thumbs up.png"),
          TryLoadMenuBitmap("Thumbs down.png"));
      _listMenu.Items.Add(new ToolStripSeparator());
      _menuListSelectAll = AddMenuItem(
          _listMenu,
          "Выделить все",
          "editselectall.png",
          Keys.None,
          (s, e) => SelectAllListItems());
      if (IsAdmin)
      {
        _listMenu.Items.Add(new ToolStripSeparator());
        _menuListChangeCatalog = AddMenuItem(
            _listMenu,
            "Изменить каталог",
            "Folder.png",
            Keys.None,
            (s, e) => ChangeSelectedCatalog());
        _menuListChangeMaterial = AddMenuItem(
            _listMenu,
            "Сменить материал",
            "Database.ico",
            Keys.None,
            (s, e) => ChangeSelectedMaterial());
      }
      _listMenu.Opening += OnListMenuOpening;
      _listView.ContextMenuStrip = _listMenu;
    }

    private static ToolStripMenuItem AddMenuItem(
        ContextMenuStrip menu,
        string text,
        string icon,
        Keys shortcut,
        EventHandler click)
    {
      var item = new ToolStripMenuItem(text, TryLoadMenuBitmap(icon));
      item.Click += click;
      if (shortcut != Keys.None)
      {
        item.ShortcutKeys = shortcut;
        item.ShowShortcutKeys = true;
      }

      menu.Items.Add(item);
      return item;
    }

    private void OnTreeMenuOpening(object sender, System.ComponentModel.CancelEventArgs e)
    {
      TreeNode node = _folderTreeView.GetNodeAt(_folderTreeView.PointToClient(Cursor.Position));
      if (node != null) _folderTreeView.SelectedNode = node;
      VelumAssemblyRegistrySectionPath path = GetFolderPath(_folderTreeView.SelectedNode);
      bool folder = path != null;
      bool nested = folder && !path.IsRoot;
      _treeMenu.Items[0].Enabled = folder && !_loading;
      _treeMenu.Items[1].Enabled = nested && !_loading;
      _treeMenu.Items[2].Enabled = nested && !_loading;
    }

    private void OnListMenuOpening(object sender, System.ComponentModel.CancelEventArgs e)
    {
      bool selected = _listView.SelectedItems.Count > 0;
      bool hasRows = _listView.Items.Count > 0;
      bool idle = !_loading;
      if (_menuListOpen != null)
        _menuListOpen.Enabled = selected && idle;
      if (_menuListPrintDrawings != null)
        _menuListPrintDrawings.Enabled = selected && idle;
      if (_menuListSelectAll != null)
        _menuListSelectAll.Enabled = hasRows && idle;
      if (_menuListChangeCatalog != null)
        _menuListChangeCatalog.Enabled = selected && idle;
      if (_menuListChangeMaterial != null)
        _menuListChangeMaterial.Enabled = selected && idle;
    }

    /// <summary>Сопоставление видимого индекса столбца (позиция в панели фильтров) с реальным индексом в _activeColumns.</summary>
    private int[] _visibleToActualFilterIndex = Array.Empty<int>();

    private void BuildFilterEditors()
    {
      _filtersHost.Controls.Clear();

      // Строим маппинг видимых индексов на реальные (только ShowInList == true).
      _visibleToActualFilterIndex = Enumerable.Range(0, _activeColumns.Count)
          .Where(i => _activeColumns[i] != null && _activeColumns[i].ShowInList)
          .ToArray();

      int count = _visibleToActualFilterIndex.Length;
      _filterBoxes = new TextBox[count];
      _activeFilters = new string[_activeColumns.Count];
      for (int i = 0; i < _activeColumns.Count; i++)
        _activeFilters[i] = string.Empty;

      // После InitializeExportHubUi: [1]=hub, [2]=фильтры Absolute.
      const int filtersRow = 2;
      if (count == 0)
      {
        if (_listPanel.RowStyles.Count > filtersRow)
        {
          _listPanel.RowStyles[filtersRow].SizeType = SizeType.Absolute;
          _listPanel.RowStyles[filtersRow].Height = 24F;
        }
        return;
      }

      int rowCount = (count + 1) / 2;
      if (_listPanel.RowStyles.Count > filtersRow)
      {
        _listPanel.RowStyles[filtersRow].SizeType = SizeType.Absolute;
        _listPanel.RowStyles[filtersRow].Height = Math.Min(220, Math.Max(56, rowCount * 28 + 12));
      }

      var layout = new TableLayoutPanel
      {
        ColumnCount = 4,
        RowCount = Math.Max(1, rowCount),
        Dock = DockStyle.Fill,
        AutoSize = true
      };
      for (int c = 0; c < 4; c++)
        layout.ColumnStyles.Add(new ColumnStyle(c % 2 == 0 ? SizeType.AutoSize : SizeType.Percent, c % 2 == 0 ? 0 : 50F));

      for (int v = 0; v < count; v++)
      {
        int actualIndex = _visibleToActualFilterIndex[v];
        VelumAssemblyRegistryColumnDef col = _activeColumns[actualIndex];
        string caption = VelumAssemblyRegistryColumnStore.CaptionFromName(col.Name);
        string tip = col.Description ?? string.Empty;
        var label = new Label
        {
          AutoSize = true,
          Anchor = AnchorStyles.Left,
          Text = caption + ":",
          Margin = new Padding(3, 6, 3, 0)
        };
        var box = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(3, 3, 12, 3) };
        if (!string.IsNullOrEmpty(tip))
        {
          _toolTip.SetToolTip(label, tip);
          _toolTip.SetToolTip(box, tip);
        }

        box.KeyDown += (s, e) =>
        {
          if (e.KeyCode != Keys.Enter) return;
          ApplyFiltersFromUi();
          e.Handled = true;
          e.SuppressKeyPress = true;
        };
        _filterBoxes[v] = box;
        layout.Controls.Add(label, (v % 2) * 2, v / 2);
        layout.Controls.Add(box, (v % 2) * 2 + 1, v / 2);
      }

      _filtersHost.Controls.Add(layout);

      for (int v = 0; v < count; v++)
      {
        int actualIndex = _visibleToActualFilterIndex[v];
        string filter = _activeColumns[actualIndex].Filter ?? string.Empty;
        if (_filterBoxes[v] != null)
          _filterBoxes[v].Text = filter;
        _activeFilters[actualIndex] = filter.Trim();
      }

      // Инициализируем фильтры из шаблона для скрытых столбцов — чтобы фильтрация по ним работала.
      for (int i = 0; i < _activeColumns.Count; i++)
      {
        if (_activeColumns[i] != null && !_activeColumns[i].ShowInList)
          _activeFilters[i] = (_activeColumns[i].Filter ?? string.Empty).Trim();
      }
    }

    private void OnListMouseMove(object sender, MouseEventArgs e)
    {
      // Заголовки обслуживает hook SysHeader32 — здесь только область строк.
      if (e.Y < GetListHeaderHeight())
        return;

      ShowListColumnTooltip(ResolveListColumnAt(e.Location), fromHeader: false);
    }

    private void OnListHeaderMouseMove(int clientX)
    {
      ShowListColumnTooltip(ResolveListColumnByX(clientX), fromHeader: true);
    }

    private void ShowListColumnTooltip(int column, bool fromHeader)
    {
      string tip = GetColumnDescriptionTooltip(column);
      if (column == _listTooltipColumn &&
          string.Equals(tip, _listTooltipText, StringComparison.Ordinal))
        return;

      _listTooltipColumn = column;
      _listTooltipText = tip ?? string.Empty;

      if (string.IsNullOrWhiteSpace(tip))
      {
        _toolTip.Hide(_listView);
        return;
      }

      if (fromHeader)
      {
        // SetToolTip не срабатывает над дочерним хедером ListView — показываем явно.
        Point client = _listView.PointToClient(Control.MousePosition);
        int y = Math.Max(GetListHeaderHeight() + 2, client.Y + 18);
        _toolTip.Show(tip, _listView, client.X, y, Math.Max(1000, _toolTip.AutoPopDelay));
      }
      else
      {
        // Показываем tooltip явно через Show, чтобы не трогать SetToolTip на ListView —
        // это мешает tooltip на кнопках формы (ToolTip — один на всю форму).
        Point client = _listView.PointToClient(Control.MousePosition);
        _toolTip.Show(tip, _listView, client.X, client.Y + 20, Math.Max(1000, _toolTip.AutoPopDelay));
      }
    }

    private string GetColumnDescriptionTooltip(int column)
    {
      if (column < 0)
        return string.Empty;

      if (column < _visibleToActualIndex.Length)
      {
        int actualIndex = _visibleToActualIndex[column];
        if (actualIndex >= 0 && actualIndex < _activeColumns.Count)
        {
          string fromHeader = Convert.ToString(_listView.Columns[column].Tag);
          if (!string.IsNullOrWhiteSpace(fromHeader))
            return fromHeader.Trim();
          return (_activeColumns[actualIndex].Description ?? string.Empty).Trim();
        }
      }

      return string.Empty;
    }

    private void ClearListColumnTooltip()
    {
      _listTooltipColumn = -1;
      _listTooltipText = string.Empty;
      if (_toolTip != null && _listView != null && !_listView.IsDisposed)
        _toolTip.Hide(_listView);
    }

    private int ResolveListColumnAt(Point point)
    {
      if (_listView.Columns.Count == 0)
        return -1;

      ListViewHitTestInfo hit = _listView.HitTest(point);
      if (hit.Item != null && hit.SubItem != null)
      {
        for (int i = 0; i < hit.Item.SubItems.Count && i < _listView.Columns.Count; i++)
        {
          if (ReferenceEquals(hit.Item.SubItems[i], hit.SubItem))
            return i;
        }
      }

      return ResolveListColumnByX(point.X);
    }

    private int ResolveListColumnByX(int clientX)
    {
      if (_listView.Columns.Count == 0)
        return -1;

      int x = clientX;
      try
      {
        const int SB_HORZ = 0;
        x += GetScrollPos(_listView.Handle, SB_HORZ);
      }
      catch
      {
      }

      int edge = 0;
      for (int i = 0; i < _listView.Columns.Count; i++)
      {
        edge += _listView.Columns[i].Width;
        if (x < edge)
          return i;
      }

      return -1;
    }

    private int GetListHeaderHeight()
    {
      if (!_listView.IsHandleCreated)
        return 0;

      IntPtr header = SendMessage(_listView.Handle, LvmGetHeader, IntPtr.Zero, IntPtr.Zero);
      if (header == IntPtr.Zero)
        return 0;

      RECT rect;
      if (!GetClientRect(header, out rect))
        return 0;
      return Math.Max(0, rect.Bottom - rect.Top);
    }

    private void AttachListHeaderTooltipHook()
    {
      if (_listView == null || !_listView.IsHandleCreated || _listView.IsDisposed)
        return;

      IntPtr header = SendMessage(_listView.Handle, LvmGetHeader, IntPtr.Zero, IntPtr.Zero);
      if (header == IntPtr.Zero)
        return;

      if (_listHeaderTooltipHook == null)
        _listHeaderTooltipHook = new ListViewHeaderTooltipHook(this);

      if (_listHeaderTooltipHook.Handle != header)
      {
        if (_listHeaderTooltipHook.Handle != IntPtr.Zero)
          _listHeaderTooltipHook.ReleaseHandle();
        _listHeaderTooltipHook.AssignHandle(header);
      }
    }

    private void ReleaseListHeaderTooltipHook()
    {
      if (_listHeaderTooltipHook == null)
        return;
      if (_listHeaderTooltipHook.Handle != IntPtr.Zero)
        _listHeaderTooltipHook.ReleaseHandle();
    }

    private const int LvmGetHeader = 0x101F;
    private const int WmMouseMove = 0x0200;
    private const int WmMouseLeave = 0x02A3;
    private const int TmeLeave = 0x0002;

    [DllImport("user32.dll")]
    private static extern int GetScrollPos(IntPtr hWnd, int nBar);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool TrackMouseEvent(ref TRACKMOUSEEVENT lpEventTrack);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
      public int Left;
      public int Top;
      public int Right;
      public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TRACKMOUSEEVENT
    {
      public int cbSize;
      public int dwFlags;
      public IntPtr hwndTrack;
      public int dwHoverTime;
    }

    /// <summary>MouseMove над заголовками ListView (отдельное окно SysHeader32).</summary>
    private sealed class ListViewHeaderTooltipHook : NativeWindow
    {
      private readonly VelumAssemblyRegistryForm _owner;
      private bool _trackingLeave;

      internal ListViewHeaderTooltipHook(VelumAssemblyRegistryForm owner)
      {
        _owner = owner;
      }

      protected override void WndProc(ref Message m)
      {
        if (m.Msg == WmMouseMove)
        {
          int x = (short)(m.LParam.ToInt32() & 0xFFFF);
          _owner.OnListHeaderMouseMove(x);
          EnsureMouseLeaveTracking();
        }
        else if (m.Msg == WmMouseLeave)
        {
          _trackingLeave = false;
          _owner.ClearListColumnTooltip();
        }

        base.WndProc(ref m);
      }

      private void EnsureMouseLeaveTracking()
      {
        if (_trackingLeave || Handle == IntPtr.Zero)
          return;

        var tme = new TRACKMOUSEEVENT
        {
          cbSize = Marshal.SizeOf(typeof(TRACKMOUSEEVENT)),
          dwFlags = TmeLeave,
          hwndTrack = Handle,
          dwHoverTime = 0
        };
        if (TrackMouseEvent(ref tme))
          _trackingLeave = true;
      }
    }

    private void RebuildFromSolidWorks()
    {
      RebuildFromSolidWorks(null, null);
    }

    private void RebuildFromSolidWorks(string selectFolderPath, HashSet<string> expandFolderKeys)
    {
      if (_loading) return;
      HashSet<string> preserveExpand = expandFolderKeys ?? CaptureExpandedFolderKeys();
      string preserveSelect = selectFolderPath;
      if (string.IsNullOrEmpty(preserveSelect))
      {
        VelumAssemblyRegistrySectionPath selectedPath = GetFolderPath(_folderTreeView.SelectedNode);
        if (selectedPath != null)
          preserveSelect = selectedPath.ToStableKey();
      }

      _stopRequested = false;
      _loading = true;
      SetLoadingUi(true);
      ClearSessionData();
      BindList();
      SetProgress(0, 1, "Обновление дерева…");
      try
      {
        var walker = new VelumAssemblyRegistryWalker(_swApp, () => _stopRequested, OnWalkProgress);
        VelumAssemblyRegistryGraph graph;
        string error;
        bool ok = walker.TryBuild(out graph, out error);
        if (_stopRequested)
        {
          ClearSessionData();
          RebuildTreeProjection(preserveSelect, preserveExpand);
          _progressLabel.Text = "Прервано";
          return;
        }
        if (!ok)
        {
          ClearSessionData();
          RebuildTreeProjection(preserveSelect, preserveExpand);
          if (!string.IsNullOrEmpty(error)) ShowMessage(error, MessageBoxIcon.Information);
          _progressLabel.Text = string.Empty;
          return;
        }
        _graph = graph ?? new VelumAssemblyRegistryGraph();
        SetProgress(1, 1, "Построение дерева каталогов…");
        RebuildTreeProjection(preserveSelect, preserveExpand);
        _progressLabel.Text = "Узлов: " + CountUniqueDocuments() + ", конфигураций: " + _graph.Components.Count;
      }
      catch (Exception ex)
      {
        ClearSessionData();
        RebuildTreeProjection(preserveSelect, preserveExpand);
        ShowMessage(ex.Message, MessageBoxIcon.Error);
      }
      finally
      {
        _loading = false;
        SetLoadingUi(false);
        _progressBar.Value = 0;
      }
    }

    private int CountUniqueDocuments()
    {
      var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (VelumAssemblyRegistryComponent item in _graph.Components.Values)
      {
        string key = VelumAssemblyRegistryPropertyReader.NormalizePath(item.FilePath);
        if (!string.IsNullOrEmpty(key))
          keys.Add(key);
      }

      return keys.Count;
    }

    private void ClearSessionData()
    {
      _graph = new VelumAssemblyRegistryGraph();
      _ephemeralFolderKeys.Clear();
      ClearTreeSearch(true);
    }

    private void OnWalkProgress(VelumAssemblyRegistryWalker.WalkProgress progress)
    {
      if (progress == null) return;
      SetProgress(progress.Current, progress.Maximum, progress.Status);
    }

    private void SetProgress(int current, int maximum, string status)
    {
      int max = Math.Max(maximum, 1);
      if (_progressBar.Maximum != max) _progressBar.Maximum = max;
      _progressBar.Value = Math.Max(0, Math.Min(current, _progressBar.Maximum));
      _progressLabel.Text = status ?? string.Empty;
      Application.DoEvents();
    }

    private void SetLoadingUi(bool loading)
    {
      _progressBar.Visible = loading;
      _treePanel.RowStyles[4].Height = loading ? 22F : 0F;
      if (loading)
      {
        _progressBar.Minimum = 0;
        _progressBar.Value = 0;
      }

      _btnStop.Enabled = loading;
      _btnReport.Enabled = !loading;
      _btnReports.Enabled = !loading;
      _btnColumnSettings.Enabled = !loading;
      if (_btnAssemblyComposition != null) _btnAssemblyComposition.Enabled = !loading;
      _folderTreeView.Enabled = !loading;
      _listView.Enabled = !loading;
      _btnFilterApply.Enabled = !loading;
      _btnFilterReset.Enabled = !loading;
      _btnFilterHelp.Enabled = !loading;
      if (_btnExportDxf != null) _btnExportDxf.Enabled = !loading;
      if (_btnExportPdf != null) _btnExportPdf.Enabled = !loading;
      if (_btnExportTemplates != null) _btnExportTemplates.Enabled = !loading;
      if (_btnImportTemplates != null) _btnImportTemplates.Enabled = !loading;
      foreach (TextBox box in _filterBoxes)
        if (box != null) box.Enabled = !loading;
    }

    private void RebuildTreeProjection(string selectFolderPath)
    {
      RebuildTreeProjection(selectFolderPath, null);
    }

    private void RebuildTreeProjection(string selectFolderPath, HashSet<string> expandFolderKeys)
    {
      _suppressTreeEvents = true;
      _folderTreeView.BeginUpdate();
      try
      {
        ClearTreeMultiSelection(updateVisuals: false);
        _folderTreeView.Nodes.Clear();
        ClearTreeSearch(true);
        AddFamilyRoot(VelumAssemblyRegistryFamily.Assembly);
        AddFamilyRoot(VelumAssemblyRegistryFamily.Part);
        AddFamilyRoot(VelumAssemblyRegistryFamily.Standard);
        _folderTreeView.CollapseAll();
        RestoreExpandedFolderKeys(expandFolderKeys);
        TreeNode selected = FindFolderNodeByKey(_folderTreeView.Nodes, selectFolderPath);
        if (selected == null && _folderTreeView.Nodes.Count > 0) selected = _folderTreeView.Nodes[0];
        if (selected != null)
        {
          ExpandParents(selected);
          _folderTreeView.SelectedNode = selected;
          try { selected.EnsureVisible(); }
          catch { /* ignore */ }
        }
      }
      finally
      {
        _folderTreeView.EndUpdate();
        _suppressTreeEvents = false;
      }
      BindList();
    }

    private HashSet<string> CaptureExpandedFolderKeys()
    {
      var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      CollectExpandedFolderKeys(_folderTreeView.Nodes, keys);
      return keys;
    }

    private static void CollectExpandedFolderKeys(TreeNodeCollection nodes, HashSet<string> keys)
    {
      if (nodes == null || keys == null)
        return;
      foreach (TreeNode node in nodes)
      {
        VelumAssemblyRegistrySectionPath path = GetFolderPath(node);
        if (path != null && node.IsExpanded)
          keys.Add(path.ToStableKey());
        CollectExpandedFolderKeys(node.Nodes, keys);
      }
    }

    private void RestoreExpandedFolderKeys(HashSet<string> keys)
    {
      if (keys == null || keys.Count == 0)
        return;
      ExpandFoldersByKeys(_folderTreeView.Nodes, keys);
    }

    private static void ExpandFoldersByKeys(TreeNodeCollection nodes, HashSet<string> keys)
    {
      if (nodes == null || keys == null)
        return;
      foreach (TreeNode node in nodes)
      {
        VelumAssemblyRegistrySectionPath path = GetFolderPath(node);
        if (path != null && keys.Contains(path.ToStableKey()))
          node.Expand();
        ExpandFoldersByKeys(node.Nodes, keys);
      }
    }

    private void AddFamilyRoot(VelumAssemblyRegistryFamily family)
    {
      VelumAssemblyRegistryNodeKind kind = VelumAssemblyRegistrySectionPath.NodeKindFromFamily(family);
      bool hasComponents = false;
      foreach (VelumAssemblyRegistryComponent item in _graph.Components.Values)
        if (item.Kind == kind) { hasComponents = true; break; }
      if (!hasComponents && !HasEphemeralFamily(family)) return;

      var rootPath = new VelumAssemblyRegistrySectionPath(family, Array.Empty<string>());
      TreeNode root = CreateFolderNode(rootPath.Prefix, rootPath);
      var placedDocuments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (VelumAssemblyRegistryComponent item in SortByTitle(CollectByKind(kind)))
      {
        string documentKey = VelumAssemblyRegistryPropertyReader.NormalizePath(item.FilePath);
        if (string.IsNullOrEmpty(documentKey) || !placedDocuments.Add(documentKey))
          continue;
        EnsureFolder(root, item.FolderSegments).Nodes.Add(CreateComponentNode(item));
      }

      foreach (VelumAssemblyRegistrySectionPath path in GetEphemeralPaths(family))
        EnsureFolder(root, path.Segments);
      _folderTreeView.Nodes.Add(root);
    }

    private bool HasEphemeralFamily(VelumAssemblyRegistryFamily family)
    {
      foreach (VelumAssemblyRegistrySectionPath path in GetEphemeralPaths(family))
        if (path != null) return true;
      return false;
    }

    private IEnumerable<VelumAssemblyRegistrySectionPath> GetEphemeralPaths(VelumAssemblyRegistryFamily family)
    {
      foreach (string key in _ephemeralFolderKeys)
      {
        VelumAssemblyRegistrySectionPath path;
        if (TryPathFromKey(key, out path) && path.Family == family) yield return path;
      }
    }

    private static bool TryPathFromKey(string key, out VelumAssemblyRegistrySectionPath path)
    {
      path = null;
      if (string.IsNullOrEmpty(key)) return false;
      string[] pieces = key.Split('|');
      int rawFamily;
      if (pieces.Length == 0 || !int.TryParse(pieces[0], out rawFamily) ||
          rawFamily < (int)VelumAssemblyRegistryFamily.Assembly ||
          rawFamily > (int)VelumAssemblyRegistryFamily.Standard) return false;
      var segments = new List<string>();
      for (int i = 1; i < pieces.Length; i++)
        if (!string.IsNullOrEmpty(pieces[i])) segments.Add(pieces[i]);
      path = new VelumAssemblyRegistrySectionPath((VelumAssemblyRegistryFamily)rawFamily, segments.ToArray());
      return true;
    }

    private TreeNode EnsureFolder(TreeNode root, string[] segments)
    {
      TreeNode current = root;
      var built = new List<string>();
      if (segments == null) return current;
      foreach (string raw in segments)
      {
        string segment = VelumAssemblyRegistrySectionPath.SanitizeFolderName(raw);
        if (string.IsNullOrEmpty(segment)) continue;
        built.Add(segment);
        TreeNode child = FindChildFolder(current, segment);
        if (child == null)
        {
          VelumAssemblyRegistrySectionPath rootPath = GetFolderPath(root);
          var path = new VelumAssemblyRegistrySectionPath(rootPath.Family, built.ToArray());
          child = CreateFolderNode(segment, path);
          current.Nodes.Add(child);
        }
        current = child;
      }
      return current;
    }

    private static TreeNode FindChildFolder(TreeNode parent, string text)
    {
      foreach (TreeNode node in parent.Nodes)
      {
        if (GetFolderPath(node) != null &&
            string.Equals(node.Text, text, StringComparison.CurrentCultureIgnoreCase)) return node;
      }
      return null;
    }

    private TreeNode CreateFolderNode(string text, VelumAssemblyRegistrySectionPath path)
    {
      var node = new TreeNode(text)
      {
        Tag = new VelumAssemblyRegistryTreeTag
        {
          Kind = VelumAssemblyRegistryTreeTag.TagKind.Folder,
          FolderPath = path
        }
      };
      ApplyFolderNodeImage(node);
      return node;
    }

    private TreeNode CreateComponentNode(VelumAssemblyRegistryComponent item)
    {
      var node = new TreeNode(item.FileTitle ?? string.Empty)
      {
        Tag = new VelumAssemblyRegistryTreeTag
        {
          Kind = VelumAssemblyRegistryTreeTag.TagKind.Component,
          DocumentKey = VelumAssemblyRegistryPropertyReader.NormalizePath(item.FilePath)
        }
      };
      ApplyComponentNodeImage(node, item.Kind);
      return node;
    }

    private static VelumAssemblyRegistrySectionPath GetFolderPath(TreeNode node)
    {
      VelumAssemblyRegistryTreeTag tag = node == null ? null : node.Tag as VelumAssemblyRegistryTreeTag;
      return tag != null && tag.Kind == VelumAssemblyRegistryTreeTag.TagKind.Folder ? tag.FolderPath : null;
    }

    private static TreeNode FindFolderNodeByKey(TreeNodeCollection nodes, string key)
    {
      if (string.IsNullOrEmpty(key)) return null;
      foreach (TreeNode node in nodes)
      {
        VelumAssemblyRegistrySectionPath path = GetFolderPath(node);
        if (path != null && string.Equals(path.ToStableKey(), key, StringComparison.OrdinalIgnoreCase)) return node;
        TreeNode nested = FindFolderNodeByKey(node.Nodes, key);
        if (nested != null) return nested;
      }
      return null;
    }

    private List<VelumAssemblyRegistryComponent> CollectByKind(VelumAssemblyRegistryNodeKind kind)
    {
      var result = new List<VelumAssemblyRegistryComponent>();
      foreach (VelumAssemblyRegistryComponent item in _graph.Components.Values)
        if (item.Kind == kind) result.Add(item);
      return result;
    }

    private static List<VelumAssemblyRegistryComponent> SortByTitle(List<VelumAssemblyRegistryComponent> items)
    {
      items.Sort((a, b) => string.Compare(a.FileTitle, b.FileTitle, StringComparison.CurrentCultureIgnoreCase));
      return items;
    }

    private void ApplyComponentNodeImage(TreeNode node, VelumAssemblyRegistryNodeKind kind)
    {
      int index = kind == VelumAssemblyRegistryNodeKind.Assembly ? _idxAssembly :
          kind == VelumAssemblyRegistryNodeKind.Standard ? _idxStandard : _idxPart;
      if (index >= 0) node.ImageIndex = node.SelectedImageIndex = index;
    }

    private void ApplyFolderNodeImage(TreeNode node)
    {
      if (GetFolderPath(node) == null) return;
      int index = node.IsExpanded ? _idxFolderOpen : _idxFolderClosed;
      if (index >= 0) node.ImageIndex = node.SelectedImageIndex = index;
    }

    private void OnTreeAfterSelect(object sender, TreeViewEventArgs e)
    {
      if (_suppressTreeEvents)
        return;

      // Обычный клик по каталогу сбрасывает мультивыделение листьев.
      if (GetFolderPath(e.Node) != null &&
          (Control.ModifierKeys & Keys.Control) == 0 &&
          (Control.ModifierKeys & Keys.Shift) == 0)
        ClearTreeMultiSelection(updateVisuals: true);

      BindList();
    }

    private void OnTreeMouseDown(object sender, MouseEventArgs e)
    {
      _treePendingSingleSelect = null;
      _treeDragInProgress = false;

      if (e.Button != MouseButtons.Left || _loading)
        return;

      TreeNode node = _folderTreeView.GetNodeAt(e.Location);
      if (node == null)
        return;

      VelumAssemblyRegistryTreeTag tag = node.Tag as VelumAssemblyRegistryTreeTag;
      bool isComponent = tag != null && tag.Kind == VelumAssemblyRegistryTreeTag.TagKind.Component;
      if (!isComponent)
        return;

      bool ctrl = (Control.ModifierKeys & Keys.Control) == Keys.Control;
      bool shift = (Control.ModifierKeys & Keys.Shift) == Keys.Shift;

      if (shift && _treeShiftAnchor != null && ReferenceEquals(_treeShiftAnchor.Parent, node.Parent))
      {
        TreeNode anchor = _treeShiftAnchor;
        SelectComponentRange(anchor, node);
        _treeShiftAnchor = anchor;
        _folderTreeView.SelectedNode = node;
        BindList();
        return;
      }

      if (ctrl)
      {
        if (_treeMultiSelected.Contains(node))
          RemoveTreeMultiSelected(node);
        else
          AddTreeMultiSelected(node);
        _treeShiftAnchor = node;
        _folderTreeView.SelectedNode = node;
        BindList();
        return;
      }

      // Уже в мультивыделении — не сбрасывать до MouseUp / начала drag.
      if (_treeMultiSelected.Contains(node) && _treeMultiSelected.Count > 1)
      {
        _treePendingSingleSelect = node;
        _folderTreeView.SelectedNode = node;
        return;
      }

      ClearTreeMultiSelection(updateVisuals: true);
      AddTreeMultiSelected(node);
      _treeShiftAnchor = node;
    }

    private void OnTreeMouseUp(object sender, MouseEventArgs e)
    {
      if (e.Button != MouseButtons.Left)
        return;

      bool wasDrag = _treeDragInProgress;
      _treeDragInProgress = false;

      TreeNode pending = _treePendingSingleSelect;
      _treePendingSingleSelect = null;
      if (pending == null || wasDrag || _loading)
        return;

      if ((Control.ModifierKeys & Keys.Control) != 0 || (Control.ModifierKeys & Keys.Shift) != 0)
        return;

      ClearTreeMultiSelection(updateVisuals: true);
      AddTreeMultiSelected(pending);
      _treeShiftAnchor = pending;
      _folderTreeView.SelectedNode = pending;
      BindList();
    }

    private void OnFolderTreeKeyDown(object sender, KeyEventArgs e)
    {
      if (!IsAdmin || _loading)
        return;

      TreeNode editing = _folderTreeView.SelectedNode;
      if (editing != null && editing.IsEditing)
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
        BeginRenameFolder();
        e.Handled = true;
        e.SuppressKeyPress = true;
      }
    }

    private void SelectComponentRange(TreeNode from, TreeNode to)
    {
      if (from == null || to == null || from.Parent == null || !ReferenceEquals(from.Parent, to.Parent))
        return;

      ClearTreeMultiSelection(updateVisuals: true);
      var components = new List<TreeNode>();
      foreach (TreeNode sibling in from.Parent.Nodes)
      {
        if (IsComponentNode(sibling))
          components.Add(sibling);
      }

      int i0 = components.IndexOf(from);
      int i1 = components.IndexOf(to);
      if (i0 < 0 || i1 < 0)
        return;
      if (i0 > i1)
      {
        int tmp = i0;
        i0 = i1;
        i1 = tmp;
      }

      for (int i = i0; i <= i1; i++)
        AddTreeMultiSelected(components[i]);
    }

    private void ClearTreeMultiSelection(bool updateVisuals)
    {
      if (updateVisuals)
      {
        foreach (TreeNode node in _treeMultiSelected)
          ResetTreeNodeVisual(node);
      }

      _treeMultiSelected.Clear();
      _treeShiftAnchor = null;
    }

    private void AddTreeMultiSelected(TreeNode node)
    {
      if (node == null || !_treeMultiSelected.Add(node))
        return;
      node.BackColor = SystemColors.Highlight;
      node.ForeColor = SystemColors.HighlightText;
    }

    private void RemoveTreeMultiSelected(TreeNode node)
    {
      if (node == null || !_treeMultiSelected.Remove(node))
        return;
      ResetTreeNodeVisual(node);
    }

    private void ResetTreeNodeVisual(TreeNode node)
    {
      if (node == null)
        return;
      node.BackColor = _folderTreeView.BackColor;
      node.ForeColor = _folderTreeView.ForeColor;
    }

    private static bool IsComponentNode(TreeNode node)
    {
      VelumAssemblyRegistryTreeTag tag = node == null ? null : node.Tag as VelumAssemblyRegistryTreeTag;
      return tag != null && tag.Kind == VelumAssemblyRegistryTreeTag.TagKind.Component;
    }

    private void BindList()
    {
      _listView.BeginUpdate();
      try
      {
        _listView.Items.Clear();
        List<VelumAssemblyRegistryComponent> rows = ApplyItemFilters(ResolveRowsForSelection());
        SortItems(rows);
        foreach (VelumAssemblyRegistryComponent item in rows)
        {
          int productQty = ReadProductQuantity();
          string first = _visibleToActualIndex.Length > 0
              ? VelumAssemblyRegistryColumnResolver.ResolveDisplay(_activeColumns[_visibleToActualIndex[0]], item, productQty)
              : string.Empty;
          var row = new ListViewItem(first ?? string.Empty) { Tag = item.Identity };
          for (int v = 1; v < _visibleToActualIndex.Length; v++)
            row.SubItems.Add(
                VelumAssemblyRegistryColumnResolver.ResolveDisplay(
                    _activeColumns[_visibleToActualIndex[v]], item, productQty) ??
                string.Empty);
          _listView.Items.Add(row);
        }

        _listStatusLabel.Text = "Строк: " + _listView.Items.Count;
        UpdateTotalsValues(rows);
      }
      finally
      {
        _listView.EndUpdate();
      }
    }

    private List<VelumAssemblyRegistryComponent> ResolveRowsForSelection()
    {
      var result = new List<VelumAssemblyRegistryComponent>();
      if (_treeMultiSelected.Count > 1)
      {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (TreeNode node in _treeMultiSelected)
        {
          VelumAssemblyRegistryTreeTag tag = node.Tag as VelumAssemblyRegistryTreeTag;
          if (tag == null ||
              tag.Kind != VelumAssemblyRegistryTreeTag.TagKind.Component ||
              string.IsNullOrEmpty(tag.DocumentKey))
            continue;

          foreach (VelumAssemblyRegistryComponent item in _graph.GetComponentsByDocumentKey(tag.DocumentKey))
          {
            if (seen.Add(item.Identity))
              result.Add(item);
          }
        }

        return result;
      }

      TreeNode selected = _folderTreeView.SelectedNode;
      VelumAssemblyRegistryTreeTag selectedTag = selected == null ? null : selected.Tag as VelumAssemblyRegistryTreeTag;
      if (selectedTag == null)
        return result;
      if (selectedTag.Kind == VelumAssemblyRegistryTreeTag.TagKind.Component)
      {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (VelumAssemblyRegistryComponent item in _graph.GetComponentsByDocumentKey(selectedTag.DocumentKey))
        {
          if (!seen.Add(item.Identity))
            continue;
          result.Add(item);

          // Для сборки — также плоский список сборок-потомков (все их конфигурации).
          if (item.Kind != VelumAssemblyRegistryNodeKind.Assembly)
            continue;

          foreach (string childIdentity in _graph.GetDescendantAssemblies(item.Identity))
          {
            VelumAssemblyRegistryComponent child;
            if (!_graph.Components.TryGetValue(childIdentity, out child) ||
                child.Kind != VelumAssemblyRegistryNodeKind.Assembly ||
                !seen.Add(child.Identity))
              continue;
            result.Add(child);
          }
        }

        return result;
      }

      VelumAssemblyRegistrySectionPath folder = selectedTag.FolderPath;
      if (folder == null)
        return result;
      foreach (VelumAssemblyRegistryComponent item in _graph.Components.Values)
      {
        VelumAssemblyRegistrySectionPath path = item.GetSectionPath();
        if (path.Family == folder.Family &&
            VelumAssemblyRegistrySectionPath.IsPrefix(path.Segments, folder.Segments))
          result.Add(item);
      }

      return result;
    }

    private void ApplyFiltersFromUi()
    {
      for (int v = 0; v < _filterBoxes.Length; v++)
      {
        int actualIndex = _visibleToActualFilterIndex[v];
        _activeFilters[actualIndex] = _filterBoxes[v] == null
            ? string.Empty
            : (_filterBoxes[v].Text ?? string.Empty).Trim();
      }
      BindList();
    }

    private void ResetFilters()
    {
      for (int v = 0; v < _filterBoxes.Length; v++)
      {
        if (_filterBoxes[v] != null)
          _filterBoxes[v].Text = string.Empty;
        int actualIndex = _visibleToActualFilterIndex[v];
        _activeFilters[actualIndex] = string.Empty;
      }
      BindList();
    }

    private List<VelumAssemblyRegistryComponent> ApplyItemFilters(List<VelumAssemblyRegistryComponent> items)
    {
      var result = new List<VelumAssemblyRegistryComponent>();
      foreach (VelumAssemblyRegistryComponent item in items)
        if (MatchesFilters(item)) result.Add(item);
      return result;
    }

    private bool MatchesFilters(VelumAssemblyRegistryComponent item)
    {
      int productQty = ReadProductQuantity();
      for (int i = 0; i < _activeFilters.Length && i < _activeColumns.Count; i++)
      {
        if (string.IsNullOrEmpty(_activeFilters[i]))
          continue;
        string display =
            VelumAssemblyRegistryColumnResolver.ResolveDisplay(_activeColumns[i], item, productQty) ??
            string.Empty;
        string raw =
            VelumAssemblyRegistryColumnResolver.ResolveRaw(_activeColumns[i], item, productQty) ??
            string.Empty;
        if (!VelumListFilterHelper.Matches(display, _activeFilters[i], raw))
          return false;
      }

      return true;
    }

    private void SortItems(List<VelumAssemblyRegistryComponent> items)
    {
      int direction = _sortOrder == SortOrder.Descending ? -1 : 1;
      items.Sort((a, b) =>
      {
        int cmp = CompareColumn(a, b, _sortColumn);
        if (cmp == 0) cmp = string.Compare(a.Identity, b.Identity, StringComparison.OrdinalIgnoreCase);
        return cmp * direction;
      });
    }

    private int CompareColumn(VelumAssemblyRegistryComponent a, VelumAssemblyRegistryComponent b, int column)
    {
      string left = GetColumnRaw(a, column);
      string right = GetColumnRaw(b, column);
      if (LooksNumeric(left) || LooksNumeric(right))
        return CompareNumeric(left, right);
      return string.Compare(left, right, StringComparison.CurrentCultureIgnoreCase);
    }

    private string GetColumnRaw(VelumAssemblyRegistryComponent item, int column)
    {
      if (column < 0 || column >= _activeColumns.Count)
        return string.Empty;
      return VelumAssemblyRegistryColumnResolver.ResolveRaw(
                 _activeColumns[column],
                 item,
                 ReadProductQuantity()) ??
             string.Empty;
    }

    private static bool LooksNumeric(string text)
    {
      double value;
      return TryParseSortNumber(text, out value);
    }

    private static int CompareNumeric(string left, string right)
    {
      double a, b;
      bool leftOk = TryParseSortNumber(left, out a), rightOk = TryParseSortNumber(right, out b);
      if (leftOk && rightOk) return a.CompareTo(b);
      if (leftOk) return -1;
      if (rightOk) return 1;
      return string.Compare(left, right, StringComparison.CurrentCultureIgnoreCase);
    }

    private static bool TryParseSortNumber(string text, out double value)
    {
      return VelumAssemblyRegistryPropertyReader.TryParseListNumber(text, out value);
    }

    private void OnListColumnClick(object sender, ColumnClickEventArgs e)
    {
      int actualColumn = e.Column < _visibleToActualIndex.Length
          ? _visibleToActualIndex[e.Column]
          : e.Column;
      if (actualColumn == _sortColumn) _sortOrder = _sortOrder == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
      else { _sortColumn = actualColumn; _sortOrder = SortOrder.Ascending; }
      BindList();
    }

    private void AddFolder()
    {
      if (!IsAdmin || _loading) return;
      VelumAssemblyRegistrySectionPath selected = GetFolderPath(_folderTreeView.SelectedNode);
      if (selected == null) return;
      string name = VelumAssemblyRegistrySectionPath.SanitizeFolderName("Новый каталог");
      if (string.IsNullOrEmpty(name)) name = "Новый_каталог";
      VelumAssemblyRegistrySectionPath added = selected.AppendSegment(name);
      _ephemeralFolderKeys.Add(added.ToStableKey());
      RebuildTreeProjection(added.ToStableKey());
      if (_folderTreeView.SelectedNode != null) _folderTreeView.SelectedNode.BeginEdit();
    }

    private void BeginRenameFolder()
    {
      VelumAssemblyRegistrySectionPath path = GetFolderPath(_folderTreeView.SelectedNode);
      if (IsAdmin && path != null && !path.IsRoot && !_loading) _folderTreeView.SelectedNode.BeginEdit();
    }

    private void OnFolderAfterLabelEdit(object sender, NodeLabelEditEventArgs e)
    {
      if (!IsAdmin || e.Node == null) { e.CancelEdit = true; return; }
      VelumAssemblyRegistrySectionPath oldPath = GetFolderPath(e.Node);
      if (oldPath == null || oldPath.IsRoot || e.Label == null) { e.CancelEdit = true; return; }
      string name = VelumAssemblyRegistrySectionPath.SanitizeFolderName(e.Label);
      if (string.IsNullOrEmpty(name))
      {
        e.CancelEdit = true;
        ShowMessage("Введите непустое имя каталога.", MessageBoxIcon.Information);
        return;
      }
      VelumAssemblyRegistrySectionPath newPath = oldPath.WithRenamedLeaf(name);
      if (oldPath.Equals(newPath)) { e.CancelEdit = true; return; }
      e.CancelEdit = true;
      RenameFolder(oldPath, newPath);
    }

    private void RenameFolder(VelumAssemblyRegistrySectionPath oldPath, VelumAssemblyRegistrySectionPath newPath)
    {
      List<VelumAssemblyRegistryComponent> targets = CollectComponentsUnderFolder(oldPath);
      if (targets.Count == 0)
      {
        RewriteEphemeralPrefix(oldPath, newPath);
        RebuildTreeProjection(newPath.ToStableKey());
        return;
      }
      if (!Confirm("Переименовать каталог «" + oldPath.ToDisplayPath() + "»?")) return;
      VelumAssemblyRegistrySectionBatchResult result = RunMutation(targets, item =>
      {
        VelumAssemblyRegistrySectionPath rewritten;
        return VelumAssemblyRegistrySectionPath.TryRewriteRenamedPrefix(item.GetSectionPath(), oldPath, newPath, out rewritten)
            ? rewritten.ToPropertyValue() : item.GetSectionPath().ToPropertyValue();
      });
      if (!result.HadFailure && !result.Stopped)
      {
        RewriteEphemeralPrefix(oldPath, newPath);
        RebuildTreeAfterMutation();
      }
      ShowReport(result);
    }

    private void DeleteSelectedFolder()
    {
      if (!IsAdmin || _loading) return;
      VelumAssemblyRegistrySectionPath deleted = GetFolderPath(_folderTreeView.SelectedNode);
      if (deleted == null || deleted.IsRoot) return;
      if (!Confirm("Удалить каталог «" + deleted.ToDisplayPath() + "»? Компоненты будут перенесены на уровень выше.")) return;
      List<VelumAssemblyRegistryComponent> targets = CollectComponentsUnderFolder(deleted);
      VelumAssemblyRegistrySectionBatchResult result = RunMutation(targets, item =>
      {
        VelumAssemblyRegistrySectionPath rewritten;
        return VelumAssemblyRegistrySectionPath.TryRemoveDeletedSegment(item.GetSectionPath(), deleted, out rewritten)
            ? rewritten.ToPropertyValue() : item.GetSectionPath().ToPropertyValue();
      });
      if (!result.HadFailure && !result.Stopped)
      {
        RemoveEphemeralUnder(deleted);
        RebuildTreeAfterMutation();
      }
      ShowReport(result);
    }

    private List<VelumAssemblyRegistryComponent> CollectComponentsUnderFolder(VelumAssemblyRegistrySectionPath folder)
    {
      var result = new List<VelumAssemblyRegistryComponent>();
      if (folder == null) return result;
      foreach (VelumAssemblyRegistryComponent item in _graph.Components.Values)
      {
        VelumAssemblyRegistrySectionPath itemPath = item.GetSectionPath();
        if (itemPath.Family == folder.Family &&
            VelumAssemblyRegistrySectionPath.IsPrefix(itemPath.Segments, folder.Segments)) result.Add(item);
      }
      return result;
    }

    private void RewriteEphemeralPrefix(VelumAssemblyRegistrySectionPath oldPath, VelumAssemblyRegistrySectionPath newPath)
    {
      var changes = new List<string>();
      foreach (string key in _ephemeralFolderKeys)
      {
        VelumAssemblyRegistrySectionPath path, rewritten;
        if (TryPathFromKey(key, out path) &&
            VelumAssemblyRegistrySectionPath.TryRewriteRenamedPrefix(path, oldPath, newPath, out rewritten))
          changes.Add(key + "\n" + rewritten.ToStableKey());
      }
      foreach (string pair in changes)
      {
        int split = pair.IndexOf('\n');
        _ephemeralFolderKeys.Remove(pair.Substring(0, split));
        _ephemeralFolderKeys.Add(pair.Substring(split + 1));
      }
    }

    private void RemoveEphemeralUnder(VelumAssemblyRegistrySectionPath deleted)
    {
      var remove = new List<string>();
      foreach (string key in _ephemeralFolderKeys)
      {
        VelumAssemblyRegistrySectionPath path;
        if (TryPathFromKey(key, out path) && path.Family == deleted.Family &&
            VelumAssemblyRegistrySectionPath.IsPrefix(path.Segments, deleted.Segments)) remove.Add(key);
      }
      foreach (string key in remove) _ephemeralFolderKeys.Remove(key);
    }

    private void ChangeSelectedCatalog()
    {
      if (!IsAdmin || _loading) return;
      List<VelumAssemblyRegistryComponent> targets = GetSelectedListComponents();
      if (targets.Count == 0) return;
      VelumAssemblyRegistrySectionPath initial = targets[0].GetSectionPath();
      using (var picker = new VelumAssemblyRegistryFolderPickerForm(CollectFolderPathsForPicker(), initial))
      {
        if (picker.ShowDialog(this) == DialogResult.OK && picker.SelectedPath != null)
          MoveComponentsToFolder(targets, picker.SelectedPath);
      }
    }

    private void ChangeSelectedMaterial()
    {
      if (!IsAdmin || _loading)
        return;

      List<VelumAssemblyRegistryComponent> selected = GetSelectedListComponents();
      if (selected.Count == 0)
        return;

      var parts = new List<VelumAssemblyRegistryComponent>();
      int assembliesSkipped = 0;
      foreach (VelumAssemblyRegistryComponent item in selected)
      {
        if (item.Kind == VelumAssemblyRegistryNodeKind.Assembly)
          assembliesSkipped++;
        else
          parts.Add(item);
      }

      if (parts.Count == 0)
      {
        ShowMessage(
            assembliesSkipped > 0
                ? "В выделении только сборки. Материал назначается только деталям."
                : "В выделении нет деталей для смены материала.",
            MessageBoxIcon.Information);
        return;
      }

      if (assembliesSkipped > 0)
      {
        ShowMessage(
            "Сборки в выделении (" + assembliesSkipped +
            ") будут проигнорированы. Материал назначается только деталям.",
            MessageBoxIcon.Warning);
      }

      var rows = new List<VelumMaterialBatchRow>();
      foreach (VelumAssemblyRegistryComponent item in parts)
      {
        string material = item.Material ?? string.Empty;
        if (string.Equals(
                material,
                VelumAssemblyRegistryPropertyReader.MissingMarker,
                StringComparison.Ordinal))
          material = string.Empty;

        string displayName = string.Empty;
        try
        {
          displayName = Path.GetFileName(item.FilePath);
        }
        catch
        {
        }

        if (string.IsNullOrWhiteSpace(displayName))
          displayName = item.FileTitle ?? string.Empty;

        rows.Add(new VelumMaterialBatchRow
        {
          PartPath = item.FilePath,
          PartDisplayName = displayName,
          ConfigName = item.ConfigurationName ?? string.Empty,
          MaterialName = material,
          MaterialDatabase = string.Empty,
          Selected = true
        });
      }

      bool applied;
      if (!VelumMaterialBatchFormHost.TryShowWithRows(_swApp, this, rows, out applied))
        return;

      if (applied)
        RebuildTreeAfterMutation();
    }

    private IEnumerable<VelumAssemblyRegistrySectionPath> CollectFolderPathsForPicker()
    {
      var paths = new Dictionary<string, VelumAssemblyRegistrySectionPath>(StringComparer.OrdinalIgnoreCase);
      CollectTreeFolderPaths(_folderTreeView.Nodes, paths);
      foreach (string key in _ephemeralFolderKeys)
      {
        VelumAssemblyRegistrySectionPath path;
        if (TryPathFromKey(key, out path)) paths[path.ToStableKey()] = path;
      }
      foreach (VelumAssemblyRegistryFamily family in new[]
          { VelumAssemblyRegistryFamily.Assembly, VelumAssemblyRegistryFamily.Part, VelumAssemblyRegistryFamily.Standard })
      {
        var root = new VelumAssemblyRegistrySectionPath(family, Array.Empty<string>());
        paths[root.ToStableKey()] = root;
      }
      return paths.Values;
    }

    private static void CollectTreeFolderPaths(TreeNodeCollection nodes, Dictionary<string, VelumAssemblyRegistrySectionPath> paths)
    {
      foreach (TreeNode node in nodes)
      {
        VelumAssemblyRegistrySectionPath path = GetFolderPath(node);
        if (path != null) paths[path.ToStableKey()] = path;
        CollectTreeFolderPaths(node.Nodes, paths);
      }
    }

    private List<VelumAssemblyRegistryComponent> GetSelectedListComponents()
    {
      var result = new List<VelumAssemblyRegistryComponent>();
      var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (ListViewItem row in _listView.SelectedItems)
      {
        string identity = row.Tag as string;
        VelumAssemblyRegistryComponent item;
        if (!string.IsNullOrEmpty(identity) && seen.Add(identity) &&
            _graph.Components.TryGetValue(identity, out item)) result.Add(item);
      }
      return result;
    }

    private void MoveComponentsToFolder(List<VelumAssemblyRegistryComponent> source, VelumAssemblyRegistrySectionPath target)
    {
      if (!IsAdmin || target == null || source == null || source.Count == 0)
        return;

      var allowed = new List<VelumAssemblyRegistryComponent>();
      var forbidden = new List<string>();
      var alreadyThere = new List<string>();
      foreach (VelumAssemblyRegistryComponent item in source)
      {
        VelumAssemblyRegistrySectionPath current = item.GetSectionPath();
        if (!VelumAssemblyRegistrySectionPath.CanMove(current.Family, target.Family))
        {
          forbidden.Add(string.IsNullOrEmpty(item.Designation) ? item.FileTitle : item.Designation);
          continue;
        }

        if (current.Equals(target))
        {
          alreadyThere.Add(string.IsNullOrEmpty(item.Designation) ? item.FileTitle : item.Designation);
          continue;
        }

        allowed.Add(item);
      }

      if (forbidden.Count > 0)
      {
        ShowMessage(
            "Пропущены (нельзя переносить между этими семействами):\n" +
            string.Join("\n", forbidden.ToArray()),
            MessageBoxIcon.Information);
      }

      if (allowed.Count == 0)
      {
        if (alreadyThere.Count > 0 && forbidden.Count == 0)
          ShowMessage("Выбранные позиции уже в каталоге «" + target.ToDisplayPath() + "».", MessageBoxIcon.Information);
        return;
      }

      if (!Confirm(
          "Переместить " + allowed.Count + " позиц. в «" + target.ToDisplayPath() + "»?\n" +
          "Будет изменено свойство «Раздел» и выполнено сохранение файлов."))
        return;

      VelumAssemblyRegistrySectionBatchResult result = RunMutation(allowed, item => target.ToPropertyValue());
      if (!result.HadFailure && !result.Stopped)
        RebuildTreeAfterMutation(target.ToStableKey());
      ShowReport(result);
    }

    private void MoveFolderTo(VelumAssemblyRegistrySectionPath sourceFolder, VelumAssemblyRegistrySectionPath targetParent)
    {
      if (!IsAdmin || sourceFolder == null || targetParent == null || sourceFolder.IsRoot)
        return;

      if (VelumAssemblyRegistrySectionPath.IsInvalidFolderDropTarget(sourceFolder, targetParent))
      {
        ShowMessage(
            "Нельзя переместить каталог «" + sourceFolder.ToDisplayPath() +
            "» в «" + targetParent.ToDisplayPath() + "».",
            MessageBoxIcon.Information);
        return;
      }

      if (!VelumAssemblyRegistrySectionPath.CanMove(sourceFolder.Family, targetParent.Family))
      {
        ShowMessage(
            "Нельзя переносить каталоги между этими семействами.",
            MessageBoxIcon.Information);
        return;
      }

      string leaf = sourceFolder.Segments[sourceFolder.Segments.Length - 1];
      VelumAssemblyRegistrySectionPath newLocation;
      try
      {
        newLocation = targetParent.AppendSegment(leaf);
      }
      catch (ArgumentException ex)
      {
        ShowMessage(ex.Message, MessageBoxIcon.Warning);
        return;
      }

      if (sourceFolder.Equals(newLocation))
      {
        ShowMessage("Каталог уже в «" + targetParent.ToDisplayPath() + "».", MessageBoxIcon.Information);
        return;
      }

      List<VelumAssemblyRegistryComponent> targets = CollectComponentsUnderFolder(sourceFolder);
      string confirm =
          "Переместить каталог «" + sourceFolder.ToDisplayPath() +
          "» в «" + targetParent.ToDisplayPath() + "»?\n" +
          "Будет изменено свойство «Раздел» у всех входящих элементов" +
          (targets.Count > 0 ? " (" + targets.Count + ")" : string.Empty) +
          ", включая вложенные каталоги, и выполнено сохранение файлов.";
      if (!Confirm(confirm))
        return;

      if (targets.Count == 0)
      {
        RewriteEphemeralMovedPrefix(sourceFolder, newLocation);
        HashSet<string> expand = CaptureExpandedFolderKeys();
        expand.Add(newLocation.ToStableKey());
        expand.Add(targetParent.ToStableKey());
        RebuildTreeProjection(newLocation.ToStableKey(), expand);
        return;
      }

      VelumAssemblyRegistrySectionBatchResult result = RunMutation(targets, item =>
      {
        VelumAssemblyRegistrySectionPath rewritten;
        return VelumAssemblyRegistrySectionPath.TryRewriteMovedPrefix(
                item.GetSectionPath(),
                sourceFolder,
                newLocation,
                out rewritten)
            ? rewritten.ToPropertyValue()
            : item.GetSectionPath().ToPropertyValue();
      });
      if (!result.HadFailure && !result.Stopped)
      {
        RewriteEphemeralMovedPrefix(sourceFolder, newLocation);
        HashSet<string> expand = CaptureExpandedFolderKeys();
        expand.Add(newLocation.ToStableKey());
        expand.Add(targetParent.ToStableKey());
        RebuildTreeAfterMutation(newLocation.ToStableKey(), expand);
      }
      ShowReport(result);
    }

    private void RewriteEphemeralMovedPrefix(
        VelumAssemblyRegistrySectionPath sourceFolder,
        VelumAssemblyRegistrySectionPath newLocation)
    {
      var changes = new List<string>();
      foreach (string key in _ephemeralFolderKeys)
      {
        VelumAssemblyRegistrySectionPath path, rewritten;
        if (TryPathFromKey(key, out path) &&
            VelumAssemblyRegistrySectionPath.TryRewriteMovedPrefix(path, sourceFolder, newLocation, out rewritten))
          changes.Add(key + "\n" + rewritten.ToStableKey());
      }
      foreach (string pair in changes)
      {
        int split = pair.IndexOf('\n');
        _ephemeralFolderKeys.Remove(pair.Substring(0, split));
        _ephemeralFolderKeys.Add(pair.Substring(split + 1));
      }
    }

    private void RebuildTreeAfterMutation()
    {
      RebuildTreeAfterMutation(null, null);
    }

    private void RebuildTreeAfterMutation(string selectFolderPath)
    {
      RebuildTreeAfterMutation(selectFolderPath, null);
    }

    private void RebuildTreeAfterMutation(string selectFolderPath, HashSet<string> expandFolderKeys)
    {
      HashSet<string> expand = expandFolderKeys ?? CaptureExpandedFolderKeys();
      if (!string.IsNullOrEmpty(selectFolderPath))
        expand.Add(selectFolderPath);
      SetProgress(0, 1, "Обновление дерева после записи свойств…");
      Application.DoEvents();
      RebuildFromSolidWorks(selectFolderPath, expand);
    }

    private VelumAssemblyRegistrySectionBatchResult RunMutation(
        IReadOnlyList<VelumAssemblyRegistryComponent> targets,
        Func<VelumAssemblyRegistryComponent, string> valueFactory)
    {
      _stopRequested = false;
      _loading = true;
      SetLoadingUi(true);
      try
      {
        var mutator = new VelumAssemblyRegistrySectionMutator(_swApp, () => _stopRequested, SetProgress);
        return mutator.Apply(targets, valueFactory);
      }
      catch (Exception ex)
      {
        ShowMessage(ex.Message, MessageBoxIcon.Error);
        return new VelumAssemblyRegistrySectionBatchResult { HadFailure = true };
      }
      finally
      {
        _loading = false;
        SetLoadingUi(false);
        _progressBar.Value = 0;
      }
    }

    private void ShowReport(VelumAssemblyRegistrySectionBatchResult result)
    {
      if (result == null)
        return;
      ShowMessage(result.BuildReport(), result.HadFailure ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
    }

    private void OnTreeItemDrag(object sender, ItemDragEventArgs e)
    {
      if (!IsAdmin || _loading)
        return;

      TreeNode node = e.Item as TreeNode;
      if (node == null)
        return;

      VelumAssemblyRegistrySectionPath folderPath = GetFolderPath(node);
      if (folderPath != null)
      {
        if (folderPath.IsRoot)
          return;
        _treeDragInProgress = true;
        _treePendingSingleSelect = null;
        _folderTreeView.DoDragDrop(
            new VelumAssemblyRegistryTreeDragData { SourceFolder = folderPath },
            DragDropEffects.Move);
        return;
      }

      if (!IsComponentNode(node))
        return;

      if (!_treeMultiSelected.Contains(node))
      {
        ClearTreeMultiSelection(updateVisuals: true);
        AddTreeMultiSelected(node);
        _treeShiftAnchor = node;
      }

      var identities = new List<string>();
      var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (TreeNode selected in _treeMultiSelected)
      {
        VelumAssemblyRegistryTreeTag tag = selected.Tag as VelumAssemblyRegistryTreeTag;
        if (tag == null || string.IsNullOrEmpty(tag.DocumentKey))
          continue;

        foreach (VelumAssemblyRegistryComponent item in _graph.GetComponentsByDocumentKey(tag.DocumentKey))
        {
          if (seen.Add(item.Identity))
            identities.Add(item.Identity);
        }
      }

      if (identities.Count == 0)
        return;

      _treeDragInProgress = true;
      _treePendingSingleSelect = null;
      _folderTreeView.DoDragDrop(
          new VelumAssemblyRegistryTreeDragData { ComponentIdentities = identities },
          DragDropEffects.Move);
    }

    private void OnListItemDrag(object sender, ItemDragEventArgs e)
    {
      if (!IsAdmin || _loading)
        return;
      var identities = new List<string>();
      foreach (VelumAssemblyRegistryComponent item in GetSelectedListComponents())
        identities.Add(item.Identity);
      if (identities.Count > 0)
      {
        _listView.DoDragDrop(
            new VelumAssemblyRegistryTreeDragData { ComponentIdentities = identities },
            DragDropEffects.Move);
      }
    }

    private void OnTreeDragEnter(object sender, DragEventArgs e)
    {
      SetTreeDragEffect(e);
    }

    private void OnTreeDragOver(object sender, DragEventArgs e)
    {
      SetTreeDragEffect(e);
    }

    private void SetTreeDragEffect(DragEventArgs e)
    {
      e.Effect = DragDropEffects.None;
      if (!IsAdmin || _loading)
        return;

      VelumAssemblyRegistryTreeDragData payload = GetTreeDragPayload(e.Data);
      if (payload == null)
        return;

      TreeNode node = _folderTreeView.GetNodeAt(_folderTreeView.PointToClient(new Point(e.X, e.Y)));
      VelumAssemblyRegistrySectionPath target = GetFolderPath(node);
      if (target == null)
        return;

      if (payload.SourceFolder != null)
      {
        if (!VelumAssemblyRegistrySectionPath.IsInvalidFolderDropTarget(payload.SourceFolder, target) &&
            VelumAssemblyRegistrySectionPath.CanMove(payload.SourceFolder.Family, target.Family))
          e.Effect = DragDropEffects.Move;
        return;
      }

      if (payload.ComponentIdentities != null && payload.ComponentIdentities.Count > 0)
        e.Effect = DragDropEffects.Move;
    }

    private void OnTreeDragDrop(object sender, DragEventArgs e)
    {
      if (!IsAdmin || _loading)
        return;

      VelumAssemblyRegistryTreeDragData payload = GetTreeDragPayload(e.Data);
      if (payload == null)
        return;

      TreeNode node = _folderTreeView.GetNodeAt(_folderTreeView.PointToClient(new Point(e.X, e.Y)));
      VelumAssemblyRegistrySectionPath target = GetFolderPath(node);
      if (target == null)
        return;

      // MessageBox во время DragDrop часто не показывается — откладываем до конца жеста.
      VelumAssemblyRegistrySectionPath targetCopy = target;
      if (payload.SourceFolder != null)
      {
        VelumAssemblyRegistrySectionPath sourceCopy = payload.SourceFolder;
        BeginInvoke(new Action(() => MoveFolderTo(sourceCopy, targetCopy)));
        return;
      }

      if (payload.ComponentIdentities == null || payload.ComponentIdentities.Count == 0)
        return;

      var items = new List<VelumAssemblyRegistryComponent>();
      foreach (string identity in payload.ComponentIdentities)
      {
        VelumAssemblyRegistryComponent item;
        if (_graph.Components.TryGetValue(identity, out item))
          items.Add(item);
      }

      if (items.Count == 0)
        return;

      List<VelumAssemblyRegistryComponent> itemsCopy = items;
      BeginInvoke(new Action(() => MoveComponentsToFolder(itemsCopy, targetCopy)));
    }

    private static VelumAssemblyRegistryTreeDragData GetTreeDragPayload(IDataObject data)
    {
      if (data == null)
        return null;

      if (data.GetDataPresent(typeof(VelumAssemblyRegistryTreeDragData)))
        return data.GetData(typeof(VelumAssemblyRegistryTreeDragData)) as VelumAssemblyRegistryTreeDragData;

      // Совместимость со старым форматом List<string> (на случай внешних вызовов).
      if (data.GetDataPresent(typeof(List<string>)))
      {
        var identities = data.GetData(typeof(List<string>)) as List<string>;
        if (identities != null && identities.Count > 0)
          return new VelumAssemblyRegistryTreeDragData { ComponentIdentities = identities };
      }

      return null;
    }

    private sealed class VelumAssemblyRegistryTreeDragData
    {
      internal List<string> ComponentIdentities;
      internal VelumAssemblyRegistrySectionPath SourceFolder;
    }

    private void PrintSelectedDrawings()
    {
      if (_loading)
        return;

      List<VelumAssemblyRegistryComponent> selected = GetSelectedListComponents();
      if (selected.Count == 0)
      {
        ShowMessage("Выделите позиции в списке для печати чертежей.", MessageBoxIcon.Information);
        return;
      }

      // Чертежи — по свойству «путь чертежа», без каталога «Чертежи» в hub.
      List<VelumAssemblyRegistryPrintDrawingsService.PrintRow> rows =
          VelumAssemblyRegistryPrintDrawingsService.Resolve(selected);
      if (rows.Count == 0)
      {
        ShowMessage("По выделенным позициям нечего печатать.", MessageBoxIcon.Information);
        return;
      }

      using (var form = new VelumAssemblyRegistryPrintDrawingsForm(_swApp, rows))
        form.ShowDialog(this);
    }

    private void OpenSelectedListItems()
    {
      if (_loading)
        return;

      List<VelumAssemblyRegistryComponent> items = GetSelectedListComponents();
      if (items.Count == 0 || _swApp == null || _swApp.Sw == null)
        return;

      // Много документов подряд — OpenDoc/Activate в SW может заметно тормозить UI.
      const int bulkOpenWarnThreshold = 10;
      bool bulk = items.Count > bulkOpenWarnThreshold;
      if (bulk)
      {
        if (!Confirm(
            "Выбрано " + items.Count + " документов.\n" +
            "Открытие может занять продолжительное время из‑за загрузки в SolidWorks.\n\n" +
            "Продолжить?"))
          return;

        _stopRequested = false;
        _loading = true;
        SetLoadingUi(true);
        UseWaitCursor = true;
      }

      var missing = new List<string>();
      try
      {
        int total = items.Count;
        for (int i = 0; i < total; i++)
        {
          if (bulk && _stopRequested)
            break;

          VelumAssemblyRegistryComponent item = items[i];
          string label = string.IsNullOrWhiteSpace(item.Designation)
              ? (item.FileTitle ?? "?")
              : item.Designation;

          if (bulk)
            SetProgress(i, total, "Открытие " + (i + 1) + " из " + total + ": " + label);

          string error;
          if (!TryOpenComponentInOwnWindow(item, out error))
            missing.Add(label + (string.IsNullOrEmpty(error) ? string.Empty : (": " + error)));
        }

        if (bulk)
        {
          if (_stopRequested)
            _progressLabel.Text = "Прервано";
          else
            SetProgress(total, total, "Открыто: " + (total - missing.Count) + " из " + total);
        }
      }
      catch (Exception ex)
      {
        missing.Add(ex.Message);
      }
      finally
      {
        if (bulk)
        {
          UseWaitCursor = false;
          _loading = false;
          SetLoadingUi(false);
          _progressBar.Value = 0;
          if (!_stopRequested)
            _progressLabel.Text = string.Empty;
        }
      }

      if (missing.Count > 0)
        ShowMessage("Не удалось открыть:\n" + string.Join("\n", missing.ToArray()), MessageBoxIcon.Warning);
    }

    /// <summary>
    /// Открывает документ в отдельном окне SW.
    /// Компонент из состава сборки уже в памяти — его всё равно нужно ActivateDoc3
    /// (иначе отдельное окно не появится). Головную сборку после Open не восстанавливаем.
    /// </summary>
    private bool TryOpenComponentInOwnWindow(VelumAssemblyRegistryComponent item, out string error)
    {
      error = string.Empty;
      if (item == null)
      {
        error = "Нет записи";
        return false;
      }

      if (string.IsNullOrWhiteSpace(item.FilePath) || !File.Exists(item.FilePath))
      {
        error = "Файл не найден";
        return false;
      }

      int docType = item.Kind == VelumAssemblyRegistryNodeKind.Assembly
          ? (int)swDocumentTypes_e.swDocASSEMBLY
          : (int)swDocumentTypes_e.swDocPART;

      ModelDoc2 doc = FindOpenDocument(item.FilePath);
      if (doc == null)
      {
        try
        {
          int openErrors = 0;
          int warnings = 0;
          doc = _swApp.Sw.OpenDoc6(
              item.FilePath,
              docType,
              0,
              string.Empty,
              ref openErrors,
              ref warnings) as ModelDoc2;
          if (doc == null)
            doc = FindOpenDocument(item.FilePath);

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
        // Документ, подгруженный только как компонент сборки, часто Invisible —
        // без Visible/Activate отдельное окно не создаётся.
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

        if (!string.IsNullOrEmpty(item.ConfigurationName))
        {
          try
          {
            doc.ShowConfiguration2(item.ConfigurationName);
          }
          catch
          {
          }
        }

        return true;
      }
      catch (Exception ex)
      {
        error = ex.Message;
        return false;
      }
    }

    private ModelDoc2 FindOpenDocument(string path)
    {
      string normalized = VelumAssemblyRegistryPropertyReader.NormalizePath(path);
      try
      {
        ModelDoc2 doc = _swApp.Sw.GetFirstDocument() as ModelDoc2;
        while (doc != null)
        {
          if (string.Equals(VelumAssemblyRegistryPropertyReader.NormalizePath(doc.GetPathName()), normalized, StringComparison.OrdinalIgnoreCase))
            return doc;
          doc = doc.GetNext() as ModelDoc2;
        }
      }
      catch { }
      return null;
    }

    private void SelectAllListItems()
    {
      _listView.BeginUpdate();
      try { foreach (ListViewItem item in _listView.Items) item.Selected = true; }
      finally { _listView.EndUpdate(); }
    }

    private void OnTreeSearchKeyDown(object sender, KeyEventArgs e)
    {
      if (e.KeyCode != Keys.Enter) return;
      SearchTree();
      e.Handled = true;
      e.SuppressKeyPress = true;
    }

    private void SearchTree()
    {
      _treeSearchResults.Clear();
      _treeSearchIndex = -1;
      string query = (_treeSearchBox.Text ?? string.Empty).Trim();
      if (query.Length == 0) { _treeSearchStatusLabel.Text = string.Empty; return; }
      CollectMatchingNodes(_folderTreeView.Nodes, query, _treeSearchResults);
      _treeSearchStatusLabel.Text = _treeSearchResults.Count == 0 ? "Ничего не найдено" : "Найдено: " + _treeSearchResults.Count;
      if (_treeSearchResults.Count > 0) ShowTreeResult(0);
    }

    private static void CollectMatchingNodes(TreeNodeCollection nodes, string query, List<TreeNode> results)
    {
      foreach (TreeNode node in nodes)
      {
        if ((node.Text ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) results.Add(node);
        CollectMatchingNodes(node.Nodes, query, results);
      }
    }

    private void ShowNextTreeResult()
    {
      if (_treeSearchResults.Count == 0) return;
      ShowTreeResult((_treeSearchIndex + 1) % _treeSearchResults.Count);
    }

    private void ShowPreviousTreeResult()
    {
      if (_treeSearchResults.Count == 0) return;
      ShowTreeResult((_treeSearchIndex + _treeSearchResults.Count - 1) % _treeSearchResults.Count);
    }

    private void ShowTreeResult(int index)
    {
      if (index < 0 || index >= _treeSearchResults.Count) return;
      _treeSearchIndex = index;
      TreeNode node = _treeSearchResults[index];
      ExpandParents(node);
      _folderTreeView.SelectedNode = node;
      node.EnsureVisible();
      _treeSearchStatusLabel.Text = "Найдено: " + _treeSearchResults.Count + " (" + (index + 1) + ")";
    }

    private static void ExpandParents(TreeNode node)
    {
      for (TreeNode parent = node == null ? null : node.Parent; parent != null; parent = parent.Parent) parent.Expand();
    }

    private void ClearTreeSearch(bool keepText)
    {
      _treeSearchResults.Clear();
      _treeSearchIndex = -1;
      _treeSearchStatusLabel.Text = string.Empty;
      if (!keepText) _treeSearchBox.Text = string.Empty;
    }

    protected override bool ProcessDialogKey(Keys keyData)
    {
      if (keyData == Keys.Escape)
      {
        if (!_loading) Close();
        return true;
      }
      return base.ProcessDialogKey(keyData);
    }

    private bool Confirm(string text)
    {
      return MessageBox.Show(this, text, "Реестр изделия", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
    }

    private void ShowMessage(string text, MessageBoxIcon icon)
    {
      MessageBox.Show(this, text, "Реестр изделия", MessageBoxButtons.OK, icon);
    }

    private void SetupTreeImages()
    {
      var images = new ImageList { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(16, 16) };
      TryAddTreeImage(images, ImgFolderClosed, "Folder.png");
      if (!TryAddTreeImage(images, ImgFolderOpen, "open_folder.png")) TryAddTreeImage(images, ImgFolderOpen, "Folder.png");
      if (!images.Images.ContainsKey(ImgFolderClosed) && images.Images.ContainsKey(ImgFolderOpen))
        images.Images.Add(ImgFolderClosed, images.Images[ImgFolderOpen]);
      TryAddTreeImage(images, ImgAssembly, "Assemblies.png");
      TryAddTreeImage(images, ImgPart, "Parts.png");
      TryAddTreeImage(images, ImgStandard, "toolbox.png");
      _idxFolderClosed = IndexOfTreeImage(images, ImgFolderClosed);
      _idxFolderOpen = IndexOfTreeImage(images, ImgFolderOpen);
      _idxAssembly = IndexOfTreeImage(images, ImgAssembly);
      _idxPart = IndexOfTreeImage(images, ImgPart);
      _idxStandard = IndexOfTreeImage(images, ImgStandard);
      if (images.Images.Count > 0) _folderTreeView.ImageList = images;
    }

    private static int IndexOfTreeImage(ImageList images, string key)
    {
      return images != null && images.Images.ContainsKey(key) ? images.Images.IndexOfKey(key) : -1;
    }

    private static bool TryAddTreeImage(ImageList images, string key, string fileName)
    {
      if (images == null || images.Images.ContainsKey(key)) return images != null && images.Images.ContainsKey(key);
      Image loaded = TryLoadMenuBitmap(fileName);
      if (loaded == null) return false;
      Image sized = ResizeToTreeIcon(loaded, images.ImageSize);
      if (!ReferenceEquals(sized, loaded)) loaded.Dispose();
      images.Images.Add(key, sized);
      return true;
    }

    private static Image ResizeToTreeIcon(Image source, Size size)
    {
      if (source.Width == size.Width && source.Height == size.Height) return source;
      var bitmap = new Bitmap(size.Width, size.Height);
      using (Graphics graphics = Graphics.FromImage(bitmap))
      {
        graphics.Clear(Color.Transparent);
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        graphics.DrawImage(source, new Rectangle(0, 0, size.Width, size.Height));
      }
      return bitmap;
    }

    private void ApplySearchButtonIcons()
    {
      ApplyButtonImage(_btnTreeSearchPrev, "Up.png");
      ApplyButtonImage(_btnTreeSearchNext, "Down.png");
      ApplyButtonImage(_btnTreeSearchClear, "Erase.png");
    }

    private static void ApplyButtonImage(Button button, string fileName)
    {
      Image image = TryLoadMenuBitmap(fileName);
      if (image == null) return;
      button.Image = image;
      button.Text = string.Empty;
    }

    private static Icon TryLoadFormIcon()
    {
      try
      {
        string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string path = Path.Combine(dir ?? string.Empty, "icons", "Graf.png");
        if (!File.Exists(path)) return null;
        using (var bitmap = new Bitmap(path))
        {
          IntPtr handle = bitmap.GetHicon();
          using (Icon icon = Icon.FromHandle(handle)) return (Icon)icon.Clone();
        }
      }
      catch { return null; }
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
