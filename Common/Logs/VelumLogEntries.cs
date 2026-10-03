using System;
using System.Collections.Generic;
using System.Globalization;

namespace Velum.UI.Logs
{
  /// <summary>
  /// Запись системного лога агента (строка <c>AgentLogs.csv</c>).
  /// </summary>
  internal sealed class VelumAgentLogEntry
  {
    /// <summary>Временная метка записи.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Номер пульса системы.</summary>
    public int? Pulse { get; set; }

    /// <summary>Идентификатор базового состояния гомеостаза.</summary>
    public int? BaseId { get; set; }

    /// <summary>Идентификатор образа стилей поведения.</summary>
    public int? BaseStyleId { get; set; }

    /// <summary>Идентификатор типа темы мышления.</summary>
    public int? ThinkingThemeTypeId { get; set; }

    /// <summary>Идентификатор пускового стимула (без метрик среды).</summary>
    public int? TriggerStimulusId { get; set; }

    /// <summary>Признак опасной ситуации в информационной среде.</summary>
    public bool InformationEnvironmentDanger { get; set; }

    /// <summary>Признак актуальной ситуации в информационной среде.</summary>
    public bool InformationEnvironmentVeryActual { get; set; }

    /// <summary>Идентификатор безусловного (генетического) рефлекса.</summary>
    public int? GeneticReflexId { get; set; }

    /// <summary>Идентификатор условного рефлекса.</summary>
    public int? ConditionReflexId { get; set; }

    /// <summary>Идентификатор автоматизма.</summary>
    public int? AutomatizmId { get; set; }

    /// <summary>Полезность автоматизма на момент записи строки.</summary>
    public int? AutomatizmUsefulnessAtSnapshot { get; set; }

    /// <summary>Цепочка условных рефлексов в формате <c>ChainId:ActionId</c>.</summary>
    public string ReflexChainInfo { get; set; } = string.Empty;

    /// <summary>Цепочка автоматизмов в формате <c>ChainId:ActionId</c>.</summary>
    public string AutomatizmChainInfo { get; set; } = string.Empty;

    /// <summary>Уровень мышления: 1 = УМ1, 2 = УМ2, null = не активирован.</summary>
    public int? ThinkingLevel { get; set; }

    /// <summary>Успех решения проблемы на активированном уровне мышления.</summary>
    public bool? ThinkingLevelSuccess { get; set; }

    /// <summary>Номер текущего главного цикла мышления.</summary>
    public int? MainThinkingCycleId { get; set; }

    /// <summary>JSON фоновых циклов мышления (как в ResearchLogger «ЦиклыФ_json»).</summary>
    public string BackgroundThinkingCyclesJson { get; set; }

    /// <summary>Ячейка «Среда» в виде <c>id:величина</c>.</summary>
    public string EnvironmentPressureCell { get; set; }

    /// <summary>Сохранённая подсказка ячейки «Среда».</summary>
    public string EnvironmentPressureTooltip { get; set; }

    /// <summary>Ориентационный рефлекс: 1 = ОР1, 2 = ОР2, null = не активирован.</summary>
    public int? OrientationReflexType { get; set; }

    /// <summary>Отформатированное время для отображения.</summary>
    public string DisplayTime => Timestamp.ToString("HH:mm:ss", CultureInfo.CurrentCulture);

    /// <summary>Отформатированный номер пульса.</summary>
    public string DisplayPulse => Pulse?.ToString(CultureInfo.InvariantCulture) ?? "-";

    /// <summary>Отформатированный идентификатор состояния.</summary>
    public string DisplayBaseId => BaseId?.ToString(CultureInfo.InvariantCulture) ?? "-";

    /// <summary>Отформатированный идентификатор стилей.</summary>
    public string DisplayBaseStyleId => BaseStyleId?.ToString(CultureInfo.InvariantCulture) ?? "-";

    /// <summary>Отформатированный идентификатор темы мышления.</summary>
    public string DisplayThinkingThemeId =>
        ThinkingThemeTypeId.HasValue && ThinkingThemeTypeId.Value > 0
            ? ThinkingThemeTypeId.Value.ToString(CultureInfo.InvariantCulture)
            : "-";

    /// <summary>Отформатированный идентификатор триггера.</summary>
    public string DisplayTriggerStimulusId => TriggerStimulusId?.ToString(CultureInfo.InvariantCulture) ?? "-";

    /// <summary>Единая колонка ОР/УМ: при активном УМ — «УМ1»/«УМ2», иначе ОР1/ОР2 или «-».</summary>
    public string DisplayOrUm
    {
      get
      {
        if (ThinkingLevel.HasValue && (ThinkingLevel.Value == 1 || ThinkingLevel.Value == 2))
          return ThinkingLevel.Value == 1 ? "УМ1" : "УМ2";
        return DisplayOrientationReflexType;
      }
    }

    /// <summary>Отформатированный ориентационный рефлекс.</summary>
    public string DisplayOrientationReflexType
    {
      get
      {
        if (!OrientationReflexType.HasValue)
          return "-";
        switch (OrientationReflexType.Value)
        {
          case 1:
            return "ОР1";
          case 2:
            return "ОР2";
          default:
            return OrientationReflexType.Value.ToString(CultureInfo.InvariantCulture);
        }
      }
    }

    /// <summary>Ячейка «Опасно»: «1» или «0».</summary>
    public string DisplayDanger => InformationEnvironmentDanger ? "1" : "0";

    /// <summary>Ячейка «Актуально»: «1» или «0».</summary>
    public string DisplayVeryActual => InformationEnvironmentVeryActual ? "1" : "0";

    /// <summary>Ячейка «Среда».</summary>
    public string DisplayEnvironmentPressure =>
        string.IsNullOrWhiteSpace(EnvironmentPressureCell) ? "-" : EnvironmentPressureCell;

    /// <summary>Отформатированный идентификатор безусловного рефлекса.</summary>
    public string DisplayGeneticReflexId => GeneticReflexId?.ToString(CultureInfo.InvariantCulture) ?? "-";

    /// <summary>Отформатированный идентификатор условного рефлекса.</summary>
    public string DisplayConditionReflexId =>
        ConditionReflexId.HasValue && ConditionReflexId.Value > 0
            ? ConditionReflexId.Value.ToString(CultureInfo.InvariantCulture)
            : "-";

    /// <summary>Отформатированный идентификатор автоматизма.</summary>
    public string DisplayAutomatizmId => AutomatizmId?.ToString(CultureInfo.InvariantCulture) ?? "-";

    /// <summary>Отформатированная цепочка рефлексов.</summary>
    public string DisplayReflexChainInfo =>
        string.IsNullOrEmpty(ReflexChainInfo) ? "-" : ReflexChainInfo;

    /// <summary>Отформатированная цепочка автоматизмов.</summary>
    public string DisplayAutomatizmChainInfo =>
        string.IsNullOrEmpty(AutomatizmChainInfo) ? "-" : AutomatizmChainInfo;

    /// <summary>Отформатированный главный цикл мышления.</summary>
    public string DisplayMainThinkingCycle =>
        MainThinkingCycleId.HasValue && MainThinkingCycleId.Value > 0
            ? MainThinkingCycleId.Value.ToString(CultureInfo.InvariantCulture)
            : "-";

    /// <summary>Отформатированные фоновые циклы мышления.</summary>
    public string DisplayBackgroundThinkingCycles =>
        string.IsNullOrWhiteSpace(BackgroundThinkingCyclesJson) ? "-" : BackgroundThinkingCyclesJson;
  }

  /// <summary>
  /// Запись лога стилей поведения (<c>AgentLogs_Styles.csv</c>, этап «Final»).
  /// </summary>
  internal sealed class VelumStyleLogEntry
  {
    /// <summary>Временная метка записи.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Номер пульса системы.</summary>
    public int Pulse { get; set; }

    /// <summary>Идентификатор стиля поведения.</summary>
    public int StyleId { get; set; }

    /// <summary>Наименование стиля поведения.</summary>
    public string StyleName { get; set; } = string.Empty;
  }

  /// <summary>
  /// Запись активации стилей от параметров гомеостаза (<c>AgentLogs_Styles.csv</c>, этап «ParameterActivation»).
  /// </summary>
  internal sealed class VelumStyleParameterActivationEntry
  {
    /// <summary>Временная метка записи.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Номер пульса системы.</summary>
    public int Pulse { get; set; }

    /// <summary>Идентификатор параметра гомеостаза.</summary>
    public int ParameterId { get; set; }

    /// <summary>Наименование параметра гомеостаза.</summary>
    public string ParameterName { get; set; } = string.Empty;

    /// <summary>Идентификатор зоны активации (0–6).</summary>
    public int ZoneId { get; set; }

    /// <summary>Описание зоны активации.</summary>
    public string ZoneDescription { get; set; } = string.Empty;

    /// <summary>Идентификатор стиля поведения.</summary>
    public int StyleId { get; set; }

    /// <summary>Наименование стиля поведения.</summary>
    public string StyleName { get; set; } = string.Empty;
  }

  /// <summary>
  /// Запись лога параметров гомеостаза (<c>AgentLogs_Parameters.csv</c>).
  /// </summary>
  internal sealed class VelumParameterLogEntry
  {
    /// <summary>Временная метка записи.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Номер пульса системы.</summary>
    public int Pulse { get; set; }

    /// <summary>Идентификатор параметра гомеостаза.</summary>
    public int ParamId { get; set; }

    /// <summary>Наименование параметра гомеостаза.</summary>
    public string ParamName { get; set; } = string.Empty;

    /// <summary>Вес параметра в системе гомеостаза.</summary>
    public int Weight { get; set; }

    /// <summary>Значение нормы параметра.</summary>
    public int NormaWell { get; set; }

    /// <summary>Скорость изменения параметра.</summary>
    public int Speed { get; set; }

    /// <summary>Текущее значение параметра.</summary>
    public float Value { get; set; }

    /// <summary>Значение функции срочности.</summary>
    public float UrgencyFunction { get; set; }

    /// <summary>Текущее состояние параметра.</summary>
    public string ParameterState { get; set; } = string.Empty;

    /// <summary>Зона активации параметра.</summary>
    public string ActivationZone { get; set; } = string.Empty;

    /// <summary>Код состояния для подсветки: -1 плохо, 1 хорошо, 0 норма.</summary>
    public int StateCode
    {
      get
      {
        string state = (ParameterState ?? string.Empty).ToLowerInvariant();
        if (state.Contains("плохо") || state.Contains("bad") || state.Contains("критич"))
          return -1;
        if (state.Contains("хорошо") || state.Contains("good"))
          return 1;
        return 0;
      }
    }
  }

  /// <summary>
  /// Данные сессии лога стилей: итоговые стили и активации от параметров.
  /// </summary>
  internal sealed class VelumStyleLogSessionData
  {
    /// <summary>Итоговые стили поведения.</summary>
    public List<VelumStyleLogEntry> StyleEntries { get; } = new List<VelumStyleLogEntry>();

    /// <summary>Активации стилей от параметров гомеостаза.</summary>
    public List<VelumStyleParameterActivationEntry> Activations { get; } =
        new List<VelumStyleParameterActivationEntry>();
  }
}
