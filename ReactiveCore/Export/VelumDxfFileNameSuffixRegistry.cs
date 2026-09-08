using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
using Velum.Configuration;

namespace Velum.ReactiveCore.Export
{
  /// <summary>
  /// Список имён суффиксов имени DXF в Settings.xml (разделитель «|»).
  /// Список накапливается: новые имена добавляются, уже сохранённые не удаляются.
  /// Кол-во ([Quantity]/[Кол-во]) в список не входит — суффикс «N шт» задаётся флажком на batch-форме.
  /// </summary>
  internal static class VelumDxfFileNameSuffixRegistry
  {
    internal static IReadOnlyList<string> GetSuffixes()
    {
      PurgeQuantityTokensFromSettingsIfPresent();
      return Parse(VelumAppConfig.DxfFileNameSuffixList);
    }

    /// <summary>
    /// Добавляет в Settings.xml имена свойств детали, которых ещё нет в списке суффиксов.
    /// </summary>
    internal static bool TryMergeFromPartProperties(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return false;

      return TryMergeSuffixes(VelumDxfFileNameHelper.CollectCustomPropertyNames(modelDoc));
    }

    /// <summary>
    /// Добавляет в Settings.xml новые имена суффиксов (существующие сохраняются).
    /// </summary>
    internal static bool TryMergeSuffixes(IEnumerable<string> candidateNames)
    {
      return TryMergeSuffixesCore(candidateNames);
    }

    private static bool TryMergeSuffixesCore(IEnumerable<string> candidateNames)
    {
      if (candidateNames == null)
        return false;

      List<string> current = Parse(VelumAppConfig.DxfFileNameSuffixList);
      var existing = new HashSet<string>(current, StringComparer.OrdinalIgnoreCase);
      var ordered = new List<string>(current);
      bool changed = false;

      foreach (string candidate in candidateNames)
      {
        string trimmed = (candidate ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
          continue;
        if (VelumDxfQuantityToken.IsQuantityToken(trimmed))
          continue;

        if (!existing.Add(trimmed))
          continue;

        ordered.Add(trimmed);
        changed = true;
      }

      if (!changed)
        return false;

      ordered.Sort(StringComparer.OrdinalIgnoreCase);
      VelumAppConfig.SetDxfFileNameSuffixList(string.Join("|", ordered));
      return true;
    }

    /// <summary>Удаляет устаревшие qty-токены из Settings.xml (раньше добавлялись в маску).</summary>
    private static void PurgeQuantityTokensFromSettingsIfPresent()
    {
      string raw = VelumAppConfig.DxfFileNameSuffixList;
      if (string.IsNullOrWhiteSpace(raw))
        return;

      List<string> parsed = ParseRaw(raw);
      List<string> withoutQty = parsed
          .Where(x => !VelumDxfQuantityToken.IsQuantityToken(x))
          .ToList();
      if (withoutQty.Count == parsed.Count)
        return;

      VelumAppConfig.SetDxfFileNameSuffixList(string.Join("|", withoutQty));
    }

    private static List<string> Parse(string raw)
    {
      return ParseRaw(raw)
          .Where(x => !VelumDxfQuantityToken.IsQuantityToken(x))
          .ToList();
    }

    private static List<string> ParseRaw(string raw)
    {
      if (string.IsNullOrWhiteSpace(raw))
        return new List<string>();

      return raw
          .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
          .Select(x => x.Trim())
          .Where(x => !string.IsNullOrWhiteSpace(x))
          .Distinct(StringComparer.OrdinalIgnoreCase)
          .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
          .ToList();
    }
  }
}
