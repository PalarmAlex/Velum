using System.Collections.Generic;
using System.Globalization;

namespace Velum.Isida
{
  /// <summary>Запись реестра групп сценариев (идентификация в списке). Совместимо с форматом AIStudio.</summary>
  internal sealed class VelumScenarioGroupHeader
  {
    /// <summary>Идентификатор группы в реестре.</summary>
    internal int Id { get; set; }

    /// <summary>Краткое название.</summary>
    internal string Title { get; set; } = "";

    /// <summary>Описание.</summary>
    internal string Description { get; set; } = "";

    private const int DescriptionDisplayLimit = 100;

    /// <summary>Первые N символов описания в одну строку для столбца списка.</summary>
    internal string DescriptionShort
    {
      get
      {
        if (string.IsNullOrEmpty(Description))
          return "";
        string flat = Description.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
        return flat.Length <= DescriptionDisplayLimit
            ? flat
            : flat.Substring(0, DescriptionDisplayLimit) + "…";
      }
    }
  }

  /// <summary>Один сценарий в составе группы: порядок и параметры предзапуска группового прогона.</summary>
  internal sealed class VelumScenarioGroupMember
  {
    /// <summary>Порядковый номер в группе.</summary>
    internal int SortOrderInGroup { get; set; }

    /// <summary>ID сценария из одиночного реестра.</summary>
    internal int ScenarioId { get; set; }

    /// <summary>Стадия предзапуска (0–5); −1 — не менять.</summary>
    internal int PreRunTargetStage { get; set; } = -1;

    /// <summary>Очищать данные при переходе на стадию.</summary>
    internal bool PreRunClearAgentData { get; set; }

    /// <summary>Высетать «норму» по ориентации Speed перед прогоном.</summary>
    internal bool PreRunNormalHomeostasisState { get; set; }

    /// <summary>Режим наблюдения при прогоне.</summary>
    internal bool ScenarioObservationMode { get; set; }

    /// <summary>Авторитарная запись вербального стимула.</summary>
    internal bool ScenarioAuthoritativeRecording { get; set; }
  }

  /// <summary>Полное описание группы: идентификация, коэффициент пульсации и состав.</summary>
  internal sealed class VelumScenarioGroupDocument
  {
    /// <summary>ID группы.</summary>
    internal int Id { get; set; }

    /// <summary>Название.</summary>
    internal string Title { get; set; } = "";

    /// <summary>Описание.</summary>
    internal string Description { get; set; } = "";

    /// <summary>Коэффициент ускорения пульса для всех сценариев группы.</summary>
    internal int RunPulseTimingCoefficient { get; set; } = 1;

    /// <summary>Участники группы.</summary>
    internal List<VelumScenarioGroupMember> Members { get; } = new List<VelumScenarioGroupMember>();

    /// <summary>
    /// Копия одиночного сценария с подставлением полей группового слота и общим коэффициентом пульсации.
    /// Повторяет логику <c>ScenarioGroupDocument.ApplyMemberToScenario</c> из AIStudio.
    /// </summary>
    /// <param name="source">Загруженный одиночный сценарий.</param>
    /// <param name="member">Слот группы.</param>
    /// <param name="groupRunPulseTimingCoefficient">Общий коэффициент пульсации группы.</param>
    /// <returns>Новый документ для прогона.</returns>
    internal static ISIDA.Scenarios.ScenarioDocument ApplyMemberToScenario(
        ISIDA.Scenarios.ScenarioDocument source,
        VelumScenarioGroupMember member,
        int groupRunPulseTimingCoefficient)
    {
      ISIDA.Scenarios.ScenarioDocument doc = source.Clone();
      doc.Header.PreRunTargetStage = member.PreRunTargetStage;
      doc.Header.PreRunClearAgentData = member.PreRunClearAgentData;
      doc.Header.PreRunNormalHomeostasisState = member.PreRunNormalHomeostasisState;
      doc.Header.ScenarioObservationMode = member.ScenarioObservationMode;
      doc.Header.ScenarioAuthoritativeRecording = member.ScenarioAuthoritativeRecording;
      int c = groupRunPulseTimingCoefficient;
      if (c == 1 || c == 5 || c == 10 || c == 20 || c == 50 || c == 100)
        doc.Header.RunPulseTimingCoefficient = c;
      return doc;
    }

    /// <summary>Короткая подпись стадии для столбцов списка.</summary>
    internal static string FormatPreRunStageShort(int stage)
    {
      if (stage >= 0 && stage <= 5)
        return stage.ToString(CultureInfo.InvariantCulture);
      return "не менять";
    }
  }
}