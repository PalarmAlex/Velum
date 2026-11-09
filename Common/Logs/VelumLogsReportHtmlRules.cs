using System.Text;

namespace Velum.UI.Logs
{
  /// <summary>
  /// Правила разметки и стилей HTML-отчётов по логам: таблица данных с <b>фиксированной
  /// шапкой</b> (строка заголовков столбцов прилипает к верху окна при вертикальной прокрутке).
  /// Класс не зависит от ISIDA, WinForms и файловой системы — используется и в отчётах, и в тестах.
  /// </summary>
  internal static class VelumLogsReportHtmlRules
  {
    /// <summary>Цвет фона заголовков столбцов (должен быть непрозрачным, иначе при прокрутке просвечивают строки).</summary>
    private const string HeaderBackground = "#ECEFF1";

    /// <summary>Цвет рамки ячеек (совпадает с <c>border</c> в общем правиле <c>th,td</c>).</summary>
    private const string CellBorderColor = "#CFD8DC";

    /// <summary>
    /// Начало шапки таблицы данных: открывает <c>&lt;thead&gt;</c> и строку заголовков.
    /// Дальше идут только <c>&lt;th&gt;</c>, закрывает <see cref="EndTableHead"/>.
    /// </summary>
    /// <param name="sb">Целевой <see cref="StringBuilder"/> с HTML отчёта.</param>
    public static void BeginTableHead(StringBuilder sb)
    {
      sb.Append("<thead><tr>");
    }

    /// <summary>Конец шапки таблицы данных: закрывает строку заголовков и <c>&lt;thead&gt;</c>.</summary>
    /// <param name="sb">Целевой <see cref="StringBuilder"/> с HTML отчёта.</param>
    public static void EndTableHead(StringBuilder sb)
    {
      sb.AppendLine("</tr></thead>");
    }

    /// <summary>
    /// CSS отчёта (без тега <c>&lt;style&gt;</c>). Ключевое правило — <c>thead th</c> с
    /// <c>position:sticky</c>: у таблиц логов тысячи строк, без фиксации заголовков столбцов
    /// при прокрутке теряется подписи колонок. Рамка шапки рисуется через <c>box-shadow</c>,
    /// потому что при <c>border-collapse:collapse</c> границы «отстают» от прилипшей ячейки.
    /// </summary>
    /// <returns>Текст таблицы стилей для вставки внутрь <c>&lt;style&gt;</c>.</returns>
    public static string BuildStyles()
    {
      var sb = new StringBuilder();
      sb.AppendLine("body{font-family:Segoe UI,Tahoma,sans-serif;margin:10px;color:#222;}");
      sb.AppendLine("h1{font-size:14px;color:#1565C0;margin:0 0 4px;font-weight:600;}");
      sb.AppendLine("h2{font-size:12px;color:#37474F;margin:10px 0 2px;border-bottom:1px solid #B0BEC5;padding-bottom:2px;}");
      sb.AppendLine("table{border-collapse:collapse;width:100%;margin:4px 0;font-size:12px;}");
      sb.AppendLine("th,td{border:1px solid " + CellBorderColor + ";padding:2px 6px;text-align:left;vertical-align:middle;line-height:1.25;color:#000;white-space:nowrap;}");
      sb.AppendLine("th{background:" + HeaderBackground + ";font-weight:600;cursor:help;}");
      sb.AppendLine("td[title]{cursor:help;}");
      sb.AppendLine("table.data-zebra tr:nth-child(even){background:#FAFAFA;}");
      // Фиксированная шапка: прилипает к верху окна, перекрывает строки непрозрачным фоном.
      sb.AppendLine("table.data-zebra thead th{position:sticky;top:0;z-index:2;background:" + HeaderBackground
          + ";box-shadow:inset 0 1px 0 " + CellBorderColor + ",inset 0 -1px 0 #90A4AE;}");
      sb.AppendLine("td.fill-red{background:#F8D0D0;-webkit-print-color-adjust:exact;print-color-adjust:exact;}");
      sb.AppendLine("td.fill-yellow{background:#FBF3C0;-webkit-print-color-adjust:exact;print-color-adjust:exact;}");
      sb.AppendLine("td.fill-green{background:#D3F0D3;-webkit-print-color-adjust:exact;print-color-adjust:exact;}");
      sb.AppendLine(".muted{color:#78909C;}");
      sb.AppendLine(".footer{margin-top:10px;font-size:10px;}");
      sb.AppendLine("table.meta-table{width:100%;table-layout:fixed;margin:2px 0 4px;}");
      sb.AppendLine("table.meta-table th.meta-label{width:12%;min-width:80px;max-width:120px;font-size:10px;font-weight:600;padding:1px 6px;background:" + HeaderBackground + ";}");
      sb.AppendLine("table.meta-table td.meta-value{width:88%;font-size:11px;padding:1px 6px;line-height:1.25;word-wrap:break-word;}");
      // Блок «Событийные логи»: токены сокращённого лога движка (ShortLogChannel). Каждый
      // токен — блок текста за один пульс; «№ пульса» и «×N» (повторы) — в заголовке блока.
      // white-space:pre-wrap на .ev-token переопределяет наследуемое th,td{white-space:nowrap}.
      sb.AppendLine(".event-log{margin:8px 0;border:1px solid #CFD8DC;border-radius:6px;background:#FCFDFE;padding:8px 10px;max-height:520px;overflow:auto;}");
      sb.AppendLine(".event-log .ev-token{margin:0 0 8px 0;padding:6px 8px;border:1px solid #E0E6EA;border-radius:4px;background:#fff;font-family:Consolas,'Courier New',monospace;font-size:11.5px;line-height:1.4;white-space:pre-wrap;word-wrap:break-word;}");
      sb.AppendLine(".event-log .ev-token:last-child{margin-bottom:0;}");
      sb.AppendLine(".event-log .ev-pulse{color:#1565C0;font-weight:600;}");
      sb.AppendLine(".event-log .ev-empty{color:#78909C;font-size:12px;margin:0;}");
      sb.AppendLine("@media print{body{margin:6px;} h1{color:#000;} table.data-zebra thead th{position:static;} th{background:#eee !important;-webkit-print-color-adjust:exact;print-color-adjust:exact;} td{color:#000 !important;}}");
      return sb.ToString();
    }
  }
}
