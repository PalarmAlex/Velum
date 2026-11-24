using System;
using System.Collections.Generic;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Реестр обработчиков шагов <c>invoke</c> (ключ handler → исполнение в среде).
  /// </summary>
  public static class RecipeStepHandlerRegistry
  {
    private delegate bool HandlerDelegate(
        RecipeStepExecutionContext context,
        IReadOnlyDictionary<string, string> args,
        out RecipeStepExecutionResult result);

    private static readonly Dictionary<string, HandlerDelegate> Handlers =
        new Dictionary<string, HandlerDelegate>(StringComparer.OrdinalIgnoreCase);

    static RecipeStepHandlerRegistry()
    {
      Register("set_custom_property", ExecuteSetCustomProperty);
      Register("save_file_name", ExecuteSaveFileNameMetadata);
      Register("assign_session_material", ExecuteAssignSessionMaterial);
      Register("run_sw_command", ExecuteRunSwCommand);
      Register("run_macro", ExecuteRunMacro);
      Register("rebuild", ExecuteRebuild);
      Register(RecipeExecutorHandlersGeometry.ExtrudePlateHandlerId, ExecuteSketchExtrudePlate);
      Register(RecipeExecutorHandlersGeometry.SketchLineHandlerId, ExecuteSketchLine);
      Register("log", ExecuteLog);
      Register("set_custom_property_if_part", ExecuteSetCustomPropertyIfPart);
      Register("set_custom_property_if_part_or_assembly", ExecuteSetCustomPropertyIfPartOrAssembly);
      Register("export_documentation_dialog", ExecuteExportDocumentationDialog);
      Register("export_drawing_pdf_dialog", ExecuteExportDrawingPdfDialog);
      Register("export_documentation_create_files", ExecuteExportDocumentationCreateFiles);
      Register("delete_pdf_artifact", ExecuteDeletePdfArtifact);
      Register("write_drawing_path", ExecuteWriteDrawingPath);
      Register("ensure_blank_size_property_links", ExecuteEnsureBlankSizePropertyLinks);
      Register(RecipeExecutorHandlersRegistry.ShowProblemsHandlerId, ExecuteProductRegistryShowProblems);
      Register(RecipeExecutorHandlersBomExport.BomExportHandlerId, ExecuteBomExchangeExport);
      Register(RecipeExecutorHandlersBomExport.BomExchangeDialogHandlerId, ExecuteBomExchangeShowDialog);
    }

    private static void Register(string handlerId, HandlerDelegate handler)
    {
      if (string.IsNullOrWhiteSpace(handlerId) || handler == null)
        return;
      Handlers[handlerId.Trim()] = handler;
    }

    /// <summary>
    /// Исполняет шаг <c>invoke</c> из flat-ключей шага (handler + argsSchema).
    /// </summary>
    public static bool TryExecuteInvoke(
        IReadOnlyDictionary<string, string> stepParameters,
        RecipeStepExecutionContext context,
        out RecipeStepExecutionResult result)
    {
      result = null;
      string handlerId = RecipeArgsParser.Get(stepParameters, "handler");
      if (string.IsNullOrWhiteSpace(handlerId))
      {
        result = new RecipeStepExecutionResult(
            context?.StepIndex ?? 0,
            "invoke",
            false,
            false,
            "missing_handler");
        return false;
      }

      HandlerDelegate handler;
      if (!Handlers.TryGetValue(handlerId.Trim(), out handler))
      {
        result = new RecipeStepExecutionResult(
            context?.StepIndex ?? 0,
            "invoke",
            false,
            false,
            "unknown_handler:" + handlerId);
        return false;
      }

      IReadOnlyDictionary<string, string> args = ExtractHandlerArgs(stepParameters);

      // Флаг confirm на шаге: при ручном запуске просим оператора подтвердить/поправить параметры.
      // Автономный прогон психики (автоматизмы, вербальные ответы, эхо) окно не показывает.
      VelumMotorParamDialogRules.ConfirmMode confirmMode =
          VelumMotorParamDialogRules.ParseConfirmMode(RecipeArgsParser.Get(stepParameters, "confirm"));
      int activationSource = context != null ? (int)context.ActivationSource : 0;
      if (context != null &&
          VelumMotorParamDialogRules.ShouldAskDialog(confirmMode, activationSource))
      {
        string title = context?.Recipe?.DisplayName;
        if (string.IsNullOrWhiteSpace(title))
          title = handlerId;

        bool confirmed;
        IReadOnlyDictionary<string, string> merged;
        VelumMotorParamsDialogHost.TryShow(
            title,
            BuildParamFields(args),
            args,
            out merged,
            out confirmed);

        if (!confirmed)
        {
          // Оператор отменил: действие не выполняем, но шаг формально обработан.
          // Confirmed=false исключает шаг из «выполненных» (HasPerformedStep) и даёт
          // цепочке рефлексов оценку «стоп» (VelumChainStepOutcomeRules.Evaluate).
          result = new RecipeStepExecutionResult(
              context?.StepIndex ?? 0,
              "invoke",
              true,
              false,
              "motor_params_cancelled",
              false);
          return true;
        }

        args = merged;
      }

      return handler(context, args, out result);
    }

    /// <summary>
    /// Строит поля диалога из фактических параметров шага: рендерятся только реально
    /// присутствующие ключи (значения из рецепта/переменных — как стартовые).
    /// Типы параметров (справочник <c>paramId→type</c>) приходят из ISIDA позже; сейчас
    /// все поля — свободный текст, необязательные (обратная совместимость с рецептами без схем).
    /// </summary>
    private static IReadOnlyList<VelumMotorParamField> BuildParamFields(
        IReadOnlyDictionary<string, string> args)
    {
      var fields = new List<VelumMotorParamField>();
      if (args == null)
        return fields;

      foreach (KeyValuePair<string, string> kv in args)
      {
        if (string.IsNullOrWhiteSpace(kv.Key))
          continue;
        fields.Add(new VelumMotorParamField(
            kv.Key,
            kv.Key,
            "text",
            kv.Value,
            required: false,
            enumValues: null));
      }

      return fields;
    }

    /// <summary>Служебные ключи шага, не являющиеся аргументами handler'а.</summary>
    private static readonly HashSet<string> NonArgKeys =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "handler", "confirm" };

    private static IReadOnlyDictionary<string, string> ExtractHandlerArgs(
        IReadOnlyDictionary<string, string> stepParameters)
    {
      var args = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      if (stepParameters == null)
        return args;

      foreach (KeyValuePair<string, string> kv in stepParameters)
      {
        if (NonArgKeys.Contains(kv.Key))
          continue;
        args[kv.Key] = kv.Value;
      }

      return args;
    }

    private static bool ExecuteSetCustomProperty(
        RecipeStepExecutionContext context,
        IReadOnlyDictionary<string, string> args,
        out RecipeStepExecutionResult result)
    {
      return RecipeExecutorHandlers.TryExecuteSetCustomProperty(
          context.StepIndex,
          args,
          context.ModelDoc,
          context.TemplateContext,
          out result);
    }

    private static bool ExecuteSaveFileNameMetadata(
        RecipeStepExecutionContext context,
        IReadOnlyDictionary<string, string> args,
        out RecipeStepExecutionResult result)
    {
      return RecipeExecutorHandlers.TryExecuteSaveFileName(
          context.StepIndex,
          args,
          context.ModelDoc,
          context.Sw,
          context.TemplateContext,
          out result);
    }

    private static bool ExecuteAssignSessionMaterial(
        RecipeStepExecutionContext context,
        IReadOnlyDictionary<string, string> args,
        out RecipeStepExecutionResult result)
    {
      return RecipeExecutorHandlers.TryExecuteAssignSessionMaterial(
          context.StepIndex,
          args,
          context.ModelDoc,
          out result);
    }

    private static bool ExecuteRunSwCommand(
        RecipeStepExecutionContext context,
        IReadOnlyDictionary<string, string> args,
        out RecipeStepExecutionResult result)
    {
      return RecipeExecutorHandlers.TryExecuteRunSwCommand(
          context.StepIndex,
          args,
          context.Sw,
          context.CommandPatternId,
          out result);
    }

    private static bool ExecuteRunMacro(
        RecipeStepExecutionContext context,
        IReadOnlyDictionary<string, string> args,
        out RecipeStepExecutionResult result)
    {
      return RecipeExecutorHandlers.TryExecuteMacro(
          context.StepIndex,
          args,
          context.Sw,
          out result);
    }

    private static bool ExecuteSketchExtrudePlate(
        RecipeStepExecutionContext context,
        IReadOnlyDictionary<string, string> args,
        out RecipeStepExecutionResult result)
    {
      return RecipeExecutorHandlersGeometry.TryExecuteExtrudePlate(
          context.StepIndex,
          args,
          context.ModelDoc,
          context.TemplateContext,
          context.PsychicVariables,
          out result);
    }

    private static bool ExecuteSketchLine(
        RecipeStepExecutionContext context,
        IReadOnlyDictionary<string, string> args,
        out RecipeStepExecutionResult result)
    {
      return RecipeExecutorHandlersGeometry.TryExecuteLine(
          context.StepIndex,
          args,
          context.ModelDoc,
          context.TemplateContext,
          context.PsychicVariables,
          out result);
    }

    private static bool ExecuteRebuild(
        RecipeStepExecutionContext context,
        IReadOnlyDictionary<string, string> args,
        out RecipeStepExecutionResult result)
    {
      return RecipeExecutorHandlers.TryExecuteRebuild(
          context.StepIndex,
          context.ModelDoc,
          out result);
    }

    private static bool ExecuteLog(
        RecipeStepExecutionContext context,
        IReadOnlyDictionary<string, string> args,
        out RecipeStepExecutionResult result)
    {
      return RecipeExecutorHandlers.TryExecuteLog(
          context.StepIndex,
          args,
          context.Recipe,
          context.TemplateContext,
          out result);
    }

    private static bool ExecuteSetCustomPropertyIfPart(
        RecipeStepExecutionContext context,
        IReadOnlyDictionary<string, string> args,
        out RecipeStepExecutionResult result)
    {
      return RecipeExecutorHandlersExport.TryExecuteSetCustomPropertyIfPart(
          context.StepIndex,
          args,
          context.ModelDoc,
          context.TemplateContext,
          out result);
    }

    private static bool ExecuteSetCustomPropertyIfPartOrAssembly(
        RecipeStepExecutionContext context,
        IReadOnlyDictionary<string, string> args,
        out RecipeStepExecutionResult result)
    {
      return RecipeExecutorHandlersExport.TryExecuteSetCustomPropertyIfPartOrAssembly(
          context.StepIndex,
          args,
          context.ModelDoc,
          context.TemplateContext,
          out result);
    }

    private static bool ExecuteExportDocumentationDialog(
        RecipeStepExecutionContext context,
        IReadOnlyDictionary<string, string> args,
        out RecipeStepExecutionResult result)
    {
      return RecipeExecutorHandlersExport.TryExecuteExportDocumentationDialog(
          context.StepIndex,
          context.ModelDoc,
          context.FromConditionedReflex,
          out result);
    }

    private static bool ExecuteExportDrawingPdfDialog(
        RecipeStepExecutionContext context,
        IReadOnlyDictionary<string, string> args,
        out RecipeStepExecutionResult result)
    {
      return RecipeExecutorHandlersExport.TryExecuteExportDrawingPdfDialog(
          context.StepIndex,
          context.ModelDoc,
          context.FromConditionedReflex,
          out result);
    }

    private static bool ExecuteExportDocumentationCreateFiles(
        RecipeStepExecutionContext context,
        IReadOnlyDictionary<string, string> args,
        out RecipeStepExecutionResult result)
    {
      return RecipeExecutorHandlersExport.TryExecuteExportDocumentationCreateFiles(
          context.StepIndex,
          context.ModelDoc,
          out result);
    }

    private static bool ExecuteDeletePdfArtifact(
        RecipeStepExecutionContext context,
        IReadOnlyDictionary<string, string> args,
        out RecipeStepExecutionResult result)
    {
      return RecipeExecutorHandlersExport.TryExecuteDeletePdfArtifact(
          context.StepIndex,
          context.ModelDoc,
          out result);
    }

    private static bool ExecuteWriteDrawingPath(
        RecipeStepExecutionContext context,
        IReadOnlyDictionary<string, string> args,
        out RecipeStepExecutionResult result)
    {
      return RecipeExecutorHandlersExport.TryExecuteWriteDrawingPath(
          context.StepIndex,
          args,
          context.ModelDoc,
          context.Sw,
          context.TemplateContext,
          out result);
    }

    private static bool ExecuteEnsureBlankSizePropertyLinks(
        RecipeStepExecutionContext context,
        IReadOnlyDictionary<string, string> args,
        out RecipeStepExecutionResult result)
    {
      return RecipeExecutorHandlersBlankSize.TryExecuteEnsureBlankSizePropertyLinks(
          context.StepIndex,
          context.ModelDoc,
          args,
          out result);
    }

    private static bool ExecuteProductRegistryShowProblems(
        RecipeStepExecutionContext context,
        IReadOnlyDictionary<string, string> args,
        out RecipeStepExecutionResult result)
    {
      return RecipeExecutorHandlersRegistry.TryExecuteShowProblems(
          context.StepIndex,
          context.ModelDoc,
          out result);
    }

    private static bool ExecuteBomExchangeExport(
        RecipeStepExecutionContext context,
        IReadOnlyDictionary<string, string> args,
        out RecipeStepExecutionResult result)
    {
      return RecipeExecutorHandlersBomExport.TryExecuteBomExchangeExport(
          context.StepIndex,
          args,
          out result);
    }

    private static bool ExecuteBomExchangeShowDialog(
        RecipeStepExecutionContext context,
        IReadOnlyDictionary<string, string> args,
        out RecipeStepExecutionResult result)
    {
      return RecipeExecutorHandlersBomExport.TryExecuteBomExchangeShowDialog(
          context.StepIndex,
          out result);
    }

    /// <summary>true, если все invoke-шаги допускают отсутствие активного ModelDoc.</summary>
    internal static bool AllowsNullModelDoc(IReadOnlyList<RecipeStepDefinition> steps)
    {
      if (steps == null || steps.Count == 0)
        return false;

      for (int i = 0; i < steps.Count; i++)
      {
        RecipeStepDefinition step = steps[i];
        if (step == null)
          continue;
        string type = (step.Type ?? string.Empty).Trim();
        if (!string.Equals(type, "invoke", StringComparison.OrdinalIgnoreCase))
          return false;

        string handler = null;
        if (step.Parameters != null)
          step.Parameters.TryGetValue("handler", out handler);
        handler = (handler ?? string.Empty).Trim();
        if (string.Equals(handler, RecipeExecutorHandlersRegistry.ShowProblemsHandlerId, StringComparison.OrdinalIgnoreCase))
          continue;
        if (string.Equals(handler, RecipeExecutorHandlersBomExport.BomExchangeDialogHandlerId, StringComparison.OrdinalIgnoreCase))
          continue;
        return false;
      }

      return true;
    }
  }
}
