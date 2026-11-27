using System;
using System.IO;

namespace Velum.ReactiveCore.DocumentTemplates
{
  /// <summary>
  /// Pure-правила идентичности документа SolidWorks: нормализация пути и ключ
  /// <c>doc.key</c>. Без COM, файлов и ISIDA — линкуется в тесты.
  /// </summary>
  /// <remarks>
  /// <c>doc.key</c> живёт только в Velum и никогда не уходит в движок как «документ»:
  /// в ISIDA он попадает как непрозрачный <c>context.key</c>.
  /// Значения параметров в ключ не входят — иначе правка размера создавала бы новый документ.
  /// </remarks>
  internal static class VelumDocumentIdentityRules
  {
    /// <summary>Тип документа: деталь.</summary>
    internal const string DocTypePart = "part";

    /// <summary>Тип документа: сборка.</summary>
    internal const string DocTypeAssembly = "asm";

    /// <summary>Тип документа: чертёж.</summary>
    internal const string DocTypeDrawing = "drw";

    /// <summary>Префикс ключа несохранённого документа.</summary>
    internal const string UnsavedPrefix = "unsaved:";

    /// <summary>
    /// Определяет тип документа по расширению файла (<c>.sldprt/.sldasm/.slddrw</c>).
    /// </summary>
    /// <param name="pathOrExtension">Путь или расширение файла.</param>
    /// <returns><c>part</c>, <c>asm</c>, <c>drw</c> либо пустая строка.</returns>
    internal static string ResolveDocType(string pathOrExtension)
    {
      string value = (pathOrExtension ?? string.Empty).Trim();
      if (value.Length == 0)
        return string.Empty;

      string ext = value.StartsWith(".", StringComparison.Ordinal)
          ? value
          : Path.GetExtension(value);

      if (string.Equals(ext, ".sldprt", StringComparison.OrdinalIgnoreCase))
        return DocTypePart;
      if (string.Equals(ext, ".sldasm", StringComparison.OrdinalIgnoreCase))
        return DocTypeAssembly;
      if (string.Equals(ext, ".slddrw", StringComparison.OrdinalIgnoreCase))
        return DocTypeDrawing;

      return string.Empty;
    }

    /// <summary>
    /// Нормализует путь для сравнения: <see cref="Path.GetFullPath"/> без
    /// префикса <c>\\?\</c>. Возвращает пустую строку, если путь пуст или некорректен.
    /// </summary>
    /// <param name="path">Путь к файлу документа.</param>
    /// <returns>Нормализованный абсолютный путь либо пустая строка.</returns>
    internal static string NormalizePath(string path)
    {
      string value = (path ?? string.Empty).Trim();
      if (value.Length == 0)
        return string.Empty;

      try
      {
        string full = Path.GetFullPath(value);
        const string longPrefix = @"\\?\";
        if (full.StartsWith(longPrefix, StringComparison.Ordinal))
          full = full.Substring(longPrefix.Length);
        return full;
      }
      catch
      {
        return string.Empty;
      }
    }

    /// <summary>
    /// Строит <c>doc.key</c> для сохранённого документа:
    /// <c>&lt;normalizedPath&gt;|&lt;docType&gt;|&lt;configName&gt;</c>.
    /// </summary>
    /// <param name="path">Путь к файлу.</param>
    /// <param name="docType">Тип документа (<c>part</c>/<c>asm</c>/<c>drw</c>).</param>
    /// <param name="configName">Имя активной конфигурации (может быть пустым).</param>
    /// <returns>Ключ либо пустая строка, если путь/тип некорректны.</returns>
    internal static string BuildDocKey(string path, string docType, string configName)
    {
      string normalized = NormalizePath(path);
      string type = (docType ?? string.Empty).Trim().ToLowerInvariant();
      if (normalized.Length == 0 || type.Length == 0)
        return string.Empty;

      string config = (configName ?? string.Empty).Trim();
      return normalized + "|" + type + "|" + config;
    }

    /// <summary>
    /// Строит ключ несохранённого документа: <c>unsaved:&lt;sessionGuid&gt;:&lt;title&gt;</c>.
    /// Такой ключ никогда не должен попадать в постоянную память.
    /// </summary>
    /// <param name="sessionGuid">Идентификатор сессии SolidWorks.</param>
    /// <param name="title">Заголовок документа.</param>
    /// <returns>Ключ либо пустая строка при пустых аргументах.</returns>
    internal static string BuildUnsavedDocKey(string sessionGuid, string title)
    {
      string guid = (sessionGuid ?? string.Empty).Trim();
      string name = (title ?? string.Empty).Trim();
      if (guid.Length == 0 || name.Length == 0)
        return string.Empty;

      return UnsavedPrefix + guid + ":" + name;
    }

    /// <summary>true, если ключ — ключ несохранённого документа.</summary>
    /// <param name="docKey">Проверяемый ключ.</param>
    /// <returns>true для ключей с префиксом <c>unsaved:</c>.</returns>
    internal static bool IsUnsavedDocKey(string docKey)
    {
      return (docKey ?? string.Empty).Trim()
          .StartsWith(UnsavedPrefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Сравнивает два ключа без учёта регистра (OrdinalIgnoreCase) —
    /// пути на Windows регистронезависимы.
    /// </summary>
    /// <param name="a">Первый ключ.</param>
    /// <param name="b">Второй ключ.</param>
    /// <returns>true, если ключи эквивалентны.</returns>
    internal static bool DocKeysEqual(string a, string b)
    {
      return string.Equals(
          (a ?? string.Empty).Trim(),
          (b ?? string.Empty).Trim(),
          StringComparison.OrdinalIgnoreCase);
    }
  }
}
