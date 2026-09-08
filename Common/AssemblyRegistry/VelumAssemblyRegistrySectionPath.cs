using System;
using System.Collections.Generic;
using System.Text;

namespace Velum.UI.AssemblyRegistry
{
  /// <summary>Семейство каталога реестра изделия (префикс свойства «Раздел»).</summary>
  internal enum VelumAssemblyRegistryFamily
  {
    Assembly = 0,
    Part = 1,
    Standard = 2
  }

  /// <summary>
  /// Путь каталога: семейство + сегменты после префикса.
  /// Канон записи: корень Детали/Сборки → пустая строка; корень Стандарты → «Стандарты»;
  /// иначе «Префикс.Seg1.Seg2».
  /// </summary>
  internal sealed class VelumAssemblyRegistrySectionPath : IEquatable<VelumAssemblyRegistrySectionPath>
  {
    internal const string PrefixAssemblies = "Сборки";
    internal const string PrefixParts = "Детали";
    internal const string PrefixStandards = "Стандарты";
    internal const string PropSection = "Раздел";

    internal VelumAssemblyRegistryFamily Family { get; }

    /// <summary>Сегменты после префикса семейства (без самого «Сборки»/«Детали»/«Стандарты»).</summary>
    internal string[] Segments { get; }

    internal VelumAssemblyRegistrySectionPath(VelumAssemblyRegistryFamily family, string[] segments)
    {
      Family = family;
      Segments = segments == null || segments.Length == 0
          ? Array.Empty<string>()
          : (string[])segments.Clone();
    }

    internal bool IsRoot
    {
      get { return Segments.Length == 0; }
    }

    internal string Prefix
    {
      get { return GetPrefix(Family); }
    }

    internal static string GetPrefix(VelumAssemblyRegistryFamily family)
    {
      switch (family)
      {
        case VelumAssemblyRegistryFamily.Assembly:
          return PrefixAssemblies;
        case VelumAssemblyRegistryFamily.Standard:
          return PrefixStandards;
        default:
          return PrefixParts;
      }
    }

    internal static VelumAssemblyRegistryFamily FamilyFromNodeKind(VelumAssemblyRegistryNodeKind kind)
    {
      switch (kind)
      {
        case VelumAssemblyRegistryNodeKind.Assembly:
          return VelumAssemblyRegistryFamily.Assembly;
        case VelumAssemblyRegistryNodeKind.Standard:
          return VelumAssemblyRegistryFamily.Standard;
        default:
          return VelumAssemblyRegistryFamily.Part;
      }
    }

    internal static VelumAssemblyRegistryNodeKind NodeKindFromFamily(VelumAssemblyRegistryFamily family)
    {
      switch (family)
      {
        case VelumAssemblyRegistryFamily.Assembly:
          return VelumAssemblyRegistryNodeKind.Assembly;
        case VelumAssemblyRegistryFamily.Standard:
          return VelumAssemblyRegistryNodeKind.Standard;
        default:
          return VelumAssemblyRegistryNodeKind.Part;
      }
    }

    /// <summary>
    /// Разбор сырого «Раздел». Невалидный префикс → false (считается пустым).
    /// </summary>
    internal static bool TryParse(string raw, out VelumAssemblyRegistrySectionPath path)
    {
      path = null;
      string text = (raw ?? string.Empty).Trim();
      if (text.Length == 0)
        return false;

      string[] parts = text.Split(new[] { '.' }, StringSplitOptions.None);
      if (parts.Length == 0)
        return false;

      string head = (parts[0] ?? string.Empty).Trim();
      VelumAssemblyRegistryFamily family;
      if (string.Equals(head, PrefixAssemblies, StringComparison.OrdinalIgnoreCase))
        family = VelumAssemblyRegistryFamily.Assembly;
      else if (string.Equals(head, PrefixParts, StringComparison.OrdinalIgnoreCase))
        family = VelumAssemblyRegistryFamily.Part;
      else if (string.Equals(head, PrefixStandards, StringComparison.OrdinalIgnoreCase))
        family = VelumAssemblyRegistryFamily.Standard;
      else
        return false;

      var segments = new List<string>();
      for (int i = 1; i < parts.Length; i++)
      {
        string seg = SanitizeFolderName(parts[i]);
        if (string.IsNullOrEmpty(seg))
          continue;
        segments.Add(seg);
      }

      path = new VelumAssemblyRegistrySectionPath(family, segments.ToArray());
      return true;
    }

    /// <summary>
    /// Классификация по типу файла и сырому «Раздел».
    /// Чужой префикс для типа файла игнорируется (как пустое свойство).
    /// </summary>
    internal static void Classify(
        bool isAssemblyDocument,
        string sectionRaw,
        out VelumAssemblyRegistryNodeKind kind,
        out string[] folderSegments)
    {
      folderSegments = Array.Empty<string>();
      VelumAssemblyRegistrySectionPath parsed;
      bool hasPath = TryParse(sectionRaw, out parsed);

      if (isAssemblyDocument)
      {
        kind = VelumAssemblyRegistryNodeKind.Assembly;
        if (hasPath && parsed.Family == VelumAssemblyRegistryFamily.Assembly)
          folderSegments = parsed.Segments;
        return;
      }

      if (hasPath && parsed.Family == VelumAssemblyRegistryFamily.Standard)
      {
        kind = VelumAssemblyRegistryNodeKind.Standard;
        folderSegments = parsed.Segments;
        return;
      }

      kind = VelumAssemblyRegistryNodeKind.Part;
      if (hasPath && parsed.Family == VelumAssemblyRegistryFamily.Part)
        folderSegments = parsed.Segments;
    }

    /// <summary>Каноническое значение для записи в config вхождения.</summary>
    internal string ToPropertyValue()
    {
      if (Segments.Length == 0)
      {
        // Корень Детали/Сборки — пусто; корень Стандарты — префикс (иначе станет деталью).
        if (Family == VelumAssemblyRegistryFamily.Standard)
          return PrefixStandards;
        return string.Empty;
      }

      var sb = new StringBuilder();
      sb.Append(Prefix);
      for (int i = 0; i < Segments.Length; i++)
      {
        sb.Append('.');
        sb.Append(Segments[i]);
      }

      return sb.ToString();
    }

    internal VelumAssemblyRegistrySectionPath AppendSegment(string segment)
    {
      string sanitized = SanitizeFolderName(segment);
      if (string.IsNullOrEmpty(sanitized))
        throw new ArgumentException("Недопустимое имя каталога.", nameof(segment));

      var next = new string[Segments.Length + 1];
      Array.Copy(Segments, next, Segments.Length);
      next[Segments.Length] = sanitized;
      return new VelumAssemblyRegistrySectionPath(Family, next);
    }

    internal VelumAssemblyRegistrySectionPath WithRenamedLeaf(string newLeafName)
    {
      if (Segments.Length == 0)
        throw new InvalidOperationException("Корневой каталог нельзя переименовать.");

      string sanitized = SanitizeFolderName(newLeafName);
      if (string.IsNullOrEmpty(sanitized))
        throw new ArgumentException("Недопустимое имя каталога.", nameof(newLeafName));

      var next = (string[])Segments.Clone();
      next[next.Length - 1] = sanitized;
      return new VelumAssemblyRegistrySectionPath(Family, next);
    }

    /// <summary>
    /// Удаление каталога path: срезает сегмент на глубине path у всех потомков
    /// (K1.K2.K3 → K1.K3 при удалении K2).
    /// </summary>
    internal static bool TryRemoveDeletedSegment(
        VelumAssemblyRegistrySectionPath itemPath,
        VelumAssemblyRegistrySectionPath deletedFolder,
        out VelumAssemblyRegistrySectionPath result)
    {
      result = null;
      if (itemPath == null || deletedFolder == null)
        return false;
      if (itemPath.Family != deletedFolder.Family)
        return false;
      if (deletedFolder.IsRoot)
        return false;
      if (!IsPrefix(itemPath.Segments, deletedFolder.Segments))
        return false;

      int cut = deletedFolder.Segments.Length - 1;
      var list = new List<string>(itemPath.Segments.Length - 1);
      for (int i = 0; i < itemPath.Segments.Length; i++)
      {
        if (i == cut)
          continue;
        list.Add(itemPath.Segments[i]);
      }

      result = new VelumAssemblyRegistrySectionPath(itemPath.Family, list.ToArray());
      return true;
    }

    /// <summary>
    /// Переименование листа deletedFolder → newFolder (тот же родитель, другое имя листа).
    /// </summary>
    internal static bool TryRewriteRenamedPrefix(
        VelumAssemblyRegistrySectionPath itemPath,
        VelumAssemblyRegistrySectionPath oldFolder,
        VelumAssemblyRegistrySectionPath newFolder,
        out VelumAssemblyRegistrySectionPath result)
    {
      result = null;
      if (itemPath == null || oldFolder == null || newFolder == null)
        return false;
      if (itemPath.Family != oldFolder.Family || oldFolder.Family != newFolder.Family)
        return false;
      if (oldFolder.Segments.Length == 0 || oldFolder.Segments.Length != newFolder.Segments.Length)
        return false;
      if (!IsPrefix(itemPath.Segments, oldFolder.Segments))
        return false;

      var list = new List<string>(itemPath.Segments.Length);
      for (int i = 0; i < itemPath.Segments.Length; i++)
      {
        if (i < newFolder.Segments.Length)
          list.Add(newFolder.Segments[i]);
        else
          list.Add(itemPath.Segments[i]);
      }

      result = new VelumAssemblyRegistrySectionPath(itemPath.Family, list.ToArray());
      return true;
    }

    /// <summary>
    /// Перемещение каталога sourceFolder → newFolderLocation (включая всех потомков).
    /// Префикс sourceFolder у пути элемента заменяется на newFolderLocation (семейство — целевое).
    /// </summary>
    internal static bool TryRewriteMovedPrefix(
        VelumAssemblyRegistrySectionPath itemPath,
        VelumAssemblyRegistrySectionPath sourceFolder,
        VelumAssemblyRegistrySectionPath newFolderLocation,
        out VelumAssemblyRegistrySectionPath result)
    {
      result = null;
      if (itemPath == null || sourceFolder == null || newFolderLocation == null)
        return false;
      if (sourceFolder.IsRoot)
        return false;
      if (itemPath.Family != sourceFolder.Family)
        return false;
      if (!IsPrefix(itemPath.Segments, sourceFolder.Segments))
        return false;

      int tail = itemPath.Segments.Length - sourceFolder.Segments.Length;
      var list = new List<string>(newFolderLocation.Segments.Length + Math.Max(0, tail));
      for (int i = 0; i < newFolderLocation.Segments.Length; i++)
        list.Add(newFolderLocation.Segments[i]);
      for (int i = sourceFolder.Segments.Length; i < itemPath.Segments.Length; i++)
        list.Add(itemPath.Segments[i]);

      result = new VelumAssemblyRegistrySectionPath(newFolderLocation.Family, list.ToArray());
      return true;
    }

    /// <summary>Нельзя бросать каталог в себя или в собственного потомка.</summary>
    internal static bool IsInvalidFolderDropTarget(
        VelumAssemblyRegistrySectionPath sourceFolder,
        VelumAssemblyRegistrySectionPath targetFolder)
    {
      if (sourceFolder == null || targetFolder == null)
        return true;
      if (sourceFolder.Family != targetFolder.Family &&
          !CanMove(sourceFolder.Family, targetFolder.Family))
        return true;
      if (sourceFolder.Family == targetFolder.Family &&
          IsPrefix(targetFolder.Segments, sourceFolder.Segments))
        return true;
      return false;
    }

    internal static bool IsPrefix(string[] path, string[] prefix)
    {
      if (path == null || prefix == null)
        return false;
      if (prefix.Length > path.Length)
        return false;
      for (int i = 0; i < prefix.Length; i++)
      {
        if (!string.Equals(path[i], prefix[i], StringComparison.OrdinalIgnoreCase))
          return false;
      }

      return true;
    }

    internal static bool CanMove(
        VelumAssemblyRegistryFamily sourceFamily,
        VelumAssemblyRegistryFamily targetFamily)
    {
      if (sourceFamily == targetFamily)
        return true;
      // Детали ↔ Стандарты.
      if (sourceFamily == VelumAssemblyRegistryFamily.Assembly ||
          targetFamily == VelumAssemblyRegistryFamily.Assembly)
        return false;
      return true;
    }

    /// <summary>«.» → «_»; trim; пустые / только _ и пробелы → пустая строка (недопустимо).</summary>
    internal static string SanitizeFolderName(string name)
    {
      if (name == null)
        return string.Empty;

      var sb = new StringBuilder(name.Length);
      for (int i = 0; i < name.Length; i++)
      {
        char c = name[i];
        sb.Append(c == '.' ? '_' : c);
      }

      string trimmed = sb.ToString().Trim();
      if (trimmed.Length == 0)
        return string.Empty;

      bool onlyNoise = true;
      for (int i = 0; i < trimmed.Length; i++)
      {
        char c = trimmed[i];
        if (c != '_' && !char.IsWhiteSpace(c))
        {
          onlyNoise = false;
          break;
        }
      }

      return onlyNoise ? string.Empty : trimmed;
    }

    internal string ToStableKey()
    {
      var sb = new StringBuilder();
      sb.Append((int)Family);
      for (int i = 0; i < Segments.Length; i++)
      {
        sb.Append('|');
        sb.Append(Segments[i]);
      }

      return sb.ToString();
    }

    internal string ToDisplayPath()
    {
      if (Segments.Length == 0)
        return Prefix;
      return Prefix + "." + string.Join(".", Segments);
    }

    public bool Equals(VelumAssemblyRegistrySectionPath other)
    {
      if (other == null)
        return false;
      if (Family != other.Family || Segments.Length != other.Segments.Length)
        return false;
      for (int i = 0; i < Segments.Length; i++)
      {
        if (!string.Equals(Segments[i], other.Segments[i], StringComparison.OrdinalIgnoreCase))
          return false;
      }

      return true;
    }

    public override bool Equals(object obj)
    {
      return Equals(obj as VelumAssemblyRegistrySectionPath);
    }

    public override int GetHashCode()
    {
      int hash = (int)Family;
      for (int i = 0; i < Segments.Length; i++)
        hash = (hash * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(Segments[i] ?? string.Empty);
      return hash;
    }
  }
}
