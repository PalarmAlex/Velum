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
    /// кнопка «Запрет» активна — она штрафует путь активации (связь CS₁→CS₂ при
    /// срабатывании через сенсорный гейт, иначе крепость у-рефлекса).
    /// </param>
    /// <param name="confirmed">
    /// Подтвердил ли пользователь экспорт (диалог закрыт с <c>DialogResult.OK</c>).
    /// <c>false</c> — отмена, закрытие окна либо ранний выход без показа диалога.
    /// Используется для оценки шага цепочки рефлексов.
    /// </param>
    /// <returns>true, если шаг обработан.</returns>
    internal static bool TryShowModal(ModelDoc2 modelDoc, bool fromConditionedReflex, out bool confirmed)
    {
      confirmed = false;
      if (!VelumAdminAccess.TryRequireAdmin(null, VelumAdminAccess.FormDeniedMessage))
        return false;

      // Снимок до любых early-return: иначе эпизод зависнет до следующего эпизода.
      VelumConditionedReflexForbidHelper.VelumReflexEpisode episode =
          VelumConditionedReflexForbidHelper.CaptureAndClearCurrentReflexEpisode();

      // «Запрет» штрафует путь активации у-рефлекса, поэтому при рецепте от б/у рефлекса
      // (или автоматизма) эпизод не отдаём форме — даже если он остался от прошлого эпизода.
      if ((episode.ReflexId > 0 || episode.IsGateActivation) && !fromConditionedReflex)
      {
        Logger.Info(
            "Velum DXF dialog: эпизод у-рефлекса " + episode.ReflexId +
            " не применён (рецепт запущен не условным рефлексом)");
        episode = default(VelumConditionedReflexForbidHelper.VelumReflexEpisode);
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

        using (var dialog = new VelumDxfExportDialog(modelDoc, episode))
        {
          DialogResult dr = dialog.ShowDialog();
          confirmed = dr == DialogResult.OK;
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
