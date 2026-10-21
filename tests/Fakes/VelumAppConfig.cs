using System;
using System.Collections.Generic;

namespace Velum.Configuration
{
  /// <summary>
  /// Заглушка VelumAppConfig для тестов — без чтения Settings.xml и COM-зависимостей.
  /// </summary>
  internal static class VelumAppConfig
  {
    private static Dictionary<string, object> _settings = new Dictionary<string, object>
    {
      { "SolidEnvironmentMetricDeltaEpsilon", 0.51f },
      { "SolidHomeostasisDebugLog", false },
      { "ObservationMode", false },
      { "ScannerProbeConcurrency", 64 },
      { "ScannerBatchSize", 1000 },
      { "ScannerUnavailableRootPollPulses", 30 },
      { "BomDiffScanPeriodPulses", 3 },
    };

    internal static float SolidEnvironmentMetricDeltaEpsilon
    {
      get => GetFloatSetting("SolidEnvironmentMetricDeltaEpsilon", 0.51f);
    }

    internal static bool SolidHomeostasisDebugLog
    {
      get => GetBoolSetting("SolidHomeostasisDebugLog", false);
    }

    internal static bool ObservationMode
    {
      get => GetBoolSetting("ObservationMode", false);
    }

    internal static int ScannerProbeConcurrency
    {
      get => GetIntSetting("ScannerProbeConcurrency", 64);
    }

    internal static int ScannerBatchSize
    {
      get => GetIntSetting("ScannerBatchSize", 1000);
    }

    internal static int ScannerUnavailableRootPollPulses
    {
      get => GetIntSetting("ScannerUnavailableRootPollPulses", 30);
    }

    internal static int BomDiffScanPeriodPulses
    {
      get => GetIntSetting("BomDiffScanPeriodPulses", 3);
    }

    internal static void SetSetting(string key, string value)
    {
      _settings[key] = value;
    }

    private static float GetFloatSetting(string key, float defaultValue)
    {
      if (_settings.TryGetValue(key, out object val))
        return Convert.ToSingle(val);
      return defaultValue;
    }

    private static int GetIntSetting(string key, int defaultValue)
    {
      if (_settings.TryGetValue(key, out object val))
        return Convert.ToInt32(val);
      return defaultValue;
    }

    private static bool GetBoolSetting(string key, bool defaultValue)
    {
      if (_settings.TryGetValue(key, out object val))
        return Convert.ToBoolean(val);
      return defaultValue;
    }

    internal static void Reset()
    {
      _settings.Clear();
      _settings["SolidEnvironmentMetricDeltaEpsilon"] = 0.51f;
      _settings["SolidHomeostasisDebugLog"] = false;
      _settings["ObservationMode"] = false;
      _settings["ScannerProbeConcurrency"] = 64;
      _settings["ScannerBatchSize"] = 1000;
      _settings["ScannerUnavailableRootPollPulses"] = 30;
      _settings["BomDiffScanPeriodPulses"] = 3;
    }
  }
}
