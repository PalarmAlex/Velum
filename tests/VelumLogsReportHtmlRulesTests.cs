using System.Text;
using Velum.UI.Logs;
using Xunit;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>
  /// Регрессия: в HTML-отчётах по логам шапка таблицы (строка заголовков столбцов) должна быть
  /// фиксированной — иначе в логах на тысячи строк при вертикальной прокрутке теряются подписи
  /// колонок и таблицу невозможно читать. Тест проверяет обёртку <c>&lt;thead&gt;</c> и CSS
  /// прилипания (<c>position:sticky</c> с непрозрачным фоном, отключённый при печати).
  /// </summary>
  public class VelumLogsReportHtmlRulesTests
  {
    private static string HeadMarkup()
    {
      var sb = new StringBuilder();
      VelumLogsReportHtmlRules.BeginTableHead(sb);
      sb.Append("<th>Время</th><th>Пульс</th>");
      VelumLogsReportHtmlRules.EndTableHead(sb);
      return sb.ToString();
    }

    [Fact]
    public void BeginTableHead_OpensTheadAndRow()
    {
      Assert.StartsWith("<thead><tr>", HeadMarkup());
    }

    [Fact]
    public void EndTableHead_ClosesRowAndThead()
    {
      Assert.EndsWith("</tr></thead>" + System.Environment.NewLine, HeadMarkup());
    }

    [Fact]
    public void BuildStyles_SticksHeaderRowToTopOfViewport()
    {
      string css = VelumLogsReportHtmlRules.BuildStyles();

      // Правило именно для заголовков таблицы логов, иначе sticky не сработает.
      int ruleStart = css.IndexOf("table.data-zebra thead th{", System.StringComparison.Ordinal);
      Assert.True(ruleStart >= 0, "Нет правила фиксированной шапки для table.data-zebra thead th.");
      string rule = css.Substring(ruleStart, css.IndexOf('}', ruleStart) - ruleStart);

      Assert.Contains("position:sticky", rule);
      Assert.Contains("top:0", rule);
      Assert.Contains("z-index:", rule);
    }

    [Fact]
    public void BuildStyles_StickyHeaderBackgroundIsOpaque()
    {
      string css = VelumLogsReportHtmlRules.BuildStyles();
      int ruleStart = css.IndexOf("table.data-zebra thead th{", System.StringComparison.Ordinal);
      string rule = css.Substring(ruleStart, css.IndexOf('}', ruleStart) - ruleStart);

      // Прозрачный фон шапки при прокрутке просвечивает строками лога — «полупрозрачная» шапка.
      Assert.DoesNotContain("transparent", rule);
      Assert.DoesNotContain("rgba(", rule);
      Assert.Matches("background:#[0-9A-Fa-f]{6}", rule);
    }

    [Fact]
    public void BuildStyles_HeaderBorderSurvivesStickyViaBoxShadow()
    {
      string css = VelumLogsReportHtmlRules.BuildStyles();
      int ruleStart = css.IndexOf("table.data-zebra thead th{", System.StringComparison.Ordinal);
      string rule = css.Substring(ruleStart, css.IndexOf('}', ruleStart) - ruleStart);

      // При border-collapse:collapse рамка прилипшей ячейки «отстаёт» — нужен box-shadow.
      Assert.Contains("box-shadow:", rule);
    }

    [Fact]
    public void BuildStyles_PrintModeDisablesStickyHeader()
    {
      string css = VelumLogsReportHtmlRules.BuildStyles();
      int printStart = css.IndexOf("@media print", System.StringComparison.Ordinal);
      Assert.True(printStart >= 0, "Нет блока @media print.");
      string print = css.Substring(printStart);

      Assert.Contains("table.data-zebra thead th{position:static;}", print);
    }

    [Fact]
    public void BuildStyles_KeepsExistingReportLookRules()
    {
      string css = VelumLogsReportHtmlRules.BuildStyles();

      // Фиксация шапки не должна потерять прежние правила отчёта.
      Assert.Contains("table.data-zebra tr:nth-child(even){background:#FAFAFA;}", css);
      Assert.Contains("td.fill-red{", css);
      Assert.Contains("td.fill-yellow{", css);
      Assert.Contains("td.fill-green{", css);
      Assert.Contains("table.meta-table th.meta-label{", css);
    }
  }
}
