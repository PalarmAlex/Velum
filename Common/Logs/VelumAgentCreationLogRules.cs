using System;
using System.Globalization;
using System.Text;

namespace Velum.UI.Logs
{
  /// <summary>
  /// Тип объекта, созданного движком ISIDA в рантайме (события
  /// <c>ConditionedReflexesSystem.ConditionedReflexCreated</c> и
  /// <c>AutomatizmSystem.AutomatizmCreated</c>).
  /// </summary>
  public enum VelumCreatedEntityKind
  {
    /// <summary>Условный рефлекс.</summary>
    УсловныйРефлекс,

    /// <summary>Автоматизм.</summary>
    Автоматизм
  }

  /// <summary>
  /// Одна запись сокращённого лога о создании объекта движком: тип, ID, опциональные
  /// резолвленные имя мотора, линия стимула и ID узла дерева автоматизмов
  /// (заполняются резолвером в UI-потоке).
  /// </summary>
  public sealed class VelumCreatedEntityRecord
  {
    /// <summary>Тип созданного объекта.</summary>
    public VelumCreatedEntityKind Kind { get; set; }

    /// <summary>ID объекта в системе ISIDA.</summary>
    public int Id { get; set; }

    /// <summary>Имя мотора (первое адаптивное действие); пусто — если не резолвится.</summary>
    public string MotorName { get; set; } = string.Empty;

    /// <summary>
    /// Печатная линия стимула (результат <c>FormatLine</c> строки стимула); пусто — если
    /// стимул не резолвится или не применим (для автоматизмов).
    /// </summary>
    public string StimulusLine { get; set; } = string.Empty;

    /// <summary>
    /// ID узла дерева автоматизмов (<c>AutomatizmTree.BranchID</c>); заполняется для
    /// автоматизмов в <c>EmitCreationBlockUi</c> после резолва по <see cref="Id"/>.
    /// </summary>
    public int TreeNodeId { get; set; }
  }

  /// <summary>
  /// Чистые правила блока «СОЗДАНИЕ» сокращённого лога панели: сборка печатного блока и
  /// дедуп-сигнатура. Без зависимостей от ISIDA/WinForms — линкуется в тестовый проект
  /// <c>Velum.ReactiveCore.Tests</c>.
  /// </summary>
  public static class VelumAgentCreationLogRules
  {
    /// <summary>Заголовок блока создания.</summary>
    public const string CreationHeader = "СОЗДАНИЕ:";

    /// <summary>Подпись линии стимула в блоке создания.</summary>
    public const string StimulusLabel = "Стимул";

    /// <summary>Подпись линии узла дерева автоматизмов в блоке создания.</summary>
    public const string TreeNodeLabel = "Узел АВ";

    /// <summary>Русская подпись типа созданного объекта («У-рефлекс», «Автоматизм»).</summary>
    /// <param name="kind">Тип объекта.</param>
    /// <returns>Подпись типа.</returns>
    public static string KindLabel(VelumCreatedEntityKind kind)
    {
      return kind == VelumCreatedEntityKind.Автоматизм ? "Автоматизм" : "У-рефлекс";
    }

    /// <summary>
    /// Собирает печатный блок одной записи о создании: заголовок, линия объекта
    /// («У-рефлекс: ID 12, Экспорт PDF»), опциональные линии стимула и мотора.
    /// Возвращает <c>null</c> для некорректной записи (пустая или ID &lt;= 0).
    /// </summary>
    /// <param name="record">Запись о создании.</param>
    /// <param name="newLine">Разделитель строк (обычно <see cref="Environment.NewLine"/>).</param>
    /// <returns>Текст блока либо <c>null</c>.</returns>
    public static string BuildBlock(VelumCreatedEntityRecord record, string newLine)
    {
      if (record == null || record.Id <= 0)
        return null;

      if (string.IsNullOrEmpty(newLine))
        newLine = "\n";

      const string indent = "  ";
      var sb = new StringBuilder();
      sb.Append(CreationHeader);

      sb.Append(newLine).Append(indent).Append(KindLabel(record.Kind)).Append(": ID ")
        .Append(record.Id.ToString(CultureInfo.InvariantCulture));

      if (!string.IsNullOrWhiteSpace(record.StimulusLine))
      {
        sb.Append(newLine).Append(indent).Append(StimulusLabel).Append(": ")
          .Append(record.StimulusLine.Trim());
      }

      if (record.TreeNodeId > 0)
      {
        sb.Append(newLine).Append(indent).Append(TreeNodeLabel).Append(": ")
          .Append(record.TreeNodeId.ToString(CultureInfo.InvariantCulture));
      }

      if (!string.IsNullOrWhiteSpace(record.MotorName))
      {
        sb.Append(newLine).Append(indent).Append("Мотор: ")
          .Append(record.MotorName.Trim());
      }

      return sb.ToString();
    }

    /// <summary>
    /// Дедуп-сигнатура записи о создании: тип + ID (не зависит от резолвов — они могут
    /// меняться, а объект создаётся один раз).
    /// </summary>
    /// <param name="record">Запись о создании.</param>
    /// <returns>Стабильная строка-подпись (пустая для некорректной записи).</returns>
    public static string BuildSignature(VelumCreatedEntityRecord record)
    {
      if (record == null || record.Id <= 0)
        return string.Empty;

      return ((int)record.Kind).ToString(CultureInfo.InvariantCulture)
             + ":" + record.Id.ToString(CultureInfo.InvariantCulture);
    }
  }
}