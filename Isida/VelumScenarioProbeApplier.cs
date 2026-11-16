using System;
using System.Collections.Generic;
using System.Linq;
using ISIDA.Actions;
using ISIDA.Common;
using ISIDA.Gomeostas;
using ISIDA.SymbiontEnv.Contract;
using Velum.SolidHomeostasis;

namespace Velum.Isida
{
  /// <summary>
  /// Явное давление (+) и отпускание (−) метрик среды по шагам сценария оператора.
  /// Эквивалент виртуального пульта AIStudio (<c>VirtualProbePressureApplier</c>), но в Velum
  /// записи идут через штатный публичный путь хоста <see cref="GomeostasSystem.HostBatchUpdateParameterValues"/>
  /// (тот же, что использует оркестратор давления метрик среды), а не через <c>internal</c> движка.
  /// </summary>
  internal static class VelumScenarioProbeApplier
  {
    /// <summary>Значение probe-метрики, гарантированно попадающее в «плохую» зону (для фазы давления).</summary>
    private const float VirtualBadMetric = 0f;

    /// <summary>
    /// Применяет явные воздействия метрик среды шага сценария.
    /// </summary>
    /// <param name="gomeostas">Система гомеостаза из контекста ISIDA.</param>
    /// <param name="influenceActions">Система воздействий (для справочника EA среды и логирования).</param>
    /// <param name="pressureActionIds">ID EA среды со знаком «+» (давление).</param>
    /// <param name="releaseActionIds">ID EA среды со знаком «−» (отпускание).</param>
    public static void ApplyExplicit(
        GomeostasSystem gomeostas,
        InfluenceActionSystem influenceActions,
        IReadOnlyList<int> pressureActionIds,
        IReadOnlyList<int> releaseActionIds)
    {
      if (gomeostas == null || influenceActions == null)
        return;

      // В режиме наблюдения воздействия среды не меняют гомеостаз (как на пульте).
      if (!AppGlobalState.ObservationMode)
      {
        List<EnvironmentProbePressureRule> rules = BuildPressureRules();
        float epsilon = MetricProbeThresholds.DefaultMetricDeltaEpsilon;
        var merged = new Dictionary<int, float>();

        List<string> pressureKeys = CollectProbeKeys(pressureActionIds);
        if (pressureKeys.Count > 0)
        {
          var probeSnapshot = pressureKeys.Distinct(StringComparer.Ordinal)
              .ToDictionary(k => k, _ => VirtualBadMetric, StringComparer.Ordinal);
          if (EnvironmentMetricPressureComposer.TryComposePressureWrites(
                  probeSnapshot,
                  rules,
                  influenceScaleByKey: null,
                  pressureKeys,
                  epsilon,
                  ids => gomeostas.HostGetParameterValues(ids),
                  out Dictionary<int, float> pressure))
          {
            MergeWrites(merged, pressure);
          }
        }

        foreach (string key in CollectProbeKeys(releaseActionIds))
        {
          EnvironmentProbePressureRule rule = rules.FirstOrDefault(r =>
              string.Equals(r.ProbeKey, key, StringComparison.Ordinal));
          if (rule == null)
            continue;

          if (EnvironmentMetricParameterRelease.TryComposeReleaseWrites(
                  key,
                  rule,
                  paramId => GetParameterState(gomeostas, paramId),
                  out Dictionary<int, float> release))
          {
            MergeWrites(merged, release);
          }
        }

        if (merged.Count > 0)
          gomeostas.HostBatchUpdateParameterValues(merged);
      }

      RecordEnvironmentProbeLog(influenceActions, pressureActionIds, releaseActionIds);
    }

    /// <summary>Фиксирует воздействия среды в текущем снимке лога (столбец «Воздействие от среды»).</summary>
    private static void RecordEnvironmentProbeLog(
        InfluenceActionSystem influenceActions,
        IReadOnlyList<int> pressureActionIds,
        IReadOnlyList<int> releaseActionIds)
    {
      if (pressureActionIds != null)
      {
        foreach (int id in pressureActionIds.Where(i => i > 0).Distinct())
        {
          int mag = influenceActions.GetInfluenceMagnitudeSum(id);
          if (mag != 0)
            AppGlobalState.RecordEnvironmentProbeAction(id, mag);
        }
      }

      if (releaseActionIds != null)
      {
        foreach (int id in releaseActionIds.Where(i => i > 0).Distinct())
        {
          int mag = influenceActions.GetInfluenceMagnitudeSum(id);
          if (mag != 0)
            AppGlobalState.RecordEnvironmentProbeAction(id, -mag);
        }
      }
    }

    /// <summary>Собирает probe-ключи по выбранным ID активных EA среды.</summary>
    private static List<string> CollectProbeKeys(IReadOnlyList<int> actionIds)
    {
      var keys = new List<string>();
      if (actionIds == null || actionIds.Count == 0)
        return keys;

      foreach (InfluenceActionSystem.GomeostasisInfluenceAction ea in
               VelumSolidEnvironmentInfluenceCatalog.GetActiveEnvironmentActions())
      {
        if (!actionIds.Contains(ea.Id))
          continue;
        string key = (ea.ProbeKey ?? string.Empty).Trim();
        if (key.Length > 0 && !keys.Contains(key))
          keys.Add(key);
      }

      return keys;
    }

    /// <summary>Строит шаблоны давления по активным EA среды (ProbeKey + influences).</summary>
    private static List<EnvironmentProbePressureRule> BuildPressureRules()
    {
      return VelumSolidEnvironmentInfluenceCatalog.GetActiveEnvironmentActions()
          .Select(a => new EnvironmentProbePressureRule
          {
            ProbeKey = (a.ProbeKey ?? string.Empty).Trim(),
            Influences = a.Influences ?? new Dictionary<int, int>()
          })
          .Where(r => r.ProbeKey.Length > 0)
          .ToList();
    }

    /// <summary>Снимок параметра для расчёта отпускания (поиск по публичному <see cref="GomeostasSystem.GetAllParameters"/>).</summary>
    private static GomeostasisParameterState GetParameterState(GomeostasSystem gomeostas, int paramId)
    {
      GomeostasSystem.ParameterData param = null;
      foreach (GomeostasSystem.ParameterData p in gomeostas.GetAllParameters())
      {
        if (p != null && p.Id == paramId)
        {
          param = p;
          break;
        }
      }
      if (param == null)
        return null;

      return new GomeostasisParameterState
      {
        Id = param.Id,
        Value = param.Value,
        Speed = param.Speed,
        NormaWell = param.NormaWell,
        CriticalMinValue = param.CriticalMinValue,
        CriticalMaxValue = param.CriticalMaxValue
      };
    }

    private static void MergeWrites(Dictionary<int, float> target, Dictionary<int, float> source)
    {
      if (source == null)
        return;
      foreach (KeyValuePair<int, float> kv in source)
        target[kv.Key] = kv.Value;
    }
  }
}
