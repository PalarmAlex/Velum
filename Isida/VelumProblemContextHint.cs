using System;
using System.Collections.Generic;
using System.Linq;
using ISIDA.Actions;
using ISIDA.Common;
using ISIDA.Gomeostas;
using ISIDA.Psychic;
using ISIDA.Reflexes;
using Velum.Configuration;
using Velum.SolidHomeostasis;
using Velum.UI.ProductRegistry;
using Xarial.XCad.SolidWorks;

namespace Velum.Isida
{
  /// <summary>
  /// Подсказка оператору: актуальная проблема (доминантный параметр стилей)
  /// и одно уместное воздействие с пульта по б/у рефлексам текущего контекста.
  /// </summary>
  internal static class VelumProblemContextHint
  {
    /// <summary>
    /// Зона стилевой активации «норма» (см. HomeostasisCalculator / StyleActivations[2]).
    /// </summary>
    private const int StyleZoneNormal = 2;

    private enum OperatorHintFamily
    {
      None = 0,
      PdfExport,
      DxfExport,
      Registry
    }

    /// <summary>
    /// Вычисляет, есть ли проблема, и имя <see cref="AppGlobalState.DominantParam"/> (источник активных стилей).
    /// При <see cref="AppGlobalState.HomeostasisState.Well"/> проблемы нет.
    /// Иначе проблема, если Bad / VeryActual / зона доминанта ≠ норма.
    /// </summary>
    public static bool TryGetActualProblem(out bool hasProblem, out string problemName)
    {
      hasProblem = false;
      problemName = string.Empty;

      if (!VelumIsidaHost.IsReady)
        return false;

      try
      {
        AppGlobalState.HomeostasisState overall = AppGlobalState.CurrentOverallState;
        if (overall == AppGlobalState.HomeostasisState.Well)
          return true;

        GomeostasSystem g = VelumIsidaHost.Context.Gomeostas;
        if (g?.Calculator == null)
          return false;

        List<GomeostasSystem.ParameterData> parameters = g.GetAllParameters();
        if (parameters == null || parameters.Count == 0)
          return true;

        var (dominant, zone, _) = g.Calculator.FindDominantParameter(
            parameters,
            g.DynamicTime,
            g.DifSensorPar);

        bool veryActual = InformationEnvironmentSystem.IsInitialized
            && InformationEnvironmentSystem.Instance.VeryActualSituation;

        hasProblem = overall == AppGlobalState.HomeostasisState.Bad
            || veryActual
            || zone != StyleZoneNormal;

        if (!hasProblem || dominant == null)
          return true;

        if (!string.IsNullOrWhiteSpace(dominant.Name))
          problemName = dominant.Name.Trim();
        else
          problemName = "параметр " + dominant.Id;

        return true;
      }
      catch (Exception ex)
      {
        if (VelumAppConfig.SolidHomeostasisDebugLog)
          System.Diagnostics.Trace.WriteLine("VelumProblemContextHint.TryGetActualProblem: " + ex);
        return false;
      }
    }

    /// <summary>
    /// Одно воздействие с пульта для подсветки: б/у рефлексы с Level1+Level2 как текущий контекст
    /// (без проверки триггера), с непустым <c>InfluenceActionIds</c>.
    /// При нескольких кандидатах — сужение по горящим пробам / типу документа, иначе минимальный ID.
    /// Только при наличии проблемы. Нет подходящих рефлексов — <c>null</c>.
    /// </summary>
    public static int? TryGetSuggestedOperatorInfluenceActionId()
    {
      // Пока снимок проб не доверен (смена документа, COM-timeout) ActiveStyles могут
      // ещё отражать предыдущий эпизод — не подсвечиваем чужое воздействие.
      if (VelumSolidEnvironmentGate.SnapshotUntrustworthy)
        return null;

      if (!TryGetActualProblem(out bool hasProblem, out _) || !hasProblem)
        return null;

      if (!GeneticReflexesSystem.IsInitialized)
        return null;

      try
      {
        int level1 = (int)AppGlobalState.CurrentOverallState;
        List<int> activeStyleIds = AppGlobalState.ActiveStyles == null
            ? new List<int>()
            : AppGlobalState.ActiveStyles.Where(s => s != null).Select(s => s.Id).ToList();

        var candidates = new HashSet<int>();
        foreach (GeneticReflexesSystem.GeneticReflex reflex in GeneticReflexesSystem.Instance.GetAllGeneticReflexes())
        {
          if (reflex == null)
            continue;
          if (reflex.Level1 != level1)
            continue;
          if (!Level2MatchesActiveStyles(reflex.Level2, activeStyleIds))
            continue;
          if (reflex.InfluenceActionIds == null || reflex.InfluenceActionIds.Count == 0)
            continue;

          foreach (int id in reflex.InfluenceActionIds)
          {
            if (id > 0)
              candidates.Add(id);
          }
        }

        if (candidates.Count == 0)
          return null;

        if (candidates.Count == 1)
          return candidates.Min();

        IReadOnlyDictionary<int, string> actionNames = TryBuildOperatorActionNameMap();
        OperatorHintFamily family = ResolvePreferredFamily();
        if (family != OperatorHintFamily.None)
        {
          var narrowed = new List<int>();
          foreach (int id in candidates)
          {
            if (ClassifyOperatorAction(id, actionNames) == family)
              narrowed.Add(id);
          }

          if (narrowed.Count > 0)
            return narrowed.Min();
        }

        return candidates.Min();
      }
      catch (Exception ex)
      {
        if (VelumAppConfig.SolidHomeostasisDebugLog)
          System.Diagnostics.Trace.WriteLine("VelumProblemContextHint.TryGetSuggestedOperatorInfluenceActionId: " + ex);
        return null;
      }
    }

    /// <summary>
    /// Семья подсказки: сначала по горящим пробам активного документа, иначе по типу документа
    /// (чертёж → PDF, деталь → DXF). Нужно, т.к. стиль «Проблемы с документацией» общий для PDF и DXF.
    /// </summary>
    private static OperatorHintFamily ResolvePreferredFamily()
    {
      VelumSolidDocumentEditContext editContext = null;
      ISwApplication app = VelumSolidEnvironmentBridge.TryGetSolidWorksApplication();
      if (app != null)
        editContext = VelumSolidDocumentEditContextResolver.Resolve(app);

      OperatorHintFamily fromProbes = ResolvePreferredFamilyFromPressingProbes(editContext);
      if (fromProbes != OperatorHintFamily.None)
        return fromProbes;

      if (editContext == null || editContext.ActiveDocument == null)
        return OperatorHintFamily.None;

      if (editContext.IsDrawingDocument)
        return OperatorHintFamily.PdfExport;

      if (editContext.HasPartLevelProbeTarget)
        return OperatorHintFamily.DxfExport;

      return OperatorHintFamily.None;
    }

    private static OperatorHintFamily ResolvePreferredFamilyFromPressingProbes(
        VelumSolidDocumentEditContext editContext)
    {
      IReadOnlyDictionary<string, float> snap = VelumSolidEnvironmentGate.GetPublishedSnapshot();
      if (snap == null || snap.Count == 0)
        return OperatorHintFamily.None;

      bool pdf = false;
      bool dxf = false;
      bool registry = false;

      foreach (KeyValuePair<string, float> kv in snap)
      {
        string probeKey = (kv.Key ?? string.Empty).Trim();
        if (probeKey.Length == 0)
          continue;
        if (!VelumSolidMetricProbeThresholds.IsProbeBad(kv.Value))
          continue;
        if (!VelumSolidWorksHomeostasisMetrics.IsProbeAllowedForDocumentContext(probeKey, editContext))
          continue;

        // Registry-пробы всегда классифицируются как Registry, независимо от содержимого имени.
        // Это предотвращает ложное попадание Velum.Registry.HasDxfProblems в категорию DXF.
        if (VelumProductRegistryIntegrityProbes.IsRegistryProbeKey(probeKey))
        {
          registry = true;
          continue;
        }

        if (VelumSolidExportDocumentationProbe.IsPdfProbeKey(probeKey))
          pdf = true;
        else if (VelumSolidExportDocumentationProbe.IsDxfProbeKey(probeKey))
          dxf = true;
      }

      // Документные пробы важнее host-global (реестр), если обе горят.
      if (pdf)
        return OperatorHintFamily.PdfExport;
      if (dxf)
        return OperatorHintFamily.DxfExport;
      if (registry)
        return OperatorHintFamily.Registry;
      return OperatorHintFamily.None;
    }

    private static OperatorHintFamily ClassifyOperatorAction(
        int actionId,
        IReadOnlyDictionary<int, string> actionNames)
    {
      if (actionNames == null || !actionNames.TryGetValue(actionId, out string name) ||
          string.IsNullOrWhiteSpace(name))
        return OperatorHintFamily.None;

      if (ContainsToken(name, "PDF"))
        return OperatorHintFamily.PdfExport;
      if (ContainsToken(name, "DXF"))
        return OperatorHintFamily.DxfExport;
      if (ContainsToken(name, "реестр") || ContainsToken(name, "реестра"))
        return OperatorHintFamily.Registry;
      return OperatorHintFamily.None;
    }

    private static bool ContainsToken(string text, string token)
    {
      return text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static IReadOnlyDictionary<int, string> TryBuildOperatorActionNameMap()
    {
      if (!VelumIsidaHost.IsReady)
        return null;

      try
      {
        InfluenceActionSystem sys = VelumIsidaHost.Context.InfluenceActions;
        if (sys == null)
          return null;

        var map = new Dictionary<int, string>();
        foreach (InfluenceActionSystem.GomeostasisInfluenceAction a in sys.GetAllInfluenceActions())
        {
          if (a == null || a.Id <= 0 || a.IsEnvironmentProbeAction)
            continue;
          map[a.Id] = a.Name ?? string.Empty;
        }

        return map;
      }
      catch
      {
        return null;
      }
    }

    /// <summary>
    /// Как в <c>ReflexesActivator.IsReflexConditionsMet</c>: пустой Level2 не ограничивает;
    /// иначе точное равенство множеств со стилями.
    /// </summary>
    private static bool Level2MatchesActiveStyles(IList<int> level2, IList<int> activeStyleIds)
    {
      if (level2 == null || level2.Count == 0)
        return true;

      if (activeStyleIds == null || activeStyleIds.Count == 0)
        return false;

      return level2.All(activeStyleIds.Contains) && activeStyleIds.All(level2.Contains);
    }
  }
}
