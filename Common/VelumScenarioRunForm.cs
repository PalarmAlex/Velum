using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ISIDA.Scenarios;
using Velum.Configuration;
using Velum.Isida;

namespace Velum.UI
{
  /// <summary>
  /// Диалог прогонов тестовых сценариев оператора в Velum: выбор сценария из реестра,
  /// запуск/остановка, живой статус и итог прогона. Поддерживает одиночные и групповые сценарии.
  /// </summary>
  internal sealed class VelumScenarioRunForm : Form
  {
    private static VelumScenarioRunForm _openInstance;

    private readonly TabControl _tabControl;
    private readonly ListView _listScenarios;
    private readonly ListView _listGroups;
    private readonly Button _btnReload;
    private readonly Button _btnRun;
    private readonly Button _btnStop;
    private readonly Button _btnOpenReport;
    private readonly Label _lblStatus;
    private readonly TextBox _txtLog;
    private readonly ToolTip _toolTip;

    // Путь к последнему сгенерированному HTML-отчёту (для кнопки «Открыть отчёт»).
    private string _lastReportPath = "";

    // Одиночные сценарии.
    private List<ScenarioHeader> _singleHeaders = new List<ScenarioHeader>();
    private int _singleSortColumn = 0;
    private bool _singleSortAscending = true;
    private string[] _singleFilters = new string[4];

    // Групповые сценарии.
    private List<VelumScenarioGroupHeader> _groupHeaders = new List<VelumScenarioGroupHeader>();
    private int _groupSortColumn = 0;
    private bool _groupSortAscending = true;
    private string[] _groupFilters = new string[4];

    /// <summary>Показывает диалог (повторно активирует уже открытый).</summary>
    internal static new void Show(IWin32Window owner)
    {
      if (_openInstance != null && !_openInstance.IsDisposed)
      {
        _openInstance.Activate();
        _openInstance.BringToFront();
        return;
      }

      var form = new VelumScenarioRunForm();
      VelumFormIcon.Apply(form);
      _openInstance = form;
      try
      {
        form.ShowDialog(owner);
      }
      finally
      {
        _openInstance = null;
      }
    }

    private VelumScenarioRunForm()
    {
      Text = "Прогон сценариев Velum";
      StartPosition = FormStartPosition.CenterParent;
      ShowInTaskbar = false;
      MaximizeBox = true;
      MinimizeBox = false;
      ClientSize = new Size(800, 560);
      Font = SystemFonts.MessageBoxFont;

      // Иконка формы — List.png.
      Icon = LoadListIcon();

      // Верхняя панель кнопок.
      var top = new Panel { Dock = DockStyle.Top, Height = 32, Padding = new Padding(4, 4, 0, 0) };
      _btnReload = MakeButton("Обновить", 12);
      _btnRun = MakeButton("Запустить", 108);
      _btnStop = MakeButton("Стоп", 204);
      _btnStop.Enabled = false;
      _btnOpenReport = MakeButton("Отчёт", 300);
      _btnOpenReport.Enabled = false;
      top.Controls.Add(_btnReload);
      top.Controls.Add(_btnRun);
      top.Controls.Add(_btnStop);
      top.Controls.Add(_btnOpenReport);

      // Вкладки.
      _tabControl = new TabControl { Dock = DockStyle.Fill };
      TabPage tabSingle = new TabPage("Одиночные сценарии");
      TabPage tabGroup = new TabPage("Групповые сценарии");
      _tabControl.TabPages.Add(tabSingle);
      _tabControl.TabPages.Add(tabGroup);

      // ListView одиночных сценариев.
      _listScenarios = CreateScenarioListView();
      _listScenarios.Columns.Add("#", 48, HorizontalAlignment.Left);
      _listScenarios.Columns.Add("Название", 260, HorizontalAlignment.Left);
      _listScenarios.Columns.Add("Стадия", 60, HorizontalAlignment.Center);
      _listScenarios.Columns.Add("Описание", 400, HorizontalAlignment.Left);
      _listScenarios.ColumnClick += OnSingleColumnClick;
      Panel filterSingle = CreateFilterPanel(_singleFilters, OnSingleFilterChanged);
      tabSingle.Controls.Add(_listScenarios);
      tabSingle.Controls.Add(filterSingle);
      _listScenarios.MouseMove += OnListViewMouseMove;
      _listScenarios.MouseLeave += OnListViewMouseLeave;

      // ListView групповых сценариев.
      _listGroups = CreateGroupListView();
      _listGroups.Columns.Add("#", 48, HorizontalAlignment.Left);
      _listGroups.Columns.Add("Название", 280, HorizontalAlignment.Left);
      _listGroups.Columns.Add("Описание", 380, HorizontalAlignment.Left);
      _listGroups.Columns.Add("Членов", 70, HorizontalAlignment.Center);
      _listGroups.ColumnClick += OnGroupColumnClick;
      _listGroups.MouseMove += OnListViewMouseMove;
      _listGroups.MouseLeave += OnListViewMouseLeave;
      tabGroup.Controls.Add(_listGroups);
      Panel filterGroup = CreateFilterPanel(_groupFilters, OnGroupFilterChanged);
      tabGroup.Controls.Add(filterGroup);

      // Статус и лог.
      _lblStatus = new Label
      {
        Dock = DockStyle.Bottom,
        Height = 22,
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(6, 0, 0, 0),
        Text = "Готов."
      };

      _txtLog = new TextBox
      {
        Dock = DockStyle.Bottom,
        Height = 120,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        BackColor = Color.White,
        Font = new Font(FontFamily.GenericMonospace, 8.25f)
      };

      _toolTip = new ToolTip { AutoPopDelay = 15000, InitialDelay = 500, ReshowDelay = 200, ShowAlways = true };

      Controls.Add(_tabControl);
      Controls.Add(_txtLog);
      Controls.Add(_lblStatus);
      Controls.Add(top);

      _btnReload.Click += (s, e) =>
      {
        LoadSingleRegistry();
        LoadGroupRegistry();
      };
      _btnRun.Click += (s, e) => RunSelected();
      _btnStop.Click += (s, e) => VelumScenarioHost.Stop();
      _btnOpenReport.Click += (s, e) => OpenLastReport();

      VelumScenarioHost.RunningStateChanged += OnRunningStateChanged;
      VelumScenarioHost.RunFinished += OnRunFinished;
      VelumScenarioHost.ReportGenerated += OnReportGenerated;
      FormClosed += (s, e) =>
      {
        VelumScenarioHost.RunningStateChanged -= OnRunningStateChanged;
        VelumScenarioHost.RunFinished -= OnRunFinished;
        VelumScenarioHost.ReportGenerated -= OnReportGenerated;
        _toolTip?.Dispose();
        _openInstance = null;
      };

      Load += (s, e) =>
      {
        LoadSingleRegistry();
        LoadGroupRegistry();
      };
    }

    private void OnListViewMouseMove(object sender, MouseEventArgs e)
    {
      if (sender is ListView list)
        UpdateTooltip(list, e);
    }

    private void OnListViewMouseLeave(object sender, EventArgs e)
    {
      if (sender is ListView list)
        _toolTip.SetToolTip(list, "");
    }

    private static Icon LoadListIcon()
    {
      try
      {
        string dir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        string[] candidates =
        {
          Path.Combine(dir, "icons", "List.png"),
          Path.Combine(dir, "List.png"),
        };
        foreach (string path in candidates)
        {
          if (File.Exists(path))
          {
            using (var bmp = new Bitmap(path))
              return Icon.FromHandle(bmp.GetHicon());
          }
        }
      }
      catch { }
      return null;
    }

    private static ListView CreateScenarioListView()
    {
      return new ListView
      {
        Dock = DockStyle.Fill,
        View = View.Details,
        FullRowSelect = true,
        MultiSelect = false,
        HideSelection = false,
        GridLines = true,
        LabelWrap = false
      };
    }

    private static ListView CreateGroupListView()
    {
      return new ListView
      {
        Dock = DockStyle.Fill,
        View = View.Details,
        FullRowSelect = true,
        MultiSelect = false,
        HideSelection = false,
        GridLines = true,
        LabelWrap = false
      };
    }

    /// <summary>Панель фильтров по столбцам ListView.</summary>
    private static Panel CreateFilterPanel(string[] filters, Action<string[]> onFilterChanged)
    {
      var panel = new Panel { Dock = DockStyle.Top, Height = 28, Padding = new Padding(2, 0, 0, 0) };
      for (int i = 0; i < filters.Length; i++)
      {
        var tb = new TextBox
        {
          Location = new Point(i * 200 + 4, 3),
          Width = 190,
          Height = 22,
          Font = SystemFonts.MessageBoxFont,
          Text = filters[i],
          Tag = i
        };
        tb.TextChanged += (s, e) =>
        {
          filters[(int)tb.Tag] = tb.Text;
          onFilterChanged(filters);
        };
        panel.Controls.Add(tb);
      }
      return panel;
    }

    private void OnSingleFilterChanged(string[] filters)
    {
      ApplySingleFilterAndSort();
    }

    private void OnGroupFilterChanged(string[] filters)
    {
      ApplyGroupFilterAndSort();
    }

    // ===== Сортировка по клику на столбец =====

    private void OnSingleColumnClick(object sender, ColumnClickEventArgs e)
    {
      if (e.Column == _singleSortColumn)
        _singleSortAscending = !_singleSortAscending;
      else
      {
        _singleSortColumn = e.Column;
        _singleSortAscending = true;
      }
      ApplySingleFilterAndSort();
    }

    private void OnGroupColumnClick(object sender, ColumnClickEventArgs e)
    {
      if (e.Column == _groupSortColumn)
        _groupSortAscending = !_groupSortAscending;
      else
      {
        _groupSortColumn = e.Column;
        _groupSortAscending = true;
      }
      ApplyGroupFilterAndSort();
    }

    private void ApplySingleFilterAndSort()
    {
      var filtered = _singleHeaders.AsEnumerable();

      // Фильтрация.
      if (!string.IsNullOrWhiteSpace(_singleFilters[0]))
      {
        string f = _singleFilters[0].ToLowerInvariant();
        filtered = filtered.Where(h => h.Id.ToString(CultureInfo.InvariantCulture).Contains(f));
      }
      if (!string.IsNullOrWhiteSpace(_singleFilters[1]))
      {
        string f = _singleFilters[1].ToLowerInvariant();
        filtered = filtered.Where(h => (h.Title ?? "").ToLowerInvariant().Contains(f));
      }
      if (!string.IsNullOrWhiteSpace(_singleFilters[2]))
      {
        string f = _singleFilters[2].ToLowerInvariant();
        filtered = filtered.Where(h =>
            (h.PreRunTargetStage >= 0 ? h.PreRunTargetStage.ToString(CultureInfo.InvariantCulture) : "—")
            .ToLowerInvariant().Contains(f));
      }
      if (!string.IsNullOrWhiteSpace(_singleFilters[3]))
      {
        string f = _singleFilters[3].ToLowerInvariant();
        filtered = filtered.Where(h => (h.Description ?? "").ToLowerInvariant().Contains(f));
      }

      List<ScenarioHeader> list = filtered.ToList();
      int col = _singleSortColumn;
      bool asc = _singleSortAscending;
      list.Sort((a, b) =>
      {
        string va = GetSingleSortValue(a, col);
        string vb = GetSingleSortValue(b, col);
        int cmp = string.Compare(va, vb, StringComparison.Ordinal);
        return asc ? cmp : -cmp;
      });

      SafeUi(() =>
      {
        _listScenarios.BeginUpdate();
        _listScenarios.Items.Clear();
        foreach (ScenarioHeader h in list)
        {
          var item = new ListViewItem(h.Id.ToString(CultureInfo.InvariantCulture));
          item.SubItems.Add(h.Title ?? "");
          item.SubItems.Add(h.PreRunTargetStage >= 0
              ? h.PreRunTargetStage.ToString(CultureInfo.InvariantCulture)
              : "—");
          // Сохраняем полное описание в SubItem.Tag для ToolTip.
          string descShort = h.DescriptionShort ?? "";
          var subDesc = new ListViewItem.ListViewSubItem { Text = descShort, Tag = h.Description };
          _listScenarios.Columns[3].DisplayIndex = 3;
          item.SubItems.Add(subDesc);
          item.Tag = h.Id;
          _listScenarios.Items.Add(item);
        }
        _listScenarios.EndUpdate();
      });
    }

    private void ApplyGroupFilterAndSort()
    {
      var filtered = _groupHeaders.AsEnumerable();

      if (!string.IsNullOrWhiteSpace(_groupFilters[0]))
      {
        string f = _groupFilters[0].ToLowerInvariant();
        filtered = filtered.Where(h => h.Id.ToString(CultureInfo.InvariantCulture).Contains(f));
      }
      if (!string.IsNullOrWhiteSpace(_groupFilters[1]))
      {
        string f = _groupFilters[1].ToLowerInvariant();
        filtered = filtered.Where(h => (h.Title ?? "").ToLowerInvariant().Contains(f));
      }
      if (!string.IsNullOrWhiteSpace(_groupFilters[2]))
      {
        string f = _groupFilters[2].ToLowerInvariant();
        filtered = filtered.Where(h => (h.Description ?? "").ToLowerInvariant().Contains(f));
      }
      if (!string.IsNullOrWhiteSpace(_groupFilters[3]))
      {
        string f = _groupFilters[3].ToLowerInvariant();
        filtered = filtered.Where(h =>
            VelumScenarioGroupStorage.GetMemberCount(h.Id).ToString(CultureInfo.InvariantCulture)
            .Contains(f));
      }

      List<VelumScenarioGroupHeader> list = filtered.ToList();
      int col = _groupSortColumn;
      bool asc = _groupSortAscending;
      list.Sort((a, b) =>
      {
        string va = GetGroupSortValue(a, col);
        string vb = GetGroupSortValue(b, col);
        int cmp = string.Compare(va, vb, StringComparison.Ordinal);
        return asc ? cmp : -cmp;
      });

      SafeUi(() =>
      {
        _listGroups.BeginUpdate();
        _listGroups.Items.Clear();
        foreach (VelumScenarioGroupHeader h in list)
        {
          int memberCount = VelumScenarioGroupStorage.GetMemberCount(h.Id);
          var item = new ListViewItem(h.Id.ToString(CultureInfo.InvariantCulture));
          item.SubItems.Add(h.Title ?? "");
          item.SubItems.Add(h.DescriptionShort ?? "");
          item.SubItems.Add(memberCount.ToString(CultureInfo.InvariantCulture));
          item.Tag = h.Id;
          _listGroups.Items.Add(item);
        }
        _listGroups.EndUpdate();
      });
    }

    private static string GetSingleSortValue(ScenarioHeader h, int column)
    {
      switch (column)
      {
        case 0: return h.Id.ToString(CultureInfo.InvariantCulture);
        case 1: return h.Title ?? "";
        case 2: return (h.PreRunTargetStage >= 0 ? h.PreRunTargetStage.ToString(CultureInfo.InvariantCulture) : "—");
        case 3: return h.Description ?? "";
        default: return "";
      }
    }

    private static string GetGroupSortValue(VelumScenarioGroupHeader h, int column)
    {
      switch (column)
      {
        case 0: return h.Id.ToString(CultureInfo.InvariantCulture);
        case 1: return h.Title ?? "";
        case 2: return h.Description ?? "";
        case 3: return VelumScenarioGroupStorage.GetMemberCount(h.Id).ToString(CultureInfo.InvariantCulture);
        default: return "";
      }
    }

    // ===== Загрузка данных =====

    private void LoadSingleRegistry()
    {
      try
      {
        _singleHeaders = VelumScenarioStorage.LoadRegistry();
        ApplySingleFilterAndSort();
        AppendLog("Загружено одиночных сценариев: "
            + _singleHeaders.Count.ToString(CultureInfo.InvariantCulture)
            + " (" + VelumScenarioStorage.RootFolder + ")");
      }
      catch (Exception ex)
      {
        AppendLog("Ошибка загрузки реестра одиночных сценариев: " + ex.Message);
      }
    }

    private void LoadGroupRegistry()
    {
      try
      {
        _groupHeaders = VelumScenarioGroupStorage.LoadGroupRegistry();
        ApplyGroupFilterAndSort();
        AppendLog("Загружено групповых сценариев: "
            + _groupHeaders.Count.ToString(CultureInfo.InvariantCulture));
      }
      catch (Exception ex)
      {
        AppendLog("Ошибка загрузки реестра групповых сценариев: " + ex.Message);
      }
    }

    // ===== Запуск прогона =====

    private void RunSelected()
    {
      ListView list = GetCurrentListView();
      if (list == null || list.SelectedItems.Count == 0)
      {
        AppendLog("Выберите сценарий или группу в списке.");
        return;
      }

      bool isGroup = list == _listGroups;
      int id = (int)list.SelectedItems[0].Tag;

      if (isGroup)
      {
        string error = VelumScenarioHost.TryStartGroup(id, VelumAppConfig.ScenarioReportsFolderPath);
        if (error != null)
        {
          AppendLog("Запуск группы отменён: " + error);
          return;
        }
        AppendLog("Запущен групповой прогон #" + id.ToString(CultureInfo.InvariantCulture)
            + " «" + GetGroupTitle(id) + "», членов: "
            + VelumScenarioGroupStorage.GetMemberCount(id).ToString(CultureInfo.InvariantCulture));
      }
      else
      {
        int scenarioId = id;
        ScenarioDocument doc;
        try
        {
          doc = VelumScenarioStorage.LoadScenario(scenarioId);
        }
        catch (Exception ex)
        {
          AppendLog("Не удалось загрузить сценарий #"
              + scenarioId.ToString(CultureInfo.InvariantCulture) + ": " + ex.Message);
          return;
        }

        string error = VelumScenarioHost.TryStart(doc);
        if (error != null)
        {
          AppendLog("Запуск отменён: " + error);
          return;
        }

        AppendLog("Запущен прогон #" + scenarioId.ToString(CultureInfo.InvariantCulture)
            + " «" + (doc.Header?.Title ?? "") + "», шагов: "
            + (doc.Lines?.Count ?? 0).ToString(CultureInfo.InvariantCulture));
      }
    }

    private string GetGroupTitle(int groupId)
    {
      try
      {
        var registry = VelumScenarioGroupStorage.LoadGroupRegistry();
        var h = registry.Find(x => x.Id == groupId);
        return h?.Title ?? "";
      }
      catch
      {
        return "";
      }
    }

    private ListView GetCurrentListView()
    {
      if (_tabControl.SelectedIndex == 0)
        return _listScenarios;
      return _listGroups;
    }

    // ===== Статус и лог =====

    private void OnRunningStateChanged()
    {
      SafeUi(() =>
      {
        bool busy = VelumScenarioHost.IsBusy;
        _btnRun.Enabled = !busy;
        _btnStop.Enabled = busy;
        _btnReload.Enabled = !busy;
        _btnOpenReport.Enabled = !busy && !string.IsNullOrEmpty(_lastReportPath)
            && File.Exists(_lastReportPath);

        if (busy)
        {
          string batch = VelumScenarioHost.BatchCurrentMemberTitle;
          if (!string.IsNullOrEmpty(batch))
            _lblStatus.Text = "Прогон выполняется… [" + batch + "]";
          else
            _lblStatus.Text = "Прогон выполняется…";
        }
        else
        {
          _lblStatus.Text = "Готов.";
        }
      });
    }

    private void OnRunFinished(OperatorScenarioCompletedEventArgs e)
    {
      SafeUi(() =>
      {
        string outcome = e.Success
            ? "успех"
            : (e.AbortedByUser
                ? "остановлен пользователем"
                : (e.AbortedByPulsationStop ? "прерван остановкой пульса" : "ошибка"));
        AppendLog("Прогон завершён: " + outcome
            + "; пульсов: " + e.ElapsedPulses.ToString(CultureInfo.InvariantCulture)
            + "; длительность, с: " + e.ElapsedWallTime.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture));
        if (!string.IsNullOrEmpty(e.ErrorMessage))
          AppendLog("Ошибка: " + e.ErrorMessage);
      });
    }

    private void OnReportGenerated(string reportPath)
    {
      SafeUi(() =>
      {
        _lastReportPath = reportPath;
        _btnOpenReport.Enabled = !VelumScenarioHost.IsBusy && File.Exists(reportPath);
        AppendLog("HTML-отчёт: " + reportPath);
      });
    }

    private void OpenLastReport()
    {
      if (string.IsNullOrEmpty(_lastReportPath) || !File.Exists(_lastReportPath))
      {
        AppendLog("Файл отчёта не найден: " + (_lastReportPath ?? "(не указан)"));
        return;
      }
      try
      {
        Process.Start(new ProcessStartInfo
        {
          FileName = _lastReportPath,
          UseShellExecute = true
        });
      }
      catch (Exception ex)
      {
        AppendLog("Не удалось открыть отчёт: " + ex.Message);
      }
    }

    private void SafeUi(Action action)
    {
      if (IsDisposed)
        return;
      if (InvokeRequired)
      {
        try { BeginInvoke(action); } catch { /* форма закрывается */ }
        return;
      }
      action();
    }

    private void AppendLog(string message)
    {
      SafeUi(() =>
      {
        _txtLog.AppendText(DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture)
            + "  " + message + Environment.NewLine);
      });
    }

    private static Button MakeButton(string text, int left)
    {
      return new Button
      {
        Text = text,
        Left = left,
        Top = 4,
        Width = 88,
        Height = 24
      };
    }

    // ===== ToolTip для длинных ячеек =====

    private void UpdateTooltip(ListView list, MouseEventArgs e)
    {
      ListViewHitTestInfo hit = list.HitTest(e.Location);
      if (hit.Item == null || hit.SubItem == null)
      {
        _toolTip.SetToolTip(list, "");
        return;
      }

      // Полное значение хранится в Tag (если есть), иначе — Text.
      string text = hit.SubItem.Tag as string ?? hit.SubItem.Text;
      if (string.IsNullOrEmpty(text))
      {
        _toolTip.SetToolTip(list, "");
        return;
      }

      // Показываем, если текст не помещается или содержит переносы.
      if (text.Contains("\n") || text.Length > hit.SubItem.Bounds.Width / 6)
      {
        _toolTip.SetToolTip(list, text.Replace("\n", Environment.NewLine));
      }
      else
      {
        _toolTip.SetToolTip(list, "");
      }
    }
  }
}