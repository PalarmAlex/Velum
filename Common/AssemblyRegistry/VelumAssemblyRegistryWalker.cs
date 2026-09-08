using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Xarial.XCad.SolidWorks;

namespace Velum.UI.AssemblyRegistry
{
  /// <summary>
  /// Обход дерева вхождений головной сборки: счётчики, рёбра сборок, классификация.
  /// </summary>
  internal sealed class VelumAssemblyRegistryWalker
  {
    internal sealed class WalkProgress
    {
      internal int Current { get; set; }

      internal int Maximum { get; set; }

      internal string Status { get; set; }
    }

    private readonly ISwApplication _swApp;
    private readonly Func<bool> _isCancelRequested;
    private readonly Action<WalkProgress> _onProgress;

    internal VelumAssemblyRegistryWalker(
        ISwApplication swApp,
        Func<bool> isCancelRequested,
        Action<WalkProgress> onProgress)
    {
      _swApp = swApp;
      _isCancelRequested = isCancelRequested ?? (() => false);
      _onProgress = onProgress;
    }

    /// <summary>
    /// false — пользователь отменил resolve lightweight или Стоп; graph очищен вызывающим.
    /// </summary>
    internal bool TryBuild(out VelumAssemblyRegistryGraph graph, out string error)
    {
      graph = new VelumAssemblyRegistryGraph();
      error = string.Empty;

      if (_swApp?.Sw == null)
      {
        error = "SolidWorks недоступен";
        return false;
      }

      ModelDoc2 active = null;
      try
      {
        active = _swApp.Sw.IActiveDoc2 as ModelDoc2;
      }
      catch (Exception ex)
      {
        error = ex.Message;
        return false;
      }

      if (active == null)
      {
        error = "Нет активного документа";
        return false;
      }

      if (active.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
      {
        error = "Активный документ не является сборкой";
        return false;
      }

      AssemblyDoc assemblyDoc = active as AssemblyDoc;
      if (assemblyDoc == null)
      {
        error = "Не удалось получить AssemblyDoc";
        return false;
      }

      if (!TryResolveLightweight(active, assemblyDoc, out error))
        return false;

      if (IsCancel())
        return false;

      string rootPath = string.Empty;
      try
      {
        rootPath = active.GetPathName() ?? string.Empty;
      }
      catch
      {
        rootPath = string.Empty;
      }

      string rootConfig = string.Empty;
      try
      {
        SolidWorks.Interop.sldworks.Configuration activeConfig =
            active.GetActiveConfiguration() as SolidWorks.Interop.sldworks.Configuration;
        rootConfig = activeConfig?.Name ?? string.Empty;
      }
      catch
      {
        rootConfig = string.Empty;
      }

      graph.RootAssemblyPath = rootPath;
      graph.RootConfigurationName = rootConfig;

      int estimated = EstimateComponentCount(assemblyDoc);
      Report(0, Math.Max(estimated, 1), "Обход состава…");

      object[] topLevel = null;
      try
      {
        // Только верхний уровень: вложенные обходятся через GetChildren.
        // GetComponents(false) уже возвращает все вхождения — при рекурсии Qty задваивался.
        topLevel = assemblyDoc.GetComponents(true) as object[];
      }
      catch (Exception ex)
      {
        error = "GetComponents: " + ex.Message;
        return false;
      }

      if (topLevel == null || topLevel.Length == 0)
      {
        Report(1, 1, "Состав пуст");
        return true;
      }

      int visited = 0;
      foreach (object obj in topLevel)
      {
        if (IsCancel())
          return false;

        Component2 comp = obj as Component2;
        if (comp == null)
          continue;

        VisitComponent(comp, parentAssemblyIdentity: null, graph, ref visited, estimated);
      }

      if (IsCancel())
        return false;

      RecalculateTotals(graph);
      Report(Math.Max(estimated, visited), Math.Max(estimated, visited), "Готово");
      return true;
    }

    private static void RecalculateTotals(VelumAssemblyRegistryGraph graph)
    {
      foreach (VelumAssemblyRegistryComponent item in graph.Components.Values)
      {
        if (!item.PropertiesLoaded)
          continue;

        // FillProperties вызывается при Quantity=0; кэш Quantity/Кол-во нужно обновить после подсчёта.
        SyncQuantityCache(item);

        item.TotalMass = MultiplyForTotal(item.Quantity, item.Mass);
        item.TotalLength = MultiplyForTotal(item.Quantity, item.Length);
      }
    }

    private static void SyncQuantityCache(VelumAssemblyRegistryComponent item)
    {
      if (item?.PropertyValues == null)
        return;

      string qty = item.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture);
      item.PropertyValues["Quantity"] = qty;
      item.PropertyValues["Кол-во"] = qty;
    }

    private static string MultiplyForTotal(int quantity, string rawValue)
    {
      if (quantity <= 0)
        return string.Empty;
      if (string.IsNullOrWhiteSpace(rawValue) ||
          string.Equals(rawValue, VelumAssemblyRegistryPropertyReader.MissingMarker, StringComparison.Ordinal))
        return string.Empty;

      double number;
      if (!Velum.ReactiveCore.VelumBlankSizeProperties.TryParseNumber(rawValue, out number))
        return string.Empty;

      double total = number * quantity;
      double rounded = Math.Round(total, 3, MidpointRounding.AwayFromZero);
      long asInt = (long)Math.Round(rounded, MidpointRounding.AwayFromZero);
      if (Math.Abs(rounded - asInt) < 1e-9)
        return asInt.ToString(System.Globalization.CultureInfo.InvariantCulture);

      return rounded.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }

    private void VisitComponent(
        Component2 comp,
        string parentAssemblyIdentity,
        VelumAssemblyRegistryGraph graph,
        ref int visited,
        int estimated)
    {
      if (comp == null || IsCancel())
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
        path = string.Empty;
      }

      if (string.IsNullOrWhiteSpace(path))
        return;

      string configName = string.Empty;
      try
      {
        configName = comp.ReferencedConfiguration ?? string.Empty;
      }
      catch
      {
        configName = string.Empty;
      }

      string identity = VelumAssemblyRegistryPropertyReader.BuildIdentity(path, configName);
      string fileTitle = VelumAssemblyRegistryPropertyReader.FileTitleFromPath(path);

      ModelDoc2 modelDoc = null;
      try
      {
        modelDoc = comp.GetModelDoc2() as ModelDoc2;
      }
      catch
      {
        modelDoc = null;
      }

      if (modelDoc == null)
        return;

      int docType;
      try
      {
        docType = modelDoc.GetType();
      }
      catch
      {
        return;
      }

      bool isAssembly = docType == (int)swDocumentTypes_e.swDocASSEMBLY;
      bool isPart = docType == (int)swDocumentTypes_e.swDocPART;
      if (!isAssembly && !isPart)
        return;

      VelumAssemblyRegistryComponent item;
      if (!graph.Components.TryGetValue(identity, out item))
      {
        string section = VelumAssemblyRegistryPropertyReader.ReadSectionRaw(modelDoc, configName);
        VelumAssemblyRegistryNodeKind kind;
        string[] folderSegments;
        VelumAssemblyRegistrySectionPath.Classify(
            isAssembly,
            section,
            out kind,
            out folderSegments);

        item = new VelumAssemblyRegistryComponent
        {
          Identity = identity,
          FilePath = path,
          FileTitle = fileTitle,
          ConfigurationName = configName,
          Kind = kind,
          FolderSegments = folderSegments ?? Array.Empty<string>(),
          Quantity = 0
        };

        VelumAssemblyRegistryPropertyReader.FillProperties(item, modelDoc);
        graph.Components[identity] = item;
      }

      item.Quantity++;
      visited++;
      Report(visited, Math.Max(estimated, visited), fileTitle);

      if (isAssembly)
      {
        if (!string.IsNullOrEmpty(parentAssemblyIdentity))
        {
          HashSet<string> kids;
          if (!graph.AssemblyChildren.TryGetValue(parentAssemblyIdentity, out kids))
          {
            kids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            graph.AssemblyChildren[parentAssemblyIdentity] = kids;
          }

          kids.Add(identity);
        }

        object[] children = null;
        try
        {
          children = comp.GetChildren() as object[];
        }
        catch
        {
          children = null;
        }

        if (children == null)
          return;

        foreach (object childObj in children)
        {
          if (IsCancel())
            return;

          VisitComponent(childObj as Component2, identity, graph, ref visited, estimated);
        }
      }
    }

    private bool TryResolveLightweight(ModelDoc2 modelDoc, AssemblyDoc assemblyDoc, out string error)
    {
      error = string.Empty;
      int lightweightCount = 0;
      try
      {
        object[] all = assemblyDoc.GetComponents(true) as object[];
        if (all != null)
        {
          foreach (object obj in all)
          {
            Component2 comp = obj as Component2;
            if (comp == null)
              continue;
            try
            {
              if (comp.GetSuppression() == (int)swComponentSuppressionState_e.swComponentLightweight)
                lightweightCount++;
            }
            catch
            {
            }
          }
        }
      }
      catch (Exception ex)
      {
        error = "Проверка lightweight: " + ex.Message;
        return false;
      }

      if (lightweightCount > 10)
      {
        var result = System.Windows.Forms.MessageBox.Show(
            "В сборке найдено " + lightweightCount +
            " сокращённых компонентов. Их нужно решить для чтения свойств.\n" +
            "Решение всех компонентов может занять время.\n\n" +
            "Решить компоненты?",
            "Реестр изделия",
            System.Windows.Forms.MessageBoxButtons.YesNo,
            System.Windows.Forms.MessageBoxIcon.Warning);

        if (result != System.Windows.Forms.DialogResult.Yes)
        {
          error = "Решение сокращённых компонентов отменено";
          return false;
        }
      }

      if (lightweightCount > 0)
      {
        try
        {
          Report(0, 1, "Решение сокращённых компонентов…");
          assemblyDoc.ResolveAllLightWeightComponents(false);
          modelDoc.EditRebuild3();
        }
        catch (Exception ex)
        {
          error = "ResolveAllLightWeightComponents: " + ex.Message;
          return false;
        }
      }

      return true;
    }

    private static int EstimateComponentCount(AssemblyDoc assemblyDoc)
    {
      try
      {
        object[] all = assemblyDoc.GetComponents(false) as object[];
        return all == null ? 1 : Math.Max(all.Length, 1);
      }
      catch
      {
        return 1;
      }
    }

    private bool IsCancel()
    {
      return _isCancelRequested();
    }

    private void Report(int current, int maximum, string status)
    {
      if (_onProgress == null)
        return;

      _onProgress(new WalkProgress
      {
        Current = current,
        Maximum = Math.Max(maximum, 1),
        Status = status ?? string.Empty
      });
    }
  }
}
