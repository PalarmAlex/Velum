using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;
using Velum.ReactiveCore;
using Velum.ReactiveCore.Export;
using Velum.SolidHomeostasis;
using Velum.UI.AssemblyRegistry;
using Xarial.XCad.SolidWorks;

namespace Velum.UI
{
  /// <summary>Пакетная диагностика и headless-экспорт DXF.</summary>
  internal sealed partial class VelumDxfBatchDiagnosticsForm : Form
  {
    private const string StatusFilterAll = "Все статусы";

    private readonly ISwApplication _swApp;
    private readonly List<VelumDxfBatchDiagnosticRow> _rows = new List<VelumDxfBatchDiagnosticRow>();
    private readonly List<string> _orphanDxfFiles = new List<string>();
    private IReadOnlyList<VelumAssemblyRegistryComponent> _components;
    private bool _cancelRequested;
    private bool _suppressListSelectionSync;
    private bool _suppressStatusFilterEvents;
    private ContextMenuStrip _listContextMenu;
    private string _assemblyFolder = string.Empty;
    private bool _fromActiveAssembly;
    private readonly bool _bomMode;
    private bool _enableAssemblyQuantity;
    private Button _btnDiagnose;
    private int _sortColumn = -1;
    private SortOrder _sortOrder = SortOrder.None;

    public VelumDxfBatchDiagnosticsForm(ISwApplication swApp)
        : this(swApp, null)
    {
    }

    public VelumDxfBatchDiagnosticsForm(ISwApplication swApp, VelumDxfBomLaunchContext bomContext)
    {
      _swApp = swApp;
      _bomMode = bomContext != null;
      InitializeComponent();
      InitializeDiagnoseButton();
      VelumFormHelp.Bind(this, VelumHelpTopics.DxfBatch);
      InitializeRuntime();
      if (bomContext != null)
        ApplyBomLaunchContext(bomContext);
    }

    private void ApplyBomLaunchContext(VelumDxfBomLaunchContext context)
    {
      _assemblyFolder = (context.AssemblyFolder ?? string.Empty).Trim();
      if (string.IsNullOrWhiteSpace(_assemblyFolder))
        _assemblyFolder = TryReadActiveAssemblyFolder();
      _fromActiveAssembly = !string.IsNullOrWhiteSpace(_assemblyFolder);
      _components = context.Components;

      // Каталог выгрузки всегда пустой при открытии — без подстановки из настроек.
      VelumBatchFormFolderBootstrap.ApplyFolderOrHint(_dxfFolderBox, string.Empty, null);

      _enableAssemblyQuantity = context != null && context.EnableAssemblyQuantity;
      ApplyAssemblyQuantityUiVisibility();
      SetProductQuantity(context != null ? context.ProductQuantity : 1);
      SyncExportOptionsEnabled();

      _rows.Clear();
      _orphanDxfFiles.Clear();
      if (context.Rows != null)
      {
        for (int i = 0; i < context.Rows.Count; i++)
        {
          if (context.Rows[i] != null)
            _rows.Add(context.Rows[i]);
        }
      }

      VelumDxfBatchDiagnosticsSession.BeginDiagnostics(_rows, _orphanDxfFiles);
      BindListView();
      UpdateStatusLabel();
      Text = "Пакетный DXF";
    }

    private void InitializeRuntime()
    {
      Icon icon = TryLoadVelumWindowIcon();
      if (icon != null)
        Icon = icon;

      if (!_bomMode)
      {
        VelumBatchFormFolderBootstrap.DxfFolders folders = VelumBatchFormFolderBootstrap.ResolveDxfFolders(_swApp);
        ApplyResolvedFolders(folders);
      }

      // Поля суффиксов/имени файла убраны: пакетная выгрузка использует имя из свойства детали.
      _patternRow.Visible = false;
      _resolvedSuffixesRow.Visible = false;

      InitializeStatusFilter();
      InitializeListViewInteractions();
      SyncExportOptionsEnabled();
      UpdateStatusLabel();
    }

    private void InitializeStatusFilter()
    {
      _suppressStatusFilterEvents = true;
      try
      {
        _cmbStatusFilter.Items.Clear();
        _cmbStatusFilter.Items.Add(StatusFilterAll);
        _cmbStatusFilter.SelectedIndex = 0;
      }
      finally
      {
        _suppressStatusFilterEvents = false;
      }

      _cmbStatusFilter.SelectedIndexChanged += OnStatusFilterChanged;
    }

    private void OnStatusFilterChanged(object sender, EventArgs e)
    {
      if (_suppressStatusFilterEvents)
        return;
      BindListView();
    }

    private void InitializeDiagnoseButton()
    {
      _btnDiagnose = new Button
      {
        Text = "Диагностика",
        Dock = DockStyle.Fill,
        AutoSize = true,
        MinimumSize = new Size(110, 24),
        Margin = new Padding(3),
        UseVisualStyleBackColor = true
      };
      _btnDiagnose.Click += OnDiagnoseSelected;

      _actionsRow.SuspendLayout();
      _actionsRow.Controls.Remove(_btnExport);
      _actionsRow.Controls.Remove(_btnDeleteOrphans);
      _actionsRow.Controls.Remove(_btnStop);
      _actionsRow.Controls.Remove(_btnClose);
      _actionsRow.ColumnCount = 6;
      _actionsRow.ColumnStyles.Clear();
      _actionsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
      _actionsRow.ColumnStyles.Add(new ColumnStyle());
      _actionsRow.ColumnStyles.Add(new ColumnStyle());
      _actionsRow.ColumnStyles.Add(new ColumnStyle());
      _actionsRow.ColumnStyles.Add(new ColumnStyle());
      _actionsRow.ColumnStyles.Add(new ColumnStyle());
      _actionsRow.Controls.Add(_btnDiagnose, 1, 0);
      _actionsRow.Controls.Add(_btnExport, 2, 0);
      _actionsRow.Controls.Add(_btnDeleteOrphans, 3, 0);
      _actionsRow.Controls.Add(_btnStop, 4, 0);
      _actionsRow.Controls.Add(_btnClose, 5, 0);
      _actionsRow.ResumeLayout();
      ApplyActionTooltips();
    }

    private void ApplyActionTooltips()
    {
      var tip = new ToolTip
      {
        AutoPopDelay = 12000,
        InitialDelay = 400,
        ReshowDelay = 200,
        ShowAlways = true
      };
      tip.SetToolTip(_btnDiagnose,
          "Проверить статусы DXF по штампам геометрии и свойствам (без выгрузки). " +
          "При указанном каталоге выгрузки — также файлы «- N шт».");
      tip.SetToolTip(_btnExport, "Выгрузить выделенные строки в каталог выдачи (в т.ч. со статусом «Не проверено»).");
      tip.SetToolTip(_btnDeleteOrphans, "Удалить выделенные сироты и мусорные DXF (после диагностики).");
      tip.SetToolTip(_btnStop, "Прервать текущую диагностику или экспорт.");
      tip.SetToolTip(_btnClose, "Закрыть форму.");
      tip.SetToolTip(_btnBrowseDxf, "Выбрать каталог выдачи. Дерево открывается в корне активной сборки.");
      if (_chkThicknessSubfolders != null)
      {
        tip.SetToolTip(
            _chkThicknessSubfolders,
            "Группировать копии DXF в субкаталоги по свойству «Толщина» (1,5 / 2 / 5…). " +
            "Без числа или с битой ссылкой — в каталог «Прочее».");
      }
      if (_chkAddAssemblyQuantity != null)
      {
        tip.SetToolTip(
            _chkAddAssemblyQuantity,
            "Добавить « - N шт» к имени копии в каталоге выдачи. " +
            "N = кол-во по сборке × «Кол-во изделия». " +
            "Нельзя выгружать с кол-вом в каталог, где уже лежат базовые DXF из списка.");
      }
      if (_nudProductQuantity != null)
      {
        tip.SetToolTip(
            _nudProductQuantity,
            "Множитель изделия: при «Добавить кол-во по сборке» в имя файла идёт " +
            "глобальное кол-во компонента × это значение.");
      }
    }

    private void ApplyResolvedFolders(VelumBatchFormFolderBootstrap.DxfFolders folders)
    {
      _fromActiveAssembly = folders != null && folders.FromActiveAssembly;
      _assemblyFolder = folders?.AssemblyFolder ?? string.Empty;
      _enableAssemblyQuantity = _bomMode
          ? _enableAssemblyQuantity
          : _fromActiveAssembly;
      ApplyAssemblyQuantityUiVisibility();

      // Каталог выгрузки не подставляем из настроек / last-used.
      VelumBatchFormFolderBootstrap.ApplyFolderOrHint(
          _dxfFolderBox,
          string.Empty,
          null);
      SyncExportOptionsEnabled();
    }

    private void ApplyAssemblyQuantityUiVisibility()
    {
      if (_chkAddAssemblyQuantity != null)
      {
        _chkAddAssemblyQuantity.Visible = _enableAssemblyQuantity;
        if (!_enableAssemblyQuantity)
          _chkAddAssemblyQuantity.Checked = false;
      }

      if (_lblProductQuantity != null)
        _lblProductQuantity.Visible = _enableAssemblyQuantity;
      if (_nudProductQuantity != null)
        _nudProductQuantity.Visible = _enableAssemblyQuantity;
    }

    private void OnDxfFolderTextChanged(object sender, EventArgs e)
    {
      SyncExportOptionsEnabled();
    }

    private void SyncExportOptionsEnabled()
    {
      bool hasFolder = !string.IsNullOrWhiteSpace(
          VelumBatchFormFolderBootstrap.ReadFolderPath(_dxfFolderBox));
      if (_chkThicknessSubfolders != null)
        _chkThicknessSubfolders.Enabled = hasFolder;
      if (_chkAddAssemblyQuantity != null)
        _chkAddAssemblyQuantity.Enabled = hasFolder && _enableAssemblyQuantity;
    }

    private void SetProductQuantity(int value)
    {
      if (_nudProductQuantity == null)
        return;
      int qty = value < 1 ? 1 : value;
      if (qty > (int)_nudProductQuantity.Maximum)
        qty = (int)_nudProductQuantity.Maximum;
      _nudProductQuantity.Value = qty;
    }

    private int ReadProductQuantity()
    {
      if (_nudProductQuantity == null)
        return 1;
      int qty = (int)_nudProductQuantity.Value;
      return qty < 1 ? 1 : qty;
    }

    private int GetEffectiveDeliveryQuantity(VelumDxfBatchDiagnosticRow row)
    {
      if (row == null || !row.Quantity.HasValue || row.Quantity.Value <= 0)
        return 0;
      return row.Quantity.Value * ReadProductQuantity();
    }

    private string TryReadActiveAssemblyFolder()
    {
      try
      {
        ModelDoc2 active = _swApp?.Sw?.IActiveDoc2 as ModelDoc2;
        if (active == null ||
            active.GetType() != (int)SolidWorks.Interop.swconst.swDocumentTypes_e.swDocASSEMBLY)
          return string.Empty;
        return Path.GetDirectoryName(active.GetPathName() ?? string.Empty) ?? string.Empty;
      }
      catch
      {
        return string.Empty;
      }
    }

    private void InitializeListViewInteractions()
    {
      _listView.DoubleClick += OnListViewDoubleClick;
      _listView.MouseUp += OnListViewMouseUp;
      _listView.ColumnClick += OnListColumnClick;

      Image selectAllIcon = TryLoadMenuBitmap("editselectall.png");

      _listContextMenu = new ContextMenuStrip();
      _listContextMenu.Items.Add(
          new ToolStripMenuItem("Выбрать все", selectAllIcon, (s, e) => SelectAllListItems()));
      _listContextMenu.Items.Add(new ToolStripSeparator());

      var openPartItem = new ToolStripMenuItem("Открыть деталь", TryLoadMenuBitmap("Modify.png"));
      openPartItem.Click += (s, e) => OnOpenPartFromList();
      _listContextMenu.Items.Add(openPartItem);

      var disableExportItem = new ToolStripMenuItem("Отключить экспорт", TryLoadMenuBitmap("No.png"));
      disableExportItem.Click += (s, e) => OnDisableExportFromList();
      _listContextMenu.Items.Add(disableExportItem);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
      if (VelumDxfBatchDiagnosticsSession.HasProblems() && e.CloseReason == CloseReason.UserClosing)
      {
        DialogResult answer = MessageBox.Show(
            "Остались проблемные детали. Закрыть?",
            "Пакетная диагностика DXF",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (answer != DialogResult.Yes)
        {
          e.Cancel = true;
          return;
        }
      }

      VelumDxfBatchDiagnosticsSession.DisposeSession();
      base.OnFormClosing(e);
    }

    private void OnBrowseDxfFolder(object sender, EventArgs e)
    {
      BrowseFolder(_dxfFolderBox, "Каталог DXF");
    }

    private void OnDiagnoseSelected(object sender, EventArgs e)
    {
      if (_components == null || _components.Count == 0)
      {
        MessageBox.Show(
            "Нет состава сборки для диагностики.",
            "Пакетная диагностика DXF",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      string dxfRoot = VelumBatchFormFolderBootstrap.ReadFolderPath(_dxfFolderBox);
      BeginOperation();
      _progressBar.Minimum = 0;
      _progressBar.Maximum = Math.Max(1, _components.Count);
      _progressBar.Value = 0;
      _progressBar.Visible = true;
      _lblProgress.Visible = true;
      _lblProgress.Text = "Диагностика…";

      VelumAssemblyRegistryBomDiagnostics.DxfResult result;
      try
      {
        result = VelumAssemblyRegistryBomDiagnostics.RunDxf(
            _swApp,
            _components,
            dxfRoot,
            () => _cancelRequested,
            (current, total, status) =>
            {
              _progressBar.Maximum = Math.Max(1, total);
              _progressBar.Value = Math.Max(0, Math.Min(current, _progressBar.Maximum));
              _lblProgress.Text = status ?? "Диагностика…";
              Application.DoEvents();
            },
            ReadProductQuantity());
      }
      finally
      {
        EndOperation();
        ResetExportProgressUi();
      }

      if (_cancelRequested)
        return;

      _rows.Clear();
      _orphanDxfFiles.Clear();
      for (int i = 0; i < result.Rows.Count; i++)
      {
        if (result.Rows[i] != null)
          _rows.Add(result.Rows[i]);
      }

      VelumDxfBatchDiagnosticsSession.ReplaceRows(_rows, _orphanDxfFiles);
      BindListView();
      UpdateStatusLabel();
      VelumSolidProbeRefreshPlanner.MarkExportDocumentationStale();
    }

    private void OnExportSelected(object sender, EventArgs e)
    {
      if (!VelumDxfBatchDiagnosticsSession.IsActive)
      {
        MessageBox.Show(
            "Нет строк для экспорта.",
            "Пакетная диагностика DXF",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      var selected = new List<VelumDxfBatchDiagnosticRow>();
      for (int i = 0; i < _rows.Count; i++)
      {
        VelumDxfBatchDiagnosticRow row = _rows[i];
        if (row.Selected && IsExportableStatus(row.Status))
          selected.Add(row);
      }

      if (selected.Count == 0)
      {
        MessageBox.Show(
            "Выделите строки со статусом «Нужен экспорт», «Устарел», «Нет/устарел файл с кол-вом», «Ручной» или «Не проверено».\n" +
            "«Первая выгрузка!» и «Нет плоскости» в пакет не входят — сделайте вручную через диалог экспорта DXF.\n" +
            "«Не проверено» — можно сразу выгрузить без диагностики.",
            "Пакетная диагностика DXF",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      string dxfRoot = VelumBatchFormFolderBootstrap.ReadFolderPath(_dxfFolderBox);
      bool hasDeliveryFolder = !string.IsNullOrWhiteSpace(dxfRoot);
      if (!hasDeliveryFolder)
      {
        DialogResult answer = MessageBox.Show(
            "Не указан каталог выгрузки DXF, будут обновлены только базовые файлы DXF. Продолжить?",
            "Пакетная диагностика DXF",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes)
          return;
      }
      else if (!Directory.Exists(dxfRoot))
      {
        MessageBox.Show(
            "Укажите существующий каталог выгрузки DXF.",
            "Пакетная диагностика DXF",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return;
      }

      bool addQty = hasDeliveryFolder &&
          _enableAssemblyQuantity &&
          _chkAddAssemblyQuantity != null &&
          _chkAddAssemblyQuantity.Checked;
      bool thicknessSubfolders = hasDeliveryFolder &&
          _chkThicknessSubfolders != null &&
          _chkThicknessSubfolders.Checked;
      if (hasDeliveryFolder)
      {
        VelumDxfDeliveryFolderGuard.CheckResult guard = VelumDxfDeliveryFolderGuard.Evaluate(
            dxfRoot,
            selected,
            row => BuildDeliveryBaseName(row, row.DxfPath),
            addQty,
            thicknessSubfolders);
        if (guard.Blocked)
        {
          MessageBox.Show(
              guard.BlockMessage,
              "Пакетная диагностика DXF",
              MessageBoxButtons.OK,
              MessageBoxIcon.Warning);
          return;
        }

        if (guard.NeedsOverwriteConfirm)
        {
          DialogResult overwriteAnswer = MessageBox.Show(
              guard.OverwriteMessage,
              "Пакетная диагностика DXF",
              MessageBoxButtons.YesNo,
              MessageBoxIcon.Warning,
              MessageBoxDefaultButton.Button2);
          if (overwriteAnswer != DialogResult.Yes)
            return;
        }
      }

      _progressBar.Minimum = 0;
      _progressBar.Maximum = selected.Count;
      _progressBar.Value = 0;
      _progressBar.Visible = true;
      _lblProgress.Visible = true;

      int exported = 0;
      int failed = 0;
      int progress = 0;
      var failureSummaries = new List<string>();
      var rowsByPart = new Dictionary<string, List<VelumDxfBatchDiagnosticRow>>(StringComparer.OrdinalIgnoreCase);
      for (int i = 0; i < selected.Count; i++)
      {
        VelumDxfBatchDiagnosticRow row = selected[i];
        List<VelumDxfBatchDiagnosticRow> bucket;
        if (!rowsByPart.TryGetValue(row.PartPath, out bucket))
        {
          bucket = new List<VelumDxfBatchDiagnosticRow>();
          rowsByPart[row.PartPath] = bucket;
        }

        bucket.Add(row);
      }

      try
      {
        HashSet<string> keepOpenPartPaths = VelumDxfBatchDocumentHelper.CollectOpenPartPaths(_swApp);

        BeginOperation();
        foreach (KeyValuePair<string, List<VelumDxfBatchDiagnosticRow>> partGroup in rowsByPart)
        {
          if (_cancelRequested)
            break;

          List<VelumDxfBatchDiagnosticRow> partRows = partGroup.Value;
          if (partRows.Count == 0)
            continue;

          ModelDoc2 modelDoc = VelumDxfBatchDocumentHelper.TryOpenPartForExport(
              _swApp,
              partGroup.Key,
              out string openError);
          if (modelDoc == null)
          {
            string message = string.IsNullOrWhiteSpace(openError)
                ? "Не удалось открыть деталь."
                : openError;
            for (int i = 0; i < partRows.Count; i++)
            {
              VelumDxfBatchDiagnosticRow row = partRows[i];
              row.Status = VelumDxfBatchRowStatus.Error;
              row.StatusText = "Ошибка: " + message;
              failed++;
              AddFailureSummary(failureSummaries, row, message);
            }

            continue;
          }

          if (VelumDxfPartGeometryHelper.IsEmptyDocument(modelDoc))
          {
            for (int i = 0; i < partRows.Count; i++)
            {
              VelumDxfBatchDiagnosticRow row = partRows[i];
              row.Status = VelumDxfBatchRowStatus.EmptyDocument;
              row.StatusText = "Пустой документ";
              failed++;
              AddFailureSummary(failureSummaries, row, "Пустой документ");
            }

            VelumDxfBatchDocumentHelper.TryReleasePartAfterBatch(
                _swApp,
                modelDoc,
                partGroup.Key,
                persistChanges: false,
                keepOpenPartPaths);
            continue;
          }

          var exportedConfigs = new List<string>();
          try
          {
            for (int i = 0; i < partRows.Count; i++)
            {
              if (_cancelRequested)
                break;

              VelumDxfBatchDiagnosticRow row = partRows[i];
              progress++;
              _lblProgress.Text = "Экспорт: " + row.PartDisplayName + " / " + row.ConfigName +
                                  " (" + progress + "/" + selected.Count + ")";
              _progressBar.Value = progress;
              Application.DoEvents();

              string partCatalog = VelumDxfBatchDocumentHelper.TryReadDxfCatalog(modelDoc);
              string canonicalFolder = partCatalog;
              if (string.IsNullOrWhiteSpace(canonicalFolder) || !Directory.Exists(canonicalFolder))
              {
                row.Status = VelumDxfBatchRowStatus.CatalogUnavailable;
                row.StatusText = "Каталог из свойства «Путь dxf» недоступен.";
                row.Selected = false;
                failed++;
                AddFailureSummary(failureSummaries, row, row.StatusText);
                continue;
              }

              string deliveryBaseName = BuildDeliveryBaseName(row, row.DxfPath);
              string deliveryFolder = string.Empty;
              if (hasDeliveryFolder)
              {
                deliveryFolder = ResolveThicknessDeliveryFolder(
                    dxfRoot,
                    modelDoc,
                    row.ConfigName,
                    thicknessSubfolders,
                    out string deliveryFolderError);
                if (string.IsNullOrWhiteSpace(deliveryFolder))
                {
                  row.Status = VelumDxfBatchRowStatus.ExportedNotDelivered;
                  row.StatusText = "Каталог выдачи недоступен: " +
                      (string.IsNullOrWhiteSpace(deliveryFolderError)
                          ? "не удалось создать субкаталог."
                          : deliveryFolderError);
                  row.Selected = false;
                  failed++;
                  AddFailureSummary(failureSummaries, row, row.StatusText);
                  continue;
                }
              }

              if (row.Status == VelumDxfBatchRowStatus.QuantityOutdated)
              {
                if (!hasDeliveryFolder)
                  continue;

                string deliveryError = string.Empty;
                bool canDeliver = true;
                if (canDeliver &&
                    !string.IsNullOrWhiteSpace(row.DxfPath) &&
                    File.Exists(row.DxfPath) &&
                    VelumExportDeliveryHelper.TryCopyToDelivery(
                        row.DxfPath,
                        deliveryFolder,
                        deliveryBaseName,
                        ".dxf",
                        out _,
                        out deliveryError))
                {
                  exported++;
                  _rows.Remove(row);
                }
                else
                {
                  row.Status = VelumDxfBatchRowStatus.ExportedNotDelivered;
                  row.StatusText = "Файл с кол-вом не доставлен: " +
                      (string.IsNullOrWhiteSpace(deliveryError) ? "нет канонического DXF." : deliveryError);
                  row.Selected = false;
                  failed++;
                  AddFailureSummary(failureSummaries, row, row.StatusText);
                }
                continue;
              }

              VelumDxfProjectionView projectionView = VelumDxfProjectionView.Front;
              bool isSheetMetal = VelumSolidSheetMetalHelper.IsSheetMetalPart(modelDoc) &&
                  VelumSolidSheetMetalHelper.TryFindFlatPatternFeature(modelDoc) != null;
              if (!isSheetMetal &&
                  !VelumDxfFileNameHelper.TryGetProjectionViewProperty(
                      modelDoc,
                      row.ConfigName,
                      out projectionView))
              {
                row.Status = VelumDxfBatchRowStatus.MissingProjection;
                row.StatusText = "Нет плоскости";
                row.Selected = false;
                failed++;
                AddFailureSummary(failureSummaries, row, row.StatusText);
                continue;
              }

              var request = new VelumDxfExportService.ExportRequest
              {
                ModelDoc = modelDoc,
                CanonicalFolder = canonicalFolder,
                DeliveryFolder = deliveryFolder,
                DeliveryFileName = deliveryBaseName,
                FileNamePattern = string.Empty,
                // Пакет: имя канонического DXF берётся из свойства «Имя файла dxf» и не перезаписывается.
                ReadOnlyFileNameProperty = true,
                ConfigName = row.ConfigName,
                ProjectionView = projectionView,
                TemplateContext = VelumDxfFileNameHelper.BuildTemplateContext(modelDoc)
              };

              VelumDxfExportService.ExportResult exportResult = VelumDxfExportService.TryExport(request);
              if (exportResult.Success)
              {
                exported++;
                exportedConfigs.Add(row.ConfigName);
                if (!exportResult.Delivered)
                {
                  row.Status = VelumDxfBatchRowStatus.ExportedNotDelivered;
                  row.StatusText = "Экспортирован, но не доставлен: " + exportResult.DeliveryError;
                  row.Selected = false;
                  failed++;
                  AddFailureSummary(failureSummaries, row, row.StatusText);
                }
                else
                  _rows.Remove(row);
              }
              else
              {
                if (exportResult.IsEmptyDocument)
                {
                  row.Status = VelumDxfBatchRowStatus.EmptyDocument;
                  row.StatusText = "Пустой документ";
                }
                else if (exportResult.IsFirstExport)
                {
                  // Пустое свойство «Имя файла dxf»: пакет не выгружает, нужна первая выгрузка вручную.
                  row.Status = VelumDxfBatchRowStatus.FirstExport;
                  row.StatusText = "Первая выгрузка";
                  row.Selected = false;
                }
                else
                {
                  row.Status = VelumDxfBatchRowStatus.Error;
                  row.StatusText = "Ошибка: " + exportResult.Message;
                }

                failed++;
                AddFailureSummary(failureSummaries, row, row.StatusText);
              }
            }

            if (exportedConfigs.Count > 0)
            {
              string catalog = VelumDxfBatchDocumentHelper.TryReadDxfCatalog(modelDoc);
              IReadOnlyList<string> configsToResync =
                  VelumDxfBatchScanner.CollectConfigsWithDxfFiles(modelDoc, catalog);
              if (configsToResync.Count == 0)
                configsToResync = exportedConfigs;

              // Save поднимает GetUpdateStamp — штампы надо дожать после Save, иначе «Устарел».
              VelumDxfBatchDocumentHelper.TryPersistAndResyncDxfExportStamps(
                  modelDoc,
                  configsToResync);

              VelumDxfBatchScanner.RefreshPartRows(modelDoc, partGroup.Key, dxfRoot, _rows);
            }
          }
          finally
          {
            VelumDxfBatchDocumentHelper.TryReleasePartAfterBatch(
                _swApp,
                modelDoc,
                partGroup.Key,
                persistChanges: false,
                keepOpenPartPaths);
          }
        }
      }
      finally
      {
        EndOperation();
        ResetExportProgressUi();
      }

      VelumDxfBatchDiagnosticsSession.ReplaceRows(_rows, _orphanDxfFiles);
      BindListView();
      UpdateStatusLabel();
      VelumSolidProbeRefreshPlanner.MarkExportDocumentationStale();

      int skipped = selected.Count - exported - failed;
      if (skipped < 0)
        skipped = 0;

      string completionMessage = _cancelRequested
          ? "Экспорт остановлен. Экспортировано строк: " + exported
          : "Экспортировано строк: " + exported;
      if (failed > 0)
      {
        completionMessage += System.Environment.NewLine +
            "Не экспортировано (или не доставлено): " + failed +
            ". См. статусы в списке.";
        completionMessage += FormatFailureSummaryBlock(failureSummaries);
      }

      if (skipped > 0)
      {
        completionMessage += System.Environment.NewLine +
            "Не обработано (остановка): " + skipped + ".";
      }

      bool hasProblems = failed > 0 || skipped > 0;
      MessageBoxIcon completionIcon = hasProblems
          ? MessageBoxIcon.Warning
          : MessageBoxIcon.Information;
      MessageBox.Show(
          completionMessage,
          "Пакетная диагностика DXF",
          MessageBoxButtons.OK,
          completionIcon);
    }

    private void OnDeleteOrphans(object sender, EventArgs e)
    {
      if (!VelumDxfBatchDiagnosticsSession.IsActive)
      {
        MessageBox.Show(
            "Сначала выполните диагностику.",
            "Пакетная диагностика DXF",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      var filesToDelete = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      var junkRowsToRemove = new List<VelumDxfBatchDiagnosticRow>();

      for (int i = 0; i < _listView.SelectedItems.Count; i++)
      {
        ListViewItem listItem = _listView.SelectedItems[i];
        var item = listItem.Tag as VelumDxfBatchListItem;
        if (item == null)
          continue;

        if (item.IsOrphan)
        {
          if (!string.IsNullOrWhiteSpace(item.OrphanDxfPath))
            filesToDelete.Add(item.OrphanDxfPath);
        }
        else if (item.Row != null && item.Row.Status == VelumDxfBatchRowStatus.Junk)
        {
          if (!string.IsNullOrWhiteSpace(item.Row.DxfPath))
            filesToDelete.Add(item.Row.DxfPath);
          junkRowsToRemove.Add(item.Row);
        }
      }

      if (filesToDelete.Count == 0)
      {
        MessageBox.Show(
            "Мусорные DXF — это:\n" +
            "• файлы в каталоге DXF без соответствующей детали SLDPRT;\n" +
            "• DXF при «Нужен dxf = Нет» (статус «Мусорный DXF»).\n\n" +
            "Статус «Устарел» — не мусор: выделите такие строки и используйте «Экспортировать выделенные».\n\n" +
            "Выделите в списке строки «Мусорный DXF» или сироты без детали.",
            "Пакетная диагностика DXF",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      DialogResult answer = MessageBox.Show(
          "Удалить выделенные мусорные DXF (" + filesToDelete.Count + ")?\n\n" +
          "Будут удалены только файлы на диске; свойства SLDPRT не изменяются.",
          "Пакетная диагностика DXF",
          MessageBoxButtons.YesNo,
          MessageBoxIcon.Warning);
      if (answer != DialogResult.Yes)
        return;

      int deleted = 0;
      foreach (string path in filesToDelete)
      {
        try
        {
          if (File.Exists(path))
          {
            File.Delete(path);
            deleted++;
          }
        }
        catch
        {
        }
      }

      for (int i = 0; i < junkRowsToRemove.Count; i++)
        _rows.Remove(junkRowsToRemove[i]);

      for (int i = _orphanDxfFiles.Count - 1; i >= 0; i--)
      {
        if (filesToDelete.Contains(_orphanDxfFiles[i]))
          _orphanDxfFiles.RemoveAt(i);
      }

      VelumDxfBatchDiagnosticsSession.ReplaceRows(_rows, _orphanDxfFiles);
      BindListView();
      UpdateStatusLabel();

      MessageBox.Show(
          "Удалено файлов: " + deleted,
          "Пакетная диагностика DXF",
          MessageBoxButtons.OK,
          MessageBoxIcon.Information);
    }

    private void BindListView()
    {
      RefreshStatusFilterItems();

      string statusFilter = _cmbStatusFilter.SelectedItem as string ?? StatusFilterAll;
      bool filterAll = string.Equals(statusFilter, StatusFilterAll, StringComparison.Ordinal);

      _suppressListSelectionSync = true;
      _listView.BeginUpdate();
      try
      {
        _listView.Items.Clear();
        int visibleCount = 0;

        for (int i = 0; i < _orphanDxfFiles.Count; i++)
        {
          string path = _orphanDxfFiles[i];
          const string orphanStatus = "Сирота (нет детали SLDPRT)";
          if (!filterAll && !string.Equals(statusFilter, orphanStatus, StringComparison.Ordinal))
            continue;

          var tag = new VelumDxfBatchListItem
          {
            OrphanDxfPath = path,
            Selected = true
          };
          var listItem = new ListViewItem(string.Empty)
          {
            Tag = tag
          };
          listItem.SubItems.Add(string.Empty);
          listItem.SubItems.Add(Path.GetFileName(path));
          listItem.SubItems.Add(orphanStatus);
          _listView.Items.Add(listItem);
          listItem.Selected = true;
          visibleCount++;
        }

        for (int i = 0; i < _rows.Count; i++)
        {
          VelumDxfBatchDiagnosticRow row = _rows[i];
          string statusText = row.StatusText ?? string.Empty;
          if (!filterAll && !string.Equals(statusFilter, statusText, StringComparison.Ordinal))
            continue;

          var tag = new VelumDxfBatchListItem
          {
            Row = row,
            Selected = row.Selected
          };
          var listItem = new ListViewItem(row.PartDisplayName)
          {
            Tag = tag
          };
          listItem.SubItems.Add(row.ConfigName);
          listItem.SubItems.Add(FormatDxfFileName(row.DxfPath));
          listItem.SubItems.Add(statusText);
          _listView.Items.Add(listItem);
          listItem.Selected = row.Selected;
          visibleCount++;
        }

        if (_sortColumn >= 0 && _sortOrder != SortOrder.None)
        {
          _listView.ListViewItemSorter = new ListViewTextComparer(_sortColumn, _sortOrder);
          _listView.Sort();
        }

        _lblRecordCount.Text = "Строк: " + visibleCount;
      }
      finally
      {
        _listView.EndUpdate();
        _suppressListSelectionSync = false;
      }
    }

    private void RefreshStatusFilterItems()
    {
      string previous = _cmbStatusFilter.SelectedItem as string ?? StatusFilterAll;
      var statuses = new SortedSet<string>(StringComparer.Ordinal);
      for (int i = 0; i < _rows.Count; i++)
      {
        string text = _rows[i]?.StatusText;
        if (!string.IsNullOrWhiteSpace(text))
          statuses.Add(text);
      }

      if (_orphanDxfFiles.Count > 0)
        statuses.Add("Сирота (нет детали SLDPRT)");

      _suppressStatusFilterEvents = true;
      try
      {
        _cmbStatusFilter.Items.Clear();
        _cmbStatusFilter.Items.Add(StatusFilterAll);
        foreach (string status in statuses)
          _cmbStatusFilter.Items.Add(status);

        int index = _cmbStatusFilter.Items.IndexOf(previous);
        _cmbStatusFilter.SelectedIndex = index >= 0 ? index : 0;
      }
      finally
      {
        _suppressStatusFilterEvents = false;
      }
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

      _listView.ListViewItemSorter = new ListViewTextComparer(_sortColumn, _sortOrder);
      _listView.Sort();
    }

    private void SelectAllListItems()
    {
      if (_listView.Items.Count == 0)
        return;

      _suppressListSelectionSync = true;
      _listView.BeginUpdate();
      try
      {
        foreach (ListViewItem listItem in _listView.Items)
        {
          listItem.Selected = true;
          var tag = listItem.Tag as VelumDxfBatchListItem;
          if (tag == null)
            continue;
          tag.Selected = true;
          if (tag.Row != null)
            tag.Row.Selected = true;
        }
      }
      finally
      {
        _listView.EndUpdate();
        _suppressListSelectionSync = false;
      }
    }

    private void OnListViewItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
    {
      if (_suppressListSelectionSync)
        return;

      var item = e.Item.Tag as VelumDxfBatchListItem;
      if (item == null)
        return;

      item.Selected = e.IsSelected;
      if (item.Row != null)
        item.Row.Selected = e.IsSelected;
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
          while (_listView.SelectedItems.Count > 0)
            _listView.SelectedItems[0].Selected = false;
          hit.Selected = true;
        }
      }
      else if (_listView.Items.Count == 0)
      {
        return;
      }

      bool hasPartSelection = CollectSelectedPartPaths().Count > 0;
      for (int i = 0; i < _listContextMenu.Items.Count; i++)
      {
        ToolStripItem menuItem = _listContextMenu.Items[i];
        if (menuItem is ToolStripSeparator)
          continue;
        if (string.Equals(menuItem.Text, "Выбрать все", StringComparison.Ordinal))
        {
          menuItem.Enabled = _listView.Items.Count > 0;
          continue;
        }

        menuItem.Enabled = hasPartSelection;
      }

      _listContextMenu.Show(_listView, e.Location);
    }

    private void OnOpenPartFromList()
    {
      List<string> paths = CollectSelectedPartPaths();
      if (paths.Count == 0)
        return;

      var errors = new List<string>();
      for (int i = 0; i < paths.Count; i++)
      {
        string path = paths[i];
        if (!VelumDxfBatchDocumentHelper.TryActivateOrOpenPartVisible(_swApp, path, out string error))
        {
          errors.Add(
              Path.GetFileName(path) + ": " +
              (string.IsNullOrWhiteSpace(error) ? "Не удалось открыть деталь." : error));
        }
      }

      if (errors.Count > 0)
      {
        MessageBox.Show(
            string.Join(System.Environment.NewLine, errors),
            "Пакетная диагностика DXF",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
      }
    }

    private List<string> CollectSelectedPartPaths()
    {
      var paths = new List<string>();
      var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      for (int i = 0; i < _listView.SelectedItems.Count; i++)
      {
        var item = _listView.SelectedItems[i].Tag as VelumDxfBatchListItem;
        if (item == null || item.IsOrphan || item.Row == null)
          continue;

        string path = item.Row.PartPath;
        if (string.IsNullOrWhiteSpace(path) || !seen.Add(path))
          continue;

        paths.Add(path);
      }

      return paths;
    }

    private List<VelumDxfBatchDiagnosticRow> CollectSelectedRows()
    {
      var rows = new List<VelumDxfBatchDiagnosticRow>();
      for (int i = 0; i < _listView.SelectedItems.Count; i++)
      {
        var item = _listView.SelectedItems[i].Tag as VelumDxfBatchListItem;
        if (item == null || item.IsOrphan || item.Row == null)
          continue;
        if (string.IsNullOrWhiteSpace(item.Row.PartPath))
          continue;
        rows.Add(item.Row);
      }

      return rows;
    }

    private void OnDisableExportFromList()
    {
      List<VelumDxfBatchDiagnosticRow> selected = CollectSelectedRows();
      if (selected.Count == 0)
        return;

      string dxfRoot = VelumBatchFormFolderBootstrap.ReadFolderPath(_dxfFolderBox);
      HashSet<string> keepOpenPartPaths = VelumDxfBatchDocumentHelper.CollectOpenPartPaths(_swApp);
      var errors = new List<string>();
      bool anyChanged = false;

      for (int i = 0; i < selected.Count; i++)
      {
        VelumDxfBatchDiagnosticRow row = selected[i];
        string partPath = row.PartPath;
        ModelDoc2 modelDoc = VelumDxfBatchDocumentHelper.TryOpenPartSilent(_swApp, partPath, out string openError);
        if (modelDoc == null)
        {
          errors.Add(
              Path.GetFileName(partPath) + ": " +
              (string.IsNullOrWhiteSpace(openError) ? "Не удалось открыть деталь." : openError));
          continue;
        }

        try
        {
          if (!VelumDxfBatchDocumentHelper.TrySetNeedDxf(
                  modelDoc,
                  false,
                  row.ConfigName,
                  out string setError))
          {
            errors.Add(
                Path.GetFileName(partPath) + " / " + (row.ConfigName ?? "") + ": " +
                (string.IsNullOrWhiteSpace(setError) ? "Не удалось отключить экспорт." : setError));
            continue;
          }

          if (!VelumDxfBatchDocumentHelper.TrySavePartSilent(modelDoc, out string saveError))
          {
            errors.Add(
                Path.GetFileName(partPath) + ": " +
                (string.IsNullOrWhiteSpace(saveError) ? "Не удалось сохранить деталь." : saveError));
            continue;
          }

          VelumDxfBatchScanner.RefreshPartRows(modelDoc, partPath, dxfRoot, _rows);
          anyChanged = true;
        }
        finally
        {
          VelumDxfBatchDocumentHelper.TryReleasePartAfterBatch(
              _swApp,
              modelDoc,
              partPath,
              persistChanges: false,
              keepOpenPartPaths);
        }
      }

      if (anyChanged)
      {
        VelumDxfBatchDiagnosticsSession.ReplaceRows(_rows, _orphanDxfFiles);
        BindListView();
        UpdateStatusLabel();
        VelumSolidProbeRefreshPlanner.MarkExportDocumentationStale();
      }

      if (errors.Count > 0)
      {
        MessageBox.Show(
            string.Join(System.Environment.NewLine, errors),
            "Пакетная диагностика DXF",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
      }
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
      _btnExport.Enabled = false;
      _btnDeleteOrphans.Enabled = false;
      _btnBrowseDxf.Enabled = false;
      _btnSuffixes.Enabled = false;
      _btnClose.Enabled = false;
      _dxfFolderBox.Enabled = false;
      _patternBox.Enabled = false;
      _cmbStatusFilter.Enabled = false;
      _chkThicknessSubfolders.Enabled = false;
      _chkAddAssemblyQuantity.Enabled = false;
      if (_btnDiagnose != null)
        _btnDiagnose.Enabled = false;
    }

    /// <summary>
    /// Разблокирует все элементы управления формы после завершения длительной операции.
    /// </summary>
    private void EndOperation()
    {
      _btnStop.Enabled = false;
      _btnExport.Enabled = true;
      _btnDeleteOrphans.Enabled = true;
      _btnBrowseDxf.Enabled = true;
      _btnSuffixes.Enabled = true;
      _btnClose.Enabled = true;
      _dxfFolderBox.Enabled = true;
      _patternBox.Enabled = true;
      _cmbStatusFilter.Enabled = true;
      _chkThicknessSubfolders.Enabled = true;
      _chkAddAssemblyQuantity.Enabled = true;
      if (_btnDiagnose != null)
        _btnDiagnose.Enabled = true;
    }

    private void ResetExportProgressUi()
    {
      _progressBar.Value = 0;
      _progressBar.Visible = false;
      _lblProgress.Visible = false;
      _lblProgress.Text = "Экспорт:";
    }

    private void UpdateStatusLabel()
    {
      int uncheckedCount = 0;
      for (int i = 0; i < _rows.Count; i++)
      {
        if (_rows[i] != null && _rows[i].Status == VelumDxfBatchRowStatus.Unchecked)
          uncheckedCount++;
      }

      bool hasProblems = VelumDxfBatchDiagnosticsSession.HasProblems();
      string text;
      if (uncheckedCount > 0 && uncheckedCount == _rows.Count && _orphanDxfFiles.Count == 0)
        text = "Не проверено — можно выгрузить или запустить диагностику";
      else if (hasProblems)
        text = "Есть проблемные детали";
      else
        text = "Нет проблемных деталей";
      _statusLabel.Text = text;
      _statusLabel.ForeColor = hasProblems ? Color.DarkRed : Color.DarkGreen;
    }

    private static string FormatDxfFileName(string dxfPath)
    {
      return string.IsNullOrWhiteSpace(dxfPath) ? string.Empty : Path.GetFileName(dxfPath);
    }

    private static bool IsExportableStatus(VelumDxfBatchRowStatus status)
    {
      return status == VelumDxfBatchRowStatus.NeedExport ||
             status == VelumDxfBatchRowStatus.Outdated ||
             status == VelumDxfBatchRowStatus.QuantityOutdated ||
             status == VelumDxfBatchRowStatus.Unchecked;
    }

    private static void AddFailureSummary(
        List<string> summaries,
        VelumDxfBatchDiagnosticRow row,
        string reason)
    {
      if (summaries == null || row == null)
        return;

      const int maxItems = 12;
      if (summaries.Count >= maxItems)
        return;

      string part = string.IsNullOrWhiteSpace(row.PartDisplayName)
          ? (row.PartPath ?? string.Empty)
          : row.PartDisplayName;
      string config = string.IsNullOrWhiteSpace(row.ConfigName) ? string.Empty : row.ConfigName;
      string detail = string.IsNullOrWhiteSpace(reason) ? "ошибка экспорта" : reason.Trim();
      summaries.Add(string.IsNullOrWhiteSpace(config)
          ? "• " + part + " — " + detail
          : "• " + part + " / " + config + " — " + detail);
    }

    private static string FormatFailureSummaryBlock(List<string> summaries)
    {
      if (summaries == null || summaries.Count == 0)
        return string.Empty;

      var sb = new System.Text.StringBuilder();
      sb.AppendLine();
      sb.AppendLine();
      sb.Append("Примеры:");
      for (int i = 0; i < summaries.Count; i++)
      {
        sb.AppendLine();
        sb.Append(summaries[i]);
      }

      return sb.ToString();
    }

    private string BuildDeliveryBaseName(VelumDxfBatchDiagnosticRow row, string canonicalPath)
    {
      string baseName = Path.GetFileNameWithoutExtension(canonicalPath ?? string.Empty);
      if (string.IsNullOrWhiteSpace(baseName))
        baseName = row?.ExpectedDxfBaseName ?? string.Empty;
      baseName = VelumDxfQuantityToken.StripQuantitySuffix(baseName);

      int quantity = GetEffectiveDeliveryQuantity(row);
      bool forceQty = row != null && row.Status == VelumDxfBatchRowStatus.QuantityOutdated;
      if ((_enableAssemblyQuantity &&
           _chkAddAssemblyQuantity != null &&
           _chkAddAssemblyQuantity.Checked &&
           quantity > 0) ||
          (forceQty && quantity > 0))
        return baseName + " - " + quantity + " шт";

      return baseName;
    }

    private static string ResolveThicknessDeliveryFolder(
        string dxfRoot,
        ModelDoc2 modelDoc,
        string configName,
        bool thicknessSubfolders,
        out string error)
    {
      error = string.Empty;
      string folder = VelumDxfThicknessDeliveryFolder.Resolve(
          dxfRoot,
          modelDoc,
          configName,
          thicknessSubfolders);
      if (string.IsNullOrWhiteSpace(folder))
      {
        error = "delivery_folder_empty";
        return string.Empty;
      }

      if (!VelumDxfThicknessDeliveryFolder.TryEnsureDirectory(folder, out error))
        return string.Empty;

      return folder;
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

    private static Icon TryLoadVelumWindowIcon()
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

    private sealed class VelumDxfBatchListItem
    {
      internal VelumDxfBatchDiagnosticRow Row { get; set; }

      internal string OrphanDxfPath { get; set; }

      internal bool Selected { get; set; }

      internal bool IsOrphan => !string.IsNullOrWhiteSpace(OrphanDxfPath);
    }

    private sealed class ListViewTextComparer : System.Collections.IComparer
    {
      private readonly int _column;
      private readonly int _direction;

      internal ListViewTextComparer(int column, SortOrder order)
      {
        _column = column;
        _direction = order == SortOrder.Descending ? -1 : 1;
      }

      public int Compare(object x, object y)
      {
        var a = x as ListViewItem;
        var b = y as ListViewItem;
        if (a == null && b == null)
          return 0;
        if (a == null)
          return -1 * _direction;
        if (b == null)
          return 1 * _direction;

        string textA = GetSubItemText(a, _column);
        string textB = GetSubItemText(b, _column);
        int cmp = string.Compare(textA, textB, StringComparison.CurrentCultureIgnoreCase);
        return cmp * _direction;
      }

      private static string GetSubItemText(ListViewItem item, int column)
      {
        if (item == null || column < 0 || column >= item.SubItems.Count)
          return string.Empty;
        return item.SubItems[column].Text ?? string.Empty;
      }
    }

  }
}
