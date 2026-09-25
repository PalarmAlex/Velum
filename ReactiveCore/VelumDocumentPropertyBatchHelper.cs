using System;
using System.Collections.Generic;
using System.IO;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Velum.ReactiveCore.Export;
using Velum.SolidHomeostasis;
using Xarial.XCad.SolidWorks;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Куда создавать свойство, если его нет ни в конфигурации, ни на главной вкладке.
  /// </summary>
  internal enum VelumDocumentPropertyCreateTarget
  {
    /// <summary>Главная вкладка свойств документа («Настройка»).</summary>
    DocumentSettings = 0,

    /// <summary>Все конфигурации документа.</summary>
    AllConfigurations = 1
  }

  /// <summary>
  /// Сбор документов и запись свойств для пакетной формы обновления свойств.
  /// </summary>
  internal static class VelumDocumentPropertyBatchHelper
  {
    internal const SearchOption CatalogSearchOption = SearchOption.AllDirectories;

    internal sealed class PropertyPair
    {
      internal string Name { get; set; }

      internal string Value { get; set; }
    }

    /// <summary>
    /// Уникальные детали и сборки из активной сборки (GetComponents, без suppressed / Envelope / hidden).
    /// Корневая сборка включается в список сборок. По одной строке на конфигурацию.
    /// </summary>
    internal static void CollectFromActiveAssembly(
        ISwApplication swApp,
        List<VelumDocumentPropertyBatchRow> parts,
        List<VelumDocumentPropertyBatchRow> assemblies)
    {
      if (parts == null || assemblies == null)
        return;

      parts.Clear();
      assemblies.Clear();

      ModelDoc2 active = TryGetActiveModel(swApp);
      if (active == null || !IsAssembly(active))
        return;

      HashSet<string> keepOpen = CollectOpenDocumentPaths(swApp);
      var partPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      var asmPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

      string rootPath = TryNormalizePath(TryGetPathName(active));
      if (!string.IsNullOrEmpty(rootPath))
      {
        asmPaths.Add(rootPath);
        AppendRowsForDocument(active, rootPath, isAssembly: true, assemblies);
      }

      AssemblyDoc assemblyDoc = active as AssemblyDoc;
      if (assemblyDoc == null)
      {
        parts.Sort(CompareRows);
        assemblies.Sort(CompareRows);
        return;
      }

      object[] components = null;
      try
      {
        components = assemblyDoc.GetComponents(false) as object[];
      }
      catch
      {
        components = null;
      }

      if (components == null)
      {
        parts.Sort(CompareRows);
        assemblies.Sort(CompareRows);
        return;
      }

      for (int i = 0; i < components.Length; i++)
      {
        Component2 comp = components[i] as Component2;
        if (comp == null || ShouldSkipComponent(comp))
          continue;

        string path = TryNormalizePath(TryGetComponentPath(comp));
        if (string.IsNullOrEmpty(path))
          continue;

        string ext = Path.GetExtension(path) ?? string.Empty;
        bool isAsm = string.Equals(ext, ".sldasm", StringComparison.OrdinalIgnoreCase);
        bool isPart = string.Equals(ext, ".sldprt", StringComparison.OrdinalIgnoreCase);
        if (!isAsm && !isPart)
          continue;

        HashSet<string> pathSet = isAsm ? asmPaths : partPaths;
        if (!pathSet.Add(path))
          continue;

        List<VelumDocumentPropertyBatchRow> target = isAsm ? assemblies : parts;
        AppendRowsOpeningIfNeeded(swApp, path, isAsm, target, keepOpen, comp);
      }

      parts.Sort(CompareRows);
      assemblies.Sort(CompareRows);
    }

    /// <summary>Только активная деталь в список деталей (по строке на конфигурацию).</summary>
    internal static void CollectFromActivePart(
        ISwApplication swApp,
        List<VelumDocumentPropertyBatchRow> parts,
        List<VelumDocumentPropertyBatchRow> assemblies)
    {
      if (parts == null || assemblies == null)
        return;

      parts.Clear();
      assemblies.Clear();

      ModelDoc2 active = TryGetActiveModel(swApp);
      if (active == null || IsAssembly(active))
        return;

      if (active.GetType() != (int)swDocumentTypes_e.swDocPART)
        return;

      string path = TryNormalizePath(TryGetPathName(active));
      if (string.IsNullOrEmpty(path))
        return;

      AppendRowsForDocument(active, path, isAssembly: false, parts);
    }

    /// <summary>
    /// Рекурсивный обход каталога: *.sldprt → детали, *.sldasm → сборки.
    /// По одной строке на конфигурацию документа.
    /// </summary>
    internal static void CollectFromCatalogFolder(
        ISwApplication swApp,
        string catalogFolder,
        List<VelumDocumentPropertyBatchRow> parts,
        List<VelumDocumentPropertyBatchRow> assemblies,
        Func<bool> isCancelled = null,
        Action<int, int, string> onProgress = null)
    {
      if (parts == null || assemblies == null)
        return;

      parts.Clear();
      assemblies.Clear();

      string root = (catalogFolder ?? string.Empty).Trim();
      if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        return;

      var partPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      var asmPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

      CollectFiles(root, "*.sldprt", partPaths, isCancelled);
      if (isCancelled != null && isCancelled())
        return;

      CollectFiles(root, "*.sldasm", asmPaths, isCancelled);
      if (isCancelled != null && isCancelled())
        return;

      HashSet<string> keepOpen = CollectOpenDocumentPaths(swApp);
      var all = new List<KeyValuePair<string, bool>>(partPaths.Count + asmPaths.Count);
      foreach (string path in partPaths)
        all.Add(new KeyValuePair<string, bool>(path, false));
      foreach (string path in asmPaths)
        all.Add(new KeyValuePair<string, bool>(path, true));

      for (int i = 0; i < all.Count; i++)
      {
        if (isCancelled != null && isCancelled())
          break;

        string path = all[i].Key;
        bool isAssembly = all[i].Value;
        if (onProgress != null)
          onProgress(i + 1, all.Count, Path.GetFileName(path));

        List<VelumDocumentPropertyBatchRow> target = isAssembly ? assemblies : parts;
        AppendRowsOpeningIfNeeded(swApp, path, isAssembly, target, keepOpen, component: null);
      }

      parts.Sort(CompareRows);
      assemblies.Sort(CompareRows);
    }

    internal static HashSet<string> CollectOpenDocumentPaths(ISwApplication swApp)
    {
      var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      if (swApp?.Sw == null)
        return result;

      try
      {
        object docsObj = swApp.Sw.GetDocuments();
        object[] docs = docsObj as object[];
        if (docs == null)
          return result;

        for (int i = 0; i < docs.Length; i++)
        {
          ModelDoc2 doc = docs[i] as ModelDoc2;
          string path = TryNormalizePath(TryGetPathName(doc));
          if (!string.IsNullOrEmpty(path))
            result.Add(path);
        }
      }
      catch
      {
      }

      return result;
    }

    internal static ModelDoc2 TryOpenSilent(
        ISwApplication swApp,
        string filePath,
        bool isAssembly,
        out string error)
    {
      error = string.Empty;
      string path = TryNormalizePath(filePath);
      if (string.IsNullOrEmpty(path) || swApp?.Sw == null)
      {
        error = "Нет пути или SolidWorks";
        return null;
      }

      ModelDoc2 existing = TryFindOpenByPath(swApp, path);
      if (existing != null)
        return existing;

      int openErrors = 0;
      int warnings = 0;
      ModelDoc2 modelDoc = null;
      try
      {
        modelDoc = swApp.Sw.OpenDoc6(
            path,
            isAssembly
                ? (int)swDocumentTypes_e.swDocASSEMBLY
                : (int)swDocumentTypes_e.swDocPART,
            (int)(swOpenDocOptions_e.swOpenDocOptions_Silent |
                  swOpenDocOptions_e.swOpenDocOptions_LoadModel),
            string.Empty,
            ref openErrors,
            ref warnings) as ModelDoc2;
      }
      catch (Exception ex)
      {
        error = ex.Message;
        return null;
      }

      if (modelDoc == null)
      {
        error = "OpenDoc6 errors=" + openErrors + " warnings=" + warnings;
        return TryFindOpenByPath(swApp, path);
      }

      return modelDoc;
    }

    internal static void TryReleaseAfterBatch(
        ISwApplication swApp,
        ModelDoc2 modelDoc,
        string filePath,
        bool persistChanges,
        ISet<string> keepOpenPaths)
    {
      if (modelDoc == null)
        return;

      try
      {
        if (persistChanges)
          TrySaveSilent(modelDoc);

        string normalized = TryNormalizePath(filePath);
        if (!string.IsNullOrWhiteSpace(normalized) &&
            keepOpenPaths != null &&
            keepOpenPaths.Contains(normalized))
          return;

        string title = modelDoc.GetTitle();
        if (!string.IsNullOrWhiteSpace(title) && swApp?.Sw != null)
          swApp.Sw.CloseDoc(title);
      }
      catch
      {
      }
    }

    internal static bool TrySaveSilent(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return false;

      try
      {
        int errors = 0;
        int warnings = 0;
        return modelDoc.Save3(
            (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
            ref errors,
            ref warnings);
      }
      catch
      {
        return false;
      }
    }

    /// <summary>
    /// Обновляет свойства для указанной конфигурации по иерархии:
    /// если имя есть во вкладке конфигурации — пишем туда;
    /// если имя есть на основной вкладке документа — пишем туда;
    /// если есть в обоих местах — обновляются оба.
    /// Если нигде нет — создаём по <paramref name="createTarget"/>.
    /// </summary>
    internal static bool TryApplyDocumentProperties(
        ModelDoc2 modelDoc,
        string configurationName,
        IReadOnlyList<PropertyPair> properties,
        VelumDocumentPropertyCreateTarget createTarget)
    {
      if (modelDoc == null || properties == null || properties.Count == 0)
        return false;

      string config = (configurationName ?? string.Empty).Trim();
      CustomPropertyManager configCpm = string.IsNullOrEmpty(config)
          ? null
          : VelumRecipeSolidWorksCustomProperties.TryGetManager(modelDoc, config);
      CustomPropertyManager docCpm =
          VelumRecipeSolidWorksCustomProperties.TryGetManager(modelDoc, "document");

      bool any = false;
      for (int i = 0; i < properties.Count; i++)
      {
        PropertyPair pair = properties[i];
        if (pair == null || string.IsNullOrWhiteSpace(pair.Name))
          continue;

        string name = pair.Name.Trim();
        string value = pair.Value ?? string.Empty;

        bool inConfig = configCpm != null &&
            VelumRecipeSolidWorksCustomProperties.TryPropertyExists(configCpm, name);
        bool inDocument = docCpm != null &&
            VelumRecipeSolidWorksCustomProperties.TryPropertyExists(docCpm, name);

        if (inConfig)
        {
          if (TryWriteProperty(configCpm, name, value))
            any = true;
        }

        if (inDocument)
        {
          if (TryWriteProperty(docCpm, name, value))
            any = true;
        }

        if (inConfig || inDocument)
          continue;

        if (createTarget == VelumDocumentPropertyCreateTarget.DocumentSettings)
        {
          if (TryWriteProperty(docCpm, name, value))
            any = true;
        }
        else if (TryCreatePropertyInAllConfigurations(modelDoc, name, value))
        {
          any = true;
        }
      }

      if (any)
        Velum.UI.ProductRegistry.VelumProductRegistryExportMetaSync.TrySyncOpenDocumentFromDisk(modelDoc);

      return any;
    }

    private static bool TryCreatePropertyInAllConfigurations(
        ModelDoc2 modelDoc,
        string propertyName,
        string value)
    {
      IReadOnlyList<string> configs = VelumDxfArtifactResolver.TryGetConfigurationNames(modelDoc);
      bool any = false;
      for (int i = 0; i < configs.Count; i++)
      {
        string configName = (configs[i] ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(configName))
          continue;

        CustomPropertyManager cpm =
            VelumRecipeSolidWorksCustomProperties.TryGetManager(modelDoc, configName);
        if (TryWriteProperty(cpm, propertyName, value))
          any = true;
      }

      return any;
    }

    private static bool TryWriteProperty(
        CustomPropertyManager cpm,
        string name,
        string value)
    {
      if (cpm == null)
        return false;

      return VelumRecipeSolidWorksCustomProperties.TrySetValue(
          cpm,
          name,
          value,
          overwrite: "always",
          propertyTypeKey: "text",
          out _,
          out _);
    }

    internal static bool TryActivateOrOpenVisible(
        ISwApplication swApp,
        string filePath,
        bool isAssembly,
        out string error)
    {
      error = string.Empty;
      if (swApp?.Sw == null)
      {
        error = "SolidWorks недоступен";
        return false;
      }

      string path = TryNormalizePath(filePath);
      if (string.IsNullOrEmpty(path) || !VelumPathExists.FileExists(path))
      {
        error = "Файл не найден";
        return false;
      }

      ModelDoc2 open = TryFindOpenByPath(swApp, path);
      if (open != null)
      {
        try
        {
          string title = open.GetTitle();
          if (string.IsNullOrWhiteSpace(title))
          {
            error = "Не удалось определить заголовок документа";
            return false;
          }

          int activateErrors = 0;
          swApp.Sw.ActivateDoc3(
              title,
              true,
              (int)swRebuildOnActivation_e.swDontRebuildActiveDoc,
              ref activateErrors);
          return true;
        }
        catch (Exception ex)
        {
          error = ex.Message;
          return false;
        }
      }

      int openErrors = 0;
      int warnings = 0;
      try
      {
        ModelDoc2 doc = swApp.Sw.OpenDoc6(
            path,
            isAssembly
                ? (int)swDocumentTypes_e.swDocASSEMBLY
                : (int)swDocumentTypes_e.swDocPART,
            0,
            string.Empty,
            ref openErrors,
            ref warnings) as ModelDoc2;
        if (doc == null)
        {
          error = "Не удалось открыть документ";
          return false;
        }

        return true;
      }
      catch (Exception ex)
      {
        error = ex.Message;
        return false;
      }
    }

    private static void AppendRowsOpeningIfNeeded(
        ISwApplication swApp,
        string path,
        bool isAssembly,
        List<VelumDocumentPropertyBatchRow> target,
        ISet<string> keepOpenPaths,
        Component2 component)
    {
      ModelDoc2 modelDoc = TryGetComponentModel(component);
      bool openedByUs = false;
      if (modelDoc == null)
      {
        modelDoc = TryOpenSilent(swApp, path, isAssembly, out _);
        openedByUs = modelDoc != null;
      }

      if (modelDoc == null)
        return;

      try
      {
        AppendRowsForDocument(modelDoc, path, isAssembly, target);
      }
      finally
      {
        if (openedByUs)
        {
          TryReleaseAfterBatch(
              swApp,
              modelDoc,
              path,
              persistChanges: false,
              keepOpenPaths: keepOpenPaths);
        }
      }
    }

    private static void AppendRowsForDocument(
        ModelDoc2 modelDoc,
        string path,
        bool isAssembly,
        List<VelumDocumentPropertyBatchRow> target)
    {
      if (modelDoc == null || target == null || string.IsNullOrEmpty(path))
        return;

      string displayName = Path.GetFileName(path);
      IReadOnlyList<string> configs = VelumDxfArtifactResolver.TryGetConfigurationNames(modelDoc);
      for (int i = 0; i < configs.Count; i++)
      {
        string configName = (configs[i] ?? string.Empty).Trim();
        target.Add(new VelumDocumentPropertyBatchRow
        {
          FilePath = path,
          DisplayName = displayName,
          ConfigurationName = configName,
          IsAssembly = isAssembly,
          Selected = true
        });
      }
    }

    private static ModelDoc2 TryGetComponentModel(Component2 comp)
    {
      if (comp == null)
        return null;

      try
      {
        return comp.GetModelDoc2() as ModelDoc2;
      }
      catch
      {
        return null;
      }
    }

    private static void CollectFiles(
        string root,
        string pattern,
        HashSet<string> into,
        Func<bool> isCancelled)
    {
      try
      {
        string[] files = Directory.GetFiles(root, pattern, CatalogSearchOption);
        for (int i = 0; i < files.Length; i++)
        {
          if (isCancelled != null && isCancelled())
            return;

          string name = Path.GetFileName(files[i]);
          if (!string.IsNullOrEmpty(name) && name.StartsWith("~$", StringComparison.Ordinal))
            continue;

          string normalized = TryNormalizePath(files[i]);
          if (!string.IsNullOrEmpty(normalized))
            into.Add(normalized);
        }
      }
      catch
      {
      }
    }

    private static int CompareRows(
        VelumDocumentPropertyBatchRow a,
        VelumDocumentPropertyBatchRow b)
    {
      int byName = string.Compare(
          a?.DisplayName,
          b?.DisplayName,
          StringComparison.OrdinalIgnoreCase);
      if (byName != 0)
        return byName;

      return string.Compare(
          a?.ConfigurationName,
          b?.ConfigurationName,
          StringComparison.OrdinalIgnoreCase);
    }

    private static ModelDoc2 TryGetActiveModel(ISwApplication swApp)
    {
      try
      {
        return swApp?.Sw?.IActiveDoc2 as ModelDoc2;
      }
      catch
      {
        return null;
      }
    }

    private static bool IsAssembly(ModelDoc2 modelDoc)
    {
      try
      {
        return modelDoc != null &&
            modelDoc.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY;
      }
      catch
      {
        return false;
      }
    }

    private static string TryGetPathName(ModelDoc2 doc)
    {
      try
      {
        return doc?.GetPathName();
      }
      catch
      {
        return null;
      }
    }

    private static string TryGetComponentPath(Component2 comp)
    {
      try
      {
        return comp?.GetPathName();
      }
      catch
      {
        return null;
      }
    }

    private static string TryNormalizePath(string path)
    {
      if (string.IsNullOrWhiteSpace(path))
        return null;

      try
      {
        return Path.GetFullPath(path.Trim());
      }
      catch
      {
        return path.Trim();
      }
    }

    private static ModelDoc2 TryFindOpenByPath(ISwApplication swApp, string path)
    {
      if (swApp?.Sw == null || string.IsNullOrEmpty(path))
        return null;

      try
      {
        object docsObj = swApp.Sw.GetDocuments();
        object[] docs = docsObj as object[];
        if (docs == null)
          return null;

        for (int i = 0; i < docs.Length; i++)
        {
          ModelDoc2 doc = docs[i] as ModelDoc2;
          string openPath = TryNormalizePath(TryGetPathName(doc));
          if (string.Equals(openPath, path, StringComparison.OrdinalIgnoreCase))
            return doc;
        }
      }
      catch
      {
      }

      return null;
    }

    private static bool ShouldSkipComponent(Component2 comp)
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
  }
}

