using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Velum.Isida.Logs;

namespace Velum.UI.Logs
{
  /// <summary>
  /// Построение HTML-отчётов по логам агента, стилей и параметров.
  /// Обычный стиль (не «Matrix»); подсказки выводятся через атрибут <c>title</c>
  /// на ячейках и заголовках (аналог WPF-подсказок AIStudio).
  /// </summary>
  internal static class VelumLogsReportHtmlBuilder
  {
    /// <summary>Каталог для сохранения отчётов логов: <c>%ProgramData%\VELUM\Logs\Reports</c>.</summary>
    internal static string ReportsFolderPath =>
        Path.Combine(VelumLogPaths.LogsFolderPath, "Reports");

    /// <summary>Имя файла отчёта по дате формирования.</summary>
    internal static string BuildFileName(string prefix, DateTime stamp)
    {
      return (prefix ?? "Log") + "_"
             + stamp.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".html";
    }

    /// <summary>
    /// Имя файла отчёта, привязанное к сессии: <c>{префикс}_{yyyyMMdd_HHmmss}.html</c>,
    /// где штамп — время <b>начала</b> сессии. По этому имени отчёт связывается с сессией
    /// при очистке выделенных записей.
    /// </summary>
    internal static string BuildSessionFileName(string prefix, DateTime sessionStart)
    {
      return VelumLogSessionRules.BuildSessionReportFileName(prefix, sessionStart);
    }

    /// <summary>HTML-отчёт по системному логу агента.</summary>
    public static string BuildSystemLogHtml(IReadOnlyList<VelumAgentLogEntry> entries)
    {
      var sb = new StringBuilder();
      AppendHead(sb, "Логи системы");
      AppendMetaTable(sb, "Системный лог агента", entries?.Count ?? 0);

      var list = entries ?? Array.Empty<VelumAgentLogEntry>();
      if (list.Count == 0)
      {
        sb.AppendLine("<p class=\"muted\">Нет записей для отчёта.</p>");
        AppendFoot(sb);
        return sb.ToString();
      }

      var provider = new VelumLogCellTooltipProvider();

      sb.AppendLine("<table class=\"data-zebra\">");
      sb.Append("<tr>");
      AppendTh(sb, "Время", "Время записи в сессии.");
      AppendTh(sb, "Пульс", "Номер пульса системы (1 пульс = 1 секунда).");
      AppendTh(sb, "Состояние", "Код состояния гомеостаза: -1 плохо, 0 норма, 1 хорошо.");
      AppendTh(sb, "Стиль", "Стили поведения на пульсе (наведите — расшифровка).");
      AppendTh(sb, "Тема", "Тип темы мышления и её вес.");
      AppendTh(sb, "Триггер", "Пусковой стимул: воздействие, фразы, команды, тон, настроение, цвет.");
      AppendTh(sb, "Среда", "Давление метрик среды (id:величина).");
      AppendTh(sb, "ОР/УМ", "Ориентировочный рефлекс или уровень мышления (УМ1/УМ2).");
      AppendTh(sb, "Опасно", "Признак опасной ситуации в информационной среде.");
      AppendTh(sb, "Актуально", "Признак актуальной ситуации в информационной среде.");
      AppendTh(sb, "Б/у рефлекс", "Безусловный (генетический) рефлекс и его действия.");
      AppendTh(sb, "Усл. рефлекс", "Условный рефлекс и действия исходного безусловного.");
      AppendTh(sb, "Автоматизм", "Автоматизм: действия, фразы, тон, настроение, полезность.");
      AppendTh(sb, "Цепочка РФ", "Активная цепочка рефлексов (ChainId:ActionId).");
      AppendTh(sb, "Цепочка АВ", "Активная цепочка автоматизмов (ChainId:ActionId).");
      AppendTh(sb, "Цикл М", "Номер текущего главного цикла мышления.");
      AppendTh(sb, "Циклы Ф", "Фоновые циклы мышления (JSON).");
      sb.AppendLine("</tr>");

      foreach (VelumAgentLogEntry e in list)
      {
        if (e == null)
          continue;
        sb.Append("<tr>");
        AppendTd(sb, e.Timestamp.ToString("HH:mm:ss", CultureInfo.CurrentCulture), null);
        AppendTd(sb, e.DisplayPulse, null);
        AppendTd(sb, e.DisplayBaseId, VelumLogCellTooltipProvider.GetStateCodeTooltip(e.DisplayBaseId),
            VelumLogCellFillRules.StateFill(e.BaseId));
        AppendTd(sb, e.DisplayBaseStyleId, provider.GetStyleCellTooltip(e.DisplayBaseStyleId));
        AppendTd(sb, e.DisplayThinkingThemeId, provider.GetThinkingThemeTypeTooltip(e.DisplayThinkingThemeId));
        AppendTd(sb, e.DisplayTriggerStimulusId, provider.GetTriggerTooltip(e.DisplayTriggerStimulusId));
        AppendTd(sb, e.DisplayEnvironmentPressure,
            provider.GetEnvironmentPressureTooltip(e.EnvironmentPressureCell, e.EnvironmentPressureTooltip),
            VelumLogCellFillRules.EnvironmentFill(e.EnvironmentPressureCell));
        AppendTd(sb, e.DisplayOrUm, provider.GetOrUmTooltip(e.DisplayOrUm, e.ThinkingLevelSuccess),
            VelumLogCellFillRules.OrUmFill(e.DisplayOrUm, e.ThinkingLevelSuccess));
        AppendTd(sb, e.DisplayDanger, null, VelumLogCellFillRules.DangerFill(e.InformationEnvironmentDanger));
        AppendTd(sb, e.DisplayVeryActual, null);
        AppendTd(sb, e.DisplayGeneticReflexId, provider.GetActionsForGeneticReflex(e.DisplayGeneticReflexId));
        AppendTd(sb, e.DisplayConditionReflexId, provider.GetActionsForConditionReflex(e.DisplayConditionReflexId));
        AppendTd(sb, e.DisplayAutomatizmId,
            provider.GetAutomatizmTooltip(e.DisplayAutomatizmId, e.AutomatizmUsefulnessAtSnapshot));
        AppendTd(sb, e.DisplayReflexChainInfo, provider.GetReflexChainTooltip(e.ReflexChainInfo));
        AppendTd(sb, e.DisplayAutomatizmChainInfo, provider.GetAutomatizmChainTooltip(e.AutomatizmChainInfo));
        AppendTd(sb, e.DisplayMainThinkingCycle, null);
        AppendTd(sb, e.DisplayBackgroundThinkingCycles, null);
        sb.AppendLine("</tr>");
      }

      sb.AppendLine("</table>");
      AppendFoot(sb);
      return sb.ToString();
    }

    /// <summary>HTML-отчёт по логу стилей поведения.</summary>
    public static string BuildStyleLogHtml(VelumStyleLogSessionData data)
    {
      var sb = new StringBuilder();
      AppendHead(sb, "Логи стилей");

      var finalEntries = data?.StyleEntries ?? new List<VelumStyleLogEntry>();
      var activations = data?.Activations ?? new List<VelumStyleParameterActivationEntry>();

      AppendMetaTable(sb, "Лог стилей поведения", finalEntries.Count);

      sb.AppendLine("<h2>Итоговые стили по пульсам</h2>");
      if (finalEntries.Count == 0)
      {
        sb.AppendLine("<p class=\"muted\">Нет записей.</p>");
      }
      else
      {
        sb.AppendLine("<table class=\"data-zebra\">");
        sb.Append("<tr>");
        AppendTh(sb, "Время", "Время записи этапа Final.");
        AppendTh(sb, "Пульс", "Номер пульса системы.");
        AppendTh(sb, "Стиль", "Итоговый доминирующий стиль поведения.");
        sb.AppendLine("</tr>");
        foreach (VelumStyleLogEntry e in finalEntries.OrderBy(x => x.Pulse).ThenBy(x => x.Timestamp))
        {
          sb.Append("<tr>");
          AppendTd(sb, e.Timestamp.ToString("HH:mm:ss", CultureInfo.CurrentCulture), null);
          AppendTd(sb, e.Pulse.ToString(CultureInfo.InvariantCulture), null);
          AppendTd(sb, FormatStyle(e.StyleId, e.StyleName), null);
          sb.AppendLine("</tr>");
        }
        sb.AppendLine("</table>");
      }

      sb.AppendLine("<h2>Активация стилей от параметров</h2>");
      if (activations.Count == 0)
      {
        sb.AppendLine("<p class=\"muted\">Нет записей.</p>");
      }
      else
      {
        sb.AppendLine("<table class=\"data-zebra\">");
        sb.Append("<tr>");
        AppendTh(sb, "Время", "Время записи этапа ParameterActivation.");
        AppendTh(sb, "Пульс", "Номер пульса системы.");
        AppendTh(sb, "Параметр", "Параметр гомеостаза, активировавший стиль.");
        AppendTh(sb, "Зона", "Зона активации параметра.");
        AppendTh(sb, "Стиль", "Активированный стиль поведения.");
        sb.AppendLine("</tr>");
        foreach (VelumStyleParameterActivationEntry e in
                 activations.OrderBy(x => x.Pulse).ThenBy(x => x.Timestamp))
        {
          sb.Append("<tr>");
          AppendTd(sb, e.Timestamp.ToString("HH:mm:ss", CultureInfo.CurrentCulture), null);
          AppendTd(sb, e.Pulse.ToString(CultureInfo.InvariantCulture), null);
          AppendTd(sb, FormatParameter(e.ParameterId, e.ParameterName), null);
          AppendTd(sb, FormatZone(e.ZoneId, e.ZoneDescription), null);
          AppendTd(sb, FormatStyle(e.StyleId, e.StyleName), null);
          sb.AppendLine("</tr>");
        }
        sb.AppendLine("</table>");
      }

      AppendFoot(sb);
      return sb.ToString();
    }

    /// <summary>HTML-отчёт по логу параметров гомеостаза.</summary>
    public static string BuildParameterLogHtml(IReadOnlyList<VelumParameterLogEntry> entries)
    {
      var sb = new StringBuilder();
      AppendHead(sb, "Логи параметров");

      var list = entries ?? Array.Empty<VelumParameterLogEntry>();
      AppendMetaTable(sb, "Лог параметров гомеостаза", list.Count);

      if (list.Count == 0)
      {
        sb.AppendLine("<p class=\"muted\">Нет записей для отчёта.</p>");
        AppendFoot(sb);
        return sb.ToString();
      }

      sb.AppendLine("<table class=\"data-zebra\">");
      sb.Append("<tr>");
      AppendTh(sb, "Время", "Время записи.");
      AppendTh(sb, "Пульс", "Номер пульса системы.");
      AppendTh(sb, "Параметр", "Параметр гомеостаза.");
      AppendTh(sb, "Значение", "Текущее значение параметра.");
      AppendTh(sb, "Норма", "Значение нормы (комфортного уровня).");
      AppendTh(sb, "Вес", "Вес параметра в системе гомеостаза.");
      AppendTh(sb, "Скорость", "Скорость изменения параметра.");
      AppendTh(sb, "Срочность", "Значение функции срочности.");
      AppendTh(sb, "Состояние", "Текущее состояние параметра.");
      AppendTh(sb, "Зона", "Зона активации параметра.");
      sb.AppendLine("</tr>");

      foreach (VelumParameterLogEntry e in list)
      {
        if (e == null)
          continue;
        sb.Append("<tr>");
        AppendTd(sb, e.Timestamp.ToString("HH:mm:ss", CultureInfo.CurrentCulture), null);
        AppendTd(sb, e.Pulse.ToString(CultureInfo.InvariantCulture), null);
        AppendTd(sb, FormatParameter(e.ParamId, e.ParamName), null);
        AppendTd(sb, e.Value.ToString("0.###", CultureInfo.CurrentCulture), null);
        AppendTd(sb, e.NormaWell.ToString(CultureInfo.InvariantCulture), null);
        AppendTd(sb, e.Weight.ToString(CultureInfo.InvariantCulture), null);
        AppendTd(sb, e.Speed.ToString(CultureInfo.InvariantCulture), null);
        AppendTd(sb, e.UrgencyFunction.ToString("0.###", CultureInfo.CurrentCulture), null);
        AppendTd(sb, e.ParameterState, GetParameterStateTooltip(e.StateCode),
            VelumLogCellFillRules.ParameterStateFill(e.StateCode));
        AppendTd(sb, e.ActivationZone, null);
        sb.AppendLine("</tr>");
      }

      sb.AppendLine("</table>");
      AppendFoot(sb);
      return sb.ToString();
    }

    private static string FormatStyle(int styleId, string styleName)
    {
      if (!string.IsNullOrWhiteSpace(styleName))
        return styleName.Trim();
      return styleId > 0 ? "Стиль " + styleId.ToString(CultureInfo.InvariantCulture) : "-";
    }

    private static string FormatParameter(int parameterId, string parameterName)
    {
      if (!string.IsNullOrWhiteSpace(parameterName))
        return parameterName.Trim();
      return parameterId > 0 ? "Параметр " + parameterId.ToString(CultureInfo.InvariantCulture) : "-";
    }

    private static string FormatZone(int zoneId, string zoneDescription)
    {
      if (!string.IsNullOrWhiteSpace(zoneDescription))
        return zoneDescription.Trim();
      return zoneId.ToString(CultureInfo.InvariantCulture);
    }

    private static string GetParameterStateTooltip(int stateCode)
    {
      switch (stateCode)
      {
        case -1:
          return "Состояние параметра: плохо (вне нормы).";
        case 1:
          return "Состояние параметра: хорошо (в норме).";
        default:
          return "Состояние параметра: норма.";
      }
    }

    private static void AppendHead(StringBuilder sb, string title)
    {
      sb.AppendLine("<!DOCTYPE html>");
      sb.AppendLine("<html><head><meta charset=\"utf-8\"/>");
      sb.Append("<title>").Append(Escape(title)).AppendLine("</title>");
      AppendStyles(sb);
      sb.AppendLine("</head><body>");
      sb.Append("<h1>").Append(Escape(title)).AppendLine("</h1>");
    }

    private static void AppendMetaTable(StringBuilder sb, string caption, int recordCount)
    {
      sb.AppendLine("<table class=\"meta-table\">");
      AppendMetaRow(sb, "Отчёт", caption);
      AppendMetaRow(
          sb,
          "Сформировано",
          DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.GetCultureInfo("ru-RU")));
      AppendMetaRow(sb, "Записей", recordCount.ToString(CultureInfo.InvariantCulture));
      sb.AppendLine("</table>");
    }

    private static void AppendFoot(StringBuilder sb)
    {
      sb.AppendLine("<p class=\"muted footer\">Сформировано Velum. Наведите курсор на ячейку или заголовок для подсказки.</p>");
      sb.AppendLine("</body></html>");
    }

    private static void AppendTh(StringBuilder sb, string text, string tooltip)
    {
      sb.Append("<th");
      if (!string.IsNullOrEmpty(tooltip))
        sb.Append(" title=\"").Append(Escape(tooltip)).Append('"');
      sb.Append('>').Append(Escape(text)).Append("</th>");
    }

    private static void AppendTd(StringBuilder sb, string text, string tooltip)
    {
      AppendTd(sb, text, tooltip, VelumLogCellFill.None);
    }

    private static void AppendTd(StringBuilder sb, string text, string tooltip, VelumLogCellFill fill)
    {
      sb.Append("<td");
      string fillClass = FillCssClass(fill);
      if (!string.IsNullOrEmpty(fillClass))
        sb.Append(" class=\"").Append(fillClass).Append('"');
      if (!string.IsNullOrEmpty(tooltip))
        sb.Append(" title=\"").Append(Escape(tooltip)).Append('"');
      sb.Append('>').Append(Escape(text ?? string.Empty)).Append("</td>");
    }

    /// <summary>CSS-класс фона ячейки по коду <see cref="VelumLogCellFill"/> (пусто — без класса).</summary>
    private static string FillCssClass(VelumLogCellFill fill)
    {
      switch (fill)
      {
        case VelumLogCellFill.Red:
          return "fill-red";
        case VelumLogCellFill.Yellow:
          return "fill-yellow";
        case VelumLogCellFill.Green:
          return "fill-green";
        default:
          return string.Empty;
      }
    }

    private static void AppendMetaRow(StringBuilder sb, string label, string value)
    {
      sb.Append("<tr><th class=\"meta-label\">").Append(Escape(label ?? string.Empty))
          .Append("</th><td class=\"meta-value\">").Append(Escape(value ?? string.Empty))
          .AppendLine("</td></tr>");
    }

    private static void AppendStyles(StringBuilder sb)
    {
      sb.AppendLine("<style>");
      sb.AppendLine("body{font-family:Segoe UI,Tahoma,sans-serif;margin:10px;color:#222;}");
      sb.AppendLine("h1{font-size:14px;color:#1565C0;margin:0 0 4px;font-weight:600;}");
      sb.AppendLine("h2{font-size:12px;color:#37474F;margin:10px 0 2px;border-bottom:1px solid #B0BEC5;padding-bottom:2px;}");
      sb.AppendLine("table{border-collapse:collapse;width:100%;margin:4px 0;font-size:12px;}");
      sb.AppendLine("th,td{border:1px solid #CFD8DC;padding:2px 6px;text-align:left;vertical-align:middle;line-height:1.25;color:#000;white-space:nowrap;}");
      sb.AppendLine("th{background:#ECEFF1;font-weight:600;cursor:help;}");
      sb.AppendLine("td[title]{cursor:help;}");
      sb.AppendLine("table.data-zebra tr:nth-child(even){background:#FAFAFA;}");
      sb.AppendLine("td.fill-red{background:#F8D0D0;-webkit-print-color-adjust:exact;print-color-adjust:exact;}");
      sb.AppendLine("td.fill-yellow{background:#FBF3C0;-webkit-print-color-adjust:exact;print-color-adjust:exact;}");
      sb.AppendLine("td.fill-green{background:#D3F0D3;-webkit-print-color-adjust:exact;print-color-adjust:exact;}");
      sb.AppendLine(".muted{color:#78909C;}");
      sb.AppendLine(".footer{margin-top:10px;font-size:10px;}");
      sb.AppendLine("table.meta-table{width:100%;table-layout:fixed;margin:2px 0 4px;}");
      sb.AppendLine("table.meta-table th.meta-label{width:12%;min-width:80px;max-width:120px;font-size:10px;font-weight:600;padding:1px 6px;background:#ECEFF1;}");
      sb.AppendLine("table.meta-table td.meta-value{width:88%;font-size:11px;padding:1px 6px;line-height:1.25;word-wrap:break-word;}");
      sb.AppendLine("@media print{body{margin:6px;} h1{color:#000;} th{background:#eee !important;-webkit-print-color-adjust:exact;print-color-adjust:exact;} td{color:#000 !important;}}");
      sb.AppendLine("</style>");
    }

    private static string Escape(string text)
    {
      return WebUtility.HtmlEncode(text ?? string.Empty);
    }
  }
}
