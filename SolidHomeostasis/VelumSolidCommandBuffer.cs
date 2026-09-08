using System;
using System.Text;
using ISIDA.Common;
using Velum.Configuration;

namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Потокобезопасный буфер командных токенов (<c>sw:…</c>, <c>pt:…</c>), наполняемый из событий SolidWorks до отправки в ISIDA.
  /// <para>
  /// <c>pt:…</c> из шагов рецептов: запись в этот буфер <b>отложена</b> (см. план рефакторинга, фаза 2 —
  /// «pt:* из рецептов»). Сейчас append только <c>sw:NNN</c> из <c>CommandOpenPreNotify</c>.
  /// </para>
  /// </summary>
  internal static class VelumSolidCommandBuffer
  {
    private static readonly object Gate = new object();
    private static readonly StringBuilder CommandLine = new StringBuilder();
    private static volatile bool _configRecordingEnabled = true;
    private static int _lastCommandId = int.MinValue;

    /// <summary>
    /// Кэш <see cref="VelumAppConfig.CommandBufferRecordingEnabled"/> (обновляется при загрузке/сохранении настроек).
    /// </summary>
    internal static void SyncRecordingFromConfig()
    {
      _configRecordingEnabled = VelumAppConfig.CommandBufferRecordingEnabled;
    }

    private static bool IsAppendAllowed()
    {
      if (!_configRecordingEnabled)
        return false;

      try
      {
        return GlobalTimer.IsPulsationRunning;
      }
      catch
      {
        return false;
      }
    }

    /// <summary>
    /// Текущее содержимое буфера (только командные токены, разделённые пробелами).
    /// </summary>
    public static string GetSnapshot()
    {
      lock (Gate)
      {
        return CommandLine.ToString().Trim();
      }
    }

    /// <summary>
    /// Очищает буфер.
    /// </summary>
    public static void Clear()
    {
      lock (Gate)
      {
        CommandLine.Clear();
        _lastCommandId = int.MinValue;
      }
    }

    /// <summary>Число токенов в буфере (под lock вызывающего).</summary>
    private static int CountTokensLocked()
    {
      if (CommandLine.Length == 0)
        return 0;

      int count = 1;
      for (int i = 0; i < CommandLine.Length; i++)
      {
        if (CommandLine[i] == ' ')
          count++;
      }

      return count;
    }

    /// <summary>
    /// Добавляет команду из <c>CommandOpenPreNotify</c>. Подряд идущие дубликаты пропускаются.
    /// </summary>
    public static void TryAppendSwCommand(int commandId)
    {
      if (commandId <= 0 || !IsAppendAllowed())
        return;

      bool appended;
      bool firstToken;
      int tokenCount;
      lock (Gate)
      {
        if (commandId == _lastCommandId)
          return;
        firstToken = CommandLine.Length == 0;
        _lastCommandId = commandId;
        string tok = "sw:" + commandId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (CommandLine.Length > 0)
          CommandLine.Append(' ');
        CommandLine.Append(tok);
        tokenCount = CountTokensLocked();
        appended = true;
      }

      if (appended)
        VelumCommandIdleFlusher.NotifyBufferAppended(firstToken, tokenCount);
    }

    /// <summary>
    /// Снимает копию и очищает буфер (после успешной отправки в движок).
    /// </summary>
    public static string ConsumeSnapshot()
    {
      lock (Gate)
      {
        string s = CommandLine.ToString().Trim();
        CommandLine.Clear();
        _lastCommandId = int.MinValue;
        return s;
      }
    }

    /// <summary>
    /// Снимает копию без очистки (предпросмотр / воспроизведение без отправки).
    /// </summary>
    public static string PeekSnapshot()
    {
      lock (Gate)
      {
        return CommandLine.ToString().Trim();
      }
    }
  }
}
