using System.Globalization;

namespace Velum.UI
{
  /// <summary>Элемент списка стадий эволюции: в списке только номер; полное описание — через <see cref="GetFullDescription"/>.</summary>
  internal sealed class EvolutionStageListItem
  {
    private static readonly string[] StageTitles =
    {
      "Калибровка регуляции (P, T)",
      "Обучение ассоциативных контуров (AC)",
      "Базовые автоматизмы из RC/AC",
      "Автоматизмы по демонстрации оператора",
      "Память, дерево понимания, циклы анализа",
      "Продвинутый сценарий",
    };

    public EvolutionStageListItem(int stageNumber)
    {
      StageNumber = stageNumber;
    }

    public int StageNumber { get; }

    /// <summary>Полное описание стадии (как в AIStudio / AgentPropertiesDialog).</summary>
    public static string GetFullDescription(int stageNumber)
    {
      if (stageNumber >= 0 && stageNumber < StageTitles.Length)
        return StageTitles[stageNumber];
      return string.Empty;
    }

    public override string ToString()
    {
      return StageNumber.ToString(CultureInfo.InvariantCulture);
    }
  }
}
