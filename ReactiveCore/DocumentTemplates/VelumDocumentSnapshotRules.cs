using System;
using System.Collections.Generic;
using ISIDA.SymbiontEnv.Contract;

namespace Velum.ReactiveCore.DocumentTemplates
{
  /// <summary>
  /// Чистые правила снимка документа: распознавание активного документа среди
  /// библиотеки шаблонов по отпечатку. Не обращается к COM — принимает готовые
  /// <see cref="VelumDocumentFingerprintRules.DocumentFacts"/> и список шаблонов.
  /// </summary>
  internal static class VelumDocumentSnapshotRules
  {
    /// <summary>Итог распознавания активного документа.</summary>
    internal sealed class RecognitionResult
    {
      /// <summary>Идентификатор распознанного шаблона либо <c>null</c> (unknown).</summary>
      public string TemplateId { get; set; }

      /// <summary>Признак уверенного распознавания (кандидат выбран).</summary>
      public bool Recognized { get; set; }

      /// <summary><c>confidence</c> выбранного кандидата (0…1); 0 при unknown.</summary>
      public double Confidence { get; set; }

      /// <summary>Все результаты сравнения (для диагностики «почему не распознан»). </summary>
      public IReadOnlyList<VelumDocumentFingerprintRules.MatchResult> Matches { get; set; }
          = new List<VelumDocumentFingerprintRules.MatchResult>();
    }

    /// <summary>
    /// Сопоставляет факты документа с библиотекой шаблонов и выбирает кандидата.
    /// Пустая библиотека или отсутствие кандидата → <see cref="RecognitionResult.Recognized"/>
    /// = <c>false</c> (сторонняя деталь — <c>unknown</c>).
    /// </summary>
    /// <param name="templates">Шаблоны библиотеки.</param>
    /// <param name="facts">Факты активного документа.</param>
    /// <param name="rules">Веса и пороги распознавания.</param>
    /// <returns>Итог распознавания.</returns>
    internal static RecognitionResult Recognize(
        IReadOnlyList<EnvironmentDocumentTemplateData> templates,
        VelumDocumentFingerprintRules.DocumentFacts facts,
        VelumDocumentFingerprintRules.Rules rules)
    {
      rules = rules ?? new VelumDocumentFingerprintRules.Rules();
      var matches = new List<VelumDocumentFingerprintRules.MatchResult>();

      if (templates != null)
      {
        foreach (EnvironmentDocumentTemplateData t in templates)
        {
          if (t == null)
            continue;

          matches.Add(VelumDocumentFingerprintRules.Match(ToTemplateFingerprint(t), facts, rules));
        }
      }

      string picked = VelumDocumentFingerprintRules.PickCandidate(matches, rules);
      double confidence = 0d;
      if (picked != null)
      {
        foreach (VelumDocumentFingerprintRules.MatchResult m in matches)
        {
          if (m != null && string.Equals(m.TemplateId, picked, StringComparison.OrdinalIgnoreCase))
          {
            confidence = m.Confidence;
            break;
          }
        }
      }

      return new RecognitionResult
      {
        TemplateId = picked,
        Recognized = picked != null,
        Confidence = confidence,
        Matches = matches
      };
    }

    /// <summary>
    /// Ищет в результатах сравнения шаблон с максимальной <c>confidence</c>,
    /// не прошедший порог — для формулировки «вероятно X» при unknown.
    /// </summary>
    /// <param name="result">Итог распознавания.</param>
    /// <returns>Наиболее близкий шаблон либо <c>null</c>.</returns>
    internal static VelumDocumentFingerprintRules.MatchResult FindBestGuess(RecognitionResult result)
    {
      if (result?.Matches == null)
        return null;

      VelumDocumentFingerprintRules.MatchResult best = null;
      foreach (VelumDocumentFingerprintRules.MatchResult m in result.Matches)
      {
        if (m == null || !m.PassedGate)
          continue;
        if (best == null || m.Confidence > best.Confidence)
          best = m;
      }

      return best;
    }

    private static VelumDocumentFingerprintRules.TemplateFingerprint ToTemplateFingerprint(
        EnvironmentDocumentTemplateData t)
    {
      EnvironmentDocumentFingerprintData fp = t.Fingerprint ?? new EnvironmentDocumentFingerprintData();
      return new VelumDocumentFingerprintRules.TemplateFingerprint
      {
        TemplateId = t.TemplateId,
        DocumentType = t.DocumentType,
        ConfigCountMin = fp.ConfigCountMin,
        RequiredEquations = fp.RequiredEquations ?? Array.Empty<string>(),
        RequiredProperties = fp.RequiredProperties ?? Array.Empty<string>(),
        RequiredFeatures = fp.RequiredFeatures ?? Array.Empty<string>()
      };
    }
  }
}
