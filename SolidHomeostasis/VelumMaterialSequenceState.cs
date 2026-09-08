using System;
using System.Collections.Generic;
using System.IO;
using ISIDA.Common;
using SolidWorks.Interop.sldworks;

namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Session-learned материал: обучение на Save Part (каталог + база sldmat + имя) и выдача по рецепту.
  /// Состояние по каталогу живёт до закрытия SolidWorks.
  /// </summary>
  internal static class VelumMaterialSequenceState
  {
    private sealed class LearnedMaterial
    {
      internal string MaterialName;
      internal string DatabaseName;
    }

    private static readonly object Sync = new object();

    private static readonly Dictionary<string, LearnedMaterial> ByDirectory =
        new Dictionary<string, LearnedMaterial>(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> AutoMaterialDocumentKeys =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Есть ли хотя бы один выученный каталог.</summary>
    internal static bool IsReady
    {
      get
      {
        lock (Sync)
          return ByDirectory.Count > 0;
      }
    }

    /// <summary>Обновляет состояние после успешного Save Part с назначенным материалом.</summary>
    internal static void TryLearnFromSavedDocument(ModelDoc2 modelDoc, string saveFileNameHint)
    {
      if (modelDoc == null)
        return;

      if (!(modelDoc is PartDoc))
        return;

      string fullPath = ResolveSavedPath(modelDoc, saveFileNameHint);
      if (string.IsNullOrWhiteSpace(fullPath))
      {
        Logger.Info("Velum material sequence learn skipped (no saved path)");
        return;
      }

      if (!VelumSolidWorksMaterialComHelper.TryReadPartMaterial(
              modelDoc,
              out string materialName,
              out string databaseName))
      {
        Logger.Info(
            "Velum material sequence learn skipped (COM read failed) path=\"" + fullPath + "\"");
        return;
      }

      if (string.IsNullOrWhiteSpace(materialName))
      {
        Logger.Info(
            "Velum material sequence learn skipped (no material on part) path=\"" + fullPath + "\"");
        return;
      }

      if (string.IsNullOrWhiteSpace(databaseName))
      {
        Logger.Info(
            "Velum material sequence learn skipped (empty material database) path=\"" + fullPath +
            "\" material=\"" + materialName.Trim() + "\"");
        return;
      }

      string directory;
      try
      {
        directory = Path.GetDirectoryName(fullPath);
      }
      catch
      {
        return;
      }

      if (string.IsNullOrWhiteSpace(directory))
        return;

      string key = NormalizeDirectory(directory);
      lock (Sync)
      {
        ByDirectory[key] = new LearnedMaterial
        {
          MaterialName = materialName.Trim(),
          DatabaseName = databaseName.Trim()
        };
      }

      Logger.Info(
          "Velum material sequence learned dir=\"" + directory +
          "\" material=\"" + materialName.Trim() +
          "\" database=\"" + databaseName.Trim() + "\"");
    }

    /// <summary>Есть ли выученный материал для каталога активной детали.</summary>
    internal static bool IsReadyForDirectory(string directory)
    {
      if (string.IsNullOrWhiteSpace(directory))
        return false;

      lock (Sync)
        return ByDirectory.ContainsKey(NormalizeDirectory(directory));
    }

    /// <summary>Снимок выученного материала для каталога.</summary>
    internal static bool TryGetLearnedMaterial(
        string directory,
        out string materialName,
        out string databaseName)
    {
      materialName = null;
      databaseName = null;
      if (string.IsNullOrWhiteSpace(directory))
        return false;

      lock (Sync)
      {
        LearnedMaterial learned;
        if (!ByDirectory.TryGetValue(NormalizeDirectory(directory), out learned) || learned == null)
          return false;

        materialName = learned.MaterialName;
        databaseName = learned.DatabaseName;
        return !string.IsNullOrWhiteSpace(materialName) &&
               !string.IsNullOrWhiteSpace(databaseName);
      }
    }

    /// <summary>Материал по рецепту уже назначен этому открытому документу в текущей сессии SW.</summary>
    internal static bool WasAutoMaterialAppliedToDocument(ModelDoc2 modelDoc)
    {
      string key = VelumSolidWorksModelDocHelper.TryGetDispatchDocumentKey(modelDoc);
      if (string.IsNullOrWhiteSpace(key) || string.Equals(key, "no_doc", StringComparison.OrdinalIgnoreCase))
        return false;

      lock (Sync)
        return AutoMaterialDocumentKeys.Contains(key);
    }

    internal static void MarkAutoMaterialAppliedToDocument(ModelDoc2 modelDoc)
    {
      string key = VelumSolidWorksModelDocHelper.TryGetDispatchDocumentKey(modelDoc);
      if (string.IsNullOrWhiteSpace(key) || string.Equals(key, "no_doc", StringComparison.OrdinalIgnoreCase))
        return;

      lock (Sync)
        AutoMaterialDocumentKeys.Add(key);
    }

    /// <summary>Текущий материал документа совпадает с выученным для его каталога.</summary>
    internal static bool DocumentMaterialMatchesLearned(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return false;

      string directory = TryGetDocumentDirectory(modelDoc);
      if (string.IsNullOrWhiteSpace(directory))
        return false;

      if (!TryGetLearnedMaterial(directory, out string learnedName, out string learnedDatabase))
        return false;

      if (!VelumSolidWorksMaterialComHelper.TryReadPartMaterial(
              modelDoc,
              out string currentName,
              out string currentDatabase))
        return false;

      return string.Equals(learnedName, currentName, StringComparison.OrdinalIgnoreCase) &&
             string.Equals(learnedDatabase, currentDatabase, StringComparison.OrdinalIgnoreCase);
    }

    internal static string TryGetDocumentDirectory(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return null;

      try
      {
        string path = modelDoc.GetPathName();
        if (string.IsNullOrWhiteSpace(path))
          return null;

        return Path.GetDirectoryName(path);
      }
      catch
      {
        return null;
      }
    }

    /// <summary>
    /// Каталог для assign: путь сохранённой детали или единственный выученный каталог сессии (несохранённая деталь).
    /// </summary>
    internal static bool TryResolveAssignDirectory(ModelDoc2 modelDoc, out string directory)
    {
      directory = TryGetDocumentDirectory(modelDoc);
      if (!string.IsNullOrWhiteSpace(directory))
        return true;

      lock (Sync)
      {
        if (ByDirectory.Count != 1)
          return false;

        foreach (string key in ByDirectory.Keys)
        {
          directory = key;
          return true;
        }
      }

      return false;
    }

    private static string NormalizeDirectory(string directory)
    {
      try
      {
        return Path.GetFullPath(directory.Trim());
      }
      catch
      {
        return directory.Trim();
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
  }
}
