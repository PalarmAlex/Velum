namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Контекст текущего опроса SW на такте пульса (UI-поток SolidWorks) для диагностических логов COM.
  /// </summary>
  internal static class VelumSolidProbeContext
  {
    [System.ThreadStatic]
    private static int _pulseNumber;

    [System.ThreadStatic]
    private static string _probeKey;

    internal static int PulseNumber
    {
      get => _pulseNumber;
      set => _pulseNumber = value;
    }

    internal static string ProbeKey
    {
      get => _probeKey ?? string.Empty;
      set => _probeKey = value;
    }

    internal static void Begin(int pulseNumber)
    {
      _pulseNumber = pulseNumber;
      _probeKey = null;
    }

    internal static void Clear()
    {
      _pulseNumber = 0;
      _probeKey = null;
    }
  }
}
