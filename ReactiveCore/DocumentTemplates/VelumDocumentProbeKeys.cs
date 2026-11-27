using System;

namespace Velum.ReactiveCore.DocumentTemplates
{
  /// <summary>
  /// Ключи проб восприятия документа, формируемые <b>данными</b> библиотеки шаблонов,
  /// а не перечислением в коде. Префиксная проверка позволяет добавлять параметры и
  /// свойства без пересборки: <see cref="IsParameterProbeKey"/> и
  /// <see cref="IsPropertyProbeKey"/> распознают любой ключ вида
  /// <c>Velum.Solid.Parameter.&lt;templateId&gt;.&lt;paramId&gt;</c> и
  /// <c>Velum.Solid.Property.&lt;templateId&gt;.&lt;paramId&gt;</c>.
  /// </summary>
  internal static class VelumDocumentProbeKeys
  {
    /// <summary>Префикс пробы значения параметра документа (зона good/warn/bad).</summary>
    public const string ParameterProbePrefix = "Velum.Solid.Parameter.";

    /// <summary>Префикс пробы наличия/значения пользовательского свойства документа.</summary>
    public const string PropertyProbePrefix = "Velum.Solid.Property.";

    /// <summary>Формирует ключ пробы параметра.</summary>
    /// <param name="templateId">Идентификатор шаблона.</param>
    /// <param name="paramId">Идентификатор параметра.</param>
    /// <returns>Ключ пробы либо пустая строка при пустых частях.</returns>
    public static string FormatParameterProbeKey(string templateId, string paramId)
    {
      return Format(ParameterProbePrefix, templateId, paramId);
    }

    /// <summary>Формирует ключ пробы свойства.</summary>
    /// <param name="templateId">Идентификатор шаблона.</param>
    /// <param name="paramId">Идентификатор параметра-свойства.</param>
    /// <returns>Ключ пробы либо пустая строка при пустых частях.</returns>
    public static string FormatPropertyProbeKey(string templateId, string paramId)
    {
      return Format(PropertyProbePrefix, templateId, paramId);
    }

    /// <summary>true, если ключ — проба значения параметра (по префиксу).</summary>
    /// <param name="probeKey">Ключ пробы.</param>
    /// <returns>Признак принадлежности к пробам параметров.</returns>
    public static bool IsParameterProbeKey(string probeKey)
    {
      return HasPrefix(probeKey, ParameterProbePrefix);
    }

    /// <summary>true, если ключ — проба свойства (по префиксу).</summary>
    /// <param name="probeKey">Ключ пробы.</param>
    /// <returns>Признак принадлежности к пробам свойств.</returns>
    public static bool IsPropertyProbeKey(string probeKey)
    {
      return HasPrefix(probeKey, PropertyProbePrefix);
    }

    /// <summary>true, если ключ относится к пробам документа (параметр либо свойство).</summary>
    /// <param name="probeKey">Ключ пробы.</param>
    /// <returns>Признак документной пробы данных.</returns>
    public static bool IsDocumentDataProbeKey(string probeKey)
    {
      return IsParameterProbeKey(probeKey) || IsPropertyProbeKey(probeKey);
    }

    /// <summary>
    /// Разбирает ключ пробы параметра на <c>templateId</c> и <c>paramId</c>.
    /// Идентификатор параметра не содержит точек (валидируется шаблоном <c>[A-Za-z_][A-Za-z0-9_]*</c>),
    /// а идентификатор шаблона может (например <c>plate.standard</c>), поэтому граница —
    /// <b>последняя</b> точка: paramId — последний сегмент, templateId — всё до него.
    /// </summary>
    /// <param name="probeKey">Ключ пробы.</param>
    /// <param name="templateId">Извлечённый идентификатор шаблона.</param>
    /// <param name="paramId">Извлечённый идентификатор параметра.</param>
    /// <returns><c>true</c>, если ключ корректного формата.</returns>
    public static bool TryParseParameterProbeKey(string probeKey, out string templateId, out string paramId)
    {
      return TryParse(ParameterProbePrefix, probeKey, out templateId, out paramId);
    }

    /// <summary>
    /// Разбирает ключ пробы свойства на <c>templateId</c> и <c>paramId</c>.
    /// </summary>
    /// <param name="probeKey">Ключ пробы.</param>
    /// <param name="templateId">Извлечённый идентификатор шаблона.</param>
    /// <param name="paramId">Извлечённый идентификатор параметра-свойства.</param>
    /// <returns><c>true</c>, если ключ корректного формата.</returns>
    public static bool TryParsePropertyProbeKey(string probeKey, out string templateId, out string paramId)
    {
      return TryParse(PropertyProbePrefix, probeKey, out templateId, out paramId);
    }

    private static string Format(string prefix, string templateId, string paramId)
    {
      string t = (templateId ?? string.Empty).Trim();
      string p = (paramId ?? string.Empty).Trim();
      if (t.Length == 0 || p.Length == 0)
        return string.Empty;

      return prefix + t + "." + p;
    }

    private static bool HasPrefix(string probeKey, string prefix)
    {
      string key = (probeKey ?? string.Empty).Trim();
      return key.Length > prefix.Length && key.StartsWith(prefix, StringComparison.Ordinal);
    }

    private static bool TryParse(string prefix, string probeKey, out string templateId, out string paramId)
    {
      templateId = string.Empty;
      paramId = string.Empty;

      string key = (probeKey ?? string.Empty).Trim();
      if (key.Length <= prefix.Length || !key.StartsWith(prefix, StringComparison.Ordinal))
        return false;

      string rest = key.Substring(prefix.Length);
      int dot = rest.LastIndexOf('.');
      if (dot <= 0 || dot >= rest.Length - 1)
        return false;

      templateId = rest.Substring(0, dot);
      paramId = rest.Substring(dot + 1);
      return templateId.Length > 0 && paramId.Length > 0;
    }
  }
}
