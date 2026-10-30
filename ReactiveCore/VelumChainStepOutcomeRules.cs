using System.Collections.Generic;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Чистые правила итоговой оценки мотор-действия цепочки рефлексов по результатам
  /// шагов рецепта. Не зависит от WinForms / COM / isida.dll — тестируется в изоляции.
  /// Оценка передаётся в ISIDA через <c>ReflexesActivator.SetChainStepResult</c>:
  /// <c>true</c> — звено выполнено успешно (цепочка идёт дальше), <c>false</c> — стоп.
  /// </summary>
  internal static class VelumChainStepOutcomeRules
  {
    /// <summary>
    /// Определяет оценку шага цепочки по списку результатов шагов рецепта.
    /// </summary>
    /// <param name="steps">Результаты шагов исполненного рецепта.</param>
    /// <param name="recipeSucceeded">Итог исполнения рецепта (без фатальной ошибки).</param>
    /// <returns>
    /// <c>true</c> — пользователь подтвердил действие в диалоге либо (для не-диалогового
    /// рецепта) рецепт выполнен успешно; <c>false</c> — диалог отменён/закрыт/не подтверждён
    /// либо рецепт провален.
    /// </returns>
    /// <remarks>
    /// Диалоговый шаг (экспортные формы и т.п.) проставляет <see cref="RecipeStepExecutionResult.Confirmed"/>:
    /// <c>true</c> — нажато подтверждение («Применить»/«ОК»), <c>false</c> — отмена/закрытие/ошибка.
    /// Если в рецепте есть хотя бы один подтверждённый диалог — звено считается успешным.
    /// Если диалог был, но не подтверждён — неуспешным, независимо от <paramref name="recipeSucceeded"/>
    /// (шаг-то формально «обработан», но оператор действие не выполнил).
    /// Диалоговых шагов нет — оценка равна общему итогу рецепта.
    /// </remarks>
    public static bool Evaluate(IReadOnlyList<RecipeStepExecutionResult> steps, bool recipeSucceeded)
    {
      bool hasDialog = false;
      bool anyConfirmed = false;

      if (steps != null)
      {
        for (int i = 0; i < steps.Count; i++)
        {
          RecipeStepExecutionResult step = steps[i];
          if (step == null || !step.Confirmed.HasValue)
            continue;

          hasDialog = true;
          if (step.Confirmed.Value)
            anyConfirmed = true;
        }
      }

      if (hasDialog)
        return anyConfirmed;

      return recipeSucceeded;
    }
  }
}
