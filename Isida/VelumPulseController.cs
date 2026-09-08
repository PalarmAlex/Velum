using System;
using ISIDA.Common;

namespace Velum.Isida
{
  /// <summary>
  /// Управление глобальной пульсацией ISIDA (<see cref="GlobalTimer"/>).
  /// Индикация «агент вкл/выкл» выводится в панели задач SolidWorks (Velum Task Pane), без отдельных окон.
  /// </summary>
  public sealed class VelumPulseController : IDisposable
  {
    private bool _disposed;

    /// <summary>
    /// Событие при запуске пульсации. Используется для принудительного пересбора метрик.
    /// </summary>
    internal event Action OnPulseStarted;

    /// <summary>
    /// Событие при остановке пульсации. Используется для фиксации stamp документа.
    /// </summary>
    internal event Action OnPulseStopped;

    /// <summary>
    /// Возвращает true, если <see cref="GlobalTimer"/> сейчас выполняет пульсацию.
    /// </summary>
    public bool IsRunning => GlobalTimer.IsPulsationRunning;

    /// <summary>
    /// Запускает пульсацию.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Объект уже освобождён.</exception>
    public void Start()
    {
      ThrowIfDisposed();
      GlobalTimer.Start();
      OnPulseStarted?.Invoke();
    }

    /// <summary>
    /// Останавливает пульсацию.
    /// </summary>
    public void Stop()
    {
      VelumAdapterSleepReset.PrepareEngineSleepWhilePulseRunning();
      GlobalTimer.Stop();
      OnPulseStopped?.Invoke();
    }

    /// <summary>
    /// Останавливает пульсацию при выгрузке надстройки.
    /// </summary>
    public void Dispose()
    {
      if (_disposed)
        return;
      _disposed = true;

      try
      {
        Stop();
      }
      catch
      {
        // игнорируем повторную остановку
      }
    }

    private void ThrowIfDisposed()
    {
      if (_disposed)
        throw new ObjectDisposedException(nameof(VelumPulseController));
    }
  }
}
