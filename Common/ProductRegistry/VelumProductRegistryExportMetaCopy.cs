using System;

namespace Velum.UI.ProductRegistry
{
  /// <summary>Копирование зеркальных полей export-meta при UI-правках registry-only полей.</summary>
  internal static class VelumProductRegistryExportMetaCopy
  {
    internal static void CopyMirrorFields(VelumProductItem source, VelumProductItem target)
    {
      if (source == null || target == null)
        return;

      target.NeedDxf = source.NeedDxf;
      target.DrawingPath = source.DrawingPath;
      target.DxfPath = source.DxfPath;
      target.NeedPdf = source.NeedPdf;
      target.PdfPath = source.PdfPath;
target.PdfGeometryUpdateStamp = source.PdfGeometryUpdateStamp;
      target.PdfGeometryPendingStamp = source.PdfGeometryPendingStamp;
      target.ModelGeometryStamp = source.ModelGeometryStamp;
      target.PdfModelStampAtExport = source.PdfModelStampAtExport;
      target.ExportMetaConfigs = CloneConfigs(source.ExportMetaConfigs);
    }

    private static VelumProductExportMetaConfig[] CloneConfigs(VelumProductExportMetaConfig[] source)
    {
      if (source == null || source.Length == 0)
        return Array.Empty<VelumProductExportMetaConfig>();

      var copy = new VelumProductExportMetaConfig[source.Length];
      for (int i = 0; i < source.Length; i++)
      {
        VelumProductExportMetaConfig c = source[i];
        if (c == null)
        {
          copy[i] = null;
          continue;
        }

        copy[i] = new VelumProductExportMetaConfig
        {
          ConfigName = c.ConfigName,
          NeedDxf = c.NeedDxf,
          DxfFileName = c.DxfFileName,
          DxfProjectionView = c.DxfProjectionView,
          DxfFileFingerprint = c.DxfFileFingerprint,
          DxfGeometryUpdateStamp = c.DxfGeometryUpdateStamp,
          DxfGeometryPendingStamp = c.DxfGeometryPendingStamp
        };
      }

      return copy;
    }
  }
}
