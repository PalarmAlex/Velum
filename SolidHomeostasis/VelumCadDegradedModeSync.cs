using ISIDA.Common;
using Velum.Configuration;
using Velum.Isida;
using Xarial.XCad;

namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Синхронизация <see cref="AppGlobalState.HostEnvironmentDegraded"/> с SessionHealth на такте пульса (п. 3 плана устойчивости).
  /// Гистерезис: вход ниже <see cref="VelumAppConfig.CadDegradedEnterThreshold"/>, выход выше <see cref="VelumAppConfig.CadDegradedExitThreshold"/>.
  /// </summary>
  internal static class VelumCadDegradedModeSync
  {
    private static bool _modeActive;

    /// <summary>Режим деградации активен по гистерезису Velum (до сброса <see cref="Reset"/>).</summary>
    internal static bool ModeActive => _modeActive;

    /// <summary>
    /// Обновляет флаг движка по текущему SessionHealth (<see cref="VelumSolidSessionHealth.ComputeScore"/>). Вызывать на пульсе.
    /// </summary>
    /// <param name="pulseNumber">Номер пульса для трассировки.</param>
    /// <param name="sessionHealthScore">Значение 0…100.</param>
    internal static void SyncFromSessionHealthOnPulse(int pulseNumber, float sessionHealthScore)
    {
      if (!VelumIsidaHost.IsReady)
        return;

      float enter = VelumAppConfig.CadDegradedEnterThreshold;
      float exit = VelumAppConfig.CadDegradedExitThreshold;
      if (exit <= enter)
        exit = enter + 1f;

      bool wasActive = _modeActive;
      if (!_modeActive && sessionHealthScore < enter)
        _modeActive = true;
      else if (_modeActive && sessionHealthScore > exit)
        _modeActive = false;

      if (AppGlobalState.HostEnvironmentDegraded != _modeActive)
        AppGlobalState.HostEnvironmentDegraded = _modeActive;

      //if (wasActive != _modeActive)
      //  VelumSolidDiagLog.WriteCadDegradedTransition(pulseNumber, _modeActive, sessionHealthScore, enter, exit);
    }

    /// <summary>
    /// Пересчёт по текущей сессии SW (UI панели агента).
    /// </summary>
    internal static void SyncFromCurrentSession(IXApplication app)
    {
      if (!VelumIsidaHost.IsReady)
        return;
      float score = VelumSolidSessionHealth.ComputeScore(app);
      SyncFromSessionHealthOnPulse(GlobalTimer.GlobalPulsCount, score);
    }

    /// <summary>Сброс при отключении моста / остановке движка.</summary>
    internal static void Reset()
    {
      _modeActive = false;
      if (VelumIsidaHost.IsReady)
        AppGlobalState.HostEnvironmentDegraded = false;
    }
  }
}
