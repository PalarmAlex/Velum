using System;
using System.Collections.Generic;
using System.Drawing;
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
  /// вкладки «Карточки» (расхождения свойств компонентов), «Структура»
  /// (операции add/update/delete по строкам состава в очереди на выгрузку)
  /// и «Все» (весь журнал, включая уже выгруженные).
  /// </summary>
  internal sealed partial class VelumBomExchangeForm : Form
  {
    /// <summary>
    /// Индекс колонки «Дата/время» в списке структуры. Числовое сравнение в
    /// <see cref="VelumListFilterHelper"/> (<c>&gt;</c>/<c>&lt;</c>) идёт по текстовому
    /// значению ячейки, поэтому фильтру нужны сортируемые ISO-метки; человек видит
    /// местное время. ISO-метка строки лежит в <see cref="StructureRowData.IsoSortKey"/>,
    /// а не в тексте колонки.
    /// </summary>
    private const int StructColumnTimestamp = 6;

    /// <summary>Индекс колонки «Действие» (add/update/delete) в списке структуры.</summary>
    private const int StructColumnAction = 4;

    /// <summary>Индекс колонки «ExternalId» ребёнка в списке структуры.</summary>
    private const int StructColumnExternalId = 5;

    /// <summary>Индекс колонки «Родитель» в списке структуры (и во вкладке «Все»).</summary>
    private const int StructColumnParent = 0;

    /// <summary>Индекс колонки «Компонент» в списке структуры (и во вкладке «Все»).</summary>
    private const int StructColumnChild = 1;

    /// <summary>Индекс колонки «Конфигурация» в списке структуры (и во вкладке «Все»).</summary>
    private const int StructColumnConfig = 2;

    /// <summary>Индекс колонки «Кол-во» в списке структуры (и во вкладке «Все»).</summary>
    private const int StructColumnQuantity = 3;

    /// <summary>Индекс колонки «Состояние» во вкладке «Все» (есть только там).</summary>
    private const int AllColumnState = 7;

    /// <summary>Данные одной строки списков состава + ключи сортировки и фильтра.</summary>
    private sealed class StructureRowData
    {
      /// <summary>Запись журнала (копия проектора с актуализированными полями).</summary>
      public VelumBomChangeRecord Record;

      /// <summary>Обозначение родителя (из структуры, fallback — ExternalId).</summary>
      public string ParentText;

      /// <summary>Обозначение компонента (из зеркала карточек, fallback — ExternalId).</summary>
      public string ChildText;

      /// <summary>
      /// Метка времени в местном времени, ISO-вид (<c>yyyy-MM-dd HH:mm:ss</c>):
      /// ключ сортировки и операнд числовых сравнений фильтра. В колонке показывается
      /// короткое представление, а сортировка/фильтр идут по этому значению —
      /// так «&gt;=2026-09-01» работает независимо от формата вывода.
      /// </summary>
      public string IsoSortKey;

      /// <summary>Запись уже выгружена в 1С (вкладка «Все»).</summary>
      public bool Exported;
    }

    /// <summary>Строки вкладки «Структура» в порядке иерархии (до сортировки и фильтра).</summary>
    private List<StructureRowData> _structureRows = new List<StructureRowData>();

    /// <summary>Строки вкладки «Все» (весь журнал, включая выгруженные).</summary>
    private List<StructureRowData> _allRows = new List<StructureRowData>();

    /// <summary>Номер колонки структуры, по которой выполнена сортировка (-1 — порядок по умолчанию).</summary>
    private int _structureSortColumn = -1;

    /// <summary>Признак обратного направления сортировки структуры по клику.</summary>
    private bool _structureSortDescending;

    /// <summary>Номер колонки вкладки «Все», по которой выполнена сортировка (-1 — по дате).</summary>
    private int _allSortColumn = -1;

    /// <summary>Признак обратного направления сортировки вкладки «Все».</summary>
    private bool _allSortDescending;

    /// <summary>Значения фильтров вкладки «Структура», применённые кнопкой «Применить».</summary>
    private BomExchangeFilterValues _structureFilters = BomExchangeFilterValues.Empty();

    /// <summary>Значения фильтров вкладки «Все», применённые кнопкой «Применить».</summary>
    private BomExchangeFilterValues _allFilters = BomExchangeFilterValues.Empty();

    /// <summary>
    /// Значения ячеек списка карточек (по колонкам) для сортировки:
    /// строка списка хранится отдельным буфером, т.к. <see cref="ListView"/> не даёт
    /// доступа к данным через <c>ListViewItemComparer</c> без пересборки списка.
    /// </summary>
    private readonly List<string[]> _cardRowValues = new List<string[]>();

    /// <summary>Номер колонки карточек, по которой выполнена сортировка (-1 — исходный порядок).</summary>
    private int _cardsSortColumn = -1;

    /// <summary>Признак обратного направления сортировки карточек по клику.</summary>
    private bool _cardsSortDescending;

    /// <summary>Включённые колонки выгрузки карточек (в порядке следования) — из bomExchangeLayout.json.</summary>
    private List<VelumBomExchangeColumnDef> _enabledColumns =
        new List<VelumBomExchangeColumnDef>();

    /// <summary>Число карточек к выгрузке (для статус-строки).</summary>
    private int _cardCount;

    /// <summary>Число строк состава к выгрузке (для статус-строки).</summary>
    private int _structureCount;

    /// <summary>Число карточек, скрытых фильтром «только реестр» (для статус-строки).</summary>
    private int _hiddenByRegistryCount;

    /// <summary>Число видимых карточек, отсутствующих в реестре изделий (показаны серым).</summary>
    private int _notRegisteredCount;

    /// <summary>Загруженный индекс реестра изделий (для сверки вкладки карточек).</summary>
    private VelumBomExchangeRegistryIndex _registryIndex;

    /// <summary>Карточки, показанные последней загрузкой (до повторной сортировки).</summary>
    private List<VelumAssemblyBomMirrorEntry> _lastCards;

    /// <summary>Всего записей в зеркале (счётчик статус-строки, без обращения к диску).</summary>
    private int _mirrorTotalCount;

    /// <summary>Записей с невыгруженным расхождением (без Stale).</summary>
    private int _mirrorDiscrepancyCount;

    /// <summary>Устаревших записей (файл исчез, Stale).</summary>
    private int _mirrorStaleCount;

    public VelumBomExchangeForm()
    {
      InitializeComponent();
      VelumFormIcon.Apply(this);
      VelumFormHelp.Bind(this, VelumHelpTopics.BomExchange);
      VelumAppConfig.EnsureInitialized();
      _folderBox.Text = VelumAppConfig.BomExchangeFolder;

      // Признак «только зарегистрированные в реестре изделий» — из настроек.
      // Изменение обрабатывает OnRegistryFilterChanged (см. Designer).
      _registryFilterCheck.Checked = VelumAppConfig.BomExchangeOnlyRegisteredInProductRegistry;

      // Включить кнопку экспорта только если указан каталог обмена.
      UpdateExportButtonState();
      _folderBox.TextChanged += (s, e) => UpdateExportButtonState();

      // Построить колонки списка карточек из настроек выгрузки, затем загрузить строки.
      RebuildColumns();
      LoadDiscrepancyList();
      LoadStructureList();
      LoadAllList();
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
    /// При включённом фильтре «только реестр» строки, файлы которых отсутствуют
    /// в реестре изделий, не показываются и не выгружаются.
    /// </summary>
    private void LoadDiscrepancyList()
    {
      try
      {
        var store = new VelumAssemblyBomMirrorStore();
        store.Load();
        var entries = store.GetDiscrepancyEntries()
            .Where(e => VelumBomExportFilterRules.IsExportable(e.ExternalId))
            .ToList();

        // Счётчики состояния зеркала — из загруженного JSON, без обращения к диску.
        _mirrorTotalCount = store.CountAllEntries();
        _mirrorDiscrepancyCount = store.CountDiscrepancyEntries();
        _mirrorStaleCount = store.CountStaleEntries();

        // Сверка с реестром изделий (по нормализованному пути файла).
        bool filterByRegistry = _registryFilterCheck != null &&
            _registryFilterCheck.Checked;
        _registryIndex = VelumBomExchangeRegistryIndex.Load();
        _hiddenByRegistryCount = 0;
        _notRegisteredCount = 0;
        if (_registryIndex.Available)
        {
          // Реестр доступен: считаем и скрытые (при фильтре), и видимые
          // незарегистрированные (для серой пометки и статус-строки).
          var visible = new List<VelumAssemblyBomMirrorEntry>();
          foreach (VelumAssemblyBomMirrorEntry entry in entries)
          {
            bool registered = _registryIndex.IsRegistered(entry);
            if (!registered)
              _notRegisteredCount++;
            if (!filterByRegistry || registered)
              visible.Add(entry);
            else
              _hiddenByRegistryCount++;
          }
          entries = visible;
        }

        if (_enabledColumns.Count == 0)
          RebuildColumns();

        _lastCards = entries;
        RenderCardsList();

        _cardCount = entries.Count;
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum bomExchange: unable to load discrepancy list: " + ex.Message);
        _listView.Items.Clear();
        _lastCards = null;
        _cardRowValues.Clear();
        _cardCount = 0;
        _hiddenByRegistryCount = 0;
        _notRegisteredCount = 0;
        _mirrorTotalCount = 0;
        _mirrorDiscrepancyCount = 0;
        _mirrorStaleCount = 0;
      }

      UpdateNoteLabel();
    }

    /// <summary>
    /// Построить список карточек в порядке отображения с учётом сортировки по клику.
    /// </summary>
    private void RenderCardsList()
    {
      _listView.BeginUpdate();
      try
      {
        _listView.Items.Clear();
        _cardRowValues.Clear();
        if (_lastCards == null || _enabledColumns.Count == 0)
          return;

        List<VelumAssemblyBomMirrorEntry> ordered = _lastCards;
        if (_cardsSortColumn >= 0 && _cardsSortColumn < _enabledColumns.Count)
        {
          string field = _enabledColumns[_cardsSortColumn].Field;
          int direction = _cardsSortDescending ? -1 : 1;
          ordered = _lastCards
              .OrderBy(e => VelumBomExchangeRowProjector.GetValue(e, _enabledColumns[_cardsSortColumn]) ?? string.Empty,
                  StringComparer.CurrentCultureIgnoreCase)
              .ThenBy(e => e.ExternalId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
              .ToList();
          if (direction < 0)
            ordered.Reverse();
          if (string.IsNullOrEmpty(field))
            ordered = _lastCards;
        }

        foreach (VelumAssemblyBomMirrorEntry entry in ordered)
        {
          string first = VelumBomExchangeRowProjector.GetValue(entry, _enabledColumns[0]);
          var item = new ListViewItem(first ?? string.Empty);
          var values = new string[_enabledColumns.Count];
          values[0] = first ?? string.Empty;
          for (int i = 1; i < _enabledColumns.Count; i++)
          {
            string value = VelumBomExchangeRowProjector.GetValue(entry, _enabledColumns[i]) ?? string.Empty;
            values[i] = value;
            item.SubItems.Add(value);
          }
          _cardRowValues.Add(values);

          // Позиции, файлов которых нет в реестре изделий, показываем серым,
          // чтобы принадлежность к реестру была видна и без фильтра.
          if (_registryIndex != null && _registryIndex.Available &&
              !_registryIndex.IsRegistered(entry))
            item.ForeColor = SystemColors.GrayText;
          _listView.Items.Add(item);
        }
      }
      finally
      {
        _listView.EndUpdate();
      }
    }

    /// <summary>Обработчик клика по заголовку списка карточек: сортировка с разворотом.</summary>
    private void OnCardsColumnClick(object sender, ColumnClickEventArgs e)
    {
      if (e.Column == _cardsSortColumn)
        _cardsSortDescending = !_cardsSortDescending;
      else
      {
        _cardsSortColumn = e.Column;
        _cardsSortDescending = false;
      }

      RenderCardsList();
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

        // Иерархический порядок — общий с CSV-выгрузкой
        // (см. RecipeExecutorHandlersBomExport.GenerateStructureCsv).
        VelumBomExchangeHierarchyRanker ranker =
            VelumBomExchangeHierarchyRanker.Build(structureStore.GetAllEntries());
        List<VelumBomChangeRecord> pending = selection.Records
            .OrderBy(r => ranker.RankOfParentExternalId(r.ParentExternalId))
            .ThenBy(r => r.ParentExternalId, StringComparer.Ordinal)
            .ThenBy(r => r.ChildExternalId, StringComparer.Ordinal)
            .ThenBy(r => r.TimestampUtc)
            .ToList();

        _structureRows = BuildStructureRows(pending, selection, mirrorStore);
        RenderStructureList();

        _structureCount = pending.Count;
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum bomExchange: unable to load structure list: " + ex.Message);
        _structureListView.Items.Clear();
        _structureRows = new List<StructureRowData>();
        _structureCount = 0;
      }

      UpdateNoteLabel();
    }

    /// <summary>
    /// Загрузить весь журнал изменений состава (включая уже выгруженные записи) —
    /// вкладка «Все». Отбор — только по непустому родителю, чтобы строка совпадала
    /// со структурой записи журнала, а не с отбором очереди на выгрузку.
    /// </summary>
    private void LoadAllList()
    {
      try
      {
        var structureStore = new VelumBomStructureStore();
        structureStore.Load();
        var mirrorStore = new VelumAssemblyBomMirrorStore();
        mirrorStore.Load();
        var changeStore = new VelumBomChangeLogStore();
        changeStore.Load();

        List<VelumBomChangeRecord> records = changeStore.GetAll()
            .Where(r => r != null && !string.IsNullOrWhiteSpace(r.ParentExternalId))
            .ToList();

        _allRows = BuildStructureRows(records, null, mirrorStore);
        RenderAllList();
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum bomExchange: unable to load change log list: " + ex.Message);
        _allListView.Items.Clear();
        _allRows = new List<StructureRowData>();
      }

      UpdateNoteLabel();
    }

    /// <summary>
    /// Построить строки вкладок состава: обозначения родителя и компонента,
    /// ISO-ключ времени. Для вкладки «Структура» обозначение родителя берётся из
    /// структуры (selection), для вкладки «Все» — только ExternalId.
    /// </summary>
    /// <param name="records">Записи журнала.</param>
    /// <param name="selection">Отбор структуры (может быть null для вкладки «Все»).</param>
    /// <param name="mirrorStore">Зеркало карточек — источник обозначений компонентов.</param>
    private List<StructureRowData> BuildStructureRows(
        List<VelumBomChangeRecord> records,
        VelumBomExchangeStructureProjector.Selection selection,
        VelumAssemblyBomMirrorStore mirrorStore)
    {
      var rows = new List<StructureRowData>();
      foreach (VelumBomChangeRecord record in records)
      {
        // Обозначение родителя: из структуры, fallback — ExternalId.
        string parentDesignation = record.ParentExternalId;
        VelumBomStructureEntry structureEntry;
        if (selection != null &&
            selection.StructuresByParentExternalId.TryGetValue(
                record.ParentExternalId, out structureEntry) &&
            !string.IsNullOrWhiteSpace(structureEntry.ParentDesignation))
          parentDesignation = structureEntry.ParentDesignation;

        // Обозначение компонента: из зеркала карточек, fallback — ExternalId.
        string childDesignation = record.ChildExternalId;
        VelumAssemblyBomMirrorEntry childMirror = mirrorStore.GetEntry(record.ChildIdentity);
        if (childMirror != null && !string.IsNullOrWhiteSpace(childMirror.Designation))
          childDesignation = childMirror.Designation;

        rows.Add(new StructureRowData
        {
          Record = record,
          ParentText = parentDesignation,
          ChildText = childDesignation,
          IsoSortKey = record.TimestampUtc.ToLocalTime()
              .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
          Exported = record.Exported
        });
      }

      return rows;
    }

    /// <summary>Отобразить строки вкладки «Структура» с учётом фильтра и сортировки.</summary>
    private void RenderStructureList()
    {
      List<StructureRowData> visible = ApplyStructureFilters(_structureRows, _structureFilters);

      List<StructureRowData> ordered = _structureSortColumn < 0
          ? visible
          : SortStructureRows(visible, _structureSortColumn, _structureSortDescending);

      RenderStructureItems(_structureListView, ordered, showState: false);
    }

    /// <summary>Отобразить строки вкладки «Все» с учётом фильтра и сортировки.</summary>
    private void RenderAllList()
    {
      List<StructureRowData> visible = ApplyStructureFilters(_allRows, _allFilters);

      List<StructureRowData> ordered = _allSortColumn < 0
          ? visible
              .OrderBy(r => r.Record.TimestampUtc)
              .ThenBy(r => r.Record.Id, StringComparer.Ordinal)
              .ToList()
          : SortStructureRows(visible, _allSortColumn, _allSortDescending);

      RenderStructureItems(_allListView, ordered, showState: true);
    }

    /// <summary>Заполнить список состава строками (общий код вкладок «Структура» и «Все»).</summary>
    /// <param name="list">Целевой ListView.</param>
    /// <param name="rows">Строки в порядке отображения.</param>
    /// <param name="showState">Добавлять колонку «Состояние» (вкладка «Все»).</param>
    private void RenderStructureItems(ListView list, List<StructureRowData> rows, bool showState)
    {
      list.BeginUpdate();
      try
      {
        list.Items.Clear();
        foreach (StructureRowData row in rows)
        {
          var item = new ListViewItem(row.ParentText);
          item.SubItems.Add(row.ChildText);
          item.SubItems.Add(row.Record.ChildConfiguration ?? string.Empty);
          item.SubItems.Add(row.Record.Quantity.ToString(CultureInfo.InvariantCulture));
          item.SubItems.Add(VelumBomChangeLogStore.RenderAction(row.Record.Action));
          item.SubItems.Add(row.Record.ChildExternalId ?? string.Empty);
          if (showState)
          {
            item.SubItems.Add(row.IsoSortKey);
            item.SubItems.Add(row.Exported ? "Выгружено" : "В очереди");
            if (row.Exported)
              item.ForeColor = SystemColors.GrayText;
          }
          else
          {
            // Момент записи — UTC в хранилище; в колонке — короткое местное время,
            // сортировка и фильтр идут по ISO-ключу (см. IsoSortKey).
            item.SubItems.Add(row.Record.TimestampUtc.ToLocalTime()
                .ToString("g", CultureInfo.CurrentCulture));
          }
          item.Tag = row;
          list.Items.Add(item);
        }
      }
      finally
      {
        list.EndUpdate();
      }
    }

    /// <summary>Отобрать строки состава по выражениям фильтра (И между полями).</summary>
    private static List<StructureRowData> ApplyStructureFilters(
        List<StructureRowData> rows,
        BomExchangeFilterValues filters)
    {
      if (filters == null)
        return new List<StructureRowData>(rows);

      var result = new List<StructureRowData>();
      foreach (StructureRowData row in rows)
      {
        if (!VelumListFilterHelper.Matches(row.ParentText, filters.Parent))
          continue;
        if (!VelumListFilterHelper.Matches(row.ChildText, filters.Child))
          continue;
        if (!VelumListFilterHelper.Matches(
                VelumBomChangeLogStore.RenderAction(row.Record.Action), filters.Action))
          continue;
        if (!VelumListFilterHelper.Matches(row.Record.ChildExternalId, filters.ExternalId))
          continue;
        if (!VelumListFilterHelper.Matches(row.IsoSortKey, filters.Date, row.IsoSortKey))
          continue;
        result.Add(row);
      }

      return result;
    }

    /// <summary>Отсортировать строки состава по колонке отображения.</summary>
    private static List<StructureRowData> SortStructureRows(
        List<StructureRowData> rows,
        int column,
        bool descending)
    {
      List<StructureRowData> sorted = rows.ToList();
      int direction = descending ? -1 : 1;
      sorted.Sort((a, b) =>
      {
        int cmp;
        switch (column)
        {
          case StructColumnParent:
            cmp = string.Compare(a.ParentText, b.ParentText, StringComparison.CurrentCultureIgnoreCase);
            break;
          case StructColumnChild:
            cmp = string.Compare(a.ChildText, b.ChildText, StringComparison.CurrentCultureIgnoreCase);
            break;
          case StructColumnConfig:
            cmp = string.Compare(a.Record.ChildConfiguration, b.Record.ChildConfiguration,
                StringComparison.CurrentCultureIgnoreCase);
            break;
          case StructColumnQuantity:
            cmp = a.Record.Quantity.CompareTo(b.Record.Quantity);
            break;
          case StructColumnAction:
            cmp = string.Compare(
                VelumBomChangeLogStore.RenderAction(a.Record.Action),
                VelumBomChangeLogStore.RenderAction(b.Record.Action),
                StringComparison.CurrentCultureIgnoreCase);
            break;
          case StructColumnExternalId:
            cmp = string.Compare(a.Record.ChildExternalId, b.Record.ChildExternalId,
                StringComparison.OrdinalIgnoreCase);
            break;
          case StructColumnTimestamp:
            cmp = string.Compare(a.IsoSortKey, b.IsoSortKey, StringComparison.Ordinal);
            break;
          case AllColumnState:
            cmp = a.Exported.CompareTo(b.Exported);
            break;
          default:
            cmp = 0;
            break;
        }

        // Стабильный добивочный ключ — сохраняет предсказуемый порядок равных строк.
        if (cmp == 0)
          cmp = a.Record.TimestampUtc.CompareTo(b.Record.TimestampUtc);
        if (cmp == 0)
          cmp = string.Compare(a.Record.Id, b.Record.Id, StringComparison.Ordinal);
        return cmp * direction;
      });
      return sorted;
    }

    /// <summary>Обработчик клика по заголовку списка структуры.</summary>
    private void OnStructureColumnClick(object sender, ColumnClickEventArgs e)
    {
      if (e.Column == _structureSortColumn)
        _structureSortDescending = !_structureSortDescending;
      else
      {
        _structureSortColumn = e.Column;
        _structureSortDescending = false;
      }

      RenderStructureList();
    }

    /// <summary>Обработчик клика по заголовку списка вкладки «Все».</summary>
    private void OnAllColumnClick(object sender, ColumnClickEventArgs e)
    {
      if (e.Column == _allSortColumn)
        _allSortDescending = !_allSortDescending;
      else
      {
        _allSortColumn = e.Column;
        _allSortDescending = false;
      }

      RenderAllList();
    }

    /// <summary>Обновить статус-строку: счётчики зеркала и сколько карточек/строк к выгрузке.</summary>
    private void UpdateNoteLabel()
    {
      string registryNote;
      if (_registryIndex != null && _registryIndex.Available)
      {
        if (_registryFilterCheck != null && _registryFilterCheck.Checked)
        {
          registryNote = " Из них " + _hiddenByRegistryCount +
              " скрыто как незарегистрированные в реестре изделий.";
        }
        else if (_notRegisteredCount > 0)
        {
          registryNote = " Вне реестра изделий: " + _notRegisteredCount +
              " (показаны серым).";
        }
        else
        {
          registryNote = string.Empty;
        }
      }
      else
      {
        registryNote = " Сверка с реестром изделий недоступна.";
      }

      // Счётчики зеркала — без обращения к диску, обновляются при загрузке списка.
      string staleNote = _mirrorStaleCount > 0
          ? " Устаревших (файл не найден): " + _mirrorStaleCount + "."
          : string.Empty;

      noteLabel.Text =
          "Зеркало: всего " + _mirrorTotalCount +
          ", к выгрузке " + _mirrorDiscrepancyCount +
          "." + staleNote +
          " Карточек: " + _cardCount +
          ". Строк состава: " + _structureCount + "." +
          registryNote +
          " Компоненты с пустым ExternalId или «0» пропускаются при экспорте.";
    }

    /// <summary>Переключение фильтра «только зарегистрированные в реестре изделий».</summary>
    private void OnRegistryFilterChanged(object sender, EventArgs e)
    {
      VelumAppConfig.SetBomExchangeOnlyRegisteredInProductRegistry(_registryFilterCheck.Checked);
      LoadDiscrepancyList();
    }

    /// <summary>
    /// «Применить»: считать условия из полей и пересобрать список текущей вкладки.
    /// Пересчёт только по нажатию — ввод в поле список не трогает.
    /// </summary>
    private void OnFilterApplyClick(object sender, EventArgs e)
    {
      BomExchangeFilterValues values = ReadFilterValues();

      if (IsAllTabActive())
      {
        _allFilters = values;
        RenderAllList();
      }
      else
      {
        _structureFilters = values;
        RenderStructureList();
      }

      UpdateNoteLabel();
    }

    /// <summary>«Сброс фильтра»: очистить поля условий и показать все строки.</summary>
    private void OnFilterResetClick(object sender, EventArgs e)
    {
      ClearFilterFields();

      if (IsAllTabActive())
      {
        _allFilters = BomExchangeFilterValues.Empty();
        RenderAllList();
      }
      else
      {
        _structureFilters = BomExchangeFilterValues.Empty();
        RenderStructureList();
      }

      UpdateNoteLabel();
    }

    /// <summary>Активна ли вкладка «Все» (для адресации фильтра и счётчиков).</summary>
    private bool IsAllTabActive()
    {
      return _tabs != null && _tabs.SelectedTab == _allTab;
    }

    /// <summary>Считать введённые условия фильтра из полей панели.</summary>
    private BomExchangeFilterValues ReadFilterValues()
    {
      return new BomExchangeFilterValues
      {
        Parent = _filterParentBox.Text,
        Child = _filterChildBox.Text,
        Action = _filterActionBox.Text,
        ExternalId = _filterExternalIdBox.Text,
        Date = _filterDateBox.Text
      };
    }

    /// <summary>Очистить поля панели фильтров.</summary>
    private void ClearFilterFields()
    {
      _filterParentBox.Text = string.Empty;
      _filterChildBox.Text = string.Empty;
      _filterActionBox.Text = string.Empty;
      _filterExternalIdBox.Text = string.Empty;
      _filterDateBox.Text = string.Empty;
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
          LoadAllList();
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
