using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Velum.Isida.Logs;

namespace Velum.UI.Logs
{
  /// <summary>
  /// Просмотр логов агента: три вкладки («Логи системы», «Логи стилей», «Логи параметров»),
  /// на каждой — чек-список файловых сессий и запуск просмотра HTML-отчётом.
  /// </summary>
  internal sealed partial class VelumLogsForm : Form
  {
    private sealed class SessionListItem
    {
      public VelumLogFileSessionInfo Info;
      public string Label;
      public bool Checked;

      public override string ToString()
      {
        return Label ?? string.Empty;
      }
    }

    private readonly List<SessionListItem> _systemSessions = new List<SessionListItem>();
    private readonly List<SessionListItem> _styleSessions = new List<SessionListItem>();
    private readonly List<SessionListItem> _parameterSessions = new List<SessionListItem>();
    private bool _loading;
    private string _filterText = string.Empty;

    /// <summary>Конструктор для конструктора форм Visual Studio.</summary>
    public VelumLogsForm()
    {
      InitializeComponent();
      VelumFormHelp.Bind(this, VelumHelpTopics.Logs);
    }

    /// <summary>Публичный конструктор формы логов.</summary>
    public VelumLogsForm(bool _)
        : this()
    {
      Icon icon = VelumFormIcon.TryLoad();
      if (icon != null)
        Icon = icon;

      var tip = new ToolTip();
      tip.SetToolTip(_txtFilter, "Фильтр по подписи сессии на активной вкладке (по вхождению текста)");
      tip.SetToolTip(_btnFilterClear, "Очистить фильтр");
      tip.SetToolTip(_btnRefresh, "Перечитать список сессий из файлов логов");
      tip.SetToolTip(_btnSelectAll, "Выделить все видимые сессии на активной вкладке");
      tip.SetToolTip(_btnClearAll, "Снять выделение с видимых сессий на активной вкладке");
      tip.SetToolTip(_btnView, "Сформировать HTML-отчёт по выделенным сессиям");
      tip.SetToolTip(_btnClear, "Удалить выделенные записи из лога и связанные с ними отчёты");
      tip.SetToolTip(_btnOpenFolder, "Открыть каталог логов");
      tip.SetToolTip(_btnClose, "Закрыть окно");

      _clbSystem.ItemCheck += OnSessionItemCheck;
      _clbStyles.ItemCheck += OnSessionItemCheck;
      _clbParameters.ItemCheck += OnSessionItemCheck;
      _txtFilter.TextChanged += OnFilterTextChanged;
      _btnFilterClear.Click += OnFilterClearClick;

      ReloadSessions();
    }

    private void ReloadSessions()
    {
      _loading = true;
      try
      {
        LoadSessions(_systemSessions, VelumAgentLogFileSessions.ListFileSessions());
        LoadSessions(_styleSessions, VelumStyleLogFileSessions.ListFileSessions());
        LoadSessions(_parameterSessions, VelumParameterLogFileSessions.ListFileSessions());
        ApplyFilterToAllTabs();
      }
      finally
      {
        _loading = false;
      }
      RefreshButtons();
    }

    private static void LoadSessions(
        List<SessionListItem> target,
        IReadOnlyList<VelumLogFileSessionInfo> sessions)
    {
      target.Clear();
      foreach (VelumLogFileSessionInfo info in sessions ?? Array.Empty<VelumLogFileSessionInfo>())
      {
        target.Add(new SessionListItem
        {
          Info = info,
          Label = info.BuildDisplayLabel(),
          Checked = false
        });
      }
    }

    private void OnFilterTextChanged(object sender, EventArgs e)
    {
      _filterText = (_txtFilter.Text ?? string.Empty).Trim();
      ApplyFilterToAllTabs();
      RefreshButtons();
    }

    private void OnFilterClearClick(object sender, EventArgs e)
    {
      if (_txtFilter.TextLength == 0)
        return;
      _txtFilter.Text = string.Empty;
    }

    /// <summary>Применяет текущий фильтр ко всем трём вкладкам (сохраняя отметки).</summary>
    private void ApplyFilterToAllTabs()
    {
      _loading = true;
      try
      {
        ApplyFilterToList(_clbSystem, _systemSessions);
        ApplyFilterToList(_clbStyles, _styleSessions);
        ApplyFilterToList(_clbParameters, _parameterSessions);
      }
      finally
      {
        _loading = false;
      }
    }

    private void ApplyFilterToList(CheckedListBox list, List<SessionListItem> items)
    {
      if (list == null)
        return;

      // Запоминаем актуальные отметки перед перестроением.
      var visible = GetVisibleItems(list);
      for (int i = 0; i < visible.Count; i++)
        visible[i].Checked = list.GetItemChecked(i);

      list.Items.Clear();
      foreach (SessionListItem item in items)
      {
        if (!MatchesFilter(item))
          continue;
        list.Items.Add(item, item.Checked);
      }
    }

    private bool MatchesFilter(SessionListItem item)
    {
      if (string.IsNullOrEmpty(_filterText))
        return true;
      string label = item?.Label ?? string.Empty;
      return label.IndexOf(_filterText, StringComparison.CurrentCultureIgnoreCase) >= 0;
    }

    private static List<SessionListItem> GetVisibleItems(CheckedListBox list)
    {
      var result = new List<SessionListItem>();
      if (list == null)
        return result;
      foreach (object obj in list.Items)
      {
        var item = obj as SessionListItem;
        if (item != null)
          result.Add(item);
      }
      return result;
    }

    private void OnSessionItemCheck(object sender, ItemCheckEventArgs e)
    {
      if (_loading)
        return;
      // Событие ItemCheck приходит ДО применения нового состояния к контролу,
      // поэтому модель (SessionListItem.Checked) обновляем явно из e.NewValue —
      // иначе CollectSelectedIndices, читающий модель, не увидит выбор флажком.
      var list = sender as CheckedListBox;
      if (list != null && e.Index >= 0 && e.Index < list.Items.Count)
      {
        var item = list.Items[e.Index] as SessionListItem;
        if (item != null)
          item.Checked = e.NewValue == CheckState.Checked;
      }
      // Кнопки зависят от числа выделенных — обновляем после применения состояния.
      if (list != null && list.IsHandleCreated)
        list.BeginInvoke((MethodInvoker)RefreshButtons);
    }

    private void RefreshButtons()
    {
      int checkedCount = CountChecked(GetActiveList());
      if (_btnView != null && !_btnView.IsDisposed)
        _btnView.Enabled = checkedCount > 0;
      if (_btnClear != null && !_btnClear.IsDisposed)
        _btnClear.Enabled = checkedCount > 0;
    }

    private static int CountChecked(CheckedListBox list)
    {
      return list == null ? 0 : list.CheckedItems.Count;
    }

    private CheckedListBox GetActiveList()
    {
      if (_tabs == null)
        return _clbSystem;
      switch (_tabs.SelectedIndex)
      {
        case 1:
          return _clbStyles;
        case 2:
          return _clbParameters;
        default:
          return _clbSystem;
      }
    }

    private List<SessionListItem> GetActiveItems()
    {
      switch (_tabs != null ? _tabs.SelectedIndex : 0)
      {
        case 1:
          return _styleSessions;
        case 2:
          return _parameterSessions;
        default:
          return _systemSessions;
      }
    }

    /// <summary>Индексы выделенных сессий на активной вкладке (по всему списку, не только видимому).</summary>
    private List<int> CollectSelectedIndices()
    {
      return VelumLogSessionRules.CollectSelectedSessionIndices(
          GetActiveItems(),
          item => item.Checked && item.Info != null,
          item => item.Info.SessionIndex);
    }

    private void OnTabChanged(object sender, EventArgs e)
    {
      RefreshButtons();
    }

    private void OnRefreshClick(object sender, EventArgs e)
    {
      ReloadSessions();
    }

    private void OnSelectAllClick(object sender, EventArgs e)
    {
      SetAllChecked(true);
    }

    private void OnClearAllClick(object sender, EventArgs e)
    {
      SetAllChecked(false);
    }

    private void SetAllChecked(bool value)
    {
      CheckedListBox list = GetActiveList();
      if (list == null)
        return;
      List<SessionListItem> items = GetActiveItems();
      _loading = true;
      try
      {
        // Меняем только видимые (отфильтрованные) строки.
        foreach (SessionListItem item in GetVisibleItems(list))
          item.Checked = value;
        for (int i = 0; i < list.Items.Count; i++)
          list.SetItemChecked(i, value);
      }
      finally
      {
        _loading = false;
      }
      RefreshButtons();
    }

    private void OnViewClick(object sender, EventArgs e)
    {
      int tabIndex = _tabs != null ? _tabs.SelectedIndex : 0;
      List<int> indices = CollectSelectedIndices();
      if (indices.Count == 0)
      {
        MessageBox.Show(
            this,
            "Не выбрана ни одна сессия.",
            Text,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      string html;
      string prefix;
      try
      {
        switch (tabIndex)
        {
          case 1:
            html = VelumLogsReportHtmlBuilder.BuildStyleLogHtml(
                VelumStyleLogFileSessions.LoadMergedSessions(indices));
            prefix = "Логи_стилей";
            break;
          case 2:
            html = VelumLogsReportHtmlBuilder.BuildParameterLogHtml(
                VelumParameterLogFileSessions.LoadMergedSessions(indices));
            prefix = "Логи_параметров";
            break;
          default:
            html = VelumLogsReportHtmlBuilder.BuildSystemLogHtml(
                VelumAgentLogFileSessions.LoadMergedSessions(indices));
            prefix = "Логи_системы";
            break;
        }
      }
      catch (Exception ex)
      {
        MessageBox.Show(
            this,
            "Не удалось сформировать отчёт:\n" + ex.Message,
            Text,
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
        return;
      }

      // Имя отчёта привязывается к времени начала первой (самой ранней) из выделенных
      // сессий — так отчёт можно найти при очистке этих записей.
      DateTime sessionStart = GetSelectedSessionStart(indices);
      string folder = VelumLogsReportHtmlBuilder.ReportsFolderPath;
      string path;
      try
      {
        Directory.CreateDirectory(folder);
        path = Path.Combine(
            folder,
            VelumLogsReportHtmlBuilder.BuildSessionFileName(prefix, sessionStart));
        File.WriteAllText(path, html, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
      }
      catch (Exception ex)
      {
        MessageBox.Show(
            this,
            "Не удалось сохранить отчёт:\n" + ex.Message,
            Text,
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
        return;
      }

      DialogResult open = MessageBox.Show(
          this,
          "Отчёт сохранён:\n" + path + "\n\nОткрыть отчёт в браузере?",
          Text,
          MessageBoxButtons.YesNo,
          MessageBoxIcon.Information);
      if (open != DialogResult.Yes)
        return;

      try
      {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
          FileName = path,
          UseShellExecute = true
        });
      }
      catch (Exception ex)
      {
        MessageBox.Show(
            this,
            "Не удалось открыть отчёт:\n" + ex.Message,
            Text,
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
      }
    }

    private void OnOpenFolderClick(object sender, EventArgs e)
    {
      string folder = VelumLogPaths.LogsFolderPath;
      try
      {
        if (!Directory.Exists(folder))
          Directory.CreateDirectory(folder);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
          FileName = folder,
          UseShellExecute = true
        });
      }
      catch (Exception ex)
      {
        MessageBox.Show(
            this,
            "Не удалось открыть каталог логов:\n" + ex.Message,
            Text,
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
      }
    }

    /// <summary>Префикс имени отчёта по индексу вкладки (система/стили/параметры).</summary>
    private static string GetTabReportPrefix(int tabIndex)
    {
      switch (tabIndex)
      {
        case 1:
          return "Логи_стилей";
        case 2:
          return "Логи_параметров";
        default:
          return "Логи_системы";
      }
    }

    /// <summary>Время начала самой ранней из выбранных сессий активной вкладки.</summary>
    private DateTime GetSelectedSessionStart(IEnumerable<int> indices)
    {
      var wanted = new HashSet<int>(indices ?? Enumerable.Empty<int>());
      DateTime? earliest = null;
      foreach (SessionListItem item in GetActiveItems())
      {
        if (item?.Info == null || !wanted.Contains(item.Info.SessionIndex))
          continue;
        if (!earliest.HasValue || item.Info.StartedLocal < earliest.Value)
          earliest = item.Info.StartedLocal;
      }
      return earliest ?? DateTime.Now;
    }

    /// <summary>
    /// Удаляет выделенные сессии активной вкладки: их строки в файле лога и связанные
    /// HTML-отчёты (имя отчёта = префикс вкладки + время начала сессии).
    /// </summary>
    private void OnClearClick(object sender, EventArgs e)
    {
      int tabIndex = _tabs != null ? _tabs.SelectedIndex : 0;
      List<int> indices = CollectSelectedIndices();
      if (indices.Count == 0)
      {
        MessageBox.Show(
            this,
            "Не выбрана ни одна сессия.",
            Text,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      DialogResult confirm = MessageBox.Show(
          this,
          "Удалить выделенные записи (" + indices.Count.ToString(CultureInfo.InvariantCulture)
              + ") из лога и связанные с ними отчёты?\n\nДействие необратимо.",
          Text,
          MessageBoxButtons.YesNo,
          MessageBoxIcon.Warning);
      if (confirm != DialogResult.Yes)
        return;

      List<VelumLogFileSessionInfo> removed;
      try
      {
        switch (tabIndex)
        {
          case 1:
            removed = VelumStyleLogFileSessions.DeleteSessions(indices);
            break;
          case 2:
            removed = VelumParameterLogFileSessions.DeleteSessions(indices);
            break;
          default:
            removed = VelumAgentLogFileSessions.DeleteSessions(indices);
            break;
        }
      }
      catch (Exception ex)
      {
        MessageBox.Show(
            this,
            "Не удалось удалить записи лога:\n" + ex.Message,
            Text,
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
        return;
      }

      int reportsDeleted = DeleteLinkedReports(GetTabReportPrefix(tabIndex), removed);
      ReloadSessions();

      MessageBox.Show(
          this,
          "Удалено сессий: " + removed.Count.ToString(CultureInfo.InvariantCulture)
              + ".\nУдалено связанных отчётов: " + reportsDeleted.ToString(CultureInfo.InvariantCulture) + ".",
          Text,
          MessageBoxButtons.OK,
          MessageBoxIcon.Information);
    }

    /// <summary>
    /// Удаляет HTML-отчёты, связанные с указанными сессиями: файл считается связанным,
    /// если его имя равно <c>{префикс вкладки}_{yyyyMMdd_HHmmss начала сессии}.html</c>.
    /// </summary>
    private static int DeleteLinkedReports(string prefix, IEnumerable<VelumLogFileSessionInfo> sessions)
    {
      if (sessions == null)
        return 0;

      string folder = VelumLogsReportHtmlBuilder.ReportsFolderPath;
      if (!Directory.Exists(folder))
        return 0;

      int deleted = 0;
      foreach (VelumLogFileSessionInfo session in sessions)
      {
        if (session == null)
          continue;
        string fileName = VelumLogsReportHtmlBuilder.BuildSessionFileName(prefix, session.StartedLocal);
        string path = Path.Combine(folder, fileName);
        try
        {
          if (File.Exists(path))
          {
            File.Delete(path);
            deleted++;
          }
        }
        catch
        {
          // Отчёт мог быть занят (открыт в браузере) — не прерываем очистку логов.
        }
      }
      return deleted;
    }

    private static string BuildSummaryText(int count)
    {
      return count == 1
          ? "Выделена 1 сессия."
          : "Выделено сессий: " + count.ToString(CultureInfo.InvariantCulture) + ".";
    }

    private void OnListSelectedIndexChanged(object sender, EventArgs e)
    {
      RefreshButtons();
    }

    private void OnListMouseDoubleClick(object sender, MouseEventArgs e)
    {
      var list = sender as CheckedListBox;
      if (list == null)
        return;
      int index = list.IndexFromPoint(e.Location);
      if (index < 0 || index >= list.Items.Count)
        return;
      list.SetItemChecked(index, !list.GetItemChecked(index));
      RefreshButtons();
    }

    private void OnFormShown(object sender, EventArgs e)
    {
      RefreshButtons();
    }

    /// <summary>Итоговая подпись выделения для отчёта (используется в заголовке).</summary>
    internal static string DescribeSelection(IReadOnlyList<int> indices)
    {
      return BuildSummaryText(indices?.Count ?? 0);
    }

    private static Icon TryLoadVelumWindowIcon()
    {
      try
      {
        string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (string.IsNullOrEmpty(dir))
          return null;
        string[] candidates =
        {
          Path.Combine(dir, "velum.ico"),
          Path.Combine(dir, "icons", "velum.ico"),
        };
        foreach (string path in candidates)
        {
          if (File.Exists(path))
            return new Icon(path);
        }
      }
      catch
      {
      }

      return null;
    }
  }
}
