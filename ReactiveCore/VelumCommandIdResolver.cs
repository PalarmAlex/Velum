using ISIDA.Sensors;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Резолвинг <c>CommandPatternId</c> адаптивного действия в числовой код команды SolidWorks.
  /// <para>
  /// Использует <c>SensorySystem.CommandChannel.GetPhraseFromPhraseId</c> для получения
  /// человекочитаемого токена (например, "sw:21"), затем парсит число через
  /// <see cref="VelumRunSwCommandEchoRules.TryParseSwCode"/>.
  /// </para>
  /// </summary>
  internal static class VelumCommandIdResolver
  {
    /// <summary>
    /// Резолвит CommandPatternId адаптивного действия в числовой код команды SolidWorks.
    /// Возвращает 0, если резолвинг неудачен (нет ISIDA, нет канала, токен не sw:N).
    /// </summary>
    /// <param name="commandPatternId">ID паттерна Command-канала из AdaptiveAction.CommandPatternId.</param>
    /// <returns>Числовой код команды (например, 21) или 0.</returns>
    public static int ResolveSwCode(int commandPatternId)
    {
      if (commandPatternId <= 0)
        return 0;

      try
      {
        if (!SensorySystem.IsInitialized)
          return 0;

        var channel = SensorySystem.Instance.CommandChannel;
        if (channel == null)
          return 0;

        string phrase = channel.GetPhraseFromPhraseId(commandPatternId);
        if (!VelumRunSwCommandEchoRules.TryParseSwCode(phrase, out int commandId))
          return 0;

        return commandId;
      }
      catch
      {
        return 0;
      }
    }
  }
}
