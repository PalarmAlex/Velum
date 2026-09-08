using System;
using System.Collections.Generic;
using System.IO;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Velum.ReactiveCore;
using ISIDA.Common;

namespace Velum.UI.AssemblyRegistry
{
  /// <summary>
  /// Зеркалирование состава и свойств сборки в bomMirrorStore.
  /// Вызывается из OnModelFileSavePostNotify при сохранении .sldasm.
  /// </summary>
  internal static class VelumAssemblyBomMirrorCoordinator
  {
    /// <summary>Имя свойства SW для внешнего ID (связь с 1C).</summary>
    private const string ExternalIdPropertyName = "ExternalId";

    /// <summary>
    /// Выполнить зеркалирование сохранённой сборки.
    /// </summary>
    /// <param name="modelDoc">Активный документ (сборка).</param>
    /// <param name="savePath">Путь сохранения (из события).</param>
    /// <returns>true, если зеркалирование выполнено успешно.</returns>
    public static bool TryMirrorSavedAssembly(ModelDoc2 modelDoc, string savePath)
    {
      if (modelDoc == null)
        return false;

      if (modelDoc.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
        return false;

      try
      {
        AssemblyDoc assemblyDoc = modelDoc as AssemblyDoc;
        if (assemblyDoc == null)
          return false;

        // Служебное свойство связи с 1С (по умолчанию пустое).
        EnsureExternalIdProperty(modelDoc);

        // Resolve lightweight components for property reading.
        if (!TryResolveLightweight(modelDoc, assemblyDoc))
          return false;

        // Walk the assembly tree.
        VelumAssemblyRegistryGraph graph = BuildGraph(modelDoc, assemblyDoc);
        if (graph == null || graph.Components.Count == 0)
          return false;

        // Load tracked properties config.
        IReadOnlyList<string> trackedProperties =
            VelumAssemblyBomTrackedPropertiesConfig.Load();
        if (trackedProperties.Count == 0)
        {
          Logger.Info("Velum bomMirror: no tracked properties configured");
          return false;
        }

        // Load or create the mirror store.
        VelumAssemblyBomMirrorStore store = new VelumAssemblyBomMirrorStore();
        store.Load();

        // Mirror each component in the graph.
        foreach (VelumAssemblyRegistryComponent comp in graph.Components.Values)
        {
          if (comp == null || string.IsNullOrWhiteSpace(comp.FilePath))
            continue;

          // Read ExternalId from the component document.
          string externalId = ReadExternalId(comp);

          // Compute hash from tracked properties + quantity.
          string hash = VelumAssemblyBomTrackedPropertiesConfig.ComputeHash(
              comp.PropertyValues,
              trackedProperties,
              comp.Quantity);

          // Upsert into mirror store.
          string identity = comp.Identity ??
              VelumAssemblyRegistryPropertyReader.BuildIdentity(
                  comp.FilePath, comp.ConfigurationName);

          store.Upsert(
              identity,
              comp.FilePath,
              comp.ConfigurationName,
              externalId,
              comp.Designation,
              comp.Name,
              comp.Quantity,
              hash,
              ExtractTrackedValues(comp.PropertyValues, trackedProperties));
        }

        // Save the mirror store.
        store.Save();

        Logger.Info(
            "Velum bomMirror assembly OK components=" + graph.Components.Count +
            " path=\"" + (savePath ?? modelDoc.GetTitle()) + "\"");
        return true;
      }
      catch (Exception ex)
      {
        Logger.Error("Velum bomMirror assembly FAIL: " + ex.Message);
        return false;
      }
    }

    /// <summary>
    /// Выполнить зеркалирование сохранённой детали.
    /// </summary>
    /// <param name="modelDoc">Активный документ (деталь).</param>
    /// <param name="savePath">Путь сохранения (из события).</param>
    /// <returns>true, если зеркалирование выполнено успешно.</returns>
    public static bool TryMirrorSavedPart(ModelDoc2 modelDoc, string savePath)
    {
      if (modelDoc == null)
        return false;

      if (modelDoc.GetType() != (int)swDocumentTypes_e.swDocPART)
        return false;

      try
      {
        // Служебное свойство связи с 1С (по умолчанию пустое).
        EnsureExternalIdProperty(modelDoc);

        // Load tracked properties config.
        IReadOnlyList<string> trackedProperties =
            VelumAssemblyBomTrackedPropertiesConfig.Load();
        if (trackedProperties.Count == 0)
        {
          Logger.Info("Velum bomMirror: no tracked properties configured");
          return false;
        }

        // Load or create the mirror store.
        VelumAssemblyBomMirrorStore store = new VelumAssemblyBomMirrorStore();
        store.Load();

        // Read properties from the part document.
        string filePath = (savePath ?? modelDoc.GetPathName() ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(filePath))
          return false;

        string configurationName = string.Empty;
        try
        {
          SolidWorks.Interop.sldworks.Configuration activeConfig =
              modelDoc.GetActiveConfiguration() as SolidWorks.Interop.sldworks.Configuration;
          if (activeConfig != null)
            configurationName = activeConfig.Name ?? string.Empty;
        }
        catch
        {
          // Ignore.
        }

        string identity = VelumAssemblyRegistryPropertyReader.BuildIdentity(
            filePath, configurationName);

        // Collect property values for the active configuration.
        Dictionary<string, string> propertyValues =
            CollectPartPropertyValues(modelDoc, configurationName);

        // Read ExternalId from the part document.
        string externalId = ReadExternalIdFromPart(modelDoc, configurationName);

        // Get designation and name.
        string designation = ReadDesignation(modelDoc, configurationName);
        string name = ReadCustomProperty(modelDoc, configurationName, "Наименование");

        // Compute hash from tracked properties + quantity (0 for standalone part).
        string hash = VelumAssemblyBomTrackedPropertiesConfig.ComputeHash(
            propertyValues,
            trackedProperties,
            0);

        // Upsert into mirror store.
        store.Upsert(
            identity,
            filePath,
            configurationName,
            externalId,
            designation,
            name,
            0,
            hash,
            ExtractTrackedValues(propertyValues, trackedProperties));

        // Save the mirror store.
        store.Save();

        Logger.Info(
            "Velum bomMirror part OK path=\"" + filePath + "\"");
        return true;
      }
      catch (Exception ex)
      {
        Logger.Error("Velum bomMirror part FAIL: " + ex.Message);
        return false;
      }
    }

    /// <summary>
    /// Собрать снимок значений отслеживаемых свойств из кэша свойств.
    /// </summary>
    private static Dictionary<string, string> ExtractTrackedValues(
        Dictionary<string, string> propertyValues,
        IReadOnlyList<string> trackedProperties)
    {
      var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      if (trackedProperties == null)
        return result;

      foreach (string propName in trackedProperties)
      {
        if (string.IsNullOrWhiteSpace(propName))
          continue;

        string value = string.Empty;
        if (propertyValues != null &&
            propertyValues.TryGetValue(propName, out string raw))
        {
          value = (raw ?? string.Empty).Trim();
          if (string.Equals(value, "?", StringComparison.Ordinal))
            value = string.Empty;
        }

        result[propName] = value;
      }

      return result;
    }

    /// <summary>Обход дерева вхождений сборка → graph.Components.</summary>
    private static VelumAssemblyRegistryGraph BuildGraph(
        ModelDoc2 modelDoc, AssemblyDoc assemblyDoc)
    {
      var graph = new VelumAssemblyRegistryGraph();

      string rootPath;
      try
      {
        rootPath = modelDoc.GetPathName() ?? string.Empty;
      }
      catch
      {
        rootPath = string.Empty;
      }
      graph.RootAssemblyPath = rootPath;

      string rootConfig;
      try
      {
        SolidWorks.Interop.sldworks.Configuration activeConfig =
            modelDoc.GetActiveConfiguration() as SolidWorks.Interop.sldworks.Configuration;
        rootConfig = activeConfig?.Name ?? string.Empty;
      }
      catch
      {
        rootConfig = string.Empty;
      }
      graph.RootConfigurationName = rootConfig;

      object[] topLevel;
      try
      {
        topLevel = assemblyDoc.GetComponents(true) as object[];
      }
      catch
      {
        return null;
      }

      if (topLevel == null || topLevel.Length == 0)
        return graph;

      foreach (object obj in topLevel)
      {
        Component2 comp = obj as Component2;
        if (comp == null)
          continue;

        VisitComponent(comp, null, graph);
      }

      return graph;
    }

    /// <summary>Рекурсивный обход компонента и его детей.</summary>
    private static void VisitComponent(
        Component2 comp,
        string parentAssemblyIdentity,
        VelumAssemblyRegistryGraph graph)
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

      string path;
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

      string configName;
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

      ModelDoc2 modelDoc;
      try
      {
        modelDoc = comp.GetModelDoc2() as ModelDoc2;
      }
      catch
      {
        return;
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

        object[] children;
        try
        {
          children = comp.GetChildren() as object[];
        }
        catch
        {
          return;
        }

        if (children == null)
          return;

        foreach (object childObj in children)
        {
          VisitComponent(childObj as Component2, identity, graph);
        }
      }
    }

    /// <summary>Решить lightweight компоненты, если их больше 10.</summary>
    private static bool TryResolveLightweight(ModelDoc2 modelDoc, AssemblyDoc assemblyDoc)
    {
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
              if (comp.GetSuppression() ==
                  (int)swComponentSuppressionState_e.swComponentLightweight)
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
        Logger.Warning("Velum bomMirror lightweight check: " + ex.Message);
        return true; // Don't fail on check error.
      }

      if (lightweightCount > 0)
      {
        try
        {
          assemblyDoc.ResolveAllLightWeightComponents(false);
          modelDoc.EditRebuild3();
        }
        catch (Exception ex)
        {
          Logger.Warning("Velum bomMirror resolve lightweight: " + ex.Message);
        }
      }

      return true;
    }

    /// <summary>
    /// Собрать tracked properties из кэша компонента (используем PropertyValues).
    /// </summary>
    private static Dictionary<string, string> CollectPartPropertyValues(
        ModelDoc2 modelDoc, string configurationName)
    {
      var cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

      // Collect names from configuration and document scopes.
      var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      CollectPropertyNames(modelDoc, configurationName, names);
      CollectPropertyNames(modelDoc, string.Empty, names);

      foreach (string name in names)
      {
        if (string.IsNullOrWhiteSpace(name))
          continue;

        string value;
        bool exists;
        if (!VelumRecipeSolidWorksCustomProperties.TryReadConfigThenDocument(
                modelDoc,
                configurationName,
                name,
                out value,
                out exists) ||
            !exists)
          continue;

        cache[name] = value ?? string.Empty;
      }

      return cache;
    }

    private static void CollectPropertyNames(
        ModelDoc2 modelDoc,
        string configurationName,
        HashSet<string> names)
    {
      string configKey = string.IsNullOrWhiteSpace(configurationName)
          ? "document"
          : configurationName.Trim();
      CustomPropertyManager cpm =
          VelumRecipeSolidWorksCustomProperties.TryGetManager(modelDoc, configKey);
      if (cpm == null)
        return;

      try
      {
        string[] list = cpm.GetNames() as string[];
        if (list == null)
          return;
        foreach (string name in list)
        {
          if (!string.IsNullOrWhiteSpace(name))
            names.Add(name.Trim());
        }
      }
      catch
      {
      }
    }

    /// <summary>
    /// Прочитать ExternalId из свойства компонента.
    /// </summary>
    private static string ReadExternalId(VelumAssemblyRegistryComponent comp)
    {
      if (comp == null || string.IsNullOrWhiteSpace(comp.FilePath))
        return string.Empty;

      // Try to read from PropertyValues first.
      if (comp.PropertyValues != null &&
          comp.PropertyValues.TryGetValue(ExternalIdPropertyName, out string extId))
      {
        return (extId ?? string.Empty).Trim();
      }

      return string.Empty;
    }

    /// <summary>Прочитать ExternalId из свойства детали.</summary>
    private static string ReadExternalIdFromPart(
        ModelDoc2 modelDoc, string configurationName)
    {
      string value;
      bool exists;
      if (!VelumRecipeSolidWorksCustomProperties.TryReadConfigThenDocument(
              modelDoc,
              configurationName,
              ExternalIdPropertyName,
              out value,
              out exists) ||
          !exists)
        return string.Empty;

      return (value ?? string.Empty).Trim();
    }

    /// <summary>Прочитать обозначение из свойства SW.</summary>
    private static string ReadDesignation(ModelDoc2 modelDoc, string configurationName)
    {
      string value;
      bool exists;
      if (!VelumRecipeSolidWorksCustomProperties.TryReadConfigThenDocument(
              modelDoc,
              configurationName,
              VelumAssemblyRegistryPropertyReader.PropDesignation,
              out value,
              out exists) ||
          !exists)
      {
        // Fallback: file title.
        try
        {
          return modelDoc.GetTitle() ?? string.Empty;
        }
        catch
        {
          return string.Empty;
        }
      }

      return (value ?? string.Empty).Trim();
    }

    /// <summary>Прочитать произвольное custom property.</summary>
    private static string ReadCustomProperty(
        ModelDoc2 modelDoc, string configurationName, string propertyName)
    {
      string value;
      bool exists;
      if (!VelumRecipeSolidWorksCustomProperties.TryReadConfigThenDocument(
              modelDoc,
              configurationName,
              propertyName,
              out value,
              out exists) ||
          !exists)
        return string.Empty;

      return (value ?? string.Empty).Trim();
    }

    /// <summary>
    /// Гарантировать наличие служебного свойства <see cref="ExternalIdPropertyName"/>
    /// в документе (по умолчанию пустое). Свойство нужно оператору для заполнения
    /// идентификатора связи с 1С; создаётся без участия пульсации. Вызывается из
    /// pre-save (попадает в текущий файл) и post-save (идемпотентно).
    /// </summary>
    internal static void EnsureExternalIdProperty(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return;

      try
      {
        // Уже есть в конфигурации — документное свойство не требуется.
        string configName = string.Empty;
        try
        {
          SolidWorks.Interop.sldworks.Configuration activeConfig =
              modelDoc.GetActiveConfiguration() as SolidWorks.Interop.sldworks.Configuration;
          configName = activeConfig?.Name ?? string.Empty;
        }
        catch
        {
        }

        if (!string.IsNullOrWhiteSpace(configName))
        {
          CustomPropertyManager configCpm =
              VelumRecipeSolidWorksCustomProperties.TryGetManager(modelDoc, configName);
          if (configCpm != null &&
              VelumRecipeSolidWorksCustomProperties.TryPropertyExists(
                  configCpm, ExternalIdPropertyName))
          {
            return;
          }
        }

        CustomPropertyManager docCpm =
            VelumRecipeSolidWorksCustomProperties.TryGetManager(modelDoc, "document");
        if (docCpm == null)
          return;

        if (VelumRecipeSolidWorksCustomProperties.TryPropertyExists(
                docCpm, ExternalIdPropertyName))
        {
          return;
        }

        int addResult = docCpm.Add3(
            ExternalIdPropertyName,
            (int)swCustomInfoType_e.swCustomInfoText,
            string.Empty,
            (int)swCustomPropertyAddOption_e.swCustomPropertyOnlyIfNew);

        if (addResult != 0)
        {
          Logger.Warning(
              "Velum bomMirror: не удалось создать свойство " +
              ExternalIdPropertyName + " (result=" + addResult + ")");
        }
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum bomMirror: EnsureExternalIdProperty: " + ex.Message);
      }
    }
  }
}
