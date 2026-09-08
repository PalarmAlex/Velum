using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ISIDA.Common;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Velum.ReactiveCore;
using Velum.ReactiveCore.Export;
using Velum.SolidHomeostasis;
using Velum.UI;
using Xarial.XCad.SolidWorks;

namespace Velum.UI.ProductRegistry
{
  /// <summary>Исход синхронизации зеркала export-meta SW → <see cref="VelumProductItem"/>.</summary>
  internal enum VelumProductRegistryExportMetaSyncOutcome
  {
    Updated,
    Unchanged,
    NotInRegistry,
    Unsupported,
    DocumentNotOpen,
    Failed
  }

  /// <summary>
  /// Односторонний sync зеркала свойств экспортной документации из CPM в реестр.
  /// Зеркальные поля в UI реестра только для чтения; правка — в документе.
  /// </summary>
  internal static class VelumProductRegistryExportMetaSync
  {
    private static readonly object DiskSyncGate = new object();
    private static string _lastActiveSyncedPath = string.Empty;
    private static string _lastActiveSyncedFingerprint = string.Empty;

    internal static bool IsExportMetaSyncSupported(string filePath)
    {
      string ext = VelumProductRegistryStore.GetDocumentTypeKey(filePath);
      return string.Equals(ext, ".sldprt", StringComparison.OrdinalIgnoreCase)
          || string.Equals(ext, ".sldasm", StringComparison.OrdinalIgnoreCase)
          || string.Equals(ext, ".slddrw", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Sync по уже открытому документу (без OpenDoc).</summary>
    internal static VelumProductRegistryExportMetaSyncOutcome TrySyncOpenDocument(
        VelumProductRegistryStore store,
        string filePath,
        ModelDoc2 modelDoc,
        bool persist,
        out string error)
    {
      error = string.Empty;
      if (store == null)
      {
        error = "store null";
        return VelumProductRegistryExportMetaSyncOutcome.Failed;
      }

      if (modelDoc == null)
      {
        error = "Документ не открыт";
        return VelumProductRegistryExportMetaSyncOutcome.DocumentNotOpen;
      }

      string path = VelumProductRegistryStore.NormalizeFilePathKey(filePath);
      if (string.IsNullOrEmpty(path))
      {
        try
        {
          path = VelumProductRegistryStore.NormalizeFilePathKey(modelDoc.GetPathName());
        }
        catch
        {
          path = string.Empty;
        }
      }

      if (string.IsNullOrEmpty(path))
      {
        error = "Путь пуст";
        return VelumProductRegistryExportMetaSyncOutcome.Failed;
      }

      if (!IsExportMetaSyncSupported(path))
        return VelumProductRegistryExportMetaSyncOutcome.Unsupported;

      VelumProductItem item = store.FindItemByFilePath(path);
      if (item == null)
        return VelumProductRegistryExportMetaSyncOutcome.NotInRegistry;

      if (!TryReadSnapshot(modelDoc, path, out VelumProductExportMetaSnapshot snap, out error))
        return VelumProductRegistryExportMetaSyncOutcome.Failed;

      return TryApplySnapshot(store, item, snap, persist);
    }

    /// <summary>
    /// Принудительная синхронизация зеркал DXF/PDF для сохранённого документа.
    /// Вызывается сразу после обновления штампов при сохранении.
    /// </summary>
    internal static void TrySyncStampsAfterSave(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return;

      try
      {
        // Проверяем, что это деталь или сборка
        int docType = modelDoc.GetType();
        if (docType != (int)swDocumentTypes_e.swDocPART &&
            docType != (int)swDocumentTypes_e.swDocASSEMBLY)
          return;

        // Синхронизируем зеркала в реестр
        TrySyncOpenDocumentFromDisk(modelDoc);
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum registry export-meta sync after save: " + ex.Message);
      }
    }


    /// <summary>Sync выбранной записи с возможным OpenDoc.</summary>
    internal static VelumProductRegistryExportMetaSyncOutcome TrySyncItemAllowOpen(
        VelumProductRegistryStore store,
        ISwApplication swApp,
        VelumProductItem item,
        bool persist,
        out string error)
    {
      error = string.Empty;
      if (store == null || item == null)
      {
        error = "item/store null";
        return VelumProductRegistryExportMetaSyncOutcome.Failed;
      }

      if (!IsExportMetaSyncSupported(item.FilePath))
        return VelumProductRegistryExportMetaSyncOutcome.Unsupported;

      if (!TryOpenOrFind(swApp, item.FilePath, out ModelDoc2 modelDoc, out bool openedByUs, out error))
        return VelumProductRegistryExportMetaSyncOutcome.Failed;

      try
      {
        return TrySyncOpenDocument(store, item.FilePath, modelDoc, persist, out error);
      }
      finally
      {
        if (openedByUs && modelDoc != null && swApp?.Sw != null)
        {
          try
          {
            swApp.Sw.CloseDoc(modelDoc.GetPathName());
          }
          catch
          {
          }
        }
      }
    }

    /// <summary>Удобный fire-and-forget sync открытого документа с дисковым store.</summary>
    internal static void TrySyncOpenDocumentFromDisk(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return;
      if (VelumProductRegistryFormHost.IsOpen)
        return;

      string path;
      try
      {
        path = VelumProductRegistryStore.NormalizeFilePathKey(modelDoc.GetPathName());
      }
      catch
      {
        return;
      }

      if (string.IsNullOrEmpty(path) || !IsExportMetaSyncSupported(path))
        return;

      lock (DiskSyncGate)
      {
        try
        {
          var store = new VelumProductRegistryStore();
          store.Load();
          TrySyncOpenDocument(store, path, modelDoc, persist: true, out _);
        }
        catch (Exception ex)
        {
          Logger.Warning("Velum registry export-meta sync from disk: " + ex.Message);
        }
      }
    }

    /// <summary>Пакетный sync открытых путей (документ должен быть уже в сессии SW).</summary>
    internal static void SyncOpenPathsFromDisk(ISwApplication swApp, IEnumerable<string> filePaths)
    {
      if (swApp?.Sw == null || filePaths == null)
        return;
      if (VelumProductRegistryFormHost.IsOpen)
        return;

      lock (DiskSyncGate)
      {
        try
        {
          var store = new VelumProductRegistryStore();
          store.Load();
          bool anyUpdated = false;
          var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
          foreach (string raw in filePaths)
          {
            string path = VelumProductRegistryStore.NormalizeFilePathKey(raw);
            if (string.IsNullOrEmpty(path) || !seen.Add(path))
              continue;
            if (!IsExportMetaSyncSupported(path))
              continue;

            ModelDoc2 doc = VelumProductRegistryNameSyncHelper.TryFindOpenDocumentByPath(swApp, path);
            if (doc == null)
              continue;

            VelumProductRegistryExportMetaSyncOutcome outcome = TrySyncOpenDocument(
                store, path, doc, persist: false, out _);
            if (outcome == VelumProductRegistryExportMetaSyncOutcome.Updated)
              anyUpdated = true;
          }

          if (anyUpdated)
            store.Save();
        }
        catch (Exception ex)
        {
          Logger.Warning("Velum registry export-meta sync open paths: " + ex.Message);
        }
      }
    }

    /// <summary>Sync активного документа (без OpenDoc).</summary>
    internal static void TrySyncActiveDocument(ISwApplication swApp)
    {
      if (swApp?.Sw == null)
        return;
      if (VelumProductRegistryFormHost.IsOpen)
        return;

      ModelDoc2 active = null;
      string path = string.Empty;
      try
      {
        active = swApp.Sw.IActiveDoc2 as ModelDoc2;
        if (active != null)
          path = active.GetPathName();
      }
      catch
      {
        return;
      }

      path = VelumProductRegistryStore.NormalizeFilePathKey(path);
      if (string.IsNullOrEmpty(path) || !IsExportMetaSyncSupported(path))
        return;

      if (!TryReadSnapshot(active, path, out VelumProductExportMetaSnapshot snap, out _))
        return;

      string fingerprint = snap.ComputeCompareKey();
      if (string.Equals(_lastActiveSyncedPath, path, StringComparison.OrdinalIgnoreCase)
          && string.Equals(_lastActiveSyncedFingerprint, fingerprint, StringComparison.Ordinal))
        return;

      lock (DiskSyncGate)
      {
        try
        {
          var store = new VelumProductRegistryStore();
          store.Load();
          VelumProductRegistryExportMetaSyncOutcome outcome = TryApplySnapshot(
              store,
              store.FindItemByFilePath(path),
              snap,
              persist: true);
          if (outcome == VelumProductRegistryExportMetaSyncOutcome.NotInRegistry
              || outcome == VelumProductRegistryExportMetaSyncOutcome.Updated
              || outcome == VelumProductRegistryExportMetaSyncOutcome.Unchanged)
          {
            _lastActiveSyncedPath = path;
            _lastActiveSyncedFingerprint = fingerprint;
          }
        }
        catch (Exception ex)
        {
          Logger.Warning("Velum registry export-meta sync active: " + ex.Message);
        }
      }
    }

    private static VelumProductRegistryExportMetaSyncOutcome TryApplySnapshot(
        VelumProductRegistryStore store,
        VelumProductItem item,
        VelumProductExportMetaSnapshot snap,
        bool persist)
    {
      if (store == null || snap == null)
        return VelumProductRegistryExportMetaSyncOutcome.Failed;
      if (item == null)
        return VelumProductRegistryExportMetaSyncOutcome.NotInRegistry;

      // Если pending > export — геометрия изменилась, но export-штамп не обновился
      // (race condition между записью pending и синхронизацией реестра).
      // Обновляем export-штамп до ModelGeometryStamp детали.
      if (snap.ApplyDxfMeta && snap.Configs != null && item.ExportMetaConfigs != null)
      {
        for (int i = 0; i < snap.Configs.Count; i++)
        {
          var cfg = snap.Configs[i];
          if (cfg == null || cfg.DxfGeometryPendingStamp == null)
            continue;

          var existing = item.ExportMetaConfigs
              .FirstOrDefault(c => string.Equals(c?.ConfigName, cfg.ConfigName, StringComparison.OrdinalIgnoreCase));
          if (existing == null)
            continue;

          if (cfg.DxfGeometryPendingStamp > cfg.DxfGeometryUpdateStamp &&
              snap.ModelGeometryStamp.HasValue &&
              snap.ModelGeometryStamp.Value > cfg.DxfGeometryUpdateStamp)
          {
            cfg.DxfGeometryUpdateStamp = snap.ModelGeometryStamp.Value;
            Logger.Info(
                "Velum registry export-meta: DxfGeometryUpdateStamp updated due to pending > export" +
                " config=" + cfg.ConfigName + " old=" + cfg.DxfGeometryUpdateStamp +
                " new=" + snap.ModelGeometryStamp.Value);
          }
        }
      }

      if (!snap.HasChangesAgainst(item))
        return VelumProductRegistryExportMetaSyncOutcome.Unchanged;

      snap.ApplyTo(item);
      try
      {
        store.UpdateItem(item, persist);
        return VelumProductRegistryExportMetaSyncOutcome.Updated;
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum registry export-meta apply: " + ex.Message);
        return VelumProductRegistryExportMetaSyncOutcome.Failed;
      }
    }

    /// <summary>
    /// После успешного экспорта PDF: записывает <see cref="VelumProductItem.PdfModelStampAtExport"/>
    /// из <see cref="VelumProductItem.ModelGeometryStamp"/> связанной детали.
    /// Чертёж "принял" текущую геометрию модели — PDF больше не устарел по модели.
    /// </summary>
    internal static void TrySyncPdfModelStampAtExport(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return;

      string path;
      try
      {
        path = VelumProductRegistryStore.NormalizeFilePathKey(modelDoc.GetPathName());
      }
      catch
      {
        return;
      }

      if (string.IsNullOrEmpty(path) || !IsExportMetaSyncSupported(path))
        return;

      string ext = VelumProductRegistryStore.GetDocumentTypeKey(path);
      if (!string.Equals(ext, ".slddrw", StringComparison.OrdinalIgnoreCase))
        return;

      lock (DiskSyncGate)
      {
        try
        {
          var store = new VelumProductRegistryStore();
          store.Load();

          VelumProductItem drawingItem = store.FindItemByFilePath(path);
          if (drawingItem == null)
            return;

          // Ищем связанную деталь.
          string partPath = (drawingItem.DrawingPath ?? string.Empty).Trim();
          if (string.IsNullOrEmpty(partPath))
          {
            // Fallback: ищем по базовому имени файла чертежа.
            string drawingBaseName = System.IO.Path.GetFileNameWithoutExtension(
                drawingItem.FilePath ?? string.Empty);
            if (!string.IsNullOrEmpty(drawingBaseName))
            {
              string normalizedDrawingBaseName = drawingBaseName.ToLowerInvariant();
              foreach (var item in store.GetAllItems())
              {
                if (item == null || item.FilePath == null)
                  continue;
                string itemExt = System.IO.Path.GetExtension(item.FilePath).ToLowerInvariant();
                if (itemExt != ".sldprt" && itemExt != ".sldasm")
                  continue;
                string itemBaseName = System.IO.Path.GetFileNameWithoutExtension(item.FilePath).ToLowerInvariant();
                if (itemBaseName == normalizedDrawingBaseName)
                {
                  partPath = item.FilePath;
                  break;
                }
              }
            }
          }

          if (string.IsNullOrEmpty(partPath))
            return;

          string normalizedPartPath = VelumProductRegistryStore.NormalizeFilePathKey(partPath);
          if (string.IsNullOrEmpty(normalizedPartPath))
            return;

          VelumProductItem partItem = store.FindItemByFilePath(normalizedPartPath);
          if (partItem == null)
            return;

          // PdfModelStampAtExport предназначен для чертежей деталей и сборок.
          // ModelGeometryStamp хранит GetUpdateStamp() связанной модели (детали или сборки) —
          // это то, что нужно сравнивать: если stamp сборки/детали вырос с момента экспорта
          // PDF, значит геометрия изменилась, PDF устарел.
          string partExt = System.IO.Path.GetExtension(partItem.FilePath ?? string.Empty).ToLowerInvariant();
          if (string.Equals(partExt, ".sldasm", StringComparison.OrdinalIgnoreCase))
          {
            // Для сборок запись PdfModelStampAtExport допустима:
            // ModelGeometryStamp = GetUpdateStamp() сборки, это корректный маркер изменений.
          }

          // Для листовой детали: использовать максимальный DxfGeometryUpdateStamp вместо
          // ModelGeometryStamp. ModelGeometryStamp увеличивается при fold/unfold развёртки,
          // хотя геометрия DXF не меняется — это вызывает ложные срабатывания PDF IsOutdated.
          int stamp;
          if (string.Equals(partExt, ".sldprt", StringComparison.OrdinalIgnoreCase) &&
              partItem.ExportMetaConfigs != null && partItem.ExportMetaConfigs.Length > 0)
          {
            int maxDxfStamp = 0;
            for (int i = 0; i < partItem.ExportMetaConfigs.Length; i++)
            {
              var cfg = partItem.ExportMetaConfigs[i];
              if (cfg != null && cfg.DxfGeometryUpdateStamp.HasValue &&
                  cfg.DxfGeometryUpdateStamp.Value > maxDxfStamp)
              {
                maxDxfStamp = cfg.DxfGeometryUpdateStamp.Value;
              }
            }

            if (maxDxfStamp > 0)
            {
              stamp = maxDxfStamp;
              Logger.Info(
                  "Velum registry export-meta: using DxfGeometryUpdateStamp for part " +
                  partPath + " stamp=" + stamp);
            }
            else
            {
              // Fallback: если DXF-штампов нет, используем ModelGeometryStamp.
              if (!partItem.ModelGeometryStamp.HasValue)
                return;
              stamp = partItem.ModelGeometryStamp.Value;
            }
          }
          else
          {
            if (!partItem.ModelGeometryStamp.HasValue)
              return;
            stamp = partItem.ModelGeometryStamp.Value;
          }

          if (drawingItem.PdfModelStampAtExport == stamp)
            return;

          drawingItem.PdfModelStampAtExport = stamp;
          store.UpdateItem(drawingItem, persist: true);
          VelumProductRegistryIntegrityScheduler.NotifyRegistryChanged();
          Logger.Info(
              "Velum registry export-meta: PdfModelStampAtExport synced after PDF export: " +
              path + " stamp=" + stamp);
        }
        catch (Exception ex)
        {
          Logger.Warning("Velum registry export-meta pdf model stamp sync: " + ex.Message);
        }
      }
    }

    private static bool TryReadSnapshot(
        ModelDoc2 modelDoc,
        string path,
        out VelumProductExportMetaSnapshot snap,
        out string error)
    {
      snap = new VelumProductExportMetaSnapshot();
      error = string.Empty;
      if (modelDoc == null)
      {
        error = "model null";
        return false;
      }

      string ext = VelumProductRegistryStore.GetDocumentTypeKey(path);
      bool isPart = string.Equals(ext, ".sldprt", StringComparison.OrdinalIgnoreCase);
      bool isAsm = string.Equals(ext, ".sldasm", StringComparison.OrdinalIgnoreCase);
      bool isDrw = string.Equals(ext, ".slddrw", StringComparison.OrdinalIgnoreCase);

      if (isPart || isAsm)
      {
        EnsureDefaultNeedFlags(modelDoc, isPart);

        if (isPart)
        {
          snap.ApplyDxfMeta = true;
          snap.Configs = ReadPartConfigs(modelDoc);
          bool anyNeed = false;
          for (int i = 0; i < snap.Configs.Count; i++)
          {
            if (snap.Configs[i] != null && snap.Configs[i].NeedDxf == true)
            {
              anyNeed = true;
              break;
            }
          }

          snap.NeedDxf = anyNeed;

          snap.DxfPath = NormalizeOptionalPath(VelumDxfBatchDocumentHelper.TryReadDxfCatalog(modelDoc));
        }
        else
        {
          snap.ApplyDxfMeta = false;
        }

        if (VelumDrawingPathPropertyHelper.TryReadNeedDrawing(modelDoc, out bool needDrawing))
          snap.NeedDrawing = needDrawing;
        else
          snap.NeedDrawing = true;

snap.DrawingPath = NormalizeOptionalPath(VelumDrawingPathPropertyHelper.TryRead(modelDoc));

        // Штамп геометрии модели: хранится в реестре как единая база для определения
        // устаревания PDF/DXF по изменению геометрии (не зависит от свойств документа).
        if (VelumExportDocumentationGeometryStampHelper.TryGetCurrentUpdateStamp(modelDoc, out int modelStamp)
            && modelStamp > 0)
          snap.ModelGeometryStamp = modelStamp;
      }

      if (isDrw)
      {
        // Как batch: отсутствие флага = нужен PDF. Ensure для NeedPdf в CPM не пишем
        // (NeedDxf по умолчанию No — только на детали; NeedDrawing — на детали/сборке).
        if (VelumPdfBatchDocumentHelper.TryReadNeedPdf(modelDoc, out bool needPdf))
          snap.NeedPdf = needPdf;
        else
          snap.NeedPdf = true;

        snap.PdfPath = NormalizeOptionalPath(VelumPdfArtifactResolver.TryReadPdfPathProperty(modelDoc));

        if (VelumExportDocumentationGeometryStampHelper.TryReadStoredUpdateStamp(
                modelDoc,
                VelumExportDocumentationProperties.PdfGeometryUpdateStamp,
                out int pdfExport))
          snap.PdfGeometryUpdateStamp = pdfExport;

        if (VelumExportDocumentationGeometryStampHelper.TryReadStoredUpdateStamp(
                modelDoc,
                VelumExportDocumentationProperties.PdfGeometryPendingStamp,
                out int pdfPending))
          snap.PdfGeometryPendingStamp = pdfPending;
      }

      return true;
    }

    private static void EnsureDefaultNeedFlags(ModelDoc2 modelDoc, bool isPart)
    {
      if (isPart)
      {
        try
        {
          VelumDxfBatchDocumentHelper.TryEnsureNeedDxfDefaultIfPart(modelDoc);
        }
        catch (Exception ex)
        {
          Logger.Warning("Velum registry export-meta ensure NeedDxf: " + ex.Message);
        }
      }

      try
      {
        if (!VelumDrawingPathPropertyHelper.EnsureNeedDrawingFlag(modelDoc, out string message) &&
            !string.IsNullOrWhiteSpace(message) &&
            !string.Equals(message, "already_exists", StringComparison.OrdinalIgnoreCase))
        {
          Logger.Warning("Velum registry export-meta ensure NeedDrawing: " + message);
        }
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum registry export-meta ensure NeedDrawing: " + ex.Message);
      }
    }

    private static List<VelumProductExportMetaConfig> ReadPartConfigs(ModelDoc2 modelDoc)
    {
      var list = new List<VelumProductExportMetaConfig>();
      var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      IReadOnlyList<string> names = VelumDxfArtifactResolver.TryGetConfigurationNames(modelDoc);
      for (int i = 0; i < names.Count; i++)
      {
        string configName = names[i] ?? string.Empty;
        if (!seenNames.Add(configName))
          continue;

        var cfg = new VelumProductExportMetaConfig
        {
          ConfigName = configName,
          NeedDxf = VelumDxfNeedFlagResolver.IsExportable(modelDoc, configName),
          DxfFileName = VelumDxfArtifactResolver.TryReadPerConfigFileName(modelDoc, configName),
          DxfFileFingerprint = string.Empty
        };

        if (VelumDxfFileNameHelper.TryGetProjectionViewProperty(
                modelDoc, configName, out VelumDxfProjectionView view))
          cfg.DxfProjectionView = view.ToString();
        else
          cfg.DxfProjectionView = string.Empty;

        if (VelumDxfFinalizeService.TryReadStoredFingerprint(modelDoc, configName, out string fp))
          cfg.DxfFileFingerprint = fp ?? string.Empty;

        if (VelumExportDocumentationGeometryStampHelper.TryReadStoredUpdateStamp(
                modelDoc,
                VelumExportDocumentationProperties.DxfGeometryUpdateStamp,
                configName,
                out int exportStamp))
          cfg.DxfGeometryUpdateStamp = exportStamp;

        if (VelumExportDocumentationGeometryStampHelper.TryReadStoredUpdateStamp(
                modelDoc,
                VelumExportDocumentationProperties.DxfGeometryPendingStamp,
                configName,
                out int pendingStamp))
          cfg.DxfGeometryPendingStamp = pendingStamp;

        list.Add(cfg);
      }

      return list;
    }

    private static string NormalizeOptionalPath(string raw)
    {
      string value = (raw ?? string.Empty).Trim();
      return value.Length == 0 ? string.Empty : value;
    }

    private static bool TryOpenOrFind(
        ISwApplication swApp,
        string filePath,
        out ModelDoc2 modelDoc,
        out bool openedByUs,
        out string error)
    {
      return VelumProductRegistryNameSyncHelper.TryGetOrOpenDocument(
          swApp,
          filePath,
          out modelDoc,
          out openedByUs,
          out error);
    }

    private sealed class VelumProductExportMetaSnapshot
    {
      public bool? NeedDxf;
      public bool? NeedDrawing;
      public string DrawingPath = string.Empty;
      public string DxfPath = string.Empty;
      public bool? NeedPdf;
      public string PdfPath = string.Empty;
public int? PdfGeometryUpdateStamp;
      public int? PdfGeometryPendingStamp;
      public int? ModelGeometryStamp;
      public List<VelumProductExportMetaConfig> Configs = new List<VelumProductExportMetaConfig>();
      public bool ApplyDxfMeta = true;

      public string ComputeCompareKey()
      {
var sb = new System.Text.StringBuilder();
        sb.Append(NeedDxf).Append('|').Append(NeedDrawing).Append('|')
            .Append(DrawingPath).Append('|').Append(DxfPath).Append('|')
            .Append(NeedPdf).Append('|').Append(PdfPath).Append('|')
            .Append(PdfGeometryUpdateStamp).Append('|').Append(PdfGeometryPendingStamp)
            .Append('|').Append(ModelGeometryStamp);
        for (int i = 0; i < Configs.Count; i++)
        {
          VelumProductExportMetaConfig c = Configs[i];
          if (c == null)
            continue;
          sb.Append('#').Append(c.ConfigName).Append(';')
              .Append(c.NeedDxf).Append(';')
              .Append(c.DxfFileName).Append(';')
              .Append(c.DxfProjectionView).Append(';')
              .Append(c.DxfFileFingerprint).Append(';')
              .Append(c.DxfGeometryUpdateStamp).Append(';')
              .Append(c.DxfGeometryPendingStamp);
        }

        return sb.ToString();
      }

      public bool HasChangesAgainst(VelumProductItem item)
      {
        if (item == null)
          return true;
        if (ApplyDxfMeta && item.NeedDxf != NeedDxf)
          return true;
        if (NeedDrawing.HasValue && item.NeedDrawing != NeedDrawing.Value)
          return true;
        // null в реестре = зеркало ещё не писали — нужно сохранить даже пустую строку,
        // иначе MissingDrawing/DXF/PDF-сканеры вечно пропускают запись (DrawingPath == null /
        // !NeedDxf.HasValue после «тишины» StringEquals(null, "")).
        if (MirrorStringNeedsWrite(item.DrawingPath, DrawingPath))
          return true;
        if (ApplyDxfMeta && MirrorStringNeedsWrite(item.DxfPath, DxfPath))
          return true;
        if (item.NeedPdf != NeedPdf)
          return true;
        if (MirrorStringNeedsWrite(item.PdfPath, PdfPath))
          return true;
if (item.PdfGeometryUpdateStamp != PdfGeometryUpdateStamp)
          return true;
        if (item.PdfGeometryPendingStamp != PdfGeometryPendingStamp)
          return true;
        if (item.ModelGeometryStamp != ModelGeometryStamp)
          return true;

        VelumProductExportMetaConfig[] existing = item.ExportMetaConfigs
            ?? Array.Empty<VelumProductExportMetaConfig>();
        if (ApplyDxfMeta && existing.Length != Configs.Count)
          return true;

        if (!ApplyDxfMeta)
          return false;

        // Сверяем по имени конфигурации: удалённые из SW не должны оставаться в зеркале.
        var existingByName = new Dictionary<string, VelumProductExportMetaConfig>(
            StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < existing.Length; i++)
        {
          VelumProductExportMetaConfig a = existing[i];
          if (a == null)
            return true;
          string name = a.ConfigName ?? string.Empty;
          if (existingByName.ContainsKey(name))
            return true;
          existingByName[name] = a;
        }

        for (int i = 0; i < Configs.Count; i++)
        {
          VelumProductExportMetaConfig b = Configs[i];
          if (b == null)
            return true;
          string name = b.ConfigName ?? string.Empty;
          VelumProductExportMetaConfig a;
          if (!existingByName.TryGetValue(name, out a))
            return true;
          if (!StringEquals(a.DxfFileName, b.DxfFileName)
              || a.NeedDxf != b.NeedDxf
              || !StringEquals(a.DxfProjectionView, b.DxfProjectionView)
              || !StringEquals(a.DxfFileFingerprint, b.DxfFileFingerprint)
              || a.DxfGeometryUpdateStamp != b.DxfGeometryUpdateStamp
              || a.DxfGeometryPendingStamp != b.DxfGeometryPendingStamp)
            return true;
        }

        return false;
      }

      public void ApplyTo(VelumProductItem item)
      {
        if (ApplyDxfMeta)
        {
          item.NeedDxf = NeedDxf;
          item.DxfPath = DxfPath ?? string.Empty;
          item.ExportMetaConfigs = CloneConfigsSnapshot(Configs);
        }

        if (NeedDrawing.HasValue)
          item.NeedDrawing = NeedDrawing.Value;
        item.DrawingPath = DrawingPath ?? string.Empty;
        item.NeedPdf = NeedPdf;
        item.PdfPath = PdfPath ?? string.Empty;
item.PdfGeometryUpdateStamp = PdfGeometryUpdateStamp;
        item.PdfGeometryPendingStamp = PdfGeometryPendingStamp;
        item.ModelGeometryStamp = ModelGeometryStamp;
      }

      private static VelumProductExportMetaConfig[] CloneConfigsSnapshot(
          List<VelumProductExportMetaConfig> source)
      {
        if (source == null || source.Count == 0)
          return Array.Empty<VelumProductExportMetaConfig>();

        var copy = new VelumProductExportMetaConfig[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
          VelumProductExportMetaConfig c = source[i];
          if (c == null)
          {
            copy[i] = new VelumProductExportMetaConfig
            {
              ConfigName = string.Empty,
              DxfFileName = string.Empty,
              DxfProjectionView = string.Empty,
              DxfFileFingerprint = string.Empty
            };
            continue;
          }

          copy[i] = new VelumProductExportMetaConfig
          {
            ConfigName = c.ConfigName ?? string.Empty,
            NeedDxf = c.NeedDxf,
            DxfFileName = c.DxfFileName ?? string.Empty,
            DxfProjectionView = c.DxfProjectionView ?? string.Empty,
            DxfFileFingerprint = c.DxfFileFingerprint ?? string.Empty,
            DxfGeometryUpdateStamp = c.DxfGeometryUpdateStamp,
            DxfGeometryPendingStamp = c.DxfGeometryPendingStamp
          };
        }

        return copy;
      }

      /// <summary>
      /// true, если в реестре ещё null (не синхронизировали) или значение отличается от снимка.
      /// </summary>
      private static bool MirrorStringNeedsWrite(string itemValue, string snapValue)
      {
        if (itemValue == null)
          return true;
        return !string.Equals(itemValue, snapValue ?? string.Empty, StringComparison.Ordinal);
      }

      private static bool StringEquals(string a, string b)
      {
        return string.Equals(a ?? string.Empty, b ?? string.Empty, StringComparison.Ordinal);
      }
    }
  }
}
