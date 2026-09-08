using System;
using System.Threading;
using ISIDA.Common;
using Velum.Configuration;
using Velum.Isida;

namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Idle-flush Command-буфера SolidWorks: после паузы без новых <c>sw:*</c> отправляет накопленные токены
  /// в ISIDA тем же путём, что кнопка «Отправить» (только Command, без EA и речи).
  /// Принудительный flush — при переполнении (<see cref="VelumAppConfig.CommandBufferMaxTokens"/>)
  /// или истечении max age с первого токена (<see cref="VelumAppConfig.CommandBufferMaxAgeMs"/>).
  /// Таймер срабатывает на pool-потоке; вызов движка — только с UI-потока панели задач.
  /// </summary>
  internal static class VelumCommandIdleFlusher
  {
    private static readonly object Gate = new object();
    private static Timer _idleTimer;
    private static Timer _maxAgeTimer;
    private static volatile bool _flushScheduled;

    /// <summary>Подключить idle-flush (после регистрации сессии SolidWorks).</summary>
    internal static void Attach()
    {
      lock (Gate)
      {
        EnsureTimers();
      }
    }

    /// <summary>Отключить таймеры и сбросить состояние (отключение надстройки).</summary>
    internal static void Detach()
    {
      lock (Gate)
      {
        DisposeTimers();
        _flushScheduled = false;
      }
    }

    /// <summary>
    /// Сбросить idle-таймер после успешного append в буфер (безопасно с любого потока).
    /// При первом токене запускает max-age; при переполнении — принудительный flush.
    /// </summary>
    internal static void NotifyBufferAppended(bool firstToken, int tokenCount)
    {
      if (!GlobalTimer.IsPulsationRunning)
        return;

      bool forceFlush = false;
      lock (Gate)
      {
        if (_idleTimer == null)
          return;

        int idleMs = VelumAppConfig.CommandBufferIdleFlushMs;
        if (idleMs > 0)
          _idleTimer.Change(idleMs, Timeout.Infinite);

        if (firstToken)
        {
          int maxAgeMs = VelumAppConfig.CommandBufferMaxAgeMs;
          if (maxAgeMs > 0 && _maxAgeTimer != null)
            _maxAgeTimer.Change(maxAgeMs, Timeout.Infinite);
        }

        int maxTokens = VelumAppConfig.CommandBufferMaxTokens;
        if (maxTokens > 0 && tokenCount >= maxTokens)
          forceFlush = true;
      }

      if (forceFlush)
        ScheduleFlush("max_tokens");
    }

    /// <summary>Отменить ожидающий idle-flush (кнопка «Отправить», остановка пульсации).</summary>
    internal static void CancelIdleTimer()
    {
      CancelPendingFlush();
    }

    /// <summary>
    /// Отменить таймеры и флаг flush без очистки буфера (смена документа: буфер сбрасывает вызывающий).
    /// </summary>
    internal static void CancelPendingFlush()
    {
      lock (Gate)
      {
        CancelPendingTimersLocked();
        _flushScheduled = false;
      }
    }

    /// <summary>Пульсация остановлена: отменить таймер и очистить буфер.</summary>
    internal static void OnPulseStopped()
    {
      lock (Gate)
      {
        CancelPendingTimersLocked();
        _flushScheduled = false;
      }

      VelumSolidCommandBuffer.Clear();
    }

    private static void EnsureTimers()
    {
      if (_idleTimer == null)
        _idleTimer = new Timer(OnIdleTimerFired, null, Timeout.Infinite, Timeout.Infinite);

      if (_maxAgeTimer == null)
        _maxAgeTimer = new Timer(OnMaxAgeTimerFired, null, Timeout.Infinite, Timeout.Infinite);
    }

    private static void DisposeTimers()
    {
      DisposeTimer(ref _idleTimer);
      DisposeTimer(ref _maxAgeTimer);
    }

    private static void DisposeTimer(ref Timer timer)
    {
      if (timer == null)
        return;

      try
      {
        timer.Change(Timeout.Infinite, Timeout.Infinite);
        timer.Dispose();
      }
      catch
      {
      }

      timer = null;
    }

    private static void CancelPendingTimersLocked()
    {
      if (_idleTimer != null)
        _idleTimer.Change(Timeout.Infinite, Timeout.Infinite);

      if (_maxAgeTimer != null)
        _maxAgeTimer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private static void OnIdleTimerFired(object state)
    {
      ScheduleFlush("idle");
    }

    private static void OnMaxAgeTimerFired(object state)
    {
      ScheduleFlush("max_age");
    }

    private static void ScheduleFlush(string reason)
    {
      if (!GlobalTimer.IsPulsationRunning)
        return;

      bool scheduleFlush;
      lock (Gate)
      {
        scheduleFlush = !_flushScheduled;
        if (scheduleFlush)
        {
          _flushScheduled = true;
          CancelPendingTimersLocked();
        }
      }

      if (!scheduleFlush)
        return;

      VelumSolidEnvironmentBridge.RunOnTaskPaneUiThread(() => FlushCommandBufferOnUiThread(reason));
    }

    private static void FlushCommandBufferOnUiThread(string reason)
    {
      lock (Gate)
      {
        _flushScheduled = false;
      }

      try
      {
        if (!GlobalTimer.IsPulsationRunning)
          return;

        if (AppGlobalState.IsDead || AppGlobalState.HostEnvironmentDegraded)
          return;

        string commandLine = VelumSolidCommandBuffer.ConsumeSnapshot();
        if (string.IsNullOrWhiteSpace(commandLine))
          return;

        if (!VelumIsidaHost.TryInitialize(out string initErr))
        {
          Logger.Warning("Velum command buffer flush (" + reason + "): " + (initErr ?? "init_failed"));
          return;
        }

        if (!VelumAgentStimulusSender.TrySendOperatorStimulus(
                string.Empty,
                commandLine,
                Array.Empty<int>(),
                toneId: 0,
                moodId: 0,
                commandOnlyFlush: true,
                out string sendErr,
                out bool stimulusApplied))
        {
          Logger.Warning(
              "Velum command buffer flush FAIL (" + reason + "): " +
              (sendErr ?? "unknown") +
              " tokens=" + commandLine);
          return;
        }

        if (stimulusApplied)
          Logger.Info("Velum command buffer flush OK (" + reason + "): " + commandLine);
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum command buffer flush error (" + reason + "): " + ex.Message);
      }
      finally
      {
        VelumSolidEnvironmentBridge.NotifySolidCommandBufferChanged();
      }
    }
  }
}
