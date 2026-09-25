using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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
  internal static class VelumPathExists
  {
    /// <summary>Таймаут одной проверки, мс.</summary>
    internal const int DefaultTimeoutMs = 1500;

    /// <summary>Время жизни положительного результата в кэше, мс.</summary>
    private const int OkCacheTtlMs = 15000;
    /// <summary>Время жизни отрицательного/неизвестного результата в кэше, мс (короче — битые ссылки чинятся).</summary>
    private const int NoCacheTtlMs = 5000;

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
    }

    /// <summary>
    /// Кэш результатов: key = признак_каталога + нормализованный путь.
    /// Положительные живут дольше, отрицательные/Unknown — короче (битые ссылки чинятся).
    /// </summary>
    private static readonly Dictionary<string, CacheEntry> _cache =
        new Dictionary<string, CacheEntry>();

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
      string trimmed = (path ?? string.Empty).Trim();
      if (trimmed.Length == 0)
        return Result.No;

      string key = CacheKey(isDirectory, trimmed);
      long now = DateTime.UtcNow.Ticks;

      lock (_cache)
      {
        if (_cache.TryGetValue(key, out CacheEntry cached))
        {
          if (cached.DeadlineTicks > now)
            return cached.Result;

          // Просрочено — перепроверяем ниже, запись перезапишем.
          _cache.Remove(key);
        }
      }

      Result result = Probe(isDirectory, trimmed, timeoutMs);

      // Кэшируем, но не заполняем кэш до отказа: Unknown (таймаут) полезно помнить,
      // т.к. повторный Probe только снова потратит таймаут.
      long ttl = result == Result.Yes ? OkCacheTtlMs : NoCacheTtlMs;
      lock (_cache)
      {
        _cache[key] = new CacheEntry
        {
          IsDirectory = isDirectory,
          Result = result,
          DeadlineTicks = now + TimeSpan.FromMilliseconds(ttl).Ticks
        };
      }

      return result;
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

    /// <summary>Сбрасывает весь кэш (например, при смене корня реестра документов).</summary>
    internal static void InvalidateAll()
    {
      lock (_cache)
      {
        _cache.Clear();
      }
    }
  }
}