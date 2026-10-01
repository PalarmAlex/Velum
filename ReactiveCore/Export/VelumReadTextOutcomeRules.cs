using System;

namespace Velum.ReactiveCore.Export
{
  /// <summary>
  /// Pure-правила интерпретации трёхзначного исхода чтения с таймаутом
  /// (<see cref="ReadTextOutcome"/>) и гейта записи файловых хранилищ.
  /// Никакого IO, COM и конфигурации — только чистые предикаты над исходом и набором
  /// данных. Линкуется в тестовый проект Velum.ReactiveCore.Tests.
  /// </summary>
  /// <remarks>
  /// Регрессии, которые защищают правила: CASEBOOK-2 случай 20 — E41 («не удалось
  /// прочитать» не равно «пусто») и E42 (автовосстановление дефолта в Load() только
  /// при доказанном отсутствии данных; гейд уровня записи пустого набора каталогов).
  /// </remarks>
  internal static class VelumReadTextOutcomeRules
  {
    /// <summary>
    /// Легитимно ли отсутствие файла (первый запуск): только явный <c>FileMissing</c>.
    /// <c>Failed</c> (таймаут/ошибка на доступном пути) — НЕ отсутствие: файл может
    /// существовать, и подменять его пустым набором нельзя (E41).
    /// </summary>
    /// <param name="outcome">Исход чтения.</param>
    /// <returns>true, если данные legitimately отсутствуют.</returns>
    internal static bool IsFileMissing(ReadTextOutcome outcome)
    {
      return outcome == ReadTextOutcome.FileMissing;
    }

    /// <summary>
    /// Недостоверны ли прочитанные данные: любой <c>Failed</c> означает, что живой
    /// файл мог быть, но мы его не прочли — молча подменять такие данные пустыми
    /// нельзя (E41).
    /// </summary>
    /// <param name="outcome">Исход чтения.</param>
    /// <returns>true, если загрузку нужно прервать, не трогая память и диск.</returns>
    internal static bool IsDataUntrusted(ReadTextOutcome outcome)
    {
      return outcome == ReadTextOutcome.Failed;
    }

    /// <summary>
    /// Прерывать ли загрузку хранилища из-за сбоя чтения любого из файлов:
    /// достаточно одного <c>Failed</c>, чтобы не трогать ни память, ни диск (E41).
    /// </summary>
    /// <param name="outcomes">Исходы чтения всех файлов хранилища.</param>
    /// <returns>true, если нужно бросить исключение и оставить всё как было.</returns>
    internal static bool ShouldAbortLoad(ReadTextOutcome[] outcomes)
    {
      if (outcomes == null)
        return false;
      for (int i = 0; i < outcomes.Length; i++)
      {
        if (IsDataUntrusted(outcomes[i]))
          return true;
      }
      return false;
    }

    /// <summary>
    /// Создавать ли и сохранять дефолтный набор при пустом (<c>FileMissing</c>)
    /// хранилище. Автовосстановление дефолта допустимо только при доказанном
    /// отсутствии данных; на <c>Failed</c> это уничтожитель данных — Save()
    /// перезапишет живые файлы дефолтом (E42).
    /// </summary>
    /// <param name="outcome">Исход чтения файла данных.</param>
    /// <param name="dataIsEmpty">Пуст ли в памяти набор после загрузки.</param>
    /// <returns>true, если легитимно создать дефолт и сохранить его.</returns>
    internal static bool ShouldBootstrapDefault(
        ReadTextOutcome outcome, bool dataIsEmpty)
    {
      return dataIsEmpty && IsFileMissing(outcome);
    }

    /// <summary>
    /// Гейт записи файла каталогов: блокирует запись заведомо нелегитимного пустого
    /// набора (0 каталогов) — через UI последний каталог удалить нельзя, поэтому пустая
    /// запись сюда легально не доходит, а значит пришла из сбоя (E42).
    /// </summary>
    /// <param name="folderCount">Число каталогов в записываемом наборе.</param>
    /// <returns>true, если запись нужно отклонить (бросить исключение).</returns>
    internal static bool ShouldBlockEmptyFoldersWrite(int folderCount)
    {
      return folderCount <= 0;
    }
  }
}