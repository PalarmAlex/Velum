using System;
using ISIDA.Common;
using SolidWorks.Interop.sldworks;

namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Назначение выученного материала сразу после Save Part без ожидания рефлекса/G_AD.
  /// </summary>
  internal static class VelumMaterialAssignOnSaveCoordinator
  {
    internal static void TryAssignAfterSaveIfEligible(ModelDoc2 modelDoc, string saveFileNameHint)
    {
      if (modelDoc == null || !(modelDoc is PartDoc))
        return;

      string fullPath = ResolveSavedPath(modelDoc, saveFileNameHint);
      if (string.IsNullOrWhiteSpace(fullPath))
        return;

      // Экспорт (Save As → DXF/PDF/STEP) тоже шлёт FileSavePostNotify. Назначение материала
      // делает SetMaterialPropertyName2 + EditRebuild3 — в момент открытого PropertyManager
      // настроек экспорта SolidWorks закрывает его. Назначаем только при нативном .sldprt.
      if (!VelumSolidWorksSaveFileNameHelper.IsNativeSavePath(fullPath, ".sldprt"))
        return;


      string directory = VelumMaterialSequenceState.TryGetDocumentDirectory(modelDoc);
      if (string.IsNullOrWhiteSpace(directory))
        return;

      if (!VelumMaterialSequenceState.IsReady)
      {
        Logger.Info(
            "Velum material assign after save skipped (no learned material in session) path=\"" +
            fullPath + "\"");
        return;
      }

      if (!VelumMaterialSequenceState.IsReadyForDirectory(directory))
      {
        Logger.Info(
            "Velum material assign after save skipped (directory not learned) dir=\"" +
            directory + "\"");
        return;
      }

      if (VelumMaterialSequenceState.WasAutoMaterialAppliedToDocument(modelDoc))
        return;

      if (VelumSolidWorksMaterialComHelper.TryReadPartMaterial(
              modelDoc,
              out string currentMaterial,
              out string _) &&
          !string.IsNullOrWhiteSpace(currentMaterial))
      {
        Logger.Info(
            "Velum material assign after save skipped (material already set) path=\"" +
            fullPath + "\" material=\"" + currentMaterial.Trim() + "\"");
        return;
      }

      if (!VelumMaterialSequenceState.TryGetLearnedMaterial(
              directory,
              out string materialName,
              out string databaseName))
      {
        Logger.Info(
            "Velum material assign after save skipped (learned material not found) dir=\"" +
            directory + "\"");
        return;
      }

      bool ok = false;
      VelumSolidEnvironmentBridge.RunOnTaskPaneUiThread(() =>
      {
        ok = VelumSolidWorksMaterialComHelper.TryAssignPartMaterial(
            modelDoc,
            string.Empty,
            materialName,
            databaseName);
      });

      if (ok)
      {
        VelumMaterialSequenceState.MarkAutoMaterialAppliedToDocument(modelDoc);
        Logger.Info(
            "Velum material assign after save OK path=\"" + fullPath +
            "\" material=\"" + materialName + "\" database=\"" + databaseName + "\"");
        return;
      }

      Logger.Warning(
          "Velum material assign after save FAIL path=\"" + fullPath +
          "\" material=\"" + materialName + "\" database=\"" + databaseName + "\"");
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
