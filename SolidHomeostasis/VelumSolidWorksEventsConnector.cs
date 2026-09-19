using System;
using System.IO;
using ISIDA.Common;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Velum.Configuration;
using Velum.Isida;
using Velum.ReactiveCore;
using Velum.ReactiveCore.Export;
using Velum.UI.AssemblyRegistry;
using Velum.UI.ProductRegistry;
using Xarial.XCad.SolidWorks;

namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Подписка на события SolidWorks: смена активного документа, регенерация, Save, Modify,
  /// <c>CommandOpenPreNotify</c> для записи команд в <see cref="VelumSolidCommandBuffer"/>.
  /// Отслеживание изменений (MarkStale, геометрия, штампы) работает при готовом ISIDA независимо
  /// от пульсации. Запись в CommandBuffer и адаптер — только при активной пульсации.
  /// События только помечают категории проб устаревшими (<see cref="VelumSolidProbeRefreshPlanner"/>);
  /// COM-опрос — на такте пульса в <see cref="VelumSolidEnvironmentBridge"/>.
  /// </summary>
  internal static class VelumSolidWorksEventsConnector
  {
    private static SldWorks _sw;
    private static ISwApplication _swApp;
    private static DSldWorksEvents_ActiveModelDocChangeNotifyEventHandler _activeModelDocChangeNotify;
    private static DSldWorksEvents_CommandOpenPreNotifyEventHandler _commandOpenPreNotify;

    private static PartDoc _part;
    private static AssemblyDoc _assy;
    private static DrawingDoc _drawing;
    private static DPartDocEvents_RegenPostNotify2EventHandler _partRegen;
    private static DAssemblyDocEvents_RegenPostNotify2EventHandler _assyRegen;
    private static DPartDocEvents_ModifyNotifyEventHandler _partModify;
    private static DAssemblyDocEvents_ModifyNotifyEventHandler _assyModify;
    private static DPartDocEvents_FileSavePostNotifyEventHandler _partSavePost;
    private static DPartDocEvents_FileSaveNotifyEventHandler _partSavePre;
    private static DPartDocEvents_FileSaveAsNotify2EventHandler _partSaveAsPre;
    private static DAssemblyDocEvents_FileSavePostNotifyEventHandler _assySavePost;
    private static DAssemblyDocEvents_FileSaveNotifyEventHandler _assySavePre;
    private static DAssemblyDocEvents_FileSaveAsNotify2EventHandler _assySaveAsPre;
    private static DDrawingDocEvents_FileSavePostNotifyEventHandler _drawingSavePost;
    private static DDrawingDocEvents_FileSaveNotifyEventHandler _drawingSavePre;
    private static DDrawingDocEvents_FileSaveAsNotify2EventHandler _drawingSaveAsPre;
    private static DDrawingDocEvents_ModifyNotifyEventHandler _drawingModify;
    private static DDrawingDocEvents_AddItemNotifyEventHandler _drawingAddItem;
    private static DDrawingDocEvents_DeleteItemNotifyEventHandler _drawingDeleteItem;
    private static DDrawingDocEvents_DimensionChangeNotifyEventHandler _drawingDimensionChange;
    private static DDrawingDocEvents_SketchSolveNotifyEventHandler _drawingSketchSolve;
    private static DDrawingDocEvents_UndoPostNotifyEventHandler _drawingUndoPost;
    private static DDrawingDocEvents_RedoPostNotifyEventHandler _drawingRedoPost;
    private static DDrawingDocEvents_ViewNewNotify2EventHandler _drawingViewNew;
    private static DDrawingDocEvents_RegenPostNotifyEventHandler _drawingRegenPost;
    private static int _activeModelDocChangeDepth;

    private static readonly VelumSolidProbeCategory RegenStaleCategories =
        VelumSolidProbeCategory.PartMaterial;

    private static readonly VelumSolidProbeCategory ModifyStaleCategories =
        VelumSolidProbeCategory.PartMaterial |
        VelumSolidProbeCategory.ExportDocumentation;

    private static readonly VelumSolidProbeCategory DrawingChangeStaleCategories =
        VelumSolidProbeCategory.ExportDocumentation;

    private static readonly VelumSolidProbeCategory SaveStaleCategories =
        VelumSolidProbeCategory.Document |
        VelumSolidProbeCategory.PartMaterial |
        VelumSolidProbeCategory.ExportDocumentation;

    /// <summary>true, если повешена подписка на события приложения.</summary>
    internal static bool IsEventSubscriptionActive => _sw != null;

    internal static void Attach(ISwApplication app)
    {
      Detach();
      if (app?.Sw == null)
        return;

      _sw = app.Sw as SldWorks;
      if (_sw == null)
        return;
      _swApp = app;

      _activeModelDocChangeNotify = OnActiveModelDocChangeNotify;
      _sw.ActiveModelDocChangeNotify += _activeModelDocChangeNotify;

      _commandOpenPreNotify = OnCommandOpenPreNotify;
      _sw.CommandOpenPreNotify += _commandOpenPreNotify;

      try
      {
        ModelDoc2 activeDoc = _sw.IActiveDoc2 as ModelDoc2;
        HookModelDoc(activeDoc);
      }
      catch
      {
      }
    }

    /// <summary>Переподписать события активного документа (после старта пульсации и смены документа).</summary>
    internal static void RehookActiveDocument(ISwApplication app)
    {
      if (app?.Sw == null)
        return;

      try
      {
        ModelDoc2 activeDoc = app.Sw.IActiveDoc2 as ModelDoc2;
        HookModelDoc(activeDoc);
      }
      catch
      {
      }
    }

    internal static void Detach()
    {
      UnhookModelDoc();

      if (_sw != null && _activeModelDocChangeNotify != null)
      {
        try
        {
          _sw.ActiveModelDocChangeNotify -= _activeModelDocChangeNotify;
        }
        catch
        {
        }
      }

      if (_sw != null && _commandOpenPreNotify != null)
      {
        try
        {
          _sw.CommandOpenPreNotify -= _commandOpenPreNotify;
        }
        catch
        {
        }
      }

      _activeModelDocChangeNotify = null;
      _commandOpenPreNotify = null;
      _swApp = null;
      _sw = null;
    }

    private static int OnActiveModelDocChangeNotify()
    {
      if (!IsSolidWorksEventsAllowed())
        return 0;

      if (_activeModelDocChangeDepth > 0)
        return 0;

      _activeModelDocChangeDepth++;
      try
      {
        VelumSolidEnvironmentBridge.OnActiveSolidDocumentChanged();
        try
        {
          ModelDoc2 activeDoc = _sw != null ? _sw.IActiveDoc2 as ModelDoc2 : null;
          HookModelDoc(activeDoc);
        }
        catch
        {
        }
      }
      finally
      {
        if (_activeModelDocChangeDepth > 0)
          _activeModelDocChangeDepth--;
      }

      return 0;
    }

    private static int OnCommandOpenPreNotify(int command, int userCommand)
    {
      // Запись в CommandBuffer — только при пульсации (реакция адаптера)
      if (ShouldRecordSolidWorksEvents())
      {
        try
        {
          int id = command != 0 ? command : userCommand;
          VelumSolidCommandBuffer.TryAppendSwCommand(id);
          VelumSolidEnvironmentBridge.NotifySolidCommandBufferChanged();
        }
        catch
        {
        }
      }

      // ClearPartSaveRebuildAbsorb — всегда (нужен для корректной работы штампов геометрии)
      if (IsSolidWorksEventsAllowed())
      {
        VelumExportDocumentationGeometryStampHelper.ClearPartSaveRebuildAbsorb();
      }

      return 0;
    }

    private static void HookModelDoc(ModelDoc2 md)
    {
      UnhookModelDoc();
      if (md == null)
        return;

      _part = md as PartDoc;
      if (_part != null)
      {
        _partRegen = OnPartRegenPostNotify2;
        _part.RegenPostNotify2 += _partRegen;
        _partModify = OnPartModifyNotify;
        _part.ModifyNotify += _partModify;
        _partSavePre = OnPartFileSaveNotify;
        _part.FileSaveNotify += _partSavePre;
        _partSaveAsPre = OnPartFileSaveAsNotify2;
        _part.FileSaveAsNotify2 += _partSaveAsPre;
        _partSavePost = OnPartFileSavePostNotify;
        _part.FileSavePostNotify += _partSavePost;
        try
        {
          VelumExportDocumentationGeometryStampHelper.CaptureFlatPatternFoldSignature(md);
        }
        catch
        {
        }
        return;
      }

      _assy = md as AssemblyDoc;
      if (_assy != null)
      {
        _assyRegen = OnAssyRegenPostNotify2;
        _assy.RegenPostNotify2 += _assyRegen;
        _assyModify = OnAssyModifyNotify;
        _assy.ModifyNotify += _assyModify;
        _assySavePre = OnAssyFileSaveNotify;
        _assy.FileSaveNotify += _assySavePre;
        _assySaveAsPre = OnAssyFileSaveAsNotify2;
        _assy.FileSaveAsNotify2 += _assySaveAsPre;
        _assySavePost = OnAssyFileSavePostNotify;
        _assy.FileSavePostNotify += _assySavePost;
        return;
      }

      _drawing = md as DrawingDoc;
      if (_drawing != null)
      {
        _drawingSavePost = OnDrawingFileSavePostNotify;
        _drawing.FileSavePostNotify += _drawingSavePost;
        _drawingSavePre = OnDrawingFileSaveNotify;
        _drawing.FileSaveNotify += _drawingSavePre;
        _drawingSaveAsPre = OnDrawingFileSaveAsNotify2;
        _drawing.FileSaveAsNotify2 += _drawingSaveAsPre;
        // ModifyNotify у чертежей часто не приходит на размеры/эскизы/виды —
        // дублируем через предметные события листа.
        _drawingModify = OnDrawingRebuildOrDirty;
        _drawing.ModifyNotify += _drawingModify;
        _drawingAddItem = OnDrawingAddItemNotify;
        _drawing.AddItemNotify += _drawingAddItem;
        _drawingDeleteItem = OnDrawingDeleteItemNotify;
        _drawing.DeleteItemNotify += _drawingDeleteItem;
        _drawingDimensionChange = OnDrawingDimensionChangeNotify;
        _drawing.DimensionChangeNotify += _drawingDimensionChange;
        _drawingSketchSolve = OnDrawingSketchSolveNotify;
        _drawing.SketchSolveNotify += _drawingSketchSolve;
        _drawingUndoPost = OnDrawingSheetEdited;
        _drawing.UndoPostNotify += _drawingUndoPost;
        _drawingRedoPost = OnDrawingSheetEdited;
        _drawing.RedoPostNotify += _drawingRedoPost;
        _drawingViewNew = OnDrawingViewNewNotify2;
        _drawing.ViewNewNotify2 += _drawingViewNew;
        _drawingRegenPost = OnDrawingRebuildOrDirty;
        _drawing.RegenPostNotify += _drawingRegenPost;
      }
    }

    private static void UnhookModelDoc()
    {
      if (_part != null)
      {
        if (_partRegen != null)
        {
          try
          {
            _part.RegenPostNotify2 -= _partRegen;
          }
          catch
          {
          }
        }

        if (_partModify != null)
        {
          try
          {
            _part.ModifyNotify -= _partModify;
          }
          catch
          {
          }
        }

        if (_partSavePost != null)
        {
          try
          {
            _part.FileSavePostNotify -= _partSavePost;
          }
          catch
          {
          }
        }

        if (_partSavePre != null)
        {
          try
          {
            _part.FileSaveNotify -= _partSavePre;
          }
          catch
          {
          }
        }

        if (_partSaveAsPre != null)
        {
          try
          {
            _part.FileSaveAsNotify2 -= _partSaveAsPre;
          }
          catch
          {
          }
        }
      }

      _part = null;
      _partRegen = null;
      _partModify = null;
      _partSavePost = null;
      _partSavePre = null;
      _partSaveAsPre = null;

      if (_assy != null)
      {
        if (_assyRegen != null)
        {
          try
          {
            _assy.RegenPostNotify2 -= _assyRegen;
          }
          catch
          {
          }
        }

        if (_assyModify != null)
        {
          try
          {
            _assy.ModifyNotify -= _assyModify;
          }
          catch
          {
          }
        }

        if (_assySavePost != null)
        {
          try
          {
            _assy.FileSavePostNotify -= _assySavePost;
          }
          catch
          {
          }
        }

        if (_assySavePre != null)
        {
          try
          {
            _assy.FileSaveNotify -= _assySavePre;
          }
          catch
          {
          }
        }

        if (_assySaveAsPre != null)
        {
          try
          {
            _assy.FileSaveAsNotify2 -= _assySaveAsPre;
          }
          catch
          {
          }
        }
      }

      _assy = null;
      _assyRegen = null;
      _assyModify = null;
      _assySavePost = null;
      _assySavePre = null;
      _assySaveAsPre = null;

      if (_drawing != null)
      {
        if (_drawingSavePost != null)
        {
          try
          {
            _drawing.FileSavePostNotify -= _drawingSavePost;
          }
          catch
          {
          }
        }

        if (_drawingSavePre != null)
        {
          try
          {
            _drawing.FileSaveNotify -= _drawingSavePre;
          }
          catch
          {
          }
        }

        if (_drawingSaveAsPre != null)
        {
          try
          {
            _drawing.FileSaveAsNotify2 -= _drawingSaveAsPre;
          }
          catch
          {
          }
        }

        if (_drawingModify != null)
        {
          try
          {
            _drawing.ModifyNotify -= _drawingModify;
          }
          catch
          {
          }
        }

        if (_drawingAddItem != null)
        {
          try
          {
            _drawing.AddItemNotify -= _drawingAddItem;
          }
          catch
          {
          }
        }

        if (_drawingDeleteItem != null)
        {
          try
          {
            _drawing.DeleteItemNotify -= _drawingDeleteItem;
          }
          catch
          {
          }
        }

        if (_drawingDimensionChange != null)
        {
          try
          {
            _drawing.DimensionChangeNotify -= _drawingDimensionChange;
          }
          catch
          {
          }
        }

        if (_drawingSketchSolve != null)
        {
          try
          {
            _drawing.SketchSolveNotify -= _drawingSketchSolve;
          }
          catch
          {
          }
        }

        if (_drawingUndoPost != null)
        {
          try
          {
            _drawing.UndoPostNotify -= _drawingUndoPost;
          }
          catch
          {
          }
        }

        if (_drawingRedoPost != null)
        {
          try
          {
            _drawing.RedoPostNotify -= _drawingRedoPost;
          }
          catch
          {
          }
        }

        if (_drawingViewNew != null)
        {
          try
          {
            _drawing.ViewNewNotify2 -= _drawingViewNew;
          }
          catch
          {
          }
        }

        if (_drawingRegenPost != null)
        {
          try
          {
            _drawing.RegenPostNotify -= _drawingRegenPost;
          }
          catch
          {
          }
        }
      }

      _drawing = null;
      _drawingSavePost = null;
      _drawingSavePre = null;
      _drawingSaveAsPre = null;
      _drawingModify = null;
      _drawingAddItem = null;
      _drawingDeleteItem = null;
      _drawingDimensionChange = null;
      _drawingSketchSolve = null;
      _drawingUndoPost = null;
      _drawingRedoPost = null;
      _drawingViewNew = null;
      _drawingRegenPost = null;
    }

    /// <summary>
    /// true, если события SW можно обрабатывать для отслеживания изменений документа.
    /// Требуется готовый контекст ISIDA, но пульсация не обязательна — отслеживание
    /// изменений является самостоятельным свойством системы, а не частью гомеостаза.
    /// </summary>
    private static bool IsSolidWorksEventsAllowed()
    {
      try
      {
        return VelumIsidaHost.IsReady;
      }
      catch
      {
        return false;
      }
    }

    /// <summary>
    /// true, если события SW можно обрабатывать для записи в адаптер (CommandBuffer и т.п.).
    /// Требует активной пульсации — это реакция гомеостаза, а не отслеживание изменений.
    /// </summary>
    private static bool ShouldRecordSolidWorksEvents()
    {
      try
      {
        return GlobalTimer.IsPulsationRunning;
      }
      catch
      {
        return false;
      }
    }

    private static int OnPartRegenPostNotify2(object regeneratingObject)
    {
      if (!IsSolidWorksEventsAllowed())
        return 0;

      NotifyPartGeometryModifiedForCurrentDocument();
      VelumSolidProbeRefreshPlanner.MarkStale(RegenStaleCategories | VelumSolidProbeCategory.ExportDocumentation);

      // Принудительное обновление штампов вне пульсации
      VelumSolidEnvironmentBridge.RefreshMetricsOffPulse(VelumSolidProbeCategory.ExportDocumentation);

      return 0;
    }

    private static int OnAssyRegenPostNotify2(object regeneratingObject)
    {
      if (!IsSolidWorksEventsAllowed())
        return 0;

      NotifyPartGeometryModifiedForCurrentDocument();
      VelumSolidProbeRefreshPlanner.MarkStale(RegenStaleCategories | VelumSolidProbeCategory.ExportDocumentation);
      return 0;
    }

    private static int OnPartModifyNotify()
    {
      if (!IsSolidWorksEventsAllowed())
        return 0;

      NotifyPartGeometryModifiedForCurrentDocument();
      VelumSolidProbeRefreshPlanner.MarkStale(ModifyStaleCategories);

      // Принудительное обновление штампов вне пульсации
      VelumSolidEnvironmentBridge.RefreshMetricsOffPulse(VelumSolidProbeCategory.ExportDocumentation);

      return 0;
    }

    private static int OnAssyModifyNotify()
    {
      if (!IsSolidWorksEventsAllowed())
        return 0;

      NotifyPartGeometryModifiedForCurrentDocument();
      VelumSolidProbeRefreshPlanner.MarkStale(ModifyStaleCategories);
      // Уведомляем планировщик о возможном изменении состава сборки
      VelumProductRegistryIntegrityScheduler.NotifyAssemblyStructureChanged();
      return 0;
    }

    private static int OnDrawingAddItemNotify(int entityType, string itemName)
    {
      return OnDrawingSheetEdited();
    }

    private static int OnDrawingDeleteItemNotify(int entityType, string itemName)
    {
      return OnDrawingSheetEdited();
    }

    private static int OnDrawingDimensionChangeNotify(object displayDim)
    {
      return OnDrawingSheetEdited();
    }

    private static int OnDrawingViewNewNotify2(object viewBeingAdded)
    {
      return OnDrawingSheetEdited();
    }

    private static int OnDrawingSketchSolveNotify(string featName)
    {
      return OnDrawingRebuildOrDirty();
    }

    /// <summary>
    /// Rebuild / dirty: pending только если токен листа сдвинулся.
    /// Save детали перестраивает виды — без этого PDF и DXF гоняют друг друга.
    /// </summary>
    private static int OnDrawingRebuildOrDirty()
    {
      return OnDrawingContentChanged(explicitSheetEdit: false);
    }

    private static int OnDrawingSheetEdited()
    {
      return OnDrawingContentChanged(explicitSheetEdit: true);
    }

    private static int OnDrawingContentChanged(bool explicitSheetEdit)
    {
      if (!IsSolidWorksEventsAllowed())
        return 0;

      try
      {
        ModelDoc2 drawing = _drawing as ModelDoc2;
        if (drawing != null)
        {
          if (explicitSheetEdit)
            VelumExportDocumentationGeometryStampHelper.NotifyDrawingSheetEdited(drawing);
          else
            VelumExportDocumentationGeometryStampHelper.NotifyDrawingModified(drawing);
        }
      }
      catch
      {
      }

      VelumSolidProbeRefreshPlanner.MarkStale(DrawingChangeStaleCategories);
      return 0;
    }

    private static int OnPartFileSaveNotify(string fileName)
    {
      if (!IsSolidWorksEventsAllowed())
        return 0;
      if (!IsNativePartOrAssemblySavePath(fileName, ".sldprt"))
        return 0;

      VelumExportDocumentationGeometryStampHelper.BeginPartSave();
      VelumNeedDrawingOnSaveCoordinator.TryEnsureBeforeSaveIfEligible(_part as ModelDoc2);
      return 0;
    }

    private static int OnPartFileSaveAsNotify2(string fileName)
    {
      if (!IsSolidWorksEventsAllowed())
        return 0;
      if (!IsNativePartOrAssemblySavePath(fileName, ".sldprt"))
        return 0;

      VelumExportDocumentationGeometryStampHelper.BeginPartSave();
      VelumNeedDrawingOnSaveCoordinator.TryEnsureBeforeSaveIfEligible(_part as ModelDoc2);
      return 0;
    }

    private static int OnAssyFileSaveNotify(string fileName)
    {
      if (!IsSolidWorksEventsAllowed())
        return 0;
      if (!IsNativePartOrAssemblySavePath(fileName, ".sldasm"))
        return 0;

      VelumNeedDrawingOnSaveCoordinator.TryEnsureBeforeSaveIfEligible(_assy as ModelDoc2);
      return 0;
    }

    private static int OnAssyFileSaveAsNotify2(string fileName)
    {
      if (!IsSolidWorksEventsAllowed())
        return 0;
      if (!IsNativePartOrAssemblySavePath(fileName, ".sldasm"))
        return 0;

      VelumNeedDrawingOnSaveCoordinator.TryEnsureBeforeSaveIfEligible(_assy as ModelDoc2);
      return 0;
    }

    private static int OnPartFileSavePostNotify(int saveType, string fileName)
    {
      try
      {
        if (!IsSolidWorksEventsAllowed())
          return 0;

        try
        {
          ModelDoc2 part = _part as ModelDoc2;
          if (part != null &&
              VelumExportDocumentationGeometryStampHelper.NotifyPartSaved(part))
          {
            VelumSolidProbeRefreshPlanner.MarkStale(VelumSolidProbeCategory.ExportDocumentation);
            // Принудительное обновление при сохранении
            VelumSolidEnvironmentBridge.RefreshMetricsOffPulse(VelumSolidProbeCategory.ExportDocumentation);
            // синхронизация зеркал реестра сразу после обновления штампов
            VelumProductRegistryExportMetaSync.TrySyncStampsAfterSave(part);
          }
        }
        catch
        {
        }

        VelumSolidProbeRefreshPlanner.MarkStale(SaveStaleCategories);
        return OnModelFileSavePostNotify(_part as ModelDoc2, fileName);
      }
      finally
      {
        VelumExportDocumentationGeometryStampHelper.EndPartSave();
      }
    }

    private static int OnAssyFileSavePostNotify(int saveType, string fileName)
    {
      if (!IsSolidWorksEventsAllowed())
        return 0;

      VelumSolidProbeRefreshPlanner.MarkStale(SaveStaleCategories);
      // При сохранении сборки всегда уведомляем о необходимости синхронизации состава
      VelumProductRegistryIntegrityScheduler.NotifyAssemblyStructureChanged();
      // синхронизация зеркал в реестре
      VelumProductRegistryExportMetaSync.TrySyncStampsAfterSave(_assy as ModelDoc2);

      return OnModelFileSavePostNotify(_assy as ModelDoc2, fileName);
    }

    private static int OnDrawingFileSaveNotify(string fileName)
    {
      // SaveAs PDF тоже шлёт save-события — не путать с сохранением SLDDRW.
      if (!IsNativeDrawingSavePath(fileName))
        return 0;

      if (!IsSolidWorksEventsAllowed())
        return 0;

      VelumExportDocumentationGeometryStampHelper.BeginDrawingSave();
      return 0;
    }

    private static int OnDrawingFileSaveAsNotify2(string fileName)
    {
      if (!IsNativeDrawingSavePath(fileName))
        return 0;

      if (!IsSolidWorksEventsAllowed())
        return 0;

      VelumExportDocumentationGeometryStampHelper.BeginDrawingSave();
      return 0;
    }

    private static int OnDrawingFileSavePostNotify(int saveType, string fileName)
    {
      if (!IsNativeDrawingSavePath(fileName))
      {
        VelumSolidProbeRefreshPlanner.MarkStale(SaveStaleCategories);


        return OnModelFileSavePostNotify(_drawing as ModelDoc2, fileName);
      }

      if (!IsSolidWorksEventsAllowed())
      {
        VelumExportDocumentationGeometryStampHelper.EndDrawingSave();
        return 0;
      }

      try
      {
        ModelDoc2 drawing = _drawing as ModelDoc2;
        if (drawing != null &&
            VelumExportDocumentationGeometryStampHelper.NotifyDrawingSaved(drawing))
        {
          // Ложный pending от Save после PDF-экспорта снят — пересобрать метрику.
          VelumSolidProbeRefreshPlanner.MarkStale(DrawingChangeStaleCategories);
          // синхронизация зеркал в реестре
          VelumProductRegistryExportMetaSync.TrySyncStampsAfterSave(drawing);
        }

        VelumDrawingPathSavePropagator.TryPropagateFromSavedDrawing(_sw, drawing);
      }
      catch
      {
      }
      finally
      {
        VelumExportDocumentationGeometryStampHelper.EndDrawingSave();
      }

      VelumSolidProbeRefreshPlanner.MarkStale(SaveStaleCategories);
      return OnModelFileSavePostNotify(_drawing as ModelDoc2, fileName);
    }

    /// <summary>
    /// true для сохранения самого чертежа (.slddrw). SaveAs в PDF/другие форматы — false:
    /// иначе Begin/EndDrawingSave и NotifyDrawingSaved срывают absorb штампов после экспорта.
    /// </summary>
    private static bool IsNativeDrawingSavePath(string fileName)
    {
      return VelumSolidWorksSaveFileNameHelper.IsNativeSavePath(fileName, ".slddrw");
    }

    private static bool IsNativePartOrAssemblySavePath(string fileName, string expectedExtension)
    {
      return VelumSolidWorksSaveFileNameHelper.IsNativeSavePath(fileName, expectedExtension);
    }

    private static int OnModelFileSavePostNotify(ModelDoc2 modelDoc, string fileName)
    {
      try
      {
        // Служебное свойство связи с 1С — после сохранения, когда документ разблокирован.
        // Работает независимо от состояния пульсации. Только для деталей и сборок, не для чертежей.
        // Пишем лишь при нативном сохранении: FileSavePostNotify приходит и на экспорт
        // (Save As → DXF/PDF), а запись свойства в этот момент рвёт PropertyManager экспорта.
        try
        {
          int docType = modelDoc.GetType();
          string nativeExtension =
              docType == (int)swDocumentTypes_e.swDocPART ? ".sldprt" :
              docType == (int)swDocumentTypes_e.swDocASSEMBLY ? ".sldasm" : null;

          if (nativeExtension != null &&
              VelumSolidWorksSaveFileNameHelper.IsNativeSavePath(fileName, nativeExtension))
          {
            Velum.UI.AssemblyRegistry.VelumAssemblyBomMirrorCoordinator.EnsureExternalIdProperty(modelDoc);
          }
        }
        catch
        {
          // Ignore.
        }

        VelumFileNameSequenceState.TryLearnFromSavedDocument(modelDoc, fileName);
        VelumMaterialSequenceState.TryLearnFromSavedDocument(modelDoc, fileName);
        // id3 / AA 40: материал сразу после Save Part (рецепт — ручная починка по триггеру).
        VelumMaterialAssignOnSaveCoordinator.TryAssignAfterSaveIfEligible(modelDoc, fileName);
        // id7 / AA 45: ссылки габарита заготовки сразу после Save Part.
        VelumBlankSizeLinksOnSaveCoordinator.TryEnsureAfterSaveIfEligible(modelDoc, fileName);
        // BOM mirror: зеркалирование состава/свойств при сохранении.
        string ext = null;
        try { ext = System.IO.Path.GetExtension(fileName); } catch { }
        if (string.Equals(ext, ".sldasm", StringComparison.OrdinalIgnoreCase))
        {
          VelumAssemblyBomMirrorCoordinator.TryMirrorSavedAssembly(modelDoc, fileName);
        }
        else if (string.Equals(ext, ".sldprt", StringComparison.OrdinalIgnoreCase))
        {
          VelumAssemblyBomMirrorCoordinator.TryMirrorSavedPart(modelDoc, fileName);
        }
        VelumProductRegistryExportMetaSync.TrySyncOpenDocumentFromDisk(modelDoc);
      }
      catch
      {
      }

      return 0;
    }

    private static void NotifyPartGeometryModifiedForCurrentDocument()
    {
      if (_part != null)
      {
        VelumExportDocumentationGeometryStampHelper.NotifyPartGeometryModified(_part as ModelDoc2);
        return;
      }

      if (_assy == null)
        return;

      try
      {
        object target = _assy.GetEditTarget();
        if (target is ModelDoc2 targetMd)
          VelumExportDocumentationGeometryStampHelper.NotifyPartGeometryModified(targetMd);
      }
      catch
      {
      }
    }
  }
}
