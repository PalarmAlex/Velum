using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Velum.ReactiveCore.DocumentTemplates
{
  /// <summary>
  /// Pure-правила структурного хэша контракта шаблона (<c>ContractHash</c>) и версии
  /// политики (<c>PolicyVersion</c>). Без COM, файлов и ISIDA.
  /// </summary>
  /// <remarks>
  /// <c>ContractHash</c> покрывает только <b>структуру</b> контракта (id/type/unit/address),
  /// но не допуски: правка <c>min/max/default/step/writePolicy/onInvalid</c> не должна
  /// «ломать» уже созданные документы — за это отвечает отдельный <c>PolicyVersion</c>.
  /// </remarks>
  internal static class VelumDocumentTemplateHashRules
  {
    /// <summary>Сколько hex-символов SHA-256 попадает в <c>ContractHash</c>.</summary>
    internal const int ContractHashLength = 16;

    /// <summary>
    /// Строит каноническую строку структуры контракта:
    /// <c>templateId|schemaVersion|sorted(id:type:unit:address)</c>.
    /// </summary>
    /// <param name="templateId">Идентификатор шаблона.</param>
    /// <param name="templateSchema">Версия схемы шаблона.</param>
    /// <param name="parameters">Параметры шаблона.</param>
    /// <returns>Каноническая строка.</returns>
    internal static string BuildCanonicalString(
        string templateId,
        string templateSchema,
        IEnumerable<VelumDocumentParameterDescriptor> parameters)
    {
      var parts = new List<string>();
      if (parameters != null)
      {
        foreach (VelumDocumentParameterDescriptor p in parameters)
        {
          if (p == null)
            continue;
          parts.Add(
              (p.Id ?? string.Empty).Trim() + ":" +
              (p.Type ?? string.Empty).Trim().ToLowerInvariant() + ":" +
              (p.Unit ?? string.Empty).Trim() + ":" +
              (p.Address ?? string.Empty).Trim());
        }
      }

      parts.Sort(StringComparer.Ordinal);

      var sb = new StringBuilder();
      sb.Append((templateId ?? string.Empty).Trim());
      sb.Append('|');
      sb.Append((templateSchema ?? string.Empty).Trim());
      sb.Append('|');
      sb.Append(string.Join(";", parts));
      return sb.ToString();
    }

    /// <summary>
    /// Считает <c>ContractHash</c>: SHA-256 от канонической строки, первые
    /// <see cref="ContractHashLength"/> hex-символов в нижнем регистре.
    /// </summary>
    /// <param name="templateId">Идентификатор шаблона.</param>
    /// <param name="templateSchema">Версия схемы шаблона.</param>
    /// <param name="parameters">Параметры шаблона.</param>
    /// <returns>Hex-строка длиной 16 либо пустая строка.</returns>
    internal static string ComputeContractHash(
        string templateId,
        string templateSchema,
        IEnumerable<VelumDocumentParameterDescriptor> parameters)
    {
      if (string.IsNullOrWhiteSpace(templateId))
        return string.Empty;

      string canonical = BuildCanonicalString(templateId, templateSchema, parameters);
      return Sha256HexPrefix(canonical, ContractHashLength);
    }

    /// <summary>Считает SHA-256 и возвращает первые <paramref name="length"/> hex-символов.</summary>
    /// <param name="text">Входная строка (UTF-8).</param>
    /// <param name="length">Сколько hex-символов вернуть.</param>
    /// <returns>Hex-строка в нижнем регистре.</returns>
    internal static string Sha256HexPrefix(string text, int length)
    {
      if (string.IsNullOrEmpty(text) || length <= 0)
        return string.Empty;

      using (var sha = SHA256.Create())
      {
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
        var sb = new StringBuilder(hash.Length * 2);
        foreach (byte b in hash)
          sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));

        return sb.Length <= length ? sb.ToString() : sb.ToString(0, length);
      }
    }
  }

  /// <summary>
  /// Дескриптор параметра для расчёта <c>ContractHash</c>: только структурные поля.
  /// Допуски и политики сюда не входят.
  /// </summary>
  internal sealed class VelumDocumentParameterDescriptor
  {
    /// <summary>Идентификатор параметра.</summary>
    public string Id { get; set; }

    /// <summary>Тип параметра (<c>number</c>/<c>int</c>/<c>bool</c>/<c>enum</c>/<c>text</c>).</summary>
    public string Type { get; set; }

    /// <summary>Единица измерения.</summary>
    public string Unit { get; set; }

    /// <summary>Канонический адрес (<c>equation:NAME</c> и т.п.).</summary>
    public string Address { get; set; }
  }
}
