using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using ISIDA.Common;
using Velum.Configuration;

namespace Velum.UI
{
  /// <summary>
  /// Логика вкладки «У-рефлексы» (параметры модели условных рефлексов).
  /// Разметка вкладки — в <c>InitializeComponent</c> (как остальные вкладки),
  /// здесь только чтение/валидация/сохранение файла
  /// <c>%ProgramData%\VELUM\Data\Reflexes\ConditionedReflexSettings.dat</c>
  /// через <see cref="VelumConditionedReflexSettingsStore"/>.
  /// </summary>
  public sealed partial class VelumProjectSettingsForm
  {
    /// <summary>
    /// Полный путь к <c>ConditionedReflexSettings.dat</c>: каталог рефлексов из каталога
    /// данных гомеостаза (там же, где его читает/пишет ISIDA).
    /// </summary>
    private static string ResolveConditionedReflexSettingsPath()
    {
      string reflexesFolder = IsidaDataPaths.ResolveReflexesFolder(VelumAppConfig.DataFolderPath);
      return Path.Combine(reflexesFolder, VelumConditionedReflexSettingsStore.FileName);
    }

    /// <summary>
    /// Читает файл настроек у-рефлексов и заполняет поля вкладки. Если файл был в старом
    /// формате (запятая вместо точки) или неполном — предупреждает, что при сохранении
    /// он будет перезаписан в корректном формате.
    /// </summary>
    private void LoadConditionedReflexTab()
    {
      try
      {
        VelumConditionedReflexSettingsLoadResult load =
            VelumConditionedReflexSettingsStore.Load(ResolveConditionedReflexSettingsPath());
        ApplyConditionedReflexValues(load.Model);

        if (load.NeedsRewrite && load.Notes.Count > 0)
        {
          MessageBox.Show(
              this,
              "Файл настроек у-рефлексов требует обновления формата:\n\n  • " +
              string.Join("\n  • ", load.Notes) +
              "\n\nПри сохранении он будет перезаписан в корректном виде.",
              "У-рефлексы",
              MessageBoxButtons.OK,
              MessageBoxIcon.Information);
        }

        // Валидация и на загрузке (случай 33): битый файл может содержать значения,
        // которые парсятся как числа, но выведены из физического смысла (например,
        // потеряна десятичная точка). Молча подставить их в поля нельзя — предупредим,
        // чтобы пользователь исправил и сохранил, иначе движок не сможет активировать УР.
        List<string> loadErrors = VelumConditionedReflexSettingsStore.Validate(load.Model);
        if (loadErrors.Count > 0)
        {
          MessageBox.Show(
              this,
              "Внимание: значения настроек у-рефлексов вне допустимых диапазонов " +
              "(возможно, файл повреждён):\n\n  • " + string.Join("\n  • ", loadErrors) +
              "\n\nИсправьте значения и сохраните — иначе условные рефлексы не будут " +
              "создаваться и удерживаться.",
              "У-рефлексы",
              MessageBoxButtons.OK,
              MessageBoxIcon.Warning);
        }
      }
      catch (Exception ex)
      {
        MessageBox.Show(
            this,
            "Не удалось прочитать настройки у-рефлексов: " + ex.Message,
            "У-рефлексы",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
      }
    }

    /// <summary>Заполняет поля вкладки значениями из модели.</summary>
    private void ApplyConditionedReflexValues(VelumConditionedReflexSettingsModel m)
    {
      _tbCrxLearningRate.Text = F(m.LearningRate);
      _tbCrxDecayRate.Text = F(m.DecayRate);
      _tbCrxActivationThreshold.Text = F(m.ActivationThreshold);
      _tbCrxMinAssociationStrength.Text = F(m.MinAssociationStrength);
      _tbCrxTimeWindowPulses.Text = I(m.TimeWindowPulses);
      _tbCrxInitialLifetimePulses.Text = I(m.InitialLifetimePulses);
      _tbCrxActiveExtinctionRate.Text = F(m.ActiveExtinctionRate);
      _tbCrxHigherOrderStrengthReductionCoefficient.Text = F(m.HigherOrderStrengthReductionCoefficient);
      _tbCrxCompetitionStrengthRatioThreshold.Text = F(m.CompetitionStrengthRatioThreshold);
      _chkCrxTieBreakPreferSmallerReflexId.Checked = m.TieBreakPreferSmallerReflexId;
      _tbCrxPassiveDecayPeriodPulses.Text = I(m.PassiveDecayPeriodPulses);
      _tbCrxPassiveDecayFallbackPeriodPulses.Text = I(m.PassiveDecayFallbackPeriodPulses);
      _chkCrxEnableCompetitiveLearning.Checked = m.EnableCompetitiveLearning;
      _tbCrxCompetitionSuppressionCoefficient.Text = F(m.CompetitionSuppressionCoefficient);
      _tbCrxInitialStrengthBonus.Text = F(m.InitialStrengthBonus);
      _tbCrxAuthoritativeStrength.Text = F(m.AuthoritativeStrength);
      _tbCrxEstablishedStrengthThreshold.Text = F(m.EstablishedStrengthThreshold);
      _tbCrxActivationReinforcementFraction.Text = F(m.ActivationReinforcementFraction);
      _tbCrxMaxLifetimePulsesCap.Text = I(m.MaxLifetimePulsesCap);
      _tbCrxSensoryDecayPeriodPulses.Text = I(m.SensoryDecayPeriodPulses);
      _tbCrxSensoryStrengthFloor.Text = F(m.SensoryStrengthFloor);
      _tbCrxSensoryHighStrengthThreshold.Text = F(m.SensoryHighStrengthThreshold);
      _tbCrxSensoryHighStrengthDecayRate.Text = F(m.SensoryHighStrengthDecayRate);
      _tbCrxSensoryMidStrengthThreshold.Text = F(m.SensoryMidStrengthThreshold);
    }

    /// <summary>
    /// Собирает и валидирует поля вкладки без записи файла. Возвращает false
    /// (и переключает вкладку на проблемную), если есть ошибки ввода.
    /// </summary>
    private bool ValidateConditionedReflexTab(out VelumConditionedReflexSettingsModel model)
    {
      model = new VelumConditionedReflexSettingsModel();

      if (!TryCollectConditionedReflexValues(model, out string collectError))
      {
        tabControl1.SelectedTab = tabPageReflexes;
        MessageBox.Show(this, collectError, "У-рефлексы", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
      }

      List<string> errors = VelumConditionedReflexSettingsStore.Validate(model);
      if (errors.Count > 0)
      {
        tabControl1.SelectedTab = tabPageReflexes;
        MessageBox.Show(
            this,
            "Проверьте параметры у-рефлексов:\n\n  • " + string.Join("\n  • ", errors),
            "У-рефлексы",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return false;
      }

      return true;
    }

    /// <summary>
    /// Сохраняет уже провалидированную модель у-рефлексов в <c>.dat</c>.
    /// Возвращает false (и переключает вкладку), если запись не удалась.
    /// </summary>
    private bool SaveConditionedReflexModel(VelumConditionedReflexSettingsModel model)
    {
      try
      {
        VelumConditionedReflexSettingsStore.Save(ResolveConditionedReflexSettingsPath(), model);
      }
      catch (Exception ex)
      {
        tabControl1.SelectedTab = tabPageReflexes;
        MessageBox.Show(
            this,
            "Не удалось сохранить настройки у-рефлексов: " + ex.Message,
            "У-рефлексы",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return false;
      }

      return true;
    }

    /// <summary>
    /// Собирает значения полей вкладки в модель. Возвращает false и сообщение,
    /// если какое-то число не разбирается.
    /// </summary>
    private bool TryCollectConditionedReflexValues(
        VelumConditionedReflexSettingsModel m, out string error)
    {
      error = null;

      if (!TryGetFloat(_tbCrxLearningRate, out float learningRate)) return FieldError("Коэффициент обучения (α)", out error);
      m.LearningRate = learningRate;
      if (!TryGetFloat(_tbCrxDecayRate, out float decayRate)) return FieldError("Коэфф. затухания λ", out error);
      m.DecayRate = decayRate;
      if (!TryGetFloat(_tbCrxActivationThreshold, out float activationThreshold)) return FieldError("Порог активации (γ)", out error);
      m.ActivationThreshold = activationThreshold;
      if (!TryGetFloat(_tbCrxMinAssociationStrength, out float minAssoc)) return FieldError("Минимальная крепость связи (C_min)", out error);
      m.MinAssociationStrength = minAssoc;
      if (!TryGetInt(_tbCrxTimeWindowPulses, out int timeWindow)) return FieldError("Временное окно корреляции (τ)", out error);
      m.TimeWindowPulses = timeWindow;
      if (!TryGetInt(_tbCrxInitialLifetimePulses, out int initialLifetime)) return FieldError("Начальный лимит простоя", out error);
      m.InitialLifetimePulses = initialLifetime;
      if (!TryGetFloat(_tbCrxActiveExtinctionRate, out float activeExt)) return FieldError("Активное угасание (α_ext)", out error);
      m.ActiveExtinctionRate = activeExt;
      if (!TryGetFloat(_tbCrxHigherOrderStrengthReductionCoefficient, out float higherOrder)) return FieldError("Коэфф. понижения крепости вторичных (K)", out error);
      m.HigherOrderStrengthReductionCoefficient = higherOrder;
      if (!TryGetFloat(_tbCrxCompetitionStrengthRatioThreshold, out float compRatio)) return FieldError("Порог отношения крепостей (θ_comp)", out error);
      m.CompetitionStrengthRatioThreshold = compRatio;
      m.TieBreakPreferSmallerReflexId = _chkCrxTieBreakPreferSmallerReflexId.Checked;
      if (!TryGetInt(_tbCrxPassiveDecayPeriodPulses, out int passivePeriod)) return FieldError("Период пассивного угасания", out error);
      m.PassiveDecayPeriodPulses = passivePeriod;
      if (!TryGetInt(_tbCrxPassiveDecayFallbackPeriodPulses, out int passiveFallback)) return FieldError("Резервный период угасания", out error);
      m.PassiveDecayFallbackPeriodPulses = passiveFallback;
      m.EnableCompetitiveLearning = _chkCrxEnableCompetitiveLearning.Checked;
      if (!TryGetFloat(_tbCrxCompetitionSuppressionCoefficient, out float compSuppr)) return FieldError("Доля подавления конкурентами", out error);
      m.CompetitionSuppressionCoefficient = compSuppr;
      if (!TryGetFloat(_tbCrxInitialStrengthBonus, out float initialBonus)) return FieldError("Прибавка к стартовой крепости", out error);
      m.InitialStrengthBonus = initialBonus;
      if (!TryGetFloat(_tbCrxAuthoritativeStrength, out float authStrength)) return FieldError("Крепость авторитарной записи", out error);
      m.AuthoritativeStrength = authStrength;
      if (!TryGetFloat(_tbCrxEstablishedStrengthThreshold, out float established)) return FieldError("Порог «установившегося» рефлекса", out error);
      m.EstablishedStrengthThreshold = established;
      if (!TryGetFloat(_tbCrxActivationReinforcementFraction, out float actReinf)) return FieldError("Доля α при подкреплении активацией", out error);
      m.ActivationReinforcementFraction = actReinf;
      if (!TryGetInt(_tbCrxMaxLifetimePulsesCap, out int maxTtl)) return FieldError("Потолок TTL при удвоении", out error);
      m.MaxLifetimePulsesCap = maxTtl;
      if (!TryGetInt(_tbCrxSensoryDecayPeriodPulses, out int sensPeriod)) return FieldError("Период затухания CS↔CS", out error);
      m.SensoryDecayPeriodPulses = sensPeriod;
      if (!TryGetFloat(_tbCrxSensoryStrengthFloor, out float sensFloor)) return FieldError("Нижний предел крепости CS↔CS", out error);
      m.SensoryStrengthFloor = sensFloor;
      if (!TryGetFloat(_tbCrxSensoryHighStrengthThreshold, out float sensHigh)) return FieldError("Верхняя зона крепости CS↔CS", out error);
      m.SensoryHighStrengthThreshold = sensHigh;
      if (!TryGetFloat(_tbCrxSensoryHighStrengthDecayRate, out float sensHighDecay)) return FieldError("Эфф. коэфф. затухания устойчивых CS↔CS", out error);
      m.SensoryHighStrengthDecayRate = sensHighDecay;
      if (!TryGetFloat(_tbCrxSensoryMidStrengthThreshold, out float sensMid)) return FieldError("Средняя зона крепости CS↔CS", out error);
      m.SensoryMidStrengthThreshold = sensMid;

      return true;
    }

    private static bool FieldError(string name, out string error)
    {
      error = "Некорректное числовое значение: " + name + ".";
      return false;
    }

    private static string F(float value) => value.ToString(CultureInfo.InvariantCulture);
    private static string I(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static bool TryGetFloat(TextBox tb, out float value)
    {
      value = 0f;
      string text = (tb.Text ?? string.Empty).Trim().Replace(',', '.');
      return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryGetInt(TextBox tb, out int value)
    {
      value = 0;
      string text = (tb.Text ?? string.Empty).Trim();
      return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    // Модель требует точку; пользователь мог ввести запятую — нормализуем при уходе фокуса.
    private void CrxTextBox_LostFocus(object sender, EventArgs e)
    {
      var tb = sender as TextBox;
      if (tb == null || string.IsNullOrEmpty(tb.Text))
        return;

      string corrected = tb.Text.Replace(',', '.');
      if (corrected != tb.Text)
        tb.Text = corrected;
    }
  }
}