using System;

using System.Collections.Generic;

using System.Linq;

using ISIDA.Actions;

using ISIDA.SymbiontEnv.Contract;

using Velum.Configuration;

using Velum.Isida;



namespace Velum.SolidHomeostasis

{

  /// <summary>Индекс воздействий среды (EA с ProbeKey) из ISIDA InfluenceActionSystem.</summary>

  public static class VelumSolidEnvironmentInfluenceCatalog

  {

    /// <summary>Уникальные непустые ProbeKey из активных EA среды.</summary>

    public static IReadOnlyList<string> EnumerateDistinctEnvironmentProbeKeys()

    {

      if (!VelumIsidaHost.IsReady)

        return Array.Empty<string>();



      return QueryActiveEnvironmentActions()

          .Select(a => (a.ProbeKey ?? string.Empty).Trim())

          .Where(s => s.Length > 0)

          .Distinct(StringComparer.Ordinal)

          .OrderBy(s => s, StringComparer.Ordinal)

          .ToList();

    }



    /// <summary>Все EA среды с ProbeKey (включая отключённые — для UI).</summary>

    public static IReadOnlyList<InfluenceActionSystem.GomeostasisInfluenceAction> GetAllEnvironmentActions()

    {

      if (!VelumIsidaHost.IsReady)

        return Array.Empty<InfluenceActionSystem.GomeostasisInfluenceAction>();



      return QueryAllEnvironmentActions();

    }



    /// <summary>Только активные EA среды (runtime: опрос и давление).</summary>

    public static IReadOnlyList<InfluenceActionSystem.GomeostasisInfluenceAction> GetActiveEnvironmentActions()

    {

      if (!VelumIsidaHost.IsReady)

        return Array.Empty<InfluenceActionSystem.GomeostasisInfluenceAction>();



      return QueryActiveEnvironmentActions();

    }



    private static IReadOnlyList<InfluenceActionSystem.GomeostasisInfluenceAction> QueryAllEnvironmentActions()

    {

      InfluenceActionSystem sys = VelumIsidaHost.Context.InfluenceActions;

      try

      {

        return sys.GetEnvironmentInfluenceActions();

      }

      catch (MissingMethodException)

      {

        return FilterEnvironmentActions(sys.GetAllInfluenceActions());

      }

    }



    private static IReadOnlyList<InfluenceActionSystem.GomeostasisInfluenceAction> QueryActiveEnvironmentActions()
    {
      InfluenceActionSystem sys = VelumIsidaHost.Context.InfluenceActions;
      try
      {
        var result = sys.GetActiveEnvironmentInfluenceActions();
        System.Diagnostics.Debug.WriteLine("[Velum.EA] GetActiveEnvironmentActions count=" + result.Count + " ids=" + string.Join(",", result.Select(a => a.Id)));
        return result;
      }
      catch (MissingMethodException)
      {
        return FilterEnvironmentActions(sys.GetAllInfluenceActions())
            .Where(a => a.IsActive)
            .ToList();
      }
    }

    private static List<InfluenceActionSystem.GomeostasisInfluenceAction> FilterEnvironmentActions(
        System.Collections.ObjectModel.ReadOnlyCollection<InfluenceActionSystem.GomeostasisInfluenceAction> all)
    {
      var filtered = (all ?? new System.Collections.ObjectModel.ReadOnlyCollection<InfluenceActionSystem.GomeostasisInfluenceAction>(
              Array.Empty<InfluenceActionSystem.GomeostasisInfluenceAction>()))
          .Where(a => a != null && a.IsEnvironmentProbeAction)
          .OrderBy(a => a.Id)
          .ToList();
      System.Diagnostics.Debug.WriteLine("[Velum.EA] FilterEnvironmentActions all=" + (all?.Count ?? 0) + " filtered=" + filtered.Count);
      return filtered;
    }



    /// <summary>EA по ProbeKey или null.</summary>

    public static InfluenceActionSystem.GomeostasisInfluenceAction TryGetByProbeKey(string probeKey)

    {

      if (!VelumIsidaHost.IsReady || string.IsNullOrWhiteSpace(probeKey))

        return null;



      return VelumIsidaHost.Context.InfluenceActions.GetInfluenceActionByProbeKey(probeKey);

    }



    /// <summary>true, если активная метрика среды сейчас «плохая» (давит или готова давить).</summary>
    public static bool IsActiveMetricPressing(InfluenceActionSystem.GomeostasisInfluenceAction ea)
    {
      if (ea == null || !ea.IsEnvironmentProbeAction || !ea.IsActive)
        return false;

      string probeKey = (ea.ProbeKey ?? string.Empty).Trim();
      if (probeKey.Length == 0)
        return false;

      bool noActiveDoc = VelumSolidMetricPressureReset.IsNoActiveSolidDocument();
      if (noActiveDoc)
      {
        // Без документа SW горят только host-global метрики (реестр); документные плашки гаснут.
        if (!Velum.UI.ProductRegistry.VelumProductRegistryIntegrityProbes.IsRegistryProbeKey(probeKey))
          return false;
      }
      else
      {
        Xarial.XCad.SolidWorks.ISwApplication app = VelumSolidEnvironmentBridge.TryGetSolidWorksApplication();
        if (app != null)
        {
          VelumSolidDocumentEditContext editContext = VelumSolidDocumentEditContextResolver.Resolve(app);
          if (!VelumSolidWorksHomeostasisMetrics.IsProbeAllowedForDocumentContext(probeKey, editContext))
            return false;
        }
      }

      IReadOnlyDictionary<string, float> snap = VelumSolidEnvironmentGate.GetPublishedSnapshot();
      if (snap == null || !snap.TryGetValue(probeKey, out float metric))
      {
        System.Diagnostics.Debug.WriteLine("[Velum.EA] IsActiveMetricPressing MISS probe=" + probeKey);
        return false;
      }

      bool bad = MetricProbeThresholds.IsProbeBad(metric, VelumAppConfig.SolidEnvironmentMetricDeltaEpsilon);
      if (bad)
      {
        System.Diagnostics.Debug.WriteLine("[Velum.EA] IsActiveMetricPressing BAD probe=" + probeKey + " metric=" + metric);
      }
      return bad;
    }

  }

}


