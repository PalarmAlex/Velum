using System.Collections.Generic;

namespace ISIDA.Common
{
  /// <summary>
  /// Заглушка логгера ISIDA для тестов: сообщения накапливаются в памяти.
  /// </summary>
  internal static class Logger
  {
    internal static readonly List<string> InfoMessages = new List<string>();

    internal static readonly List<string> ErrorMessages = new List<string>();

    internal static void Info(string message)
    {
      InfoMessages.Add(message ?? string.Empty);
    }

    internal static void Error(string message)
    {
      ErrorMessages.Add(message ?? string.Empty);
    }
  }
}
