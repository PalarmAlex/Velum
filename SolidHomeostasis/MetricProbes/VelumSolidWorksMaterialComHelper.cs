using System;
using System.Collections.Generic;
using System.IO;
using ISIDA.Common;
using SolidWorks.Interop.sldworks;
using Xarial.XCad;
using Xarial.XCad.Documents;
using Xarial.XCad.SolidWorks;

namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Чтение и назначение материала детали через COM SolidWorks.
  /// </summary>
  internal static class VelumSolidWorksMaterialComHelper
  {
    internal static float? TryScorePartMaterialFromCom(IXApplication app, IXDocument ixDoc)
    {
      ModelDoc2 md = VelumSolidWorksModelDocHelper.TryGetActiveModelDoc2(app, ixDoc);
      return ScorePartMaterialFromModelDoc(md);
    }

    internal static float? ScorePartMaterialFromModelDoc(ModelDoc2 md)
    {
      if (md == null)
        return null;
      if (!(md is PartDoc))
        return null;

      if (!TryReadPartMaterial(md, out string materialName, out string _))
        return null;

      bool has = !string.IsNullOrWhiteSpace(materialName);
      return has ? VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore
          : VelumSolidWorksHomeostasisMetrics.PartNoMaterialScore;
    }

    internal static bool TryReadPartMaterial(
        ModelDoc2 modelDoc,
        out string materialName,
        out string databaseName)
    {
      return TryReadPartMaterialForConfig(modelDoc, string.Empty, out materialName, out databaseName);
    }

    internal static bool TryReadPartMaterialForConfig(
        ModelDoc2 modelDoc,
        string configName,
        out string materialName,
        out string databaseName)
    {
      materialName = null;
      databaseName = null;
      if (modelDoc == null || !(modelDoc is PartDoc partDoc))
        return false;

      try
      {
        // API: (ConfigName, out Database) → MaterialName.
        string configuration = configName ?? string.Empty;
        materialName = partDoc.GetMaterialPropertyName2(configuration, out databaseName);
        return true;
      }
      catch
      {
        materialName = null;
        databaseName = null;
        return false;
      }
    }

    internal static string NormalizeMaterialDatabaseForAssign(string databaseFromRead)
    {
      if (string.IsNullOrWhiteSpace(databaseFromRead))
        return string.Empty;

      string trimmed = databaseFromRead.Trim();
      string resolved = TryResolveMaterialDatabasePath(trimmed);
      if (!string.IsNullOrWhiteSpace(resolved))
        return resolved;

      if (trimmed.EndsWith(".sldmat", StringComparison.OrdinalIgnoreCase))
        return trimmed;

      return trimmed + ".sldmat";
    }

    internal static bool TryAssignPartMaterial(
        ModelDoc2 modelDoc,
        string configName,
        string materialName,
        string databaseName)
    {
      if (modelDoc == null || !(modelDoc is PartDoc partDoc))
        return false;
      if (string.IsNullOrWhiteSpace(materialName) || string.IsNullOrWhiteSpace(databaseName))
        return false;

      string trimmedName = materialName.Trim();
      string databaseForAssign = NormalizeMaterialDatabaseForAssign(databaseName);
      if (string.IsNullOrWhiteSpace(databaseForAssign))
        return false;

      foreach (string configuration in EnumerateConfigurationCandidates(modelDoc, configName))
      {
        foreach (string databaseCandidate in EnumerateDatabaseCandidates(databaseForAssign, databaseName))
        {
          if (TryAssignOnce(partDoc, modelDoc, configuration, trimmedName, databaseCandidate))
            return true;
        }
      }

      Logger.Warning(
          "Velum material COM assign failed material=\"" + trimmedName +
          "\" database=\"" + databaseForAssign + "\"");
      return false;
    }

    private static bool TryAssignOnce(
        PartDoc partDoc,
        ModelDoc2 modelDoc,
        string configuration,
        string materialName,
        string databaseForAssign)
    {
      try
      {
        // SetMaterialPropertyName2(ConfigName, Database, Name).
        partDoc.SetMaterialPropertyName2(configuration, databaseForAssign, materialName);

        try
        {
          modelDoc.EditRebuild3();
        }
        catch
        {
        }

        // Read-back по той же конфигурации, в которую писали (не по активной).
        if (!TryReadPartMaterialForConfig(
                modelDoc,
                configuration,
                out string appliedName,
                out string _))
        {
          Logger.Info(
              "Velum material COM assign attempt read-back failed config=\"" + configuration +
              "\" material=\"" + materialName + "\" database=\"" + databaseForAssign + "\"");
          return false;
        }

        if (string.IsNullOrWhiteSpace(appliedName))
        {
          Logger.Info(
              "Velum material COM assign attempt still empty config=\"" + configuration +
              "\" material=\"" + materialName + "\" database=\"" + databaseForAssign + "\"");
          return false;
        }

        if (!string.Equals(materialName, appliedName, StringComparison.OrdinalIgnoreCase))
        {
          Logger.Info(
              "Velum material COM assign attempt name mismatch requested=\"" + materialName +
              "\" actual=\"" + appliedName + "\" database=\"" + databaseForAssign + "\"");
          return false;
        }

        Logger.Info(
            "Velum material COM assign OK config=\"" + configuration +
            "\" material=\"" + appliedName + "\" database=\"" + databaseForAssign + "\"");
        return true;
      }
      catch (Exception ex)
      {
        Logger.Info(
            "Velum material COM assign attempt error config=\"" + configuration +
            "\" material=\"" + materialName + "\" database=\"" + databaseForAssign +
            "\" error=" + ex.Message);
        return false;
      }
    }

    private static IEnumerable<string> EnumerateConfigurationCandidates(ModelDoc2 modelDoc, string configName)
    {
      var candidates = new List<string>();
      var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

      // Явная конфигурация — только она (без fallback на active/"": иначе материал
      // может записаться в чужую конфигурацию после ложного read-back).
      if (!string.IsNullOrWhiteSpace(configName))
      {
        string trimmed = configName.Trim();
        if (seen.Add(trimmed))
          candidates.Add(trimmed);
        return candidates;
      }

      try
      {
        string active = modelDoc?.ConfigurationManager?.ActiveConfiguration?.Name;
        if (!string.IsNullOrWhiteSpace(active) && seen.Add(active.Trim()))
          candidates.Add(active.Trim());
      }
      catch
      {
      }

      if (seen.Add(string.Empty))
        candidates.Add(string.Empty);

      return candidates;
    }

    private static IEnumerable<string> EnumerateDatabaseCandidates(
        string primaryDatabase,
        string rawDatabaseFromLearn)
    {
      var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      var ordered = new List<string>();

      void enqueue(string candidate)
      {
        if (string.IsNullOrWhiteSpace(candidate))
          return;
        if (seen.Add(candidate))
          ordered.Add(candidate);
      }

      string baseName = StripSldmatExtension(primaryDatabase);
      if (string.IsNullOrWhiteSpace(baseName) && !string.IsNullOrWhiteSpace(rawDatabaseFromLearn))
        baseName = StripSldmatExtension(rawDatabaseFromLearn.Trim());

      foreach (string path in TryGetAllMaterialDatabasePaths())
      {
        string fileName = Path.GetFileName(path);
        if (string.Equals(path, primaryDatabase, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, primaryDatabase, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(StripSldmatExtension(fileName), baseName, StringComparison.OrdinalIgnoreCase))
          enqueue(path);
      }

      enqueue(primaryDatabase);
      enqueue(StripSldmatExtension(primaryDatabase) + ".sldmat");

      if (!string.IsNullOrWhiteSpace(rawDatabaseFromLearn))
      {
        string raw = rawDatabaseFromLearn.Trim();
        enqueue(raw);
        enqueue(StripSldmatExtension(raw) + ".sldmat");
      }

      foreach (string candidate in ordered)
        yield return candidate;
    }

    private static List<string> TryGetAllMaterialDatabasePaths()
    {
      var paths = new List<string>();
      try
      {
        ISwApplication swApp = VelumSolidEnvironmentBridge.TryGetSolidWorksApplication();
        SldWorks sw = swApp?.Sw as SldWorks;
        if (sw == null)
          return paths;

        object raw = sw.GetMaterialDatabases();
        if (!(raw is Array databases))
          return paths;

        foreach (object item in databases)
        {
          string path = item?.ToString();
          if (!string.IsNullOrWhiteSpace(path))
            paths.Add(path.Trim());
        }
      }
      catch
      {
      }

      return paths;
    }

    private static string TryResolveMaterialDatabasePath(string databaseFromRead)
    {
      if (string.IsNullOrWhiteSpace(databaseFromRead))
        return null;

      try
      {
        string trimmed = databaseFromRead.Trim();
        string withExtension = trimmed.EndsWith(".sldmat", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : trimmed + ".sldmat";
        string baseName = StripSldmatExtension(trimmed);

        foreach (string path in TryGetAllMaterialDatabasePaths())
        {
          string fileName = Path.GetFileName(path);
          if (string.Equals(path, trimmed, StringComparison.OrdinalIgnoreCase) ||
              string.Equals(path, withExtension, StringComparison.OrdinalIgnoreCase) ||
              string.Equals(fileName, withExtension, StringComparison.OrdinalIgnoreCase) ||
              string.Equals(StripSldmatExtension(fileName), baseName, StringComparison.OrdinalIgnoreCase))
            return path;
        }
      }
      catch
      {
      }

      return null;
    }

    private static string StripSldmatExtension(string value)
    {
      if (string.IsNullOrWhiteSpace(value))
        return string.Empty;

      return value.EndsWith(".sldmat", StringComparison.OrdinalIgnoreCase)
          ? value.Substring(0, value.Length - ".sldmat".Length)
          : value.Trim();
    }
  }
}
