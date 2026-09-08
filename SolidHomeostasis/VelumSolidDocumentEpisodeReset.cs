using ISIDA.Common;
using ISIDA.Psychic;
using ISIDA.Gomeostas;
using Velum.Isida;
using Velum.ReactiveCore;

namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Сброс эпизода среды при смене активного документа SW.
  /// Работает при готовом ISIDA независимо от пульсации — сброс давления и adapter state
  /// необходим для корректного состояния gate при любом сценарии.
  /// G_AD и триггеры ISIDA — только при активной пульсации.
  /// </summary>
  internal static class VelumSolidDocumentEpisodeReset
  {
    internal static void OnActiveSolidDocumentChanged()
    {
      if (!VelumIsidaHost.IsReady)
        return;

      bool noActiveDocument = VelumSolidMetricPressureReset.IsNoActiveSolidDocument();
      GomeostasSystem g = VelumIsidaHost.Context.Gomeostas;

      // Сброс давления — всегда (нужен для корректного состояния gate)
      if (noActiveDocument)
      {
        VelumSolidMetricPressureReset.OnNoActiveSolidDocument(g);
      }
      else
      {
        // Смена документа A→B: отпустить давление старого эпизода до Clear оркестратора.
        // Документные пробы в gate нельзя оставлять (даже как stale): иначе до COM-опроса
        // нового файла оркестратор снова engage по метрикам предыдущего документа —
        // «фокус внимания» симбионта и подсказка с пульта остаются чужими.
        VelumSolidMetricPressureReset.TryReleaseAllEngagedParameters(g);
        VelumSolidMetricPressureReset.StripNonHostGlobalFromPublishedGate();
      }

      VelumAdapterSleepReset.ResetVelumAdapterState(clearMetricGateSnapshot: noActiveDocument);
      VelumSolidEnvironmentBridge.InvalidateInFlightSolidProbeRefresh();

      // G_AD и триггеры ISIDA — только при активной пульсации
      VelumAdapterSleepReset.ResetEngineEpisodeState(requirePulseRunning: true);

      // Сброс сессии наблюдения при смене документа (план §2.3: сброс при смене документа).
      if (OperatorMotorObservationSession.IsInitialized)
      {
        OperatorMotorObservationSession.Instance.Reset();
        Logger.Info("OperatorMotorObservationSession: reset on document change");
      }

      // Скрыть форму-индикатор ожидания при смене документа.
      RecipeDispatcher.HideWaitingIndicator();

      Logger.Info(
          noActiveDocument
              ? "Velum document episode reset (no active document)"
              : "Velum document episode reset");
    }
  }
}
