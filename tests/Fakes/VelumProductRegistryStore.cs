using System;

namespace Velum.UI.ProductRegistry
{
  /// <summary>
  /// Заглушка стора реестра: у кэша проблем востребован только
  /// <see cref="NormalizeFilePathKey"/> (ключевание MissingRegistryEntry по пути).
  /// Реализация — нормализация разделителей без разворота по корню (корня в тестах нет).
  /// </summary>
  internal static class VelumProductRegistryStore
  {
    internal static string NormalizeFilePathKey(string filePath)
    {
      string path = (filePath ?? string.Empty).Trim();
      if (path.Length == 0)
        return string.Empty;
      return path.Replace('/', '\\');
    }
  }
}