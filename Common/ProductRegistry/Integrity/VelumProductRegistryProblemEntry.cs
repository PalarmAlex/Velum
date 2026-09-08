namespace Velum.UI.ProductRegistry
{
  /// <summary>Запись в кэше проблем реестра.</summary>
  internal sealed class VelumProductRegistryProblemEntry
  {
    public int ItemId { get; set; }

    public VelumProductRegistryProblemKind Kind { get; set; }

    public string Designation { get; set; }

    public string Name { get; set; }

    public string FilePath { get; set; }

    public string Detail { get; set; }
  }
}
