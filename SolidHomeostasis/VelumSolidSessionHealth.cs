using System;
using System.Collections.Generic;
using Xarial.XCad;

namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Оценка доступности CAD-сессии SolidWorks (SessionHealth, п. 2 плана устойчивости).
  /// Используется для <see cref="VelumCadDegradedModeSync"/> и UI; в гомеостаз не пишется.
  /// </summary>
  public static class VelumSolidSessionHealth
  {
    /// <summary>SW доступен, последний опрос в бюджете успешен.</summary>
    public const float ScoreHealthy = 100f;

    /// <summary>COM-ошибка при опросе (модель/документ недоступны).</summary>
    public const float ScoreComError = 70f;

    /// <summary>Таймаут опроса или устаревший снимок без нового COM.</summary>
    public const float ScoreDegraded = 30f;

    /// <summary>Процесс SW недоступен / сессия снята.</summary>
    public const float ScoreUnavailable = 0f;

    /// <summary>
    /// Вычисляет значение SessionHealth по метаданным gate и состоянию приложения SW.
    /// </summary>
    /// <param name="app">Сессия XCad; null — недоступна.</param>
    /// <returns>0…100.</returns>
    public static float ComputeScore(IXApplication app)
    {
      if (app == null || !app.IsAlive)
        return ScoreUnavailable;

      if (VelumSolidEnvironmentGate.LastSnapshotTimedOut || VelumSolidEnvironmentGate.LastSnapshotIsStale)
      {
        // Таймаут последнего COM не означает отсутствие снимка: gate мог остаться от успешного опроса.
        // ScoreComError (70) — ниже «здоровья», но выше порога CAD-degraded (50): мотор и давление допустимы.
        IReadOnlyDictionary<string, float> snap = VelumSolidEnvironmentGate.GetPublishedSnapshot();
        if (snap != null && snap.Count > 0 && !VelumSolidEnvironmentGate.LastSnapshotIsStale)
          return ScoreComError;

        return ScoreDegraded;
      }

      VelumSolidProbeErrorKind kind = VelumSolidEnvironmentGate.LastErrorKind;
      switch (kind)
      {
        case VelumSolidProbeErrorKind.None:
          return ScoreHealthy;
        case VelumSolidProbeErrorKind.SessionUnavailable:
          return ScoreUnavailable;
        case VelumSolidProbeErrorKind.ComAccessFailed:
        case VelumSolidProbeErrorKind.PartialProbeFailure:
          return ScoreComError;
        case VelumSolidProbeErrorKind.TimedOut:
          return ScoreDegraded;
        default:
          return ScoreDegraded;
      }
    }

    /// <summary>Краткая подпись для UI панели агента.</summary>
    public static string FormatUiStatus(float score)
    {
      if (score >= ScoreHealthy - 0.5f)
        return "CAD: OK";
      if (score >= ScoreComError - 0.5f)
        return "CAD: ошибка COM";
      if (score >= ScoreDegraded - 0.5f)
        return "CAD: деградация";
      return "CAD: недоступна";
    }
  }

  /// <summary>Классификация последнего такта опроса/публикации снимка метрик SW.</summary>
  public enum VelumSolidProbeErrorKind
  {
    /// <summary>Успешный опрос в бюджете, снимок достоверен.</summary>
    None = 0,

    /// <summary><c>!app.IsAlive</c> или сессия SW снята.</summary>
    SessionUnavailable = 1,

    /// <summary>Не удалось получить активный документ или снимок проб пуст.</summary>
    ComAccessFailed = 2,

    /// <summary>Часть проб не собрана из-за исключений COM.</summary>
    PartialProbeFailure = 3,

    /// <summary>Опрос не уложился в бюджет времени (п. 1).</summary>
    TimedOut = 4
  }
}
