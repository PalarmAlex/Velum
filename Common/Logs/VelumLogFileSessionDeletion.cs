using System;
using ISIDA.Common;
using Velum.Isida;

namespace Velum.UI.Logs
{
  /// <summary>
  /// Безопасное удаление сессий из CSV-логов, которые одновременно удерживает открытыми
  /// <see cref="ResearchLogger"/> (isida.dll загружена в тот же процесс SolidWorks, что и надстройка,
  /// и держит writers с <c>FileShare.Read</c>). Без освобождения writers перезапись файла падает
  /// как «файл занят процессом».
  /// </summary>
  /// <remarks>
  /// Реализация повторяет эталонный <c>LogFileSessionDeletion</c> из AIStudio: операция перезаписи
  /// оборачивается в <see cref="ResearchLogger.RunLogFileMaintenance"/>, который временно закрывает
  /// свои writers нужной области, выполняет действие (файл в этот момент физически свободен)
  /// и переоткрывает writers. Если логгер недоступен (ISIDA не инициализирована либо уже освобождена),
  /// файлы никем не удерживаются — удаление выполняется напрямую.
  /// </remarks>
  internal static class VelumLogFileSessionDeletion
  {
    /// <summary>
    /// Выполняет <paramref name="deleteAction"/> с освобождением writers логгера для указанной области логов.
    /// </summary>
    public static void Run(ResearchLogger.LogFileMaintenanceScope scope, Action deleteAction)
    {
      if (deleteAction == null)
        throw new ArgumentNullException(nameof(deleteAction));

      ResearchLogger logger = TryGetLogger();
      if (logger == null || logger.IsDisposed)
      {
        deleteAction();
        return;
      }

      logger.RunLogFileMaintenance(scope, deleteAction);
    }

    private static ResearchLogger TryGetLogger()
    {
      try
      {
        return VelumIsidaHost.IsReady ? VelumIsidaHost.Context.ResearchLogger : null;
      }
      catch
      {
        // Контекст мог быть освобождён параллельно (Shutdown) — считаем логгер недоступным.
        return null;
      }
    }
  }
}
