using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ISIDA.Common;
using ISIDA.Sensors;
using Velum.Configuration;
using Velum.SolidHomeostasis;

namespace Velum.Isida
{
  /// <summary>Канал первичников: командный (атомарные токены) или вербальный (символы).</summary>
  internal enum VelumSensorPrimariesChannel
  {
    Command,
    Verbal
  }

  /// <summary>
  /// Буферные файлы новых первичников (<c>CommandPrimariesBuffer.tmp</c>, <c>VerbalPrimariesBuffer.tmp</c>)
  /// и перенос подтверждённых записей в справочники первичников.
  /// </summary>
  internal static class VelumSensorPrimariesBuffer
  {
    public const string CommandBufferFileName = "CommandPrimariesBuffer";
    public const string VerbalBufferFileName = "VerbalPrimariesBuffer";

    // Синхронизирует операции чтения-модификации-записи над буферными файлами и
    // справочниками первичников: пульсовый цикл ISIDA и UI-потоки могут вызывать
    // методы одновременно, а параллельные AppendAllText/ReadAllLines приводят к
    // гонкам и IOException.
    private static readonly object AppendSync = new object();

    public static string ResolveSensorsFolder()
    {
      return IsidaDataPaths.ResolveSensorsFolder(VelumAppConfig.DataFolderPath);
    }

    public static string GetBufferFilePath(VelumSensorPrimariesChannel channel)
    {
      string name = channel == VelumSensorPrimariesChannel.Command
          ? CommandBufferFileName
          : VerbalBufferFileName;
      return Path.Combine(ResolveSensorsFolder(), name + ".tmp");
    }

    public static string GetPrimariesFilePath(VelumSensorPrimariesChannel channel)
    {
      string name = channel == VelumSensorPrimariesChannel.Command
          ? SensorySystem.DefaultCommandPrimariesFileName
          : SensorySystem.DefaultVerbalPrimariesFileName;
      return Path.Combine(ResolveSensorsFolder(), name + ".tmp");
    }

    public static IReadOnlyList<string> ReadBufferEntries(VelumSensorPrimariesChannel channel)
    {
      lock (AppendSync)
      {
        string path = GetBufferFilePath(channel);
        if (!File.Exists(path))
          return Array.Empty<string>();

        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string raw in File.ReadAllLines(path))
        {
          string line = raw?.Trim() ?? string.Empty;
          if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
            continue;

          if (channel == VelumSensorPrimariesChannel.Verbal && line.Length != 1)
            continue;

          if (seen.Add(line))
            result.Add(line);
        }

        return result;
      }
    }

    public static void QueueCommandTokensIfMissing(IEnumerable<string> tokens)
    {
      if (tokens == null)
        return;

      var pending = new List<string>();
      foreach (string token in tokens)
      {
        if (string.IsNullOrWhiteSpace(token))
          continue;
        if (!VelumOperatorStimulusCodec.IsCommandToken(token))
          continue;
        if (IsCommandPrimaryKnown(token.Trim()))
          continue;
        pending.Add(token.Trim());
      }

      AppendUniqueToBuffer(VelumSensorPrimariesChannel.Command, pending);
    }

    public static void QueueVerbalSymbolsIfMissing(string text)
    {
      if (string.IsNullOrWhiteSpace(text))
        return;

      var pending = new List<string>();
      var seen = new HashSet<char>();
      foreach (char ch in text)
      {
        if (!char.IsLetter(ch))
          continue;
        if (!seen.Add(ch))
          continue;
        if (IsVerbalPrimaryKnown(ch))
          continue;
        pending.Add(ch.ToString());
      }

      AppendUniqueToBuffer(VelumSensorPrimariesChannel.Verbal, pending);
    }

    private static void AppendUniqueToBuffer(VelumSensorPrimariesChannel channel, IReadOnlyList<string> keys)
    {
      if (keys == null || keys.Count == 0)
        return;

      lock (AppendSync)
      {
        var existing = new HashSet<string>(ReadBufferEntries(channel), StringComparer.Ordinal);
        var toAppend = keys.Where(k => !existing.Contains(k)).ToList();
        if (toAppend.Count == 0)
          return;

        string path = GetBufferFilePath(channel);
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
          Directory.CreateDirectory(directory);

        if (!File.Exists(path))
          WriteBufferHeader(path, channel);

        var sb = new StringBuilder();
        foreach (string key in toAppend)
          sb.AppendLine(key);
        File.AppendAllText(path, sb.ToString());
      }
    }

    private static void WriteBufferHeader(string path, VelumSensorPrimariesChannel channel)
    {
      if (channel == VelumSensorPrimariesChannel.Command)
      {
        File.WriteAllLines(path, new[]
        {
          "# Новые первичные токены командного канала (ожидают подтверждения)",
          "# Формат: одна строка — один токен (sw:N, pt:…)"
        });
        return;
      }

      File.WriteAllLines(path, new[]
      {
        "# Новые первичные символы вербального канала (ожидают подтверждения)",
        "# Формат: одна строка — один символ"
      });
    }

    public static bool TryCommit(
        VelumSensorPrimariesChannel channel,
        out int addedCount,
        out string errorMessage)
    {
      addedCount = 0;
      errorMessage = null;

      lock (AppendSync)
      {
        IReadOnlyList<string> pending = ReadBufferEntries(channel);
        if (pending.Count == 0)
        {
          errorMessage = "Буфер пуст — нет новых первичников для добавления.";
          return false;
        }

        string primariesPath = GetPrimariesFilePath(channel);
        if (!File.Exists(primariesPath))
        {
          errorMessage = "Файл первичников не найден: " + primariesPath;
          return false;
        }

        var existingKeys = LoadPrimariesKeys(channel, primariesPath);
        int maxId = LoadPrimariesMaxId(primariesPath);
        var toAdd = pending.Where(k => !existingKeys.Contains(k)).ToList();
        if (toAdd.Count == 0)
        {
          ClearBuffer(channel);
          ReloadChannelIfReady(channel);
          return true;
        }

        var sb = new StringBuilder();
        foreach (string key in toAdd)
        {
          maxId++;
          sb.AppendLine(FormatPrimariesLine(channel, key, maxId));
          addedCount++;
        }

        try
        {
          File.AppendAllText(primariesPath, sb.ToString());
          ClearBuffer(channel);
          ReloadChannelIfReady(channel);
          return true;
        }
        catch (Exception ex)
        {
          errorMessage = ex.Message;
          return false;
        }
      }
    }

    public static void ClearBuffer(VelumSensorPrimariesChannel channel)
    {
      lock (AppendSync)
      {
        string path = GetBufferFilePath(channel);
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
          Directory.CreateDirectory(directory);

        WriteBufferHeader(path, channel);
      }
    }

    /// <summary>
    /// Полностью очищает буферный файл (только заголовок).
    /// </summary>
    public static bool TryClearBuffer(VelumSensorPrimariesChannel channel, out string errorMessage)
    {
      errorMessage = null;
      try
      {
        ClearBuffer(channel);
        return true;
      }
      catch (Exception ex)
      {
        errorMessage = ex.Message;
        return false;
      }
    }

    /// <summary>
    /// Удаляет выбранные записи из буферного файла.
    /// </summary>
    public static bool TryRemoveBufferEntries(
        VelumSensorPrimariesChannel channel,
        IEnumerable<string> keys,
        out int removedCount,
        out string errorMessage)
    {
      removedCount = 0;
      errorMessage = null;

      if (keys == null)
      {
        errorMessage = "Не выбраны записи для удаления.";
        return false;
      }

      var toRemove = new HashSet<string>(StringComparer.Ordinal);
      foreach (string key in keys)
      {
        if (string.IsNullOrWhiteSpace(key))
          continue;
        toRemove.Add(key.Trim());
      }

      if (toRemove.Count == 0)
      {
        errorMessage = "Не выбраны записи для удаления.";
        return false;
      }

      lock (AppendSync)
      {
        IReadOnlyList<string> current = ReadBufferEntries(channel);
        if (current.Count == 0)
        {
          errorMessage = "Буфер пуст.";
          return false;
        }

        var remaining = new List<string>();
        foreach (string entry in current)
        {
          if (toRemove.Contains(entry))
            removedCount++;
          else
            remaining.Add(entry);
        }

        if (removedCount == 0)
        {
          errorMessage = "Выбранные записи не найдены в буфере.";
          return false;
        }

        try
        {
          WriteBufferEntries(channel, remaining);
          return true;
        }
        catch (Exception ex)
        {
          errorMessage = ex.Message;
          return false;
        }
      }
    }

    private static void WriteBufferEntries(VelumSensorPrimariesChannel channel, IReadOnlyList<string> entries)
    {
      lock (AppendSync)
      {
        string path = GetBufferFilePath(channel);
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
          Directory.CreateDirectory(directory);

        WriteBufferHeader(path, channel);
        if (entries == null || entries.Count == 0)
          return;

        var sb = new StringBuilder();
        foreach (string key in entries)
        {
          if (string.IsNullOrWhiteSpace(key))
            continue;
          sb.AppendLine(key.Trim());
        }

        if (sb.Length > 0)
          File.AppendAllText(path, sb.ToString());
      }
    }

    private static string FormatPrimariesLine(VelumSensorPrimariesChannel channel, string key, int id)
    {
      return key + "|#|" + id.ToString(CultureInfo.InvariantCulture);
    }

    private static void ReloadChannelIfReady(VelumSensorPrimariesChannel channel)
    {
      if (!SensorySystem.IsInitialized)
        return;

      try
      {
        if (channel == VelumSensorPrimariesChannel.Command)
          SensorySystem.Instance.CommandChannel?.ReloadPrimarySensors();
        else
          SensorySystem.Instance.VerbalChannel?.ReloadPrimarySensors();
      }
      catch
      {
      }
    }

    private static bool IsCommandPrimaryKnown(string token)
    {
      if (SensorySystem.IsInitialized)
        return SensorySystem.Instance.CommandChannel?.HasPrimaryToken(token) ?? false;

      return LoadPrimariesKeys(VelumSensorPrimariesChannel.Command, GetPrimariesFilePath(VelumSensorPrimariesChannel.Command))
          .Contains(token);
    }

    private static bool IsVerbalPrimaryKnown(char symbol)
    {
      if (SensorySystem.IsInitialized)
        return SensorySystem.Instance.VerbalChannel?.GetPrimarySensorId(symbol) != 0;

      return LoadPrimariesKeys(
              VelumSensorPrimariesChannel.Verbal,
              GetPrimariesFilePath(VelumSensorPrimariesChannel.Verbal))
          .Contains(symbol.ToString());
    }

    private static HashSet<string> LoadPrimariesKeys(VelumSensorPrimariesChannel channel, string path)
    {
      var keys = new HashSet<string>(StringComparer.Ordinal);
      if (!File.Exists(path))
        return keys;

      foreach (string raw in File.ReadAllLines(path))
      {
        string line = raw?.Trim() ?? string.Empty;
        if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
          continue;

        string[] parts = line.Split(new[] { "|#|" }, StringSplitOptions.None);
        if (parts.Length != 2)
          continue;

        string key = parts[0].Trim();
        if (channel == VelumSensorPrimariesChannel.Verbal)
        {
          if (key.Length == 1)
            keys.Add(key);
          continue;
        }

        if (!string.IsNullOrEmpty(key))
          keys.Add(key);
      }

      return keys;
    }

    private static int LoadPrimariesMaxId(string path)
    {
      int maxId = 0;
      if (!File.Exists(path))
        return maxId;

      foreach (string raw in File.ReadAllLines(path))
      {
        string line = raw?.Trim() ?? string.Empty;
        if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
          continue;

        string[] parts = line.Split(new[] { "|#|" }, StringSplitOptions.None);
        if (parts.Length != 2)
          continue;

        if (int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) && id > maxId)
          maxId = id;
      }

      return maxId;
    }
  }
}
