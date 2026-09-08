using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using ISIDA.Common;
using Velum.SolidHomeostasis;
using Xarial.XCad.SolidWorks;

namespace Velum.UI
{
  /// <summary>Хост пакетной формы присвоения материалов.</summary>
  internal static class VelumMaterialBatchFormHost
  {
    private static int _formOpen;

    internal static bool TryShow(ISwApplication swApp)
    {
      return TryShowCore(swApp, null, closeOnSuccessfulApply: false, out _);
    }

    /// <summary>
    /// Показ с предзагруженными строками (вызов из реестра изделия).
    /// <paramref name="appliedSuccessfully"/> — true, если пользователь успешно применил материал.
    /// </summary>
    internal static bool TryShowWithRows(
        ISwApplication swApp,
        IWin32Window owner,
        IReadOnlyList<VelumMaterialBatchRow> seedRows,
        out bool appliedSuccessfully)
    {
      return TryShowCore(swApp, seedRows, closeOnSuccessfulApply: true, out appliedSuccessfully, owner);
    }

    private static bool TryShowCore(
        ISwApplication swApp,
        IReadOnlyList<VelumMaterialBatchRow> seedRows,
        bool closeOnSuccessfulApply,
        out bool appliedSuccessfully,
        IWin32Window owner = null)
    {
      appliedSuccessfully = false;

      if (!VelumAdminAccess.TryRequireAdmin(owner, VelumAdminAccess.FormDeniedMessage))
        return false;

      if (Interlocked.CompareExchange(ref _formOpen, 1, 0) != 0)
      {
        Logger.Info("Velum material batch form skipped (already open)");
        return true;
      }

      try
      {
        using (var form = new VelumMaterialBatchForm(swApp, seedRows, closeOnSuccessfulApply))
        {
          DialogResult result = owner != null
              ? form.ShowDialog(owner)
              : form.ShowDialog();
          appliedSuccessfully = result == DialogResult.OK;
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
