using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Xarial.XCad.SolidWorks;

namespace Velum.UI.ProductRegistry
{
  /// <summary>
  /// Снимок открытых документов SW и загруженных компонентов сборок:
  /// один проход, путь → «Наименование» + ModelDoc. Без OpenDoc.
  /// </summary>
  internal sealed class VelumProductRegistryOpenPropertyCache
  {
    private sealed class Entry
    {
      public string Name;
      public ModelDoc2 Doc;
    }

    private readonly Dictionary<string, Entry> _byPath =
        new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _sessionPaths =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    internal int Count
    {
      get { return _byPath.Count; }
    }

    internal bool Contains(string filePath)
    {
      string path = VelumProductRegistryStore.NormalizeFilePathKey(filePath);
      return path.Length > 0 && _byPath.ContainsKey(path);
    }

    /// <summary>
    /// Путь есть в сессии SW (открытый документ или компонент сборки),
    /// даже если ModelDoc недоступен (lightweight).
    /// </summary>
    internal bool IsSessionPath(string filePath)
    {
      string path = VelumProductRegistryStore.NormalizeFilePathKey(filePath);
      return path.Length > 0 && _sessionPaths.Contains(path);
    }

    internal static VelumProductRegistryOpenPropertyCache Collect(ISwApplication swApp)
    {
      var cache = new VelumProductRegistryOpenPropertyCache();
      if (swApp?.Sw == null)
        return cache;

      try
      {
        ModelDoc2 doc = swApp.Sw.GetFirstDocument() as ModelDoc2;
        while (doc != null)
        {
          cache.TryAdd(doc);
          cache.TryAddAssemblyComponents(doc);
          ModelDoc2 next = null;
          try
          {
            next = doc.GetNext() as ModelDoc2;
          }
          catch
          {
            next = null;
          }

          doc = next;
        }
      }
      catch
      {
      }

      try
      {
        ModelDoc2 active = swApp.Sw.IActiveDoc2 as ModelDoc2;
        cache.TryAdd(active);
        cache.TryAddAssemblyComponents(active);
      }
      catch
      {
      }

      return cache;
    }

    /// <summary>
    /// Применяет снимок к записи. false — пути нет среди открытых ModelDoc.
    /// </summary>
    internal bool TryApplyToItem(
        VelumProductRegistryStore store,
        VelumProductItem item,
        bool persist,
        out VelumProductRegistryNameSyncOutcome nameOutcome,
        out VelumProductRegistryExportMetaSyncOutcome metaOutcome,
        out string metaError)
    {
      nameOutcome = VelumProductRegistryNameSyncOutcome.Unsupported;
      metaOutcome = VelumProductRegistryExportMetaSyncOutcome.Unsupported;
      metaError = string.Empty;
      if (store == null || item == null)
        return false;

      string path = VelumProductRegistryStore.NormalizeFilePathKey(item.FilePath);
      Entry entry;
      if (string.IsNullOrEmpty(path) || !_byPath.TryGetValue(path, out entry) || entry == null)
        return false;

      if (VelumProductRegistryNameSyncHelper.IsNameSyncSupported(item.FilePath))
        nameOutcome = VelumProductRegistryNameSync.TryApplySwName(store, item, entry.Name, persist);

      if (VelumProductRegistryExportMetaSync.IsExportMetaSyncSupported(item.FilePath)
          && entry.Doc != null)
      {
        metaOutcome = VelumProductRegistryExportMetaSync.TrySyncOpenDocument(
            store,
            path,
            entry.Doc,
            persist,
            out metaError);
      }

      return true;
    }

    /// <summary>
    /// Fallback: один OpenDoc на запись, затем наименование и зеркало экспорта.
    /// </summary>
    internal static void SyncAllowOpen(
        VelumProductRegistryStore store,
        ISwApplication swApp,
        VelumProductItem item,
        bool persist,
        out VelumProductRegistryNameSyncOutcome nameOutcome,
        out string nameError,
        out VelumProductRegistryExportMetaSyncOutcome metaOutcome,
        out string metaError)
    {
      nameOutcome = VelumProductRegistryNameSyncOutcome.Unsupported;
      nameError = string.Empty;
      metaOutcome = VelumProductRegistryExportMetaSyncOutcome.Unsupported;
      metaError = string.Empty;
      if (store == null || item == null)
      {
        nameOutcome = VelumProductRegistryNameSyncOutcome.Failed;
        nameError = "item/store null";
        metaOutcome = VelumProductRegistryExportMetaSyncOutcome.Failed;
        metaError = nameError;
        return;
      }

      bool needName = VelumProductRegistryNameSyncHelper.IsNameSyncSupported(item.FilePath);
      bool needMeta = VelumProductRegistryExportMetaSync.IsExportMetaSyncSupported(item.FilePath);
      if (!needName && !needMeta)
        return;

      ModelDoc2 modelDoc;
      bool openedByUs;
      string openError;
      if (!VelumProductRegistryNameSyncHelper.TryGetOrOpenDocument(
              swApp,
              item.FilePath,
              out modelDoc,
              out openedByUs,
              out openError))
      {
        if (needName)
        {
          nameOutcome = VelumProductRegistryNameSyncOutcome.Failed;
          nameError = openError;
        }

        if (needMeta)
        {
          metaOutcome = VelumProductRegistryExportMetaSyncOutcome.Failed;
          metaError = openError;
        }

        return;
      }

      try
      {
        if (needName)
        {
          string swName = VelumProductRegistryNameSyncHelper.ReadNameProperty(modelDoc);
          nameOutcome = VelumProductRegistryNameSync.TryApplySwName(store, item, swName, persist);
        }

        if (needMeta)
        {
          metaOutcome = VelumProductRegistryExportMetaSync.TrySyncOpenDocument(
              store,
              item.FilePath,
              modelDoc,
              persist,
              out metaError);
        }
      }
      catch (Exception ex)
      {
        if (needName && nameOutcome == VelumProductRegistryNameSyncOutcome.Unsupported)
        {
          nameOutcome = VelumProductRegistryNameSyncOutcome.Failed;
          nameError = ex.Message;
        }

        if (needMeta && metaOutcome == VelumProductRegistryExportMetaSyncOutcome.Unsupported)
        {
          metaOutcome = VelumProductRegistryExportMetaSyncOutcome.Failed;
          metaError = ex.Message;
        }
      }
      finally
      {
        VelumProductRegistryNameSyncHelper.TryCloseIfOpened(swApp, modelDoc, openedByUs);
      }
    }

    private void TryAdd(ModelDoc2 doc)
    {
      if (doc == null)
        return;

      string path;
      try
      {
        path = VelumProductRegistryStore.NormalizeFilePathKey(doc.GetPathName());
      }
      catch
      {
        return;
      }

      if (string.IsNullOrEmpty(path) || _byPath.ContainsKey(path))
        return;

      _sessionPaths.Add(path);

      string name = string.Empty;
      try
      {
        name = VelumProductRegistryNameSyncHelper.ReadNameProperty(doc);
      }
      catch
      {
        name = string.Empty;
      }

      _byPath[path] = new Entry
      {
        Name = name ?? string.Empty,
        Doc = doc
      };
    }

    private void TryAddAssemblyComponents(ModelDoc2 assemblyModel)
    {
      if (assemblyModel == null)
        return;

      AssemblyDoc assemblyDoc = assemblyModel as AssemblyDoc;
      if (assemblyDoc == null)
      {
        try
        {
          if (assemblyModel.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            return;
        }
        catch
        {
          return;
        }

        assemblyDoc = assemblyModel as AssemblyDoc;
        if (assemblyDoc == null)
          return;
      }

      object[] components = null;
      try
      {
        components = assemblyDoc.GetComponents(false) as object[];
      }
      catch
      {
        return;
      }

      if (components == null || components.Length == 0)
        return;

      for (int i = 0; i < components.Length; i++)
      {
        Component2 comp = components[i] as Component2;
        if (comp == null)
          continue;

        try
        {
          string compPath = VelumProductRegistryStore.NormalizeFilePathKey(comp.GetPathName());
          if (!string.IsNullOrEmpty(compPath))
            _sessionPaths.Add(compPath);
        }
        catch
        {
        }

        ModelDoc2 model = null;
        try
        {
          model = comp.GetModelDoc2() as ModelDoc2;
        }
        catch
        {
          model = null;
        }

        if (model != null)
          TryAdd(model);
      }
    }
  }
}
