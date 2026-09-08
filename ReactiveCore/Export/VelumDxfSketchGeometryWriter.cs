using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using ISIDA.Common;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace Velum.ReactiveCore.Export
{
  /// <summary>Запись 2D-геометрии эскиза в DXF (мм) без чертежа и ExportToDWG2.</summary>
  internal static class VelumDxfSketchGeometryWriter
  {
    private const double MetersToMillimeters = 1000.0;

    internal static bool TryWriteSketchToDxf(
        ModelDoc2 modelDoc,
        Feature sketchFeature,
        string outputPath,
        out string error)
    {
      error = string.Empty;
      Sketch sketch = TryGetSketch(sketchFeature);
      if (sketch == null)
      {
        error = "Не удалось получить эскиз для записи DXF.";
        return false;
      }

      MathUtility mathUtility = TryGetMathUtility();
      if (mathUtility == null)
      {
        error = "SolidWorks MathUtility недоступен.";
        return false;
      }

      MathTransform toSketchTransform = null;
      try
      {
        toSketchTransform = sketch.ModelToSketchTransform;
      }
      catch (Exception ex)
      {
        error = "Не удалось получить преобразование эскиза: " + ex.Message;
        return false;
      }

      object[] segments = sketch.GetSketchSegments() as object[];
      if (segments == null || segments.Length == 0)
      {
        error = "Эскиз не содержит сегментов для экспорта.";
        return false;
      }

      var entityLines = new List<string>();
      int written = 0;
      for (int i = 0; i < segments.Length; i++)
      {
        var segment = segments[i] as SketchSegment;
        if (segment == null)
          continue;

        if (TryAppendSegmentEntities(
                segment,
                mathUtility,
                toSketchTransform,
                entityLines,
                ref written))
          continue;

        Logger.Info("Velum DXF sketch writer: skipped segment type=" + segment.GetType());
      }

      if (written <= 0)
      {
        error = "Не удалось преобразовать сегменты эскиза в сущности DXF.";
        return false;
      }

      try
      {
        string directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
          Directory.CreateDirectory(directory);

        File.WriteAllText(outputPath, BuildDxfFile(entityLines), Encoding.ASCII);
        Logger.Info("Velum DXF sketch writer OK: entities=" + written + " path=" + outputPath);
        return true;
      }
      catch (Exception ex)
      {
        error = ex.Message;
        return false;
      }
    }

    private static bool TryAppendSegmentEntities(
        SketchSegment segment,
        MathUtility mathUtility,
        MathTransform toSketchTransform,
        List<string> entityLines,
        ref int written)
    {
      int segmentType = segment.GetType();
      if (segmentType == (int)swSketchSegments_e.swSketchLINE)
        return TryAppendLine((SketchLine)segment, mathUtility, toSketchTransform, entityLines, ref written);

      if (segmentType == (int)swSketchSegments_e.swSketchARC)
        return TryAppendArc((SketchArc)segment, mathUtility, toSketchTransform, entityLines, ref written);

      return false;
    }

    private static bool TryAppendLine(
        SketchLine line,
        MathUtility mathUtility,
        MathTransform toSketchTransform,
        List<string> entityLines,
        ref int written)
    {
      if (!TryGetSketchPointMm(line.GetStartPoint2() as SketchPoint, mathUtility, toSketchTransform, out double x1, out double y1))
        return false;
      if (!TryGetSketchPointMm(line.GetEndPoint2() as SketchPoint, mathUtility, toSketchTransform, out double x2, out double y2))
        return false;

      AppendLine(entityLines, x1, y1, x2, y2);
      written++;
      return true;
    }

    private static bool TryAppendArc(
        SketchArc arc,
        MathUtility mathUtility,
        MathTransform toSketchTransform,
        List<string> entityLines,
        ref int written)
    {
      if (!TryGetSketchPointMm(arc.GetCenterPoint2() as SketchPoint, mathUtility, toSketchTransform, out double cx, out double cy))
        return false;

      double radius;
      try
      {
        radius = arc.GetRadius() * MetersToMillimeters;
      }
      catch
      {
        return false;
      }

      if (radius <= 0)
        return false;

      bool isCircle = false;
      try
      {
        isCircle = arc.IsCircle() != 0;
      }
      catch
      {
      }

      if (isCircle)
      {
        AppendCircle(entityLines, cx, cy, radius);
        written++;
        return true;
      }

      if (!TryGetSketchPointMm(arc.GetStartPoint2() as SketchPoint, mathUtility, toSketchTransform, out double sx, out double sy))
        return false;
      if (!TryGetSketchPointMm(arc.GetEndPoint2() as SketchPoint, mathUtility, toSketchTransform, out double ex, out double ey))
        return false;

      double startAngle = NormalizeDegrees(Math.Atan2(sy - cy, sx - cx) * 180.0 / Math.PI);
      double endAngle = NormalizeDegrees(Math.Atan2(ey - cy, ex - cx) * 180.0 / Math.PI);
      AppendArc(entityLines, cx, cy, radius, startAngle, endAngle);
      written++;
      return true;
    }

    private static bool TryGetSketchPointMm(
        SketchPoint sketchPoint,
        MathUtility mathUtility,
        MathTransform toSketchTransform,
        out double xMm,
        out double yMm)
    {
      xMm = yMm = 0;
      if (sketchPoint == null)
        return false;

      try
      {
        double x = sketchPoint.X;
        double y = sketchPoint.Y;
        double z = sketchPoint.Z;
        if (!TryTransformToSketchMm(mathUtility, toSketchTransform, x, y, z, out xMm, out yMm))
          return false;

        return true;
      }
      catch
      {
        return false;
      }
    }

    private static bool TryTransformToSketchMm(
        MathUtility mathUtility,
        MathTransform toSketchTransform,
        double x,
        double y,
        double z,
        out double xMm,
        out double yMm)
    {
      xMm = yMm = 0;
      if (mathUtility == null)
        return false;

      try
      {
        MathPoint mathPoint = mathUtility.CreatePoint(new[] { x, y, z }) as MathPoint;
        if (mathPoint == null)
          return false;

        if (toSketchTransform != null)
          mathPoint = mathPoint.MultiplyTransform(toSketchTransform) as MathPoint;
        if (mathPoint == null)
          return false;

        double[] data = mathPoint.ArrayData as double[];
        if (data == null || data.Length < 2)
          return false;

        xMm = data[0] * MetersToMillimeters;
        yMm = data[1] * MetersToMillimeters;
        return true;
      }
      catch
      {
        return false;
      }
    }

    private static string BuildDxfFile(List<string> entityLines)
    {
      var sb = new StringBuilder(entityLines.Count * 48 + 256);
      AppendPair(sb, 0, "SECTION");
      AppendPair(sb, 2, "HEADER");
      AppendPair(sb, 9, "$ACADVER");
      AppendPair(sb, 1, "AC1009");
      AppendPair(sb, 9, "$INSUNITS");
      AppendPair(sb, 70, "4");
      AppendPair(sb, 9, "$MEASUREMENT");
      AppendPair(sb, 70, "1");
      AppendPair(sb, 0, "ENDSEC");
      AppendPair(sb, 0, "SECTION");
      AppendPair(sb, 2, "ENTITIES");
      for (int i = 0; i < entityLines.Count; i++)
        sb.Append(entityLines[i]);
      AppendPair(sb, 0, "ENDSEC");
      AppendPair(sb, 0, "EOF");
      return sb.ToString();
    }

    private static void AppendLine(List<string> sink, double x1, double y1, double x2, double y2)
    {
      var sb = new StringBuilder(96);
      AppendPair(sb, 0, "LINE");
      AppendPair(sb, 8, "0");
      AppendPair(sb, 10, Format(x1));
      AppendPair(sb, 20, Format(y1));
      AppendPair(sb, 30, "0.0");
      AppendPair(sb, 11, Format(x2));
      AppendPair(sb, 21, Format(y2));
      AppendPair(sb, 31, "0.0");
      sink.Add(sb.ToString());
    }

    private static void AppendCircle(List<string> sink, double cx, double cy, double radius)
    {
      var sb = new StringBuilder(72);
      AppendPair(sb, 0, "CIRCLE");
      AppendPair(sb, 8, "0");
      AppendPair(sb, 10, Format(cx));
      AppendPair(sb, 20, Format(cy));
      AppendPair(sb, 30, "0.0");
      AppendPair(sb, 40, Format(radius));
      sink.Add(sb.ToString());
    }

    private static void AppendArc(
        List<string> sink,
        double cx,
        double cy,
        double radius,
        double startAngle,
        double endAngle)
    {
      var sb = new StringBuilder(120);
      AppendPair(sb, 0, "ARC");
      AppendPair(sb, 8, "0");
      AppendPair(sb, 10, Format(cx));
      AppendPair(sb, 20, Format(cy));
      AppendPair(sb, 30, "0.0");
      AppendPair(sb, 40, Format(radius));
      AppendPair(sb, 50, Format(startAngle));
      AppendPair(sb, 51, Format(endAngle));
      sink.Add(sb.ToString());
    }

    private static void AppendPair(StringBuilder sb, int code, string value)
    {
      sb.Append(code.ToString(CultureInfo.InvariantCulture));
      sb.Append('\n');
      sb.Append(value ?? string.Empty);
      sb.Append('\n');
    }

    private static string Format(double value)
    {
      return value.ToString("0.######", CultureInfo.InvariantCulture);
    }

    private static double NormalizeDegrees(double degrees)
    {
      degrees %= 360.0;
      if (degrees < 0)
        degrees += 360.0;
      return degrees;
    }

    private static Sketch TryGetSketch(Feature sketchFeature)
    {
      try
      {
        return sketchFeature?.GetSpecificFeature2() as Sketch;
      }
      catch
      {
        return null;
      }
    }

    private static MathUtility TryGetMathUtility()
    {
      try
      {
        SldWorks swApp = Marshal.GetActiveObject("SldWorks.Application") as SldWorks;
        return swApp?.GetMathUtility() as MathUtility;
      }
      catch
      {
        return null;
      }
    }
  }
}
