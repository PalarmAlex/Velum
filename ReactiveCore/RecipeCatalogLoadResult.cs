using System.Collections.Generic;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Результат загрузки каталога рецептов.
  /// </summary>
  public sealed class RecipeCatalogLoadResult
  {
    /// <summary>
    /// Создаёт результат загрузки.
    /// </summary>
    public RecipeCatalogLoadResult(
        IReadOnlyList<RecipeDefinition> recipes,
        IReadOnlyList<string> errors,
        IReadOnlyList<string> warnings)
    {
      Recipes = recipes ?? new RecipeDefinition[0];
      Errors = errors ?? new string[0];
      Warnings = warnings ?? new string[0];
    }

    /// <summary>Успешно загруженные рецепты.</summary>
    public IReadOnlyList<RecipeDefinition> Recipes { get; }

    /// <summary>Ошибки (файл не разобран и т. п.).</summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>Предупреждения (дубликат adaptive_action_id и т. п.).</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>Загрузка прошла без ошибок.</summary>
    public bool Success => Errors.Count == 0;
  }
}
