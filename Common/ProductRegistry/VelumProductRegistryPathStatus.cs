namespace Velum.UI.ProductRegistry
{
  /// <summary>
  /// Статус пути записи реестра (только в памяти, не сохраняется в JSON).
  /// </summary>
  internal enum VelumProductRegistryPathStatus
  {
    /// <summary>Файл по пути найден.</summary>
    Ok = 0,

    /// <summary>Путь пуст или файл отсутствует.</summary>
    No = 1,

    /// <summary>Проверка не выполнена или прервана по таймауту.</summary>
    Unknown = 2
  }

  /// <summary>Текст и разбор статуса пути для списка и фильтра.</summary>
  internal static class VelumProductRegistryPathStatusText
  {
    /// <summary>Строчный текст — визуально легче и лучше читается в списке, чем «OK»/«NO».</summary>
    internal const string Ok = "ok";
    /// <summary>Выделяется регистром относительно ok / ?.</summary>
    internal const string No = "NO";
    internal const string Unknown = "?";

    internal static string ToDisplay(VelumProductRegistryPathStatus status)
    {
      switch (status)
      {
        case VelumProductRegistryPathStatus.Ok:
          return Ok;
        case VelumProductRegistryPathStatus.No:
          return No;
        default:
          return Unknown;
      }
    }

    internal static bool TryParseFilter(string text, out VelumProductRegistryPathStatus status)
    {
      status = VelumProductRegistryPathStatus.Unknown;
      string t = (text ?? string.Empty).Trim();
      if (string.Equals(t, Ok, System.StringComparison.OrdinalIgnoreCase))
      {
        status = VelumProductRegistryPathStatus.Ok;
        return true;
      }

      // «no» / «NO» — одно и то же (раньше в UI было «no»).
      if (string.Equals(t, No, System.StringComparison.OrdinalIgnoreCase) ||
          string.Equals(t, "no", System.StringComparison.OrdinalIgnoreCase))
      {
        status = VelumProductRegistryPathStatus.No;
        return true;
      }

      if (t == Unknown || string.Equals(t, "unknown", System.StringComparison.OrdinalIgnoreCase))
      {
        status = VelumProductRegistryPathStatus.Unknown;
        return true;
      }

      return false;
    }
  }
}
