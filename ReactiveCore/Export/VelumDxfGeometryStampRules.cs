using System;

namespace Velum.ReactiveCore.Export
{
  /// <summary>
  /// Чистые (без COM / SolidWorks) правила штампов геометрии DXF.
  /// Линкуется в тестовый проект <c>tests\Velum.ReactiveCore.Tests.csproj</c>.
  /// <para>
  /// Ключевая проблема (Случай 56): при добавлении радиуса скругления или изменении
  /// толщины листовой детали SolidWorks не всегда поднимает <c>GetUpdateStamp</c>
  /// в момент прихода <c>ModifyNotify</c>. Если <c>GetUpdateStamp</c> равен экспортному
  /// штампу — <c>TrySyncPendingGeometryStamp</c> записывает <c>pending == export</c>,
  /// а условие «DXF устарел» требует <c>pending &gt; export</c> — метрика молчит.
  /// <para>
  /// Решение: если текущий <c>GetUpdateStamp</c> не превышает экспортный штамп —
  /// форсим <c>pending = export + 1</c>. Это повторяет логику PDF-версии
  /// (<c>TrySyncPendingPdfGeometryStamp</c>), где такой форс уже был добавлен
  /// (Случай 18 / E38).
  /// </para>
  /// </para>
  /// </summary>
  internal static class VelumDxfGeometryStampRules
  {
    /// <summary>
    /// Определяет, нужно ли форсировать pending-штамп выше экспортного.
    /// <para>
    /// Если <c>currentStamp</c> (текущий <c>GetUpdateStamp</c>) не превышает
    /// <c>exportStamp</c> — правка геометрии могла не поднять счётчик ревизии
    /// (add радиуса / толщина), но геометрия реально изменилась. В этом случае
    /// pending обязан быть строго больше export, чтобы метрика сработала.
    /// </para>
    /// </summary>
    /// <param name="currentStamp">Текущий GetUpdateStamp детали.</param>
    /// <param name="exportStamp">Экспортный штамп (DxfGeometryUpdateStamp); 0 если отсутствует.</param>
    /// <returns>
    /// true, если <c>exportStamp &gt; 0</c> и <c>currentStamp &lt;= exportStamp</c> —
    /// pending нужно форсировать выше export.
    /// </returns>
    internal static bool ShouldForcePendingBeyondExport(int currentStamp, int exportStamp)
    {
      return exportStamp > 0 && currentStamp <= exportStamp;
    }

    /// <summary>
    /// Вычисляет форсированный pending-штамп: <c>exportStamp + 1</c>, если форс нужен,
    /// иначе возвращает <c>currentStamp</c> без изменений.
    /// </summary>
    /// <param name="currentStamp">Текущий GetUpdateStamp детали.</param>
    /// <param name="exportStamp">Экспортный штамп (DxfGeometryUpdateStamp); 0 если отсутствует.</param>
    /// <returns>
    /// Принудительный pending, который гарантированно &gt; exportStamp.
    /// </returns>
    internal static int ComputeForcedPendingStamp(int currentStamp, int exportStamp)
    {
      if (ShouldForcePendingBeyondExport(currentStamp, exportStamp))
        return exportStamp + 1;

      return currentStamp;
    }

    /// <summary>
    /// Нужно ли дописать DXF pending-штамп ПОСЛЕ сохранения детали (Случай 57).
    /// <para>
    /// «Сохранить» заставляет SolidWorks сделать genuine rebuild («обновить перед
    /// сохранением»), но он приходит внутри save-контура, где live-запись pending
    /// подавлена флагом <c>_partSaveDepth</c>. В результате правка толщины/радиуса,
    /// не подтверждённая отдельным «Обновить», теряется: метрика не загорается, пока
    /// пользователь вручную не нажмёт светофор. Дискриминатор genuine-правки —
    /// <c>GetSaveFlag()</c>, зафиксированный ДО сохранения.
    /// </para>
    /// </summary>
    /// <param name="wasAbsorbingExportDirty">
    /// true, если dirty был вызван записью свойств DXF-экспорта (absorb-окно): такие штампы
    /// перифинализируются отдельно и pending здесь писать нельзя.
    /// </param>
    /// <param name="wasDirtyBeforeSave">До Save документ был изменён (<c>GetSaveFlag()</c>).</param>
    /// <param name="isFlatPatternFoldOnlyChange">Изменилась только сигнатура fold развёртки (не DXF-геометрия).</param>
    /// <param name="isActiveConfigurationOnlyChange">Сменилась только активная конфигурация (не правка геометрии).</param>
    /// <returns>true, если genuine-правка геометрии и pending после Save дописать нужно.</returns>
    internal static bool ShouldWriteDxfPendingAfterSave(
        bool wasAbsorbingExportDirty,
        bool wasDirtyBeforeSave,
        bool isFlatPatternFoldOnlyChange,
        bool isActiveConfigurationOnlyChange)
    {
      // Dirty от DXF-экспорта перифинализируется в отдельной ветке — здесь не пишем.
      if (wasAbsorbingExportDirty)
        return false;

      // Документ не менялся до Save — правки геометрии не было.
      if (!wasDirtyBeforeSave)
        return false;

      // Смена fold/unfold или активной конфигурации DXF-геометрию не меняет (E38-щиты).
      if (isFlatPatternFoldOnlyChange || isActiveConfigurationOnlyChange)
        return false;

      return true;
    }
  }
}
