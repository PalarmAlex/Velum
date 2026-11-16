using System;
using ISIDA.Common;
using SolidWorks.Interop.sldworks;
using Velum.UI;
using Xarial.XCad.SolidWorks;

namespace Velum.ReactiveCore
{
  /// <summary>Handler'ы рецептов реестра изделий.</summary>
  internal static class RecipeExecutorHandlersRegistry
  {
    public const string ShowProblemsHandlerId = "product_registry_show_problems";

    /// <summary>product_registry_show_problems — список проблем кэша целостности.</summary>
    public static bool TryExecuteShowProblems(
        int index,
        ModelDoc2 modelDoc,
        out RecipeStepExecutionResult result)
    {
      ISwApplication swApp = Velum.SolidHomeostasis.VelumSolidEnvironmentBridge.TryGetSolidWorksApplication();
      if (swApp == null)
      {
        result = new RecipeStepExecutionResult(index, "invoke", false, false, "solidworks_session_unavailable");
        return false;
      }

      VelumProductRegistryProblemsFormHost.TryShow(swApp);
      result = new RecipeStepExecutionResult(
          index,
          "invoke",
          true,
          false,
          "product_registry_show_problems_closed");
      Logger.Info("Velum product_registry_show_problems closed");
      return true;
    }
  }
}
