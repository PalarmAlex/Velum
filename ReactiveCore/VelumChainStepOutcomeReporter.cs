using System;
using System.Collections.Generic;
using ISIDA.Reflexes;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Сообщает движку ISIDA оценку выполненного мотор-действия, чтобы транзитивная цепочка
  /// (<c>IsTransitive = true</c>) могла немедленно перейти к следующему звену либо остановиться.
  /// Оценка считается чистыми правилами <see cref="VelumChainStepOutcomeRules"/> и передаётся
  /// через <c>ReflexesActivator.SetChainStepResult</c>.
  /// </summary>
  /// <remarks>
  /// Вызов безопасен, только если активна цепочка в состоянии ожидания оценки
  /// (<c>IsChainWaitingForResult</c>): вне этого ISIDA кладёт значение в резервное поле,
  /// и оно может быть ошибочно прочитано позже. Поэтому репортёр явно проверяет ожидание.
  /// </remarks>
  internal static class VelumChainStepOutcomeReporter
  {
    /// <summary>
    /// Оценивает результат рецепта и, если активна цепочка, ждущая оценки, передаёт исход в ISIDA.
    /// </summary>
    /// <param name="steps">Результаты шагов исполненного рецепта.</param>
    /// <param name="recipeSucceeded">Итог исполнения рецепта (без фатальной ошибки).</param>
    /// <param name="recipeId">Идентификатор рецепта — для лога.</param>
    /// <returns>
    /// <c>true</c>, если оценка была передана в ISIDA; <c>false</c> — цепочка не ожидала оценки
    /// либо ISIDA недоступна (рецепт исполнялся не по транзитивной цепочке).
    /// </returns>
    public static bool Report(
        IReadOnlyList<RecipeStepExecutionResult> steps,
        bool recipeSucceeded,
        string recipeId)
    {
      try
      {
        if (!ReflexesActivator.IsInitialized)
          return false;

        ReflexesActivator activator = ReflexesActivator.Instance;
        if (!activator.IsChainWaitingForResult())
          return false;

        bool success = VelumChainStepOutcomeRules.Evaluate(steps, recipeSucceeded);
        activator.SetChainStepResult(success);

        ISIDA.Common.Logger.Info(
            "Velum chain step evaluation sent: recipe=" + (recipeId ?? "?") +
            " success=" + success +
            " chainId=" + activator.GetActiveChainId());
        return true;
      }
      catch (Exception ex)
      {
        ISIDA.Common.Logger.Warning("VelumChainStepOutcomeReporter: " + ex.Message);
        return false;
      }
    }
  }
}
