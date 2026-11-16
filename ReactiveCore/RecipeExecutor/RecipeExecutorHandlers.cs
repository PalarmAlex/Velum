using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ISIDA.Common;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Velum.Configuration;
using Velum.SolidHomeostasis;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Реализации handler'ов шагов <c>invoke</c> для SolidWorks.
  /// </summary>
  internal static class RecipeExecutorHandlers
  {
    public static bool TryExecuteSaveFileName(
        int index,
        IReadOnlyDictionary<string, string> parameters,
        ModelDoc2 modelDoc,
        SldWorks sw,
        IReadOnlyDictionary<string, string> templateContext,
        out RecipeStepExecutionResult result)
    {
      result = null;
      if (modelDoc == null)
      {
        result = new RecipeStepExecutionResult(index, "invoke", false, false, "model_doc_null");
        return false;
      }

      VelumSolidDocumentKind activeKind = VelumFileNameSequenceState.ClassifyDocumentKind(modelDoc);
      // Чертежи получают имя от исходного документа — автоподстановка КБ не применяется.
      if (activeKind == VelumSolidDocumentKind.Drawing)
      {
        result = new RecipeStepExecutionResult(index, "invoke", true, true, "skipped_drawing");
        Logger.Info("Velum Recipe save_file_name skipped (drawing)");
        return true;
      }

      if (!VelumFileNameSequenceState.IsReady)
      {
        result = new RecipeStepExecutionResult(index, "invoke", true, true, "skipped_empty_state");
        Logger.Info("Velum Recipe save_file_name skipped (empty sequence state)");
        return true;
      }

      if (activeKind != VelumSolidDocumentKind.Part &&
          activeKind != VelumSolidDocumentKind.Assembly)
      {
        result = new RecipeStepExecutionResult(index, "invoke", true, true, "skipped_unsupported_document_kind");
        Logger.Info("Velum Recipe save_file_name skipped (unsupported document kind)");
        return true;
      }

      if (!VelumFileNameSequenceState.IsReadyForDocumentKind(activeKind))
      {
        result = new RecipeStepExecutionResult(index, "invoke", true, true, "skipped_kind_mismatch");
        Logger.Info("Velum Recipe save_file_name skipped (document kind mismatch)");
        return true;
      }

      if (VelumFileNameSequenceState.WasAutoNameAppliedToDocument(modelDoc))
      {
        result = new RecipeStepExecutionResult(index, "invoke", true, true, "skipped_already_served");
        Logger.Info("Velum Recipe save_file_name skipped (already served for this document)");
        return true;
      }

      if (!VelumFileNameSequenceState.TryResolveNextBaseName(activeKind, out string baseName, out string resolvedSuffix) ||
          string.IsNullOrWhiteSpace(baseName))
      {
        result = new RecipeStepExecutionResult(index, "invoke", true, true, "skipped_name_collision");
        Logger.Info("Velum Recipe save_file_name skipped (unique name not found in directory)");
        return true;
      }

      if (VelumFileNameSequenceState.DocumentBaseNameMatches(modelDoc, baseName))
      {
        VelumFileNameSequenceState.CommitSuffix(activeKind, resolvedSuffix);
        VelumFileNameSequenceState.MarkAutoNameAppliedToDocument(modelDoc);
        result = new RecipeStepExecutionResult(index, "invoke", true, true, "skipped_already_applied");
        Logger.Info("Velum Recipe save_file_name skipped (name already applied) value=\"" + baseName + "\"");
        return true;
      }

      string extension = VelumSolidWorksSaveFileNameHelper.TryGetDefaultExtension(modelDoc);
      bool ok = VelumSolidWorksSaveFileNameHelper.TrySetSuggestedSaveFileName(
          modelDoc,
          sw,
          baseName,
          extension);

      if (ok)
      {
        VelumFileNameSequenceState.CommitSuffix(activeKind, resolvedSuffix);
        VelumFileNameSequenceState.MarkAutoNameAppliedToDocument(modelDoc);
      }

      string fileNameWithExtension = baseName + extension;
      result = new RecipeStepExecutionResult(
          index,
          "invoke",
          ok,
          false,
          ok ? "value=" + fileNameWithExtension : "set_save_file_name_failed");

      if (ok)
      {
        Logger.Info("Velum Recipe save_file_name OK value=\"" + fileNameWithExtension + "\"");
      }
      else
      {
        Logger.Warning("Velum Recipe save_file_name FAIL value=\"" + fileNameWithExtension + "\"");
      }

      return ok;
    }

    public static bool TryExecuteAssignSessionMaterial(
        int index,
        IReadOnlyDictionary<string, string> parameters,
        ModelDoc2 modelDoc,
        out RecipeStepExecutionResult result)
    {
      result = null;
      if (modelDoc == null)
      {
        result = new RecipeStepExecutionResult(index, "invoke", false, false, "model_doc_null");
        return false;
      }

      if (!(modelDoc is PartDoc))
      {
        result = new RecipeStepExecutionResult(index, "invoke", true, true, "skipped_not_part");
        Logger.Info("Velum Recipe assign_session_material skipped (not a part)");
        return true;
      }

      string directory;
      if (!VelumMaterialSequenceState.TryResolveAssignDirectory(modelDoc, out directory) ||
          string.IsNullOrWhiteSpace(directory))
      {
        result = new RecipeStepExecutionResult(index, "invoke", true, true, "skipped_directory_mismatch");
        Logger.Info("Velum Recipe assign_session_material skipped (directory mismatch)");
        return true;
      }

      if (!VelumMaterialSequenceState.IsReady)
      {
        result = new RecipeStepExecutionResult(index, "invoke", true, true, "skipped_empty_state");
        Logger.Info("Velum Recipe assign_session_material skipped (empty sequence state)");
        return true;
      }

      if (!VelumMaterialSequenceState.IsReadyForDirectory(directory))
      {
        result = new RecipeStepExecutionResult(index, "invoke", true, true, "skipped_directory_mismatch");
        Logger.Info("Velum Recipe assign_session_material skipped (directory mismatch)");
        return true;
      }

      if (VelumMaterialSequenceState.WasAutoMaterialAppliedToDocument(modelDoc))
      {
        result = new RecipeStepExecutionResult(index, "invoke", true, true, "skipped_already_served");
        Logger.Info("Velum Recipe assign_session_material skipped (already served for this document)");
        return true;
      }

      if (VelumSolidWorksMaterialComHelper.TryReadPartMaterial(
              modelDoc,
              out string currentMaterial,
              out string _) &&
          !string.IsNullOrWhiteSpace(currentMaterial))
      {
        result = new RecipeStepExecutionResult(index, "invoke", true, true, "skipped_already_has_material");
        Logger.Info("Velum Recipe assign_session_material skipped (material already assigned)");
        return true;
      }

      if (VelumMaterialSequenceState.DocumentMaterialMatchesLearned(modelDoc))
      {
        VelumMaterialSequenceState.MarkAutoMaterialAppliedToDocument(modelDoc);
        result = new RecipeStepExecutionResult(index, "invoke", true, true, "skipped_already_applied");
        Logger.Info("Velum Recipe assign_session_material skipped (material already matches learned)");
        return true;
      }

      if (!VelumMaterialSequenceState.TryGetLearnedMaterial(
              directory,
              out string materialName,
              out string databaseName))
      {
        result = new RecipeStepExecutionResult(index, "invoke", true, true, "skipped_empty_state");
        Logger.Info("Velum Recipe assign_session_material skipped (learned material not found)");
        return true;
      }

      bool ok = VelumSolidWorksMaterialComHelper.TryAssignPartMaterial(
          modelDoc,
          string.Empty,
          materialName,
          databaseName);

      if (ok)
        VelumMaterialSequenceState.MarkAutoMaterialAppliedToDocument(modelDoc);

      result = new RecipeStepExecutionResult(
          index,
          "invoke",
          ok,
          false,
          ok
              ? "material=" + materialName + ";database=" + databaseName
              : "assign_session_material_failed");

      if (ok)
      {
        Logger.Info(
            "Velum Recipe assign_session_material OK material=\"" + materialName +
            "\" database=\"" + databaseName + "\"");
      }
      else
      {
        Logger.Warning(
            "Velum Recipe assign_session_material FAIL material=\"" + materialName +
            "\" database=\"" + databaseName + "\"");
      }

      return ok;
    }

    public static bool TryExecuteSetCustomProperty(
        int index,
        IReadOnlyDictionary<string, string> parameters,
        ModelDoc2 modelDoc,
        IReadOnlyDictionary<string, string> templateContext,
        out RecipeStepExecutionResult result)
    {
      string name = RecipeArgsParser.Get(parameters, "name");
      string template = RecipeArgsParser.Get(parameters, "template");
      string config = RecipeArgsParser.Get(parameters, "config");
      string overwrite = RecipeArgsParser.Get(parameters, "overwrite");
      string propertyType = RecipeArgsParser.Get(parameters, "property_type");
      if (string.IsNullOrWhiteSpace(propertyType))
        propertyType = VelumSolidCustomPropertyTypes.TypeKeyText;
      if (string.Equals(overwrite, "never_if_filled", StringComparison.OrdinalIgnoreCase))
        overwrite = "if_empty";

      if (string.IsNullOrWhiteSpace(name))
      {
        result = new RecipeStepExecutionResult(index, "invoke", false, false, "missing_name");
        return false;
      }

      string value = VelumRecipeTemplateResolver.Resolve(template ?? string.Empty, templateContext);

      if (VelumExportDocumentationProperties.IsDxfPartOnlyProperty(name))
      {
        int docType = 0;
        try
        {
          docType = modelDoc != null ? modelDoc.GetType() : 0;
        }
        catch
        {
          docType = 0;
        }

        if (docType == (int)swDocumentTypes_e.swDocASSEMBLY)
        {
          result = new RecipeStepExecutionResult(
              index,
              "invoke",
              true,
              true,
              name + ":skipped_assembly_dxf_property");
          Logger.Info(
              "Velum Recipe set_custom_property skipped (assembly DXF property) name=\"" + name + "\"");
          return true;
        }

        if (docType == (int)swDocumentTypes_e.swDocPART &&
            string.Equals(name, VelumExportDocumentationProperties.NeedDxf, StringComparison.Ordinal))
        {
          bool needDxf = VelumAppConfig.NeedDxfDefault;
          if (VelumSolidCustomPropertyTypes.TryParseBooleanString(value, out bool parsed))
            needDxf = parsed;

          CustomPropertyManager existingCpm =
              VelumRecipeSolidWorksCustomProperties.TryGetManager(modelDoc, "document");
          bool exists = existingCpm != null &&
              VelumRecipeSolidWorksCustomProperties.TryPropertyExists(
                  existingCpm,
                  VelumExportDocumentationProperties.NeedDxf);
          if (!exists)
            needDxf = VelumAppConfig.NeedDxfDefault;

          bool written = VelumYesOrNoCustomPropertyWriter.TryWrite(
              modelDoc,
              VelumExportDocumentationProperties.NeedDxf,
              needDxf,
              out string writeError);
          result = new RecipeStepExecutionResult(
              index,
              "invoke",
              true,
              !written,
              written
                  ? name + ":" + (needDxf ? "Yes" : "No")
                  : name + ":" + writeError);
          if (written)
            VelumSolidProbeRefreshPlanner.MarkExportDocumentationStale();
          else
            Logger.Warning("Velum Recipe NeedDxf write failed: " + writeError);
          return true;
        }
      }

      bool anyWritten = false;
      bool anyOk = false;
      string lastMessage = "custom_property_manager_unavailable";

      foreach (string configKey in VelumRecipeSolidWorksCustomProperties.EnumerateWriteConfigurationKeys(
          modelDoc,
          config))
      {
        CustomPropertyManager cpm;
        try
        {
          cpm = modelDoc.Extension?.CustomPropertyManager[configKey ?? string.Empty];
        }
        catch
        {
          cpm = null;
        }

        if (cpm == null)
          continue;

        bool ok = VelumRecipeSolidWorksCustomProperties.TrySetValue(
            cpm,
            name,
            value,
            overwrite,
            propertyType,
            out bool skipped,
            out string message);

        string scope = string.IsNullOrEmpty(configKey) ? "document" : configKey;
        lastMessage = scope + ":" + message;

        if (!ok)
        {
          result = new RecipeStepExecutionResult(
              index,
              "invoke",
              false,
              false,
              name + ":" + lastMessage);
          return false;
        }

        anyOk = true;
        if (!skipped)
        {
          anyWritten = true;
          Logger.Info(
              "Velum Recipe set_custom_property name=\"" + name +
              "\" value=\"" + value + "\" scope=" + scope);
        }
      }

      if (!anyOk)
      {
        result = new RecipeStepExecutionResult(
            index,
            "invoke",
            false,
            false,
            name + ":" + lastMessage);
        return false;
      }

      result = new RecipeStepExecutionResult(
          index,
          "invoke",
          true,
          !anyWritten,
          name + ":" + lastMessage);

      if (!anyWritten)
      {
        Logger.Info(
            "Velum Recipe set_custom_property skipped name=\"" + name + "\" (" + lastMessage + ")");
      }
      else if (VelumExportDocumentationProperties.IsExportDocumentationProbeProperty(name))
      {
        VelumSolidProbeRefreshPlanner.MarkExportDocumentationStale();
      }

      return true;
    }

    public static bool TryExecuteRunSwCommand(
        int index,
        IReadOnlyDictionary<string, string> parameters,
        SldWorks sw,
        out RecipeStepExecutionResult result)
    {
      if (sw == null)
      {
        result = new RecipeStepExecutionResult(index, "invoke", false, false, "sldworks_null");
        return false;
      }

      if (!TryParseIntParam(parameters, "command_id", out int commandId) || commandId <= 0)
      {
        result = new RecipeStepExecutionResult(index, "invoke", false, false, "invalid_command_id");
        return false;
      }

      try
      {
        sw.RunCommand(commandId, string.Empty);
        result = new RecipeStepExecutionResult(
            index,
            "invoke",
            true,
            false,
            "command_id=" + commandId.ToString(CultureInfo.InvariantCulture));
        return true;
      }
      catch (Exception ex)
      {
        result = new RecipeStepExecutionResult(index, "invoke", false, false, ex.Message);
        return false;
      }
    }

    public static bool TryExecuteRebuild(
        int index,
        ModelDoc2 modelDoc,
        out RecipeStepExecutionResult result)
    {
      if (modelDoc == null)
      {
        result = new RecipeStepExecutionResult(index, "invoke", false, false, "model_doc_null");
        return false;
      }

      try
      {
        bool rebuilt = modelDoc.EditRebuild3();
        result = new RecipeStepExecutionResult(
            index,
            "invoke",
            true,
            false,
            rebuilt ? "rebuild_ok" : "rebuild_returned_false");
        return true;
      }
      catch (Exception ex)
      {
        result = new RecipeStepExecutionResult(index, "invoke", false, false, ex.Message);
        return false;
      }
    }

    public static bool TryExecuteLog(
        int index,
        IReadOnlyDictionary<string, string> parameters,
        RecipeDefinition recipe,
        IReadOnlyDictionary<string, string> templateContext,
        out RecipeStepExecutionResult result)
    {
      string message = RecipeArgsParser.Get(parameters, "message");
      if (string.IsNullOrWhiteSpace(message))
        message = "recipe_id=" + (recipe?.RecipeId ?? string.Empty);

      message = VelumRecipeTemplateResolver.Resolve(message, templateContext);
      string level = (RecipeArgsParser.Get(parameters, "level") ?? "info").Trim().ToLowerInvariant();

      switch (level)
      {
        case "warning":
        case "warn":
          Logger.Warning("Velum Recipe step: " + message);
          break;
        case "error":
          Logger.Error("Velum Recipe step: " + message);
          break;
        default:
          Logger.Info("Velum Recipe step: " + message);
          break;
      }

      result = new RecipeStepExecutionResult(index, "invoke", true, false, message);
      return true;
    }

    /// <summary>
    /// Handler <c>run_macro</c>: <c>ISldWorks.RunMacro2</c>.
    /// Args: <c>path</c> (обязателен), <c>module</c> (по умолчанию Module1), <c>procedure</c> (по умолчанию main).
    /// </summary>
    public static bool TryExecuteMacro(
        int index,
        IReadOnlyDictionary<string, string> parameters,
        SldWorks sw,
        out RecipeStepExecutionResult result)
    {
      if (sw == null)
      {
        result = new RecipeStepExecutionResult(index, "invoke", false, false, "sldworks_null");
        return false;
      }

      string path = RecipeArgsParser.Get(parameters, "path");
      if (string.IsNullOrWhiteSpace(path))
        path = RecipeArgsParser.Get(parameters, "macro_id");
      path = (path ?? string.Empty).Trim().Trim('"');
      if (string.IsNullOrWhiteSpace(path))
      {
        result = new RecipeStepExecutionResult(index, "invoke", false, false, "missing_macro_path");
        return false;
      }

      string module = RecipeArgsParser.Get(parameters, "module");
      if (string.IsNullOrWhiteSpace(module))
        module = "Module1";
      else
        module = module.Trim();

      string procedure = RecipeArgsParser.Get(parameters, "procedure");
      if (string.IsNullOrWhiteSpace(procedure))
        procedure = "main";
      else
        procedure = procedure.Trim();

      if (!File.Exists(path))
      {
        result = new RecipeStepExecutionResult(
            index,
            "invoke",
            false,
            false,
            "macro_file_not_found:" + path);
        return false;
      }

      try
      {
        // swRunMacroOption_e.swRunMacroDefault == 0
        int options = 0;
        bool runOk = sw.RunMacro2(path, module, procedure, options, out int macroErr);
        if (runOk)
        {
          result = new RecipeStepExecutionResult(
              index,
              "invoke",
              true,
              false,
              "run_macro path=" + path + " module=" + module + " procedure=" + procedure);
          return true;
        }

        result = new RecipeStepExecutionResult(
            index,
            "invoke",
            false,
            false,
            "run_macro_failed:" + macroErr.ToString(CultureInfo.InvariantCulture) +
                " path=" + path + " module=" + module + " procedure=" + procedure);
        return false;
      }
      catch (Exception ex)
      {
        result = new RecipeStepExecutionResult(index, "invoke", false, false, ex.Message);
        return false;
      }
    }

    private static bool TryParseIntParam(
        IReadOnlyDictionary<string, string> parameters,
        string key,
        out int value)
    {
      value = 0;
      string raw = RecipeArgsParser.Get(parameters, key);
      return int.TryParse(raw?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static string TryGetDocumentPath(ModelDoc2 modelDoc)
    {
      try
      {
        return modelDoc?.GetPathName() ?? string.Empty;
      }
      catch
      {
        return string.Empty;
      }
    }

    private static string TryGetDocumentTitle(ModelDoc2 modelDoc)
    {
      try
      {
        return modelDoc?.GetTitle() ?? string.Empty;
      }
      catch
      {
        return string.Empty;
      }
    }
  }
}
