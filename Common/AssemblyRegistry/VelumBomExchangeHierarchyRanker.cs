using System;
using System.Collections.Generic;

namespace Velum.UI.AssemblyRegistry
{
  /// <summary>
  /// Иерархический ранг узлов состава для порядка строк 1C_bom_*.csv.
  /// Граф строится по полному состоянию структур (bomStructure.json), а не только
  /// по расходящимся: ребро ParentIdentity → ChildIdentity для каждой строки состава.
  /// Ранг корня = 0, ранг потомка = ранг родителя + 1; строки сортируются
  /// по возрастанию ранга родителя — «сверху вниз», корневые сборки раньше
  /// вложенных, чтобы 1С применяла add родителя до add вложенной сборки.
  /// Обход детерминирован (корни и рёбра в стабильном порядке); циклы обрываются.
  /// </summary>
  internal sealed class VelumBomExchangeHierarchyRanker
  {
    /// <summary>Ранг узла по Identity родителя (FilePath|Config).</summary>
    private readonly Dictionary<string, int> _rankByParentIdentity =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Ранг по ExternalId родителя — для записей журнала, где Identity отсутствует.
    /// </summary>
    private readonly Dictionary<string, int> _rankByParentExternalId =
        new Dictionary<string, int>(StringComparer.Ordinal);

    private VelumBomExchangeHierarchyRanker()
    {
    }

    /// <summary>
    /// Построить ранкер по полному состоянию структур состава.
    /// </summary>
    /// <param name="structures">Все структуры (GetAllEntries), включая нерасходящиеся.</param>
    /// <returns>Ранкер (пустой, если структур нет).</returns>
    internal static VelumBomExchangeHierarchyRanker Build(
        IReadOnlyList<VelumBomStructureEntry> structures)
    {
      var ranker = new VelumBomExchangeHierarchyRanker();
      if (structures == null)
        return ranker;

      // Граф: ParentIdentity → ChildIdentity (стабильный порядок рёбер).
      var childrenByParent =
          new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
      var isChild = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (VelumBomStructureEntry entry in structures)
      {
        if (entry == null || string.IsNullOrWhiteSpace(entry.ParentIdentity))
          continue;
        string parent = entry.ParentIdentity.Trim();
        var children = new List<string>();
        if (entry.Lines != null)
        {
          foreach (VelumBomStructureLine line in entry.Lines)
          {
            if (line == null || string.IsNullOrWhiteSpace(line.ChildIdentity))
              continue;
            string child = line.ChildIdentity.Trim();
            children.Add(child);
            isChild.Add(child);
          }
        }
        children.Sort((a, b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase));
        childrenByParent[parent] = children;
      }

      // Корни — узлы, которые нигде не являются детьми; сортируем для детерминизма.
      var roots = new List<string>();
      foreach (string node in childrenByParent.Keys)
      {
        if (!isChild.Contains(node))
          roots.Add(node);
      }
      roots.Sort((a, b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase));

      // DFS от корней: ранг = глубина; visited + путь — обрыв циклов.
      var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      var path = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (string root in roots)
        Visit(root, 0, childrenByParent, ranker._rankByParentIdentity, visited, path);

      // Ранг по ParentExternalId — из структуры. Порядок GetAllEntries стабилен
      // (по ParentIdentity), поэтому при совпадении ExternalId у нескольких
      // структур побеждает первая по алфавиту.
      foreach (VelumBomStructureEntry entry in structures)
      {
        if (entry == null || string.IsNullOrWhiteSpace(entry.ParentIdentity) ||
            string.IsNullOrWhiteSpace(entry.ParentExternalId))
          continue;
        string externalId = entry.ParentExternalId.Trim();
        if (ranker._rankByParentExternalId.ContainsKey(externalId))
          continue;
        ranker._rankByParentExternalId[externalId] =
            ranker.RankOfParentIdentity(entry.ParentIdentity);
      }

      return ranker;
    }

    /// <summary>Ранг узла по Identity родителя; неизвестный узел — 0 (как корень).</summary>
    internal int RankOfParentIdentity(string parentIdentity)
    {
      if (string.IsNullOrWhiteSpace(parentIdentity))
        return 0;
      int rank;
      return _rankByParentIdentity.TryGetValue(parentIdentity.Trim(), out rank) ? rank : 0;
    }

    /// <summary>Ранг по ExternalId родителя; неизвестный — 0.</summary>
    internal int RankOfParentExternalId(string parentExternalId)
    {
      if (string.IsNullOrWhiteSpace(parentExternalId))
        return 0;
      int rank;
      return _rankByParentExternalId.TryGetValue(parentExternalId.Trim(), out rank) ? rank : 0;
    }

    /// <summary>
    /// Рекурсивный обход в глубину: присваивает ранг глубины, обрывает циклы
    /// (узел уже в текущем пути) и не пересматривает посещённые узлы.
    /// </summary>
    private static void Visit(
        string node,
        int depth,
        Dictionary<string, List<string>> childrenByParent,
        Dictionary<string, int> rankByParentIdentity,
        HashSet<string> visited,
        HashSet<string> path)
    {
      if (path.Contains(node) || visited.Contains(node))
        return; // цикл или уже вычисленный узел

      visited.Add(node);
      path.Add(node);
      rankByParentIdentity[node] = depth;

      List<string> children;
      if (childrenByParent.TryGetValue(node, out children))
      {
        foreach (string child in children)
          Visit(child, depth + 1, childrenByParent, rankByParentIdentity, visited, path);
      }

      path.Remove(node);
    }
  }
}