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

    /// <summary>
    /// Создаёт результат диалогового шага с явным исходом пользовательского подтверждения.
    /// </summary>
    /// <param name="index">Индекс шага (0-based).</param>
    /// <param name="stepType">Тип шага из YAML.</param>
    /// <param name="success">Шаг обработан (без фатальной ошибки).</param>
    /// <param name="skipped">Шаг пропущен (не тот тип документа и т.п.).</param>
    /// <param name="message">Детали для лога.</param>
    /// <param name="confirmed">
    /// Подтвердил ли пользователь действие в диалоге (<c>true</c> — нажал «Применить»/«ОК»,
    /// <c>false</c> — отмена/закрытие/ошибка). Для не-диалоговых шагов не используется.
    /// </param>
    public RecipeStepExecutionResult(
        int index,
        string stepType,
        bool success,
        bool skipped,
        string message,
        bool confirmed)
    {
      Index = index;
      StepType = stepType ?? string.Empty;
      Success = success;
      Skipped = skipped;
      Message = message ?? string.Empty;
      Confirmed = confirmed;
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

    /// <summary>
    /// Исход пользовательского подтверждения в диалоговом шаге: <c>true</c> — подтверждено,
    /// <c>false</c> — отменено/закрыто/ошибка, <see langword="null"/> — шаг не диалоговый.
    /// Используется для оценки шага транзитивной цепочки (<c>SetChainStepResult</c>).
    /// </summary>
    public bool? Confirmed { get; }
  }
}
