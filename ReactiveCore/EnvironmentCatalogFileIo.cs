using System;
using System.Collections.Generic;
using System.IO;
using ISIDA.SymbiontEnv.Contract;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Загрузка и сохранение каталогов среды через <see cref="EnvironmentYamlCodec"/> (SymbiontEnv.Contract).
  /// </summary>
  public static class EnvironmentCatalogFileIo
  {
    /// <summary>Загружает рецепты из файла.</summary>
    public static IReadOnlyList<RecipeDefinition> LoadRecipes(string filePath, IList<string> errors)
    {
      List<EnvironmentRecipeData> data = EnvironmentYamlCodec.ReadRecipes(filePath, errors);
      var list = new List<RecipeDefinition>(data.Count);
      foreach (EnvironmentRecipeData item in data)
      {
        RecipeDefinition recipe = EnvironmentContractMapper.ToRecipeDefinition(item, filePath);
        if (recipe != null)
          list.Add(recipe);
      }

      return list;
    }

    /// <summary>Сохраняет рецепты в файл.</summary>
    public static void SaveRecipes(string filePath, IReadOnlyList<RecipeDefinition> recipes)
    {
      var data = new List<EnvironmentRecipeData>();
      if (recipes != null)
      {
        foreach (RecipeDefinition recipe in recipes)
        {
          EnvironmentRecipeData item = EnvironmentContractMapper.ToRecipeData(recipe);
          if (item != null)
            data.Add(item);
        }
      }

      EnvironmentYamlCodec.WriteRecipes(filePath, data);
    }

    /// <summary>Создаёт копии рецептов для редактирования (изменяемые поля через новые экземпляры).</summary>
    public static RecipeDefinition CloneRecipe(RecipeDefinition source)
    {
      if (source == null)
        return null;

      var steps = new List<RecipeStepDefinition>();
      if (source.Steps != null)
      {
        foreach (RecipeStepDefinition step in source.Steps)
        {
          var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
          if (step?.Parameters != null)
          {
            foreach (KeyValuePair<string, string> kv in step.Parameters)
              parameters[kv.Key] = kv.Value;
          }

          steps.Add(new RecipeStepDefinition(step?.Type ?? string.Empty, parameters));
        }
      }

      return new RecipeDefinition(
          source.RecipeId,
          source.AdaptiveActionId,
          source.DisplayName,
          source.Description,
          source.ReactiveEligible,
          steps,
          source.SourceFilePath);
    }
  }
}
