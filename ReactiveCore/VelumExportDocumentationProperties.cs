using System;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Имена пользовательских свойств SolidWorks для политики экспортной документации (PDF/DXF).
  /// Согласованы с шагами <c>kb_apply_properties_saved</c> и будущим опросом метрик.
  /// </summary>
  public static class VelumExportDocumentationProperties
  {
    /// <summary>Свойство документа: требуется сопроводительный PDF.</summary>
    public const string NeedPdf = "Нужен pdf";

    /// <summary>Свойство документа: требуется сопроводительный DXF.</summary>
    public const string NeedDxf = "Нужен dxf";

    /// <summary>Свойство детали/сборки: нужен связанный чертёж.</summary>
    public const string NeedDrawing = "Нужен чертеж";

    /// <summary>Путь к сопроводительному PDF (заполняется после экспорта).</summary>
    public const string PdfPath = "Путь pdf";

    /// <summary>Полный путь к нативному чертежу .slddrw, связанному с деталью или сборкой.</summary>
    public const string DrawingPath = "путь чертежа";

    /// <summary>Каталог хранения сопроводительных DXF (не полный путь к файлу).</summary>
    public const string DxfPath = "Путь dxf";

    /// <summary>Фактический basename DXF без расширения (per-configuration).</summary>
    public const string DxfFileName = "Имя файла dxf";

    /// <summary>Вид проекции DXF: Front / Top / Right / Back / Bottom / Left (per-configuration).</summary>
    public const string DxfProjectionView = "Вид проекции dxf";

    /// <summary>
    /// Отпечаток DXF-файла на момент последней фиксации (per-configuration):
    /// <c>{LastWriteTimeUtc.Ticks}|{Length}</c> — защита от «пустого» обновления штампа.
    /// </summary>
    public const string DxfFileFingerprint = "Отпечаток файла dxf";

    /// <summary>
    /// Штамп ревизии чертежа на момент последнего экспорта PDF (ModelDoc2.GetUpdateStamp).
    /// </summary>
    public const string PdfGeometryUpdateStamp = "Штамп геометрии pdf";

    /// <summary>
    /// Штамп GetUpdateStamp на момент последнего изменения чертежа (document scope).
    /// Сравнивается с <see cref="PdfGeometryUpdateStamp"/>; переживает сохранение и повторное открытие SLDDRW.
    /// PDF устарел только если это значение больше штампа геометрии. Рост GetUpdateStamp
    /// от Save детали / rebuild видов сам по себе pending не пишет.
    /// </summary>
    public const string PdfGeometryPendingStamp = "Штамп изменения pdf";

    /// <summary>
    /// Штамп ревизии детали на момент последнего экспорта DXF (per-configuration, GetUpdateStamp).
    /// </summary>
    public const string DxfGeometryUpdateStamp = "Штамп геометрии dxf";

    /// <summary>
    /// Штамп GetUpdateStamp на момент последнего изменения геометрии в этой конфигурации (per-configuration).
    /// Сравнивается с <see cref="DxfGeometryUpdateStamp"/>; переживает сохранение и повторное открытие SLDPRT.
    /// </summary>
    public const string DxfGeometryPendingStamp = "Штамп изменения геометрии dxf";

    /// <summary>Значение логического свойства SW: Yes (требуется экспорт).</summary>
    public const string FlagYes = "Yes";

    /// <summary>Значение логического свойства SW: No (экспорт не требуется).</summary>
    public const string FlagNo = "No";

    /// <summary>true, если свойство DXF допускается только на детали (не на сборке).</summary>
    public static bool IsDxfPartOnlyProperty(string propertyName)
    {
      string name = (propertyName ?? string.Empty).Trim();
      if (name.Length == 0)
        return false;

      return string.Equals(name, NeedDxf, StringComparison.Ordinal)
          || string.Equals(name, DxfPath, StringComparison.Ordinal);
    }

    /// <summary>true, если изменение свойства влияет на пробы PDF/DXF.</summary>
    public static bool IsExportDocumentationProbeProperty(string propertyName)
    {
      string name = (propertyName ?? string.Empty).Trim();
      if (name.Length == 0)
        return false;

      return string.Equals(name, NeedPdf, StringComparison.Ordinal)
          || string.Equals(name, NeedDxf, StringComparison.Ordinal)
          || string.Equals(name, NeedDrawing, StringComparison.Ordinal)
          || string.Equals(name, PdfPath, StringComparison.Ordinal)
          || string.Equals(name, DrawingPath, StringComparison.Ordinal)
          || string.Equals(name, DxfPath, StringComparison.Ordinal)
          || string.Equals(name, PdfGeometryUpdateStamp, StringComparison.Ordinal)
          || string.Equals(name, PdfGeometryPendingStamp, StringComparison.Ordinal)
          || string.Equals(name, DxfGeometryUpdateStamp, StringComparison.Ordinal)
          || string.Equals(name, DxfGeometryPendingStamp, StringComparison.Ordinal)
          || string.Equals(name, DxfFileFingerprint, StringComparison.Ordinal);
    }
  }
}
