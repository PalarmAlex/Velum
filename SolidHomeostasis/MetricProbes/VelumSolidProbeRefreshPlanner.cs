using System;
using System.Collections.Generic;
using ISIDA.Common;
using SolidWorks.Interop.sldworks;
using Velum.ReactiveCore;
using Xarial.XCad;

namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Планировщик пересбора снимка метрик: события SW только помечают категории устаревшими;
  /// решение и COM — только на такте пульса (дешёвый штамп + флаги).
  /// Отслеживание изменений вне пульса: запоминает UpdateStamp при остановке пульса и
  /// обнаруживает несохранённые изменения при старте пульса.
  /// </summary>
  internal static class VelumSolidProbeRefreshPlanner
  {
    private static readonly object Gate = new object();
    private static VelumSolidProbeCategory _pendingCategories = VelumSolidProbeCategory.None;
    private static string _trackedDocumentKey;
    private static int _lastProbedUpdateStamp = -1;
    private static bool _lastObservedSaveFlag;
    private static int _consecutiveIncompleteRefreshes;

    // Отслеживание изменений вне пульса: запоминаем stamp при остановке пульса
    // и обнаруживаем несохранённые изменения при старте пульса.
    private static int? _lastDocumentStampWhilePulseStopped;
    private static bool _hasUntrackedChanges;

    /// <summary>Пометить категории устаревшими (вызывается из обработчиков событий SW).</summary>
    internal static void MarkStale(VelumSolidProbeCategory categories)
    {
      if (categories == VelumSolidProbeCategory.None)
        return;
      lock (Gate)
      {
        _pendingCategories |= categories;
      }
    }

    /// <summary>
    /// Проверяет, есть ли устаревшие категории указанного типа.
    /// </summary>
    internal static bool HasStaleCategories(VelumSolidProbeCategory categories)
    {
      lock (Gate)
      {
        return (_pendingCategories & categories) != 0;
      }
    }

    /// <summary>
    /// Проверяет, нужно ли обновить метрики вне пульсации (для штампов DXF/PDF).
    /// </summary>
    internal static bool ShouldRefreshOffPulse(VelumSolidProbeCategory categories)
    {
      lock (Gate)
      {
        return (_pendingCategories & categories) != 0;
      }
    }

    /// <summary>
    /// Сбрасывает флаги устаревания для указанных категорий (после принудительного обновления).
    /// </summary>
    internal static void ClearStaleCategories(VelumSolidProbeCategory categories)
    {
      lock (Gate)
      {
        _pendingCategories &= ~categories;
      }
    }

    /// <summary>
    /// После экспорта PDF/DXF или закрытия диалога — пересобрать пробы наличия/версии файла.
    /// </summary>
    internal static void MarkExportDocumentationStale()
    {
      MarkStale(VelumSolidProbeCategory.ExportDocumentation);
    }

    /// <summary>Сброс при смене активного документа.</summary>
    internal static void ResetForDocumentChange()
    {
      lock (Gate)
      {
        // Раньше оставляли только Document — MarkExportDocumentationStale перед закрытием
        // модалки мог быть стёрт ActiveModelDocChange до пульса, и PDF-пробы в gate
        // оставались красными (MergeProbeResults сохраняет старые ключи).
        _pendingCategories = VelumSolidProbeCategory.Full;
        _trackedDocumentKey = null;
        _lastProbedUpdateStamp = -1;
        _lastObservedSaveFlag = false;
        _consecutiveIncompleteRefreshes = 0;
      }

      VelumExportDocumentationGeometryStampHelper.ResetGeometryEditTracking();
    }

    /// <summary>Сброс при старте цикла пульсации.</summary>
    internal static void ResetForNewPulseCycle(IXApplication app)
    {
      VelumSolidDocumentEditContext ctx = VelumSolidDocumentEditContextResolver.Resolve(app);
      lock (Gate)
      {
        _trackedDocumentKey = null;
        _lastProbedUpdateStamp = -1;
        _lastObservedSaveFlag = false;
        _consecutiveIncompleteRefreshes = 0;
        if (ctx != null && ctx.IsAssemblyDocument && !ctx.HasPartLevelProbeTarget)
          _pendingCategories = VelumSolidProbeCategory.Document;
        else
          _pendingCategories = VelumSolidProbeCategoryPolicy.ResolveCategoriesForContext(ctx);
      }
    }

    /// <summary>
    /// Зафиксировать UpdateStamp активного документа при остановке пульсации.
    /// Вызывается из VelumAddIn.StopPulse() и VelumEnginePulseBridge.OnPulsationStateChanged().
    /// </summary>
    internal static void CaptureDocumentStampOnPulseStop()
    {
      try
      {
        var swApp = VelumSolidEnvironmentBridge.TryGetSolidWorksApplication();
        if (swApp == null)
          return;

        var editContext = VelumSolidDocumentEditContextResolver.Resolve(swApp);
        if (editContext?.ActiveDocument == null)
          return;

        var activeModel = VelumSolidWorksModelDocHelper.TryGetActiveModelDoc2(swApp, editContext.ActiveDocument);
        if (activeModel != null &&
            VelumSolidWorksModelDocHelper.TryReadDocumentRevision(activeModel, out int stamp, out _))
        {
          _lastDocumentStampWhilePulseStopped = stamp;
        }
      }
      catch
      {
        // Не критично — если не удалось зафиксировать stamp, просто не обнаружим изменения
      }
    }

    /// <summary>
    /// Проверить наличие непроверенных изменений при включении пульсации.
    /// Сравнивает текущий UpdateStamp с зафиксированным при остановке пульса.
    /// Если изменения обнаружены — помечает полный пересбор.
    /// </summary>
    internal static void CheckForUntrackedChangesOnPulseStart()
    {
      lock (Gate)
      {
        if (!_lastDocumentStampWhilePulseStopped.HasValue)
          return;

        try
        {
          var swApp = VelumSolidEnvironmentBridge.TryGetSolidWorksApplication();
          if (swApp == null)
            return;

          var editContext = VelumSolidDocumentEditContextResolver.Resolve(swApp);
          if (editContext?.ActiveDocument == null)
            return;

          var activeModel = VelumSolidWorksModelDocHelper.TryGetActiveModelDoc2(swApp, editContext.ActiveDocument);
          if (activeModel != null &&
              VelumSolidWorksModelDocHelper.TryReadDocumentRevision(activeModel, out int currentStamp, out _))
          {
            if (currentStamp != _lastDocumentStampWhilePulseStopped.Value)
            {
              _hasUntrackedChanges = true;
              _pendingCategories |= VelumSolidProbeCategory.Full;
            }
          }
        }
        catch
        {
          // Не критично
        }

        _lastDocumentStampWhilePulseStopped = null;
      }
    }

    /// <summary>
    /// true, если были изменения документа, обнаруженные вне пульса и ещё не учтённые метриками.
    /// </summary>
    internal static bool HasUntrackedChanges
    {
      get { lock (Gate) return _hasUntrackedChanges; }
    }

    /// <summary>
    /// Сбросить флаг непроверенных изменений после обработки.
    /// </summary>
    internal static void ClearUntrackedChangesFlag()
    {
      lock (Gate) { _hasUntrackedChanges = false; }
    }

    internal static void Clear()
    {
      lock (Gate)
      {
        _pendingCategories = VelumSolidProbeCategory.None;
        _trackedDocumentKey = null;
        _lastProbedUpdateStamp = -1;
        _lastObservedSaveFlag = false;
        _consecutiveIncompleteRefreshes = 0;
      }
    }

    /// <summary>
    /// На такте пульса (UI-поток): нужен ли пересбор снимка и какие категории.
    /// </summary>
    internal static bool TryPlanRefreshOnPulse(
        IXApplication app,
        out VelumSolidProbeCategory categoriesToCollect,
        out VelumSolidDocumentEditContext editContext,
        out int currentUpdateStamp)
    {
      categoriesToCollect = VelumSolidProbeCategory.None;
      editContext = VelumSolidDocumentEditContextResolver.Resolve(app);
      currentUpdateStamp = -1;

      if (editContext?.ActiveDocument == null)
        return false;

      ModelDoc2 activeModel =
          VelumSolidWorksModelDocHelper.TryGetActiveModelDoc2(app, editContext.ActiveDocument);

      bool hasStamp = VelumSolidWorksModelDocHelper.TryReadDocumentRevision(
          activeModel,
          out int updateStamp,
          out bool saveFlag);

      if (hasStamp)
        currentUpdateStamp = updateStamp;

      bool drawingGeometryChanged = false;
      if (editContext.IsDrawingDocument && activeModel != null)
      {
        try
        {
          drawingGeometryChanged =
              VelumExportDocumentationGeometryStampHelper.TryDetectAndSyncDrawingPdfPendingOnPulse(activeModel);
        }
        catch
        {
        }
      }

      string docKey = editContext?.DocumentKey;
      bool hasPublishedSnapshot = HasPublishedSnapshot();

      lock (Gate)
      {
        if (!string.IsNullOrEmpty(docKey) &&
            !string.Equals(docKey, _trackedDocumentKey, StringComparison.Ordinal))
        {
          _trackedDocumentKey = docKey;
          _pendingCategories |= VelumSolidProbeCategoryPolicy.ResolveCategoriesForContext(editContext);
          _lastProbedUpdateStamp = -1;
        }

        if (drawingGeometryChanged)
          _pendingCategories |= VelumSolidProbeCategory.ExportDocumentation;

        if (hasStamp)
        {
          if (updateStamp != _lastProbedUpdateStamp)
            _pendingCategories |= VelumSolidProbeCategoryPolicy.ResolveCategoriesForContext(editContext);

          // clean → dirty после Save: пересобрать метрики. Pending PDF не пишем —
          // Rebuild от Save детали тоже ставит dirty, это не правка листа.
          if (!_lastObservedSaveFlag && saveFlag)
          {
            _pendingCategories |= VelumSolidProbeCategory.Document;
            if (editContext != null && editContext.IsDrawingDocument)
            {
              _pendingCategories |= VelumSolidProbeCategory.ExportDocumentation;
            }
            else if (editContext != null && editContext.HasPartLevelProbeTarget)
            {
              _pendingCategories |= VelumSolidProbeCategory.PartMaterial |
                                    VelumSolidProbeCategory.ExportDocumentation;
            }
          }

          if (_lastObservedSaveFlag && !saveFlag)
          {
            _pendingCategories |= VelumSolidProbeCategory.Document;
            if (editContext != null && editContext.HasPartLevelProbeTarget)
            {
              _pendingCategories |= VelumSolidProbeCategory.PartMaterial |
                                    VelumSolidProbeCategory.ExportDocumentation;
            }
            else if (editContext != null && editContext.IsDrawingDocument)
            {
              _pendingCategories |= VelumSolidProbeCategory.ExportDocumentation;
            }
          }

          _lastObservedSaveFlag = saveFlag;
        }

        if (!hasPublishedSnapshot)
          _pendingCategories |= VelumSolidProbeCategoryPolicy.ResolveCategoriesForContext(editContext);

        categoriesToCollect = _pendingCategories;
        if (categoriesToCollect != VelumSolidProbeCategory.None)
          categoriesToCollect |= VelumSolidProbeCategory.HostGlobal;

        bool deferForIncomplete =
            categoriesToCollect != VelumSolidProbeCategory.None &&
            _consecutiveIncompleteRefreshes >= 2 &&
            !drawingGeometryChanged;

        if (deferForIncomplete)
        {
          int pulse = GlobalTimer.GlobalPulsCount;
          if (pulse > 0 && pulse % 3 != 0)
            return false;
        }
      }

      return categoriesToCollect != VelumSolidProbeCategory.None;
    }

    /// <summary>Успешный пересбор всех запрошенных ключей на этом пульсе.</summary>
    internal static void NotifyRefreshCompleted(int updateStamp)
    {
      lock (Gate)
      {
        if (updateStamp >= 0)
          _lastProbedUpdateStamp = updateStamp;
        _pendingCategories = VelumSolidProbeCategory.None;
        _consecutiveIncompleteRefreshes = 0;
      }
    }

    /// <summary>Таймаут или частичный сбор — на следующем пульсе повторим с теми же флагами.</summary>
    internal static void NotifyRefreshIncomplete()
    {
      lock (Gate)
      {
        if (_consecutiveIncompleteRefreshes < int.MaxValue)
          _consecutiveIncompleteRefreshes++;
      }
    }

    private static bool HasPublishedSnapshot()
    {
      IReadOnlyDictionary<string, float> snap = VelumSolidEnvironmentGate.GetPublishedSnapshot();
      return snap != null && snap.Count > 0;
    }
  }
}
