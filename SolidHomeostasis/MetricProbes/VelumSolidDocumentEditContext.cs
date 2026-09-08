using SolidWorks.Interop.sldworks;
using Xarial.XCad.Documents;

namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Контекст активного документа SW для выбора допустимых проб (сборка vs деталь vs редактирование компонента).
  /// </summary>
  internal sealed class VelumSolidDocumentEditContext
  {
    /// <summary>Активный документ XCad (может быть null).</summary>
    public IXDocument ActiveDocument { get; internal set; }

    /// <summary>Активный документ — деталь.</summary>
    public bool IsPartDocument { get; internal set; }

    /// <summary>Активный документ — сборка.</summary>
    public bool IsAssemblyDocument { get; internal set; }

    /// <summary>Активный документ — чертёж.</summary>
    public bool IsDrawingDocument { get; internal set; }

    /// <summary>
    /// Деталь под редактированием в контексте сборки (AssemblyDoc.GetEditTarget),
    /// иначе null.
    /// </summary>
    public ModelDoc2 EditTargetPartModel { get; internal set; }

    /// <summary>Стабильный ключ активного документа для отслеживания штампа.</summary>
    public string DocumentKey { get; internal set; }

    /// <summary>COM ModelDoc2 активного документа (деталь, сборка или чертёж).</summary>
    public ModelDoc2 ActiveModelDoc { get; internal set; }

    /// <summary>true — допустимы пробы материала и экспортной документации (деталь или edit-target в сборке).</summary>
    public bool HasPartLevelProbeTarget =>
        IsPartDocument || EditTargetPartModel != null;
  }
}
