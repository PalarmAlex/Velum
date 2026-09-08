namespace Velum.SolidHomeostasis
{
  /// <summary>Строка пакетного присвоения материала (деталь + конфигурация).</summary>
  internal sealed class VelumMaterialBatchRow
  {
    internal string PartPath { get; set; }

    internal string PartDisplayName { get; set; }

    internal string ConfigName { get; set; }

    internal string MaterialName { get; set; }

    internal string MaterialDatabase { get; set; }

    internal bool Selected { get; set; }

    /// <summary>Значение пользовательского свойства «Раздел» детали.</summary>
    internal string Section { get; set; }
  }
}
