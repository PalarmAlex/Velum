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
      return handler(context, args, out result);
    }

    private static IReadOnlyDictionary<string, string> ExtractHandlerArgs(
        IReadOnlyDictionary<string, string> stepParameters)
    {
      var args = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      if (stepParameters == null)
        return args;

      foreach (KeyValuePair<string, string> kv in stepParameters)
      {
        if (string.Equals(kv.Key, "handler", StringComparison.OrdinalIgnoreCase))
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
