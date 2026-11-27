using System;
using System.Collections.Generic;
using System.IO;
using ISIDA.SymbiontEnv.Contract;

namespace Velum.ReactiveCore.DocumentTemplates
{
  /// <summary>
  /// Pure-правила выбора корня библиотеки шаблонов и проверки относительных путей
  /// файлов шаблонов. Проверка существования каталогов выполняется вызывающим кодом
  /// через <c>VelumPathExists</c> в UI-потоке SolidWorks.
  /// </summary>
  internal static class VelumDocumentTemplateCatalogRules
  {
    /// <summary>
    /// Разрешает относительный <c>templateFile</c> внутри корня библиотеки.
    /// </summary>
    /// <param name="libraryRoot">Корень библиотеки.</param>
    /// <param name="templateFile">Относительный путь внутри корня.</param>
    /// <returns>Полный путь либо пустая строка, если путь недопустим.</returns>
    internal static string ResolveTemplateFile(string libraryRoot, string templateFile)
    {
      string root = (libraryRoot ?? string.Empty).Trim();
      string file = (templateFile ?? string.Empty).Trim();
      if (root.Length == 0 || file.Length == 0)
        return string.Empty;

      if (Path.IsPathRooted(file) ||
          file.StartsWith("\\", StringComparison.Ordinal) ||
          file.StartsWith("/", StringComparison.Ordinal) ||
          file.Contains(":"))
        return string.Empty;

      if (ContainsTraversal(file))
        return string.Empty;

      string normalizedFile = file.Replace('/', '\\');
      string full;
      try
      {
        full = Path.GetFullPath(Path.Combine(root, normalizedFile));
      }
      catch
      {
        return string.Empty;
      }

      string fullRoot;
      try
      {
        fullRoot = Path.GetFullPath(root);
        if (!fullRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
          fullRoot += Path.DirectorySeparatorChar;
      }
      catch
      {
        return string.Empty;
      }

      // Защита от выхода за пределы корня.
      if (!full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        return string.Empty;

      return full;
    }

    /// <summary>
    /// Выбирает активный корень библиотеки: проектная перекрывает общую.
    /// Возвращает первый каталог, где существует <c>document-templates.json</c>.
    /// </summary>
    /// <param name="projectRoot">Проектный корень (может быть пустым).</param>
    /// <param name="sharedRoot">Общий корень (может быть пустым).</param>
    /// <param name="fileExists">Предикат существования файла (для тестов и гарда UI-потока).</param>
    /// <returns>Активный корень либо пустая строка.</returns>
    internal static string ResolveLibraryRoot(
        string projectRoot,
        string sharedRoot,
        Func<string, bool> fileExists)
    {
      Func<string, bool> exists = fileExists ?? File.Exists;

      if (TryLibraryAt(projectRoot, exists, out string project))
        return project;

      if (TryLibraryAt(sharedRoot, exists, out string shared))
        return shared;

      return string.Empty;
    }

    private static bool TryLibraryAt(string root, Func<string, bool> exists, out string resolved)
    {
      resolved = string.Empty;
      string value = (root ?? string.Empty).Trim();
      if (value.Length == 0)
        return false;

      string manifest;
      try
      {
        manifest = Path.Combine(value, DocumentTemplatesJsonCodec.FileName);
      }
      catch
      {
        return false;
      }

      if (!exists(manifest))
        return false;

      resolved = value;
      return true;
    }

    private static bool ContainsTraversal(string file)
    {
      string[] parts = file.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
      foreach (string part in parts)
      {
        if (string.Equals(part, "..", StringComparison.Ordinal))
          return true;
      }

      return false;
    }

    /// <summary>
    /// Возвращает полный путь к файлу библиотеки в заданном корне.
    /// </summary>
    /// <param name="root">Корень библиотеки.</param>
    /// <returns>Полный путь к <c>document-templates.json</c> либо пустая строка.</returns>
    internal static string GetLibraryFilePath(string root)
    {
      string value = (root ?? string.Empty).Trim();
      if (value.Length == 0)
        return string.Empty;

      try
      {
        return Path.Combine(value, DocumentTemplatesJsonCodec.FileName);
      }
      catch
      {
        return string.Empty;
      }
    }
  }
}
