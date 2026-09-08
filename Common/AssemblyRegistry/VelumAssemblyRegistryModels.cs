using System;
using System.Collections.Generic;

namespace Velum.UI.AssemblyRegistry
{
  /// <summary>Классификация узла состава.</summary>
  internal enum VelumAssemblyRegistryNodeKind
  {
    Assembly = 0,
    Part = 1,
    Standard = 2
  }

  /// <summary>Агрегированный узел состава (path + configuration).</summary>
  internal sealed class VelumAssemblyRegistryComponent
  {
    internal string Identity { get; set; }

    internal string FilePath { get; set; }

    internal string FileTitle { get; set; }

    internal string ConfigurationName { get; set; }

    internal VelumAssemblyRegistryNodeKind Kind { get; set; }

    /// <summary>Сегменты каталога после префикса семейства (пусто = корень семейства).</summary>
    internal string[] FolderSegments { get; set; }

    /// <summary>Число вхождений в головной сборке.</summary>
    internal int Quantity { get; set; }

    internal string Designation { get; set; }

    internal string Name { get; set; }

    internal string Material { get; set; }

    internal string Mass { get; set; }

    internal string RolledStock { get; set; }

    internal string Thickness { get; set; }

    internal string Width { get; set; }

    internal string Length { get; set; }

    internal string TotalMass { get; set; }

    internal string TotalLength { get; set; }

    /// <summary>
    /// Кэш значений: custom props (config→document) + reserved (<c>FileName</c>, <c>Quantity</c>, алиасы).
    /// </summary>
    internal Dictionary<string, string> PropertyValues { get; set; }

    internal bool PropertiesLoaded { get; set; }

    internal VelumAssemblyRegistrySectionPath GetSectionPath()
    {
      return new VelumAssemblyRegistrySectionPath(
          VelumAssemblyRegistrySectionPath.FamilyFromNodeKind(Kind),
          FolderSegments);
    }
  }

  /// <summary>Результат обхода головной сборки (только оперативная память).</summary>
  internal sealed class VelumAssemblyRegistryGraph
  {
    internal Dictionary<string, VelumAssemblyRegistryComponent> Components { get; } =
        new Dictionary<string, VelumAssemblyRegistryComponent>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Прямые рёбра сборка → дочерняя сборка (identity).</summary>
    internal Dictionary<string, HashSet<string>> AssemblyChildren { get; } =
        new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

    internal string RootAssemblyPath { get; set; }

    internal string RootConfigurationName { get; set; }

    internal void Clear()
    {
      Components.Clear();
      AssemblyChildren.Clear();
      RootAssemblyPath = string.Empty;
      RootConfigurationName = string.Empty;
    }

    internal IReadOnlyList<VelumAssemblyRegistryComponent> GetComponentsByDocumentKey(string documentKey)
    {
      string key = VelumAssemblyRegistryPropertyReader.NormalizePath(documentKey);
      if (string.IsNullOrEmpty(key))
        return Array.Empty<VelumAssemblyRegistryComponent>();

      var result = new List<VelumAssemblyRegistryComponent>();
      foreach (VelumAssemblyRegistryComponent item in Components.Values)
      {
        if (string.Equals(
                VelumAssemblyRegistryPropertyReader.NormalizePath(item.FilePath),
                key,
                StringComparison.OrdinalIgnoreCase))
          result.Add(item);
      }

      return result;
    }

    internal HashSet<string> GetDescendantAssemblies(string assemblyIdentity)
    {
      var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      if (string.IsNullOrEmpty(assemblyIdentity))
        return result;

      var stack = new Stack<string>();
      HashSet<string> direct;
      if (AssemblyChildren.TryGetValue(assemblyIdentity, out direct))
      {
        foreach (string child in direct)
          stack.Push(child);
      }

      while (stack.Count > 0)
      {
        string id = stack.Pop();
        if (!result.Add(id))
          continue;

        HashSet<string> kids;
        if (!AssemblyChildren.TryGetValue(id, out kids))
          continue;

        foreach (string child in kids)
          stack.Push(child);
      }

      return result;
    }
  }

  /// <summary>Тег узла TreeView.</summary>
  internal sealed class VelumAssemblyRegistryTreeTag
  {
    internal enum TagKind
    {
      Folder,
      Component
    }

    internal TagKind Kind { get; set; }

    /// <summary>
    /// Ключ документа (нормализованный путь файла) для Kind=Component.
    /// Узел дерева = документ; конфигурации показываются в списке.
    /// </summary>
    internal string DocumentKey { get; set; }

    /// <summary>Путь каталога (для Kind=Folder).</summary>
    internal VelumAssemblyRegistrySectionPath FolderPath { get; set; }
  }
}
