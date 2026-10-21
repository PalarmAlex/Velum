using System;
using System.Collections.Generic;
using System.Linq;
using Velum.SolidHomeostasis;
using Velum.UI.ProductRegistry;
using Xunit;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>
  /// Property-тест конвейера сканирования: случайная (но воспроизводимая по seed)
  /// последовательность тактов сканеров + публикация после каждого такта.
  /// <para>
  /// Инварианты, проверяемые после КАЖДОГО такта:
  /// 1) модель-двойник кэша (Dictionary по kind) совпадает с реальным
  ///    ProblemCache по HasKind/HasAny/Count — семантика Begin/Commit/Discard
  ///    не «уплывает» ни при какой интерливинг-последовательности;
  /// 2) снимок gate после публикации = множество Bad-проб, которое TrySample
  ///    вычисляет из текущего кэша (плашка ⇔ находка, без фантомов);
  /// 3) статус красный (HasAny) ⇔ в снимке есть Bad-проба (E67 на уровне снимка).
  /// </para>
  /// </summary>
  public class VelumMetricPipelinePropertyTests : IDisposable
  {
    private static readonly VelumProductRegistryProblemKind[] ScanKinds =
    {
      VelumProductRegistryProblemKind.BrokenLink,
      VelumProductRegistryProblemKind.MissingDrawing,
      VelumProductRegistryProblemKind.DuplicateDesignation,
      VelumProductRegistryProblemKind.OutdatedDxf,
      VelumProductRegistryProblemKind.OutdatedPdf,
      VelumProductRegistryProblemKind.BomDiff,
    };

    private static readonly string[] AllProbeKeys =
    {
      VelumProductRegistryIntegrityProbes.ProbeKeyHasBrokenLinks,
      VelumProductRegistryIntegrityProbes.ProbeKeyHasMissingDrawings,
      VelumProductRegistryIntegrityProbes.ProbeKeyHasMissingRegistryEntries,
      VelumProductRegistryIntegrityProbes.ProbeKeyHasDxfProblems,
      VelumProductRegistryIntegrityProbes.ProbeKeyHasPdfProblems,
      VelumProductRegistryIntegrityProbes.ProbeKeyHasDuplicateDesignations,
      Velum.UI.AssemblyRegistry.VelumAssemblyBomDiffProbe.ProbeKey,
    };

    /// <summary>kind → ключ плашки, которую он зажигает (маппинг продакшна из TrySample).</summary>
    private static string BrickKeyOf(VelumProductRegistryProblemKind kind)
    {
      switch (kind)
      {
        case VelumProductRegistryProblemKind.BrokenLink:
          return VelumProductRegistryIntegrityProbes.ProbeKeyHasBrokenLinks;
        case VelumProductRegistryProblemKind.MissingDrawing:
          return VelumProductRegistryIntegrityProbes.ProbeKeyHasMissingDrawings;
        case VelumProductRegistryProblemKind.DuplicateDesignation:
          return VelumProductRegistryIntegrityProbes.ProbeKeyHasDuplicateDesignations;
        case VelumProductRegistryProblemKind.NeedDxfExport:
        case VelumProductRegistryProblemKind.OutdatedDxf:
        case VelumProductRegistryProblemKind.MissingDxfProjection:
        case VelumProductRegistryProblemKind.DxfCatalogUnavailable:
        case VelumProductRegistryProblemKind.DxfFirstExport:
        case VelumProductRegistryProblemKind.JunkDxf:
          return VelumProductRegistryIntegrityProbes.ProbeKeyHasDxfProblems;
        case VelumProductRegistryProblemKind.NeedPdfExport:
        case VelumProductRegistryProblemKind.OutdatedPdf:
        case VelumProductRegistryProblemKind.JunkPdf:
          return VelumProductRegistryIntegrityProbes.ProbeKeyHasPdfProblems;
        case VelumProductRegistryProblemKind.BomDiff:
          return Velum.UI.AssemblyRegistry.VelumAssemblyBomDiffProbe.ProbeKey;
        default:
          return null;
      }
    }

    public VelumMetricPipelinePropertyTests()
    {
      ResetPipeline();
    }

    public void Dispose()
    {
      ResetPipeline();
    }

    private static void ResetPipeline()
    {
      VelumProductRegistryProblemCache.Clear();
      VelumSolidEnvironmentGate.Clear();
    }

    /// <summary>Модель: kind → набор itemId в Problems / Pending.</summary>
    private sealed class CacheModel
    {
      public readonly Dictionary<VelumProductRegistryProblemKind, HashSet<int>> Problems =
          new Dictionary<VelumProductRegistryProblemKind, HashSet<int>>();
      public readonly Dictionary<VelumProductRegistryProblemKind, HashSet<int>> Pending =
          new Dictionary<VelumProductRegistryProblemKind, HashSet<int>>();

      public bool HasKind(VelumProductRegistryProblemKind kind) =>
          (Problems.TryGetValue(kind, out HashSet<int> p) && p.Count > 0)
          || (Pending.TryGetValue(kind, out HashSet<int> q) && q.Count > 0);

      public bool HasAny =>
          Problems.Values.Any(s => s.Count > 0) || Pending.Values.Any(s => s.Count > 0);

      public HashSet<string> BadBrickKeys()
      {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (KeyValuePair<VelumProductRegistryProblemKind, HashSet<int>> kv in Problems)
        {
          if (kv.Value.Count == 0)
            continue;
          string b = BrickKeyOf(kv.Key);
          if (b != null)
            keys.Add(b);
        }
        foreach (KeyValuePair<VelumProductRegistryProblemKind, HashSet<int>> kv in Pending)
        {
          if (kv.Value.Count == 0)
            continue;
          string b = BrickKeyOf(kv.Key);
          if (b != null)
            keys.Add(b);
        }
        return keys;
      }

      private static HashSet<int> Bucket(
          Dictionary<VelumProductRegistryProblemKind, HashSet<int>> map,
          VelumProductRegistryProblemKind kind)
      {
        if (!map.TryGetValue(kind, out HashSet<int> set))
        {
          set = new HashSet<int>();
          map[kind] = set;
        }
        return set;
      }

      public void Begin(VelumProductRegistryProblemKind kind) =>
          Pending[kind] = new HashSet<int>();

      public void UpsertPending(VelumProductRegistryProblemKind kind, int itemId) =>
          Bucket(Pending, kind).Add(itemId);

      public void Commit(VelumProductRegistryProblemKind kind)
      {
        Problems[kind] = Pending.TryGetValue(kind, out HashSet<int> q)
            ? new HashSet<int>(q)
            : new HashSet<int>();
        Pending[kind] = new HashSet<int>();
      }

      public void Discard(VelumProductRegistryProblemKind kind) =>
          Pending[kind] = new HashSet<int>();

      public void Upsert(VelumProductRegistryProblemKind kind, int itemId)
      {
        Bucket(Problems, kind).Add(itemId);
        if (Pending.TryGetValue(kind, out HashSet<int> q))
          q.Remove(itemId);
      }

      public void Remove(VelumProductRegistryProblemKind kind, int itemId)
      {
        if (Problems.TryGetValue(kind, out HashSet<int> p))
          p.Remove(itemId);
        if (Pending.TryGetValue(kind, out HashSet<int> q))
          q.Remove(itemId);
      }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(13)]
    [InlineData(42)]
    [InlineData(123)]
    [InlineData(2026)]
    [InlineData(99991)]
    public void RandomScanSequence_SnapshotAlwaysMatchesCache(int seed)
    {
      var rng = new Random(seed);
      var model = new CacheModel();
      int nextItemId = 1;

      for (int tick = 0; tick < 120; tick++)
      {
        // Случайный такт: один или несколько сканеров делают операцию.
        int ops = rng.Next(1, 4);
        for (int o = 0; o < ops; o++)
        {
          VelumProductRegistryProblemKind kind =
              ScanKinds[rng.Next(ScanKinds.Length)];
          int action = rng.Next(100);

          if (action < 20)
          {
            VelumProductRegistryProblemCache.BeginPendingPass(kind);
            model.Begin(kind);
          }
          else if (action < 55)
          {
            int id = nextItemId++;
            var entry = new VelumProductRegistryProblemEntry
            {
              ItemId = id,
              Kind = kind,
              Designation = "ИЗД-" + id,
              FilePath = @"C:\Vault\ITEM" + id + ".sldprt"
            };
            VelumProductRegistryProblemCache.UpsertPending(entry);
            model.UpsertPending(kind, id);
          }
          else if (action < 75)
          {
            VelumProductRegistryProblemCache.CommitPendingPass(kind);
            model.Commit(kind);
          }
          else if (action < 85)
          {
            VelumProductRegistryProblemCache.DiscardPendingPass(kind);
            model.Discard(kind);
          }
          else if (action < 93)
          {
            int id = nextItemId++;
            var entry = new VelumProductRegistryProblemEntry
            {
              ItemId = id,
              Kind = kind,
              Designation = "ИЗД-" + id,
              FilePath = @"C:\Vault\ITEM" + id + ".sldprt"
            };
            VelumProductRegistryProblemCache.Upsert(entry);
            model.Upsert(kind, id);
          }
          else
          {
            // Точечное решение проблемы (перепроверка/рефлекс): Remove.
            int id = rng.Next(1, Math.Max(2, nextItemId));
            VelumProductRegistryProblemCache.Remove(id, kind);
            model.Remove(kind, id);
          }
        }

        // Инвариант 1: реальный кэш == модель.
        foreach (VelumProductRegistryProblemKind kind in ScanKinds)
        {
          Assert.Equal(model.HasKind(kind),
              VelumProductRegistryProblemCache.HasKind(kind));
        }
        Assert.Equal(model.HasAny, VelumProductRegistryProblemCache.HasAny);

        // Такт завершается публикацией (как PublishScoresToGate в конце RunTick).
        VelumProductRegistryIntegrityProbes.PublishScoresToGate(registryOnlySnapshot: true);

        // Инвариант 2: Bad-пробы снимка == Bad-пробы, которые считает TrySample.
        var expectedBad = new HashSet<string>(StringComparer.Ordinal);
        foreach (string key in AllProbeKeys)
        {
          if (VelumProductRegistryIntegrityProbes.TrySample(key, out float v, out _) && v < 99f)
            expectedBad.Add(key);
        }

        IReadOnlyDictionary<string, float> snap =
            VelumSolidEnvironmentGate.GetPublishedSnapshot();
        Assert.NotNull(snap);
        var actualBad = new HashSet<string>(StringComparer.Ordinal);
        foreach (string key in AllProbeKeys)
        {
          if (snap.TryGetValue(key, out float v) && v < 99f)
            actualBad.Add(key);
        }
        Assert.Equal(expectedBad, actualBad);

        // Инвариант 3: Bad-пробы снимка == плашки из модели (нет фантомов и потерь).
        Assert.Equal(model.BadBrickKeys(), actualBad);

        // Инвариант 4 (E67 на уровне снимка): статус красный ⇔ есть Bad-плашка.
        Assert.Equal(model.HasAny, actualBad.Count > 0);
      }
    }

    [Theory]
    [InlineData(5)]
    [InlineData(77)]
    public void AbortDuringPass_NoPhantomBricksAfterCorrectRecovery(int seed)
    {
      // Сканер начал проход и был прерван (открыли документ): продакшн делает
      // DiscardPendingPass по всем kind + Clear при смене области. После такой
      // корректной очистки и публикации ни одна плашка не должна гореть
      // «вчерашним» набором, если кэш очищен полностью.
      var rng = new Random(seed);
      for (int pre = 0; pre < 10; pre++)
      {
        VelumProductRegistryProblemKind kind = ScanKinds[rng.Next(ScanKinds.Length)];
        var entry = new VelumProductRegistryProblemEntry
        {
          ItemId = pre + 1,
          Kind = kind,
          FilePath = @"C:\Vault\ITEM" + pre + ".sldprt"
        };
        if (rng.Next(2) == 0)
          VelumProductRegistryProblemCache.UpsertPending(entry);
        else
          VelumProductRegistryProblemCache.Upsert(entry);
        VelumProductRegistryProblemCache.BeginPendingPass(kind);
        VelumProductRegistryProblemCache.UpsertPending(entry);
      }
      VelumProductRegistryIntegrityProbes.PublishScoresToGate(registryOnlySnapshot: true);

      // AbortDiscoveryPassesUnlocked + ClearAllProblemCacheUnlocked (переход open).
      foreach (VelumProductRegistryProblemKind kind in ScanKinds)
        VelumProductRegistryProblemCache.DiscardPendingPass(kind);
      VelumProductRegistryProblemCache.Clear();
      VelumProductRegistryIntegrityProbes.PublishScoresToGate(registryOnlySnapshot: true);

      IReadOnlyDictionary<string, float> snap =
          VelumSolidEnvironmentGate.GetPublishedSnapshot();
      Assert.False(VelumProductRegistryProblemCache.HasAny);
      foreach (string key in AllProbeKeys)
      {
        Assert.True(snap.TryGetValue(key, out float v));
        Assert.Equal(VelumProductRegistryIntegrityProbes.GoodScore, v);
      }
    }
  }
}
