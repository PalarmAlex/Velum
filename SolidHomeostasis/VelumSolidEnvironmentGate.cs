using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Доля «успешных» подпризнаков метрики среды для масштабирования величины из справочника воздействий
  /// (<c>YesSlots / TotalSlots</c>). При <c>TotalSlots == 1</c> — бинарная метрика (полная величина из справочника).
  /// </summary>
  public readonly struct VelumSolidProbeInfluenceContext
  {
    /// <summary>Число подпризнаков в состоянии «да» (ОК).</summary>
    public int YesSlots { get; }

    /// <summary>Число подпризнаков в расчёте (≥ 1).</summary>
    public int TotalSlots { get; }

    /// <summary>
    /// Задаёт долю «да» для масштабирования величины из справочника воздействий (<see cref="VelumSolidEnvironmentInfluenceComposer"/>).
    /// </summary>
    /// <param name="yesSlots">Число успешных подпризнаков (неотрицательное; при превышении <paramref name="totalSlots"/> обрезается).</param>
    /// <param name="totalSlots">Число слотов (не меньше 1).</param>
    public VelumSolidProbeInfluenceContext(int yesSlots, int totalSlots)
    {
      YesSlots = yesSlots < 0 ? 0 : yesSlots;
      TotalSlots = totalSlots < 1 ? 1 : totalSlots;
      if (YesSlots > TotalSlots)
        YesSlots = TotalSlots;
    }

    /// <summary>Один слот «да»: бинарная метрика без дробного масштаба (1/1).</summary>
    public static VelumSolidProbeInfluenceContext BinaryNeutral => new VelumSolidProbeInfluenceContext(1, 1);

    /// <summary>true, если слотов несколько — применяется масштаб <c>YesSlots/TotalSlots</c> и порог после масштаба.</summary>
    public bool IsComposite => TotalSlots > 1;
  }

  /// <summary>
  /// Последний снимок значений проб метрик среды SW (строковый ключ → 0…100) и контексты масштаба воздействия.
  /// Публикуется целиком; на пульсе ISIDA забирается композером среды.
  /// </summary>
  public static class VelumSolidEnvironmentGate
  {
    private static readonly object Gate = new object();
    private static IReadOnlyDictionary<string, float> _published;
    private static IReadOnlyDictionary<string, VelumSolidProbeInfluenceContext> _publishedContexts;

    private static readonly IReadOnlyDictionary<string, VelumSolidProbeInfluenceContext> EmptyContexts =
        new ReadOnlyDictionary<string, VelumSolidProbeInfluenceContext>(
            new Dictionary<string, VelumSolidProbeInfluenceContext>(0, StringComparer.Ordinal));

    private static bool _lastSnapshotTimedOut;
    private static bool _lastSnapshotIsStale;
    private static long _lastProbeElapsedMs;
    private static VelumSolidProbeErrorKind _lastErrorKind;
    private static int _lastSuccessPulse;

    /// <summary>true, если последняя публикация снимка — после таймаута COM-опроса на пульсе (п. 1 плана устойчивости).</summary>
    public static bool LastSnapshotTimedOut
    {
      get
      {
        lock (Gate)
        {
          return _lastSnapshotTimedOut;
        }
      }
    }

    /// <summary>true, если опубликован устаревший снимок без нового COM (таймаут + кэш).</summary>
    public static bool LastSnapshotIsStale
    {
      get
      {
        lock (Gate)
        {
          return _lastSnapshotIsStale;
        }
      }
    }

    /// <summary>Длительность последнего опроса или попытки опроса SW на пульсе, мс.</summary>
    public static long LastProbeElapsedMs
    {
      get
      {
        lock (Gate)
        {
          return _lastProbeElapsedMs;
        }
      }
    }

    /// <summary>Классификация последнего такта опроса (для SessionHealth и композера).</summary>
    public static VelumSolidProbeErrorKind LastErrorKind
    {
      get
      {
        lock (Gate)
        {
          return _lastErrorKind;
        }
      }
    }

    /// <summary>Номер пульса последнего успешного опроса SW в бюджете (0 — ещё не было).</summary>
    public static int LastSuccessPulse
    {
      get
      {
        lock (Gate)
        {
          return _lastSuccessPulse;
        }
      }
    }

    /// <summary>true, если снимок проб нельзя использовать для импульсов InfluenceActions (таймаут, устаревание, сбой COM).</summary>
    public static bool SnapshotUntrustworthy
    {
      get
      {
        lock (Gate)
        {
          if (_lastSnapshotTimedOut || _lastSnapshotIsStale)
            return true;
          return _lastErrorKind != VelumSolidProbeErrorKind.None;
        }
      }
    }

    /// <summary>Зафиксировать исход такта опроса для SessionHealth.</summary>
    /// <param name="kind">Вид ошибки; <see cref="VelumSolidProbeErrorKind.None"/> при успехе.</param>
    /// <param name="pulseNumber">Номер пульса ISIDA.</param>
    public static void SetLastProbeOutcome(VelumSolidProbeErrorKind kind, int pulseNumber)
    {
      lock (Gate)
      {
        _lastErrorKind = kind;
        if (kind == VelumSolidProbeErrorKind.None && pulseNumber > 0)
          _lastSuccessPulse = pulseNumber;
      }
    }

    /// <summary>Метаданные последнего такта опроса/публикации снимка (для п. 2 SessionHealth).</summary>
    /// <param name="timedOut">Опрос не уложился в бюджет времени.</param>
    /// <param name="elapsedMs">Фактическое время ожидания UI/COM.</param>
    /// <param name="isStale">Снимок взят из кэша без нового COM.</param>
    public static void SetLastProbeMetadata(bool timedOut, long elapsedMs, bool isStale)
    {
      lock (Gate)
      {
        _lastSnapshotTimedOut = timedOut;
        _lastProbeElapsedMs = elapsedMs < 0 ? 0 : elapsedMs;
        _lastSnapshotIsStale = isStale;
      }
    }

    /// <summary>Заменяет опубликованный снимок копией словарей (потокобезопасно).</summary>
    /// <param name="snapshot">Значения проб 0…100.</param>
    /// <param name="influenceContexts">Масштаб по ключу пробы; null — для всех ключей используется бинарный контекст 1/1.</param>
    public static void Publish(
        IReadOnlyDictionary<string, float> snapshot,
        IReadOnlyDictionary<string, VelumSolidProbeInfluenceContext> influenceContexts = null)
    {
      if (snapshot == null || snapshot.Count == 0)
        return;
      var copy = new Dictionary<string, float>(StringComparer.Ordinal);
      foreach (KeyValuePair<string, float> kv in snapshot)
        copy[kv.Key] = kv.Value;

      IReadOnlyDictionary<string, VelumSolidProbeInfluenceContext> ctxCopy = null;
      if (influenceContexts != null && influenceContexts.Count > 0)
      {
        var c = new Dictionary<string, VelumSolidProbeInfluenceContext>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, VelumSolidProbeInfluenceContext> kv in influenceContexts)
          c[kv.Key] = kv.Value;
        ctxCopy = new ReadOnlyDictionary<string, VelumSolidProbeInfluenceContext>(c);
      }

      lock (Gate)
      {
        _published = copy;
        _publishedContexts = ctxCopy;
      }
    }

    /// <summary>Текущий снимок или null, если ещё ни разу не публиковали.</summary>
    public static IReadOnlyDictionary<string, float> GetPublishedSnapshot()
    {
      lock (Gate)
      {
        if (_published == null || _published.Count == 0)
          System.Diagnostics.Debug.WriteLine("[Velum.Gate] GetPublishedSnapshot EMPTY");
        else
          System.Diagnostics.Debug.WriteLine("[Velum.Gate] GetPublishedSnapshot keys=" + string.Join(",", _published.Keys));
        return _published;
      }
    }

    /// <summary>Контексты масштаба воздействия по ключу пробы; пустой словарь, если не публиковали или не передавали.</summary>
    public static IReadOnlyDictionary<string, VelumSolidProbeInfluenceContext> GetPublishedInfluenceContexts()
    {
      lock (Gate)
      {
        return _publishedContexts ?? EmptyContexts;
      }
    }

    /// <summary>
    /// Пометить опубликованный снимок устаревшим, но оставить значения для давления до следующего COM-опроса.
    /// </summary>
    internal static void MarkSnapshotStale()
    {
      lock (Gate)
      {
        _lastSnapshotIsStale = true;
      }
    }

    /// <summary>
    /// Пометить опубликованный снимок свежим (после публикации host-global значений без COM-опроса,
    /// например registry-only снимка при отсутствии активного документа).
    /// </summary>
    internal static void MarkSnapshotFresh()
    {
      lock (Gate)
      {
        _lastSnapshotIsStale = false;
        _lastSnapshotTimedOut = false;
      }
    }

    /// <summary>
    /// Очистить document-specific пробы из снимка (вызывается при отсутствии активного документа).
    /// Оставляет только host-global пробы (реестр, BOM и т.п.).
    /// </summary>
    internal static void ClearDocumentSpecificProbes()
    {
      lock (Gate)
      {
        if (_published == null || _published.Count == 0)
          return;

        var keep = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (var kv in _published)
        {
          // Оставляем только host-global ключи (Velum.Host.*, Velum.Registry.*, Velum.Assembly.*).
          // Document-specific ключи (Velum.Solid.Dxf.*, Velum.Solid.Pdf.*) удаляем.
          if (kv.Key.StartsWith("Velum.Host.", StringComparison.Ordinal) ||
              kv.Key.StartsWith("Velum.Registry.", StringComparison.Ordinal) ||
              kv.Key.StartsWith("Velum.Assembly.", StringComparison.Ordinal))
          {
            keep[kv.Key] = kv.Value;
          }
        }

        if (keep.Count != _published.Count)
        {
          _published = new ReadOnlyDictionary<string, float>(
              new Dictionary<string, float>(keep, StringComparer.Ordinal));
        }
      }
    }

    /// <summary>Сброс при выгрузке надстройки (подписка на пульс снимется отдельно).</summary>
    public static void Clear()
    {
      lock (Gate)
      {
        _published = null;
        _publishedContexts = null;
        _lastSnapshotTimedOut = false;
        _lastSnapshotIsStale = false;
        _lastProbeElapsedMs = 0;
        _lastErrorKind = VelumSolidProbeErrorKind.None;
        _lastSuccessPulse = 0;
      }
    }
  }
}
