using System;
using System.Threading;
using System.Windows.Forms;
using ISIDA.Common;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Velum.SolidHomeostasis;
using Velum.UI;

namespace Velum.ReactiveCore.Export
{
  /// <summary>Модальный диалог экспорта PDF и защита от повторного открытия.</summary>
  internal static class VelumPdfExportDialogHost
  {
    private static int _dialogOpen;

    internal static bool TryShowModal(ModelDoc2 modelDoc)
    {
      if (!VelumAdminAccess.TryRequireAdmin(null, VelumAdminAccess.FormDeniedMessage))
        return false;

      // Снимок до любых early-return: иначе ID у-рефлекса зависнет до следующего эпизода.
      int conditionedReflexId = VelumConditionedReflexForbidHelper.CaptureAndClearCurrentConditionedReflexId();

      if (Interlocked.CompareExchange(ref _dialogOpen, 1, 0) != 0)
      {
        Logger.Info("Velum PDF dialog skipped (already open)");
        return true;
      }

      try
      {
        if (modelDoc == null)
        {
          MessageBox.Show(
              "Нет активного документа SolidWorks.",
              "Экспорт PDF",
              MessageBoxButtons.OK,
              MessageBoxIcon.Warning);
          return true;
        }

        if (modelDoc.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
        {
          MessageBox.Show(
              "Экспорт PDF доступен только для чертежа.",
              "Экспорт PDF",
              MessageBoxButtons.OK,
              MessageBoxIcon.Information);
          return true;
        }

        if (!VelumPdfFileNameHelper.EnsureNeedPdfFlag(modelDoc, out string ensureMessage))
          Logger.Warning("Velum PDF dialog: не удалось задать «Нужен pdf»: " + ensureMessage);

        using (var dialog = new VelumPdfExportDialog(modelDoc, conditionedReflexId))
        {
          dialog.ShowDialog();
        }

        VelumSolidProbeRefreshPlanner.MarkExportDocumentationStale();
        return true;
      }
      finally
      {
        Interlocked.Exchange(ref _dialogOpen, 0);
      }
    }
  }
}
