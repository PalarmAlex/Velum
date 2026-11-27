using System.Text;
using Velum.ReactiveCore.DocumentTemplates;

namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Проба значения параметра/свойства документа, формируемая <b>данными</b> библиотеки
  /// шаблонов (ключ вида <c>Velum.Solid.Parameter.&lt;templateId&gt;.&lt;paramId&gt;</c>).
  /// Значение и зона допуска берутся из кэшированного снимка восприятия
  /// (<see cref="VelumDocumentPerceptionService"/>), поэтому опрос на такте пульса не
  /// обращается к COM. Если снимок не распознан или параметр отсутствует — проба
  /// неприменима (возвращает <c>false</c>), чтобы не создавать ложного давления.
  /// </summary>
  internal static class VelumSolidDocumentParametersProbe
  {
    /// <summary>
    /// Оценивает параметр документа по ключу пробы.
    /// </summary>
    /// <param name="probeKey">Ключ пробы (параметр либо свойство).</param>
    /// <param name="value">Оценка 0…100 (зона допуска).</param>
    /// <param name="detail">Пояснение для тултипа (значение, зона, единица).</param>
    /// <returns>true, если проба применима (есть снимок и параметр в нём).</returns>
    internal static bool TryScore(string probeKey, out float value, out string detail)
    {
      value = VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
      detail = null;

      if (!TryResolveParamId(probeKey, out string templateId, out string paramId))
        return false;

      VelumDocumentSnapshot snapshot = VelumDocumentPerceptionService.Current;
      if (snapshot == null || !snapshot.Recognized)
        return false;

      // Ключ привязан к определённому шаблону: если распознан другой — проба неприменима.
      if (!string.Equals(snapshot.TemplateId, templateId, System.StringComparison.OrdinalIgnoreCase))
        return false;

      if (!snapshot.Parameters.TryGetValue(paramId, out VelumDocumentParameterReading reading))
        return false;

      value = reading.Score;
      detail = BuildDetail(reading);
      return true;
    }

    private static bool TryResolveParamId(string probeKey, out string templateId, out string paramId)
    {
      if (VelumDocumentProbeKeys.TryParseParameterProbeKey(probeKey, out templateId, out paramId))
        return true;

      if (VelumDocumentProbeKeys.TryParsePropertyProbeKey(probeKey, out templateId, out paramId))
        return true;

      templateId = string.Empty;
      paramId = string.Empty;
      return false;
    }

    private static string BuildDetail(VelumDocumentParameterReading reading)
    {
      var sb = new StringBuilder();
      sb.Append(reading.Label);
      sb.Append(": ");
      sb.Append(reading.Found ? (reading.RawValue ?? "—") : "не найден");
      if (!string.IsNullOrWhiteSpace(reading.Unit))
        sb.Append(' ').Append(reading.Unit);
      sb.Append(System.Environment.NewLine);
      sb.Append("Зона: ").Append(ZoneToText(reading.Zone));
      if (!reading.Found && !string.IsNullOrWhiteSpace(reading.Note))
      {
        sb.Append(System.Environment.NewLine);
        sb.Append(reading.Note);
      }

      return sb.ToString();
    }

    private static string ZoneToText(VelumDocumentParameterProbeRules.Zone zone)
    {
      switch (zone)
      {
        case VelumDocumentParameterProbeRules.Zone.Warn:
          return "warn";
        case VelumDocumentParameterProbeRules.Zone.Bad:
          return "плохо";
        default:
          return "норма";
      }
    }
  }
}
