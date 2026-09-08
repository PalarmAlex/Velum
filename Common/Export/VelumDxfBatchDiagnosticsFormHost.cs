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
  /// <summary>Хост пакетной формы DXF.</summary>
  internal static class VelumDxfBatchDiagnosticsFormHost
  {
    private static int _formOpen;

    internal static bool TryShow(ISwApplication swApp)
    {
      if (!TryBuildMenuContext(swApp, out VelumDxfBomLaunchContext context))
        return false;
      return TryShowCore(swApp, context);
    }

    internal static bool TryShowFromAssemblyRegistry(ISwApplication swApp, VelumDxfBomLaunchContext context)
    {
      return TryShowCore(swApp, context);
    }

    private static bool TryShowCore(ISwApplication swApp, VelumDxfBomLaunchContext bomContext)
    {
      if (!VelumAdminAccess.TryRequireAdmin(null, VelumAdminAccess.FormDeniedMessage))
        return false;

      if (Interlocked.CompareExchange(ref _formOpen, 1, 0) != 0)
      {
        Logger.Info("Velum DXF batch form skipped (already open)");
        return true;
      }

      try
      {
        using (var form = new VelumDxfBatchDiagnosticsForm(swApp, bomContext))
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
        out VelumDxfBomLaunchContext context)
    {
      context = null;
      ModelDoc2 active = swApp?.Sw?.IActiveDoc2 as ModelDoc2;
      if (active == null || active.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
      {
        MessageBox.Show(
            "Пакетный DXF доступен только при активной сборке.",
            "Пакетный DXF",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return false;
      }

      string assemblyFolder = Path.GetDirectoryName(active.GetPathName() ?? string.Empty) ?? string.Empty;
      List<VelumAssemblyRegistryComponent> components = CollectAssemblyParts(active as AssemblyDoc);
      VelumAssemblyRegistryBomDiagnostics.DxfResult result =
          VelumAssemblyRegistryBomDiagnostics.BuildUncheckedDxf(swApp, components);
      if (result.Rows.Count == 0)
      {
        MessageBox.Show("Нет позиций с «Нужен dxf = Да».", "Пакетный DXF", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return false;
      }

      context = new VelumDxfBomLaunchContext
      {
        DxfExportFolder = string.Empty,
        AssemblyFolder = assemblyFolder,
        Rows = result.Rows,
        Components = components,
        EnableAssemblyQuantity = true
      };
      return true;
    }

    private static List<VelumAssemblyRegistryComponent> CollectAssemblyParts(AssemblyDoc assembly)
    {
      var rows = new Dictionary<string, VelumAssemblyRegistryComponent>(StringComparer.OrdinalIgnoreCase);
      if (assembly == null)
        return new List<VelumAssemblyRegistryComponent>();

      TryResolveLightweight(assembly);

      object[] topLevel = assembly.GetComponents(true) as object[];
      if (topLevel == null)
        return new List<VelumAssemblyRegistryComponent>();

      foreach (object obj in topLevel)
      {
        var comp = obj as Component2;
        if (comp == null)
          continue;

        VisitPartComponent(comp, rows);
      }
      return new List<VelumAssemblyRegistryComponent>(rows.Values);
    }

    /// <summary>
    /// Рекурсивный обход дерева сборки: сбор всех вхождений деталей (Part).
    /// Суб-сборки обходятся через GetChildren() — без открытия компонентов.
    /// </summary>
    private static void VisitPartComponent(Component2 comp, Dictionary<string, VelumAssemblyRegistryComponent> rows)
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

      string config = comp.ReferencedConfiguration ?? string.Empty;
      string key = path + "|" + config;

      if (isPart)
      {
        VelumAssemblyRegistryComponent row;
        if (!rows.TryGetValue(key, out row))
        {
          row = new VelumAssemblyRegistryComponent
          {
            FilePath = path,
            FileTitle = model.GetTitle() ?? string.Empty,
            ConfigurationName = config,
            Kind = VelumAssemblyRegistryNodeKind.Part,
            Quantity = 0
          };
          rows[key] = row;
        }
        row.Quantity++;
      }

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
            VisitPartComponent(childObj as Component2, rows);
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
