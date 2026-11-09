using System;
using Velum.UI.Logs;
using Xunit;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>
  /// Регрессия экранирования текста токена событийного лога (<c>AgentLogs_Events.csv</c>):
  /// текст токена может содержать <c>;</c>, CR, LF и обратный слэш — без кодирования они
  /// ломают CSV (строка делится по <c>;</c>, блок-сессия режется на строки). Round-trip
  /// <c>Decode(Encode(x)) == x</c> — минимальная гарантия, что форма логов и отчёт
  /// покажут тот же текст, что виден в окне «Ответ агента».
  /// </summary>
  public class VelumEventLogCsvRulesTests
  {
    [Fact]
    public void Encode_BackslashFirst_ThenSemicolons()
    {
      // Настоящий слэш обязан стать «\\», а не слиться со служебными парами следующих шагов.
      Assert.Equal("a\\\\b", VelumEventLogCsvRules.Encode("a\\b"));
      Assert.Equal("a\\;b", VelumEventLogCsvRules.Encode("a;b"));
      Assert.Equal("a\\rb", VelumEventLogCsvRules.Encode("a\rb"));
      Assert.Equal("a\\nb", VelumEventLogCsvRules.Encode("a\nb"));
    }

    [Fact]
    public void Encode_BackslashBeforeSemicolon_DoesNotDoubleEscape()
    {
      // Классическая ловушка порядка замен: если сначала «;»→«\;», а потом «\»→«\\», 
      // строка «;» превратилась бы в «\\;» и декодирование вернуло бы «\;», а не «;».
      string encoded = VelumEventLogCsvRules.Encode(";");
      Assert.Equal("\\;", encoded);
      Assert.Equal(";", VelumEventLogCsvRules.Decode(encoded));
    }

    [Fact]
    public void Encode_RemovesAllSeparatorsFromText()
    {
      string encoded = VelumEventLogCsvRules.Encode("а;б\rв\ng\\д");
      // «;» остаётся в тексте, но экранирована слэшем («\;»); переносы заменяются
      // буквенными парами «\r» / «\n» — физических CR/LF в строке быть не должно.
      Assert.DoesNotContain('\r', encoded);
      Assert.DoesNotContain('\n', encoded);
      Assert.Equal("а\\;б\\rв\\ng\\\\д", encoded);
    }

    [Fact]
    public void Decode_StandardPairs_RestoreOriginalCharacters()
    {
      Assert.Equal("a\\b", VelumEventLogCsvRules.Decode("a\\\\b"));
      Assert.Equal("a;b", VelumEventLogCsvRules.Decode("a\\;b"));
      Assert.Equal("a\rb", VelumEventLogCsvRules.Decode("a\\rb"));
      Assert.Equal("a\nb", VelumEventLogCsvRules.Decode("a\\nb"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("обычный текст без спецсимволов")]
    [InlineData("№ пульса: 042")]
    [InlineData("а;б;в")]
    [InlineData("многострочный\r\ntext\nсо\r смесью")]
    [InlineData("Слэш \\ и ещё \\\\ подряд")]
    [InlineData("СТИМУЛ:\n  фразы: п\nОТВЕТ:\n  Б/У рефлекс: ID 5, Экспорт PDF")]
    [InlineData("У-рефлекс: ID 12, (К=0.85)\nповтор ×3")]
    [InlineData("смешанно: \\; \\\\ \n текст")]
    [InlineData(";;\\;\\;\\\\")]
    public void RoundTrip_DecodeOfEncodeEqualsOriginal(string text)
    {
      string encoded = VelumEventLogCsvRules.Encode(text);
      string decoded = VelumEventLogCsvRules.Decode(encoded);

      string expected = string.IsNullOrEmpty(text) ? string.Empty : text;
      Assert.Equal(expected, decoded);
    }

    [Fact]
    public void RoundTrip_TypicalCondensedLogBlock_PreservesNewLinesAndColons()
    {
      // Типовой блок движка: переносы строк и двоеточия внутри текста.
      string block =
          "СТИМУЛ:\r\n" +
          "  Фраза: \"собери деталь\"\r\n" +
          "  Воздействие: ID 65, Запуск экспорта PDF\r\n" +
          "ОТВЕТ:\r\n" +
          "  Автоматизм: ID 3, Экспорт PDF (вес 42)";

      string encoded = VelumEventLogCsvRules.Encode(block);
      Assert.DoesNotContain('\r', encoded);
      Assert.DoesNotContain('\n', encoded);
      Assert.DoesNotContain(';', encoded);

      Assert.Equal(block, VelumEventLogCsvRules.Decode(encoded));
    }

    [Fact]
    public void Encode_IdempotentDecode_OnPlainTextWithoutSpecials()
    {
      // Текст без спецсимволов не должен меняться ни при кодировании, ни при декодировании.
      string plain = "Б/У рефлекс: ID 5, Экспорт PDF";
      Assert.Equal(plain, VelumEventLogCsvRules.Encode(plain));
      Assert.Equal(plain, VelumEventLogCsvRules.Decode(plain));
    }

    [Fact]
    public void Decode_UnknownEscapePair_KeepsBothCharacters()
    {
      // Неизвестная escape-пара (ручная правка файла, «\x») не должна съедать символ.
      Assert.Equal("a\\xb", VelumEventLogCsvRules.Decode("a\\xb"));
      Assert.Equal("\\q", VelumEventLogCsvRules.Decode("\\q"));
    }

    [Fact]
    public void Decode_TrailingBackslash_IsPreserved()
    {
      // Висящий в конце слэш (обрывок) — сохраняем, а не теряем.
      Assert.Equal("abc\\", VelumEventLogCsvRules.Decode("abc\\"));
    }

    [Fact]
    public void Decode_EmptyOrNull_ReturnsEmpty()
    {
      Assert.Equal(string.Empty, VelumEventLogCsvRules.Decode(null));
      Assert.Equal(string.Empty, VelumEventLogCsvRules.Decode(""));
    }

    [Fact]
    public void Encode_NullOrEmpty_ReturnsEmpty()
    {
      Assert.Equal(string.Empty, VelumEventLogCsvRules.Encode(null));
      Assert.Equal(string.Empty, VelumEventLogCsvRules.Encode(""));
    }

    [Fact]
    public void BuildHeaderLine_HasColumnsInRequiredOrder()
    {
      // Колонка Text — последняя: читалка делит строку по «;» и берёт Time/Pulse/Repeat
      // по индексам до текста, поэтому их позиции фиксированы.
      Assert.Equal("Time;Pulse;Repeat;Text", VelumEventLogCsvRules.BuildHeaderLine());

      string[] columns = VelumEventLogCsvRules.BuildHeaderLine().Split(';');
      Assert.Equal("Time", columns[0]);
      Assert.Equal("Pulse", columns[1]);
      Assert.Equal("Repeat", columns[2]);
      Assert.Equal("Text", columns[columns.Length - 1]);
    }

    [Fact]
    public void BuildHeaderLine_IsRecognizedAsHeaderBySameMarkersAsReader()
    {
      // Совпадение маркеров заголовка с тем, что использует предикат IsHeaderRow у
      // VelumEventLogFileSessions: наличие всех трёх имён колонок и разделителя «;».
      string header = VelumEventLogCsvRules.BuildHeaderLine();
      Assert.Contains("Time", header, StringComparison.Ordinal);
      Assert.Contains("Pulse", header, StringComparison.Ordinal);
      Assert.Contains("Repeat", header, StringComparison.Ordinal);
      Assert.Contains(";", header, StringComparison.Ordinal);
    }

    [Fact]
    public void Encode_EncodedTextContainsNoPhysicalLineBreaks()
    {
      // Ключевое свойство формата: закодированный текст — одна физическая строка
      // (переносы заменены буквенными парами «\r»/«\n»), поэтому блок-сессия не режется
      // на строки. «;» остаётся, но экранирована слэшем; читалка берёт текст
      // последней колонкой целиком (Split с лимитом 4), поэтому «;» внутри не мешает.
      string tricky = "ключ: значение;\r\nследующая; строка\\со слэшем";
      string encoded = VelumEventLogCsvRules.Encode(tricky);
      Assert.DoesNotContain('\r', encoded);
      Assert.DoesNotContain('\n', encoded);

      // Строка данных целиком: 4 поля, текст — последнее (забирает остаток строки).
      string line = "12:34:56;10;1;" + encoded;
      string[] parts = line.Split(new[] { ';' }, 4);
      Assert.Equal(4, parts.Length);
      Assert.Equal("12:34:56", parts[0]);
      Assert.Equal("10", parts[1]);
      Assert.Equal("1", parts[2]);
      Assert.Equal(encoded, parts[3]);
      Assert.Equal(tricky, VelumEventLogCsvRules.Decode(parts[3]));
    }

    [Fact]
    public void Decode_RestoresSemicolonInsideText_EvenWhenSplitLimitedToFour()
    {
      // Текст с несколькими «;» — после кодирования в строке данных они экранированы;
      // Split с лимитом 4 отдаёт текст целиком, Decode возвращает исходные «;».
      string text = "a;b;c";
      string line = "00:00:01;7;2;" + VelumEventLogCsvRules.Encode(text);
      string[] parts = line.Split(new[] { ';' }, 4);

      Assert.Equal("00:00:01", parts[0]);
      Assert.Equal("7", parts[1]);
      Assert.Equal("2", parts[2]);
      Assert.Equal(text, VelumEventLogCsvRules.Decode(parts[3]));
    }
  }
}