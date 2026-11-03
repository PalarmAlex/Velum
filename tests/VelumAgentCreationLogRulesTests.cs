using System;
using Velum.UI.Logs;
using Xunit;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>
  /// Регрессия: блок «СОЗДАНИЕ» сокращённого лога (события создания у-рефлексов и
  /// автоматизмов движком) формирует корректные строки, отбрасывает некорректные записи
  /// и даёт стабильную дедуп-сигнатуру, нечувствительную к резолвам.
  /// </summary>
  public class VelumAgentCreationLogRulesTests
  {
    [Fact]
    public void BuildBlock_WithMotorAndStimulus_ContainsAllLines()
    {
      var record = new VelumCreatedEntityRecord
      {
        Kind = VelumCreatedEntityKind.УсловныйРефлекс,
        Id = 12,
        MotorName = "Экспорт PDF",
        StimulusLine = "Фраза: \"п\""
      };

      string block = VelumAgentCreationLogRules.BuildBlock(record, "\n");

      Assert.NotNull(block);
      Assert.Contains(VelumAgentCreationLogRules.CreationHeader, block);
      Assert.Contains("У-рефлекс: ID 12", block);
      Assert.Contains("Стимул: Фраза: \"п\"", block);
      Assert.Contains("Мотор: Экспорт PDF", block);
    }

    [Fact]
    public void BuildBlock_AutomatizmWithoutStimulus_OmitsStimulusLine()
    {
      var record = new VelumCreatedEntityRecord
      {
        Kind = VelumCreatedEntityKind.Автоматизм,
        Id = 7,
        MotorName = "Запуск экспорта PDF"
      };

      string block = VelumAgentCreationLogRules.BuildBlock(record, "\n");

      Assert.NotNull(block);
      Assert.Contains("Автоматизм: ID 7", block);
      Assert.Contains("Мотор: Запуск экспорта PDF", block);
      Assert.DoesNotContain(VelumAgentCreationLogRules.StimulusLabel, block);
    }

    [Fact]
    public void BuildBlock_WithoutMotor_ContainsOnlyObjectLine()
    {
      var record = new VelumCreatedEntityRecord
      {
        Kind = VelumCreatedEntityKind.УсловныйРефлекс,
        Id = 3
      };

      string block = VelumAgentCreationLogRules.BuildBlock(record, "\n");

      Assert.NotNull(block);
      Assert.Contains("У-рефлекс: ID 3", block);
      Assert.DoesNotContain("Мотор:", block);
      Assert.DoesNotContain("Стимул:", block);
    }

    [Fact]
    public void BuildBlock_NullRecord_ReturnsNull()
    {
      Assert.Null(VelumAgentCreationLogRules.BuildBlock(null, "\n"));
    }

    [Fact]
    public void BuildBlock_NonPositiveId_ReturnsNull()
    {
      var record = new VelumCreatedEntityRecord
      {
        Kind = VelumCreatedEntityKind.Автоматизм,
        Id = 0
      };

      Assert.Null(VelumAgentCreationLogRules.BuildBlock(record, "\n"));
    }

    [Fact]
    public void BuildSignature_SameKindAndId_Stable()
    {
      var first = new VelumCreatedEntityRecord
      {
        Kind = VelumCreatedEntityKind.УсловныйРефлекс,
        Id = 12,
        MotorName = "Экспорт PDF"
      };
      var second = new VelumCreatedEntityRecord
      {
        Kind = VelumCreatedEntityKind.УсловныйРефлекс,
        Id = 12,
        MotorName = "Другое имя мотора"
      };

      Assert.Equal(
          VelumAgentCreationLogRules.BuildSignature(first),
          VelumAgentCreationLogRules.BuildSignature(second));
    }

    [Fact]
    public void BuildSignature_DifferentKindOrId_Differs()
    {
      var crx = new VelumCreatedEntityRecord { Kind = VelumCreatedEntityKind.УсловныйРефлекс, Id = 7 };
      var atmz = new VelumCreatedEntityRecord { Kind = VelumCreatedEntityKind.Автоматизм, Id = 7 };
      var otherCrx = new VelumCreatedEntityRecord { Kind = VelumCreatedEntityKind.УсловныйРефлекс, Id = 8 };

      Assert.NotEqual(VelumAgentCreationLogRules.BuildSignature(crx),
          VelumAgentCreationLogRules.BuildSignature(atmz));
      Assert.NotEqual(VelumAgentCreationLogRules.BuildSignature(crx),
          VelumAgentCreationLogRules.BuildSignature(otherCrx));
    }

    [Fact]
    public void BuildSignature_InvalidRecord_ReturnsEmpty()
    {
      Assert.Equal(string.Empty, VelumAgentCreationLogRules.BuildSignature(null));
      Assert.Equal(string.Empty,
          VelumAgentCreationLogRules.BuildSignature(new VelumCreatedEntityRecord { Id = -1 }));
    }

    [Fact]
    public void KindLabel_ReturnsRussianLabels()
    {
      Assert.Equal("У-рефлекс",
          VelumAgentCreationLogRules.KindLabel(VelumCreatedEntityKind.УсловныйРефлекс));
      Assert.Equal("Автоматизм",
          VelumAgentCreationLogRules.KindLabel(VelumCreatedEntityKind.Автоматизм));
    }
  }
}