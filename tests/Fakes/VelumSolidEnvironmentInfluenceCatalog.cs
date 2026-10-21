using System.Collections.Generic;
using ISIDA.Actions;

namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Заглушка каталога EA среды: реальный класс привязан к COM/Xarial и
  /// VelumIsidaHost. Для IntegrityProbes.CollectInfluencedParamIds нужен только
  /// список активных EA — тесты наполняют <see cref="ActiveActions"/> вручную.
  /// </summary>
  public static class VelumSolidEnvironmentInfluenceCatalog
  {
    internal static readonly List<InfluenceActionSystem.GomeostasisInfluenceAction> ActiveActions =
        new List<InfluenceActionSystem.GomeostasisInfluenceAction>();

    public static IReadOnlyList<InfluenceActionSystem.GomeostasisInfluenceAction> GetActiveEnvironmentActions()
    {
      return ActiveActions;
    }

    internal static void Reset()
    {
      ActiveActions.Clear();
    }
  }
}
