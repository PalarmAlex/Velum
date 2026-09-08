using System.Threading;
using System.Windows.Forms;
using ISIDA.Common;
using Velum.UI.ProductRegistry;
using Xarial.XCad.SolidWorks;

namespace Velum.UI
{
  /// <summary>Хост формы проблем целостности реестра.</summary>
  internal static class VelumProductRegistryProblemsFormHost
  {
    private static int _formOpen;

    internal static bool TryShow(ISwApplication swApp)
    {
      if (Interlocked.CompareExchange(ref _formOpen, 1, 0) != 0)
      {
        Logger.Info("Velum registry problems form skipped (already open)");
        return true;
      }

      try
      {
        using (var form = new VelumProductRegistryProblemsForm(swApp))
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
