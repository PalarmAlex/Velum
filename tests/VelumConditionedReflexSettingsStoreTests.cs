using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Velum.UI;
using Xunit;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>
  /// Тесты хранилища настроек у-рефлексов: толерантность к «старому» формату с
  /// десятичной запятой, миграция неполного файла, roundtrip сохранения и валидация
  /// диапазонов модели ISIDA.
  /// </summary>
  public class VelumConditionedReflexSettingsStoreTests : IDisposable
  {
    private readonly string _dir;
    private readonly string _file;

    public VelumConditionedReflexSettingsStoreTests()
    {
      _dir = Path.Combine(Path.GetTempPath(), "velum_crx_" + Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(_dir);
      _file = Path.Combine(_dir, VelumConditionedReflexSettingsStore.FileName);
    }

    public void Dispose()
    {
      try { Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    private void Write(string content) => File.WriteAllText(_file, content, new UTF8Encoding(false));

    [Fact]
    public void Load_OldCommaFormat_ParsedAndMarkedForRewrite()
    {
      // Старый файл Velum: дроби через запятую, нет части параметров новой модели.
      Write(
          "# Настройки системы условных рефлексов\n" +
          "LearningRate=0,2\n" +
          "DecayRate=0,98\n" +
          "ActivationThreshold=0,6\n" +
          "MinAssociationStrength=0,1\n" +
          "TimeWindowPulses=5\n" +
          "InitialLifetimePulses=86400\n" +
          "ActiveExtinctionRate=0,05\n" +
          "PassiveDecayPeriodPulses=1000\n" +
          "HigherOrderStrengthReductionCoefficient=1,5\n" +
          "CompetitionStrengthRatioThreshold=0,8\n" +
          "TieBreakPreferSmallerReflexId=True\n" +
          "EnableCompetitiveLearning=True\n" +
          "CompetitionSuppressionCoefficient=1\n");

      var res = VelumConditionedReflexSettingsStore.Load(_file);

      Assert.False(res.FileMissing);
      Assert.True(res.NeedsRewrite); // запятые + неполный набор
      Assert.Equal(0.2f, res.Model.LearningRate);
      Assert.Equal(0.98f, res.Model.DecayRate);
      Assert.Equal(0.6f, res.Model.ActivationThreshold);
      Assert.Equal(0.05f, res.Model.ActiveExtinctionRate);
      Assert.Equal(1.5f, res.Model.HigherOrderStrengthReductionCoefficient);
      Assert.True(res.Model.TieBreakPreferSmallerReflexId);
      Assert.True(res.Model.EnableCompetitiveLearning);
      // Отсутствующие ключи новой модели — по умолчанию.
      Assert.Equal(0.95f, res.Model.AuthoritativeStrength);
      Assert.Equal(88473600, res.Model.MaxLifetimePulsesCap);
      Assert.Equal(0.998f, res.Model.SensoryHighStrengthDecayRate);
    }

    [Fact]
    public void Load_MissingFile_FlagsDefaultsWithRewrite()
    {
      var res = VelumConditionedReflexSettingsStore.Load(Path.Combine(_dir, "absent.dat"));
      Assert.True(res.FileMissing);
      Assert.True(res.NeedsRewrite);
      Assert.Equal(0.2f, res.Model.LearningRate);
      Assert.Equal(0.6f, res.Model.ActivationThreshold);
    }

    [Fact]
    public void SaveThenLoad_Roundtrip_KeepsValuesAndCleanFormat()
    {
      var m = new VelumConditionedReflexSettingsModel
      {
        LearningRate = 0.23f,
        DecayRate = 0.97f,
        ActivationThreshold = 0.55f,
        MaxLifetimePulsesCap = 1234567,
        EnableCompetitiveLearning = false,
        TieBreakPreferSmallerReflexId = false,
        SensoryHighStrengthDecayRate = 0.999f
      };

      VelumConditionedReflexSettingsStore.Save(_file, m);
      string text = File.ReadAllText(_file);

      // Канонический формат — точка, без запятых в значениях.
      Assert.Contains("LearningRate=0.23", text);
      Assert.DoesNotContain("LearningRate=0,23", text);

      var res = VelumConditionedReflexSettingsStore.Load(_file);
      Assert.False(res.NeedsRewrite); // файл полный и в правильном формате
      Assert.Equal(0.23f, res.Model.LearningRate);
      Assert.Equal(0.97f, res.Model.DecayRate);
      Assert.Equal(0.55f, res.Model.ActivationThreshold);
      Assert.Equal(1234567, res.Model.MaxLifetimePulsesCap);
      Assert.False(res.Model.EnableCompetitiveLearning);
      Assert.False(res.Model.TieBreakPreferSmallerReflexId);
      Assert.Equal(0.999f, res.Model.SensoryHighStrengthDecayRate);
    }

    [Fact]
    public void Save_CreatesMissingDirectory()
    {
      string nested = Path.Combine(_dir, "sub", "deep", VelumConditionedReflexSettingsStore.FileName);
      VelumConditionedReflexSettingsStore.Save(nested, new VelumConditionedReflexSettingsModel());
      Assert.True(File.Exists(nested));
    }

    [Fact]
    public void Validate_Defaults_HasNoErrors()
    {
      var errors = VelumConditionedReflexSettingsStore.Validate(new VelumConditionedReflexSettingsModel());
      Assert.Empty(errors);
    }

    [Theory]
    [InlineData("LearningRate", 0.05f)]   // ниже 0.1
    [InlineData("LearningRate", 0.5f)]    // выше 0.3
    [InlineData("ActivationThreshold", 0.4f)]
    [InlineData("DecayRate", 0.9f)]
    public void Validate_OutOfRangeFloat_ReportsError(string prop, float value)
    {
      var m = new VelumConditionedReflexSettingsModel();
      typeof(VelumConditionedReflexSettingsModel).GetProperty(prop).SetValue(m, value);
      var errors = VelumConditionedReflexSettingsStore.Validate(m);
      Assert.NotEmpty(errors);
    }

    [Fact]
    public void Validate_MaxLifetimeBelowInitial_ReportsError()
    {
      var m = new VelumConditionedReflexSettingsModel
      {
        InitialLifetimePulses = 100000,
        MaxLifetimePulsesCap = 50000
      };
      var errors = VelumConditionedReflexSettingsStore.Validate(m);
      Assert.Contains(errors, e => e.Contains("Потолок TTL"));
    }

    [Fact]
    public void Validate_MidZoneNotBelowHighZone_ReportsError()
    {
      var m = new VelumConditionedReflexSettingsModel
      {
        SensoryMidStrengthThreshold = 0.9f,
        SensoryHighStrengthThreshold = 0.8f
      };
      var errors = VelumConditionedReflexSettingsStore.Validate(m);
      Assert.Contains(errors, e => e.Contains("Средняя зона"));
    }

    [Fact]
    public void Load_MigrationOfRealOldFile_ProducesValidModel()
    {
      // Полный старый набор + запятые — после чтения валидируется без ошибок.
      Write(
          "LearningRate=0,2\n" +
          "DecayRate=0,98\n" +
          "ActivationThreshold=0,6\n" +
          "MinAssociationStrength=0,1\n" +
          "TimeWindowPulses=5\n" +
          "InitialLifetimePulses=86400\n" +
          "ActiveExtinctionRate=0,05\n" +
          "PassiveDecayPeriodPulses=1000\n" +
          "HigherOrderStrengthReductionCoefficient=1,5\n" +
          "CompetitionStrengthRatioThreshold=0,8\n" +
          "TieBreakPreferSmallerReflexId=True\n" +
          "EnableCompetitiveLearning=True\n" +
          "CompetitionSuppressionCoefficient=1\n");

      var res = VelumConditionedReflexSettingsStore.Load(_file);
      var errors = VelumConditionedReflexSettingsStore.Validate(res.Model);
      Assert.Empty(errors);

      // Миграция: сохранение чинит формат, повторное чтение — уже без признаков rewrite.
      VelumConditionedReflexSettingsStore.Save(_file, res.Model);
      var reloaded = VelumConditionedReflexSettingsStore.Load(_file);
      Assert.False(reloaded.NeedsRewrite);
      Assert.Empty(VelumConditionedReflexSettingsStore.Validate(reloaded.Model));
    }

    [Fact]
    public void Load_IgnoresUnknownAndLegacyKeys()
    {
      Write(
          "LearningRate=0.2\n" +
          "PassiveDecayHalfLifePulses=999\n" +   // устаревший — игнор
          "SomeFutureKey=42\n");                  // неизвестный — не ломает чтение
      var res = VelumConditionedReflexSettingsStore.Load(_file);
      Assert.Equal(0.2f, res.Model.LearningRate);
      Assert.True(res.NeedsRewrite); // неполный файл
    }

    // Регрессия бага «перестали создаваться условные рефлексы» (случай 33): в файле у
    // дробных параметров пропала десятичная точка (0.2→2, 0.98→98, 0.6→6 …). Такие
    // значения парсятся как корректные числа, но движок ISIDA трактует их как доли, и
    // ни один УР не проходит порог активации. Хранилище обязано отловить это валидацией.
    private static string CorruptedDroppedDecimalPointFile() =>
        "LearningRate=2\n" +
        "DecayRate=98\n" +
        "ActivationThreshold=6\n" +
        "MinAssociationStrength=1\n" +
        "TimeWindowPulses=5\n" +
        "InitialLifetimePulses=86400\n" +
        "ActiveExtinctionRate=5\n" +
        "PassiveDecayPeriodPulses=1000\n" +
        "HigherOrderStrengthReductionCoefficient=15\n" +
        "CompetitionStrengthRatioThreshold=8\n" +
        "TieBreakPreferSmallerReflexId=True\n" +
        "EnableCompetitiveLearning=True\n" +
        "CompetitionSuppressionCoefficient=1\n" +
        "InitialStrengthBonus=0.1\n" +
        "AuthoritativeStrength=0.95\n" +
        "EstablishedStrengthThreshold=0.8\n" +
        "ActivationReinforcementFraction=0.25\n" +
        "MaxLifetimePulsesCap=88473600\n" +
        "PassiveDecayFallbackPeriodPulses=1000\n" +
        "SensoryDecayPeriodPulses=100\n" +
        "SensoryStrengthFloor=0.1\n" +
        "SensoryHighStrengthThreshold=0.8\n" +
        "SensoryHighStrengthDecayRate=0.998\n" +
        "SensoryMidStrengthThreshold=0.4\n";

    [Fact]
    public void Load_DroppedDecimalPoint_ParsesButFailsValidation()
    {
      Write(CorruptedDroppedDecimalPointFile());

      var res = VelumConditionedReflexSettingsStore.Load(_file);

      // Парсятся как числа (стор не «чинит» молча), но значения завышены в разы…
      Assert.Equal(2f, res.Model.LearningRate);
      Assert.Equal(6f, res.Model.ActivationThreshold);
      Assert.Equal(98f, res.Model.DecayRate);

      // …и валидация обязана поднять ошибку по каждому пострадавшему параметру —
      // именно это не даёт форме сохранить такие настройки и указывает на причину.
      var errors = VelumConditionedReflexSettingsStore.Validate(res.Model);
      Assert.NotEmpty(errors);
      Assert.Contains(errors, e => e.Contains("Порог активации"));
      Assert.Contains(errors, e => e.Contains("Коэффициент обучения"));
      Assert.Contains(errors, e => e.Contains("Коэфф. затухания"));
      Assert.Contains(errors, e => e.Contains("Минимальная крепость"));
      Assert.Contains(errors, e => e.Contains("Активное угасание"));
      Assert.Contains(errors, e => e.Contains("крепости вторичных"));
      Assert.Contains(errors, e => e.Contains("отношения крепостей"));
    }

    [Fact]
    public void Validate_DroppedDecimalPointRestoredToDefaults_HasNoErrors()
    {
      // Те же 7 параметров, но с возвращённой точкой (как в живом фиксе) — валидация чистая.
      var m = new VelumConditionedReflexSettingsModel
      {
        LearningRate = 0.2f,
        DecayRate = 0.98f,
        ActivationThreshold = 0.6f,
        MinAssociationStrength = 0.1f,
        ActiveExtinctionRate = 0.05f,
        HigherOrderStrengthReductionCoefficient = 1.5f,
        CompetitionStrengthRatioThreshold = 0.8f
      };
      Assert.Empty(VelumConditionedReflexSettingsStore.Validate(m));
    }
  }
}