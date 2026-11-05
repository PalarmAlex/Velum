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
    public void BuildBlock_AutomatizmWithColorOnlyStimulus_ReturnsColorLine()
    {
      // Регресс бага: образ-спуск автоматизма пуст по вербальным каналам, несёт только цвет.
      // Раньше такой блок не строился (Stimuli пуст) и запуск автоматизма терялся в ленте.
      var record = new VelumCondensedLogRecord
      {
        ResponseKind = VelumCondensedResponseKind.Автоматизм,
        ResponseId = 1,
        ResponseActionName = "Экспорт PDF"
      };
      record.Stimuli.Add(Line(VelumCondensedStimulusKind.ЦветовойФон, 0, "Оранжевый"));

      string block = VelumAgentLogCondensedRules.BuildBlock(record, "\n");
      Assert.NotNull(block);
      Assert.Contains(VelumAgentLogCondensedRules.StimulusHeader, block);
      Assert.Contains("Цветовой фон: Оранжевый", block);
      Assert.Contains("Автоматизм: ID 1, Экспорт PDF", block);
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
      Assert.Equal("Цветовой фон", VelumCondensedStimulusLine.Label(VelumCondensedStimulusKind.ЦветовойФон));
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
    public void StimulusLine_FormatLine_ColorPrintsNameNoIdNoQuotes()
    {
      // Цвет: код 0 (белый) валиден, но по hasId не прошёл бы — печатаем именем без кавычек.
      VelumCondensedStimulusLine line = Line(VelumCondensedStimulusKind.ЦветовойФон, 0, "Оранжевый");
      Assert.Equal("Цветовой фон: Оранжевый", line.FormatLine());
    }

    [Fact]
    public void StimulusLine_FormatLine_ColorEmptyText_ReturnsLabelAndDash()
    {
      VelumCondensedStimulusLine line = Line(VelumCondensedStimulusKind.ЦветовойФон, 0, string.Empty);
      Assert.Equal("Цветовой фон: —", line.FormatLine());
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

    [Fact]
    public void PrependBlocks_EmptyExisting_PlacesBlocksOnTop()
    {
      var blocks = new List<string>
      {
        "СТИМУЛ:\n  Воздействие: ID 1\nОТВЕТ:\n  Б/У рефлекс: ID 1",
        "СТИМУЛ:\n  Фраза: \"hi\"\nОТВЕТ:\n  У-рефлекс: ID 2"
      };
      string result = VelumAgentLogCondensedRules.PrependBlocks(string.Empty, blocks, "\n");
      Assert.StartsWith(blocks[1], result);
      Assert.Contains(blocks[0], result);
    }

    [Fact]
    public void PrependBlocks_ReversesOrder_FreshestFirst()
    {
      var blocks = new List<string>
      {
        "OLD",
        "NEW"
      };
      string result = VelumAgentLogCondensedRules.PrependBlocks("EXISTING", blocks, "\n");
      // Самый свежий (NEW) должен быть первым, потом OLD, потом EXISTING.
      int posNew = result.IndexOf("NEW");
      int posOld = result.IndexOf("OLD");
      int posExisting = result.IndexOf("EXISTING");
      Assert.True(posNew >= 0 && posOld > posNew, "NEW должен быть выше OLD");
      Assert.True(posExisting > posOld, "OLD должен быть выше EXISTING");
    }

    [Fact]
    public void PrependBlocks_NullBlocks_ReturnsExisting()
    {
      const string existing = "DATA";
      Assert.Equal(existing, VelumAgentLogCondensedRules.PrependBlocks(existing, null, "\n"));
    }

    [Fact]
    public void PrependBlocks_EmptyBlocks_ReturnsExisting()
    {
      const string existing = "DATA";
      Assert.Equal(existing, VelumAgentLogCondensedRules.PrependBlocks(existing, new List<string>(), "\n"));
    }

    [Fact]
    public void PrependBlocks_NullExisting_PlacesBlocksOnly()
    {
      var blocks = new List<string> { "A", "B" };
      string result = VelumAgentLogCondensedRules.PrependBlocks(null, blocks, "\n");
      // B свежее (последний в списке) — сверху, A ниже.
      Assert.StartsWith("B", result);
      Assert.EndsWith("A", result);
    }

    [Fact]
    public void TrimToHead_WithinLimit_ReturnsUnchanged()
    {
      const string text = "SHORT";
      Assert.Equal(text, VelumAgentLogCondensedRules.TrimToHead(text, 100, "\n"));
    }

    [Fact]
    public void TrimToHead_EmptyText_ReturnsEmpty()
    {
      Assert.Equal(string.Empty, VelumAgentLogCondensedRules.TrimToHead(string.Empty, 100, "\n"));
    }

    [Fact]
    public void TrimToHead_NullText_ReturnsEmpty()
    {
      Assert.Equal(string.Empty, VelumAgentLogCondensedRules.TrimToHead(null, 100, "\n"));
    }

    [Fact]
    public void TrimToHead_ExceedsLimit_CutsOldTail()
    {
      // Модель ленты: свежее в начале. "NEW" + разделитель + "OLD" — OLD в хвосте.
      string text = "NEW BLOCK" + "\n\n" + "OLD BLOCK";
      string result = VelumAgentLogCondensedRules.TrimToHead(text, 11, "\n");
      Assert.Contains("NEW", result);
      Assert.DoesNotContain("OLD", result);
    }

    [Fact]
    public void TrimToHead_CutsByBlockBoundary_NotInMiddleOfBlock()
    {
      string sep = "\n\n";
      // b1 — самый свежий (в начале), b3 — самый старый (в хвосте).
      string text = "b1" + sep + "b2" + sep + "b3";

      // Лимит заведомо больше текста — ничего не режется.
      Assert.Equal(text, VelumAgentLogCondensedRules.TrimToHead(text, 1000, "\n"));

      // Маленький лимит — остаётся только верхний (самый свежий) блок целиком, не разорванный.
      string result = VelumAgentLogCondensedRules.TrimToHead(text, 8, "\n");
      Assert.Equal("b1", result);
    }

    [Fact]
    public void TrimToHead_SingleBlockWithoutSeparator_NotCut()
    {
      // Один блок без разделителей резать нельзя — иначе потеряем единственный (свежий) блок.
      string text = new string('x', 500);
      Assert.Equal(text, VelumAgentLogCondensedRules.TrimToHead(text, 100, "\n"));
    }

    [Fact]
    public void BuildBlock_WithTimeText_PrependsTimeAsFirstLine()
    {
      var record = new VelumCondensedLogRecord
      {
        ResponseKind = VelumCondensedResponseKind.БезусловныйРефлекс,
        ResponseId = 5,
        ResponseActionName = "Экспорт PDF",
        TimeText = "2026-05-13 14:23:45"
      };
      record.Stimuli.Add(Line(VelumCondensedStimulusKind.Воздействие, 65, "Запуск экспорта PDF"));

      string block = VelumAgentLogCondensedRules.BuildBlock(record, "\n");
      Assert.NotNull(block);
      string[] lines = block.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
      // Время (hh:mm:ss) — самая первая строка блока.
      Assert.Equal("14:23:45", lines[0]);
      Assert.Equal(VelumAgentLogCondensedRules.StimulusHeader, lines[1]);
    }

    [Fact]
    public void BuildBlock_WithConditionedReflexStrength_AppendsStrengthToResponse()
    {
      var record = new VelumCondensedLogRecord
      {
        ResponseKind = VelumCondensedResponseKind.УсловныйРефлекс,
        ResponseId = 12,
        ResponseActionName = "Подготовка чертежа",
        AssociationStrength = 0.85f
      };
      record.Stimuli.Add(Line(VelumCondensedStimulusKind.Фраза, 0, "привет"));

      string block = VelumAgentLogCondensedRules.BuildBlock(record, "\n");
      Assert.NotNull(block);
      Assert.Contains("У-рефлекс: ID 12, Подготовка чертежа (К=0.85)", block);
    }

    [Fact]
    public void BuildBlock_WithAutomatizmUsefulness_AppendsWeightToResponse()
    {
      var record = new VelumCondensedLogRecord
      {
        ResponseKind = VelumCondensedResponseKind.Автоматизм,
        ResponseId = 3,
        ResponseActionName = "Экспорт PDF",
        AutomatizmUsefulness = 3
      };
      record.Stimuli.Add(Line(VelumCondensedStimulusKind.Среда, 0, "65:200"));

      string block = VelumAgentLogCondensedRules.BuildBlock(record, "\n");
      Assert.NotNull(block);
      Assert.Contains("Автоматизм: ID 3, Экспорт PDF (вес 3)", block);
    }

    [Fact]
    public void BuildBlock_ConditionedReflexWithoutStrength_NoDetailsSuffix()
    {
      var record = new VelumCondensedLogRecord
      {
        ResponseKind = VelumCondensedResponseKind.УсловныйРефлекс,
        ResponseId = 12,
        ResponseActionName = "Подготовка чертежа",
        AssociationStrength = null
      };
      record.Stimuli.Add(Line(VelumCondensedStimulusKind.Фраза, 0, "привет"));

      string block = VelumAgentLogCondensedRules.BuildBlock(record, "\n");
      Assert.NotNull(block);
      Assert.Contains("У-рефлекс: ID 12, Подготовка чертежа", block);
      Assert.DoesNotContain("(К=", block);
    }

    [Fact]
    public void NormalizeNewLines_LiteralEscapes_BecomeRealLineBreaks()
    {
      // Регресс: движок отдаёт текст с литеральными «\n» (два символа: слэш + n),
      // многострочный TextBox их не отображает как перенос — блок слипался в одну строку.
      string text = "СОЗДАНИЕ:\\n  Селективный клон - Автоматизм: ID 1, (вес 0)";
      string result = VelumAgentLogCondensedRules.NormalizeNewLines(text);
      Assert.Equal("СОЗДАНИЕ:" + Environment.NewLine + "  Селективный клон - Автоматизм: ID 1, (вес 0)", result);
    }

    [Fact]
    public void NormalizeNewLines_BareLineFeed_DoesNotProduceDoubleCarriageReturn()
    {
      // Регресс: при обратном порядке замен «\n» → Environment.NewLine давало «\r\r\n»
      // (лишние пустые строки в окне вывода).
      string result = VelumAgentLogCondensedRules.NormalizeNewLines("19:42:22\nСОЗДАНИЕ:");
      Assert.Equal("19:42:22" + Environment.NewLine + "СОЗДАНИЕ:", result);
      Assert.DoesNotContain("\r\r", result);
    }

    [Fact]
    public void NormalizeNewLines_MixedCrlfAndLiteral_IsIdempotent()
    {
      string once = VelumAgentLogCondensedRules.NormalizeNewLines("а\r\nб\\nв");
      Assert.Equal("а" + Environment.NewLine + "б" + Environment.NewLine + "в", once);
      // Повторный прогон уже нормализованного текста ничего не добавляет.
      Assert.Equal(once, VelumAgentLogCondensedRules.NormalizeNewLines(once));
    }

    [Fact]
    public void NormalizeNewLines_EmptyAndNull_PassThrough()
    {
      Assert.Null(VelumAgentLogCondensedRules.NormalizeNewLines(null));
      Assert.Equal(string.Empty, VelumAgentLogCondensedRules.NormalizeNewLines(string.Empty));
    }
  }
}
