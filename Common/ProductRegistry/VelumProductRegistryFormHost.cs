using System.Threading;
using System.Windows.Forms;
using ISIDA.Common;
using Velum.UI.ProductRegistry;
using Xarial.XCad.SolidWorks;

namespace Velum.UI
{
  /// <summary>Хост формы реестра изделий.</summary>
  internal static class VelumProductRegistryFormHost
  {
    private static int _formOpen;

    /// <summary>Форма реестра открыта (модально) — фоновый name-sync пропускается.</summary>
    internal static bool IsOpen => Volatile.Read(ref _formOpen) == 1;

    internal static bool TryShow(ISwApplication swApp, int? selectFolderId = null)
    {
      if (Interlocked.CompareExchange(ref _formOpen, 1, 0) != 0)
      {
        Logger.Info("Velum product registry form skipped (already open)");
        return true;
      }

      try
      {
        VelumProductRegistryIndexTrace.Mark("form.open.begin");
        using (var form = new VelumProductRegistryForm(swApp))
        {
          if (!form.TryPrepare(selectFolderId))
          {
            VelumProductRegistryIndexTrace.Mark("form.open.aborted_prepare");
            return true;
          }

          VelumProductRegistryIndexTrace.Mark("form.showdialog.begin");
          form.ShowDialog();
          VelumProductRegistryIndexTrace.Mark("form.showdialog.end");
        }

        // После закрытия: подхватить изменения JSON и сразу снять устаревшие
        // BrokenLink/MissingDrawing (не ждать heavy-тик / «Сохранить» в SW).
        VelumProductRegistryIntegrityScheduler.NotifyRegistryChanged();
        VelumProductRegistryIntegrityScheduler.RevalidateCachedItemProblems();
        return true;
      }
      finally
      {
        Interlocked.Exchange(ref _formOpen, 0);
      }
    }
  }
}
