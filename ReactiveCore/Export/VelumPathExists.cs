using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Velum.Configuration;

namespace Velum.ReactiveCore.Export
{
  /// <summary>
  /// Безопасная проверка существования файла/каталога с таймаутом.
  /// <see cref="System.IO.File.Exists"/> / <see cref="System.IO.Directory.Exists"/> на Windows
  /// могут виснуть десятки секунд не только на недоступных сетевых шарах, но и на битых
  /// симлинках/junction на отключённый том, на несуществующих буквах дисков и на относительных
  /// путях, развёрнутых от незаданного корня реестра. Чтобы не блокировать UI/фоновый поток,
  /// проверка всегда выполняется в пуле потоков с коротким таймаутом, а результаты (в том числе
  /// отрицательные и «неизвестно») короткоживуще кэшируются — одни и те же битые ссылки не
  /// перепроверяются каждым сканером/метрикой.
  /// </summary>
  /// <remarks>
  /// Для массовых обходов (сканеры целостности реестра) предназначен
  /// <see cref="CheckBatch"/>: одиночная проверка по сетевой шаре стоит один сетевой
  /// раундтрип, и последовательный обход тысяч путей превращает время раундтрипа
  /// в минуты полного прохода. Пакет выполняет проверки параллельно, ограничивая
  /// число одновременно висящих обращений к ФС.
  /// </remarks>
  internal static class VelumPathExists
  {
    /// <summary>Таймаут одной проверки, мс.</summary>
    internal const int DefaultTimeoutMs = 1500;

    /// <summary>Время жизни положительного результата в кэше, мс.</summary>
    private const int OkCacheTtlMs = 15000;
    /// <summary>Время жизни отрицательного результата в кэше, мс (короче — битые ссылки чинятся).</summary>
    private const int NoCacheTtlMs = 5000;
    /// <summary>Время жизни положительного результата в кэше для сетевых путей, мс.</summary>
    /// <summary>Проверка на UNC-шаре стоит сетевой раундтрип, поэтому кэшируется дольше локального.</summary>
    private const int NetworkOkCacheTtlMs = 60000;
    /// <summary>Время жизни отрицательного результата в кэше для сетевых путей, мс.</summary>
    private const int NetworkNoCacheTtlMs = 20000;

    /// <summary>
    /// Базовая длительность кэша «неизвестно» (таймаут проверки), мс. Короче прочих:
    /// пока шара снова стала доступной, сканер должен это заметить, а не молчать.
    /// </summary>
    private const int UnknownBaseTtlMs = 3000;
    /// <summary>
    /// Потолок длительности кэша «неизвестно»: при повторных таймаутах одного пути
    /// (недоступная шара, отключённый том) TTL растёт экспоненциально, но не выше
    /// этого значения, чтобы вернувшийся ресурс был замечен в разумный срок.
    /// </summary>
    private const int UnknownMaxTtlMs = 120000;
    /// <summary>Максимальный множитель экспоненциального роста длительности «неизвестно».</summary>
    private const int UnknownBackoffMaxFactor = 64;

    /// <summary>
    /// Порог «недоступности» сетевого корня: число таймаутов подряд на одном UNC-префиксе,
    /// после которого все проверки путей внутри корня на <see cref="NetworkRootRecoveryMs"/>
    /// мгновенно возвращают <see cref="Result.Unknown"/>, не обращаясь к ФС. Недоступный
    /// корень (выключенный сервер, оборванный VPN) иначе тратил бы весь таймаут на каждый
    /// путь прохода — батч из сотен записей растягивался бы на минуты без единого ответа.
    /// </summary>
    private const int NetworkRootTimeoutThreshold = 8;

    /// <summary>Период восстановления «недоступного» сетевого корня, мс.</summary>
    private const int NetworkRootRecoveryMs = 30000;

    /// <summary>Состояние circuit-breaker одного сетевого корня (UNC-префикса).</summary>
    private sealed class RootState
    {
      /// <summary>Число подряд идущих таймаутов на этом корне.</summary>
      public int ConsecutiveTimeouts;

      /// <summary>Момент (ticks), до которого корень считается «недоступным»; 0 — доступен.</summary>
      public long UnavailableUntilTicks;
    }

    /// <summary>Состояния circuit-breaker по сетевым корням: key — нормализованный UNC-префикс.</summary>
    private static readonly Dictionary<string, RootState> _networkRoots =
        new Dictionary<string, RootState>();

    /// <summary>Исход проверки существования пути.</summary>
    internal enum Result
    {
      /// <summary>Путь существует (файл/каталог найден).</summary>
      Yes,
      /// <summary>Путь пуст, отсутствует или недоступен (явный «нет»).</summary>
      No,
      /// <summary>Проверка не уложилась в таймаут — считаем недоступным без блокировки.</summary>
      Unknown
    }

    private sealed class CacheEntry
    {
      public bool IsDirectory;
      public Result Result;
      public long DeadlineTicks;

      /// <summary>
      /// Множитель экспоненциального роста длительности кэша «неизвестно»:
      /// повторный таймаут того же пути удваивает его (см. <see cref="Remember"/>).
      /// Для Yes/No всегда 1.
      /// </summary>
      public int RetryFactor;
    }

    /// <summary>
    /// Кэш результатов: key = признак_каталога + нормализованный путь.
    /// Положительные живут дольше, отрицательные/Unknown — короче (битые ссылки чинятся).
    /// </summary>
    private static readonly Dictionary<string, CacheEntry> _cache =
        new Dictionary<string, CacheEntry>();

    /// <summary>
    /// Thread-static кэш текущего кванта прохода: сканер один раз проверяет пути батча,
    /// запоминает их в scope, а классификация (FileExists/DirectoryExists/Check) читает
    /// их отсюда, не обращаясь к ФС повторно. Живёт только в рамках батча.
    /// </summary>
    [ThreadStatic]
    private static Dictionary<string, Result> _scope;

    /// <summary>
    /// Открывает thread-static scope кванта. Все результаты, записанные через
    /// <see cref="ScopeResult"/>, читаются <see cref="Check"/> без обращения к ФС.
    /// Возвращаемый <see cref="IDisposable"/> при dispose сбрасывает scope текущего потока.
    /// </summary>
    internal static IDisposable EnterScope()
    {
      _scope = new Dictionary<string, Result>();
      return new ScopeGuard();
    }

    /// <summary>Запоминает результат пути в thread-static scope текущего кванта.</summary>
    internal static void ScopeResult(bool isDirectory, string path, Result result)
    {
      if (_scope == null || string.IsNullOrEmpty(path))
        return;
      _scope[CacheKey(isDirectory, path)] = result;
    }

    /// <summary>Читает результат пути из thread-static scope текущего кванта.</summary>
    private static bool TryGetScoped(bool isDirectory, string path, out Result result)
    {
      result = Result.No;
      if (_scope == null)
        return false;
      return _scope.TryGetValue(CacheKey(isDirectory, path), out result);
    }

    /// <summary>Dispose-заглушка, сбрасывающая thread-static scope текущего потока.</summary>
    private sealed class ScopeGuard : IDisposable
    {
      public void Dispose()
      {
        _scope = null;
      }
    }

    /// <summary>Ключ кэша: префикс каталога + путь (без нормализации, чтобы не тянуть IO).</summary>
    private static string CacheKey(bool isDirectory, string path)
    {
      return (isDirectory ? "D:" : "F:") + path;
    }

    /// <summary>Проверка существования файла. true — существует; иначе false (в т.ч. таймаут).</summary>
    internal static bool FileExists(string path, int timeoutMs = DefaultTimeoutMs)
    {
      return Check(false, path, timeoutMs) == Result.Yes;
    }

    /// <summary>Проверка существования каталога. true — существует; иначе false (в т.ч. таймаут).</summary>
    internal static bool DirectoryExists(string path, int timeoutMs = DefaultTimeoutMs)
    {
      return Check(true, path, timeoutMs) == Result.Yes;
    }

    /// <summary>Проверка существования пути с трёхзначным результатом.</summary>
    internal static Result Check(bool isDirectory, string path, int timeoutMs = DefaultTimeoutMs)
    {
      string trimmed = Normalize(path);
      if (trimmed.Length == 0)
        return Result.No;

      Result scoped;
      if (TryGetScoped(isDirectory, trimmed, out scoped))
        return scoped;

      Result cached;
      if (TryGetCached(isDirectory, trimmed, out cached))
        return cached;

      // Если родительский каталог файла уже известен как отсутствующий, файл
      // гарантированно отсутствует: проверять его на (возможно, сетевой) шаре
      // незачем. Несуществующий каталог модели/чертежа не порождает ни одного
      // лишнего раундтрипа по каждой записи реестра внутри него.
      if (!isDirectory && ParentKnownMissing(trimmed))
        return Result.No;

      // Сетевой корень помечен «недоступным» после N таймаутов подряд: не ходим на
      // ФС и возвращаем Unknown сразу — иначе недоступный корень тратил бы весь
      // таймаут на каждый путь прохода без единого ответа.
      if (IsNetworkRootUnavailable(trimmed, DateTime.UtcNow.Ticks))
        return Result.Unknown;

      Result result = Probe(isDirectory, trimmed, timeoutMs);
      Remember(isDirectory, trimmed, result);
      return result;
    }

    /// <summary>
    /// true, если родительский каталог файла уже известен как отсутствующий
    /// (из scope текущего кванта или неистёкшего кэша). Тогда файл не существует.
    /// </summary>
    private static bool ParentKnownMissing(string path)
    {
      string parent = ParentDirectory(path);
      if (parent.Length == 0)
        return false;

      Result scoped;
      if (TryGetScoped(true, parent, out scoped))
        return scoped == Result.No;

      Result cached;
      return TryGetCached(true, parent, out cached) && cached == Result.No;
    }

    /// <summary>
    /// Вариант для пакетной проверки: дополнительно к scope/кэшу учитывает словарь
    /// уже известных результатов прохода (<paramref name="known"/>), чтобы не ходить
    /// на шару за файлами несуществующего каталога, чей «нет» уже получен.
    /// </summary>
    private static bool ParentKnownMissing(
        string path,
        IDictionary<string, Result> known,
        long now)
    {
      string parent = ParentDirectory(path);
      if (parent.Length == 0)
        return false;

      Result r;
      if (known != null && known.TryGetValue(parent, out r) && r == Result.No)
        return true;

      return TryGetCached(true, parent, out r, now) && r == Result.No;
    }

    /// <summary>
    /// Выделяет родительский каталог пути (всё до последнего разделителя).
    /// Пустая строка — у пути нет каталога (голое имя файла или корень), оптимизацию
    /// не применяем.
    /// </summary>
    private static string ParentDirectory(string path)
    {
      int lastSep = Math.Max(
          string.IsNullOrEmpty(path) ? -1 : path.LastIndexOf('\\'),
          string.IsNullOrEmpty(path) ? -1 : path.LastIndexOf('/'));
      if (lastSep <= 0)
        return string.Empty;
      return path.Substring(0, lastSep);
    }


    /// <summary>
    /// Максимум одновременно висящих проверок existence. Обращение к недоступной шаре
    /// держит поток до таймаута, поэтому без ограничения пакет из сотен путей исчерпал
    /// бы пул потоков и затормозил остальные фоновые работы.
    /// </summary>
    private static int MaxConcurrentProbes
    {
      get
      {
        int v = 16;
        try
        {
          v = VelumAppConfig.ScannerProbeConcurrency;
        }
        catch (Exception)
        {
          // Config may be unavailable on early start - stay on default.
        }
        return v < 1 ? 1 : v;
      }
    }

    /// <summary>
    /// Верхняя граница ожидания всего пакета: даже при полностью недоступной шаре
    /// сканер не должен висеть дольше, иначе пульс UI не успеет его прервать.
    /// </summary>
    private const int MaxBatchWaitMs = 30000;

    private static readonly SemaphoreSlim ProbeSlots = new SemaphoreSlim(MaxConcurrentProbes, MaxConcurrentProbes);

    /// <summary>
    /// Пакетная проверка путей: кэш читается последовательно (он в памяти), реальные
    /// обращения к ФС выполняются параллельно в пуле потоков. Полная проверка пакета
    /// стоит времени самой медленной проверки, а не суммы всех - на сетевом корне это
    /// разница между минутами и секундами полного прохода реестра.
    /// </summary>
    /// <param name="requests">Пути для проверки (повторы внутри пакета допустимы).</param>
    /// <param name="timeoutMs">Таймаут одной проверки, мс.</param>
    /// <param name="known">
    /// Уже известные результаты текущего прохода (обычно проверки файлов моделей);
    /// используются вместо повторного обращения к ФС. Может быть <c>null</c>.
    /// </param>
    /// <returns>
    /// Результаты в том же порядке, что и <paramref name="requests"/>; пустой массив при пустом входе.
    /// </returns>
    internal static Result[] CheckBatch(
        IList<PathCheckRequest> requests,
        int timeoutMs = DefaultTimeoutMs,
        IDictionary<string, Result> known = null)
    {
      if (requests == null || requests.Count == 0)
        return new Result[0];

      int n = requests.Count;
      Result[] results = new Result[n];
      var pending = new List<int>();
      long now = DateTime.UtcNow.Ticks;

      for (int i = 0; i < n; i++)
      {
        PathCheckRequest req = requests[i];
        req.Path = Normalize(req.Path);
        requests[i] = req;

        if (req.Path.Length == 0)
        {
          results[i] = Result.No;
          continue;
        }

        Result knownResult;
        if (known != null && known.TryGetValue(req.Path, out knownResult))
        {
          results[i] = knownResult;
          continue;
        }

        Result cached;
        if (TryGetCached(req.IsDirectory, req.Path, out cached, now))
        {
          results[i] = cached;
          continue;
        }

        // Родительский каталог файла уже известен как отсутствующий — файл внутри
        // него не существует, и не стоит расходовать слот семафора на обращение к
        // (сетевой) шаре за каждой записью несуществующего каталога.
        if (!req.IsDirectory && ParentKnownMissing(req.Path, known, now))
        {
          results[i] = Result.No;
          continue;
        }

        // Сетевой корень помечен «недоступным» (N таймаутов подряд): путь не
        // включаем в пакет — проверка всё равно дала бы Unknown, но заняла бы
        // слот семафора и время ожидания пакета.
        if (IsNetworkRootUnavailable(req.Path, now))
        {
          results[i] = Result.Unknown;
          continue;
        }

        pending.Add(i);
      }

      if (pending.Count > 0)
        ProbeBatch(requests, pending, results, timeoutMs);

      return results;
    }

    /// <summary>Выполняет параллельную проверку индексов, не найденных в кэше.</summary>
    private static void ProbeBatch(
        IList<PathCheckRequest> requests,
        List<int> pending,
        Result[] results,
        int timeoutMs)
    {
      // Все проверки стартуют сразу: у каждой свой токен, а слот удерживается только
      // на время реальной проверки (семафор), поэтому задержка одной недоступной шары
      // не переносится на остальные пути пакета.
      var running = new Task<Result>[pending.Count];
      for (int i = 0; i < pending.Count; i++)
        running[i] = ProbeAsync(requests[pending[i]], timeoutMs);

      // Ждём все, а не первую: частичный ответ по пакету опаснее, чем Unknown по
      // неотвеченным путям - последний не порождает ложный BrokenLink.
      try
      {
        Task.WhenAll(running).Wait(AllWaitTimeoutMs(pending.Count, timeoutMs));
      }
      catch (AggregateException)
      {
        // Отдельные проверки уже сведены к Result.Unknown внутри ProbeAsync.
      }

      for (int i = 0; i < pending.Count; i++)
      {
        int index = pending[i];
        Result result = Result.Unknown;
        Task<Result> task = running[i];
        if (task.IsCompleted && !task.IsFaulted && !task.IsCanceled)
          result = task.Result;

        results[index] = result;

        string path = requests[index].Path;
        if (path.Length > 0)
          Remember(requests[index].IsDirectory, path, result);
      }
    }

    /// <summary>
    /// Общий таймаут ожидания пакета: время одной проверки плюс запас на очередь к
    /// семафору при батче больше числа слотов, с ограничением сверху.
    /// </summary>
    private static int AllWaitTimeoutMs(int probeCount, int timeoutMs)
    {
      if (timeoutMs <= 0)
        return DefaultTimeoutMs;

      long budget = timeoutMs + Math.Max(timeoutMs, 1000L);
      int waves = (probeCount + MaxConcurrentProbes - 1) / MaxConcurrentProbes;
      budget += (long)timeoutMs * (waves - 1) / 2;

      return budget > MaxBatchWaitMs ? MaxBatchWaitMs : (int)budget;
    }

    /// <summary>
    /// Асинхронная проверка existence: слот семафора держится только на время проверки,
    /// любая ошибка или переполнение пула сводятся к неизвестному результату, чтобы
    /// вызывающий код не получил ложного «файл не найден».
    /// </summary>
    private static async Task<Result> ProbeAsync(PathCheckRequest request, int timeoutMs)
    {
      bool slotTaken = false;
      try
      {
        slotTaken = await ProbeSlots.WaitAsync(timeoutMs).ConfigureAwait(false);
        if (!slotTaken)
          return Result.Unknown;

        bool exists = await Task.Run(() =>
        {
          try
          {
            return request.IsDirectory
                ? System.IO.Directory.Exists(request.Path)
                : System.IO.File.Exists(request.Path);
          }
          catch
          {
            return false;
          }
        }).ConfigureAwait(false);

        return exists ? Result.Yes : Result.No;
      }
      catch
      {
        return Result.Unknown;
      }
      finally
      {
        if (slotTaken)
          ProbeSlots.Release();
      }
    }

    /// <summary>Читает несохранённый результат из кэша (текущее время).</summary>
    private static bool TryGetCached(bool isDirectory, string path, out Result result)
    {
      return TryGetCached(isDirectory, path, out result, DateTime.UtcNow.Ticks);
    }

    /// <summary>
    /// Читает несохранённый результат из кэша. Положительные живут дольше,
    /// отрицательные и неизвестные - короче (битые ссылки чинятся).
    /// </summary>
    private static bool TryGetCached(bool isDirectory, string path, out Result result, long now)
    {
      result = Result.No;
      string key = CacheKey(isDirectory, path);
      lock (_cache)
      {
        CacheEntry cached;
        if (!_cache.TryGetValue(key, out cached))
          return false;

        if (cached.DeadlineTicks <= now)
        {
          _cache.Remove(key);
          return false;
        }

        result = cached.Result;
        return true;
      }
    }

    /// <summary>
    /// Сохраняет результат в кэш, включая неизвестный: повторная проверка недоступной
    /// шары снова потратила бы весь таймаут.
    /// </summary>
    private static void Remember(bool isDirectory, string path, Result result)
    {
      if (string.IsNullOrEmpty(path))
        return;

      string key = CacheKey(isDirectory, path);
      long now = DateTime.UtcNow.Ticks;

      lock (_cache)
      {
        int retryFactor = 1;
        long ttlMs;
        // Сетевой путь (автоопределение UNC-корня) кэшируется дольше: каждая проверка на шаре стоит сетевой раундтрип, и повторный опрос того же файла в одном проходе бесполезен.
        bool isNetwork = NetworkRoot(path).Length > 0;
        switch (result)
        {
          case Result.Yes:
            ttlMs = isNetwork ? NetworkOkCacheTtlMs : OkCacheTtlMs;
            break;
          case Result.Unknown:
            // Повторный таймаут того же пути (недоступная шара) удваивает длительность
            // кэша: каждый следующий раз мы тратим весь таймаут, поэтому перепроверять
            // безнадёжную шару каждые 3с бессмысленно. Yes/No сбрасывают множитель.
            CacheEntry prior;
            if (_cache.TryGetValue(key, out prior) && prior.Result == Result.Unknown)
              retryFactor = Math.Min(prior.RetryFactor * 2, UnknownBackoffMaxFactor);
            ttlMs = Math.Min((long)UnknownBaseTtlMs * retryFactor, (long)UnknownMaxTtlMs);
            break;
          default:
            ttlMs = isNetwork ? NetworkNoCacheTtlMs : NoCacheTtlMs;
            break;
        }

        long ttlTicks = TimeSpan.FromMilliseconds(ttlMs).Ticks;
        _cache[key] = new CacheEntry
        {
          IsDirectory = isDirectory,
          Result = result,
          DeadlineTicks = now + ttlTicks,
          RetryFactor = retryFactor
        };
      }

      // Реальный ответ ФС (Remember вызывается только после Probe) влияет на
      // circuit-breaker сетевого корня: таймаут копит счётчик, явный ответ сбрасывает.
      RecordNetworkRootResult(path, result, now);
    }

    /// <summary>
    /// true, если сетевой корень пути помечен «недоступным»: после N таймаутов подряд
    /// на одном UNC-префиксе проверки внутри него мгновенно возвращают Unknown, пока
    /// не истечёт <see cref="NetworkRootRecoveryMs"/>. По истечении состояние сбрасывается,
    /// чтобы вернувшийся корень снова проверялся на ФС.
    /// </summary>
    private static bool IsNetworkRootUnavailable(string path, long now)
    {
      string root = NetworkRoot(path);
      if (root.Length == 0)
        return false;

      lock (_networkRoots)
      {
        RootState state;
        if (!_networkRoots.TryGetValue(root, out state))
          return false;

        if (state.UnavailableUntilTicks <= now)
        {
          _networkRoots.Remove(root);
          return false;
        }

        return true;
      }
    }

    /// <summary>
    /// Учитывает реальный ответ проверки в circuit-breaker корня: таймаут (Unknown)
    /// увеличивает счётчик подряд идущих таймаутов и при достижении порога помечает
    /// корень недоступным; явный ответ (Yes/No) сбрасывает счётчик и снимает пометку.
    /// </summary>
    private static void RecordNetworkRootResult(string path, Result result, long now)
    {
      string root = NetworkRoot(path);
      if (root.Length == 0)
        return;

      lock (_networkRoots)
      {
        RootState state;
        if (!_networkRoots.TryGetValue(root, out state))
        {
          state = new RootState();
          _networkRoots[root] = state;
        }

        if (result == Result.Unknown)
        {
          state.ConsecutiveTimeouts++;
          if (state.ConsecutiveTimeouts >= NetworkRootTimeoutThreshold)
            state.UnavailableUntilTicks =
                now + TimeSpan.FromMilliseconds(NetworkRootRecoveryMs).Ticks;
        }
        else
        {
          state.ConsecutiveTimeouts = 0;
          state.UnavailableUntilTicks = 0;
        }
      }
    }

    /// <summary>
    /// Нормализованный UNC-префикс (\\server\share) пути или пустая строка, если путь
    /// не сетевой. Сетевые пути опознаются по началу <c>\\server\share\...</c> (включая
    /// расширенную форму <c>\\?\UNC\server\share\...</c>); буквы диска — не сетевой корень.
    /// </summary>
    private static string NetworkRoot(string path)
    {
      if (string.IsNullOrEmpty(path))
        return string.Empty;

      string p = path;
      if (p.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
        p = @"\\" + p.Substring(8);
      else if (p.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
        return string.Empty;

      if (!p.StartsWith(@"\\", StringComparison.Ordinal))
        return string.Empty;

      // p = \\server\share\rest...
      string tail = p.Substring(2);
      int firstSep = tail.IndexOf('\\');
      if (firstSep < 0)
        return string.Empty;
      string server = tail.Substring(0, firstSep);
      if (server.Length == 0)
        return string.Empty;

      string afterServer = tail.Substring(firstSep + 1);
      int secondSep = afterServer.IndexOf('\\');
      string share = secondSep < 0 ? afterServer : afterServer.Substring(0, secondSep);
      if (share.Length == 0)
        return string.Empty;

      return ("\\\\" + server + "\\" + share).ToLowerInvariant();
    }

    private static string Normalize(string path)
    {
      return (path ?? string.Empty).Trim();
    }


    /// <summary>
    /// Один запрос пакетной проверки: признак каталога вместо файла и сам путь.
    /// </summary>
    internal struct PathCheckRequest
    {
      /// <summary>Проверяемый путь.</summary>
      internal string Path;

      /// <summary>true - проверяется каталог, false - файл.</summary>
      internal bool IsDirectory;

      /// <summary>Создаёт запрос пакетной проверки.</summary>
      internal PathCheckRequest(bool isDirectory, string path)
      {
        IsDirectory = isDirectory;
        Path = path;
      }
    }

    /// <summary>Выполняет реальную проверку в пуле потоков с таймаутом (без кэша).</summary>
    private static Result Probe(bool isDirectory, string path, int timeoutMs)
    {
      try
      {
        Task<bool> task = Task.Run(() =>
        {
          try
          {
            return isDirectory
                ? System.IO.Directory.Exists(path)
                : System.IO.File.Exists(path);
          }
          catch
          {
            return false;
          }
        });

        if (!task.Wait(timeoutMs))
          return Result.Unknown;

        return task.Result ? Result.Yes : Result.No;
      }
      catch
      {
        return Result.No;
      }
    }

    /// <summary>Сбрасывает весь кэш и состояние circuit-breaker (например, при смене корня реестра документов).</summary>
    internal static void InvalidateAll()
    {
      lock (_cache)
      {
        _cache.Clear();
      }

      lock (_networkRoots)
      {
        _networkRoots.Clear();
      }
    }
  }
}