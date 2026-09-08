using System;
using System.Collections.Generic;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Кулдаун dispatch рецепта по ключу <c>(recipe_id, document)</c> в тактах пульса.
  /// </summary>
  internal static class RecipeDispatchCooldownTracker
  {
    private static readonly object Sync = new object();
    private static readonly Dictionary<string, int> LastDispatchPulseByKey =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Сбрасывает все записи кулдауна.</summary>
    public static void Clear()
    {
      lock (Sync)
        LastDispatchPulseByKey.Clear();
    }

    /// <summary>
    /// Возвращает <c>true</c>, если dispatch для ключа разрешён на данном пульсе.
    /// </summary>
    public static bool TryAllowDispatch(string cooldownKey, int currentPulse, int cooldownPulses)
    {
      if (string.IsNullOrWhiteSpace(cooldownKey))
        return true;

      if (cooldownPulses <= 0)
        return true;

      lock (Sync)
      {
        if (!LastDispatchPulseByKey.TryGetValue(cooldownKey, out int lastPulse))
          return true;

        return currentPulse - lastPulse >= cooldownPulses;
      }
    }

    /// <summary>Фиксирует успешный dispatch на пульсе.</summary>
    public static void RegisterDispatch(string cooldownKey, int currentPulse)
    {
      if (string.IsNullOrWhiteSpace(cooldownKey))
        return;

      lock (Sync)
        LastDispatchPulseByKey[cooldownKey] = currentPulse;
    }
  }
}
