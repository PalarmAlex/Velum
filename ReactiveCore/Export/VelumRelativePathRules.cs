using System;
using System.IO;

namespace Velum.ReactiveCore.Export
{
  /// <summary>
  /// Pure-правила конверсии путей для хранения относительных ссылок: срез корневого
  /// каталога (<see cref="ToStored"/>) и достройка относительного значения по корню
  /// (<see cref="ToFull"/>). Корень передаётся параметром — класса нет зависимостей
  /// от COM, файлов, ISIDA и конфигурации. Линкуется в тестовый проект
  /// Velum.ReactiveCore.Tests.
  /// </summary>
  /// <remarks>
  /// Регрессии, которые защищают правила: CASEBOOK-1 случай 5 (два префикса через «;»
  /// порождали дубль сегмента пути), случай 6 итерация 4 (тяжёлый <c>Path.GetFullPath</c>
  /// в горячем цикле), CASEBOOK-2 случай 23 (одиночный «\» в литерале UNC-префикса).
  /// </remarks>
  internal static class VelumRelativePathRules
  {
    /// <summary>
    /// Готовит значение к записи: срезает корневой каталог (регистронезависимо) →
    /// относительный путь. Неизвестный/абсолютный без совпадения, уже относительный,
    /// пустой — без изменений. Пустой корень — значение проходит без изменений.
    /// </summary>
    /// <param name="fullPath">Полный или уже хранимый путь.</param>
    /// <param name="root">Корневой каталог (нормализованный).</param>
    /// <returns>Хранимое значение.</returns>
    internal static string ToStored(string fullPath, string root)
    {
      string value = (fullPath ?? string.Empty).Trim();
      if (value.Length == 0)
        return string.Empty;

      string prefix = (root ?? string.Empty).Trim();
      if (prefix.Length == 0)
        return value;

      if (TryStripPrefix(value, prefix, out string relative))
        return relative;

      return value;
    }

    /// <summary>
    /// Восстанавливает полный путь из хранимого значения: относительный достраивается
    /// по корневому каталогу; абсолютный и пустой — без изменений. Пустой корень —
    /// относительное значение проходит без изменений.
    /// </summary>
    /// <param name="storedPath">Хранимое (возможно, относительное) значение.</param>
    /// <param name="root">Корневой каталог (нормализованный).</param>
    /// <returns>Полный путь либо исходное значение.</returns>
    internal static string ToFull(string storedPath, string root)
    {
      string value = (storedPath ?? string.Empty).Trim();
      if (value.Length == 0)
        return string.Empty;

      if (Path.IsPathRooted(value))
        return value;

      string prefix = (root ?? string.Empty).Trim();
      if (prefix.Length == 0)
        return value;

      try
      {
        return Path.Combine(prefix, value);
      }
      catch
      {
        return value;
      }
    }

    /// <summary>
    /// Ключ сравнения путей: значение разворачивается до полного (<see cref="ToFull"/>)
    /// и нормализуется через <c>Path.GetFullPath</c>. Для относительных хранимых
    /// значений нужен именно <see cref="ToFull"/> — иначе путь развернулся бы от
    /// текущего каталога процесса.
    /// </summary>
    /// <param name="path">Путь (хранимый или полный).</param>
    /// <param name="root">Корневой каталог (нормализованный).</param>
    /// <returns>Нормализованный полный путь.</returns>
    internal static string NormalizeForCompare(string path, string root)
    {
      string value = (path ?? string.Empty).Trim();
      if (value.Length == 0)
        return string.Empty;

      string full = ToFull(value, root);
      try
      {
        return Path.GetFullPath(full);
      }
      catch
      {
        return full;
      }
    }

    /// <summary>true, если значение похоже на относительное хранимое (не пустое и не укоренённое).</summary>
    /// <param name="storedPath">Хранимое значение.</param>
    /// <returns>true для относительного непустого значения.</returns>
    internal static bool IsRelativeStored(string storedPath)
    {
      string value = (storedPath ?? string.Empty).Trim();
      return value.Length > 0 && !Path.IsPathRooted(value);
    }

    /// <summary>
    /// Пытается срезать корневой префикс (регистронезависимо). После среза снимает
    /// один ведущий разделитель — «Z:\» + «\03_Производство\...» дал бы двойной.
    /// </summary>
    /// <param name="value">Полное значение.</param>
    /// <param name="prefix">Корневой префикс.</param>
    /// <param name="relative">Результат среза.</param>
    /// <returns>true, если префикс совпал.</returns>
    internal static bool TryStripPrefix(string value, string prefix, out string relative)
    {
      relative = string.Empty;
      if (string.IsNullOrEmpty(prefix))
        return false;

      if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        return false;

      string rest = value.Substring(prefix.Length);
      if (rest.StartsWith("\\", StringComparison.Ordinal) || rest.StartsWith("/", StringComparison.Ordinal))
        rest = rest.Substring(1);

      relative = rest;
      return true;
    }
  }
}
