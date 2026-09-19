using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using ISIDA.Common;
using SolidWorks.Interop.sldworks;
using Velum.ReactiveCore.Export;
using Velum.SolidHomeostasis;

namespace Velum.UI
{
  /// <summary>Диалог экспорта детали в DXF (развёртка или проекция).</summary>
  internal sealed partial class VelumDxfExportDialog : Form
  {
    private ModelDoc2 _modelDoc;
    private bool _isSheetMetal;
    private bool _isSketchOnly;
    private bool _isEmptyDocument;
    private bool _suppressViewChange;
    private string _activeConfigName;
    private readonly int _launchConditionedReflexId;
    private ToolTip _folderToolTip;

    public VelumDxfExportDialog()
    {
      InitializeComponent();
      VelumFormHelp.Bind(this, VelumHelpTopics.DxfExport);
    }

    public VelumDxfExportDialog(ModelDoc2 modelDoc)
        : this(modelDoc, 0)
    {
    }

    public VelumDxfExportDialog(ModelDoc2 modelDoc, int launchConditionedReflexId)
        : this()
    {
      _modelDoc = modelDoc;
      _launchConditionedReflexId = launchConditionedReflexId > 0 ? launchConditionedReflexId : 0;
      _isEmptyDocument = VelumDxfPartGeometryHelper.IsEmptyDocument(modelDoc);
      _isSheetMetal = VelumSolidSheetMetalHelper.IsSheetMetalPart(modelDoc) &&
          VelumSolidSheetMetalHelper.TryFindFlatPatternFeature(modelDoc) != null;
      _isSketchOnly = !_isEmptyDocument &&
          !_isSheetMetal &&
          !VelumDxfPartGeometryHelper.HasSolidBodies(modelDoc);
      InitializeRuntime(modelDoc);
    }

    private void InitializeRuntime(ModelDoc2 modelDoc)
    {
      Icon icon = TryLoadVelumWindowIcon();
      if (icon != null)
        Icon = icon;

      VelumConditionedReflexForbidHelper.BindForbidButton(_btnForbid, _launchConditionedReflexId, this);

      _activeConfigName = VelumDxfArtifactResolver.TryResolveDefaultExportConfigurationName(modelDoc);

      _lblDocument.Text = "Деталь: " + TryGetDocumentTitle(modelDoc);
      if (_isEmptyDocument)
        _lblMode.Text = "Режим: документ пустой — нет эскизов и твёрдых тел.";
      else if (_isSheetMetal)
        _lblMode.Text = "Режим: листовая деталь — экспорт развёртки.";
      else if (_isSketchOnly)
        _lblMode.Text = "Режим: эскиз — экспорт первого эскиза в дереве построения.";
      else
        _lblMode.Text = "Режим: проекция детали (Front / Top / Right / Back / Bottom / Left).";

      _patternBox.Text = VelumDxfFileNameHelper.GetInitialNamePattern() ?? string.Empty;
      _patternBox.TextChanged += (s, e) => UpdateResolvedPreview();
      _folderBox.Text = VelumDxfFileNameHelper.TryResolveDefaultOutputFolder(modelDoc) ?? string.Empty;
      if (_folderToolTip == null)
        _folderToolTip = new ToolTip();
      VelumBatchFormFolderBootstrap.BindFolderPathTooltip(_folderBox, _folderToolTip);
      _folderToolTip.SetToolTip(_btnSuffixes, "Настроить суффиксы маски имени DXF");
      _folderToolTip.SetToolTip(_btnBrowse, "Выбрать каталог выгрузки DXF");
      _folderToolTip.SetToolTip(_btnExport, "Экспортировать деталь в DXF");
      _folderToolTip.SetToolTip(_btnCancel, "Закрыть");

      _radioFront.CheckedChanged += OnProjectionViewChanged;
      _radioTop.CheckedChanged += OnProjectionViewChanged;
      _radioRight.CheckedChanged += OnProjectionViewChanged;
      _radioBack.CheckedChanged += OnProjectionViewChanged;
      _radioBottom.CheckedChanged += OnProjectionViewChanged;
      _radioLeft.CheckedChanged += OnProjectionViewChanged;

      if (_isSheetMetal || _isSketchOnly || _isEmptyDocument)
      {
        _radioFront.Enabled = false;
        _radioTop.Enabled = false;
        _radioRight.Enabled = false;
        _radioBack.Enabled = false;
        _radioBottom.Enabled = false;
        _radioLeft.Enabled = false;
        _viewGroup.Enabled = false;
      }
      else
      {
        // Свойство «Вид проекции dxf» или DxfDefaultProjectionView — без перебора всех плоскостей.
        VelumDxfProjectionView projectionView =
            VelumDxfFileNameHelper.TryReadProjectionViewProperty(modelDoc, _activeConfigName);
        SetInitialProjectionView(projectionView);
        ApplyProjectionViewToModel();
      }

      UpdateResolvedPreview();

      AcceptButton = _btnExport;
      // Фокус на кнопку при открытии, чтобы Enter сразу сработал.
      Shown += (s, e) =>
      {
        _btnExport.Focus();
        // На всякий случай: если фокус "убежал" на TextBox при первом показе.
        BeginInvoke(new Action(() => _btnExport.Focus()));
      };
    }

    private void SetInitialProjectionView(VelumDxfProjectionView projectionView)
    {
      _suppressViewChange = true;
      switch (projectionView)
      {
        case VelumDxfProjectionView.Top:
          _radioTop.Checked = true;
          break;
        case VelumDxfProjectionView.Right:
          _radioRight.Checked = true;
          break;
        case VelumDxfProjectionView.Back:
          _radioBack.Checked = true;
          break;
        case VelumDxfProjectionView.Bottom:
          _radioBottom.Checked = true;
          break;
        case VelumDxfProjectionView.Left:
          _radioLeft.Checked = true;
          break;
        default:
          _radioFront.Checked = true;
          break;
      }

      _suppressViewChange = false;
    }

    private void OnProjectionViewChanged(object sender, EventArgs e)
    {
      if (_suppressViewChange)
        return;

      RadioButton radio = sender as RadioButton;
      if (radio == null || !radio.Checked || _isSheetMetal || _isSketchOnly || _modelDoc == null)
        return;

      ApplyProjectionViewToModel();
    }

    private void ApplyProjectionViewToModel()
    {
      VelumDxfProjectionView view = GetSelectedProjectionView();
      if (!VelumDxfStandardViewHelper.TryShowStandardView(_modelDoc, view))
        Logger.Warning("Velum DXF dialog: не удалось переключить вид " + view);
    }

    private void OnPickSuffix(object sender, EventArgs e)
    {
      using (var picker = new VelumDxfFileNameSuffixPickerForm(_modelDoc))
      {
        if (picker.ShowDialog(this) != DialogResult.OK)
          return;

        if (string.IsNullOrWhiteSpace(picker.SelectedSuffixName))
          return;

        _patternBox.Text = (_patternBox.Text ?? string.Empty) + picker.SelectedSuffixName;
        _patternBox.SelectionStart = _patternBox.TextLength;
        _patternBox.Focus();
        PersistNamePatternSettings();
        UpdateResolvedPreview();
      }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
      if (!e.Cancel)
        PersistNamePatternSettings();

      base.OnFormClosing(e);
    }

    private void PersistNamePatternSettings()
    {
      VelumDxfFileNameHelper.PersistNamePatternSettings(_patternBox.Text ?? string.Empty);
    }

    private void OnBrowseFolder(object sender, EventArgs e)
    {
      string initial = (_folderBox.Text ?? string.Empty).Trim();
      string selected;
      if (VelumFolderBrowser.TrySelect(this, "Каталог для файла DXF", initial, out selected))
        _folderBox.Text = selected;
    }

    private void OnExport(object sender, EventArgs e)
    {
      string selectedFolder = (_folderBox.Text ?? string.Empty).Trim();
      string canonicalFolder = VelumDxfBatchDocumentHelper.TryReadDxfCatalog(_modelDoc);
      string deliveryFolder = string.Empty;
      if (string.IsNullOrWhiteSpace(canonicalFolder) || !Directory.Exists(canonicalFolder))
      {
        canonicalFolder = selectedFolder;
      }
      else if (!string.Equals(canonicalFolder, selectedFolder, StringComparison.OrdinalIgnoreCase) &&
               MessageBox.Show(
                   this,
                   "Выбранный каталог отличается от канонического «Путь dxf».\n\nДа — сменить канонический каталог.\nНет — оставить его и скопировать DXF в выбранный каталог.",
                   "Каталог DXF",
                   MessageBoxButtons.YesNo,
                   MessageBoxIcon.Question) == DialogResult.No)
      {
        deliveryFolder = selectedFolder;
      }
      else
      {
        canonicalFolder = selectedFolder;
      }

      var request = new VelumDxfExportService.ExportRequest
      {
        ModelDoc = _modelDoc,
        CanonicalFolder = canonicalFolder,
        DeliveryFolder = deliveryFolder,
        FileNamePattern = _patternBox.Text,
        TemplateContext = BuildTemplateContext(),
        ProjectionView = GetSelectedProjectionView(),
        ConfigName = _activeConfigName,
      };

      IReadOnlyList<string> configs = VelumDxfNeedFlagResolver.CollectExportable(_modelDoc);
      if (configs.Count == 0)
      {
        MessageBox.Show(
            "Нет конфигураций с «Нужен dxf = Да».",
            "Экспорт DXF",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      bool showProgress = configs.Count > 1;
      _progressBar.Minimum = 0;
      _progressBar.Maximum = Math.Max(1, configs.Count);
      _progressBar.Value = 0;
      _progressBar.Visible = showProgress;
      _lblProgress.Visible = showProgress;

      VelumDxfExportService.ExportResult exportResult;
      try
      {
        if (configs.Count == 1)
        {
          request.ConfigName = configs[0];
          exportResult = VelumDxfExportService.TryExport(request);
        }
        else
        {
          exportResult = VelumDxfExportService.TryExportAllConfigurations(
              request,
              progress =>
              {
                _progressBar.Value = Math.Min(_progressBar.Maximum, progress.CurrentIndex);
                _lblProgress.Text = "Конфигурация: " + (progress.ConfigName ?? "(default)") +
                                    " (" + progress.CurrentIndex + "/" + progress.TotalCount + ")";
                Application.DoEvents();
              });
        }
      }
      finally
      {
        _progressBar.Visible = false;
        _progressBar.Value = 0;
        _lblProgress.Visible = false;
        _lblProgress.Text = "Конфигурация:";
      }

      bool hasSuffixSkips = !string.IsNullOrEmpty(exportResult.Message) &&
          exportResult.Message.IndexOf(
              "Пропущено из-за пустых свойств суффикса",
              StringComparison.Ordinal) >= 0;
      MessageBoxIcon icon = exportResult.Success && !hasSuffixSkips
          ? MessageBoxIcon.Information
          : MessageBoxIcon.Warning;

      MessageBox.Show(
          exportResult.Message ?? (exportResult.Success ? "Экспорт выполнен." : "Экспорт не выполнен."),
          "Экспорт DXF",
          MessageBoxButtons.OK,
          icon);

      if (exportResult.Success)
      {
        DialogResult = DialogResult.OK;
        Close();
      }
    }

    private VelumDxfProjectionView GetSelectedProjectionView()
    {
      if (_radioTop.Checked)
        return VelumDxfProjectionView.Top;
      if (_radioRight.Checked)
        return VelumDxfProjectionView.Right;
      if (_radioBack.Checked)
        return VelumDxfProjectionView.Back;
      if (_radioBottom.Checked)
        return VelumDxfProjectionView.Bottom;
      if (_radioLeft.Checked)
        return VelumDxfProjectionView.Left;
      return VelumDxfProjectionView.Front;
    }

    private void UpdateResolvedPreview()
    {
      if (_modelDoc == null)
      {
        _resolvedValuesLabel.Text = string.Empty;
        return;
      }

      string fullName = VelumDxfFileNameHelper.BuildFullFileName(
          _patternBox.Text,
          _modelDoc,
          _activeConfigName,
          BuildTemplateContext());

      _resolvedValuesLabel.Text = string.IsNullOrWhiteSpace(fullName)
          ? string.Empty
          : fullName;
      _resolvedValuesLabel.ForeColor = string.IsNullOrWhiteSpace(fullName)
          ? SystemColors.GrayText
          : SystemColors.ControlText;
    }

    private IReadOnlyDictionary<string, string> BuildTemplateContext()
    {
      return VelumDxfFileNameHelper.BuildTemplateContext(_modelDoc);
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

      return "(деталь)";
    }

    private static Icon TryLoadVelumWindowIcon()
    {
      return VelumFormIcon.TryLoad();
    }

  }
}
