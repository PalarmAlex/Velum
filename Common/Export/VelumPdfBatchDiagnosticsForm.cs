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
using Velum.UI.ProductRegistry;
using Xarial.XCad.SolidWorks;

namespace Velum.UI
{
  /// <summary>Пакетная диагностика и headless-экспорт PDF.</summary>
  internal sealed partial class VelumPdfBatchDiagnosticsForm : Form
  {
    private const string StatusFilterAll = "Все статусы";

    private readonly ISwApplication _swApp;
    private readonly List<VelumPdfBatchDiagnosticRow> _rows = new List<VelumPdfBatchDiagnosticRow>();
    private readonly List<string> _orphanPdfFiles = new List<string>();
    private IReadOnlyList<VelumAssemblyRegistryComponent> _components;
    private bool _cancelRequested;
    private bool _suppressListSelectionSync;
    private bool _suppressStatusFilterEvents;
    private ContextMenuStrip _listContextMenu;
    private string _assemblyFolder = string.Empty;
    private readonly bool _bomMode;
    private Button _btnDiagnose;
    private int _sortColumn = -1;
    private SortOrder _sortOrder = SortOrder.None;

    public VelumPdfBatchDiagnosticsForm(ISwApplication swApp)
        : this(swApp, null)
    {
    }

    public VelumPdfBatchDiagnosticsForm(ISwApplication swApp, VelumPdfBomLaunchContext bomContext)
    {
      _swApp = swApp;
      _bomMode = bomContext != null;
      InitializeComponent();
      InitializeDiagnoseButton();
      VelumFormHelp.Bind(this, VelumHelpTopics.PdfBatch);
      InitializeRuntime();
      if (bomContext != null)
        ApplyBomLaunchContext(bomContext);
    }

    private void ApplyBomLaunchContext(VelumPdfBomLaunchContext context)
    {
      _assemblyFolder = (context.AssemblyFolder ?? string.Empty).Trim();
      if (string.IsNullOrWhiteSpace(_assemblyFolder))
        _assemblyFolder = TryReadActiveAssemblyFolder();
      _components = context.Components;

      VelumBatchFormFolderBootstrap.ApplyFolderOrHint(_pdfFolderBox, string.Empty, null);

      _rows.Clear();
      _orphanPdfFiles.Clear();
      if (context.Rows != null)
      {
        for (int i = 0; i < context.Rows.Count; i++)
        {
          if (context.Rows[i] != null)
            _rows.Add(context.Rows[i]);
        }
      }

      VelumPdfBatchDiagnosticsSession.BeginDiagnostics(_rows, _orphanPdfFiles);
      BindListView();
      UpdateStatusLabel();
      Text = "Пакетный PDF";
    }

    private void InitializeRuntime()
    {
      Icon icon = TryLoadVelumWindowIcon();
      if (icon != null)
        Icon = icon;

      if (!_bomMode)
      {
        VelumBatchFormFolderBootstrap.PdfFolders folders = VelumBatchFormFolderBootstrap.ResolvePdfFolders(_swApp);
        ApplyResolvedFolders(folders);
      }

      InitializeStatusFilter();
      InitializeListViewInteractions();
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
      tip.SetToolTip(_btnDiagnose, "Проверить статусы PDF по штампам чертежей (откроет чертежи).");
      tip.SetToolTip(_btnExport, "Выгрузить выделенные PDF в каталог выдачи (в т.ч. со статусом «Не проверено»).");
      tip.SetToolTip(_btnDeleteOrphans, "Удалить выделенные сироты и мусорные PDF (после диагностики).");
      tip.SetToolTip(_btnStop, "Прервать текущую диагностику или экспорт.");
      tip.SetToolTip(_btnClose, "Закрыть форму.");
      tip.SetToolTip(_btnBrowsePdf, "Выбрать каталог выдачи. Дерево открывается в корне активной сборки.");
    }

    private void ApplyResolvedFolders(VelumBatchFormFolderBootstrap.PdfFolders folders)
    {
      _assemblyFolder = folders?.AssemblyFolder ?? string.Empty;

      VelumBatchFormFolderBootstrap.ApplyFolderOrHint(
          _pdfFolderBox,
          string.Empty,
          null);
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

      var openDrawingItem = new ToolStripMenuItem("Открыть чертеж", TryLoadMenuBitmap("Modify.png"));
      openDrawingItem.Click += (s, e) => OnOpenDrawingFromList();
      _listContextMenu.Items.Add(openDrawingItem);

      _listContextMenu.Items.Add(new ToolStripSeparator());

      var disableExportItem = new ToolStripMenuItem("Отключить экспорт", TryLoadMenuBitmap("No.png"));
      disableExportItem.Click += (s, e) => OnDisableExportFromList();
      _listContextMenu.Items.Add(disableExportItem);

      var drawingNotNeededItem = new ToolStripMenuItem(
          "Не нужен чертеж",
          TryLoadMenuBitmap("Thumbs down.png"));
      drawingNotNeededItem.Click += (s, e) => OnDrawingNotNeededFromList();
      _listContextMenu.Items.Add(drawingNotNeededItem);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
      if (VelumPdfBatchDiagnosticsSession.HasProblems() && e.CloseReason == CloseReason.UserClosing)
      {
        DialogResult answer = MessageBox.Show(
            "Остались проблемные чертежи. Закрыть?",
            "Пакетная диагностика PDF",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (answer != DialogResult.Yes)
        {
          e.Cancel = true;
          return;
        }
      }

      VelumPdfBatchDiagnosticsSession.DisposeSession();
      base.OnFormClosing(e);
    }

    private void OnBrowsePdfFolder(object sender, EventArgs e)
    {
      BrowseFolder(_pdfFolderBox, "Каталог PDF");
    }

    private void OnDiagnoseSelected(object sender, EventArgs e)
    {
      if (_components == null || _components.Count == 0)
      {
        MessageBox.Show(
            "Нет состава сборки для диагностики.",
            "Пакетная диагностика PDF",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      string pdfRoot = VelumBatchFormFolderBootstrap.ReadFolderPath(_pdfFolderBox);
      BeginOperation();
      _progressBar.Minimum = 0;
      _progressBar.Maximum = Math.Max(1, _components.Count);
      _progressBar.Value = 0;
      _progressBar.Visible = true;
      _lblProgress.Visible = true;
      _lblProgress.Text = "Диагностика…";

      VelumAssemblyRegistryBomDiagnostics.PdfResult result;
      try
      {
        result = VelumAssemblyRegistryBomDiagnostics.RunPdf(
            _swApp,
            _components,
            pdfRoot,
            () => _cancelRequested,
            (current, total, status) =>
            {
              _progressBar.Maximum = Math.Max(1, total);
              _progressBar.Value = Math.Max(0, Math.Min(current, _progressBar.Maximum));
              _lblProgress.Text = status ?? "Диагностика…";
              Application.DoEvents();
            });
      }
      finally
      {
        EndOperation();
        _progressBar.Value = 0;
        _progressBar.Visible = false;
        _lblProgress.Visible = false;
        _lblProgress.Text = "Экспорт:";
      }

      if (_cancelRequested)
        return;

      _rows.Clear();
      _orphanPdfFiles.Clear();
      for (int i = 0; i < result.Rows.Count; i++)
      {
        if (result.Rows[i] != null)
          _rows.Add(result.Rows[i]);
      }

      VelumPdfBatchDiagnosticsSession.ReplaceRows(_rows, _orphanPdfFiles);
      BindListView();
      UpdateStatusLabel();
      VelumSolidProbeRefreshPlanner.MarkExportDocumentationStale();
    }

    private void OnExportSelected(object sender, EventArgs e)
    {
      if (!VelumPdfBatchDiagnosticsSession.IsActive)
      {
        MessageBox.Show(
            "Нет строк реестра для экспорта.",
            "Пакетная диагностика PDF",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      var selected = new List<VelumPdfBatchDiagnosticRow>();
      for (int i = 0; i < _rows.Count; i++)
      {
        VelumPdfBatchDiagnosticRow row = _rows[i];
        if (row.Selected && IsExportableStatus(row.Status))
          selected.Add(row);
      }

      if (selected.Count == 0)
      {
        MessageBox.Show(
            "Выделите строки со статусом «Нужен экспорт», «Устарел» или «Не проверено».\n" +
            "Статус «Устарел» — файл есть, но чертёж изменился; его нужно переэкспортировать, а не удалять.\n" +
            "«Не проверено» — можно сразу перезаписать PDF без диагностики.",
            "Пакетная диагностика PDF",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      string pdfRoot = VelumBatchFormFolderBootstrap.ReadFolderPath(_pdfFolderBox);
      bool hasDeliveryFolder = !string.IsNullOrWhiteSpace(pdfRoot);
      if (!hasDeliveryFolder)
      {
        DialogResult answer = MessageBox.Show(
            "Не указан каталог выгрузки PDF, будут обновлены только базовые файлы PDF. Продолжить?",
            "Пакетная диагностика PDF",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes)
          return;
      }
      else if (!Directory.Exists(pdfRoot))
      {
        MessageBox.Show(
            "Укажите существующий каталог PDF.",
            "Пакетная диагностика PDF",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return;
      }

      _progressBar.Minimum = 0;
      _progressBar.Maximum = selected.Count;
      _progressBar.Value = 0;
      _progressBar.Visible = true;
      _lblProgress.Visible = true;

      int exported = 0;
      int progress = 0;
      HashSet<string> keepOpenDrawingPaths = VelumPdfBatchDocumentHelper.CollectOpenDrawingPaths(_swApp);
      string activeDrawingPathBeforeExport =
          VelumPdfBatchDocumentHelper.TryGetActiveDrawingPath(_swApp);

      try
      {
        BeginOperation();
        for (int i = 0; i < selected.Count; i++)
        {
          if (_cancelRequested)
            break;

          VelumPdfBatchDiagnosticRow row = selected[i];
          progress++;
          _lblProgress.Text = "Экспорт: " + row.DrawingDisplayName +
                              " (" + progress + "/" + selected.Count + ")";
          _progressBar.Value = progress;
          Application.DoEvents();

          ModelDoc2 modelDoc = VelumPdfBatchDocumentHelper.TryOpenDrawingSilent(
              _swApp,
              row.DrawingPath,
              out _);
          if (modelDoc == null)
            continue;

          bool exportOk = false;
          try
          {
            string canonicalPdfPath = row.PdfPath;
            string canonicalFolder = string.IsNullOrWhiteSpace(canonicalPdfPath)
                ? string.Empty
                : Path.GetDirectoryName(canonicalPdfPath);
            if (string.IsNullOrWhiteSpace(canonicalFolder) || !Directory.Exists(canonicalFolder))
            {
              row.Status = VelumPdfBatchRowStatus.Error;
              row.StatusText = "Первая выгрузка PDF выполняется вручную: нет доступного «Путь pdf».";
              row.Selected = false;
              continue;
            }

            var request = new VelumPdfExportService.ExportRequest
            {
              ModelDoc = modelDoc,
              CanonicalFolder = canonicalFolder,
              DeliveryFolder = pdfRoot
            };

            VelumPdfExportService.ExportResult exportResult = VelumPdfExportService.TryExport(request);
            exportOk = exportResult.Success;
            if (exportOk)
            {
              exported++;
              if (exportResult.Delivered)
                _rows.Remove(row);
              else
              {
                row.Status = VelumPdfBatchRowStatus.ExportedNotDelivered;
                row.StatusText = "Экспортирован, но не доставлен: " + exportResult.DeliveryError;
                row.Selected = false;
              }
            }
            else
            {
              row.Status = VelumPdfBatchRowStatus.Error;
              row.StatusText = "Ошибка: " + exportResult.Message;
            }

            VelumPdfBatchScanner.RefreshDrawingRow(modelDoc, row.DrawingPath, pdfRoot, _rows);
          }
          finally
          {
            bool keepOpen = IsKeepOpenDrawing(row.DrawingPath, keepOpenDrawingPaths);
            // Уже открытый чертёж: свойства/штампы в памяти часто не доезжают до файла,
            // и проба сразу показывает «Устарел». Reconcile (Save под suppress) + закрытие;
            // активный откроется снова ниже уже с диска.
            if (exportOk && keepOpen)
            {
              VelumPdfBatchDocumentHelper.TryReconcileKeepOpenDrawingAfterPdfExport(modelDoc);
              VelumPdfBatchDocumentHelper.TryReleaseDrawingAfterBatch(
                  _swApp,
                  modelDoc,
                  row.DrawingPath,
                  persistChanges: false,
                  keepOpenDrawingPaths: null);
            }
            else
            {
              // Save3 поднимает GetUpdateStamp чертежа, но PdfGeometryUpdateStamp остаётся старым.
              // Дожимаем export-штамп до текущего GetUpdateStamp, чтобы при повторном открытии
              // чертёж не помечался как «Устарел» (GetUpdateStamp > PdfGeometryUpdateStamp).
              VelumExportDocumentationGeometryStampHelper.RunWithGeometryPendingStampSyncSuppressed(() =>
              {
                VelumExportDocumentationGeometryStampHelper.TryFinalizePdfExportStamps(modelDoc, out _);
              });

              VelumPdfBatchDocumentHelper.TryReleaseDrawingAfterBatch(
                  _swApp,
                  modelDoc,
                  row.DrawingPath,
                  persistChanges: true,
                  keepOpenDrawingPaths);
            }
          }
        }
      }
      finally
      {
        EndOperation();
        ResetExportProgressUi();
      }

      // Вернуть фокус на чертёж, который был активен до пакета — иначе gate может
      // опросить сборку и оставить старые красные PDF-пробы в MergeProbeResults.
      if (!string.IsNullOrWhiteSpace(activeDrawingPathBeforeExport) &&
          IsKeepOpenDrawing(activeDrawingPathBeforeExport, keepOpenDrawingPaths))
      {
        VelumPdfBatchDocumentHelper.TryActivateOrOpenDrawingVisible(
            _swApp,
            activeDrawingPathBeforeExport,
            out _);
      }

      VelumPdfBatchDiagnosticsSession.ReplaceRows(_rows, _orphanPdfFiles);
      BindListView();
      UpdateStatusLabel();
      VelumSolidProbeRefreshPlanner.MarkExportDocumentationStale();

      string completionMessage = _cancelRequested
          ? "Экспорт остановлен. Экспортировано строк: " + exported
          : "Экспортировано строк: " + exported;
      MessageBox.Show(
          completionMessage,
          "Пакетная диагностика PDF",
          MessageBoxButtons.OK,
          MessageBoxIcon.Information);
    }

    private void OnDeleteOrphans(object sender, EventArgs e)
    {
      if (!VelumPdfBatchDiagnosticsSession.IsActive)
      {
        MessageBox.Show(
            "Сначала выполните диагностику.",
            "Пакетная диагностика PDF",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      var filesToDelete = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      var junkRowsToRemove = new List<VelumPdfBatchDiagnosticRow>();

      for (int i = 0; i < _listView.SelectedItems.Count; i++)
      {
        ListViewItem listItem = _listView.SelectedItems[i];
        var item = listItem.Tag as VelumPdfBatchListItem;
        if (item == null)
          continue;

        if (item.IsOrphan)
        {
          if (!string.IsNullOrWhiteSpace(item.OrphanPdfPath))
            filesToDelete.Add(item.OrphanPdfPath);
        }
        else if (item.Row != null && item.Row.Status == VelumPdfBatchRowStatus.Junk)
        {
          if (!string.IsNullOrWhiteSpace(item.Row.PdfPath))
            filesToDelete.Add(item.Row.PdfPath);
          junkRowsToRemove.Add(item.Row);
        }
      }

      if (filesToDelete.Count == 0)
      {
        MessageBox.Show(
            "Мусорные PDF — это:\n" +
            "• файлы в каталоге PDF без соответствующего чертежа SLDDRW;\n" +
            "• PDF при «Нужен pdf = Нет» (статус «Мусорный PDF»).\n\n" +
            "Статус «Устарел» — не мусор: выделите такие строки и используйте «Экспортировать выделенные».\n\n" +
            "Выделите в списке строки «Мусорный PDF» или сироты без чертежа.",
            "Пакетная диагностика PDF",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      DialogResult answer = MessageBox.Show(
          "Удалить выделенные мусорные PDF (" + filesToDelete.Count + ")?\n\n" +
          "Будут удалены только файлы на диске; свойства SLDDRW не изменяются.",
          "Пакетная диагностика PDF",
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

      for (int i = _orphanPdfFiles.Count - 1; i >= 0; i--)
      {
        if (filesToDelete.Contains(_orphanPdfFiles[i]))
          _orphanPdfFiles.RemoveAt(i);
      }

      VelumPdfBatchDiagnosticsSession.ReplaceRows(_rows, _orphanPdfFiles);
      BindListView();
      UpdateStatusLabel();

      MessageBox.Show(
          "Удалено файлов: " + deleted,
          "Пакетная диагностика PDF",
          MessageBoxButtons.OK,
          MessageBoxIcon.Information);
    }

    private void BindListView()
    {
      RefreshStatusFilterItems();

      string statusFilter = _cmbStatusFilter.SelectedItem as string ?? StatusFilterAll;
      bool filterAll = string.Equals(statusFilter, StatusFilterAll, StringComparison.Ordinal);
      var pendingSelection = new List<ListViewItem>();

      _suppressListSelectionSync = true;
      _listView.BeginUpdate();
      try
      {
        _listView.Items.Clear();
        int visibleCount = 0;

        for (int i = 0; i < _orphanPdfFiles.Count; i++)
        {
          string path = _orphanPdfFiles[i];
          const string orphanStatus = "Сирота (нет чертежа SLDDRW)";
          if (!filterAll && !string.Equals(statusFilter, orphanStatus, StringComparison.Ordinal))
            continue;

          var tag = new VelumPdfBatchListItem
          {
            OrphanPdfPath = path,
            Selected = true
          };
          var listItem = new ListViewItem(string.Empty)
          {
            Tag = tag
          };
          listItem.SubItems.Add(Path.GetFileName(path));
          listItem.SubItems.Add(orphanStatus);
          _listView.Items.Add(listItem);
          pendingSelection.Add(listItem);
          visibleCount++;
        }

        for (int i = 0; i < _rows.Count; i++)
        {
          VelumPdfBatchDiagnosticRow row = _rows[i];
          string statusText = row.StatusText ?? string.Empty;
          if (!filterAll && !string.Equals(statusFilter, statusText, StringComparison.Ordinal))
            continue;

          var tag = new VelumPdfBatchListItem
          {
            Row = row,
            Selected = row.Selected
          };
          var listItem = new ListViewItem(row.DrawingDisplayName)
          {
            Tag = tag
          };
          listItem.SubItems.Add(FormatPdfFileName(row.PdfPath));
          listItem.SubItems.Add(statusText);
          _listView.Items.Add(listItem);
          if (row.Selected)
            pendingSelection.Add(listItem);
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
      }

      // Selected внутри BeginUpdate у новых ListViewItem часто не попадает в SelectedItems —
      // из‑за этого после диагностики пункты «Открыть/Отключить» остаются выключенными.
      try
      {
        for (int i = 0; i < pendingSelection.Count; i++)
          pendingSelection[i].Selected = true;
      }
      finally
      {
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

      if (_orphanPdfFiles.Count > 0)
        statuses.Add("Сирота (нет чертежа SLDDRW)");

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
      try
      {
        foreach (ListViewItem listItem in _listView.Items)
        {
          listItem.Selected = true;
          var tag = listItem.Tag as VelumPdfBatchListItem;
          if (tag == null)
            continue;
          tag.Selected = true;
          if (tag.Row != null)
            tag.Row.Selected = true;
        }
      }
      finally
      {
        _suppressListSelectionSync = false;
      }
    }

    private void OnListViewItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
    {
      if (_suppressListSelectionSync)
        return;

      var item = e.Item.Tag as VelumPdfBatchListItem;
      if (item == null)
        return;

      item.Selected = e.IsSelected;
      if (item.Row != null)
        item.Row.Selected = e.IsSelected;
    }

    private void OnListViewDoubleClick(object sender, EventArgs e)
    {
      OnOpenDrawingFromList();
    }

    private void OnListViewMouseUp(object sender, MouseEventArgs e)
    {
      if (e.Button != MouseButtons.Right)
        return;

      if (_listView.Items.Count == 0)
        return;

      // GetItemAt видит только 1-й столбец; после диагностики ПКМ обычно по статусу/PDF.
      ListViewHitTestInfo hitTest = _listView.HitTest(e.Location);
      ListViewItem hit = hitTest?.Item;
      if (hit != null && !hit.Selected)
      {
        while (_listView.SelectedItems.Count > 0)
          _listView.SelectedItems[0].Selected = false;
        hit.Selected = true;
      }

      bool hasDrawingSelection = CollectSelectedDrawingPaths().Count > 0;
      bool hasRowSelection = HasSelectedDiagnosticRows();
      for (int i = 0; i < _listContextMenu.Items.Count; i++)
      {
        ToolStripItem menuItem = _listContextMenu.Items[i];
        if (menuItem is ToolStripSeparator)
          continue;
        if (string.Equals(menuItem.Text, "Выбрать все", StringComparison.Ordinal))
        {
          menuItem.Enabled = true;
          continue;
        }

        if (string.Equals(menuItem.Text, "Не нужен чертеж", StringComparison.Ordinal))
        {
          // Доступен при любом статусе, в т.ч. «Нет чертежа» (нужен SourceModelPath).
          menuItem.Enabled = hasRowSelection;
          continue;
        }

        menuItem.Enabled = hasDrawingSelection;
      }

      _listContextMenu.Show(_listView, e.Location);
    }

    private bool HasSelectedDiagnosticRows()
    {
      for (int i = 0; i < _listView.Items.Count; i++)
      {
        ListViewItem listItem = _listView.Items[i];
        if (!listItem.Selected)
          continue;

        var item = listItem.Tag as VelumPdfBatchListItem;
        if (item != null && !item.IsOrphan && item.Row != null)
          return true;
      }

      return false;
    }

    private void OnOpenDrawingFromList()
    {
      List<string> paths = CollectSelectedDrawingPaths();
      if (paths.Count == 0)
        return;

      var errors = new List<string>();
      for (int i = 0; i < paths.Count; i++)
      {
        string path = paths[i];
        if (!VelumPdfBatchDocumentHelper.TryActivateOrOpenDrawingVisible(_swApp, path, out string error))
        {
          errors.Add(
              Path.GetFileName(path) + ": " +
              (string.IsNullOrWhiteSpace(error) ? "Не удалось открыть чертёж." : error));
        }
      }

      if (errors.Count > 0)
      {
        MessageBox.Show(
            string.Join(System.Environment.NewLine, errors),
            "Пакетная диагностика PDF",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
      }
    }

    private List<string> CollectSelectedDrawingPaths()
    {
      var paths = new List<string>();
      var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      for (int i = 0; i < _listView.Items.Count; i++)
      {
        ListViewItem listItem = _listView.Items[i];
        if (!listItem.Selected)
          continue;

        var item = listItem.Tag as VelumPdfBatchListItem;
        if (item == null || item.IsOrphan || item.Row == null)
          continue;

        string path = item.Row.DrawingPath;
        if (string.IsNullOrWhiteSpace(path) || !seen.Add(path))
          continue;

        paths.Add(path);
      }

      return paths;
    }

    private void OnDisableExportFromList()
    {
      List<string> paths = CollectSelectedDrawingPaths();
      if (paths.Count == 0)
        return;

      string pdfRoot = VelumBatchFormFolderBootstrap.ReadFolderPath(_pdfFolderBox);
      HashSet<string> keepOpenDrawingPaths = VelumPdfBatchDocumentHelper.CollectOpenDrawingPaths(_swApp);
      var errors = new List<string>();
      bool anyChanged = false;

      for (int i = 0; i < paths.Count; i++)
      {
        string drawingPath = paths[i];
        ModelDoc2 modelDoc = VelumPdfBatchDocumentHelper.TryOpenDrawingSilent(
            _swApp,
            drawingPath,
            out string openError);
        if (modelDoc == null)
        {
          errors.Add(
              Path.GetFileName(drawingPath) + ": " +
              (string.IsNullOrWhiteSpace(openError) ? "Не удалось открыть чертёж." : openError));
          continue;
        }

        try
        {
          if (!VelumPdfBatchDocumentHelper.TrySetNeedPdf(modelDoc, false, out string setError))
          {
            errors.Add(
                Path.GetFileName(drawingPath) + ": " +
                (string.IsNullOrWhiteSpace(setError) ? "Не удалось отключить экспорт." : setError));
            continue;
          }

          if (!VelumPdfBatchDocumentHelper.TrySaveDrawingSilent(modelDoc, out string saveError))
          {
            errors.Add(
                Path.GetFileName(drawingPath) + ": " +
                (string.IsNullOrWhiteSpace(saveError) ? "Не удалось сохранить чертёж." : saveError));
            continue;
          }

          VelumPdfBatchScanner.RefreshDrawingRow(modelDoc, drawingPath, pdfRoot, _rows);
          anyChanged = true;
        }
        finally
        {
          VelumPdfBatchDocumentHelper.TryReleaseDrawingAfterBatch(
              _swApp,
              modelDoc,
              drawingPath,
              persistChanges: false,
              keepOpenDrawingPaths);
        }
      }

      if (anyChanged)
      {
        VelumPdfBatchDiagnosticsSession.ReplaceRows(_rows, _orphanPdfFiles);
        BindListView();
        UpdateStatusLabel();
        VelumSolidProbeRefreshPlanner.MarkExportDocumentationStale();
      }

      if (errors.Count > 0)
      {
        MessageBox.Show(
            string.Join(System.Environment.NewLine, errors),
            "Пакетная диагностика PDF",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
      }
    }

    private void OnDrawingNotNeededFromList()
    {
      var selectedRows = new List<VelumPdfBatchDiagnosticRow>();
      for (int i = 0; i < _listView.Items.Count; i++)
      {
        ListViewItem listItem = _listView.Items[i];
        if (!listItem.Selected)
          continue;

        var item = listItem.Tag as VelumPdfBatchListItem;
        if (item == null || item.IsOrphan || item.Row == null)
          continue;

        selectedRows.Add(item.Row);
      }

      if (selectedRows.Count == 0)
        return;

      var errors = new List<string>();
      var rowsToRemove = new HashSet<VelumPdfBatchDiagnosticRow>();
      var syncedModelPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      bool anyChanged = false;

      for (int i = 0; i < selectedRows.Count; i++)
      {
        VelumPdfBatchDiagnosticRow row = selectedRows[i];
        List<string> modelPaths = CollectSourceModelPaths(row);
        if (modelPaths.Count == 0)
        {
          errors.Add(
              (row.DrawingDisplayName ?? Path.GetFileName(row.DrawingPath) ?? "?") +
              ": не найден документ-источник чертежа.");
          continue;
        }

        bool rowOk = true;
        for (int m = 0; m < modelPaths.Count; m++)
        {
          string modelPath = modelPaths[m];
          if (!VelumDrawingPathPropertyHelper.TrySetNeedDrawingToPath(
                  _swApp?.Sw,
                  modelPath,
                  false,
                  out string setError))
          {
            errors.Add(
                Path.GetFileName(modelPath) + ": " +
                (string.IsNullOrWhiteSpace(setError) ? "Не удалось записать «Нужен чертеж»." : setError));
            rowOk = false;
            continue;
          }

          if (syncedModelPaths.Add(modelPath))
            TryMirrorNeedDrawingInProductRegistry(modelPath, false);

          UpdateComponentNeedDrawingCache(modelPath, false);
        }

        if (!rowOk)
          continue;

        rowsToRemove.Add(row);
        anyChanged = true;
      }

      if (anyChanged)
      {
        for (int i = _rows.Count - 1; i >= 0; i--)
        {
          if (rowsToRemove.Contains(_rows[i]))
            _rows.RemoveAt(i);
        }

        VelumPdfBatchDiagnosticsSession.ReplaceRows(_rows, _orphanPdfFiles);
        BindListView();
        UpdateStatusLabel();
        VelumSolidProbeRefreshPlanner.MarkExportDocumentationStale();
      }

      if (errors.Count > 0)
      {
        MessageBox.Show(
            string.Join(System.Environment.NewLine, errors),
            "Пакетная диагностика PDF",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
      }
    }

    private List<string> CollectSourceModelPaths(VelumPdfBatchDiagnosticRow row)
    {
      var paths = new List<string>();
      var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      if (row == null)
        return paths;

      // Источник — всегда деталь/сборка, не чертёж.
      if (IsPartOrAssemblyPath(row.SourceModelPath) &&
          seen.Add(row.SourceModelPath.Trim()))
        paths.Add(row.SourceModelPath.Trim());

      if (_components != null)
      {
        string drawingKey = string.IsNullOrWhiteSpace(row.DrawingPath)
            ? string.Empty
            : VelumPdfBatchDocumentHelper.TryNormalizeDrawingPath(row.DrawingPath);
        string displayStem = Path.GetFileNameWithoutExtension(
            row.DrawingDisplayName ?? string.Empty);

        for (int i = 0; i < _components.Count; i++)
        {
          VelumAssemblyRegistryComponent comp = _components[i];
          if (comp == null || !IsPartOrAssemblyPath(comp.FilePath))
            continue;

          string compPath = comp.FilePath.Trim();
          bool match = false;

          if (!string.IsNullOrWhiteSpace(row.SourceModelPath) &&
              string.Equals(
                  VelumDrawingPathPropertyHelper.NormalizePath(compPath),
                  VelumDrawingPathPropertyHelper.NormalizePath(row.SourceModelPath),
                  StringComparison.OrdinalIgnoreCase))
          {
            match = true;
          }
          else if (!string.IsNullOrWhiteSpace(drawingKey))
          {
            string cachedDrawing = string.Empty;
            if (comp.PropertyValues != null)
            {
              string raw;
              if (comp.PropertyValues.TryGetValue(
                      VelumExportDocumentationProperties.DrawingPath, out raw) &&
                  raw != null)
                cachedDrawing = raw.Trim();
            }

            if (!string.IsNullOrWhiteSpace(cachedDrawing))
            {
              string cachedKey = VelumPdfBatchDocumentHelper.TryNormalizeDrawingPath(cachedDrawing);
              if (string.Equals(cachedKey, drawingKey, StringComparison.OrdinalIgnoreCase))
                match = true;
            }
          }
          else if (!string.IsNullOrWhiteSpace(displayStem))
          {
            string compStem = Path.GetFileNameWithoutExtension(compPath);
            if (string.Equals(compStem, displayStem, StringComparison.OrdinalIgnoreCase))
              match = true;
          }

          if (match && seen.Add(compPath))
            paths.Add(compPath);
        }
      }

      if (paths.Count > 0)
        return paths;

      // Только если путь чертежа реален — резолв referenced моделей (не для «Нет чертежа»).
      if (string.IsNullOrWhiteSpace(row.DrawingPath) ||
          !File.Exists(row.DrawingPath) ||
          _swApp?.Sw == null)
        return paths;

      HashSet<string> keepOpenDrawingPaths = VelumPdfBatchDocumentHelper.CollectOpenDrawingPaths(_swApp);
      ModelDoc2 drawingDoc = VelumPdfBatchDocumentHelper.TryFindOpenDrawingByPath(
          _swApp, row.DrawingPath);
      bool openedHere = false;
      if (drawingDoc == null)
      {
        drawingDoc = VelumPdfBatchDocumentHelper.TryOpenDrawingSilent(
            _swApp, row.DrawingPath, out _);
        openedHere = drawingDoc != null;
      }

      if (drawingDoc == null)
        return paths;

      try
      {
        var targets = new Dictionary<string, ModelDoc2>(StringComparer.OrdinalIgnoreCase);
        VelumDrawingPathSavePropagator.CollectReferencedPartOrAssemblyTargets(drawingDoc, targets);
        foreach (string path in targets.Keys)
        {
          if (IsPartOrAssemblyPath(path) && seen.Add(path.Trim()))
            paths.Add(path.Trim());
        }
      }
      finally
      {
        if (openedHere)
        {
          VelumPdfBatchDocumentHelper.TryReleaseDrawingAfterBatch(
              _swApp,
              drawingDoc,
              row.DrawingPath,
              persistChanges: false,
              keepOpenDrawingPaths);
        }
      }

      return paths;
    }

    private static bool IsPartOrAssemblyPath(string path)
    {
      if (string.IsNullOrWhiteSpace(path))
        return false;
      string ext = Path.GetExtension(path.Trim());
      return string.Equals(ext, ".sldprt", StringComparison.OrdinalIgnoreCase)
          || string.Equals(ext, ".sldasm", StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateComponentNeedDrawingCache(string modelPath, bool needDrawing)
    {
      if (_components == null || string.IsNullOrWhiteSpace(modelPath))
        return;

      string expected = VelumDrawingPathPropertyHelper.NormalizePath(modelPath);
      string value = needDrawing
          ? VelumExportDocumentationProperties.FlagYes
          : VelumExportDocumentationProperties.FlagNo;

      for (int i = 0; i < _components.Count; i++)
      {
        VelumAssemblyRegistryComponent comp = _components[i];
        if (comp == null || string.IsNullOrWhiteSpace(comp.FilePath))
          continue;
        if (!string.Equals(
                VelumDrawingPathPropertyHelper.NormalizePath(comp.FilePath),
                expected,
                StringComparison.OrdinalIgnoreCase))
          continue;

        if (comp.PropertyValues == null)
          comp.PropertyValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        comp.PropertyValues[VelumExportDocumentationProperties.NeedDrawing] = value;
      }
    }

    private static void TryMirrorNeedDrawingInProductRegistry(string modelPath, bool needDrawing)
    {
      if (string.IsNullOrWhiteSpace(modelPath))
        return;

      try
      {
        var store = new VelumProductRegistryStore();
        store.Load();
        VelumProductItem item = store.FindItemByFilePath(modelPath);
        if (item == null)
          return;
        if (!VelumProductRegistryNeedDrawingCommands.IsPartOrAssemblyPath(item.FilePath))
          return;
        if (item.NeedDrawing == needDrawing)
          return;

        item.NeedDrawing = needDrawing;
        store.UpdateItem(item, persist: true);
        VelumProductRegistryIntegrityScheduler.NotifyRegistryChanged();
        VelumProductRegistryIntegrityScheduler.RevalidateItem(item.Id);
      }
      catch
      {
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
      _btnBrowsePdf.Enabled = false;
      _btnClose.Enabled = false;
      _pdfFolderBox.Enabled = false;
      _cmbStatusFilter.Enabled = false;
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
      _btnBrowsePdf.Enabled = true;
      _btnClose.Enabled = true;
      _pdfFolderBox.Enabled = true;
      _cmbStatusFilter.Enabled = true;
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
        if (_rows[i] != null && _rows[i].Status == VelumPdfBatchRowStatus.Unchecked)
          uncheckedCount++;
      }

      if (uncheckedCount > 0 && uncheckedCount == _rows.Count && _orphanPdfFiles.Count == 0)
      {
        _statusLabel.Text = "Не проверено — можно выгрузить или запустить диагностику";
        _statusLabel.ForeColor = Color.DarkGreen;
        return;
      }

      bool hasProblems = VelumPdfBatchDiagnosticsSession.HasProblems();
      _statusLabel.Text = hasProblems
          ? "Есть проблемные чертежи"
          : "Нет проблемных чертежей";
      _statusLabel.ForeColor = hasProblems ? Color.DarkRed : Color.DarkGreen;
    }

    private static bool IsKeepOpenDrawing(string drawingPath, ISet<string> keepOpenDrawingPaths)
    {
      if (keepOpenDrawingPaths == null || keepOpenDrawingPaths.Count == 0)
        return false;

      string normalized = VelumPdfBatchDocumentHelper.TryNormalizeDrawingPath(drawingPath);
      return !string.IsNullOrWhiteSpace(normalized) && keepOpenDrawingPaths.Contains(normalized);
    }

    private static string FormatPdfFileName(string pdfPath)
    {
      return string.IsNullOrWhiteSpace(pdfPath) ? string.Empty : Path.GetFileName(pdfPath);
    }

    private static bool IsExportableStatus(VelumPdfBatchRowStatus status)
    {
      return status == VelumPdfBatchRowStatus.NeedExport ||
             status == VelumPdfBatchRowStatus.Outdated ||
             status == VelumPdfBatchRowStatus.Unchecked;
    }

    /// <returns>true, если каталог выбран.</returns>
    private bool BrowseFolder(TextBox target, string description)
    {
      string initial = _assemblyFolder ?? string.Empty;
      if (string.IsNullOrWhiteSpace(initial))
        initial = VelumBatchFormFolderBootstrap.ReadFolderPath(target);
      string selected;
      if (!VelumFolderBrowser.TrySelect(this, description, initial, out selected))
        return false;

      target.ForeColor = SystemColors.WindowText;
      target.Text = selected;
      return true;
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

    private sealed class VelumPdfBatchListItem
    {
      internal VelumPdfBatchDiagnosticRow Row { get; set; }

      internal string OrphanPdfPath { get; set; }

      internal bool Selected { get; set; }

      internal bool IsOrphan => !string.IsNullOrWhiteSpace(OrphanPdfPath);
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
