using System;

using System.Collections.Generic;

using System.IO;

using System.Linq;

using ISIDA.Common;

using SolidWorks.Interop.sldworks;

using SolidWorks.Interop.swconst;



namespace Velum.ReactiveCore.Export

{

  /// <summary>Экспорт ортогональной проекции объемной детали в DXF через ExportToDWG2.</summary>

  internal static class VelumDxfProjectionExportHelper

  {

    private const double NormalMatchTolerance = 0.85;



    internal static bool TryExport(

        ModelDoc2 modelDoc,

        string outputPath,

        VelumDxfProjectionView projectionView,

        out string error)

    {

      error = string.Empty;

      string modelPath = modelDoc?.GetPathName();

      if (string.IsNullOrWhiteSpace(modelPath))

      {

        error = "Деталь должна быть сохранена на диск перед экспортом DXF.";

        return false;

      }



      Logger.Info("Velum DXF projection export start: view=" + projectionView + " path=" + outputPath);

      TryDeleteOutputFile(outputPath);

      var partDoc = (PartDoc)modelDoc;

      // Скрываем все эскизы — чтобы они не попадали в DXF.
      HideAllSketches(modelDoc);

      try
      {
        // Экспортируем весь видимый контур проекции — только 3D-геометрия, без эскизов.
        if (TryExportAnnotationViews(partDoc, modelPath, outputPath, projectionView))
        {
          Logger.Info("Velum DXF projection export OK via annotation views (sketches hidden): " + projectionView);
          return true;
        }
      }
      finally
      {
        // Восстанавливаем видимость эскизов.
        ShowAllSketches(modelDoc);
      }



      // Fallback: экспорт через аннотационные виды (может включать линии эскизов).

      if (TryExportAnnotationViews(partDoc, modelPath, outputPath, projectionView))

      {

        Logger.Info("Velum DXF projection export OK via annotation views: " + projectionView);

        return true;

      }



      if (string.IsNullOrWhiteSpace(error))

        error = "SolidWorks не выполнил экспорт проекции " + projectionView + ".";



      Logger.Warning("Velum DXF projection export failed: " + error);

      return false;

    }



    /// <summary>
    /// Временно подавляет все эскизы детали, чтобы они не попали в экспорт DXF.
    /// </summary>
    private static void HideAllSketches(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return;

      try
      {
        Feature feat = modelDoc.FirstFeature() as Feature;
        while (feat != null)
        {
          SuppressSketchFeature(modelDoc, feat);
          feat = feat.GetNextFeature() as Feature;
        }
        modelDoc.GraphicsRedraw2();
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum DXF HideAllSketches ex: " + ex.Message);
      }
    }

    /// <summary>
    /// Восстанавливает все подавленные эскизы после экспорта DXF.
    /// </summary>
    private static void ShowAllSketches(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return;

      try
      {
        Feature feat = modelDoc.FirstFeature() as Feature;
        while (feat != null)
        {
          ResumeSketchFeature(modelDoc, feat);
          feat = feat.GetNextFeature() as Feature;
        }
        modelDoc.GraphicsRedraw2();
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum DXF ShowAllSketches ex: " + ex.Message);
      }
    }

    /// <summary>
    /// Рекурсивно подавляет эскиз (и вложенные эскизы) внутри feature.
    /// </summary>
    private static void SuppressSketchFeature(ModelDoc2 modelDoc, Feature feature)
    {
      if (feature == null)
        return;

      try
      {
        object spec = feature.GetSpecificFeature2();
        if (spec is Sketch sketch)
        {
          feature.SetSuppression2(
              (int)swFeatureSuppressionAction_e.swSuppressFeature,
              (int)swInConfigurationOpts_e.swSpecifyConfiguration,
              null);
        }
      }
      catch
      {
        // Игнорируем ошибки для отдельных feature
      }

      // Рекурсивно обрабатываем вложенные feature (например, "Эскиз 1" содержит линии)
      Feature sub = feature.GetFirstSubFeature() as Feature;
      while (sub != null)
      {
        SuppressSketchFeature(modelDoc, sub);
        sub = sub.GetNextSubFeature() as Feature;
      }
    }

    /// <summary>
    /// Рекурсивно восстанавливает эскиз (и вложенные эскизы) внутри feature.
    /// </summary>
    private static void ResumeSketchFeature(ModelDoc2 modelDoc, Feature feature)
    {
      if (feature == null)
        return;

      try
      {
        object spec = feature.GetSpecificFeature2();
        if (spec is Sketch sketch)
        {
          feature.SetSuppression2(
              (int)swFeatureSuppressionAction_e.swUnSuppressFeature,
              (int)swInConfigurationOpts_e.swSpecifyConfiguration,
              null);
        }
      }
      catch
      {
        // Игнорируем ошибки для отдельных feature
      }

      // Рекурсивно обрабатываем вложенные feature
      Feature sub = feature.GetFirstSubFeature() as Feature;
      while (sub != null)
      {
        ResumeSketchFeature(modelDoc, sub);
        sub = sub.GetNextSubFeature() as Feature;
      }
    }

    private static void PrepareModelForAnnotationExport(ModelDoc2 modelDoc)

    {

      if (modelDoc == null)

        return;



      try

      {

        modelDoc.GraphicsRedraw2();

      }

      catch (Exception ex)

      {

        Logger.Info("Velum DXF prepare model: " + ex.Message);

      }

    }



    private static bool TryExportAnnotationViews(

        PartDoc partDoc,

        string modelPath,

        string outputPath,

        VelumDxfProjectionView projectionView)

    {

      string[] viewNames = BuildAnnotationViewOrder(projectionView);



      return TryAnnotationExportPasses(partDoc, modelPath, outputPath, viewNames, projectionView);

    }



    private static bool TryAnnotationExportPasses(

        PartDoc partDoc,

        string modelPath,

        string outputPath,

        string[] viewNames,

        VelumDxfProjectionView projectionView)

    {

      double[] identityAlignment = CreateIdentityAlignment();



      foreach (string viewName in viewNames)

      {

        if (string.IsNullOrWhiteSpace(viewName))

          continue;



        if (TryExportAnnotationView(

                partDoc,

                modelPath,

                outputPath,

                outputPath,

                viewName,

                identityAlignment,

                singleFile: true,

                logFailure: false))

          return true;

      }



      string outputBase = Path.Combine(

          Path.GetDirectoryName(outputPath) ?? string.Empty,

          Path.GetFileNameWithoutExtension(outputPath));

      double[] projectionAlignment = CreateProjectionAlignment(projectionView);



      foreach (string viewName in viewNames)

      {

        if (string.IsNullOrWhiteSpace(viewName))

          continue;



        if (TryExportAnnotationView(

                partDoc,

                modelPath,

                outputPath,

                outputBase,

                viewName,

                identityAlignment,

                singleFile: true,

                logFailure: false))

          return true;



        if (TryExportAnnotationView(

                partDoc,

                modelPath,

                outputPath,

                outputPath,

                viewName,

                projectionAlignment,

                singleFile: true,

                logFailure: false))

          return true;

      }



      if (TryExportAnnotationView(

              partDoc,

              modelPath,

              outputPath,

              outputPath,

              null,

              identityAlignment,

              singleFile: true,

              logFailure: true))

        return true;



      Logger.Info("Velum DXF annotation export: all view candidates failed for " + projectionView);

      return false;

    }



    private static bool TryExportAnnotationView(

        PartDoc partDoc,

        string modelPath,

        string desiredOutputPath,

        string exportPath,

        string viewName,

        double[] alignment,

        bool singleFile,

        bool logFailure)

    {

      object views = viewName == null ? null : new[] { viewName };

      string viewLabel = viewName ?? "<all>";



      try

      {

        TryDeleteOutputFile(desiredOutputPath);

        bool ok = partDoc.ExportToDWG2(

            exportPath,

            modelPath,

            (int)swExportToDWG_e.swExportToDWG_ExportAnnotationViews,

            singleFile,

            alignment,

            false,

            false,

            0,

            views);



        if (TryFinalizeExportedFile(desiredOutputPath, exportPath, viewName, singleFile, ok))

        {

          Logger.Info("Velum DXF annotation export OK: view=\"" + viewLabel +

            "\" exportPath=\"" + exportPath + "\"");

          return true;

        }



        if (logFailure)

        {

          Logger.Info("Velum DXF annotation export fail: view=\"" + viewLabel +

              "\" ok=" + ok + " exportPath=\"" + exportPath + "\"");

        }

      }

      catch (Exception ex)

      {

        Logger.Warning("Velum DXF annotation export ex: view=\"" + viewLabel + "\": " + ex.Message);

      }



      return false;

    }



    private static string[] BuildAnnotationViewOrder(VelumDxfProjectionView projectionView)

    {

      return VelumDxfStandardViewHelper.GetAnnotationViewCandidates(projectionView).ToArray();

    }



    private static bool TryExportSelectedPlanarFace(

        PartDoc partDoc,

        ModelDoc2 modelDoc,

        string modelPath,

        string outputPath,

        VelumDxfProjectionView projectionView,

        out string error)

    {

      error = string.Empty;

      modelDoc.ClearSelection2(true);



      if (!TrySelectPlanarFaceForProjection(modelDoc, projectionView, out string faceKind))

      {

        error = "Не удалось выбрать плоскую грань для экспорта DXF.";

        Logger.Info("Velum DXF planar face export: selection failed view=" + projectionView);

        return false;

      }



      Logger.Info("Velum DXF planar face export: selected " + faceKind + " view=" + projectionView);



      try

      {

        object alignment = CreateIdentityAlignment();

        bool ok = partDoc.ExportToDWG2(

            outputPath,

            modelPath,

            (int)swExportToDWG_e.swExportToDWG_ExportSelectedFacesOrLoops,

            true,

            alignment,

            false,

            false,

            0,

            null);



        if (!ok)

          error = "SolidWorks не выполнил экспорт выбранной грани в DXF.";

        else if (!File.Exists(outputPath))

        {

          ok = false;

          error = "Файл DXF не создан после ExportToDWG2.";

        }



        return ok;

      }

      catch (Exception ex)

      {

        error = ex.Message;

        return false;

      }

      finally

      {

        modelDoc.ClearSelection2(true);

      }

    }



    private static bool TrySelectPlanarFaceForProjection(

        ModelDoc2 modelDoc,

        VelumDxfProjectionView projectionView,

        out string faceKind)

    {

      faceKind = string.Empty;



      if (TrySelectBestPlanarFaceByNormal(modelDoc, projectionView, out faceKind))

        return true;



      if (TrySelectPlanarFaceByRay(modelDoc, projectionView, out faceKind))

        return true;



      return false;

    }



    private static bool TrySelectBestPlanarFaceByNormal(

        ModelDoc2 modelDoc,

        VelumDxfProjectionView projectionView,

        out string faceKind)

    {

      faceKind = string.Empty;

      if (!TryGetDesiredFaceNormal(projectionView, out double nx, out double ny, out double nz))

        return false;



      Face2 bestFace = null;

      double bestScore = double.MinValue;

      int planarCount = 0;



      foreach (Face2 face in EnumerateSolidFaces(modelDoc))

      {

        if (!IsPlanarFace(face))

          continue;



        planarCount++;

        if (!TryEvaluateFaceNormal(face, out double fnx, out double fny, out double fnz))

          continue;



        double score = Dot(nx, ny, nz, fnx, fny, fnz);

        if (score > bestScore)

        {

          bestScore = score;

          bestFace = face;

        }

      }



      if (bestFace == null || bestScore < NormalMatchTolerance)

      {

        Logger.Info("Velum DXF planar by normal: no match view=" + projectionView +

            " planarFaces=" + planarCount + " bestScore=" + bestScore.ToString("0.###"));

        return false;

      }



      bool selected = ((Entity)bestFace).Select4(false, null);

      if (selected)

        faceKind = "planar-normal score=" + bestScore.ToString("0.###");

      return selected;

    }



    private static bool TrySelectPlanarFaceByRay(

        ModelDoc2 modelDoc,

        VelumDxfProjectionView projectionView,

        out string faceKind)

    {

      faceKind = string.Empty;

      if (!TryGetProjectionRayDirection(projectionView, out double dx, out double dy, out double dz))

        return false;



      if (!TrySelectFaceByRayDirection(modelDoc, dx, dy, dz, "projection"))

        return false;



      Face2 selectedFace = TryGetFirstSelectedFace(modelDoc);

      if (selectedFace == null)

      {

        faceKind = "ray-without-face";

        return true;

      }



      if (!IsPlanarFace(selectedFace))

      {

        modelDoc.ClearSelection2(true);

        string surfaceKind = DescribeSurface(selectedFace);

        Logger.Info("Velum DXF planar by ray: rejected non-planar surface=" + surfaceKind +

            " view=" + projectionView);

        return false;

      }



      faceKind = "planar-ray surface=" + DescribeSurface(selectedFace);

      return true;

    }



    private static Face2 TryGetFirstSelectedFace(ModelDoc2 modelDoc)

    {

      try

      {

        SelectionMgr selectionMgr = modelDoc?.SelectionManager as SelectionMgr;

        if (selectionMgr == null)

          return null;



        for (int i = 1; i <= selectionMgr.GetSelectedObjectCount2(-1); i++)

        {

          if (selectionMgr.GetSelectedObjectType3(i, -1) != (int)swSelectType_e.swSelFACES)

            continue;



          return selectionMgr.GetSelectedObject6(i, -1) as Face2;

        }

      }

      catch

      {

      }



      return null;

    }



    private static bool IsPlanarFace(Face2 face)

    {

      try

      {

        Surface surface = face?.GetSurface() as Surface;

        return surface != null && surface.IsPlane();

      }

      catch

      {

        return false;

      }

    }



    private static string DescribeSurface(Face2 face)

    {

      try

      {

        Surface surface = face?.GetSurface() as Surface;

        if (surface == null)

          return "unknown";

        if (surface.IsPlane())

          return "plane";

        if (surface.IsCylinder())

          return "cylinder";

        if (surface.IsCone())

          return "cone";

        if (surface.IsSphere())

          return "sphere";

        return "other";

      }

      catch

      {

        return "unknown";

      }

    }



    private static bool TrySelectFaceByRayDirection(

        ModelDoc2 modelDoc,

        double dx,

        double dy,

        double dz,

        string source)

    {

      Normalize(ref dx, ref dy, ref dz);

      if (IsZeroVector(dx, dy, dz))

      {

        Logger.Info("Velum DXF SelectByRay: zero direction source=" + source);

        return false;

      }



      double[] box = ((PartDoc)modelDoc).GetPartBox(true) as double[];

      if (box == null || box.Length < 6)

        return false;



      double cx = (box[0] + box[3]) * 0.5;

      double cy = (box[1] + box[4]) * 0.5;

      double cz = (box[2] + box[5]) * 0.5;



      double diag = Math.Sqrt(

          Square(box[3] - box[0]) +

          Square(box[4] - box[1]) +

          Square(box[5] - box[2]));

      if (diag <= 0)

        diag = 0.1;



      double dist = diag * 3.0;

      double startX = cx - dx * dist;

      double startY = cy - dy * dist;

      double startZ = cz - dz * dist;



      double[] radii = { 1e-6, 1e-4, diag * 0.01, diag * 0.05, diag * 0.1 };

      for (int i = 0; i < radii.Length; i++)

      {

        try

        {

          bool selected = modelDoc.Extension.SelectByRay(

              startX,

              startY,

              startZ,

              dx,

              dy,

              dz,

              radii[i],

              (int)swSelectType_e.swSelFACES,

              false,

              0,

              0);



          if (selected)

          {

            Logger.Info("Velum DXF SelectByRay OK source=" + source +

                " ray=(" + dx.ToString("0.###") + "," + dy.ToString("0.###") + "," + dz.ToString("0.###") + ")" +

                " radius=" + radii[i].ToString("0.######"));

            return true;

          }

        }

        catch (Exception ex)

        {

          Logger.Warning("Velum DXF SelectByRay ex source=" + source + ": " + ex.Message);

          return false;

        }

      }



      Logger.Info("Velum DXF SelectByRay fail source=" + source +

          " ray=(" + dx.ToString("0.###") + "," + dy.ToString("0.###") + "," + dz.ToString("0.###") + ")");

      return false;

    }



    private static IEnumerable<Face2> EnumerateSolidFaces(ModelDoc2 modelDoc)

    {

      object[] bodies = ((PartDoc)modelDoc).GetBodies2((int)swBodyType_e.swSolidBody, true) as object[];

      if (bodies == null)

        yield break;



      for (int i = 0; i < bodies.Length; i++)

      {

        Body2 body = bodies[i] as Body2;

        object[] faces = body?.GetFaces() as object[];

        if (faces == null)

          continue;



        for (int j = 0; j < faces.Length; j++)

        {

          Face2 face = faces[j] as Face2;

          if (face != null)

            yield return face;

        }

      }

    }



    private static bool TryEvaluateFaceNormal(Face2 face, out double nx, out double ny, out double nz)

    {

      nx = ny = nz = 0;

      if (face == null)

        return false;



      try

      {

        Surface surface = face.GetSurface() as Surface;

        double[] uv = face.GetUVBounds() as double[];

        if (surface == null || uv == null || uv.Length < 4)

          return false;



        double u = (uv[0] + uv[2]) * 0.5;

        double v = (uv[1] + uv[3]) * 0.5;

        double[] eval = surface.Evaluate(u, v, 0, 0) as double[];

        if (eval == null || eval.Length < 6)

          return false;



        nx = eval[3];

        ny = eval[4];

        nz = eval[5];

        Normalize(ref nx, ref ny, ref nz);

        return !IsZeroVector(nx, ny, nz);

      }

      catch

      {

        return false;

      }

    }



    private static bool TryGetDesiredFaceNormal(

        VelumDxfProjectionView projectionView,

        out double nx,

        out double ny,

        out double nz)

    {

      nx = ny = nz = 0;

      if (!TryGetProjectionRayDirection(projectionView, out double dx, out double dy, out double dz))

        return false;



      nx = -dx;

      ny = -dy;

      nz = -dz;

      Normalize(ref nx, ref ny, ref nz);

      return !IsZeroVector(nx, ny, nz);

    }



    private static bool TryGetProjectionRayDirection(

        VelumDxfProjectionView projectionView,

        out double dx,

        out double dy,

        out double dz)

    {

      switch (projectionView)

      {

        case VelumDxfProjectionView.Top:

          dx = 0;

          dy = 0;

          dz = -1;

          return true;

        case VelumDxfProjectionView.Right:

          dx = -1;

          dy = 0;

          dz = 0;

          return true;

        case VelumDxfProjectionView.Back:

          dx = 0;

          dy = 1;

          dz = 0;

          return true;

        case VelumDxfProjectionView.Bottom:

          dx = 0;

          dy = 0;

          dz = 1;

          return true;

        case VelumDxfProjectionView.Left:

          dx = 1;

          dy = 0;

          dz = 0;

          return true;

        default:

          dx = 0;

          dy = -1;

          dz = 0;

          return true;

      }

    }



    private static bool TryFinalizeExportedFile(

        string desiredOutputPath,

        string exportPath,

        string viewName,

        bool singleFile,

        bool apiResult)

    {

      if (File.Exists(desiredOutputPath))

        return apiResult || true;



      string directory = Path.GetDirectoryName(desiredOutputPath);

      if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))

        return false;



      string desiredBase = Path.GetFileNameWithoutExtension(desiredOutputPath);

      string exportBase = Path.GetFileName(exportPath) ?? string.Empty;

      string viewToken = (viewName ?? string.Empty).Trim('*');



      string[] candidates = Directory.GetFiles(directory, "*.dxf", SearchOption.TopDirectoryOnly)

          .Where(path =>

          {

            string fileName = Path.GetFileNameWithoutExtension(path) ?? string.Empty;

            if (fileName.Equals(desiredBase, StringComparison.OrdinalIgnoreCase))

              return true;

            if (!string.IsNullOrWhiteSpace(exportBase) &&

                fileName.StartsWith(exportBase, StringComparison.OrdinalIgnoreCase))

              return true;

            if (!string.IsNullOrWhiteSpace(viewToken) &&

                fileName.IndexOf(viewToken, StringComparison.OrdinalIgnoreCase) >= 0)

              return true;

            return false;

          })

          .OrderByDescending(File.GetLastWriteTimeUtc)

          .ToArray();



      if (candidates.Length == 0)

        return false;



      string source = candidates[0];

      if (!source.Equals(desiredOutputPath, StringComparison.OrdinalIgnoreCase))

      {

        TryDeleteOutputFile(desiredOutputPath);

        File.Copy(source, desiredOutputPath, overwrite: true);

        if (!singleFile && !source.Equals(exportPath, StringComparison.OrdinalIgnoreCase))

        {

          try

          {

            File.Delete(source);

          }

          catch

          {

          }

        }

      }



      return File.Exists(desiredOutputPath);

    }



    private static double[] CreateIdentityAlignment()

    {

      return new double[]

      {

        0, 0, 0,

        1, 0, 0,

        0, 1, 0,

        0, 0, 1,

      };

    }



    private static double[] CreateProjectionAlignment(VelumDxfProjectionView projectionView)

    {

      switch (projectionView)

      {

        case VelumDxfProjectionView.Top:

          return new double[]

          {

            0, 0, 0,

            1, 0, 0,

            0, 0, -1,

            0, 1, 0,

          };

        case VelumDxfProjectionView.Right:

          return new double[]

          {

            0, 0, 0,

            0, 1, 0,

            0, 0, 1,

            -1, 0, 0,

          };

        case VelumDxfProjectionView.Back:

          return new double[]

          {

            0, 0, 0,

            -1, 0, 0,

            0, 1, 0,

            0, 0, -1,

          };

        case VelumDxfProjectionView.Bottom:

          return new double[]

          {

            0, 0, 0,

            1, 0, 0,

            0, 0, 1,

            0, -1, 0,

          };

        case VelumDxfProjectionView.Left:

          return new double[]

          {

            0, 0, 0,

            0, -1, 0,

            0, 0, 1,

            1, 0, 0,

          };

        default:

          return new double[]

          {

            0, 0, 0,

            1, 0, 0,

            0, 1, 0,

            0, 0, 1,

          };

      }

    }



    private static double Dot(double ax, double ay, double az, double bx, double by, double bz)

    {

      return ax * bx + ay * by + az * bz;

    }



    private static bool IsZeroVector(double x, double y, double z)

    {

      return Math.Abs(x) < 1e-9 && Math.Abs(y) < 1e-9 && Math.Abs(z) < 1e-9;

    }



    private static void Normalize(ref double x, ref double y, ref double z)

    {

      double len = Math.Sqrt(Square(x) + Square(y) + Square(z));

      if (len <= 0)

        return;



      x /= len;

      y /= len;

      z /= len;

    }



    private static double Square(double value) => value * value;



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


