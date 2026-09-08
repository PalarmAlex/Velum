using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using ISIDA.Common;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Velum.ReactiveCore;

namespace Velum.ReactiveCore.Export
{
  /// <summary>Переносит путь сохранённого чертежа в свойства показанных им моделей.</summary>
  internal static class VelumDrawingPathSavePropagator
  {
    internal sealed class PropagationResult
    {
      internal int Written { get; set; }
      internal int Skipped { get; set; }
      internal int Failed { get; set; }
    }

    internal static PropagationResult TryPropagateFromSavedDrawing(
        ISldWorks swApp,
        ModelDoc2 drawingDoc)
    {
      var result = new PropagationResult();
      if (swApp == null || drawingDoc == null ||
          drawingDoc.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
        return result;

      string drawingPath;
      try
      {
        drawingPath = (drawingDoc.GetPathName() ?? string.Empty).Trim();
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum drawing path propagation: " + ex.Message);
        return result;
      }

      if (string.IsNullOrWhiteSpace(drawingPath) ||
          !string.Equals(Path.GetExtension(drawingPath), ".slddrw", StringComparison.OrdinalIgnoreCase))
        return result;

      // path → загруженный ModelDoc (null = нужно silent open)
      var targets = new Dictionary<string, ModelDoc2>(StringComparer.OrdinalIgnoreCase);
      CollectReferencedPartOrAssemblyTargets(drawingDoc, targets);

      VelumExportDocumentationGeometryStampHelper.RunWithGeometryPendingStampSyncSuppressed(() =>
      {
        foreach (KeyValuePair<string, ModelDoc2> entry in targets)
        {
          if (entry.Value != null)
            TryWriteLoadedModel(entry.Value, drawingPath, result);
          else if (VelumDrawingPathPropertyHelper.TryWriteToPath(
                       swApp, entry.Key, drawingPath, out bool skipped, out string message))
          {
            if (skipped)
              result.Skipped++;
            else
              result.Written++;
          }
          else
          {
            result.Failed++;
            Logger.Warning("Velum drawing path propagation: " + entry.Key + " " + message);
          }
        }
      });
      return result;
    }

    /// <summary>
    /// Собирает referenced детали/сборки чертежа (path → загруженный ModelDoc или null).
    /// </summary>
    internal static void CollectReferencedPartOrAssemblyTargets(
        ModelDoc2 drawingDoc,
        IDictionary<string, ModelDoc2> targets)
    {
      if (drawingDoc == null || targets == null)
        return;

      try
      {
        DrawingDoc drawing = drawingDoc as DrawingDoc;
        if (drawing == null)
          return;

        object sheetViews = drawing.GetViews();
        IEnumerable sheets = sheetViews as IEnumerable;
        if (sheets == null)
          return;

        foreach (object sheet in sheets)
        {
          Array views = sheet as Array;
          if (views == null)
            continue;

          // Первый View каждого листа — представление листа, а не модельный вид.
          for (int i = 1; i < views.Length; i++)
            TryCollectReferencedModel(views.GetValue(i) as View, targets);
        }
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum drawing path collect references: " + ex.Message);
      }
    }

    private static void TryCollectReferencedModel(
        View view,
        IDictionary<string, ModelDoc2> targets)
    {
      if (view == null)
        return;

      ModelDoc2 modelDoc = null;
      try
      {
        modelDoc = view.ReferencedDocument as ModelDoc2;
      }
      catch
      {
      }

      if (modelDoc != null && IsPartOrAssembly(modelDoc))
      {
        string loadedPath = TryGetModelPath(modelDoc);
        if (!string.IsNullOrWhiteSpace(loadedPath))
          targets[loadedPath] = modelDoc;
        return;
      }

      try
      {
        string modelPath = (view.GetReferencedModelName() ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(modelPath) || !IsPartOrAssemblyPath(modelPath))
          return;
        if (!targets.ContainsKey(modelPath))
          targets[modelPath] = null;
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum drawing path propagation: " + ex.Message);
      }
    }

    private static void TryWriteLoadedModel(
        ModelDoc2 modelDoc,
        string drawingPath,
        PropagationResult result)
    {
      try
      {
        if (!VelumDrawingPathPropertyHelper.TryWrite(
                modelDoc, drawingPath, out bool skipped, out string message))
        {
          result.Failed++;
          Logger.Warning("Velum drawing path propagation: " + message);
          return;
        }

        if (skipped)
        {
          result.Skipped++;
          return;
        }

        int errors = 0;
        int warnings = 0;
        if (!modelDoc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref errors, ref warnings))
        {
          result.Failed++;
          Logger.Warning(
              "Velum drawing path propagation: Save3 errors=" + errors + " warnings=" + warnings);
          return;
        }
        result.Written++;
      }
      catch (Exception ex)
      {
        result.Failed++;
        Logger.Warning("Velum drawing path propagation: " + ex.Message);
      }
    }

    private static string TryGetModelPath(ModelDoc2 modelDoc)
    {
      try
      {
        return (modelDoc.GetPathName() ?? string.Empty).Trim();
      }
      catch
      {
        return string.Empty;
      }
    }

    private static bool IsPartOrAssembly(ModelDoc2 modelDoc)
    {
      try
      {
        int type = modelDoc.GetType();
        return type == (int)swDocumentTypes_e.swDocPART ||
               type == (int)swDocumentTypes_e.swDocASSEMBLY;
      }
      catch
      {
        return false;
      }
    }

    private static bool IsPartOrAssemblyPath(string path)
    {
      string extension = Path.GetExtension(path ?? string.Empty);
      return string.Equals(extension, ".sldprt", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(extension, ".sldasm", StringComparison.OrdinalIgnoreCase);
    }
  }
}
