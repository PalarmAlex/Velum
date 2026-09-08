using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace Velum.ReactiveCore.Export
{
  /// <summary>Классификация геометрии детали для выбора режима экспорта DXF.</summary>
  internal static class VelumDxfPartGeometryHelper
  {
    internal const string EmptyDocumentMessage =
        "Документ пустой: нет эскизов и твёрдых тел. Экспорт DXF невозможен.";

    internal static bool IsEmptyDocument(ModelDoc2 modelDoc)
    {
      return !HasSolidBodies(modelDoc) && !HasAnySketch(modelDoc);
    }

    internal static bool HasSolidBodies(ModelDoc2 modelDoc)
    {
      if (modelDoc == null || modelDoc.GetType() != (int)swDocumentTypes_e.swDocPART)
        return false;

      try
      {
        object[] bodies = ((PartDoc)modelDoc).GetBodies2((int)swBodyType_e.swSolidBody, true) as object[];
        return bodies != null && bodies.Length > 0;
      }
      catch
      {
        return false;
      }
    }

    internal static bool HasAnySketch(ModelDoc2 modelDoc)
    {
      return TryFindFirstSketchFeature(modelDoc, forExport: false) != null;
    }

    internal static bool IsSketchOnlyPart(ModelDoc2 modelDoc)
    {
      return !HasSolidBodies(modelDoc) && HasAnySketch(modelDoc);
    }

    internal static Feature TryFindFirstSketchFeature(ModelDoc2 modelDoc, bool forExport)
    {
      if (modelDoc == null)
        return null;

      Feature feature = modelDoc.FirstFeature() as Feature;
      while (feature != null)
      {
        Feature found = TryFindFirstSketchInBranch(feature, forExport);
        if (found != null)
          return found;

        feature = feature.GetNextFeature() as Feature;
      }

      return null;
    }

    private static Feature TryFindFirstSketchInBranch(Feature feature, bool forExport)
    {
      if (feature == null)
        return null;

      if (TryAsExportableSketchFeature(feature, forExport, out Feature sketchFeature))
        return sketchFeature;

      Feature sub = feature.GetFirstSubFeature() as Feature;
      while (sub != null)
      {
        Feature found = TryFindFirstSketchInBranch(sub, forExport);
        if (found != null)
          return found;

        sub = sub.GetNextSubFeature() as Feature;
      }

      return null;
    }

    private static bool TryAsExportableSketchFeature(
        Feature feature,
        bool forExport,
        out Feature sketchFeature)
    {
      sketchFeature = null;
      if (feature == null)
        return false;

      try
      {
        if (feature.IsSuppressed())
          return false;
      }
      catch
      {
        return false;
      }

      object spec;
      try
      {
        spec = feature.GetSpecificFeature2();
      }
      catch
      {
        return false;
      }

      if (!(spec is Sketch sketch))
        return false;

      try
      {
        if (sketch.Is3D())
          return false;
      }
      catch
      {
        return false;
      }

      if (forExport && ShouldSkipSketchForExport(feature))
        return false;

      sketchFeature = feature;
      return true;
    }

    private static bool ShouldSkipSketchForExport(Feature feature)
    {
      string name = TryGetFeatureName(feature);
      if (string.IsNullOrWhiteSpace(name))
        return false;

      if (string.Equals(name, "Исходная точка", System.StringComparison.OrdinalIgnoreCase) ||
          string.Equals(name, "Origin Point", System.StringComparison.OrdinalIgnoreCase))
        return true;

      if (name.StartsWith("Сгиб", System.StringComparison.OrdinalIgnoreCase) ||
          name.StartsWith("Bend", System.StringComparison.OrdinalIgnoreCase))
        return true;

      if (name.IndexOf("Граничная рамка", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
          name.IndexOf("Bounding Box", System.StringComparison.OrdinalIgnoreCase) >= 0)
        return true;

      return false;
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
  }
}
