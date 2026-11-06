using System;
using System.Collections.Generic;
using ISIDA.Common;
using Velum.UI.Logs;

namespace Velum.UI
{
  /// <summary>
  /// Подписка на канал сокращённого (семантического) лога ISIDA
  /// <see cref="ShortLogChannel"/>. Заменяет файловый опрос AgentLogs.csv:
  /// токены приходят напрямую от движка в событиях OnToken / OnSessionStarted / OnSessionCompleted.
  /// </summary>
  public sealed partial class VelumAgentTaskPane
  {
    /// <summary>Флаг, что подписка на ShortLogChannel уже выполнена (идемпотентность).</summary>
    private bool _shortLogHooked;

    /// <summary>
    /// Подписка на события <see cref="ShortLogChannel"/>.
    /// Вызывается из <see cref="HookPulseEvents"/>.
    /// </summary>
    private void HookShortLogChannel()
    {
      if (_shortLogHooked)
        return;

      ShortLogChannel.OnSessionStarted += OnShortLogSessionStarted;
      ShortLogChannel.OnToken += OnShortLogToken;
      ShortLogChannel.OnSessionCompleted += OnShortLogSessionCompleted;
      _shortLogHooked = true;
    }

    /// <summary>
    /// Отписка от событий <see cref="ShortLogChannel"/>.
    /// Вызывается из <see cref="UnhookPulseEvents"/> и <see cref="OnHandleDestroyed"/>.
    /// </summary>
    private void UnhookShortLogChannel()
    {
      if (!_shortLogHooked)
        return;

      ShortLogChannel.OnSessionStarted -= OnShortLogSessionStarted;
      ShortLogChannel.OnToken -= OnShortLogToken;
      ShortLogChannel.OnSessionCompleted -= OnShortLogSessionCompleted;
      _shortLogHooked = false;
    }

    /// <summary>
    /// Сессия сокращённого лога началась (включена пульсация).
    /// Очищаем окно вывода, чтобы начать с чистого листа.
    /// </summary>
    private void OnShortLogSessionStarted(int sessionId)
    {
      if (IsDisposed)
        return;

      try
      {
        if (!IsHandleCreated)
          return;

        BeginInvoke(new Action(() =>
        {
          if (_txtAgentOutput != null && !IsDisposed)
            _txtAgentOutput.Clear();
        }));
      }
      catch
      {
        // Сбой очистки не критичен — токены всё равно пойдут.
      }
    }

    /// <summary>
    /// Пришёл токен сокращённого лога — выводим в окно ответа агента.
    /// Токен содержит готовое представление через ToString().
    /// </summary>
    private void OnShortLogToken(ShortLogToken token)
    {
      if (IsDisposed)
        return;

      try
      {
        if (!IsHandleCreated)
          return;

        BeginInvoke(new Action(() =>
        {
          if (_txtAgentOutput == null || IsDisposed)
            return;

          string text = token?.ToString() ?? string.Empty;
          if (string.IsNullOrEmpty(text))
            return;

          // Движок присылает текст с экранированными «\n» (две буквы), а Multiline TextBox
          //recognизирует только реальные переносы — нормализуем через чистое правило.
          text = VelumAgentLogCondensedRules.NormalizeNewLines(text);

          // Лента — «свежее сверху»: новый блок (токен = весь пульс, см. группировку ISIDA
          // FlushPulse) ставится перед старым текстом через пустую строку (двойной перенос),
          // чтобы блоки пульсов визуально разделялись. Граница-разделитель совпадает с той,
          // по которой TrimToHead режет ленту, поэтому обрезка идёт по границе блока.
          _txtAgentOutput.Text = VelumAgentLogCondensedRules.PrependBlocks(
              _txtAgentOutput.Text, new[] { text }, Environment.NewLine);

          _txtAgentOutput.SelectionStart = 0;
          _txtAgentOutput.ScrollToCaret();

          // Мягко ограничиваем длину окна вывода.
          TrimCondensedOutputIfNeeded();
        }));
      }
      catch
      {
        // Лента — некритичная диагностика.
      }
    }

    /// <summary>
    /// Сессия сокращённого лога завершена (пульсация остановлена).
    /// Фиксируем конец сессии в выводе (опционально: можно сохранить в файл).
    /// </summary>
    private void OnShortLogSessionCompleted(int sessionId, ShortLogToken[] tokens)
    {
      if (IsDisposed)
        return;

      try
      {
        if (!IsHandleCreated)
          return;

        BeginInvoke(new Action(() =>
        {
          if (_txtAgentOutput == null || IsDisposed)
            return;

          // Отметка конца сессии — вставляем разделитель через пустую строку (двойной перенос),
          // как и блоки токенов, чтобы он не прилипал к последнему пульсу.
          string separator = "── Сессия " + sessionId + " завершена ──";
          _txtAgentOutput.Text = VelumAgentLogCondensedRules.PrependBlocks(
              _txtAgentOutput.Text, new[] { separator }, Environment.NewLine);

          _txtAgentOutput.SelectionStart = 0;
          _txtAgentOutput.ScrollToCaret();
        }));
      }
      catch
      {
        // Некритично.
      }
    }

    /// <summary>
    /// Мягко ограничивает длину окна вывода, чтобы длительная работа не раздувала TextBox
    /// (текстовый буфер WinForms не бесконечен). Обрезает старые блоки с конца, оставляя
    /// свежие в начале.
    /// </summary>
    private void TrimCondensedOutputIfNeeded()
    {
      const int maxChars = 200000;
      if (_txtAgentOutput == null || _txtAgentOutput.TextLength <= maxChars)
        return;

      try
      {
        _txtAgentOutput.Text = VelumAgentLogCondensedRules.TrimToHead(
            _txtAgentOutput.Text, maxChars, Environment.NewLine);
      }
      catch
      {
      }
    }
  }
}
