using System;
using System.Diagnostics;
using ISIDA.Gomeostas;

namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Отладочная трассировка SW-гомеостаз (окно Output, отладчик).
  /// По умолчанию пишутся только ошибки и таймауты; подробности пульса по флагу.
  /// </summary>
  internal static class VelumSolidDiagLog
  {
    /// <summary>
    /// Пульсовая трассировка: запись метрик хоста до шага гомеостаза.
    /// Вместе с движком включайте SetPulseSolidHomeostasisTrace.
    /// </summary>
    internal static volatile bool PulseSolidHomeostasis;

    /// <summary>
    /// Устанавливает трассировку Velum и ParameterData.TracePulseHold в движке ISIDA.
    /// </summary>
    internal static void SetPulseSolidHomeostasisTrace(bool enabled)
    {
      PulseSolidHomeostasis = enabled;
      GomeostasSystem.ParameterData.TracePulseHold = enabled;
    }

    /// <summary>Ошибки, таймауты и деградация SessionHealth — всегда в Output (один sink).</summary>
    internal static void WriteError(string message)
    {
      try
      {
        string line = "[Velum.SolidDiag] " + DateTime.Now.ToString("HH:mm:ss.fff") + " " + message;
        Debug.WriteLine(line);
      }
      catch
      {
      }
    }

    /// <summary>Подробности пульса — только при включённом PulseSolidHomeostasis.</summary>
    internal static void WriteVerbose(string message)
    {
      if (!PulseSolidHomeostasis)
        return;
      WriteError(message);
    }

    internal static void WritePulseHost(string message)
    {
      WriteVerbose(message);
    }

    /// <summary>Трассировка опроса SW на пульсе — только при таймауте.</summary>
    internal static void WriteSolidProbeRefresh(
        int pulseNumber,
        bool timedOut,
        long elapsedMs,
        bool usedStale,
        int budgetMs)
    {
      if (!timedOut)
        return;

      WriteError(
          "SolidProbe pulse=" + pulseNumber +
          " timeout=1 elapsedMs=" + elapsedMs +
          " usedStale=" + (usedStale ? "1" : "0") +
          " budgetMs=" + budgetMs);
    }

    /// <summary>SessionHealth — при деградации или ошибке опроса.</summary>
    internal static void WriteSessionHealth(int pulseNumber, float score, VelumSolidProbeErrorKind kind)
    {
      if (kind == VelumSolidProbeErrorKind.None &&
          score >= VelumSolidSessionHealth.ScoreHealthy - 0.5f)
        return;

      WriteError(
          "SessionHealth pulse=" + pulseNumber +
          " score=" + score.ToString("F0") +
          " kind=" + kind);
    }

    /// <summary>Переход CAD-degraded в normal (п. 3).</summary>
    internal static void WriteCadDegradedTransition(
        int pulseNumber,
        bool degradedActive,
        float sessionHealthScore,
        float enterThreshold,
        float exitThreshold)
    {
      string line = "CadDegraded pulse=" + pulseNumber +
                      " active=" + (degradedActive ? "1" : "0") +
                      " sessionHealth=" + sessionHealthScore.ToString("F0") +
                      " enterLt=" + enterThreshold.ToString("F0") +
                      " exitGt=" + exitThreshold.ToString("F0");
      WriteError(line);
    }

    /// <summary>Перехваченный COM-вызов при опросе SW (всегда в Output для диагностики).</summary>
    internal static void WriteComProbe(string site, string rcwType, string member, string contextSuffix, Exception ex)
    {
      int pulse = VelumSolidProbeContext.PulseNumber;
      string probe = VelumSolidProbeContext.ProbeKey;
      string pulsePart = pulse > 0 ? " pulse=" + pulse : string.Empty;
      string probePart = string.IsNullOrEmpty(probe) ? string.Empty : " probe=" + probe;
      WriteError(
          "COM" + pulsePart + probePart +
          " site=" + site +
          " rcw=" + rcwType +
          "." + member +
          contextSuffix +
          ": " + FormatEx(ex));
    }

    internal static string FormatEx(Exception ex)
    {
      if (ex == null)
        return "(null)";
      if (ex is System.Runtime.InteropServices.COMException c)
        return ex.GetType().Name + " hr=0x" + c.ErrorCode.ToString("X8") + " " + ex.Message;
      return ex.GetType().Name + " " + ex.Message;
    }
  }
}
