namespace Velum.ReactiveCore
{
  /// <summary>
  /// Результат одного шага рецепта.
  /// </summary>
  public sealed class RecipeStepExecutionResult
  {
    /// <summary>
    /// Создаёт результат шага.
    /// </summary>
    public RecipeStepExecutionResult(
        int index,
        string stepType,
        bool success,
        bool skipped,
        string message)
    {
      Index = index;
      StepType = stepType ?? string.Empty;
      Success = success;
      Skipped = skipped;
      Message = message ?? string.Empty;
    }

    /// <summary>Индекс шага (0-based).</summary>
    public int Index { get; }

    /// <summary>Тип шага из YAML.</summary>
    public string StepType { get; }

    /// <summary>Шаг завершён успешно или пропущен по политике overwrite.</summary>
    public bool Success { get; }

    /// <summary>Шаг пропущен (overwrite, пустой шаблон).</summary>
    public bool Skipped { get; }

    /// <summary>Детали для лога.</summary>
    public string Message { get; }
  }
}
