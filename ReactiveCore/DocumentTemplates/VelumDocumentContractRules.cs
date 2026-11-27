using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Velum.ReactiveCore.DocumentTemplates
{
  /// <summary>
  /// Pure-правила адресов параметров контракта документа: разбор, форматирование и
  /// проверка совместимости с политикой записи. Без COM и ISIDA.
  /// </summary>
  internal static class VelumDocumentContractRules
  {
    /// <summary>Адрес — глобальная переменная EquationMgr (канал записи).</summary>
    internal const string AddressEquation = "equation";

    /// <summary>Адрес — размер по <c>Dimension.FullName</c> (fallback, только чтение/repair).</summary>
    internal const string AddressDimension = "dimension";

    /// <summary>Адрес — пользовательское свойство (только чтение).</summary>
    internal const string AddressProperty = "property";

    /// <summary>Адрес — свойство конфигурации (только чтение).</summary>
    internal const string AddressConfigProperty = "configProperty";

    /// <summary>Адрес — операция модели (резерв).</summary>
    internal const string AddressFeature = "feature";

    /// <summary>Политика записи: писать всегда.</summary>
    internal const string WritePolicyAlways = "always";

    /// <summary>Политика записи: писать, только если пусто.</summary>
    internal const string WritePolicyIfEmpty = "if_empty";

    /// <summary>Политика записи: чинить существующее значение.</summary>
    internal const string WritePolicyRepair = "repair";

    /// <summary>Политика записи: не писать никогда.</summary>
    internal const string WritePolicyNever = "never";

    /// <summary>Поведение при невалидном значении: пропустить.</summary>
    internal const string OnInvalidSkip = "skip";

    /// <summary>Поведение при невалидном значении: подставить default.</summary>
    internal const string OnInvalidDefault = "default";

    /// <summary>Поведение при невалидном значении: зажать в диапазон.</summary>
    internal const string OnInvalidClamp = "clamp";

    private static readonly Regex EquationNamePattern = new Regex(
        @"^[A-Za-z_][A-Za-z0-9_]*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Разбирает адрес параметра в структуру.</summary>
    /// <param name="address">Строка вида <c>equation:PLATE_W</c>.</param>
    /// <returns>Разобранный адрес; при ошибке — с пустым <see cref="VelumDocumentAddress.Type"/>.</returns>
    internal static VelumDocumentAddress ParseAddress(string address)
    {
      var result = new VelumDocumentAddress();
      string raw = (address ?? string.Empty).Trim();
      if (raw.Length == 0)
        return result;

      int colon = raw.IndexOf(':');
      if (colon <= 0)
      {
        result.Type = raw;
        return result;
      }

      result.Type = raw.Substring(0, colon).Trim();
      string rest = raw.Substring(colon + 1).Trim();

      if (string.Equals(result.Type, AddressConfigProperty, StringComparison.OrdinalIgnoreCase))
      {
        int slash = rest.IndexOf('/');
        if (slash > 0)
        {
          result.Context = rest.Substring(0, slash).Trim();
          result.Name = rest.Substring(slash + 1).Trim();
        }
        else
        {
          result.Name = rest;
        }
      }
      else
      {
        result.Name = rest;
      }

      return result;
    }

    /// <summary>Форматирует адрес обратно в строку.</summary>
    /// <param name="address">Разобранный адрес.</param>
    /// <returns>Строка адреса либо пустая строка.</returns>
    internal static string FormatAddress(VelumDocumentAddress address)
    {
      if (address == null || string.IsNullOrWhiteSpace(address.Type))
        return string.Empty;

      string type = address.Type.Trim();
      string name = (address.Name ?? string.Empty).Trim();

      if (string.Equals(type, AddressConfigProperty, StringComparison.OrdinalIgnoreCase))
      {
        string context = (address.Context ?? string.Empty).Trim();
        return context.Length == 0 ? type + ":" + name : type + ":" + context + "/" + name;
      }

      return type + ":" + name;
    }

    /// <summary>true, если тип адреса допускает запись значения в документ.</summary>
    /// <param name="addressType">Тип адреса.</param>
    /// <returns>false для <c>property</c> и <c>configProperty</c>.</returns>
    internal static bool IsWritableAddressType(string addressType)
    {
      string type = (addressType ?? string.Empty).Trim();
      if (type.Length == 0)
        return false;

      return !string.Equals(type, AddressProperty, StringComparison.OrdinalIgnoreCase)
          && !string.Equals(type, AddressConfigProperty, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Проверяет совместимость адреса с политикой записи: адрес «только чтение» обязан
    /// иметь <c>writePolicy = never</c>.
    /// </summary>
    /// <param name="addressType">Тип адреса.</param>
    /// <param name="writePolicy">Политика записи.</param>
    /// <returns>true, если комбинация допустима.</returns>
    internal static bool IsAddressPolicyCombinationValid(string addressType, string writePolicy)
    {
      if (IsWritableAddressType(addressType))
        return true;

      return string.Equals(
          (writePolicy ?? string.Empty).Trim(),
          WritePolicyNever,
          StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>true, если имя переменной уравнения корректно.</summary>
    /// <param name="name">Имя переменной.</param>
    /// <returns>true для <c>[A-Za-z_][A-Za-z0-9_]*</c>.</returns>
    internal static bool IsValidEquationName(string name)
    {
      string value = (name ?? string.Empty).Trim();
      return value.Length > 0 && EquationNamePattern.IsMatch(value);
    }

    /// <summary>
    /// Разбирает значение глобальной переменной EquationMgr вида <c>"NAME" = value</c>.
    /// </summary>
    /// <param name="raw">Сырая строка уравнения.</param>
    /// <param name="name">Имя переменной.</param>
    /// <param name="valueText">Текстовое значение справа от «=».</param>
    /// <returns>true, если строка разобрана.</returns>
    internal static bool TryParseEquation(string raw, out string name, out string valueText)
    {
      name = null;
      valueText = null;

      string text = (raw ?? string.Empty).Trim();
      if (text.Length == 0)
        return false;

      int eq = text.IndexOf('=');
      if (eq <= 0)
        return false;

      string left = text.Substring(0, eq).Trim();
      if (left.Length >= 2 && left[0] == '"' && left[left.Length - 1] == '"')
        left = left.Substring(1, left.Length - 2).Trim();

      if (left.Length == 0)
        return false;

      name = left;
      valueText = text.Substring(eq + 1).Trim().Trim('"').Trim();
      return true;
    }

    /// <summary>
    /// Зажимает значение в <c>[min + step, max − step]</c>, чтобы после зажатия оно
    /// оставалось в допустимом диапазоне с учётом шага.
    /// </summary>
    /// <param name="value">Исходное значение.</param>
    /// <param name="min">Минимум.</param>
    /// <param name="max">Максимум.</param>
    /// <param name="step">Шаг (по умолчанию 1).</param>
    /// <returns>Значение внутри допустимого диапазона.</returns>
    internal static double ClampValue(double value, double min, double max, double step)
    {
      double effectiveStep = step > 0 ? step : 1d;
      double low = min + effectiveStep;
      double high = max - effectiveStep;

      // Диапазон уже шага — зажимаем в [min, max] без сдвига.
      if (low > high)
      {
        low = min;
        high = max;
      }

      if (value < low)
        return low;
      if (value > high)
        return high;
      return value;
    }

    /// <summary>true, если строка — допустимый тип адреса.</summary>
    /// <param name="addressType">Тип адреса.</param>
    /// <returns>true для equation/dimension/property/configProperty/feature.</returns>
    internal static bool IsKnownAddressType(string addressType)
    {
      string type = (addressType ?? string.Empty).Trim();
      return string.Equals(type, AddressEquation, StringComparison.OrdinalIgnoreCase)
          || string.Equals(type, AddressDimension, StringComparison.OrdinalIgnoreCase)
          || string.Equals(type, AddressProperty, StringComparison.OrdinalIgnoreCase)
          || string.Equals(type, AddressConfigProperty, StringComparison.OrdinalIgnoreCase)
          || string.Equals(type, AddressFeature, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>true, если политика записи известна.</summary>
    /// <param name="writePolicy">Политика.</param>
    /// <returns>true для always/if_empty/repair/never.</returns>
    internal static bool IsKnownWritePolicy(string writePolicy)
    {
      string value = (writePolicy ?? string.Empty).Trim();
      return string.Equals(value, WritePolicyAlways, StringComparison.OrdinalIgnoreCase)
          || string.Equals(value, WritePolicyIfEmpty, StringComparison.OrdinalIgnoreCase)
          || string.Equals(value, WritePolicyRepair, StringComparison.OrdinalIgnoreCase)
          || string.Equals(value, WritePolicyNever, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>true, если поведение при невалидном значении известно.</summary>
    /// <param name="onInvalid">Поведение.</param>
    /// <returns>true для skip/default/clamp.</returns>
    internal static bool IsKnownOnInvalid(string onInvalid)
    {
      string value = (onInvalid ?? string.Empty).Trim();
      return string.Equals(value, OnInvalidSkip, StringComparison.OrdinalIgnoreCase)
          || string.Equals(value, OnInvalidDefault, StringComparison.OrdinalIgnoreCase)
          || string.Equals(value, OnInvalidClamp, StringComparison.OrdinalIgnoreCase);
    }
  }

  /// <summary>Разобранный адрес параметра документа.</summary>
  internal sealed class VelumDocumentAddress
  {
    /// <summary>Тип адреса (<c>equation</c>/<c>dimension</c>/…).</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Имя переменной/свойства/размера.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Контекст (имя конфигурации для <c>configProperty</c>).</summary>
    public string Context { get; set; } = string.Empty;
  }
}
