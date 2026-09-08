using System;
using System.IO;
using ISIDA.Common;
using Velum.Configuration;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Инициализация Reactive Core: каталоги под <see cref="VelumAppConfig"/> (%ProgramData%\VELUM\…), загрузка каталога рецептов.
  /// </summary>
  public static class VelumReactiveCoreBootstrap
  {
    /// <summary>
    /// Создаёт каталог среды (если нужно) и перезагружает <see cref="RecipeCatalog"/> из BootData.
    /// </summary>
    public static RecipeCatalogLoadResult EnsureInitialized()
    {
      try
      {
        if (!string.IsNullOrWhiteSpace(VelumAppConfig.EnvironmentFolderPath))
          Directory.CreateDirectory(VelumAppConfig.EnvironmentFolderPath);

        string recipesFile = VelumAppConfig.EnvironmentRecipesFilePath;
        if (!string.IsNullOrWhiteSpace(recipesFile))
        {
          string recipesDir = Path.GetDirectoryName(recipesFile);
          if (!string.IsNullOrEmpty(recipesDir))
            Directory.CreateDirectory(recipesDir);
        }
      }
      catch (Exception ex)
      {
        Logger.Error("Velum ReactiveCore bootstrap: " + ex.Message);
      }

      return RecipeCatalog.Reload();
    }
  }
}
