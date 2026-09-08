using System;
using System.Collections.Generic;

namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Значения проб метрик SW с прошлого такта пульса для импульсного сравнения «текущее vs предыдущее».
  /// До первого опроса по ProbeKey база = оптимум шкалы (100).
  /// </summary>
  internal static class VelumSolidPulseMetricCompare
  {
    private static readonly Dictionary<string, float> LastByProbeKey = new Dictionary<string, float>(StringComparer.Ordinal);

    internal static bool TryGetLast(string probeKey, out float value) =>
        LastByProbeKey.TryGetValue(probeKey, out value);

    /// <summary>
    /// Предыдущее значение пробы или оптимум 100 («выше — лучше») до первого снимка по ключу.
    /// </summary>
    internal static float GetLastOrOptimalBaseline(string probeKey)
    {
      if (TryGetLast(probeKey, out float value))
        return value;
      return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
    }

    /// <summary>
    /// Засеять оптимум 100 для ключей правил давления среды, по которым ещё нет снимка.
    /// </summary>
    internal static void SeedOptimalBaselineForProbeKeys(IEnumerable<string> probeKeys)
    {
      ApplyOptimalBaselineForProbeKeys(probeKeys, overwriteExisting: false);
    }

    /// <summary>
    /// Сбросить базу сравнения в оптимум 100 для всех ключей (в т.ч. перезаписать прошлые значения).
    /// Нужно при смене активного документа: иначе бинарные метрики вроде UnsavedNew остаются 50→50 без импульса.
    /// </summary>
    internal static void ReseedOptimalBaselineForProbeKeys(IEnumerable<string> probeKeys)
    {
      ApplyOptimalBaselineForProbeKeys(probeKeys, overwriteExisting: true);
    }

    private static void ApplyOptimalBaselineForProbeKeys(IEnumerable<string> probeKeys, bool overwriteExisting)
    {
      if (probeKeys == null)
        return;

      float baseline = VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
      foreach (string probeKey in probeKeys)
      {
        string key = (probeKey ?? string.Empty).Trim();
        if (key.Length == 0)
          continue;
        if (!overwriteExisting && LastByProbeKey.ContainsKey(key))
          continue;
        LastByProbeKey[key] = baseline;
      }
    }

    /// <summary>
    /// Сохранить текущий опрос SW как базу для следующего пульса.
    /// </summary>
    internal static void RecordSnapshot(IReadOnlyDictionary<string, float> snap)
    {
      if (snap == null || snap.Count == 0)
        return;
      foreach (KeyValuePair<string, float> kv in snap)
        LastByProbeKey[kv.Key] = kv.Value;
    }

    internal static void Clear() => LastByProbeKey.Clear();
  }
}
