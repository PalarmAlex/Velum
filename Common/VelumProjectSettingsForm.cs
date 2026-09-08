using ISIDA.Actions;
using ISIDA.Common;
using ISIDA.Gomeostas;
using ISIDA.Psychic.Understanding;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Velum.Configuration;
using Velum.Isida;

namespace Velum.UI
{
  /// <summary>
  /// Редактирование <see cref="VelumAppConfig"/> по блокам (пути, регуляция, аналитика).
  /// Разметка в <see cref="InitializeComponent"/>
  /// </summary>
  public sealed partial class VelumProjectSettingsForm : Form
  {
    private bool _suppressStageEvents;

    /// <summary>Последняя стадия, синхронизированная с движком (для отката списка при отмене перехода).</summary>
    private int _lastEngineStage = -1;

    /// <summary>Вызывается при смене стадии (не при программной синхронизации). Вернуть false — откатить выбор. Устанавливается из надстройки.</summary>
    public Func<int, bool> StageTransitionHandler { get; set; }

    /// <summary>
    /// После успешного сохранения XML: если пользователь согласился на перезагрузку, вызывается из формы (например
    /// <c>VelumIsidaHost.Shutdown</c> + <c>TryInitialize</c> в надстройке). Может быть null.
    /// </summary>
    public Action ReloadRuntimeAfterSettingsSave { get; set; }

    private sealed class IdNameItem
    {
      public int Id { get; set; }
      public string Name { get; set; }
    }

    /// <summary>
    /// Перед показом формы из надстройки: проверяет уровень доступа и пульсацию.
    /// При <c>ProductRegistryAccessLevel</c> отличном от <c>admin</c> или при включённой пульсации
    /// выводит сообщение и возвращает false.
    /// </summary>
    /// <param name="owner">Родитель для окна сообщения (может быть null).</param>
    /// <returns>true, если форму настроек можно открыть.</returns>
    public static bool TryAllowOpenProjectSettings(IWin32Window owner)
    {
      IWin32Window messageOwner = owner is Control c ? c : null;

      if (!VelumAdminAccess.TryRequireAdmin(
          messageOwner,
          "Доступ к настройкам проекта разрешён только администраторам проекта."))
        return false;

      if (!GlobalTimer.IsPulsationRunning)
        return true;

      MessageBox.Show(
          messageOwner,
          "Настройки проекта недоступны во время пульсации. Сначала выключите пульсацию.",
          "Velum",
          MessageBoxButtons.OK,
          MessageBoxIcon.Information);
      return false;
    }

    /// <summary>
    /// Форма настроек проекта
    /// </summary>
    public VelumProjectSettingsForm()
    {
      VelumAppConfig.EnsureInitialized();
      InitializeComponent();
      VelumFormHelp.Bind(this, VelumHelpTopics.ProjectSettings);
      LoadFromConfig();
      PopulateCombos();
      WireEvolutionStageCombo();
      BindControlToolTips();
      Shown += (_, __) => RefreshEvolutionStageUiFromEngine();
      this.ClientSize = new Size(720, 407);
      this.MinimumSize = new Size(720, 407);
    }

    private void BindControlToolTips()
    {
      _ttpStageEvolution.SetToolTip(_btnBrowseProductRegistry, "Выбрать каталог реестра документов");
      _ttpStageEvolution.SetToolTip(_btnBrowseBomExchange, "Выбрать каталог обмена с 1С");
      _ttpStageEvolution.SetToolTip(_btnBrowseScenario, "Выбрать каталог отчётов сценариев");
      _ttpStageEvolution.SetToolTip(_btnBrowseBoot, "Выбрать каталог boot-данных");
      _ttpStageEvolution.SetToolTip(_btnBrowseLogs, "Выбрать каталог логов");
      _ttpStageEvolution.SetToolTip(_btnBrowseGomeostas, "Выбрать каталог данных гомеостаза");
      _ttpStageEvolution.SetToolTip(_btnBrowseSettings, "Выбрать каталог настроек");
      _ttpStageEvolution.SetToolTip(_chkLog, "Включить запись лога событий");
      _ttpStageEvolution.SetToolTip(_chkSolidHomeostasisDebugLog, "Включить отладочные логи SolidHomeostasis (VelumSolidDiagLog, ProductRegistryIndexTrace, Trace.WriteLine)");
      _ttpStageEvolution.SetToolTip(_chkHomeostasisPulseDrift, "Дрейф параметров гомеостаза по Speed на каждом пульсе");
      _ttpStageEvolution.SetToolTip(_chkFirstRun, "Режим первого запуска (инициализация)");
      _ttpStageEvolution.SetToolTip(_chkCommandBufferRecording, "Писать команды SW (sw:*) в буфер для разбора");
      _ttpStageEvolution.SetToolTip(_btnSave, "Сохранить настройки проекта");
      _ttpStageEvolution.SetToolTip(_btnCancel, "Закрыть без сохранения");
      _ttpStageEvolution.SetToolTip(_chkNeedDxfDefault, "Дефолтное значение свойства «Нужен dxf» при создании новых деталей");
      _ttpStageEvolution.SetToolTip(_chkNeedPdfDefault, "Дефолтное значение свойства «Нужен pdf» при создании новых чертежей");
      _ttpStageEvolution.SetToolTip(_chkNeedDrawingDefault, "Дефолтное значение свойства «Нужен чертеж» при создании новых деталей и сборок");
    }

    private void PathBrowse_Click(object sender, EventArgs e)
    {
      if (sender is Button b && b.Tag is TextBox tb)
        Browse(this, tb);
    }

    private static void Browse(IWin32Window owner, TextBox target)
    {
      string initial = null;
      try
      {
        initial = target.Text?.Trim();
      }
      catch
      {
        // ignore
      }

      string selected;
      if (VelumFolderBrowser.TrySelect(owner, "Выберите каталог", initial, out selected))
        target.Text = selected;
    }

    private void LoadFromConfig()
    {
      _tbSettingsPath.Text = VelumAppConfig.SettingsPath ?? string.Empty;
      _tbGomeostas.Text = VelumAppConfig.DataFolderPath ?? string.Empty;
      _tbLogs.Text = VelumAppConfig.LogsFolderPath ?? string.Empty;
      _tbBoot.Text = VelumAppConfig.BootDataFolderPath ?? string.Empty;
      _tbScenarioReports.Text = VelumAppConfig.ScenarioReportsFolderPath ?? string.Empty;
      _tbProductRegistry.Text = VelumAppConfig.ProductRegistryFolderPath ?? string.Empty;
      _tbBomExchange.Text = VelumAppConfig.BomExchangeFolder ?? string.Empty;

      _tbCompare.Text = VelumAppConfig.CompareLevel.ToString(CultureInfo.InvariantCulture);
      _tbDifSensor.Text = VelumAppConfig.DifSensorPar.ToString(CultureInfo.InvariantCulture);
      _tbDynamic.Text = VelumAppConfig.DynamicTime.ToString(CultureInfo.InvariantCulture);
      _tbReflexDur.Text = VelumAppConfig.ReflexActionDisplayDuration.ToString(CultureInfo.InvariantCulture);
      _tbRecognition.Text = VelumAppConfig.RecognitionThreshold.ToString(CultureInfo.InvariantCulture);

      _chkLog.Checked = VelumAppConfig.LogEnabled;

      _chkSolidHomeostasisDebugLog.Checked = VelumAppConfig.SolidHomeostasisDebugLog;

      _chkVerbalAuthoritative.Checked = VelumAppConfig.VerbalAuthoritativeMode;
      _chkObservationMode.Checked = VelumAppConfig.ObservationMode;

      _chkHomeostasisPulseDrift.Checked = VelumAppConfig.HomeostasisPulseSpeedDriftEnabled;

      _tbWaitOperator.Text = VelumAppConfig.WaitingPeriodForActionsVal.ToString(CultureInfo.InvariantCulture);
      _tbCycleDiv.Text = VelumAppConfig.ThinkingCycleDecayAgeDivisor.ToString(CultureInfo.InvariantCulture);
      _tbCycleBase.Text = VelumAppConfig.ThinkingCycleDecayBase.ToString(CultureInfo.InvariantCulture);
      _tbMainMaxAge.Text = VelumAppConfig.ThinkingCycleMainMaxAgePulses.ToString(CultureInfo.InvariantCulture);
      _tbSilence.Text = VelumAppConfig.NoOperatorStimulusSilencePulses.ToString(CultureInfo.InvariantCulture);

      _tbSolidMetricEpsilon.Text = VelumAppConfig.SolidEnvironmentMetricDeltaEpsilon.ToString(CultureInfo.InvariantCulture);
      _tbSolidHostMinDelta.Text = VelumAppConfig.SolidHostImpulseMinParameterDelta.ToString(CultureInfo.InvariantCulture);
      _tbHeavyMetricsPulsePeriod.Text = VelumAppConfig.HeavyMetricsPulsePeriod.ToString(CultureInfo.InvariantCulture);

      _tbDefaultGeneticReflexId.Text = VelumAppConfig.DefaultGeneticReflexId.ToString(CultureInfo.InvariantCulture);
      _chkFirstRun.Checked = VelumAppConfig.FirstRun != 0;
      _chkCommandBufferRecording.Checked = VelumAppConfig.CommandBufferRecordingEnabled;
      _tbCommandBufferIdleFlushSec.Text = (VelumAppConfig.CommandBufferIdleFlushMs / 1000).ToString(CultureInfo.InvariantCulture);
      _tbCommandBufferMaxTokens.Text = VelumAppConfig.CommandBufferMaxTokens.ToString(CultureInfo.InvariantCulture);
      _tbCommandBufferMaxAgeSec.Text = (VelumAppConfig.CommandBufferMaxAgeMs / 1000).ToString(CultureInfo.InvariantCulture);

      // Вкладка «Документы»: дефолты свойств новых документов
      _chkNeedDxfDefault.Checked = VelumAppConfig.NeedDxfDefault;
      _chkNeedPdfDefault.Checked = VelumAppConfig.NeedPdfDefault;
      _chkNeedDrawingDefault.Checked = VelumAppConfig.NeedDrawingDefault;

      // Стадия 2: коды стилей Поиск/Игра
      var stage2StyleIds = VelumAppConfig.Stage2SearchPlayStyleIds;
      _tbStage2SearchPlayStyleIds.Text = stage2StyleIds != null && stage2StyleIds.Count > 0
          ? string.Join(",", stage2StyleIds)
          : "3,5,7";
    }

    private void PopulateCombos()
    {
      FillLogFormats();
      FillStyles();
      FillAdaptiveActions();
      FillThemes();

      SelectComboById(_cmbStyle, VelumAppConfig.DefaultStileId);
      SelectComboById(_cmbAdaptive, VelumAppConfig.DefaultAdaptiveActionId);
      SelectComboById(_cmbTheme, VelumAppConfig.DefaultThemeTypeId);
      SelectLogFormat(VelumAppConfig.LogFormat);
      FillEvolutionStageItems();
    }

    private void FillLogFormats()
    {
      _cmbLogFormat.Items.Clear();
      _cmbLogFormat.Items.Add(new IdNameItem { Id = (int)ResearchLogger.LogFormat.None, Name = "Нет" });
      _cmbLogFormat.Items.Add(new IdNameItem { Id = (int)ResearchLogger.LogFormat.JsonL, Name = "JSON" });
      _cmbLogFormat.Items.Add(new IdNameItem { Id = (int)ResearchLogger.LogFormat.Csv, Name = "CSV" });
      _cmbLogFormat.Items.Add(new IdNameItem { Id = (int)ResearchLogger.LogFormat.All, Name = "Все" });
      _cmbLogFormat.DisplayMember = nameof(IdNameItem.Name);
      _cmbLogFormat.ValueMember = nameof(IdNameItem.Id);
    }

    private void SelectLogFormat(ResearchLogger.LogFormat format)
    {
      int id = (int)format;
      for (int i = 0; i < _cmbLogFormat.Items.Count; i++)
      {
        if (_cmbLogFormat.Items[i] is IdNameItem it && it.Id == id)
        {
          _cmbLogFormat.SelectedIndex = i;
          return;
        }
      }

      _cmbLogFormat.SelectedIndex = 0;
    }

    private ResearchLogger.LogFormat GetSelectedLogFormat()
    {
      if (_cmbLogFormat.SelectedItem is IdNameItem it &&
          Enum.IsDefined(typeof(ResearchLogger.LogFormat), it.Id))
        return (ResearchLogger.LogFormat)it.Id;
      return ResearchLogger.LogFormat.All;
    }

    private void FillStyles()
    {
      _cmbStyle.Items.Clear();
      _cmbStyle.DisplayMember = nameof(IdNameItem.Name);
      _cmbStyle.Items.Add(new IdNameItem { Id = 0, Name = "Нет" });

      if (VelumIsidaHost.TryInitialize(out _))
      {
        try
        {
          var styles = VelumIsidaHost.Context.Gomeostas.GetAllBehaviorStyles();
          if (styles != null)
          {
            foreach (var st in styles.Values.OrderBy(s => s.Id))
              _cmbStyle.Items.Add(new IdNameItem { Id = st.Id, Name = st.Name ?? ("#" + st.Id) });
          }
        }
        catch
        {
          // оставляем только «Нет»
        }
      }
    }

    private void FillAdaptiveActions()
    {
      _cmbAdaptive.Items.Clear();
      _cmbAdaptive.DisplayMember = nameof(IdNameItem.Name);
      _cmbAdaptive.Items.Add(new IdNameItem { Id = 0, Name = "Нет" });

      try
      {
        if (!VelumIsidaHost.TryInitialize(out _))
          return;

        string actionsPath = IsidaDataPaths.ResolveActionsFolder(_tbGomeostas.Text?.Trim());
        if (string.IsNullOrEmpty(actionsPath))
          actionsPath = IsidaDataPaths.ResolveActionsFolder(VelumAppConfig.DataFolderPath);

        if (!AdaptiveActionsSystem.IsInitialized && !string.IsNullOrEmpty(actionsPath))
          AdaptiveActionsSystem.InitializeInstance(VelumIsidaHost.Context.Gomeostas, actionsPath);

        if (AdaptiveActionsSystem.IsInitialized)
        {
          foreach (var a in AdaptiveActionsSystem.Instance.GetAllAdaptiveActions().OrderBy(x => x.Id))
            _cmbAdaptive.Items.Add(new IdNameItem { Id = a.Id, Name = a.Name ?? ("#" + a.Id) });
        }
      }
      catch
      {
        // только «Нет»
      }
    }

    private void FillThemes()
    {
      _cmbTheme.Items.Clear();
      _cmbTheme.DisplayMember = nameof(IdNameItem.Name);
      _cmbTheme.Items.Add(new IdNameItem { Id = 0, Name = "Нет темы" });
      foreach (var pair in ThemeImageSystem.GetDefaultThemeTypesForSettings())
        _cmbTheme.Items.Add(new IdNameItem { Id = pair.Id, Name = pair.Description });
    }

    private static void SelectComboById(ComboBox cmb, int id)
    {
      for (int i = 0; i < cmb.Items.Count; i++)
      {
        if (cmb.Items[i] is IdNameItem it && it.Id == id)
        {
          cmb.SelectedIndex = i;
          return;
        }
      }

      if (cmb.Items.Count > 0)
        cmb.SelectedIndex = 0;
    }

    private static int GetComboId(ComboBox cmb)
    {
      return (cmb.SelectedItem as IdNameItem)?.Id ?? 0;
    }

    private void OnSaveClick(object sender, EventArgs e)
    {
      if (!TryValidateAndSave())
        return;

      DialogResult = DialogResult.OK;
      Close();
    }

    private bool TryValidateAndSave()
    {
      if (!int.TryParse(_tbCompare.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int compare))
      {
        MessageBox.Show(this, "Некорректное значение интегрального порога.", "Проверка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
      }

      string difText = _tbDifSensor.Text.Trim().Replace(',', '.');
      if (!float.TryParse(difText, NumberStyles.Float, CultureInfo.InvariantCulture, out float difPar))
      {
        MessageBox.Show(this, "Некорректное значение мин. шага параметра.", "Проверка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
      }

      if (!int.TryParse(_tbDynamic.Text.Trim(), out int dyn) ||
          !int.TryParse(_tbReflexDur.Text.Trim(), out int reflexDur) ||
          !int.TryParse(_tbRecognition.Text.Trim(), out int recognition))
      {
        MessageBox.Show(this, "Некорректные целочисленные параметры регуляции.", "Проверка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
      }

      if (!int.TryParse(_tbWaitOperator.Text.Trim(), out int waitOp) ||
          !int.TryParse(_tbCycleDiv.Text.Trim(), out int cycleDiv) ||
          !int.TryParse(_tbCycleBase.Text.Trim(), out int cycleBase) ||
          !int.TryParse(_tbMainMaxAge.Text.Trim(), out int mainAge) ||
          !int.TryParse(_tbSilence.Text.Trim(), out int silence))
      {
        MessageBox.Show(this, "Некорректные целочисленные параметры аналитики.", "Проверка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
      }

      var v1 = SettingsValidator.ValidateCompareLevel(compare);
      var v2 = SettingsValidator.ValidateDifSensorPar(difPar);
      var v3 = SettingsValidator.ValidateDynamicTime(dyn);
      var v4 = SettingsValidator.ValidateRecognitionThreshold(recognition);
      foreach (var v in new[] { v1, v2, v3, v4 })
      {
        if (!v.isValid)
        {
          MessageBox.Show(this, v.errorMessage, "Проверка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
          return false;
        }
      }

      if (reflexDur >= dyn)
      {
        MessageBox.Show(
            this,
            "Время удержания действий не может быть больше или равно времени удержания состояний.",
            "Проверка",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return false;
      }

      if (cycleDiv < 1)
      {
        MessageBox.Show(this, "Делитель возраста цикла (A) должен быть не меньше 1.", "Проверка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
      }

      if (cycleBase < 0)
      {
        MessageBox.Show(this, "Базовое снятие веса (B) не может быть отрицательным.", "Проверка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
      }

      if (mainAge < 1)
      {
        MessageBox.Show(this, "Максимальный возраст главного цикла (пульсов) должен быть не меньше 1.", "Проверка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
      }

      if (silence < 1)
      {
        MessageBox.Show(this, "Порог простоя оператора должен быть не меньше 1 пульса.", "Проверка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
      }

      string solidEpsText = _tbSolidMetricEpsilon.Text.Trim().Replace(',', '.');
      string solidMinText = _tbSolidHostMinDelta.Text.Trim().Replace(',', '.');
      if (!float.TryParse(solidEpsText, NumberStyles.Float, CultureInfo.InvariantCulture, out float solidEps) ||
          !float.TryParse(solidMinText, NumberStyles.Float, CultureInfo.InvariantCulture, out float solidMinDelta))
      {
        MessageBox.Show(this, "Некорректные числовые параметры метрики среды SolidWorks.", "Проверка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
      }

      // Стадия 2: коды стилей Поиск/Игра
      var stage2StyleIdsText = _tbStage2SearchPlayStyleIds.Text.Trim();
      var stage2StyleIds = stage2StyleIdsText
          .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
          .Select(s =>
          {
            int id;
            return int.TryParse(s.Trim(), out id) ? id : 0;
          })
          .Where(id => id > 0)
          .ToList();

      if (stage2StyleIds.Count == 0)
      {
        MessageBox.Show(
            this,
            "Некорректные коды стилей Поиск/Игра. Укажите хотя бы один положительный ID стиля.",
            "Проверка",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return false;
      }

      VelumAppConfig.SetSetting("Stage2SearchPlayStyleIds", string.Join(",", stage2StyleIds));

      // Сохраняем все остальные настройки
      VelumAppConfig.SetSetting("CompareLevel", compare.ToString(CultureInfo.InvariantCulture));
      VelumAppConfig.SetSetting("DifSensorPar", difPar.ToString(CultureInfo.InvariantCulture));
      VelumAppConfig.SetSetting("DynamicTime", dyn.ToString(CultureInfo.InvariantCulture));
      VelumAppConfig.SetSetting("ReflexActionDisplayDuration", reflexDur.ToString(CultureInfo.InvariantCulture));
      VelumAppConfig.SetSetting("RecognitionThreshold", recognition.ToString(CultureInfo.InvariantCulture));
      VelumAppConfig.SetSetting("WaitingPeriodForActionsVal", waitOp.ToString(CultureInfo.InvariantCulture));
      VelumAppConfig.SetSetting("ThinkingCycleDecayAgeDivisor", cycleDiv.ToString(CultureInfo.InvariantCulture));
      VelumAppConfig.SetSetting("ThinkingCycleDecayBase", cycleBase.ToString(CultureInfo.InvariantCulture));
      VelumAppConfig.SetSetting("ThinkingCycleMainMaxAgePulses", mainAge.ToString(CultureInfo.InvariantCulture));
      VelumAppConfig.SetSetting("NoOperatorStimulusSilencePulses", silence.ToString(CultureInfo.InvariantCulture));
      VelumAppConfig.SetSetting("SolidEnvironmentMetricDeltaEpsilon", solidEps.ToString(CultureInfo.InvariantCulture));
      VelumAppConfig.SetSetting("SolidHostImpulseMinParameterDelta", solidMinDelta.ToString(CultureInfo.InvariantCulture));
      VelumAppConfig.SetSetting("SolidHomeostasisPulseTrace", _chkHomeostasisPulseDrift.Checked.ToString());
      VelumAppConfig.SetSetting("VerbalAuthoritativeMode", _chkVerbalAuthoritative.Checked.ToString());
      VelumAppConfig.SetSetting("ObservationMode", _chkObservationMode.Checked.ToString());
      VelumAppConfig.SetSetting("LogEnabled", _chkLog.Checked.ToString());
      VelumAppConfig.SetSetting("SolidHomeostasisDebugLog", _chkSolidHomeostasisDebugLog.Checked.ToString());
      VelumAppConfig.SetSetting("LogFormat", GetSelectedLogFormat().ToString());
      VelumAppConfig.SetSetting("DefaultStileId", _cmbStyle.SelectedIndex >= 0 ? ((IdNameItem)_cmbStyle.SelectedItem).Id.ToString(CultureInfo.InvariantCulture) : "0");
      VelumAppConfig.SetSetting("DefaultAdaptiveActionId", _cmbAdaptive.SelectedIndex >= 0 ? ((IdNameItem)_cmbAdaptive.SelectedItem).Id.ToString(CultureInfo.InvariantCulture) : "0");
      VelumAppConfig.SetSetting("DefaultThemeTypeId", _cmbTheme.SelectedIndex >= 0 ? ((IdNameItem)_cmbTheme.SelectedItem).Id.ToString(CultureInfo.InvariantCulture) : "0");
      VelumAppConfig.SetSetting("WaitingPeriodForActionsVal", waitOp.ToString(CultureInfo.InvariantCulture));
      VelumAppConfig.SetSetting("FirstRun", _chkFirstRun.Checked ? "1" : "0");
      VelumAppConfig.SetSetting("DefaultGeneticReflexId", _tbDefaultGeneticReflexId.Text.Trim());
      VelumAppConfig.SetSetting("CommandBufferRecordingEnabled", _chkCommandBufferRecording.Checked.ToString());
      VelumAppConfig.SetSetting("CommandBufferIdleFlushMs", (_tbCommandBufferIdleFlushSec.Text.Trim() != string.Empty ? int.Parse(_tbCommandBufferIdleFlushSec.Text.Trim()) * 1000 : 3000).ToString(CultureInfo.InvariantCulture));
      VelumAppConfig.SetSetting("CommandBufferMaxTokens", _tbCommandBufferMaxTokens.Text.Trim());
      VelumAppConfig.SetSetting("CommandBufferMaxAgeMs", (_tbCommandBufferMaxAgeSec.Text.Trim() != string.Empty ? int.Parse(_tbCommandBufferMaxAgeSec.Text.Trim()) * 1000 : 120000).ToString(CultureInfo.InvariantCulture));
      VelumAppConfig.SetSetting("HeavyMetricsPulsePeriod", _tbHeavyMetricsPulsePeriod.Text.Trim());

      // Вкладка «Документы»: дефолты свойств новых документов
      VelumAppConfig.SetNeedDxfDefault(_chkNeedDxfDefault.Checked);
      VelumAppConfig.SetNeedPdfDefault(_chkNeedPdfDefault.Checked);
      VelumAppConfig.SetNeedDrawingDefault(_chkNeedDrawingDefault.Checked);

      // Пути
      VelumAppConfig.SetSetting("SettingsPath", _tbSettingsPath.Text.Trim());
      VelumAppConfig.SetSetting("DataFolderPath", _tbGomeostas.Text.Trim());
      VelumAppConfig.SetSetting("LogsFolderPath", _tbLogs.Text.Trim());
      VelumAppConfig.SetSetting("BootDataFolderPath", _tbBoot.Text.Trim());
      VelumAppConfig.SetSetting("ScenarioReportsFolderPath", _tbScenarioReports.Text.Trim());
      VelumAppConfig.SetSetting("ProductRegistryFolderPath", _tbProductRegistry.Text.Trim());
      VelumAppConfig.SetBomExchangeFolder(_tbBomExchange.Text.Trim());

      Logger.Info("Настройки проекта сохранены.");
      return true;
    }

    /// <summary>
    /// Валидация кодов стилей Поиск/Игра в реальном времени.
    /// </summary>
    private void Stage2StyleIds_TextChanged(object sender, EventArgs e)
    {
      var text = _tbStage2SearchPlayStyleIds.Text.Trim();
      if (string.IsNullOrEmpty(text))
        return;

      var ids = text
          .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
          .Select(s =>
          {
            int id;
            return int.TryParse(s.Trim(), out id) ? id : 0;
          })
          .Where(id => id > 0)
          .ToList();

      if (ids.Count == 0)
      {
        _tbStage2SearchPlayStyleIds.ForeColor = System.Drawing.Color.Red;
      }
      else
      {
        _tbStage2SearchPlayStyleIds.ForeColor = System.Drawing.Color.Black;
      }
    }

    private void WireEvolutionStageCombo()
    {
      stage_evolution.SelectionChangeCommitted += StageEvolution_SelectionChangeCommitted;
      stage_evolution.DropDown += StageEvolution_OnDropDownOrClosed;
      stage_evolution.DropDownClosed += StageEvolution_OnDropDownOrClosed;
      stage_evolution.MouseMove += StageEvolution_MouseMove;
      stage_evolution.MouseEnter += StageEvolution_MouseEnter;
    }

    private void FillEvolutionStageItems()
    {
      stage_evolution.Items.Clear();
      if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
        return;

      for (int i = 0; i <= 5; i++)
        stage_evolution.Items.Add(new EvolutionStageListItem(i));
    }

    private void StageEvolution_OnDropDownOrClosed(object sender, EventArgs e)
    {
      UpdateStageEvolutionTooltip();
    }

    private void StageEvolution_SelectionChangeCommitted(object sender, EventArgs e)
    {
      if (_suppressStageEvents || StageTransitionHandler == null)
        return;

      if (!(stage_evolution.SelectedItem is EvolutionStageListItem item))
        return;

      int newStage = item.StageNumber;
      if (newStage == _lastEngineStage)
        return;

      bool ok = StageTransitionHandler(newStage);
      if (!ok)
      {
        RunWithStageEventsSuppressed(() =>
        {
          if (_lastEngineStage >= 0 && _lastEngineStage < stage_evolution.Items.Count)
            stage_evolution.SelectedIndex = _lastEngineStage;
        });
      }

      UpdateStageEvolutionTooltip();
    }

    private void StageEvolution_MouseEnter(object sender, EventArgs e)
    {
      UpdateStageEvolutionTooltip();
    }

    private void StageEvolution_MouseMove(object sender, MouseEventArgs e)
    {
      UpdateStageEvolutionTooltip();
    }

    private void UpdateStageEvolutionTooltip()
    {
      if (stage_evolution == null || _ttpStageEvolution == null)
        return;

      int idx = stage_evolution.SelectedIndex >= 0 ? stage_evolution.SelectedIndex : 0;
      string tip = EvolutionStageListItem.GetFullDescription(idx);
      _ttpStageEvolution.SetToolTip(stage_evolution, tip);
    }

    /// <summary>Синхронизирует номер стадии и доступность списка с состоянием ISIDA (вызывать из потока UI).</summary>
    public void RefreshEvolutionStageUiFromEngine()
    {
      if (IsDisposed)
        return;

      if (InvokeRequired)
      {
        BeginInvoke(new Action(RefreshEvolutionStageUiFromEngine));
        return;
      }

      if (!VelumIsidaHost.TryInitialize(out _))
      {
        stage_evolution.Enabled = false;
        UpdateStageEvolutionTooltip();
        return;
      }

      try
      {
        int stage = AppGlobalState.EvolutionStage;
        bool pulsing = GlobalTimer.IsPulsationRunning;

        stage_evolution.Enabled = !pulsing;

        RunWithStageEventsSuppressed(() =>
        {
          if (stage >= 0 && stage < stage_evolution.Items.Count)
          {
            stage_evolution.SelectedIndex = stage;
            _lastEngineStage = stage;
          }
        });

        UpdateStageEvolutionTooltip();
      }
      catch
      {
        stage_evolution.Enabled = false;
        UpdateStageEvolutionTooltip();
      }
    }

    /// <summary>Полупрозрачное перекрытие на время долгой очистки при возврате на предыдущую стадию.</summary>
    public void SetEvolutionStageBusyOverlayVisible(bool visible)
    {
      if (IsDisposed)
        return;

      if (InvokeRequired)
      {
        BeginInvoke(new Action(() => SetEvolutionStageBusyOverlayVisible(visible)));
        return;
      }

      _pnlStageBusyOverlay.Visible = visible;
      _pnlStageBusyOverlay.Enabled = visible;
      if (visible)
        _pnlStageBusyOverlay.BringToFront();
    }

    private void RunWithStageEventsSuppressed(Action action)
    {
      _suppressStageEvents = true;
      try
      {
        action();
      }
      finally
      {
        _suppressStageEvents = false;
      }
    }
  }
}
