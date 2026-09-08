using System;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Неизменяемый снимок сессии SolidWorks для проверки предусловий рецепта.
  /// </summary>
  public sealed class SolidWorksSessionSnapshot
  {
    /// <summary>Пустой снимок (нет документа).</summary>
    public static readonly SolidWorksSessionSnapshot Empty = new SolidWorksSessionSnapshot(
        hasActiveDocument: false,
        documentKind: VelumSolidDocumentKind.None,
        documentPath: null,
        documentTitle: null,
        isSketchEditMode: false,
        isReadOnly: false,
        isDirty: false,
        pdmCheckedOut: null,
        capturedUtc: DateTime.UtcNow);

    /// <summary>
    /// Создаёт снимок сессии.
    /// </summary>
    public SolidWorksSessionSnapshot(
        bool hasActiveDocument,
        VelumSolidDocumentKind documentKind,
        string documentPath,
        string documentTitle,
        bool isSketchEditMode,
        bool isReadOnly,
        bool isDirty,
        bool? pdmCheckedOut,
        DateTime capturedUtc)
    {
      HasActiveDocument = hasActiveDocument;
      DocumentKind = documentKind;
      DocumentPath = documentPath ?? string.Empty;
      DocumentTitle = documentTitle ?? string.Empty;
      IsSketchEditMode = isSketchEditMode;
      IsReadOnly = isReadOnly;
      IsDirty = isDirty;
      PdmCheckedOut = pdmCheckedOut;
      CapturedUtc = capturedUtc;
    }

    /// <summary>Есть активный документ в сессии.</summary>
    public bool HasActiveDocument { get; }

    /// <summary>Тип активного документа.</summary>
    public VelumSolidDocumentKind DocumentKind { get; }

    /// <summary>Полный путь файла (может быть пустым для несохранённого).</summary>
    public string DocumentPath { get; }

    /// <summary>Заголовок окна документа.</summary>
    public string DocumentTitle { get; }

    /// <summary>Режим редактирования эскиза.</summary>
    public bool IsSketchEditMode { get; }

    /// <summary>Документ открыт только для чтения.</summary>
    public bool IsReadOnly { get; }

    /// <summary>Есть несохранённые изменения.</summary>
    public bool IsDirty { get; }

    /// <summary>
    /// Checkout в PDM: <c>true</c>/<c>false</c> при известном состоянии; <c>null</c> — не определено (этап 1).
    /// </summary>
    public bool? PdmCheckedOut { get; }

    /// <summary>Время снятия снимка (UTC).</summary>
    public DateTime CapturedUtc { get; }
  }
}
