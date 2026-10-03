using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Velum.UI
{
  /// <summary>
  /// Настройки модели условных рефлексов (зеркало ISIDA
  /// <c>ConditionedReflexesSystem.ConditionedReflexSettings</c>). Хранятся в
  /// <c>%ProgramData%\VELUM\Data\Reflexes\ConditionedReflexSettings.dat</c> в формате
  /// <c>Ключ=Значение</c> (разделитель дробей — точка, <see cref="CultureInfo.InvariantCulture"/>).
  /// </summary>
  internal sealed class VelumConditionedReflexSettingsModel
  {
    // ----- Основные параметры -----
    public float LearningRate { get; set; } = 0.2f;
    public float DecayRate { get; set; } = 0.98f;
    public float ActivationThreshold { get; set; } = 0.6f;
    public float MinAssociationStrength { get; set; } = 0.1f;
    public int TimeWindowPulses { get; set; } = 5;
    public int InitialLifetimePulses { get; set; } = 86400;
    public float ActiveExtinctionRate { get; set; } = 0.05f;
    public float HigherOrderStrengthReductionCoefficient { get; set; } = 1.5f;
    public float CompetitionStrengthRatioThreshold { get; set; } = 0.8f;
    public bool TieBreakPreferSmallerReflexId { get; set; } = true;

    // ----- Угасание -----
    public int PassiveDecayPeriodPulses { get; set; } = 1000;
    public int PassiveDecayFallbackPeriodPulses { get; set; } = 1000;

    // ----- Конкурентное обучение -----
    public bool EnableCompetitiveLearning { get; set; } = true;
    public float CompetitionSuppressionCoefficient { get; set; } = 1.0f;

    // ----- Начальная крепость и установление -----
    public float InitialStrengthBonus { get; set; } = 0.1f;
    public float AuthoritativeStrength { get; set; } = 0.95f;
    public float EstablishedStrengthThreshold { get; set; } = 0.8f;
    public float ActivationReinforcementFraction { get; set; } = 0.25f;
    public int MaxLifetimePulsesCap { get; set; } = 88473600;

    // ----- Сенсорные ассоциации CS↔CS -----
    public int SensoryDecayPeriodPulses { get; set; } = 100;
    public float SensoryStrengthFloor { get; set; } = 0.1f;
    public float SensoryHighStrengthThreshold { get; set; } = 0.8f;
    public float SensoryHighStrengthDecayRate { get; set; } = 0.998f;
    public float SensoryMidStrengthThreshold { get; set; } = 0.4f;
  }

  /// <summary>Результат чтения <c>.dat</c>: модель плюс признаки для диалога настроек.</summary>
  internal sealed class VelumConditionedReflexSettingsLoadResult
  {
    public VelumConditionedReflexSettingsModel Model { get; set; } = new VelumConditionedReflexSettingsModel();

    /// <summary>Файл отсутствовал — модель заполнена значениями по умолчанию.</summary>
    public bool FileMissing { get; set; }

    /// <summary>
    /// Файл требовал исправления формата (десятичная запятая вместо точки) либо в нём
    /// отсутствовали параметры новой модели — при сохранении будет перезаписан корректно.
    /// </summary>
    public bool NeedsRewrite { get; set; }

    /// <summary>Человекочитаемые замечания по итогам чтения (для informational-сообщения).</summary>
    public List<string> Notes { get; } = new List<string>();
  }

  /// <summary>
  /// Безопасное чтение/запись/валидация файла настроек у-рефлексов. Не зависит от
  /// WinForms и isida.dll, чтобы тестироваться в изолированном тест-проекте.
  /// </summary>
  internal static class VelumConditionedReflexSettingsStore
  {
    public const string FileName = "ConditionedReflexSettings.dat";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// Читает настройки. Толерантен к «старому» формату с десятичной запятой: такие
    /// значения распознаются и помечаются к перезаписи точкой. Отсутствующие ключи
    /// новой модели получают значения по умолчанию.
    /// </summary>
    public static VelumConditionedReflexSettingsLoadResult Load(string filePath)
    {
      var result = new VelumConditionedReflexSettingsLoadResult();
      var m = result.Model;

      if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
      {
        result.FileMissing = true;
        result.NeedsRewrite = true;
        result.Notes.Add("Файл настроек у-рефлексов отсутствует — будут записаны значения по умолчанию.");
        return result;
      }

      var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

      foreach (string rawLine in File.ReadAllLines(filePath))
      {
        string line = rawLine == null ? string.Empty : rawLine.Trim();
        if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
          continue;

        int eq = line.IndexOf('=');
        if (eq <= 0)
          continue;

        string key = line.Substring(0, eq).Trim();
        string value = line.Substring(eq + 1).Trim();
        if (key.Length == 0 || value.Length == 0)
          continue;

        // Формат ISIDA — точка; старые файлы Velum писали запятую. Фиксируем это.
        if (value.IndexOf(',') >= 0 && !IsBoolValue(value))
        {
          result.NeedsRewrite = true;
        }

        switch (key)
        {
          case "LearningRate":
            m.LearningRate = ParseFloat(value, result);
            break;
          case "DecayRate":
            m.DecayRate = ParseFloat(value, result);
            break;
          case "ActivationThreshold":
            m.ActivationThreshold = ParseFloat(value, result);
            break;
          case "MinAssociationStrength":
            m.MinAssociationStrength = ParseFloat(value, result);
            break;
          case "TimeWindowPulses":
          case "TimeWindowMs":
            m.TimeWindowPulses = ParseInt(value, result);
            break;
          case "InitialLifetimePulses":
          case "BaseInactivationTime":
            m.InitialLifetimePulses = ParseInt(value, result);
            break;
          case "ActiveExtinctionRate":
            m.ActiveExtinctionRate = ParseFloat(value, result);
            break;
          case "PassiveDecayPeriodPulses":
            m.PassiveDecayPeriodPulses = ParseInt(value, result);
            break;
          case "HigherOrderStrengthReductionCoefficient":
            m.HigherOrderStrengthReductionCoefficient = ParseFloat(value, result);
            break;
          case "CompetitionStrengthRatioThreshold":
            m.CompetitionStrengthRatioThreshold = ParseFloat(value, result);
            break;
          case "TieBreakPreferSmallerReflexId":
            m.TieBreakPreferSmallerReflexId = ParseBool(value);
            break;
          case "EnableCompetitiveLearning":
            m.EnableCompetitiveLearning = ParseBool(value);
            break;
          case "CompetitionSuppressionCoefficient":
            m.CompetitionSuppressionCoefficient = ParseFloat(value, result);
            break;
          case "InitialStrengthBonus":
            m.InitialStrengthBonus = ParseFloat(value, result);
            break;
          case "AuthoritativeStrength":
            m.AuthoritativeStrength = ParseFloat(value, result);
            break;
          case "EstablishedStrengthThreshold":
            m.EstablishedStrengthThreshold = ParseFloat(value, result);
            break;
          case "ActivationReinforcementFraction":
            m.ActivationReinforcementFraction = ParseFloat(value, result);
            break;
          case "MaxLifetimePulsesCap":
            m.MaxLifetimePulsesCap = ParseInt(value, result);
            break;
          case "PassiveDecayFallbackPeriodPulses":
            m.PassiveDecayFallbackPeriodPulses = ParseInt(value, result);
            break;
          case "SensoryDecayPeriodPulses":
            m.SensoryDecayPeriodPulses = ParseInt(value, result);
            break;
          case "SensoryStrengthFloor":
            m.SensoryStrengthFloor = ParseFloat(value, result);
            break;
          case "SensoryHighStrengthThreshold":
            m.SensoryHighStrengthThreshold = ParseFloat(value, result);
            break;
          case "SensoryHighStrengthDecayRate":
            m.SensoryHighStrengthDecayRate = ParseFloat(value, result);
            break;
          case "SensoryMidStrengthThreshold":
            m.SensoryMidStrengthThreshold = ParseFloat(value, result);
            break;
          // Устаревшие ключи (half-life / protection ratio) — молча игнорируем.
          case "PassiveDecayProtectionRatio":
          case "PassiveDecayHalfLifePulses":
            break;
          default:
            continue; // неизвестный ключ — не считаем распознанным
        }

        seen.Add(NormalizeKey(key));
      }

      // Если в файле не было части параметров новой модели — файл неполный, перезапишем.
      if (seen.Count < ExpectedKeyCount)
      {
        result.NeedsRewrite = true;
        int missing = ExpectedKeyCount - seen.Count;
        result.Notes.Add(
            "В файле не хватает " + missing +
            " параметр(ов) новой модели — подставлены значения по умолчанию.");
      }

      return result;
    }

    /// <summary>
    /// Сохраняет настройки в каноническом формате ISIDA (точка как разделитель, все
    /// параметры модели). Директория создаётся при необходимости.
    /// </summary>
    public static void Save(string filePath, VelumConditionedReflexSettingsModel m)
    {
      if (string.IsNullOrEmpty(filePath))
        throw new ArgumentException("Не задан путь к файлу настроек у-рефлексов.", nameof(filePath));

      string dir = Path.GetDirectoryName(filePath);
      if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        Directory.CreateDirectory(dir);

      var lines = new List<string>
      {
        "# Настройки системы условных рефлексов",
        "# LearningRate: коэффициент обучения α (0.1-0.3)",
        "# DecayRate: λ для сенсорных ассоциаций CS→CS (0.95-0.99)",
        "# ActivationThreshold: порог активации γ (0.5-0.7)",
        "# MinAssociationStrength: минимальная крепость C_min (0.01-0.3)",
        "# TimeWindowPulses: временное окно корреляции в пульсах (1-10)",
        "# InitialLifetimePulses: начальный лимит простоя УР; при активации удваивается (3600-604800)",
        "# ActiveExtinctionRate: α_ext активного угасания при CS без US (0.01-0.2)",
        "# PassiveDecayPeriodPulses: период пассивного угасания в пульсах (>=1, по умолчанию 1000)",
        "# HigherOrderStrengthReductionCoefficient: коэфф. понижения крепости вторичных (1.2-3.0)",
        "# CompetitionStrengthRatioThreshold: порог отношения крепостей θ_comp для конкурентного подавления (0.5-0.9)",
        "# TieBreakPreferSmallerReflexId: при равной крепости — меньший ID у-рефлекса (true/false)",
        "# EnableCompetitiveLearning: конкурентный слой обучения (ΔV / Kamin blocking) — true/false",
        "# CompetitionSuppressionCoefficient: доля подавления подавляющими CS (0..1)",
        "# InitialStrengthBonus: прибавка к C_min в стартовой крепости C0=(C_min+bonus)/K",
        "# AuthoritativeStrength: крепость авторитарной записи (до понижения по порядку)",
        "# EstablishedStrengthThreshold: порог MaxAchievedStrength для IsEstablished",
        "# ActivationReinforcementFraction: доля α при слабом подкреплении успешной активации",
        "# MaxLifetimePulsesCap: потолок удвоения TTL при активации/успехе",
        "# PassiveDecayFallbackPeriodPulses: резервный период пассива при PassiveDecayPeriodPulses<=0",
        "# SensoryDecayPeriodPulses: период затухания сенсорных связей CS→CS (пульсы)",
        "# SensoryStrengthFloor: нижний предел крепости для кривой затухания CS→CS",
        "# SensoryHighStrengthThreshold: верхняя зона CS→CS (затухание по SensoryHighStrengthDecayRate)",
        "# SensoryHighStrengthDecayRate: эффективный коэффициент затухания устойчивых связей CS→CS",
        "# SensoryMidStrengthThreshold: средняя зона CS→CS (выше — λ^C, ниже — λ^√C)"
      };

      lines.Add("LearningRate=" + F(m.LearningRate));
      lines.Add("DecayRate=" + F(m.DecayRate));
      lines.Add("ActivationThreshold=" + F(m.ActivationThreshold));
      lines.Add("MinAssociationStrength=" + F(m.MinAssociationStrength));
      lines.Add("TimeWindowPulses=" + m.TimeWindowPulses.ToString(Inv));
      lines.Add("InitialLifetimePulses=" + m.InitialLifetimePulses.ToString(Inv));
      lines.Add("ActiveExtinctionRate=" + F(m.ActiveExtinctionRate));
      lines.Add("PassiveDecayPeriodPulses=" + m.PassiveDecayPeriodPulses.ToString(Inv));
      lines.Add("HigherOrderStrengthReductionCoefficient=" + F(m.HigherOrderStrengthReductionCoefficient));
      lines.Add("CompetitionStrengthRatioThreshold=" + F(m.CompetitionStrengthRatioThreshold));
      lines.Add("TieBreakPreferSmallerReflexId=" + m.TieBreakPreferSmallerReflexId.ToString());
      lines.Add("EnableCompetitiveLearning=" + m.EnableCompetitiveLearning.ToString());
      lines.Add("CompetitionSuppressionCoefficient=" + F(m.CompetitionSuppressionCoefficient));
      lines.Add("InitialStrengthBonus=" + F(m.InitialStrengthBonus));
      lines.Add("AuthoritativeStrength=" + F(m.AuthoritativeStrength));
      lines.Add("EstablishedStrengthThreshold=" + F(m.EstablishedStrengthThreshold));
      lines.Add("ActivationReinforcementFraction=" + F(m.ActivationReinforcementFraction));
      lines.Add("MaxLifetimePulsesCap=" + m.MaxLifetimePulsesCap.ToString(Inv));
      lines.Add("PassiveDecayFallbackPeriodPulses=" + m.PassiveDecayFallbackPeriodPulses.ToString(Inv));
      lines.Add("SensoryDecayPeriodPulses=" + m.SensoryDecayPeriodPulses.ToString(Inv));
      lines.Add("SensoryStrengthFloor=" + F(m.SensoryStrengthFloor));
      lines.Add("SensoryHighStrengthThreshold=" + F(m.SensoryHighStrengthThreshold));
      lines.Add("SensoryHighStrengthDecayRate=" + F(m.SensoryHighStrengthDecayRate));
      lines.Add("SensoryMidStrengthThreshold=" + F(m.SensoryMidStrengthThreshold));

      // Атомарная запись через временный файл, чтобы сбой не оставил усечённый .dat.
      string tmp = filePath + ".tmp";
      File.WriteAllLines(tmp, lines, new UTF8Encoding(false));
      if (File.Exists(filePath))
        File.Replace(tmp, filePath, null);
      else
        File.Move(tmp, filePath);
    }

    /// <summary>
    /// Валидирует модель по диапазонам, принятым в модели ISIDA и образце настроек.
    /// Возвращает список сообщений; пустой список — настройки корректны.
    /// </summary>
    public static List<string> Validate(VelumConditionedReflexSettingsModel m)
    {
      var errors = new List<string>();

      CheckFloat(errors, "Коэффициент обучения (α)", m.LearningRate, 0.1f, 0.3f);
      CheckFloat(errors, "Коэфф. затухания λ (CS↔CS)", m.DecayRate, 0.95f, 0.99f);
      CheckFloat(errors, "Порог активации (γ)", m.ActivationThreshold, 0.5f, 0.7f);
      CheckFloat(errors, "Минимальная крепость связи (C_min)", m.MinAssociationStrength, 0.01f, 0.3f);
      CheckInt(errors, "Временное окно корреляции (τ)", m.TimeWindowPulses, 1, 10);
      CheckInt(errors, "Начальный лимит простоя (пульсы)", m.InitialLifetimePulses, 3600, 604800);
      CheckFloat(errors, "Активное угасание (α_ext)", m.ActiveExtinctionRate, 0.01f, 0.2f);
      CheckFloat(errors, "Коэфф. понижения крепости вторичных (K)", m.HigherOrderStrengthReductionCoefficient, 1.2f, 3.0f);
      CheckFloat(errors, "Порог отношения крепостей (θ_comp)", m.CompetitionStrengthRatioThreshold, 0.5f, 0.9f);

      // Пассивное угасание: основной период допускает <=0 (тогда включается резервный), но не уводит в отрицательную «бесконечность».
      CheckInt(errors, "Период пассивного угасания (пульсы)", m.PassiveDecayPeriodPulses, 0, 86400);
      CheckInt(errors, "Резервный период угасания (пульсы)", m.PassiveDecayFallbackPeriodPulses, 1, 86400);

      CheckFloat(errors, "Доля подавления конкурентами", m.CompetitionSuppressionCoefficient, 0.0f, 1.0f);

      CheckFloat(errors, "Прибавка к стартовой крепости", m.InitialStrengthBonus, 0.0f, 1.0f);
      CheckFloat(errors, "Крепость авторитарной записи", m.AuthoritativeStrength, 0.0f, 1.0f);
      CheckFloat(errors, "Порог «установившегося» рефлекса", m.EstablishedStrengthThreshold, 0.0f, 1.0f);
      CheckFloat(errors, "Доля α при подкреплении активацией", m.ActivationReinforcementFraction, 0.0f, 1.0f);

      // Потолок TTL должен покрывать начальный TTL.
      if (m.MaxLifetimePulsesCap < m.InitialLifetimePulses)
        errors.Add("Потолок TTL при удвоении должен быть не меньше начального лимита простоя.");
      CheckInt(errors, "Потолок TTL при удвоении (пульсы)", m.MaxLifetimePulsesCap, 1, int.MaxValue);

      CheckInt(errors, "Период затухания CS↔CS (пульсы)", m.SensoryDecayPeriodPulses, 1, 86400);
      CheckFloat(errors, "Нижний предел крепости CS↔CS", m.SensoryStrengthFloor, 0.0f, 1.0f);
      CheckFloat(errors, "Верхняя зона крепости CS↔CS", m.SensoryHighStrengthThreshold, 0.0f, 1.0f);
      CheckFloat(errors, "Эфф. коэфф. затухания устойчивых CS↔CS", m.SensoryHighStrengthDecayRate, 0.9f, 1.0f);
      CheckFloat(errors, "Средняя зона крепости CS↔CS", m.SensoryMidStrengthThreshold, 0.0f, 1.0f);

      // Порядок зон CS↔CS: средняя < верхняя.
      if (m.SensoryMidStrengthThreshold >= m.SensoryHighStrengthThreshold)
        errors.Add("Средняя зона крепости CS↔CS должна быть меньше верхней зоны.");

      return errors;
    }

    // Количество «канонических» ключей, записываемых в файл (для детекта неполного файла).
    private const int ExpectedKeyCount = 24;

    private static string NormalizeKey(string key)
    {
      switch (key)
      {
        case "TimeWindowMs":
          return "TimeWindowPulses";
        case "BaseInactivationTime":
          return "InitialLifetimePulses";
        default:
          return key;
      }
    }

    private static bool IsBoolValue(string value)
    {
      return value.Equals("True", StringComparison.OrdinalIgnoreCase)
          || value.Equals("False", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ParseBool(string value)
    {
      return value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
    }

    // Принимает и точку, и запятую (старые файлы Velum), затем парсит через InvariantCulture.
    private static float ParseFloat(string value, VelumConditionedReflexSettingsLoadResult result)
    {
        string normalized = value.Replace(',', '.');
        if (float.TryParse(normalized, NumberStyles.Float, Inv, out float f))
          return f;
        result.Notes.Add("Не удалось разобрать числовое значение «" + value + "» — использовано значение по умолчанию.");
        return 0f;
    }

    private static int ParseInt(string value, VelumConditionedReflexSettingsLoadResult result)
    {
        if (int.TryParse(value, NumberStyles.Integer, Inv, out int i))
          return i;
        result.Notes.Add("Не удалось разобрать целое значение «" + value + "» — использовано значение по умолчанию.");
        return 0;
    }

    private static string F(float v) => v.ToString(Inv);

    private static void CheckFloat(List<string> errors, string name, float value, float min, float max)
    {
      if (float.IsNaN(value) || float.IsInfinity(value) || value < min || value > max)
        errors.Add(name + ": допустимый диапазон " + Range(min, max) + ".");
    }

    private static void CheckInt(List<string> errors, string name, int value, int min, int max)
    {
      if (value < min || value > max)
        errors.Add(name + ": допустимый диапазон " + min + "–" + max + ".");
    }

    private static string Range(float min, float max)
    {
      return min.ToString("0.###", Inv) + "–" + max.ToString("0.###", Inv);
    }
  }
}
