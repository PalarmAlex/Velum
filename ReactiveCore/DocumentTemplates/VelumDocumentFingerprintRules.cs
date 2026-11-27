using System;
using System.Collections.Generic;

namespace Velum.ReactiveCore.DocumentTemplates
{
  /// <summary>
  /// Pure-правила распознавания стороннего документа по отпечатку шаблона:
  /// гейт по типу документа/числу конфигураций, расчёт <c>confidence</c> и выбор
  /// кандидата с учётом неоднозначности. Веса и пороги приходят из настроек.
  /// </summary>
  internal static class VelumDocumentFingerprintRules
  {
    /// <summary>Веса и пороги распознавания (значения по умолчанию — в <c>VelumAppConfig</c>).</summary>
    internal sealed class Rules
    {
      /// <summary>Вес совпадения обязательных уравнений.</summary>
      public double WeightEquations { get; set; } = 0.50;

      /// <summary>Вес совпадения обязательных свойств.</summary>
      public double WeightProperties { get; set; } = 0.25;

      /// <summary>Вес совпадения обязательных операций.</summary>
      public double WeightFeatures { get; set; } = 0.15;

      /// <summary>Базовый вес (совпадение типа документа).</summary>
      public double WeightBase { get; set; } = 0.10;

      /// <summary>Минимальный <c>confidence</c> для принятия кандидата.</summary>
      public double MinConfidence { get; set; } = 0.8;

      /// <summary>Минимальная разница между первым и вторым кандидатом (иначе неоднозначность).</summary>
      public double AmbiguityDelta { get; set; } = 0.05;
    }

    /// <summary>Снимок фактических признаков живого документа.</summary>
    internal sealed class DocumentFacts
    {
      /// <summary>Тип документа (<c>part</c>/<c>asm</c>/<c>drw</c>).</summary>
      public string DocumentType { get; set; }

      /// <summary>Число конфигураций документа.</summary>
      public int ConfigurationCount { get; set; }

      /// <summary>Имена глобальных переменных уравнений.</summary>
      public HashSet<string> Equations { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

      /// <summary>Имена пользовательских свойств документа.</summary>
      public HashSet<string> Properties { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

      /// <summary>Имена операций модели.</summary>
      public HashSet<string> Features { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Отпечаток шаблона (только признаки, без значений).</summary>
    internal sealed class TemplateFingerprint
    {
      /// <summary>Идентификатор шаблона.</summary>
      public string TemplateId { get; set; }

      /// <summary>Тип документа шаблона.</summary>
      public string DocumentType { get; set; }

      /// <summary>Минимальное число конфигураций (0/null — не проверять).</summary>
      public int? ConfigCountMin { get; set; }

      /// <summary>Обязательные уравнения.</summary>
      public IReadOnlyList<string> RequiredEquations { get; set; }

      /// <summary>Обязательные свойства.</summary>
      public IReadOnlyList<string> RequiredProperties { get; set; }

      /// <summary>Обязательные операции.</summary>
      public IReadOnlyList<string> RequiredFeatures { get; set; }
    }

    /// <summary>Результат сравнения одного шаблона.</summary>
    internal sealed class MatchResult
    {
      /// <summary>Идентификатор шаблона.</summary>
      public string TemplateId { get; set; }

      /// <summary>Доля совпавших уравнений.</summary>
      public double EquationRatio { get; set; }

      /// <summary>Доля совпавших свойств.</summary>
      public double PropertyRatio { get; set; }

      /// <summary>Доля совпавших операций.</summary>
      public double FeatureRatio { get; set; }

      /// <summary>Итоговый <c>confidence</c> 0…1.</summary>
      public double Confidence { get; set; }

      /// <summary>Прошёл ли шаблон гейт (тип документа + число конфигураций).</summary>
      public bool PassedGate { get; set; }
    }

    /// <summary>
    /// Сравнивает шаблон с фактами документа. Гейт: тип совпал и число конфигураций
    /// не меньше <c>ConfigCountMin</c> (если он задан и &gt; 0).
    /// </summary>
    /// <param name="template">Отпечаток шаблона.</param>
    /// <param name="facts">Факты документа.</param>
    /// <param name="rules">Веса и пороги.</param>
    /// <returns>Результат сравнения.</returns>
    internal static MatchResult Match(
        TemplateFingerprint template,
        DocumentFacts facts,
        Rules rules)
    {
      rules = rules ?? new Rules();
      var result = new MatchResult
      {
        TemplateId = template?.TemplateId ?? string.Empty
      };

      if (template == null || facts == null)
        return result;

      bool typeMatches = string.Equals(
          (template.DocumentType ?? string.Empty).Trim(),
          (facts.DocumentType ?? string.Empty).Trim(),
          StringComparison.OrdinalIgnoreCase);

      bool configOk = !template.ConfigCountMin.HasValue
          || template.ConfigCountMin.Value <= 0
          || facts.ConfigurationCount >= template.ConfigCountMin.Value;

      result.PassedGate = typeMatches && configOk;
      if (!result.PassedGate)
        return result;

      result.EquationRatio = Ratio(template.RequiredEquations, facts.Equations);
      result.PropertyRatio = Ratio(template.RequiredProperties, facts.Properties);
      result.FeatureRatio = Ratio(template.RequiredFeatures, facts.Features);

      result.Confidence =
          rules.WeightEquations * result.EquationRatio +
          rules.WeightProperties * result.PropertyRatio +
          rules.WeightFeatures * result.FeatureRatio +
          rules.WeightBase;

      if (result.Confidence > 1d)
        result.Confidence = 1d;

      return result;
    }

    /// <summary>
    /// Выбирает шаблон-кандидат среди результатов: нет кандидатов → <c>unknown</c>;
    /// один → он; несколько и разница первого и второго меньше
    /// <see cref="Rules.AmbiguityDelta"/> → <c>unknown</c>, иначе первый.
    /// </summary>
    /// <param name="matches">Результаты сравнения.</param>
    /// <param name="rules">Веса и пороги.</param>
    /// <returns>Идентификатор шаблона либо <c>null</c> при неоднозначности/отсутствии кандидатов.</returns>
    internal static string PickCandidate(IReadOnlyList<MatchResult> matches, Rules rules)
    {
      rules = rules ?? new Rules();
      if (matches == null || matches.Count == 0)
        return null;

      var candidates = new List<MatchResult>();
      foreach (MatchResult m in matches)
      {
        if (m != null && m.PassedGate && m.Confidence >= rules.MinConfidence)
          candidates.Add(m);
      }

      if (candidates.Count == 0)
        return null;

      candidates.Sort((a, b) => b.Confidence.CompareTo(a.Confidence));
      if (candidates.Count == 1)
        return candidates[0].TemplateId;

      double delta = candidates[0].Confidence - candidates[1].Confidence;
      if (delta < rules.AmbiguityDelta)
        return null;

      return candidates[0].TemplateId;
    }

    private static double Ratio(IReadOnlyList<string> required, HashSet<string> actual)
    {
      if (required == null || required.Count == 0)
        return 1d;

      if (actual == null || actual.Count == 0)
        return 0d;

      int matched = 0;
      foreach (string name in required)
      {
        string key = (name ?? string.Empty).Trim();
        if (key.Length == 0)
          continue;
        if (actual.Contains(key))
          matched++;
      }

      return (double)matched / required.Count;
    }
  }
}
