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
  /// <para>
  /// Проблема absorb-окна (Случай 58): после DXF-экспорта документ «глотают»
  /// до первого Save, но absorb не различает шум от записи свойств и genuine-
  /// правку эскиза. Если токен геометрии эскизов сдвинулся — absorb ломаем,
  /// даже если Save ещё не было.
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
    /// Нужно ли дописать DXF pending-штамп ПОСЛЕ сохранения детали (Случай 57, уточнён Случаем 60).
    /// <para>
    /// «Сохранить» заставляет SolidWorks сделать genuine rebuild («обновить перед
    /// сохранением»), но он приходит внутри save-контура, где live-запись pending
    /// подавлена флагом <c>_partSaveDepth</c>. В результате правка толщины/радиуса,
    /// не подтверждённая отдельным «Обновить», теряется: метрика не загорается, пока
    /// пользователь вручную не нажмёт светофор. Дискриминатор genuine-правки —
    /// <c>GetSaveFlag()</c>, зафиксированный ДО сохранения.
    /// </para>
    /// <para>
    /// Случай 60: команда «Обновить» (regen) тоже поднимает <c>GetSaveFlag()</c> без изменения
    /// геометрии. Если dirty объяснён regen-ом извне (<paramref name="regenFromUpdateCommand"/>),
    /// post-save дожим Случая 57 не нужен — он дал бы ложное устаревание.
    /// </para>
    /// </summary>
    /// <param name="wasAbsorbingExportDirty">
    /// true, если dirty был вызван записью свойств DXF-экспорта (absorb-окно): такие штампы
    /// перифинализируются отдельно и pending здесь писать нельзя.
    /// </param>
    /// <param name="wasDirtyBeforeSave">До Save документ был изменён (<c>GetSaveFlag()</c>).</param>
    /// <param name="isFlatPatternFoldOnlyChange">Изменилась только сигнатура fold развёртки (не DXF-геометрия).</param>
    /// <param name="isActiveConfigurationOnlyChange">Сменилась только активная конфигурация (не правка геометрии).</param>
    /// <param name="regenFromUpdateCommand">
    /// true, если с момента последнего Save приходил regen «Обновить» — помечаем деталь dirty
    /// без изменения геометрии. В этом случае pending не дожимаем, даже если <paramref name="wasDirtyBeforeSave"/>
    /// == true (Случай 60).
    /// </param>
    /// <returns>true, если genuine-правка геометрии и pending после Save дописать нужно.</returns>
    internal static bool ShouldWriteDxfPendingAfterSave(
        bool wasAbsorbingExportDirty,
        bool wasDirtyBeforeSave,
        bool isFlatPatternFoldOnlyChange,
        bool isActiveConfigurationOnlyChange,
        bool regenFromUpdateCommand)
    {
      // Dirty от DXF-экспорта перифинализируется в отдельной ветке — здесь не пишем.
      if (wasAbsorbingExportDirty)
        return false;

      // Документ не менялся до Save — правки геометрии не было.
      if (!wasDirtyBeforeSave)
        return false;

      // Случай 60: regen «Обновить» поднимает GetSaveFlag() без изменения геометрии.
      // Если dirty объяснён regen-ом — post-save дожим даёт ложное устаревание.
      if (regenFromUpdateCommand)
        return false;

      // Смена fold/unfold или активной конфигурации DXF-геометрию не меняет (E38-щиты).
      if (isFlatPatternFoldOnlyChange || isActiveConfigurationOnlyChange)
        return false;

      return true;
    }

    /// <summary>
    /// Нужно ли прервать absorb-окно DXF-экспорта, если токен геометрии 2D-эскизов
    /// сдвинулся относительно снимка на момент <c>ArmAfterDxfExport</c> (Случай 58).
    /// <para>
    /// Запись свойств экспорта (каталог, имя, fingerprint, штампы) не меняет геометрию
    /// эскизов — токен остаётся неизменным. Добавление/удаление/изменение элемента эскиза
    /// меняет токен — это genuine-правка, absorb нужно сломать и написать pending
    /// даже до первого Save.
    /// </para>
    /// </summary>
    /// <param name="tokenAtArm">Токен геометрии эскизов, зафиксированный на ArmAfterDxfExport.</param>
    /// <param name="currentToken">Текущий токен геометрии эскизов.</param>
    /// <returns>
    /// true, если токен сдвинулся — absorb ломаем и пишем pending.
    /// </returns>
    internal static bool ShouldBreakDxfExportAbsorbOnGeometryChange(long tokenAtArm, long currentToken)
    {
      return tokenAtArm != 0 && tokenAtArm != currentToken;
    }

    /// <summary>
    /// Надёжный live-гейт записи DXF pending-штампа (Случай 62): писать pending только если
    /// комбинированный отпечаток геометрии (эскизы + квантованный объём) отличается от
    /// baseline, снятого на последнем Save/экспорте.
    /// <para>
    /// Тех-логи доказали: команда «Обновить» в детали и regen-каскад сборки порождают
    /// <c>ModifyNotify</c> с <c>geometryEdited == true</c>, при котором объём и эскизы
    /// НЕ изменились (отпечаток равен baseline). Прежний дискриминатор по <c>geometryEdited</c>
    /// на этом ложно зажигал «DXF устарел». Отпечаток — полный дискриминатор: ловит и
    /// перемещение эскиза (токен), и add-радиус/толщину (объём, Случай 56), и не реагирует
    /// на regen-шум.
    /// </para>
    /// </summary>
    /// <param name="baselineKnown">Базовый отпечаток зафиксирован (первый контакт — false).</param>
    /// <param name="baselineFingerprint">Отпечаток геометрии на последнем Save/экспорте.</param>
    /// <param name="currentFingerprint">Текущий отпечаток геометрии детали.</param>
    /// <returns>true, если отпечаток известен и разошёлся с baseline — genuine-правка, пишем pending.</returns>
    internal static bool ShouldWriteDxfPendingOnGeometryFingerprint(
        bool baselineKnown,
        long baselineFingerprint,
        long currentFingerprint)
    {
      return baselineKnown && currentFingerprint != baselineFingerprint;
    }

    /// <summary>
    /// Нужно ли пересчитывать отпечаток геометрии детали (Случай 62, кэш дорогого COM-обхода).
    /// <para>
    /// Рост <c>GetUpdateStamp</c> — единственное надёжное свидетельство возможного изменения
    /// геометрии: при неизменном штампе объём и эскизы не могли измениться. Поэтому отпечаток
    /// (обход дерева фич + <c>CreateMassProperty</c>) пересчитываем только когда штамп вырос
    /// относительно последнего замера — regen-серии с одним штампом не платят COM дважды.
    /// </para>
    /// </summary>
    /// <param name="cachedStamp">Штамп, на котором последний раз считали отпечаток (0 — нет кэша).</param>
    /// <param name="currentStamp">Текущий <c>GetUpdateStamp</c> детали.</param>
    /// <returns>true, если штамп изменился — отпечаток нужно пересчитать.</returns>
    internal static bool ShouldRecomputeGeometryFingerprint(int cachedStamp, int currentStamp)
    {
      return cachedStamp != currentStamp;
    }
  }
}
