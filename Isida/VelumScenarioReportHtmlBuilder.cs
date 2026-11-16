using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using ISIDA.Common;
using ISIDA.Scenarios;

namespace Velum.Isida
{
  /// <summary>
  /// Генерация HTML-отчёта по прогону сценария оператора в Velum.
  /// Дублирует базовую логику AIStudio.Common.ScenarioReportHtmlBuilder, опираясь на
  /// <see cref="VelumScenarioLogComparer"/> (чтение AgentLogs.csv) и
  /// <see cref="ShortLogChannel.GetCurrentSessionTokens"/> (событийный лог).
  /// </summary>
  internal static class VelumScenarioReportHtmlBuilder
  {
    /// <summary>Результат генерации: пути к файлам и список сравнения по шагам.</summary>
    internal sealed class ReportResult
    {
      internal string HtmlPath { get; set; } = "";
      internal List<VelumScenarioLogComparer.StepCompareResult> CompareResults { get; set; }
        = new List<VelumScenarioLogComparer.StepCompareResult>();
    }

    /// <summary>
    /// Строит HTML-отчёт для одного завершённого прогона и сохраняет его в каталог отчётов.
    /// </summary>
    /// <param name="doc">Документ сценария (с ожиданиями).</param>
    /// <param name="completion">Аргументы завершения прогона (якорь, временное окно).</param>
    /// <param name="reportFolder">Каталог для сохранения HTML.</param>
    /// <returns>Результат с путём и списком сравнения; либо <c>null</c>, если данных нет.</returns>
    internal static ReportResult BuildAndSaveSingle(
        ScenarioDocument doc,
        OperatorScenarioCompletedEventArgs completion,
        string reportFolder)
    {
      if (doc == null || completion == null)
        return null;

      int anchor = completion.AnchorGlobalPulse;
      var agg = VelumScenarioLogComparer.AggregateByPulseInWindow(
          completion.RunStartLocal, completion.RunEndLocal);
      var compareList = VelumScenarioLogComparer.Compare(doc, anchor, agg);

      string html = BuildSingleHtml(doc, completion, anchor, agg, compareList);

      Directory.CreateDirectory(reportFolder);
      string fileName = string.Format(
          CultureInfo.InvariantCulture,
          "velum_scenario_{0}_{1:yyyyMMdd_HHmmss}.html",
          doc.Header?.Id ?? 0,
          DateTime.Now);
      string path = Path.Combine(reportFolder, fileName);
      File.WriteAllText(path, html, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
      Logger.Info("Velum HTML report: " + path);

      return new ReportResult { HtmlPath = path, CompareResults = compareList };
    }

    /// <summary>
    /// Строит сводный HTML-отчёт по групповому прогону (состав группы + детальные разделы членов).
    /// </summary>
    internal static ReportResult BuildAndSaveGroup(
        VelumScenarioGroupDocument groupDoc,
        List<VelumScenarioHost.BatchRunResult> results,
        string reportFolder)
    {
      if (groupDoc == null || results == null || results.Count == 0)
        return null;

      Directory.CreateDirectory(reportFolder);

      // Предварительно считаем сравнение и пути детальных HTML по каждому члену.
      var details = new List<VelumScenarioLogComparer.StepCompareResult>();
      foreach (var r in results)
      {
        if (r.EventArgs == null || r.Document == null)
          continue;
        int anchor = r.EventArgs.AnchorGlobalPulse;
        var agg = VelumScenarioLogComparer.AggregateByPulseInWindow(
            r.EventArgs.RunStartLocal, r.EventArgs.RunEndLocal);
        var cmp = VelumScenarioLogComparer.Compare(r.Document, anchor, agg);
        details.AddRange(cmp);
      }

      var sb = new StringBuilder();
      sb.AppendLine("<!DOCTYPE html>");
      sb.AppendLine("<html><head><meta charset=\"utf-8\"/>");
      AppendStyles(sb);
      sb.AppendLine("</head><body>");
      sb.AppendLine("<h1>Отчёт по групповому прогону сценариев Velum</h1>");

      // Мета-данные группы.
      sb.AppendLine("<h2>Группа</h2>");
      sb.AppendLine("<table class=\"meta-table\">");
      AppendMetaRow(sb, "ID группы", Escape(groupDoc.Id.ToString(CultureInfo.InvariantCulture)));
      AppendMetaRow(sb, "Название", Escape(groupDoc.Title ?? ""));
      string gdescHtml = WebUtility.HtmlEncode(groupDoc.Description ?? "")
          .Replace("\r\n", "\n").Replace("\n", "<br/>");
      sb.AppendLine("<tr><th class=\"meta-label\">Описание</th><td class=\"meta-value\""
          + gdescHtml + "</td></tr>");
      AppendMetaRow(sb, "Коэфф. пульсации",
          Escape(groupDoc.RunPulseTimingCoefficient.ToString(CultureInfo.InvariantCulture)));
      AppendMetaRow(sb, "Членов в группе",
          Escape(results.Count.ToString(CultureInfo.InvariantCulture)));
      AppendMetaRow(sb, "Дата",
          Escape(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)));
      sb.AppendLine("</table>");

      // Сводная таблица исходов.
      int totalMismatch = details.Count(c => !c.Ok);
      sb.AppendLine("<h2>Сводка</h2>");
      sb.AppendLine("<table class=\"steps-zebra\"><thead><tr><th>№</th><th>ID</th><th>Название</th>"
          + "<th>Исход</th><th>Пульсов</th><th>Длит., с</th><th>Расхождений</th></tr></thead><tbody>");
      foreach (var r in results)
      {
        var e = r.EventArgs;
        string outcome = e == null ? "не завершён" : DescribeOutcome(e);
        int memberMismatch = 0;
        if (e != null && r.Document != null)
        {
          var agg = VelumScenarioLogComparer.AggregateByPulseInWindow(e.RunStartLocal, e.RunEndLocal);
          memberMismatch = VelumScenarioLogComparer.Compare(r.Document, e.AnchorGlobalPulse, agg)
              .Count(c => !c.Ok);
        }
        string outcomeClass = e != null && e.Success && memberMismatch == 0 ? "summary-ok" : "summary-bad";

        sb.AppendLine("<tr>");
        sb.Append("<td>").Append((r.MemberIndex + 1).ToString(CultureInfo.InvariantCulture)).Append("</td>");
        sb.Append("<td>").Append(Escape(r.ScenarioId.ToString(CultureInfo.InvariantCulture))).Append("</td>");
        sb.Append("<td>").Append(Escape(r.Title ?? "")).Append("</td>");
        sb.Append("<td class=\"\"").Append(outcomeClass).Append("\">").Append(Escape(outcome)).Append("</td>");
        sb.Append("<td>").Append(Escape((e?.ElapsedPulses ?? 0).ToString(CultureInfo.InvariantCulture))).Append("</td>");
        sb.Append("<td>").Append(Escape((e?.ElapsedWallTime.TotalSeconds ?? 0).ToString("F2", CultureInfo.InvariantCulture))).Append("</td>");
        sb.Append("<td>").Append(Escape(memberMismatch.ToString(CultureInfo.InvariantCulture))).Append("</td>");
        sb.AppendLine("</tr>");
      }
      sb.AppendLine("</tbody></table>");

      // Детальные разделы по каждому члену.
      foreach (var r in results)
      {
        if (r.EventArgs == null || r.Document == null)
          continue;
        var e = r.EventArgs;
        int anchor = e.AnchorGlobalPulse;
        var agg = VelumScenarioLogComparer.AggregateByPulseInWindow(e.RunStartLocal, e.RunEndLocal);
        var cmp = VelumScenarioLogComparer.Compare(r.Document, anchor, agg);

        sb.AppendLine("<hr style=\"margin:28px 0;\"/>");
        sb.Append("<h2>Сценарий ").Append(Escape((r.MemberIndex + 1).ToString(CultureInfo.InvariantCulture)))
            .Append(": ID ").Append(Escape(r.ScenarioId.ToString(CultureInfo.InvariantCulture)))
            .Append(" — ").Append(Escape(r.Title ?? "")).AppendLine("</h2>");
        sb.AppendLine("<p>").Append(Escape(BuildSummaryParagraph(e))).AppendLine("</p>");
        AppendScenarioBody(sb, r.Document, anchor, agg, cmp);
        AppendEventLog(sb, anchor, e.LastExecutedPulseWithinScenario);
      }

      sb.AppendLine("<p class=\"muted\" style=\"margin-top:24px;font-size:11px;\">Сформировано Velum. "
          + "Всего расхождений: " + Escape(totalMismatch.ToString(CultureInfo.InvariantCulture)) + ".</p>");
      sb.AppendLine("</body></html>");

      string fileName = string.Format(
          CultureInfo.InvariantCulture,
          "velum_batch_{0:yyyyMMdd_HHmmss}.html",
          DateTime.Now);
      string path = Path.Combine(reportFolder, fileName);
      File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
      Logger.Info("Velum group HTML report: " + path);

      return new ReportResult { HtmlPath = path, CompareResults = details };
    }

    // ===== Одиночный HTML =====

    private static string BuildSingleHtml(
        ScenarioDocument doc,
        OperatorScenarioCompletedEventArgs completion,
        int anchor,
        Dictionary<int, VelumScenarioLogComparer.AggregatedLogSnapshot> agg,
        List<VelumScenarioLogComparer.StepCompareResult> compareList)
    {
      var sb = new StringBuilder();
      sb.AppendLine("<!DOCTYPE html>");
      sb.AppendLine("<html><head><meta charset=\"utf-8\"/>");
      AppendStyles(sb);
      sb.AppendLine("</head><body>");
      sb.AppendLine("<h1>Отчёт по прогону сценария Velum</h1>");
      sb.AppendLine("<p>").Append(Escape(BuildSummaryParagraph(completion))).AppendLine("</p>");
      AppendScenarioBody(sb, doc, anchor, agg, compareList);
      AppendEventLog(sb, anchor, completion.LastExecutedPulseWithinScenario);
      sb.AppendLine("<p class=\"muted\" style=\"margin-top:24px;font-size:11px;\">Сформировано Velum.</p>");
      sb.AppendLine("</body></html>");
      return sb.ToString();
    }

    private static void AppendScenarioBody(
        StringBuilder sb,
        ScenarioDocument doc,
        int anchor,
        Dictionary<int, VelumScenarioLogComparer.AggregatedLogSnapshot> agg,
        List<VelumScenarioLogComparer.StepCompareResult> compareList)
    {
      // Мета-данные сценария.
      sb.AppendLine("<h2>Данные сценария</h2>");
      sb.AppendLine("<table class=\"meta-table\">");
      AppendMetaRow(sb, "ID", Escape(doc.Header?.Id.ToString(CultureInfo.InvariantCulture) ?? ""));
      AppendMetaRow(sb, "Название", Escape(doc.Header?.Title ?? ""));
      string descHtml = WebUtility.HtmlEncode(doc.Header?.Description ?? "")
          .Replace("\r\n", "\n").Replace("\n", "<br/>");
      sb.AppendLine("<tr><th class=\"meta-label\">Описание</th><td class=\"meta-value\""
          + descHtml + "</td></tr>");
      AppendMetaRow(sb, "Переход перед запуском (стадия)",
          Escape(doc.Header != null && doc.Header.PreRunTargetStage >= 0 && doc.Header.PreRunTargetStage <= 5
              ? doc.Header.PreRunTargetStage.ToString(CultureInfo.InvariantCulture)
              : "не менять"));
      AppendMetaRow(sb, "Очистка данных при переходе", Escape(doc.Header?.PreRunClearAgentData == true ? "да" : "нет"));
      AppendMetaRow(sb, "Сброс параметров в «норму»", Escape(doc.Header?.PreRunNormalHomeostasisState == true ? "да" : "нет"));
      AppendMetaRow(sb, "Режим наблюдения", Escape(doc.Header?.ScenarioObservationMode == true ? "да" : "нет"));
      AppendMetaRow(sb, "Якорный глобальный пульс", Escape(anchor.ToString(CultureInfo.InvariantCulture)));
      sb.AppendLine("</table>");

      // Шаги.
      sb.AppendLine("<h2>Шаги</h2>");
      sb.AppendLine("<table class=\"steps-zebra\"><thead><tr><th>Шаг</th><th>№ пульса</th><th>Тип</th>"
          + "<th>Воздействия</th><th>Команды</th><th>Фраза</th><th>Настр.</th><th>Цвет</th></tr></thead><tbody>");
      if (doc.Lines != null)
      {
        foreach (var line in doc.Lines.OrderBy(l => l.StepIndex))
        {
          sb.AppendLine("<tr>");
          sb.Append("<td>").Append(Escape(line.StepIndex.ToString(CultureInfo.InvariantCulture))).Append("</td>");
          sb.Append("<td>").Append(Escape(line.PulseWithinScenario.ToString(CultureInfo.InvariantCulture))).Append("</td>");
          sb.Append("<td>").Append(Escape(line.Kind == ScenarioLineKind.WaitClick ? "Ожидание" : "Пульт")).Append("</td>");
          sb.Append("<td>").Append(Escape(line.ActionNamesDisplay ?? line.ActionIdsText ?? "")).Append("</td>");
          sb.Append("<td>").Append(Escape(line.CommandPatternsDisplay ?? "")).Append("</td>");
          sb.Append("<td>").Append(Escape(line.Phrase ?? "")).Append("</td>");
          sb.Append("<td>").Append(Escape(line.MoodId.ToString(CultureInfo.InvariantCulture))).Append("</td>");
          sb.Append("<td>").Append(Escape(line.VisualColorId.ToString(CultureInfo.InvariantCulture))).Append("</td>");
          sb.AppendLine("</tr>");
        }
      }
      sb.AppendLine("</tbody></table>");

      // Сравнение ожиданий с фактом.
      sb.AppendLine("<h2>Сравнение ожидаемых реакций и факта по логам</h2>");
      sb.AppendLine("<p class=\"muted\">Глобальный пульс шага = якорь + № пульса внутри сценария. "
          + "Ячейка подсвечена красным при расхождении с ожиданием (ожидание — в подсказке наведении).</p>");
      AppendComparisonTable(sb, doc, anchor, agg);
      AppendComparisonSummary(sb, compareList);
    }

    private static void AppendComparisonTable(
        StringBuilder sb,
        ScenarioDocument doc,
        int anchor,
        Dictionary<int, VelumScenarioLogComparer.AggregatedLogSnapshot> agg)
    {
      var expectations = doc.LogExpectations ?? new List<ScenarioLogExpectationRow>();
      var columns = ComparisonColumnSpecs;

      sb.AppendLine("<table class=\"compare-table\"><thead><tr><th>Шаг</th><th>№ пульса</th>");
      foreach (var col in columns)
        sb.Append("<th class=\"col-fact-h\">").Append(Escape(col.Label)).Append("</th>");
      sb.AppendLine("</tr></thead><tbody>");

      if (doc.Lines != null)
      {
        foreach (var line in doc.Lines.OrderBy(l => l.StepIndex))
        {
          var snap = VelumScenarioLogComparer.ResolveSnapshot(anchor + line.PulseWithinScenario, agg);
          var exp = expectations.FirstOrDefault(e => e.StepIndex == line.StepIndex
                    && e.PulseWithinScenario == line.PulseWithinScenario)
              ?? expectations.FirstOrDefault(e => e.StepIndex == line.StepIndex)
              ?? new ScenarioLogExpectationRow();

          sb.AppendLine("<tr>");
          sb.Append("<td>").Append(Escape(line.StepIndex.ToString(CultureInfo.InvariantCulture))).Append("</td>");
          sb.Append("<td>").Append(Escape(line.PulseWithinScenario.ToString(CultureInfo.InvariantCulture))).Append("</td>");

          foreach (var col in columns)
          {
            string expRaw = col.GetExpectedMain(exp) ?? "";
            string actRaw = col.GetActual(snap) ?? "";
            string actDisp = col.FormatFact != null ? col.FormatFact(actRaw) : NormalizeDisplay(actRaw);
            bool mismatch = !string.IsNullOrWhiteSpace(expRaw) && !col.Matches(expRaw, actRaw);

            string cls = mismatch ? "col-fact fact-mismatch" : "col-fact";
            sb.Append("<td class=\"").Append(cls).Append("\"");
            if (!string.IsNullOrWhiteSpace(expRaw))
              sb.Append(" title=\"").Append(EscapeForTitle("Ожидание: " + expRaw)).Append("\"");
            sb.Append(">").Append(Escape(actDisp)).Append("</td>");
          }

          sb.AppendLine("</tr>");
        }
      }
      sb.AppendLine("</tbody></table>");
    }

    // ===== Событийные логи =====

    private static void AppendEventLog(StringBuilder sb, int anchor, int lastExecutedPulse)
    {
      sb.AppendLine("<h2>Событийные логи</h2>");
      ShortLogToken[] tokens;
      try
      {
        tokens = ShortLogChannel.GetCurrentSessionTokens();
      }
      catch (Exception ex)
      {
        sb.AppendLine("<p class=\"muted\">Не удалось прочитать событийный лог: ")
            .Append(Escape(ex.Message)).AppendLine("</p>");
        return;
      }

      int fromPulse = anchor;
      int toPulse = anchor + Math.Max(0, lastExecutedPulse);
      var visible = new List<ShortLogToken>();
      if (tokens != null)
      {
        foreach (var t in tokens)
        {
          if (t == null || t.Pulse <= 0) continue;
          if (t.Pulse < fromPulse || t.Pulse > toPulse) continue;
          visible.Add(t);
        }
      }

      if (visible.Count == 0)
      {
        sb.AppendLine("<p class=\"muted\">Событийных логов за пульсы прогона нет "
            + "(канал сокращённого лога пуст или сессия завершена).</p>");
        return;
      }

      sb.AppendLine("<p class=\"muted\">Токенов сокращённого лога: ")
          .Append(Escape(visible.Count.ToString(CultureInfo.InvariantCulture))).AppendLine("</p>");
      sb.AppendLine("<div class=\"event-log\">");
      foreach (var t in visible)
        AppendEventLogToken(sb, t, anchor);
      sb.AppendLine("</div>");
    }

    private static void AppendEventLogToken(StringBuilder sb, ShortLogToken token, int anchor)
    {
      string full = token.ToString() ?? string.Empty;
      int nl = full.IndexOf('\n');
      string rest = nl < 0 ? string.Empty : full.Substring(nl + 1);
      string firstLine = nl < 0 ? full : full.Substring(0, nl);

      // firstLine: «№ пульса: 1007 [×3]» — заменяем абсолютный номер на относительный.
      int afterColon = firstLine.IndexOf(':');
      int numStart = afterColon >= 0 ? afterColon + 1 : 0;
      while (numStart < firstLine.Length && char.IsWhiteSpace(firstLine[numStart]))
        numStart++;
      int numEnd = numStart;
      while (numEnd < firstLine.Length && char.IsDigit(firstLine[numEnd]))
        numEnd++;

      string repeat = "";
      if (numEnd < firstLine.Length && char.IsWhiteSpace(firstLine[numEnd]))
      {
        int xIdx = firstLine.IndexOf("×", numEnd, StringComparison.Ordinal);
        if (xIdx >= 0)
          repeat = firstLine.Substring(xIdx);
      }

      int relPulse = token.Pulse - anchor;
      firstLine = firstLine.Substring(0, numStart) + relPulse.ToString(CultureInfo.InvariantCulture) + repeat;

      sb.Append("<div class=\"ev-token\"><span class=\"ev-pulse\"\">")
          .Append(Escape(firstLine)).Append("</span>");
      if (rest.Length > 0)
        sb.Append('\n').Append(Escape(rest));
      sb.AppendLine("</div>");
    }

    // ===== Итоги =====

    private static void AppendComparisonSummary(
        StringBuilder sb,
        List<VelumScenarioLogComparer.StepCompareResult> compareList)
    {
      var failed = compareList.Where(c => !c.Ok).ToList();
      sb.AppendLine("<div class=\"summary-box\">");
      sb.AppendLine("<h3>Итог сравнения ожиданий с логами</h3>");
      if (failed.Count == 0)
      {
        sb.AppendLine("<p class=\"summary-ok\">Расхождений не обнаружено.</p>");
      }
      else
      {
        sb.AppendLine("<p class=\"summary-bad\">Обнаружены расхождения в строках:</p>");
        sb.AppendLine("<ul>");
        foreach (var c in failed)
        {
          sb.Append("<li><strong>Шаг ")
              .Append(Escape(c.StepIndex.ToString(CultureInfo.InvariantCulture)))
              .Append(", № пульса ")
              .Append(Escape(c.PulseWithinScenario.ToString(CultureInfo.InvariantCulture)))
              .Append(".</strong> ")
              .Append(Escape(c.Details)).AppendLine("</li>");
        }
        sb.AppendLine("</ul>");
      }
      sb.AppendLine("</div>");
    }

    // ===== Утилиты =====

    private static void AppendStyles(StringBuilder sb)
    {
      sb.AppendLine("<style>");
      sb.AppendLine("body{font-family:Segoe UI,Tahoma,sans-serif;margin:16px;color:#222;}");
      sb.AppendLine("h1{font-size:18px;color:#1565C0;}");
      sb.AppendLine("h2{font-size:15px;color:#37474F;margin-top:20px;border-bottom:1px solid #B0BEC5;padding-bottom:4px;}");
      sb.AppendLine("table{border-collapse:collapse;width:100%;margin:8px 0;font-size:12px;}");
      sb.AppendLine("th,td{border:1px solid #CFD8DC;padding:6px 8px;text-align:left;vertical-align:top;}");
      sb.AppendLine("th{background:#ECEFF1;font-weight:600;}");
      sb.AppendLine("table.steps-zebra tr:nth-child(even){background:#FAFAFA;}");
      sb.AppendLine("table.compare-table{font-size:11px;}");
      sb.AppendLine("table.compare-table thead th{position:sticky;top:0;z-index:2;box-shadow:inset 0 1px 0 #CFD8DC,inset 0 -1px 0 #90A4AE;}");
      sb.AppendLine("table.compare-table th.col-fact-h{background:#E3F2FD;}");
      sb.AppendLine("table.compare-table td.col-fact{background:#E3F2FD;}");
      sb.AppendLine("table.compare-table td.col-fact.fact-mismatch{background:#FFEBEE;color:#B71C1C;border-color:#FFCDD2;font-weight:600;}");
      sb.AppendLine("table.meta-table{width:100%;table-layout:fixed;}");
      sb.AppendLine("table.meta-table th.meta-label{width:18%;min-width:100px;max-width:200px;font-size:11px;font-weight:600;padding:6px 8px;vertical-align:top;background:#ECEFF1;}");
      sb.AppendLine("table.meta-table td.meta-value{width:82%;font-size:13px;padding:8px 12px;line-height:1.45;word-wrap:break-word;}");
      sb.AppendLine(".muted{color:#78909C;}");
      sb.AppendLine(".summary-box{margin-top:20px;padding:14px 16px;border-radius:6px;border:1px solid #CFD8DC;background:#FAFAFA;}");
      sb.AppendLine(".summary-box h3{margin:0 0 10px 0;font-size:14px;color:#37474F;}");
      sb.AppendLine(".summary-ok{color:#2E7D32;font-size:13px;font-weight:600;margin:0;}");
      sb.AppendLine(".summary-bad{color:#C62828;font-size:13px;font-weight:600;margin:0 0 8px 0;}");
      sb.AppendLine(".summary-box ul{margin:6px 0 0 0;padding-left:20px;}");
      sb.AppendLine(".summary-box li{margin:8px 0;font-size:12px;line-height:1.45;}");
      sb.AppendLine(".event-log{margin:8px 0;border:1px solid #CFD8DC;border-radius:6px;background:#FCFDFE;padding:8px 10px;max-height:520px;overflow:auto;}");
      sb.AppendLine(".event-log .ev-token{margin:0 0 8px 0;padding:6px 8px;border:1px solid #E0E6EA;border-radius:4px;background:#fff;font-family:Consolas,'Courier New',monospace;font-size:11.5px;line-height:1.4;white-space:pre-wrap;word-wrap:break-word;}");
      sb.AppendLine(".event-log .ev-token:last-child{margin-bottom:0;}");
      sb.AppendLine(".event-log .ev-pulse{color:#1565C0;font-weight:600;}");
      sb.AppendLine("</style>");
    }

    private static void AppendMetaRow(StringBuilder sb, string name, string valueHtmlEscaped)
    {
      sb.AppendLine("<tr><th class=\"meta-label\">").Append(Escape(name))
          .Append("</th><td class=\"meta-value\">").Append(valueHtmlEscaped).AppendLine("</td></tr>");
    }

    private static string BuildSummaryParagraph(OperatorScenarioCompletedEventArgs e)
    {
      if (e == null)
        return "Нет данных о завершении.";
      if (e.Success)
        return "Сценарий завершён успешно. Последний выполненный № пульса: "
            + e.LastExecutedPulseWithinScenario.ToString(CultureInfo.InvariantCulture) + ".";
      if (e.AbortedByUser)
        return "Сценарий остановлен пользователем (кнопка «Стоп»).";
      if (e.AbortedByPulsationStop)
        return "Сценарий прерван: остановлена пульсация.";
      if (!string.IsNullOrEmpty(e.ErrorMessage))
        return "Ошибка: " + e.ErrorMessage;
      return "Прогон завершён с нестандартным исходом.";
    }

    private static string DescribeOutcome(OperatorScenarioCompletedEventArgs e)
    {
      if (e.Success) return "успех";
      if (e.AbortedByUser) return "остановлен пользователем";
      if (e.AbortedByPulsationStop) return "прерван остановкой пульсации";
      return "ошибка";
    }

    private static string Escape(string s) => WebUtility.HtmlEncode(s ?? "");

    private static string EscapeForTitle(string s)
    {
      if (string.IsNullOrEmpty(s)) return "";
      var normalized = s.Replace("\r\n", "\n").Replace('\r', '\n');
      return WebUtility.HtmlEncode(normalized).Replace("\n", "&#10;");
    }

    private static string NormalizeDisplay(string raw)
    {
      if (string.IsNullOrWhiteSpace(raw)) return "-";
      var t = raw.Trim();
      return t.Length == 0 ? "-" : t;
    }

    // ===== Спецификации колонок сравнения =====

    private sealed class ComparisonColumnSpec
    {
      internal string Label { get; set; }
      internal Func<ScenarioLogExpectationRow, string> GetExpectedMain { get; set; }
      internal Func<VelumScenarioLogComparer.AggregatedLogSnapshot, string> GetActual { get; set; }
      internal Func<string, string, bool> Matches { get; set; }
      internal Func<string, string> FormatFact { get; set; }
    }

    private static readonly ComparisonColumnSpec[] ComparisonColumnSpecs =
    {
      new ComparisonColumnSpec { Label = "Состояние", GetExpectedMain = e => e.StateText, GetActual = a => a.State,
        Matches = (exp, act) => VelumScenarioLogComparer.ExpectationCellMatches(exp, VelumScenarioLogComparer.NormalizeDisplay(act)),
        FormatFact = raw => VelumScenarioLogComparer.FormatStateCell(raw) },
      new ComparisonColumnSpec { Label = "Стиль", GetExpectedMain = e => e.StyleText, GetActual = a => a.Style,
        Matches = (exp, act) => VelumScenarioLogComparer.ExpectationCellMatches(exp, VelumScenarioLogComparer.NormalizeDisplay(act)) },
      new ComparisonColumnSpec { Label = "Тема", GetExpectedMain = e => e.ThemeText, GetActual = a => a.Theme,
        Matches = (exp, act) => VelumScenarioLogComparer.ExpectationCellMatches(exp, VelumScenarioLogComparer.NormalizeDisplay(act)) },
      new ComparisonColumnSpec { Label = "Триггер", GetExpectedMain = e => e.TriggerText, GetActual = a => a.Trigger,
        Matches = (exp, act) => VelumScenarioLogComparer.ExpectationCellMatches(exp, VelumScenarioLogComparer.NormalizeDisplay(act)) },
      new ComparisonColumnSpec { Label = "ОР/УМ", GetExpectedMain = e => e.OrUmText, GetActual = a => a.OrUm,
        Matches = (exp, act) => VelumScenarioLogComparer.ExpectationCellMatches(exp, VelumScenarioLogComparer.NormalizeDisplay(act)) },
      new ComparisonColumnSpec { Label = "Опасно", GetExpectedMain = e => e.DangerText, GetActual = a => a.Danger,
        Matches = (exp, act) => VelumScenarioLogComparer.DangerExpectationMatches(exp, act),
        FormatFact = raw => VelumScenarioLogComparer.FormatDangerComparisonCell(raw) },
      new ComparisonColumnSpec { Label = "Актуально", GetExpectedMain = e => e.VeryActualText, GetActual = a => a.VeryActual,
        Matches = (exp, act) => VelumScenarioLogComparer.DangerExpectationMatches(exp, act),
        FormatFact = raw => VelumScenarioLogComparer.FormatDangerComparisonCell(raw) },
      new ComparisonColumnSpec { Label = "Б/у рефлекс", GetExpectedMain = e => e.GeneticReflexText, GetActual = a => a.GeneticReflex,
        Matches = (exp, act) => VelumScenarioLogComparer.ExpectationCellMatches(exp, VelumScenarioLogComparer.NormalizeDisplay(act)) },
      new ComparisonColumnSpec { Label = "Усл. рефлекс", GetExpectedMain = e => e.ConditionReflexText, GetActual = a => a.ConditionReflex,
        Matches = (exp, act) => VelumScenarioLogComparer.ExpectationCellMatches(exp, VelumScenarioLogComparer.NormalizeDisplay(act)) },
      new ComparisonColumnSpec { Label = "Автоматизм", GetExpectedMain = e => e.AutomatizmText, GetActual = a => a.Automatizm,
        Matches = (exp, act) => VelumScenarioLogComparer.ExpectationCellMatches(exp, VelumScenarioLogComparer.NormalizeDisplay(act)) },
      new ComparisonColumnSpec { Label = "Успешность", GetExpectedMain = e => e.AutomatizmUsefulnessText,
        GetActual = a => VelumScenarioLogComparer.FormatAutomatizmUsefulnessCell(a.AutomatizmUsefulnessLogged),
        Matches = (exp, act) => VelumScenarioLogComparer.ExpectationCellMatches(exp, VelumScenarioLogComparer.NormalizeDisplay(act)) },
      new ComparisonColumnSpec { Label = "Цепочка РФ", GetExpectedMain = e => e.ReflexChainText, GetActual = a => a.ReflexChain,
        Matches = (exp, act) => VelumScenarioLogComparer.ExpectationCellMatches(exp, VelumScenarioLogComparer.NormalizeDisplay(act)) },
      new ComparisonColumnSpec { Label = "Цепочка АВ", GetExpectedMain = e => e.AutomatizmChainText, GetActual = a => a.AutomatizmChain,
        Matches = (exp, act) => VelumScenarioLogComparer.ExpectationCellMatches(exp, VelumScenarioLogComparer.NormalizeDisplay(act)) },
      new ComparisonColumnSpec { Label = "Цикл М", GetExpectedMain = e => e.MainCycleText, GetActual = a => a.MainCycle,
        Matches = (exp, act) => VelumScenarioLogComparer.MainCycleExpectationMatches(exp, VelumScenarioLogComparer.NormalizeDisplay(act)) },
      new ComparisonColumnSpec { Label = "Циклы Ф", GetExpectedMain = e => e.BackgroundCyclesText, GetActual = a => a.BackgroundCycles,
        Matches = (exp, act) => VelumScenarioLogComparer.MainCycleExpectationMatches(exp, VelumScenarioLogComparer.NormalizeDisplay(act)) }
    };
  }
}
