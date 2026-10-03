using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ISIDA.Actions;
using ISIDA.Common;
using ISIDA.Gomeostas;
using ISIDA.Psychic;
using ISIDA.Psychic.Automatism;
using ISIDA.Psychic.Understanding;
using ISIDA.Reflexes;
using ISIDA.Sensors;

namespace Velum.UI.Logs
{
  /// <summary>
  /// Текстовые подсказки для ячеек отчётов по логам агента.
  /// Порт <c>AgentLogCellTooltipProvider</c> из AIStudio на статические синглтоны ISIDA
  /// (Velum не создаёт WPF-моделей и берёт каталоги напрямую из <c>isida.dll</c>).
  /// </summary>
  internal sealed class VelumLogCellTooltipProvider
  {
    /// <summary>Создаёт провайдер подсказок.</summary>
    public VelumLogCellTooltipProvider()
    {
    }

    private static VerbalSensorChannel VerbalSensor =>
        SensorySystem.IsInitialized ? SensorySystem.Instance.CommandChannel : null;

    /// <summary>Подсказка колонки «Стиль» для отчёта: коды комбинации «1,2,3» или одно число.</summary>
    public string GetStyleCellTooltip(string cellRaw)
    {
      if (string.IsNullOrWhiteSpace(cellRaw))
        return "Нет данных о стилях";
      string t = cellRaw.Trim();
      if (t == "-")
        return "Нет данных о стилях";
      if (t.IndexOf(',') >= 0)
        return GetStyleCombinationNamesFromCodes(t);
      if (int.TryParse(t, out int id) && id > 0)
      {
        string byImage = GetStyleTooltip(t);
        if (byImage != "Нет данных о стилях")
          return byImage;
        return GetSingleBehaviorStyleName(id);
      }
      return "Нет данных о стилях";
    }

    private static string GetStyleCombinationNamesFromCodes(string commaSeparated)
    {
      try
      {
        var ids = new List<int>();
        foreach (string p in commaSeparated.Split(','))
        {
          if (int.TryParse(p.Trim(), out int sid) && sid > 0)
            ids.Add(sid);
        }
        if (ids.Count == 0)
          return "Нет данных о стилях";
        var allStyles = GetAllBehaviorStyles();
        var names = ids
            .OrderBy(x => x)
            .Select(styleId => allStyles != null && allStyles.ContainsKey(styleId)
                ? allStyles[styleId].Name
                : "Стиль " + styleId.ToString(CultureInfo.InvariantCulture))
            .Where(name => !string.IsNullOrEmpty(name))
            .ToList();
        return names.Count > 0 ? string.Join(", ", names) : "Нет данных о стилях";
      }
      catch (Exception ex)
      {
        return "Ошибка загрузки стилей: " + ex.Message;
      }
    }

    private static string GetSingleBehaviorStyleName(int styleId)
    {
      try
      {
        var allStyles = GetAllBehaviorStyles();
        if (allStyles != null && allStyles.ContainsKey(styleId))
          return allStyles[styleId].Name ?? "Стиль " + styleId.ToString(CultureInfo.InvariantCulture);
      }
      catch
      {
      }
      return "Стиль " + styleId.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>ID образа стиля → названия стилей поведения.</summary>
    public string GetStyleTooltip(string displayBaseStyleId)
    {
      if (string.IsNullOrEmpty(displayBaseStyleId) ||
          !int.TryParse(displayBaseStyleId, out int imageId) || imageId <= 0)
        return "Нет данных о стилях";
      try
      {
        if (!PerceptionImagesSystem.IsInitialized)
          return "Нет данных о стилях";
        var styleImages = PerceptionImagesSystem.Instance.GetAllBehaviorStyleImagesList();
        var styleImage = styleImages.FirstOrDefault(img => img.Id == imageId);
        if (styleImage != null && styleImage.BehaviorStylesList.Any())
        {
          var allStyles = GetAllBehaviorStyles();
          var styleNames = styleImage.BehaviorStylesList
              .Select(styleId => allStyles != null && allStyles.ContainsKey(styleId)
                  ? allStyles[styleId].Name
                  : "Стиль " + styleId.ToString(CultureInfo.InvariantCulture))
              .Where(name => !string.IsNullOrEmpty(name));
          return string.Join(", ", styleNames);
        }
      }
      catch (Exception ex)
      {
        return "Ошибка загрузки стилей: " + ex.Message;
      }
      return "Нет данных о стилях";
    }

    /// <summary>Подсказка колонки «Триггер»: влияние, фразы, команды, тон, настроение, цвет.</summary>
    public string GetTriggerTooltip(string displayTriggerStimulusId)
    {
      if (string.IsNullOrEmpty(displayTriggerStimulusId) ||
          !int.TryParse(displayTriggerStimulusId, out int imageId) || imageId <= 0)
        return "Нет данных о триггере";
      try
      {
        if (!PerceptionImagesSystem.IsInitialized)
          return "Нет данных о триггере";
        var perceptionImage = PerceptionImagesSystem.Instance
            .GetAllPerceptionImagesList().FirstOrDefault(img => img.Id == imageId);
        if (perceptionImage == null)
          return "Нет данных о триггере";

        string influenceLine;
        if (perceptionImage.InfluenceActionsList != null && perceptionImage.InfluenceActionsList.Any())
        {
          var allInfluences = InfluenceActionSystem.IsInitialized
              ? InfluenceActionSystem.Instance.GetAllInfluenceActions()
              : null;
          var influenceNames = perceptionImage.InfluenceActionsList
              .Where(actionId => !(InfluenceActionSystem.IsInitialized &&
                                   InfluenceActionSystem.Instance.IsEnvironmentProbeActionId(actionId)))
              .Select(actionId =>
              {
                var found = allInfluences?.FirstOrDefault(a => a.Id == actionId);
                return found?.Name ?? "Воздействие " + actionId.ToString(CultureInfo.InvariantCulture);
              })
              .Where(name => !string.IsNullOrEmpty(name))
              .ToList();
          influenceLine = influenceNames.Count > 0 ? string.Join(", ", influenceNames) : "нет";
        }
        else
          influenceLine = "нет";

        string phrasesLine;
        if (perceptionImage.PhraseIdList != null && perceptionImage.PhraseIdList.Any())
        {
          var channel = VerbalSensor;
          var phraseNames = perceptionImage.PhraseIdList
              .Select(phraseId => channel?.GetPhraseFromPhraseId(phraseId) ??
                                  "Фраза " + phraseId.ToString(CultureInfo.InvariantCulture))
              .Where(phrase => !string.IsNullOrEmpty(phrase))
              .ToList();
          phrasesLine = phraseNames.Count > 0 ? string.Join(", ", phraseNames) : "нет";
        }
        else
          phrasesLine = "нет";

        string commandsLine;
        if (perceptionImage.CommandPatternIdList != null && perceptionImage.CommandPatternIdList.Any())
        {
          var channel = VerbalSensor;
          var commandNames = perceptionImage.CommandPatternIdList
              .Where(patternId => patternId > 0)
              .Select(patternId =>
              {
                string text = channel?.GetPhraseFromPhraseId(patternId);
                return string.IsNullOrWhiteSpace(text)
                    ? "Команда " + patternId.ToString(CultureInfo.InvariantCulture)
                    : patternId.ToString(CultureInfo.InvariantCulture) + ":" + text;
              })
              .Where(name => !string.IsNullOrEmpty(name))
              .ToList();
          commandsLine = commandNames.Count > 0 ? string.Join(" → ", commandNames) : "нет";
        }
        else
          commandsLine = "нет";

        string toneLine = "—";
        string moodLine = "—";
        if (VerbalBrocaImagesSystem.IsInitialized &&
            perceptionImage.PhraseIdList != null && perceptionImage.PhraseIdList.Any())
        {
          var broca = VerbalBrocaImagesSystem.Instance.GetAllVerbalBrocaImagesList()
              .FirstOrDefault(v => AreListsEqual(v.PhraseIdList, perceptionImage.PhraseIdList));
          if (broca != null)
          {
            string t = ActionsImagesSystem.GetToneText(broca.ToneId);
            toneLine = string.IsNullOrEmpty(t)
                ? broca.ToneId.ToString(CultureInfo.InvariantCulture)
                : t + " (" + broca.ToneId.ToString(CultureInfo.InvariantCulture) + ")";
            string m = ActionsImagesSystem.GetMoodText(broca.MoodId);
            moodLine = string.IsNullOrEmpty(m)
                ? broca.MoodId.ToString(CultureInfo.InvariantCulture)
                : m + " (" + broca.MoodId.ToString(CultureInfo.InvariantCulture) + ")";
          }
        }

        int colorCode = AgentVisualColor.IsValidCode(perceptionImage.VisualColorId)
            ? perceptionImage.VisualColorId
            : AgentVisualColor.White;
        string colorLine = AgentVisualColor.GetDisplayName(colorCode);

        return "Воздействие: " + influenceLine
            + "\nФразы: " + phrasesLine
            + "\nКоманды: " + commandsLine
            + "\nТон: " + toneLine
            + "\nНастроение: " + moodLine
            + "\nЦветовой фон: " + colorLine;
      }
      catch (Exception ex)
      {
        return "Ошибка загрузки триггера: " + ex.Message;
      }
    }

    /// <summary>Подсказка колонки «Среда»: описания метрик из справочника.</summary>
    public string GetEnvironmentPressureTooltip(string cellRaw, string storedTooltip)
    {
      if (!string.IsNullOrWhiteSpace(cellRaw) && cellRaw.Trim() != "-")
      {
        string fromCell = BuildEnvironmentPressureFromCell(cellRaw);
        if (!string.IsNullOrWhiteSpace(fromCell))
          return fromCell;
      }
      if (!string.IsNullOrWhiteSpace(storedTooltip))
        return storedTooltip.Trim();
      return "На этом пульсе давление метрик среды не применялось";
    }

    private static string BuildEnvironmentPressureFromCell(string cellRaw)
    {
      var segments = ParseEnvironmentPressureSegments(cellRaw);
      if (segments.Count == 0)
        return null;
      var all = InfluenceActionSystem.IsInitialized
          ? InfluenceActionSystem.Instance.GetAllInfluenceActions()
          : null;
      var sb = new StringBuilder();
      foreach (var seg in segments)
      {
        var action = all?.FirstOrDefault(a => a.Id == seg.Id);
        string signed = seg.Magnitude > 0
            ? "+" + seg.Magnitude.ToString(CultureInfo.InvariantCulture)
            : seg.Magnitude.ToString(CultureInfo.InvariantCulture);
        if (sb.Length > 0)
          sb.AppendLine();
        string header = seg.Id.ToString(CultureInfo.InvariantCulture) + ":" + signed;
        if (action != null)
          sb.Append(header).Append(" — ").Append(action.Name ?? string.Empty);
        else
          sb.Append(header);
        if (action != null && !string.IsNullOrWhiteSpace(action.Description))
          sb.AppendLine().Append("    ").Append(action.Description.Trim());
      }
      return sb.ToString().TrimEnd();
    }

    private static List<EnvironmentPressureSegment> ParseEnvironmentPressureSegments(string cellRaw)
    {
      var list = new List<EnvironmentPressureSegment>();
      if (string.IsNullOrWhiteSpace(cellRaw) || cellRaw.Trim() == "-")
        return list;
      foreach (string part in cellRaw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
      {
        var m = Regex.Match(part.Trim(), @"^(\d+)\s*:\s*([+\-]?\d+)$");
        if (!m.Success)
          continue;
        if (!int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
          continue;
        if (!int.TryParse(m.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int mag))
          continue;
        list.Add(new EnvironmentPressureSegment(id, mag));
      }
      return list;
    }

    private sealed class EnvironmentPressureSegment
    {
      public EnvironmentPressureSegment(int id, int magnitude)
      {
        Id = id;
        Magnitude = magnitude;
      }

      public int Id { get; }

      public int Magnitude { get; }
    }

    /// <summary>Действия безусловного рефлекса.</summary>
    public string GetActionsForGeneticReflex(string displayReflexId)
    {
      if (string.IsNullOrEmpty(displayReflexId) ||
          !int.TryParse(displayReflexId, out int reflexId) || reflexId <= 0)
        return "Нет данных о действиях рефлекса";
      try
      {
        var reflex = GetAllGeneticReflexes()?.FirstOrDefault(r => r.Id == reflexId);
        if (reflex != null)
        {
          var allActions = GetAllAdaptiveActions();
          var actionNames = (reflex.AdaptiveActions ?? new List<int>())
              .Select(actionId =>
              {
                var found = allActions?.FirstOrDefault(a => a.Id == actionId);
                return found?.Name ?? "Действие " + actionId.ToString(CultureInfo.InvariantCulture);
              })
              .Where(name => !string.IsNullOrEmpty(name))
              .ToList();
          return actionNames.Count > 0
              ? "Действия: " + string.Join(", ", actionNames)
              : "Пустой образ действий рефлекса";
        }
      }
      catch (Exception ex)
      {
        return "Ошибка загрузки действий рефлекса: " + ex.Message;
      }
      return "Нет данных о действиях рефлекса";
    }

    /// <summary>Действия условного рефлекса (через исходный безусловный).</summary>
    public string GetActionsForConditionReflex(string displayReflexId)
    {
      if (string.IsNullOrEmpty(displayReflexId) ||
          !int.TryParse(displayReflexId, out int reflexId) || reflexId <= 0)
        return "Нет данных о действиях рефлекса";
      try
      {
        var conditionedReflex = GetAllConditionedReflexes()?.FirstOrDefault(r => r.Id == reflexId);
        if (conditionedReflex == null)
          return "Нет данных о действиях рефлекса";
        var actions = GetActionsForGeneticReflexes(conditionedReflex.SourceGeneticReflexId);
        var allActions = GetAllAdaptiveActions();
        var actionNames = actions
            .Select(actionId =>
            {
              var found = allActions?.FirstOrDefault(a => a.Id == actionId);
              return found?.Name ?? "Действие " + actionId.ToString(CultureInfo.InvariantCulture);
            })
            .Where(name => !string.IsNullOrEmpty(name))
            .ToList();
        return actionNames.Count > 0
            ? "Действия: " + string.Join(", ", actionNames)
            : "Пустой образ действий рефлекса";
      }
      catch (Exception ex)
      {
        return "Ошибка загрузки действий рефлекса: " + ex.Message;
      }
    }

    /// <summary>Список действий безусловного рефлекса.</summary>
    public List<int> GetActionsForGeneticReflexes(int reflexId)
    {
      try
      {
        var reflex = GetAllGeneticReflexes()?.FirstOrDefault(r => r.Id == reflexId);
        return reflex?.AdaptiveActions?.ToList() ?? new List<int>();
      }
      catch
      {
        return new List<int>();
      }
    }

    /// <summary>Подсказка колонки «Автоматизм»: действия, фразы, тон, настроение, полезность.</summary>
    public string GetAutomatizmTooltip(string displayAutomatizmId, int? usefulnessAtSnapshot = null)
    {
      if (string.IsNullOrEmpty(displayAutomatizmId) ||
          !int.TryParse(displayAutomatizmId, out int atmzId) || atmzId <= 0)
      {
        return usefulnessAtSnapshot.HasValue
            ? "Полезность: " + usefulnessAtSnapshot.Value.ToString(CultureInfo.InvariantCulture)
            : "Нет данных о действиях автоматизма";
      }
      try
      {
        if (!AutomatizmSystem.IsInitialized)
          return "Нет данных о действиях автоматизма";
        var atmz = AutomatizmSystem.Instance.GetAutomatizmById(atmzId);
        if (atmz == null)
        {
          return usefulnessAtSnapshot.HasValue
              ? "Полезность: " + usefulnessAtSnapshot.Value.ToString(CultureInfo.InvariantCulture)
              : "Нет данных о действиях автоматизма";
        }

        int usefulness = usefulnessAtSnapshot ?? atmz.Usefulness;
        var actionsImage = ActionsImagesSystem.IsInitialized
            ? ActionsImagesSystem.Instance.GetActionsImage(atmz.ActionsImageID)
            : null;

        var sb = new StringBuilder();
        var allActions = GetAllAdaptiveActions();

        if (actionsImage != null && actionsImage.ActIdList != null && actionsImage.ActIdList.Any())
        {
          var names = actionsImage.ActIdList
              .Select(id =>
              {
                var found = allActions?.FirstOrDefault(a => a.Id == id);
                return found?.Name;
              })
              .Where(n => !string.IsNullOrEmpty(n))
              .ToList();
          sb.AppendLine("Действия (" + actionsImage.ActIdList.Count.ToString(CultureInfo.InvariantCulture) + "): "
                        + (names.Count > 0 ? string.Join(", ", names) : "нет"));
        }
        else
          sb.AppendLine("Действия: нет");

        if (actionsImage != null && actionsImage.PhraseIdList != null && actionsImage.PhraseIdList.Any())
        {
          var channel = VerbalSensor;
          var phraseTexts = new List<string>();
          foreach (int phraseId in actionsImage.PhraseIdList)
          {
            string text = channel?.GetPhraseFromPhraseId(phraseId);
            phraseTexts.Add(string.IsNullOrEmpty(text)
                ? "ID: " + phraseId.ToString(CultureInfo.InvariantCulture) + " (фраза не найдена)"
                : "\"" + text + "\" (ID: " + phraseId.ToString(CultureInfo.InvariantCulture) + ")");
          }
          sb.AppendLine("Фразы (" + actionsImage.PhraseIdList.Count.ToString(CultureInfo.InvariantCulture) + "): "
                        + string.Join(", ", phraseTexts));
        }
        else
          sb.AppendLine("Фразы: нет");

        int toneId = actionsImage?.ToneId ?? 0;
        int moodId = actionsImage?.MoodId ?? 0;
        string toneText = ActionsImagesSystem.GetToneText(toneId);
        string moodText = ActionsImagesSystem.GetMoodText(moodId);
        sb.AppendLine(string.IsNullOrEmpty(toneText) ? "Тон: —" : "Тон: " + toneText);
        sb.AppendLine(string.IsNullOrEmpty(moodText) ? "Настроение: —" : "Настроение: " + moodText);
        sb.AppendLine("Полезность: " + usefulness.ToString(CultureInfo.InvariantCulture));
        return sb.ToString().TrimEnd();
      }
      catch (Exception ex)
      {
        return "Ошибка загрузки автоматизма: " + ex.Message;
      }
    }

    /// <summary>Подсказка колонки «Цепочка РФ» (действие из справочника адаптивных действий).</summary>
    public string GetReflexChainTooltip(string chainInfo)
    {
      return GetChainTooltip(chainInfo, "Нет активных цепочек рефлексов", "Неверный формат цепочки рефлекса");
    }

    /// <summary>Подсказка колонки «Цепочка АВ».</summary>
    public string GetAutomatizmChainTooltip(string chainInfo)
    {
      return GetChainTooltip(chainInfo, "Нет активных цепочек автоматизмов", "Неверный формат цепочки автоматизма");
    }

    private static string GetChainTooltip(string chainInfo, string emptyText, string badFormatText)
    {
      if (string.IsNullOrEmpty(chainInfo) || chainInfo == "-")
        return emptyText;
      var parts = chainInfo.Split(':');
      if (parts.Length != 2 || !int.TryParse(parts[1], out int actionId) || actionId <= 0)
        return badFormatText;
      try
      {
        var allActions = GetAllAdaptiveActions();
        var action = allActions?.FirstOrDefault(a => a.Id == actionId);
        return action != null ? action.Name : "Действие " + actionId.ToString(CultureInfo.InvariantCulture);
      }
      catch (Exception ex)
      {
        return "Ошибка загрузки действия: " + ex.Message;
      }
    }

    /// <summary>Подсказка колонки «Тема»: название типа темы и вес по справочнику.</summary>
    public string GetThinkingThemeTypeTooltip(string themeCell)
    {
      if (string.IsNullOrWhiteSpace(themeCell) || themeCell.Trim() == "-")
        return null;
      if (!int.TryParse(themeCell.Trim(), out int themeTypeId) || themeTypeId <= 0)
        return null;
      if (!ThemeImageSystem.IsInitialized)
        return null;
      try
      {
        string name = ThemeImageSystem.Instance.GetThemeTypeDescription(themeTypeId) ?? string.Empty;
        int w = ThemeImageSystem.Instance.GetDefaultWeightForThemeType(themeTypeId);
        return string.IsNullOrEmpty(name)
            ? "(" + w.ToString(CultureInfo.InvariantCulture) + ")"
            : name + " (" + w.ToString(CultureInfo.InvariantCulture) + ")";
      }
      catch
      {
        return null;
      }
    }

    /// <summary>Подсказка колонки «ОР/УМ».</summary>
    public string GetOrUmTooltip(string displayOrUm, bool? thinkingLevelSuccessForUm)
    {
      if (string.IsNullOrWhiteSpace(displayOrUm))
        return "ОР/УМ";
      string s = displayOrUm.Trim();
      if (s == "1" || s == "2" || s == "УМ1" || s == "УМ2")
        return GetThinkingLevelTooltip(s, thinkingLevelSuccessForUm);
      if (s == "-")
        return "Нет активации ОР или уровня мышления";
      return GetOrientationReflexTooltip(s);
    }

    /// <summary>Подсказка ориентационного рефлекса.</summary>
    public string GetOrientationReflexTooltip(string displayOrientationReflexType)
    {
      const string or1 = "Нет автоматизма, нужно быстро создать его по гомеостатическим целям";
      const string or2 = "Автоматизм есть, надо его проверить в текущих условиях";
      if (string.IsNullOrEmpty(displayOrientationReflexType))
        return "Нет ориентировочного рефлекса";
      string value = displayOrientationReflexType.Trim();
      if (value == "ОР1")
        return or1;
      if (value == "ОР2")
        return or2;
      if (int.TryParse(value, out int orType))
      {
        if (orType == 1)
          return or1;
        if (orType == 2)
          return or2;
        return "Ориентировочный рефлекс типа " + orType.ToString(CultureInfo.InvariantCulture);
      }
      return "Ориентировочный рефлекс: " + value;
    }

    /// <summary>Подсказка уровня мышления.</summary>
    public string GetThinkingLevelTooltip(string displayThinkingLevel, bool? thinkingLevelSuccess)
    {
      if (string.IsNullOrEmpty(displayThinkingLevel) || displayThinkingLevel == "-")
        return "Уровень мышления не активирован";
      string value = displayThinkingLevel.Trim();
      bool isUm1 = value == "1" || value == "УМ1";
      bool isUm2 = value == "2" || value == "УМ2";
      string levelDesc;
      if (isUm1)
        levelDesc = "Уровень мышления 1: решение за счёт штатного автоматизма узла дерева (без правил эпизодической памяти)";
      else if (isUm2)
        levelDesc = "Уровень мышления 2: поиск или создание автоматизма по правилам эпизодической памяти";
      else
        levelDesc = "Уровень мышления: " + value;
      string resultLine = thinkingLevelSuccess.HasValue
          ? (thinkingLevelSuccess.Value ? "Результат: Успех" : "Результат: Неудача")
          : string.Empty;
      return string.IsNullOrEmpty(resultLine) ? levelDesc : levelDesc + "\n" + resultLine;
    }

    /// <summary>Подсказка колонки «Состояние» по коду.</summary>
    public static string GetStateCodeTooltip(string rawStateCell)
    {
      if (string.IsNullOrWhiteSpace(rawStateCell))
        return null;
      string a = rawStateCell.Trim();
      if (a == "-" || a.Length == 0)
        return null;
      switch (a)
      {
        case "-1":
          return "ПЛОХО";
        case "0":
          return "НОРМА";
        case "1":
          return "ХОРОШО";
        default:
          return "Состояние: " + a;
      }
    }

    private static ReadOnlyDictionaryLike GetAllBehaviorStyles()
    {
      if (!GomeostasSystem.IsInitialized)
        return null;
      return new ReadOnlyDictionaryLike(GomeostasSystem.Instance.GetAllBehaviorStyles());
    }

    private static List<GeneticReflexesSystem.GeneticReflex> GetAllGeneticReflexes()
    {
      return GeneticReflexesSystem.IsInitialized
          ? GeneticReflexesSystem.Instance.GetAllGeneticReflexesList()
          : null;
    }

    private static List<ConditionedReflexesSystem.ConditionedReflex> GetAllConditionedReflexes()
    {
      return ConditionedReflexesSystem.IsInitialized
          ? ConditionedReflexesSystem.Instance.GetAllConditionedReflexes().ToList()
          : null;
    }

    private static List<AdaptiveActionsSystem.AdaptiveAction> GetAllAdaptiveActions()
    {
      return AdaptiveActionsSystem.IsInitialized
          ? AdaptiveActionsSystem.Instance.GetAllAdaptiveActions().ToList()
          : null;
    }

    private static bool AreListsEqual(List<int> a, List<int> b)
    {
      return AddUtils.AreListsEqual(a, b);
    }

    /// <summary>Обёртка над словарём стилей (упрощает null-безопасный доступ).</summary>
    private sealed class ReadOnlyDictionaryLike
    {
      private readonly System.Collections.ObjectModel.ReadOnlyDictionary<int, GomeostasSystem.BehaviorStyle> _inner;

      public ReadOnlyDictionaryLike(
          System.Collections.ObjectModel.ReadOnlyDictionary<int, GomeostasSystem.BehaviorStyle> inner)
      {
        _inner = inner;
      }

      public bool ContainsKey(int key)
      {
        return _inner != null && _inner.ContainsKey(key);
      }

      public GomeostasSystem.BehaviorStyle this[int key]
      {
        get { return _inner != null && _inner.ContainsKey(key) ? _inner[key] : null; }
      }
    }
  }
}
