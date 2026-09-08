using System.Threading;
using System.Windows.Forms;
using ISIDA.Common;
using Xarial.XCad.SolidWorks;

namespace Velum.UI
{
  /// <summary>Хост пакетной формы обновления свойств документов.</summary>
  internal static class VelumDocumentPropertyBatchFormHost
  {
    private static int _formOpen;

    internal static bool TryShow(ISwApplication swApp)
    {
      if (!VelumAdminAccess.TryRequireAdmin(null, VelumAdminAccess.FormDeniedMessage))
        return false;

      if (Interlocked.CompareExchange(ref _formOpen, 1, 0) != 0)
      {
        Logger.Info("Velum document property batch form skipped (already open)");
        return true;
      }

      try
      {
        using (var form = new VelumDocumentPropertyBatchForm(swApp))
          form.ShowDialog();

        return true;
      }
      finally
      {
        Interlocked.Exchange(ref _formOpen, 0);
      }
    }
  }
}
