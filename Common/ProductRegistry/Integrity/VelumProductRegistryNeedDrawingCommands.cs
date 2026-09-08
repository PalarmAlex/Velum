namespace Velum.UI.ProductRegistry
{
  /// <summary>Проверки пути для зеркала «Нужен чертеж» (правка только из документа / пакетного PDF).</summary>
  internal static class VelumProductRegistryNeedDrawingCommands
  {
    /// <summary>true, если путь — деталь или сборка SolidWorks.</summary>
    internal static bool IsPartOrAssemblyPath(string filePath)
    {
      return VelumProductRegistryIntegrityRules.IsPartOrAssemblyPath(filePath);
    }
  }
}
