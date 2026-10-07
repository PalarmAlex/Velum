using Velum.ReactiveCore;
using Xunit;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>
  /// Регрессия ложного устаревания PDF.
  /// <list type="bullet">
  /// <item>Баг 1: активация другого листа многостраничного чертежа не должна зажигать PDF
  /// (дискриминатор «смена листа vs правка текущего листа»).</item>
  /// <item>Баг 2: regen-всплеск <c>ModelGeometryStamp</c> (пакетный DXF/работа со сборкой)
  /// при актуальном DXF-штампе и без висящего DXF-pending не устаревает PDF; genuine-правка
  /// листовой детали без переэкспорта DXF (pending &gt; export) — устаревает.</item>
  /// </list>
  /// </summary>
  public class VelumPdfOutdatedRulesTests
  {
    private static VelumPdfOutdatedRules.DxfConfigStamps[] Cfg(
        int? update, int? pending)
    {
      return new[] { new VelumPdfOutdatedRules.DxfConfigStamps(update, pending) };
    }

    // --- Баг 1: дискриминатор смены листа ---

    [Fact]
    public void Pulse_SheetUnchanged_TokenChanged_WritesPending()
    {
      // Правка содержимого текущего листа: baseline есть, лист тот же, токен сдвинулся.
      Assert.True(VelumPdfOutdatedRules.ShouldWriteDrawingPendingOnPulse(
          hadSheetBaseline: true, sheetChanged: false, tokenChanged: true, sketchOpened: false));
    }

    [Fact]
    public void Pulse_SheetChanged_DoesNotWritePending()
    {
      // Активация другого листа двигает токен (виды активного листа) — это не правка.
      Assert.False(VelumPdfOutdatedRules.ShouldWriteDrawingPendingOnPulse(
          hadSheetBaseline: true, sheetChanged: true, tokenChanged: true, sketchOpened: false));
    }

    [Fact]
    public void Pulse_FirstContactWithSheet_DoesNotWritePending()
    {
      // Первый контакт с листом (baseline не зафиксирован) — только фиксируем baseline.
      Assert.False(VelumPdfOutdatedRules.ShouldWriteDrawingPendingOnPulse(
          hadSheetBaseline: false, sheetChanged: false, tokenChanged: true, sketchOpened: false));
    }

    [Fact]
    public void Pulse_SheetUnchanged_SketchOpened_WritesPending()
    {
      Assert.True(VelumPdfOutdatedRules.ShouldWriteDrawingPendingOnPulse(
          hadSheetBaseline: true, sheetChanged: false, tokenChanged: false, sketchOpened: true));
    }

    // --- Баг 2: regen vs genuine по состоянию DXF ---

    [Fact]
    public void RegenBump_ActualDxfNoPending_NotOutdated()
    {
      // Пакетный DXF/работа со сборкой: regen поднял ModelGeometryStamp (50), но DXF-штамп
      // актуален (max=10) и pending нет — база = maxDxfStamp, чертёж НЕ устарел.
      int? effective = VelumPdfOutdatedRules.ComputeEffectiveModelStamp(
          modelGeometryStamp: 50, configs: Cfg(update: 10, pending: null));
      Assert.Equal(10, effective);

      Assert.False(VelumPdfOutdatedRules.IsPdfOutdatedByModelGeometry(
          modelGeometryStamp: 50,
          configs: Cfg(update: 10, pending: null),
          partIsSldprt: true,
          pdfModelStampAtExport: 10,
          pdfGeometryUpdateStamp: null));
    }

    [Fact]
    public void GenuinePartEdit_NoDxfReexport_IsOutdated()
    {
      // Genuine-правка геометрии листовой детали без переэкспорта DXF: DXF-pending висит
      // (pending 50 > export 10) — база = ModelGeometryStamp, чертёж устарел.
      int? effective = VelumPdfOutdatedRules.ComputeEffectiveModelStamp(
          modelGeometryStamp: 50, configs: Cfg(update: 10, pending: 50));
      Assert.Equal(50, effective);

      Assert.True(VelumPdfOutdatedRules.IsPdfOutdatedByModelGeometry(
          modelGeometryStamp: 50,
          configs: Cfg(update: 10, pending: 50),
          partIsSldprt: true,
          pdfModelStampAtExport: 10,
          pdfGeometryUpdateStamp: null));
    }

    [Fact]
    public void WriteStampAtExport_ActualDfxUsesMaxDxf()
    {
      // Запись PdfModelStampAtExport при актуальном DXF = maxDxfStamp (не regen-завышенный ModelGeometryStamp).
      int? stamp = VelumPdfOutdatedRules.ComputePdfModelStampAtExport(
          modelGeometryStamp: 50, configs: Cfg(update: 10, pending: null));
      Assert.Equal(10, stamp);
    }

    [Fact]
    public void WriteStampAtExport_PendingDxfUsesModelGeometryStamp()
    {
      // При висящем DXF-pending (genuine-правка) записываем ModelGeometryStamp — иначе PDF
      // «залип» бы на отставшем DXF-штампе и не показал бы реальное устаревание.
      int? stamp = VelumPdfOutdatedRules.ComputePdfModelStampAtExport(
          modelGeometryStamp: 50, configs: Cfg(update: 10, pending: 50));
      Assert.Equal(50, stamp);
    }

    [Fact]
    public void WriteAndRead_AreSymmetric_AfterExportNotOutdated()
    {
      // E9: записали штамп при актуальном DXF (10), regen поднял ModelGeometryStamp —
      // чтение по тем же правилам даёт «не устарел».
      int? written = VelumPdfOutdatedRules.ComputePdfModelStampAtExport(
          modelGeometryStamp: 10, configs: Cfg(update: 10, pending: null));

      Assert.False(VelumPdfOutdatedRules.IsPdfOutdatedByModelGeometry(
          modelGeometryStamp: 50,
          configs: Cfg(update: 10, pending: null),
          partIsSldprt: true,
          pdfModelStampAtExport: written,
          pdfGeometryUpdateStamp: null));
    }

    // --- Сборка (без DXF-штампов): база = ModelGeometryStamp ---

    [Fact]
    public void Assembly_NoDxfStamps_UsesModelGeometryStamp()
    {
        int? effective = VelumPdfOutdatedRules.ComputeEffectiveModelStamp(
            modelGeometryStamp: 30, configs: System.Array.Empty<VelumPdfOutdatedRules.DxfConfigStamps>());
        Assert.Equal(30, effective);

        Assert.True(VelumPdfOutdatedRules.IsPdfOutdatedByModelGeometry(
            modelGeometryStamp: 30,
            configs: System.Array.Empty<VelumPdfOutdatedRules.DxfConfigStamps>(),
            partIsSldprt: false,
            pdfModelStampAtExport: 20,
            pdfGeometryUpdateStamp: null));
    }

    [Fact]
    public void Assembly_NoPdfModelStampAtExport_NotOutdated()
    {
      // Для сборки без записанного PdfModelStampAtExport fallback по PdfGeometryUpdateStamp
      // не применяется (штамп сборки и чертежа не связаны) — не устарел.
      Assert.False(VelumPdfOutdatedRules.IsPdfOutdatedByModelGeometry(
          modelGeometryStamp: 30,
          configs: System.Array.Empty<VelumPdfOutdatedRules.DxfConfigStamps>(),
          partIsSldprt: false,
          pdfModelStampAtExport: null,
          pdfGeometryUpdateStamp: 20));
    }

    // --- Старая запись детали: fallback по PdfGeometryUpdateStamp ---

    [Fact]
    public void LegacyPartRecord_NoPdfModelStampAtExport_UsesDrawingExportStamp()
    {
      Assert.True(VelumPdfOutdatedRules.IsPdfOutdatedByModelGeometry(
          modelGeometryStamp: 30,
          configs: Cfg(update: 30, pending: null),
          partIsSldprt: true,
          pdfModelStampAtExport: null,
          pdfGeometryUpdateStamp: 20));
    }
  }
}
