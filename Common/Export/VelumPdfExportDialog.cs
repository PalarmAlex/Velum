using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;
using Velum.ReactiveCore;
using Velum.ReactiveCore.Export;

namespace Velum.UI
{
  /// <summary>Диалог экспорта чертежа в PDF (каталог выгрузки, preview, статус версии).</summary>
  internal sealed partial class VelumPdfExportDialog : Form
  {
    private ModelDoc2 _modelDoc;
    private readonly int _launchConditionedReflexId;
    private ToolTip _folderToolTip;

    public VelumPdfExportDialog()
    {
      InitializeComponent();
      VelumFormHelp.Bind(this, VelumHelpTopics.PdfExport);
    }

    public VelumPdfExportDialog(ModelDoc2 modelDoc)
        : this(modelDoc, 0)
    {
    }

    public VelumPdfExportDialog(ModelDoc2 modelDoc, int launchConditionedReflexId)
        : this()
    {
      _modelDoc = modelDoc;
      _launchConditionedReflexId = launchConditionedReflexId > 0 ? launchConditionedReflexId : 0;
      InitializeRuntime(modelDoc);
    }

    private void InitializeRuntime(ModelDoc2 modelDoc)
    {
      Icon icon = TryLoadVelumWindowIcon();
      if (icon != null)
        Icon = icon;

      VelumConditionedReflexForbidHelper.BindForbidButton(_btnForbid, _launchConditionedReflexId, this);

      _lblDocument.Text = "Чертёж: " + TryGetDocumentTitle(modelDoc);
      _folderBox.Text = VelumPdfFileNameHelper.TryResolveDefaultOutputFolder(modelDoc) ?? string.Empty;
      _folderBox.TextChanged += (s, e) => UpdatePreviewAndStatus();
      if (_folderToolTip == null)
        _folderToolTip = new ToolTip();
      VelumBatchFormFolderBootstrap.BindFolderPathTooltip(_folderBox, _folderToolTip);
      _folderToolTip.SetToolTip(_btnBrowse, "Выбрать каталог выгрузки PDF");
      _folderToolTip.SetToolTip(_btnExport, "Экспортировать чертёж в PDF");
      _folderToolTip.SetToolTip(_btnCancel, "Закрыть");

      UpdatePreviewAndStatus();
      UpdateExportButtonState();
    }

    private void OnFormShown(object sender, EventArgs e)
    {
      FitFormHeightToContent();
      _btnExport.Focus();
      BeginInvoke(new Action(() => _btnExport.Focus()));
    }

    private void OnBrowseFolder(object sender, EventArgs e)
    {
      string initial = (_folderBox.Text ?? string.Empty).Trim();
      string selected;
      if (VelumFolderBrowser.TrySelect(this, "Каталог для файла PDF", initial, out selected))
        _folderBox.Text = selected;
    }

    private void OnExport(object sender, EventArgs e)
    {
      if (!VelumPdfExportService.IsDrawingSavedOnDisk(_modelDoc))
      {
        MessageBox.Show(
            "Сначала сохраните чертёж по правилам КБ.",
            "Экспорт PDF",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        UpdateExportButtonState();
        return;
      }

      string selectedFolder = (_folderBox.Text ?? string.Empty).Trim();
      string canonicalFolder = VelumPdfFileNameHelper.TryResolveDefaultOutputFolder(_modelDoc);
      string deliveryFolder = string.Empty;
      if (string.IsNullOrWhiteSpace(canonicalFolder) || !Directory.Exists(canonicalFolder))
        canonicalFolder = selectedFolder;
      else if (!string.Equals(canonicalFolder, selectedFolder, StringComparison.OrdinalIgnoreCase) &&
               MessageBox.Show(
                   this,
                   "Выбранный каталог отличается от канонического «Путь pdf».\n\nДа — сменить канонический каталог.\nНет — оставить его и скопировать PDF в выбранный каталог.",
                   "Каталог PDF",
                   MessageBoxButtons.YesNo,
                   MessageBoxIcon.Question) == DialogResult.No)
        deliveryFolder = selectedFolder;
      else
        canonicalFolder = selectedFolder;

      var request = new VelumPdfExportService.ExportRequest
      {
        ModelDoc = _modelDoc,
        CanonicalFolder = canonicalFolder,
        DeliveryFolder = deliveryFolder,
      };

      VelumPdfExportService.ExportResult exportResult = VelumPdfExportService.TryExport(request);
      if (exportResult.Success)
      {
        MessageBox.Show(
            exportResult.Message,
            "Экспорт PDF",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        DialogResult = DialogResult.OK;
        Close();
        return;
      }

      MessageBox.Show(
          exportResult.Message ?? "Экспорт не выполнен.",
          "Экспорт PDF",
          MessageBoxButtons.OK,
          MessageBoxIcon.Warning);
    }

    private void UpdatePreviewAndStatus()
    {
      if (_modelDoc == null)
        return;

      string baseName = VelumPdfFileNameHelper.ResolveDrawingBaseFileName(_modelDoc);
      string folder = (_folderBox.Text ?? string.Empty).Trim();
      string previewPath = string.IsNullOrWhiteSpace(folder)
          ? "(каталог не задан)"
          : Path.Combine(folder, (string.IsNullOrWhiteSpace(baseName) ? "(имя не определено)" : baseName) + ".pdf");
      _lblPreviewPath.Text = "Итоговый файл: " + previewPath;

      ResolvedPdfArtifact currentArtifact = VelumPdfArtifactResolver.Resolve(_modelDoc);
      if (currentArtifact.Found)
        _lblCurrentArtifact.Text = "Текущий артефакт: " + currentArtifact.FullPath;
      else
        _lblCurrentArtifact.Text = "Текущий артефакт: не найден";

      _lblVersionStatus.Text = "Статус версии: " + BuildVersionStatusText(currentArtifact);
      FitFormHeightToContent();
    }

    /// <summary>
    /// Высота строк «Итоговый файл» / «Текущий артефакт» растёт с длиной пути —
    /// подгоняем ClientSize, иначе кнопки внизу обрезаются.
    /// </summary>
    private void FitFormHeightToContent()
    {
      if (_rootLayout == null || !IsHandleCreated)
        return;

      int innerWidth = Math.Max(
          1,
          ClientSize.Width - _rootLayout.Padding.Horizontal - 6);
      ApplyLabelWrapWidth(_lblDocument, innerWidth);
      ApplyLabelWrapWidth(_lblPreviewPath, innerWidth);
      ApplyLabelWrapWidth(_lblCurrentArtifact, innerWidth);
      ApplyLabelWrapWidth(_lblVersionStatus, innerWidth);
      ApplyLabelWrapWidth(_lblSaveHint, innerWidth);

      _rootLayout.PerformLayout();
      Size preferred = _rootLayout.PreferredSize;
      int contentHeight = preferred.Height;
      if (contentHeight <= 0)
      {
        int[] rowHeights = _rootLayout.GetRowHeights();
        contentHeight = _rootLayout.Padding.Top + _rootLayout.Padding.Bottom;
        for (int i = 0; i < rowHeights.Length; i++)
          contentHeight += rowHeights[i];
      }

      int nonClient = Height - ClientSize.Height;
      int minClient = Math.Max(0, MinimumSize.Height - nonClient);
      int targetClient = Math.Max(contentHeight, minClient);
      if (targetClient > ClientSize.Height)
        ClientSize = new Size(ClientSize.Width, targetClient);
    }

    private static void ApplyLabelWrapWidth(Label label, int width)
    {
      if (label == null)
        return;

      label.MaximumSize = new Size(width, 0);
      label.AutoSize = true;
    }

    private string BuildVersionStatusText(ResolvedPdfArtifact artifact)
    {
      if (_modelDoc == null)
        return "документ недоступен";

      if (!artifact.Found)
        return "файл отсутствует";

      if (!VelumExportDocumentationGeometryStampHelper.TryReadStoredUpdateStamp(
              _modelDoc,
              VelumExportDocumentationProperties.PdfGeometryUpdateStamp,
              out int exportStamp))
        return "штамп не задан";

      if (!VelumExportDocumentationGeometryStampHelper.TryGetCurrentUpdateStamp(_modelDoc, out int currentStamp))
        currentStamp = 0;

      if (VelumExportDocumentationGeometryStampHelper.TryIsPdfOutdated(_modelDoc, currentStamp, exportStamp))
      {
        if (VelumExportDocumentationGeometryStampHelper.TryReadStoredUpdateStamp(
                _modelDoc,
                VelumExportDocumentationProperties.PdfGeometryPendingStamp,
                out int pendingStamp) &&
            pendingStamp > exportStamp)
          return "устарел (pending " + pendingStamp + " > export " + exportStamp + ")";

        return "устарел";
      }

      return "актуален (export " + exportStamp + ")";
    }

    private void UpdateExportButtonState()
    {
      bool saved = VelumPdfExportService.IsDrawingSavedOnDisk(_modelDoc);
      _btnExport.Enabled = saved;
      if (!saved)
        _lblSaveHint.Text = "Сохраните чертёж на диск, чтобы экспортировать PDF.";
      else
        _lblSaveHint.Text = string.Empty;
    }

    private static string TryGetDocumentTitle(ModelDoc2 modelDoc)
    {
      try
      {
        string title = modelDoc?.GetTitle();
        if (!string.IsNullOrWhiteSpace(title))
          return title;
      }
      catch
      {
      }

      return "(чертёж)";
    }

    private static Icon TryLoadVelumWindowIcon()
    {
      return VelumFormIcon.TryLoad();
    }
  }
}
