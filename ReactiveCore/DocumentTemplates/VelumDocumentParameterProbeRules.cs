using System;
using System.Collections.Generic;
using ISIDA.SymbiontEnv.Contract;

namespace Velum.ReactiveCore.DocumentTemplates
{
  /// <summary>
  /// Чистые правила метрики группы параметров документа: зоны <c>good</c>/<c>warn</c>/<c>bad</c>
  /// для одного значения и свёртка группы по режиму <c>all</c>/<c>any</c>. Шкала пробы — 0…100,
  /// пороги «плохо/хорошо» совпадают с <see cref="MetricProbeThresholds"/>.
  /// </summary>
  internal static class VelumDocumentParameterProbeRules
  {
    /// <summary>Верх шкалы (значение в допуске).</summary>
    public const float GoodScore = 100f;

    /// <summary>Нижняя граница зоны <c>warn</c> / верхняя граница зоны <c>bad</c>.</summary>
    public const float WarnBadBoundaryScore = 50f;

    /// <summary>Зона расположения значения относительно допуска.</summary>
    internal enum Zone
    {
      /// <summary>Значение в допуске — <c>good</c>.</summary>
      Good,

      /// <summary>Значение вышло за допуск в пределах полосы <c>warn</c>.</summary>
      Warn,

      /// <summary>Значение далеко за допуском — <c>bad</c>.</summary>
      Bad
    }

    /// <summary>
    /// Определяет зону значения относительно допуска <c>[min; max]</c>. Полоса <c>warn</c>
    /// шириной <paramref name="warnBand"/> отграничивает «чуть вышел» от «далеко вышел».
    /// Если допуск не задан (оба конца null), значение считается <see cref="Zone.Good"/>.
    /// </summary>
    /// <param name="value">Фактическое значение параметра.</param>
    /// <param name="min">Нижняя граница допуска (null — не проверять).</param>
    /// <param name="max">Верхняя граница допуска (null — не проверять).</param>
    /// <param name="warnBand">Ширина полосы <c>warn</c> за границей допуска (&gt; 0).</param>
    /// <returns>Зона значения.</returns>
    internal static Zone Classify(double value, double? min, double? max, double warnBand)
    {
      double excess = Excess(value, min, max);
      if (excess <= 0d)
        return Zone.Good;

      double band = warnBand > 0d ? warnBand : double.PositiveInfinity;
      return excess <= band ? Zone.Warn : Zone.Bad;
    }

    /// <summary>
    /// Оценивает одно значение параметра в шкалу 0…100:
    /// в допуске → 100; в полосе <c>warn</c> → линейно 100…50; за полосой → 50…0.
    /// </summary>
    /// <param name="value">Фактическое значение параметра.</param>
    /// <param name="min">Нижняя граница допуска (null — не проверять).</param>
    /// <param name="max">Верхняя граница допуска (null — не проверять).</param>
    /// <param name="warnBand">Ширина полосы <c>warn</c> за границей допуска (&gt; 0).</param>
    /// <returns>Оценка 0…100.</returns>
    internal static float ScoreValue(double value, double? min, double? max, double warnBand)
    {
      double excess = Excess(value, min, max);
      if (excess <= 0d)
        return GoodScore;

      double band = warnBand > 0d ? warnBand : 1d;

      if (excess <= band)
      {
        double ratio = excess / band;
        return (float)(GoodScore - (GoodScore - WarnBadBoundaryScore) * ratio);
      }

      double over = excess - band;
      double badRatio = over / band;
      if (badRatio >= 1d)
        return 0f;

      return (float)(WarnBadBoundaryScore * (1d - badRatio));
    }

    /// <summary>
    /// Сворачивает оценки параметров группы по режиму: <c>all</c> — минимальная
    /// (группа в норме, только если все параметры в допуске); <c>any</c> — максимальная
    /// (группа в норме, если хотя бы один параметр в допуске). Пустая группа → 100.
    /// </summary>
    /// <param name="scores">Оценки параметров (0…100).</param>
    /// <param name="mode">Режим группы: <c>all</c> или <c>any</c>.</param>
    /// <returns>Итоговая оценка группы 0…100.</returns>
    internal static float ScoreGroup(IReadOnlyList<float> scores, string mode)
    {
      if (scores == null || scores.Count == 0)
        return GoodScore;

      bool any = string.Equals((mode ?? string.Empty).Trim(), "any", StringComparison.OrdinalIgnoreCase);

      float result = any ? float.MinValue : float.MaxValue;
      foreach (float s in scores)
      {
        if (any)
        {
          if (s > result)
            result = s;
        }
        else
        {
          if (s < result)
            result = s;
        }
      }

      if (result == float.MinValue || result == float.MaxValue)
        return GoodScore;

      return result;
    }

    /// <summary>
    /// Величина выхода значения за границы допуска (0 — внутри допуска).
    /// Учитывает только заданные концы интервала.
    /// </summary>
    private static double Excess(double value, double? min, double? max)
    {
      double excess = 0d;

      if (min.HasValue && value < min.Value)
        excess = Math.Max(excess, min.Value - value);

      if (max.HasValue && value > max.Value)
        excess = Math.Max(excess, value - max.Value);

      return excess;
    }
  }
}
