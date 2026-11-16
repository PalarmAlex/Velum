using System.Collections.Generic;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Результат исполнения рецепта Velum на UI-потоке SolidWorks.
  /// </summary>
  public sealed class RecipeExecutionResult
  {
    /// <summary>
    /// Создаёт результат исполнения.
    /// </summary>
    public RecipeExecutionResult(
        bool success,
        string errorMessage,
        string denyReason,
        SolidWorksSessionSnapshot snapshot,
        IReadOnlyList<RecipeStepExecutionResult> steps)
    {
      Success = success;
      ErrorMessage = errorMessage ?? string.Empty;
      DenyReason = denyReason ?? string.Empty;
      Snapshot = snapshot;
      Steps = steps ?? new RecipeStepExecutionResult[0];
    }

    /// <summary>Рецепт выполнен без фатальной ошибки.</summary>
    public bool Success { get; }

    /// <summary>Сообщение об ошибке (COM, UI-поток, шаг).</summary>
    public string ErrorMessage { get; }

    /// <summary>Причина отказа по предусловиям (пусто при успешной проверке).</summary>
    public string DenyReason { get; }

    /// <summary>Снимок сессии, использованный при исполнении.</summary>
    public SolidWorksSessionSnapshot Snapshot { get; }

    /// <summary>Результаты по шагам.</summary>
    public IReadOnlyList<RecipeStepExecutionResult> Steps { get; }
  }
}
