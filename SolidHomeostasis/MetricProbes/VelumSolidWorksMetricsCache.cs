using System;
using System.Collections.Generic;

namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Кэш последнего полного снимка проб метрик SolidWorks.
  /// Инвалидируется при смене документа и старте цикла пульсации; между пульсами переиспользуется
  /// планировщиком <see cref="VelumSolidProbeRefreshPlanner"/>.
  /// </summary>
  internal static class VelumSolidWorksMetricsCache
  {
    private static readonly object Gate = new object();
    private static Dictionary<string, float> _last;
    private static Dictionary<string, string> _lastTooltipAppendix;
    private static Dictionary<string, VelumSolidProbeInfluenceContext> _lastInfluenceContexts;
    private static volatile bool _dirty = true;

    /// <summary>Сбросить кэш (например, после смены документа или регенерации).</summary>
    internal static void Invalidate()
    {
      _dirty = true;
    }

    internal static bool TryGetCached(
        out Dictionary<string, float> copy,
        out Dictionary<string, string> tooltipAppendixCopy,
        out Dictionary<string, VelumSolidProbeInfluenceContext> influenceCopy)
    {
      copy = null;
      tooltipAppendixCopy = null;
      influenceCopy = null;
      if (_dirty)
        return false;
      lock (Gate)
      {
        if (_dirty || _last == null || _last.Count == 0)
          return false;
        copy = new Dictionary<string, float>(_last, StringComparer.Ordinal);
        if (_lastTooltipAppendix != null && _lastTooltipAppendix.Count > 0)
          tooltipAppendixCopy = new Dictionary<string, string>(_lastTooltipAppendix, StringComparer.Ordinal);
        if (_lastInfluenceContexts != null && _lastInfluenceContexts.Count > 0)
          influenceCopy = new Dictionary<string, VelumSolidProbeInfluenceContext>(_lastInfluenceContexts, StringComparer.Ordinal);
        return true;
      }
    }

    internal static void Store(
        Dictionary<string, float> snapshot,
        Dictionary<string, string> tooltipAppendix,
        Dictionary<string, VelumSolidProbeInfluenceContext> influenceContexts)
    {
      if (snapshot == null || snapshot.Count == 0)
        return;
      lock (Gate)
      {
        _last = new Dictionary<string, float>(snapshot, StringComparer.Ordinal);
        if (tooltipAppendix != null && tooltipAppendix.Count > 0)
          _lastTooltipAppendix = new Dictionary<string, string>(tooltipAppendix, StringComparer.Ordinal);
        else
          _lastTooltipAppendix = null;
        if (influenceContexts != null && influenceContexts.Count > 0)
          _lastInfluenceContexts = new Dictionary<string, VelumSolidProbeInfluenceContext>(influenceContexts, StringComparer.Ordinal);
        else
          _lastInfluenceContexts = null;
        _dirty = false;
      }
    }

    internal static void Clear()
    {
      lock (Gate)
      {
        _last = null;
        _lastTooltipAppendix = null;
        _lastInfluenceContexts = null;
        _dirty = true;
      }
    }
  }
}
