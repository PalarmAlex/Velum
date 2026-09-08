using System;
using System.IO;
using ISIDA.Common;
using SolidWorks.Interop.sldworks;

namespace Velum.ReactiveCore.Export
{
  /// <summary>Экспорт первого 2D-эскиза детали в DXF (только геометрия эскиза).</summary>
  internal static class VelumDxfSketchExportHelper
  {
    internal static bool TryExportFirstSketch(ModelDoc2 modelDoc, string outputPath, out string error)
    {
      error = string.Empty;
      if (modelDoc == null)
      {
        error = "Нет активной детали.";
        return false;
      }

      if (!IsPartSavedOnDisk(modelDoc))
      {
        error = "Деталь должна быть сохранена на диск перед экспортом DXF.";
        return false;
      }

      Feature sketchFeature = VelumDxfPartGeometryHelper.TryFindFirstSketchFeature(modelDoc, forExport: true);
      if (sketchFeature == null)
      {
        error = "Не найден эскиз для экспорта DXF.";
        return false;
      }

      string sketchName = TryGetFeatureName(sketchFeature);
      Logger.Info("Velum DXF sketch export start: sketch=\"" + sketchName + "\" path=" + outputPath);
      TryDeleteOutputFile(outputPath);

      if (VelumDxfSketchGeometryWriter.TryWriteSketchToDxf(modelDoc, sketchFeature, outputPath, out error))
      {
        Logger.Info("Velum DXF sketch export OK via geometry writer: sketch=\"" + sketchName + "\"");
        return true;
      }

      if (string.IsNullOrWhiteSpace(error))
        error = "SolidWorks не выполнил экспорт эскиза «" + sketchName + "» в DXF.";

      Logger.Warning("Velum DXF sketch export failed: " + error);
      return false;
    }

    private static bool IsPartSavedOnDisk(ModelDoc2 modelDoc)
    {
      try
      {
        string path = modelDoc.GetPathName();
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
      }
      catch
      {
        return false;
      }
    }

    private static string TryGetFeatureName(Feature feature)
    {
      try
      {
        return feature?.Name ?? string.Empty;
      }
      catch
      {
        return string.Empty;
      }
    }

    private static void TryDeleteOutputFile(string outputPath)
    {
      if (string.IsNullOrWhiteSpace(outputPath))
        return;

      try
      {
        if (File.Exists(outputPath))
          File.Delete(outputPath);
      }
      catch
      {
      }
    }
  }
}
