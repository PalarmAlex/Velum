namespace Velum.SolidHomeostasis
{
  /// <summary>Строка списка документов для пакетного обновления свойств.</summary>
  internal sealed class VelumDocumentPropertyBatchRow
  {
    internal string FilePath { get; set; }

    internal string DisplayName { get; set; }

    /// <summary>Имя конфигурации документа (одна строка списка = одна конфигурация).</summary>
    internal string ConfigurationName { get; set; }

    /// <summary>true — сборка (.sldasm), false — деталь (.sldprt).</summary>
    internal bool IsAssembly { get; set; }

    internal bool Selected { get; set; }
  }
}
