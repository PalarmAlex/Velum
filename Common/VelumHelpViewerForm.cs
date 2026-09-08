using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace Velum.UI
{
  /// <summary>
  /// Просмотрщик HTML-справки из %ProgramData%\VELUM\help.
  /// Навигация — file://; CSS вшивается в &lt;style&gt; после загрузки.
  /// </summary>
  internal sealed class VelumHelpViewerForm : Form
  {
    private const string InlineStyleId = "velum-help-inline-css";
    private const int SearchPanelMaxHeight = 180;

    private readonly WebBrowser _browser;
    private readonly Button _btnBack;
    private readonly Button _btnForward;
    private readonly TextBox _searchBox;
    private readonly Label _searchStatus;
    private readonly Panel _searchPanel;
    private readonly ListBox _searchResults;
    private readonly string _initialPath;
    private readonly List<HelpSearchHit> _searchHits = new List<HelpSearchHit>();
    private List<HelpSearchEntry> _index;
    private string _currentPath;
    private bool _initialLoaded;
    private bool _injectCssPending;

    internal VelumHelpViewerForm(string htmlPath, string topicId)
    {
      _initialPath = htmlPath;

      Text = "Справка Velum";
      StartPosition = FormStartPosition.CenterParent;
      ShowInTaskbar = false;
      MinimizeBox = false;
      MaximizeBox = true;
      Size = new Size(1000, 700);
      MinimumSize = new Size(560, 400);
      Font = SystemFonts.MessageBoxFont;

      Icon icon = TryLoadFormIcon();
      if (icon != null)
        Icon = icon;

      var toolbar = new FlowLayoutPanel
      {
        Dock = DockStyle.Top,
        Height = 40,
        FlowDirection = FlowDirection.LeftToRight,
        Padding = new Padding(8, 6, 8, 6),
        WrapContents = false,
      };

      _btnBack = new Button
      {
        Text = "← Назад",
        Width = 90,
        Height = 28,
        Enabled = false,
        Margin = new Padding(0, 0, 8, 0),
      };
      _btnForward = new Button
      {
        Text = "Вперёд →",
        Width = 90,
        Height = 28,
        Enabled = false,
        Margin = new Padding(0, 0, 16, 0),
      };
      _btnBack.Click += (s, e) =>
      {
        if (_browser.CanGoBack)
          _browser.GoBack();
      };
      _btnForward.Click += (s, e) =>
      {
        if (_browser.CanGoForward)
          _browser.GoForward();
      };

      var searchLabel = new Label
      {
        Text = "Поиск:",
        AutoSize = true,
        Margin = new Padding(0, 6, 4, 0),
      };
      _searchBox = new TextBox
      {
        Width = 220,
        Height = 28,
        Margin = new Padding(0, 2, 8, 0),
      };
      _searchStatus = new Label
      {
        AutoSize = true,
        ForeColor = SystemColors.GrayText,
        Margin = new Padding(0, 6, 0, 0),
      };
      _searchBox.TextChanged += (s, e) => RunSearch();
      _searchBox.KeyDown += OnSearchBoxKeyDown;

      toolbar.Controls.Add(_btnBack);
      toolbar.Controls.Add(_btnForward);
      toolbar.Controls.Add(searchLabel);
      toolbar.Controls.Add(_searchBox);
      toolbar.Controls.Add(_searchStatus);

      var tip = new ToolTip();
      tip.SetToolTip(_btnBack, "Предыдущая страница справки");
      tip.SetToolTip(_btnForward, "Следующая страница справки");
      tip.SetToolTip(_searchBox, "Поиск по разделам справки (Ctrl+F)");

      _searchResults = new ListBox
      {
        Dock = DockStyle.Fill,
        IntegralHeight = false,
        DisplayMember = "Title",
      };
      _searchResults.Click += (s, e) => OpenSelectedSearchHit();
      _searchResults.DoubleClick += (s, e) => OpenSelectedSearchHit();
      _searchResults.KeyDown += OnSearchResultsKeyDown;

      _searchPanel = new Panel
      {
        Dock = DockStyle.Top,
        Height = 0,
        Visible = false,
        Padding = new Padding(8, 0, 8, 6),
      };
      _searchPanel.Controls.Add(_searchResults);

      _browser = new WebBrowser
      {
        Dock = DockStyle.Fill,
        AllowWebBrowserDrop = false,
        IsWebBrowserContextMenuEnabled = true,
        ScriptErrorsSuppressed = true,
        WebBrowserShortcutsEnabled = true,
      };
      _browser.DocumentCompleted += OnBrowserDocumentCompleted;
      _browser.Navigated += OnBrowserNavigated;
      _browser.CanGoBackChanged += (s, e) => UpdateNavButtons();
      _browser.CanGoForwardChanged += (s, e) => UpdateNavButtons();

      Controls.Add(_browser);
      Controls.Add(_searchPanel);
      Controls.Add(toolbar);

      KeyPreview = true;
      KeyDown += OnFormKeyDown;

      Shown += OnFirstShown;
    }

    private void UpdateNavButtons()
    {
      _btnBack.Enabled = _browser.CanGoBack;
      _btnForward.Enabled = _browser.CanGoForward;
    }

    private void OnFirstShown(object sender, EventArgs e)
    {
      if (_initialLoaded)
        return;
      _initialLoaded = true;
      EnsureSearchIndex();
      NavigateToFile(_initialPath);
    }

    private void OnFormKeyDown(object sender, KeyEventArgs e)
    {
      if (e.Control && e.KeyCode == Keys.F)
      {
        _searchBox.Focus();
        _searchBox.SelectAll();
        e.Handled = true;
        e.SuppressKeyPress = true;
        return;
      }

      if (e.KeyCode == Keys.Escape)
      {
        if (_searchPanel.Visible || !string.IsNullOrEmpty(_searchBox.Text))
        {
          ClearSearchUi();
          e.Handled = true;
          return;
        }

        Close();
        e.Handled = true;
      }
    }

    private void OnSearchBoxKeyDown(object sender, KeyEventArgs e)
    {
      if (e.KeyCode == Keys.Down && _searchResults.Items.Count > 0)
      {
        _searchResults.Focus();
        if (_searchResults.SelectedIndex < 0)
          _searchResults.SelectedIndex = 0;
        e.Handled = true;
        e.SuppressKeyPress = true;
        return;
      }

      if (e.KeyCode == Keys.Enter)
      {
        if (_searchHits.Count > 0)
        {
          if (_searchResults.SelectedIndex < 0)
            _searchResults.SelectedIndex = 0;
          OpenSelectedSearchHit();
        }
        e.Handled = true;
        e.SuppressKeyPress = true;
      }
    }

    private void OnSearchResultsKeyDown(object sender, KeyEventArgs e)
    {
      if (e.KeyCode == Keys.Enter)
      {
        OpenSelectedSearchHit();
        e.Handled = true;
        e.SuppressKeyPress = true;
      }
      else if (e.KeyCode == Keys.Escape)
      {
        ClearSearchUi();
        e.Handled = true;
      }
    }

    private void EnsureSearchIndex()
    {
      if (_index != null)
        return;
      _index = HelpSearchIndex.Build(VelumHelp.GetHelpRoot());
    }

    private void RunSearch()
    {
      EnsureSearchIndex();
      _searchHits.Clear();
      _searchResults.Items.Clear();

      string query = (_searchBox.Text ?? string.Empty).Trim();
      if (query.Length == 0)
      {
        HideSearchPanel();
        _searchStatus.Text = string.Empty;
        return;
      }

      if (query.Length < 2)
      {
        HideSearchPanel();
        _searchStatus.Text = "Введите не менее 2 символов";
        return;
      }

      foreach (HelpSearchEntry entry in _index)
      {
        if (!entry.Matches(query))
          continue;
        var hit = new HelpSearchHit(entry.Title, entry.Path, entry.MakeSnippet(query));
        _searchHits.Add(hit);
        _searchResults.Items.Add(hit);
      }

      if (_searchHits.Count == 0)
      {
        _searchStatus.Text = "Ничего не найдено";
        HideSearchPanel();
        return;
      }

      _searchStatus.Text = "Найдено: " + _searchHits.Count;
      ShowSearchPanel();
      _searchResults.SelectedIndex = 0;
    }

    private void ShowSearchPanel()
    {
      int rows = Math.Min(_searchHits.Count, 8);
      int height = Math.Min(SearchPanelMaxHeight, 8 + rows * Math.Max(18, _searchResults.ItemHeight + 2));
      _searchPanel.Height = Math.Max(40, height);
      _searchPanel.Visible = true;
    }

    private void HideSearchPanel()
    {
      _searchPanel.Visible = false;
      _searchPanel.Height = 0;
    }

    private void ClearSearchUi()
    {
      _searchBox.Text = string.Empty;
      _searchHits.Clear();
      _searchResults.Items.Clear();
      HideSearchPanel();
      _searchStatus.Text = string.Empty;
      _searchBox.Focus();
    }

    private void OpenSelectedSearchHit()
    {
      int index = _searchResults.SelectedIndex;
      if (index < 0 || index >= _searchHits.Count)
        return;

      HelpSearchHit hit = _searchHits[index];
      HideSearchPanel();
      NavigateToFile(hit.Path);
      _browser.Focus();
    }

    private void NavigateToFile(string path)
    {
      if (string.IsNullOrEmpty(path) || !File.Exists(path))
        return;

      try
      {
        _currentPath = Path.GetFullPath(path);
        _injectCssPending = true;
        _browser.Navigate(new Uri(_currentPath).AbsoluteUri);
      }
      catch (Exception ex)
      {
        _injectCssPending = false;
        MessageBox.Show(
            this,
            "Не удалось открыть файл справки:" + Environment.NewLine + path +
            Environment.NewLine + Environment.NewLine + ex.Message,
            "Справка Velum",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
      }
    }

    private void OnBrowserNavigated(object sender, WebBrowserNavigatedEventArgs e)
    {
      _injectCssPending = true;
      UpdateCurrentPathFromBrowser();
      UpdateNavButtons();
    }

    private void OnBrowserDocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
    {
      if (e.Url != null && _browser.Url != null
          && !string.Equals(e.Url.AbsoluteUri, _browser.Url.AbsoluteUri, StringComparison.OrdinalIgnoreCase))
        return;

      UpdateCurrentPathFromBrowser();
      UpdateNavButtons();

      if (!_injectCssPending)
        return;

      _injectCssPending = false;
      TryInjectInlineStylesheet();
    }

    private void UpdateCurrentPathFromBrowser()
    {
      if (_browser.Url == null
          || !string.Equals(_browser.Url.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
        return;

      try
      {
        string local = Uri.UnescapeDataString(_browser.Url.LocalPath);
        if (File.Exists(local))
          _currentPath = Path.GetFullPath(local);
      }
      catch
      {
        // ignore
      }
    }

    private void TryInjectInlineStylesheet()
    {
      try
      {
        HtmlDocument doc = _browser.Document;
        if (doc == null)
          return;

        if (doc.GetElementById(InlineStyleId) != null)
          return;

        string cssPath = Path.Combine(VelumHelp.GetHelpRoot(), "styles.css");
        if (!File.Exists(cssPath))
          return;

        string css = File.ReadAllText(cssPath, Encoding.UTF8);
        string cssLiteral = JsonConvert.SerializeObject(css);
        string js =
            "(function(){"
            + "if(document.getElementById('" + InlineStyleId + "'))return;"
            + "var css=" + cssLiteral + ";"
            + "var el=document.createElement('style');"
            + "el.id='" + InlineStyleId + "';"
            + "el.type='text/css';"
            + "if(el.styleSheet)el.styleSheet.cssText=css;"
            + "else el.appendChild(document.createTextNode(css));"
            + "var head=document.getElementsByTagName('head')[0];"
            + "if(head)head.appendChild(el);"
            + "})();";

        doc.InvokeScript("eval", new object[] { js });
      }
      catch
      {
        // best effort
      }
    }

    private static Icon TryLoadFormIcon()
    {
      try
      {
        string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (string.IsNullOrEmpty(dir))
          return null;
        string path = Path.Combine(dir, "icons", "velum.ico");
        return File.Exists(path) ? new Icon(path) : null;
      }
      catch
      {
        return null;
      }
    }

    private void InitializeComponent()
    {
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VelumHelpViewerForm));
      this.SuspendLayout();
      // 
      // VelumHelpViewerForm
      // 
      this.ClientSize = new System.Drawing.Size(284, 261);
      this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
      this.Name = "VelumHelpViewerForm";
      this.ResumeLayout(false);

    }

    private sealed class HelpSearchHit
    {
      internal HelpSearchHit(string title, string path, string snippet)
      {
        Title = string.IsNullOrEmpty(snippet) ? title : title + " — " + snippet;
        Path = path;
      }

      public string Title { get; }
      internal string Path { get; }

      public override string ToString()
      {
        return Title;
      }
    }

    private sealed class HelpSearchEntry
    {
      internal HelpSearchEntry(string title, string path, string plainText)
      {
        Title = title ?? string.Empty;
        Path = path ?? string.Empty;
        PlainText = plainText ?? string.Empty;
        SearchBlob = (Title + " " + PlainText).ToLowerInvariant();
      }

      internal string Title { get; }
      internal string Path { get; }
      internal string PlainText { get; }
      private string SearchBlob { get; }

      internal bool Matches(string query)
      {
        if (string.IsNullOrEmpty(query))
          return false;
        return SearchBlob.IndexOf(query.ToLowerInvariant(), StringComparison.Ordinal) >= 0;
      }

      internal string MakeSnippet(string query)
      {
        if (string.IsNullOrEmpty(PlainText) || string.IsNullOrEmpty(query))
          return string.Empty;

        string lower = PlainText.ToLowerInvariant();
        string q = query.ToLowerInvariant();
        int at = lower.IndexOf(q, StringComparison.Ordinal);
        if (at < 0)
          return string.Empty;

        int start = Math.Max(0, at - 24);
        int end = Math.Min(PlainText.Length, at + query.Length + 36);
        string snippet = PlainText.Substring(start, end - start).Replace('\r', ' ').Replace('\n', ' ');
        while (snippet.IndexOf("  ", StringComparison.Ordinal) >= 0)
          snippet = snippet.Replace("  ", " ");
        snippet = snippet.Trim();
        if (start > 0)
          snippet = "…" + snippet;
        if (end < PlainText.Length)
          snippet = snippet + "…";
        return snippet;
      }
    }

    private static class HelpSearchIndex
    {
      private static readonly Regex RxTitle = new Regex(
          @"<title[^>]*>(.*?)</title>",
          RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
      private static readonly Regex RxH1 = new Regex(
          @"<h1[^>]*>(.*?)</h1>",
          RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
      private static readonly Regex RxScriptStyle = new Regex(
          @"<(script|style)[^>]*>.*?</\1>",
          RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
      private static readonly Regex RxTags = new Regex(
          @"<[^>]+>",
          RegexOptions.Compiled);
      private static readonly Regex RxWs = new Regex(
          @"\s+",
          RegexOptions.Compiled);

      internal static List<HelpSearchEntry> Build(string helpRoot)
      {
        var list = new List<HelpSearchEntry>();
        if (string.IsNullOrEmpty(helpRoot) || !Directory.Exists(helpRoot))
          return list;

        string[] files;
        try
        {
          files = Directory.GetFiles(helpRoot, "*.html", SearchOption.AllDirectories);
        }
        catch
        {
          return list;
        }

        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        foreach (string file in files)
        {
          try
          {
            string html = File.ReadAllText(file, Encoding.UTF8);
            string title = ExtractTitle(html);
            string plain = HtmlToPlain(html);
            if (string.IsNullOrEmpty(title))
              title = Path.GetFileNameWithoutExtension(file);
            list.Add(new HelpSearchEntry(title, Path.GetFullPath(file), plain));
          }
          catch
          {
            // skip unreadable page
          }
        }

        return list;
      }

      private static string ExtractTitle(string html)
      {
        Match h1 = RxH1.Match(html ?? string.Empty);
        if (h1.Success)
        {
          string t = StripTags(h1.Groups[1].Value);
          if (!string.IsNullOrEmpty(t))
            return t;
        }

        Match title = RxTitle.Match(html ?? string.Empty);
        if (title.Success)
        {
          string t = StripTags(title.Groups[1].Value);
          int dash = t.IndexOf("—", StringComparison.Ordinal);
          if (dash < 0)
            dash = t.IndexOf(" - ", StringComparison.Ordinal);
          if (dash > 0)
            t = t.Substring(0, dash).Trim();
          if (!string.IsNullOrEmpty(t))
            return t;
        }

        return string.Empty;
      }

      private static string HtmlToPlain(string html)
      {
        if (string.IsNullOrEmpty(html))
          return string.Empty;

        string text = RxScriptStyle.Replace(html, " ");
        text = RxTags.Replace(text, " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        text = RxWs.Replace(text ?? string.Empty, " ").Trim();
        return text;
      }

      private static string StripTags(string value)
      {
        if (string.IsNullOrEmpty(value))
          return string.Empty;
        string text = RxTags.Replace(value, " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        return RxWs.Replace(text ?? string.Empty, " ").Trim();
      }
    }
  }
}
