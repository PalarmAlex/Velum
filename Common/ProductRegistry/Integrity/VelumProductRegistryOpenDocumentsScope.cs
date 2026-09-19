using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Velum.SolidHomeostasis;
using Xarial.XCad.SolidWorks;

namespace Velum.UI.ProductRegistry
{
  /// <summary>
  /// Область скана целостности по активному документу SolidWorks.
  /// Пустой набор путей = полный скан реестра.
  /// Активная сборка → она + пути доступных компонентов
  /// (без suppressed / hidden / Envelope «Конверт»).
  /// При открытых: битые ссылки/чертежи — по имени документа (+ записи с тем же путём);
  /// «нет в реестре» — нет записи с тем же FilePath.
  /// </summary>
  internal static class VelumProductRegistryOpenDocumentsScope
  {
    /// <summary>
    /// Собирает нормализованные пути области скана.
    /// Нет активного документа → пусто (полный скан).
    /// Активная сборка → сборка + доступные компоненты (GetComponents,
    /// без suppressed / hidden / Envelope).
    /// Иначе → только активный документ.
    /// Вызывать с UI/COM-потока SolidWorks.
    /// </summary>
    internal static HashSet<string> TryCollectNormalizedOpenPaths()
    {
      var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      try
      {
        ModelDoc2 active = TryGetActiveModelDoc();
        if (active == null)
          return paths;

        TryAddDocPath(paths, active);

        if (IsAssemblyDocument(active))
          TryAddAvailableComponentPaths(active, paths);
      }
      catch
      {
        paths.Clear();
      }

      return paths;
    }

    /// <summary>
    /// true — активный документ SW является сборкой; тогда <paramref name="paths"/> =
    /// путь сборки + доступные компоненты (без suppressed / hidden / Envelope).
    /// Иначе false и пустой набор. Вызывать с UI/COM-потока SolidWorks.
    /// </summary>
    internal static bool TryCollectActiveAssemblyNormalizedPaths(out HashSet<string> paths)
    {
      paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      try
      {
        ModelDoc2 active = TryGetActiveModelDoc();
        if (active == null || !IsAssemblyDocument(active))
          return false;

        TryAddDocPath(paths, active);
        TryAddAvailableComponentPaths(active, paths);
        return paths.Count > 0;
      }
      catch
      {
        paths.Clear();
        return false;
      }
    }

    private static ModelDoc2 TryGetActiveModelDoc()
    {
      ISwApplication swApp = VelumSolidEnvironmentBridge.TryGetSolidWorksApplication();
      if (swApp?.Sw == null)
        return null;

      try
      {
        return swApp.Sw.IActiveDoc2 as ModelDoc2;
      }
      catch
      {
        return null;
      }
    }

    /// <summary>
    /// Лёгкая проверка наличия активного документа SW: один вызов <c>IActiveDoc2</c>,
    /// без обхода дерева сборки. true — активный документ есть; false — документов нет.
    /// При недоступности COM консервативно true (область open не сбрасывается).
    /// Вызывать с UI/COM-потока SolidWorks.
    /// </summary>
    internal static bool HasActiveDocument()
    {
      try
      {
        ISwApplication swApp = VelumSolidEnvironmentBridge.TryGetSolidWorksApplication();
        if (swApp?.Sw == null)
          return false;

        return swApp.Sw.IActiveDoc2 != null;
      }
      catch
      {
        return true;
      }
    }

    private static bool IsAssemblyDocument(ModelDoc2 doc)
    {
      if (doc == null)
        return false;

      try
      {
        if (doc is AssemblyDoc)
          return true;
      }
      catch
      {
      }

      try
      {
        return doc.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY;
      }
      catch
      {
        return false;
      }
    }

    private static void TryAddDocPath(HashSet<string> paths, ModelDoc2 doc)
    {
      if (paths == null || doc == null)
        return;

      string raw = null;
      try
      {
        raw = doc.GetPathName();
      }
      catch
      {
        raw = null;
      }

      string key = VelumProductRegistryStore.NormalizeFilePathKey(raw);
      if (!string.IsNullOrEmpty(key))
        paths.Add(key);
    }

    /// <summary>
    /// Все вхождения сборки (включая вложенные): GetComponents(false).
    /// Пропускает suppressed, Envelope («Конверт») и скрытые;
    /// lightweight оставляем — GetPathName обычно доступен
    /// (IsHidden для lightweight может врать — поэтому lightweight не режем по hidden).
    /// </summary>
    private static void TryAddAvailableComponentPaths(ModelDoc2 assemblyModel, HashSet<string> paths)
    {
      if (assemblyModel == null || paths == null)
        return;

      AssemblyDoc assemblyDoc = assemblyModel as AssemblyDoc;
      if (assemblyDoc == null)
        return;

      object[] components = null;
      try
      {
        components = assemblyDoc.GetComponents(false) as object[];
      }
      catch
      {
        components = null;
      }

      if (components == null || components.Length == 0)
        return;

      for (int i = 0; i < components.Length; i++)
      {
        Component2 comp = components[i] as Component2;
        if (comp == null)
          continue;

        if (ShouldSkipAssemblyComponent(comp))
          continue;

        string raw = null;
        try
        {
          raw = comp.GetPathName();
        }
        catch
        {
          raw = null;
        }

        string key = VelumProductRegistryStore.NormalizeFilePathKey(raw);
        if (!string.IsNullOrEmpty(key))
          paths.Add(key);
      }
    }

    /// <summary>
    /// true → не включать в область метрик 69/71 (suppressed / Envelope / hidden).
    /// </summary>
    private static bool ShouldSkipAssemblyComponent(Component2 comp)
    {
      if (comp == null)
        return true;

      try
      {
        if (comp.IsSuppressed())
          return true;
      }
      catch
      {
        return true;
      }

      try
      {
        if (comp.IsEnvelope())
          return true;
      }
      catch
      {
        return true;
      }

      // IsHidden(true/false) для lightweight часто даёт true — не отсекаем lightweight.
      bool lightweight = false;
      try
      {
        lightweight = comp.GetSuppression()
            == (int)swComponentSuppressionState_e.swComponentLightweight;
      }
      catch
      {
        lightweight = false;
      }

      if (!lightweight)
      {
        try
        {
          if (comp.IsHidden(false))
            return true;
        }
        catch
        {
          return true;
        }
      }

      return false;
    }

    /// <summary>Id записей с FilePath ∈ openPaths.</summary>
    internal static HashSet<int> ResolveItemIdsByPath(
        VelumProductRegistryStore store,
        HashSet<string> openPaths)
    {
      var ids = new HashSet<int>();
      if (store == null || openPaths == null || openPaths.Count == 0)
        return ids;

      IReadOnlyList<VelumProductItem> items = store.GetAllItems();
      for (int i = 0; i < items.Count; i++)
      {
        VelumProductItem item = items[i];
        if (item == null || item.Id <= 0)
          continue;

        string path = VelumProductRegistryStore.NormalizeFilePathKey(item.FilePath);
        if (string.IsNullOrEmpty(path))
          continue;
        if (openPaths.Contains(path))
          ids.Add(item.Id);
      }

      return ids;
    }

    /// <summary>
    /// Id записей, чей match-key (Designation или basename) совпадает
    /// с basename одного из открытых документов.
    /// </summary>
    internal static HashSet<int> ResolveItemIdsByDocumentName(
        VelumProductRegistryStore store,
        HashSet<string> openPaths)
    {
      var ids = new HashSet<int>();
      if (store == null || openPaths == null || openPaths.Count == 0)
        return ids;

      var nameKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (string path in openPaths)
      {
        string key = VelumProductRegistryMatchKey.FromFilePath(path);
        if (!string.IsNullOrEmpty(key))
          nameKeys.Add(key);
      }

      if (nameKeys.Count == 0)
        return ids;

      IReadOnlyList<VelumProductItem> items = store.GetAllItems();
      for (int i = 0; i < items.Count; i++)
      {
        VelumProductItem item = items[i];
        if (item == null || item.Id <= 0)
          continue;

        string itemKey = VelumProductRegistryMatchKey.FromItem(item);
        if (string.IsNullOrEmpty(itemKey))
          continue;
        if (nameKeys.Contains(itemKey))
          ids.Add(item.Id);
      }

      return ids;
    }

    /// <summary>
    /// Область проверки BrokenLink/MissingDrawing при открытых документах:
    /// записи по имени документа ∪ записи с тем же путём (если Designation ≠ basename).
    /// </summary>
    internal static HashSet<int> ResolveLinkCheckItemIds(
        VelumProductRegistryStore store,
        HashSet<string> openPaths)
    {
      HashSet<int> byName = ResolveItemIdsByDocumentName(store, openPaths);
      HashSet<int> byPath = ResolveItemIdsByPath(store, openPaths);
      if (byPath.Count == 0)
        return byName;
      if (byName.Count == 0)
        return byPath;

      foreach (int id in byPath)
        byName.Add(id);
      return byName;
    }

    internal static bool HasItemWithPath(VelumProductRegistryStore store, string filePath)
    {
      if (store == null)
        return false;
      return store.ContainsFilePath(filePath);
    }

    internal static List<VelumProductItem> FilterItems(
        VelumProductRegistryStore store,
        HashSet<int> scopedItemIds)
    {
      var list = new List<VelumProductItem>();
      if (store == null || scopedItemIds == null || scopedItemIds.Count == 0)
        return list;

      foreach (int id in scopedItemIds)
      {
        VelumProductItem item = store.GetItem(id);
        if (item != null && item.Id > 0)
          list.Add(item);
      }

      return list;
    }
  }
}
