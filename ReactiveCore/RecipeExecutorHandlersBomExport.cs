using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ISIDA.Common;
using Velum.Configuration;
using Velum.UI.AssemblyRegistry;
using Velum.UI.ProductRegistry;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Обработчик рецепта экспорта BOM-данных в CSV для обмена с 1С.
  /// </summary>
  internal static class RecipeExecutorHandlersBomExport
  {
    /// <summary>Идентификатор шага-рефлекса экспорта CSV.</summary>
    public const string BomExportHandlerId = "bom_exchange_export";

    /// <summary>Идентификатор шага-рефлекса открытия диалога обмена.</summary>
    public const string BomExchangeDialogHandlerId = "bom_exchange_show_dialog";

    /// <summary>bom_exchange_show_dialog — открыть диалог обмена BOM с 1C.</summary>
    public static bool TryExecuteBomExchangeShowDialog(
        int index,
        out RecipeStepExecutionResult result)
    {
      Velum.UI.VelumBomExchangeFormHost.TryShow();
      result = new RecipeStepExecutionResult(
          index,
          "invoke",
          true,
          false,
          "bom_exchange_dialog_closed");
      Logger.Info("Velum bom_exchange_show_dialog closed");
      return true;
    }

    /// <summary>
    /// Экспорт BOM-расхождений в CSV в сетевой каталог.
    /// После успеха — сброс previousHash (сброс проблемы).
    /// </summary>
    public static bool TryExecuteBomExchangeExport(
        int index,
        IReadOnlyDictionary<string, string> args,
        out RecipeStepExecutionResult result)
    {
      try
      {
        // Load mirror store.
        VelumAssemblyBomMirrorStore store = new VelumAssemblyBomMirrorStore();
        store.Load();

        // Get discrepancy entries.
        IReadOnlyList<VelumAssemblyBomMirrorEntry> discrepancies = store.GetDiscrepancyEntries();

        if (discrepancies.Count == 0)
        {
          result = new RecipeStepExecutionResult(
              index, "invoke", false, false, "no_bom_discrepancies");
          Logger.Info("Velum bomExport: no discrepancies to export");
          return false;
        }

        // Get exchange folder from args or config.
        string exchangeFolder = null;
        if (args != null && args.TryGetValue("exchangeFolder", out string argFolder))
          exchangeFolder = (argFolder ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(exchangeFolder))
          exchangeFolder = VelumAppConfig.BomExchangeFolder;

        if (string.IsNullOrWhiteSpace(exchangeFolder))
        {
          result = new RecipeStepExecutionResult(
              index, "invoke", false, false, "exchange_folder_not_configured");
          Logger.Warning("Velum bomExport: exchange folder not configured");
          return false;
        }

        // Validate exchange folder.
        if (!Directory.Exists(exchangeFolder))
        {
          try
          {
            Directory.CreateDirectory(exchangeFolder);
          }
          catch (Exception ex)
          {
            result = new RecipeStepExecutionResult(
                index, "invoke", false, false, "exchange_folder_create_failed:" + ex.Message);
            Logger.Warning("Velum bomExport: cannot create exchange folder: " + ex.Message);
            return false;
          }
        }

        // Filter entries with ExternalId (skip those without).
        var exportEntries = new List<VelumAssemblyBomMirrorEntry>();
        foreach (VelumAssemblyBomMirrorEntry entry in discrepancies)
        {
          if (string.IsNullOrWhiteSpace(entry.ExternalId))
            continue;
          exportEntries.Add(entry);
        }

        if (exportEntries.Count == 0)
        {
          result = new RecipeStepExecutionResult(
              index, "invoke", false, false, "no_entries_with_external_id");
          Logger.Info("Velum bomExport: no entries with ExternalId to export");
          return false;
        }

        // Resolve RegistryId from the product registry by file path.
        try
        {
          var registry = new Velum.UI.ProductRegistry.VelumProductRegistryStore();
          registry.Load();
          foreach (VelumAssemblyBomMirrorEntry entry in exportEntries)
          {
            Velum.UI.ProductRegistry.VelumProductItem item =
                registry.FindItemByFilePath(entry.FilePath);
            entry.RegistryId = item?.Id;
          }
        }
        catch (Exception ex)
        {
          Logger.Warning("Velum bomExport: registry resolve failed: " + ex.Message);
        }

        // Generate CSV content.
        string csvContent = GenerateCsv(exportEntries);

        // Generate filename with timestamp.
        string timestamp = DateTime.Now.ToString(
            "yyyy.MM.dd.HH.mm.ss",
            CultureInfo.InvariantCulture);
        string fileName = "1C_update_" + timestamp + ".csv";
        string filePath = Path.Combine(exchangeFolder, fileName);

        // Write CSV atomically.
        string tempPath = filePath + ".tmp";
        try
        {
          File.WriteAllText(tempPath, csvContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
          if (File.Exists(filePath))
            File.Replace(tempPath, filePath, null);
          else
            File.Move(tempPath, filePath);
        }
        catch (Exception ex)
        {
          result = new RecipeStepExecutionResult(
              index, "invoke", false, false, "csv_write_failed:" + ex.Message);
          Logger.Error("Velum bomExport: CSV write failed: " + ex.Message);
          return false;
        }

        // Update previousHash = currentHash for all exported entries.
        foreach (VelumAssemblyBomMirrorEntry entry in exportEntries)
        {
          store.UpdatePreviousHash(entry.Identity, entry.CurrentHash);
        }

        // Save the mirror store.
        store.Save();

        result = new RecipeStepExecutionResult(
            index,
            "invoke",
            true,
            false,
            "bom_exchange_exported:" + exportEntries.Count + ":1C_update_" + timestamp + ".csv");
        Logger.Info(
            "Velum bomExport OK entries=" + exportEntries.Count + " path=\"" + filePath + "\"");
        return true;
      }
      catch (Exception ex)
      {
        result = new RecipeStepExecutionResult(
            index, "invoke", false, false, "bom_export_error:" + ex.Message);
        Logger.Error("Velum bomExport: " + ex.Message);
        return false;
      }
    }

    /// <summary>
    /// Сформировать CSV-контент для обмена с 1C.
    /// Формат: UTF-8 с BOM, разделитель «;».
    /// </summary>
    private static string GenerateCsv(IReadOnlyList<VelumAssemblyBomMirrorEntry> entries)
    {
      // Load tracked properties config for dynamic columns.
      IReadOnlyList<string> trackedProperties =
          VelumAssemblyBomTrackedPropertiesConfig.Load();

      var sb = new StringBuilder();

      // UTF-8 BOM.
      sb.Append('\uFEFF');

      // Header row.
      sb.Append("ExternalId;"); // Id связи с 1C
      sb.Append("RegistryId;"); // Id записи в реестре
      sb.Append("Designation;"); // Обозначение
      sb.Append("Name;"); // Наименование
      sb.Append("FilePath;"); // Путь к файлу
      sb.Append("Configuration;"); // Конфигурация
      sb.Append("Quantity;"); // Количество
      foreach (string prop in trackedProperties)
      {
        sb.Append(prop + ";");
      }
      sb.AppendLine();

      // Data rows.
      foreach (VelumAssemblyBomMirrorEntry entry in entries)
      {
        sb.Append(CsvField(entry.ExternalId ?? string.Empty));
        sb.Append(';');
        sb.Append(CsvField((entry.RegistryId.HasValue ? entry.RegistryId.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)));
        sb.Append(';');
        sb.Append(CsvField(entry.Designation ?? string.Empty));
        sb.Append(';');
        sb.Append(CsvField(entry.Name ?? string.Empty));
        sb.Append(';');
        sb.Append(CsvField(entry.FilePath ?? string.Empty));
        sb.Append(';');
        sb.Append(CsvField(entry.ConfigurationName ?? string.Empty));
        sb.Append(';');
        sb.Append(CsvField(entry.Quantity.ToString(CultureInfo.InvariantCulture)));

        // Tracked property values from the mirror snapshot.
        foreach (string prop in trackedProperties)
        {
          string value = string.Empty;
          if (entry.TrackedValues != null &&
              entry.TrackedValues.TryGetValue(prop, out string raw))
          {
            value = raw ?? string.Empty;
          }

          sb.Append(';');
          sb.Append(CsvField(value));
        }

        sb.AppendLine();
      }

      return sb.ToString();
    }

    /// <summary>Экранировать CSV-поле: если есть разделитель/кавычки — обернуть в "".</summary>
    private static string CsvField(string value)
    {
      if (string.IsNullOrEmpty(value))
        return string.Empty;

      // Escape double quotes by doubling them.
      if (value.Contains("\""))
        value = value.Replace("\"", "\"\"");

      // Wrap in quotes if contains delimiter, quote, or newline.
      if (value.Contains(';') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        return '"' + value + '"';

      return value;
    }
  }
}
