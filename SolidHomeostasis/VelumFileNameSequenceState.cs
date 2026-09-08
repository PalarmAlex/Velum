using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ISIDA.Common;
using SolidWorks.Interop.sldworks;
using Velum.ReactiveCore;

namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Полуавтомат имени файла: обучение на Save (префикс + цифровой суффикс) и выдача следующего имени по рецепту.
  /// Состояние по типу документа (Part / Assembly) живёт до закрытия SolidWorks.
  /// Чертежи не участвуют: имя обычно наследуют от исходной детали/сборки.
  /// </summary>
  internal static class VelumFileNameSequenceState
  {
    internal const int MaxUniqueNameAttempts = 10;

    private sealed class LearnedFileNameSequence
    {
      internal string Directory;
      internal string Prefix;
      internal string Suffix;
      internal string Extension;
    }

    private static readonly object Sync = new object();

    private static readonly Dictionary<VelumSolidDocumentKind, LearnedFileNameSequence> ByKind =
        new Dictionary<VelumSolidDocumentKind, LearnedFileNameSequence>();

    private static readonly HashSet<string> AutoNamedDocumentKeys =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Есть ли обученное состояние для автоподстановки имени хотя бы одного типа документа.</summary>
    internal static bool IsReady
    {
      get
      {
        lock (Sync)
        {
          foreach (LearnedFileNameSequence sequence in ByKind.Values)
          {
            if (HasReadySequenceLocked(sequence))
              return true;
          }

          return false;
        }
      }
    }

    /// <summary>
    /// Обновляет состояние после успешного Save, если имя заканчивается цифровым хвостом (ASCII 0–9).
    /// </summary>
    internal static void TryLearnFromSavedDocument(ModelDoc2 modelDoc, string saveFileNameHint)
    {
      if (modelDoc == null)
        return;

      string fullPath = ResolveSavedPath(modelDoc, saveFileNameHint);
      if (string.IsNullOrWhiteSpace(fullPath))
        return;

      VelumSolidDocumentKind kind = ClassifyDocumentKind(modelDoc);
      if (!IsSupportedDocumentKind(kind))
        return;

      TryLearnFromSavedPath(fullPath, kind);
    }

    /// <summary>Есть ли обученное состояние для указанного типа документа.</summary>
    internal static bool IsReadyForDocumentKind(VelumSolidDocumentKind activeKind)
    {
      lock (Sync)
        return TryGetReadySequenceLocked(activeKind, out _);
    }

    /// <summary>Подбирает следующее свободное имя без изменения состояния.</summary>
    internal static bool TryResolveNextBaseName(
        VelumSolidDocumentKind activeKind,
        out string baseName,
        out string resolvedSuffix)
    {
      baseName = null;
      resolvedSuffix = null;
      lock (Sync)
      {
        if (!TryGetReadySequenceLocked(activeKind, out LearnedFileNameSequence sequence))
          return false;

        string workingSuffix = sequence.Suffix;
        for (int attempt = 0; attempt < MaxUniqueNameAttempts; attempt++)
        {
          string nextSuffix = IncrementSuffix(workingSuffix);
          if (string.IsNullOrEmpty(nextSuffix))
            return false;

          string candidate = sequence.Prefix + nextSuffix;
          if (!IsNameOccupiedInDirectoryLocked(sequence, candidate))
          {
            baseName = candidate;
            resolvedSuffix = nextSuffix;
            return true;
          }

          workingSuffix = nextSuffix;
        }

        return false;
      }
    }

    internal static void CommitSuffix(VelumSolidDocumentKind activeKind, string suffix)
    {
      if (string.IsNullOrEmpty(suffix) || !IsSupportedDocumentKind(activeKind))
        return;

      lock (Sync)
      {
        if (!ByKind.TryGetValue(activeKind, out LearnedFileNameSequence sequence) || sequence == null)
          return;

        sequence.Suffix = suffix;
      }
    }

    /// <summary>Совпадает ли текущее имя документа с целевым базовым именем.</summary>
    internal static bool DocumentBaseNameMatches(ModelDoc2 modelDoc, string targetBaseName)
    {
      if (modelDoc == null || string.IsNullOrWhiteSpace(targetBaseName))
        return false;

      string current = TryGetDocumentBaseName(modelDoc);
      if (string.IsNullOrWhiteSpace(current))
        return false;

      return string.Equals(
          VelumSolidWorksSaveFileNameHelper.NormalizeBaseName(current),
          VelumSolidWorksSaveFileNameHelper.NormalizeBaseName(targetBaseName),
          StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Имя по рецепту уже назначено этому открытому документу в текущей сессии SW.</summary>
    internal static bool WasAutoNameAppliedToDocument(ModelDoc2 modelDoc)
    {
      string key = VelumSolidWorksModelDocHelper.TryGetDispatchDocumentKey(modelDoc);
      if (string.IsNullOrWhiteSpace(key) || string.Equals(key, "no_doc", StringComparison.OrdinalIgnoreCase))
        return false;

      lock (Sync)
        return AutoNamedDocumentKeys.Contains(key);
    }

    internal static void MarkAutoNameAppliedToDocument(ModelDoc2 modelDoc)
    {
      string key = VelumSolidWorksModelDocHelper.TryGetDispatchDocumentKey(modelDoc);
      if (string.IsNullOrWhiteSpace(key) || string.Equals(key, "no_doc", StringComparison.OrdinalIgnoreCase))
        return;

      lock (Sync)
        AutoNamedDocumentKeys.Add(key);
    }

    internal static void TryLearnFromSavedPath(string fullPath, VelumSolidDocumentKind kind)
    {
      if (string.IsNullOrWhiteSpace(fullPath) || !IsSupportedDocumentKind(kind))
        return;

      string directory;
      string baseName;
      string extension;
      try
      {
        directory = Path.GetDirectoryName(fullPath);
        baseName = Path.GetFileNameWithoutExtension(fullPath);
        extension = Path.GetExtension(fullPath);
      }
      catch
      {
        return;
      }

      if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(baseName))
        return;

      if (!TryParseNumericSuffix(baseName, out string prefix, out string suffix))
        return;

      lock (Sync)
      {
        ByKind[kind] = new LearnedFileNameSequence
        {
          Directory = directory,
          Prefix = prefix,
          Suffix = suffix,
          Extension = NormalizeExtension(extension, kind)
        };
      }

      Logger.Info(
          "Velum file name sequence learned dir=\"" + directory +
          "\" prefix=\"" + prefix + "\" suffix=\"" + suffix +
          "\" kind=" + kind.ToString());
    }

    private static bool IsSupportedDocumentKind(VelumSolidDocumentKind kind)
    {
      return kind == VelumSolidDocumentKind.Part ||
             kind == VelumSolidDocumentKind.Assembly;
    }

    private static bool TryGetReadySequenceLocked(
        VelumSolidDocumentKind kind,
        out LearnedFileNameSequence sequence)
    {
      sequence = null;
      if (!IsSupportedDocumentKind(kind))
        return false;

      if (!ByKind.TryGetValue(kind, out sequence) || sequence == null)
        return false;

      return HasReadySequenceLocked(sequence);
    }

    private static bool HasReadySequenceLocked(LearnedFileNameSequence sequence)
    {
      return sequence != null &&
             !string.IsNullOrWhiteSpace(sequence.Directory) &&
             sequence.Suffix != null &&
             sequence.Prefix != null &&
             !string.IsNullOrWhiteSpace(sequence.Extension);
    }

    private static bool IsNameOccupiedInDirectoryLocked(LearnedFileNameSequence sequence, string baseName)
    {
      if (sequence == null ||
          string.IsNullOrWhiteSpace(sequence.Directory) ||
          string.IsNullOrWhiteSpace(baseName))
        return true;

      try
      {
        string fullPath = Path.Combine(sequence.Directory, baseName + sequence.Extension);
        return File.Exists(fullPath);
      }
      catch
      {
        return true;
      }
    }

    private static string ResolveSavedPath(ModelDoc2 modelDoc, string saveFileNameHint)
    {
      if (!string.IsNullOrWhiteSpace(saveFileNameHint))
      {
        try
        {
          if (File.Exists(saveFileNameHint) || Directory.Exists(Path.GetDirectoryName(saveFileNameHint)))
            return saveFileNameHint.Trim();
        }
        catch
        {
        }
      }

      try
      {
        return modelDoc.GetPathName();
      }
      catch
      {
        return null;
      }
    }

    private static string TryGetDocumentBaseName(ModelDoc2 modelDoc)
    {
      try
      {
        string path = modelDoc.GetPathName();
        if (!string.IsNullOrWhiteSpace(path))
          return Path.GetFileNameWithoutExtension(path);
      }
      catch
      {
      }

      try
      {
        return modelDoc.GetTitle();
      }
      catch
      {
        return null;
      }
    }

    internal static bool TryParseNumericSuffix(
        string baseNameWithoutExtension,
        out string prefix,
        out string suffix)
    {
      prefix = null;
      suffix = null;
      if (string.IsNullOrWhiteSpace(baseNameWithoutExtension))
        return false;

      string trimmed = baseNameWithoutExtension.Trim();
      int end = trimmed.Length - 1;
      if (end < 0 || !IsAsciiDigit(trimmed[end]))
        return false;

      int index = end;
      while (index >= 0 && IsAsciiDigit(trimmed[index]))
        index--;

      suffix = trimmed.Substring(index + 1, end - index);
      prefix = trimmed.Substring(0, index + 1);
      return suffix.Length > 0;
    }

    internal static string IncrementSuffix(string suffix)
    {
      if (string.IsNullOrEmpty(suffix))
        return null;

      if (!int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
        return null;

      value++;
      return value.ToString(CultureInfo.InvariantCulture).PadLeft(suffix.Length, '0');
    }

    private static bool IsAsciiDigit(char c) => c >= '0' && c <= '9';

    private static string NormalizeExtension(string extension, VelumSolidDocumentKind kind)
    {
      string ext = string.IsNullOrWhiteSpace(extension) ? string.Empty : extension.Trim();
      if (ext.Length > 0 && ext[0] != '.')
        ext = "." + ext;

      if (IsSolidWorksDocumentExtension(ext))
        return ext;

      return VelumSolidWorksSaveFileNameHelper.TryGetDefaultExtensionForKind(kind);
    }

    private static bool IsSolidWorksDocumentExtension(string extension)
    {
      return extension.Equals(".sldprt", StringComparison.OrdinalIgnoreCase) ||
             extension.Equals(".sldasm", StringComparison.OrdinalIgnoreCase) ||
             extension.Equals(".slddrw", StringComparison.OrdinalIgnoreCase);
    }

    internal static VelumSolidDocumentKind ClassifyDocumentKind(ModelDoc2 modelDoc)
    {
      if (modelDoc is AssemblyDoc)
        return VelumSolidDocumentKind.Assembly;
      if (modelDoc is DrawingDoc)
        return VelumSolidDocumentKind.Drawing;
      if (modelDoc is PartDoc)
        return VelumSolidDocumentKind.Part;
      return VelumSolidDocumentKind.Other;
    }
  }
}
