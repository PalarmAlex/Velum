using System.Threading;
using System.Windows.Forms;
using ISIDA.Common;
using Velum.UI.AssemblyRegistry;

namespace Velum.UI
{
  /// <summary>Хост диалога обмена BOM с 1C (защита от повторного открытия).</summary>
  internal static class VelumBomExchangeFormHost
  {
    private static int _formOpen;

    /// <summary>Показать диалог обмена модально. Повторный вызов при открытом окне игнорируется.</summary>
    internal static bool TryShow()
    {
      if (Interlocked.CompareExchange(ref _formOpen, 1, 0) != 0)
      {
        Logger.Info("Velum bom exchange form skipped (already open)");
        return true;
      }

      try
      {
        using (var form = new VelumBomExchangeForm())
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
