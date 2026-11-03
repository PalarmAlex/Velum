using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Velum.UI.Logs
{
  /// <summary>
  /// Тип стимула в сокращённом логе «стимул → ответ». Определяется по составу образа
  /// восприятия (<c>PerceptionImage</c>): непустой список воздействий — «Воздействие»,
  /// непустой список команд — «Команда», непустой список фраз — «Фраза»; пустой триггер
  /// при реакции на давление среды — «Среда».
  /// </summary>
  public enum VelumCondensedStimulusKind
  {
    /// <summary>Операторское воздействие на параметры (EA) или зонд среды — из <c>InfluenceActionsList</c>.</summary>
    Воздействие,

    /// <summary>Команда командного канала — из <c>CommandPatternIdList</c>.</summary>
    Команда,

    /// <summary>Вербальная фраза — из <c>PhraseIdList</c>.</summary>
    Фраза,

    /// <summary>Стимул от среды (состояние/стили) без пускового триггера оператора.</summary>
    Среда,

    /// <summary>
    /// Цветовой фон образа-спуска (<c>VisualColorId</c>) — единственный канал образа, пустого
    /// по воздействиям/фразам/командам. Для автоматизмов ISIDA создаёт отдельный образ-спуск,
    /// несущий только цвет (контекст «деталь/сборка/чертёж»); без этой линии блок запуска
    /// автоматизма не строился бы и терялся в ленте.
    /// </summary>
    ЦветовойФон,

    /// <summary>
    /// ID узла дерева автоматизмов (<c>AutomatizmTree</c>, колонка «ТриггерАВ»), по которому
    /// сматчился автоматизм. По правилу «чей мотор в строке, тот триггер» для строки
    /// автоматизма печатается вместо образа восприятия (узла-спуска с цветом).
    /// </summary>
    УзелДереваАВ
  }

  /// <summary>
  /// Тип ответа симбионта в сокращённом логе: какой контур активирован в пульсе.
  /// В одном пульсе активен ровно один из них (иерархия активации ISIDA).
  /// </summary>
  public enum VelumCondensedResponseKind
  {
    /// <summary>Безусловный (генетический) рефлекс.</summary>
    БезусловныйРефлекс,

    /// <summary>Условный рефлекс.</summary>
    УсловныйРефлекс,

    /// <summary>Автоматизм.</summary>
    Автоматизм
  }

  /// <summary>
  /// Одна линия стимула сокращённого лога (один образ восприятия может дать несколько
  /// линий: воздействие + фраза + команда одновременно).
  /// </summary>
  public sealed class VelumCondensedStimulusLine
  {
    /// <summary>Тип стимула.</summary>
    public VelumCondensedStimulusKind Kind { get; set; }

    /// <summary>
    /// Идентификатор (для «Воздействия» и «Среды» печатается как <c>ID N</c>;
    /// для вербальных «Команда»/«Фраза» не выводится).
    /// </summary>
    public int Id { get; set; }

    /// <summary>Имя воздействия или текст вербального триггера (для «Среды» — описание).</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Формирует печатную строку линии стимула. Вербальные типы («Команда», «Фраза»)
    /// печатаются без ID, текст оборачивается кавычками; «Цветовой фон» — именем цвета
    /// без ID и кавычек; «Воздействие» и «Среда» — с идентификатором.
    /// </summary>
    /// <returns>Строка вида «Воздействие: ID 65, Запуск экспорта PDF» или «Фраза: "привет"».</returns>
    public string FormatLine()
    {
      string label = Label(Kind);
      if (Kind == VelumCondensedStimulusKind.Команда || Kind == VelumCondensedStimulusKind.Фраза)
        return label + ": " + Quote(Text);

      // Цвет печатается именем (код 0 = «белый» валиден, но по hasId не прошёл бы).
      if (Kind == VelumCondensedStimulusKind.ЦветовойФон)
        return string.IsNullOrWhiteSpace(Text) ? label + ": —" : label + ": " + Text;

      bool hasId = Id > 0;
      bool hasText = !string.IsNullOrWhiteSpace(Text);
      if (hasId && hasText)
        return label + ": ID " + Id.ToString(CultureInfo.InvariantCulture) + ", " + Text;
      if (hasId)
        return label + ": ID " + Id.ToString(CultureInfo.InvariantCulture);
      if (hasText)
        return label + ": " + Text;
      return label + ": —";
    }

    /// <summary>Стабильная сигнатура линии для дедупликации (не чувствительна к переформатированию).</summary>
    public string BuildSignaturePart()
    {
      return ((int)Kind).ToString(CultureInfo.InvariantCulture)
             + ":" + Id.ToString(CultureInfo.InvariantCulture)
             + ":" + (Text ?? string.Empty);
    }

    /// <summary>Русская подпись типа стимула.</summary>
    /// <param name="kind">Тип стимула.</param>
    /// <returns>Подпись: «Воздействие», «Команда», «Фраза», «Среда» или «Цветовой фон».</returns>
    public static string Label(VelumCondensedStimulusKind kind)
    {
      switch (kind)
      {
        case VelumCondensedStimulusKind.Команда:
          return "Команда";
        case VelumCondensedStimulusKind.Фраза:
          return "Фраза";
        case VelumCondensedStimulusKind.Среда:
          return "Среда";
        case VelumCondensedStimulusKind.ЦветовойФон:
          return "Цветовой фон";
        case VelumCondensedStimulusKind.УзелДереваАВ:
          return "Узел дерева автоматизмов";
        default:
          return "Воздействие";
      }
    }

    private static string Quote(string text)
    {
      return "\"" + (text ?? string.Empty) + "\"";
    }
  }

  /// <summary>
  /// Одна пульс-строка сокращённого лога: ноль или более линий стимула и ровно один
  /// (или ни одного) ответ. Строится резолвером из строки <c>AgentLogs.csv</c>.
  /// </summary>
  public sealed class VelumCondensedLogRecord
  {
    /// <summary>Линии стимула (пусто — если в образе восприятия ничего не распознано и среда не помечена).</summary>
    public List<VelumCondensedStimulusLine> Stimuli { get; } = new List<VelumCondensedStimulusLine>();

    /// <summary>Тип активированного контура; <c>null</c> — реакции в пульсе не было.</summary>
    public VelumCondensedResponseKind? ResponseKind { get; set; }

    /// <summary>Идентификатор рефлекса/автоматизма из колонки ответа.</summary>
    public int ResponseId { get; set; }

    /// <summary>Имя мотора (первое адаптивное действие контура); пусто — если не резолвится.</summary>
    public string ResponseActionName { get; set; } = string.Empty;

    /// <summary>Есть ли ответ (сокращённый лог печатает только пары «стимул → ответ»).</summary>
    public bool HasResponse
    {
      get { return ResponseKind.HasValue; }
    }

    /// <summary>Печатная строка ответа вида «Б/У рефлекс: ID 5, Экспорт PDF».</summary>
    /// <returns>Строка ответа.</returns>
    public string FormatResponseLine()
    {
      if (!ResponseKind.HasValue)
        return string.Empty;

      string label = VelumAgentLogCondensedRules.ResponseLabel(ResponseKind.Value);
      bool hasId = ResponseId > 0;
      bool hasName = !string.IsNullOrWhiteSpace(ResponseActionName);
      if (hasId && hasName)
        return label + ": ID " + ResponseId.ToString(CultureInfo.InvariantCulture) + ", " + ResponseActionName;
      if (hasId)
        return label + ": ID " + ResponseId.ToString(CultureInfo.InvariantCulture);
      if (hasName)
        return label + ": " + ResponseActionName;
      return label + ": —";
    }
  }

  /// <summary>
  /// Чистые правила сокращённого лога агента: сборка текстового блока «Стимул/Ответ»,
  /// признак «есть ответ» и дедуп-сигнатура. Без зависимостей от ISIDA/WinForms — линкуется
  /// в тестовый проект <c>Velum.ReactiveCore.Tests</c>.
  /// </summary>
  public static class VelumAgentLogCondensedRules
  {
    /// <summary>Заголовок блока стимула.</summary>
    public const string StimulusHeader = "СТИМУЛ:";

    /// <summary>Заголовок блока ответа.</summary>
    public const string ResponseHeader = "ОТВЕТ:";

    /// <summary>Русская подпись типа ответа.</summary>
    /// <param name="kind">Тип ответа.</param>
    /// <returns>«Б/У рефлекс», «У-рефлекс» или «Автоматизм».</returns>
    public static string ResponseLabel(VelumCondensedResponseKind kind)
    {
      switch (kind)
      {
        case VelumCondensedResponseKind.УсловныйРефлекс:
          return "У-рефлекс";
        case VelumCondensedResponseKind.Автоматизм:
          return "Автоматизм";
        default:
          return "Б/У рефлекс";
      }
    }

    /// <summary>
    /// Печатать ли запись: только при наличии ответа (пара «стимул → ответ»). Стимул без
    /// реакции и реакция без стимула в сокращённом логе не интересны.
    /// </summary>
    /// <param name="record">Запись лога.</param>
    /// <returns><c>true</c>, если есть ответ.</returns>
    public static bool ShouldEmit(VelumCondensedLogRecord record)
    {
      return record != null && record.HasResponse;
    }

    /// <summary>
    /// Собирает печатный блок одной записи. Возвращает <c>null</c>, если запись не должна
    /// печататься (нет ответа или нет линий стимула).
    /// </summary>
    /// <param name="record">Запись лога.</param>
    /// <param name="newLine">Разделитель строк (обычно <see cref="Environment.NewLine"/>).</param>
    /// <returns>Текст блока либо <c>null</c>.</returns>
    public static string BuildBlock(VelumCondensedLogRecord record, string newLine)
    {
      if (!ShouldEmit(record) || record.Stimuli.Count == 0)
        return null;

      if (string.IsNullOrEmpty(newLine))
        newLine = "\n";

      const string indent = "  ";
      var sb = new StringBuilder();
      sb.Append(StimulusHeader);
      for (int i = 0; i < record.Stimuli.Count; i++)
      {
        sb.Append(newLine);
        sb.Append(indent).Append(record.Stimuli[i].FormatLine());
      }

      sb.Append(newLine);
      sb.Append(ResponseHeader);
      sb.Append(newLine);
      sb.Append(indent).Append(record.FormatResponseLine());
      return sb.ToString();
    }

    /// <summary>
    /// Дедуп-сигнатура записи: комбинация всех линий стимула и ответа. Одинаковые
    /// сигнатуры подряд (рефлекс держится несколько пульсов)suppressируются.
    /// </summary>
    /// <param name="record">Запись лога.</param>
    /// <returns>Стабильная строка-подпись (пустая для пустой записи).</returns>
    public static string BuildSignature(VelumCondensedLogRecord record)
    {
      if (record == null)
        return string.Empty;

      var sb = new StringBuilder();
      for (int i = 0; i < record.Stimuli.Count; i++)
      {
        if (i > 0)
          sb.Append('|');
        sb.Append(record.Stimuli[i].BuildSignaturePart());
      }

      sb.Append("=>");
      if (record.ResponseKind.HasValue)
      {
        sb.Append(((int)record.ResponseKind.Value).ToString(CultureInfo.InvariantCulture))
          .Append(':')
          .Append(record.ResponseId.ToString(CultureInfo.InvariantCulture))
          .Append(':')
          .Append(record.ResponseActionName ?? string.Empty);
      }

      return sb.ToString();
    }

    /// <summary>
    /// Подавлять ли вывод записи, если её сигнатура совпадает с только что выведенной.
    /// </summary>
    /// <param name="signature">Сигнатура текущей записи.</param>
    /// <param name="lastEmittedSignature">Сигнатура последней выведенной записи.</param>
    /// <returns><c>true</c>, если это повтор и печатать не нужно.</returns>
    public static bool IsDuplicate(string signature, string lastEmittedSignature)
    {
      return !string.IsNullOrEmpty(signature)
             && string.Equals(signature, lastEmittedSignature, StringComparison.Ordinal);
    }

    /// <summary>
    /// Склеивает новые блоки и существующий текст окна в ленту «свежее сверху»: блоки
    /// (приходят в хронологическом порядке) разворачиваются и ставятся перед старым текстом,
    /// разделитель между блоками и старым текстом — двойной <paramref name="newLine"/>.
    /// </summary>
    /// <param name="existingText">Текущий текст окна вывода (старые блоки).</param>
    /// <param name="blocks">Новые блоки в хронологическом порядке (последний — самый свежий).</param>
    /// <param name="newLine">Разделитель строк (обычно <see cref="Environment.NewLine"/>).</param>
    /// <returns>Новый текст окна: самый свежий блок сверху, ниже — предыдущие и старые блоки.</returns>
    public static string PrependBlocks(string existingText, IList<string> blocks, string newLine)
    {
      if (blocks == null || blocks.Count == 0)
        return existingText ?? string.Empty;

      if (string.IsNullOrEmpty(newLine))
        newLine = "\n";

      string separator = newLine + newLine;
      var sb = new StringBuilder();

      // Разворот: блоки идут от старых к новым, а сверху должен оказаться самый свежий.
      for (int i = blocks.Count - 1; i >= 0; i--)
      {
        if (string.IsNullOrEmpty(blocks[i]))
          continue;
        if (sb.Length > 0)
          sb.Append(separator);
        sb.Append(blocks[i]);
      }

      if (sb.Length == 0)
        return existingText ?? string.Empty;

      if (!string.IsNullOrEmpty(existingText))
      {
        sb.Append(separator);
        sb.Append(existingText);
      }

      return sb.ToString();
    }

    /// <summary>
    /// Ограничивает длину ленты, отрезая старые блоки с конца (свежие блоки в начале
    /// сохраняются). Режет по границе блока — двойному <paramref name="newLine"/> — чтобы
    /// не рвать блок пополам.
    /// </summary>
    /// <param name="text">Текущий текст окна вывода.</param>
    /// <param name="maxChars">Порог, при превышении которого выполняется обрезка.</param>
    /// <param name="newLine">Разделитель строк (обычно <see cref="Environment.NewLine"/>).</param>
    /// <returns>Обрезанный текст (не длиннее исходного); при пустом входе — пустая строка.</returns>
    public static string TrimToHead(string text, int maxChars, string newLine)
    {
      if (string.IsNullOrEmpty(text))
        return string.Empty;
      if (text.Length <= maxChars)
        return text;

      if (string.IsNullOrEmpty(newLine))
        newLine = "\n";

      // Оставляем примерно половину объёма, старое (хвост) отсекаем.
      int keep = maxChars / 2;
      if (keep <= 0)
        return string.Empty;

      string separator = newLine + newLine;

      // Граница блока не позже целевой длины: отрезаем старые блоки с конца.
      int boundary = text.LastIndexOf(separator, keep, StringComparison.Ordinal);
      if (boundary > 0)
        return text.Substring(0, boundary);

      // Целевая длина попала внутрь самого свежего (первого) блока — не рвём его:
      // оставляем всё до первого разделителя (то есть первый блок целиком).
      int firstSeparator = text.IndexOf(separator, StringComparison.Ordinal);
      if (firstSeparator > 0)
        return text.Substring(0, firstSeparator);

      // Разделителей нет (один блок) — оставляем как есть, не обрезаем.
      return text;
    }
  }
}