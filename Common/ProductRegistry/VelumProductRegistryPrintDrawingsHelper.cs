using System;
using System.Collections.Generic;
using System.IO;
using Velum.ReactiveCore.Export;
using Velum.UI.AssemblyRegistry;
using Velum.UI.ProductRegistry;

namespace Velum.UI
{
  /// <summary>Сопоставление записей реестра документов с очередью печати чертежей/PDF.</summary>
  internal static class VelumProductRegistryPrintDrawingsHelper
  {
    internal static bool IsPrintablePath(string filePath)
    {
      return VelumProductRegistryIntegrityRules.IsDrawingPath(filePath)
          || VelumProductRegistryIntegrityRules.IsPdfPath(filePath);
    }

    internal static List<VelumAssemblyRegistryPrintDrawingsService.PrintRow> Resolve(
        IReadOnlyList<VelumProductItem> items)
    {
      var rows = new List<VelumAssemblyRegistryPrintDrawingsService.PrintRow>();
      if (items == null || items.Count == 0)
        return rows;

      var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

      for (int i = 0; i < items.Count; i++)
      {
        VelumProductItem item = items[i];
        if (item == null)
          continue;

        string path = VelumProductRegistryStore.NormalizeFilePathKey(item.FilePath);
        string display = DisplayName(item);
        if (string.IsNullOrEmpty(path))
        {
          rows.Add(new VelumAssemblyRegistryPrintDrawingsService.PrintRow
          {
            ComponentDisplayName = display,
            ModelPath = string.Empty,
            BaseName = string.Empty,
            DrawingPath = string.Empty,
            Kind = VelumAssemblyRegistryPrintDrawingsService.PrintRowKind.Skipped,
            StatusText = "Нет пути к файлу",
            IsPdfDocument = false
          });
          continue;
        }

        bool isPdf = VelumProductRegistryIntegrityRules.IsPdfPath(path);
        bool isDrawing = VelumProductRegistryIntegrityRules.IsDrawingPath(path);
        if (!isPdf && !isDrawing)
        {
          rows.Add(new VelumAssemblyRegistryPrintDrawingsService.PrintRow
          {
            ComponentDisplayName = display,
            ModelPath = path,
            BaseName = Path.GetFileNameWithoutExtension(path) ?? string.Empty,
            DrawingPath = string.Empty,
            Kind = VelumAssemblyRegistryPrintDrawingsService.PrintRowKind.Skipped,
            StatusText = "Не чертёж/PDF",
            IsPdfDocument = false
          });
          continue;
        }

        if (!VelumPathExists.FileExists(path))
        {
          rows.Add(new VelumAssemblyRegistryPrintDrawingsService.PrintRow
          {
            ComponentDisplayName = display,
            ModelPath = path,
            BaseName = Path.GetFileNameWithoutExtension(path) ?? string.Empty,
            DrawingPath = string.Empty,
            Kind = VelumAssemblyRegistryPrintDrawingsService.PrintRowKind.Missing,
            StatusText = "Файл не найден",
            IsPdfDocument = isPdf
          });
          continue;
        }

        if (!seenPaths.Add(path))
          continue;

        rows.Add(new VelumAssemblyRegistryPrintDrawingsService.PrintRow
        {
          ComponentDisplayName = display,
          ModelPath = path,
          BaseName = Path.GetFileNameWithoutExtension(path) ?? string.Empty,
          DrawingPath = path,
          Kind = VelumAssemblyRegistryPrintDrawingsService.PrintRowKind.Ready,
          StatusText = Path.GetFileName(path),
          IsPdfDocument = isPdf
        });
      }

      return rows;
    }

    private static string DisplayName(VelumProductItem item)
    {
      if (item == null)
        return string.Empty;
      if (!string.IsNullOrWhiteSpace(item.Designation))
        return item.Designation.Trim();
      string path = item.FilePath ?? string.Empty;
      if (path.Length > 0)
        return Path.GetFileName(path);
      return "(без имени)";
    }
  }
}
