using System.Collections.Generic;
using ISIDA.Actions;
using SolidWorks.Interop.sldworks;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Контекст исполнения одного шага рецепта на UI-потоке SolidWorks.
  /// </summary>
  public sealed class RecipeStepExecutionContext
  {
    /// <summary>
    /// Создаёт контекст шага.
    /// </summary>
    public RecipeStepExecutionContext(
        int stepIndex,
        RecipeDefinition recipe,
        ModelDoc2 modelDoc,
        SldWorks sw,
        IReadOnlyDictionary<string, string> templateContext,
        AdaptiveActionsSystem.ActionActivationSource activationSource)
    {
      StepIndex = stepIndex;
      Recipe = recipe;
      ModelDoc = modelDoc;
      Sw = sw;
      TemplateContext = templateContext ?? new Dictionary<string, string>();
      ActivationSource = activationSource;
    }

    /// <summary>Индекс шага в рецепте.</summary>
    public int StepIndex { get; }

    /// <summary>Исполняемый рецепт.</summary>
    public RecipeDefinition Recipe { get; }

    /// <summary>Активный документ SolidWorks.</summary>
    public ModelDoc2 ModelDoc { get; }

    /// <summary>COM-объект SolidWorks.</summary>
    public SldWorks Sw { get; }

    /// <summary>Контекст подстановки шаблонов.</summary>
    public IReadOnlyDictionary<string, string> TemplateContext { get; }

    /// <summary>
    /// Источник активации действия, из-за которого исполняется рецепт
    /// (безусловный рефлекс, условный рефлекс, автоматизм).
    /// </summary>
    public AdaptiveActionsSystem.ActionActivationSource ActivationSource { get; }

    /// <summary>
    /// Рецепт запущен условным рефлексом: при несовпадении типа документа интерактивные
    /// шаги должны тихо пропускаться без пользовательского MessageBox (оператор не просил действие).
    /// </summary>
    public bool FromConditionedReflex =>
        ActivationSource == AdaptiveActionsSystem.ActionActivationSource.ConditionedReflex;
  }
}

