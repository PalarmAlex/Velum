using System;
using System.Collections.Generic;
using System.Linq;
using ISIDA.Actions;
using ISIDA.Common;
using ISIDA.Psychic.Automatism;
using ISIDA.Reflexes;
using ISIDA.Scenarios;
using Velum.SolidHomeostasis;

namespace Velum.Isida
{
  /// <summary>
  /// Пульт оператора для прогонов сценариев в Velum: подаёт стимулы шага в движок ISIDA
  /// (воздействия EA, вербальный и командный каналы, цвет образа, оценки звеньев цепочек),
  /// а воздействия метрик среды применяет через <see cref="VelumScenarioProbeApplier"/>.
  /// Реализует <see cref="IOperatorScenarioPult"/> поверх живого <see cref="VelumIsidaHost.Context"/>.
  /// Вызывается <see cref="OperatorScenarioRunner"/> на фазе пульса «после гомеостаза, до психики».
  /// </summary>
  internal sealed class VelumScenarioPult : IOperatorScenarioPult
  {
    /// <summary>
    /// Применяет стимул шага сценария. Возвращает текст ошибки при неуспехе, <c>null</c> при успехе.
    /// </summary>
    /// <param name="actionIds">ID воздействий оператора (EA); probe-воздействия будут отделены.</param>
    /// <param name="environmentProbes">Явные воздействия метрик среды (+ давление, − отпускание).</param>
    /// <param name="phraseText">Текст вербального стимула (может быть пустым).</param>
    /// <param name="toneId">ID тона речи.</param>
    /// <param name="moodId">ID настроения.</param>
    /// <param name="commandPatternIds">ID паттернов Command-канала (порядок важен для genetic match).</param>
    /// <param name="visualColorId">Код зрительного канала образа (см. <see cref="AgentVisualColor"/>).</param>
    /// <returns>Строка ошибки либо <c>null</c>.</returns>
    public string TryApplyScenarioStimulus(
        IReadOnlyList<int> actionIds,
        IReadOnlyList<ScenarioEnvironmentProbeEntry> environmentProbes,
        string phraseText,
        int toneId,
        int moodId,
        IReadOnlyList<int> commandPatternIds,
        int visualColorId = 0)
    {
      if (!VelumIsidaHost.IsReady)
        return "ISIDA не инициализирована.";

      if (AppGlobalState.IsDead)
        return "Симбионт мёртв.";

      if (!GlobalTimer.IsPulsationRunning)
        return "Пульсация выключена.";

      var context = VelumIsidaHost.Context;
      InfluenceActionSystem influence = context.InfluenceActions;

      // Разделяем ID из строки сценария: операторские стимулы идут в ApplyMultipleInfluenceActions,
      // probe-воздействия — в отдельную фазу давления на параметры среды (как на пульте AIStudio).
      var allIds = (actionIds ?? new List<int>()).Where(id => id > 0).Distinct().ToList();
      var operatorStimulusIds = new List<int>();
      var legacyProbePressureIds = new List<int>();
      foreach (int id in allIds)
      {
        if (influence.IsEnvironmentProbeActionId(id))
          legacyProbePressureIds.Add(id);
        else
          operatorStimulusIds.Add(id);
      }

      SplitEnvironmentProbes(environmentProbes, out List<int> probePressureIds, out List<int> probeReleaseIds);
      // Legacy probe из actionIds трактуем как давление (+).
      foreach (int id in legacyProbePressureIds)
      {
        if (!probePressureIds.Contains(id))
          probePressureIds.Add(id);
      }

      int colorForStep = AgentVisualColor.IsValidCode(visualColorId)
          ? visualColorId
          : AgentVisualColor.White;

      bool hasPhrase = !string.IsNullOrWhiteSpace(phraseText);
      bool hasCommands = commandPatternIds != null && commandPatternIds.Count > 0;
      bool hasOperatorStimulus = operatorStimulusIds.Count > 0
          || hasPhrase
          || hasCommands
          || colorForStep != AgentVisualColor.White;
      bool hasProbeAction = probePressureIds.Count > 0 || probeReleaseIds.Count > 0;

      if (!hasOperatorStimulus && !hasProbeAction)
        return "Пустой шаг сценария";

      try
      {
        if (hasProbeAction)
        {
          VelumScenarioProbeApplier.ApplyExplicit(
              context.Gomeostas, influence, probePressureIds, probeReleaseIds);
        }

        if (hasOperatorStimulus)
        {
          List<int> phraseIds = new List<int>();
          if (hasPhrase)
          {
            phraseIds = context.SensorySystem.VerbalChannel.RecognizeText(
                phraseText.Trim(),
                authoritativeWrite: true);
          }

          List<int> commandIds = hasCommands
              ? commandPatternIds.Where(id => id > 0).Distinct().ToList()
              : new List<int>();

          var (success, errorMessage) = influence.ApplyMultipleInfluenceActions(
              operatorStimulusIds,
              phraseIds,
              commandIds,
              authoritativeMode: true,
              toneId: toneId,
              moodId: moodId,
              visualColorId: colorForStep);

          if (!success)
            return errorMessage ?? "Ошибка применения воздействий";

          if (AppGlobalState.IsAutomatizmChainActive && AutomatismExecutionService.IsInitialized)
            AutomatismExecutionService.Instance.ApplyStimulusEffectAndAdvanceChain();
        }

        return null;
      }
      catch (Exception ex)
      {
        Logger.Error("VelumScenarioPult.TryApplyScenarioStimulus: " + ex.Message);
        return ex.Message;
      }
    }

    /// <summary>
    /// Применяет оценку звена цепочки, заданную шагом сценария. «Нет» — no-op
    /// (цепочка прервётся по таймауту ожидания оценки).
    /// </summary>
    /// <param name="result">Оценка звена из строки сценария.</param>
    public void ApplyScenarioChainStepResult(ScenarioChainStepResult result)
    {
      if (result == ScenarioChainStepResult.None)
        return;

      try
      {
        bool success = result == ScenarioChainStepResult.Success;

        if (AppGlobalState.IsReflexChainActive && ReflexesActivator.IsInitialized)
        {
          ReflexesActivator.Instance.SetChainStepResult(success);
          return;
        }

        if (AppGlobalState.IsAutomatizmChainActive && AutomatismExecutionService.IsInitialized)
        {
          int chainId = AutomatismExecutionService.Instance.GetActiveAutomatizmChainId();
          if (chainId > 0)
            AutomatismExecutionService.Instance.SetChainStepResult(chainId, success ? 1 : -1);
        }
      }
      catch (Exception ex)
      {
        Logger.Error("VelumScenarioPult.ApplyScenarioChainStepResult: " + ex.Message);
      }
    }

    /// <summary>Разделяет записи среды на давление (+) и отпускание (−).</summary>
    private static void SplitEnvironmentProbes(
        IReadOnlyList<ScenarioEnvironmentProbeEntry> probes,
        out List<int> pressureIds,
        out List<int> releaseIds)
    {
      pressureIds = new List<int>();
      releaseIds = new List<int>();
      if (probes == null)
        return;

      foreach (ScenarioEnvironmentProbeEntry e in probes)
      {
        if (e == null || e.ActionId <= 0)
          continue;
        if (e.IsPressure)
        {
          if (!pressureIds.Contains(e.ActionId))
            pressureIds.Add(e.ActionId);
        }
        else if (!releaseIds.Contains(e.ActionId))
        {
          releaseIds.Add(e.ActionId);
        }
      }
    }
  }
}
