using System;
using System.Collections.Generic;
using ISIDA.Common;
using Velum.UI.Logs;

namespace Velum.UI
{
  /// <summary>
  /// Подписка на канал сокращённого (семантического) лога ISIDA
  /// <see cref="ShortLogChannel"/>. Заменяет файловый опрос AgentLogs.csv:
  /// токены приходят напрямую от движка в событиях OnToken / OnTokenUpdated /
  /// OnSessionStarted / OnSessionCompleted. OnTokenUpdated — повтор последнего блока
  /// (схлопнут идентичный пульс, вырос RepeatCount): перерисовываем верхний блок, а не добавляем новый.
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
      ShortLogChannel.OnTokenUpdated += OnShortLogTokenUpdated;
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
      ShortLogChannel.OnTokenUpdated -= OnShortLogTokenUpdated;
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
    /// Пришёл повтор последнего блока (движок схлопнул идентичный пульс и поднял
    /// <c>RepeatCount</c>). Новый токен не создаётся — перерисовываем верхний (самый свежий)
    /// блок ленты, чтобы в нём появился счётчик «×N», вместо добавления нового блока.
    /// </summary>
    private void OnShortLogTokenUpdated(ShortLogToken token)
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

          // Та же нормализация переносов, что и для новых токенов.
          text = VelumAgentLogCondensedRules.NormalizeNewLines(text);

          // Верхний блок ленты — это тот же пульс, ставший повтором: заменяем его целиком,
          // нижние блоки не трогаем.
          _txtAgentOutput.Text = VelumAgentLogCondensedRules.ReplaceTopBlock(
              _txtAgentOutput.Text, text, Environment.NewLine);

          _txtAgentOutput.SelectionStart = 0;
          _txtAgentOutput.ScrollToCaret();
        }));
      }
      catch
      {
        // Лента — некритичная диагностика.
      }
    }

    /// <summary>
    /// Сессия сокращённого лога завершена (пульсация остановлена).
    /// Фиксируем конец сессии в выводе и сохраняем сессию в событийный лог
    /// (<c>AgentLogs_Events.csv</c>, каталог из настроек проекта).
    /// </summary>
    private void OnShortLogSessionCompleted(int sessionId, ShortLogToken[] tokens)
    {
      // Запись событийного лога — на потоке автора (пульса), до работы с UI: она не зависит
      // от состояния панели, а AppendSession сам гасит исключения (сбой диска не роняет пульс).
      VelumEventLogFileSessions.AppendSession(tokens);

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
