using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Velum.UI.Logs
{
  /// <summary>
  /// Инкрементальный «хвостовой» читатель CSV-лога: при каждом обращении отдаёт только
  /// строки, дописанные в файл с прошлого обращения. Хранит байтовое смещение и байтовый
  /// остаток последней незавершённой строки (лог пишется ISIDA в реальном времени и хвост
  /// может обрываться на середине строки). Открытие — с общим доступом
  /// <see cref="FileShare.ReadWrite"/>|<see cref="FileShare.Delete"/>, как в
  /// <see cref="VelumCsvLogSessionReader"/>, чтобы не конфликтовать с писателем.
  /// Синхронный (чистый File IO); вызывается из фонового потока (<c>Task.Run</c>), не из UI.
  /// </summary>
  internal sealed class VelumAgentLogTailReader
  {
    private static readonly byte[] Utf8Bom = { 0xEF, 0xBB, 0xBF };

    private long _offset;
    private readonly List<byte> _pending = new List<byte>();

    /// <summary>Текущее байтовое смещение (для диагностики/тестов).</summary>
    internal long Offset
    {
      get { return _offset; }
    }

    /// <summary>
    /// Сбрасывает состояние: следующее чтение начнёт с указанной позиции (0 — с начала файла).
    /// Используется при старте опроса, чтобы не печатать историческое содержимое лога.
    /// </summary>
    /// <param name="startFromEnd">
    /// <c>true</c> — запомнить текущий конец файла как точку отсчёта (печатать только новые
    /// строки, появившиеся после старта); <c>false</c> — начать с нуля.
    /// </param>
    /// <param name="path">Путь к файлу (нужен для определения длины при <paramref name="startFromEnd"/>).</param>
    internal void Reset(bool startFromEnd, string path)
    {
      _pending.Clear();
      _offset = 0;
      if (!startFromEnd)
        return;

      try
      {
        using (var fs = OpenShared(path))
          _offset = fs.Length;
      }
      catch
      {
        _offset = 0;
      }
    }

    /// <summary>
    /// Читает строки, дописанные с прошлого обращения. Возвращает только полностью
    /// завершённые строки (заканчивающиеся переводом строки); незавершённый хвост
    /// сохраняется и будет дописан к следующему обращению.
    /// </summary>
    /// <param name="path">Путь к CSV-логу.</param>
    /// <returns>Список новых завершённых строк (без переводов строк, без BOM).</returns>
    internal IReadOnlyList<string> ReadNewLines(string path)
    {
      var result = new List<string>();
      if (string.IsNullOrEmpty(path))
        return result;

      try
      {
        using (var fs = OpenShared(path))
        {
          long len = fs.Length;
          // Файл урезан/перезаписан (ротация, новая сессия с пустого файла) — начинаем заново.
          if (len < _offset)
          {
            _offset = 0;
            _pending.Clear();
          }

          long toRead = len - _offset;
          if (toRead <= 0)
            return result;

          // Защита от гигантского разового прироста: читаем не больше окна за проход.
          int chunk = toRead > VelumLogTailMaxReadPerPass
              ? VelumLogTailMaxReadPerPass
              : (int)toRead;

          fs.Seek(_offset, SeekOrigin.Begin);
          byte[] buf = new byte[chunk];
          int read = fs.Read(buf, 0, chunk);
          if (read <= 0)
            return result;

          _offset += read;
          for (int i = 0; i < read; i++)
            _pending.Add(buf[i]);
        }
      }
      catch
      {
        return result;
      }

      // Режем накопленные байты по 0x0A (для UTF-8 безопасно: 0x0A не встречается внутри
      // многобайтовых последовательностей). Последний фрагмент без перевода — оставляем в буфере.
      int lineStart = 0;
      for (int i = 0; i < _pending.Count; i++)
      {
        if (_pending[i] != (byte)'\n')
          continue;

        int len = i - lineStart;
        byte[] lineBytes = _pending.GetRange(lineStart, len).ToArray();
        string s = DecodeLine(lineBytes);
        if (!string.IsNullOrEmpty(s))
          result.Add(s);
        lineStart = i + 1;
      }

      if (lineStart > 0)
        _pending.RemoveRange(0, lineStart);

      return result;
    }

    /// <summary>Максимум байт за один проход чтения хвоста (защита от разового гигантского прироста).</summary>
    private const int VelumLogTailMaxReadPerPass = 512 * 1024;

    private static FileStream OpenShared(string path)
    {
      return new FileStream(
          path,
          FileMode.Open,
          FileAccess.Read,
          FileShare.ReadWrite | FileShare.Delete,
          4096,
          FileOptions.SequentialScan);
    }

    private static string DecodeLine(byte[] lineBytes)
    {
      if (lineBytes == null || lineBytes.Length == 0)
        return string.Empty;

      // Снять \r в конце (Windows CRLF).
      int len = lineBytes.Length;
      if (lineBytes[len - 1] == (byte)'\r')
        len--;

      if (len <= 0)
        return string.Empty;

      int start = 0;
      if (len >= 3 && lineBytes[0] == Utf8Bom[0] && lineBytes[1] == Utf8Bom[1] && lineBytes[2] == Utf8Bom[2])
        start = 3;

      if (len - start <= 0)
        return string.Empty;

      return Encoding.UTF8.GetString(lineBytes, start, len - start);
    }
  }
}