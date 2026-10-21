namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Копия перечисления из VelumSolidSessionHealth.cs (тот файл не линкуется —
  /// тянет COM-зависимости). Значения обязаны совпадать с оригиналом.
  /// </summary>
  public enum VelumSolidProbeErrorKind
  {
    None = 0,
    SessionUnavailable = 1,
    ComAccessFailed = 2,
    PartialProbeFailure = 3,
    TimedOut = 4
  }
}
