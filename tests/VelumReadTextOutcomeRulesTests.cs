using Velum.ReactiveCore.Export;
using Xunit;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>
  /// Тесты pure-правил интерпретации трёхзначного исхода чтения и гейта записи.
  /// Защищают регрессии CASEBOOK-2 случай 20: E41 («не удалось прочитать» ≠ «пусто»)
  /// и E42 (дефолт только при доказанном отсутствии; блок записи пустого набора каталогов).
  /// </summary>
  public class VelumReadTextOutcomeRulesTests
  {
    // Параметры [Theory] — int, а не ReadTextOutcome: enum internal, а публичный
    // тестовый метод не может открывать internal-тип в сигнатуре (CS0051).
    private const int Success = (int)ReadTextOutcome.Success;
    private const int FileMissing = (int)ReadTextOutcome.FileMissing;
    private const int Failed = (int)ReadTextOutcome.Failed;

    [Theory]
    [InlineData(Success, false)]
    [InlineData(FileMissing, true)]
    [InlineData(Failed, false)]
    public void IsFileMissing_OnlyForExplicitAbsence(int outcome, bool expected)
    {
      Assert.Equal(expected, VelumReadTextOutcomeRules.IsFileMissing((ReadTextOutcome)outcome));
    }

    [Theory]
    [InlineData(Success, false)]
    [InlineData(FileMissing, false)]
    [InlineData(Failed, true)]
    public void IsDataUntrusted_OnlyForFailure(int outcome, bool expected)
    {
      Assert.Equal(expected, VelumReadTextOutcomeRules.IsDataUntrusted((ReadTextOutcome)outcome));
    }

    [Fact]
    public void ShouldAbortLoad_AnyFailedAmongMany_True()
    {
      // E41: сбой чтения хотя бы одного файла — прерывать загрузку целиком.
      Assert.True(VelumReadTextOutcomeRules.ShouldAbortLoad(new[] { ReadTextOutcome.Success, ReadTextOutcome.Failed }));
      Assert.True(VelumReadTextOutcomeRules.ShouldAbortLoad(new[] { ReadTextOutcome.Failed, ReadTextOutcome.FileMissing }));
    }

    [Fact]
    public void ShouldAbortLoad_NoFailed_False()
    {
      Assert.False(VelumReadTextOutcomeRules.ShouldAbortLoad(new[] { ReadTextOutcome.Success, ReadTextOutcome.FileMissing }));
      Assert.False(VelumReadTextOutcomeRules.ShouldAbortLoad(new[] { ReadTextOutcome.Success, ReadTextOutcome.Success }));
    }

    [Fact]
    public void ShouldAbortLoad_NullOrEmpty_False()
    {
      Assert.False(VelumReadTextOutcomeRules.ShouldAbortLoad(null));
      Assert.False(VelumReadTextOutcomeRules.ShouldAbortLoad(new ReadTextOutcome[0]));
    }

    [Fact]
    public void ShouldBootstrapDefault_EmptyAndFileMissing_True()
    {
      // Первый запуск: файла нет и в памяти пусто — легитимно создать дефолт.
      Assert.True(VelumReadTextOutcomeRules.ShouldBootstrapDefault(ReadTextOutcome.FileMissing, dataIsEmpty: true));
    }

    [Fact]
    public void ShouldBootstrapDefault_EmptyButFailed_False()
    {
      // E42: на сбое чтения дефолт запрещён — Save() затрёт живые файлы пустышкой.
      Assert.False(VelumReadTextOutcomeRules.ShouldBootstrapDefault(ReadTextOutcome.Failed, dataIsEmpty: true));
    }

    [Fact]
    public void ShouldBootstrapDefault_NotEmpty_False()
    {
      Assert.False(VelumReadTextOutcomeRules.ShouldBootstrapDefault(ReadTextOutcome.FileMissing, dataIsEmpty: false));
      Assert.False(VelumReadTextOutcomeRules.ShouldBootstrapDefault(ReadTextOutcome.Success, dataIsEmpty: false));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(-1, true)]
    [InlineData(1, false)]
    public void ShouldBlockEmptyFoldersWrite_BlocksNonPositive(int folderCount, bool expected)
    {
      // E42: запись folders.json с нулём каталогов — всегда след сбоя, а не легитимное состояние.
      Assert.Equal(expected, VelumReadTextOutcomeRules.ShouldBlockEmptyFoldersWrite(folderCount));
    }
  }
}
