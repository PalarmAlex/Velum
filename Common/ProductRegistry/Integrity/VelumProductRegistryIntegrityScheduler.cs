using System;
using System.Collections.Generic;
using System.Threading;
using ISIDA.Common;
using Velum.Configuration;
using Velum.SolidHomeostasis;
using Velum.UI.AssemblyRegistry;

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
   /// Пульсовый такт <b>не выполняет</b> тяжёлой COM-работы: дерево активной сборки
   /// не обходим, реестр по открытым документам (Name/ExportMeta sync) на пульсе
   /// не синхронизируем. Синхронизация открытых документов выполняется только на
   /// событиях SW (FileSavePostNotify, OnActiveModelDocChangeNotify) и по кнопке
    /// «Обновить свойства» формы реестра. Сканеры проблем реестра работают в
    /// closed-области (нет открытых документов) в фоновых ThreadPool-тиках.
    /// Счётчик пульсов без активного документа сбрасывается при появлении ActiveDoc;
    /// первый флот-тик — через N таких пульсов, далее каждые N.
    /// </para>
    /// <para>
    /// Область open/closed отслеживается событием <c>ActiveModelDocChangeNotify</c>
    /// и синхронизируется при старте пульсации. Так как это событие <b>не приходит</b>
    /// при закрытии последнего документа (нового активного нет), на каждом пульсе
    /// в open-области выполняется лёгкая проверка наличия открытых документов
    /// (один вызов <c>GetFirstDocument</c>, без обхода дерева сборки): если документов нет,
    /// область переводится в closed и сканирование возобновляется.
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
    /// <summary>1 — открыт хотя бы один документ SW; сканеры проблем реестра не работают.</summary>
    private static int _openDocumentsScopeActive;
    private static bool _brokenPassActive;
    private static bool _drawingPassActive;
    private static bool _dupDesPassActive;
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

    /// <summary>
    /// Отписаться от пульса ISIDA и сбросить флаг подписки. Вызывается перед
    /// <c>VelumIsidaHost.Shutdown</c>: Dispose контекста ISIDA выполняет
    /// <c>GlobalTimer.ClearSystems()</c>, который обнуляет делегаты событий таймера —
    /// без сброса <see cref="_hooked"/> повторная подписка после перезагрузки ISIDA
    /// (сохранение настроек) не выполнялась бы, и шедулер терял пульс до перезапуска SW.
    /// </summary>
    internal static void Detach()
    {
      GlobalTimer.OnPulseCompleted -= OnPulseCompleted;
      GlobalTimer.PulsationStateChanged -= OnPulsationStateChanged;
      Interlocked.Exchange(ref _hooked, 0);
      Interlocked.Exchange(ref _enabled, 0);
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
        {
          Interlocked.Exchange(ref _reloadRequested, 1);

          // Синхронизировать область скана при старте пульсации: события
          // ActiveModelDocChange могли не прийти (документы открыты до загрузки
          // надстройки; последний документ закрыт при остановленной пульсации).
          // Разовый вызов по команде пользователя — тяжёлая работа допустима (E10).
          NotifyOpenDocumentsScopeChanged();
        }
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
          // Область open определяем по наличию любого открытого документа, а не по
          // множеству путей: у нового несохранённого документа GetPathName() пуст,
          // и раньше он ошибочно считался closed-областью — сканеры целостности
          // продолжали работать при открытом документе.
          openScope = VelumProductRegistryOpenDocumentsScope.HasOpenDocuments();
        }
        catch
        {
          openScope = true;
        }

        SyncDocumentsScope(openScope);

        // При входе в open заставляем текущий фоновый квант уступить на следующем
        // элементе: SyncDocumentsScope обнулил курсоры/кэш, продолжать старый квант нет смысла.
        // Выставляем ПОСЛЕ SyncDocumentsScope — внутри него AbortDiscoveryPassesUnlocked
        // сбрасывает флаг в 0.
        if (openScope)
          Interlocked.Exchange(ref _tickYieldRequested, 1);
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
        bool dupDesPassActive;
        bool dxfPassActive;
        bool pdfPassActive;
        lock (Gate)
        {
          store = _store;
          mappings = _mappings;
          brokenPassActive = _brokenPassActive;
          drawingPassActive = _drawingPassActive;
          dupDesPassActive = _dupDesPassActive;
          dxfPassActive = _dxfPassActive;
          pdfPassActive = _pdfPassActive;
        }

        if (store == null)
          return;

        VelumProductItem item = store.GetItem(itemId);
        if (item == null)
        {
          VelumProductRegistryProblemCache.RemoveAllForItem(itemId);

          // Запись удалена (её прежний ключ уже недоступен), а с ней мог исчезнуть
          // дубль у оставшегося партнёра - перепроверяем записи с кэшированным дублем.
          VelumProductRegistryDuplicateDesignationScanner.RevalidateCachedDuplicates(store);
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

        // Дубль обозначения. Прежний ключ читаем ДО перепроверки: после удачной
        // правки проблема снимается, и найти «бывшего» партнёра по новому ключу
        // уже невозможно - иначе его строка висела бы до следующего полного прохода.
        VelumProductRegistryProblemEntry dupOldProblem;
        VelumProductRegistryProblemCache.TryGet(
            itemId,
            VelumProductRegistryProblemKind.DuplicateDesignation,
            out dupOldProblem);

        // Множество visited исключает повторный обход группы (запись и её партнёр
        // имеют один ключ, поэтому рекурсивный обход ушёл бы по кругу).
        var dupVisited = new HashSet<int> { itemId };
        VelumProductRegistryDuplicateDesignationScanner.RevalidateItem(
            store, item, writePending: dupDesPassActive);

        // Партнёры по новому ключу (конфликт остался или появился) и по прежнему
        // ключу (конфликт только что убрали) - проблема пересчитывается сразу.
        VelumProductRegistryDuplicateDesignationScanner.RevalidateKeyGroup(
            store, item.Designation, item.FilePath, dupVisited, dupDesPassActive);
        if (dupOldProblem != null)
        {
          VelumProductRegistryDuplicateDesignationScanner.RevalidateKeyGroup(
              store,
              dupOldProblem.Designation,
              dupOldProblem.FilePath,
              dupVisited,
              dupDesPassActive);
        }

        VelumProductRegistryDxfFleetScanner.RevalidateItem(item, writePending: dxfPassActive);
        VelumProductRegistryPdfFleetScanner.RevalidateItem(store, item, writePending: pdfPassActive);

        string path = item.GetNormalizedPathKey();
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


    /// <summary>
    /// Пульсовый такт: только счётчики и планирование фоновых тиков сканеров.
    /// COM-дерево активной сборки не обходим, реестр по открытым документам
    /// (Name/ExportMeta sync) на пульсе не синхронизируем — тяжёлая работа со
    /// сборкой на каждый пульс подвешивала UI SolidWorks. Синхронизация открытых
    /// документов выполняется на событиях SW (FileSavePostNotify,
    /// OnActiveModelDocChangeNotify) и по кнопке «Обновить свойства» формы реестра.
    /// Область open/closed отслеживается событиями (см. <see cref="NotifyOpenDocumentsScopeChanged"/>).
    /// </summary>
    private static void OnPulseCompleted(int pulseNumber)
    {
      if (Interlocked.CompareExchange(ref _enabled, 1, 1) != 1)
        return;
      if (!GlobalTimer.IsPulsationRunning)
        return;

      int period = VelumAppConfig.HeavyMetricsPulsePeriod;
      if (period < 1)
        period = 1;

      // Документ открыт: сканеры проблем реестра на паузе, синхронизация — на событиях SW.
      // НО: ActiveModelDocChangeNotify не приходит при закрытии последнего документа
      // (нового активного нет), и область могла застрять в open — лёгкая проверка
      // наличия открытых документов (один GetFirstDocument, без обхода дерева, E10).
      if (IsOpenDocumentsScopeActive && !TrySyncClosedScopeIfNoOpenDocuments())
      {
        Interlocked.Exchange(ref _closedScopePulseCount, 0);
        return;
      }

      // Симметрично: closed → open. Событие ActiveModelDocChangeNotify могло не прийти
      // (гонка, COM-исключение в колбэке, документ открыт «молча» через API), и область
      // застряла в closed при открытом документе — сканер молотил бы реестр. Лёгкая
      // проверка наличия открытых документов (один GetFirstDocument, без дерева, E10).
      if (!IsOpenDocumentsScopeActive && TrySyncOpenScopeIfOpenDocuments())
      {
        Interlocked.Exchange(ref _closedScopePulseCount, 0);
        // На случай, если тик уже стартовал между проверками — заставляем уступить.
        Interlocked.Exchange(ref _tickYieldRequested, 1);
        return;
      }

      int closedCount = Interlocked.Increment(ref _closedScopePulseCount);
      RaiseScanStateChanged();
      bool due = closedCount > 0 && (closedCount % period == 0);
      if (!due)
        return;

      if (!QueueTick(null))
      {
        // Предыдущий тик ещё выполняется в фоновом потоке — не пропускаем работу,
        // а сигнализируем ему уступить: он завершит текущий квант и сразу продолжит
        // с сохранённых курсоров. Так сканирование идёт непрерывно, а пульс UI
        // успевает проходить между квантами.
        Interlocked.Exchange(ref _tickYieldRequested, 1);
        return;
      }

      RaiseScanStateChanged();
    }

    /// <summary>
    /// Лёгкая проверка open-области на пульсе: <c>ActiveModelDocChangeNotify</c> не приходит
    /// при закрытии последнего документа (нового активного нет), и область могла застрять в open.
    /// Проверяется наличие любого открытого документа (<c>GetFirstDocument</c>) — без обхода
    /// дерева сборки (E10). Возвращает true, если открытых документов нет и область переведена в closed.
    /// </summary>
    private static bool TrySyncClosedScopeIfNoOpenDocuments()
    {
      bool becameClosed = false;
      VelumSolidEnvironmentBridge.RunOnTaskPaneUiThread(() =>
      {
        if (VelumProductRegistryOpenDocumentsScope.HasOpenDocuments())
          return;

        SyncDocumentsScope(openScope: false);
        becameClosed = true;
      });
      return becameClosed;
    }

    /// <summary>
    /// Симметрично <see cref="TrySyncClosedScopeIfNoOpenDocuments"/>: лёгкая проверка
    /// closed-области на пульсе. Если открытый документ есть (в т.ч. новый несохранённый
    /// или только что сохранённый, когда <c>ActiveModelDocChangeNotify</c> не пришёл),
    /// область переводится в open. Возвращает true, если открытые документы есть и
    /// область переведена в open.
    /// </summary>
    private static bool TrySyncOpenScopeIfOpenDocuments()
    {
      bool becameOpen = false;
      VelumSolidEnvironmentBridge.RunOnTaskPaneUiThread(() =>
      {
        if (!VelumProductRegistryOpenDocumentsScope.HasOpenDocuments())
          return;

        SyncDocumentsScope(openScope: true);
        becameOpen = true;
      });
      return becameOpen;
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

      // Живая проверка: между пульсом (closed) и стартом тика мог открыться документ,
      // а событие ActiveModelDocChangeNotify не прийти (новый несохранённый — FileSavePostNotify
      // тоже не сработает до сохранения). Один COM-вызов на тик (не на элемент) — допустимо.
      // RunTick в фоновом потоке: RunOnTaskPaneUiThread синхронно входит в STA-поток SW (E5),
      // тик подождёт, если UI-поток занят — для фонового тика это приемлемо.
      // Проверка и перевод в open — одним входом на UI-поток: документ не «убежит» между ними.
      bool activeNow = false;
      try
      {
        VelumSolidEnvironmentBridge.RunOnTaskPaneUiThread(() =>
        {
          activeNow = VelumProductRegistryOpenDocumentsScope.HasOpenDocuments();
          if (activeNow)
            SyncDocumentsScope(openScope: true);
        });
      }
      catch
      {
        // COM-исключение до SyncDocumentsScope — переводим область в open консервативно.
        activeNow = true;
        try
        {
          VelumSolidEnvironmentBridge.RunOnTaskPaneUiThread(
              () => SyncDocumentsScope(openScope: true));
        }
        catch
        {
        }
      }

      if (activeNow)
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
      // Результаты проверки путей BrokenLink передаются остальным сканерам (known):
      // модель, уже проверенная сканером битых ссылок, на сетевой шаре повторно не ходит.
      Dictionary<string, VelumProductRegistryPathStatus> knownPaths = null;
      bool brokenPassCompleted = VelumProductRegistryBrokenLinkScanner.Tick(
          store, ref _brokenCursor, ref _brokenPassActive, null, 0, shouldStop, null,
          out knownPaths);
      passCompleted &= brokenPassCompleted;
      if (AbortRunTickIfOpenDocumentsScope())
        return;

      // Early-exit по битым ссылкам: если BrokenLink завершил полный проход и нашёл
      // битые ссылки, реестр уже повреждён — дорогой сетевой обход
      // чертежей (MissingDrawing/DXF/PDF) в этом проходе бесполезен: пока есть битые
      // ссылки, он будет повторяться и на следующих тиках. DuplicateDesignation
      // (чисто в памяти) выполняется всегда.
      bool brokenEarlyExit = brokenPassCompleted && HasBrokenLinksInKnown(knownPaths);
      if (!brokenEarlyExit)
      {
        passCompleted &= VelumProductRegistryMissingDrawingScanner.Tick(
            store, mappings, ref _drawingCursor, ref _drawingPassActive,
            shouldStop: shouldStop, known: knownPaths);
        if (AbortRunTickIfOpenDocumentsScope())
          return;

        passCompleted &= VelumProductRegistryDxfFleetScanner.Tick(
            store, ref _dxfCursor, ref _dxfPassActive,
            shouldStop: shouldStop, known: knownPaths);
        if (AbortRunTickIfOpenDocumentsScope())
          return;

        passCompleted &= VelumProductRegistryPdfFleetScanner.Tick(
            store, ref _pdfCursor, ref _pdfPassActive,
            shouldStop: shouldStop, known: knownPaths);
        if (AbortRunTickIfOpenDocumentsScope())
          return;
      }

      // Дубли обозначений — группировка по ключам в памяти (без ФС), весь проход
      // укладывается в один квант; курсор между тиками не нужен.
      passCompleted &= VelumProductRegistryDuplicateDesignationScanner.Tick(
          store, ref _dupDesPassActive);
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
    /// true, если среди уже проверенных путей есть битые ссылки (статус <see cref="VelumProductRegistryPathStatus.No"/>).
    /// Используется для раннего выхода: при повреждённом реестре дорогие сетевые проверки чертежей не запускаются.
    /// </summary>
    private static bool HasBrokenLinksInKnown(
        Dictionary<string, VelumProductRegistryPathStatus> knownPaths)
    {
      if (knownPaths == null || knownPaths.Count == 0)
        return false;

      foreach (KeyValuePair<string, VelumProductRegistryPathStatus> pair in knownPaths)
      {
        if (pair.Value == VelumProductRegistryPathStatus.No)
          return true;
      }

      return false;
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

      if (_dupDesPassActive)
        VelumProductRegistryDuplicateDesignationScanner.DiscardPending();

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
      _dupDesPassActive = false;
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
          // Автоимена каталогов — не часть реестра: перечитывать их при каждом
          // изменении items.json не нужно (файл меняется только из формы
          // автоимён — там вызывается NotifyMappingsChanged). Иначе каждое
          // фоновое сохранение зеркал перечитывало оба файла настроек.
          if (_mappings == null || _mappings.Count == 0)
            _mappings = VelumProductRegistryFolderAutoNames.LoadOrCreate();
        }
        catch (Exception ex)
        {
          Logger.Warning("Velum registry integrity load: " + ex.Message);
        }
      }
    }

    /// <summary>
    /// Сброс кэша автоимён каталогов (после сохранения из формы автоимён)
    /// и запрос перечитать реестр на следующем тике.
    /// </summary>
    internal static void NotifyMappingsChanged()
    {
      lock (Gate)
      {
        _mappings = new List<VelumProductFolderAutoNameMapping>();
      }

      Interlocked.Exchange(ref _reloadRequested, 1);
    }
  }
}
