using System.Threading;
using System.Windows.Forms;
using ISIDA.Common;

namespace Velum.UI.Logs
{
  /// <summary>Хост формы просмотра логов агента.</summary>
  internal static class VelumLogsFormHost
  {
    private static int _formOpen;

    /// <summary>Форма логов открыта (модально).</summary>
    internal static bool IsOpen => Volatile.Read(ref _formOpen) == 1;

    /// <summary>Открыть форму логов; повторный вызов при открытой форме игнорируется.</summary>
    internal static bool TryShow(IWin32Window owner)
    {
      if (Interlocked.CompareExchange(ref _formOpen, 1, 0) != 0)
      {
        Logger.Info("Velum logs form skipped (already open)");
        return true;
      }

      try
      {
        using (var form = new VelumLogsForm(true))
        {
          if (owner != null)
            form.ShowDialog(owner);
          else
            form.ShowDialog();
        }
        return true;
      }
      finally
      {
        Interlocked.Exchange(ref _formOpen, 0);
      }
    }
  }
}
