namespace ISIDA.Common
{
  /// <summary>
  /// Заглушка счётчика глобального пульса ISIDA для тестов (без isida.dll).
  /// </summary>
  internal static class GlobalTimer
  {
    /// <summary>Текущий номер глобального пульса (управляется тестом).</summary>
    internal static int GlobalPulsCount { get; set; }
  }
}
