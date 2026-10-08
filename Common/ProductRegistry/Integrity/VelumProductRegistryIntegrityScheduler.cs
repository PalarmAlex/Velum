using System;
using System.Collections.Generic;
using System.Threading;
using ISIDA.Common;
using Velum.Configuration;
using Velum.ReactiveCore.Export;
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
  /// <para>
  /// Гейт доступности корня реестра (<see cref="IsRegistryRootUnavailable"/>): если
  /// сетевой каталог реестра недоступен (оборванный VPN), глобальный скан не
  /// запускается вовсе — счётчик итераций не растёт, иначе создаётся видимость
  /// «бесконечного тормозного сканирования» без реальной работы. Проба доступности
  /// редкая (раз в <see cref="VelumAppConfig.ScannerUnavailableRootPollPulses"/> пульсов),
  /// чтобы проверка мёртвого тома не добавляла задержку каждому такту.
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
    /// <summary>
    /// 1 — discovery-проход BOM-расхождений активен (находки лежат в pending кэша проблем).
    /// BOM-скан не курсорный и укладывается в один тик, но флаг нужен, чтобы прерывание
    /// прохода (открытие документа, смена области) сбрасывало pending наравне с остальными
    /// сканерами: иначе находки, закоммиченные в тот же тик, «восставали» бы в closed-области
    /// раньше, чем отработали сетевые сканеры.
    /// </summary>
    private static int _bomPassActive;
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
    /// <summary>
    /// true — последний <see cref="VelumProductRegistryStore.Load"/> завершился успешно
    /// (файлы реестра прочитаны). Отличает «пустой реестр» (загрузили, записей нет) от
    /// «не удалось прочитать» (сетевой том недоступен / таймаут — Load бросает IOException).
    /// Без этого признака пустой <see cref="_store"/> при недоступном шаре выдавался бы за
    /// «Реестр пустой» (тот же класс, что CASEBOOK-2 «затёртые после VPN-сбоя файлы»).
    /// </summary>
    private static bool _storeLoadedOk;
    /// <summary>
    /// true — пустое состояние реестра уже обслужено: состояние прохода и кэш проблем
    /// очищены, метрики опубликованы. Нужен, чтобы не дёргать <c>PublishScoresToGate</c>
    /// и полную очистку на каждом тике, пока реестр пуст. Сбрасывается при переходе
    /// к непустому реестру, чтобы следующий вход в «пусто» снова корректно почистился.
    /// </summary>
    private static bool _emptyRegistryCleaned;
    /// <summary>
    /// 1 — корневой каталог реестра документов (<see cref="VelumProductRegistryStore.RegistryFolderPath"/>)
    /// недоступен (сетевой том/VPN оборван): глобальное сканирование не запускается,
    /// счётчик итераций не крутится. Сбрасывается, когда проба доступа снова успешна.
    /// </summary>
    private static int _registryRootUnavailable;
    /// <summary>
    /// Сколько пульсов осталось до следующей пробы недоступного корня реестра
    /// (см. <see cref="VelumAppConfig.ScannerUnavailableRootPollPulses"/>). Пока счётчик
    /// положителен, <see cref="OnPulseCompleted"/> не ставит тик сканирования — проверка
    /// мёртвого тома не добавляет задержку каждому такту.
    /// </summary>
    private static int _unavailableRootPollCountdown;
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
    /// Сканер завершил полный проход в текущем цикле (сбрасывается по завершении
    /// цикла и при abort). Сканеры заканчивают проход в разных тиках (разные размеры
    /// квант и фильтры), а завершившийся сканер на следующем тике сразу начал бы
    /// новый проход и вернул false — поэтому «все завершены в одном тике» невозможно,
    /// и завершение фиксируется флагами до конца цикла.
    /// </summary>
    private static bool _brokenPassDone;
    private static bool _drawingPassDone;
    private static bool _dxfPassDone;
    private static bool _pdfPassDone;

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

    /// <summary>
    /// true — идёт цикл полного прохода реестра (от первого тика до закрытия всех проходов).
    /// В отличие от <see cref="IsDiscoveryPassActive"/> не «провисает» между тиками:
    /// сканеры завершают проход в разных тиках и сбрасывают свои passActive раньше
    /// конца цикла — вердикт «проблем нет/есть» на панели корректен только после
    /// закрытия всего цикла, а не по завершении части сканеров.
    /// </summary>
    internal static bool IsScanCycleActive
    {
      get
      {
        lock (Gate)
          return _scanPassInProgress;
      }
    }

    /// <summary>
    /// true, если discovery-проход BOM-расхождений активен (находки в pending).
    /// Использует <see cref="Interlocked"/> — флаг пишется из фонового тика и читается
    /// при прерывании прохода.
    /// </summary>
    internal static bool IsBomDiscoveryPassActive => Volatile.Read(ref _bomPassActive) == 1;

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

    /// <summary>Номер итерации сканирования (инкрементируется при каждом запуске RunTick,
    /// сбрасывается на 0 при полном проходе всего реестра — новый проход с №1).
    /// </summary>
    internal static int ScanIterationCount => Volatile.Read(ref _scanIterationCount);

    /// <summary>
    /// Прогресс текущего прохода: сколько строк реестра пройдено самым МЕДЛЕННЫМ
    /// из ещё не завершившихся сканеров (min по курсорам not-done); завершившийся
    /// сканер (done) считается как весь реестр — его курсор после commit сбрасывается
    /// в 0. Max по курсорам показывал лидера: у сетевых сканеров курсор — индекс в
    /// общем списке, а квант — отфильтрованные записи, поэтому их курсоры перескакивают
    /// через тысячи строк; плюс любой done при max залипал на total, пока шёл
    /// последний сканер. Для статуса панели: «пройдено X из Y строк» — честный
    /// прогресс цикла (монотонный: курсоры not-done только растут, вклад done = total).
    /// </summary>
    internal static int ScanProgressCursor
    {
        get
        {
          lock (Gate)
          {
            int total = 0;
            VelumProductRegistryStore store = _store;
            if (store != null)
              total = store.GetAllItems().Length;

            // Все done (момент перед закрытием цикла) — весь реестр пройден.
            int min = total;
            if (!_brokenPassDone)
              min = Math.Min(min, _brokenCursor);
            if (!_drawingPassDone)
              min = Math.Min(min, _drawingCursor);
            if (!_dxfPassDone)
              min = Math.Min(min, _dxfCursor);
            if (!_pdfPassDone)
              min = Math.Min(min, _pdfCursor);

            // Начало цикла: курсоры ещё 0, но первый квант уже в работе —
            // показываем его размер, иначе весь первый тик статус выглядит
            // как «0 пройдено», будто ничего не делается.
            if (min == 0 && total > 0)
              min = Math.Min(VelumAppConfig.ScannerBatchSize, total);

            return min;
          }
        }
    }

    /// <summary>Число записей реестра в текущем проходе (0 — стор не загружен).</summary>
    internal static int ScanProgressTotal
    {
      get
      {
        VelumProductRegistryStore store = _store;
        if (store == null)
          return 0;
        return store.GetAllItems().Length;
      }
    }

    /// <summary>Номер текущего тика фонового сканирования (для кратности BOM-прохода).</summary>
    private static int _tickSequence;

    /// <summary>
    /// true — реестр успешно прочитан и в нём нет ни одной записи (папки/файлы есть,
    /// изделий 0). Сканировать нечего: счётчик итераций не крутится, на панели
    /// показывается «Реестр пустой». false при недоступном пути (Load бросил
    /// IOException) — это НЕ пустой реестр, а сбой чтения (см. <see cref="_storeLoadedOk"/>).
    /// </summary>
    internal static bool IsRegistryEmpty
    {
      get
      {
        if (!Volatile.Read(ref _storeLoadedOk))
          return false;
        VelumProductRegistryStore store = _store;
        return store != null && store.GetAllItems().Length == 0;
      }
    }

    /// <summary>
    /// true — корневой каталог реестра недоступен: глобальный скан не запускается,
    /// счётчик итераций не растёт (см. <see cref="_registryRootUnavailable"/>).
    /// </summary>
    internal static bool IsRegistryRootUnavailable =>
        Volatile.Read(ref _registryRootUnavailable) == 1;

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

      // Корень реестра недоступен (оборванный VPN/сетевой том): сканировать нечего,
      // счётчик итераций не крутим. Проба доступности — редкая (раз в
      // ScannerUnavailableRootPollPulses пульсов), чтобы проверка мёртвого тома
      // не добавляла задержку каждому такту.
      if (ShouldSkipScanForUnavailableRoot())
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
        if (openScope)
        {
          // Вход в open-область (документ открыт): удалить document-specific ключи из gate,
          // сбросить маркер недостоверности снимка (COM-опрос при открытых документах
          // состоится на следующем пульсе, а _lastErrorKind от закрытой области
          // блокирует release параметров гомеостаза до первого успешного COM).
          VelumSolidEnvironmentGate.ClearDocumentSpecificProbes();
          VelumSolidEnvironmentGate.MarkSnapshotFresh();
        }

        // registryOnlySnapshot: true — публикуем только значения из кэша проблем реестра
        // (которые уже очищены ClearAllProblemCacheUnlocked). При открытии документа
        // document-specific пробы (PDF/DXF) не нужны в gate — они обновятся на пульсе.
        VelumProductRegistryIntegrityProbes.PublishScoresToGate(registryOnlySnapshot: true);
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

      // Корень реестра недоступен: сканировать нечего (сетевой том/VPN оборван).
      // Проверка идёт ДО EnsureStoreLoaded: иначе Load платил бы таймаут на чтение
      // несуществующего тома на каждом тике, а счётчик итераций рос бы без работы.
      if (AbortIfRegistryRootUnavailable())
        return;

      EnsureStoreLoaded(force: Interlocked.Exchange(ref _reloadRequested, 0) == 1);

      // Proba корня прошла — снимаем пометку недоступности (том вернулся). Делаем это
      // после Load: если чтение всё же упало (гонка), пометку поставит EnsureStoreLoaded.
      MarkRegistryRootAvailable();

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

      // Пустой реестр (успешно прочитан, записей 0): сканировать нечего. Курсорные
      // сканеры при items.Count==0 в первом тике возвращают passCompleted=false
      // (passActive ещё false, коммитить нечего) — флаги *PassDone никогда не
      // выставились бы, цикл _scanPassInProgress не закрылся бы, и счётчик итераций
      // крутился бы бесконечно (1, 2, 3 …) при фактическом отсутствии работы.
      // Ранний выход: один раз чистим состояние прохода/кэш/метрики и не трогаем счётчик.
      if (IsRegistryEmpty)
      {
        if (!Volatile.Read(ref _emptyRegistryCleaned))
        {
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
            Logger.Warning("Velum registry integrity empty publish: " + ex.Message);
          }

          Volatile.Write(ref _emptyRegistryCleaned, true);
          RaiseScanStateChanged();
        }

        return;
      }

      // Реестр непустой — разрешаем повторную очистку при следующем опустении.
      Volatile.Write(ref _emptyRegistryCleaned, false);

      if (!_scanPassInProgress)
      {
        // Начинается новый полный проход реестра — счётчик итераций стартует с №1.
        Interlocked.Exchange(ref _scanIterationCount, 0);
        lock (Gate)
          _scanPassInProgress = true;
      }

      Interlocked.Increment(ref _scanIterationCount);
      int tickSeq = Interlocked.Increment(ref _tickSequence);

      // Колбэк остановки кванта: следующий тяжёлый пульс пришёл или документ открылся.
      // Сканеры проверяют его между элементами и уступают, сохраняя курсор.
      Func<bool> shouldStop = () =>
          Volatile.Read(ref _tickYieldRequested) == 1
          || Volatile.Read(ref _openDocumentsScopeActive) == 1;

      VelumProductRegistryMissingEntryScanner.TickFullScanClear();
      if (AbortRunTickIfOpenDocumentsScope())
        return;

      // Дубли обозначений укладываются в один квант — их результат учитывается
      // в этом же тике; курсорные сканеры завершают проход в разных тиках,
      // их завершение фиксируется флагами *PassDone до конца цикла.
      bool passCompleted = true;

      // Результаты проверки путей BrokenLink передаются остальным сканерам (known):
      // модель, уже проверенная сканером битых ссылок, на сетевой шаре повторно не ходит.
      Dictionary<string, VelumProductRegistryPathStatus> knownPaths = null;
      bool brokenEarlyExit = false;
      if (!_brokenPassDone)
      {
        bool brokenPassCompleted = VelumProductRegistryBrokenLinkScanner.Tick(
            store, ref _brokenCursor, ref _brokenPassActive, null, 0, shouldStop, null,
            out knownPaths);
        if (brokenPassCompleted)
        {
          _brokenPassDone = true;
        }
      }
      if (AbortRunTickIfOpenDocumentsScope())
        return;

      // Early-exit по битым ссылкам: если BrokenLink завершил полный проход и нашёл
      // битые ссылки, реестр уже повреждён — дорогой сетевой обход
      // чертежей (MissingDrawing/DXF/PDF) в этом проходе бесполезен: пока есть битые
      // ссылки, он будет повторяться и на следующих тиках. DuplicateDesignation
      // (чисто в памяти) выполняется всегда.
      brokenEarlyExit = _brokenPassDone && HasBrokenLinksInKnown(knownPaths);
      if (!brokenEarlyExit)
      {
        if (!_drawingPassDone)
        {
          if (VelumProductRegistryMissingDrawingScanner.Tick(
              store, mappings, ref _drawingCursor, ref _drawingPassActive,
              shouldStop: shouldStop, known: knownPaths))
          {
            _drawingPassDone = true;
          }
        }
        if (AbortRunTickIfOpenDocumentsScope())
          return;

        if (!_dxfPassDone)
        {
          if (VelumProductRegistryDxfFleetScanner.Tick(
              store, ref _dxfCursor, ref _dxfPassActive,
              shouldStop: shouldStop, known: knownPaths))
          {
            _dxfPassDone = true;
          }
        }
        if (AbortRunTickIfOpenDocumentsScope())
          return;

        if (!_pdfPassDone)
        {
          if (VelumProductRegistryPdfFleetScanner.Tick(
              store, ref _pdfCursor, ref _pdfPassActive,
              shouldStop: shouldStop, known: knownPaths))
          {
            _pdfPassDone = true;
          }
        }
        if (AbortRunTickIfOpenDocumentsScope())
          return;
      }
      else
      {
        // Реестр повреждён — сетевые проходы этого цикла пропускаются целиком:
        // без фиксации «done» они считались бы незавершёнными, и цикл не закрылся бы.
        _drawingPassDone = true;
        _dxfPassDone = true;
        _pdfPassDone = true;
      }

      // Дубли обозначений — группировка по ключам в памяти (без ФС), весь проход
      // укладывается в один квант; курсор между тиками не нужен.
      passCompleted &= VelumProductRegistryDuplicateDesignationScanner.Tick(
          store, ref _dupDesPassActive);
      if (AbortRunTickIfOpenDocumentsScope())
        return;

      // BOM diff probe — лёгкое сканирование без COM, только чтение JSON.
      // Кратность по настройке: не курсорный проход завершается и коммитится за один тик,
      // поэтому на первом тике цикла он опережал DXF/PDF и поднимал свою метрику раньше.
      int bomPeriod = VelumAppConfig.BomDiffScanPeriodPulses;
      bool bomDue = tickSeq % bomPeriod == 0;
      if (bomDue && Volatile.Read(ref _openDocumentsScopeActive) != 1)
      {
        Interlocked.Exchange(ref _bomPassActive, 1);
        try
        {
          VelumAssemblyBomDiffProbe.RunScan();
        }
        finally
        {
          Interlocked.Exchange(ref _bomPassActive, 0);
        }
      }
      if (AbortRunTickIfOpenDocumentsScope())
        return;

      if (_brokenPassDone && _drawingPassDone && _dxfPassDone && _pdfPassDone
          && passCompleted)
      {
        // Все сканеры завершили полный проход реестра — счётчик сбросится
        // на следующем тике, и будет видно начало нового сканирования с №1.
        lock (Gate)
        {
          _scanPassInProgress = false;
          _brokenPassDone = false;
          _drawingPassDone = false;
          _dxfPassDone = false;
          _pdfPassDone = false;
        }
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
      // Пустой реестр после прерывания (смена scope, открытие документа) нужно
      // обслужить заново: повторно очистить кэш и опубликовать метрики.
      Volatile.Write(ref _emptyRegistryCleaned, false);
      _brokenPassDone = false;
      _drawingPassDone = false;
      _dxfPassDone = false;
      _pdfPassDone = false;

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
      DiscardBomPendingPassUnlocked();

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

    /// <summary>
    /// Завершить discovery-проход BOM-расхождений (обычно в <c>finally</c> сканера):
    /// помечает pending «находки этого прохода» неактивным.
    /// </summary>
    internal static void EndBomDiscoveryPass()
    {
      Interlocked.Exchange(ref _bomPassActive, 0);
    }

    /// <summary>
    /// Сброс находок незавершённого BOM-прохода (прерывание: открылся документ, сменилась
    /// область). Вызывается из <see cref="AbortDiscoveryPassesUnlocked"/>.
    /// </summary>
    private static void DiscardBomPendingPassUnlocked()
    {
      VelumProductRegistryProblemCache.DiscardPendingPass(VelumProductRegistryProblemKind.BomDiff);
      Interlocked.Exchange(ref _bomPassActive, 0);
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
          Volatile.Write(ref _storeLoadedOk, true);
          // Автоимена каталогов — не часть реестра: перечитывать их при каждом
          // изменении items.json не нужно (файл меняется только из формы
          // автоимён — там вызывается NotifyMappingsChanged). Иначе каждое
          // фоновое сохранение зеркал перечитывало оба файла настроек.
          if (_mappings == null || _mappings.Count == 0)
            _mappings = VelumProductRegistryFolderAutoNames.LoadOrCreate();
        }
        catch (Exception ex)
        {
          // Сбой чтения (сеть/таймаут) — не «пустой реестр»: флаг успешной загрузки
          // сбрасываем, чтобы панель не показала «Реестр пустой» при недоступном шаре.
          Volatile.Write(ref _storeLoadedOk, false);
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

    /// <summary>
    /// Гейт доступности корневого каталога реестра. Если корень уже помечен недоступным,
    /// проба выполняется не чаще, чем раз в <see cref="VelumAppConfig.ScannerUnavailableRootPollPulses"/>
    /// пульсов (счётчик <see cref="_unavailableRootPollCountdown"/> декрементируется вызовом).
    /// Возвращает true, если скан запускать нельзя (корень недоступен).
    /// </summary>
    private static bool ShouldSkipScanForUnavailableRoot()
    {
      if (Volatile.Read(ref _registryRootUnavailable) == 1)
      {
        // Корень уже признан недоступным: пробуем снова только по расписанию.
        if (Interlocked.Decrement(ref _unavailableRootPollCountdown) > 0)
          return true;
      }

      if (ProbeRegistryRootAvailable())
        return false;

      // Проба не прошла — помечаем корень недоступным и планируем следующую пробу.
      Volatile.Write(ref _registryRootUnavailable, 1);
      Volatile.Write(ref _unavailableRootPollCountdown, PollPeriod());
      RaiseScanStateChanged();
      return true;
    }

    /// <summary>
    /// Быстрая проба доступности корневого каталога реестра (с таймаутом и кэшем
    /// <see cref="VelumPathExists"/>). Возвращает false и при отсутствии пути, и при таймауте:
    /// для решения «сканировать ли» недостоверный ответ равнозначен недоступности.
    /// </summary>
    private static bool ProbeRegistryRootAvailable()
    {
      try
      {
        // Пробуем доступность КАТАЛОГА, а не «была ли удачная загрузка»: флаг
        // _storeLoadedOk выставляется только внутри RunTick (после EnsureStoreLoaded),
        // но сам RunTick не запускается, пока проба корня не пройдёт — зависимость
        // от него замыкала порочный круг: на первом же тяжёлом пульсе корень
        // помечался недоступным и сканирование не запускалось никогда (панель
        // «Каталог реестра недоступен» даже при живом каталоге).
        string root = VelumProductRegistryStore.RegistryFolderPath;
        if (string.IsNullOrWhiteSpace(root))
          return true; // настройка пуста — путь определит сам стор

        return VelumPathExists.DirectoryExists(root);
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum registry integrity root probe: " + ex.Message);
        return false;
      }
    }

    /// <summary>Период опроса недоступного корня в пульсах (минимум 1).</summary>
    private static int PollPeriod()
    {
      int v = VelumAppConfig.ScannerUnavailableRootPollPulses;
      return v < 1 ? 1 : v;
    }

    /// <summary>
    /// Ранний выход из тика при недоступном корне реестра, если он ещё не помечен:
    /// проверка перед работой сканеров (между пульсом и стартом тика том мог пропасть).
    /// Возвращает true, если скан нужно прервать.
    /// </summary>
    private static bool AbortIfRegistryRootUnavailable()
    {
      if (Volatile.Read(ref _registryRootUnavailable) == 1)
        return true;
      if (ProbeRegistryRootAvailable())
        return false;

      Volatile.Write(ref _registryRootUnavailable, 1);
      Volatile.Write(ref _unavailableRootPollCountdown, PollPeriod());
      RaiseScanStateChanged();
      return true;
    }

    /// <summary>
    /// Успешная проба корня: снимает пометку недоступности и сбрасывает счётчик опроса.
    /// </summary>
    private static void MarkRegistryRootAvailable()
    {
      if (Interlocked.Exchange(ref _registryRootUnavailable, 0) == 1)
      {
        Volatile.Write(ref _unavailableRootPollCountdown, 0);
        RaiseScanStateChanged();
      }
    }
  }
}
