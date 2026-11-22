using Velum.ReactiveCore.Export;
using Xunit;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>
  /// Регрессия ложного «DXF устарел» при правке, не поднимающей GetUpdateStamp.
  /// <para>
  /// Случай 56: при добавлении радиуса скругления или изменении толщины листовой детали
  /// SolidWorks может не поднять GetUpdateStamp в момент ModifyNotify. Если pending == export,
  /// метрика «DXF устарел» (pending &gt; export) молчит. Форс pending выше export закрывает
  /// эту дыру (по образцу PDF-версии, Случай 18 / E38).
  /// </para>
  /// </summary>
  public class VelumDxfGeometryStampRulesTests
  {
    // --- ShouldForcePendingBeyondExport ---

    [Fact]
    public void Force_NoExportStamp_ReturnsFalse()
    {
      // Нет экспортного штампа — форсить нечего, первый запуск.
      Assert.False(VelumDxfGeometryStampRules.ShouldForcePendingBeyondExport(currentStamp: 10, exportStamp: 0));
    }

    [Fact]
    public void Force_CurrentEqualsExport_ReturnsTrue()
    {
      // GetUpdateStamp не вырос от экспорта — правка могла не поднять счётчик (add радиуса).
      Assert.True(VelumDxfGeometryStampRules.ShouldForcePendingBeyondExport(currentStamp: 15, exportStamp: 15));
    }

    [Fact]
    public void Force_CurrentLessThanExport_ReturnsTrue()
    {
      // Текущий штамп меньше экспортного (возврат GetUpdateStamp) — форсим pending.
      Assert.True(VelumDxfGeometryStampRules.ShouldForcePendingBeyondExport(currentStamp: 10, exportStamp: 15));
    }

    [Fact]
    public void Force_CurrentGreaterThanExport_ReturnsFalse()
    {
      // GetUpdateStamp реально вырос — правка зафиксирована, форс не нужен.
      Assert.False(VelumDxfGeometryStampRules.ShouldForcePendingBeyondExport(currentStamp: 16, exportStamp: 15));
    }

    [Fact]
    public void Force_ExportZero_CurrentZero_ReturnsFalse()
    {
      // Оба нулевых — exportStamp <= 0, форс не нужен.
      Assert.False(VelumDxfGeometryStampRules.ShouldForcePendingBeyondExport(currentStamp: 0, exportStamp: 0));
    }

    // --- ComputeForcedPendingStamp ---

    [Fact]
    public void Forced_ExportZero_ReturnsCurrentStamp()
    {
      // Нет экспортного штампа — вернуть текущий без изменений.
      Assert.Equal(42, VelumDxfGeometryStampRules.ComputeForcedPendingStamp(currentStamp: 42, exportStamp: 0));
    }

    [Fact]
    public void Forced_CurrentEqualsExport_ReturnsExportPlusOne()
    {
      // GetUpdateStamp не вырос — форсим pending = export + 1.
      Assert.Equal(16, VelumDxfGeometryStampRules.ComputeForcedPendingStamp(currentStamp: 15, exportStamp: 15));
    }

    [Fact]
    public void Forced_CurrentLessThanExport_ReturnsExportPlusOne()
    {
      // Текущий меньше экспортного — форсим pending = export + 1.
      Assert.Equal(16, VelumDxfGeometryStampRules.ComputeForcedPendingStamp(currentStamp: 10, exportStamp: 15));
    }

    [Fact]
    public void Forced_CurrentGreaterThanExport_ReturnsCurrentStamp()
    {
      // GetUpdateStamp вырос — вернуть текущий, форс не нужен.
      Assert.Equal(16, VelumDxfGeometryStampRules.ComputeForcedPendingStamp(currentStamp: 16, exportStamp: 15));
    }

    [Fact]
    public void Forced_ZeroStamps_ReturnsZero()
    {
      // Оба нуля — exportStamp <= 0, возвращаем currentStamp.
      Assert.Equal(0, VelumDxfGeometryStampRules.ComputeForcedPendingStamp(currentStamp: 0, exportStamp: 0));
    }

    [Fact]
    public void Forced_Edge_LargeExport_ReturnsExportPlusOne()
    {
      // Большой экспортный штамп — форс работает корректно.
      Assert.Equal(int.MaxValue, VelumDxfGeometryStampRules.ComputeForcedPendingStamp(
          currentStamp: 100, exportStamp: int.MaxValue - 1));
    }

    // --- ShouldWriteDxfPendingAfterSave (Случай 57, уточнён Случаем 60) ---

    [Fact]
    public void AfterSave_GenuineEdit_WritesPending()
    {
      // Правка толщины/радиуса: dirty до Save, не fold, не смена конфигурации, нет regen → пишем pending.
      Assert.True(VelumDxfGeometryStampRules.ShouldWriteDxfPendingAfterSave(
          wasAbsorbingExportDirty: false,
          wasDirtyBeforeSave: true,
          isFlatPatternFoldOnlyChange: false,
          isActiveConfigurationOnlyChange: false,
          regenFromUpdateCommand: false));
    }

    [Fact]
    public void AfterSave_AbsorbingExportDirty_DoesNotWritePending()
    {
      // Dirty от записи свойств DXF-экспорта перифинализируется отдельно — pending не пишем.
      Assert.False(VelumDxfGeometryStampRules.ShouldWriteDxfPendingAfterSave(
          wasAbsorbingExportDirty: true,
          wasDirtyBeforeSave: true,
          isFlatPatternFoldOnlyChange: false,
          isActiveConfigurationOnlyChange: false,
          regenFromUpdateCommand: false));
    }

    [Fact]
    public void AfterSave_RegenFromUpdateCommand_DoesNotWritePending()
    {
      // Случай 60: команда «Обновить» (regen) поднимает GetSaveFlag() без изменения геометрии.
      // Если dirty объяснён regen-ом — post-save дожим даёт ложное устаревание.
      Assert.False(VelumDxfGeometryStampRules.ShouldWriteDxfPendingAfterSave(
          wasAbsorbingExportDirty: false,
          wasDirtyBeforeSave: true,
          isFlatPatternFoldOnlyChange: false,
          isActiveConfigurationOnlyChange: false,
          regenFromUpdateCommand: true));
    }

    [Fact]
    public void AfterSave_FoldOnlyChange_DoesNotWritePending()
    {
      // Свернуть/развернуть развёртку DXF-геометрию не меняет — pending не пишем.
      Assert.False(VelumDxfGeometryStampRules.ShouldWriteDxfPendingAfterSave(
          wasAbsorbingExportDirty: false,
          wasDirtyBeforeSave: true,
          isFlatPatternFoldOnlyChange: true,
          isActiveConfigurationOnlyChange: false,
          regenFromUpdateCommand: false));
    }

    [Fact]
    public void AfterSave_ActiveConfigOnlyChange_DoesNotWritePending()
    {
      // Переключение активной конфигурации — не правка DXF-геометрии, pending не пишем.
      Assert.False(VelumDxfGeometryStampRules.ShouldWriteDxfPendingAfterSave(
          wasAbsorbingExportDirty: false,
          wasDirtyBeforeSave: true,
          isFlatPatternFoldOnlyChange: false,
          isActiveConfigurationOnlyChange: true,
          regenFromUpdateCommand: false));
    }

    [Fact]
    public void AfterSave_NotDirtyBeforeSave_DoesNotWritePending()
    {
      // Документ не менялся до Save — правки геометрии не было, pending не нужен.
      Assert.False(VelumDxfGeometryStampRules.ShouldWriteDxfPendingAfterSave(
          wasAbsorbingExportDirty: false,
          wasDirtyBeforeSave: false,
          isFlatPatternFoldOnlyChange: false,
          isActiveConfigurationOnlyChange: false,
          regenFromUpdateCommand: false));
    }

    [Fact]
    public void AfterSave_RegenAndFoldOnly_DoesNotWritePending()
    {
      // Комбинация «нет Modify» + fold-only — тем более не пишем.
      Assert.False(VelumDxfGeometryStampRules.ShouldWriteDxfPendingAfterSave(
          wasAbsorbingExportDirty: false,
          wasDirtyBeforeSave: false,
          isFlatPatternFoldOnlyChange: true,
          isActiveConfigurationOnlyChange: false,
          regenFromUpdateCommand: true));
    }

    // --- ShouldBreakDxfExportAbsorbOnGeometryChange (Случай 58) ---

    [Fact]
    public void BreakAbsorb_TokenShifted_ReturnsTrue()
    {
      // Правка эскиза после экспорта: токен геометрии сдвинулся — absorb ломаем, pending пишем.
      Assert.True(VelumDxfGeometryStampRules.ShouldBreakDxfExportAbsorbOnGeometryChange(
          tokenAtArm: 123456, currentToken: 654321));
    }

    [Fact]
    public void BreakAbsorb_TokenUnchanged_ReturnsFalse()
    {
      // Запись свойств экспорта не меняет геометрию эскизов — absorb держим.
      Assert.False(VelumDxfGeometryStampRules.ShouldBreakDxfExportAbsorbOnGeometryChange(
          tokenAtArm: 123456, currentToken: 123456));
    }

    [Fact]
    public void BreakAbsorb_BaselineNotCaptured_ReturnsFalse()
    {
      // Baseline (tokenAtArm == 0) не зафиксирован — не ломаем (первый контакт, консервативно).
      Assert.False(VelumDxfGeometryStampRules.ShouldBreakDxfExportAbsorbOnGeometryChange(
          tokenAtArm: 0, currentToken: 654321));
    }

    [Fact]
    public void BreakAbsorb_BothZero_ReturnsFalse()
    {
      // Нет baseline и нет геометрии — не ломаем.
      Assert.False(VelumDxfGeometryStampRules.ShouldBreakDxfExportAbsorbOnGeometryChange(
          tokenAtArm: 0, currentToken: 0));
    }

    // --- ShouldWriteDxfPendingOnGeometryFingerprint (Случай 62) ---

    [Fact]
    public void Fingerprint_RegenNoise_SameAsBaseline_DoesNotWritePending()
    {
      // «Обновить» в детали / regen-каскад сборки через edit-target: геометрия не изменилась,
      // отпечаток равен baseline (в тех-логах volume константа) — pending не пишем.
      Assert.False(VelumDxfGeometryStampRules.ShouldWriteDxfPendingOnGeometryFingerprint(
          baselineKnown: true, baselineFingerprint: 987654321L, currentFingerprint: 987654321L));
    }

    [Fact]
    public void Fingerprint_GenuineEdit_DiffersFromBaseline_WritesPending()
    {
      // Genuine-правка (эскиз/перемещение/радиус/толщина) меняет отпечаток относительно
      // baseline Save/экспорта — пишем pending.
      Assert.True(VelumDxfGeometryStampRules.ShouldWriteDxfPendingOnGeometryFingerprint(
          baselineKnown: true, baselineFingerprint: 987654321L, currentFingerprint: 123456789L));
    }

    [Fact]
    public void Fingerprint_BaselineUnknown_DoesNotWritePending()
    {
      // Первый контакт (baseline не зафиксирован) — только фиксируем, pending не пишем.
      Assert.False(VelumDxfGeometryStampRules.ShouldWriteDxfPendingOnGeometryFingerprint(
          baselineKnown: false, baselineFingerprint: 0L, currentFingerprint: 123456789L));
    }

    // --- ShouldRecomputeGeometryFingerprint (Случай 62, кэш COM-обхода) ---

    [Fact]
    public void Recompute_StampGrew_ReturnsTrue()
    {
      // GetUpdateStamp вырос — геометрия могла измениться, отпечаток пересчитываем.
      Assert.True(VelumDxfGeometryStampRules.ShouldRecomputeGeometryFingerprint(
          cachedStamp: 4116, currentStamp: 4117));
    }

    [Fact]
    public void Recompute_StampUnchanged_ReturnsFalse()
    {
      // Штамп не менялся — объём и эскизы не могли измениться, COM-обход не повторяем.
      Assert.False(VelumDxfGeometryStampRules.ShouldRecomputeGeometryFingerprint(
          cachedStamp: 4271, currentStamp: 4271));
    }

    [Fact]
    public void Recompute_NoCacheYet_ReturnsTrue()
    {
      // Кэша нет (cachedStamp == 0) — первый замер обязателен.
      Assert.True(VelumDxfGeometryStampRules.ShouldRecomputeGeometryFingerprint(
          cachedStamp: 0, currentStamp: 4271));
    }
  }
}
