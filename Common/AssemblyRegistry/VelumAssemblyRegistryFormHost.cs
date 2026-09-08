using System.Threading;
using System.Windows.Forms;
using ISIDA.Common;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Xarial.XCad.SolidWorks;

namespace Velum.UI
{
  /// <summary>Хост формы реестра изделия (доступна всем при активной сборке).</summary>
  internal static class VelumAssemblyRegistryFormHost
  {
    private static int _formOpen;

    internal static bool TryShow(ISwApplication swApp)
    {
      if (Interlocked.CompareExchange(ref _formOpen, 1, 0) != 0)
      {
        Logger.Info("Velum assembly registry form skipped (already open)");
        return true;
      }

      try
      {
        if (!TryEnsureActiveAssembly(swApp, out string error))
        {
          MessageBox.Show(
              string.IsNullOrEmpty(error) ? "Откройте сборку в активном окне." : error,
              "Реестр изделия",
              MessageBoxButtons.OK,
              MessageBoxIcon.Information);
          return false;
        }

        using (var form = new VelumAssemblyRegistryForm(swApp))
        {
          form.ShowDialog();
        }

        return true;
      }
      finally
      {
        Interlocked.Exchange(ref _formOpen, 0);
      }
    }

    private static bool TryEnsureActiveAssembly(ISwApplication swApp, out string error)
    {
      error = string.Empty;
      if (swApp?.Sw == null)
      {
        error = "SolidWorks недоступен";
        return false;
      }

      try
      {
        ModelDoc2 active = swApp.Sw.IActiveDoc2 as ModelDoc2;
        if (active == null)
        {
          error = "Нет активного документа. Откройте сборку.";
          return false;
        }

        if (active.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
        {
          error = "Реестр изделия доступен только при активной сборке.";
          return false;
        }

        return true;
      }
      catch (System.Exception ex)
      {
        error = ex.Message;
        return false;
      }
    }
  }
}
