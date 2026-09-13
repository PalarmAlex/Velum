using System;
using System.Collections.Generic;
using System.Globalization;
using ISIDA.Common;
using SolidWorks.Interop.sldworks;
using Velum.SolidHomeostasis;
using Xarial.XCad;
using Xarial.XCad.Documents;
using Xarial.XCad.SolidWorks;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Исполнение рецептов Velum на UI-потоке панели задач (COM SolidWorks).
  /// </summary>
  public static class RecipeExecutor
  {
    private static readonly object ExecutionSync = new object();
    private static readonly ISolidWorksSessionProbe DefaultProbe = new VelumSolidWorksSessionProbe();
    private static int _activeAdaptiveActionId;
    private static bool _cancelRequested;

    /// <summary>Сбрасывает состояние исполнения (при остановке пульсации).</summary>
    public static void ResetExecutionState()
    {
      lock (ExecutionSync)
      {
        _activeAdaptiveActionId = 0;
        _cancelRequested = false;
      }
    }

    /// <summary>
    /// Проверяет снятие G_AD на <see cref="GlobalTimer.OnPulseCompleted"/> во время исполнения рецепта.
    /// </summary>
    public static void OnPulseCompleted(int pulseNumber)
    {
      lock (ExecutionSync)
      {
        if (_activeAdaptiveActionId <= 0)
          return;

        if (!IsAdaptiveActionActive(_activeAdaptiveActionId))
          _cancelRequested = true;
      }
    }

    /// <summary>
    /// Исполняет рецепт по <c>recipe_id</c> из <see cref="RecipeCatalog"/>.
    /// </summary>
    public static bool TryExecuteByRecipeId(string recipeId, out RecipeExecutionResult result)
    {
      result = null;
      RecipeDefinition recipe = RecipeCatalog.FindByRecipeId(recipeId);
      if (recipe == null)
      {
        result = FailedResult(null, SolidWorksSessionSnapshot.Empty, "recipe_not_found:" + recipeId, null);
        return false;
      }

      return TryExecute(recipe, out result);
    }

    /// <summary>
    /// Исполняет рецепт на UI-потоке SolidWorks.
    /// </summary>
    public static bool TryExecute(RecipeDefinition recipe, out RecipeExecutionResult result)
    {
      return TryExecute(
          recipe,
          DefaultProbe,
          ISIDA.Actions.AdaptiveActionsSystem.ActionActivationSource.GeneticReflex,
          out result);
    }

    /// <summary>
    /// Исполняет рецепт с указанным зондом сессии.
    /// </summary>
    public static bool TryExecute(
        RecipeDefinition recipe,
        ISolidWorksSessionProbe probe,
        out RecipeExecutionResult result)
    {
      return TryExecute(
          recipe,
          probe,
          ISIDA.Actions.AdaptiveActionsSystem.ActionActivationSource.GeneticReflex,
          out result);
    }

    /// <summary>
    /// Исполняет рецепт с указанным зондом сессии и источником активации действия.
    /// </summary>
    /// <param name="recipe">Исполняемый рецепт.</param>
    /// <param name="probe">Зонд сессии SolidWorks.</param>
    /// <param name="activationSource">
    /// Источник активации (безусловный/условный рефлекс, автоматизм): влияет на то,
    /// показывать ли пользовательские предупреждения при несовпадении типа документа.
    /// </param>
    /// <param name="result">Результат исполнения.</param>
    public static bool TryExecute(
        RecipeDefinition recipe,
        ISolidWorksSessionProbe probe,
        ISIDA.Actions.AdaptiveActionsSystem.ActionActivationSource activationSource,
        out RecipeExecutionResult result)
    {
      result = null;
      if (recipe == null)
      {
        result = FailedResult(null, SolidWorksSessionSnapshot.Empty, "recipe_null", null);
        return false;
      }

      if (probe == null)
        probe = DefaultProbe;

      ISwApplication swApp = VelumSolidEnvironmentBridge.TryGetSolidWorksApplication();
      if (swApp == null)
      {
        result = FailedResult(
            recipe,
            SolidWorksSessionSnapshot.Empty,
            "solidworks_session_unavailable",
            null);
        return false;
      }

      if (recipe.Steps == null || recipe.Steps.Count == 0)
        Logger.Warning("Velum Recipe execute: steps=0 for id=" + recipe.RecipeId);

      BeginExecution(recipe.AdaptiveActionId);

      var stepResults = new List<RecipeStepExecutionResult>();
      string fatalError = null;
      SolidWorksSessionSnapshot executedSnapshot = SolidWorksSessionSnapshot.Empty;
      bool success = false;
      bool invoked = false;
      Exception invokeEx = null;

      try
      {
        VelumSolidEnvironmentBridge.RunOnTaskPaneUiThread(() =>
        {
          invoked = true;
          try
          {
            executedSnapshot = probe.Capture(swApp);
            ModelDoc2 modelDoc = TryGetActiveModelDoc(swApp);
            bool allowNullModel = RecipeStepHandlerRegistry.AllowsNullModelDoc(recipe.Steps);
            if (modelDoc == null && !allowNullModel)
            {
              fatalError = "no_active_model_doc";
              return;
            }

            SldWorks sw = swApp.Sw as SldWorks;
            IReadOnlyDictionary<string, string> templateContext =
                VelumRecipeTemplateResolver.BuildContext(executedSnapshot, modelDoc);

            IReadOnlyList<RecipeStepDefinition> steps = recipe.Steps;
            for (int i = 0; i < steps.Count; i++)
            {
              if (ShouldStopExecution(recipe.AdaptiveActionId))
              {
                fatalError = "adaptive_action_deactivated";
                return;
              }

              RecipeStepDefinition step = steps[i];
              var context = new RecipeStepExecutionContext(
                  i,
                  recipe,
                  modelDoc,
                  sw,
                  templateContext,
                  activationSource);

              if (!TryExecuteStep(step, context, out RecipeStepExecutionResult stepResult))
              {
                stepResults.Add(stepResult);
                fatalError = stepResult.Message;
                return;
              }

              stepResults.Add(stepResult);
            }

            success = true;
          }
          catch (Exception ex)
          {
            invokeEx = ex;
          }
        });
      }
      finally
      {
        EndExecution();
      }

      if (!invoked)
      {
        result = FailedResult(
            recipe,
            executedSnapshot,
            "no_ui_thread_for_solidworks",
            stepResults);
        LogRecipeOutcome(recipe, false, result.ErrorMessage, stepResults);
        return false;
      }

      if (invokeEx != null)
      {
        result = FailedResult(recipe, executedSnapshot, invokeEx.Message, stepResults);
        LogRecipeOutcome(recipe, false, result.ErrorMessage, stepResults);
        return false;
      }

      if (!success)
      {
        result = FailedResult(
            recipe,
            executedSnapshot,
            fatalError ?? "execution_failed",
            stepResults);
        LogRecipeOutcome(recipe, false, result.ErrorMessage, stepResults);
        return false;
      }

      result = new RecipeExecutionResult(true, string.Empty, string.Empty, executedSnapshot, stepResults);
      LogRecipeOutcome(recipe, true, string.Empty, stepResults);
      return true;
    }

    private static void BeginExecution(int adaptiveActionId)
    {
      lock (ExecutionSync)
      {
        _activeAdaptiveActionId = adaptiveActionId;
        _cancelRequested = false;
      }
    }

    private static void EndExecution()
    {
      lock (ExecutionSync)
      {
        _activeAdaptiveActionId = 0;
        _cancelRequested = false;
      }
    }

    private static bool ShouldStopExecution(int adaptiveActionId)
    {
      lock (ExecutionSync)
      {
        if (_cancelRequested)
          return true;
      }

      if (adaptiveActionId <= 0)
        return false;

      return !IsAdaptiveActionActive(adaptiveActionId);
    }

    private static bool IsAdaptiveActionActive(int adaptiveActionId)
    {
      IList<ISIDA.Actions.AdaptiveActionsSystem.AdaptiveAction> actions = SnapshotActiveAdaptiveActions();
      for (int i = 0; i < actions.Count; i++)
      {
        ISIDA.Actions.AdaptiveActionsSystem.AdaptiveAction action = actions[i];
        if (action != null && action.Id == adaptiveActionId)
          return true;
      }

      return false;
    }

    private static IList<ISIDA.Actions.AdaptiveActionsSystem.AdaptiveAction> SnapshotActiveAdaptiveActions()
    {
      try
      {
        var live = AppGlobalState.ActiveAdaptiveActions;
        if (live == null)
          return Array.Empty<ISIDA.Actions.AdaptiveActionsSystem.AdaptiveAction>();

        var copy = new List<ISIDA.Actions.AdaptiveActionsSystem.AdaptiveAction>();
        foreach (ISIDA.Actions.AdaptiveActionsSystem.AdaptiveAction action in live)
          copy.Add(action);
        return copy;
      }
      catch
      {
        return Array.Empty<ISIDA.Actions.AdaptiveActionsSystem.AdaptiveAction>();
      }
    }

    private static bool TryExecuteStep(
        RecipeStepDefinition step,
        RecipeStepExecutionContext context,
        out RecipeStepExecutionResult result)
    {
      string type = (step?.Type ?? string.Empty).Trim().ToLowerInvariant();
      IReadOnlyDictionary<string, string> p = step?.Parameters;

      switch (type)
      {
        case "comment":
          result = new RecipeStepExecutionResult(
              context.StepIndex,
              "comment",
              true,
              true,
              string.Empty);
          return true;

        case "invoke":
          return RecipeStepHandlerRegistry.TryExecuteInvoke(p, context, out result);

        default:
          result = new RecipeStepExecutionResult(
              context.StepIndex,
              type,
              false,
              false,
              "unknown_step_type:" + type);
          return false;
      }
    }

    private static ModelDoc2 TryGetActiveModelDoc(IXApplication app)
    {
      IXDocument ixDoc = null;
      try
      {
        ixDoc = app?.Documents?.Active;
      }
      catch
      {
        return null;
      }

      return VelumSolidWorksModelDocHelper.TryGetActiveModelDoc2(app, ixDoc);
    }

    private static RecipeExecutionResult FailedResult(
        RecipeDefinition recipe,
        SolidWorksSessionSnapshot snapshot,
        string error,
        List<RecipeStepExecutionResult> steps)
    {
      return new RecipeExecutionResult(
          false,
          error ?? string.Empty,
          string.Empty,
          snapshot ?? SolidWorksSessionSnapshot.Empty,
          steps ?? new List<RecipeStepExecutionResult>());
    }

    private static void LogRecipeOutcome(
        RecipeDefinition recipe,
        bool success,
        string detail,
        IReadOnlyList<RecipeStepExecutionResult> steps)
    {
      if (recipe == null)
        return;

      int defSteps = recipe.Steps?.Count ?? 0;
      int runSteps = steps?.Count ?? 0;
      string msg = "Velum Recipe " + (success ? "OK" : "FAIL") +
                   " id=" + recipe.RecipeId +
                   " action=" + recipe.AdaptiveActionId.ToString(CultureInfo.InvariantCulture) +
                   " steps_def=" + defSteps.ToString(CultureInfo.InvariantCulture) +
                   " steps_run=" + runSteps.ToString(CultureInfo.InvariantCulture);
      if (!string.IsNullOrWhiteSpace(detail))
        msg += " detail=" + detail;

      if (success)
        Logger.Info(msg);
      else
        Logger.Warning(msg);

      if (steps == null || steps.Count == 0)
        return;

      for (int i = 0; i < steps.Count; i++)
      {
        RecipeStepExecutionResult s = steps[i];
        if (s == null)
          continue;
        string stepMsg = "Velum Recipe step[" + i.ToString(CultureInfo.InvariantCulture) + "] " +
                           (s.StepType ?? string.Empty) +
                           " ok=" + s.Success.ToString() +
                           " skipped=" + s.Skipped.ToString() +
                           " msg=" + (s.Message ?? string.Empty);
        if (s.Success)
          Logger.Info(stepMsg);
        else
          Logger.Warning(stepMsg);
      }
    }
  }
}
