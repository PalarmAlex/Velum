using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ISIDA.Actions;
using ISIDA.Psychic;
using ISIDA.Psychic.Automatism;
using ISIDA.Reflexes;
using ISIDA.Sensors;

namespace Velum.UI.Logs
{
  /// <summary>
  /// Резолвер сокращённого лога: парсит сырые строки хвоста <c>AgentLogs.csv</c>
  /// (разделитель «;») и превращает каждую пульс-строку в
  /// <see cref="VelumCondensedLogRecord"/>, подставляя имена стимула и мотора из
  /// стартовых справочников ISIDA. Те же API, что у <see cref="VelumLogCellTooltipProvider"/>:
  /// образы восприятия (<c>PerceptionImagesSystem</c>) → тип/имя стимула,
  /// рефлекс/автоматизм → имя адаптивного действия (<c>AdaptiveActionsSystem</c>).
  /// Stateful: запоминает карту колонок из последней строки-заголовка сессии.
  /// Поток-безопасен только при последовательных вызовах из одного фонового потока.
  /// </summary>
  internal sealed class VelumAgentLogCondensedResolver
  {
    /// <summary>Карта «имя колонки → индекс» из последнего заголовка; пуста до первой встречи заголовка.</summary>
    private Dictionary<string, int> _columns;

    /// <summary>
    /// Обрабатывает одну сырую строку хвоста лога. Строку-заголовок запоминает как карту
    /// колонок и возвращает <c>null</c>; пульс-строку разбирает в запись (может вернуть
    /// <c>null</c>, если строка не похожа на данные или нет карты колонок).
    /// </summary>
    /// <param name="line">Сырая строка (без перевода строки, без BOM).</param>
    /// <returns>Запись сокращённого лога либо <c>null</c>.</returns>
    internal VelumCondensedLogRecord ProcessLine(string line)
    {
      if (string.IsNullOrWhiteSpace(line))
        return null;

      if (IsHeaderRow(line))
      {
        _columns = ParseHeaderColumns(line);
        return null;
      }

      if (_columns == null || _columns.Count == 0)
        return null;

      string[] parts = line.Split(';');
      if (parts.Length < 3)
        return null;

      string triggerId = Get(parts, "Триггер");
      string geneticReflex = Get(parts, "Б/у рефлекс");
      string conditionedReflex = Get(parts, "Усл. рефлекс");
      string automatizm = Get(parts, "Автоматизм");
      string environment = Get(parts, "Среда");

      var record = new VelumCondensedLogRecord();
      ParseResponse(record, geneticReflex, conditionedReflex, automatizm);
      ParseStimulus(record, triggerId, environment);
      return record;
    }

    /// <summary>
    /// Резолвит имя мотора (первое адаптивное действие) для объекта, созданного движком
    /// (события <c>ConditionedReflexCreated</c> / <c>AutomatizmCreated</c>). Пусто — если
    /// система не инициализирована или действие не найдено.
    /// </summary>
    /// <param name="kind">Тип созданного объекта.</param>
    /// <param name="id">ID объекта.</param>
    /// <returns>Имя действия либо пустая строка.</returns>
    internal static string ResolveCreatedMotorName(VelumCreatedEntityKind kind, int id)
    {
      if (id <= 0)
        return string.Empty;
      return kind == VelumCreatedEntityKind.Автоматизм
          ? ResolveAutomatizmActionName(id)
          : ResolveConditionedReflexActionName(id);
    }

    /// <summary>
    /// Резолвит линии стимула по ID образа восприятия (поле <c>Level3</c> у-рефлекса —
    /// образ пускового стимула). Пустой список — если образ не найден или не разложен.
    /// </summary>
    /// <param name="imageId">ID образа восприятия.</param>
    /// <returns>Список линий стимула (может быть пустым).</returns>
    internal static List<VelumCondensedStimulusLine> ResolveStimulusLines(int imageId)
    {
      var record = new VelumCondensedLogRecord();
      if (imageId > 0)
        ResolvePerceptionImage(record, imageId);
      return record.Stimuli;
    }

    /// <summary>
    /// Признак строки-заголовка сессии (совпадает с детектором <c>VelumAgentLogFileSessions</c>).
    /// </summary>
    /// <param name="line">Строка лога.</param>
    /// <returns><c>true</c>, если строка — заголовок.</returns>
    internal static bool IsHeaderRow(string line)
    {
      line = line ?? string.Empty;
      return line.IndexOf("Автоматизм", StringComparison.Ordinal) >= 0
             && line.IndexOf("Время", StringComparison.Ordinal) >= 0
             && line.IndexOf("Пульс", StringComparison.Ordinal) >= 0
             && line.IndexOf(';') >= 0;
    }

    /// <summary>Строит карту «имя колонки → индекс» из строки заголовка.</summary>
    /// <param name="headerLine">Строка заголовка CSV.</param>
    /// <returns>Словарь «имя → индекс».</returns>
    internal static Dictionary<string, int> ParseHeaderColumns(string headerLine)
    {
      var map = new Dictionary<string, int>(StringComparer.Ordinal);
      string[] parts = VelumCsvLogSessionReader.StripBom(headerLine ?? string.Empty).Split(';');
      for (int i = 0; i < parts.Length; i++)
      {
        string name = parts[i].Trim();
        if (string.IsNullOrEmpty(name) || map.ContainsKey(name))
          continue;
        map[name] = i;
      }
      return map;
    }

    private string Get(string[] parts, string key)
    {
      if (_columns != null && _columns.TryGetValue(key, out int ix) && ix < parts.Length)
        return (parts[ix] ?? string.Empty).Trim();
      return string.Empty;
    }

    private static void ParseStimulus(VelumCondensedLogRecord record, string triggerId, string environment)
    {
      int imageId = ParseId(triggerId);
      if (imageId > 0)
      {
        ResolvePerceptionImage(record, imageId);
        return;
      }

      // Триггер пустой: если было давление среды и есть ответ — стимул пришёл от среды.
      if (!string.IsNullOrWhiteSpace(environment) && record.HasResponse)
      {
        record.Stimuli.Add(new VelumCondensedStimulusLine
        {
          Kind = VelumCondensedStimulusKind.Среда,
          Id = 0,
          Text = environment.Trim()
        });
      }
    }

    private static void ResolvePerceptionImage(VelumCondensedLogRecord record, int imageId)
    {
      try
      {
        if (!PerceptionImagesSystem.IsInitialized)
          return;

        var image = PerceptionImagesSystem.Instance.GetAllPerceptionImagesList()
            .FirstOrDefault(img => img != null && img.Id == imageId);
        if (image == null)
          return;

        // У ISIDA два отдельных вербальных канала: фразы (PhraseIdList) живут в VerbalChannel,
        // команды (CommandPatternIdList) — в CommandChannel. Один и тот же числовой ID в них
        // означает разное: фразу «п» и команду SolidWorks «sw:…». Резолвим каждый по своему каналу.
        var verbalChannel = SensorySystem.IsInitialized ? SensorySystem.Instance.VerbalChannel : null;
        var commandChannel = SensorySystem.IsInitialized ? SensorySystem.Instance.CommandChannel : null;

        // 1. Воздействия (не зонды среды).
        if (image.InfluenceActionsList != null)
        {
          IEnumerable<InfluenceActionSystem.GomeostasisInfluenceAction> allInfluences =
              InfluenceActionSystem.IsInitialized
                  ? InfluenceActionSystem.Instance.GetAllInfluenceActions()
                  : Enumerable.Empty<InfluenceActionSystem.GomeostasisInfluenceAction>();

          foreach (int actId in image.InfluenceActionsList)
          {
            // Зонды среды (метрики) не относим к операторскому воздействию.
            if (InfluenceActionSystem.IsInitialized && InfluenceActionSystem.Instance.IsEnvironmentProbeActionId(actId))
              continue;

            string name = allInfluences.FirstOrDefault(a => a != null && a.Id == actId)?.Name ?? string.Empty;
            record.Stimuli.Add(new VelumCondensedStimulusLine
            {
              Kind = VelumCondensedStimulusKind.Воздействие,
              Id = actId,
              Text = name
            });
          }
        }

        // 2. Команды командного канала.
        if (image.CommandPatternIdList != null)
        {
          foreach (int patternId in image.CommandPatternIdList)
          {
            if (patternId <= 0)
              continue;
            record.Stimuli.Add(new VelumCondensedStimulusLine
            {
              Kind = VelumCondensedStimulusKind.Команда,
              Id = patternId,
              Text = ResolveTriggerText(commandChannel, patternId)
            });
          }
        }

        // 3. Вербальные фразы.
        if (image.PhraseIdList != null)
        {
          foreach (int phraseId in image.PhraseIdList)
          {
            if (phraseId <= 0)
              continue;
            record.Stimuli.Add(new VelumCondensedStimulusLine
            {
              Kind = VelumCondensedStimulusKind.Фраза,
              Id = phraseId,
              Text = ResolveTriggerText(verbalChannel, phraseId)
            });
          }
        }

        // 4. Цветовой фон — единственный канал образа-спуска. У автоматизмов ISIDA создаёт
        // отдельный PerceptionImage, несущий только цвет (контекст «деталь/сборка/чертёж»):
        // без этой линии блок запуска автоматизма не строился бы и терялся в ленте. Цвет
        // добавляем только когда вербальные каналы пусты (иначе это шум рядом с фразой/командой).
        if (record.Stimuli.Count == 0
            && AgentVisualColor.IsValidCode(image.VisualColorId)
            && image.VisualColorId != AgentVisualColor.White)
        {
          record.Stimuli.Add(new VelumCondensedStimulusLine
          {
            Kind = VelumCondensedStimulusKind.ЦветовойФон,
            Id = 0,
            Text = AgentVisualColor.GetDisplayName(image.VisualColorId)
          });
        }
      }
      catch
      {
        // Сбой справочника — стимул просто не будет разложен; вывод не роняем.
      }
    }

    private static string ResolveTriggerText(VerbalSensorChannel channel, int id)
    {
      if (channel == null)
        return string.Empty;
      try
      {
        return channel.GetPhraseFromPhraseId(id) ?? string.Empty;
      }
      catch
      {
        return string.Empty;
      }
    }

    private static void ParseResponse(
        VelumCondensedLogRecord record,
        string geneticReflex,
        string conditionedReflex,
        string automatizm)
    {
      // Ровно один контур активен в пульсе (иерархия ISIDA). Приоритет чтения колонок
      // не важен, но фиксируем детерминированный порядок: б/у → у-рефлекс → автоматизм.
      int reflexId = ParseId(geneticReflex);
      if (reflexId > 0)
      {
        record.ResponseKind = VelumCondensedResponseKind.БезусловныйРефлекс;
        record.ResponseId = reflexId;
        record.ResponseActionName = ResolveGeneticReflexActionName(reflexId);
        return;
      }

      int crxId = ParseId(conditionedReflex);
      if (crxId > 0)
      {
        record.ResponseKind = VelumCondensedResponseKind.УсловныйРефлекс;
        record.ResponseId = crxId;
        record.ResponseActionName = ResolveConditionedReflexActionName(crxId);
        return;
      }

      int atmzId = ParseId(automatizm);
      if (atmzId > 0)
      {
        record.ResponseKind = VelumCondensedResponseKind.Автоматизм;
        record.ResponseId = atmzId;
        record.ResponseActionName = ResolveAutomatizmActionName(atmzId);
      }
    }

    private static int ParseId(string raw)
    {
      if (string.IsNullOrWhiteSpace(raw))
        return 0;
      raw = raw.Trim();
      if (raw == "-")
        return 0;
      return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) && v > 0 ? v : 0;
    }

    private static string ResolveGeneticReflexActionName(int reflexId)
    {
      try
      {
        if (!GeneticReflexesSystem.IsInitialized)
          return string.Empty;

        var reflex = GeneticReflexesSystem.Instance.GetAllGeneticReflexesList()
            .FirstOrDefault(r => r != null && r.Id == reflexId);
        if (reflex == null || reflex.AdaptiveActions == null || reflex.AdaptiveActions.Count == 0)
          return string.Empty;

        return ResolveFirstActionName(reflex.AdaptiveActions);
      }
      catch
      {
        return string.Empty;
      }
    }

    private static string ResolveConditionedReflexActionName(int crxId)
    {
      try
      {
        if (!ConditionedReflexesSystem.IsInitialized)
          return string.Empty;

        var crx = ConditionedReflexesSystem.Instance.GetAllConditionedReflexes()
            .FirstOrDefault(r => r != null && r.Id == crxId);
        if (crx == null)
          return string.Empty;

        // У-рефлекс исполняет действия исходного безусловного рефлекса.
        return ResolveGeneticReflexActionName(crx.SourceGeneticReflexId);
      }
      catch
      {
        return string.Empty;
      }
    }

    private static string ResolveAutomatizmActionName(int atmzId)
    {
      try
      {
        if (!AutomatizmSystem.IsInitialized)
          return string.Empty;

        var atmz = AutomatizmSystem.Instance.GetAutomatizmById(atmzId);
        if (atmz == null)
          return string.Empty;

        int actionsImageId = atmz.ActionsImageID;
        if (actionsImageId <= 0 || !ActionsImagesSystem.IsInitialized)
          return string.Empty;

        var image = ActionsImagesSystem.Instance.GetActionsImage(actionsImageId);
        if (image == null || image.ActIdList == null || image.ActIdList.Count == 0)
          return string.Empty;

        return ResolveFirstActionName(image.ActIdList);
      }
      catch
      {
        return string.Empty;
      }
    }

    private static string ResolveFirstActionName(List<int> actionIds)
    {
      try
      {
        if (!AdaptiveActionsSystem.IsInitialized)
          return string.Empty;

        var allActions = AdaptiveActionsSystem.Instance.GetAllAdaptiveActions().ToList();
        foreach (int actId in actionIds)
        {
          var act = allActions.FirstOrDefault(a => a != null && a.Id == actId);
          if (act != null && !string.IsNullOrWhiteSpace(act.Name))
            return act.Name;
        }
      }
      catch
      {
      }
      return string.Empty;
    }
  }
}