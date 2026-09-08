using System;
using System.IO;

namespace Velum.UI.ProductRegistry
{
  /// <summary>
  /// Ключ сопоставления детали/сборки с чертежом в реестре.
  /// Предпочтительно <see cref="VelumProductItem.Designation"/> (обозначение в реестре);
  /// если пусто — basename из <see cref="VelumProductItem.FilePath"/> (имя файла без расширения).
  /// </summary>
  internal static class VelumProductRegistryMatchKey
  {
    internal static string FromItem(VelumProductItem item)
    {
      if (item == null)
        return string.Empty;

      string designation = (item.Designation ?? string.Empty).Trim();
      if (designation.Length > 0)
        return designation;

      return FromFilePath(item.FilePath);
    }

    internal static string FromFilePath(string filePath)
    {
      string path = VelumProductRegistryStore.NormalizeFilePathKey(filePath);
      if (string.IsNullOrEmpty(path))
        return string.Empty;

      try
      {
        return (Path.GetFileNameWithoutExtension(path) ?? string.Empty).Trim();
      }
      catch
      {
        return string.Empty;
      }
    }

    internal static bool EqualsKeys(string a, string b)
    {
      return string.Equals(
          (a ?? string.Empty).Trim(),
          (b ?? string.Empty).Trim(),
          StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Ключ сопоставления чертежа / PDF / DXF с деталью или сборкой.
    /// Для DXF — первая часть обозначения (или basename) до разделителя
    /// «:» / «∶» (U+2236) / «：» (U+FF1A) / пробела.
    /// </summary>
    internal static string FromRelatedDocument(VelumProductItem item)
    {
      string key = FromItem(item);
      if (string.IsNullOrEmpty(key))
        return string.Empty;

      if (item != null && VelumProductRegistryIntegrityRules.IsDxfPath(item.FilePath))
        return FirstSegmentBeforeColon(key);

      return key;
    }

    internal static string FirstSegmentBeforeColon(string value)
    {
      string s = (value ?? string.Empty).Trim();
      if (s.Length == 0)
        return string.Empty;

      for (int i = 0; i < s.Length; i++)
      {
        char c = s[i];
        if (c == ':' || c == '\u2236' || c == '\uFF1A' || c == ' ')
          return s.Substring(0, i).Trim();
      }

      return s;
    }
  }
}
