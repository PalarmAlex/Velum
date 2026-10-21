using System.Collections.Generic;

namespace ISIDA.Actions
{
  /// <summary>
  /// Заглушка ISIDA.Actions.InfluenceActionSystem для тестов (без isida.dll).
  /// Нужна, чтобы скомпилировать VelumProductRegistryIntegrityProbes
  /// (CollectInfluencedParamIds) и собрать синтетические EA в тестах.
  /// </summary>
  public class InfluenceActionSystem
  {
    /// <summary>Воздействие среды (EA) — минимальная копия реальной вложенной модели.</summary>
    public class GomeostasisInfluenceAction
    {
      public int Id { get; set; }

      public string Name { get; set; }

      public string Description { get; set; }

      public bool IsActive { get; set; }

      public bool IsEnvironmentProbeAction { get; set; }

      public string ProbeKey { get; set; }

      /// <summary>paramId → величина давления.</summary>
      public Dictionary<int, int> Influences { get; set; }
          = new Dictionary<int, int>();
    }
  }
}
