using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ISIDA.Common;
using ISIDA.SymbiontEnv.Contract;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Velum.ReactiveCore.DocumentTemplates;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Handler'ы рецептов для работы с шаблонами документов SolidWorks.
  /// </summary>
  internal static class RecipeExecutorHandlersDocumentTemplates
  {
    /// <summary>Создание нового документа из шаблона.</summary>
    public const string NewDocumentFromTemplateHandlerId = "new_document_from_template";

    /// <summary>Клонирование шаблона с дефолтными параметрами.</summary>
    public const string CloneDocumentWithDefaultsHandlerId = "clone_document_with_defaults";

    /// <summary>Применение значений параметров к документу.</summary>
    public const string ApplyDocumentParametersHandlerId = "apply_document_parameters";

    /// <summary>Чтение параметров из живого документа.</summary>
    public const string ReadDocumentParametersHandlerId = "read_document_parameters";

    /// <summary>Запись маркера шаблона (Velum.TemplateId и т.п.).</summary>
    public const string WriteTemplateMarkerHandlerId = "write_template_marker";

    /// <summary>Валидация контракта параметра в живом документе.</summary>
    public const string ValidateDocumentContractHandlerId = "validate_document_contract";

    private static VelumDocumentTemplateCatalog _catalog;
    private static readonly object CatalogSync = new object();

    /// <summary>
    /// Создаёт новый документ SolidWorks из шаблона и назначает имя файла для сохранения.
    /// Параметры: <c>template_id</c>, <c>save_file_name</c> (опц.; по умолчанию — имя шаблона).
    /// </summary>
    public static bool TryExecuteNewDocumentFromTemplate(
        int index,
        IReadOnlyDictionary<string, string> args,
        ModelDoc2 modelDoc,
        SldWorks sw,
        out RecipeStepExecutionResult result)
    {
      result = null;

      if (sw == null)
      {
        result = new RecipeStepExecutionResult(index, "invoke", false, false, "solidworks_session_unavailable");
        return false;
      }

      string templateId = (RecipeArgsParser.Get(args, "template_id") ?? string.Empty).Trim();
      if (templateId.Length == 0)
      {
        result = new RecipeStepExecutionResult(index, "invoke", false, false, "missing_template_id");
        return false;
      }

      VelumDocumentTemplateCatalog catalog = GetOrCreateCatalog();
      EnvironmentDocumentTemplateData template = catalog.FindByTemplateId(templateId);
      if (template == null)
      {
        result = new RecipeStepExecutionResult(index, "invoke", false, false, "template_not_found:" + templateId);
        return false;
      }

      string templateFile = catalog.ResolveTemplateFilePath(template);
      if (string.IsNullOrEmpty(templateFile))
      {
        result = new RecipeStepExecutionResult(index, "invoke", false, false, "template_file_missing");
        return false;
      }

      string docType = (template.DocumentType ?? string.Empty).Trim();
      int swDocType = ResolveSwDocumentType(docType);
      if (swDocType == 0)
      {
        result = new RecipeStepExecutionResult(index, "invoke", false, false, "unsupported_document_type:" + docType);
        return false;
      }

      string saveFileName = (RecipeArgsParser.Get(args, "save_file_name") ?? string.Empty).Trim();
      if (saveFileName.Length == 0)
      {
        string baseName = Path.GetFileNameWithoutExtension(templateFile);
        saveFileName = baseName + GetSwExtension(docType);
      }

      try
      {
        ModelDoc2 newDoc = sw.INewDocument2(templateFile, swDocType, 0, 0);
        if (newDoc == null)
        {
          result = new RecipeStepExecutionResult(index, "invoke", false, false, "new_document_returned_null");
          return false;
        }

        // Имя для сохранения (без немедленной записи — по плану шаг не сохраняет).
        newDoc.SetSaveAsFileName(saveFileName);

        result = new RecipeStepExecutionResult(
            index,
            "invoke",
            true,
            false,
            "created:" + saveFileName);
        Logger.Info("Velum Recipe new_document_from_template OK template=\"" + templateId + "\" name=\"" + saveFileName + "\"");
        return true;
      }
      catch (Exception ex)
      {
        result = new RecipeStepExecutionResult(index, "invoke", false, false, ex.Message);
        Logger.Warning("Velum Recipe new_document_from_template failed: " + ex.Message);
        return false;
      }
    }

    /// <summary>
    /// Создаёт документ из шаблона и применяет значения параметров по умолчанию
    /// (рефлекс «клон шаблона»). Документ не сохраняется — сохранение выполняет
    /// отдельный шаг <c>save_file_name</c>/политика имени.
    /// </summary>
    public static bool TryExecuteCloneDocumentWithDefaults(
        int index,
        IReadOnlyDictionary<string, string> args,
        ModelDoc2 modelDoc,
        SldWorks sw,
        out RecipeStepExecutionResult result)
    {
      if (!TryExecuteNewDocumentFromTemplate(index, args, modelDoc, sw, out result))
        return false;

      string templateId = (RecipeArgsParser.Get(args, "template_id") ?? string.Empty).Trim();
      VelumDocumentTemplateCatalog catalog = GetOrCreateCatalog();
      EnvironmentDocumentTemplateData template = catalog.FindByTemplateId(templateId);
      if (template == null || template.Parameters == null || template.Parameters.Count == 0)
      {
        result = new RecipeStepExecutionResult(index, "invoke", true, false, "cloned_no_parameters");
        return true;
      }

      // Свежесозданный документ активен — берём его как текущий.
      ModelDoc2 activeDoc = TryGetActiveDocument(sw) ?? modelDoc;
      if (activeDoc == null)
      {
        result = new RecipeStepExecutionResult(index, "invoke", true, true, "cloned_no_active_document");
        return true;
      }

      var defaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      foreach (EnvironmentDocumentParameterData p in template.Parameters)
      {
        if (p == null || p.Default == null)
          continue;
        defaults[p.Id] = Convert.ToString(p.Default, CultureInfo.InvariantCulture);
      }

      if (defaults.Count == 0)
      {
        result = new RecipeStepExecutionResult(index, "invoke", true, false, "cloned_no_defaults");
        return true;
      }

      VelumDocumentParameterService.EnsureSketchClosed(activeDoc);
      IReadOnlyList<VelumDocumentParameterService.ParameterWriteResult> writeResults =
          VelumDocumentParameterService.WriteParameters(activeDoc, template, defaults);

      bool allOk = true;
      bool anyChanged = false;
      foreach (VelumDocumentParameterService.ParameterWriteResult wr in writeResults)
      {
        if (!wr.Ok)
        {
          allOk = false;
          Logger.Warning("Velum Recipe clone defaults write failed id=\"" + wr.Id + "\": " + wr.Note);
        }
        else if (wr.Changed)
        {
          anyChanged = true;
        }
      }

      if (anyChanged)
        activeDoc.EditRebuild3();

      result = new RecipeStepExecutionResult(
          index,
          "invoke",
          allOk,
          false,
          allOk ? "cloned_with_defaults" : "cloned_defaults_partial_fail");
      return allOk;
    }

    /// <summary>
    /// Применяет значения параметров к активному документу.
    /// Параметры: <c>template_id</c> и значения <c>id=value</c>; дополнительно
    /// <c>param_id</c> (фильтр одного параметра) и <c>save_after</c> (<c>true</c>/<c>false</c>).
    /// </summary>
    public static bool TryExecuteApplyDocumentParameters(
        int index,
        IReadOnlyDictionary<string, string> args,
        ModelDoc2 modelDoc,
        SldWorks sw,
        out RecipeStepExecutionResult result)
    {
      result = null;

      if (modelDoc == null)
      {
        result = new RecipeStepExecutionResult(index, "invoke", false, false, "model_doc_null");
        return false;
      }

      string templateId = (RecipeArgsParser.Get(args, "template_id") ?? string.Empty).Trim();
      if (templateId.Length == 0)
      {
        result = new RecipeStepExecutionResult(index, "invoke", false, false, "missing_template_id");
        return false;
      }

      VelumDocumentTemplateCatalog catalog = GetOrCreateCatalog();
      EnvironmentDocumentTemplateData template = catalog.FindByTemplateId(templateId);
      if (template == null)
      {
        result = new RecipeStepExecutionResult(index, "invoke", false, false, "template_not_found:" + templateId);
        return false;
      }

      var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      if (args != null)
      {
        foreach (KeyValuePair<string, string> kv in args)
        {
          if (string.Equals(kv.Key, "template_id", StringComparison.OrdinalIgnoreCase) ||
              string.Equals(kv.Key, "param_id", StringComparison.OrdinalIgnoreCase) ||
              string.Equals(kv.Key, "save_after", StringComparison.OrdinalIgnoreCase) ||
              string.Equals(kv.Key, "handler", StringComparison.OrdinalIgnoreCase) ||
              string.Equals(kv.Key, "confirm", StringComparison.OrdinalIgnoreCase))
            continue;

          if (string.IsNullOrWhiteSpace(kv.Value))
            continue;

          values[kv.Key] = kv.Value;
        }
      }

      string filterParamId = (RecipeArgsParser.Get(args, "param_id") ?? string.Empty).Trim();
      if (filterParamId.Length > 0)
      {
        if (values.TryGetValue(filterParamId, out string filteredValue))
        {
          values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
          {
            { filterParamId, filteredValue }
          };
        }
        else
        {
          result = new RecipeStepExecutionResult(
              index, "invoke", true, true, "skipped_no_value_for_param:" + filterParamId);
          return true;
        }
      }

      if (values.Count == 0)
      {
        result = new RecipeStepExecutionResult(index, "invoke", true, true, "skipped_no_values");
        return true;
      }

      VelumDocumentParameterService.EnsureSketchClosed(modelDoc);

      IReadOnlyList<VelumDocumentParameterService.ParameterWriteResult> writeResults =
          VelumDocumentParameterService.WriteParameters(modelDoc, template, values);

      bool allOk = true;
      bool anyChanged = false;
      var notes = new List<string>();
      foreach (VelumDocumentParameterService.ParameterWriteResult wr in writeResults)
      {
        if (!wr.Ok)
        {
          allOk = false;
          notes.Add(wr.Id + ":" + wr.Code);
          Logger.Warning("Velum Recipe apply param write failed id=\"" + wr.Id + "\": " + wr.Note);
        }
        else if (wr.Changed)
        {
          anyChanged = true;
        }
      }

      if (anyChanged)
        modelDoc.EditRebuild3();

      string saveAfter = (RecipeArgsParser.Get(args, "save_after") ?? "false").Trim();
      if (anyChanged && string.Equals(saveAfter, "true", StringComparison.OrdinalIgnoreCase))
      {
        int errors = 0;
        int warnings = 0;
        modelDoc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref errors, ref warnings);
      }

      string message = allOk ? "parameters_applied" : "parameters_partial_fail";
      if (notes.Count > 0)
        message += ":" + string.Join(",", notes);

      result = new RecipeStepExecutionResult(index, "invoke", allOk, false, message);
      return allOk;
    }

    /// <summary>
    /// Читает параметры шаблона из живого документа.
    /// Параметры: <c>template_id</c>.
    /// </summary>
    public static bool TryExecuteReadDocumentParameters(
        int index,
        IReadOnlyDictionary<string, string> args,
        ModelDoc2 modelDoc,
        SldWorks sw,
        out RecipeStepExecutionResult result)
    {
      result = null;

      if (modelDoc == null)
      {
        result = new RecipeStepExecutionResult(index, "invoke", false, false, "model_doc_null");
        return false;
      }

      string templateId = (RecipeArgsParser.Get(args, "template_id") ?? string.Empty).Trim();
      if (templateId.Length == 0)
      {
        result = new RecipeStepExecutionResult(index, "invoke", false, false, "missing_template_id");
        return false;
      }

      VelumDocumentTemplateCatalog catalog = GetOrCreateCatalog();
      EnvironmentDocumentTemplateData template = catalog.FindByTemplateId(templateId);
      if (template == null)
      {
        result = new RecipeStepExecutionResult(index, "invoke", false, false, "template_not_found:" + templateId);
        return false;
      }

      IReadOnlyList<VelumDocumentParameterService.ParameterReadResult> readResults =
          VelumDocumentParameterService.ReadParameters(modelDoc, template);

      var parts = new List<string>();
      foreach (VelumDocumentParameterService.ParameterReadResult rr in readResults)
      {
        string val = rr.Found ? (rr.Value ?? string.Empty) : ("(" + (rr.Note ?? "not_found") + ")");
        parts.Add(rr.Id + "=" + val);
      }

      result = new RecipeStepExecutionResult(
          index, "invoke", true, false, "read:" + string.Join(";", parts));
      return true;
    }

    /// <summary>
    /// Записывает маркер шаблона в пользовательские свойства документа:
    /// Velum.TemplateId, Velum.TemplateSchema, Velum.ContractHash, Velum.PolicyVersion.
    /// </summary>
    public static bool TryExecuteWriteTemplateMarker(
        int index,
        IReadOnlyDictionary<string, string> args,
        ModelDoc2 modelDoc,
        SldWorks sw,
        out RecipeStepExecutionResult result)
    {
      result = null;

      if (modelDoc == null)
      {
        result = new RecipeStepExecutionResult(index, "invoke", false, false, "model_doc_null");
        return false;
      }

      string templateId = (RecipeArgsParser.Get(args, "template_id") ?? string.Empty).Trim();
      if (templateId.Length == 0)
      {
        result = new RecipeStepExecutionResult(index, "invoke", false, false, "missing_template_id");
        return false;
      }

      VelumDocumentTemplateCatalog catalog = GetOrCreateCatalog();
      EnvironmentDocumentTemplateData template = catalog.FindByTemplateId(templateId);
      if (template == null)
      {
        result = new RecipeStepExecutionResult(index, "invoke", false, false, "template_not_found:" + templateId);
        return false;
      }

      string contractHash = VelumDocumentTemplateHashRules.ComputeContractHash(
          template.TemplateId,
          template.TemplateSchema ?? "1",
          BuildDescriptors(template));

      CustomPropertyManager cpm =
          VelumRecipeSolidWorksCustomProperties.TryGetManager(modelDoc, "document");
      if (cpm == null)
      {
        result = new RecipeStepExecutionResult(index, "invoke", false, false, "custom_property_manager_unavailable");
        return false;
      }

      string[] names = { "Velum.TemplateId", "Velum.TemplateSchema", "Velum.ContractHash", "Velum.PolicyVersion" };
      string[] values =
      {
        templateId,
        template.TemplateSchema ?? "1",
        contractHash,
        (RecipeArgsParser.Get(args, "policy_version") ?? "1").Trim()
      };

      bool ok = true;
      for (int i = 0; i < names.Length; i++)
      {
        if (!VelumRecipeSolidWorksCustomProperties.TrySetValue(
                cpm, names[i], values[i], "always", "text", out bool skipped, out string note))
        {
          ok = false;
          Logger.Warning("Velum Recipe write_template_marker failed prop=\"" + names[i] + "\": " + note);
        }
        else if (skipped)
        {
          Logger.Info("Velum Recipe write_template_marker skipped prop=\"" + names[i] + "\": " + note);
        }
      }

      result = new RecipeStepExecutionResult(
          index, "invoke", ok, false, ok ? "template_marker_written" : "template_marker_partial_fail");
      return ok;
    }

    /// <summary>
    /// Валидация контракта параметров в живом документе: наличие переменных уравнений
    /// и корректность адресов. Параметры: <c>template_id</c>.
    /// </summary>
    public static bool TryExecuteValidateDocumentContract(
        int index,
        IReadOnlyDictionary<string, string> args,
        ModelDoc2 modelDoc,
        SldWorks sw,
        out RecipeStepExecutionResult result)
    {
      result = null;

      if (modelDoc == null)
      {
        result = new RecipeStepExecutionResult(index, "invoke", false, false, "model_doc_null");
        return false;
      }

      string templateId = (RecipeArgsParser.Get(args, "template_id") ?? string.Empty).Trim();
      if (templateId.Length == 0)
      {
        result = new RecipeStepExecutionResult(index, "invoke", false, false, "missing_template_id");
        return false;
      }

      VelumDocumentTemplateCatalog catalog = GetOrCreateCatalog();
      EnvironmentDocumentTemplateData template = catalog.FindByTemplateId(templateId);
      if (template == null)
      {
        result = new RecipeStepExecutionResult(index, "invoke", false, false, "template_not_found:" + templateId);
        return false;
      }

      var errors = new List<string>();
      var warnings = new List<string>();

      EquationMgr equations = VelumDocumentParameterService.TryGetEquationManager(modelDoc);
      if (equations == null)
        errors.Add("equation_mgr_unavailable");

      if (template.Parameters != null)
      {
        foreach (EnvironmentDocumentParameterData p in template.Parameters)
        {
          if (p == null)
            continue;

          VelumDocumentAddress address = VelumDocumentParameterService.ToAddress(p.Address);
          string type = (address.Type ?? string.Empty).Trim();

          if (string.Equals(type, VelumDocumentContractRules.AddressEquation, StringComparison.OrdinalIgnoreCase))
          {
            if (!VelumDocumentContractRules.IsValidEquationName(address.Name))
            {
              errors.Add("parameter_" + p.Id + ":invalid_equation_name");
              continue;
            }

            if (equations != null)
            {
              string value = VelumDocumentParameterService.TryReadGlobalVariable(equations, address.Name);
              if (value == null)
                warnings.Add("parameter_" + p.Id + ":equation_not_found");
            }
          }
          else if (!VelumDocumentContractRules.IsKnownAddressType(type))
          {
            errors.Add("parameter_" + p.Id + ":unknown_address_type:" + type);
          }
        }
      }

      bool ok = errors.Count == 0;
      string detail = "errors=" + errors.Count + " warnings=" + warnings.Count;
      if (errors.Count > 0)
        detail += " [" + string.Join(";", errors) + "]";
      if (warnings.Count > 0)
        detail += " [" + string.Join(";", warnings) + "]";

      result = new RecipeStepExecutionResult(index, "invoke", ok, false, "validate:" + detail);
      return ok;
    }

    private static IEnumerable<VelumDocumentParameterDescriptor> BuildDescriptors(
        EnvironmentDocumentTemplateData template)
    {
      var list = new List<VelumDocumentParameterDescriptor>();
      if (template?.Parameters == null)
        return list;

      foreach (EnvironmentDocumentParameterData p in template.Parameters)
      {
        if (p == null)
          continue;

        list.Add(new VelumDocumentParameterDescriptor
        {
          Id = p.Id,
          Type = p.Type,
          Unit = p.Unit ?? string.Empty,
          Address = VelumDocumentContractRules.FormatAddress(
              VelumDocumentParameterService.ToAddress(p.Address))
        });
      }

      return list;
    }

    private static int ResolveSwDocumentType(string docType)
    {
      if (string.Equals(docType, "part", StringComparison.OrdinalIgnoreCase))
        return (int)swDocumentTypes_e.swDocPART;
      if (string.Equals(docType, "asm", StringComparison.OrdinalIgnoreCase))
        return (int)swDocumentTypes_e.swDocASSEMBLY;
      if (string.Equals(docType, "drw", StringComparison.OrdinalIgnoreCase))
        return (int)swDocumentTypes_e.swDocDRAWING;
      return 0;
    }

    private static string GetSwExtension(string docType)
    {
      if (string.Equals(docType, "part", StringComparison.OrdinalIgnoreCase))
        return ".sldprt";
      if (string.Equals(docType, "asm", StringComparison.OrdinalIgnoreCase))
        return ".sldasm";
      if (string.Equals(docType, "drw", StringComparison.OrdinalIgnoreCase))
        return ".slddrw";
      return string.Empty;
    }

    private static ModelDoc2 TryGetActiveDocument(SldWorks sw)
    {
      try
      {
        return sw?.IActiveDoc2;
      }
      catch
      {
        return null;
      }
    }

    private static VelumDocumentTemplateCatalog GetOrCreateCatalog()
    {
      if (_catalog != null)
        return _catalog;

      lock (CatalogSync)
      {
        if (_catalog == null)
        {
          var catalog = new VelumDocumentTemplateCatalog();
          catalog.Load();
          _catalog = catalog;
        }
      }

      return _catalog;
    }
  }
}