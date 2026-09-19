using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ISIDA.Common;
using Velum.Configuration;
using Velum.ReactiveCore;
using Velum.UI;

namespace Velum.UI.AssemblyRegistry
{
  /// <summary>
  /// Форма диалога запуска формирования CSV-файлов обмена с 1C:
  /// вкладка «Карточки» (расхождения свойств компонентов) и вкладка
  /// «Структура» (операции add/update/delete по строкам состава).
  /// </summary>
  internal sealed partial class VelumBomExchangeForm : Form
  {
    /// <summary>Включённые колонки выгрузки карточек (в порядке следования) — из bomExchangeLayout.json.</summary>
    private List<VelumBomExchangeColumnDef> _enabledColumns =
        new List<VelumBomExchangeColumnDef>();

    /// <summary>Число карточек к выгрузке (для статус-строки).</summary>
    private int _cardCount;

    /// <summary>Число строк состава к выгрузке (для статус-строки).</summary>
    private int _structureCount;

    public VelumBomExchangeForm()
    {
      InitializeComponent();
      VelumFormIcon.Apply(this);
      VelumAppConfig.EnsureInitialized();
      _folderBox.Text = VelumAppConfig.BomExchangeFolder;

      // Включить кнопку экспорта только если указан каталог обмена.
      UpdateExportButtonState();
      _folderBox.TextChanged += (s, e) => UpdateExportButtonState();

      // Построить колонки списка карточек из настроек выгрузки, затем загрузить строки.
      RebuildColumns();
      LoadDiscrepancyList();
      LoadStructureList();
    }

    private void UpdateExportButtonState()
    {
      string folder = (_folderBox.Text ?? string.Empty).Trim();
      _exportButton.Enabled = !string.IsNullOrEmpty(folder);
    }

    private void OnBrowseClick(object sender, EventArgs e)
    {
      string currentFolder = (_folderBox.Text ?? string.Empty).Trim();
      if (VelumFolderBrowser.TrySelect(this, "Выберите каталог обмена с 1C:", currentFolder, out string selected))
      {
        _folderBox.Text = selected;
        VelumAppConfig.SetBomExchangeFolder(selected);
      }
    }

    /// <summary>
    /// Перестроить колонки списка карточек из настроек выгрузки (bomExchangeLayout.json).
    /// </summary>
    private void RebuildColumns()
    {
      VelumBomExchangeLayoutFile layout = VelumBomExchangeLayoutStore.LoadOrCreate();
      _enabledColumns = layout.Columns
          .Where(c => c != null && c.Enabled)
          .OrderBy(c => c.Order)
          .ToList();

      _listView.BeginUpdate();
      try
      {
        _listView.Columns.Clear();
        foreach (VelumBomExchangeColumnDef col in _enabledColumns)
        {
          string header = string.IsNullOrWhiteSpace(col.Header) ? col.Field : col.Header;
          _listView.Columns.Add(header ?? string.Empty, col.Width);
        }
      }
      finally
      {
        _listView.EndUpdate();
      }
    }

    /// <summary>
    /// Загрузить список расхождений карточек, доступных для экспорта.
    /// </summary>
    private void LoadDiscrepancyList()
    {
      try
      {
        var store = new VelumAssemblyBomMirrorStore();
        store.Load();
        var entries = store.GetDiscrepancyEntries()
            .Where(e => !string.IsNullOrWhiteSpace(e.ExternalId))
            .ToList();

        if (_enabledColumns.Count == 0)
          RebuildColumns();

        _listView.BeginUpdate();
        try
        {
          _listView.Items.Clear();
          if (_enabledColumns.Count == 0)
          {
            // Колонки не выбраны — список остаётся пустым.
          }
          else
          {
            foreach (VelumAssemblyBomMirrorEntry entry in entries)
            {
              string first = VelumBomExchangeRowProjector.GetValue(entry, _enabledColumns[0]);
              var item = new ListViewItem(first ?? string.Empty);
              for (int i = 1; i < _enabledColumns.Count; i++)
                item.SubItems.Add(
                    VelumBomExchangeRowProjector.GetValue(entry, _enabledColumns[i]) ?? string.Empty);
              _listView.Items.Add(item);
            }
          }
        }
        finally
        {
          _listView.EndUpdate();
        }

        _cardCount = entries.Count;
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum bomExchange: unable to load discrepancy list: " + ex.Message);
        _listView.Items.Clear();
        _cardCount = 0;
      }

      UpdateNoteLabel();
    }

    /// <summary>
    /// Загрузить список строк состава, ожидающих выгрузки в 1С.
    /// Отбор — общий с CSV-выгрузкой (<see cref="VelumBomExchangeStructureProjector"/>):
    /// список в форме совпадает со строками 1C_bom_*.csv.
    /// </summary>
    private void LoadStructureList()
    {
      try
      {
        var structureStore = new VelumBomStructureStore();
        structureStore.Load();
        var mirrorStore = new VelumAssemblyBomMirrorStore();
        mirrorStore.Load();
        var changeStore = new VelumBomChangeLogStore();
        changeStore.Load();

        VelumBomExchangeStructureProjector.Selection selection =
            VelumBomExchangeStructureProjector.Select(structureStore, mirrorStore, changeStore);

        List<VelumBomChangeRecord> pending = selection.Records
            .OrderBy(r => r.ParentExternalId, StringComparer.Ordinal)
            .ThenBy(r => r.ChildExternalId, StringComparer.Ordinal)
            .ThenBy(r => r.TimestampUtc)
            .ToList();

        _structureListView.BeginUpdate();
        try
        {
          _structureListView.Items.Clear();
          foreach (VelumBomChangeRecord record in pending)
          {
            // Обозначение родителя: из структуры, fallback — ExternalId.
            string parentDesignation = record.ParentExternalId;
            VelumBomStructureEntry structureEntry;
            if (selection.StructuresByParentExternalId.TryGetValue(
                    record.ParentExternalId, out structureEntry) &&
                !string.IsNullOrWhiteSpace(structureEntry.ParentDesignation))
              parentDesignation = structureEntry.ParentDesignation;

            // Обозначение компонента: из зеркала карточек, fallback — ExternalId.
            string childDesignation = record.ChildExternalId;
            VelumAssemblyBomMirrorEntry childMirror = mirrorStore.GetEntry(record.ChildIdentity);
            if (childMirror != null && !string.IsNullOrWhiteSpace(childMirror.Designation))
              childDesignation = childMirror.Designation;

            var item = new ListViewItem(parentDesignation);
            item.SubItems.Add(childDesignation);
            item.SubItems.Add(record.ChildConfiguration ?? string.Empty);
            item.SubItems.Add(record.Quantity.ToString(CultureInfo.InvariantCulture));
            item.SubItems.Add(VelumBomChangeLogStore.RenderAction(record.Action));
            _structureListView.Items.Add(item);
          }
        }
        finally
        {
          _structureListView.EndUpdate();
        }

        _structureCount = pending.Count;
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum bomExchange: unable to load structure list: " + ex.Message);
        _structureListView.Items.Clear();
        _structureCount = 0;
      }

      UpdateNoteLabel();
    }

    /// <summary>Обновить статус-строку: сколько карточек и строк состава к выгрузке.</summary>
    private void UpdateNoteLabel()
    {
      noteLabel.Text =
          "Карточек к выгрузке: " + _cardCount +
          ". Строк состава: " + _structureCount + "." +
          " Компоненты без заполненного ExternalId будут пропущены при экспорте.";
    }

    private void OnSettingsClick(object sender, EventArgs e)
    {
      using (var form = new VelumBomTrackedPropertiesEditForm())
      {
        form.ShowDialog(this);
      }
    }

    /// <summary>
    /// Открыть окно настройки полей выгрузки карточек, затем перестроить список.
    /// </summary>
    private void OnLayoutSettingsClick(object sender, EventArgs e)
    {
      using (var form = new VelumBomExchangeLayoutForm())
      {
        form.ShowDialog(this);
      }
      RebuildColumns();
      LoadDiscrepancyList();
    }

    private void OnExportClick(object sender, EventArgs e)
    {
      string folder = (_folderBox.Text ?? string.Empty).Trim();
      if (string.IsNullOrEmpty(folder))
      {
        MessageBox.Show(
            this,
            "Укажите каталог обмена.",
            Text,
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return;
      }

      if (!Directory.Exists(folder))
      {
        try
        {
          Directory.CreateDirectory(folder);
        }
        catch (Exception ex)
        {
          MessageBox.Show(
              this,
              "Не удалось создать каталог: " + ex.Message,
              Text,
              MessageBoxButtons.OK,
              MessageBoxIcon.Error);
          return;
        }
      }

      // Сохранить настройку каталога.
      VelumAppConfig.SetBomExchangeFolder(folder);

      // Выполнить рефлекс.
      if (RecipeExecutorHandlersBomExport.TryExecuteBomExchangeExport(
              0,
              new System.Collections.Generic.Dictionary<string, string> { { "exchangeFolder", folder } },
              out RecipeStepExecutionResult result))
      {
        if (result.Success)
        {
          MessageBox.Show(
              this,
              "Экспорт выполнен успешно.\n" + result.Message,
              Text,
              MessageBoxButtons.OK,
              MessageBoxIcon.Information);
          // previousHash и журнал обновлены внутри TryExecuteBomExchangeExport.
          LoadDiscrepancyList();
          LoadStructureList();
          this.DialogResult = DialogResult.OK;
          Close();
        }
        else
        {
          MessageBox.Show(
              this,
              "Экспорт не выполнен: " + result.Message,
              Text,
              MessageBoxButtons.OK,
              MessageBoxIcon.Warning);
        }
      }
      else
      {
        MessageBox.Show(
            this,
            "Ошибка при выполнении экспорта.",
            Text,
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
      }
    }
  }
}
