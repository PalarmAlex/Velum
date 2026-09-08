using System;
using System.Collections.Generic;
using System.IO;
using Velum.SolidHomeostasis;

namespace Velum.ReactiveCore.Export
{
  /// <summary>
  /// Проверки каталога delivery перед пакетным экспортом DXF:
  /// жёсткий запрет qty рядом с базовыми файлами списка; предупреждение о перезаписи при qty выкл.
  /// </summary>
  internal static class VelumDxfDeliveryFolderGuard
  {
    internal sealed class CheckResult
    {
      internal bool Blocked { get; set; }

      internal string BlockMessage { get; set; }

      internal bool NeedsOverwriteConfirm { get; set; }

      internal string OverwriteMessage { get; set; }

      internal int OverwriteCount { get; set; }
    }

    internal static CheckResult Evaluate(
        string deliveryFolder,
        IReadOnlyList<VelumDxfBatchDiagnosticRow> selectedRows,
        Func<VelumDxfBatchDiagnosticRow, string> buildDeliveryBaseName,
        bool addAssemblyQuantity,
        bool searchImmediateSubfolders = false)
    {
      var result = new CheckResult();
      string folder = (deliveryFolder ?? string.Empty).Trim();
      if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        return result;

      if (selectedRows == null || selectedRows.Count == 0 || buildDeliveryBaseName == null)
        return result;

      string folderFull;
      try
      {
        folderFull = Path.GetFullPath(folder);
      }
      catch
      {
        return result;
      }

      IReadOnlyList<string> searchFolders = CollectSearchFolders(folderFull, searchImmediateSubfolders);

      var baseHits = new List<string>();
      var overwriteHits = new List<string>();
      var seenBase = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      var seenOverwrite = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

      for (int i = 0; i < selectedRows.Count; i++)
      {
        VelumDxfBatchDiagnosticRow row = selectedRows[i];
        if (row == null)
          continue;

        string canonicalBase = TryResolveCanonicalBaseName(row);
        if (!string.IsNullOrWhiteSpace(canonicalBase))
        {
          for (int f = 0; f < searchFolders.Count; f++)
          {
            string basePath = Path.Combine(searchFolders[f], canonicalBase + ".dxf");
            if (File.Exists(basePath) && seenBase.Add(basePath))
              baseHits.Add(FormatHitDisplay(folderFull, basePath));
          }

          if (!string.IsNullOrWhiteSpace(row.DxfPath))
          {
            try
            {
              string canonicalFull = Path.GetFullPath(row.DxfPath.Trim());
              string canonicalDir = Path.GetDirectoryName(canonicalFull) ?? string.Empty;
              if (IsUnderSearchRoot(canonicalDir, folderFull, searchImmediateSubfolders) &&
                  File.Exists(canonicalFull) &&
                  seenBase.Add(canonicalFull))
                baseHits.Add(FormatHitDisplay(folderFull, canonicalFull));
            }
            catch
            {
            }
          }
        }

        if (!addAssemblyQuantity)
        {
          string deliveryBase = (buildDeliveryBaseName(row) ?? string.Empty).Trim();
          if (string.IsNullOrWhiteSpace(deliveryBase))
            continue;

          for (int f = 0; f < searchFolders.Count; f++)
          {
            string deliveryPath = Path.Combine(searchFolders[f], deliveryBase + ".dxf");
            if (File.Exists(deliveryPath) && seenOverwrite.Add(deliveryPath))
              overwriteHits.Add(FormatHitDisplay(folderFull, deliveryPath));
          }
        }
      }

      if (addAssemblyQuantity && baseHits.Count > 0)
      {
        result.Blocked = true;
        result.BlockMessage =
            "В каталоге выгрузки уже есть базовые DXF из списка экспорта (" +
            baseHits.Count +
            "). При включённом «Добавить кол-во по сборке» выгрузка в этот каталог запрещена — " +
            "выберите другой каталог (например подпапку комплекта)." +
            Environment.NewLine +
            Environment.NewLine +
            FormatSample(baseHits);
        return result;
      }

      if (!addAssemblyQuantity && overwriteHits.Count > 0)
      {
        result.NeedsOverwriteConfirm = true;
        result.OverwriteCount = overwriteHits.Count;
        result.OverwriteMessage =
            "В каталоге выгрузки будут перезаписаны файлы DXF: " +
            overwriteHits.Count +
            "." +
            Environment.NewLine +
            Environment.NewLine +
            FormatSample(overwriteHits) +
            Environment.NewLine +
            Environment.NewLine +
            "Продолжить?";
      }

      return result;
    }

    private static IReadOnlyList<string> CollectSearchFolders(string folderFull, bool includeImmediateSubfolders)
    {
      var folders = new List<string> { folderFull };
      if (!includeImmediateSubfolders)
        return folders;

      try
      {
        string[] children = Directory.GetDirectories(folderFull);
        for (int i = 0; i < children.Length; i++)
        {
          string child = children[i];
          if (!string.IsNullOrWhiteSpace(child))
            folders.Add(child);
        }
      }
      catch
      {
      }

      return folders;
    }

    private static bool IsUnderSearchRoot(string directory, string rootFull, bool includeImmediateSubfolders)
    {
      if (string.Equals(directory, rootFull, StringComparison.OrdinalIgnoreCase))
        return true;

      if (!includeImmediateSubfolders)
        return false;

      try
      {
        string parent = Path.GetDirectoryName(directory) ?? string.Empty;
        return string.Equals(parent, rootFull, StringComparison.OrdinalIgnoreCase);
      }
      catch
      {
        return false;
      }
    }

    private static string FormatHitDisplay(string rootFull, string fullPath)
    {
      try
      {
        string relative = fullPath;
        if (fullPath.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
        {
          relative = fullPath.Substring(rootFull.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        if (!string.IsNullOrWhiteSpace(relative))
          return relative;
      }
      catch
      {
      }

      return Path.GetFileName(fullPath);
    }

    private static string TryResolveCanonicalBaseName(VelumDxfBatchDiagnosticRow row)
    {
      if (row == null)
        return string.Empty;

      string expected = (row.ExpectedDxfBaseName ?? string.Empty).Trim();
      if (!string.IsNullOrWhiteSpace(expected))
        return VelumDxfQuantityToken.StripQuantitySuffix(expected);

      string fromPath = Path.GetFileNameWithoutExtension(row.DxfPath ?? string.Empty);
      if (!string.IsNullOrWhiteSpace(fromPath))
        return VelumDxfQuantityToken.StripQuantitySuffix(fromPath);

      string fromPart = Path.GetFileNameWithoutExtension(row.PartPath ?? string.Empty);
      return (fromPart ?? string.Empty).Trim();
    }

    private static string FormatSample(IList<string> names)
    {
      if (names == null || names.Count == 0)
        return string.Empty;

      const int max = 8;
      var parts = new List<string>();
      int limit = Math.Min(names.Count, max);
      for (int i = 0; i < limit; i++)
        parts.Add(names[i]);

      string text = string.Join(Environment.NewLine, parts);
      if (names.Count > max)
        text += Environment.NewLine + "… и ещё " + (names.Count - max);
      return text;
    }
  }
}
