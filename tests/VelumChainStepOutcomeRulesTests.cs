using System.Collections.Generic;
using Velum.ReactiveCore;
using Xunit;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>
  /// Тесты чистых правил оценки шага транзитивной цепочки по результатам шагов рецепта:
  /// подтверждение диалога важнее формального «рецепт отработал», диалог без подтверждения
  /// останавливает цепочку, отсутствие диалога — оценка равна итогу рецепта.
  /// </summary>
  public class VelumChainStepOutcomeRulesTests
  {
    private static RecipeStepExecutionResult Step(bool confirmed)
        => new RecipeStepExecutionResult(0, "invoke", true, false, "dialog", confirmed);

    private static RecipeStepExecutionResult Plain()
        => new RecipeStepExecutionResult(0, "invoke", true, false, "plain");

    [Fact]
    public void ConfirmedDialog_EvaluatesTrue_EvenIfRecipeFailed()
    {
      var steps = new List<RecipeStepExecutionResult> { Step(true) };
      Assert.True(VelumChainStepOutcomeRules.Evaluate(steps, recipeSucceeded: false));
    }

    [Fact]
    public void CancelledDialog_EvaluatesFalse_EvenIfRecipeSucceeded()
    {
      var steps = new List<RecipeStepExecutionResult> { Step(false) };
      Assert.False(VelumChainStepOutcomeRules.Evaluate(steps, recipeSucceeded: true));
    }

    [Fact]
    public void AnyConfirmedAmongDialogs_EvaluatesTrue()
    {
      var steps = new List<RecipeStepExecutionResult> { Step(false), Step(true) };
      Assert.True(VelumChainStepOutcomeRules.Evaluate(steps, recipeSucceeded: false));
    }

    [Fact]
    public void NoDialog_FollowsRecipeResult_True()
    {
      var steps = new List<RecipeStepExecutionResult> { Plain() };
      Assert.True(VelumChainStepOutcomeRules.Evaluate(steps, recipeSucceeded: true));
    }

    [Fact]
    public void NoDialog_FollowsRecipeResult_False()
    {
      var steps = new List<RecipeStepExecutionResult> { Plain() };
      Assert.False(VelumChainStepOutcomeRules.Evaluate(steps, recipeSucceeded: false));
    }

    [Fact]
    public void EmptyOrNullSteps_FollowsRecipeResult()
    {
      Assert.True(VelumChainStepOutcomeRules.Evaluate(null, recipeSucceeded: true));
      Assert.False(VelumChainStepOutcomeRules.Evaluate(new List<RecipeStepExecutionResult>(), recipeSucceeded: false));
    }

    [Fact]
    public void ConfirmedProperty_IsNullForPlainStep_AndSetForDialogStep()
    {
      Assert.Null(Plain().Confirmed);
      Assert.Equal(true, Step(true).Confirmed);
      Assert.Equal(false, Step(false).Confirmed);
    }
  }
}
