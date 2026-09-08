using System;
using System.Collections.Generic;
using System.IO;

namespace Velum.UI.ProductRegistry
{
  /// <summary>
  /// Открытый документ SW без записи в реестре с тем же FilePath.
  /// В режиме автосканирования используется только при отсутствии открытых документов
  /// (<see cref="VelumProductRegistryIntegrityScheduler"/>); при открытом документе кэш очищается.
  /// </summary>
  internal static class VelumProductRegistryMissingEntryScanner
  {
    internal static void TickFullScanClear()
    {
      VelumProductRegistryProblemCache.RemoveAllOfKind(
          VelumProductRegistryProblemKind.MissingRegistryEntry);
    }

    /// <summary>
    /// Синхронизирует кэш MissingRegistryEntry с набором открытых путей.
    /// </summary>
    internal static void TickOpenDocuments(
        VelumProductRegistryStore store,
        HashSet<string> openPaths)
    {
      if (openPaths == null || openPaths.Count == 0)
      {
        TickFullScanClear();
        return;
      }

      IReadOnlyList<VelumProductRegistryProblemEntry> cached =
          VelumProductRegistryProblemCache.SnapshotKind(
              VelumProductRegistryProblemKind.MissingRegistryEntry);
      foreach (VelumProductRegistryProblemEntry entry in cached)
      {
        if (entry == null)
          continue;

        string path = VelumProductRegistryStore.NormalizeFilePathKey(entry.FilePath);
        if (string.IsNullOrEmpty(path) || !openPaths.Contains(path))
        {
          VelumProductRegistryProblemCache.RemoveMissingRegistryEntry(entry.FilePath);
          continue;
        }

        if (VelumProductRegistryOpenDocumentsScope.HasItemWithPath(store, path))
          VelumProductRegistryProblemCache.RemoveMissingRegistryEntry(path);
      }

      foreach (string openPath in openPaths)
      {
        if (string.IsNullOrEmpty(openPath))
          continue;
        if (VelumProductRegistryOpenDocumentsScope.HasItemWithPath(store, openPath))
        {
          VelumProductRegistryProblemCache.RemoveMissingRegistryEntry(openPath);
          continue;
        }

        VelumProductRegistryProblemCache.Upsert(Build(openPath));
      }
    }

    private static VelumProductRegistryProblemEntry Build(string filePath)
    {
      string path = VelumProductRegistryStore.NormalizeFilePathKey(filePath);
      string basename = VelumProductRegistryMatchKey.FromFilePath(path);
      string name = basename;
      try
      {
        name = Path.GetFileName(path) ?? basename;
      }
      catch
      {
      }

      return new VelumProductRegistryProblemEntry
      {
        ItemId = 0,
        Kind = VelumProductRegistryProblemKind.MissingRegistryEntry,
        Designation = basename,
        Name = name,
        FilePath = path,
        Detail = "Открытый документ отсутствует в реестре изделий (нет записи с этим путём)."
      };
    }
  }
}
