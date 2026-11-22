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
    /// Был ли документ изменён (<c>GetSaveFlag()</c>) ДО начала сохранения. «Сохранить»
    /// сбрасывает флаг при записи файла, а genuine rebuild («обновить перед сохранением»)
    /// приходит внутри save-контура, где live-запись pending подавлена. Фиксируем dirty
    /// заранее, чтобы в post-save дожать DXF pending (Случай 57).
    /// </summary>
    private static bool _partDirtyBeforeSave;

    /// <summary>Ключ документа, для которого зафиксирован <see cref="_partDirtyBeforeSave"/>.</summary>
    private static string _partDirtyBeforeSaveKey;

    /// <summary>
    /// Приходил ли regen («Обновить», <c>RegenPostNotify2</c> с <c>geometryEdited == false</c>)
    /// ИЗВНЕ save-контура с момента последнего Save детали. Regen помечает деталь dirty
    /// (<c>GetSaveFlag()</c>) без изменения геометрии, поэтому post-save дожим Случая 57
    /// не должен срабатывать, когда dirty объяснён именно regen-ом (Случай 60).
    /// Genuine-правка (<c>ModifyNotify</c>) вне save-контура сбрасывает этот флаг: правка
    /// уже записала live pending, regen её не «отменяет».
    /// </summary>
    private static bool _partRegenSinceLastSave;

    /// <summary>Ключ детали, для которого зафиксирован <see cref="_partRegenSinceLastSave"/>.</summary>
    private static string _partRegenSinceLastSaveKey;

    /// <summary>
    /// Baseline комбинированного отпечатка геометрии (эскизы + квантованный объём) детали.
    /// Снимается на Save/экспорте и после фактической записи pending. Live-гейтpending
    /// пишет только когда текущий отпечаток отличается от baseline (Случай 62): regen-шум
    /// («Обновить» в детали, regen-каскад сборки через edit-target) геометрию не меняет —
    /// отпечаток равен baseline, pending не пишем.
    /// </summary>
    private static long _partGeometryFingerprintBaseline;

    /// <summary>Есть ли зафиксированный baseline отпечатка (<see cref="_partGeometryFingerprintBaseline"/>).</summary>
    private static bool _partGeometryFingerprintBaselineKnown;

    /// <summary>Ключ детали, для которого зафиксирован baseline отпечатка геометрии.</summary>
    private static string _partGeometryFingerprintKey;

    /// <summary>
    /// Кэш <c>GetUpdateStamp</c>, на котором последний раз пересчитывали отпечаток геометрии.
    /// При неизменном штампе объём и эскизы не могли измениться — отпечаток не пересчитываем
    /// (Случай 62, кэш дорогого COM-обхода; regen-серии с одним штампом не платят дважды).
    /// </summary>
    private static int _partGeometryFingerprintStamp;

    /// <summary>Кэш текущего отпечатка геометрии, соответствующий <see cref="_partGeometryFingerprintStamp"/>.</summary>
    private static long _partGeometryFingerprintCached;

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

    /// <summary>
    /// Save детали: не писать pending, пока идёт FileSave. Фиксирует <c>GetSaveFlag()</c>
    /// ДО сохранения — genuine rebuild («обновить перед сохранением») приходит внутри
    /// save-контура и live-запись pending подавлена; флаг нужен для post-save дожима (Случай 57).
    /// </summary>
    internal static void BeginPartSave(ModelDoc2 modelDoc)
    {
      if (_partSaveDepth < 1)
        _partSaveDepth = 1;

      string key = TryGetDocumentKey(modelDoc);
      _partDirtyBeforeSave = TryGetPartSaveFlag(modelDoc);
      _partDirtyBeforeSaveKey = key;
    }

    /// <summary>Конец Save детали.</summary>
    internal static void EndPartSave()
    {
      if (_partSaveDepth > 0)
        _partSaveDepth--;
    }

    /// <summary>Значение <c>GetSaveFlag()</c> детали; false при недоступности COM.</summary>
    private static bool TryGetPartSaveFlag(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return false;

      try
      {
        return modelDoc.GetSaveFlag();
      }
      catch
      {
        return false;
      }
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
    /// <para>
    /// Случай 59: <paramref name="geometryEdited"/> различает источник события. Pending
    /// («DXF устарел») пишем только при genuine-правке (<c>ModifyNotify</c>). Чистая
    /// перестройка (<c>RegenPostNotify2</c> — «Обновить», regen сборки каскадом, regen при
    /// открытии детали в контексте сборки) геометрию не меняет, но SolidWorks поднимает
    /// <c>GetUpdateStamp</c> и помечает деталь грязной; форс pending из Случая 56
    /// превращал это в ложное устаревание и зацикливал экспорт (regen сборки → regen детали
    /// → pending → «устарел» → …). Regen переснимает baseline и сбрасывает absorb regen-после-Save,
    /// но pending не пишет.
    /// </para>
    /// </summary>
    /// <param name="modelDoc">Деталь, для которой пришло событие Modify/Regen.</param>
    /// <param name="geometryEdited">
    /// true — genuine-правка геометрии (<c>ModifyNotify</c>), pending писать можно;
    /// false — чистая перестройка (<c>RegenPostNotify2</c>), pending не пишем.
    /// </param>
    internal static void NotifyPartGeometryModified(ModelDoc2 modelDoc, bool geometryEdited)
    {
      if (modelDoc == null || IsGeometryPendingStampSyncSuppressed)
        return;

      // Случай 60: regen-щит ставится/снимается В НАЧАЛЕ метода, до absorb- и save-rebuild-
      // веток. Нужен post-save дожиму Случая 57 (NotifyPartSaved), чтобы отличить dirty от
      // regen «Обновить» от dirty от genuine-правки. Regen извне (geometryEdited == false)
      // ставит флаг, genuine-правка (Modify) снимает. Regen ВНУТРИ save-контура сюда не
      // доходит (IsGeometryPendingStampSyncSuppressed отсекает выше) — Случай 57 цел.
      if (geometryEdited)
      {
        _partRegenSinceLastSave = false;
        _partRegenSinceLastSaveKey = null;
      }
      else
      {
        _partRegenSinceLastSave = true;
        _partRegenSinceLastSaveKey = TryGetDocumentKey(modelDoc);
      }

      if (IsFlatPatternFoldOnlyChange(modelDoc))
      {
        CaptureFlatPatternFoldSignature(modelDoc);
        return;
      }

      if (IsAbsorbingDxfExportPropertyDirty(modelDoc))
      {
        // Случай 58 + 62: absorb-окно DXF-экспорта «глотает» dirty от записи свойств до Save,
        // но не должно глушить genuine-правку. Дискриминатор — отпечаток геометрии
        // (эскизы + квантованный объём): запись свойств его не меняет, правка — меняет.
        // Если отпечаток разошёлся с baseline, снятым на ArmAfterDxfExport, — это реальная
        // правка, ломаем absorb и проваливаемся к записи pending (не дожидаясь Save).
        // Regen-всплеск геометрию не меняет и absorb не снимает.
        if (!ShouldWritePendingByGeometryFingerprint(modelDoc))
        {
          // Dirty от записи пути/штампов после DXF (или regen-всплеск): до Save pending не пишем.
          CaptureFlatPatternFoldSignature(modelDoc);
          return;
        }

        // Genuine-правка внутри absorb-окна: снимаем absorb, чтобы Save не
        // перифинализировал штампы (иначе правка потеряется), и пишем pending ниже.
        _absorbDxfExportPropertyDirtyUntilSave = false;
        Logger.Info("Velum DXF pending: geometry changed after export (absorb broken before save)");
      }

      if (_absorbPartSaveRebuild)
      {
        // Save сам поднимает GetUpdateStamp и шлёт Regen — это не смена геометрии DXF.
        _absorbPartSaveRebuild = false;
        CaptureFlatPatternFoldSignature(modelDoc);
        return;
      }

      // Переключение активной конфигурации поднимает GetUpdateStamp и шлёт Regen/Modify,
      // но DXF-геометрию активной конфигурации не меняет — не пишем pending.
      if (TryAbsorbActiveConfigurationChange(modelDoc))
      {
        CaptureFlatPatternFoldSignature(modelDoc);
        return;
      }

      // Случай 62 — надёжный live-гейт. Решает НЕ geometryEdited, а расхождение отпечатка
      // геометрии (эскизы + квантованный объём) с baseline, снятым на Save/экспорте:
      //  - «Обновить» в детали и regen-каскад сборки через edit-target геометрию не меняют —
      //    отпечаток равен baseline, pending не пишем (снимает обе фазы бага);
      //  - genuine-правка (эскиз/перемещение/радиус/толщина) меняет отпечаток — пишем pending.
      // Add-правка радиуса/толщины (Случай 56): Modify приходит ДО rebuild со СТАРЫМ
      // отпечатком — гейт не пропускает, но regen ПОСЛЕ rebuild даёт новый объём →
      // отпечаток расходится → pending запишется здесь же (regen-ветка больше не «слепая»).
      if (!ShouldWritePendingByGeometryFingerprint(modelDoc))
      {
        // Чистая перестройка (regen) без смены геометрии: переснимаем fold/config baseline,
        // чтобы следующий Modify не выглядел правкой из-за сдвига счётчика regen-ом.
        CaptureFlatPatternFoldSignature(modelDoc);

        // Случай 62: отпечаток совпал с baseline — это regen-шум. Новый pending не пишем,
        // но СТАРЫЙ pending от прошлого цикла (pending > export) снимаем: геометрия
        // не менялась, «устарел» — ложь. Иначе метрика горит вечно на старом pending.
        ClearDxfPendingIfStale(modelDoc);

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

      // Pending записан — отпечаток теперь актуален как новый baseline: иначе следующая
      // regen-серия с тем же отпечатком снова пыталась бы писать pending (COM-шум).
      CapturePartGeometryFingerprintBaseline(modelDoc);
    }

    /// <summary>
    /// Случай 62: снимает устаревший pending-штамп DXF-конфигурации, если геометрия
    /// не менялась (regen-шум). pending &gt; export, но отпечаток == baseline — метрика
    /// «устарел» ложна, старый pending от прошлого цикла должен быть очищен.
    /// </summary>
    private static void ClearDxfPendingIfStale(ModelDoc2 modelDoc)
    {
        if (modelDoc == null)
            return;

        string key = TryGetDocumentKey(modelDoc);
        if (string.IsNullOrEmpty(key) || string.Equals(key, "no_doc", StringComparison.Ordinal))
            return;

        string configName =
            VelumDxfArtifactResolver.TryResolveDefaultExportConfigurationName(modelDoc);
        if (string.IsNullOrWhiteSpace(configName))
            return;

        // Нет pending — снимать нечего.
        if (!TryReadStoredUpdateStamp(
                modelDoc,
                VelumExportDocumentationProperties.DxfGeometryPendingStamp,
                configName,
                out int pendingStamp) ||
            pendingStamp <= 0)
            return;

        // pending не больше export — метрика и так молчит.
        TryReadStoredUpdateStamp(
            modelDoc,
            VelumExportDocumentationProperties.DxfGeometryUpdateStamp,
            configName,
            out int exportStamp);
        if (exportStamp > 0 && pendingStamp <= exportStamp)
            return;

        RunWithGeometryPendingStampSyncSuppressed(() =>
        {
            TryClearGeometryStamp(
                modelDoc,
                VelumExportDocumentationProperties.DxfGeometryPendingStamp,
                configName,
                out _);
        });

        Logger.Info(
            "Velum DXF pending cleared on regen-noise (fingerprint == baseline) pending=" +
            pendingStamp.ToString(CultureInfo.InvariantCulture) +
            " export=" + exportStamp.ToString(CultureInfo.InvariantCulture));
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
    /// Save / «Обновить» сборки: дожимает DXF pending-штамп в компонентах, реально изменённых
    /// (<c>GetSaveFlag</c> — тот же признак, что значок «светофор» в дереве построения).
    /// Правка детали в контексте сборки порождает Modify/Regen на САМОЙ сборке, а
    /// <c>GetEditTarget</c> покрывает только текущую редактируемую деталь, поэтому остальные
    /// изменённые компоненты оставались без pending и диагностика пакетного экспорта DXF
    /// показывала «OK» вместо «Устарел».
    /// Обход дерева компонентов выполняется только по событию Save/Update сборки — на пульсе
    /// и при активной сборке сканирования нет (E10).
    /// </summary>
    /// <param name="assy">Сборка, компоненты которой обновляют или сохраняют.</param>
    /// <returns>Открытые документы деталей, в которые записан pending-штамп (для синхронизации зеркал реестра).</returns>
    internal static IReadOnlyList<ModelDoc2> StampDirtyAssemblyComponents(AssemblyDoc assy)
    {
      var stamped = new List<ModelDoc2>();
      if (assy == null)
        return stamped;

      object[] components;
      try
      {
        components = assy.GetComponents(true) as object[];
      }
      catch
      {
        return stamped;
      }

      if (components == null || components.Length == 0)
        return stamped;

      var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      for (int i = 0; i < components.Length; i++)
      {
        if (!(components[i] is Component2 comp))
          continue;

        ModelDoc2 part;
        try
        {
          part = comp.GetModelDoc2() as ModelDoc2;
        }
        catch
        {
          continue;
        }

        if (part == null)
          continue;

        try
        {
          // Только реально изменённые детали: у компонентов, поднятых regen-всплеском
          // при открытии/перестройке сборки, флаг чистый (E63 — не плодить ложное устаревание).
          if (part.GetType() != (int)swDocumentTypes_e.swDocPART || !part.GetSaveFlag())
            continue;

          string path = part.GetPathName();
          if (!string.IsNullOrWhiteSpace(path) && !seen.Add(path))
            continue;

          // DXF-конфигурация резолвится как при правке (E37), иначе pending уйдёт в развёртку.
          string configName = VelumDxfArtifactResolver.TryResolveDefaultExportConfigurationName(part);

          // Запись свойства поднимает ModifyNotify на сборке — подавляем рекурсивный pending.
          RunWithGeometryPendingStampSyncSuppressed(() =>
              TrySyncPendingGeometryStamp(part, configName, out _));

          stamped.Add(part);
        }
        catch
        {
        }
      }

      if (stamped.Count > 0)
      {
        Logger.Info("Velum DXF stamps: pending written to " +
                    stamped.Count.ToString(CultureInfo.InvariantCulture) +
                    " modified assembly components");
      }

      return stamped;
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

      // Случай 62: фиксируем baseline отпечатка геометрии на момент экспорта — live-гейт
      // в absorb-окне и после него пишет pending только при расхождении с этим baseline.
      CapturePartGeometryFingerprintBaseline(modelDoc);
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
    /// <para>
    /// Если документ был изменен ДО Save (Случай 57), genuine rebuild («обновить перед
    /// сохранением») пришел внутри save-контура, где live-запись pending подавлена, а
    /// ModifyNotify на правку толщины/радиуса не доходит. Post-save regen глотается
    /// <c>_absorbPartSaveRebuild</c> — метрика молчала до ручного «Обновить». Дожимает
    /// DXF pending по <c>GetSaveFlag()</c>, зафиксированному в <see cref="BeginPartSave"/>,
    /// исключая fold/unfold, смену активной конфигурации и regen-шум извне («Обновить»
    /// без правки, Случай 60): если dirty объяснён regen-ом (<see cref="_partRegenSinceLastSave"/>),
    /// pending не дожимается.
    /// </para>
    /// </summary>
    /// <returns>true, если штампы перефинализированы или дописан pending (нужен MarkStale Export).</returns>
    internal static bool NotifyPartSaved(ModelDoc2 modelDoc)
    {
        if (modelDoc == null)
            return false;

        string key = TryGetDocumentKey(modelDoc);
        bool sameDoc = string.Equals(key, _trackedPartDocKey, StringComparison.Ordinal);
        bool wasAbsorbing = sameDoc && _absorbDxfExportPropertyDirtyUntilSave;

        // Genuine-правка геометрии, подавленная save-контуром: GetSaveFlag() был true ДО save.
        // Сравниваем fold/config ДО пересъёма baseline (CaptureFlatPatternFoldSignature ниже).
        bool wasDirtyBeforeSave =
            string.Equals(key, _partDirtyBeforeSaveKey, StringComparison.Ordinal) &&
            _partDirtyBeforeSave;
        // Случай 60: regen ИЗВНЕ save-контура («Обновить») тоже поднимает GetSaveFlag() без
        // правки геометрии. Если dirty объяснён regen-ом — post-save дожим Случая 57 не нужен
        // (и дал бы ложное устаревание). Genuine-правка (Modify) вне save-контура сбрасывает
        // этот флаг и пишет live pending, поэтому Случай 57 остаётся покрытым.
        bool regenSinceSave =
            _partRegenSinceLastSave &&
            string.Equals(key, _partRegenSinceLastSaveKey, StringComparison.Ordinal);
        bool foldOnlyChange = IsFlatPatternFoldOnlyChange(modelDoc);
        bool configOnlyChange = TryAbsorbActiveConfigurationChange(modelDoc);

        // Случай 62: запоминаем расхождение отпечатка ДО пересъёма baseline, иначе baseline
        // обновится на текущее состояние и post-save гейт всегда даст false (регресс Случая 57).
        bool geometryChangedBeforeSave = ShouldWritePendingByGeometryFingerprint(modelDoc);

        _partDirtyBeforeSave = false;
        _partDirtyBeforeSaveKey = null;
        _partRegenSinceLastSave = false;
        _partRegenSinceLastSaveKey = null;
        _absorbDxfExportPropertyDirtyUntilSave = false;
        _absorbPartSaveRebuild = true;

        if (!sameDoc)
            _trackedPartDocKey = key;

        CaptureFlatPatternFoldSignature(modelDoc);

        // Случай 62: после Save деталь чиста — текущий отпечаток геометрии становится новым
        // baseline. Следующий regen-каскад сборки («Обновить» при возврате в сборку, фаза 2)
        // сравнит отпечаток с этим baseline: геометрия та же → pending не пишем.
        CapturePartGeometryFingerprintBaseline(modelDoc);

        if (wasAbsorbing)
        {
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

        // Не DXF-экспорт: если была genuine-правка геометрии — дожать pending.
        bool needPending = VelumDxfGeometryStampRules.ShouldWriteDxfPendingAfterSave(
            wasAbsorbingExportDirty: false,
            wasDirtyBeforeSave: wasDirtyBeforeSave,
            isFlatPatternFoldOnlyChange: foldOnlyChange,
            isActiveConfigurationOnlyChange: configOnlyChange,
            regenFromUpdateCommand: regenSinceSave);

        if (!needPending)
            return false;

        // Случай 62: post-save контур тоже не должен писать pending на regen-шуме.
        // _partRegenSinceLastSave сбрасывается на Modify (geometryEdited=true), а regen
        // «Обновить» приходит именно как Modify — поэтому флага недостаточно. Проверяем
        // отпечаток геометрии (снят ДО пересъёма baseline выше).
        if (!geometryChangedBeforeSave)
        {
            Logger.Info("Velum DXF pending after save skipped (fingerprint == baseline, regen-noise)");
            return false;
        }

        string dxfConfigName =
            VelumDxfArtifactResolver.TryResolveDefaultExportConfigurationName(modelDoc);
        RunWithGeometryPendingStampSyncSuppressed(() =>
            TrySyncPendingGeometryStamp(modelDoc, dxfConfigName, out _));

        Logger.Info("Velum DXF pending after save (geometry changed before save, suppressed in save contour)");
        return true;
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
    /// Объём детали через <c>ModelDoc2.Extension.CreateMassProperty()</c> →
    /// <c>IMassProperty.Volume</c> (СИ модели). Входит в комбинированный отпечаток
    /// геометрии (<see cref="ComputePartGeometryFingerprint"/>) как дискриминатор
    /// add-правки радиуса/толщины, которую токен эскизов не ловит (Случай 56/62).
    /// Объём читается через <c>InvokeMember</c> и целиком в <c>try/catch</c>: сбой COM
    /// возвращает NaN (отпечаток тогда опирается только на эскизы), на поток событий
    /// не влияет.
    /// </summary>
    private static double TryReadPartVolume(ModelDoc2 modelDoc)
    {
      try
      {
        object ext = modelDoc?.Extension;
        if (ext == null)
          return double.NaN;

        if (!VelumSolidComDispatchHelper.TryInvokeMethod(
                "DXF_FINGERPRINT", ext, "CreateMassProperty", new object[0], out object mp) ||
            mp == null)
          return double.NaN;

        if (!VelumSolidComDispatchHelper.TryGetProperty(
                "DXF_FINGERPRINT", mp, "Volume", out object volObj))
          return double.NaN;

        return Convert.ToDouble(volObj, CultureInfo.InvariantCulture);
      }
      catch
      {
        return double.NaN;
      }
    }

    /// <summary>
    /// Токен геометрии 2D-эскизов детали (дерево фич). Без GetUpdateStamp — запись
    /// свойств экспорта его не «сдвигает». Ловит добавление/удаление/изменение элемента
    /// эскиза внутри absorb-окна DXF-экспорта (Случай 58).
    /// </summary>
    private static long ComputePartGeometryToken(ModelDoc2 modelDoc)
    {
      unchecked
      {
        long hash = 17L;
        if (modelDoc == null)
          return hash;

        try
        {
          // Рекурсивный обход дерева фич, извлечение Sketch, хеш координат точек эскиза.
          Feature feat = modelDoc.FirstFeature() as Feature;
          int guard = 0;
          while (feat != null && guard++ < 512)
          {
            hash = hash * 31L + HashSketchGeometry(feat);
            feat = feat.GetNextFeature() as Feature;
          }
        }
        catch
        {
        }

        return hash;
      }
    }

    /// <summary>
    /// Комбинированный отпечаток геометрии детали (Случай 62) — эскизы + квантованный объём.
    /// Токен эскизов ловит перемещение/изменение элементов; квантованный объём дополняет его
    /// для add-правки радиуса/толщины, которая эскизы не меняет, но меняет mass-property.
    /// Объём квантуется грубо (<c>volume * 1e9</c>), чтобы не ложно срабатывать на COM-шум
    /// regen при неизменной геометрии (на regen «Обновить» объём постоянен).
    /// Только для одной детали / edit-target — дерево сборки не обходится.
    /// </summary>
    private static long ComputePartGeometryFingerprint(ModelDoc2 modelDoc)
    {
      unchecked
      {
        long hash = ComputePartGeometryToken(modelDoc);

        double volume = TryReadPartVolume(modelDoc);
        long volumeBucket =
            double.IsNaN(volume) || double.IsInfinity(volume)
                ? 0L
                : (long)Math.Round(volume * 1e9);

        return hash * 31L + volumeBucket;
      }
    }

    /// <summary>
    /// Возвращает текущий отпечаток геометрии детали с кэшем по <c>GetUpdateStamp</c>
    /// (Случай 62). Пересчитывает дорогой COM-обход (эскизы + mass-property) только когда
    /// штамп вырос относительно последнего замера по этой же детали — regen-серии с одним
    /// штампом не платят COM дважды. При смене детали кэш сбрасывается и пересчёт обязателен.
    /// </summary>
    private static long GetOrRefreshGeometryFingerprint(ModelDoc2 modelDoc, string key, int currentStamp)
    {
      bool sameDoc = string.Equals(key, _partGeometryFingerprintKey, StringComparison.Ordinal);
      if (sameDoc &&
          !VelumDxfGeometryStampRules.ShouldRecomputeGeometryFingerprint(
              _partGeometryFingerprintStamp, currentStamp))
      {
        return _partGeometryFingerprintCached;
      }

      long fingerprint = ComputePartGeometryFingerprint(modelDoc);
      _partGeometryFingerprintKey = key;
      _partGeometryFingerprintStamp = currentStamp;
      _partGeometryFingerprintCached = fingerprint;
      return fingerprint;
    }

    /// <summary>
    /// Снимает baseline отпечатка геометрии детали на текущем <c>GetUpdateStamp</c>
    /// (Случай 62): вызывается на Save/экспорте и после фактической записи pending.
    /// Первый контакт (baseline не был известен) тоже фиксирует baseline, не порождая pending.
    /// </summary>
    private static void CapturePartGeometryFingerprintBaseline(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return;

      string key = TryGetDocumentKey(modelDoc);
      if (string.IsNullOrEmpty(key) || string.Equals(key, "no_doc", StringComparison.Ordinal))
        return;

      TryGetCurrentUpdateStamp(modelDoc, out int currentStamp);
      long fingerprint = ComputePartGeometryFingerprint(modelDoc);

      _partGeometryFingerprintKey = key;
      _partGeometryFingerprintStamp = currentStamp;
      _partGeometryFingerprintCached = fingerprint;
      _partGeometryFingerprintBaseline = fingerprint;
      _partGeometryFingerprintBaselineKnown = true;
    }

    /// <summary>
    /// Надёжный live-гейт (Случай 62): писать ли pending по текущему событию Modify/Regen
    /// над деталью. Переснимает baseline при первом контакте и возвращает false (не писать).
    /// Иначе пишет pending только когда отпечаток геометрии разошёлся с baseline — regen-шум
    /// («Обновить», regen-каскад сборки через edit-target) отпечаток не меняет → false.
    /// </summary>
    private static bool ShouldWritePendingByGeometryFingerprint(ModelDoc2 modelDoc)
    {
      string key = TryGetDocumentKey(modelDoc);
      TryGetCurrentUpdateStamp(modelDoc, out int currentStamp);

      bool baselineKnown =
          _partGeometryFingerprintBaselineKnown &&
          string.Equals(key, _partGeometryFingerprintKey, StringComparison.Ordinal);

      if (!baselineKnown)
      {
        CapturePartGeometryFingerprintBaseline(modelDoc);
        return false;
      }

      long current = GetOrRefreshGeometryFingerprint(modelDoc, key, currentStamp);
      return VelumDxfGeometryStampRules.ShouldWriteDxfPendingOnGeometryFingerprint(
          baselineKnown: true,
          baselineFingerprint: _partGeometryFingerprintBaseline,
          currentFingerprint: current);
    }

    /// <summary>
    /// Рекурсивный обход дерева фич: если spec — Sketch, хешируем его геометрию;
    /// иначе — рекурсивно спускаемся в подфичи (вложенные эскизы, паттерны).
    /// </summary>
    private static long HashSketchGeometry(Feature feature)
    {
      unchecked
      {
        long hash = 0L;
        if (feature == null)
          return hash;

        try
        {
          object spec = feature.GetSpecificFeature2();
          if (spec is Sketch sketch)
          {
            return HashSketch(sketch);
          }

          // Подфичи (вложенные эскизы, паттерны, тела и т.д.)
          Feature sub = feature.GetFirstSubFeature() as Feature;
          int guard = 0;
          while (sub != null && guard++ < 256)
          {
            hash = hash * 31L + HashSketchGeometry(sub);
            sub = sub.GetNextSubFeature() as Feature;
          }
        }
        catch
        {
        }

        return hash;
      }
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

        // Случай 56: добавление радиуса / изменение толщины может не поднять GetUpdateStamp
        // в момент ModifyNotify — тогда pending == export и метрика «DXF устарел» молчит.
        // Форсим pending > export (по образцу PDF-версии), если счётчик не вырос относительно
        // экспортного штампа.
        TryReadStoredUpdateStamp(
            modelDoc,
            VelumExportDocumentationProperties.DxfGeometryUpdateStamp,
            config,
            out int exportStamp);
        int pendingToWrite = VelumDxfGeometryStampRules.ComputeForcedPendingStamp(stamp, exportStamp);

        if (TryReadStoredUpdateStamp(
                modelDoc,
                VelumExportDocumentationProperties.DxfGeometryPendingStamp,
                config,
                out int existingPending) &&
            existingPending >= pendingToWrite)
          return true;

        if (!TryWriteExportGeometryStamp(
                modelDoc,
                VelumExportDocumentationProperties.DxfGeometryPendingStamp,
                config,
                pendingToWrite,
                out message))
          return false;

        if (!TryGetCurrentUpdateStamp(modelDoc, out int afterStamp) || afterStamp <= 0)
          return true;

        // Запись свойства могла сдвинуть GetUpdateStamp — догоняем pending, но не понижаем
        // его ниже уже записанного форса (после форса pendingToWrite может быть больше
        // реального GetUpdateStamp, а afterStamp — вернуться к exportStamp).
        int finalPending = VelumDxfGeometryStampRules.ComputeForcedPendingStamp(afterStamp, exportStamp);
        if (finalPending <= pendingToWrite)
          return true;

        return TryWriteExportGeometryStamp(
            modelDoc,
            VelumExportDocumentationProperties.DxfGeometryPendingStamp,
            config,
            finalPending,
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
