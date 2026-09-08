using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ISIDA.Common;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Velum.ReactiveCore.Export;
using Velum.SolidHomeostasis;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Ensure живых ссылок Толщина/Длина/Ширина (конфигурации) и Прокат=Лист (document) для листовой детали.
  /// </summary>
  internal static class VelumBlankSizePropertyLinksService
  {
    internal enum EnsureStatus
    {
      SkippedNotPart,
      SkippedNotSheetMetal,
      Applied,
      Unchanged,
      Failed
    }

    private const string CutListLengthEn = "Bounding Box Length";
    private const string CutListWidthEn = "Bounding Box Width";
    private const string CutListThicknessEn = "Sheet Metal Thickness";
    private const string CutListLengthRu = "Длина граничной рамки";
    private const string CutListWidthRu = "Ширина граничной рамки";
    private const string CutListThicknessRu = "Толщина листового металла";

    private const string SwFormulaLength = "SW-Bounding Box Length";
    private const string SwFormulaWidth = "SW-Bounding Box Width";
    private const string SwFormulaThickness = "SW-Sheet Metal Thickness";

    /// <summary>
    /// Записывает или чинит ссылки по политике overwrite (<c>if_empty</c>/<c>repair</c>/<c>always</c>/<c>never</c>).
    /// По умолчанию пишет во все пользовательские конфигурации (вкладка конфигурации), не в document.
    /// </summary>
    internal static EnsureStatus TryEnsure(
        ModelDoc2 modelDoc,
        string overwrite,
        out string message)
    {
      return TryEnsure(modelDoc, overwrite, null, out message);
    }

    /// <summary>
    /// Записывает или чинит ссылки; целевые конфигурации задаёт <paramref name="configParameter"/>.
    /// </summary>
    /// <param name="modelDoc">Деталь SolidWorks.</param>
    /// <param name="overwrite">Политика: <c>if_empty</c>/<c>repair</c>/<c>always</c>/<c>never</c>.</param>
    /// <param name="configParameter">
    /// <c>null</c>/пусто — все конфиги DXF; <c>active</c> — активная; <c>document</c> — главная вкладка.
    /// </param>
    /// <param name="message">Краткий итог для лога.</param>
    internal static EnsureStatus TryEnsure(
        ModelDoc2 modelDoc,
        string overwrite,
        string configParameter,
        out string message)
    {
      message = string.Empty;
      if (modelDoc == null || modelDoc.GetType() != (int)swDocumentTypes_e.swDocPART)
      {
        message = "skipped_not_part";
        return EnsureStatus.SkippedNotPart;
      }

      if (!VelumSolidSheetMetalHelper.IsSheetMetalPart(modelDoc) ||
          VelumSolidSheetMetalHelper.TryFindFlatPatternFeature(modelDoc) == null)
      {
        message = "skipped_not_sheet_metal";
        return EnsureStatus.SkippedNotSheetMetal;
      }

      string mode = NormalizeOverwrite(overwrite);
      if (string.Equals(mode, "never", StringComparison.OrdinalIgnoreCase))
      {
        message = "overwrite_never";
        return EnsureStatus.Unchanged;
      }

      if (!TryEnsureSheetMetalCutList(modelDoc, out Feature cutListFeat, out string cutListError))
      {
        message = string.IsNullOrWhiteSpace(cutListError)
            ? "cut_list_unavailable"
            : cutListError;
        return EnsureStatus.Failed;
      }

      if (!TryBuildLinkValues(
              modelDoc,
              cutListFeat,
              out string thicknessLink,
              out string lengthLink,
              out string widthLink,
              out string buildError))
      {
        message = string.IsNullOrWhiteSpace(buildError) ? "link_build_failed" : buildError;
        return EnsureStatus.Failed;
      }

      IReadOnlyList<string> writeConfigs = ResolveWriteConfigurationKeys(modelDoc, configParameter);
      if (writeConfigs.Count == 0)
      {
        message = "no_write_configs";
        return EnsureStatus.Failed;
      }

      bool anyWritten = false;
      bool anyFailed = false;
      var log = new StringBuilder();

      VelumExportDocumentationGeometryStampHelper.RunWithGeometryPendingStampSyncSuppressed(() =>
      {
        bool writeToDocumentOnly =
            writeConfigs.Count == 1 && string.IsNullOrEmpty(writeConfigs[0]);
        if (!writeToDocumentOnly)
          TryClearDocumentBlankSizeProperties(modelDoc, log);

        // Главная вкладка документа: Прокат = Лист (только листовые).
        CustomPropertyManager docCpm = TryGetConfigurationPropertyManager(modelDoc, string.Empty);
        if (docCpm != null)
        {
          log.Append("[document] ");
          if (!TryWriteLiteralPropertyIfNeeded(
                  docCpm,
                  VelumBlankSizeProperties.RolledStock,
                  VelumBlankSizeProperties.RolledStockSheetValue,
                  mode,
                  ref anyWritten,
                  ref anyFailed,
                  log))
            return;
        }
        else
        {
          anyFailed = true;
          log.Append("cpm_fail:document; ");
        }

        for (int i = 0; i < writeConfigs.Count; i++)
        {
          string configKey = writeConfigs[i];
          CustomPropertyManager cpm = TryGetConfigurationPropertyManager(modelDoc, configKey);
          if (cpm == null)
          {
            anyFailed = true;
            log.Append("cpm_fail:").Append(configKey).Append("; ");
            continue;
          }

          string scopeTag = string.IsNullOrEmpty(configKey) ? "document" : configKey;
          log.Append('[').Append(scopeTag).Append("] ");

          if (!TryWritePropertyIfNeeded(
                  cpm,
                  VelumBlankSizeProperties.Thickness,
                  thicknessLink,
                  mode,
                  ref anyWritten,
                  ref anyFailed,
                  log))
            return;

          if (!TryWritePropertyIfNeeded(
                  cpm,
                  VelumBlankSizeProperties.Length,
                  lengthLink,
                  mode,
                  ref anyWritten,
                  ref anyFailed,
                  log))
            return;

          TryWritePropertyIfNeeded(
              cpm,
              VelumBlankSizeProperties.Width,
              widthLink,
              mode,
              ref anyWritten,
              ref anyFailed,
              log);
        }

        // Rebuild внутри suppress: иначе GetUpdateStamp/pending → ложный DXF IsOutdated.
        if (anyWritten && !anyFailed)
          TryForceRebuild(modelDoc);

        if (anyWritten && !anyFailed)
          TryResyncDxfStampsAfterPropertyWrite(modelDoc);
      });

      if (anyFailed)
      {
        message = log.Length > 0 ? log.ToString() : "write_failed";
        return EnsureStatus.Failed;
      }

      if (anyWritten)
      {
        VelumSolidProbeRefreshPlanner.MarkExportDocumentationStale();
        message = log.Length > 0 ? log.ToString() : "applied";
        return EnsureStatus.Applied;
      }

      message = "unchanged";
      return EnsureStatus.Unchanged;
    }

    /// <summary>
    /// Ключи CPM для записи: только именованные пользовательские конфигурации.
    /// Пустой ключ (document / главная вкладка) — только при явном <c>config=document</c>.
    /// </summary>
    private static IReadOnlyList<string> ResolveWriteConfigurationKeys(
        ModelDoc2 modelDoc,
        string overwriteConfigHint)
    {
      string hint = (overwriteConfigHint ?? string.Empty).Trim();
      if (string.Equals(hint, "document", StringComparison.OrdinalIgnoreCase))
        return new[] { string.Empty };

      if (string.Equals(hint, "active", StringComparison.OrdinalIgnoreCase))
      {
        string active = VelumRecipeSolidWorksCustomProperties.ResolveConfigurationKey(modelDoc, "active");
        if (!string.IsNullOrWhiteSpace(active))
          return new[] { active };
        return new string[0];
      }

      var result = new List<string>();
      IReadOnlyList<string> configs = VelumDxfArtifactResolver.TryGetConfigurationNames(modelDoc);
      if (configs != null)
      {
        for (int i = 0; i < configs.Count; i++)
        {
          string name = (configs[i] ?? string.Empty).Trim();
          // "" = document CPM — не писать габарит на главную вкладку.
          if (name.Length == 0)
            continue;
          result.Add(name);
        }
      }

      if (result.Count > 0)
        return result;

      string fallback = VelumRecipeSolidWorksCustomProperties.ResolveConfigurationKey(modelDoc, "active");
      if (!string.IsNullOrWhiteSpace(fallback))
        return new[] { fallback };

      return new string[0];
    }

    /// <summary>
    /// Удаляет Толщина/Длина/Ширина с главной вкладки документа, если пишем в конфигурации.
    /// </summary>
    private static void TryClearDocumentBlankSizeProperties(ModelDoc2 modelDoc, StringBuilder log)
    {
      CustomPropertyManager docCpm = TryGetConfigurationPropertyManager(modelDoc, string.Empty);
      if (docCpm == null)
        return;

      bool cleared = false;
      cleared |= TryDeletePropertyIfExists(docCpm, VelumBlankSizeProperties.Thickness);
      cleared |= TryDeletePropertyIfExists(docCpm, VelumBlankSizeProperties.Length);
      cleared |= TryDeletePropertyIfExists(docCpm, VelumBlankSizeProperties.Width);
      if (cleared && log != null)
        log.Append("[document] cleared blank-size; ");
    }

    private static bool TryDeletePropertyIfExists(CustomPropertyManager cpm, string propertyName)
    {
      if (cpm == null || string.IsNullOrWhiteSpace(propertyName))
        return false;

      try
      {
        if (!VelumRecipeSolidWorksCustomProperties.TryPropertyExists(cpm, propertyName))
          return false;

        cpm.Delete2(propertyName);
        return true;
      }
      catch
      {
        try
        {
          cpm.Delete(propertyName);
          return true;
        }
        catch
        {
          return false;
        }
      }
    }

    private static CustomPropertyManager TryGetConfigurationPropertyManager(
        ModelDoc2 modelDoc,
        string configurationName)
    {
      if (modelDoc?.Extension == null)
        return null;

      try
      {
        string key = configurationName ?? string.Empty;
        return modelDoc.Extension.CustomPropertyManager[key];
      }
      catch
      {
        return null;
      }
    }

    private static void TryResyncDxfStampsAfterPropertyWrite(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return;

      try
      {
        IReadOnlyList<string> configs = VelumDxfArtifactResolver.TryGetConfigurationNames(modelDoc);
        if (configs == null || configs.Count == 0)
          return;

        VelumExportDocumentationGeometryStampHelper.TryResyncPerConfigDxfGeometryStamps(
            modelDoc,
            configs);
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum blank-size DXF stamp resync: " + ex.Message);
      }
    }

    /// <summary>
    /// Проверка валидности ссылок L/W/T (активная конфигурация) и Прокат=Лист (document).
    /// </summary>
    internal static bool TryEvaluateLinksValidity(
        ModelDoc2 modelDoc,
        out bool allValid,
        out string detail)
    {
      allValid = false;
      detail = string.Empty;
      if (modelDoc == null)
        return false;

      CustomPropertyManager configCpm = VelumRecipeSolidWorksCustomProperties.TryGetManager(modelDoc, "active");
      if (configCpm == null)
        configCpm = VelumRecipeSolidWorksCustomProperties.TryGetManager(modelDoc, "document");
      if (configCpm == null)
      {
        detail = "CPM недоступен";
        return false;
      }

      var problems = new List<string>();
      if (!IsPropertyLinkValid(configCpm, VelumBlankSizeProperties.Thickness, out string tProblem))
        problems.Add(tProblem);
      if (!IsPropertyLinkValid(configCpm, VelumBlankSizeProperties.Length, out string lProblem))
        problems.Add(lProblem);
      if (!IsPropertyLinkValid(configCpm, VelumBlankSizeProperties.Width, out string wProblem))
        problems.Add(wProblem);

      CustomPropertyManager docCpm = TryGetConfigurationPropertyManager(modelDoc, string.Empty);
      if (docCpm == null)
        problems.Add(VelumBlankSizeProperties.RolledStock + ": CPM документа недоступен");
      else if (!IsLiteralPropertyValid(
                   docCpm,
                   VelumBlankSizeProperties.RolledStock,
                   VelumBlankSizeProperties.RolledStockSheetValue,
                   out string rolledProblem))
        problems.Add(rolledProblem);

      if (problems.Count > 0)
      {
        detail = string.Join("; ", problems);
        allValid = false;
        return true;
      }

      allValid = true;
      return true;
    }

    private static bool TryWriteLiteralPropertyIfNeeded(
        CustomPropertyManager cpm,
        string propertyName,
        string literalValue,
        string mode,
        ref bool anyWritten,
        ref bool anyFailed,
        StringBuilder log)
    {
      if (!ShouldWriteLiteralProperty(cpm, propertyName, literalValue, mode, out string reason))
      {
        if (!string.IsNullOrWhiteSpace(reason))
          log.Append(propertyName).Append('=').Append(reason).Append("; ");
        return true;
      }

      if (!VelumRecipeSolidWorksCustomProperties.TrySetValue(
              cpm,
              propertyName,
              literalValue,
              "always",
              "text",
              out _,
              out string setMessage))
      {
        anyFailed = true;
        log.Append(propertyName).Append("=fail:").Append(setMessage).Append("; ");
        return false;
      }

      anyWritten = true;
      log.Append(propertyName).Append("=written; ");
      return true;
    }

    private static bool ShouldWriteLiteralProperty(
        CustomPropertyManager cpm,
        string propertyName,
        string expectedValue,
        string mode,
        out string skipReason)
    {
      skipReason = string.Empty;
      if (string.IsNullOrWhiteSpace(expectedValue))
      {
        skipReason = "empty_literal";
        return false;
      }

      if (string.Equals(mode, "always", StringComparison.OrdinalIgnoreCase))
        return true;

      bool exists = VelumRecipeSolidWorksCustomProperties.TryGetRawAndResolved(
          cpm,
          propertyName,
          out string raw,
          out string resolved);

      string current = !string.IsNullOrWhiteSpace(resolved) ? resolved.Trim() : (raw ?? string.Empty).Trim();
      bool empty = !exists || string.IsNullOrWhiteSpace(current);
      bool matches = string.Equals(current, expectedValue.Trim(), StringComparison.OrdinalIgnoreCase);

      if (string.Equals(mode, "if_empty", StringComparison.OrdinalIgnoreCase))
      {
        if (!empty)
        {
          skipReason = "if_empty_skip";
          return false;
        }

        return true;
      }

      if (string.Equals(mode, "repair", StringComparison.OrdinalIgnoreCase))
      {
        if (matches)
        {
          skipReason = "repair_ok";
          return false;
        }

        return true;
      }

      skipReason = "unknown_mode";
      return false;
    }

    private static bool IsLiteralPropertyValid(
        CustomPropertyManager cpm,
        string propertyName,
        string expectedValue,
        out string problem)
    {
      problem = string.Empty;
      if (!VelumRecipeSolidWorksCustomProperties.TryGetRawAndResolved(
              cpm,
              propertyName,
              out string raw,
              out string resolved))
      {
        problem = propertyName + ": отсутствует";
        return false;
      }

      string current = !string.IsNullOrWhiteSpace(resolved) ? resolved.Trim() : (raw ?? string.Empty).Trim();
      if (!string.Equals(current, expectedValue.Trim(), StringComparison.OrdinalIgnoreCase))
      {
        problem = propertyName + ": ожидалось \"" + expectedValue + "\"";
        return false;
      }

      return true;
    }

    private static bool TryWritePropertyIfNeeded(
        CustomPropertyManager cpm,
        string propertyName,
        string linkValue,
        string mode,
        ref bool anyWritten,
        ref bool anyFailed,
        StringBuilder log)
    {
      if (!ShouldWriteProperty(cpm, propertyName, linkValue, mode, out string reason))
      {
        if (!string.IsNullOrWhiteSpace(reason))
          log.Append(propertyName).Append('=').Append(reason).Append("; ");
        return true;
      }

      if (!VelumRecipeSolidWorksCustomProperties.TrySetValue(
              cpm,
              propertyName,
              linkValue,
              "always",
              "text",
              out _,
              out string setMessage))
      {
        anyFailed = true;
        log.Append(propertyName).Append("=fail:").Append(setMessage).Append("; ");
        return false;
      }

      anyWritten = true;
      log.Append(propertyName).Append("=written; ");
      return true;
    }

    private static bool ShouldWriteProperty(
        CustomPropertyManager cpm,
        string propertyName,
        string linkValue,
        string mode,
        out string skipReason)
    {
      skipReason = string.Empty;
      if (string.IsNullOrWhiteSpace(linkValue))
      {
        skipReason = "empty_link";
        return false;
      }

      if (string.Equals(mode, "always", StringComparison.OrdinalIgnoreCase))
        return true;

      bool exists = VelumRecipeSolidWorksCustomProperties.TryGetRawAndResolved(
          cpm,
          propertyName,
          out string raw,
          out string resolved);

      bool empty = !exists || IsEffectivelyEmpty(raw) && IsEffectivelyEmpty(resolved);
      bool validLink = exists && IsValidLink(raw, resolved);

      if (string.Equals(mode, "if_empty", StringComparison.OrdinalIgnoreCase))
      {
        if (!empty)
        {
          skipReason = "if_empty_skip";
          return false;
        }

        return true;
      }

      if (string.Equals(mode, "repair", StringComparison.OrdinalIgnoreCase))
      {
        if (validLink)
        {
          skipReason = "repair_ok";
          return false;
        }

        return true;
      }

      skipReason = "unknown_mode";
      return false;
    }

    private static bool IsPropertyLinkValid(
        CustomPropertyManager cpm,
        string propertyName,
        out string problem)
    {
      problem = string.Empty;
      if (!VelumRecipeSolidWorksCustomProperties.TryGetRawAndResolved(
              cpm,
              propertyName,
              out string raw,
              out string resolved))
      {
        problem = propertyName + ": отсутствует";
        return false;
      }

      if (!IsValidLink(raw, resolved))
      {
        problem = propertyName + ": пусто или невалидно";
        return false;
      }

      return true;
    }

    private static bool IsValidLink(string raw, string resolved)
    {
      if (IsEffectivelyEmpty(raw) && IsEffectivelyEmpty(resolved))
        return false;

      string source = !IsEffectivelyEmpty(raw) ? raw.Trim() : string.Empty;
      if (source.Length > 0 && !LooksLikeLink(source))
        return false;

      if (!TryParsePositiveNumber(resolved, out _))
        return false;

      return true;
    }

    private static bool LooksLikeLink(string raw)
    {
      string t = (raw ?? string.Empty).Trim();
      if (t.Length == 0)
        return false;

      if (t.IndexOf('@') >= 0)
        return true;

      if (t.StartsWith("\"", StringComparison.Ordinal) &&
          t.IndexOf("SW-", StringComparison.OrdinalIgnoreCase) >= 0)
        return true;

      return false;
    }

    private static bool IsEffectivelyEmpty(string value)
    {
      if (string.IsNullOrWhiteSpace(value))
        return true;

      string t = value.Trim();
      return string.Equals(t, "-", StringComparison.Ordinal)
          || string.Equals(t, "—", StringComparison.Ordinal);
    }

    private static bool TryParsePositiveNumber(string text, out double value)
    {
      value = 0;
      if (string.IsNullOrWhiteSpace(text))
        return false;

      string t = text.Trim();
      var sb = new StringBuilder(t.Length);
      bool seenDigit = false;
      bool seenDot = false;
      for (int i = 0; i < t.Length; i++)
      {
        char c = t[i];
        if (c >= '0' && c <= '9')
        {
          sb.Append(c);
          seenDigit = true;
          continue;
        }

        if ((c == '.' || c == ',') && !seenDot)
        {
          sb.Append('.');
          seenDot = true;
          continue;
        }

        if ((c == '-' || c == '+') && sb.Length == 0)
        {
          sb.Append(c);
          continue;
        }

        // хвост единиц: mm, мм, in …
        if (seenDigit)
          break;
      }

      if (!seenDigit)
        return false;

      if (!double.TryParse(sb.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        return false;

      return value > 0;
    }

    private static string DescribeCutListPropertyNames(CustomPropertyManager cutCpm)
    {
      try
      {
        string[] names = cutCpm?.GetNames() as string[];
        if (names == null || names.Length == 0)
          return "cut-list props: (none)";

        var sb = new StringBuilder("cut-list props: ");
        int limit = Math.Min(names.Length, 20);
        for (int i = 0; i < limit; i++)
        {
          if (i > 0)
            sb.Append(", ");
          sb.Append(names[i]);
        }

        if (names.Length > limit)
          sb.Append(", …");
        return sb.ToString();
      }
      catch
      {
        return "cut-list props: (error)";
      }
    }

    private static bool TryBuildLinkValues(
        ModelDoc2 modelDoc,
        Feature cutListFeat,
        out string thicknessLink,
        out string lengthLink,
        out string widthLink,
        out string error)
    {
      thicknessLink = null;
      lengthLink = null;
      widthLink = null;
      error = string.Empty;

      string fileName = TryGetPartFileNameWithExtension(modelDoc);
      string cutListName = (cutListFeat?.Name ?? string.Empty).Trim();
      if (string.IsNullOrWhiteSpace(cutListName))
      {
        error = "cut_list_name_empty";
        return false;
      }

      if (string.IsNullOrWhiteSpace(fileName))
      {
        error = "part_not_saved";
        return false;
      }

      CustomPropertyManager cutCpm = cutListFeat.CustomPropertyManager;
      if (cutCpm == null)
      {
        error = "cut_list_cpm_unavailable";
        return false;
      }

      if (!TryResolveThicknessLink(
              modelDoc,
              cutCpm,
              cutListName,
              fileName,
              out thicknessLink,
              out string thicknessError))
      {
        error = thicknessError;
        return false;
      }

      string lengthPropName = ResolveCutListPropertyName(
          cutCpm,
          new[] { CutListLengthRu, CutListLengthEn });
      string widthPropName = ResolveCutListPropertyName(
          cutCpm,
          new[] { CutListWidthRu, CutListWidthEn });

      if (string.IsNullOrWhiteSpace(lengthPropName) || string.IsNullOrWhiteSpace(widthPropName))
      {
        error = "cut_list_bbox_names_missing " + DescribeCutListPropertyNames(cutCpm);
        return false;
      }

      lengthLink = BuildPreferredCutListLink(
          cutCpm,
          lengthPropName,
          SwFormulaLength,
          cutListName,
          fileName,
          out string lengthDiag);
      widthLink = BuildPreferredCutListLink(
          cutCpm,
          widthPropName,
          SwFormulaWidth,
          cutListName,
          fileName,
          out string widthDiag);

      double lengthValue;
      double widthValue;
      bool haveLength = TryReadCutListResolved(cutCpm, new[] { lengthPropName }, out lengthValue);
      bool haveWidth = TryReadCutListResolved(cutCpm, new[] { widthPropName }, out widthValue);

      if (haveLength && haveWidth && widthValue > lengthValue)
      {
        string swap = lengthLink;
        lengthLink = widthLink;
        widthLink = swap;
      }
      else if (!haveLength || !haveWidth)
      {
        Logger.Info(
            "Velum blank-size: cut-list bbox numbers unresolved; using named Length/Width mapping. " +
            lengthDiag + "; " + widthDiag + "; " + DescribeCutListPropertyNames(cutCpm));
      }

      return true;
    }

    private static string ResolveCutListPropertyName(CustomPropertyManager cutCpm, string[] candidates)
    {
      if (cutCpm == null || candidates == null)
        return null;

      try
      {
        string[] names = cutCpm.GetNames() as string[];
        if (names != null)
        {
          for (int c = 0; c < candidates.Length; c++)
          {
            string wanted = candidates[c];
            for (int i = 0; i < names.Length; i++)
            {
              if (string.Equals(names[i], wanted, StringComparison.OrdinalIgnoreCase))
                return names[i];
            }
          }

          for (int c = 0; c < candidates.Length; c++)
          {
            string wanted = candidates[c];
            for (int i = 0; i < names.Length; i++)
            {
              string n = names[i] ?? string.Empty;
              if (n.IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0)
                return names[i];
            }
          }
        }
      }
      catch
      {
      }

      return candidates.Length > 0 ? candidates[0] : null;
    }

    /// <summary>
    /// Предпочитает сырое значение cut-list (как CodeStack), иначе формулу на локализованное имя,
    /// иначе <c>SW-Bounding Box …</c>.
    /// </summary>
    private static string BuildPreferredCutListLink(
        CustomPropertyManager cutCpm,
        string cutListPropertyName,
        string swEnglishFormulaName,
        string cutListName,
        string fileName,
        out string diagnostic)
    {
      diagnostic = cutListPropertyName ?? string.Empty;
      string raw;
      string resolved;
      if (TryReadCutListRawAndResolved(cutCpm, cutListPropertyName, out raw, out resolved))
      {
        diagnostic = cutListPropertyName + " raw=\"" + TruncateForLog(raw) + "\" res=\"" +
                     TruncateForLog(resolved) + "\"";
        if (LooksLikeLink(raw))
          return raw.Trim();
      }

      // Локализованное имя свойства cut-list (русская SW).
      string localizedFormula = BuildCutListFormula(cutListPropertyName, cutListName, fileName);
      if (!string.IsNullOrWhiteSpace(localizedFormula))
        return localizedFormula;

      return BuildCutListFormula(swEnglishFormulaName, cutListName, fileName);
    }

    private static string TruncateForLog(string value)
    {
      string t = value ?? string.Empty;
      if (t.Length <= 80)
        return t;
      return t.Substring(0, 80) + "…";
    }

    private static bool TryResolveThicknessLink(
        ModelDoc2 modelDoc,
        CustomPropertyManager cutCpm,
        string cutListName,
        string fileName,
        out string thicknessLink,
        out string error)
    {
      thicknessLink = null;
      error = string.Empty;

      Feature sheetMetal = TryFindSheetMetalFeature(modelDoc);
      if (sheetMetal != null)
      {
        string featureName = (sheetMetal.Name ?? string.Empty).Trim();
        if (featureName.Length > 0 && !string.IsNullOrWhiteSpace(fileName))
        {
          // Русская SW: "Толщина@Листовой металл1@Part.SLDPRT" (не Thickness@… без файла).
          thicknessLink = "\"" + "Толщина@" + featureName + "@" + fileName + "\"";
          return true;
        }
      }

      string thicknessPropName = ResolveCutListPropertyName(
          cutCpm,
          new[] { CutListThicknessRu, CutListThicknessEn });
      if (!string.IsNullOrWhiteSpace(thicknessPropName))
      {
        thicknessLink = BuildPreferredCutListLink(
            cutCpm,
            thicknessPropName,
            SwFormulaThickness,
            cutListName,
            fileName,
            out _);
        if (!string.IsNullOrWhiteSpace(thicknessLink))
          return true;
      }

      thicknessLink = BuildCutListFormula(SwFormulaThickness, cutListName, fileName);
      if (string.IsNullOrWhiteSpace(thicknessLink))
      {
        error = "thickness_link_unavailable";
        return false;
      }

      return true;
    }

    private static string BuildCutListFormula(string swPropertyName, string cutListName, string fileName)
    {
      return "\"" + swPropertyName + "@@@" + cutListName + "@" + fileName + "\"";
    }

    private static string TryGetPartFileNameWithExtension(ModelDoc2 modelDoc)
    {
      try
      {
        string path = modelDoc?.GetPathName();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
          return null;

        return Path.GetFileName(path);
      }
      catch
      {
        return null;
      }
    }

    private static Feature TryFindSheetMetalFeature(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return null;

      try
      {
        Feature feature = modelDoc.FirstFeature() as Feature;
        while (feature != null)
        {
          string typeName = TryGetFeatureTypeName(feature);
          if (string.Equals(typeName, "SheetMetal", StringComparison.OrdinalIgnoreCase))
            return feature;

          feature = feature.GetNextFeature() as Feature;
        }
      }
      catch
      {
      }

      return null;
    }

    private static bool TryEnsureSheetMetalCutList(
        ModelDoc2 modelDoc,
        out Feature cutListFeat,
        out string error)
    {
      cutListFeat = null;
      error = string.Empty;
      try
      {
        TryForceRebuild(modelDoc);
        TryUpdateAutomaticCutLists(modelDoc);
        TryForceRebuild(modelDoc);

        List<Feature> sheetMetalCutLists = CollectSheetMetalCutLists(modelDoc);
        if (sheetMetalCutLists.Count == 0)
        {
          error = "no_sheet_metal_cut_list";
          return false;
        }

        cutListFeat = sheetMetalCutLists[0];
        return true;
      }
      catch (Exception ex)
      {
        error = ex.Message;
        Logger.Warning("Velum blank-size cut list: " + ex.Message);
        return false;
      }
    }

    private static void TryForceRebuild(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return;

      try
      {
        modelDoc.ForceRebuild3(false);
      }
      catch
      {
        try
        {
          modelDoc.EditRebuild3();
        }
        catch
        {
        }
      }
    }

    private static void TryUpdateAutomaticCutLists(ModelDoc2 modelDoc)
    {
      Feature feature = modelDoc.FirstFeature() as Feature;
      while (feature != null)
      {
        TryUpdateSolidBodyFolderCutList(feature);
        TraverseUpdateCutListSubFeatures(feature);
        feature = feature.GetNextFeature() as Feature;
      }
    }

    private static void TryUpdateSolidBodyFolderCutList(Feature feature)
    {
      if (feature == null)
        return;

      string typeName = TryGetFeatureTypeName(feature);
      if (!string.Equals(typeName, "SolidBodyFolder", StringComparison.OrdinalIgnoreCase))
        return;

      try
      {
        BodyFolder bodyFolder = feature.GetSpecificFeature2() as BodyFolder;
        if (bodyFolder == null)
          return;

        bodyFolder.SetAutomaticCutList(true);
        bodyFolder.SetAutomaticUpdate(true);
        bodyFolder.UpdateCutList();
      }
      catch
      {
      }
    }

    private static void TraverseUpdateCutListSubFeatures(Feature parent)
    {
      try
      {
        Feature child = parent.GetFirstSubFeature() as Feature;
        while (child != null)
        {
          TryUpdateSolidBodyFolderCutList(child);
          TraverseUpdateCutListSubFeatures(child);
          child = child.GetNextSubFeature() as Feature;
        }
      }
      catch
      {
      }
    }

    private static List<Feature> CollectSheetMetalCutLists(ModelDoc2 modelDoc)
    {
      var result = new List<Feature>();
      Feature feature = modelDoc.FirstFeature() as Feature;
      while (feature != null)
      {
        CollectCutListFromFeature(feature, result);
        CollectCutListFromSubFeatures(feature, result);
        feature = feature.GetNextFeature() as Feature;
      }

      return result;
    }

    private static void CollectCutListFromSubFeatures(Feature parent, List<Feature> result)
    {
      try
      {
        Feature child = parent.GetFirstSubFeature() as Feature;
        while (child != null)
        {
          CollectCutListFromFeature(child, result);
          CollectCutListFromSubFeatures(child, result);
          child = child.GetNextSubFeature() as Feature;
        }
      }
      catch
      {
      }
    }

    private static void CollectCutListFromFeature(Feature feature, List<Feature> result)
    {
      if (feature == null)
        return;

      string typeName = TryGetFeatureTypeName(feature);
      if (!string.Equals(typeName, "CutListFolder", StringComparison.OrdinalIgnoreCase))
        return;

      try
      {
        BodyFolder bodyFolder = feature.GetSpecificFeature2() as BodyFolder;
        if (bodyFolder == null)
          return;

        object bodiesObj = bodyFolder.GetBodies();
        object[] bodies = bodiesObj as object[];
        if (bodies == null || bodies.Length == 0)
          return;

        Body2 body = bodies[0] as Body2;
        if (body == null || !body.IsSheetMetal())
          return;

        for (int i = 0; i < result.Count; i++)
        {
          if (ReferenceEquals(result[i], feature))
            return;
        }

        result.Add(feature);
      }
      catch
      {
      }
    }

    private static bool TryReadCutListResolved(
        CustomPropertyManager cutCpm,
        string[] candidateNames,
        out double value)
    {
      value = 0;
      if (cutCpm == null || candidateNames == null)
        return false;

      for (int i = 0; i < candidateNames.Length; i++)
      {
        if (TryReadCutListPropertyResolved(cutCpm, candidateNames[i], out value))
          return true;
      }

      // Fallback: scan names for substring match (локализованные имена).
      try
      {
        string[] names = cutCpm.GetNames() as string[];
        if (names != null)
        {
          for (int i = 0; i < names.Length; i++)
          {
            string n = names[i] ?? string.Empty;
            for (int c = 0; c < candidateNames.Length; c++)
            {
              if (n.IndexOf(candidateNames[c], StringComparison.OrdinalIgnoreCase) < 0)
                continue;

              if (TryReadCutListPropertyResolved(cutCpm, n, out value))
                return true;
            }
          }
        }
      }
      catch
      {
      }

      return false;
    }

    /// <summary>
    /// Читает cut-list свойство по имени напрямую (без обязательного GetNames) —
    /// служебные Bounding Box Length/Width иногда не видны в GetNames до первого Get.
    /// </summary>
    private static bool TryReadCutListPropertyResolved(
        CustomPropertyManager cutCpm,
        string propertyName,
        out double value)
    {
      value = 0;
      if (!TryReadCutListRawAndResolved(cutCpm, propertyName, out _, out string resolved))
        return false;

      return TryParsePositiveNumber(resolved, out value);
    }

    private static bool TryReadCutListRawAndResolved(
        CustomPropertyManager cutCpm,
        string propertyName,
        out string raw,
        out string resolved)
    {
      raw = string.Empty;
      resolved = string.Empty;
      if (cutCpm == null || string.IsNullOrWhiteSpace(propertyName))
        return false;

      try
      {
        string valOut;
        string resolvedValOut;
        cutCpm.Get2(propertyName, out valOut, out resolvedValOut);
        raw = valOut ?? string.Empty;
        resolved = resolvedValOut ?? string.Empty;
        if (string.IsNullOrEmpty(resolved) && !string.IsNullOrEmpty(raw))
          resolved = raw;
        return true;
      }
      catch
      {
      }

      if (VelumRecipeSolidWorksCustomProperties.TryGetRawAndResolved(
              cutCpm,
              propertyName,
              out raw,
              out resolved))
        return true;

      return false;
    }

    private static string TryGetFeatureTypeName(Feature feature)
    {
      if (feature == null)
        return string.Empty;

      try
      {
        string type2 = feature.GetTypeName2();
        if (!string.IsNullOrWhiteSpace(type2))
          return type2;
      }
      catch
      {
      }

      try
      {
        return feature.GetTypeName() ?? string.Empty;
      }
      catch
      {
        return string.Empty;
      }
    }

    private static string NormalizeOverwrite(string overwrite)
    {
      string mode = (overwrite ?? "repair").Trim();
      if (mode.Length == 0)
        return "repair";
      return mode;
    }
  }
}
