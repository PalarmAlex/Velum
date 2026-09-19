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

      // FileSavePostNotify приходит и на экспорт (Save As → DXF/PDF/STEP). Для листовой детали
      // TryEnsure делает ForceRebuild3 + UpdateCutList + запись свойств, и это рвёт
      // PropertyManager настроек экспорта DXF («мигает и схлопывается»). Чиним ссылки
      // только когда реально сохраняется нативный .sldprt.
      if (!VelumSolidWorksSaveFileNameHelper.IsNativeSavePath(fullPath, ".sldprt"))
        return;

      // Как рецепт ensure_blank_size_links: active + repair. Синхронно на потоке UI панели:
      // FileSavePostNotify — после записи файла на диск, менять документ здесь безопасно.
      // Откладывать через BeginInvoke нельзя: задача доедет до момента, когда пользователь
      // уже открыл PropertyManager «Сохранить как», и схлопнет его.
      VelumBlankSizePropertyLinksService.EnsureStatus status =
          VelumBlankSizePropertyLinksService.EnsureStatus.Failed;
      string message = string.Empty;

      try
      {
        VelumSolidEnvironmentBridge.RunOnTaskPaneUiThread(() =>
        {
          status = VelumBlankSizePropertyLinksService.TryEnsure(
              modelDoc,
              "repair",
              "active",
              out message);
        });
      }
      catch (Exception ex)
      {
        Logger.Warning(
            "Velum blank-size links after save FAIL path=\"" + (fullPath ?? string.Empty) +
            "\" " + ex.Message);
        return;
      }

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
