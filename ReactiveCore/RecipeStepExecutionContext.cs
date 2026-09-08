using System.Collections.Generic;
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
        IReadOnlyDictionary<string, string> templateContext)
    {
      StepIndex = stepIndex;
      Recipe = recipe;
      ModelDoc = modelDoc;
      Sw = sw;
      TemplateContext = templateContext ?? new Dictionary<string, string>();
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
  }
}
