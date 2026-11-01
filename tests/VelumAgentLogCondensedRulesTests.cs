using System;
using System.Collections.Generic;
using Velum.UI.Logs;
using Xunit;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>
  /// Регрессия: сокращённый лог «стимул → ответ» формирует корректные строки,
  /// корректно подавляет дубликаты и корректно типизирует типы стимула/ответа.
  /// </summary>
  public class VelumAgentLogCondensedRulesTests
  {
    private static VelumCondensedStimulusLine Line(
        VelumCondensedStimulusKind kind, int id, string text)
    {
      return new VelumCondensedStimulusLine { Kind = kind, Id = id, Text = text };
    }

    [Fact]
    public void BuildBlock_WithAllKinds_ReturnsStimulusAndResponseHeaders()
    {
      var record = new VelumCondensedLogRecord
      {
        ResponseKind = VelumCondensedResponseKind.БезусловныйРефлекс,
        ResponseId = 5,
        ResponseActionName = "Экспорт PDF"
      };
      record.Stimuli.Add(Line(VelumCondensedStimulusKind.Воздействие, 65, "Запуск экспорта PDF"));
      record.Stimuli.Add(Line(VelumCondensedStimulusKind.Фраза, 0, "п"));

      string block = VelumAgentLogCondensedRules.BuildBlock(record, "\n");
      Assert.NotNull(block);
      Assert.Contains(VelumAgentLogCondensedRules.StimulusHeader, block);
      Assert.Contains(VelumAgentLogCondensedRules.ResponseHeader, block);
      Assert.Contains("Воздействие: ID 65, Запуск экспорта PDF", block);
      Assert.Contains("Фраза: \"п\"", block);
      Assert.Contains("Б/У рефлекс: ID 5, Экспорт PDF", block);
    }

    [Fact]
    public void BuildBlock_WithCommand_QuotesTextWithoutId()
    {
      var record = new VelumCondensedLogRecord
      {
        ResponseKind = VelumCondensedResponseKind.УсловныйРефлекс,
        ResponseId = 12,
        ResponseActionName = "Подготовка чертежа"
      };
      record.Stimuli.Add(Line(VelumCondensedStimulusKind.Команда, 0, "экспорт pdf"));

      string block = VelumAgentLogCondensedRules.BuildBlock(record, "\n");
      Assert.NotNull(block);
      Assert.Contains("Команда: \"экспорт pdf\"", block);
      Assert.Contains("У-рефлекс: ID 12, Подготовка чертежа", block);
    }

    [Fact]
    public void BuildBlock_WithEnvironmentStimulus_ReturnsEnvironmentLabel()
    {
      var record = new VelumCondensedLogRecord
      {
        ResponseKind = VelumCondensedResponseKind.Автоматизм,
        ResponseId = 3,
        ResponseActionName = "Подготовка чертежа"
      };
      record.Stimuli.Add(Line(VelumCondensedStimulusKind.Среда, 0, "65:200"));

      string block = VelumAgentLogCondensedRules.BuildBlock(record, "\n");
      Assert.NotNull(block);
      Assert.Contains("Среда: 65:200", block);
      Assert.Contains("Автоматизм: ID 3, Подготовка чертежа", block);
    }

    [Fact]
    public void BuildBlock_NullRecord_ReturnsNull()
    {
      Assert.Null(VelumAgentLogCondensedRules.BuildBlock(null, "\n"));
    }

    [Fact]
    public void BuildBlock_NoResponse_ReturnsNull()
    {
      var record = new VelumCondensedLogRecord();
      record.Stimuli.Add(Line(VelumCondensedStimulusKind.Воздействие, 1, "тест"));
      Assert.Null(VelumAgentLogCondensedRules.BuildBlock(record, "\n"));
    }

    [Fact]
    public void BuildBlock_NoStimuli_ReturnsNull()
    {
      var record = new VelumCondensedLogRecord
      {
        ResponseKind = VelumCondensedResponseKind.БезусловныйРефлекс,
        ResponseId = 5,
        ResponseActionName = "Экспорт PDF"
      };
      Assert.Null(VelumAgentLogCondensedRules.BuildBlock(record, "\n"));
    }

    [Fact]
    public void IsDuplicate_ReturnsTrue_WhenSignaturesMatch()
    {
      var record = new VelumCondensedLogRecord
      {
        ResponseKind = VelumCondensedResponseKind.БезусловныйРефлекс,
        ResponseId = 5,
        ResponseActionName = "Экспорт PDF"
      };
      record.Stimuli.Add(Line(VelumCondensedStimulusKind.Воздействие, 65, "Запуск экспорта PDF"));
      string sig = VelumAgentLogCondensedRules.BuildSignature(record);
      Assert.True(VelumAgentLogCondensedRules.IsDuplicate(sig, sig));
    }

    [Fact]
    public void IsDuplicate_ReturnsFalse_WhenSignaturesDiffer()
    {
      var record1 = new VelumCondensedLogRecord
      {
        ResponseKind = VelumCondensedResponseKind.БезусловныйРефлекс,
        ResponseId = 5,
        ResponseActionName = "Экспорт PDF"
      };
      record1.Stimuli.Add(Line(VelumCondensedStimulusKind.Воздействие, 65, "Запуск экспорта PDF"));

      var record2 = new VelumCondensedLogRecord
      {
        ResponseKind = VelumCondensedResponseKind.УсловныйРефлекс,
        ResponseId = 12,
        ResponseActionName = "Подготовка чертежа"
      };
      record2.Stimuli.Add(Line(VelumCondensedStimulusKind.Фраза, 0, "привет"));

      string sig1 = VelumAgentLogCondensedRules.BuildSignature(record1);
      string sig2 = VelumAgentLogCondensedRules.BuildSignature(record2);
      Assert.False(VelumAgentLogCondensedRules.IsDuplicate(sig1, sig2));
    }

    [Fact]
    public void IsDuplicate_EmptySignature_ReturnsFalse()
    {
      Assert.False(VelumAgentLogCondensedRules.IsDuplicate("", "anything"));
    }

    [Fact]
    public void BuildSignature_ConsistentWithStimulusLineBuildSignaturePart()
    {
      var record = new VelumCondensedLogRecord
      {
        ResponseKind = VelumCondensedResponseKind.БезусловныйРефлекс,
        ResponseId = 5,
        ResponseActionName = "Экспорт PDF"
      };
      record.Stimuli.Add(Line(VelumCondensedStimulusKind.Воздействие, 65, "Запуск экспорта PDF"));

      string sig = VelumAgentLogCondensedRules.BuildSignature(record);
      Assert.Contains("0:65:Запуск экспорта PDF", sig); // (int)Воздействие = 0
      Assert.Contains("=>0:5:Экспорт PDF", sig);
    }

    [Fact]
    public void ResponseLabel_CorrectNames()
    {
      Assert.Equal("Б/У рефлекс", VelumAgentLogCondensedRules.ResponseLabel(VelumCondensedResponseKind.БезусловныйРефлекс));
      Assert.Equal("У-рефлекс", VelumAgentLogCondensedRules.ResponseLabel(VelumCondensedResponseKind.УсловныйРефлекс));
      Assert.Equal("Автоматизм", VelumAgentLogCondensedRules.ResponseLabel(VelumCondensedResponseKind.Автоматизм));
    }

    [Fact]
    public void StimulusLineLabel_CorrectNames()
    {
      Assert.Equal("Воздействие", VelumCondensedStimulusLine.Label(VelumCondensedStimulusKind.Воздействие));
      Assert.Equal("Команда", VelumCondensedStimulusLine.Label(VelumCondensedStimulusKind.Команда));
      Assert.Equal("Фраза", VelumCondensedStimulusLine.Label(VelumCondensedStimulusKind.Фраза));
      Assert.Equal("Среда", VelumCondensedStimulusLine.Label(VelumCondensedStimulusKind.Среда));
    }

    [Fact]
    public void FormatResponseLine_NoId_NoName_ReturnsLabelAndDash()
    {
      var record = new VelumCondensedLogRecord
      {
        ResponseKind = VelumCondensedResponseKind.БезусловныйРефлекс,
        ResponseId = 0,
        ResponseActionName = string.Empty
      };
      Assert.Equal("Б/У рефлекс: —", record.FormatResponseLine());
    }

    [Fact]
    public void FormatResponseLine_OnlyId_ReturnsIdOnly()
    {
      var record = new VelumCondensedLogRecord
      {
        ResponseKind = VelumCondensedResponseKind.УсловныйРефлекс,
        ResponseId = 42,
        ResponseActionName = string.Empty
      };
      Assert.Equal("У-рефлекс: ID 42", record.FormatResponseLine());
    }

    [Fact]
    public void FormatResponseLine_NoResponseKind_ReturnsEmpty()
    {
      var record = new VelumCondensedLogRecord
      {
        ResponseKind = null,
        ResponseId = 0,
        ResponseActionName = string.Empty
      };
      Assert.Equal(string.Empty, record.FormatResponseLine());
    }

    [Fact]
    public void StimulusLine_FormatLine_WithEmptyIdAndText_ReturnsLabelAndDash()
    {
      VelumCondensedStimulusLine line = Line(VelumCondensedStimulusKind.Среда, 0, string.Empty);
      Assert.Equal("Среда: —", line.FormatLine());
    }

    [Fact]
    public void StimulusLine_FormatLine_WithOnlyId()
    {
      VelumCondensedStimulusLine line = Line(VelumCondensedStimulusKind.Среда, 10, string.Empty);
      Assert.Equal("Среда: ID 10", line.FormatLine());
    }

    [Fact]
    public void StimulusLine_FormatLine_VerbalQuotesTextNoId()
    {
      VelumCondensedStimulusLine line = Line(VelumCondensedStimulusKind.Фраза, 7, "привет");
      // Вербальный тип: ID не печатается, текст в кавычках.
      Assert.Equal("Фраза: \"привет\"", line.FormatLine());
    }

    [Fact]
    public void StimulusLine_BuildSignaturePart_IncludesAllFields()
    {
      VelumCondensedStimulusLine line = Line(VelumCondensedStimulusKind.Фраза, 0, "привет");
      string part = line.BuildSignaturePart();
      Assert.Equal("2:0:привет", part); // (int)Фраза = 2
    }

    [Fact]
    public void ShouldEmit_ReturnsFalse_WhenRecordIsNull()
    {
      Assert.False(VelumAgentLogCondensedRules.ShouldEmit(null));
    }

    [Fact]
    public void ShouldEmit_ReturnsTrue_WhenHasResponse()
    {
      var record = new VelumCondensedLogRecord
      {
        ResponseKind = VelumCondensedResponseKind.Автоматизм,
        ResponseId = 1,
        ResponseActionName = "Test"
      };
      Assert.True(VelumAgentLogCondensedRules.ShouldEmit(record));
    }

    [Fact]
    public void ShouldEmit_ReturnsFalse_WhenNoResponse()
    {
      var record = new VelumCondensedLogRecord();
      Assert.False(VelumAgentLogCondensedRules.ShouldEmit(record));
    }

    [Fact]
    public void BuildBlock_UsesNewLineParameter()
    {
      var record = new VelumCondensedLogRecord
      {
        ResponseKind = VelumCondensedResponseKind.БезусловныйРефлекс,
        ResponseId = 1,
        ResponseActionName = "y"
      };
      record.Stimuli.Add(Line(VelumCondensedStimulusKind.Фраза, 0, "x"));

      string block = VelumAgentLogCondensedRules.BuildBlock(record, "\n");
      string[] lines = block.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
      Assert.Contains(VelumAgentLogCondensedRules.StimulusHeader, lines);
      Assert.Contains("  Б/У рефлекс: ID 1, y", lines);
    }
  }
}
