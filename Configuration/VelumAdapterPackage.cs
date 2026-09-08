using System;
using System.IO;
using System.Reflection;

namespace Velum.Configuration
{
  /// <summary>
  /// Расположение каталога <c>adapter-package</c> рядом с <c>velum.dll</c>.
  /// </summary>
  internal static class VelumAdapterPackage
  {
    /// <summary>
    /// Корень пакета адаптера (<c>{app}\adapter-package</c>) или <c>null</c>, если каталог отсутствует.
    /// </summary>
    public static string TryResolvePackageRoot()
    {
      try
      {
        string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (string.IsNullOrWhiteSpace(assemblyDir))
          return null;

        string candidate = Path.Combine(assemblyDir, "adapter-package");
        return Directory.Exists(candidate) ? candidate : null;
      }
      catch
      {
        return null;
      }
    }
  }
}
