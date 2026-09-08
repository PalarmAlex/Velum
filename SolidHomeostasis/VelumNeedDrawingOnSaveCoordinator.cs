using System;
using ISIDA.Common;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Velum.ReactiveCore.Export;

namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// При Save детали/сборки создаёт «Нужен чертеж = Yes», если свойства ещё нет.
  /// На детали также создаёт «Нужен dxf = No», если свойства ещё нет.
  /// </summary>
  internal static class VelumNeedDrawingOnSaveCoordinator
  {
    internal static void TryEnsureBeforeSaveIfEligible(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return;

      try
      {
        int type = modelDoc.GetType();
        if (type != (int)swDocumentTypes_e.swDocPART &&
            type != (int)swDocumentTypes_e.swDocASSEMBLY)
          return;
      }
      catch
      {
        return;
      }

      try
      {
        if (VelumDrawingPathPropertyHelper.EnsureNeedDrawingFlag(modelDoc, out string message))
        {
VelumDxfBatchDocumentHelper.TryEnsureNeedDxfDefaultIfPart(modelDoc);
          return;
        }

        if (!string.IsNullOrWhiteSpace(message) &&
            !string.Equals(message, "already_exists", StringComparison.OrdinalIgnoreCase))
        {
          Logger.Warning("Velum NeedDrawing ensure before save: " + message);
        }
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum NeedDrawing ensure before save: " + ex.Message);
      }

      VelumDxfBatchDocumentHelper.TryEnsureNeedDxfDefaultIfPart(modelDoc);
    }
  }
}
