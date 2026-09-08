using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using ISIDA.Common;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Выделение порядкового номера <c>{SEQ}</c> / <c>{SEQ:n}</c> по уже существующим файлам в каталоге документа.
  /// </summary>
  internal static class VelumRecipeSeqAllocator
  {
    private static readonly Regex SeqTokenRegex = new Regex(
        @"\{SEQ(?::(\d+))?\}",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly string[] SolidWorksExtensions =
    {
      ".sldprt",
      ".sldasm",
      ".slddrw",
    };

    /// <summary>
    /// Следующий SEQ: max среди совпадающих имён файлов в каталоге документа + 1,
    /// либо номер текущего файла, если его имя уже соответствует маске шаблона.
    /// </summary>
    public static int AllocateNextSeq(string template, IReadOnlyDictionary<string, string> context)
    {
      if (TryParseExplicitSeqOverride(context, out int explicitSeq))
        return explicitSeq;

      if (!TryBuildFileNamePattern(template, context, out Regex namePattern, out _))
        return 1;

      string documentPath = GetContextValue(context, "DOCUMENT_PATH");
      string directory = null;
      string currentBaseName = string.Empty;

      if (!string.IsNullOrWhiteSpace(documentPath))
      {
        try
        {
          directory = Path.GetDirectoryName(documentPath);
          currentBaseName = Path.GetFileNameWithoutExtension(documentPath);
        }
        catch
        {
        }
      }

      if (string.IsNullOrWhiteSpace(directory))
        directory = GetContextValue(context, "SEQ_SCAN_DIRECTORY");

      if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        return 1;

      int maxSeq = 0;
      int? currentFileSeq = null;

      try
      {
        foreach (string filePath in Directory.EnumerateFiles(directory))
        {
          string extension;
          try
          {
            extension = Path.GetExtension(filePath);
          }
          catch
          {
            continue;
          }

          if (!IsSolidWorksExtension(extension))
            continue;

          string baseName;
          try
          {
            baseName = Path.GetFileNameWithoutExtension(filePath);
          }
          catch
          {
            continue;
          }

          if (!TryExtractSeq(namePattern, baseName, out int seq))
            continue;

          if (!string.IsNullOrWhiteSpace(currentBaseName) &&
              string.Equals(baseName, currentBaseName, StringComparison.OrdinalIgnoreCase))
          {
            currentFileSeq = seq;
            continue;
          }

          if (seq > maxSeq)
            maxSeq = seq;
        }
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum Recipe SEQ scan failed: " + ex.Message);
        return currentFileSeq ?? 1;
      }

      if (currentFileSeq.HasValue)
      {
        Logger.Info(
            "Velum Recipe SEQ from current file name: " +
            currentFileSeq.Value.ToString(CultureInfo.InvariantCulture));
        return currentFileSeq.Value;
      }

      int next = maxSeq + 1;
      Logger.Info(
          "Velum Recipe SEQ allocated: next=" + next.ToString(CultureInfo.InvariantCulture) +
          " max=" + maxSeq.ToString(CultureInfo.InvariantCulture) +
          " dir=\"" + directory + "\"");
      return next;
    }

    private static bool TryBuildFileNamePattern(
        string template,
        IReadOnlyDictionary<string, string> context,
        out Regex namePattern,
        out int seqWidth)
    {
      namePattern = null;
      seqWidth = 0;

      if (string.IsNullOrEmpty(template) || context == null)
        return false;

      Match seqMatch = SeqTokenRegex.Match(template);
      if (!seqMatch.Success)
        return false;

      string widthGroup = seqMatch.Groups[1].Value;
      if (!string.IsNullOrEmpty(widthGroup) &&
          int.TryParse(widthGroup, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedWidth) &&
          parsedWidth > 0 &&
          parsedWidth < 32)
        seqWidth = parsedWidth;

      string prefixTemplate = template.Substring(0, seqMatch.Index);
      string suffixTemplate = template.Substring(seqMatch.Index + seqMatch.Length);

      string resolvedPrefix = VelumRecipeTemplateResolver.ResolveTemplateForSeqMask(prefixTemplate, context);
      string resolvedSuffix = VelumRecipeTemplateResolver.ResolveTemplateForSeqMask(suffixTemplate, context);

      string digitCapture = seqWidth > 0
          ? @"(\d{" + seqWidth.ToString(CultureInfo.InvariantCulture) + "})"
          : @"(\d+)";

      string regexPattern = "^" +
                            Regex.Escape(resolvedPrefix) +
                            digitCapture +
                            Regex.Escape(resolvedSuffix) +
                            "$";

      try
      {
        namePattern = new Regex(regexPattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        return true;
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum Recipe SEQ pattern invalid: " + ex.Message);
        return false;
      }
    }

    private static bool TryExtractSeq(Regex namePattern, string baseName, out int seq)
    {
      seq = 0;
      if (namePattern == null || string.IsNullOrWhiteSpace(baseName))
        return false;

      Match match = namePattern.Match(baseName.Trim());
      if (!match.Success || match.Groups.Count < 2)
        return false;

      return int.TryParse(
          match.Groups[1].Value,
          NumberStyles.Integer,
          CultureInfo.InvariantCulture,
          out seq);
    }

    private static bool TryParseExplicitSeqOverride(
        IReadOnlyDictionary<string, string> context,
        out int seq)
    {
      seq = 0;
      if (context == null || !context.TryGetValue("SEQ", out string raw) || string.IsNullOrWhiteSpace(raw))
        return false;

      return int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out seq) && seq > 0;
    }

    private static bool IsSolidWorksExtension(string extension)
    {
      if (string.IsNullOrWhiteSpace(extension))
        return false;

      foreach (string allowed in SolidWorksExtensions)
      {
        if (extension.Equals(allowed, StringComparison.OrdinalIgnoreCase))
          return true;
      }

      return false;
    }

    private static string GetContextValue(IReadOnlyDictionary<string, string> context, string name)
    {
      if (context != null && context.TryGetValue(name, out string value) && value != null)
        return value;
      return string.Empty;
    }
  }
}
