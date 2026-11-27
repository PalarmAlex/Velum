using System;
using System.Collections.Generic;
using System.Globalization;

namespace Velum.ReactiveCore.DocumentTemplates
{
  /// <summary>
  /// Pure-правила валидации значений параметров документа: тип, min/max, step, enum
  /// и применение <c>onInvalid</c> (<c>skip</c>/<c>default</c>/<c>clamp</c>).
  /// Без COM и ISIDA — линкуется в тесты.
  /// </summary>
  internal static class VelumDocumentParameterValueRules
  {
    /// <summary>Результат валидации значения параметра.</summary>
    internal enum ValueOutcome
    {
      /// <summary>Значение допустимо и может быть записано.</summary>
      Valid = 0,

      /// <summary>Значение недопустимо, но успешно исправлено (<c>default</c>/<c>clamp</c>).</summary>
      Repaired = 1,

      /// <summary>Значение недопустимо, шаг нужно пропустить (<c>skip</c>).</summary>
      Skip = 2
    }

    /// <summary>Результат применения правил к значению.</summary>
    internal sealed class ValueValidationResult
    {
      /// <summary>Итоговое значение (может быть <c>null</c> при <see cref="ValueOutcome.Skip"/>).</summary>
      public object Value { get; set; }

      /// <summary>Исход валидации.</summary>
      public ValueOutcome Outcome { get; set; }

      /// <summary>Причина (для лога и результата шага рецепта).</summary>
      public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Применяет правила валидации к значению параметра.
    /// </summary>
    /// <param name="parameter">Описание параметра.</param>
    /// <param name="rawValue">Сырое значение (из памяти/Params/контракта).</param>
    /// <returns>Результат с итоговым значением.</returns>
    internal static ValueValidationResult Validate(
        VelumDocumentParameterRuleDescriptor parameter,
        string rawValue)
    {
      var result = new ValueValidationResult { Value = rawValue };
      if (parameter == null)
      {
        result.Outcome = ValueOutcome.Skip;
        result.Reason = "parameter_missing";
        return result;
      }

      string type = (parameter.Type ?? string.Empty).Trim().ToLowerInvariant();

      if (string.Equals(type, "number", StringComparison.Ordinal))
        return ValidateNumeric(parameter, rawValue, allowFraction: true);
      if (string.Equals(type, "int", StringComparison.Ordinal))
        return ValidateNumeric(parameter, rawValue, allowFraction: false);
      if (string.Equals(type, "bool", StringComparison.Ordinal))
        return ValidateBool(parameter, rawValue);
      if (string.Equals(type, "enum", StringComparison.Ordinal))
        return ValidateEnum(parameter, rawValue);

      // text — принимаем любое значение.
      result.Outcome = ValueOutcome.Valid;
      return result;
    }

    private static ValueValidationResult ValidateNumeric(
        VelumDocumentParameterRuleDescriptor parameter,
        string rawValue,
        bool allowFraction)
    {
      var result = new ValueValidationResult { Value = rawValue };
      string text = (rawValue ?? string.Empty).Trim();

      double value;
      bool parsed = double.TryParse(
          text,
          NumberStyles.Float,
          CultureInfo.InvariantCulture,
          out value);

      if (!parsed)
      {
        result.Reason = "not_a_number";
        return ApplyOnInvalid(parameter, result);
      }

      if (!allowFraction && Math.Abs(value - Math.Round(value)) > 1e-9)
      {
        result.Reason = "not_an_integer";
        return ApplyOnInvalid(parameter, result);
      }

      double min = parameter.Min ?? double.MinValue;
      double max = parameter.Max ?? double.MaxValue;
      double step = parameter.Step ?? 1d;

      if (value < min || value > max)
      {
        result.Reason = "out_of_range";
        result.Value = value;
        return ApplyOnInvalid(parameter, result);
      }

      if (step > 0 && parameter.Min.HasValue)
      {
        double offset = value - min;
        double remainder = offset % step;
        bool onGrid = Math.Abs(remainder) < 1e-9
            || Math.Abs(remainder - step) < 1e-9;
        if (!onGrid)
        {
          result.Reason = "step_mismatch";
          result.Value = value;
          return ApplyOnInvalid(parameter, result);
        }
      }

      result.Value = allowFraction
          ? (object)value
          : (object)(long)Math.Round(value);
      result.Outcome = ValueOutcome.Valid;
      return result;
    }

    private static ValueValidationResult ValidateBool(
        VelumDocumentParameterRuleDescriptor parameter,
        string rawValue)
    {
      var result = new ValueValidationResult { Value = rawValue };
      string text = (rawValue ?? string.Empty).Trim();

      if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase) ||
          string.Equals(text, "1", StringComparison.Ordinal))
      {
        result.Value = true;
        result.Outcome = ValueOutcome.Valid;
        return result;
      }

      if (string.Equals(text, "false", StringComparison.OrdinalIgnoreCase) ||
          string.Equals(text, "0", StringComparison.Ordinal))
      {
        result.Value = false;
        result.Outcome = ValueOutcome.Valid;
        return result;
      }

      result.Reason = "not_a_bool";
      return ApplyOnInvalid(parameter, result);
    }

    private static ValueValidationResult ValidateEnum(
        VelumDocumentParameterRuleDescriptor parameter,
        string rawValue)
    {
      var result = new ValueValidationResult { Value = rawValue };
      string text = (rawValue ?? string.Empty).Trim();

      if (parameter.Values != null)
      {
        foreach (string key in parameter.Values)
        {
          if (string.Equals((key ?? string.Empty).Trim(), text, StringComparison.Ordinal))
          {
            result.Outcome = ValueOutcome.Valid;
            return result;
          }
        }
      }

      result.Reason = "not_in_enum";
      return ApplyOnInvalid(parameter, result);
    }

    private static ValueValidationResult ApplyOnInvalid(
        VelumDocumentParameterRuleDescriptor parameter,
        ValueValidationResult result)
    {
      string onInvalid = (parameter.OnInvalid ?? string.Empty).Trim().ToLowerInvariant();

      if (string.Equals(onInvalid, "default", StringComparison.Ordinal))
      {
        result.Value = parameter.Default;
        result.Outcome = ValueOutcome.Repaired;
        return result;
      }

      if (string.Equals(onInvalid, "clamp", StringComparison.Ordinal) &&
          result.Value is double numeric &&
          parameter.Min.HasValue && parameter.Max.HasValue)
      {
        double step = parameter.Step ?? 1d;
        result.Value = VelumDocumentContractRules.ClampValue(
            numeric, parameter.Min.Value, parameter.Max.Value, step);
        result.Outcome = ValueOutcome.Repaired;
        return result;
      }

      // skip — и любое неизвестное поведение.
      result.Value = null;
      result.Outcome = ValueOutcome.Skip;
      return result;
    }
  }

  /// <summary>
  /// Плоский дескриптор параметра для правил валидации значений
  /// (без зависимости от COM/DTO контракта).
  /// </summary>
  internal sealed class VelumDocumentParameterRuleDescriptor
  {
    /// <summary>Идентификатор параметра.</summary>
    public string Id { get; set; }

    /// <summary>Тип (<c>number</c>/<c>int</c>/<c>bool</c>/<c>enum</c>/<c>text</c>).</summary>
    public string Type { get; set; }

    /// <summary>Минимум (для числовых).</summary>
    public double? Min { get; set; }

    /// <summary>Максимум (для числовых).</summary>
    public double? Max { get; set; }

    /// <summary>Значение по умолчанию.</summary>
    public object Default { get; set; }

    /// <summary>Шаг (по умолчанию 1).</summary>
    public double? Step { get; set; }

    /// <summary>Допустимые значения enum.</summary>
    public IReadOnlyList<string> Values { get; set; }

    /// <summary>Поведение при невалидном значении.</summary>
    public string OnInvalid { get; set; }
  }
}
