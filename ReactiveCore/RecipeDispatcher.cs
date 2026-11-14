using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ISIDA.Actions;
using ISIDA.Common;
using Velum.Configuration;
using Velum.Isida;
using SolidWorks.Interop.sldworks;
using Velum.SolidHomeostasis;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Моторный ответ Velum: рецепт исполняется только при активном G_AD от рефлекса
  /// (<see cref="AppGlobalState.ActiveAdaptiveActions"/>). Повтор на следующих пульсах того же эпизода
  /// активации, пока G_AD не снято и рецепт ещё не выполнен успешно.
  /// </summary>
  public static class RecipeDispatcher
  {
    private static readonly ISolidWorksSessionProbe DefaultProbe = new VelumSolidWorksSessionProbe();

    /// <summary>Сброс состояния dispatch (при остановке пульсации).</summary>
    public static void ResetMotorDispatchState()
    {
      RecipeExecutor.ResetExecutionState();
      HideWaitingIndicator();
    }

    /// <summary>
    /// Обрабатывает моторный ответ движка на такте пульса (для тестов и явного вызова).
    /// </summary>
    public static void ProcessPulse(int pulseNumber)
    {
      int pulse = ResolveDispatchPulseNumber(pulseNumber);
      if (pulse <= 0)
        return;

      RecipeExecutor.OnPulseCompleted(pulse);

      if (!CanAttemptMotorDispatch(pulse, out _))
        return;

      IList<AdaptiveActionsSystem.AdaptiveAction> actions = SnapshotActiveAdaptiveActions();
      foreach (AdaptiveActionsSystem.AdaptiveAction action in actions)
      {
        if (action == null || action.Id <= 0)
          continue;

        int activationPulse = action.ActivationPulse;
        if (activationPulse <= 0 || activationPulse > pulse)
          continue;

        TryDispatchActiveAdaptiveAction(action, pulse);
      }
    }

    private static bool TryDispatchActiveAdaptiveAction(
        AdaptiveActionsSystem.AdaptiveAction action,
        int pulse)
    {
      int adaptiveActionId = action.Id;
      int activationPulse = action.ActivationPulse;

      // Эхо-комментарий стадии 3: действие лежит в моторе только ради рецепта (для будущего сдвига).
      // При первичном попугайском запуске рецепт выполнять нельзя — иначе команда уйдёт в SolidWorks
      // повторно (задвоение). Пропускаем диспатч до поиска рецепта; фраза-комментарий рисуется отдельно.
      //
      // Почему точечный флаг источника, а не глобальный «запрет двух одинаковых моторов/команд подряд»
      // (эвристика — не заменять на трекер повторов, см. регресс-тест EchoCommentaryCommandChannelRegressionTests):
      //  1) задвоение рождается не из пары dispatch→dispatch: первый «Сохранить» — действие оператора в самом
      //     SolidWorks, в диспатчер он не приходит; в диспатчер попадает только один эхо-вызов, «повтора подряд» нет;
      //  2) мотор намеренно повторяет одно действие несколько пульсов, пока G_AD активно и рецепт не выполнен
      //     (см. шапку класса) — глобальный антиповтор сломал бы этот штатный retry;
      //  3) против спама уже есть трекеры состояния: RecipeDispatchCooldownTracker (кулдаун по action+recipe+doc)
      //     и RecipeDispatchEpisodeTracker (один успех на эпизод + рефрактерность) — они работают по состоянию,
      //     а не по хрупкому признаку «подряд».
      if (action.ActivationSource == AdaptiveActionsSystem.ActionActivationSource.AutomatizmEchoCommentary)
      {
        Logger.Info(
            "Velum motor skipped (echo commentary): action=" + adaptiveActionId.ToString(CultureInfo.InvariantCulture));
        return false;
      }

      RecipeDefinition recipe = RecipeCatalog.FindByAdaptiveActionId(adaptiveActionId);
      if (recipe == null)
      {
        Logger.Warning(
            "Velum motor: рецепт для G_AD=" + adaptiveActionId.ToString(CultureInfo.InvariantCulture) +
            " не найден в каталоге (EnvironmentRecipes.yaml)");
        return false;
      }

      if (!recipe.ReactiveEligible)
      {
        Logger.Info(
            "Velum motor skipped (not reactive_eligible): " + recipe.RecipeId +
            " action=" + adaptiveActionId.ToString(CultureInfo.InvariantCulture));
        return false;
      }

      string docKey = ResolveDocumentKey();
      string episodeKey = BuildEpisodeKey(adaptiveActionId, activationPulse, docKey);
      if (RecipeDispatchEpisodeTracker.WasSuccessfullyDispatched(episodeKey))
        return false;

      string cooldownKey = BuildCooldownKey(adaptiveActionId, recipe.RecipeId, docKey);
      int cooldownPulses = VelumAppConfig.RecipeDispatchCooldownPulses;
      if (!RecipeDispatchCooldownTracker.TryAllowDispatch(cooldownKey, pulse, cooldownPulses))
      {
        Logger.Info(
            "Velum motor cooldown: " + recipe.RecipeId +
            " action=" + adaptiveActionId.ToString(CultureInfo.InvariantCulture) +
            " activationPulse=" + activationPulse.ToString(CultureInfo.InvariantCulture) +
            " pulse=" + pulse.ToString(CultureInfo.InvariantCulture) +
            " doc=" + docKey);
        return false;
      }

      if (!RecipeExecutor.TryExecute(recipe, DefaultProbe, action.ActivationSource, out RecipeExecutionResult result))
      {
        LogDispatchFailure(recipe, adaptiveActionId, pulse, activationPulse, result);
        RecipeMotorOutcomeNotifier.TryNotify(recipe, result);
        // Оценка шага цепочки рефлексов: провал мотор-действия → false (цепочка остановится).
        VelumChainStepOutcomeReporter.Report(
            result?.Steps, false, recipe.RecipeId);

        // Мотор провалился: без уведомления ISIDA окно ожидания висело бы до таймаута,
        // не отличаясь от «оператор ещё думает». Закрываем результат как невыполненный —
        // либо, если часть шагов успела отработать, даём среде шанс отреагировать до дедлайна.
        TryNotifyIsidaMotorCompleted(
            adaptiveActionId,
            recipe.DisplayName ?? action.Name,
            motorSkipped: !HasPerformedStep(result));
        return false;
      }

      RecipeDispatchCooldownTracker.RegisterDispatch(cooldownKey, pulse);
      RecipeDispatchEpisodeTracker.MarkSuccessfullyDispatched(episodeKey);
      Logger.Info(
          "Velum motor OK: " + recipe.RecipeId +
          " action=" + adaptiveActionId.ToString(CultureInfo.InvariantCulture) +
          " activationPulse=" + activationPulse.ToString(CultureInfo.InvariantCulture) +
          " pulse=" + pulse.ToString(CultureInfo.InvariantCulture) +
          " doc=" + docKey);
      RecipeMotorOutcomeNotifier.TryNotify(recipe, result);

      // Оценка шага цепочки рефлексов: фактический исход действия (для диалогов —
      // подтверждение оператора), а не только «рецепт отработал без исключения».
      VelumChainStepOutcomeReporter.Report(
          result?.Steps, result != null && result.Success, recipe.RecipeId);

      // Сообщить ISIDA: мотор завершён. Окно ожидания отсчитывается от запуска автоматизма и
      // обслуживает и ответ оператора, и отклик среды; здесь фиксируется лишь факт завершения мотора.
      // Ни один шаг фактически не выполнен (диалог закрыт «Отменой», все шаги пропущены) →
      // действия не было, оценивать по среде нечего.
      TryNotifyIsidaMotorCompleted(
          adaptiveActionId,
          recipe.DisplayName ?? action.Name,
          motorSkipped: !HasPerformedStep(result));

      return true;
    }

    private static IList<AdaptiveActionsSystem.AdaptiveAction> SnapshotActiveAdaptiveActions()
    {
      try
      {
        IEnumerable<AdaptiveActionsSystem.AdaptiveAction> live = AppGlobalState.ActiveAdaptiveActions;
        if (live == null)
          return Array.Empty<AdaptiveActionsSystem.AdaptiveAction>();
        return live.ToList();
      }
      catch
      {
        return Array.Empty<AdaptiveActionsSystem.AdaptiveAction>();
      }
    }

    private static void LogDispatchFailure(
        RecipeDefinition recipe,
        int adaptiveActionId,
        int pulse,
        int activationPulse,
        RecipeExecutionResult result)
    {
      string detail = string.Empty;
      if (result != null)
      {
        if (!string.IsNullOrWhiteSpace(result.DenyReason))
          detail = result.DenyReason;
        else if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
          detail = result.ErrorMessage;
      }

      if (string.IsNullOrWhiteSpace(detail))
        detail = "execution_failed";

      Logger.Warning(
          "Velum motor FAIL: " + (recipe?.RecipeId ?? "?") +
          " action=" + adaptiveActionId.ToString(CultureInfo.InvariantCulture) +
          " activationPulse=" + activationPulse.ToString(CultureInfo.InvariantCulture) +
          " pulse=" + pulse.ToString(CultureInfo.InvariantCulture) +
          " reason=" + detail);
    }

    private static bool CanAttemptMotorDispatch(int pulse, out string blockReason)
    {
      blockReason = string.Empty;
      if (!VelumAppConfig.RecipeDispatchEnabled)
      {
        blockReason = "recipe_dispatch_disabled";
        return false;
      }

      if (!GlobalTimer.IsPulsationRunning || !VelumIsidaHost.IsReady)
      {
        blockReason = "pulse_or_isida_not_ready";
        return false;
      }

      if (AppGlobalState.IsDead)
      {
        blockReason = "agent_dead";
        return false;
      }

      if (AppGlobalState.HostEnvironmentDegraded)
      {
        blockReason = "host_environment_degraded";
        return false;
      }

      if (VelumSolidEnvironmentBridge.TryGetSolidWorksApplication() == null)
      {
        blockReason = "solidworks_session_unavailable";
        return false;
      }

      if (pulse <= 0)
      {
        blockReason = "invalid_pulse";
        return false;
      }

      return true;
    }

    private static int ResolveDispatchPulseNumber(int pulseNumber)
    {
      try
      {
        if (GlobalTimer.GlobalPulsCount > 0)
          return GlobalTimer.GlobalPulsCount;
      }
      catch
      {
      }

      return pulseNumber > 0 ? pulseNumber : 0;
    }

    private static string BuildCooldownKey(int adaptiveActionId, string recipeId, string docKey)
    {
      return "adaptive_action:" + adaptiveActionId.ToString(CultureInfo.InvariantCulture) + "|" +
             (recipeId ?? string.Empty).Trim() + "|" + (docKey ?? "no_doc");
    }

    /// <summary>Один успешный dispatch на эпизод: G_AD + пульс активации рефлекса + документ.</summary>
    private static string BuildEpisodeKey(int adaptiveActionId, int activationPulse, string docKey)
    {
      return adaptiveActionId.ToString(CultureInfo.InvariantCulture) + "|" +
             activationPulse.ToString(CultureInfo.InvariantCulture) + "|" +
             (docKey ?? "no_doc");
    }

    private static string ResolveDocumentKey()
    {
      try
      {
        var swApp = VelumSolidEnvironmentBridge.TryGetSolidWorksApplication();
        ModelDoc2 modelDoc = swApp?.Sw?.IActiveDoc2 as ModelDoc2;
        return VelumSolidWorksModelDocHelper.TryGetDispatchDocumentKey(modelDoc);
      }
      catch
      {
        return "no_doc";
      }
    }

    /// <summary>
    /// Сообщить ISIDA о завершении мотора: зафиксировать факт завершения и показать индикатор ожидания.
    /// Отсчёт окна ведёт движок от запуска автоматизма; оценка — по дельте состояния (ответ среды).
    /// </summary>
    private static void TryNotifyIsidaMotorCompleted(int adaptiveActionId, string actionLabel, bool motorSkipped)
    {
      try
      {
        if (AdaptiveActionsSystem.IsInitialized)
        {
          AdaptiveActionsSystem.Instance.NotifyMotorCompleted(adaptiveActionId, motorSkipped);
          Logger.Info(
              "Velum motor notified ISIDA: action=" +
              adaptiveActionId.ToString(CultureInfo.InvariantCulture) +
              " skipped=" + motorSkipped +
              " — waiting for environment response");
        }

        ShowWaitingIndicator(actionLabel);
      }
      catch (Exception ex)
      {
        Logger.Error("TryNotifyIsidaMotorCompleted: " + ex.Message);
      }
    }

    /// <summary>
    /// Был ли в рецепте хотя бы один фактически выполненный шаг (действие в среде состоялось).
    /// Диалоговый шаг считается выполненным только при подтверждении оператором
    /// (<see cref="RecipeStepExecutionResult.Confirmed"/> = <c>true</c>); «Отмена»/закрытие окна —
    /// действия не было. Пропущенные по политике шаги действием не считаются.
    /// </summary>
    /// <returns><c>true</c>, если действия в среде были; <c>false</c> — рецепт ничего не выполнил.</returns>
    private static bool HasPerformedStep(RecipeExecutionResult result)
    {
      IReadOnlyList<RecipeStepExecutionResult> steps = result?.Steps;
      if (steps == null)
        return false;

      for (int i = 0; i < steps.Count; i++)
      {
        RecipeStepExecutionResult step = steps[i];
        if (step == null || step.Skipped)
          continue;

        // Диалог без подтверждения оператора — шаг обработан формально, но действия не было.
        if (step.Confirmed == false)
          continue;

        if (step.Success)
          return true;
      }

      return false;
    }

    /// <summary>
    /// Показать индикатор ожидания в шапке панели задач (обратный отсчёт пульсов).
    /// Вызывается на UI-потоке после завершения мотора.
    /// </summary>
    private static void ShowWaitingIndicator(string actionLabel)
    {
      VelumSolidEnvironmentBridge.RunOnTaskPaneUiThread(() =>
      {
        try
        {
          HideWaitingIndicator();

          if (VelumSolidEnvironmentBridge.TryGetTaskPane() is Velum.UI.VelumAgentTaskPane taskPane)
          {
            taskPane.ShowCountdown(
                string.IsNullOrWhiteSpace(actionLabel) ? "Action" : actionLabel);
          }
        }
        catch (Exception ex)
        {
          Logger.Warning("ShowWaitingIndicator: " + ex.Message);
        }
      });
    }

    /// <summary>
    /// Скрыть индикатор ожидания в шапке панели (при смене документа, остановке пульсации и т.п.).
    /// </summary>
    internal static void HideWaitingIndicator()
    {
      VelumSolidEnvironmentBridge.RunOnTaskPaneUiThread(() =>
      {
        try
        {
          if (VelumSolidEnvironmentBridge.TryGetTaskPane() is Velum.UI.VelumAgentTaskPane taskPane)
          {
            taskPane.HideCountdown();
          }
        }
        catch (Exception ex)
        {
          Logger.Warning("HideWaitingIndicator: " + ex.Message);
        }
      });
    }

  }

}
