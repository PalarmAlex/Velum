using System;
using System.IO;

namespace Velum.ReactiveCore.Export
{
  /// <summary>Копирует уже сохранённый канонический файл в каталог выдачи.</summary>
  internal static class VelumExportDeliveryHelper
  {
    internal static bool TryCopyToDelivery(
        string sourceFullPath,
        string deliveryFolder,
        string deliveryBaseNameWithoutExt,
        string extensionWithDot,
        out string deliveryPath,
        out string error)
    {
      deliveryPath = string.Empty;
      error = string.Empty;
      try
      {
        string source = Path.GetFullPath((sourceFullPath ?? string.Empty).Trim());
        string folder = (deliveryFolder ?? string.Empty).Trim();
        string baseName = (deliveryBaseNameWithoutExt ?? string.Empty).Trim();
        string extension = (extensionWithDot ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
        {
          error = "source_missing";
          return false;
        }
        if (string.IsNullOrWhiteSpace(folder))
          return true;
        if (!Directory.Exists(folder))
        {
          error = "delivery_folder_missing";
          return false;
        }
        if (string.IsNullOrWhiteSpace(baseName))
          baseName = Path.GetFileNameWithoutExtension(source);
        if (string.IsNullOrWhiteSpace(extension))
          extension = Path.GetExtension(source);
        if (!extension.StartsWith(".", StringComparison.Ordinal))
          extension = "." + extension;

        deliveryPath = Path.Combine(folder, baseName + extension);
        if (string.Equals(
                source,
                Path.GetFullPath(deliveryPath),
                StringComparison.OrdinalIgnoreCase))
          return true;

        File.Copy(source, deliveryPath, true);
        return true;
      }
      catch (Exception ex)
      {
        error = ex.Message;
        return false;
      }
    }
  }
}
