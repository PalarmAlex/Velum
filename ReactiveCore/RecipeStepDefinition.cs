using System.Collections.Generic;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Один шаг рецепта (элемент списка <c>steps</c> в YAML).
  /// </summary>
  public sealed class RecipeStepDefinition
  {
    /// <summary>
    /// Создаёт описание шага.
    /// </summary>
    /// <param name="type">Тип шага, например <c>set_custom_property</c>.</param>
    /// <param name="parameters">Параметры шага (ключи YAML без <c>type</c>).</param>
    public RecipeStepDefinition(string type, IReadOnlyDictionary<string, string> parameters)
    {
      Type = type ?? string.Empty;
      Parameters = parameters ?? new Dictionary<string, string>();
    }

    /// <summary>Тип шага.</summary>
    public string Type { get; }

    /// <summary>Параметры шага.</summary>
    public IReadOnlyDictionary<string, string> Parameters { get; }
  }
}
