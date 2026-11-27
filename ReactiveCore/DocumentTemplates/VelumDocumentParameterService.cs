using System;
using System.Collections.Generic;
using System.Globalization;
using ISIDA.Common;
using ISIDA.SymbiontEnv.Contract;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace Velum.ReactiveCore.DocumentTemplates
{
  /// <summary>
  /// Чтение и запись параметров контракта документа через COM-API SolidWorks:
  /// глобальные переменные <c>EquationMgr</c> (основной канал записи), размеры
  /// (<c>Dimension</c>, только чтение/repair) и пользовательские свойства (только чтение).
  /// </summary>
  /// <remarks>
  /// Все операции выполняются в UI-потоке SolidWorks. Порядок записи:
  /// проверка read-only → параметры → <c>EditRebuild3</c> (выполняет вызывающий код).
  /// Идемпотентность: значение пишется только при фактическом отличии.
  /// Сигнатуры COM проверены по метаданным PIA: <c>EquationMgr.GetCount</c>,
  /// <c>EquationMgr.Equation[i]</c>, <c>EquationMgr.GlobalVariable[i]</c>,
  /// <c>EquationMgr.Add2(index, equation, globalVariable)</c>,
  /// <c>EquationMgr.SetEquationAndConfigurationOption(index, equation, option, configName)</c>,
  /// <c>ModelDoc2.IParameter(fullName)</c>, <c>Dimension.SetSystemValue3(value, configOption, configName)</c>.
  /// </remarks>
  internal static class VelumDocumentParameterService
  {
    /// <summary>Результат чтения одного параметра.</summary>
    internal sealed class ParameterReadResult
    {
      /// <summary>Идентификатор параметра.</summary>
      public string Id { get; set; }

      /// <summary>Прочитанное значение (строка) либо null, если адрес не найден.</summary>
      public string Value { get; set; }

      /// <summary>true, если параметр прочитан.</summary>
      public bool Found { get; set; }

      /// <summary>Причина отсутствия/деградации (для лога и результата шага).</summary>
      public string Note { get; set; }
    }

    /// <summary>Результат записи одного параметра.</summary>
    internal sealed class ParameterWriteResult
    {
      /// <summary>Идентификатор параметра.</summary>
      public string Id { get; set; }

      /// <summary>Итог: true — значение записано либо уже совпадало.</summary>
      public bool Ok { get; set; }

      /// <summary>true, если значение реально изменилось на диске.</summary>
      public bool Changed { get; set; }

      /// <summary>Код результата (<c>ok</c>, <c>skipped_policy</c>, <c>write_failed</c>, …).</summary>
      public string Code { get; set; }

      /// <summary>Человекочитаемая причина.</summary>
      public string Note { get; set; }
    }

    /// <summary>
    /// Проверяет, доступен ли документ для записи (не read-only).
    /// </summary>
    /// <param name="modelDoc">Документ SolidWorks.</param>
    /// <returns>true — документ доступен для изменения.</returns>
    internal static bool IsWritable(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return false;

      try
      {
        // PIA: IsOpenedReadOnly() / IsOpenedViewOnly(); отдельного IsReadOnly() нет.
        return !modelDoc.IsOpenedReadOnly() && !modelDoc.IsOpenedViewOnly();
      }
      catch
      {
        return false;
      }
    }

    /// <summary>
    /// Читает значения всех параметров шаблона из живого документа.
    /// </summary>
    /// <param name="modelDoc">Документ SolidWorks.</param>
    /// <param name="template">Шаблон контракта.</param>
    /// <returns>Список результатов чтения (по одному на параметр).</returns>
    internal static IReadOnlyList<ParameterReadResult> ReadParameters(
        ModelDoc2 modelDoc,
        EnvironmentDocumentTemplateData template)
    {
      var results = new List<ParameterReadResult>();
      if (modelDoc == null || template?.Parameters == null)
        return results;

      EquationMgr equations = TryGetEquationManager(modelDoc);
      foreach (EnvironmentDocumentParameterData p in template.Parameters)
      {
        if (p == null)
          continue;

        results.Add(ReadOne(modelDoc, equations, p));
      }

      return results;
    }

    private static ParameterReadResult ReadOne(
        ModelDoc2 modelDoc,
        EquationMgr equations,
        EnvironmentDocumentParameterData parameter)
    {
      var result = new ParameterReadResult
      {
        Id = parameter.Id ?? string.Empty,
        Found = false,
        Note = string.Empty
      };

      VelumDocumentAddress address = ToAddress(parameter.Address);
      string type = (address.Type ?? string.Empty).Trim();

      if (string.Equals(type, VelumDocumentContractRules.AddressEquation, StringComparison.OrdinalIgnoreCase))
      {
        string value = TryReadGlobalVariable(equations, address.Name);
        if (value == null)
        {
          result.Note = "equation_not_found";
          return result;
        }

        result.Value = value;
        result.Found = true;
        return result;
      }

      if (string.Equals(type, VelumDocumentContractRules.AddressDimension, StringComparison.OrdinalIgnoreCase))
      {
        Dimension dim = TryGetDimension(modelDoc, address.Name);
        if (dim == null)
        {
          result.Note = "dimension_not_found";
          return result;
        }

        try
        {
          // Размеры хранятся в метрах; контракт оперирует миллиметрами.
          result.Value = (dim.SystemValue * 1000d).ToString("R", CultureInfo.InvariantCulture);
          result.Found = true;
        }
        catch (Exception ex)
        {
          result.Note = "dimension_read_failed:" + ex.Message;
        }

        return result;
      }

      if (string.Equals(type, VelumDocumentContractRules.AddressProperty, StringComparison.OrdinalIgnoreCase) ||
          string.Equals(type, VelumDocumentContractRules.AddressConfigProperty, StringComparison.OrdinalIgnoreCase))
      {
        string configKey = string.Equals(type, VelumDocumentContractRules.AddressConfigProperty, StringComparison.OrdinalIgnoreCase)
            ? address.Context
            : string.Empty;

        CustomPropertyManager cpm = VelumRecipeSolidWorksCustomProperties.TryGetManager(modelDoc, configKey);
        if (cpm != null &&
            VelumRecipeSolidWorksCustomProperties.TryGetRawAndResolved(
                cpm, address.Name, out string raw, out string resolved))
        {
          result.Value = string.IsNullOrEmpty(resolved) ? raw : resolved;
          result.Found = true;
        }
        else
        {
          result.Note = "property_not_found";
        }

        return result;
      }

      result.Note = "unsupported_address_type:" + type;
      return result;
    }

    /// <summary>
    /// Записывает значения параметров в живой документ.
    /// </summary>
    /// <param name="modelDoc">Документ SolidWorks (UI-поток).</param>
    /// <param name="template">Шаблон контракта.</param>
    /// <param name="values">Значения: id параметра → строка.</param>
    /// <returns>Список результатов записи.</returns>
    internal static IReadOnlyList<ParameterWriteResult> WriteParameters(
        ModelDoc2 modelDoc,
        EnvironmentDocumentTemplateData template,
        IReadOnlyDictionary<string, string> values)
    {
      var results = new List<ParameterWriteResult>();
      if (modelDoc == null || template?.Parameters == null)
        return results;

      if (!IsWritable(modelDoc))
      {
        foreach (EnvironmentDocumentParameterData p in template.Parameters)
        {
          if (p == null)
            continue;
          results.Add(new ParameterWriteResult
          {
            Id = p.Id ?? string.Empty,
            Ok = false,
            Changed = false,
            Code = "document_read_only",
            Note = "Документ открыт только для чтения — запись не выполнялась."
          });
        }

        return results;
      }

      EquationMgr equations = TryGetEquationManager(modelDoc);

      foreach (EnvironmentDocumentParameterData p in template.Parameters)
      {
        if (p == null)
          continue;

        results.Add(WriteOne(modelDoc, equations, p, values));
      }

      return results;
    }

    private static ParameterWriteResult WriteOne(
        ModelDoc2 modelDoc,
        EquationMgr equations,
        EnvironmentDocumentParameterData parameter,
        IReadOnlyDictionary<string, string> values)
    {
      var result = new ParameterWriteResult
      {
        Id = parameter.Id ?? string.Empty,
        Ok = false,
        Changed = false,
        Code = "skipped"
      };

      VelumDocumentAddress address = ToAddress(parameter.Address);
      string type = (address.Type ?? string.Empty).Trim();

      // Только read-only адреса.
      if (!VelumDocumentContractRules.IsWritableAddressType(type))
      {
        result.Code = "skipped_read_only_address";
        result.Note = "Адрес «" + type + "» доступен только для чтения.";
        return result;
      }

      string writePolicy = (parameter.WritePolicy ?? string.Empty).Trim();
      if (string.Equals(writePolicy, VelumDocumentContractRules.WritePolicyNever, StringComparison.OrdinalIgnoreCase))
      {
        result.Code = "skipped_policy_never";
        result.Note = "Политика записи never.";
        return result;
      }

      // Значение из Params, иначе default.
      string rawValue = null;
      if (values != null)
        values.TryGetValue(result.Id, out rawValue);
      if (rawValue == null && parameter.Default != null)
        rawValue = Convert.ToString(parameter.Default, CultureInfo.InvariantCulture);

      // Валидация значения (тип, min/max, step, enum).
      VelumDocumentParameterValueRules.ValueValidationResult validation =
          VelumDocumentParameterValueRules.Validate(ToRuleDescriptor(parameter), rawValue);

      if (validation.Outcome == VelumDocumentParameterValueRules.ValueOutcome.Skip)
      {
        result.Code = "skipped_invalid_value";
        result.Note = "Значение отклонено: " + (validation.Reason ?? "invalid");
        return result;
      }

      string newValue = Convert.ToString(validation.Value, CultureInfo.InvariantCulture);

      if (string.Equals(type, VelumDocumentContractRules.AddressEquation, StringComparison.OrdinalIgnoreCase))
        return WriteGlobalVariable(equations, address.Name, newValue, writePolicy, result);

      if (string.Equals(type, VelumDocumentContractRules.AddressDimension, StringComparison.OrdinalIgnoreCase))
        return WriteDimension(modelDoc, address.Name, newValue, writePolicy, result);

      result.Code = "skipped_unsupported_address";
      result.Note = "Запись по адресу «" + type + "» не реализована.";
      return result;
    }

    private static ParameterWriteResult WriteGlobalVariable(
        EquationMgr equations,
        string name,
        string newValue,
        string writePolicy,
        ParameterWriteResult result)
    {
      if (equations == null)
      {
        result.Code = "equation_manager_unavailable";
        return result;
      }

      string variable = (name ?? string.Empty).Trim();
      if (!VelumDocumentContractRules.IsValidEquationName(variable))
      {
        result.Code = "invalid_equation_name";
        result.Note = "Некорректное имя переменной: «" + variable + "».";
        return result;
      }

      int index = FindGlobalVariableIndex(equations, variable);
      string current = index >= 0 ? ReadEquationValue(equations, index) : null;

      // if_empty: пишем только если пусто/отсутствует.
      if (string.Equals(writePolicy, VelumDocumentContractRules.WritePolicyIfEmpty, StringComparison.OrdinalIgnoreCase) &&
          !string.IsNullOrWhiteSpace(current))
      {
        result.Ok = true;
        result.Code = "skipped_policy_if_empty";
        result.Note = "Переменная уже задана: " + current;
        return result;
      }

      // Идемпотентность: значение уже совпадает.
      if (current != null && ValuesEqual(current, newValue))
      {
        result.Ok = true;
        result.Code = "unchanged";
        return result;
      }

      string equationText = "\"" + variable + "\" = " + newValue;

      try
      {
        int addResult;
        if (index < 0)
        {
          // -1: добавить в конец списка; true — глобальная переменная.
          addResult = equations.Add2(-1, equationText, true);
          if (addResult < 0)
          {
            result.Code = "write_failed";
            result.Note = "EquationMgr.Add2 вернул " + addResult + ".";
            return result;
          }
        }
        else
        {
          // swInConfigurationOpts_e.swThisConfiguration = 1.
          addResult = equations.SetEquationAndConfigurationOption(
              index, equationText, (int)swInConfigurationOpts_e.swThisConfiguration, null);
          if (addResult != 0)
          {
            result.Code = "write_failed";
            result.Note = "EquationMgr.SetEquationAndConfigurationOption вернул " + addResult + ".";
            return result;
          }
        }

        equations.EvaluateAll();
      }
      catch (Exception ex)
      {
        result.Code = "write_failed";
        result.Note = "Ошибка записи переменной: " + ex.Message;
        Logger.Warning("Velum DocumentParameter write equation \"" + variable + "\" failed: " + ex.Message);
        return result;
      }

      result.Ok = true;
      result.Changed = true;
      result.Code = "written";
      return result;
    }

    private static ParameterWriteResult WriteDimension(
        ModelDoc2 modelDoc,
        string fullName,
        string newValue,
        string writePolicy,
        ParameterWriteResult result)
    {
      Dimension dim = TryGetDimension(modelDoc, fullName);
      if (dim == null)
      {
        result.Code = "dimension_not_found";
        result.Note = "Размер «" + fullName + "» не найден.";
        return result;
      }

      double currentMm;
      try
      {
        currentMm = dim.SystemValue * 1000d;
      }
      catch (Exception ex)
      {
        result.Code = "dimension_read_failed";
        result.Note = ex.Message;
        return result;
      }

      if (string.Equals(writePolicy, VelumDocumentContractRules.WritePolicyIfEmpty, StringComparison.OrdinalIgnoreCase) &&
          Math.Abs(currentMm) > 1e-9)
      {
        result.Ok = true;
        result.Code = "skipped_policy_if_empty";
        return result;
      }

      if (!double.TryParse(newValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double targetMm))
      {
        result.Code = "invalid_value";
        result.Note = "Значение не является числом: " + newValue;
        return result;
      }

      if (Math.Abs(currentMm - targetMm) < 1e-9)
      {
        result.Ok = true;
        result.Code = "unchanged";
        return result;
      }

      try
      {
        int status = dim.SetSystemValue3(
            targetMm / 1000d,
            (int)swInConfigurationOpts_e.swThisConfiguration,
            null);

        if (status != 0)
        {
          result.Code = "write_failed";
          result.Note = "Dimension.SetSystemValue3 вернул " + status + ".";
          return result;
        }
      }
      catch (Exception ex)
      {
        result.Code = "write_failed";
        result.Note = "Ошибка записи размера: " + ex.Message;
        Logger.Warning("Velum DocumentParameter write dimension \"" + fullName + "\" failed: " + ex.Message);
        return result;
      }

      result.Ok = true;
      result.Changed = true;
      result.Code = "written";
      return result;
    }

    /// <summary>
    /// Читает имя активной конфигурации документа (для <c>doc.key</c>).
    /// </summary>
    /// <param name="modelDoc">Документ SolidWorks.</param>
    /// <returns>Имя конфигурации либо пустая строка.</returns>
    internal static string GetActiveConfigurationName(ModelDoc2 modelDoc)
    {
      try
      {
        string name = modelDoc?.ConfigurationManager?.ActiveConfiguration?.Name;
        return string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();
      }
      catch
      {
        return string.Empty;
      }
    }

    /// <summary>
    /// Выходит из эскиза перед записью параметров (COM: <c>SketchManager.InsertSketch(true)</c>).
    /// </summary>
    /// <param name="modelDoc">Документ SolidWorks.</param>
    /// <returns>true, если эскиз закрыт либо его не было.</returns>
    internal static bool EnsureSketchClosed(ModelDoc2 modelDoc)
    {
      try
      {
        SketchManager sketchManager = modelDoc?.SketchManager;
        if (sketchManager == null)
          return true;

        // PIA: ActiveSketch — объект Sketch, а не bool; эскиз активен, если != null.
        if (sketchManager.ActiveSketch == null)
          return true;

        sketchManager.InsertSketch(true);
        return true;
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum DocumentParameter close sketch failed: " + ex.Message);
        return false;
      }
    }

    /// <summary>Преобразует адрес DTO в плоскую структуру правил.</summary>
    /// <param name="address">Адрес DTO.</param>
    /// <returns>Разобранный адрес правил.</returns>
    internal static VelumDocumentAddress ToAddress(EnvironmentDocumentParameterAddressData address)
    {
      if (address == null)
        return new VelumDocumentAddress();

      return new VelumDocumentAddress
      {
        Type = address.Type ?? string.Empty,
        Name = address.Name ?? string.Empty,
        Context = address.Context ?? string.Empty
      };
    }

    /// <summary>Преобразует параметр DTO в дескриптор правил валидации.</summary>
    /// <param name="parameter">Параметр контракта.</param>
    /// <returns>Дескриптор правил.</returns>
    internal static VelumDocumentParameterRuleDescriptor ToRuleDescriptor(EnvironmentDocumentParameterData parameter)
    {
      if (parameter == null)
        return null;

      var values = new List<string>();
      if (parameter.Values != null)
      {
        foreach (EnumValueData v in parameter.Values)
        {
          if (v != null && !string.IsNullOrWhiteSpace(v.Key))
            values.Add(v.Key.Trim());
        }
      }

      return new VelumDocumentParameterRuleDescriptor
      {
        Id = parameter.Id,
        Type = parameter.Type,
        Min = parameter.Min,
        Max = parameter.Max,
        Default = parameter.Default,
        Step = parameter.Step,
        Values = values,
        OnInvalid = parameter.OnInvalid
      };
    }

    /// <summary>
    /// Возвращает менеджер уравнений документа (глобальные переменные) либо <c>null</c>.
    /// </summary>
    /// <param name="modelDoc">Документ SolidWorks.</param>
    /// <returns>COM-объект <c>EquationMgr</c> либо <c>null</c>.</returns>
    internal static EquationMgr TryGetEquationManager(ModelDoc2 modelDoc)
    {
      try
      {
        return modelDoc?.GetEquationMgr();
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum DocumentParameter GetEquationMgr failed: " + ex.Message);
        return null;
      }
    }

    private static Dimension TryGetDimension(ModelDoc2 modelDoc, string fullName)
    {
      string name = (fullName ?? string.Empty).Trim();
      if (name.Length == 0)
        return null;

      try
      {
        return modelDoc?.IParameter(name);
      }
      catch
      {
        return null;
      }
    }

    private static int FindGlobalVariableIndex(EquationMgr equations, string variableName)
    {
      try
      {
        int count = equations.GetCount();
        for (int i = 0; i < count; i++)
        {
          bool isGlobal;
          try
          {
            isGlobal = equations.GlobalVariable[i];
          }
          catch
          {
            continue;
          }

          if (!isGlobal)
            continue;

          string raw = equations.Equation[i];
          if (VelumDocumentContractRules.TryParseEquation(raw, out string name, out _) &&
              string.Equals(name, variableName, StringComparison.OrdinalIgnoreCase))
            return i;
        }
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum DocumentParameter enumerate equations failed: " + ex.Message);
      }

      return -1;
    }

    private static string ReadEquationValue(EquationMgr equations, int index)
    {
      try
      {
        string raw = equations.Equation[index];
        return VelumDocumentContractRules.TryParseEquation(raw, out _, out string value)
            ? value
            : null;
      }
      catch
      {
        return null;
      }
    }

    internal static string TryReadGlobalVariable(EquationMgr equations, string variableName)
    {
      if (equations == null)
        return null;

      int index = FindGlobalVariableIndex(equations, variableName);
      return index < 0 ? null : ReadEquationValue(equations, index);
    }

    private static bool ValuesEqual(string a, string b)
    {
      if (double.TryParse((a ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double da) &&
          double.TryParse((b ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double db))
        return Math.Abs(da - db) < 1e-9;

      return string.Equals((a ?? string.Empty).Trim(), (b ?? string.Empty).Trim(), StringComparison.Ordinal);
    }
  }
}