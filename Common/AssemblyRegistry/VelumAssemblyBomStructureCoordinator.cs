using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Velum.ReactiveCore;
using ISIDA.Common;

namespace Velum.UI.AssemblyRegistry
{
  /// <summary>
  /// Зеркалирование структуры состава сохранённой сборки в VelumBomStructureStore
  /// с фиксацией операций add/update/delete в VelumBomChangeLogStore.
  /// Вызывается из OnModelFileSavePostNotify при сохранении .sldasm,
  /// строго после VelumAssemblyBomMirrorCoordinator.TryMirrorSavedAssembly
  /// (карточки зеркалируются первыми — нужны ExternalId детей).
  /// </summary>
  internal static class VelumAssemblyBomStructureCoordinator
  {
    /// <summary>Имя свойства SW для внешнего ID (связь с 1C).</summary>
    private const string ExternalIdPropertyName = "ExternalId";

    /// <summary>
    /// Выполнить зеркалирование структуры состава сохранённой сборки.
    /// Обрабатываются только прямые дети родителя (без рекурсии) —
    /// структура каждой вложенной сборки строится при её собственном сохранении.
    /// </summary>
    /// <param name="modelDoc">Активный документ (сборка).</param>
    /// <param name="savePath">Путь сохранения (из события).</param>
    /// <returns>true, если зеркалирование структуры выполнено успешно.</returns>
    public static bool TryMirrorSavedAssemblyStructure(ModelDoc2 modelDoc, string savePath)
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

        // Активная конфигурация родителя (часть ParentIdentity и ключа 1С).
        string parentConfiguration = ReadActiveConfiguration(modelDoc);

        // ParentExternalId читается из свойства ExternalId самого документа сборки:
        // корневая сохраняемая сборка не является записью в bomMirror.json
        // (там хранятся только её компоненты).
        string parentExternalId = ReadExternalIdFromDocument(modelDoc, parentConfiguration);
        if (string.IsNullOrWhiteSpace(parentExternalId))
        {
          // Без ExternalId родителя структура не связывается с 1С —
          // не пишем ни в bomStructure.json, ни в change log.
          Logger.Info("bomStructure: parent ExternalId empty, skip");
          return false;
        }

        // Подсчёт вхождений прямых детей за один проход (без предварительных
        // оценочных проходов — один GetComponents(true) и словарь).
        // Только верхний уровень (прямые дети), в отличие от рекурсивного обхода
        // в VelumAssemblyBomMirrorCoordinator.BuildGraph: структура каждой вложенной
        // сборки строится при её собственном сохранении.
        object[] topLevel;
        try
        {
          topLevel = assemblyDoc.GetComponents(true) as object[];
        }
        catch
        {
          return false;
        }

        var counters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var childConfigs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (topLevel != null)
        {
          foreach (object obj in topLevel)
          {
            Component2 comp = obj as Component2;
            if (comp == null)
              continue;

            try
            {
              if (comp.IsSuppressed() || comp.IsEnvelope())
                continue;
            }
            catch
            {
              continue;
            }

            string path;
            try
            {
              path = comp.GetPathName() ?? string.Empty;
            }
            catch
            {
              continue;
            }

            if (string.IsNullOrWhiteSpace(path))
              continue;

            string config;
            try
            {
              config = comp.ReferencedConfiguration ?? string.Empty;
            }
            catch
            {
              config = string.Empty;
            }

            // Разные конфигурации одного файла — разные childIdentity.
            string childIdentity = VelumAssemblyRegistryPropertyReader.BuildIdentity(path, config);
            if (string.IsNullOrWhiteSpace(childIdentity))
              continue;

            counters.TryGetValue(childIdentity, out int q);
            counters[childIdentity] = q + 1;
            childConfigs[childIdentity] = config;
          }
        }

        // Метаданные детей (ExternalId/обозначение/наименование) — из зеркала карточек.
        VelumAssemblyBomMirrorStore mirrorStore = new VelumAssemblyBomMirrorStore();
        mirrorStore.Load();

        var lines = new List<VelumBomStructureLine>();
        foreach (var kv in counters)
        {
          VelumAssemblyBomMirrorEntry mirrorEntry = mirrorStore.GetEntry(kv.Key);
          string childConfig;
          childConfigs.TryGetValue(kv.Key, out childConfig);

          lines.Add(new VelumBomStructureLine
          {
            ChildIdentity = kv.Key,
            ChildExternalId = mirrorEntry?.ExternalId ?? string.Empty,
            ChildDesignation = mirrorEntry?.Designation ?? string.Empty,
            ChildName = mirrorEntry?.Name ?? string.Empty,
            Quantity = kv.Value
          });
        }

        // Identity родителя — путь документа + активная конфигурация.
        string parentPath = ReadParentPath(modelDoc, savePath);
        string parentIdentity = VelumAssemblyRegistryPropertyReader.BuildIdentity(
            parentPath, parentConfiguration);

        // Обозначение и наименование родителя (информационно).
        string parentDesignation = ReadDesignation(modelDoc, parentConfiguration);
        string parentName = ReadCustomProperty(modelDoc, parentConfiguration, "Наименование");

        VelumBomStructureStore structureStore = new VelumBomStructureStore();
        structureStore.Load();

        VelumBomStructureEntry oldEntry = structureStore.GetEntry(parentIdentity);
        string currentHash = VelumBomStructureStore.ComputeStructureHash(parentExternalId, lines);

        // Построчное сравнение со старым состоянием → записи журнала изменений.
        VelumBomChangeLogStore changeStore = new VelumBomChangeLogStore();
        changeStore.Load();

        AppendChanges(
            changeStore,
            oldEntry,
            parentExternalId,
            parentConfiguration,
            lines,
            currentHash);

        // Новая запись структуры: для существующей записи Upsert сохраняет её
        // PreviousHash; для новой — PreviousHash пуст (первое зеркалирование).
        structureStore.Upsert(new VelumBomStructureEntry
        {
          ParentIdentity = parentIdentity,
          ParentExternalId = parentExternalId,
          ParentConfiguration = parentConfiguration,
          ParentDesignation = parentDesignation,
          ParentName = parentName,
          CurrentHash = currentHash,
          Lines = lines,
          LastMirroredUtc = DateTime.UtcNow
        });

        changeStore.Save();
        structureStore.Save();

        Logger.Info(
            "Velum bomStructure OK parent=\"" + parentExternalId + "\" lines=" + lines.Count +
            " path=\"" + (savePath ?? modelDoc.GetTitle()) + "\"");
        return true;
      }
      catch (Exception ex)
      {
        Logger.Error("Velum bomStructure assembly FAIL: " + ex.Message);
        return false;
      }
    }

    /// <summary>
    /// Сравнить новое состояние структуры со старым и добавить записи
    /// add/update/delete в журнал изменений.
    /// Update — при изменении количества ИЛИ ExternalId ребёнка.
    /// </summary>
    private static void AppendChanges(
        VelumBomChangeLogStore changeStore,
        VelumBomStructureEntry oldEntry,
        string parentExternalId,
        string parentConfiguration,
        List<VelumBomStructureLine> lines,
        string currentHash)
    {
      // Словарь старых строк: ChildIdentity → строка (первая, дублей быть не должно).
      Dictionary<string, VelumBomStructureLine> oldLines =
          oldEntry != null && oldEntry.Lines != null
              ? oldEntry.Lines
                  .Where(l => l != null && !string.IsNullOrWhiteSpace(l.ChildIdentity))
                  .GroupBy(l => l.ChildIdentity, StringComparer.OrdinalIgnoreCase)
                  .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase)
              : new Dictionary<string, VelumBomStructureLine>(StringComparer.OrdinalIgnoreCase);

      DateTime nowUtc = DateTime.UtcNow;

      // Новые и изменённые строки.
      foreach (VelumBomStructureLine line in lines)
      {
        if (line == null || string.IsNullOrWhiteSpace(line.ChildIdentity))
          continue;

        VelumBomStructureLine oldLine;
        bool existed = oldLines.TryGetValue(line.ChildIdentity, out oldLine);
        if (existed &&
            oldLine.Quantity == line.Quantity &&
            string.Equals(oldLine.ChildExternalId, line.ChildExternalId, StringComparison.Ordinal))
        {
          // Строка не изменилась — операция не нужна.
          continue;
        }

        changeStore.Append(new VelumBomChangeRecord
        {
          Id = Guid.NewGuid().ToString("D"),
          Action = existed ? VelumBomChangeAction.Update : VelumBomChangeAction.Add,
          ParentExternalId = parentExternalId,
          ParentConfiguration = parentConfiguration,
          ChildIdentity = line.ChildIdentity,
          ChildExternalId = line.ChildExternalId,
          ChildConfiguration = ChildConfigurationFromIdentity(line.ChildIdentity),
          Quantity = line.Quantity,
          TimestampUtc = nowUtc,
          SourceHash = currentHash,
          Exported = false
        });
      }

      // Удалённые строки: есть в старом состоянии, нет в новом.
      foreach (VelumBomStructureLine oldLine in oldLines.Values)
      {
        if (lines.Any(l => l != null && string.Equals(
                l.ChildIdentity, oldLine.ChildIdentity, StringComparison.OrdinalIgnoreCase)))
          continue;

        changeStore.Append(new VelumBomChangeRecord
        {
          Id = Guid.NewGuid().ToString("D"),
          Action = VelumBomChangeAction.Delete,
          ParentExternalId = parentExternalId,
          ParentConfiguration = parentConfiguration,
          ChildIdentity = oldLine.ChildIdentity,
          ChildExternalId = oldLine.ChildExternalId,
          ChildConfiguration = ChildConfigurationFromIdentity(oldLine.ChildIdentity),
          Quantity = oldLine.Quantity,
          TimestampUtc = nowUtc,
          // Для Delete — хэш состояния, из которого строка удалена.
          SourceHash = oldEntry != null ? (oldEntry.PreviousHash ?? string.Empty) : string.Empty,
          Exported = false
        });
      }
    }

    /// <summary>Конфигурация ребёнка из Identity (вторая половина «path|config»).</summary>
    private static string ChildConfigurationFromIdentity(string childIdentity)
    {
      if (string.IsNullOrEmpty(childIdentity))
        return string.Empty;
      int separatorIndex = childIdentity.IndexOf('|');
      return separatorIndex >= 0 ? childIdentity.Substring(separatorIndex + 1) : string.Empty;
    }

    /// <summary>Прочитать имя активной конфигурации документа.</summary>
    private static string ReadActiveConfiguration(ModelDoc2 modelDoc)
    {
      try
      {
        SolidWorks.Interop.sldworks.Configuration activeConfig =
            modelDoc.GetActiveConfiguration() as SolidWorks.Interop.sldworks.Configuration;
        return activeConfig?.Name ?? string.Empty;
      }
      catch
      {
        return string.Empty;
      }
    }

    /// <summary>Путь документа сборки (fallback — путь события сохранения).</summary>
    private static string ReadParentPath(ModelDoc2 modelDoc, string savePath)
    {
      string path;
      try
      {
        path = modelDoc.GetPathName() ?? string.Empty;
      }
      catch
      {
        path = string.Empty;
      }

      return string.IsNullOrWhiteSpace(path) ? (savePath ?? string.Empty) : path;
    }

    /// <summary>Прочитать ExternalId из свойства документа (конфигурация → документ).</summary>
    private static string ReadExternalIdFromDocument(
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

    /// <summary>Прочитать обозначение родителя (fallback — заголовок документа).</summary>
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
        // Fallback: заголовок документа.
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
  }
}