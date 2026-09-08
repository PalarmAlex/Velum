using System;
using System.Collections.Generic;
using ISIDA.Common;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Успешно исполненные эпизоды G_AD: <c>actionId|activationPulse|document</c>.
  /// Пока G_AD активно, dispatch повторяется на следующих пульсах до успеха (см. <see cref="RecipeDispatcher"/>).
  /// </summary>
  internal static class RecipeDispatchEpisodeTracker
  {
    private static readonly object Sync = new object();
    private static readonly HashSet<string> DispatchedEpisodes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<int, int> RefractoryActions =
        new Dictionary<int, int>(); // actionId → pulse, до которого рефрактерность

    /// <summary>
    /// Очищает все данные трекера (при остановке пульсации или смене документа).
    /// </summary>
    internal static void Clear()
    {
      lock (Sync)
      {
        DispatchedEpisodes.Clear();
        RefractoryActions.Clear();
      }
    }

    /// <summary>
    /// Устанавливает рефрактерность для действия на заданное количество пульсов.
    /// В это время диалог не будет открываться повторно.
    /// </summary>
    /// <param name="actionId">ID адаптивного действия.</param>
    /// <param name="refractoryPulses">Количество пульсов рефрактерности.</param>
    internal static void MarkRefractory(int actionId, int refractoryPulses = 30)
    {
      lock (Sync)
      {
        RefractoryActions[actionId] = GlobalTimer.GlobalPulsCount + refractoryPulses;
        Logger.Info($"RecipeDispatchEpisodeTracker: refractory action={actionId} until pulse={RefractoryActions[actionId]}");
      }
    }

    /// <summary>
    /// Проверяет, находится ли действие в рефрактерности.
    /// </summary>
    /// <param name="actionId">ID адаптивного действия.</param>
    /// <returns>True, если действие рефрактерно (блокировано).</returns>
    internal static bool IsRefractory(int actionId)
    {
      lock (Sync)
      {
        if (!RefractoryActions.TryGetValue(actionId, out int untilPulse))
          return false;

        if (GlobalTimer.GlobalPulsCount >= untilPulse)
        {
          // Рефрактерность истекла — убираем
          RefractoryActions.Remove(actionId);
          Logger.Info($"RecipeDispatchEpisodeTracker: refractory expired action={actionId}");
          return false;
        }

        return true;
      }
    }

    internal static bool WasSuccessfullyDispatched(string episodeKey)
    {
      if (string.IsNullOrWhiteSpace(episodeKey))
        return false;

      lock (Sync)
        return DispatchedEpisodes.Contains(episodeKey);
    }

    internal static void MarkSuccessfullyDispatched(string episodeKey)
    {
      if (string.IsNullOrWhiteSpace(episodeKey))
        return;

      lock (Sync)
        DispatchedEpisodes.Add(episodeKey);
    }
  }
}
