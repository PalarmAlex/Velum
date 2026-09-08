using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using ISIDA.Common;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Velum.UI.AssemblyRegistry;
using Xarial.XCad.SolidWorks;

namespace Velum.UI
{
  /// <summary>Хост пакетной формы PDF.</summary>
  internal static class VelumPdfBatchDiagnosticsFormHost
  {
    private static int _formOpen;

    internal static bool TryShow(ISwApplication swApp)
    {
      if (!TryBuildMenuContext(swApp, out VelumPdfBomLaunchContext context))
        return false;
      return TryShowCore(swApp, context);
    }

    internal static bool TryShowFromAssemblyRegistry(ISwApplication swApp, VelumPdfBomLaunchContext context)
    {
      return TryShowCore(swApp, context);
    }

    private static bool TryShowCore(ISwApplication swApp, VelumPdfBomLaunchContext bomContext)
    {
      if (!VelumAdminAccess.TryRequireAdmin(null, VelumAdminAccess.FormDeniedMessage))
        return false;

      if (Interlocked.CompareExchange(ref _formOpen, 1, 0) != 0)
      {
        Logger.Info("Velum PDF batch form skipped (already open)");
        return true;
      }
      try
      {
        using (var form = new VelumPdfBatchDiagnosticsForm(swApp, bomContext))
          form.ShowDialog();

        return true;
      }
      finally
      {
        Interlocked.Exchange(ref _formOpen, 0);
      }
    }

    private static bool TryBuildMenuContext(
        ISwApplication swApp,
        out VelumPdfBomLaunchContext context)
    {
      context = null;
      ModelDoc2 active = swApp?.Sw?.IActiveDoc2 as ModelDoc2;
      if (active == null || active.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
      {
        MessageBox.Show(
            "Пакетный PDF доступен только при активной сборке.",
            "Пакетный PDF",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return false;
      }

      string assemblyFolder = Path.GetDirectoryName(active.GetPathName() ?? string.Empty) ?? string.Empty;
      List<VelumAssemblyRegistryComponent> components = CollectAssemblyModels(active as AssemblyDoc);
      VelumAssemblyRegistryBomDiagnostics.PdfResult result =
          VelumAssemblyRegistryBomDiagnostics.BuildUncheckedPdf(swApp, components);
      if (result.Rows.Count == 0)
      {
        MessageBox.Show(
            "Нет позиций для пакетного PDF\n" +
            "(состав пуст или все с «Нужен чертеж/pdf = Нет»).",
            "Пакетный PDF",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return false;
      }

      context = new VelumPdfBomLaunchContext
      {
        PdfExportFolder = string.Empty,
        AssemblyFolder = assemblyFolder,
        Rows = result.Rows,
        Components = components
      };
      return true;
    }

    private static List<VelumAssemblyRegistryComponent> CollectAssemblyModels(AssemblyDoc assembly)
    {
      var result = new List<VelumAssemblyRegistryComponent>();
      var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      if (assembly == null)
        return result;

      TryResolveLightweight(assembly);

      object[] topLevel = assembly.GetComponents(true) as object[];
      if (topLevel == null)
        return result;

      foreach (object obj in topLevel)
      {
        var comp = obj as Component2;
        if (comp == null)
          continue;

        VisitPdfComponent(comp, seen, result);
      }

      return result;
    }

    /// <summary>
    /// Рекурсивный обход дерева сборки: сбор всех вхождений деталей и сборок (Part/Assembly).
    /// Суб-сборки обходятся через GetChildren() — без открытия компонентов.
    /// </summary>
    private static void VisitPdfComponent(
        Component2 comp,
        HashSet<string> seen,
        List<VelumAssemblyRegistryComponent> result)
    {
      if (comp == null)
        return;

      try
      {
        if (comp.IsSuppressed() || comp.IsEnvelope())
          return;
      }
      catch
      {
        return;
      }

      string path = string.Empty;
      try
      {
        path = comp.GetPathName() ?? string.Empty;
      }
      catch
      {
        return;
      }
      if (string.IsNullOrWhiteSpace(path))
        return;

      if (!seen.Add(path))
        return;

      ModelDoc2 model = null;
      try
      {
        model = comp.GetModelDoc2() as ModelDoc2;
      }
      catch
      {
        return;
      }
      if (model == null)
        return;

      int type;
      try
      {
        type = model.GetType();
      }
      catch
      {
        return;
      }

      bool isPart = type == (int)swDocumentTypes_e.swDocPART;
      bool isAssembly = type == (int)swDocumentTypes_e.swDocASSEMBLY;
      if (!isPart && !isAssembly)
        return;

      result.Add(new VelumAssemblyRegistryComponent
      {
        FilePath = path,
        FileTitle = model.GetTitle() ?? string.Empty,
        Kind = isAssembly
            ? VelumAssemblyRegistryNodeKind.Assembly
            : VelumAssemblyRegistryNodeKind.Part,
        Quantity = 1
      });

      if (isAssembly)
      {
        object[] children = null;
        try
        {
          children = comp.GetChildren() as object[];
        }
        catch
        {
          children = null;
        }

        if (children != null)
        {
          foreach (object childObj in children)
          {
            VisitPdfComponent(childObj as Component2, seen, result);
          }
        }
      }
    }

    private static void TryResolveLightweight(AssemblyDoc assembly)
    {
      try
      {
        assembly.ResolveAllLightWeightComponents(false);
        var model = assembly as ModelDoc2;
        model?.EditRebuild3();
      }
      catch
      {
      }
    }
  }
}
