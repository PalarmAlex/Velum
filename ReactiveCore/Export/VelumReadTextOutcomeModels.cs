using System;

namespace Velum.ReactiveCore.Export
{
  /// <summary>
  /// Итог чтения текста файла с таймаутом: различает «файл прочитан»,
  /// «файл отсутствует» (первый запуск) и «чтение не удалось» (таймаут/ошибка).
  /// Вынесен в отдельный файл, чтобы линковаться в тестовый проект без тяги
  /// <c>VelumPathExists</c> (который делает реальный IO и не линкуется).
  /// </summary>
  /// <remarks>
  /// Регрессии: CASEBOOK-2 случай 20 — E41 (сбой чтения ≠ пустой реестр).
  /// </remarks>
  internal enum ReadTextOutcome
  {
    /// <summary>Файл прочитан (см. <see cref="ReadTextResult.Content"/>).</summary>
    Success,

    /// <summary>Файл отсутствует (или путь не существует) — не ошибка.</summary>
    FileMissing,

    /// <summary>Таймаут проверки/чтения или ошибка чтения доступного файла.</summary>
    Failed
  }

  /// <summary>Результат <c>VelumPathExists.ReadAllTextWithStatus</c>: текст и причина неудачи.</summary>
  internal readonly struct ReadTextResult
  {
    public ReadTextResult(ReadTextOutcome outcome, string content)
    {
      Outcome = outcome;
      Content = content;
    }

    internal ReadTextOutcome Outcome { get; }

    /// <summary>Содержимое файла; null при неудаче любого рода.</summary>
    internal string Content { get; }

    /// <summary>True — файл реально прочитан.</summary>
    internal bool Success => Outcome == ReadTextOutcome.Success;
  }
}
