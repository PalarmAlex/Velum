using System;
using System.Collections.Generic;
using ISIDA.Common;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace Velum.ReactiveCore.Export
{
  /// <summary>Определение и установка стандартных видов Front/Top/Right/Back/Bottom/Left.</summary>
  internal static class VelumDxfStandardViewHelper
  {
    private const double OrientationTolerance = 0.02;
    private const int UseViewNameOnly = -1;

    internal static VelumDxfProjectionView? TryDetectCurrentStandardView(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return null;

      MathTransform saved = TryCaptureOrientation(modelDoc);
      if (saved == null)
      {
        Logger.Info("Velum DXF view detect: no active orientation");
        return null;
      }

      try
      {
        foreach (VelumDxfProjectionView candidate in Enum.GetValues(typeof(VelumDxfProjectionView)))
        {
          if (!TryApplyStandardView(modelDoc, candidate, logAttempts: false, out MathTransform applied))
            continue;

          if (OrientationsMatch(saved, applied, OrientationTolerance))
          {
            Logger.Info("Velum DXF view detect: matched " + candidate);
            return candidate;
          }
        }

        Logger.Info("Velum DXF view detect: no standard view matched");
        return null;
      }
      finally
      {
        TryRestoreOrientation(modelDoc, saved);
      }
    }

    internal static bool TryShowStandardView(ModelDoc2 modelDoc, VelumDxfProjectionView view)
    {
      return TryApplyStandardView(modelDoc, view, logAttempts: true, out _);
    }

    internal static bool EnsureStandardView(ModelDoc2 modelDoc, VelumDxfProjectionView desired)
    {
      if (TryApplyStandardView(modelDoc, desired, logAttempts: true, out _))
        return true;

      Logger.Warning("Velum DXF EnsureStandardView failed: " + desired);
      return false;
    }

    private static bool TryApplyStandardView(
        ModelDoc2 modelDoc,
        VelumDxfProjectionView view,
        bool logAttempts,
        out MathTransform appliedOrientation)
    {
      appliedOrientation = null;
      if (modelDoc == null)
        return false;

      MathTransform before = TryCaptureOrientation(modelDoc);

      if (TryApplyStandardViewById(modelDoc, view, before, logAttempts, out appliedOrientation))
        return true;

      foreach (string namedView in GetNamedViewCandidates(view))
      {
        try
        {
          modelDoc.ShowNamedView2(namedView, UseViewNameOnly);
          modelDoc.ViewZoomtofit2();
          modelDoc.GraphicsRedraw2();

          appliedOrientation = TryCaptureOrientation(modelDoc);
          if (!IsViewApplied(before, appliedOrientation, view))
          {
            if (logAttempts)
            {
              Logger.Info("Velum DXF view: named view not applied view=" + view +
                  " name=\"" + namedView + "\"");
            }

            continue;
          }

          if (logAttempts)
          {
            Logger.Info("Velum DXF view OK by name: " + view + " name=\"" + namedView + "\"");
          }

          return true;
        }
        catch (Exception ex)
        {
          if (logAttempts)
          {
            Logger.Warning("Velum DXF view ex: " + view + " name=\"" + namedView + "\": " + ex.Message);
          }
        }
      }

      if (logAttempts)
        Logger.Warning("Velum DXF view: all candidates failed for " + view);
      return false;
    }

    private static bool TryApplyStandardViewById(
        ModelDoc2 modelDoc,
        VelumDxfProjectionView view,
        MathTransform before,
        bool logAttempts,
        out MathTransform appliedOrientation)
    {
      appliedOrientation = null;
      int viewId = ToStandardViewId(view);
      if (viewId <= 0)
        return false;

      try
      {
        modelDoc.ShowNamedView2(string.Empty, viewId);
        modelDoc.ViewZoomtofit2();
        modelDoc.GraphicsRedraw2();

        appliedOrientation = TryCaptureOrientation(modelDoc);
        if (!IsViewApplied(before, appliedOrientation, view))
        {
          if (logAttempts)
            Logger.Info("Velum DXF view: standard view id not applied view=" + view + " id=" + viewId);
          return false;
        }

        if (logAttempts)
          Logger.Info("Velum DXF view OK by id: " + view + " id=" + viewId);
        return true;
      }
      catch (Exception ex)
      {
        if (logAttempts)
          Logger.Warning("Velum DXF view id ex: " + view + " id=" + viewId + ": " + ex.Message);
        return false;
      }
    }

    private static bool IsViewApplied(
        MathTransform before,
        MathTransform after,
        VelumDxfProjectionView view)
    {
      if (after == null)
        return false;

      if (before == null || !OrientationsMatch(before, after, OrientationTolerance))
        return true;

      // Уже на нужном виде: ориентация не меняется, но это успех.
      return true;
    }

    private static int ToStandardViewId(VelumDxfProjectionView view)
    {
      switch (view)
      {
        case VelumDxfProjectionView.Top:
          return (int)swStandardViews_e.swTopView;
        case VelumDxfProjectionView.Right:
          return (int)swStandardViews_e.swRightView;
        case VelumDxfProjectionView.Back:
          return (int)swStandardViews_e.swBackView;
        case VelumDxfProjectionView.Bottom:
          return (int)swStandardViews_e.swBottomView;
        case VelumDxfProjectionView.Left:
          return (int)swStandardViews_e.swLeftView;
        default:
          return (int)swStandardViews_e.swFrontView;
      }
    }

    internal static MathTransform TryCaptureOrientation(ModelDoc2 modelDoc)
    {
      try
      {
        ModelView view = modelDoc?.ActiveView as ModelView;
        return view?.Orientation3;
      }
      catch
      {
        return null;
      }
    }

    internal static void TryRestoreOrientation(ModelDoc2 modelDoc, MathTransform orientation)
    {
      if (modelDoc == null || orientation == null)
        return;

      try
      {
        ModelView view = modelDoc.ActiveView as ModelView;
        if (view == null)
          return;

        view.Orientation3 = orientation;
        modelDoc.GraphicsRedraw2();
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum DXF view restore: " + ex.Message);
      }
    }

    internal static string ToAnnotationViewName(VelumDxfProjectionView view)
    {
      switch (view)
      {
        case VelumDxfProjectionView.Top:
          return "*Top";
        case VelumDxfProjectionView.Right:
          return "*Right";
        case VelumDxfProjectionView.Back:
          return "*Back";
        case VelumDxfProjectionView.Bottom:
          return "*Bottom";
        case VelumDxfProjectionView.Left:
          return "*Left";
        default:
          return "*Front";
      }
    }

    internal static IEnumerable<string> GetAnnotationViewCandidates(VelumDxfProjectionView view)
    {
      foreach (string name in GetNamedViewCandidates(view))
        yield return name;

      yield return "*Current";
      yield return "*Текущий";
      yield return "*В процессе";
    }

    private static IEnumerable<string> GetNamedViewCandidates(VelumDxfProjectionView view)
    {
      switch (view)
      {
        case VelumDxfProjectionView.Top:
          yield return "*Сверху";
          yield return "*Вид сверху";
          yield return "*Top";
          yield return "*Top Plane";
          break;
        case VelumDxfProjectionView.Right:
          yield return "*Справа";
          yield return "*Right";
          yield return "*Right Plane";
          break;
        case VelumDxfProjectionView.Back:
          yield return "*Сзади";
          yield return "*Вид сзади";
          yield return "*Back";
          yield return "*Back Plane";
          break;
        case VelumDxfProjectionView.Bottom:
          yield return "*Снизу";
          yield return "*Вид снизу";
          yield return "*Bottom";
          yield return "*Bottom Plane";
          break;
        case VelumDxfProjectionView.Left:
          yield return "*Слева";
          yield return "*Вид слева";
          yield return "*Left";
          yield return "*Left Plane";
          break;
        default:
          yield return "*Спереди";
          yield return "*Front";
          yield return "*Front Plane";
          break;
      }
    }

    private static bool OrientationsMatch(MathTransform a, MathTransform b, double tolerance)
    {
      if (a == null || b == null)
        return false;

      double[] da;
      double[] db;
      try
      {
        da = a.ArrayData as double[];
        db = b.ArrayData as double[];
      }
      catch
      {
        return false;
      }

      if (da == null || db == null || da.Length != db.Length)
        return false;

      for (int i = 0; i < da.Length; i++)
      {
        if (Math.Abs(da[i] - db[i]) > tolerance)
          return false;
      }

      return true;
    }
  }
}
