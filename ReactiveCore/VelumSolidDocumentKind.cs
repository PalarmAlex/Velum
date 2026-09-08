namespace Velum.ReactiveCore
{
  /// <summary>
  /// Тип активного документа SolidWorks для предусловий рецепта.
  /// </summary>
  public enum VelumSolidDocumentKind
  {
    /// <summary>Нет активного документа.</summary>
    None = 0,

    /// <summary>Деталь.</summary>
    Part,

    /// <summary>Сборка.</summary>
    Assembly,

    /// <summary>Чертёж.</summary>
    Drawing,

    /// <summary>Другой тип документа XCad.</summary>
    Other
  }
}
