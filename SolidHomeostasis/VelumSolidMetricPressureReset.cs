using System.Collections.Generic;
using System.Linq;
using ISIDA.Common;
using ISIDA.Gomeostas;
using Velum.Configuration;
using Velum.Isida;
using Velum.UI.ProductRegistry;

namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Разовый сброс давления метрик среды (gate, оркестратор, release engaged P_i).
  /// Не блокирует последующее давление от рецептов и COM-опроса.
  /// </summary>
  internal static class VelumSolidMetricPressureReset
  {
    /// <summary>
    /// Нет активного документа SW: отпустить виталы документных метрик среды.
    /// Host-global (реестр) не трогаем — они живут без открытого документа.
    /// </summary>
    internal static void OnNoActiveSolidDocument(GomeostasSystem g)
    {
      TryReleaseDocumentScopedEngagedParameters(g);
      TryReleaseStrandedDocumentScopedEnvironmentBadParameters(g, includeHostGlobal: false);
      StripNonHostGlobalFromPublishedGate();
      Logger.Info("Velum metric pressure reset (no active document, keep host-global)");
    }

    /// <summary>
    /// После ручного «Н»: снимок gate и оркестратор — индикатор плашек метрик на панели задач.
    /// </summary>
    internal static void OnManualNormHomeostasis()
    {
      ClearPublishedMetricState();
    }

    /// <summary>
    /// Отпустить currently-engaged параметры, кроме host-global (реестр).
    /// </summary>
    internal static void TryReleaseDocumentScopedEngagedParameters(GomeostasSystem g)
    {
      if (g == null || VelumAppConfig.ObservationMode)
        return;

      var hostGlobalParams = new HashSet<int>();
      VelumProductRegistryIntegrityProbes.CollectInfluencedParamIds(hostGlobalParams);

      IReadOnlyList<int> engaged = VelumSolidMetricPressureEngageRegistry.EnumerateEngagedParamIds();
      if (engaged.Count == 0)
        return;

      var releaseWrites = new Dictionary<int, float>();
      foreach (int paramId in engaged)
      {
        if (hostGlobalParams.Contains(paramId))
          continue;
        if (!VelumSolidMetricParameterRelease.TryComposeFullNormWriteForParam(g, paramId, out float normTarget))
          continue;
        releaseWrites[paramId] = normTarget;
      }

      if (releaseWrites.Count == 0)
        return;

      g.HostBatchUpdateParameterValues(releaseWrites);
      foreach (int paramId in releaseWrites.Keys)
        VelumSolidMetricPressureEngageRegistry.ClearEngaged(paramId);
      Logger.Info(
          "Velum metric pressure release document-scoped engaged count=" +
          releaseWrites.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Отпустить все currently-engaged параметры в норму.
    /// Вызывать до любого <see cref="VelumSolidMetricPressureOrchestrator.Clear"/> при смене эпизода документа.
    /// </summary>
    internal static void TryReleaseAllEngagedParameters(GomeostasSystem g)
    {
      if (g == null || VelumAppConfig.ObservationMode)
        return;

      IReadOnlyList<int> engaged = VelumSolidMetricPressureEngageRegistry.EnumerateEngagedParamIds();
      if (engaged.Count == 0)
        return;

      var releaseWrites = new Dictionary<int, float>();
      foreach (int paramId in engaged)
      {
        if (!VelumSolidMetricParameterRelease.TryComposeFullNormWriteForParam(g, paramId, out float normTarget))
          continue;
        releaseWrites[paramId] = normTarget;
      }

      if (releaseWrites.Count == 0)
        return;

      g.HostBatchUpdateParameterValues(releaseWrites);
      Logger.Info(
          "Velum metric pressure release engaged count=" +
          releaseWrites.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Восстановление: P_i из справочника воздействий среды, всё ещё в Bad, без записи в EngageRegistry.
    /// </summary>
    internal static void TryReleaseStrandedEnvironmentBadParameters(GomeostasSystem g)
    {
      TryReleaseStrandedDocumentScopedEnvironmentBadParameters(g, includeHostGlobal: true);
    }

    private static void TryReleaseStrandedDocumentScopedEnvironmentBadParameters(
        GomeostasSystem g,
        bool includeHostGlobal)
    {
      if (g == null || VelumAppConfig.ObservationMode)
        return;

      var hostGlobalParams = new HashSet<int>();
      if (!includeHostGlobal)
        VelumProductRegistryIntegrityProbes.CollectInfluencedParamIds(hostGlobalParams);

      var paramIds = new HashSet<int>();
      VelumSolidMetricCumulativePressureTarget.CollectAllInfluencedParamIds(paramIds);
      if (paramIds.Count == 0)
        return;

      var releaseWrites = new Dictionary<int, float>();
      foreach (int paramId in paramIds)
      {
        if (!includeHostGlobal && hostGlobalParams.Contains(paramId))
          continue;

        GomeostasSystem.ParameterData param = g.GetAllParameters()?.FirstOrDefault(p => p.Id == paramId);
        if (param == null)
          continue;
        if (!VelumSolidMetricLatchedBadValue.IsInBadZone(g, param))
          continue;
        if (!VelumSolidMetricParameterRelease.TryComposeFullNormWriteForParam(g, paramId, out float normTarget))
          continue;
        if (!VelumSolidMetricCumulativePressureTarget.ShouldWriteTarget(param, normTarget))
          continue;

        releaseWrites[paramId] = normTarget;
      }

      if (releaseWrites.Count == 0)
        return;

      g.HostBatchUpdateParameterValues(releaseWrites);
      Logger.Info(
          "Velum metric pressure release stranded env-bad count=" +
          releaseWrites.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    internal static void ClearPublishedMetricState()
    {
      VelumSolidWorksMetricsCache.Clear();
      VelumSolidEnvironmentGate.Clear();
      VelumSolidMetricPressureOrchestrator.Clear();
    }

    /// <summary>
    /// Убрать из gate всё, кроме host-global (реестр). Кэш COM чистим.
    /// </summary>
    internal static void StripNonHostGlobalFromPublishedGate()
    {
      VelumSolidWorksMetricsCache.Clear();

      IReadOnlyDictionary<string, float> published = VelumSolidEnvironmentGate.GetPublishedSnapshot();
      IReadOnlyDictionary<string, VelumSolidProbeInfluenceContext> publishedInfl =
          VelumSolidEnvironmentGate.GetPublishedInfluenceContexts();

      var values = new Dictionary<string, float>(System.StringComparer.Ordinal);
      var influence = new Dictionary<string, VelumSolidProbeInfluenceContext>(System.StringComparer.Ordinal);
      if (published != null)
      {
        foreach (KeyValuePair<string, float> kv in published)
        {
          if (VelumProductRegistryIntegrityProbes.IsRegistryProbeKey(kv.Key))
            values[kv.Key] = kv.Value;
        }
      }

      if (publishedInfl != null)
      {
        foreach (KeyValuePair<string, VelumSolidProbeInfluenceContext> kv in publishedInfl)
        {
          if (VelumProductRegistryIntegrityProbes.IsRegistryProbeKey(kv.Key))
            influence[kv.Key] = kv.Value;
        }
      }

      if (values.Count == 0)
        VelumSolidEnvironmentGate.Clear();
      else
        VelumSolidEnvironmentGate.Publish(values, influence);
    }

    /// <summary>
    /// true, если ещё есть следы документного давления среды (не host-global реестра).
    /// </summary>
    internal static bool HasPendingEnvironmentPressureResidue()
    {
      var hostGlobalParams = new HashSet<int>();
      VelumProductRegistryIntegrityProbes.CollectInfluencedParamIds(hostGlobalParams);

      foreach (int paramId in VelumSolidMetricPressureEngageRegistry.EnumerateEngagedParamIds())
      {
        if (!hostGlobalParams.Contains(paramId))
          return true;
      }

      IReadOnlyDictionary<string, float> snap = VelumSolidEnvironmentGate.GetPublishedSnapshot();
      if (snap == null || snap.Count == 0)
        return false;

      foreach (string key in snap.Keys)
      {
        if (!VelumProductRegistryIntegrityProbes.IsRegistryProbeKey(key))
          return true;
      }

      return false;
    }

    internal static bool IsNoActiveSolidDocument()
    {
      Xarial.XCad.SolidWorks.ISwApplication app = VelumSolidEnvironmentBridge.TryGetSolidWorksApplication();
      if (app == null)
        return true;

      VelumSolidDocumentEditContext ctx = VelumSolidDocumentEditContextResolver.Resolve(app);
      return ctx.ActiveDocument == null;
    }
  }
}
