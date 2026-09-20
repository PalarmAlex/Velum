using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Velum.Configuration;

namespace Velum.UI.ProductRegistry
{
  /// <summary>
  /// HTML-отчёт реестра документов по текущему списку записей.
  /// Столбцы: Обозначение, Наименование, Файл, Статус.
  /// Строки сгруппированы по узлам дерева реестра (каталогам).
  /// </summary>
  internal static class VelumProductRegistryReportHtmlBuilder
  {
    internal static string ReportsFolderPath =>
        Path.Combine(VelumAppConfig.ProductRegistryFolderPath, "Reports");

    /// <summary>Имя файла без пути.</summary>
    internal static string BuildFileName(DateTime stamp)
    {
      return "Реестр_документов_" + stamp.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".html";
    }

    internal static string BuildHtml(
        string selectionLabel,
        List<VelumProductItem> rows,
        Dictionary<int, VelumProductFolder> folderLookup,
        int skipLastFolderLevels = 0)
    {
      var sb = new StringBuilder();
      sb.AppendLine("<!DOCTYPE html>");
      sb.AppendLine("<html><head><meta charset=\"utf-8\"/>");
      AppendStyles(sb);
      sb.AppendLine("</head><body>");
      sb.AppendLine("<h1>Реестр документов</h1>");

      sb.AppendLine("<h2>Сведения</h2>");
      sb.AppendLine("<table class=\"meta-table\">");
      AppendMetaRow(sb, "Область", selectionLabel);
      AppendMetaRow(
          sb,
          "Сформировано",
          DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.GetCultureInfo("ru-RU")));
      AppendMetaRow(
          sb,
          "Записей",
          rows.Count.ToString(CultureInfo.InvariantCulture));
      sb.AppendLine("</table>");

      sb.AppendLine("<h2>Список</h2>");
      int expectedCount = rows.Count;
      int renderedCount = AppendDataTable(sb, rows, folderLookup, skipLastFolderLevels);

      sb.AppendLine("<p class=\"muted footer\">Сформировано Velum. Отрендерено записей: " +
          "<span id=\"renderedCount\">" + renderedCount + "</span> из " + expectedCount + ".</p>");
      sb.AppendLine("</body></html>");
      return sb.ToString();
    }

    private static int AppendDataTable(
        StringBuilder sb,
        List<VelumProductItem> rows,
        Dictionary<int, VelumProductFolder> folderLookup,
        int skipLastFolderLevels)
    {
      if (rows == null || rows.Count == 0)
      {
        sb.AppendLine("<p class=\"muted\">Нет записей для отчёта.</p>");
        return 0;
      }

      var folderPaths = new Dictionary<int, string>();
      foreach (var kvp in folderLookup)
      {
        folderPaths[kvp.Key] = BuildFolderPath(kvp.Value, folderLookup, skipLastFolderLevels);
      }

      var groups = new Dictionary<int, List<VelumProductItem>>();
      var groupOrder = new List<int>();

      foreach (var item in rows)
      {
        int folderId = item.FolderId;
        if (!groups.TryGetValue(folderId, out var list))
        {
          list = new List<VelumProductItem>();
          groups[folderId] = list;
          groupOrder.Add(folderId);
        }
        list.Add(item);
      }

      // Порядок групп — DFS-обход дерева, как на форме.
      var folderById = new Dictionary<int, VelumProductFolder>();
      foreach (var kvp in folderLookup)
        folderById[kvp.Key] = kvp.Value;

      // Строим дерево родитель-ребёнок.
      var childrenByParent = new Dictionary<int, List<VelumProductFolder>>();
      foreach (var folder in folderById.Values)
      {
        int parentId = folder.ParentId;
        if (!childrenByParent.TryGetValue(parentId, out var children))
        {
          children = new List<VelumProductFolder>();
          childrenByParent[parentId] = children;
        }
        children.Add(folder);
      }

      var orderedFolderIds = new List<int>();
      CollectFoldersInTreeOrder(0, childrenByParent, orderedFolderIds);

      // Оставляем только те папки, которые есть в группах с записями.
      groupOrder.Clear();
      foreach (int id in orderedFolderIds)
      {
        if (groups.ContainsKey(id))
          groupOrder.Add(id);
      }

      sb.AppendLine("<table class=\"data-zebra\">");

      sb.Append("<tr>");
      sb.Append("<th>").Append(Escape("Обозначение")).Append("</th>");
      sb.Append("<th>").Append(Escape("Наименование")).Append("</th>");
      sb.Append("<th>").Append(Escape("Файл")).Append("</th>");
      sb.Append("<th>").Append(Escape("Статус")).Append("</th>");
      sb.AppendLine("</tr>");

      // Строим иерархическое дерево из путей каталогов.
      var treeRoot = BuildGroupsTree(groupOrder, groups, folderPaths);

      // Рендерим дерево с удалёнными дублирующимися заголовками.
      int renderedCount = 0;
      RenderTree(sb, treeRoot, folderLookup, ref renderedCount);

      sb.AppendLine("</table>");
      return renderedCount;
    }

    private static string BuildFolderPath(
        VelumProductFolder folder,
        Dictionary<int, VelumProductFolder> lookup,
        int skipLastLevels)
    {
      if (folder == null)
        return string.Empty;

      var parts = new List<string>();
      int currentId = folder.Id;
      int depth = 0;
      while (currentId > 0 && depth < 50)
      {
        // Пропускаем корневой узел (Изделия) — не включаем его в путь.
        if (folder.ParentId <= 0)
          break;

        parts.Insert(0, folder.Name ?? string.Empty);
        if (!lookup.TryGetValue(folder.ParentId, out folder))
          break;
        currentId = folder.ParentId;
        depth++;
      }

      // Усечение хвоста пути (для «сокращённой ведомости» при активном фильтре дерева).
      // Не уходим в пустой путь: минимум один сегмент должен остаться,
      // иначе записи попадут в невидимый корневой узел отчёта.
      if (skipLastLevels > 0 && parts.Count > 1)
      {
        int remove = Math.Min(skipLastLevels, parts.Count - 1);
        parts.RemoveRange(parts.Count - remove, remove);
      }

      return string.Join(@"\", parts);
    }

    private static void CollectFoldersInTreeOrder(
        int parentId,
        Dictionary<int, List<VelumProductFolder>> childrenByParent,
        List<int> target)
    {
      List<VelumProductFolder> children;
      if (!childrenByParent.TryGetValue(parentId, out children))
        return;

      // Сортировка детей по SortOrder — как в GetChildFolders.
      children.Sort((a, b) =>
      {
        int cmp = a.SortOrder.CompareTo(b.SortOrder);
        if (cmp != 0)
          return cmp;
        return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
      });

      foreach (VelumProductFolder child in children)
      {
        target.Add(child.Id);
        CollectFoldersInTreeOrder(child.Id, childrenByParent, target);
      }
    }

    private static string GetStatusText(VelumProductItem item)
    {
      if (string.IsNullOrEmpty(item.FilePath))
        return "Не указан";
      // Ключ FilePath хранится относительным корню документов — достраиваем до полного.
      if (File.Exists(Velum.ReactiveCore.Export.VelumRelativeDocumentPathResolver.ToFull(item.FilePath)))
        return "OK";
      return "Отсутствует";
    }

    /// <summary>
    /// Узел дерева для иерархического рендеринга групп.
    /// </summary>
    private class GroupNode
    {
      public string Name { get; set; } = string.Empty;
      /// <summary>Глубина узла в дереве (0 — виртуальный корень, 1 — первый уровень каталогов).</summary>
      public int Depth { get; set; } = 0;
      public int FolderId { get; set; } = 0;
      public List<VelumProductItem> Items { get; set; } = new List<VelumProductItem>();
      public bool HasItems => Items.Count > 0;
      public List<GroupNode> Children { get; } = new List<GroupNode>();

      /// <summary>
      /// true, если хотя бы один дочерний узел содержит записи (напрямую или через потомков).
      /// </summary>
      public bool HasChildWithItems { get; set; } = false;
    }

    /// <summary>
    /// Строит иерархическое дерево из сгруппированных путей каталогов.
    /// Общие префиксы путей объединяются в родительские узлы,
    /// чтобы повторяющийся текст выводился только один раз по центру строки.
    /// </summary>
    private static GroupNode BuildGroupsTree(
        List<int> groupOrder,
        Dictionary<int, List<VelumProductItem>> groups,
        Dictionary<int, string> folderPaths)
    {
      var root = new GroupNode { Name = string.Empty };

      foreach (int folderId in groupOrder)
      {
        var items = groups[folderId];
        if (items == null || items.Count == 0)
          continue;

        string path = folderPaths.ContainsKey(folderId)
            ? folderPaths[folderId]
            : string.Empty;

        var parts = string.IsNullOrEmpty(path)
            ? Array.Empty<string>()
            : path.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);

        var current = root;
        for (int i = 0; i < parts.Length; i++)
        {
          var child = current.Children.FirstOrDefault(c => c.Name == parts[i]);
          if (child == null)
          {
            child = new GroupNode { Name = parts[i], Depth = i + 1 };
            current.Children.Add(child);
          }

          // Последний сегмент соответствует реальной папке с записями.
          if (i == parts.Length - 1)
          {
            child.FolderId = folderId;
            child.Items.AddRange(items);
          }

          current = child;
        }

        // Если частей нет (корневая папка), помечаем сам корень.
        if (parts.Length == 0)
        {
          root.Items.AddRange(items);
        }
      }

      // Помечаем узлы, у которых есть дочерние узлы с записями.
      MarkParents(root);

      return root;
    }

    /// <summary>
    /// Рекурсивно помечает HasChildWithItems для каждого предка.
    /// </summary>
    private static void MarkParents(GroupNode node)
    {
      foreach (var child in node.Children)
      {
        MarkParents(child);
        if (child.HasChildWithItems || child.HasItems)
          node.HasChildWithItems = true;
      }
    }

    /// <summary>
    /// Рендерит дерево групп в HTML-таблицу.
    /// Родительские узлы выровнены по центру; листовые узлы (с записями) — по левому краю.
    /// </summary>
    private static void RenderTree(StringBuilder sb, GroupNode node, Dictionary<int, VelumProductFolder> folderLookup, ref int renderedCount)
    {
      foreach (var child in node.Children)
        RenderNode(sb, child, folderLookup, ref renderedCount);
    }

    /// <summary>
    /// Рендерит один узел и его потомков.
    /// </summary>
    private static void RenderNode(StringBuilder sb, GroupNode node, Dictionary<int, VelumProductFolder> folderLookup, ref int renderedCount)
    {
      // Определяем выравнивание: по центру, когда узел является родительским (имеет потомков с записями),
      // и не содержит записей напрямую ИЛИ это узел первого уровня (корневой каталог); иначе — по левому краю.
      bool isCentered = node.HasChildWithItems && (!node.HasItems || node.Depth == 1);

      // Считаем общее количество записей в узле и потомках для заголовка.
      int totalItems = CountDescendantItems(node);

      // Строка заголовка группы.
      sb.Append("<tr class=\"group-header\"><td colspan=\"4\"");
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

      // Если в узле есть записи, выводим строки данных.
      if (node.HasItems)
      {
        foreach (var item in node.Items)
        {
          if (item == null)
            continue;

          renderedCount++;
          sb.Append("<tr>");
          sb.Append("<td>").Append(Escape(item.Designation ?? string.Empty)).Append("</td>");
          sb.Append("<td>").Append(Escape(item.Name ?? string.Empty)).Append("</td>");
          sb.Append("<td>").Append(Escape(Velum.ReactiveCore.Export.VelumRelativeDocumentPathResolver.ToFull(item.FilePath) ?? string.Empty)).Append("</td>");
          sb.Append("<td>").Append(Escape(GetStatusText(item))).Append("</td>");
          sb.AppendLine("</tr>");
        }
      }

      // Рендерим дочерние узлы (суб-каталоги).
      foreach (var child in node.Children)
        RenderNode(sb, child, folderLookup, ref renderedCount);
    }

    /// <summary>
    /// Считает все записи в узле и его потомках.
    /// </summary>
    private static int CountDescendantItems(GroupNode node)
    {
      int count = node.Items.Count;
      foreach (var child in node.Children)
        count += CountDescendantItems(child);
      return count;
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

    private static string Escape(string text)
    {
      return WebUtility.HtmlEncode(text ?? string.Empty);
    }
  }
}
