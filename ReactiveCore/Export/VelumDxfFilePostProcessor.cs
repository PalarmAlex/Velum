using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Velum.Configuration;

namespace Velum.ReactiveCore.Export
{
  /// <summary>Постобработка DXF после экспорта (метрика, DIMLFAC) — из KmdEdit.</summary>
  internal static class VelumDxfFilePostProcessor
  {
    internal static void TryFixExportedFile(string dxfFilePath)
    {
      if (string.IsNullOrWhiteSpace(dxfFilePath) || !File.Exists(dxfFilePath))
        return;

      try
      {
        List<string> lines = File.ReadAllLines(dxfFilePath).ToList();

        int dimlfacIndex = lines.IndexOf("$DIMLFAC");
        if (dimlfacIndex != -1 && dimlfacIndex + 2 < lines.Count)
        {
          if (lines[dimlfacIndex + 1].Trim() == "40")
            lines[dimlfacIndex + 2] = "1.0";
        }

        int headerSectionIndex = lines.IndexOf("SECTION");
        if (headerSectionIndex != -1 &&
            headerSectionIndex + 2 < lines.Count &&
            lines[headerSectionIndex + 1].Trim() == "2" &&
            lines[headerSectionIndex + 2].Trim() == "HEADER")
        {
          int measurementIndex = lines.IndexOf("$MEASUREMENT");
          if (measurementIndex == -1)
          {
            int endSecIndex = lines.IndexOf("ENDSEC", headerSectionIndex);
            if (endSecIndex != -1)
            {
              lines.Insert(endSecIndex, "  0");
              lines.Insert(endSecIndex, "     1");
              lines.Insert(endSecIndex, " 70");
              lines.Insert(endSecIndex, "$MEASUREMENT");
              lines.Insert(endSecIndex, "  9");
            }
          }
          else if (measurementIndex + 2 < lines.Count &&
                   lines[measurementIndex + 1].Trim() == "70")
          {
            lines[measurementIndex + 2] = "     1";
          }
        }

        File.WriteAllLines(dxfFilePath, lines);
      }
      catch (Exception ex)
      {
        if (VelumAppConfig.SolidHomeostasisDebugLog)
          Debug.WriteLine("Velum DXF post-process failed: " + ex.Message);
      }
    }
  }
}
