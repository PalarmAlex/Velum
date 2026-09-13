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

      // Сообщить ISIDA: мотор завершён — переснять «до»-снимок и перезапустить таймер ожидания.
      // Оценка автоматизма будет по дельте состояния (ответ среды), а не по кнопкам формы.
      TryNotifyIsidaMotorCompleted(adaptiveActionId, recipe.DisplayName ?? action.Name);

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
    /// Сообщить ISIDA о завершении мотора: переснимок «до»-состояния и перезапуск таймера ожидания.
    /// Оценка автоматизма — по дельте состояния после истечения таймера (ответ среды).
    /// Также показывает форму-индикатор ожидания.
    /// </summary>
    private static void TryNotifyIsidaMotorCompleted(int adaptiveActionId, string actionLabel)
    {
      try
      {
        if (AdaptiveActionsSystem.IsInitialized)
        {
          AdaptiveActionsSystem.Instance.NotifyMotorCompleted(adaptiveActionId);
          Logger.Info(
              "Velum motor notified ISIDA: action=" +
              adaptiveActionId.ToString(CultureInfo.InvariantCulture) +
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
