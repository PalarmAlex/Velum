using System.Threading;
using System.Windows.Forms;
using ISIDA.Common;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Xarial.XCad.SolidWorks;

namespace Velum.UI
{
  /// <summary>Хост формы технических требований (доступен всем при активном чертеже).</summary>
  internal static class VelumTechRequirementsFormHost
  {
    private static int _formOpen;

    internal static bool TryShow(ISwApplication swApp)
    {
      if (Interlocked.CompareExchange(ref _formOpen, 1, 0) != 0)
      {
        Logger.Info("Velum tech requirements form skipped (already open)");
        return true;
      }

      try
      {
        if (!TryEnsureActiveDrawing(swApp, out string error))
        {
          MessageBox.Show(
              string.IsNullOrEmpty(error) ? "Откройте чертеж в активном окне." : error,
              "Тех. требования",
              MessageBoxButtons.OK,
              MessageBoxIcon.Information);
          return false;
        }

        using (var form = new VelumTechRequirementsForm(swApp))
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

    private static bool TryEnsureActiveDrawing(ISwApplication swApp, out string error)
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
          error = "Нет активного документа. Откройте чертеж.";
          return false;
        }

        if (active.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
        {
          error = "Тех. требования доступны только при активном чертеже.";
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
