using System;

namespace Velum.UI.ProductRegistry
{
  /// <summary>Каталог реестра изделий (узел дерева).</summary>
  internal sealed class VelumProductFolder
  {
    public int Id { get; set; }

    /// <summary>Родительский каталог; 0 — корень.</summary>
    public int ParentId { get; set; }

    public string Name { get; set; }

    /// <summary>Произвольное описание каталога (может быть пустым).</summary>
    public string Description { get; set; }

    public int SortOrder { get; set; }
  }

  /// <summary>
  /// Per-configuration зеркало DXF-метаданных из CPM детали.
  /// Только чтение из реестра; правка — в документе.
  /// </summary>
  internal sealed class VelumProductExportMetaConfig
  {
    public string ConfigName { get; set; }

    /// <summary>«Нужен dxf» для этой конфигурации (с fallback на общую вкладку при чтении из SW).</summary>
    public bool? NeedDxf { get; set; }

    public string DxfFileName { get; set; }

    public string DxfProjectionView { get; set; }

    public string DxfFileFingerprint { get; set; }

    public int? DxfGeometryUpdateStamp { get; set; }

    public int? DxfGeometryPendingStamp { get; set; }
  }

  /// <summary>Запись изделия в каталоге.</summary>
  internal sealed class VelumProductItem
  {
    public int Id { get; set; }

    public int FolderId { get; set; }

    /// <summary>Обозначение.</summary>
    public string Designation { get; set; }

    /// <summary>Наименование.</summary>
    public string Name { get; set; }

    /// <summary>Абсолютный путь к связанному файлу (может быть пустым).</summary>
    public string FilePath { get; set; }

    /// <summary>
    /// Зеркало CPM «Нужен чертеж» (деталь/сборка). По умолчанию true;
    /// на форме не отображается — задаётся из меню / sync из документа.
    /// </summary>
    public bool NeedDrawing { get; set; } = true;

    // --- Зеркала CPM (только чтение в UI реестра) ---

    /// <summary>«Нужен dxf» (деталь): OR по конфигурациям. null — ещё не синхронизировано.</summary>
    public bool? NeedDxf { get; set; }

    /// <summary>«путь чертежа». null — ещё не синхронизировано.</summary>
    public string DrawingPath { get; set; }

    /// <summary>«Путь dxf» (каталог).</summary>
    public string DxfPath { get; set; }

    /// <summary>«Нужен pdf» (чертёж). null — ещё не синхронизировано.</summary>
    public bool? NeedPdf { get; set; }

    /// <summary>«Путь pdf».</summary>
    public string PdfPath { get; set; }

    /// <summary>«Штамп геометрии pdf».</summary>
    public int? PdfGeometryUpdateStamp { get; set; }

/// <summary>«Штамп изменения pdf».</summary>
    public int? PdfGeometryPendingStamp { get; set; }

    /// <summary>
    /// Штамп геометрии модели (детали/сборки) на момент последнего sync из открытого документа.
    /// Хранится в реестре как единая база: сравнивается с <see cref="PdfModelStampAtExport"/>
    /// чертежа для определения устаревания PDF по изменению геометрии модели.
    /// </summary>
    public int? ModelGeometryStamp { get; set; }

    /// <summary>
    /// Штамп геометрии модели на момент последнего экспорта PDF (запись чертежа).
    /// Если <see cref="ModelGeometryStamp"/> связанной детали больше — PDF устарел.
    /// </summary>
    public int? PdfModelStampAtExport { get; set; }

    /// <summary>Per-config DXF-метаданные (детали).</summary>
    public VelumProductExportMetaConfig[] ExportMetaConfigs { get; set; } =
        Array.Empty<VelumProductExportMetaConfig>();
  }

  /// <summary>Контейнер файла каталогов.</summary>
  internal sealed class VelumProductFolderFile
  {
    public int NextId { get; set; } = 1;

    public VelumProductFolder[] Folders { get; set; } = Array.Empty<VelumProductFolder>();
  }

  /// <summary>Контейнер файла записей.</summary>
  internal sealed class VelumProductItemFile
  {
    public int NextId { get; set; } = 1;

    public VelumProductItem[] Items { get; set; } = Array.Empty<VelumProductItem>();
  }
}
