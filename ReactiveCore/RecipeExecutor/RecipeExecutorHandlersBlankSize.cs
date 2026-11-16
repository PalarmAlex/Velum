using System;
using System.Collections.Generic;
using ISIDA.Common;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Velum.SolidHomeostasis;

namespace Velum.ReactiveCore
{
  /// <summary>Handler ensure_blank_size_property_links.</summary>
  internal static class RecipeExecutorHandlersBlankSize
  {
    public static bool TryExecuteEnsureBlankSizePropertyLinks(
        int index,
        ModelDoc2 modelDoc,
        IReadOnlyDictionary<string, string> args,
        out RecipeStepExecutionResult result)
    {
      string overwrite = RecipeArgsParser.Get(args, "overwrite");
      if (string.IsNullOrWhiteSpace(overwrite))
        overwrite = "repair";

      string config = RecipeArgsParser.Get(args, "config");
      // Габарит заготовки — только вкладка конфигурации; устаревший config:document игнорируем.
      if (string.Equals(config, "document", StringComparison.OrdinalIgnoreCase))
      {
        Logger.Info("Velum ensure_blank_size_property_links: config=document ignored → all configurations");
        config = null;
      }

      VelumBlankSizePropertyLinksService.EnsureStatus status =
          VelumBlankSizePropertyLinksService.TryEnsure(modelDoc, overwrite, config, out string message);

      switch (status)
      {
        case VelumBlankSizePropertyLinksService.EnsureStatus.SkippedNotPart:
          result = new RecipeStepExecutionResult(index, "invoke", true, true, "skipped_not_part " + message);
          Logger.Info("Velum ensure_blank_size_property_links skipped (not part)");
          return true;

        case VelumBlankSizePropertyLinksService.EnsureStatus.SkippedNotSheetMetal:
          result = new RecipeStepExecutionResult(
              index,
              "invoke",
              true,
              true,
              "skipped_not_sheet_metal " + message);
          Logger.Info("Velum ensure_blank_size_property_links skipped (not sheet metal)");
          return true;

        case VelumBlankSizePropertyLinksService.EnsureStatus.Unchanged:
          result = new RecipeStepExecutionResult(index, "invoke", true, true, "unchanged " + message);
          Logger.Info("Velum ensure_blank_size_property_links unchanged");
          return true;

        case VelumBlankSizePropertyLinksService.EnsureStatus.Applied:
          result = new RecipeStepExecutionResult(index, "invoke", true, false, "applied " + message);
          Logger.Info("Velum ensure_blank_size_property_links applied: " + message);
          return true;

        default:
          result = new RecipeStepExecutionResult(index, "invoke", false, false, "failed " + message);
          Logger.Warning("Velum ensure_blank_size_property_links failed: " + message);
          return false;
      }
    }
  }
}
