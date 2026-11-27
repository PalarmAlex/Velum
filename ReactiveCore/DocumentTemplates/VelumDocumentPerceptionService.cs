using System;
using System.Collections.Generic;
using System.Globalization;
using ISIDA.Common;
using ISIDA.SymbiontEnv.Contract;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Velum.Configuration;

namespace Velum.ReactiveCore.DocumentTemplates
{
  /// <summary>
  /// Снимок восприятия активного документа: распознанный шаблон, <c>confidence</c>,
  /// фактические признаки и значения параметров с зонами допуска. Строится на UI-потоке
  /// SolidWorks и кэшируется; опрос проб на такте пульса читает только кэш (без COM).
  /// </summary>
  internal sealed class VelumDocumentSnapshot
  {
    /// <summary>Стабильный ключ документа (<c>doc.key</c>), для которого собран снимок.</summary>
    public string DocKey { get; set; } = string.Empty;

    /// <summary>Идентификатор распознанного шаблона либо <c>null</c> (unknown).</summary>
    public string TemplateId { get; set; }

    /// <summary>Признак уверенного распознавания.</summary>
    public bool Recognized { get; set; }

    /// <summary><c>confidence</c> 0…1 (1.0 — документ помечен в реестре экземпляров).</summary>
    public double Confidence { get; set; }

    /// <summary>Наиболее близкий шаблон при unknown (формулировка «вероятно X»).</summary>
    public string BestGuessTemplateId { get; set; }

    /// <summary>Тип документа (<c>part</c>/<c>asm</c>/<c>drw</c>).</summary>
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>Значения параметров по <c>paramId</c> (пусто, если шаблон не распознан).</summary>
    public Dictionary<string, VelumDocumentParameterReading> Parameters { get; } =
        new Dictionary<string, VelumDocumentParameterReading>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Оценки болевых точек (групп параметров) по ключу пробы метрики шаблона.</summary>
    public Dictionary<string, float> GroupScores { get; } =
        new Dictionary<string, float>(StringComparer.Ordinal);
  }

  /// <summary>
  /// Чтение одного параметра с зоной допуска и оценкой 0…100.
  /// </summary>
  internal sealed class VelumDocumentParameterReading
  {
    /// <summary>Идентификатор параметра.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Отображаемое имя.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Единица измерения.</summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>Прочитанное значение (строка) либо <c>null</c>.</summary>
    public string RawValue { get; set; }

    /// <summary>true, если адрес параметра найден в документе.</summary>
    public bool Found { get; set; }

    /// <summary>Причина отсутствия/деградации.</summary>
    public string Note { get; set; } = string.Empty;

    /// <summary>Зона допуска.</summary>
    public VelumDocumentParameterProbeRules.Zone Zone { get; set; } = VelumDocumentParameterProbeRules.Zone.Good;

    /// <summary>Оценка 0…100 (в допуске — 100).</summary>
    public float Score { get; set; } = VelumDocumentParameterProbeRules.GoodScore;
  }

  /// <summary>
  /// Восприятие активного документа: собирает фактические признаки из COM, распознаёт шаблон
  /// по отпечатку, читает значения параметров и присваивает им зоны допуска. Кэш снимка
  /// привязан к <c>doc.key</c>; сброс — при смене/сохранении документа (<see cref="Invalidate"/>).
  /// </summary>
  /// <remarks>
  /// Все методы, обращающиеся к COM (<see cref="Refresh"/>), вызываются только с UI-потока
  /// SolidWorks. Опрос проб на такте пульса читает <see cref="Current"/> без COM.
  /// </remarks>
  internal static class VelumDocumentPerceptionService
  {
    private static readonly object Sync = new object();
    private static VelumDocumentSnapshot _cached;

    private static readonly object CatalogSync = new object();
    private static VelumDocumentTemplateCatalog _catalog;

    private static readonly object RegistrySync = new object();
    private static VelumDocumentInstanceRegistry _registry;

    /// <summary>Текущий кэшированный снимок либо <c>null</c> (нет снимка / не распознан документ).</summary>
    internal static VelumDocumentSnapshot Current
    {
      get
      {
        lock (Sync)
          return _cached;
      }
    }

    /// <summary>
    /// Сбрасывает кэш снимка (смена активного документа, сохранение, смена сессии).
    /// </summary>
    internal static void Invalidate()
    {
      lock (Sync)
        _cached = null;
    }

    /// <summary>
    /// Формирует список ключей проб параметров для снимка: по одному ключу
    /// <c>Velum.Solid.Parameter.&lt;templateId&gt;.&lt;paramId&gt;</c> на каждый параметр
    /// распознанного шаблона. Эти ключи динамические (данными библиотеки), их нет в
    ///EA-воздействиях, поэтому мост среды объединяет их со статическим набором перед опросом.
    /// </summary>
    /// <param name="snapshot">Снимок восприятия (обычно <see cref="Current"/>).</param>
    /// <returns>Ключи проб параметров; пусто, если шаблон не распознан.</returns>
    internal static IReadOnlyList<string> EnumerateProbeKeys(VelumDocumentSnapshot snapshot)
    {
      if (snapshot == null || !snapshot.Recognized || string.IsNullOrWhiteSpace(snapshot.TemplateId))
        return Array.Empty<string>();

      var keys = new List<string>();
      foreach (string paramId in snapshot.Parameters.Keys)
      {
        string key = VelumDocumentProbeKeys.FormatParameterProbeKey(snapshot.TemplateId, paramId);
        if (!string.IsNullOrEmpty(key))
          keys.Add(key);
      }

      return keys;
    }

    /// <summary>
    /// Пересобирает снимок активного документа через COM и обновляет кэш.
    /// Вызывать с UI-потока SolidWorks.
    /// </summary>
    /// <param name="modelDoc">Активный документ (<c>ModelDoc2</c>), может быть <c>null</c>.</param>
    /// <returns>Новый снимок либо <c>null</c>, если документа нет.</returns>
    internal static VelumDocumentSnapshot Refresh(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
      {
        Invalidate();
        return null;
      }

      string docKey = VelumSolidWorksModelDocHelperProbeKey(modelDoc);
      string docType = MapDocumentType(SafeGetType(modelDoc));

      VelumDocumentSnapshot snapshot = BuildSnapshot(modelDoc, docKey, docType);

      lock (Sync)
        _cached = snapshot;

      return snapshot;
    }

    /// <summary>
    /// Собирает снимок без записи в кэш (для диагностики и тестов через COM-заглушки).
    /// </summary>
    /// <param name="modelDoc">Документ SolidWorks.</param>
    /// <param name="docKey">Стабильный ключ документа.</param>
    /// <param name="docType">Тип документа (<c>part</c>/<c>asm</c>/<c>drw</c>).</param>
    /// <returns>Снимок восприятия.</returns>
    internal static VelumDocumentSnapshot BuildSnapshot(ModelDoc2 modelDoc, string docKey, string docType)
    {
      var snapshot = new VelumDocumentSnapshot
      {
        DocKey = docKey ?? string.Empty,
        DocumentType = docType ?? string.Empty
      };

      IReadOnlyList<EnvironmentDocumentTemplateData> templates = GetCatalogTemplates();

      // 1. Приоритет — метка в реестре экземпляров: документ создан из шаблона, confidence = 1.0.
      EnvironmentDocumentTemplateData recognized = TryResolveFromRegistry(docKey, out double confidence);
      if (recognized != null)
      {
        snapshot.Recognized = true;
        snapshot.Confidence = confidence;
        snapshot.TemplateId = recognized.TemplateId;
      }
      else
      {
        // 2. Распознавание по отпечатку.
        VelumDocumentFingerprintRules.DocumentFacts facts = CollectFacts(modelDoc, docType);
        VelumDocumentSnapshotRules.RecognitionResult recognition =
            VelumDocumentSnapshotRules.Recognize(templates, facts, ResolveRules());

        snapshot.Recognized = recognition.Recognized;
        snapshot.Confidence = recognition.Confidence;
        snapshot.TemplateId = recognition.TemplateId;

        if (!recognition.Recognized)
        {
          VelumDocumentFingerprintRules.MatchResult guess = VelumDocumentSnapshotRules.FindBestGuess(recognition);
          snapshot.BestGuessTemplateId = guess?.TemplateId;
        }

        if (recognition.Recognized)
          recognized = FindTemplate(templates, recognition.TemplateId);
      }

      if (recognized == null)
        return snapshot;

      // 3. Чтение значений параметров распознанного шаблона и зоны допуска.
      PopulateParameters(modelDoc, recognized, snapshot);
      PopulateGroupScores(recognized, snapshot);

      return snapshot;
    }

    private static void PopulateParameters(
        ModelDoc2 modelDoc,
        EnvironmentDocumentTemplateData template,
        VelumDocumentSnapshot snapshot)
    {
      IReadOnlyList<VelumDocumentParameterService.ParameterReadResult> readings =
          VelumDocumentParameterService.ReadParameters(modelDoc, template);

      foreach (VelumDocumentParameterService.ParameterReadResult read in readings)
      {
        if (read == null || string.IsNullOrWhiteSpace(read.Id))
          continue;

        EnvironmentDocumentParameterData parameter = FindParameter(template, read.Id);
        var reading = new VelumDocumentParameterReading
        {
          Id = read.Id,
          Label = parameter?.Label ?? read.Id,
          Unit = parameter?.Unit ?? string.Empty,
          RawValue = read.Value,
          Found = read.Found,
          Note = read.Note ?? string.Empty
        };

        if (parameter != null)
        {
          double warnBand = ResolveWarnBand(parameter.Min, parameter.Max);

          if (read.Found &&
              TryToDouble(read.Value, out double value))
          {
            reading.Zone = VelumDocumentParameterProbeRules.Classify(value, parameter.Min, parameter.Max, warnBand);
            reading.Score = VelumDocumentParameterProbeRules.ScoreValue(value, parameter.Min, parameter.Max, warnBand);
          }
          else
          {
            // Адрес не найден или значение не число — считаем зоной bad (давление на «болит»).
            reading.Zone = VelumDocumentParameterProbeRules.Zone.Bad;
            reading.Score = 0f;
          }
        }

        snapshot.Parameters[read.Id] = reading;
      }
    }

    private static void PopulateGroupScores(
        EnvironmentDocumentTemplateData template,
        VelumDocumentSnapshot snapshot)
    {
      if (template.Metrics == null)
        return;

      foreach (EnvironmentDocumentMetricGroupData group in template.Metrics)
      {
        if (group == null || string.IsNullOrWhiteSpace(group.ProbeKey))
          continue;

        var scores = new List<float>();
        if (group.Parameters != null)
        {
          foreach (string id in group.Parameters)
          {
            if (string.IsNullOrWhiteSpace(id))
              continue;
            if (snapshot.Parameters.TryGetValue(id.Trim(), out VelumDocumentParameterReading reading))
              scores.Add(reading.Score);
          }
        }

        snapshot.GroupScores[group.ProbeKey.Trim()] =
            VelumDocumentParameterProbeRules.ScoreGroup(scores, group.Mode);
      }
    }

    private static VelumDocumentFingerprintRules.DocumentFacts CollectFacts(ModelDoc2 modelDoc, string docType)
    {
      var facts = new VelumDocumentFingerprintRules.DocumentFacts
      {
        DocumentType = docType ?? string.Empty
      };

      facts.ConfigurationCount = CountConfigurations(modelDoc);
      CollectEquations(modelDoc, facts.Equations);
      CollectProperties(modelDoc, facts.Properties);
      CollectFeatures(modelDoc, facts.Features);

      return facts;
    }

    private static int CountConfigurations(ModelDoc2 modelDoc)
    {
      try
      {
        string[] names = modelDoc.GetConfigurationNames() as string[];
        return names?.Length ?? 0;
      }
      catch
      {
        return 0;
      }
    }

    private static void CollectEquations(ModelDoc2 modelDoc, HashSet<string> target)
    {
      EquationMgr equations = VelumDocumentParameterService.TryGetEquationManager(modelDoc);
      if (equations == null)
        return;

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
              !string.IsNullOrWhiteSpace(name))
            target.Add(name.Trim());
        }
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum perception collect equations failed: " + ex.Message);
      }
    }

    private static void CollectProperties(ModelDoc2 modelDoc, HashSet<string> target)
    {
      CustomPropertyManager cpm = VelumRecipeSolidWorksCustomProperties.TryGetManager(modelDoc, string.Empty);
      if (cpm == null)
        return;

      try
      {
        string[] names = cpm.GetNames() as string[];
        if (names == null)
          return;

        foreach (string name in names)
        {
          if (!string.IsNullOrWhiteSpace(name))
            target.Add(name.Trim());
        }
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum perception collect properties failed: " + ex.Message);
      }
    }

    private static void CollectFeatures(ModelDoc2 modelDoc, HashSet<string> target)
    {
      try
      {
        Feature feature = modelDoc.FirstFeature() as Feature;
        while (feature != null)
        {
          AddFeatureName(feature, target);
          feature = feature.GetNextFeature() as Feature;
        }
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum perception collect features failed: " + ex.Message);
      }
    }

    private static void AddFeatureName(Feature feature, HashSet<string> target)
    {
      try
      {
        string name = feature?.Name;
        if (!string.IsNullOrWhiteSpace(name))
          target.Add(name.Trim());
      }
      catch
      {
      }
    }

    private static EnvironmentDocumentTemplateData TryResolveFromRegistry(string docKey, out double confidence)
    {
      confidence = 0d;

      VelumDocumentInstanceData instance = TryFindInstance(docKey);
      if (instance == null || string.IsNullOrWhiteSpace(instance.TemplateId))
        return null;

      EnvironmentDocumentTemplateData template = FindTemplate(GetCatalogTemplates(), instance.TemplateId);
      if (template == null)
        return null;

      confidence = 1.0;
      return template;
    }

    private static VelumDocumentInstanceData TryFindInstance(string docKey)
    {
      if (string.IsNullOrWhiteSpace(docKey))
        return null;

      try
      {
        return GetInstanceRegistry().FindByDocKey(docKey);
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum perception instance registry read failed: " + ex.Message);
        return null;
      }
    }

    private static VelumDocumentFingerprintRules.Rules ResolveRules()
    {
      return new VelumDocumentFingerprintRules.Rules
      {
        WeightEquations = VelumAppConfig.DocumentRecognitionWeightEquations,
        WeightProperties = VelumAppConfig.DocumentRecognitionWeightProperties,
        WeightFeatures = VelumAppConfig.DocumentRecognitionWeightFeatures,
        WeightBase = VelumAppConfig.DocumentRecognitionWeightBase,
        MinConfidence = VelumAppConfig.DocumentRecognitionMinConfidence,
        AmbiguityDelta = VelumAppConfig.DocumentRecognitionAmbiguityDelta
      };
    }

    /// <summary>
    /// Ширина полосы <c>warn</c>: явная абсолютная настройка, иначе доля от полуширины допуска
    /// (при допуске с двух сторон) либо от величины единственной границы.
    /// </summary>
    private static double ResolveWarnBand(double? min, double? max)
    {
      double abs = VelumAppConfig.DocumentParameterWarnBand;
      if (abs > 0d)
        return abs;

      double ratio = VelumAppConfig.DocumentParameterWarnBandRatio;
      if (ratio <= 0d)
        ratio = 0.25d;
      if (ratio > 1d)
        ratio = 1d;

      if (min.HasValue && max.HasValue && max.Value > min.Value)
        return ratio * (max.Value - min.Value) / 2d;

      double magnitude = 0d;
      if (min.HasValue)
        magnitude = Math.Max(magnitude, Math.Abs(min.Value));
      if (max.HasValue)
        magnitude = Math.Max(magnitude, Math.Abs(max.Value));

      return magnitude > 0d ? ratio * magnitude : 1d;
    }

    private static bool TryToDouble(string raw, out double value)
    {
      return double.TryParse((raw ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static EnvironmentDocumentTemplateData FindTemplate(
        IReadOnlyList<EnvironmentDocumentTemplateData> templates, string templateId)
    {
      if (templates == null || string.IsNullOrWhiteSpace(templateId))
        return null;

      foreach (EnvironmentDocumentTemplateData t in templates)
      {
        if (t != null && string.Equals(t.TemplateId, templateId, StringComparison.OrdinalIgnoreCase))
          return t;
      }

      return null;
    }

    private static EnvironmentDocumentParameterData FindParameter(
        EnvironmentDocumentTemplateData template, string paramId)
    {
      if (template?.Parameters == null || string.IsNullOrWhiteSpace(paramId))
        return null;

      foreach (EnvironmentDocumentParameterData p in template.Parameters)
      {
        if (p != null && string.Equals(p.Id, paramId, StringComparison.OrdinalIgnoreCase))
          return p;
      }

      return null;
    }

    private static IReadOnlyList<EnvironmentDocumentTemplateData> GetCatalogTemplates()
    {
      lock (CatalogSync)
      {
        if (_catalog == null)
        {
          var catalog = new VelumDocumentTemplateCatalog();
          catalog.Load();
          _catalog = catalog;
        }

        return _catalog.Templates;
      }
    }

    private static VelumDocumentInstanceRegistry GetInstanceRegistry()
    {
      lock (RegistrySync)
      {
        if (_registry == null)
        {
          var registry = new VelumDocumentInstanceRegistry();
          registry.Load();
          _registry = registry;
        }

        return _registry;
      }
    }

    private static int SafeGetType(ModelDoc2 modelDoc)
    {
      try
      {
        return modelDoc.GetType();
      }
      catch
      {
        return 0;
      }
    }

    private static string MapDocumentType(int swDocType)
    {
      if (swDocType == (int)swDocumentTypes_e.swDocPART)
        return "part";
      if (swDocType == (int)swDocumentTypes_e.swDocASSEMBLY)
        return "asm";
      if (swDocType == (int)swDocumentTypes_e.swDocDRAWING)
        return "drw";
      return string.Empty;
    }

    // Переименован, чтобы не конфликтовать с одноимённым хелпером SolidHomeostasis при разных using;
    // делегирует тому же алгоритму ключа.
    private static string VelumSolidWorksModelDocHelperProbeKey(ModelDoc2 modelDoc)
    {
      return VelumSolidWorksModelDocKeyProbe.Of(modelDoc);
    }
  }

  /// <summary>
  /// Стабильный ключ документа для снимка восприятия (тот же алгоритм, что и dispatch рецептов:
  /// путь на диске либо identity несохранённого). Вынесен, чтобы ядро восприятия не зависело от
  /// пространства имён <c>SolidHomeostasis</c>.
  /// </summary>
  internal static class VelumSolidWorksModelDocKeyProbe
  {
    /// <summary>Возвращает стабильный ключ документа либо <c>no_doc</c>.</summary>
    /// <param name="modelDoc">Документ SolidWorks.</param>
    /// <returns>Ключ <c>path:...</c>, <c>unsaved:...</c> либо <c>no_doc</c>.</returns>
    internal static string Of(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return "no_doc";

      try
      {
        string path = modelDoc.GetPathName();
        if (!string.IsNullOrWhiteSpace(path))
          return "path:" + System.IO.Path.GetFullPath(path.Trim());
      }
      catch
      {
      }

      try
      {
        return "unsaved:" + modelDoc.GetHashCode().ToString(CultureInfo.InvariantCulture);
      }
      catch
      {
        return "no_doc";
      }
    }
  }
}
