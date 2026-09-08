using System;
using System.Collections.Generic;
using System.Threading;
using ISIDA.Common;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Velum.Configuration;
using Velum.SolidHomeostasis;
using Velum.UI.AssemblyRegistry;
using Xarial.XCad.SolidWorks;

namespace Velum.UI.ProductRegistry
{
  /// <summary>
  /// Пульсация сканеров целостности реестра: тяжёлый пульс раз в N глобальных пульсов
  /// (N = <see cref="VelumAppConfig.HeavyMetricsPulsePeriod"/>).
  /// <para>
  /// <b>Важно:</b> автосканирование проблем реестра (BrokenLink / MissingDrawing / DXF / PDF /
  /// MissingRegistryEntry) запускается <b>только при отсутствии открытых документов SW</b>.
  /// При открытии любого документа сканирование немедленно прерывается, весь кэш проблем
  /// (<see cref="VelumProductRegistryProblemCache"/>) очищается, метрики сбрасываются в «хорошо».
  /// Не вызывайте <see cref="RevalidateItem"/> / <see cref="RevalidateCachedItemProblems"/> и
  /// не запускайте сканеры вручную при открытом документе — это оставляет устаревшие метрики
  /// и приводит к багам (см. <see cref="NotifyOpenDocumentsScopeChanged"/>).
  /// </para>
  /// <para>
  /// При активном документе на heavy-тике выполняется только Name/ExportMeta sync;
  /// сканеры проблем реестра на паузе до закрытия всех документов.
  /// Счётчик пульсов без активного документа сбрасывается при появлении ActiveDoc;
  /// первый флот-тик — через N таких пульсов, далее каждые N.
  /// </para>
  /// <para>
  /// Каждый сканер копит результат прохода в pending и держит курсор
  /// (<c>searchCursor</c>). Тик работает <b>непрерывно</b> до следующего тяжёлого
  /// пульса: когда наступает следующий пульс (или открывается документ), сканер
  /// уступает (<c>shouldStop</c>), сохраняя курсор, и продолжается с места прерывания
  /// на следующем кванте — без ожидания очередного тяжёлого пульса. Полный проход
  /// реестра занимает несколько квантов, каждый проход покрывает весь реестр без пропусков.
  /// </para>
  /// <para>
  /// Счётчик итераций (<see cref="ScanIterationCount"/>) сбрасывается при полном
  /// проходе всего реестра: видно, что реестр пройден целиком и начался новый цикл с №1.
  /// </para>
  /// </summary>
  internal static class VelumProductRegistryIntegrityScheduler
  {
    private const string ScopeClosed = "*";
    private const string ScopeOpen = "open";

    private static readonly object Gate = new object();
    private static int _enabled;
    private static int _hooked;
    private static int _tickInFlight;
    /// <summary>
    /// 1 — во время текущего тика наступил следующий тяжёлый пульс (или открылся документ):
    /// сканеры должны уступить, чтобы UI-пульс прошёл. Тик завершится, и сканирование
    /// сразу продолжится с сохранённых курсоров (без ожидания следующего тяжёлого пульса).
    /// </summary>
    private static int _tickYieldRequested;
    private static int _reloadRequested = 1;
    private static int _brokenCursor;
    private static int _drawingCursor;
    private static int _dxfCursor;
    private static int _pdfCursor;
    /// <summary>Пульсы подряд без активного документа SW (сброс при ActiveDoc).</summary>
    private static int _closedScopePulseCount;
    /// <summary>Пульсы подряд при ActiveDoc (только Name/ExportMeta sync, без скана проблем).</summary>
    private static int _openScopePulseCount;
    /// <summary>1 — открыт хотя бы один документ SW; сканеры проблем реестра не работают.</summary>
    private static int _openDocumentsScopeActive;
    private static bool _brokenPassActive;
    private static bool _drawingPassActive;
    private static bool _dxfPassActive;
    private static bool _pdfPassActive;
    private static string _lastScopeKey = string.Empty;
    private static VelumProductRegistryStore _store;
    private static List<VelumProductFolderAutoNameMapping> _mappings =
        new List<VelumProductFolderAutoNameMapping>();
    /// <summary>Счётчик запущенных тиков сканирования (инкрементируется при каждом запуске RunTick).</summary>
    private static int _scanIterationCount;
    /// <summary>
    /// true — текущий «полный проход» реестра ещё не завершён (хотя бы один из
    /// сканеров держит курсор). По завершении всех четырёх проходов сбрасывается.
    /// Используется для сброса <see cref="_scanIterationCount"/>: новый проход реестра
    /// должен начинаться с №1, а не продолжать общий счётчик тиков.
    /// </summary>
    private static bool _scanPassInProgress;
    /// <summary>
    /// Количество компонентов активной сборки (GetComponents(false).Length).
    /// Используется для оптимизации: если количество не изменилось — пропускаем
    /// тяжёлую синхронизацию NameSync/ExportMetaSync на каждом N-м пульсе.
    /// -1 — сборка не открыта или количество не инициализировано.
    /// </summary>
    private static int _assemblyComponentCount = -1;

    /// <summary>Вызывается при изменении состояния фонового сканирования реестра.</summary>
    internal static event Action ScanStateChanged;

    internal static void EnsureAttached()
    {
      if (Interlocked.CompareExchange(ref _hooked, 1, 0) == 0)
      {
        GlobalTimer.OnPulseCompleted += OnPulseCompleted;
        GlobalTimer.PulsationStateChanged += OnPulsationStateChanged;
      }

      SyncEnabledFromPulse();
    }

    internal static void SyncEnabledFromPulse()
    {
      bool run = false;
      try
      {
        run = GlobalTimer.IsPulsationRunning;
      }
      catch
      {
        run = false;
      }

      if (run)
      {
        int was = Interlocked.Exchange(ref _enabled, 1);
        if (was != 1)
          Interlocked.Exchange(ref _reloadRequested, 1);
      }
      else
      {
        Interlocked.Exchange(ref _enabled, 0);
      }

      RaiseScanStateChanged();
    }

    internal static bool IsDiscoveryPassActive
    {
      get
      {
        lock (Gate)
          return _brokenPassActive || _drawingPassActive || _dxfPassActive || _pdfPassActive;
      }
    }

    /// <summary>true — открыт документ SW; автосканирование проблем реестра отключено.</summary>
    internal static bool IsOpenDocumentsScopeActive =>
        Volatile.Read(ref _openDocumentsScopeActive) == 1;

    /// <summary>true — тик сканирования сейчас выполняется в фоновом потоке.</summary>
    internal static bool IsTickInFlight => Volatile.Read(ref _tickInFlight) == 1;

    /// <summary>true — шедулер подключён к пульсу и пульсация разрешена.</summary>
    internal static bool IsEnabled => Volatile.Read(ref _enabled) == 1;

    /// <summary>true — пульсация ISIDA запущена.</summary>
    internal static bool IsPulsationRunning
    {
      get
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
    }

    /// <summary>Число пульсов подряд без активного документа.</summary>
    internal static int ClosedScopePulseCount => Volatile.Read(ref _closedScopePulseCount);

    /// <summary>Число пульсов подряд с активным документом.</summary>
    internal static int OpenScopePulseCount => Volatile.Read(ref _openScopePulseCount);

    /// <summary>Период тяжёлых пульсов (сканирование раз в N пульсов).</summary>
    internal static int HeavyMetricsPulsePeriod
    {
      get
      {
        int p = VelumAppConfig.HeavyMetricsPulsePeriod;
        return p < 1 ? 1 : p;
      }
    }

    /// <summary>
    /// Сколько пульсов осталось до следующего скана в closed-области.
    /// 0 — следующий пульс запустит скан; -1 — сканирование невозможно (open-область или не включено).
    /// </summary>
    internal static int PulsesUntilNextScan
    {
      get
      {
        if (!IsEnabled || !IsPulsationRunning || IsOpenDocumentsScopeActive)
          return -1;
        int period = HeavyMetricsPulsePeriod;
        int count = ClosedScopePulseCount;
        if (count <= 0)
          return period;
        int remainder = count % period;
        return remainder == 0 ? 0 : period - remainder;
      }
    }

    /// <summary>
    /// Номер итерации сканирования (инкрементируется при каждом запуске RunTick,
    /// сбрасывается на 0 при полном проходе всего реестра — новый проход с №1).
    /// </summary>
    internal static int ScanIterationCount => Volatile.Read(ref _scanIterationCount);

    internal static void NotifyRegistryChanged()
    {
      Interlocked.Exchange(ref _reloadRequested, 1);
    }

    /// <summary>
    /// Уведомить о возможном изменении состава активной сборки.
    /// Сбрасывает счётчик компонентов, чтобы на следующем пульсе выполнилась
    /// полная синхронизация NameSync/ExportMetaSync.
    /// Вызывать из VelumSolidWorksEventsConnector при событиях Modify/Save сборки.
    /// </summary>
    internal static void NotifyAssemblyStructureChanged()
    {
      Volatile.Write(ref _assemblyComponentCount, -1);
    }

    private static void RaiseScanStateChanged()
    {
      try
      {
        ScanStateChanged?.Invoke();
      }
      catch
      {
      }
    }

    /// <summary>
    /// Получить количество компонентов активной сборки (GetComponents(false)).
    /// Возвращает -1, если активный документ не сборка или ошибка.
    /// </summary>
    private static int GetActiveAssemblyComponentCount()
    {
      try
      {
        ModelDoc2 active = VelumSolidEnvironmentBridge.TryGetSolidWorksApplication()?.Sw?.IActiveDoc2 as ModelDoc2;
        if (active == null || !(active is AssemblyDoc))
          return -1;

        AssemblyDoc assyDoc = active as AssemblyDoc;
        object[] components = assyDoc.GetComponents(false) as object[];
        if (components == null)
          return -1;

        return components.Length;
      }
      catch
      {
        return -1;
      }
    }

    /// <summary>
    /// Смена активного документа SW: синхронизировать область скана.
    /// При открытии документа — прервать discovery и очистить весь кэш проблем.
    /// Вызывать с UI/COM-потока SolidWorks (см. <see cref="VelumSolidEnvironmentBridge.OnActiveSolidDocumentChanged"/>).
    /// </summary>
    internal static void NotifyOpenDocumentsScopeChanged()
    {
      VelumSolidEnvironmentBridge.RunOnTaskPaneUiThread(() =>
      {
        bool openScope;
        try
        {
          HashSet<string> openPaths = VelumProductRegistryOpenDocumentsScope.TryCollectNormalizedOpenPaths();
          openScope = openPaths != null && openPaths.Count > 0;
        }
        catch
        {
          openScope = true;
        }

        SyncDocumentsScope(openScope);
      });
    }

    internal static void RevalidateCachedItemProblems()
    {
      if (Volatile.Read(ref _openDocumentsScopeActive) == 1)
        return;

      try
      {
        EnsureStoreLoaded(force: false);

        IReadOnlyList<VelumProductRegistryProblemEntry> snap =
            VelumProductRegistryProblemCache.Snapshot();
        var itemIds = new HashSet<int>();
        for (int i = 0; i < snap.Count; i++)
        {
          VelumProductRegistryProblemEntry entry = snap[i];
          if (entry == null || entry.ItemId <= 0)
            continue;
          if (entry.Kind == VelumProductRegistryProblemKind.MissingRegistryEntry)
            continue;
          itemIds.Add(entry.ItemId);
        }

        foreach (int itemId in itemIds)
          RevalidateItem(itemId);

        VelumProductRegistryIntegrityProbes.PublishScoresToGate(registryOnlySnapshot: false);
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum registry integrity revalidate-cached: " + ex.Message);
      }
    }

    internal static void RevalidateItem(int itemId)
    {
      if (itemId <= 0)
        return;
      if (Volatile.Read(ref _openDocumentsScopeActive) == 1)
        return;

      try
      {
        EnsureStoreLoaded(force: false);
        VelumProductRegistryStore store;
        List<VelumProductFolderAutoNameMapping> mappings;
        bool brokenPassActive;
        bool drawingPassActive;
        bool dxfPassActive;
        bool pdfPassActive;
        lock (Gate)
        {
          store = _store;
          mappings = _mappings;
          brokenPassActive = _brokenPassActive;
          drawingPassActive = _drawingPassActive;
          dxfPassActive = _dxfPassActive;
          pdfPassActive = _pdfPassActive;
        }

        if (store == null)
          return;

        VelumProductItem item = store.GetItem(itemId);
        if (item == null)
        {
          VelumProductRegistryProblemCache.RemoveAllForItem(itemId);
          return;
        }

        if (VelumProductRegistryBrokenLinkScanner.TryEvaluateBroken(item, out VelumProductRegistryProblemEntry broken))
        {
          if (brokenPassActive)
            VelumProductRegistryProblemCache.UpsertPending(broken);
          else
            VelumProductRegistryProblemCache.Upsert(broken);
        }
        else
          VelumProductRegistryProblemCache.Remove(itemId, VelumProductRegistryProblemKind.BrokenLink);

        if (VelumProductRegistryIntegrityRules.IsPartOrAssemblyPath(item.FilePath))
        {
          if (VelumProductRegistryMissingDrawingScanner.TryEvaluateMissingDrawing(
                  store, mappings, item, out VelumProductRegistryProblemEntry missing))
          {
            if (drawingPassActive)
              VelumProductRegistryProblemCache.UpsertPending(missing);
            else
              VelumProductRegistryProblemCache.Upsert(missing);
          }
          else
            VelumProductRegistryProblemCache.Remove(itemId, VelumProductRegistryProblemKind.MissingDrawing);
        }
        else
        {
          VelumProductRegistryProblemCache.Remove(itemId, VelumProductRegistryProblemKind.MissingDrawing);
        }

        VelumProductRegistryDxfFleetScanner.RevalidateItem(item, writePending: dxfPassActive);
        VelumProductRegistryPdfFleetScanner.RevalidateItem(store, item, writePending: pdfPassActive);

        string path = VelumProductRegistryStore.NormalizeFilePathKey(item.FilePath);
        if (!string.IsNullOrEmpty(path))
          VelumProductRegistryProblemCache.RemoveMissingRegistryEntry(path);
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum registry integrity revalidate: " + ex.Message);
      }
    }

    private static void OnPulsationStateChanged()
    {
      SyncEnabledFromPulse();
    }


    private static void OnPulseCompleted(int pulseNumber)
    {
      if (Interlocked.CompareExchange(ref _enabled, 1, 1) != 1)
        return;
      if (!GlobalTimer.IsPulsationRunning)
        return;

      int period = VelumAppConfig.HeavyMetricsPulsePeriod;
      if (period < 1)
        period = 1;

      VelumSolidEnvironmentBridge.RunOnTaskPaneUiThread(() =>
      {
        HashSet<string> openPaths = null;
        try
        {
          openPaths = VelumProductRegistryOpenDocumentsScope.TryCollectNormalizedOpenPaths();
        }
        catch (Exception ex)
        {
          Logger.Warning("Velum registry integrity open-docs: " + ex.Message);
          openPaths = null;
        }

        bool openScope = openPaths != null && openPaths.Count > 0;
        SyncDocumentsScope(openScope);

        bool due;
        if (openScope)
        {
          Interlocked.Exchange(ref _closedScopePulseCount, 0);
          due = ShouldRunOpenScopeSyncTick(period);
          if (!due)
            return;

          // Оптимизация: если активный документ — сборка, проверяем количество компонентов.
          // Если количество не изменилось с последней синхронизации — пропускаем тяжёлую
          // синхронизацию (Load + итерация по всем путям). Это устраняет перетрату ресурсов
          // при работе со сборкой без изменений состава.
          int currentCompCount = GetActiveAssemblyComponentCount();
          if (currentCompCount >= 0)
          {
            // Сборка открыта: сравниваем количество компонентов
            int expectedCount = Volatile.Read(ref _assemblyComponentCount);
            if (expectedCount == currentCompCount && expectedCount >= 0)
            {
              // Количество не изменилось — пропускаем синхронизацию
              return;
            }

            // Количество изменилось или сборка впервые — обновляем счётчик
            Volatile.Write(ref _assemblyComponentCount, currentCompCount);
          }
          else
          {
            // Не сборка (деталь/чертёж) — сбрасываем счётчик компонентов
            Volatile.Write(ref _assemblyComponentCount, -1);
          }

          try
          {
            ISwApplication swApp = VelumSolidEnvironmentBridge.TryGetSolidWorksApplication();
            VelumProductRegistryNameSync.SyncOpenPathsFromDisk(swApp, openPaths);
            VelumProductRegistryExportMetaSync.SyncOpenPathsFromDisk(swApp, openPaths);

            // Обновляем счётчик компонентов после синхронизации (на случай, если сборка
            // изменилась в процессе синхронизации)
            if (currentCompCount >= 0)
            {
              int finalCompCount = GetActiveAssemblyComponentCount();
              if (finalCompCount >= 0)
                Volatile.Write(ref _assemblyComponentCount, finalCompCount);
            }
          }
          catch (Exception ex)
          {
            Logger.Warning("Velum registry integrity open-docs sync: " + ex.Message);
          }

          return;
        }

        Interlocked.Exchange(ref _openScopePulseCount, 0);
        int closedCount = Interlocked.Increment(ref _closedScopePulseCount);
        RaiseScanStateChanged();
        due = closedCount > 0 && (closedCount % period == 0);
        if (!due)
          return;

        if (!QueueTick(openPaths))
        {
          // Предыдущий тик ещё выполняется в фоновом потоке — не пропускаем работу,
          // а сигнализируем ему уступить: он завершит текущий квант и сразу продолжит
          // с сохранённых курсоров. Так сканирование идёт непрерывно, а пульс UI
          // успевает проходить между квантами.
          Interlocked.Exchange(ref _tickYieldRequested, 1);
          return;
        }

        RaiseScanStateChanged();
      });
    }

    /// <summary>
    /// Захватывает <see cref="_tickInFlight"/> и ставит фоновый тик сканирования в очередь.
    /// Возвращает true, если тик поставлен в очередь; false — если тик уже выполняется
    /// (вызывающий сам решает: пропустить или установить yield-сигнал).
    /// Если тик был прерван сигналом <see cref="_tickYieldRequested"/> (наступил следующий
    /// тяжёлый пульс), сканирование сразу продолжается с сохранённых курсоров,
    /// не дожидаясь следующего тяжёлого пульса.
    /// </summary>
    private static bool QueueTick(HashSet<string> openPaths)
    {
      if (Interlocked.CompareExchange(ref _tickInFlight, 1, 0) != 0)
        return false;

      // Сброс сигнала уступки: если он взведён к моменту старта нового тика,
      // квант начнётся заново и будет работать до следующего тяжёлого пульса.
      Interlocked.Exchange(ref _tickYieldRequested, 0);

      ThreadPool.QueueUserWorkItem(_ =>
      {
        bool continuation = false;
        try
        {
          RunTick(openPaths);
        }
        catch (Exception ex)
        {
          Logger.Warning("Velum registry integrity tick: " + ex.Message);
        }
        finally
        {
          Interlocked.Exchange(ref _tickInFlight, 0);
          RaiseScanStateChanged();
          // Сигнал пришёл во время кванта — продолжаем сканирование без паузы.
          continuation = Volatile.Read(ref _tickYieldRequested) == 1;
        }

        if (continuation)
          QueueTick(openPaths);
      });

      return true;
    }

    private static bool ShouldRunOpenScopeSyncTick(int period)
    {
      int n = Interlocked.Increment(ref _openScopePulseCount);
      return n % period == 0;
    }

    /// <summary>
    /// Переход closed ↔ open: abort discovery, полная очистка кэша проблем, сброс метрик.
    /// Сканеры проблем реестра работают только в closed-области (нет открытых документов SW).
    /// </summary>
    private static void SyncDocumentsScope(bool openScope)
    {
      lock (Gate)
      {
        string scopeKey = openScope ? ScopeOpen : ScopeClosed;
        if (string.Equals(scopeKey, _lastScopeKey, StringComparison.Ordinal))
          return;

        _lastScopeKey = scopeKey;
        Volatile.Write(ref _openDocumentsScopeActive, openScope ? 1 : 0);
        AbortDiscoveryPassesUnlocked();
        ClearAllProblemCacheUnlocked();

        // При переключении scope сбрасываем счётчик компонентов — следующая
        // синхронизация выполнится принудительно для обновления данных.
        Volatile.Write(ref _assemblyComponentCount, -1);
      }

      RaiseScanStateChanged();

      try
      {
        VelumProductRegistryIntegrityProbes.PublishScoresToGate(registryOnlySnapshot: false);
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum registry integrity scope publish: " + ex.Message);
      }
    }

    /// <summary>
    /// Полный флот-скан реестра. Вызывается только при отсутствии открытых документов SW.
    /// </summary>
    private static void RunTick(HashSet<string> openPaths)
    {
      if (Volatile.Read(ref _openDocumentsScopeActive) == 1)
        return;
      if (openPaths != null && openPaths.Count > 0)
        return;

      EnsureStoreLoaded(force: Interlocked.Exchange(ref _reloadRequested, 0) == 1);

      VelumProductRegistryStore store;
      List<VelumProductFolderAutoNameMapping> mappings;
      lock (Gate)
      {
        store = _store;
        mappings = _mappings;
      }

      if (store == null)
        return;

      if (Volatile.Read(ref _openDocumentsScopeActive) == 1)
        return;

      if (!_scanPassInProgress)
      {
        // Начинается новый полный проход реестра — счётчик итераций стартует с №1.
        Interlocked.Exchange(ref _scanIterationCount, 0);
        lock (Gate)
          _scanPassInProgress = true;
      }

      Interlocked.Increment(ref _scanIterationCount);

      // Колбэк остановки кванта: следующий тяжёлый пульс пришёл или документ открылся.
      // Сканеры проверяют его между элементами и уступают, сохраняя курсор.
      Func<bool> shouldStop = () =>
          Volatile.Read(ref _tickYieldRequested) == 1
          || Volatile.Read(ref _openDocumentsScopeActive) == 1;

      VelumProductRegistryMissingEntryScanner.TickFullScanClear();
      if (AbortRunTickIfOpenDocumentsScope())
        return;

      bool passCompleted = true;
      passCompleted &= VelumProductRegistryBrokenLinkScanner.Tick(
          store, ref _brokenCursor, ref _brokenPassActive,
          scopedItemIds: null, openDocumentCount: 0, shouldStop: shouldStop);
      if (AbortRunTickIfOpenDocumentsScope())
        return;

      passCompleted &= VelumProductRegistryMissingDrawingScanner.Tick(
          store, mappings, ref _drawingCursor, ref _drawingPassActive, shouldStop: shouldStop);
      if (AbortRunTickIfOpenDocumentsScope())
        return;

      passCompleted &= VelumProductRegistryDxfFleetScanner.Tick(
          store, ref _dxfCursor, ref _dxfPassActive, shouldStop: shouldStop);
      if (AbortRunTickIfOpenDocumentsScope())
        return;

      passCompleted &= VelumProductRegistryPdfFleetScanner.Tick(
          store, ref _pdfCursor, ref _pdfPassActive, shouldStop: shouldStop);
      if (AbortRunTickIfOpenDocumentsScope())
        return;

      // BOM diff probe — лёгкое сканирование без COM, только чтение JSON.
      if (Volatile.Read(ref _openDocumentsScopeActive) != 1)
      {
        VelumAssemblyBomDiffProbe.RunScan();
      }

      if (passCompleted)
      {
        // Все четыре сканера завершили полный проход реестра — счётчик сбросится
        // на следующем тике, и будет видно начало нового сканирования с №1.
        lock (Gate)
          _scanPassInProgress = false;
      }
    }

    /// <summary>
    /// Документ открыли во время фонового тика — сбросить частичный результат и прервать проход.
    /// </summary>
    private static bool AbortRunTickIfOpenDocumentsScope()
    {
      if (Volatile.Read(ref _openDocumentsScopeActive) != 1)
        return false;

      lock (Gate)
      {
        AbortDiscoveryPassesUnlocked();
        ClearAllProblemCacheUnlocked();
      }

      try
      {
        VelumProductRegistryIntegrityProbes.PublishScoresToGate(registryOnlySnapshot: false);
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum registry integrity tick abort publish: " + ex.Message);
      }

      return true;
    }

    private static void ClearAllProblemCacheUnlocked()
    {
      VelumProductRegistryProblemCache.Clear();
      VelumProductRegistryDxfFleetScanner.ClearAllKinds();
      VelumProductRegistryPdfFleetScanner.ClearAllKinds();
    }

private static void AbortDiscoveryPassesUnlocked()
    {
      _scanPassInProgress = false;
      _scanIterationCount = 0;
      _tickYieldRequested = 0;

      if (_brokenPassActive)
      {
        VelumProductRegistryProblemCache.DiscardPendingPass(
            VelumProductRegistryProblemKind.BrokenLink);
      }

      if (_drawingPassActive)
      {
        VelumProductRegistryProblemCache.DiscardPendingPass(
            VelumProductRegistryProblemKind.MissingDrawing);
      }

      if (_dxfPassActive)
        VelumProductRegistryDxfFleetScanner.DiscardPending();
      if (_pdfPassActive)
        VelumProductRegistryPdfFleetScanner.DiscardPending();

      _brokenCursor = 0;
      _drawingCursor = 0;
      _dxfCursor = 0;
      _pdfCursor = 0;
      _brokenPassActive = false;
      _drawingPassActive = false;
      _dxfPassActive = false;
      _pdfPassActive = false;
    }

    private static void EnsureStoreLoaded(bool force)
    {
      lock (Gate)
      {
        if (_store == null)
          _store = new VelumProductRegistryStore();

        bool reloadPending = Volatile.Read(ref _reloadRequested) == 1;
        if (!force && !reloadPending && _mappings != null && _mappings.Count > 0)
          return;

        if (reloadPending)
          Interlocked.Exchange(ref _reloadRequested, 0);

        try
        {
          _store.Load();
          _mappings = VelumProductRegistryFolderAutoNames.LoadOrCreate();
        }
        catch (Exception ex)
        {
          Logger.Warning("Velum registry integrity load: " + ex.Message);
        }
      }
    }
  }
}
