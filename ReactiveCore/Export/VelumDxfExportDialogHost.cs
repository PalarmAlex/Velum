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
  /// <summary>Модальный диалог экспорта DXF и защита от повторного открытия.</summary>
  internal static class VelumDxfExportDialogHost
  {
    private static int _dialogOpen;

    /// <summary>
    /// Открывает модальный диалог экспорта DXF.
    /// </summary>
    /// <param name="modelDoc">Активная деталь.</param>
    /// <param name="fromConditionedReflex">
    /// Рецепт исполняется по активации условного рефлекса. Только в этом случае
    /// кнопка «Запрет» активна — она сбрасывает крепость именно у-рефлекса.
    /// </param>
    /// <returns>true, если шаг обработан.</returns>
    internal static bool TryShowModal(ModelDoc2 modelDoc, bool fromConditionedReflex)
    {
      if (!VelumAdminAccess.TryRequireAdmin(null, VelumAdminAccess.FormDeniedMessage))
        return false;

      // Снимок до любых early-return: иначе ID у-рефлекса зависнет до следующего эпизода.
      int conditionedReflexId = VelumConditionedReflexForbidHelper.CaptureAndClearCurrentConditionedReflexId();

      // «Запрет» сбрасывает крепость у-рефлекса, поэтому при рецепте от б/у рефлекса
      // (или автоматизма) ID не отдаём форме — даже если он остался от прошлого эпизода.
      if (conditionedReflexId > 0 && !fromConditionedReflex)
      {
        Logger.Info(
            "Velum DXF dialog: ID у-рефлекса " + conditionedReflexId +
            " не применён (рецепт запущен не условным рефлексом)");
        conditionedReflexId = 0;
      }

      if (Interlocked.CompareExchange(ref _dialogOpen, 1, 0) != 0)

      {
        Logger.Info("Velum DXF dialog skipped (already open)");
        return true;
      }

      try
      {
        if (modelDoc == null)
        {
          MessageBox.Show(
              "Нет активного документа SolidWorks.",
              "Экспорт DXF",
              MessageBoxButtons.OK,
              MessageBoxIcon.Warning);
          return true;
        }

        if (modelDoc.GetType() != (int)swDocumentTypes_e.swDocPART)
        {
          MessageBox.Show(
              "Экспорт DXF доступен только для детали.",
              "Экспорт DXF",
              MessageBoxButtons.OK,
              MessageBoxIcon.Information);
          return true;
        }

        using (var dialog = new VelumDxfExportDialog(modelDoc, conditionedReflexId))
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
