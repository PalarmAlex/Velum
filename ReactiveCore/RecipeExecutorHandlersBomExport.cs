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
  /// Выгружает два независимых потока: карточки номенклатуры (1C_update_*.csv)
  /// и структуру состава с операциями add/update/delete (1C_bom_*.csv).
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
      bool exported = Velum.UI.VelumBomExchangeFormHost.TryShow();
      if (exported)
      {
        result = new RecipeStepExecutionResult(
            index,
            "invoke",
            true,
            false,
            "bom_exchange_exported");
        Logger.Info("Velum bom_exchange_show_dialog: export completed");
      }
      else
      {
        result = new RecipeStepExecutionResult(
            index,
            "invoke",
            false,
            false,
            "bom_exchange_dialog_closed");
        Logger.Info("Velum bom_exchange_show_dialog: closed without export");
      }
      return exported;
    }

    /// <summary>
    /// Экспорт BOM-расхождений в CSV в сетевой каталог:
    /// карточки (1C_update_*.csv) и структура состава (1C_bom_*.csv).
    /// После успеха — сброс previousHash в обоих store и пометка журнала выгруженной.
    /// </summary>
    public static bool TryExecuteBomExchangeExport(
        int index,
        IReadOnlyDictionary<string, string> args,
        out RecipeStepExecutionResult result)
    {
      try
      {
        // Load mirror store (карточки).
        VelumAssemblyBomMirrorStore store = new VelumAssemblyBomMirrorStore();
        store.Load();

        // Get discrepancy entries (карточки с ExternalId).
        IReadOnlyList<VelumAssemblyBomMirrorEntry> discrepancies = store.GetDiscrepancyEntries();
        var exportEntries = new List<VelumAssemblyBomMirrorEntry>();
        foreach (VelumAssemblyBomMirrorEntry entry in discrepancies)
        {
          if (string.IsNullOrWhiteSpace(entry.ExternalId))
            continue;
          exportEntries.Add(entry);
        }

        // Structure side: единый отбор записей структуры для выгрузки
        // (общий с формой экспорта — VelumBomExchangeStructureProjector):
        // pending-записи по структурам с расхождением, ParentConfiguration —
        // актуальный из структуры, ChildExternalId — актуальный из зеркала карточек.
        VelumBomStructureStore structureStore = new VelumBomStructureStore();
        structureStore.Load();

        VelumBomChangeLogStore changeStore = new VelumBomChangeLogStore();
        changeStore.Load();

        VelumBomExchangeStructureProjector.Selection structureSelection =
            VelumBomExchangeStructureProjector.Select(structureStore, store, changeStore);
        List<VelumBomChangeRecord> exportRecords = structureSelection.Records;

        // Нечего выгружать ни в карточках, ни в структуре — прежний результат
        // «нет расхождений» (пустые карточки не отменяют выгрузку структуры).
        if (exportEntries.Count == 0 && exportRecords.Count == 0)
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

        // Generate filename with timestamp (общий для обоих файлов).
        string timestamp = DateTime.Now.ToString(
            "yyyy.MM.dd.HH.mm.ss",
            CultureInfo.InvariantCulture);

        // Card CSV (как раньше; файл не создаётся, если выгружать нечего).
        if (exportEntries.Count > 0)
        {
          // Generate CSV content (состав колонок берётся из bomExchangeLayout.json).
          string layoutError;
          string csvContent = GenerateCsv(exportEntries, out layoutError);
          if (csvContent == null)
          {
            result = new RecipeStepExecutionResult(
                index, "invoke", false, false, layoutError ?? "Не выбрано ни одного поля для выгрузки.");
            Logger.Warning("Velum bomExport: " + (layoutError ?? "no export columns"));
            return false;
          }

          string fileName = "1C_update_" + timestamp + ".csv";
          string writeError;
          if (!TryWriteCsvAtomic(
                  Path.Combine(exchangeFolder, fileName), csvContent, out writeError))
          {
            result = new RecipeStepExecutionResult(
                index, "invoke", false, false, "csv_write_failed:" + writeError);
            Logger.Error("Velum bomExport: CSV write failed: " + writeError);
            return false;
          }

          Logger.Info(
              "Velum bomExport OK entries=" + exportEntries.Count +
              " path=\"" + Path.Combine(exchangeFolder, fileName) + "\"");
        }

        // Structure CSV (операции add/update/delete по строкам состава).
        if (exportRecords.Count > 0)
        {
          string structureCsv = GenerateStructureCsv(exportRecords);
          string structureFileName = "1C_bom_" + timestamp + ".csv";
          string writeError;
          if (!TryWriteCsvAtomic(
                  Path.Combine(exchangeFolder, structureFileName), structureCsv, out writeError))
          {
            result = new RecipeStepExecutionResult(
                index, "invoke", false, false, "csv_write_failed:" + writeError);
            Logger.Error("Velum bomExport: structure CSV write failed: " + writeError);
            return false;
          }

          Logger.Info(
              "Velum bomExport OK structure records=" + exportRecords.Count +
              " path=\"" + Path.Combine(exchangeFolder, structureFileName) + "\"");
        }

        // After successful export: previousHash = currentHash (карточки).
        foreach (VelumAssemblyBomMirrorEntry entry in exportEntries)
        {
          store.UpdatePreviousHash(entry.Identity, entry.CurrentHash);
        }

        // previousHash = currentHash для выгруженных структур
        // (только тех, чьи записи реально попали в CSV).
        var exportedStructureIdentities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (VelumBomChangeRecord record in exportRecords)
        {
          VelumBomStructureEntry structureEntry;
          if (structureSelection.StructuresByParentExternalId.TryGetValue(
                  record.ParentExternalId, out structureEntry))
            exportedStructureIdentities.Add(structureEntry.ParentIdentity);
        }

        foreach (VelumBomStructureEntry entry in structureSelection.StructuresByParentExternalId.Values)
        {
          if (exportedStructureIdentities.Contains(entry.ParentIdentity))
            structureStore.UpdatePreviousHash(entry.ParentIdentity, entry.CurrentHash);
        }

        // Пометить выгруженные записи журнала и сохранить все store.
        changeStore.MarkExported(
            exportRecords.Select(r => r.Id), DateTime.UtcNow);
        store.Save();
        structureStore.Save();
        changeStore.Save();

        // Сообщение упоминает только реально созданные файлы.
        string message = "bom_exchange_exported";
        if (exportEntries.Count > 0)
          message += ":cards=" + exportEntries.Count + ":1C_update_" + timestamp + ".csv";
        if (exportRecords.Count > 0)
          message += ":structure=" + exportRecords.Count + ":1C_bom_" + timestamp + ".csv";
        result = new RecipeStepExecutionResult(
            index,
            "invoke",
            true,
            false,
            message);
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
    /// Сформировать CSV-контент для обмена с 1C на основе настроек выгрузки.
    /// Состав, порядок и заголовки колонок берутся из <c>bomExchangeLayout.json</c>.
    /// Формат: UTF-8 с BOM, разделитель «;».
    /// </summary>
    /// <param name="entries">Выгружаемые записи зеркала BOM.</param>
    /// <param name="error">Сообщение об ошибке (если колонок для выгрузки нет).</param>
    /// <returns>CSV-строка или <c>null</c>, если выбрано ноль колонок.</returns>
    private static string GenerateCsv(IReadOnlyList<VelumAssemblyBomMirrorEntry> entries, out string error)
    {
      error = null;

      VelumBomExchangeLayoutFile layout = VelumBomExchangeLayoutStore.LoadOrCreate();
      List<VelumBomExchangeColumnDef> columns = layout.Columns
          .Where(c => c != null && c.Enabled)
          .OrderBy(c => c.Order)
          .ToList();

      if (columns.Count == 0)
      {
        error = "Не выбрано ни одного поля для выгрузки.";
        return null;
      }

      var sb = new StringBuilder();

      // UTF-8 BOM.
      sb.Append('\uFEFF');

      // Header row.
      for (int i = 0; i < columns.Count; i++)
      {
        if (i > 0) sb.Append(';');
        string header = columns[i].Header ?? columns[i].Field ?? string.Empty;
        sb.Append(CsvField(header));
      }
      sb.AppendLine();

      // Data rows.
      foreach (VelumAssemblyBomMirrorEntry entry in entries)
      {
        for (int i = 0; i < columns.Count; i++)
        {
          if (i > 0) sb.Append(';');
          string value = VelumBomExchangeRowProjector.GetValue(entry, columns[i]);
          sb.Append(CsvField(value));
        }
        sb.AppendLine();
      }

      return sb.ToString();
    }

    /// <summary>
    /// Сформировать CSV-контент структуры состава (1C_bom_*.csv).
    /// Колонки фиксированы: ParentExternalId;ParentConfiguration;ChildExternalId;
    /// ChildConfiguration;Quantity;Action. Формат: UTF-8 с BOM, разделитель «;».
    /// Поля ParentConfiguration и ChildExternalId уже актуализированы отбором
    /// (<see cref="VelumBomExchangeStructureProjector.Select"/>).
    /// Порядок строк: ParentExternalId, затем ChildExternalId, затем TimestampUtc, Id
    /// (хронология внутри пары гарантирует корректное применение add → update → delete).
    /// </summary>
    /// <param name="records">Выгружаемые записи журнала изменений (актуализированные).</param>
    /// <returns>CSV-строка.</returns>
    private static string GenerateStructureCsv(
        IReadOnlyList<VelumBomChangeRecord> records)
    {
      var sb = new StringBuilder();

      // UTF-8 BOM.
      sb.Append('\uFEFF');

      // Header row (Action — последняя колонка).
      sb.Append("ParentExternalId;ParentConfiguration;ChildExternalId;ChildConfiguration;Quantity;Action");
      sb.AppendLine();

      // Data rows (единый поток, сортировка по родителю, затем ребёнку, затем хронология).
      List<VelumBomChangeRecord> ordered = records
          .OrderBy(r => r.ParentExternalId, StringComparer.Ordinal)
          .ThenBy(r => r.ChildExternalId, StringComparer.Ordinal)
          .ThenBy(r => r.TimestampUtc)
          .ThenBy(r => r.Id, StringComparer.Ordinal)
          .ToList();

      foreach (VelumBomChangeRecord record in ordered)
      {
        sb.Append(CsvField(record.ParentExternalId ?? string.Empty));
        sb.Append(';');
        sb.Append(CsvField(record.ParentConfiguration ?? string.Empty));
        sb.Append(';');
        sb.Append(CsvField(record.ChildExternalId ?? string.Empty));
        sb.Append(';');
        sb.Append(CsvField(record.ChildConfiguration ?? string.Empty));
        sb.Append(';');
        sb.Append(record.Quantity.ToString(CultureInfo.InvariantCulture));
        sb.Append(';');
        sb.Append(VelumBomChangeLogStore.RenderAction(record.Action));
        sb.AppendLine();
      }

      return sb.ToString();
    }

    /// <summary>Записать CSV атомарно (UTF-8 с BOM).</summary>
    /// <param name="filePath">Целевой путь файла.</param>
    /// <param name="csvContent">Содержимое CSV.</param>
    /// <param name="error">Сообщение об ошибке при неудаче.</param>
    /// <returns>true, если запись успешна.</returns>
    private static bool TryWriteCsvAtomic(string filePath, string csvContent, out string error)
    {
      error = null;

      string tempPath = filePath + ".tmp";
      try
      {
        File.WriteAllText(tempPath, csvContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        if (File.Exists(filePath))
          File.Replace(tempPath, filePath, null);
        else
          File.Move(tempPath, filePath);
        return true;
      }
      catch (Exception ex)
      {
        error = ex.Message;
        return false;
      }
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