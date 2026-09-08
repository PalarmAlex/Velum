using System;
using ISIDA.Common;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Velum.ReactiveCore;

namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Чинит ссылки габарита заготовки сразу после Save Part без ожидания рефлекса/G_AD
  /// (тот же helper, что рецепт <c>ensure_blank_size_links</c> / AA 45).
  /// </summary>
  internal static class VelumBlankSizeLinksOnSaveCoordinator
  {
    internal static void TryEnsureAfterSaveIfEligible(ModelDoc2 modelDoc, string saveFileNameHint)
    {
      if (modelDoc == null)
        return;

      try
      {
        if (modelDoc.GetType() != (int)swDocumentTypes_e.swDocPART)
          return;
      }
      catch
      {
        return;
      }

      string fullPath = ResolveSavedPath(modelDoc, saveFileNameHint);
      VelumBlankSizePropertyLinksService.EnsureStatus status =
          VelumBlankSizePropertyLinksService.EnsureStatus.Failed;
      string message = string.Empty;

      // Как рецепт ensure_blank_size_links: active + repair.
      VelumSolidEnvironmentBridge.RunOnTaskPaneUiThread(() =>
      {
        status = VelumBlankSizePropertyLinksService.TryEnsure(
            modelDoc,
            "repair",
            "active",
            out message);
      });

      switch (status)
      {
        case VelumBlankSizePropertyLinksService.EnsureStatus.SkippedNotPart:
        case VelumBlankSizePropertyLinksService.EnsureStatus.SkippedNotSheetMetal:
        case VelumBlankSizePropertyLinksService.EnsureStatus.Unchanged:
          return;

        case VelumBlankSizePropertyLinksService.EnsureStatus.Applied:
          Logger.Info(
              "Velum blank-size links after save OK path=\"" + (fullPath ?? string.Empty) +
              "\" " + message);
          return;

        default:
          Logger.Warning(
              "Velum blank-size links after save FAIL path=\"" + (fullPath ?? string.Empty) +
              "\" " + message);
          return;
      }
    }

    private static string ResolveSavedPath(ModelDoc2 modelDoc, string saveFileNameHint)
    {
      if (!string.IsNullOrWhiteSpace(saveFileNameHint))
        return saveFileNameHint.Trim();

      try
      {
        return modelDoc.GetPathName();
      }
      catch
      {
        return null;
      }
    }
  }
}
