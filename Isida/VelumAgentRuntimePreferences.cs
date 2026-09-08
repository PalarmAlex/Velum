using ISIDA.Common;
using Velum.Configuration;
using Velum.SolidHomeostasis;

namespace Velum.Isida
{
  /// <summary>
  /// Синхронизация флагов среды выполнения ISIDA с <see cref="VelumAppConfig"/> (после загрузки движка или сохранения XML).
  /// Авторитарная запись сенсорных каналов задаётся только флажком на пульте агента, не из этого метода.
  /// </summary>
  internal static class VelumAgentRuntimePreferences
  {
    internal static void ApplyFromVelumConfig()
    {
      AppGlobalState.ObservationMode = VelumAppConfig.ObservationMode;
      VelumSolidCommandBuffer.SyncRecordingFromConfig();
    }
  }
}
