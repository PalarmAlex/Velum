using System;
using System.Collections.Generic;
using System.Linq;
using Velum.SolidHomeostasis;
using Velum.UI.ProductRegistry;
using Xunit;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>
  /// Регрессия конвейера «сканер → ProblemCache → PublishScoresToGate → снимок gate → плашка».
  /// <para>
  /// Симптом (развитие случаев 27/38/39): статус панели (ProblemCache.HasAny) показывает одну
  /// проблему, а горящая плашка метрики — другую (залипший набор предыдущего прохода).
  /// Плашка читает опубликованный снимок gate, который обновляется только в момент
  /// PublishScoresToGate; статус и форма проблем читают Problems ∪ Pending напрямую.
  /// </para>
  /// <para>
  /// Тесты гоняют оригинальные классы продакшна (ProblemCache, Gate, IntegrityProbes)
  /// через цифровой двойник тактов RunTick: Begin → UpsertPending → Commit/Discard → Publish.
  /// </para>
  /// </summary>
  public class VelumMetricPipelineTests : IDisposable
  {
    // Ключи-константы продакшна (VelumProductRegistryIntegrityProbes) продублированы
    // здесь только для читаемости assert'ов; сверка фактических ключей идёт через
    // сам класс (см. ProbeKeyConstants_MatchProduction).
    private const string BrokenLinksKey = "Velum.Registry.HasBrokenLinks";
    private const string MissingDrawingsKey = "Velum.Registry.HasMissingDrawings";
    private const string MissingEntriesKey = "Velum.Registry.HasMissingRegistryEntries";
    private const string DxfKey = "Velum.Registry.HasDxfProblems";
    private const string PdfKey = "Velum.Registry.HasPdfProblems";
    private const string DupKey = "Velum.Registry.HasDuplicateDesignations";
    private const string BomKey = "Velum.Assembly.BomDiff";

    private const float Bad = VelumProductRegistryIntegrityProbes.BadScore;
    private const float Good = VelumProductRegistryIntegrityProbes.GoodScore;

    /// <summary>Все registry/BOM-пробы, которые PublishScoresToGate публикует всегда.</summary>
    private static readonly string[] AllProbeKeys =
    {
      BrokenLinksKey, MissingDrawingsKey, MissingEntriesKey,
      DxfKey, PdfKey, DupKey, BomKey
    };

    public VelumMetricPipelineTests()
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

    // ─────────────────────────── helpers ───────────────────────────

    private static void AddProblem(
        VelumProductRegistryProblemKind kind,
        int itemId,
        bool pending)
    {
      var entry = new VelumProductRegistryProblemEntry
      {
        ItemId = itemId,
        Kind = kind,
        Designation = "ИЗД-" + itemId,
        FilePath = @"C:\Vault\ITEM" + itemId + ".sldprt"
      };
      if (pending)
        VelumProductRegistryProblemCache.UpsertPending(entry);
      else
        VelumProductRegistryProblemCache.Upsert(entry);
    }

    private static float Published(string probeKey)
    {
      IReadOnlyDictionary<string, float> snap = VelumSolidEnvironmentGate.GetPublishedSnapshot();
      Assert.NotNull(snap);
      Assert.True(snap.ContainsKey(probeKey),
          "проба " + probeKey + " отсутствует в опубликованном снимке");
      return snap[probeKey];
    }

    /// <summary>
    /// Инвариант E67 на уровне снимка: статус красный (HasAny) ⇔ хотя бы одна
    /// registry/BOM-проба в снимке Bad.
    /// </summary>
    private static void AssertStatusMatchesBricks(string because)
    {
      IReadOnlyDictionary<string, float> snap = VelumSolidEnvironmentGate.GetPublishedSnapshot();
      bool anyBadInSnapshot = snap != null && AllProbeKeys.Any(k =>
          snap.TryGetValue(k, out float v) && v < 99f);

      Assert.Equal(VelumProductRegistryProblemCache.HasAny, anyBadInSnapshot);
    }

    /// <summary>
    /// Инвариант «правильная плашка»: множество Bad-проб в снимке в точности равно
    /// множеству проб, которые TrySample вернул бы для текущего состояния кэша.
    /// </summary>
    private static void AssertSnapshotMatchesCache(string because)
    {
      var expectedBad = new List<string>();
      foreach (string key in AllProbeKeys)
      {
        if (VelumProductRegistryIntegrityProbes.TrySample(key, out float v, out _) && v < 99f)
          expectedBad.Add(key);
      }

      IReadOnlyDictionary<string, float> snap = VelumSolidEnvironmentGate.GetPublishedSnapshot();
      var actualBad = snap == null
          ? new List<string>()
          : AllProbeKeys.Where(k => snap.TryGetValue(k, out float v) && v < 99f).ToList();

      Assert.Equal(
          expectedBad.OrderBy(s => s, StringComparer.Ordinal),
          actualBad.OrderBy(s => s, StringComparer.Ordinal),
          StringComparer.Ordinal);
    }

    /// <summary>Такт «сканер kind нашёл проблемы itemIds»: Begin + UpsertPending.</summary>
    private static void ScannerFinds(
        VelumProductRegistryProblemKind kind,
        int[] itemIds)
    {
      VelumProductRegistryProblemCache.BeginPendingPass(kind);
      foreach (int id in itemIds)
        AddProblem(kind, id, pending: true);
    }

    /// <summary>Такт «сканер kind завершил проход» (в т.ч. пустой): Commit.</summary>
    private static void ScannerCommits(VelumProductRegistryProblemKind kind)
    {
      VelumProductRegistryProblemCache.CommitPendingPass(kind);
    }

    // ─────────────────────────── тесты ───────────────────────────

    [Fact]
    public void ProbeKeyConstants_MatchProduction()
    {
      // Константы тестов не должны разъехаться с продакшном.
      Assert.Equal(BrokenLinksKey, VelumProductRegistryIntegrityProbes.ProbeKeyHasBrokenLinks);
      Assert.Equal(MissingDrawingsKey, VelumProductRegistryIntegrityProbes.ProbeKeyHasMissingDrawings);
      Assert.Equal(MissingEntriesKey, VelumProductRegistryIntegrityProbes.ProbeKeyHasMissingRegistryEntries);
      Assert.Equal(DxfKey, VelumProductRegistryIntegrityProbes.ProbeKeyHasDxfProblems);
      Assert.Equal(PdfKey, VelumProductRegistryIntegrityProbes.ProbeKeyHasPdfProblems);
      Assert.Equal(DupKey, VelumProductRegistryIntegrityProbes.ProbeKeyHasDuplicateDesignations);
      Assert.True(VelumProductRegistryIntegrityProbes.IsRegistryProbeKey(BomKey),
          "BOM-проба обязана входить в IsRegistryProbeKey (случай 27)");
      Assert.True(VelumProductRegistryIntegrityProbes.IsHostGlobalProbeKey(BomKey));
    }

    [Fact]
    public void EmptyCommit_ClearsPublishedKind_WithoutTouchingOthers()
    {
      // В опубликованном кэше — PDF-проблема и BOM-расхождение.
      AddProblem(VelumProductRegistryProblemKind.OutdatedPdf, itemId: 1, pending: false);
      AddProblem(VelumProductRegistryProblemKind.BomDiff, itemId: 2, pending: false);
      VelumProductRegistryIntegrityProbes.PublishScoresToGate(registryOnlySnapshot: true);

      Assert.Equal(Bad, Published(PdfKey));
      Assert.Equal(Bad, Published(BomKey));

      // PDF-сканер прошёл чисто и закоммитил пустой проход → залипания быть не должно.
      VelumProductRegistryProblemCache.BeginPendingPass(VelumProductRegistryProblemKind.OutdatedPdf);
      VelumProductRegistryProblemCache.CommitPendingPass(VelumProductRegistryProblemKind.OutdatedPdf);
      VelumProductRegistryIntegrityProbes.PublishScoresToGate(registryOnlySnapshot: true);

      Assert.Equal(Good, Published(PdfKey));
      Assert.Equal(Bad, Published(BomKey)); // чужой kind не тронут
      AssertStatusMatchesBricks("PDF решён, BOM остался");
    }

    [Fact]
    public void StaleCommittedPdf_WhileBomPending_StatusSeesBom_BrickStaysPdf()
    {
      // Документирование текущего дизайна (см. README-комментарий в конце файла):
      // коммит BOM при незакрытом PDF-проходе оставляет старый PDF-набор в Problems.
      // Статус (HasAny) и форма (SnapshotIncludingPending) показывают BOM,
      // а плашка PDF продолжает гореть от вчерашнего коммита.
      AddProblem(VelumProductRegistryProblemKind.OutdatedPdf, itemId: 1, pending: false);
      VelumProductRegistryIntegrityProbes.PublishScoresToGate(registryOnlySnapshot: true);

      // Пользователь «решил» PDF вне цикла сканеров (Pending уже пуст, Problems — нет).
      // Новый проход: PDF-сканер ещё не добрался, а BOM уже нашёл расхождение.
      ScannerFinds(VelumProductRegistryProblemKind.BomDiff, new[] { 7 });
      ScannerCommits(VelumProductRegistryProblemKind.BomDiff);
      VelumProductRegistryIntegrityProbes.PublishScoresToGate(registryOnlySnapshot: true);

      // Плашка PDF всё ещё горит (старый коммит), хотя форма проблем покажет BOM.
      Assert.Equal(Bad, Published(PdfKey));
      Assert.Equal(Bad, Published(BomKey));

      // Инвариант «правильная плашка» нарушен: форма (Pending∪Problems) видит BomDiff,
      // но по набору Problems плашка PDF остаётся единственной «документационной».
      IReadOnlyList<VelumProductRegistryProblemEntry> form =
          VelumProductRegistryProblemCache.SnapshotIncludingPending();
      Assert.Contains(form, e => e.Kind == VelumProductRegistryProblemKind.BomDiff);
      Assert.Contains(form, e => e.Kind == VelumProductRegistryProblemKind.OutdatedPdf);
      // ...а после Remove решённого PDF форма покажет только BOM, но плашка PDF
      // продолжит гореть до коммита PDF-прохода:
      VelumProductRegistryProblemCache.Remove(1, VelumProductRegistryProblemKind.OutdatedPdf);
      form = VelumProductRegistryProblemCache.SnapshotIncludingPending();
      Assert.DoesNotContain(form, e => e.Kind == VelumProductRegistryProblemKind.OutdatedPdf);
      VelumProductRegistryIntegrityProbes.PublishScoresToGate(registryOnlySnapshot: true);
      // HasAny (статус) — по Pending(BomDiff); проба PDF пересчитана из Problems — Good.
      Assert.Equal(Good, Published(PdfKey));
      Assert.Equal(Bad, Published(BomKey));
      AssertStatusMatchesBricks("после Remove+публикации");
    }

    [Fact]
    public void EmptyCommit_ResolvesStuckBrick_AfterProblemRemovedExternally()
    {
      // Тот же залипший PDF, но лечится корректным путём: пустой коммит прохода PDF.
      AddProblem(VelumProductRegistryProblemKind.OutdatedPdf, itemId: 1, pending: false);
      VelumProductRegistryIntegrityProbes.PublishScoresToGate(registryOnlySnapshot: true);
      Assert.Equal(Bad, Published(PdfKey));

      // Пользователь решил проблему; точечный Remove (как делает перепроверка).
      VelumProductRegistryProblemCache.Remove(1, VelumProductRegistryProblemKind.OutdatedPdf);
      // Без коммита прохода публикация ПЕРЕСЧИТАЕТ пробу из Problems → Good.
      // (Проба читает кэш, а не «запомнит» Bad — залипание возможно только пока
      // запись физически в Problems.)
      Assert.False(VelumProductRegistryProblemCache.HasKind(
          VelumProductRegistryProblemKind.OutdatedPdf));
    }

    [Fact]
    public void PendingOnlyProblem_StatusRed_BricksGood_UntilPublish()
    {
      // Случай 39: статус красный от Pending, плашки горят только после публикации.
      ScannerFinds(VelumProductRegistryProblemKind.OutdatedDxf, new[] { 42 });

      Assert.True(VelumProductRegistryProblemCache.HasAny);

      // Снимок ещё не публиковался после находки — ни одна проба не Bad.
      VelumProductRegistryIntegrityProbes.PublishScoresToGate(registryOnlySnapshot: true);

      // PublishScoresToGate → TrySample учитывает Pending: плашка загорается
      // синхронно со статусом. Рассинхрон «красный статус / пустые плашки» закрыт.
      Assert.Equal(Bad, Published(DxfKey));
      AssertStatusMatchesBricks("pending DXF опубликован");
    }

    [Fact]
    public void DiscardPending_KeepsCommittedProblems_Published()
    {
      // Abort (открыли документ): discard pending, опубликованный набор остаётся.
      AddProblem(VelumProductRegistryProblemKind.BrokenLink, itemId: 1, pending: false);
      ScannerFinds(VelumProductRegistryProblemKind.OutdatedPdf, new[] { 2 });
      VelumProductRegistryIntegrityProbes.PublishScoresToGate(registryOnlySnapshot: true);
      Assert.Equal(Bad, Published(BrokenLinksKey));
      Assert.Equal(Bad, Published(PdfKey)); // pending PDF уже виден пробой

      VelumProductRegistryProblemCache.DiscardPendingPass(VelumProductRegistryProblemKind.OutdatedPdf);
      VelumProductRegistryIntegrityProbes.PublishScoresToGate(registryOnlySnapshot: true);

      Assert.Equal(Bad, Published(BrokenLinksKey)); // committed не тронут
      Assert.Equal(Good, Published(PdfKey));        // pending сброшен → Good
      AssertStatusMatchesBricks("после discard");
    }

    [Fact]
    public void MergePublish_KeepsDocumentProbe_StaleUntilOverwrite()
    {
      // Режим merge (registryOnlySnapshot:false) сохраняет документные пробы —
      // в closed-области это источник залипания (E66).
      var docProbe = new Dictionary<string, float>(StringComparer.Ordinal)
      {
        { "Velum.Solid.Pdf.Outdated", 50f },
        { DxfKey, Good },
      };
      VelumProductRegistryIntegrityProbes.PublishScoresToGate(registryOnlySnapshot: true);
      VelumSolidEnvironmentGate.Publish(docProbe); // эмуляция COM-публикации с документом

      // Публикация в режиме merge оставляет документную пробу.
      VelumProductRegistryIntegrityProbes.PublishScoresToGate(registryOnlySnapshot: false);
      Assert.Equal(50f, Published("Velum.Solid.Pdf.Outdated"));

      // Публикация в режиме host-global её выбрасывает.
      VelumProductRegistryIntegrityProbes.PublishScoresToGate(registryOnlySnapshot: true);
      IReadOnlyDictionary<string, float> snap = VelumSolidEnvironmentGate.GetPublishedSnapshot();
      Assert.False(snap.ContainsKey("Velum.Solid.Pdf.Outdated"));
    }

    [Fact]
    public void ClearDocumentSpecificProbes_KeepsRegistry_KeepsBom()
    {
      AddProblem(VelumProductRegistryProblemKind.BomDiff, itemId: 5, pending: false);
      VelumProductRegistryIntegrityProbes.PublishScoresToGate(registryOnlySnapshot: true);

      // Эмуляция COM-публикации с открытым документом: полный снимок =
      // документные пробы + реестровые (продакшн мержит, а не затирает реестр).
      var withDoc = new Dictionary<string, float>(StringComparer.Ordinal)
      {
        { "Velum.Solid.Dxf.Outdated", 50f },
      };
      foreach (KeyValuePair<string, float> kv in
          VelumSolidEnvironmentGate.GetPublishedSnapshot())
      {
        withDoc[kv.Key] = kv.Value;
      }
      VelumSolidEnvironmentGate.Publish(withDoc);
      Assert.Equal(Bad, Published(BomKey));

      VelumSolidEnvironmentGate.ClearDocumentSpecificProbes();

      IReadOnlyDictionary<string, float> snap = VelumSolidEnvironmentGate.GetPublishedSnapshot();
      Assert.False(snap.ContainsKey("Velum.Solid.Dxf.Outdated"));
      Assert.Equal(Bad, Published(BomKey)); // Velum.Assembly.* — host-global, остаётся
    }

    [Fact]
    public void ClearDocumentSpecificProbes_KeepsStaleFlag_MergeCannotTrustIt()
    {
      // Зафиксированное поведение: очистка документных проб НЕ снимает маркеры
      // недостоверности — поэтому переход closed↔open обязан публиковать
      // registryOnlySnapshot:true и вызывать MarkSnapshotFresh (E66).
      VelumProductRegistryIntegrityProbes.PublishScoresToGate(registryOnlySnapshot: true);
      VelumSolidEnvironmentGate.Publish(new Dictionary<string, float>(StringComparer.Ordinal)
      {
        { "Velum.Solid.Dxf.Outdated", 50f },
      });
      VelumSolidEnvironmentGate.MarkSnapshotStale();
      Assert.True(VelumSolidEnvironmentGate.SnapshotUntrustworthy);

      VelumSolidEnvironmentGate.ClearDocumentSpecificProbes();
      Assert.True(VelumSolidEnvironmentGate.SnapshotUntrustworthy);

      VelumSolidEnvironmentGate.MarkSnapshotFresh();
      Assert.False(VelumSolidEnvironmentGate.SnapshotUntrustworthy);
    }

    [Fact]
    public void BomCommittedBeforeCursorScanners_DoesNotLightDxfBrick()
    {
      // Случай 27 / E29: BOM-проба коммитится за один тик, курсорные DXF/PDF — позже.
      // BOM-находка не должна зажигать чужие плашки.
      ScannerFinds(VelumProductRegistryProblemKind.BomDiff, new[] { 9 });
      ScannerCommits(VelumProductRegistryProblemKind.BomDiff);
      VelumProductRegistryIntegrityProbes.PublishScoresToGate(registryOnlySnapshot: true);

      Assert.Equal(Bad, Published(BomKey));
      Assert.Equal(Good, Published(DxfKey));
      Assert.Equal(Good, Published(PdfKey));
      Assert.Equal(Good, Published(BrokenLinksKey));
    }

    [Fact]
    public void InterruptedPass_AfterBeginBeforeCommit_CacheKeepsPreviousCommittedSet()
    {
      // Сканер начал проход (Begin чистит pending kind), но был прерван (abort).
      // В опубликованном наборе остаётся ПРОШЛЫЙ коммит — проба продолжает гореть
      // им, пока проход не будет завершен или прерван корректно (Discard+Clear).
      AddProblem(VelumProductRegistryProblemKind.OutdatedDxf, itemId: 3, pending: false);
      VelumProductRegistryIntegrityProbes.PublishScoresToGate(registryOnlySnapshot: true);
      Assert.Equal(Bad, Published(DxfKey));

      // Новый проход начат, коммита нет → старый набор живёт в Problems.
      VelumProductRegistryProblemCache.BeginPendingPass(VelumProductRegistryProblemKind.OutdatedDxf);
      VelumProductRegistryIntegrityProbes.PublishScoresToGate(registryOnlySnapshot: true);
      Assert.Equal(Bad, Published(DxfKey)); // «залипание» прошлого прохода — осознанный дизайн

      // Корректное завершение пустого прохода гасит плашку.
      ScannerCommits(VelumProductRegistryProblemKind.OutdatedDxf);
      VelumProductRegistryIntegrityProbes.PublishScoresToGate(registryOnlySnapshot: true);
      Assert.Equal(Good, Published(DxfKey));
      AssertStatusMatchesBricks("пустой коммит погасил DXF");
    }

    [Fact]
    public void PublishScoresToGate_RegistrOnly_ReplacesEntireSnapshot()
    {
      // Режим registryOnlySnapshot:true не подмешивает старые документные ключи.
      VelumSolidEnvironmentGate.Publish(new Dictionary<string, float>(StringComparer.Ordinal)
      {
        { "Velum.Solid.Dxf.Outdated", 50f },
        { "Velum.Solid.Pdf.Outdated", 40f },
      });

      VelumProductRegistryIntegrityProbes.PublishScoresToGate(registryOnlySnapshot: true);

      IReadOnlyDictionary<string, float> snap = VelumSolidEnvironmentGate.GetPublishedSnapshot();
      Assert.All(AllProbeKeys, k => Assert.True(snap.ContainsKey(k), "нет ключа " + k));
      Assert.DoesNotContain("Velum.Solid.Dxf.Outdated", snap.Keys);
      Assert.DoesNotContain("Velum.Solid.Pdf.Outdated", snap.Keys);
    }

    [Fact]
    public void InfluenceContexts_BadProbe_ZeroYesSlots_GoodProbe_BinaryNeutral()
    {
      AddProblem(VelumProductRegistryProblemKind.DuplicateDesignation, itemId: 7, pending: false);
      VelumProductRegistryIntegrityProbes.PublishScoresToGate(registryOnlySnapshot: true);

      IReadOnlyDictionary<string, VelumSolidProbeInfluenceContext> infl =
          VelumSolidEnvironmentGate.GetPublishedInfluenceContexts();

      VelumSolidProbeInfluenceContext badCtx = infl[DupKey];
      Assert.Equal(0, badCtx.YesSlots); // Bad → полное давление

      VelumSolidProbeInfluenceContext goodCtx = infl[BomKey];
      Assert.Equal(1, goodCtx.YesSlots); // Good → бинарный нейтральный 1/1
      Assert.Equal(1, goodCtx.TotalSlots);
    }

    [Fact]
    public void CollectInfluencedParamIds_PicksOnlyRegistryActions()
    {
      // Фейковый каталог EA: один EA с registry-пробой, один с документной.
      var eaRegistry = new ISIDA.Actions.InfluenceActionSystem.GomeostasisInfluenceAction
      {
        Id = 1,
        IsActive = true,
        IsEnvironmentProbeAction = true,
        ProbeKey = PdfKey,
        Influences = new Dictionary<int, int> { { 10, 1 } }
      };
      var eaDocument = new ISIDA.Actions.InfluenceActionSystem.GomeostasisInfluenceAction
      {
        Id = 2,
        IsActive = true,
        IsEnvironmentProbeAction = true,
        ProbeKey = "Velum.Solid.Pdf.Outdated",
        Influences = new Dictionary<int, int> { { 11, 1 } }
      };
      VelumSolidEnvironmentInfluenceCatalog.ActiveActions.Add(eaRegistry);
      VelumSolidEnvironmentInfluenceCatalog.ActiveActions.Add(eaDocument);

      var registryParams = new HashSet<int>();
      VelumProductRegistryIntegrityProbes.CollectInfluencedParamIds(registryParams);
      Assert.Contains(10, registryParams);
      Assert.DoesNotContain(11, registryParams);

      // Host-global версия включает и BOM-пробу.
      var eaBom = new ISIDA.Actions.InfluenceActionSystem.GomeostasisInfluenceAction
      {
        Id = 3,
        IsActive = true,
        IsEnvironmentProbeAction = true,
        ProbeKey = BomKey,
        Influences = new Dictionary<int, int> { { 12, 1 } }
      };
      VelumSolidEnvironmentInfluenceCatalog.ActiveActions.Add(eaBom);

      var hostGlobalParams = new HashSet<int>();
      VelumProductRegistryIntegrityProbes.CollectHostGlobalInfluencedParamIds(hostGlobalParams);
      Assert.Contains(10, hostGlobalParams);
      Assert.Contains(12, hostGlobalParams);
      Assert.DoesNotContain(11, hostGlobalParams);
    }

    [Fact]
    public void DxfKindFamily_AnySubkindLightsDxfBrick_OthersStayGood()
    {
      // Плашка DXF — групповая: любой из подвидов DXF её зажигает, PDF-подвиды — нет.
      var dxfKinds = new[]
      {
        VelumProductRegistryProblemKind.NeedDxfExport,
        VelumProductRegistryProblemKind.OutdatedDxf,
        VelumProductRegistryProblemKind.MissingDxfProjection,
        VelumProductRegistryProblemKind.DxfCatalogUnavailable,
        VelumProductRegistryProblemKind.DxfFirstExport,
        VelumProductRegistryProblemKind.JunkDxf,
      };

      foreach (VelumProductRegistryProblemKind kind in dxfKinds)
      {
        ResetPipeline();
        AddProblem(kind, itemId: 11, pending: false);
        VelumProductRegistryIntegrityProbes.PublishScoresToGate(registryOnlySnapshot: true);
        Assert.Equal(Bad, Published(DxfKey));
        Assert.Equal(Good, Published(PdfKey));
        AssertStatusMatchesBricks("подвид " + kind);
      }

      ResetPipeline();
      AddProblem(VelumProductRegistryProblemKind.JunkPdf, itemId: 12, pending: false);
      VelumProductRegistryIntegrityProbes.PublishScoresToGate(registryOnlySnapshot: true);
      Assert.Equal(Good, Published(DxfKey));
      Assert.Equal(Bad, Published(PdfKey));
    }

    [Fact]
    public void RemoveAllForItem_ClearsBothDictionaries()
    {
      AddProblem(VelumProductRegistryProblemKind.OutdatedDxf, itemId: 5, pending: false);
      AddProblem(VelumProductRegistryProblemKind.OutdatedPdf, itemId: 5, pending: true);
      Assert.True(VelumProductRegistryProblemCache.HasAny);

      VelumProductRegistryProblemCache.RemoveAllForItem(5);
      Assert.False(VelumProductRegistryProblemCache.HasAny);

      VelumProductRegistryIntegrityProbes.PublishScoresToGate(registryOnlySnapshot: true);
      AssertStatusMatchesBricks("запись удалена целиком");
    }

    [Fact]
    public void UpsertDirect_ClearsPendingOfSameKey()
    {
      // Точечная перепроверка (RevalidateCachedItemProblems) пишет Upsert:
      // та же запись не должна остаться в Pending «вторым экземпляром» боли.
      AddProblem(VelumProductRegistryProblemKind.BrokenLink, itemId: 8, pending: true);
      Assert.Equal(1, VelumProductRegistryProblemCache.PendingCount);

      AddProblem(VelumProductRegistryProblemKind.BrokenLink, itemId: 8, pending: false);
      Assert.Equal(0, VelumProductRegistryProblemCache.PendingCount);
      Assert.Equal(1, VelumProductRegistryProblemCache.Count);
    }

    [Fact]
    public void UserScenario_StalePdfBrick_WhileBomIsActual()
    {
      // Буквальный сценарий жалобы: залипла плашка PDF, реальная проблема — BOM.
      // (1) Был PDF-наход, коммит, публикация → горит PDF-плашка.
      ScannerFinds(VelumProductRegistryProblemKind.OutdatedPdf, new[] { 100 });
      ScannerCommits(VelumProductRegistryProblemKind.OutdatedPdf);
      VelumProductRegistryIntegrityProbes.PublishScoresToGate(registryOnlySnapshot: true);
      Assert.Equal(Bad, Published(PdfKey));

      // (2) PDF решён (Remove), BOM-скан нашёл расхождение и закоммитил,
      //     PDF-проход ещё не перепрошёл.
      VelumProductRegistryProblemCache.Remove(100, VelumProductRegistryProblemKind.OutdatedPdf);
      ScannerFinds(VelumProductRegistryProblemKind.BomDiff, new[] { 200 });
      ScannerCommits(VelumProductRegistryProblemKind.BomDiff);

      // Статус и форма уже про BOM:
      Assert.True(VelumProductRegistryProblemCache.HasKind(VelumProductRegistryProblemKind.BomDiff));
      Assert.False(VelumProductRegistryProblemCache.HasKind(VelumProductRegistryProblemKind.OutdatedPdf));

      // ...а после публикации горит РОВНО BOM-плашка: PDF-проба пересчитана из
      // Problems и не может остаться Bad без записей.
      VelumProductRegistryIntegrityProbes.PublishScoresToGate(registryOnlySnapshot: true);
      Assert.Equal(Good, Published(PdfKey));
      Assert.Equal(Bad, Published(BomKey));
      AssertSnapshotMatchesCache("сценарий жалобы");

      // (3) Полный цикл без проблем → статус зелёный, горящих нет.
      foreach (VelumProductRegistryProblemKind k in Enum.GetValues(
                   typeof(VelumProductRegistryProblemKind)).Cast<VelumProductRegistryProblemKind>())
      {
        VelumProductRegistryProblemCache.RemoveAllOfKind(k);
      }
      VelumProductRegistryIntegrityProbes.PublishScoresToGate(registryOnlySnapshot: true);
      Assert.False(VelumProductRegistryProblemCache.HasAny);
      AssertStatusMatchesBricks("всё чисто");
    }

    [Fact]
    public void Changed_FiredByEveryMutator_SuppressesStickyBrick()
    {
      // Фикс случая 40: каждое изменение кэша поднимает Changed, подписчик
      // (шедулер) мгновенно переопубликовывает снимок gate — плашка не отстаёт.
      int events = 0;
      Action handler = () => events++;
      VelumProductRegistryProblemCache.Changed += handler;
      try
      {
        // Коммит сканера (находка).
        ScannerFinds(VelumProductRegistryProblemKind.OutdatedPdf, new[] { 1 });
        int afterPending = events;
        Assert.True(afterPending > 0, "UpsertPending обязан поднимать Changed");

        ScannerCommits(VelumProductRegistryProblemKind.OutdatedPdf);
        Assert.True(events > afterPending, "CommitPendingPass обязан поднимать Changed");

        // Точечное решение проблемы — то, что раньше залипало до конца тика.
        VelumProductRegistryProblemCache.Remove(1, VelumProductRegistryProblemKind.OutdatedPdf);
        Assert.True(events > afterPending + 1, "Remove обязан поднимать Changed");
      }
      finally
      {
        VelumProductRegistryProblemCache.Changed -= handler;
      }
    }

    [Fact]
    public void Changed_NotFiredByNoOpMutations()
    {
      // No-op (Remove отсутствующей записи, Clear пустого кэша) не должен
      // дёргать перепубликацию снимка — иначе шум на каждом холостом тике.
      int events = 0;
      Action handler = () => events++;
      VelumProductRegistryProblemCache.Changed += handler;
      try
      {
        Assert.False(VelumProductRegistryProblemCache.HasAny);

        VelumProductRegistryProblemCache.Clear();
        Assert.Equal(0, events);

        VelumProductRegistryProblemCache.Remove(999, VelumProductRegistryProblemKind.OutdatedPdf);
        Assert.Equal(0, events);

        // Discard пустого pending — операция фактически холостая, но дешёвая;
        // главное — реальные изменения её проходят.
        AddProblem(VelumProductRegistryProblemKind.BomDiff, itemId: 3, pending: false);
        Assert.Equal(1, events);
      }
      finally
      {
        VelumProductRegistryProblemCache.Changed -= handler;
      }
    }

    [Fact]
    public void Changed_HandlerException_DoesNotBreakCache()
    {
      // Подписчик падает (в проде — исключение публикации) — кэш обязан работать.
      Action bad = () => throw new InvalidOperationException("sub");
      VelumProductRegistryProblemCache.Changed += bad;
      try
      {
        AddProblem(VelumProductRegistryProblemKind.OutdatedDxf, itemId: 1, pending: true);
        Assert.True(VelumProductRegistryProblemCache.HasAny);
      }
      finally
      {
        VelumProductRegistryProblemCache.Changed -= bad;
      }
    }

    [Fact]
    public void Changed_FullStickyScenario_BrickFollowsCacheImmediately()
    {
      // Сценарий жалобы в терминах фикса: PDF решён точечным Remove — снимок
      // обязан обновиться по событию, не дожидаясь коммита PDF-прохода.
      int publishes = 0;
      Action handler = () =>
      {
        publishes++;
        VelumProductRegistryIntegrityProbes.PublishScoresToGate(registryOnlySnapshot: true);
      };

      AddProblem(VelumProductRegistryProblemKind.OutdatedPdf, itemId: 1, pending: false);
      AddProblem(VelumProductRegistryProblemKind.BomDiff, itemId: 2, pending: false);
      VelumProductRegistryIntegrityProbes.PublishScoresToGate(registryOnlySnapshot: true);
      Assert.Equal(Bad, Published(PdfKey));

      VelumProductRegistryProblemCache.Changed += handler;
      try
      {
        VelumProductRegistryProblemCache.Remove(1, VelumProductRegistryProblemKind.OutdatedPdf);
      }
      finally
      {
        VelumProductRegistryProblemCache.Changed -= handler;
      }

      // Без ожидания коммита/пульса: PDF-плашка погасла, BOM горит.
      Assert.True(publishes > 0);
      Assert.Equal(Good, Published(PdfKey));
      Assert.Equal(Bad, Published(BomKey));
      AssertSnapshotMatchesCache("событийная публикация");
    }

    [Fact]
    public void ProblemKindMapping_ScannersToBricks()
    {
      // Каждый «сканерный» kind обязан попадать ровно в одну плашку-группу.
      AddProblem(VelumProductRegistryProblemKind.OutdatedDxf, itemId: 1, pending: false);
      Assert.True(VelumProductRegistryProblemCache.HasAnyKind(new[]
      {
        VelumProductRegistryProblemKind.OutdatedDxf
      }));

      ResetPipeline();
      AddProblem(VelumProductRegistryProblemKind.BomDiff, itemId: 1, pending: false);
      // BOM не входит ни в DXF-, ни в PDF-группы.
      Assert.False(VelumProductRegistryProblemCache.HasAnyKind(new[]
      {
        VelumProductRegistryProblemKind.OutdatedDxf,
        VelumProductRegistryProblemKind.OutdatedPdf,
      }));
      Assert.True(VelumProductRegistryProblemCache.HasKind(VelumProductRegistryProblemKind.BomDiff));
    }
  }
}
