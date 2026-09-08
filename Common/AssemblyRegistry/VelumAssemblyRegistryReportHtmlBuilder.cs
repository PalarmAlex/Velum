using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Velum.Configuration;

namespace Velum.UI.AssemblyRegistry
{
  /// <summary>
  /// HTML-отчёт реестра изделия по текущему шаблону столбцов и набору строк списка
  /// (по образцу отчётов сценариев).
  /// </summary>
  internal static class VelumAssemblyRegistryReportHtmlBuilder
  {
    internal static string ReportsFolderPath =>
        Path.Combine(VelumAppConfig.AssemblyRegistryFolderPath, "Reports");

    internal static string BuildHtml(
        string templateName,
        string assemblyPath,
        string configurationName,
        string selectionLabel,
        IList<VelumAssemblyRegistryColumnDef> columns,
        IList<VelumAssemblyRegistryComponent> rows,
        int productQty)
    {
      var sb = new StringBuilder();
      sb.AppendLine("<!DOCTYPE html>");
      sb.AppendLine("<html><head><meta charset=\"utf-8\"/>");
      AppendStyles(sb);
      sb.AppendLine("</head><body>");
      sb.AppendLine("<h1>Реестр изделия</h1>");

      sb.AppendLine("<h2>Сведения</h2>");
      sb.AppendLine("<table class=\"meta-table\">");
      AppendMetaRow(sb, "Шаблон", templateName);
      AppendMetaRow(sb, "Изделие", assemblyPath);
      AppendMetaRow(sb, "Конфигурация", configurationName);
      AppendMetaRow(sb, "Область", selectionLabel);
      AppendMetaRow(sb, "Количество изделий", productQty.ToString(CultureInfo.InvariantCulture));
      AppendMetaRow(sb, "Строк", (rows == null ? 0 : rows.Count).ToString(CultureInfo.InvariantCulture));

      VelumAssemblyRegistryColumnDef groupColumn =
          VelumAssemblyRegistryColumnStore.FindGroupByColumn(columns);
      if (groupColumn != null)
      {
        string caption = VelumAssemblyRegistryColumnStore.CaptionFromName(groupColumn.Name);
        if (string.IsNullOrEmpty(caption))
          caption = groupColumn.Name ?? " ";
        AppendMetaRow(sb, "Группировка", caption);
      }

      AppendMetaRow(
          sb,
          "Сформировано",
          DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.GetCultureInfo("ru-RU")));
      sb.AppendLine("</table>");

      sb.AppendLine("<h2>Список</h2>");
      AppendDataTable(sb, columns, rows, productQty);

      sb.AppendLine("<p class=\"muted footer\">Сформировано Velum.</p>");
      sb.AppendLine("</body></html>");
      return sb.ToString();
    }

    /// <summary>Имя файла без пути: <c>Реестр_&lt;шаблон&gt;_yyyyMMdd_HHmmss.html</c>.</summary>
    internal static string BuildFileName(string templateName, DateTime stamp)
    {
      string part = SanitizeFilePart(templateName);
      if (string.IsNullOrEmpty(part))
        part = "Шаблон";
      return "Реестр_" + part + "_" + stamp.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".html";
    }

    internal static string SanitizeFilePart(string name)
    {
      string text = (name ?? string.Empty).Trim();
      if (text.Length == 0)
        return string.Empty;

      var sb = new StringBuilder(text.Length);
      foreach (char c in text)
      {
        if (c < 32 || c == '<' || c == '>' || c == ':' || c == '"' || c == '/' || c == '\\' ||
            c == '|' || c == '?' || c == '*')
          sb.Append('_');
        else
          sb.Append(c);
      }

      string result = sb.ToString().Trim();
      while (result.IndexOf("  ", StringComparison.Ordinal) >= 0)
        result = result.Replace("  ", " ");
      return result.Replace(' ', '_');
    }

    /// <summary>
    /// HTML-отчёт «Состав сборки»: только выбранные компоненты, сгруппированные по узлам дерева реестра.
    /// </summary>
    internal static string BuildAssemblyCompositionHtml(
        VelumAssemblyRegistryGraph graph,
        IList<VelumAssemblyRegistryComponent> components,
        IList<VelumAssemblyRegistryColumnDef> columns)
    {
      var sb = new StringBuilder();
      sb.AppendLine("<!DOCTYPE html>");
      sb.AppendLine("<html><head><meta charset=\"utf-8\"/>");
      AppendCompositionStyles(sb);
      sb.AppendLine("</head><body>");
      sb.AppendLine("<h1>Состав сборки</h1>");

      string assemblyPath = graph?.RootAssemblyPath ?? string.Empty;
      string configName = graph?.RootConfigurationName ?? string.Empty;

      var allComponents = new List<VelumAssemblyRegistryComponent>();
      if (components != null)
      {
        foreach (var comp in components)
          if (comp != null)
            allComponents.Add(comp);
      }

      // Метаданные.
      sb.AppendLine("<h2>Сведения</h2>");
      sb.AppendLine("<table class=\"meta-table\">");
      AppendMetaRow(sb, "Изделие", assemblyPath);
      AppendMetaRow(sb, "Конфигурация", configName);
      int assemblyCount = 0, partCount = 0, standardCount = 0;
      foreach (var comp in allComponents)
      {
        switch (comp.Kind)
        {
          case VelumAssemblyRegistryNodeKind.Assembly: assemblyCount++; break;
          case VelumAssemblyRegistryNodeKind.Part: partCount++; break;
          case VelumAssemblyRegistryNodeKind.Standard: standardCount++; break;
        }
      }
      sb.AppendLine("<tr><th class=\"meta-label\">Сборок</th><td class=\"meta-value\">")
          .Append(assemblyCount.ToString(System.Globalization.CultureInfo.InvariantCulture))
          .AppendLine("</td></tr>");
      sb.AppendLine("<tr><th class=\"meta-label\">Деталей</th><td class=\"meta-value\">")
          .Append(partCount.ToString(System.Globalization.CultureInfo.InvariantCulture))
          .AppendLine("</td></tr>");
      sb.AppendLine("<tr><th class=\"meta-label\">Стандартных изделий</th><td class=\"meta-value\">")
          .Append(standardCount.ToString(System.Globalization.CultureInfo.InvariantCulture))
          .AppendLine("</td></tr>");
      AppendMetaRow(
          sb,
          "Сформировано",
          DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.GetCultureInfo("ru-RU")));
      sb.AppendLine("</table>");

      // Группируем по узлам дерева (Family + FolderSegments).
      sb.AppendLine("<h2>Состав</h2>");
      AppendGroupedTable(sb, allComponents);

      sb.AppendLine("<p class=\"muted footer\">Сформировано Velum.</p>");
      sb.AppendLine("</body></html>");
      return sb.ToString();
    }

    /// <summary>
    /// HTML-отчёт «Состав сборки»: компоненты сгруппированы по узлам дерева реестра
    /// (Сборки, Детали, Стандарты → каталоги), с иерархическим рендерингом.
    /// </summary>
    internal static string BuildAssemblyCompositionHtml(
        VelumAssemblyRegistryGraph graph,
        IList<VelumAssemblyRegistryColumnDef> columns)
    {
      var sb = new StringBuilder();
      sb.AppendLine("<!DOCTYPE html>");
      sb.AppendLine("<html><head><meta charset=\"utf-8\"/>");
      AppendCompositionStyles(sb);
      sb.AppendLine("</head><body>");
      sb.AppendLine("<h1>Состав сборки</h1>");

      string assemblyPath = graph?.RootAssemblyPath ?? string.Empty;
      string configName = graph?.RootConfigurationName ?? string.Empty;

      // Собираем все компоненты.
      var allComponents = new List<VelumAssemblyRegistryComponent>();
      if (graph?.Components != null)
      {
        foreach (var kvp in graph.Components)
        {
          if (kvp.Value != null)
            allComponents.Add(kvp.Value);
        }
      }

      // Метаданные.
      sb.AppendLine("<h2>Сведения</h2>");
      sb.AppendLine("<table class=\"meta-table\">");
      AppendMetaRow(sb, "Изделие", assemblyPath);
      AppendMetaRow(sb, "Конфигурация", configName);
      int assemblyCount = 0, partCount = 0, standardCount = 0;
      foreach (var comp in allComponents)
      {
        switch (comp.Kind)
        {
          case VelumAssemblyRegistryNodeKind.Assembly: assemblyCount++; break;
          case VelumAssemblyRegistryNodeKind.Part: partCount++; break;
          case VelumAssemblyRegistryNodeKind.Standard: standardCount++; break;
        }
      }
      sb.AppendLine("<tr><th class=\"meta-label\">Сборок</th><td class=\"meta-value\">")
          .Append(assemblyCount.ToString(System.Globalization.CultureInfo.InvariantCulture))
          .AppendLine("</td></tr>");
      sb.AppendLine("<tr><th class=\"meta-label\">Деталей</th><td class=\"meta-value\">")
          .Append(partCount.ToString(System.Globalization.CultureInfo.InvariantCulture))
          .AppendLine("</td></tr>");
      sb.AppendLine("<tr><th class=\"meta-label\">Стандартных изделий</th><td class=\"meta-value\">")
          .Append(standardCount.ToString(System.Globalization.CultureInfo.InvariantCulture))
          .AppendLine("</td></tr>");
      AppendMetaRow(
          sb,
          "Сформировано",
          DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.GetCultureInfo("ru-RU")));
      sb.AppendLine("</table>");

      // Группируем по узлам дерева (Family + FolderSegments).
      sb.AppendLine("<h2>Состав</h2>");
      AppendGroupedTable(sb, allComponents);

      sb.AppendLine("<p class=\"muted footer\">Сформировано Velum.</p>");
      sb.AppendLine("</body></html>");
      return sb.ToString();
    }

    /// <summary>
    /// Рендерит таблицу, сгруппированную по узлам дерева реестра (Kind → FolderSegments).
    /// Стиль как в Product Registry: иерархические заголовки групп, строки компонентов.
    /// </summary>
    private static void AppendGroupedTable(StringBuilder sb, List<VelumAssemblyRegistryComponent> components)
    {
      if (components == null || components.Count == 0)
      {
        sb.AppendLine("<p class=\"muted\">Нет данных.</p>");
        return;
      }

      sb.AppendLine("<table class=\"data-zebra\">");

      // Заголовки.
      sb.Append("<tr>");
      sb.Append("<th>").Append(Escape("Обозначение")).Append("</th>");
      sb.Append("<th>").Append(Escape("Наименование")).Append("</th>");
      sb.Append("<th>").Append(Escape("Кол-во")).Append("</th>");
      sb.AppendLine("</tr>");

      // Рендерим каждое семейство (Сборки, Детали, Стандарты).
      foreach (var family in new[] { VelumAssemblyRegistryFamily.Assembly, VelumAssemblyRegistryFamily.Part, VelumAssemblyRegistryFamily.Standard })
      {
        // Собираем компоненты данного семейства.
        var familyItems = new List<VelumAssemblyRegistryComponent>();
        foreach (var comp in components)
          if (VelumAssemblyRegistrySectionPath.FamilyFromNodeKind(comp.Kind) == family)
            familyItems.Add(comp);

        if (familyItems.Count == 0)
          continue;

        string familyPrefix = VelumAssemblyRegistrySectionPath.GetPrefix(family);

        // Группируем по FolderSegments внутри семейства.
        var segmentsGroups = new Dictionary<string[], List<VelumAssemblyRegistryComponent>>(new SegmentsComparer());
        foreach (var item in familyItems)
        {
          var segs = item.FolderSegments ?? Array.Empty<string>();
          if (!segmentsGroups.TryGetValue(segs, out var list))
          {
            list = new List<VelumAssemblyRegistryComponent>();
            segmentsGroups[segs] = list;
          }
          list.Add(item);
        }

        // Строим дерево из сегментов.
        var root = new GroupNode { Name = familyPrefix, Depth = 1, Family = family };

        foreach (var kvp in segmentsGroups)
        {
          var segs = kvp.Key;
          var items = kvp.Value;

          var current = root;
          for (int i = 0; i < segs.Length; i++)
          {
            var child = current.Children.FirstOrDefault(c => c.Name == segs[i]);
            if (child == null)
            {
              child = new GroupNode { Name = segs[i], Depth = i + 2, Family = family };
              current.Children.Add(child);
            }
            current = child;
          }
          current.Items.AddRange(items);
        }

        MarkParents(root);
        RenderGroupNode(sb, root);
      }

      sb.AppendLine("</table>");
    }

    /// <summary>
    /// Сравнение массивов сегментов для словаря.
    /// </summary>
    private sealed class SegmentsComparer : IEqualityComparer<string[]>
    {
      public bool Equals(string[] x, string[] y)
      {
        if (x == null && y == null) return true;
        if (x == null || y == null) return false;
        if (x.Length != y.Length) return false;
        for (int i = 0; i < x.Length; i++)
        {
          if (!string.Equals(x[i], y[i], StringComparison.OrdinalIgnoreCase))
            return false;
        }
        return true;
      }

      public int GetHashCode(string[] obj)
      {
        if (obj == null) return 0;
        int hash = 0;
        foreach (var s in obj)
          hash = (hash * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(s ?? string.Empty);
        return hash;
      }
    }

    /// <summary>
    /// Узел дерева для иерархического рендеринга групп.
    /// </summary>
    private class GroupNode
    {
      public string Name { get; set; } = string.Empty;
      public int Depth { get; set; } = 0;
      public VelumAssemblyRegistryFamily Family { get; set; }
      public List<VelumAssemblyRegistryComponent> Items { get; } = new List<VelumAssemblyRegistryComponent>();
      public bool HasItems => Items.Count > 0;
      public List<GroupNode> Children { get; } = new List<GroupNode>();
      public bool HasChildWithItems { get; set; } = false;
    }

    private static void MarkParents(GroupNode node)
    {
      foreach (var child in node.Children)
      {
        MarkParents(child);
        if (child.HasChildWithItems || child.HasItems)
          node.HasChildWithItems = true;
      }
    }

    private static void RenderGroupNode(StringBuilder sb, GroupNode node)
    {
      bool isCentered = node.HasChildWithItems && (!node.HasItems || node.Depth == 1);
      int totalItems = CountDescendantItems(node);

      // Заголовок группы.
      sb.Append("<tr class=\"group-header\"><td colspan=\"3\"");
      if (isCentered)
        sb.Append(" class=\"centered\"");
      sb.Append(">");
      sb.Append("<strong>").Append(Escape(node.Name)).Append("</strong>");
      if (totalItems > 0)
      {
        sb.Append(" <span class=\"muted\">(")
          .Append(totalItems.ToString(CultureInfo.InvariantCulture))
          .Append(")</span>");
      }
      sb.AppendLine("</td></tr>");

      // Строки компонентов.
      if (node.HasItems)
      {
        // Сортируем: сборки первыми, затем детали/стандарты.
        var sorted = new List<VelumAssemblyRegistryComponent>(node.Items);
        sorted.Sort((a, b) =>
        {
          int cmp = ((int)a.Kind).CompareTo((int)b.Kind);
          if (cmp != 0) return cmp;
          return string.Compare(a.Designation ?? string.Empty, b.Designation ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        });

        foreach (var item in sorted)
        {
          sb.Append("<tr>");
          sb.Append("<td>").Append(Escape(item.Designation ?? string.Empty)).Append("</td>");
          sb.Append("<td>").Append(Escape(item.Name ?? string.Empty)).Append("</td>");
          sb.Append("<td>").Append(Escape(item.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture))).Append("</td>");
          sb.AppendLine("</tr>");
        }
      }

      // Дочерние узлы.
      foreach (var child in node.Children)
        RenderGroupNode(sb, child);
    }

    private static int CountDescendantItems(GroupNode node)
    {
      int count = node.Items.Count;
      foreach (var child in node.Children)
        count += CountDescendantItems(child);
      return count;
    }

    private static void AppendCompositionStyles(StringBuilder sb)
    {
      sb.AppendLine("<style>");
      sb.AppendLine("body{font-family:Segoe UI,Tahoma,sans-serif;margin:10px;color:#222;}");
      sb.AppendLine("h1{font-size:14px;color:#1565C0;margin:0 0 4px;font-weight:600;}");
      sb.AppendLine("h2{font-size:12px;color:#37474F;margin:8px 0 2px;border-bottom:1px solid #B0BEC5;padding-bottom:2px;}");
      sb.AppendLine("table{border-collapse:collapse;width:100%;margin:4px 0;font-size:13px;}");
      sb.AppendLine("th,td{border:1px solid #CFD8DC;padding:2px 6px;text-align:left;vertical-align:middle;line-height:1.25;color:#000;}");
      sb.AppendLine("th{background:#ECEFF1;font-weight:600;}");
      sb.AppendLine("table.data-zebra tr:nth-child(even){background:#FAFAFA;}");
      sb.AppendLine("table.data-zebra tr.group-header{background:#E3F2FD;font-weight:600;}");
      sb.AppendLine("table.data-zebra tr.group-header td{padding:3px 6px;}");
      sb.AppendLine(".muted{color:#78909C;}");
      sb.AppendLine(".footer{margin-top:10px;font-size:10px;}");
      sb.AppendLine("table.meta-table{width:100%;table-layout:fixed;margin:2px 0 4px;}");
      sb.AppendLine("table.meta-table th,table.meta-table td{vertical-align:middle;}");
      sb.AppendLine("table.meta-table th.meta-label{width:12%;min-width:80px;max-width:120px;font-size:10px;font-weight:600;padding:1px 6px;background:#ECEFF1;}");
      sb.AppendLine("table.meta-table td.meta-value{width:88%;font-size:11px;padding:1px 6px;line-height:1.25;word-wrap:break-word;}");
      sb.AppendLine(".centered{text-align:center;}");
      sb.AppendLine("@media print{body{margin:6px;} h1{color:#000;} th{background:#eee !important;-webkit-print-color-adjust:exact;print-color-adjust:exact;} td{color:#000 !important;}}");
      sb.AppendLine("</style>");
    }

    private static void AppendDataTable(
        StringBuilder sb,
        IList<VelumAssemblyRegistryColumnDef> columns,
        IList<VelumAssemblyRegistryComponent> rows,
        int productQty)
    {
      if (columns == null || columns.Count == 0)
      {
        sb.AppendLine("<p class=\"muted\">Нет столбцов в текущем шаблоне.</p>");
        return;
      }

      // Фильтруем только видимые колонки.
      var visibleColumns = new List<VelumAssemblyRegistryColumnDef>();
      foreach (VelumAssemblyRegistryColumnDef column in columns)
      {
        if (column != null && column.ShowInList)
          visibleColumns.Add(column);
      }

      if (visibleColumns.Count == 0)
      {
        sb.AppendLine("<p class=\"muted\">Нет отображаемых столбцов.</p>");
        return;
      }

      sb.AppendLine("<table class=\"data-zebra\">");
      sb.Append("<tr>");
      foreach (VelumAssemblyRegistryColumnDef column in visibleColumns)
      {
        string caption = VelumAssemblyRegistryColumnStore.CaptionFromName(column.Name);
        if (string.IsNullOrEmpty(caption))
          caption = " ";
        sb.Append("<th>").Append(Escape(caption)).Append("</th>");
      }
      sb.AppendLine("</tr>");

      VelumAssemblyRegistryColumnDef groupColumn =
          VelumAssemblyRegistryColumnStore.FindGroupByColumn(visibleColumns);
      bool hasTotals = HasAnyTotals(visibleColumns);

      if (groupColumn == null)
      {
        AppendPlainRows(sb, columns, rows, productQty);
        if (hasTotals)
          AppendTotalsRow(sb, columns, rows, "Итого", "totals-row", productQty);
      }
      else
      {
        List<GroupBucket> groups = BuildGroups(groupColumn, rows);
        string groupCaption = VelumAssemblyRegistryColumnStore.CaptionFromName(groupColumn.Name);
        if (string.IsNullOrEmpty(groupCaption))
          groupCaption = "Группа";

        for (int g = 0; g < groups.Count; g++)
        {
          GroupBucket bucket = groups[g];
          sb.Append("<tr class=\"group-header\"><td colspan=\"")
              .Append(visibleColumns.Count.ToString(CultureInfo.InvariantCulture))
              .Append("\"><strong>")
              .Append(Escape(groupCaption))
              .Append(": ")
              .Append(Escape(bucket.DisplayKey))
              .Append("</strong> <span class=\"muted\">(")
              .Append(bucket.Rows.Count.ToString(CultureInfo.InvariantCulture))
              .Append(")</span></td></tr>");

          AppendPlainRows(sb, visibleColumns, bucket.Rows, productQty);
          if (hasTotals)
            AppendTotalsRow(sb, visibleColumns, bucket.Rows, "Итого группы", "group-totals-row", productQty);
        }

        if (hasTotals && rows != null && rows.Count > 0)
          AppendTotalsRow(sb, visibleColumns, rows, "Всего", "totals-row", productQty);
      }

      sb.AppendLine("</table>");
    }

    private static void AppendPlainRows(
        StringBuilder sb,
        IList<VelumAssemblyRegistryColumnDef> columns,
        IList<VelumAssemblyRegistryComponent> rows,
        int productQty)
    {
      if (rows == null)
        return;

      foreach (VelumAssemblyRegistryComponent item in rows)
      {
        if (item == null)
          continue;

        sb.Append("<tr>");
        foreach (VelumAssemblyRegistryColumnDef column in columns)
        {
          string value = VelumAssemblyRegistryColumnResolver.ResolveDisplay(column, item, productQty) ?? string.Empty;
          sb.Append("<td>").Append(Escape(value)).Append("</td>");
        }
        sb.AppendLine("</tr>");
      }
    }

    private static void AppendTotalsRow(
        StringBuilder sb,
        IList<VelumAssemblyRegistryColumnDef> columns,
        IList<VelumAssemblyRegistryComponent> rows,
        string label,
        string cssClass,
        int productQty)
    {
      sb.Append("<tr class=\"").Append(cssClass).Append("\">");
      bool labelPlaced = false;
      for (int i = 0; i < columns.Count; i++)
      {
        VelumAssemblyRegistryColumnDef column = columns[i];
        string total = VelumAssemblyRegistryColumnTotals.ComputeDisplay(column, rows, productQty) ?? string.Empty;
        if (total.Length > 0 && column != null)
        {
          string kind = VelumAssemblyRegistryColumnTotals.TotalsKindDisplayName(column.TotalsKind);
          string prefix = labelPlaced ? string.Empty : (Escape(label) + " · ");
          labelPlaced = true;
          sb.Append("<td><strong>").Append(prefix).Append(Escape(kind)).Append(": ")
              .Append(Escape(total)).Append("</strong></td>");
        }
        else if (!labelPlaced && i == 0)
        {
          labelPlaced = true;
          sb.Append("<td><strong>").Append(Escape(label)).Append("</strong></td>");
        }
        else
          sb.Append("<td></td>");
      }
      sb.AppendLine("</tr>");
    }

    private static bool HasAnyTotals(IList<VelumAssemblyRegistryColumnDef> columns)
    {
      for (int i = 0; i < columns.Count; i++)
      {
        VelumAssemblyRegistryColumnDef column = columns[i];
        if (column != null && column.TotalsKind != VelumAssemblyRegistryColumnTotalsKind.None)
          return true;
      }

      return false;
    }

    private sealed class GroupBucket
    {
      internal string SortKey;
      internal string DisplayKey;
      internal List<VelumAssemblyRegistryComponent> Rows =
          new List<VelumAssemblyRegistryComponent>();
    }

    private static List<GroupBucket> BuildGroups(
        VelumAssemblyRegistryColumnDef groupColumn,
        IList<VelumAssemblyRegistryComponent> rows)
    {
      var map = new Dictionary<string, GroupBucket>(StringComparer.OrdinalIgnoreCase);
      var order = new List<string>();

      if (rows != null)
      {
        foreach (VelumAssemblyRegistryComponent item in rows)
        {
          if (item == null)
            continue;

          string display = VelumAssemblyRegistryColumnResolver.ResolveDisplay(groupColumn, item) ?? string.Empty;
          display = display.Trim();
          string key = display.Length == 0 ? string.Empty : display;
          string displayKey = display.Length == 0 ? "(пусто)" : display;

          GroupBucket bucket;
          if (!map.TryGetValue(key, out bucket))
          {
            bucket = new GroupBucket
            {
              SortKey = key,
              DisplayKey = displayKey
            };
            map[key] = bucket;
            order.Add(key);
          }

          bucket.Rows.Add(item);
        }
      }

      order.Sort(StringComparer.CurrentCultureIgnoreCase);
      var result = new List<GroupBucket>(order.Count);
      for (int i = 0; i < order.Count; i++)
        result.Add(map[order[i]]);
      return result;
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
      sb.AppendLine("h2{font-size:12px;color:#37474F;margin:8px 0 2px;border-bottom:1px solid #B0BEC5;padding-bottom:2px;}");
      sb.AppendLine("table{border-collapse:collapse;width:100%;margin:4px 0;font-size:13px;}");
      sb.AppendLine("th,td{border:1px solid #CFD8DC;padding:2px 6px;text-align:left;vertical-align:middle;line-height:1.25;color:#000;}");
      sb.AppendLine("th{background:#ECEFF1;font-weight:600;}");
      sb.AppendLine("table.data-zebra tr:nth-child(even){background:#FAFAFA;}");
      sb.AppendLine("table.data-zebra tr.group-header{background:#E3F2FD;font-weight:600;}");
      sb.AppendLine("table.data-zebra tr.group-header td{padding:3px 6px;}");
      sb.AppendLine("table.data-zebra tr.group-totals-row{background:#FFF3E0;font-weight:600;}");
      sb.AppendLine("table.data-zebra tr.totals-row{background:#FFF8E1;font-weight:600;}");
      sb.AppendLine(".muted{color:#78909C;}");
      sb.AppendLine(".footer{margin-top:10px;font-size:10px;}");
      sb.AppendLine("table.meta-table{width:100%;table-layout:fixed;margin:2px 0 4px;}");
      sb.AppendLine("table.meta-table th,table.meta-table td{vertical-align:middle;}");
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
