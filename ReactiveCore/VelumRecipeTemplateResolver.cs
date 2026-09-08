using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SolidWorks.Interop.sldworks;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Подстановка плейсхолдеров <c>{NAME}</c>, <c>{SEQ:n}</c>, <c>{FOLDER:n}</c> в шаблонах рецепта.
  /// Ссылки на штатные переменные SolidWorks (например <c>$PRP:&quot;SW-File Name&quot;</c>) передаются в SW как есть.
  /// <c>{FOLDER:n}</c> — последние <c>n</c> уровней каталога документа, разделитель «.» (по умолчанию <c>n=1</c>).
  /// </summary>
  internal static class VelumRecipeTemplateResolver
  {
    private const int DefaultFolderDepth = 1;
    private const int MaxFolderDepth = 31;

    private static readonly Regex PlaceholderRegex = new Regex(
        @"\{([A-Za-z0-9_]+)(?::(\d+))?\}",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex GeneralPrpRegex = new Regex(
        @"\$PRP:\s*""([^""]+)""",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SeqTokenRegex = new Regex(
        @"\{SEQ(?::(\d+))?\}",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Строит контекст подстановки из снимка сессии и активного документа SW.
    /// </summary>
    /// <param name="snapshot">Снимок сессии SolidWorks.</param>
    /// <param name="modelDoc">Активный документ COM или <c>null</c>.</param>
    /// <returns>Словарь плейсхолдеров (регистронезависимые ключи).</returns>
    public static IReadOnlyDictionary<string, string> BuildContext(
        SolidWorksSessionSnapshot snapshot,
        ModelDoc2 modelDoc)
    {
      var ctx = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

      string path = snapshot?.DocumentPath ?? string.Empty;
      if (string.IsNullOrWhiteSpace(path) && modelDoc != null)
      {
        try
        {
          path = modelDoc.GetPathName() ?? string.Empty;
        }
        catch
        {
        }
      }

      if (modelDoc != null)
      {
        try
        {
          string pathFromDoc = modelDoc.GetPathName();
          if (!string.IsNullOrWhiteSpace(pathFromDoc))
            path = pathFromDoc;
        }
        catch
        {
        }
      }

      if (!string.IsNullOrWhiteSpace(path))
      {
        ctx["DOCUMENT_PATH"] = path;
        try
        {
          ctx["FILE_NAME"] = Path.GetFileName(path);
        }
        catch
        {
        }
      }

      if (modelDoc != null)
        MergeCustomPropertiesIntoContext(modelDoc, ctx);

      EnrichDisciplineFromPath(path, ctx);

      return ctx;
    }

    /// <summary>
    /// Разрешает фрагмент шаблона для построения маски SEQ (без подстановки <c>{SEQ}</c>).
    /// </summary>
    /// <param name="template">Исходный шаблон рецепта.</param>
    /// <param name="context">Контекст подстановки.</param>
    internal static string ResolveTemplateForSeqMask(
        string template,
        IReadOnlyDictionary<string, string> context)
    {
      if (string.IsNullOrEmpty(template))
        return string.Empty;

      string withoutSeq = SeqTokenRegex.Replace(template, string.Empty);
      return Resolve(withoutSeq, context);
    }

    /// <summary>
    /// Разрешает шаблон с подстановкой плейсхолдеров.
    /// </summary>
    /// <param name="template">Исходный шаблон.</param>
    /// <param name="context">Контекст подстановки.</param>
    /// <returns>Строка с подставленными значениями.</returns>
    public static string Resolve(string template, IReadOnlyDictionary<string, string> context)
    {
      if (string.IsNullOrEmpty(template))
        return string.Empty;

      if (context == null || context.Count == 0)
        return template;

      string[] folderSegments = ResolveSwFolderSegments(context);

      string result = PlaceholderRegex.Replace(
        template,
        m =>
        {
          string name = m.Groups[1].Value;
          string paramGroup = m.Groups[2].Value;

          if (string.Equals(name, "SEQ", StringComparison.OrdinalIgnoreCase))
          {
            int seq = VelumRecipeSeqAllocator.AllocateNextSeq(template, context);
            if (!string.IsNullOrEmpty(paramGroup) &&
                int.TryParse(paramGroup, NumberStyles.Integer, CultureInfo.InvariantCulture, out int width) &&
                width > 0 && width < 32)
              return seq.ToString("D" + width.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            return seq.ToString(CultureInfo.InvariantCulture);
          }

          if (string.Equals(name, "FOLDER", StringComparison.OrdinalIgnoreCase))
          {
            int depth = ParseFolderDepth(paramGroup, DefaultFolderDepth);
            return FormatFolderSegmentsByDepth(folderSegments, depth);
          }

          return GetContextValue(context, name);
        });

      return result;
    }

    /// <summary>
    /// Разрешает шаблон для имени файла на диске: после <see cref="Resolve"/> подставляет
    /// оставшиеся <c>$PRP:"…"</c> из свойств SW (в рецептах формулы часто остаются для SW).
    /// </summary>
    public static string ResolveForFileName(
        string template,
        ModelDoc2 modelDoc,
        IReadOnlyDictionary<string, string> context)
    {
      if (string.IsNullOrEmpty(template))
        return string.Empty;

      string result = Resolve(template, context);
      if (modelDoc == null && (context == null || context.Count == 0))
        return result;

      return GeneralPrpRegex.Replace(
          result,
          m =>
          {
            string propertyName = (m.Groups[1].Value ?? string.Empty).Trim();
            if (propertyName.Length == 0)
              return m.Value;

            if (!TryResolvePrpPropertyValue(propertyName, modelDoc, context, out string resolved) ||
                string.IsNullOrWhiteSpace(resolved))
              return m.Value;

            return resolved.Trim();
          });
    }

    private static bool TryResolvePrpPropertyValue(
        string propertyName,
        ModelDoc2 modelDoc,
        IReadOnlyDictionary<string, string> context,
        out string resolved)
    {
      resolved = string.Empty;
      if (string.IsNullOrWhiteSpace(propertyName))
        return false;

      if (context != null &&
          context.TryGetValue(propertyName, out string fromContext) &&
          !string.IsNullOrWhiteSpace(fromContext))
      {
        resolved = FormatResolvedPropertyValueForFileName(propertyName, fromContext);
        return true;
      }

      string fromSw = TryGetSwBuiltInProperty(modelDoc, propertyName);
      if (!string.IsNullOrWhiteSpace(fromSw))
      {
        resolved = FormatResolvedPropertyValueForFileName(propertyName, fromSw);
        return true;
      }

      if (string.Equals(propertyName, "SW-File Name", StringComparison.OrdinalIgnoreCase))
      {
        string path = GetContextValue(context, "DOCUMENT_PATH");
        if (string.IsNullOrWhiteSpace(path) && modelDoc != null)
        {
          try
          {
            path = modelDoc.GetPathName() ?? string.Empty;
          }
          catch
          {
          }
        }

        if (!string.IsNullOrWhiteSpace(path))
        {
          try
          {
            resolved = Path.GetFileNameWithoutExtension(path);
            if (!string.IsNullOrWhiteSpace(resolved))
              return true;
          }
          catch
          {
          }
        }

        if (modelDoc != null)
        {
          try
          {
            string title = modelDoc.GetTitle();
            if (!string.IsNullOrWhiteSpace(title))
            {
              resolved = Path.GetFileNameWithoutExtension(title.Trim());
              return !string.IsNullOrWhiteSpace(resolved);
            }
          }
          catch
          {
          }
        }
      }

      return false;
    }

    private static string FormatResolvedPropertyValueForFileName(string propertyName, string rawValue)
    {
      string trimmed = (rawValue ?? string.Empty).Trim();
      if (VelumBlankSizeProperties.IsBlankSizePropertyName(propertyName))
        return VelumBlankSizeProperties.FormatValueForFileName(trimmed);

      return trimmed;
    }

    /// <summary>
    /// Читает значение свойства SW для подстановки в имя файла DXF.
    /// </summary>
    public static bool TryResolvePropertyValueForFileName(
        string propertyName,
        ModelDoc2 modelDoc,
        IReadOnlyDictionary<string, string> context,
        out string resolved)
    {
      return TryResolvePrpPropertyValue(propertyName, modelDoc, context, out resolved);
    }

    private static string GetContextValue(IReadOnlyDictionary<string, string> context, string name)
    {
      if (context.TryGetValue(name, out string value) && value != null)
        return value;
      return string.Empty;
    }

    /// <summary>
    /// Пустой или «технический» результат шаблона (только «-», нули, пробелы) — не записывать в SW.
    /// </summary>
    /// <param name="value">Разрешённое значение шаблона.</param>
    public static bool IsMeaninglessResolvedValue(string value)
    {
      if (string.IsNullOrWhiteSpace(value))
        return true;

      if (ContainsUnresolvedTemplateTokens(value))
        return true;

      // Только цифры/разделители без букв (в т.ч. кириллицы) — не записывать (например «--0001»).
      return !value.Any(c => char.IsLetter(c));
    }

    /// <summary>
    /// В строке остались неразрешённые плейсхолдеры шаблона (<c>$PRP:</c>, <c>{NAME}</c>).
    /// </summary>
    public static bool ContainsUnresolvedTemplateTokens(string value)
    {
      if (string.IsNullOrWhiteSpace(value))
        return false;

      return value.IndexOf("$PRP:", StringComparison.OrdinalIgnoreCase) >= 0 ||
             value.IndexOf('{') >= 0;
    }

    private static int ParseFolderDepth(string raw, int defaultDepth)
    {
      if (string.IsNullOrWhiteSpace(raw))
        return defaultDepth;

      if (!int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int depth))
        return defaultDepth;

      if (depth < 1)
        return 1;
      if (depth > MaxFolderDepth)
        return MaxFolderDepth;
      return depth;
    }

    private static string[] ResolveSwFolderSegments(IReadOnlyDictionary<string, string> context)
    {
      string path = GetContextValue(context, "DOCUMENT_PATH");
      if (string.IsNullOrWhiteSpace(path))
      {
        string scanDir = GetContextValue(context, "SEQ_SCAN_DIRECTORY");
        if (!string.IsNullOrWhiteSpace(scanDir))
          path = CombineSyntheticDocumentPath(scanDir);
      }

      return SplitFolderPath(TryInferFolderNameFromDocumentPath(path));
    }

    private static string CombineSyntheticDocumentPath(string directory)
    {
      if (string.IsNullOrWhiteSpace(directory))
        return string.Empty;

      try
      {
        return Path.Combine(directory.Trim(), "velum.sldprt");
      }
      catch
      {
        return string.Empty;
      }
    }

    private static string[] SplitFolderPath(string rawFolderPath)
    {
      if (string.IsNullOrWhiteSpace(rawFolderPath))
        return Array.Empty<string>();

      return rawFolderPath
          .Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries)
          .Select(s => s.Trim())
          .Where(s => s.Length > 0)
          .ToArray();
    }

    /// <summary>
    /// Берёт последние <paramref name="depth"/> сегментов пути каталога и соединяет точками.
    /// </summary>
    private static string FormatFolderSegmentsByDepth(string[] segments, int depth)
    {
      if (segments == null || segments.Length == 0)
        return string.Empty;

      if (depth < 1)
        depth = 1;
      if (depth > MaxFolderDepth)
        depth = MaxFolderDepth;

      int take = Math.Min(depth, segments.Length);
      return string.Join(".", segments.Skip(segments.Length - take));
    }

    /// <summary>
    /// Каталог документа относительно корня диска, как <c>Изделия\КББ</c> для <c>D:\Изделия\КББ\part.SLDPRT</c>.
    /// </summary>
    private static string TryInferFolderNameFromDocumentPath(string documentPath)
    {
      if (string.IsNullOrWhiteSpace(documentPath))
        return null;

      string dir;
      try
      {
        dir = Path.GetDirectoryName(documentPath);
      }
      catch
      {
        return null;
      }

      if (string.IsNullOrWhiteSpace(dir))
        return null;

      string root;
      try
      {
        root = Path.GetPathRoot(dir);
      }
      catch
      {
        return null;
      }

      if (string.IsNullOrEmpty(root) || dir.Length <= root.Length)
        return null;

      string relative = dir.Substring(root.Length).TrimStart('\\', '/');
      return string.IsNullOrWhiteSpace(relative) ? null : relative;
    }

    private static string TryGetSwBuiltInProperty(ModelDoc2 modelDoc, string propertyName)
    {
      if (modelDoc == null || string.IsNullOrWhiteSpace(propertyName))
        return null;

      foreach (string configKey in GetConfigKeysToProbe(modelDoc))
      {
        try
        {
          string val = modelDoc.CustomInfo2[configKey ?? string.Empty, propertyName];
          if (!string.IsNullOrWhiteSpace(val))
            return val;
        }
        catch
        {
        }

        try
        {
          CustomPropertyManager cpm = modelDoc.Extension?.CustomPropertyManager[configKey ?? string.Empty];
          if (cpm != null &&
              VelumRecipeSolidWorksCustomProperties.TryGetValue(cpm, propertyName, out string resolved) &&
              !string.IsNullOrWhiteSpace(resolved))
            return resolved;
        }
        catch
        {
        }
      }

      return null;
    }

    /// <summary>
    /// Подставляет DISCIPLINE из пути, если его нет в свойствах и контексте.
    /// </summary>
    private static void EnrichDisciplineFromPath(string documentPath, Dictionary<string, string> ctx)
    {
      if (ctx == null || ctx.ContainsKey("DISCIPLINE"))
        return;

      if (TryApplyDisciplineFromPath(documentPath, ctx))
        return;

      string scanDir = GetContextValue(ctx, "SEQ_SCAN_DIRECTORY");
      if (!string.IsNullOrWhiteSpace(scanDir))
        TryApplyDisciplineFromPath(CombineSyntheticDocumentPath(scanDir), ctx);
    }

    private static bool TryApplyDisciplineFromPath(string documentPath, Dictionary<string, string> ctx)
    {
      if (ctx == null || string.IsNullOrWhiteSpace(documentPath))
        return false;

      string[] segments;
      try
      {
        segments = documentPath
            .Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
      }
      catch
      {
        return false;
      }

      if (segments.Length < 3)
        return false;

      string grandParent = segments[segments.Length - 3];
      if (string.IsNullOrWhiteSpace(grandParent))
        return false;

      ctx["DISCIPLINE"] = grandParent.Trim();
      return true;
    }

    private static void MergeCustomPropertiesIntoContext(ModelDoc2 modelDoc, Dictionary<string, string> ctx)
    {
      foreach (string configKey in GetConfigKeysToProbe(modelDoc))
      {
        try
        {
          CustomPropertyManager cpm = modelDoc.Extension?.CustomPropertyManager[configKey];
          if (cpm == null)
            continue;

          string[] names = cpm.GetNames() as string[];
          if (names == null)
            continue;

          foreach (string name in names)
          {
            if (string.IsNullOrWhiteSpace(name))
              continue;
            if (ctx.ContainsKey(name))
              continue;

            if (VelumRecipeSolidWorksCustomProperties.TryGetValue(cpm, name, out string val) &&
                !string.IsNullOrWhiteSpace(val))
              ctx[name] = val.Trim();
          }
        }
        catch
        {
        }
      }
    }

    private static IEnumerable<string> GetConfigKeysToProbe(ModelDoc2 modelDoc)
    {
      yield return string.Empty;

      string activeName = TryGetActiveConfigurationName(modelDoc);
      if (!string.IsNullOrWhiteSpace(activeName))
        yield return activeName;
    }

    private static string TryGetActiveConfigurationName(ModelDoc2 modelDoc)
    {
      try
      {
        ConfigurationManager cm = modelDoc?.ConfigurationManager;
        SolidWorks.Interop.sldworks.Configuration active = cm?.ActiveConfiguration;
        return active?.Name;
      }
      catch
      {
        return null;
      }
    }
  }
}
