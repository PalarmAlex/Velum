using System;
using System.Collections.Generic;
using System.Globalization;
using ISIDA.Common;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Velum.ReactiveCore.Export;
using Velum.SolidHomeostasis;
using Velum.UI.ProductRegistry;
using Xarial.XCad.SolidWorks;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Штампы ревизии документа (ModelDoc2.GetUpdateStamp) для метрик «файл устарел».
  /// На каждую конфигурацию DXF в SLDPRT хранятся два per-config свойства:
  /// export-штамп (<see cref="VelumExportDocumentationProperties.DxfGeometryUpdateStamp"/>)
  /// и pending-штамп (<see cref="VelumExportDocumentationProperties.DxfGeometryPendingStamp"/>).
  /// DXF/PDF устарели, если pending больше export. Рост GetUpdateStamp от Save,
  /// rebuild ссылочной модели и записи свойств сам по себе метрику не зажигает.
  /// </summary>
  internal static class VelumExportDocumentationGeometryStampHelper
  {
    private static readonly string[] PropertyReadScopes = { "document", "active", "default" };

    /// <summary>
    /// Синхронизация записи export- и pending-штампов: предотвращает race condition
    /// между записью DxfGeometryPendingStamp (из Modify/Regen) и DxfGeometryUpdateStamp
    /// (из ExportMetaSync/RefreshMetricsOffPulse).
    /// </summary>
    private static readonly object _geometryStampWriteGate = new object();

    /// <summary>
    /// Глубина подавления/reentrancy для pending-штампов: запись свойств из Velum не должна
    /// снова вызывать TrySyncPending*GeometryStamp через ModifyNotify.
    /// </summary>
    private static int _geometryPendingStampSyncDepth;

    /// <summary>Ключ чертежа, для которого следим за геометрией после PDF-экспорта / открытия.</summary>
    private static string _trackedDrawingDocKey;

    /// <summary>Токен геометрии листа (не GetUpdateStamp) — ловит сдвиг линии без CommandOpen.</summary>
    private static long _trackedDrawingContentToken;

    /// <summary>Токен на момент ArmAfterPdfExport — для отката ложного pending при Save без правок.</summary>
    private static long _contentTokenAtPdfExportArm;

    /// <summary>
    /// После записи свойств PDF-экспорта документ dirty: не считать это «правкой» до Save
    /// при clean→dirty, пока токен геометрии не изменится.
    /// </summary>
    private static bool _absorbExportPropertyDirtyUntilSave;

    private static bool _lastActiveSketchPresent;

    /// <summary>
    /// Имя активного листа чертежа на момент последней фиксации baseline. Токен содержимого
    /// строится от видов активного листа (<c>GetFirstView</c>/<c>GetNextView</c>), поэтому
    /// активация другого листа (особенно с другой конфигурацией) меняет токен без правки
    /// геометрии. Сравнение имени листа даёт дискриминатор «смена листа vs правка листа».
    /// </summary>
    private static string _trackedDrawingSheetName;

    /// <summary>Глубина FileSave/FileSaveAs чертежа — не писать pending во время сохранения.</summary>
    private static int _drawingSaveDepth;

    /// <summary>Глубина FileSave детали — не писать pending во время сохранения SLDPRT.</summary>
    private static int _partSaveDepth;

    /// <summary>Ключ детали после успешного DXF-экспорта (absorb dirty свойств до Save).</summary>
    private static string _trackedPartDocKey;

    /// <summary>
    /// После записи свойств DXF-экспорта документ dirty: не считать Save/Regen «правкой»,
    /// пока не будет реальной смены геометрии (не fold/unfold развёртки).
    /// </summary>
    private static bool _absorbDxfExportPropertyDirtyUntilSave;

    /// <summary>
    /// Regen/Modify сразу после Save детали — это сдвиг GetUpdateStamp, не правка геометрии.
    /// </summary>
    private static bool _absorbPartSaveRebuild;

/// <summary>Сигнатура погашения FlatPattern по ключу документа — fold/unfold не пишет pending.</summary>
    private static readonly Dictionary<string, string> _flatPatternFoldSignatureByDoc =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Имя активной конфигурации по ключу документа. Смена активной (переключение
    /// конфигурации в UI) поднимает GetUpdateStamp и шлёт Regen/Modify, но геометрию
    /// не меняет — такой всплеск не должен писать pending в новую активную конфигурацию.
    /// </summary>
    private static readonly Dictionary<string, string> _activeConfigNameByDoc =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Выполняет запись метаданных экспорта без рекурсивного pending-sync по ModifyNotify.
    /// </summary>
    internal static void RunWithGeometryPendingStampSyncSuppressed(Action action)
    {
      if (action == null)
        return;

      _geometryPendingStampSyncDepth++;
      try
      {
        action();
      }
      finally
      {
        if (_geometryPendingStampSyncDepth > 0)
          _geometryPendingStampSyncDepth--;
      }
    }

    /// <summary>Совместимость: прежнее имя для PDF-контура.</summary>
    internal static void RunWithDrawingPendingStampSyncSuppressed(Action action) =>
        RunWithGeometryPendingStampSyncSuppressed(action);

    private static bool IsGeometryPendingStampSyncSuppressed =>
        _geometryPendingStampSyncDepth > 0 || _drawingSaveDepth > 0 || _partSaveDepth > 0;

    internal static void BeginDrawingSave()
    {
      _drawingSaveDepth++;
    }

    internal static void EndDrawingSave()
    {
      if (_drawingSaveDepth > 0)
        _drawingSaveDepth--;
    }

    /// <summary>Save детали: не писать pending, пока идёт FileSave.</summary>
    internal static void BeginPartSave()
    {
      if (_partSaveDepth < 1)
        _partSaveDepth = 1;
    }

    /// <summary>Конец Save детали.</summary>
    internal static void EndPartSave()
    {
      if (_partSaveDepth > 0)
        _partSaveDepth--;
    }

    /// <summary>
    /// Снять absorb Regen после Save, если дальше идёт реальная команда или смена документа.
    /// </summary>
    internal static void ClearPartSaveRebuildAbsorb()
    {
      _absorbPartSaveRebuild = false;
    }

    /// <summary>
    /// Вызывается из Modify/Regen: записывает pending-штамп в свойство активной конфигурации SLDPRT.
    /// Свернуть/развернуть развёртку листовой детали не считается правкой DXF.
    /// </summary>
    internal static void NotifyPartGeometryModified(ModelDoc2 modelDoc)
    {
      if (modelDoc == null || IsGeometryPendingStampSyncSuppressed)
        return;

      if (IsFlatPatternFoldOnlyChange(modelDoc))
      {
        CaptureFlatPatternFoldSignature(modelDoc);
        return;
      }

      if (IsAbsorbingDxfExportPropertyDirty(modelDoc))
      {
        // Dirty от записи пути/штампов после DXF: Regen/Modify до Save не пишут pending.
        CaptureFlatPatternFoldSignature(modelDoc);
        return;
      }

      if (_absorbPartSaveRebuild)
      {
        // Save сам поднимает GetUpdateStamp и шлёт Regen — это не смена геометрии DXF.
        _absorbPartSaveRebuild = false;
        CaptureFlatPatternFoldSignature(modelDoc);
        return;
      }

      // Переключение активной конфигурации поднимает GetUpdateStamp и шлёт Regen/Modify,
      // но геометрию не меняет — не пишем pending в новую активную конфигурацию.
      if (TryAbsorbActiveConfigurationChange(modelDoc))
      {
        CaptureFlatPatternFoldSignature(modelDoc);
        return;
      }

      CaptureFlatPatternFoldSignature(modelDoc);

      _geometryPendingStampSyncDepth++;
      try
      {
        // DXF-конфигурация: развёртку SheetMetal сводим к родителю, служебные вкладки
        // исключаем. Иначе pending уходил бы в SM-FLAT-PATTERN, которую TryIsDxfOutdated
        // не читает, а реальная конфигурация оставалась бы без pending.
        string configName = VelumDxfArtifactResolver.TryResolveDefaultExportConfigurationName(modelDoc);
        TrySyncPendingGeometryStamp(modelDoc, configName, out _);
      }
      finally
      {
        if (_geometryPendingStampSyncDepth > 0)
          _geometryPendingStampSyncDepth--;
      }
    }

    /// <summary>
    /// Regen/Modify/пульс: pending только если токен листа реально сдвинулся.
    /// Rebuild после Save детали и открытие чертежа токен часто не меняют — pending не пишем.
    /// </summary>
    internal static void NotifyDrawingModified(ModelDoc2 modelDoc)
    {
      // Не пишем pending, пока dirty после PDF-экспорта ещё не сохранён.
      // COM-события от SaveAs PDF могут вызвать ModifyNotify после выхода из
      // RunWithGeometryPendingStampSyncSuppressed — в этот момент ArmAfterPdfExport
      // уже установлен, но IsGeometryPendingStampSyncSuppressed его не видит.
      if (modelDoc == null || IsAbsorbingPdfExportPropertyDirty(modelDoc))
        return;

      TryDetectAndSyncDrawingPdfPendingOnPulse(modelDoc);
    }

    /// <summary>
    /// Явная правка листа (размер, вид, undo): pending даже если токен (счётчик аннотаций /
    /// точки эскиза) не изменился. Первый контакт с документом только фиксирует baseline —
    /// AddItem при открытии/rebuild после Save детали не должен зажигать PDF.
    /// </summary>
    internal static void NotifyDrawingSheetEdited(ModelDoc2 modelDoc)
    {
      if (modelDoc == null || IsGeometryPendingStampSyncSuppressed)
        return;

      // Не пишем pending, пока dirty после PDF-экспорта ещё не сохранён.
      if (IsAbsorbingPdfExportPropertyDirty(modelDoc))
        return;

      string key = TryGetDocumentKey(modelDoc);
      long token = ComputeDrawingContentToken(modelDoc);
      bool activeSketch = HasActiveSketch(modelDoc);

      if (!string.Equals(key, _trackedDrawingDocKey, StringComparison.Ordinal))
      {
        _trackedDrawingDocKey = key;
        _trackedDrawingContentToken = token;
        _trackedDrawingSheetName = TryGetActiveSheetName(modelDoc);
        _lastActiveSketchPresent = activeSketch;
        return;
      }

      bool sheetChanged = TryAbsorbActiveSheetChange(modelDoc);

      _trackedDrawingContentToken = token;
      _lastActiveSketchPresent = activeSketch;

      if (sheetChanged)
      {
        // Активация другого листа перестраивает его виды и порождает событийные колбэки
        // (AddItem/ViewNew/DimensionChange) без правки содержимого. Токен строится от видов
        // активного листа, поэтому он тоже сдвигается. Это не правка — pending не пишем,
        // baseline токена переснят выше (аналог E38 для чертежа).
        return;
      }

      _absorbExportPropertyDirtyUntilSave = false;

      _geometryPendingStampSyncDepth++;
      try
      {
        TryEnsurePdfPendingMarksOutdated(modelDoc, out _);
      }
      finally
      {
        if (_geometryPendingStampSyncDepth > 0)
          _geometryPendingStampSyncDepth--;
      }
    }

    /// <summary>
    /// После успешного DXF-экспорта: «глотаем» dirty от записи свойств до Save.
    /// </summary>
    internal static void ArmAfterDxfExport(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return;

      _trackedPartDocKey = TryGetDocumentKey(modelDoc);
      _absorbDxfExportPropertyDirtyUntilSave = true;
      CaptureFlatPatternFoldSignature(modelDoc);
    }

    /// <summary>true, пока dirty после DXF-экспорта ещё не сохранён.</summary>
    internal static bool IsAbsorbingDxfExportPropertyDirty(ModelDoc2 modelDoc)
    {
      if (!_absorbDxfExportPropertyDirtyUntilSave || modelDoc == null)
        return false;
      return string.Equals(TryGetDocumentKey(modelDoc), _trackedPartDocKey, StringComparison.Ordinal);
    }

    /// <summary>
    /// Запоминает погашение FlatPattern, чтобы последующий fold/unfold не писал pending.
    /// </summary>
    internal static void CaptureFlatPatternFoldSignature(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return;

      string key = TryGetDocumentKey(modelDoc);
      if (string.IsNullOrEmpty(key) || string.Equals(key, "no_doc", StringComparison.Ordinal))
        return;

      _flatPatternFoldSignatureByDoc[key] =
          VelumSolidSheetMetalHelper.ComputeFlatPatternFoldSignature(modelDoc);

      // Baseline активной конфигурации: синхронизируется при открытии/Save/экспорте,
      // чтобы последующее переключение конфигурации не выглядело как правка геометрии.
      _activeConfigNameByDoc[key] =
          VelumDxfArtifactResolver.TryGetActiveConfigurationName(modelDoc) ?? string.Empty;
    }

    /// <summary>
    /// true — активная конфигурация сменилась относительно baseline (переключение
    /// конфигурации в UI): это не правка геометрии DXF, pending писать нельзя.
    ///_baseline_ обновляется на текущую активную. Если baseline ещё не зафиксирован —
    /// только фиксируем и возвращаем false (первый контакт, как в <see cref="NotifyDrawingSheetEdited"/>).
    /// </summary>
    private static bool TryAbsorbActiveConfigurationChange(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return false;

      string key = TryGetDocumentKey(modelDoc);
      if (string.IsNullOrEmpty(key) || string.Equals(key, "no_doc", StringComparison.Ordinal))
        return false;

      string current =
          VelumDxfArtifactResolver.TryGetActiveConfigurationName(modelDoc) ?? string.Empty;

      string previous;
      if (!_activeConfigNameByDoc.TryGetValue(key, out previous))
      {
        _activeConfigNameByDoc[key] = current;
        return false;
      }

      if (string.Equals(previous ?? string.Empty, current, StringComparison.OrdinalIgnoreCase))
        return false;

      _activeConfigNameByDoc[key] = current;
      return true;
    }

    private static bool IsFlatPatternFoldOnlyChange(ModelDoc2 modelDoc)
    {
      if (modelDoc == null || !VelumSolidSheetMetalHelper.IsSheetMetalPart(modelDoc))
        return false;

      string key = TryGetDocumentKey(modelDoc);
      string current = VelumSolidSheetMetalHelper.ComputeFlatPatternFoldSignature(modelDoc);
      string previous;
      if (!_flatPatternFoldSignatureByDoc.TryGetValue(key, out previous))
        return false;

      return !string.Equals(current ?? string.Empty, previous ?? string.Empty, StringComparison.Ordinal);
    }

    /// <summary>
    /// После FileSave детали. Если после DXF-экспорта не было правки геометрии —
    /// дожимает export-штампы (Save раньше давал «DXF устарел»).
    /// </summary>
    /// <returns>true, если штампы перефинализированы (нужен MarkStale Export).</returns>
    internal static bool NotifyPartSaved(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return false;

      string key = TryGetDocumentKey(modelDoc);
      bool sameDoc = string.Equals(key, _trackedPartDocKey, StringComparison.Ordinal);
      bool wasAbsorbing = sameDoc && _absorbDxfExportPropertyDirtyUntilSave;
      _absorbDxfExportPropertyDirtyUntilSave = false;
      _absorbPartSaveRebuild = true;

      if (!sameDoc)
        _trackedPartDocKey = key;

      CaptureFlatPatternFoldSignature(modelDoc);

      if (!wasAbsorbing)
        return false;

      bool finalized = false;
      RunWithGeometryPendingStampSyncSuppressed(() =>
      {
        IReadOnlyList<string> configs = VelumDxfArtifactResolver.TryGetConfigurationNames(modelDoc);
        TryResyncPerConfigDxfGeometryStampsCore(modelDoc, configs);
        finalized = true;
        Logger.Info("Velum DXF stamps after save (no content change since export)");
      });

      return finalized;
    }

    /// <summary>
    /// После успешного PDF-экспорта: запоминаем геометрию и «глотаем» dirty от записи свойств до Save.
    /// </summary>
    internal static void ArmAfterPdfExport(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return;

      string key = TryGetDocumentKey(modelDoc);
      long token = ComputeDrawingContentToken(modelDoc);
      _trackedDrawingDocKey = key;
      _absorbExportPropertyDirtyUntilSave = true;
      _contentTokenAtPdfExportArm = token;
      _trackedDrawingContentToken = token;
      _trackedDrawingSheetName = TryGetActiveSheetName(modelDoc);
      _lastActiveSketchPresent = HasActiveSketch(modelDoc);
    }

    /// <summary>
    /// Keep-open после пакетного PDF: штампы уже выровнены и сохранены — фиксируем baseline
    /// геометрии без absorb (иначе сброс absorb при закрытии модалки снова даёт «устарел»).
    /// </summary>
    internal static void CommitPdfExportGeometryBaseline(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return;

      string key = TryGetDocumentKey(modelDoc);
      long token = ComputeDrawingContentToken(modelDoc);
      _trackedDrawingDocKey = key;
      _absorbExportPropertyDirtyUntilSave = false;
      _contentTokenAtPdfExportArm = token;
      _trackedDrawingContentToken = token;
      _trackedDrawingSheetName = TryGetActiveSheetName(modelDoc);
      _lastActiveSketchPresent = HasActiveSketch(modelDoc);
    }

    /// <summary>
    /// Сырое устаревание PDF без absorb-щита (для дожима keep-open после batch-экспорта).
    /// </summary>
    internal static bool TryIsPdfOutdatedIgnoringAbsorb(
        ModelDoc2 modelDoc,
        int currentStamp,
        int exportStamp)
    {
      if (exportStamp <= 0)
        return true;

      if (TryReadStoredUpdateStamp(
              modelDoc,
              VelumExportDocumentationProperties.PdfGeometryPendingStamp,
              out int pendingStamp) &&
          pendingStamp > exportStamp)
        return true;

      return currentStamp > exportStamp;
    }

/// <summary>
    /// После FileSave чертежа. Если геометрия не менялась с PDF-экспорта — снимает ложный pending
    /// (Save/сервисные команды раньше писали ensure outdated).
    /// <para>
    /// Дополнительно: если чертёж заведён в реестре, <see cref="VelumProductItem.PdfModelStampAtExport"/>
    /// обновляется до текущего <see cref="VelumProductItem.ModelGeometryStamp"/> связанной детали
    /// (чертёж "принял" изменения геометрии модели при пересохранении).
    /// </para>
    /// </summary>
    /// <returns>true, если штампы перефинализированы (нужен MarkStale Export).</returns>
    internal static bool NotifyDrawingSaved(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return false;

      string key = TryGetDocumentKey(modelDoc);
      long token = ComputeDrawingContentToken(modelDoc);
      bool sameDoc = string.Equals(key, _trackedDrawingDocKey, StringComparison.Ordinal);
      bool wasAbsorbing = sameDoc && _absorbExportPropertyDirtyUntilSave;
      bool unchangedSincePdfExport =
          sameDoc &&
          _contentTokenAtPdfExportArm != 0 &&
          token == _contentTokenAtPdfExportArm;

      _absorbExportPropertyDirtyUntilSave = false;

      if (!sameDoc)
      {
        _trackedDrawingDocKey = key;
        _contentTokenAtPdfExportArm = 0;
      }

      _trackedDrawingContentToken = token;
      _trackedDrawingSheetName = TryGetActiveSheetName(modelDoc);
      _lastActiveSketchPresent = HasActiveSketch(modelDoc);

      // После PDF-экспорта Save часто нужен только чтобы сбросить dirty от свойств.
      // Даже если токен чуть «поплыл» от UI Save — пока был absorb, дожимаем export-штамп.
      bool finalized = false;
      if (wasAbsorbing || unchangedSincePdfExport)
      {
        RunWithGeometryPendingStampSyncSuppressed(() =>
        {
          finalized = TryFinalizePdfExportStamps(modelDoc, out string msg);
          if (finalized)
            Logger.Info("Velum PDF stamps after save (no content change since export): " + msg);
          else
            Logger.Warning("Velum PDF stamps after save finalize failed: " + msg);
        });
      }

      // Чертёж пересохранён — обновляем PdfModelStampAtExport в реестре (из ModelGeometryStamp
      // связанной детали). Встраиваемость: ничего в свойства детали не пишем.
      Velum.UI.ProductRegistry.VelumProductRegistryExportMetaSync.TrySyncPdfModelStampAtExport(modelDoc);

      return finalized;
    }

    /// <summary>true, пока dirty после PDF-экспорта ещё не сохранён (не считать clean→dirty контентной правкой).</summary>
    internal static bool IsAbsorbingPdfExportPropertyDirty(ModelDoc2 modelDoc)
    {
      if (!_absorbExportPropertyDirtyUntilSave || modelDoc == null)
        return false;
      return string.Equals(TryGetDocumentKey(modelDoc), _trackedDrawingDocKey, StringComparison.Ordinal);
    }

    /// <summary>
    /// На пульсе: если геометрия листа/эскиза изменилась относительно baseline — пишем pending.
    /// Ловит сдвиг линии без CommandOpen и без роста GetUpdateStamp.
    /// Baseline обновляется только если pending реально стал &gt; export (иначе правка «теряется»).
    /// <para>
    /// При первом открытии чертежа (сброс трекинга) дополнительно:
    /// 1) Проверяем реестр — если чертёж там, ищем связанную деталь и DXF-штамбы.
    /// 2) Если чертежа нет в реестре — сравниваем GetUpdateStamp чертежа с PdfGeometryUpdateStamp.
    /// </para>
    /// </summary>
    /// <returns>true, если pending гарантированно &gt; export (нужен MarkStale Export).</returns>
    internal static bool TryDetectAndSyncDrawingPdfPendingOnPulse(ModelDoc2 modelDoc)
    {
      if (modelDoc == null || IsGeometryPendingStampSyncSuppressed)
        return false;

      if (!TryReadStoredUpdateStamp(
              modelDoc,
              VelumExportDocumentationProperties.PdfGeometryUpdateStamp,
              out int exportStamp) ||
          exportStamp <= 0)
        return false;

      string key = TryGetDocumentKey(modelDoc);
      long token = ComputeDrawingContentToken(modelDoc);
      bool activeSketch = HasActiveSketch(modelDoc);

      bool isNewDocument = !string.Equals(key, _trackedDrawingDocKey, StringComparison.Ordinal);

      if (isNewDocument)
      {
        // Первое открытие чертежа — проверяем реестр и GetUpdateStamp.
        bool partChanged = CheckPartGeometryViaRegistryOrStamp(modelDoc);
        if (partChanged)
        {
          bool ensured = TryEnsurePdfPendingMarksOutdated(modelDoc, out string ensureMessage);
          if (ensured)
          {
            _trackedDrawingDocKey = key;
            _trackedDrawingContentToken = token;
            _trackedDrawingSheetName = TryGetActiveSheetName(modelDoc);
            _contentTokenAtPdfExportArm = 0;
            _lastActiveSketchPresent = activeSketch;
            _absorbExportPropertyDirtyUntilSave = false;
            Logger.Info(
                "Velum PDF pending: part geometry changed (registry/stamp) detected on open" +
                " export=" + exportStamp.ToString(CultureInfo.InvariantCulture));
            return true;
          }
          else
          {
            Logger.Warning(
                "Velum PDF pending: part geometry changed but ensure outdated failed: " + ensureMessage);
          }
        }

        _trackedDrawingDocKey = key;
        _trackedDrawingContentToken = token;
        _trackedDrawingSheetName = TryGetActiveSheetName(modelDoc);
        _contentTokenAtPdfExportArm = 0;
        _lastActiveSketchPresent = activeSketch;
        _absorbExportPropertyDirtyUntilSave = false;
        return false;
      }

      // Смена активного листа перестраивает его виды: токен (виды активного листа) сдвигается
      // без правки содержимого. Это не правка — переснимаем baseline токена/листа и выходим
      // без pending (аналог E38 для чертежа). Первый контакт с листом (baseline не зафиксирован)
      // трактуем так же — не пишем pending.
      bool hadSheetBaseline = _trackedDrawingSheetName != null;
      bool sheetChanged = TryAbsorbActiveSheetChange(modelDoc);
      if (sheetChanged || !hadSheetBaseline)
      {
        _trackedDrawingContentToken = token;
        _lastActiveSketchPresent = activeSketch;
        return false;
      }

      bool sketchOpened = activeSketch && !_lastActiveSketchPresent;
      bool geometryChanged = token != _trackedDrawingContentToken;

      // На пульсе: pending пишем только при genuine-правке текущего листа (дискриминатор
      // «смена листа vs правка» — в чистых правилах).
      if (VelumPdfOutdatedRules.ShouldWriteDrawingPendingOnPulse(
              hadSheetBaseline, sheetChanged, geometryChanged, sketchOpened))
      {
        bool ensured = TryEnsurePdfPendingMarksOutdated(modelDoc, out string ensureMessage);
        if (ensured)
        {
          _trackedDrawingContentToken = token;
          if (_absorbExportPropertyDirtyUntilSave)
            _absorbExportPropertyDirtyUntilSave = false;

          Logger.Info(
              "Velum PDF pending: drawing geometry/sketch change detected on pulse" +
              (sketchOpened ? " (active sketch)" : string.Empty) +
              " export=" + exportStamp.ToString(CultureInfo.InvariantCulture));
          return true;
        }
        else
        {
          Logger.Warning(
              "Velum PDF pending: geometry changed but ensure outdated failed: " + ensureMessage);
        }
      }

      _lastActiveSketchPresent = activeSketch;
      return false;
    }

/// <summary>
    /// При первом открытии чертежа проверяет, изменилась ли геометрия модели:
    /// 1) Если чертёж в реестре — сравниваем <c>ModelGeometryStamp</c> связанной детали
    ///    с <c>PdfModelStampAtExport</c> чертежа (единая база штампов в реестре).
    /// 2) Если чертежа нет в реестре — сравниваем GetUpdateStamp чертежа с PdfGeometryUpdateStamp.
    /// </summary>
    private static bool CheckPartGeometryViaRegistryOrStamp(ModelDoc2 modelDoc)
    {
      string drawingPath = TryGetDocumentPath(modelDoc);
      if (string.IsNullOrEmpty(drawingPath))
        return false;

      // Пытаемся найти запись чертежа в реестре.
      try
      {
        var store = new VelumProductRegistryStore();
        store.Load();

        string normalizedDrawingPath = VelumProductRegistryStore.NormalizeFilePathKey(drawingPath);
        if (string.IsNullOrEmpty(normalizedDrawingPath))
          return false;

VelumProductItem drawingItem = store.FindItemByFilePath(normalizedDrawingPath);
        if (drawingItem != null)
        {
          // Чертёж в реестре: PDF устарел, если геометрия связанной модели сменилась
          // с момента последнего экспорта (ModelGeometryStamp > PdfModelStampAtExport).
          if (IsPdfOutdatedByModelGeometry(store, drawingItem))
          {
            Logger.Info(
                "Velum PDF pending: part geometry changed (registry) detected on open: " +
                drawingPath);
            return true;
          }

          // Запись есть, но без PdfModelStampAtExport (не зафиксирован экспорт) —
          // переходим к fallback по GetUpdateStamp чертежа ниже.
        }
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum PDF pending: registry check failed: " + ex.Message);
      }

      // Чертежа нет в реестре — fallback на GetUpdateStamp чертежа.
      if (!TryReadStoredUpdateStamp(
              modelDoc,
              VelumExportDocumentationProperties.PdfGeometryUpdateStamp,
              out int exportStamp) ||
          exportStamp <= 0)
        return false;

      if (!TryGetCurrentUpdateStamp(modelDoc, out int currentStamp) || currentStamp <= 0)
        return false;

      if (currentStamp > exportStamp)
      {
        Logger.Info(
            "Velum PDF pending: drawing GetUpdateStamp increased on open: " +
            "current=" + currentStamp + " export=" + exportStamp);
        return true;
      }

      return false;
    }

    /// <summary>
    /// PDF устарел по геометрии модели. Резолвинг связанной детали — здесь; чистый вердикт
    /// (genuine-правка vs regen-всплеск) отдаёт <see cref="VelumPdfOutdatedRules"/> (E9:
    /// единый предикат вместо расходящихся копий).
    /// </summary>
    private static bool IsPdfOutdatedByModelGeometry(
        VelumProductRegistryStore store,
        VelumProductItem drawingItem)
    {
      if (store == null || drawingItem == null)
        return false;

      // Пытаемся найти связанную деталь.
      // Вариант 1: DrawingPath чертежа (обратная связь, может быть пустой).
      string partPath = (drawingItem.DrawingPath ?? string.Empty).Trim();

      // Вариант 2: ищем по базовому имени файла чертежа среди всех деталей.
      if (string.IsNullOrEmpty(partPath))
      {
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
        return false;

      string normalizedPartPath = VelumProductRegistryStore.NormalizeFilePathKey(partPath);
      if (string.IsNullOrEmpty(normalizedPartPath))
        return false;

      VelumProductItem partItem = store.FindItemByFilePath(normalizedPartPath);
      if (partItem == null)
        return false;

      string partExt = System.IO.Path.GetExtension(partItem.FilePath ?? string.Empty).ToLowerInvariant();
      bool partIsSldprt = string.Equals(partExt, ".sldprt", StringComparison.OrdinalIgnoreCase);

      bool outdated = VelumPdfOutdatedRules.IsPdfOutdatedByModelGeometry(
          partItem.ModelGeometryStamp,
          ToConfigStamps(partItem.ExportMetaConfigs),
          partIsSldprt,
          drawingItem.PdfModelStampAtExport,
          drawingItem.PdfGeometryUpdateStamp);

      if (outdated)
        Logger.Info("Velum PDF pending: outdated by model geometry for " + partPath);

      return outdated;
    }

    /// <summary>
    /// Маппит per-config DXF-зеркало реестра в примитивные пары штампов для чистых
    /// <see cref="VelumPdfOutdatedRules"/> (правила не должны зависеть от модели реестра).
    /// </summary>
    internal static VelumPdfOutdatedRules.DxfConfigStamps[] ToConfigStamps(
        VelumProductExportMetaConfig[] configs)
    {
      if (configs == null || configs.Length == 0)
        return Array.Empty<VelumPdfOutdatedRules.DxfConfigStamps>();

      var result = new VelumPdfOutdatedRules.DxfConfigStamps[configs.Length];
      for (int i = 0; i < configs.Length; i++)
      {
        var cfg = configs[i];
        result[i] = cfg == null
            ? default(VelumPdfOutdatedRules.DxfConfigStamps)
            : new VelumPdfOutdatedRules.DxfConfigStamps(
                cfg.DxfGeometryUpdateStamp, cfg.DxfGeometryPendingStamp);
      }

      return result;
    }

    /// <summary>Сброс in-memory трекинга чертежа (смена документа / новый цикл пульса).</summary>
    internal static void ResetGeometryEditTracking(string documentKey = null)
    {
      if (!string.IsNullOrEmpty(documentKey) &&
          !string.Equals(documentKey, _trackedDrawingDocKey, StringComparison.Ordinal))
        return;

_trackedDrawingDocKey = null;
      _trackedDrawingContentToken = 0;
      _trackedDrawingSheetName = null;
      _contentTokenAtPdfExportArm = 0;
      _absorbExportPropertyDirtyUntilSave = false;
      _lastActiveSketchPresent = false;
      _drawingSaveDepth = 0;
      _absorbPartSaveRebuild = false;
    }

    private static string TryGetDocumentKey(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return "no_doc";

      try
      {
        string path = modelDoc.GetPathName();
        if (!string.IsNullOrWhiteSpace(path))
          return "path:" + System.IO.Path.GetFullPath(path.Trim());
      }
      catch
      {
      }

      try
      {
        return "unsaved:" + modelDoc.GetHashCode().ToString(CultureInfo.InvariantCulture);
      }
      catch
      {
        return "no_doc";
      }
    }

    private static string TryGetDocumentPath(ModelDoc2 modelDoc)
    {
      try
      {
        return modelDoc?.GetPathName() ?? string.Empty;
      }
      catch
      {
        return string.Empty;
      }
    }

    private static bool HasActiveSketch(ModelDoc2 modelDoc)
    {
      try
      {
        return modelDoc?.SketchManager?.ActiveSketch != null;
      }
      catch
      {
        return false;
      }
    }

    /// <summary>
    /// Имя активного листа чертежа (<c>DrawingDoc.GetCurrentSheet().GetName()</c>), либо пустая
    /// строка, если чертёж/лист недоступен. Используется как дискриминатор «смена активного
    /// листа vs правка содержимого текущего листа».
    /// </summary>
    private static string TryGetActiveSheetName(ModelDoc2 modelDoc)
    {
      try
      {
        DrawingDoc drawing = modelDoc as DrawingDoc;
        if (drawing == null)
          return string.Empty;

        ISheet sheet = drawing.GetCurrentSheet() as ISheet;
        if (sheet == null)
          return string.Empty;

        return (sheet.GetName() ?? string.Empty).Trim();
      }
      catch
      {
        return string.Empty;
      }
    }

    /// <summary>
    /// true — активный лист сменился относительно baseline: это НЕ правка содержимого,
    /// pending писать нельзя (аналог <see cref="TryAbsorbActiveConfigurationChange"/> для
    /// конфигурации детали, E38). Baseline обновляется на текущий лист. Если baseline ещё
    /// не зафиксирован — только фиксируем и возвращаем false (первый контакт).
    /// </summary>
    private static bool TryAbsorbActiveSheetChange(ModelDoc2 modelDoc)
    {
      string current = TryGetActiveSheetName(modelDoc);

      if (_trackedDrawingSheetName == null)
      {
        _trackedDrawingSheetName = current;
        return false;
      }

      if (string.Equals(_trackedDrawingSheetName, current, StringComparison.Ordinal))
        return false;

      _trackedDrawingSheetName = current;
      return true;
    }

    /// <summary>
    /// Лёгкий токен содержимого чертежа (координаты эскизов видов + позиции аннотаций +
    /// свойства основной надписи). Без GetUpdateStamp — запись pending-свойства его не
    /// должна «сдвигать» baseline.
    /// </summary>
    private static long ComputeDrawingContentToken(ModelDoc2 modelDoc)
    {
      unchecked
      {
        long hash = 17L;
        DrawingDoc drawing = modelDoc as DrawingDoc;
        if (drawing == null)
          return hash;

        try
        {
          // 1. Свойства основной надписи (Наименование, Материал, Масса, РеVISION и т.п.).
          hash = hash * 31L + HashTitleBlockProperties(modelDoc);

          View view = drawing.GetFirstView() as View;
          int viewGuard = 0;
          while (view != null && viewGuard++ < 64)
          {
            hash = hash * 31L + HashViewAnnotations(view);
            hash = hash * 31L + HashSketch(view.GetSketch() as Sketch);
            view = view.GetNextView() as View;
          }
        }
        catch
        {
        }

        return hash;
      }
    }

    /// <summary>
    /// Токен свойств основной надписи: ключевые системские свойства документа.
    /// </summary>
    private static long HashTitleBlockProperties(ModelDoc2 modelDoc)
    {
      unchecked
      {
        long hash = 37L;
        if (modelDoc == null)
          return hash;

        try
        {
          CustomPropertyManager cpm = modelDoc.Extension?.CustomPropertyManager[""];
          if (cpm == null)
            return hash;

          // Ключевые свойства, которые меняются при правке основной надписи.
          string[] titleBlockProps = { "Наименование", "Material", "Материал",
            "Mass", "Масса", "Revision", "Дополнительные данные",
            "PartNum", "Номер детали", "Примечание", "Масштаб" };

          for (int i = 0; i < titleBlockProps.Length; i++)
          {
            string propName = titleBlockProps[i];
            try
            {
              string val;
              string resolvedVal;
              cpm.Get2(propName, out val, out resolvedVal);
              string normalized = (val ?? string.Empty).Trim().ToLowerInvariant();
              hash = hash * 31L + HashNormalizedString(normalized);
            }
            catch
            {
              // Свойство не найдено или ошибка — пропускаем.
            }
          }
        }
        catch
        {
        }

        return hash;
      }
    }

    /// <summary>
    /// Нормализованный хеш строки: trim + tolower + посимвольный.
    /// </summary>
    private static long HashNormalizedString(string s)
    {
      unchecked
      {
        if (string.IsNullOrEmpty(s))
          return 1L;

        long hash = 53L;
        for (int i = 0; i < s.Length; i++)
        {
          hash = hash * 31L + s[i];
        }
        return hash;
      }
    }

    private static long HashViewAnnotations(View view)
    {
      unchecked
      {
        long hash = 13L;
        if (view == null)
          return hash;

        try
        {
          // 1. Типы аннотаций — ловим добавление/удаление размеров, выносок и т.п.
          hash = hash * 31L + view.GetAnnotationCount();

          // 2. Позиции аннотаций с агрессивным квантованием.
          //    GetPosition() после экспорта/перерисовки UI даёт ложные сдвиги,
          //    поэтому квантуем до ~1 мм (в метрах модели чертежа).
          try
          {
            object raw = view.GetAnnotations();
            object[] anns = raw as object[];
            if (anns != null)
            {
              int count = anns.Length;
              hash = hash * 31L + count;

              // Берём до 128 аннотаций для производительности.
              int limit = Math.Min(count, 128);
              for (int i = 0; i < limit; i++)
              {
                try
                {
                  Annotation ann = anns[i] as Annotation;
                  if (ann == null)
                    continue;

                // Тип аннотации (int) — различает размер, выноску, таблицу и т.п.
                // В SOLIDWORKS Interop это свойство Type (swAnnotationType_e).
                // dynamic не резолвит IDispatch-члены у __ComObject -> используем InvokeMember.
                int annType = 0;
                try
                {
                    object v = ((object)ann).GetType().InvokeMember(
                        "GetType",
                        System.Reflection.BindingFlags.GetProperty,
                        null, ann, null);
                    annType = Convert.ToInt32(v);
    }
                catch
                {
                    // Запасной путь: если в вашей версии это метод GetType2(out int).
                    try
                    {
                        object[] args = new object[1];
                        ((object)ann).GetType().InvokeMember(
                            "GetType2",
                            System.Reflection.BindingFlags.InvokeMethod,
                            null, ann, args);
                        annType = Convert.ToInt32(args[0]);

                    }
                    catch
                    {
                        annType = 0; // не поддерживаемый тип — без first-chance потока
                    }
                }
                hash = hash * 31L + annType;

                  // Позиция с квантованием ~1mm.
                  object posObj = ann.GetPosition();
                  double[] pos = posObj as double[];
                  if (pos != null && pos.Length >= 3)
                  {
                    hash = hash * 31L + QuantizePos(pos[0]);
                    hash = hash * 31L + QuantizePos(pos[1]);
                    hash = hash * 31L + QuantizePos(pos[2]);
                  }
                }
                catch
                {
                  // Пропускаем проблемные аннотации.
                }
              }
            }
          }
          catch
          {
            // Fallback: только count.
          }
        }
        catch
        {
        }

        return hash;
      }
    }

    private static long HashSketch(Sketch sketch)
    {
      unchecked
      {
        long hash = 19L;
        if (sketch == null)
          return hash;

        try
        {
          object ptsObj = sketch.GetSketchPoints2();
          object[] pts = ptsObj as object[];
          if (pts == null)
          {
            ptsObj = sketch.GetSketchPoints();
            pts = ptsObj as object[];
          }

          if (pts == null)
            return hash;

          hash = hash * 31L + pts.Length;
          int limit = Math.Min(pts.Length, 256);
          for (int i = 0; i < limit; i++)
          {
            SketchPoint pt = pts[i] as SketchPoint;
            if (pt == null)
              continue;
            try
            {
              hash = hash * 31L + DoubleQuantize(pt.X);
              hash = hash * 31L + DoubleQuantize(pt.Y);
              hash = hash * 31L + DoubleQuantize(pt.Z);
            }
            catch
            {
            }
          }
        }
        catch
        {
        }

        return hash;
      }
    }

    private static long DoubleQuantize(double value)
    {
      // ~0.01 mm в метрах модели чертежа — достаточно, чтобы ловить сдвиг линии.
      return (long)Math.Round(value * 100000.0);
    }

    /// <summary>
    /// Квантование позиции аннотации: ~1 мм в метрах модели.
    /// Подавляет шум от UI-перерисовок, но ловит осмысленное перемещение.
    /// </summary>
    private static long QuantizePos(double value)
    {
      return (long)Math.Round(value * 1000.0);
    }

    /// <summary>
    /// Проверяет, устарела ли DXF-геометрия конфигурации (pending больше export в SLDPRT).
    /// GetUpdateStamp не используем: Save / fold / запись свойств его поднимают без смены DXF.
    /// </summary>
    internal static bool TryIsPerConfigDxfOutdated(
        ModelDoc2 modelDoc,
        string configName,
        int currentStamp,
        int exportStamp)
    {
      if (exportStamp <= 0)
        return true;

      _ = currentStamp;

      if (IsAbsorbingDxfExportPropertyDirty(modelDoc))
        return false;

      if (TryReadStoredUpdateStamp(
              modelDoc,
              VelumExportDocumentationProperties.DxfGeometryPendingStamp,
              configName,
              out int pendingStamp) &&
          pendingStamp > exportStamp)
        return true;

      return false;
    }

    /// <summary>
    /// Проверяет, устарел ли PDF чертежа: pending больше export.
    /// GetUpdateStamp не используем — Rebuild после Save детали, открытие чертежа и запись
    /// свойств поднимают его без правки листа (как в реестре: только pending &gt; export).
    /// </summary>
    internal static bool TryIsPdfOutdated(
        ModelDoc2 modelDoc,
        int currentStamp,
        int exportStamp)
    {
      if (exportStamp <= 0)
        return true;

      _ = currentStamp;

      if (TryReadStoredUpdateStamp(
              modelDoc,
              VelumExportDocumentationProperties.PdfGeometryPendingStamp,
              out int pendingStamp) &&
          pendingStamp > exportStamp)
        return true;

      return false;
    }

    internal static bool TryGetCurrentUpdateStamp(ModelDoc2 modelDoc, out int updateStamp)
    {
      updateStamp = 0;
      if (modelDoc == null)
        return false;

      try
      {
        updateStamp = modelDoc.GetUpdateStamp();
        return true;
      }
      catch
      {
        return false;
      }
    }

    internal static bool TryReadStoredUpdateStamp(
        ModelDoc2 modelDoc,
        string stampPropertyName,
        out int updateStamp)
    {
      return TryReadStoredUpdateStamp(modelDoc, stampPropertyName, null, out updateStamp);
    }

    internal static bool TryReadStoredUpdateStamp(
        ModelDoc2 modelDoc,
        string stampPropertyName,
        string configName,
        out int updateStamp)
    {
      updateStamp = 0;
      if (modelDoc == null || string.IsNullOrWhiteSpace(stampPropertyName))
        return false;

      string raw = TryReadProperty(modelDoc, stampPropertyName, configName);
      if (string.IsNullOrWhiteSpace(raw))
        return false;

      return int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out updateStamp) &&
             updateStamp > 0;
    }

    internal static bool TryWriteExportGeometryStamp(
        ModelDoc2 modelDoc,
        string stampPropertyName,
        int updateStamp,
        out string message)
    {
      return TryWriteExportGeometryStamp(modelDoc, stampPropertyName, null, updateStamp, out message);
    }

    internal static bool TryWriteExportGeometryStamp(
        ModelDoc2 modelDoc,
        string stampPropertyName,
        string configName,
        int updateStamp,
        out string message)
    {
      message = string.Empty;
      if (modelDoc == null || string.IsNullOrWhiteSpace(stampPropertyName))
      {
        message = "model_or_property_missing";
        return false;
      }

      if (updateStamp <= 0)
      {
        message = "invalid_update_stamp";
        return false;
      }

      CustomPropertyManager cpm = string.IsNullOrWhiteSpace(configName)
          ? VelumRecipeSolidWorksCustomProperties.TryGetManager(modelDoc, "document")
          : VelumDxfArtifactResolver.TryGetConfigManager(modelDoc, configName);
      if (cpm == null)
      {
        message = "property_manager_unavailable";
        return false;
      }

      string value = updateStamp.ToString(CultureInfo.InvariantCulture);
      return VelumRecipeSolidWorksCustomProperties.TrySetValue(
          cpm,
          stampPropertyName,
          value,
          "always",
          VelumSolidCustomPropertyTypes.TypeKeyNumber,
          out bool skipped,
          out message) && !skipped;
    }

    internal static bool TryClearGeometryStamp(
        ModelDoc2 modelDoc,
        string stampPropertyName,
        string configName,
        out string message)
    {
      message = string.Empty;
      if (modelDoc == null || string.IsNullOrWhiteSpace(stampPropertyName))
      {
        message = "model_or_property_missing";
        return false;
      }

      CustomPropertyManager cpm = string.IsNullOrWhiteSpace(configName)
          ? VelumRecipeSolidWorksCustomProperties.TryGetManager(modelDoc, "document")
          : VelumDxfArtifactResolver.TryGetConfigManager(modelDoc, configName);
      if (cpm == null)
      {
        message = "property_manager_unavailable";
        return false;
      }

      string name = stampPropertyName.Trim();
      if (!VelumRecipeSolidWorksCustomProperties.TryResolvePropertyName(cpm, name, out string canonical))
      {
        message = "property_absent";
        return true;
      }

      try
      {
        // Пустая строка в типе Number часто не очищает значение — Delete2 обязателен.
        int deleted = cpm.Delete2(canonical);
        if (deleted == 0)
        {
          message = "delete_ok";
          return true;
        }

        deleted = cpm.Delete(canonical);
        if (deleted == 0)
        {
          message = "delete_ok_legacy";
          return true;
        }

        message = "delete_failed:" + deleted;
        return false;
      }
      catch (Exception ex)
      {
        message = ex.Message;
        return false;
      }
    }

    /// <summary>
    /// Гарантирует pending &gt; export для PDF (без опоры на GetUpdateStamp).
    /// </summary>
    internal static bool TryEnsurePdfPendingMarksOutdated(ModelDoc2 modelDoc, out string message)
    {
      message = string.Empty;
      if (modelDoc == null)
      {
        message = "model_missing";
        return false;
      }

      if (!TryReadStoredUpdateStamp(
              modelDoc,
              VelumExportDocumentationProperties.PdfGeometryUpdateStamp,
              out int exportStamp) ||
          exportStamp <= 0)
      {
        message = "export_stamp_missing";
        return false;
      }

      if (TryReadStoredUpdateStamp(
              modelDoc,
              VelumExportDocumentationProperties.PdfGeometryPendingStamp,
              out int pendingStamp) &&
          pendingStamp >= exportStamp)
        return true;

      int pending = exportStamp + 1;
      if (!TryWriteExportGeometryStamp(
              modelDoc,
              VelumExportDocumentationProperties.PdfGeometryPendingStamp,
              pending,
              out message))
        return false;

      Logger.Info(
          "Velum PDF pending stamp: ensure outdated pending=" +
          pending.ToString(CultureInfo.InvariantCulture) +
          " export=" + exportStamp.ToString(CultureInfo.InvariantCulture));
      return true;
    }

    /// <summary>
    /// После экспорта PDF: export-штамп ≥ GetUpdateStamp, pending удалён; проверка !IsOutdated.
    /// </summary>
    internal static bool TryFinalizePdfExportStamps(ModelDoc2 modelDoc, out string message)
    {
      message = string.Empty;
      if (modelDoc == null)
      {
        message = "model_missing";
        return false;
      }

      if (!TrySyncExportRevisionStampAfterPathWrite(
              modelDoc,
              VelumExportDocumentationProperties.PdfGeometryUpdateStamp,
              null,
              VelumExportDocumentationProperties.PdfGeometryPendingStamp,
              out message))
        return false;

      for (int attempt = 0; attempt < 3; attempt++)
      {
        if (!TryGetCurrentUpdateStamp(modelDoc, out int currentStamp) || currentStamp <= 0)
          currentStamp = 1;

        TryReadStoredUpdateStamp(
            modelDoc,
            VelumExportDocumentationProperties.PdfGeometryUpdateStamp,
            out int exportStamp);
        TryReadStoredUpdateStamp(
            modelDoc,
            VelumExportDocumentationProperties.PdfGeometryPendingStamp,
            out int pendingStamp);

        if (!TryIsPdfOutdated(modelDoc, currentStamp, exportStamp))
        {
          message = "ok export=" + exportStamp.ToString(CultureInfo.InvariantCulture);
          return true;
        }

        int target = Math.Max(currentStamp, Math.Max(exportStamp, pendingStamp)) + 1;
        if (!TryWriteExportGeometryStamp(
                modelDoc,
                VelumExportDocumentationProperties.PdfGeometryUpdateStamp,
                target,
                out message))
          return false;

        TryClearGeometryStamp(
            modelDoc,
            VelumExportDocumentationProperties.PdfGeometryPendingStamp,
            null,
            out _);
      }

      TryGetCurrentUpdateStamp(modelDoc, out int verifyStamp);
      TryReadStoredUpdateStamp(
          modelDoc,
          VelumExportDocumentationProperties.PdfGeometryUpdateStamp,
          out int verifyExport);
      bool stillOutdated = TryIsPdfOutdated(modelDoc, verifyStamp, verifyExport);
      message = stillOutdated
          ? "still_outdated_after_finalize export=" + verifyExport.ToString(CultureInfo.InvariantCulture)
          : "ok";
      return !stillOutdated;
    }

    /// <summary>
    /// Записывает pending-штамп активной конфигурации; повторяет запись, если запись свойства сдвинула GetUpdateStamp.
    /// </summary>
    internal static bool TrySyncPendingGeometryStamp(
        ModelDoc2 modelDoc,
        string configName,
        out string message)
    {
      message = string.Empty;
      if (modelDoc == null)
      {
        message = "model_missing";
        return false;
      }

      string config = (configName ?? string.Empty).Trim();
      if (config.Length == 0)
        config = VelumDxfArtifactResolver.TryGetActiveConfigurationName(modelDoc);
      if (string.IsNullOrWhiteSpace(config))
      {
        message = "config_missing";
        return false;
      }

      lock (_geometryStampWriteGate)
      {
        if (!TryGetCurrentUpdateStamp(modelDoc, out int stamp) || stamp <= 0)
        {
          message = "invalid_update_stamp";
          return false;
        }

        if (TryReadStoredUpdateStamp(
                modelDoc,
                VelumExportDocumentationProperties.DxfGeometryPendingStamp,
                config,
                out int existingPending) &&
            existingPending >= stamp)
          return true;

        if (!TryWriteExportGeometryStamp(
                modelDoc,
                VelumExportDocumentationProperties.DxfGeometryPendingStamp,
                config,
                stamp,
                out message))
          return false;

        if (!TryGetCurrentUpdateStamp(modelDoc, out int afterStamp) || afterStamp <= 0)
          return true;

        if (afterStamp == stamp)
          return true;

        return TryWriteExportGeometryStamp(
            modelDoc,
            VelumExportDocumentationProperties.DxfGeometryPendingStamp,
            config,
            afterStamp,
            out message);
      }
    }

    /// <summary>
    /// Записывает pending-штамп чертежа; повторяет запись, если запись свойства сдвинула GetUpdateStamp.
    /// У чертежей правка (размер, эскиз, вид) часто оставляет update-stamp модели
    /// равным штампу экспорта — тогда принудительно пишем pending &gt; export, иначе IsOutdated молчит.
    /// </summary>
    internal static bool TrySyncPendingPdfGeometryStamp(ModelDoc2 modelDoc, out string message)
    {
      message = string.Empty;
      if (modelDoc == null)
      {
        message = "model_missing";
        return false;
      }

      if (!TryGetCurrentUpdateStamp(modelDoc, out int stamp) || stamp <= 0)
      {
        message = "invalid_update_stamp";
        return false;
      }

      // Drawing: GetUpdateStamp часто не растёт при локальных правках листа.
      bool forcedBeyondExport = false;
      if (TryReadStoredUpdateStamp(
              modelDoc,
              VelumExportDocumentationProperties.PdfGeometryUpdateStamp,
              out int exportStamp) &&
          exportStamp > 0 &&
          stamp <= exportStamp)
      {
        stamp = exportStamp + 1;
        forcedBeyondExport = true;
      }

      if (TryReadStoredUpdateStamp(
              modelDoc,
              VelumExportDocumentationProperties.PdfGeometryPendingStamp,
              out int existingPending) &&
          existingPending >= stamp)
        return true;

      if (!TryWriteExportGeometryStamp(
              modelDoc,
              VelumExportDocumentationProperties.PdfGeometryPendingStamp,
              stamp,
              out message))
        return false;

      if (forcedBeyondExport)
      {
        Logger.Info(
            "Velum PDF pending stamp: drawing edit without update-stamp bump, " +
            "pending=" + stamp.ToString(CultureInfo.InvariantCulture) +
            " export=" + exportStamp.ToString(CultureInfo.InvariantCulture));
      }

      if (!TryGetCurrentUpdateStamp(modelDoc, out int afterStamp) || afterStamp <= 0)
        return true;

      if (afterStamp <= stamp)
        return true;

      return TryWriteExportGeometryStamp(
          modelDoc,
          VelumExportDocumentationProperties.PdfGeometryPendingStamp,
          afterStamp,
          out message);
    }

    /// <summary>
    /// После записи пути экспорта: фиксирует export-штамп (GetUpdateStamp) и сбрасывает pending-штамп конфигурации.
    /// </summary>
    internal static bool TrySyncExportRevisionStampAfterPathWrite(
        ModelDoc2 modelDoc,
        string stampPropertyName,
        out string message)
    {
      return TrySyncExportRevisionStampAfterPathWrite(
          modelDoc,
          stampPropertyName,
          null,
          VelumExportDocumentationProperties.DxfGeometryPendingStamp,
          out message);
    }

    internal static bool TrySyncExportRevisionStampAfterPathWrite(
        ModelDoc2 modelDoc,
        string stampPropertyName,
        string configName,
        out string message)
    {
      return TrySyncExportRevisionStampAfterPathWrite(
          modelDoc,
          stampPropertyName,
          configName,
          VelumExportDocumentationProperties.DxfGeometryPendingStamp,
          out message);
    }

    internal static bool TrySyncExportRevisionStampAfterPathWrite(
        ModelDoc2 modelDoc,
        string stampPropertyName,
        string configName,
        string pendingStampPropertyName,
        out string message)
    {
      message = string.Empty;
      if (modelDoc == null || string.IsNullOrWhiteSpace(stampPropertyName))
      {
        message = "model_or_property_missing";
        return false;
      }

      lock (_geometryStampWriteGate)
      {
        if (!TryGetCurrentUpdateStamp(modelDoc, out int stamp) || stamp <= 0)
        {
          message = "invalid_update_stamp";
          return false;
        }

        if (!TryWriteExportGeometryStamp(modelDoc, stampPropertyName, configName, stamp, out message))
          return false;

        if (!TryGetCurrentUpdateStamp(modelDoc, out int afterStamp) || afterStamp <= 0)
        {
          if (!string.IsNullOrWhiteSpace(pendingStampPropertyName))
          {
            TryClearGeometryStamp(
                modelDoc,
                pendingStampPropertyName,
                configName,
                out _);
          }

          return true;
        }

        if (afterStamp == stamp)
        {
          if (!string.IsNullOrWhiteSpace(pendingStampPropertyName))
          {
            TryClearGeometryStamp(
                modelDoc,
                pendingStampPropertyName,
                configName,
                out _);
          }

          return true;
        }

        if (!TryWriteExportGeometryStamp(modelDoc, stampPropertyName, configName, afterStamp, out message))
          return false;

        if (!string.IsNullOrWhiteSpace(pendingStampPropertyName))
        {
          TryClearGeometryStamp(
              modelDoc,
              pendingStampPropertyName,
              configName,
              out _);
        }

        return true;
      }
    }

    /// <summary>
    /// После пакетного экспорта выравнивает per-config export-штампы и сбрасывает pending по каждой конфигурации.
    /// </summary>
    internal static void TryResyncPerConfigDxfGeometryStamps(
        ModelDoc2 modelDoc,
        IReadOnlyList<string> configNames)
    {
      if (modelDoc == null || configNames == null || configNames.Count == 0)
        return;

      RunWithGeometryPendingStampSyncSuppressed(() =>
          TryResyncPerConfigDxfGeometryStampsCore(modelDoc, configNames));
    }

    private static void TryResyncPerConfigDxfGeometryStampsCore(
        ModelDoc2 modelDoc,
        IReadOnlyList<string> configNames)
    {
      lock (_geometryStampWriteGate)
      {
        int maxIterations = Math.Max(2, configNames.Count + 1);
        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
          for (int i = 0; i < configNames.Count; i++)
          {
            string configName = (configNames[i] ?? string.Empty).Trim();
            if (configName.Length == 0)
              continue;

            TrySyncExportRevisionStampAfterPathWrite(
                modelDoc,
                VelumExportDocumentationProperties.DxfGeometryUpdateStamp,
                configName,
                VelumExportDocumentationProperties.DxfGeometryPendingStamp,
                out _);
          }

          if (!TryGetCurrentUpdateStamp(modelDoc, out int currentStamp) || currentStamp <= 0)
            return;

          bool allAligned = true;
          for (int i = 0; i < configNames.Count; i++)
          {
            string configName = (configNames[i] ?? string.Empty).Trim();
            if (configName.Length == 0)
              continue;

            if (!TryReadStoredUpdateStamp(
                    modelDoc,
                    VelumExportDocumentationProperties.DxfGeometryUpdateStamp,
                    configName,
                    out int storedStamp) ||
                storedStamp != currentStamp)
            {
              allAligned = false;
              break;
            }
          }

          if (allAligned)
            return;
        }
      }
    }

    private static string TryReadProperty(ModelDoc2 modelDoc, string propertyName)
    {
      return TryReadProperty(modelDoc, propertyName, null);
    }

    private static string TryReadProperty(ModelDoc2 modelDoc, string propertyName, string configName)
    {
      if (!string.IsNullOrWhiteSpace(configName))
      {
        CustomPropertyManager configCpm = VelumDxfArtifactResolver.TryGetConfigManager(modelDoc, configName);
        if (configCpm != null &&
            VelumRecipeSolidWorksCustomProperties.TryGetValue(configCpm, propertyName, out string configRaw))
          return (configRaw ?? string.Empty).Trim();
      }

      foreach (string scope in PropertyReadScopes)
      {
        CustomPropertyManager cpm = VelumRecipeSolidWorksCustomProperties.TryGetManager(modelDoc, scope);
        if (cpm == null)
          continue;

        if (VelumRecipeSolidWorksCustomProperties.TryGetValue(cpm, propertyName, out string raw))
          return (raw ?? string.Empty).Trim();
      }

      return string.Empty;
    }
  }
}
