using System.Collections.Generic;

namespace Velum.ReactiveCore
{
  /// <summary>Описание исполнимого рецепта Velum (запись в <c>EnvironmentRecipes.yaml</c>).</summary>
  public sealed class RecipeDefinition
  {
    /// <summary>Создаёт определение рецепта.</summary>
    public RecipeDefinition(
        string recipeId,
        int adaptiveActionId,
        string displayName,
        string description,
        bool reactiveEligible,
        IReadOnlyList<RecipeStepDefinition> steps,
        string sourceFilePath)
    {
      RecipeId = recipeId ?? string.Empty;
      AdaptiveActionId = adaptiveActionId;
      DisplayName = displayName ?? string.Empty;
      Description = description ?? string.Empty;
      ReactiveEligible = reactiveEligible;
      Steps = steps ?? new RecipeStepDefinition[0];
      SourceFilePath = sourceFilePath ?? string.Empty;
    }

    /// <summary>Уникальный идентификатор рецепта.</summary>
    public string RecipeId { get; }

    /// <summary>Связанное адаптивное действие ISIDA (G_AD) — ключ моторного dispatch.</summary>
    public int AdaptiveActionId { get; }

    /// <summary>Отображаемое имя.</summary>
    public string DisplayName { get; }

    /// <summary>Описание для редактора и логов.</summary>
    public string Description { get; }

    /// <summary>Допускается реактивное автоисполнение по политике проекта.</summary>
    public bool ReactiveEligible { get; }

    /// <summary>Шаги исполнения (<c>invoke</c>).</summary>
    public IReadOnlyList<RecipeStepDefinition> Steps { get; }

    /// <summary>Путь к исходному YAML на диске.</summary>
    public string SourceFilePath { get; }
  }
}
