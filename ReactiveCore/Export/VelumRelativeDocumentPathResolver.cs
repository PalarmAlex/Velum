using System;
using System.IO;
using Velum.Configuration;

namespace Velum.ReactiveCore.Export
{
  /// <summary>
  /// Конверсия путей для хранения относительных ссылок: в свойствах документов
  /// («путь чертежа», «Путь pdf», «Путь dxf») и в ключе/полях-ссылках реестра изделий.
  /// Корневой каталог — <see cref="VelumAppConfig.DocumentRootPath"/> (настройка локальная:
  /// дома по VPN — путь по IP, на работе — буква общего диска; хранимые пути относительны корню).
  /// При пустой настройке все методы прозрачны: значения проходят без изменений
  /// (сквозное прежнее поведение с абсолютными путями).
  /// </summary>
  internal static class VelumRelativeDocumentPathResolver
  {
    /// <summary>
    /// Готовит значение к записи в свойство/зеркало/ключ реестра: срезает корневой
    /// каталог (регистронезависимо) → относительный путь.
    /// Неизвестный/абсолютный без совпадения, уже относительный, пустой — без изменений.
    /// </summary>
    internal static string ToStored(string fullPath)
    {
      string value = (fullPath ?? string.Empty).Trim();
      if (value.Length == 0)
        return string.Empty;

      string root = GetRoot();
      if (root.Length == 0)
        return value;

      if (TryStripPrefix(value, root, out string relative))
        return relative;

      return value;
    }

    /// <summary>
    /// Восстанавливает полный путь из хранимого значения: относительный достраивается
    /// по корневому каталогу; абсолютный и пустой — без изменений.
    /// </summary>
    internal static string ToFull(string storedPath)
    {
      string value = (storedPath ?? string.Empty).Trim();
      if (value.Length == 0)
        return string.Empty;

      if (Path.IsPathRooted(value))
        return value;

      string root = GetRoot();
      if (root.Length == 0)
        return value;

      try
      {
        return Path.Combine(root, value);
      }
      catch
      {
        return value;
      }
    }

    /// <summary>
    /// Ключ сравнения путей: оба значения разворачиваются до полных
    /// (<see cref="ToFull"/>) и нормализуются через <c>Path.GetFullPath</c>.
    /// Используется вместо прямого <c>Path.GetFullPath</c> для относительных
    /// хранимых значений (иначе путь разворачивается от текущего каталога процесса).
    /// </summary>
    internal static string NormalizeForCompare(string path)
    {
      string value = (path ?? string.Empty).Trim();
      if (value.Length == 0)
        return string.Empty;

      try
      {
        return Path.GetFullPath(ToFull(value));
      }
      catch
      {
        return ToFull(value);
      }
    }

    /// <summary>true, если значение похоже на относительное хранимое (не пустое и не укоренённое).</summary>
    internal static bool IsRelativeStored(string storedPath)
    {
      string value = (storedPath ?? string.Empty).Trim();
      return value.Length > 0 && !Path.IsPathRooted(value);
    }

    /// <summary>true, если корневой путь документов задан и непуст.</summary>
    internal static bool HasRootPath()
    {
      return GetRoot().Length > 0;
    }

    private static bool TryStripPrefix(string value, string prefix, out string relative)
    {
      relative = string.Empty;
      if (string.IsNullOrEmpty(prefix))
        return false;

      if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        return false;

      string rest = value.Substring(prefix.Length);
      // «Z:\» + «\03_Производство\...» — двойной разделитель после среза.
      if (rest.StartsWith("\\", StringComparison.Ordinal) || rest.StartsWith("/", StringComparison.Ordinal))
        rest = rest.Substring(1);

      relative = rest;
      return true;
    }

    /// <summary>Нормализованный корневой каталог документов (string.Empty — не задан).</summary>
    private static string GetRoot()
    {
      return VelumAppConfig.GetDocumentRootPath();
    }
  }
}
