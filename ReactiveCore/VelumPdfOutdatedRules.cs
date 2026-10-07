using System;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Чистые (без COM / реестра / SolidWorks) правила устаревания PDF по геометрии модели.
  /// Линкуется в тестовый проект <c>tests\Velum.ReactiveCore.Tests.csproj</c>.
  /// <para>
  /// Ключевая идея (случай 18 / E38): различить <em>genuine-правку</em> геометрии модели и
  /// <em>regen-всплеск</em> (открытие/перестройка сборки, переключение конфигурации) нельзя по
  /// величине разрыва <c>ModelGeometryStamp - max(DxfGeometryUpdateStamp)</c> — оба дают большой
  /// разрыв. Надёжный дискриминатор — <b>состояние DXF-геометрии</b>: genuine-правка детали
  /// всегда оставляет <c>DxfGeometryPendingStamp &gt; DxfGeometryUpdateStamp</c> (его пишет
  /// <c>NotifyPartGeometryModified</c>), а regen этот pending <b>не</b> пишет (подавлен в E38).
  /// </para>
  /// </summary>
  internal static class VelumPdfOutdatedRules
  {
    /// <summary>
    /// DXF-штампы одной конфигурации детали — примитивный вход правил (без
    /// <c>VelumProductExportMetaConfig</c>, который тянет за собой реестр).
    /// </summary>
    internal readonly struct DxfConfigStamps
    {
      /// <summary>Экспорт-штамп геометрии DXF (<c>DxfGeometryUpdateStamp</c>).</summary>
      public readonly int? UpdateStamp;

      /// <summary>Pending-штамп геометрии DXF (<c>DxfGeometryPendingStamp</c>).</summary>
      public readonly int? PendingStamp;

      /// <summary>Создаёт пару DXF-штампов конфигурации.</summary>
      /// <param name="updateStamp">Экспорт-штамп (<c>DxfGeometryUpdateStamp</c>).</param>
      /// <param name="pendingStamp">Pending-штамп (<c>DxfGeometryPendingStamp</c>).</param>
      public DxfConfigStamps(int? updateStamp, int? pendingStamp)
      {
        UpdateStamp = updateStamp;
        PendingStamp = pendingStamp;
      }
    }

    /// <summary>
    /// true — хотя бы у одной конфигурации pending больше export: геометрия DXF реально менялась
    /// (genuine-правка), DXF ещё не переэкспортирован. Regen-всплеск (E38) такого не даёт.
    /// </summary>
    /// <param name="configs">DXF-штампы конфигураций детали (может быть пустым/null).</param>
    /// <returns>true, если есть висящий DXF-pending.</returns>
    internal static bool HasDxfGeometryPending(DxfConfigStamps[] configs)
    {
      if (configs == null)
        return false;

      for (int i = 0; i < configs.Length; i++)
      {
        DxfConfigStamps cfg = configs[i];
        if (cfg.PendingStamp.HasValue && cfg.UpdateStamp.HasValue &&
            cfg.PendingStamp.Value > cfg.UpdateStamp.Value)
          return true;
      }

      return false;
    }

    /// <summary>
    /// Максимальный экспорт-штамп DXF по всем конфигурациям (0, если штампов нет).
    /// </summary>
    /// <param name="configs">DXF-штампы конфигураций детали.</param>
    /// <returns>Максимум <c>DxfGeometryUpdateStamp</c>; 0 при отсутствии штампов.</returns>
    internal static int MaxDxfUpdateStamp(DxfConfigStamps[] configs)
    {
      if (configs == null)
        return 0;

      int max = 0;
      for (int i = 0; i < configs.Length; i++)
      {
        int? s = configs[i].UpdateStamp;
        if (s.HasValue && s.Value > max)
          max = s.Value;
      }

      return max;
    }

    /// <summary>
    /// Эффективный штамп геометрии модели для сравнения с <c>PdfModelStampAtExport</c>.
    /// <list type="bullet">
    /// <item>Деталь с актуальными DXF-штампами и <b>без</b> висящего pending (regen-всплеск):
    /// базой служит <c>max(DxfGeometryUpdateStamp)</c> — regen поднял <c>ModelGeometryStamp</c>,
    /// но DXF-геометрия не менялась, устаревания нет.</item>
    /// <item>Деталь с висящим DXF-pending (genuine-правка DXF-геометрии): базой служит
    /// <c>ModelGeometryStamp</c> — чертёж устаревает сразу, не дожидаясь переэкспорта DXF.</item>
    /// <item>Нет DXF-штампов (сборка, деталь без DXF): базой служит <c>ModelGeometryStamp</c>.</item>
    /// </list>
    /// </summary>
    /// <param name="modelGeometryStamp">Штамп геометрии модели из реестра.</param>
    /// <param name="configs">DXF-штампы конфигураций детали.</param>
    /// <returns>Эффективный штамп; null, если вычислить не из чего.</returns>
    internal static int? ComputeEffectiveModelStamp(
        int? modelGeometryStamp,
        DxfConfigStamps[] configs)
    {
      int maxDxfStamp = MaxDxfUpdateStamp(configs);
      bool hasDxfStamps = maxDxfStamp > 0;

      if (hasDxfStamps && !HasDxfGeometryPending(configs))
      {
        // Актуальный DXF, regen подавлен: сравниваем по DXF-штампу, а не по ModelGeometryStamp.
        return maxDxfStamp;
      }

      // Либо genuine DXF-pending, либо DXF-штампов вовсе нет — опора на ModelGeometryStamp.
      return modelGeometryStamp;
    }

    /// <summary>
    /// Устарел ли PDF чертежа по геометрии модели.
    /// </summary>
    /// <param name="modelGeometryStamp">Штамп геометрии связанной модели из реестра.</param>
    /// <param name="configs">DXF-штампы конфигураций связанной детали.</param>
    /// <param name="partIsSldprt">true, если связанная модель — деталь (.sldprt), иначе сборка.</param>
    /// <param name="pdfModelStampAtExport">Штамп модели на момент экспорта PDF (запись чертежа).</param>
    /// <param name="pdfGeometryUpdateStamp">Экспорт-штамп чертежа (fallback для старых записей).</param>
    /// <returns>true, если PDF устарел по геометрии модели.</returns>
    internal static bool IsPdfOutdatedByModelGeometry(
        int? modelGeometryStamp,
        DxfConfigStamps[] configs,
        bool partIsSldprt,
        int? pdfModelStampAtExport,
        int? pdfGeometryUpdateStamp)
    {
      int? effective = ComputeEffectiveModelStamp(modelGeometryStamp, configs);
      if (!effective.HasValue)
        return false;

      // Вариант A: есть PdfModelStampAtExport — прямое сравнение.
      if (pdfModelStampAtExport.HasValue)
        return effective.Value > pdfModelStampAtExport.Value;

      // Вариант B: fallback для старых записей — только для деталей (.sldprt): для сборки
      // штамп модели и штамп чертежа не связаны напрямую, сравнение даёт ложные срабатывания.
      if (partIsSldprt && pdfGeometryUpdateStamp.HasValue && pdfGeometryUpdateStamp.Value > 0)
        return effective.Value > pdfGeometryUpdateStamp.Value;

      return false;
    }

    /// <summary>
    /// Вердикт пульса чертежа: писать ли PDF-pending по сдвигу токена содержимого.
    /// <para>
    /// Токен содержимого строится от видов <b>активного листа</b> чертежа, поэтому активация
    /// другого листа (особенно с другой конфигурацией) меняет токен без правки содержимого.
    /// Pending пишем только когда активный лист <b>не</b> сменился (baseline листа зафиксирован
    /// и совпадает), а содержимого-токен сдвинулся либо открылся эскиз. Смена листа или первый
    /// контакт с ним (baseline не зафиксирован) — не правка (аналог E38 для чертежа).
    /// </para>
    /// </summary>
    /// <param name="hadSheetBaseline">Есть ли зафиксированный baseline активного листа.</param>
    /// <param name="sheetChanged">Активный лист сменился относительно baseline.</param>
    /// <param name="tokenChanged">Токен содержимого сдвинулся относительно baseline.</param>
    /// <param name="sketchOpened">Активный эскиз появился (был закрыт — стал открыт).</param>
    /// <returns>true, если это genuine-правка листа и pending писать нужно.</returns>
    internal static bool ShouldWriteDrawingPendingOnPulse(
        bool hadSheetBaseline,
        bool sheetChanged,
        bool tokenChanged,
        bool sketchOpened)
    {
      if (!hadSheetBaseline || sheetChanged)
        return false;

      return tokenChanged || sketchOpened;
    }

    /// <summary>
    /// Какой штамп записать в <c>PdfModelStampAtExport</c> при экспорте PDF — симметрично
    /// <see cref="IsPdfOutdatedByModelGeometry"/> (E9: запись и чтение считают штамп одинаково).
    /// </summary>
    /// <param name="modelGeometryStamp">Штамп геометрии связанной модели из реестра.</param>
    /// <param name="configs">DXF-штампы конфигураций связанной детали.</param>
    /// <returns>Эффективный штамп для записи; null, если вычислить не из чего.</returns>
    internal static int? ComputePdfModelStampAtExport(
        int? modelGeometryStamp,
        DxfConfigStamps[] configs)
    {
      return ComputeEffectiveModelStamp(modelGeometryStamp, configs);
    }
  }
}
