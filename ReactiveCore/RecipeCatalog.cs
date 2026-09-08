using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ISIDA.Common;
using Velum.Configuration;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Каталог рецептов среды: загрузка YAML из <see cref="VelumAppConfig.EnvironmentRecipesFilePath"/>.
  /// </summary>
  public static class RecipeCatalog
  {
    private static readonly object Sync = new object();
    private static Dictionary<string, RecipeDefinition> _byRecipeId =
        new Dictionary<string, RecipeDefinition>(StringComparer.OrdinalIgnoreCase);
    private static Dictionary<int, RecipeDefinition> _byAdaptiveActionId =
        new Dictionary<int, RecipeDefinition>();
    private static RecipeCatalogLoadResult _lastLoad = new RecipeCatalogLoadResult(
        Array.Empty<RecipeDefinition>(),
        Array.Empty<string>(),
        Array.Empty<string>());

    // Метаданные файла на момент последней загрузки: пропускаем перечитывание,
    // пока файл не изменился (вызовы Reload могут приходить на каждом пульсе).
    private static bool _hasFileStamp;
    private static DateTime _lastFileStampUtc;
    private static long _lastFileLength;

    /// <summary>Последний результат загрузки.</summary>
    public static RecipeCatalogLoadResult LastLoadResult
    {
      get
      {
        lock (Sync)
          return _lastLoad;
      }
    }

    /// <summary>Перезагружает каталог из файла рецептов среды.</summary>
    public static RecipeCatalogLoadResult Reload()
    {
      lock (Sync)
      {
        var recipes = new List<RecipeDefinition>();
        var errors = new List<string>();
        var warnings = new List<string>();

        string filePath = VelumAppConfig.EnvironmentRecipesFilePath;
        if (string.IsNullOrWhiteSpace(filePath))
        {
          errors.Add("EnvironmentRecipesFilePath не задан.");
          _lastLoad = new RecipeCatalogLoadResult(recipes, errors, warnings);
          ApplyIndexes(recipes);
          return _lastLoad;
        }

        try
        {
          string dir = Path.GetDirectoryName(filePath);
          if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        }
        catch (Exception ex)
        {
          errors.Add("Не удалось создать каталог рецептов: " + ex.Message);
        }

        if (File.Exists(filePath))
        {
          // Файл не менялся с прошлой загрузки — пропускаем перечитывание и лог.
          try
          {
            FileInfo info = new FileInfo(filePath);
            if (_hasFileStamp &&
                info.LastWriteTimeUtc == _lastFileStampUtc &&
                info.Length == _lastFileLength)
            {
              return _lastLoad;
            }
          }
          catch
          {
          }

          IReadOnlyList<RecipeDefinition> loaded = EnvironmentCatalogFileIo.LoadRecipes(filePath, errors);
          foreach (RecipeDefinition recipe in loaded)
          {
            if (recipes.Exists(r => string.Equals(r.RecipeId, recipe.RecipeId, StringComparison.OrdinalIgnoreCase)))
            {
              errors.Add("Дубликат id '" + recipe.RecipeId + "' в " + filePath);
              continue;
            }

            recipes.Add(recipe);
          }

          try
          {
            FileInfo info = new FileInfo(filePath);
            _lastFileStampUtc = info.LastWriteTimeUtc;
            _lastFileLength = info.Length;
            _hasFileStamp = true;
          }
          catch
          {
            _hasFileStamp = false;
          }
        }
        else
        {
          warnings.Add("Файл рецептов не найден: " + filePath);
          _hasFileStamp = false;
        }

        _lastLoad = new RecipeCatalogLoadResult(recipes, errors, warnings);
        ApplyIndexes(recipes, warnings);
        if (recipes.Count > 0)
          Logger.Info("Velum RecipeCatalog: загружено рецептов " + recipes.Count);

        if (errors.Count > 0)
          Logger.Warning("Velum RecipeCatalog: ошибок загрузки " + errors.Count);

        return _lastLoad;
      }
    }

    /// <summary>Найти рецепт по <c>id</c>.</summary>
    public static RecipeDefinition FindByRecipeId(string recipeId)
    {
      if (string.IsNullOrWhiteSpace(recipeId))
        return null;

      lock (Sync)
      {
        RecipeDefinition recipe;
        return _byRecipeId.TryGetValue(recipeId.Trim(), out recipe) ? recipe : null;
      }
    }

    /// <summary>Найти рецепт по ID адаптивного действия ISIDA (G_AD).</summary>
    public static RecipeDefinition FindByAdaptiveActionId(int adaptiveActionId)
    {
      if (adaptiveActionId <= 0)
        return null;

      lock (Sync)
      {
        RecipeDefinition recipe;
        return _byAdaptiveActionId.TryGetValue(adaptiveActionId, out recipe) ? recipe : null;
      }
    }

    /// <summary>
    /// Проверяет, существует ли рецепт для заданного adaptive_action_id.
    /// Используется PurposeGeneticSystem для селекции клонирования.
    /// </summary>
    /// <param name="adaptiveActionId">ID адаптивного действия.</param>
    /// <returns>true, если рецепт найден.</returns>
    public static bool ExistsByAdaptiveActionId(int adaptiveActionId)
    {
      if (adaptiveActionId <= 0)
        return false;

      lock (Sync)
        return _byAdaptiveActionId.ContainsKey(adaptiveActionId);
    }

    private static void ApplyIndexes(List<RecipeDefinition> recipes, List<string> warnings = null)
    {
      var byId = new Dictionary<string, RecipeDefinition>(StringComparer.OrdinalIgnoreCase);
      var byAction = new Dictionary<int, RecipeDefinition>();

      foreach (RecipeDefinition recipe in recipes)
      {
        byId[recipe.RecipeId] = recipe;

        if (recipe.AdaptiveActionId > 0)
        {
          if (byAction.ContainsKey(recipe.AdaptiveActionId))
          {
            warnings?.Add(
                "Дубликат adaptive_action_id " + recipe.AdaptiveActionId +
                " (" + recipe.RecipeId + " игнорируется для индекса)");
          }
          else
          {
            byAction[recipe.AdaptiveActionId] = recipe;
          }
        }
      }

      _byRecipeId = byId;
      _byAdaptiveActionId = byAction;
    }
  }
}
