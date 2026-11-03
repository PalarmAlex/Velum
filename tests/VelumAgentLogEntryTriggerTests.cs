using Velum.UI.Logs;
using Xunit;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>
  /// Регрессия: колонка «Триггер» отчёта логов выводит триггер по иерархии
  /// «чей мотор в строке, тот триггер». В строке автоматизма — ID узла дерева
  /// автоматизмов (ТриггерАВ), иначе — ID образа восприятия (Триггер).
  /// Раньше в строке автоматизма показывался PerceptionImage (часто «пустой» —
  /// только цвет), из-за чего казалось, что именно он запустил автоматизм.
  /// </summary>
  public class VelumAgentLogEntryTriggerTests
  {
    [Fact]
    public void DisplayTrigger_AutomatizmRow_ShowsAutomatizmTreeNodeId()
    {
      // Строка запуска автоматизма: автоматизм заполнен, ТриггерАВ = ID узла дерева,
      // Триггер (PerceptionImage) — другой счётчик. Показываем узел дерева, а не образ.
      var entry = new VelumAgentLogEntry
      {
        AutomatizmId = 7,
        TriggerStimulusId = 3,
        TriggerAutomatizmNodeId = 42
      };

      Assert.True(entry.HasAutomatizm);
      Assert.Equal("42", entry.DisplayTrigger);
    }

    [Fact]
    public void DisplayTrigger_ReflexRow_ShowsPerceptionImageId()
    {
      // Строка рефлекса: автоматизма нет — прежний Триггер (ID образа восприятия).
      var entry = new VelumAgentLogEntry
      {
        ConditionReflexId = 5,
        TriggerStimulusId = 3,
        TriggerAutomatizmNodeId = null
      };

      Assert.False(entry.HasAutomatizm);
      Assert.Equal("3", entry.DisplayTrigger);
    }

    [Fact]
    public void DisplayTrigger_AutomatizmWithoutNode_ShowsDash()
    {
      // Граничный случай: автоматизм запущен, но узел дерева не определён —
      // прочерк, не падение и не подстановка PerceptionImage.
      var entry = new VelumAgentLogEntry
      {
        AutomatizmId = 7,
        TriggerStimulusId = 3,
        TriggerAutomatizmNodeId = null
      };

      Assert.Equal("-", entry.DisplayTrigger);
    }

    [Fact]
    public void DisplayTrigger_OldLogWithoutColumn_FallsBackToPerceptionImage()
    {
      // Старый прогон без колонки ТриггерАВ: парсер даёт null, автоматизма нет —
      // используется Триггер (прежнее поведение).
      var entry = new VelumAgentLogEntry
      {
        GeneticReflexId = 2,
        TriggerStimulusId = 9,
        TriggerAutomatizmNodeId = null
      };

      Assert.Equal("9", entry.DisplayTrigger);
    }

    [Fact]
    public void DisplayTrigger_NeverCollapsesDifferentIdSpaces()
    {
      // Числа из разных пространств имён (счётчики образов и узлов независимы):
      // при совпадении значений в строке автоматизма показывается именно узел дерева.
      var entry = new VelumAgentLogEntry
      {
        AutomatizmId = 7,
        TriggerStimulusId = 5,
        TriggerAutomatizmNodeId = 5
      };

      Assert.Equal("5", entry.DisplayTrigger);
      // Значения не схлопываются: свойства остаются раздельными.
      Assert.Equal(5, entry.TriggerStimulusId);
      Assert.Equal(5, entry.TriggerAutomatizmNodeId);
    }
  }
}
