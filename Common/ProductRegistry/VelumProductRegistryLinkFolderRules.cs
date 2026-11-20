using System;
using System.Collections.Generic;

namespace Velum.UI.ProductRegistry
{
  /// <summary>
  /// Чистые правила выбора узла, на котором раскрывать дерево при «Связать с узлом».
  /// Работает только с идентификаторами каталогов и делегатом поиска непосредственного
  /// родителя — без стора, COM и WinForms, поэтому линкуется в тестовый проект
  /// Velum.ReactiveCore.Tests.
  /// </summary>
  internal static class VelumProductRegistryLinkFolderRules
  {
    /// <summary>
    /// Определяет начальный каталог для раскрытия дерева привязки по набору каталогов
    /// выделенных записей:
    /// <list type="bullet">
    /// <item>все записи в одном каталоге — этот каталог (раскрыть прямо на нём);</item>
    /// <item>записи в разных каталогах, но у всех каталогов один общий непосредственный
    /// родитель — этот родитель (дерево раскрывается на 1 уровень выше самих каталогов);</item>
    /// <item>родители различаются (хотя бы одна запись в ветке с другим родителем) —
    /// <c>null</c>, дерево остаётся свёрнутым на корне (как раньше).</item>
    /// </list>
    /// </summary>
    /// <param name="folderIds">Идентификаторы каталогов выделенных записей (могут повторяться).</param>
    /// <param name="getParentId">Функция непосредственного родителя: 0 — корень/нет родителя.</param>
    /// <returns>Идентификатор каталога для раскрытия либо <c>null</c>.</returns>
    internal static int? ResolveInitialFolderId(
        IReadOnlyList<int> folderIds,
        Func<int, int> getParentId)
    {
      if (folderIds == null || folderIds.Count == 0 || getParentId == null)
        return null;

      // Уникальные непустые каталоги выделенных записей.
      var unique = new List<int>();
      var seen = new HashSet<int>();
      for (int i = 0; i < folderIds.Count; i++)
      {
        int id = folderIds[i];
        if (id <= 0)
          continue;
        if (seen.Add(id))
          unique.Add(id);
      }

      if (unique.Count == 0)
        return null;

      // Одна папка (или несколько записей в ней) — раскрываем прямо на ней.
      if (unique.Count == 1)
        return unique[0];

      // Разные папки: раскрываем на общем непосредственном родителе, только если он один.
      int commonParent = ParentOf(getParentId, unique[0]);
      if (commonParent <= 0)
        return null;

      for (int i = 1; i < unique.Count; i++)
      {
        if (ParentOf(getParentId, unique[i]) != commonParent)
          return null;
      }

      return commonParent;
    }

    /// <summary>
    /// Непосредственный родитель с защитой: при исключении из делегата считаем вершину (0),
    /// чтобы битый реестр не подвешивал UI.
    /// </summary>
    private static int ParentOf(Func<int, int> getParentId, int folderId)
    {
      try
      {
        int parent = getParentId(folderId);
        return parent > 0 ? parent : 0;
      }
      catch
      {
        return 0;
      }
    }
  }
}