namespace Velum.UI
{
  // Сокращённый лог «стимул → ответ» и события создания объектов движком переведены на
  // прямую подписку на канал ISIDA ShortLogChannel (см. VelumAgentTaskPane.ShortLog.cs).
  // Файловый опрос AgentLogs.csv и события ConditionedReflexCreated / AutomatizmCreated
  // больше не используются; этот partial сохранён пустым, чтобы не менять csproj.
  public sealed partial class VelumAgentTaskPane
  {
  }
}