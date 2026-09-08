using System;
using System.Collections.Generic;
using Velum.SolidHomeostasis;
using Velum.UI.AssemblyRegistry;

namespace Velum.UI.ProductRegistry
{
  /// <summary>O(1) пробы целостности реестра по <see cref="VelumProductRegistryProblemCache"/>.</summary>
  /// <remarks>
  /// Значения актуальны только при отсутствии открытых документов SW: при открытии документа
  /// кэш проблем очищается и метрики сбрасываются в «хорошо» (<see cref="VelumProductRegistryIntegrityScheduler"/>).
  /// </remarks>
  internal static class VelumProductRegistryIntegrityProbes
  {
    internal const string ProbeKeyHasBrokenLinks = "Velum.Registry.HasBrokenLinks";
    internal const string ProbeKeyHasMissingDrawings = "Velum.Registry.HasMissingDrawings";
    internal const string ProbeKeyHasMissingRegistryEntries = "Velum.Registry.HasMissingRegistryEntries";
    internal const string ProbeKeyHasDxfProblems = "Velum.Registry.HasDxfProblems";
    internal const string ProbeKeyHasPdfProblems = "Velum.Registry.HasPdfProblems";

    internal const float BadScore = 50f;
    internal const float GoodScore = 100f;

    /// <summary>Виды проблем, входящие в метрику «Реестр: проблемы DXF».</summary>
    private static readonly VelumProductRegistryProblemKind[] DxfProblemKinds =
    {
      VelumProductRegistryProblemKind.NeedDxfExport,
      VelumProductRegistryProblemKind.OutdatedDxf,
      VelumProductRegistryProblemKind.MissingDxfProjection,
      VelumProductRegistryProblemKind.DxfCatalogUnavailable,
      VelumProductRegistryProblemKind.DxfFirstExport,
      VelumProductRegistryProblemKind.JunkDxf
    };

    /// <summary>Виды проблем, входящие в метрику «Реестр: проблемы PDF».</summary>
    private static readonly VelumProductRegistryProblemKind[] PdfProblemKinds =
    {
      VelumProductRegistryProblemKind.NeedPdfExport,
      VelumProductRegistryProblemKind.OutdatedPdf,
      VelumProductRegistryProblemKind.JunkPdf
    };

    internal static bool IsRegistryProbeKey(string probeKey)
    {
      string k = (probeKey ?? string.Empty).Trim();
      return string.Equals(k, ProbeKeyHasBrokenLinks, StringComparison.Ordinal)
          || string.Equals(k, ProbeKeyHasMissingDrawings, StringComparison.Ordinal)
          || string.Equals(k, ProbeKeyHasMissingRegistryEntries, StringComparison.Ordinal)
          || string.Equals(k, ProbeKeyHasDxfProblems, StringComparison.Ordinal)
          || string.Equals(k, ProbeKeyHasPdfProblems, StringComparison.Ordinal)
          || string.Equals(k, VelumAssemblyBomDiffProbe.ProbeKey, StringComparison.Ordinal);
    }

    internal static bool TrySample(string probeKey, out float value, out string detail)
    {
      value = GoodScore;
      detail = null;
      string k = (probeKey ?? string.Empty).Trim();

      if (string.Equals(k, ProbeKeyHasBrokenLinks, StringComparison.Ordinal))
      {
        bool bad = VelumProductRegistryProblemCache.HasKind(VelumProductRegistryProblemKind.BrokenLink);
        value = bad ? BadScore : GoodScore;
        if (bad)
        {
          int n = VelumProductRegistryProblemCache.CountKindIncludingPending(
              VelumProductRegistryProblemKind.BrokenLink);
          detail = "Битые ссылки реестра: " + n;
        }

        return true;
      }

      if (string.Equals(k, ProbeKeyHasMissingDrawings, StringComparison.Ordinal))
      {
        bool bad = VelumProductRegistryProblemCache.HasKind(VelumProductRegistryProblemKind.MissingDrawing);
        value = bad ? BadScore : GoodScore;
        if (bad)
        {
          int n = VelumProductRegistryProblemCache.CountKindIncludingPending(
              VelumProductRegistryProblemKind.MissingDrawing);
          detail = "Нет чертежей в реестре: " + n;
        }

        return true;
      }

      if (string.Equals(k, ProbeKeyHasMissingRegistryEntries, StringComparison.Ordinal))
      {
        bool bad = VelumProductRegistryProblemCache.HasKind(
            VelumProductRegistryProblemKind.MissingRegistryEntry);
        value = bad ? BadScore : GoodScore;
        if (bad)
        {
          IReadOnlyList<VelumProductRegistryProblemEntry> snap =
              VelumProductRegistryProblemCache.SnapshotKind(
                  VelumProductRegistryProblemKind.MissingRegistryEntry);
          detail = "Открытые документы вне реестра: " + snap.Count;
        }

        return true;
      }

      if (string.Equals(k, ProbeKeyHasDxfProblems, StringComparison.Ordinal))
      {
        bool bad = VelumProductRegistryProblemCache.HasAnyKind(DxfProblemKinds);
        value = bad ? BadScore : GoodScore;
        if (bad)
        {
          int n = CountKindsIncludingPending(DxfProblemKinds);
          detail = "Проблемы DXF в реестре: " + n;
        }

        return true;
      }

      if (string.Equals(k, ProbeKeyHasPdfProblems, StringComparison.Ordinal))
      {
        bool bad = VelumProductRegistryProblemCache.HasAnyKind(PdfProblemKinds);
        value = bad ? BadScore : GoodScore;
        if (bad)
        {
          int n = CountKindsIncludingPending(PdfProblemKinds);
          detail = "Проблемы PDF в реестре: " + n;
        }

        VelumSolidDiagLog.WriteError(
            "registry pdf: HasAnyKind=" + bad + " count=" + CountKindsIncludingPending(PdfProblemKinds) + " score=" + value);
        return true;
      }

      // BOM mirror — расхождение состава/свойств компонентов.
      if (string.Equals(k, VelumAssemblyBomDiffProbe.ProbeKey, StringComparison.Ordinal))
      {
        bool bad = VelumProductRegistryProblemCache.HasKind(
            VelumProductRegistryProblemKind.BomDiff);
        value = bad ? BadScore : GoodScore;
        if (bad)
        {
          int n = VelumProductRegistryProblemCache.CountKindIncludingPending(
              VelumProductRegistryProblemKind.BomDiff);
          detail = "Расхождение BOM-хэша: " + n;
        }

        return true;
      }

      return false;
    }

    private static int CountKindsIncludingPending(
        IReadOnlyList<VelumProductRegistryProblemKind> kinds)
    {
      IReadOnlyList<VelumProductRegistryProblemEntry> snap =
          VelumProductRegistryProblemCache.SnapshotIncludingPending();
      int n = 0;
      for (int i = 0; i < snap.Count; i++)
      {
        VelumProductRegistryProblemEntry e = snap[i];
        if (e == null)
          continue;
        for (int j = 0; j < kinds.Count; j++)
        {
          if (e.Kind == kinds[j])
          {
            n++;
            break;
          }
        }
      }

      return n;
    }

    /// <summary>
    /// Обновляет в gate ключи реестра (каждый пульс, без COM).
    /// </summary>
    /// <param name="registryOnlySnapshot">
    /// true — gate содержит только пробы реестра (нет активного документа SW);
    /// false — мерж в существующий снимок.
    /// </param>
    internal static void PublishScoresToGate(bool registryOnlySnapshot = false)
    {
      var values = new Dictionary<string, float>(StringComparer.Ordinal);
      var influence = new Dictionary<string, VelumSolidProbeInfluenceContext>(StringComparer.Ordinal);

      if (!registryOnlySnapshot)
      {
        IReadOnlyDictionary<string, float> published = VelumSolidEnvironmentGate.GetPublishedSnapshot();
        if (published != null)
        {
          foreach (KeyValuePair<string, float> kv in published)
            values[kv.Key] = kv.Value;
        }

        IReadOnlyDictionary<string, VelumSolidProbeInfluenceContext> publishedInfl =
            VelumSolidEnvironmentGate.GetPublishedInfluenceContexts();
        if (publishedInfl != null)
        {
          foreach (KeyValuePair<string, VelumSolidProbeInfluenceContext> kv in publishedInfl)
            influence[kv.Key] = kv.Value;
        }
      }

      PublishOne(values, influence, ProbeKeyHasBrokenLinks);
      PublishOne(values, influence, ProbeKeyHasMissingDrawings);
      PublishOne(values, influence, ProbeKeyHasMissingRegistryEntries);
      PublishOne(values, influence, ProbeKeyHasDxfProblems);
      PublishOne(values, influence, ProbeKeyHasPdfProblems);
      PublishOne(values, influence, VelumAssemblyBomDiffProbe.ProbeKey);

      float pdfScore = -1f; values.TryGetValue(ProbeKeyHasPdfProblems, out pdfScore);
      float dxfScore = -1f; values.TryGetValue(ProbeKeyHasDxfProblems, out dxfScore);
      bool any = VelumProductRegistryProblemCache.HasAny;
      VelumSolidDiagLog.WriteError(
          "registry probe publish: pdf=" + pdfScore + " dxf=" + dxfScore + " any=" + any + " keys=" + values.Count);
      VelumSolidEnvironmentGate.Publish(values, influence);
    }

    private static void PublishOne(
        Dictionary<string, float> values,
        Dictionary<string, VelumSolidProbeInfluenceContext> influence,
        string probeKey)
    {
      float score;
      string tip;
      TrySample(probeKey, out score, out tip);
      values[probeKey] = score;
      influence[probeKey] = score < 99f
          ? new VelumSolidProbeInfluenceContext(0, 1)
          : new VelumSolidProbeInfluenceContext(1, 1);
    }

    /// <summary>Параметры гомеостаза, на которые влияют только registry ProbeKey.</summary>
    internal static void CollectInfluencedParamIds(HashSet<int> target)
    {
      if (target == null)
        return;

      foreach (ISIDA.Actions.InfluenceActionSystem.GomeostasisInfluenceAction ea in
               VelumSolidEnvironmentInfluenceCatalog.GetActiveEnvironmentActions())
      {
        if (ea == null || !IsRegistryProbeKey(ea.ProbeKey))
          continue;
        if (ea.Influences == null)
          continue;
        foreach (int paramId in ea.Influences.Keys)
        {
          if (paramId > 0)
            target.Add(paramId);
        }
      }
    }
  }
}
