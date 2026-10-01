using System;
using System.IO;

namespace Velum.ReactiveCore.Export
{
  /// <summary>
  /// Pure-правила нормализации корневого каталога документов (настройка
  /// <c>DocumentRootPaths</c> в Settings.xml) без COM, файлов и ISIDA.
  /// Линкуется в тестовый проект Velum.ReactiveCore.Tests.
  /// </summary>
  /// <remarks>
  /// Ключ исторически допускал список через «;»; список больше не поддерживается —
  /// берётся первая непустая часть (старые файлы настроек продолжают работать).
  /// Регрессии, которые защищают правила: CASEBOOK-1 случай 5 (список через «;» ломал пути),
  /// CASEBOOK-2 случай 23 (буква диска «Z:\» ошибочно считалась UNC-путём — E45).
  /// </remarks>
  internal static class VelumDocumentRootPathRules
  {
    /// <summary>UNC-префикс (два разделителя каталога). Строится из разделителя ОС — см. E45.</summary>
    private static readonly string UncPrefix = new string(Path.DirectorySeparatorChar, 2);

    /// <summary>Хвостовой разделитель каталога как строка.</summary>
    private static readonly string TrailingSeparator = Path.DirectorySeparatorChar.ToString();

    /// <summary>Первая непустая часть значения (обратная совместимость со списком через «;»).</summary>
    /// <param name="raw">Сырое значение настройки.</param>
    /// <returns>Первая непустая часть без обрамляющих пробелов; пусто — не задано.</returns>
    internal static string FirstPart(string raw)
    {
      string value = (raw ?? string.Empty).Trim();
      if (value.Length == 0)
        return string.Empty;

      foreach (string part in value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
      {
        string trimmed = part.Trim();
        if (trimmed.Length > 0)
          return trimmed;
      }

      return string.Empty;
    }

    /// <summary>
    /// Нормализует корневой путь: добавляет хвостовой разделитель UNC-пути
    /// («\\server\dfs» → «\\server\dfs\») и для буквы диска («Z:» → «Z:\»).
    /// Буква диска UNC-путём не считается — проверяется префикс из двух разделителей.
    /// </summary>
    /// <param name="value">Путь (обычно уже первая часть списка).</param>
    /// <returns>Нормализованный путь; пусто — не задано.</returns>
    internal static string Normalize(string value)
    {
      string trimmed = (value ?? string.Empty).Trim();
      if (trimmed.Length == 0)
        return string.Empty;

      if (trimmed.StartsWith(UncPrefix, StringComparison.Ordinal)
          && !trimmed.EndsWith(TrailingSeparator, StringComparison.Ordinal))
      {
        trimmed += TrailingSeparator;
      }
      else if (trimmed.Length == 2 && trimmed[1] == ':')
      {
        trimmed += TrailingSeparator;
      }

      return trimmed;
    }

    /// <summary>Сырое значение настройки → нормализованный корневой путь (FirstPart + Normalize).</summary>
    /// <param name="raw">Сырое значение настройки.</param>
    /// <returns>Нормализованный корневой путь; пусто — не задано.</returns>
    internal static string Resolve(string raw)
    {
      return Normalize(FirstPart(raw));
    }

    /// <summary>true, если путь начинается с UNC-префикса (двух разделителей каталога).</summary>
    /// <param name="path">Проверяемый путь.</param>
    /// <returns>true для UNC-пути.</returns>
    internal static bool IsUnc(string path)
    {
      string value = path ?? string.Empty;
      return value.StartsWith(UncPrefix, StringComparison.Ordinal);
    }
  }
}
