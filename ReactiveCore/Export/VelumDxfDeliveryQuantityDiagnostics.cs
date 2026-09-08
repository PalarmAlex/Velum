using System;
using System.IO;
using Velum.SolidHomeostasis;

namespace Velum.ReactiveCore.Export
{
  /// <summary>
  /// Проверка delivery-файла «базовоеИмя - N шт.dxf» относительно канонического DXF.
  /// Каталог — тот, что указан на пакетной форме (включая субпапки толщины).
  /// </summary>
  internal static class VelumDxfDeliveryQuantityDiagnostics
  {
    /// <summary>
    /// Если канонический DXF актуален, а файла с кол-вом нет или он старше базы —
    /// повышает статус до <see cref="VelumDxfBatchRowStatus.QuantityOutdated"/>.
    /// </summary>
    /// <param name="row">Строка диагностики.</param>
    /// <param name="deliveryRoot">Каталог выдачи на пакетной форме.</param>
    /// <param name="quantityOverride">
    /// Эффективное кол-во для имени файла (сборка × изделие); null — брать <see cref="VelumDxfBatchDiagnosticRow.Quantity"/>.
    /// </param>
    internal static void ApplyIfNeeded(
        VelumDxfBatchDiagnosticRow row,
        string deliveryRoot,
        int? quantityOverride = null)
    {
      if (row == null)
        return;

      if (row.Status != VelumDxfBatchRowStatus.Ok)
        return;

      int quantity = ResolveQuantity(row, quantityOverride);
      if (quantity <= 0)
        return;

      string root = (deliveryRoot ?? string.Empty).Trim();
      if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        return;

      string baseName = ResolveCanonicalBaseName(row);
      if (string.IsNullOrWhiteSpace(baseName))
        return;

      string canonicalPath = (row.DxfPath ?? string.Empty).Trim();
      if (string.IsNullOrWhiteSpace(canonicalPath) || !File.Exists(canonicalPath))
        return;

      string expectedQtyName = baseName + " - " + quantity + " шт";
      string qtyPath = TryFindDxfByBaseName(root, expectedQtyName);
      if (string.IsNullOrWhiteSpace(qtyPath))
      {
        row.Status = VelumDxfBatchRowStatus.QuantityOutdated;
        row.StatusText = "Нет файла с кол-вом";
        row.Selected = true;
        return;
      }

      try
      {
        // LastWriteTimeUtc: CreationTime на NTFS часто «залипает» при копировании и даёт ложные «устарел».
        DateTime baseWrite = File.GetLastWriteTimeUtc(canonicalPath);
        DateTime qtyWrite = File.GetLastWriteTimeUtc(qtyPath);
        if (qtyWrite < baseWrite)
        {
          row.Status = VelumDxfBatchRowStatus.QuantityOutdated;
          row.StatusText = "Устарел файл с кол-вом";
          row.Selected = true;
        }
      }
      catch
      {
      }
    }

    private static int ResolveQuantity(VelumDxfBatchDiagnosticRow row, int? quantityOverride)
    {
      if (quantityOverride.HasValue)
        return quantityOverride.Value;
      if (row != null && row.Quantity.HasValue)
        return row.Quantity.Value;
      return 0;
    }

    private static string ResolveCanonicalBaseName(VelumDxfBatchDiagnosticRow row)
    {
      string fromExpected = VelumDxfQuantityToken.StripQuantitySuffix(
          (row?.ExpectedDxfBaseName ?? string.Empty).Trim());
      if (!string.IsNullOrWhiteSpace(fromExpected))
        return fromExpected;

      try
      {
        string fromPath = Path.GetFileNameWithoutExtension(row?.DxfPath ?? string.Empty);
        return VelumDxfQuantityToken.StripQuantitySuffix(fromPath);
      }
      catch
      {
        return string.Empty;
      }
    }

    private static string TryFindDxfByBaseName(string root, string baseNameWithoutExtension)
    {
      if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(baseNameWithoutExtension))
        return null;

      string direct = Path.Combine(root, baseNameWithoutExtension + ".dxf");
      if (File.Exists(direct))
        return direct;

      try
      {
        foreach (string file in Directory.EnumerateFiles(root, "*.dxf", SearchOption.AllDirectories))
        {
          string name = Path.GetFileNameWithoutExtension(file);
          if (string.Equals(name, baseNameWithoutExtension, StringComparison.OrdinalIgnoreCase))
            return file;
        }
      }
      catch
      {
      }

      return null;
    }
  }
}
