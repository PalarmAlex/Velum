using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using ISIDA.Common;
using ISIDA.SymbiontEnv.Contract;
using Velum.ReactiveCore;

namespace Velum.Configuration
{
  /// <summary>
  /// Загрузка и сохранение настроек Velum в XML под %ProgramData%\VELUM\Settings\Settings.xml
  /// (по аналогии с <c>AIStudio\AppConfig</c> и <c>AIStudio.Settings.xml</c> в %ProgramData%\ISIDA\Settings\).
  /// Тот же файл по умолчанию кладёт установщик Inno из <c>config\Settings.xml</c>.
  /// </summary>
  public static class VelumAppConfig
  {
    /// <summary>
    /// Блокировка для всех операций чтения/записи Settings.xml.
    /// Предотвращает гонки между потоками, в том числе при очистке данных ISIDA
    /// (SetEvolutionStage с флагом clearData), которая может блокировать или
    /// временно недоступно делать файлы в общем каталоге %ProgramData%\VELUM.
    /// </summary>
    private static readonly object ConfigLock = new object();

    private const string ConfigFileName = "Settings.xml";

    /// <summary>
    /// Префикс путей данных в XML по умолчанию; при чтении разворачивается через <see cref="Environment.ExpandEnvironmentVariables"/>.
    /// </summary>
    private const string DefaultVelumDataRootExpression = "%ProgramData%\\VELUM";

    private static readonly string ConfigDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "VELUM",
        "Settings");

    private static readonly string ConfigFullPath = Path.Combine(ConfigDirectory, ConfigFileName);

    /// <summary>
    /// Кэш значений настроек, прочитанных из Settings.xml. Чтение файла нужно
    /// держать вне горячих циклов: нормализация путей реестра
    /// (<see cref="Velum.ReactiveCore.Export.VelumRelativeDocumentPathResolver"/> →
    /// <see cref="DocumentRootPath"/>)
    /// вызывается на каждую запись при сканировании и загрузке формы.
    /// </summary>
    private static readonly Dictionary<string, CachedSetting> _settingCache =
        new Dictionary<string, CachedSetting>(StringComparer.Ordinal);

    /// <summary>Срок жизни кэшированного значения настройки, мс. Правки файла руками видны не позже этого срока.</summary>
    private const int SettingCacheTtlMs = 1000;

    /// <summary>
    /// Полный путь к файлу настроек на машине пользователя.
    /// </summary>
    public static string ConfigFilePath => ConfigFullPath;

    /// <summary>
    /// Базовый каталог данных Velum (%ProgramData%\VELUM).
    /// </summary>
    public static string BaseDataPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "VELUM");

    static VelumAppConfig()
    {
      EnsureInitialized();
    }

    /// <summary>
    /// Создаёт каталог настроек и при отсутствии файла — конфигурацию по умолчанию.
    /// Все операции с Settings.xml выполняются под общей блокировкой, чтобы избежать
    /// гонок с ISIDA (SetEvolutionStage/clearData) и другими потоками.
    /// </summary>
    public static void EnsureInitialized()
    {
      try
      {
        lock (ConfigLock)
        {
          // Файл мог быть заменен извне (установщик, форма настроек, ISIDA) — кэш не нужен.
          _settingCache.Clear();
          Directory.CreateDirectory(ConfigDirectory);
          if (!File.Exists(ConfigFullPath))
            CreateDefaultConfig();

          EnsureScenarioReportsFolderSetting();
          EnsureHomeostasisPulseSpeedDriftSetting();
          EnsureSolidEnvironmentMetricDeltaEpsilon();
          EnsureSolidHostImpulseMinParameterDelta();
          EnsureSolidHomeostasisPulseTrace();
          EnsureSolidProbeTimeoutSettings();
          EnsureHeavyMetricsPulsePeriodSetting();
          EnsureCadDegradedThresholdSettings();
          EnsureRecipeDispatchSettings();
          EnsureDxfExportSettings();
          EnsureDocumentDefaultsSettings();
          EnsureCommandBufferSettings();
          EnsureReactiveCorePathSettings();
          EnsureDocumentVisualColorSettings();
          EnsureBomExchangeFolderSetting();
          EnsureProductRegistryAccessLevelSetting();
          EnsureProductRegistryFolderPathSetting();
          EnsureTechRequirementsFolderPathSetting();
          EnsureAssemblyRegistryFolderPathSetting();
          EnsureDocumentRootPathsSetting();
          TryCreateDirectory(ScenarioReportsFolderPath);
          EnsureDataFolderTree();
        }
        VelumReactiveCoreBootstrap.EnsureInitialized();
      }
      catch (Exception ex)
      {
        Logger.Error(ex.Message);
      }
    }

    /// <summary>
    /// Создаёт дерево каталогов данных под %ProgramData%\VELUM (без изменения ACL).
    /// </summary>
    public static void EnsureDataFolderTree()
    {
      TryCreateDirectory(BaseDataPath);
      TryCreateDirectory(DataFolderPath);
      TryCreateDirectory(IsidaDataPaths.ResolveGomeostasFolder(DataFolderPath));
      TryCreateDirectory(IsidaDataPaths.ResolveActionsFolder(DataFolderPath));
      TryCreateDirectory(IsidaDataPaths.ResolveSensorsFolder(DataFolderPath));
      TryCreateDirectory(IsidaDataPaths.ResolveReflexesFolder(DataFolderPath));
      TryCreateDirectory(IsidaDataPaths.ResolvePsychicFolder(DataFolderPath));
      TryCreateDirectory(SettingsPath);
      TryCreateDirectory(LogsFolderPath);
      TryCreateDirectory(BootDataFolderPath);
      TryCreateDirectory(ScenarioReportsFolderPath);
      TryCreateDirectory(ResearchHarnessOutputFolderPath);
      TryCreateDirectory(EnvironmentFolderPath);
      TryCreateDirectory(ProductRegistryFolderPath);
      TryCreateDirectory(TechRequirementsFolderPath);
      TryCreateDirectory(AssemblyRegistryFolderPath);
    }

    /// <summary>
    /// Каталог данных реестра изделий (по умолчанию %ProgramData%\VELUM\ProductRegistry).
    /// </summary>
    public static string ProductRegistryFolderPath => GetExpandedDataPathOrDefault(
        "ProductRegistryFolderPath",
        Path.Combine(BaseDataPath, "ProductRegistry"));

    /// <summary>
    /// Каталог шаблонов технических требований (по умолчанию %ProgramData%\VELUM\TechRequirements).
    /// </summary>
    public static string TechRequirementsFolderPath => GetExpandedDataPathOrDefault(
        "TechRequirementsFolderPath",
        Path.Combine(BaseDataPath, "TechRequirements"));

    /// <summary>
    /// Каталог настроек столбцов реестра изделия (по умолчанию %ProgramData%\VELUM\AssemblyRegistry).
    /// </summary>
    public static string AssemblyRegistryFolderPath => GetExpandedDataPathOrDefault(
        "AssemblyRegistryFolderPath",
        Path.Combine(BaseDataPath, "AssemblyRegistry"));

    /// <summary>
    /// Путь к корневому каталогу документов на <b>этой</b> машине: <c>Z:\</c> либо
    /// <c>\server\dfs\</c>. Настройка локальная — дома по VPN указывается доступ по IP,
    /// на работе — буква общего диска. Пути в реестре и в свойствах документов хранятся
    /// относительными этого корня и собираются при чтении.
    /// Пустое значение — прежнее поведение (абсолютные пути).
    /// </summary>
    /// <remarks>
    /// Ключ в Settings.xml исторически называется <c>DocumentRootPaths</c> и допускал
    /// список через «;». Список больше не поддерживается: если в значении остались
    /// разделители, используется первая часть (старые файлы настроек продолжают работать).
    /// </remarks>
    public static string DocumentRootPath
    {
      get { return FirstRootPathPart(GetSetting("DocumentRootPaths") ?? string.Empty); }
      set { SetSetting("DocumentRootPaths", (value ?? string.Empty).Trim()); }
    }

    /// <summary>
    /// Нормализованный корневой путь документов (<see cref="DocumentRootPath"/>):
    /// с гарантированным «\» на конце UNC-пути и для буквы диска («Z:» → «Z:\»).
    /// string.Empty — корень не задан.
    /// </summary>
    public static string GetDocumentRootPath()
    {
      string trimmed = DocumentRootPath;
      if (trimmed.Length == 0)
        return string.Empty;

      // UNC-префикс «\server\dfs» без хвостового «\» не срезает корень корректно;
      // буква диска «Z:» дополняется до «Z:\».
      if (trimmed.StartsWith(@"\", StringComparison.Ordinal) && !trimmed.EndsWith(@"\"))
        trimmed += @"\";
      else if (trimmed.Length == 2 && trimmed[1] == ':')
        trimmed += @"\";

      return trimmed;
    }

    /// <summary>Первая непустая часть значения (обратная совместимость со списком через «;»).</summary>
    private static string FirstRootPathPart(string raw)
    {
      string value = (raw ?? string.Empty).Trim();
      if (value.Length == 0)
        return string.Empty;

      foreach (string part in value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
      {
        string trimmed = part.Trim();
        if (trimmed.Length > 0)
          return trimmed;
      }

      return string.Empty;
    }

    /// <summary>Последний выбранный шаблон столбцов реестра изделия.</summary>
    public static string AssemblyRegistryLastTemplate
    {
      get { return (GetSetting("AssemblyRegistryLastTemplate") ?? string.Empty).Trim(); }
      set { SetSetting("AssemblyRegistryLastTemplate", (value ?? string.Empty).Trim()); }
    }

    /// <summary>
    /// Уровень доступа приложения:
    /// <c>admin</c> (полный) или любое другое значение → <c>user</c>
    /// (реестр документов только чтение; тех. требования доступны;
    /// пульсация и админские формы запрещены).
    /// </summary>
    public static string ProductRegistryAccessLevel
    {
      get
      {
        string value = (GetSetting("ProductRegistryAccessLevel") ?? string.Empty).Trim();
        if (string.Equals(value, "admin", StringComparison.OrdinalIgnoreCase))
          return "admin";
        return "user";
      }
    }

    /// <summary>True, если уровень доступа admin (полный функционал).</summary>
    public static bool IsProductRegistryAdmin =>
        string.Equals(ProductRegistryAccessLevel, "admin", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Каталог данных среды (рецепты) под <see cref="BootDataFolderPath"/>.
    /// </summary>
    public static string EnvironmentFolderPath => Path.Combine(
        BootDataFolderPath ?? string.Empty,
        "Environment");

    /// <summary>
    /// Файл рецептов среды (список <c>recipes</c> в YAML).
    /// </summary>
    public static string EnvironmentRecipesFilePath => GetExpandedDataPathOrDefault(
        "EnvironmentRecipesFilePath",
        Path.Combine(EnvironmentFolderPath, "EnvironmentRecipes.yaml"));

    /// <summary>
    /// Устаревший ключ: каталог рецептов (используйте <see cref="EnvironmentRecipesFilePath"/>).
    /// </summary>
    public static string RecipesFolderPath => EnvironmentFolderPath;

    /// <summary>Корневой каталог данных ISIDA (<c>Data</c>).</summary>
    public static string DataFolderPath => ResolveDataFolderPath();

    /// <summary>
    /// Коды стилей для механизма 3 стадии 2 (случайная проба): Поиск и Игра.
    /// Список кодиров стилей через запятую, хранится в Settings.xml.
    /// По умолчанию: 3,5,7.
    /// </summary>
    public static List<int> Stage2SearchPlayStyleIds
    {
      get
      {
        string value = GetSetting("Stage2SearchPlayStyleIds") ?? "3,5,7";
        return value.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s =>
            {
              int id;
              return int.TryParse(s.Trim(), out id) ? id : 0;
            })
            .Where(id => id > 0)
            .ToList();
      }
    }

    /// <summary>
    /// Каталог данных среды (рецепты) под <see cref="BootDataFolderPath"/>.
    /// </summary>

    /// <summary>
    /// Каталог настроек (дополнительный путь из XML).
    /// </summary>
    public static string SettingsPath => ExpandDataPath(GetSetting("SettingsPath"));

    /// <summary>
    /// Каталог логов ISIDA при работе из Velum.
    /// </summary>
    public static string LogsFolderPath => ExpandDataPath(GetSetting("LogsFolderPath"));

    /// <summary>
    /// Каталог BootData (загрузка рефлексов и т. п.).
    /// </summary>
    public static string BootDataFolderPath => GetExpandedDataPathOrDefault(
        "BootDataFolderPath",
        Path.Combine(BaseDataPath, "BootData"));

    /// <summary>
    /// Каталог HTML-отчётов сценариев (зарезервировано).
    /// </summary>
    public static string ScenarioReportsFolderPath
    {
      get
      {
        string path = ExpandDataPath(GetSetting("ScenarioReportsFolderPath"));
        if (string.IsNullOrWhiteSpace(path))
          path = IsidaDataPaths.ResolveScenarioReportsFolder(BaseDataPath);
        return path;
      }
    }

    /// <summary>
    /// Каталог вывода Research Harness (данные ISIDA).
    /// </summary>
    public static string ResearchHarnessOutputFolderPath =>
        IsidaDataPaths.ResolveResearchHarnessFolder(DataFolderPath);

    private static string ResolveDataFolderPath()
    {
      string path = ExpandDataPath(GetSetting("DataFolderPath"));
      return string.IsNullOrWhiteSpace(path)
          ? Path.Combine(BaseDataPath, IsidaDataPaths.DataFolderName)
          : path;
    }

    /// <summary>
    /// Идентификатор безусловного рефлекса по умолчанию (значение из XML Velum).
    /// </summary>
    public static int DefaultGeneticReflexId => GetIntSetting("DefaultGeneticReflexId", 0);

    /// <summary>
    /// Признак первого запуска (1 в XML — как правило, мастер/подсказки установщика).
    /// </summary>
    public static int FirstRun => GetIntSetting("FirstRun", 1);

    /// <summary>
    /// Формат файлов логов ISIDA.
    /// </summary>
    public static ResearchLogger.LogFormat LogFormat =>
        GetLogFormatSetting("LogFormat", ResearchLogger.LogFormat.All);

    /// <summary>
    /// Включено ли файловое логирование ISIDA.
    /// </summary>
    public static bool LogEnabled => GetBoolSetting("LogEnabled", false);

    /// <summary>
    /// Включено ли отладочное логирование SolidHomeostasis (VelumSolidDiagLog, ProductRegistryIndexTrace и др.).
    /// </summary>
    public static bool SolidHomeostasisDebugLog => GetBoolSetting("SolidHomeostasisDebugLog", false);

    /// <summary>
    /// Идентификатор стиля по умолчанию.
    /// </summary>
    public static int DefaultStileId => GetIntSetting("DefaultStileId", 0);

    /// <summary>
    /// Идентификатор адаптивного действия по умолчанию.
    /// </summary>
    public static int DefaultAdaptiveActionId => GetIntSetting("DefaultAdaptiveActionId", 0);

    /// <summary>
    /// Идентификатор типа темы по умолчанию.
    /// </summary>
    public static int DefaultThemeTypeId => GetIntSetting("DefaultThemeTypeId", 4);

    /// <summary>
    /// Порог распознавания вербального канала.
    /// </summary>
    public static int RecognitionThreshold => GetIntSetting("RecognitionThreshold", 3);

    /// <summary>
    /// Порог сравнения гомеостаза (CompareLevel в ISIDA).
    /// </summary>
    public static int CompareLevel => GetIntSetting("CompareLevel", 30);

    /// <summary>
    /// Минимальное изменение сенсорного параметра.
    /// </summary>
    public static float DifSensorPar => GetFloatSetting("DifSensorPar", 0.02f);

    /// <summary>
    /// Пульсовый дрейф параметров гомеостаза по полю Speed (<see cref="IsidaConfig.HomeostasisPulseSpeedDriftEnabled"/>).
    /// В Velum по умолчанию выключен (в отличие от AIStudio).
    /// </summary>
    public static bool HomeostasisPulseSpeedDriftEnabled =>
        GetBoolSetting("HomeostasisPulseSpeedDriftEnabled", false);

    /// <summary>
    /// Сохранённое значение флажка «Авторитарная запись» в настройках проекта (на пульт ISIDA не подставляется при старте).
    /// </summary>
    public static bool VerbalAuthoritativeMode =>
        GetBoolSetting("VerbalAuthoritativeMode", false);

    /// <summary>
    /// Режим наблюдения: воздействия оператора не меняют гомеостаз (автоматизмы исполняются).
    /// </summary>
    public static bool ObservationMode =>
        GetBoolSetting("ObservationMode", false);

    /// <summary>
    /// Порог заметного изменения метрики среды SolidWorks между пульсами (шкала 0…100 у встроенных проб Velum).
    /// </summary>
    public static float SolidEnvironmentMetricDeltaEpsilon =>
        GetFloatSetting("SolidEnvironmentMetricDeltaEpsilon", 0.51f);

    /// <summary>
    /// Минимальное |целевое P_i − текущее в движке| на шкале 0…100, при котором Velum реально вызывает запись
    /// в ISIDA за такт. Ниже — импульс отбрасывается (шум SW / микродельты композитора), чтобы не сбрасывать
    /// удержание «Плохо/Хорошо» по счётчику пульсов. 0 — фильтр выключен (все предложенные значения пишутся).
    /// </summary>
    public static float SolidHostImpulseMinParameterDelta =>
        GetFloatSetting("SolidHostImpulseMinParameterDelta", 0.25f);

    /// <summary>
    /// Трассировка пульса SW→гомеостаз в Output и <c>GomeostasSystem.ParameterData.TracePulseHold</c> в ISIDA.
    /// </summary>
    public static bool SolidHomeostasisPulseTrace =>
        GetBoolSetting("SolidHomeostasisPulseTrace", false);

    /// <summary>
    /// Бюджет времени (мс) на опрос SolidWorks на такте <c>OnPulseBeforeGomeostasis</c>.
    /// При превышении ожидание UI/COM прерывается; см. <see cref="SolidProbeUseStaleSnapshotOnTimeout"/>.
    /// Значение ≤ 0 — таймаут отключён (синхронный <c>Invoke</c>, как до п. 1 плана устойчивости).
    /// </summary>
    public static int SolidProbeTimeoutMs => GetIntSetting("SolidProbeTimeoutMs", 2500);

    /// <summary>
    /// Кратность глобального пульса для тяжёлых фоновых метрик (скан реестра и т.п.).
    /// Тик раз в N пульсов; минимум 1. Не накручивается автоматически: если тик
    /// не успел за бюджет, сканеры продолжают проход с сохранённого курсора.
    /// </summary>
    public static int HeavyMetricsPulsePeriod
    {
      get
      {
        int v = GetIntSetting("HeavyMetricsPulsePeriod", 5);
        return v < 1 ? 1 : v;
      }
    }

    /// <summary>
    /// При таймауте опроса SW на пульсе повторно опубликовать последний валидный снимок из кэша
    /// (без нового COM), с пометкой <see cref="P:Velum.SolidHomeostasis.VelumSolidEnvironmentGate.LastSnapshotTimedOut"/>.
    /// </summary>
    public static bool SolidProbeUseStaleSnapshotOnTimeout =>
        GetBoolSetting("SolidProbeUseStaleSnapshotOnTimeout", true);

    /// <summary>
    /// Порог SessionHealth (0…100): ниже — вход в CAD-degraded (подавление исходящего исполнения агента).
    /// </summary>
    public static float CadDegradedEnterThreshold =>
        GetFloatSetting("CadDegradedEnterThreshold", 50f);

    /// <summary>
    /// Порог SessionHealth (0…100): выше — выход из CAD-degraded (гистерезис с <see cref="CadDegradedEnterThreshold"/>).
    /// </summary>
    public static float CadDegradedExitThreshold =>
        GetFloatSetting("CadDegradedExitThreshold", 60f);

    /// <summary>
    /// Удержание состояний «Плохо»/«Хорошо» у параметров: число тактов <c>GlobalTimer</c> (не секунды).
    /// Пока активны цепочки рефлексов или автоматизма, истечение по счётчику приостанавливается.
    /// </summary>
    public static int DynamicTime => GetIntSetting("DynamicTime", 50);

    /// <summary>
    /// Длительность отображения рефлекторного действия.
    /// </summary>
    public static int ReflexActionDisplayDuration => GetIntSetting("ReflexActionDisplayDuration", 3);

    /// <summary>
    /// Включить dispatch рецептов Velum по ответам движка (ID рефлексов/автоматизмов) на такте пульса.
    /// </summary>
    public static bool RecipeDispatchEnabled => GetBoolSetting("RecipeDispatchEnabled", true);

    /// <summary>
    /// Минимальный интервал между dispatch одного рецепта на одном документе (число тактов пульса).
    /// </summary>
    public static int RecipeDispatchCooldownPulses => GetIntSetting("RecipeDispatchCooldownPulses", 3);

    /// <summary>
    /// Пауза без новых <c>sw:*</c> в Command-буфере (мс) → idle-flush в ISIDA.
    /// </summary>
    public static int CommandBufferIdleFlushMs =>
        GetIntSetting("CommandBufferIdleFlushMs", ResolveCommandBufferPolicy().IdleFlushMs);

    /// <summary>
    /// Запись <c>sw:*</c> из SolidWorks в Command-буфер (только при активной пульсации).
    /// Единственный переключатель — вкладка «Адаптер» в настройках проекта.
    /// </summary>
    public static bool CommandBufferRecordingEnabled =>
        GetBoolSetting("CommandBufferRecordingEnabled", true);

    /// <summary>
    /// Максимум токенов в Command-буфере; при достижении — принудительный flush (0 = без лимита).
    /// </summary>
    public static int CommandBufferMaxTokens =>
        GetIntSetting("CommandBufferMaxTokens", ResolveCommandBufferPolicy().MaxTokens);

    /// <summary>
    /// Максимальный возраст буфера с момента первого токена (мс); по истечении — принудительный flush (0 = без лимита).
    /// Таймер max age не сбрасывается на каждый append.
    /// </summary>
    public static int CommandBufferMaxAgeMs =>
        GetIntSetting("CommandBufferMaxAgeMs", ResolveCommandBufferPolicy().MaxAgeMs);

    /// <summary>
    /// Устарело: подстановка имени файла выполняется шагом <c>save_file_name</c> рецепта после ответа движка.
    /// </summary>
    public static bool KbSaveFileNamePrefillEnabled => GetBoolSetting("KbSaveFileNamePrefillEnabled", false);

    /// <summary>
    /// Период ожидания оценки оператора (пульсы).
    /// </summary>
    public static int WaitingPeriodForActionsVal => GetIntSetting("WaitingPeriodForActionsVal", 30);

    /// <summary>
    /// Устаревший параметр затухания циклов (совместимость с IsidaConfig).
    /// </summary>
    public static int ThinkingCycleDecayAgeDivisor => GetIntSetting("ThinkingCycleDecayAgeDivisor", 100);

    /// <summary>Устаревший параметр затухания циклов.</summary>
    public static int ThinkingCycleDecayBase => GetIntSetting("ThinkingCycleDecayBase", 1);

    /// <summary>
    /// Максимальный возраст главного цикла мышления в пульсах.
    /// </summary>
    public static int ThinkingCycleMainMaxAgePulses => GetIntSetting("ThinkingCycleMainMaxAgePulses", 1000);

    /// <summary>
    /// Порог тишины без стимула оператора (пульсы).
    /// </summary>
    public static int NoOperatorStimulusSilencePulses => GetIntSetting("NoOperatorStimulusSilencePulses", 30);

    /// <summary>
    /// Код зрительного канала (<see cref="ISIDA.Reflexes.AgentVisualColor"/>) для стимулов при активном документе «деталь».
    /// Дефолт 1 (совпадает с числовым значением <see cref="Velum.ReactiveCore.VelumSolidDocumentKind.Part"/>).
    /// </summary>
    public static int DocumentColorPart => GetIntSetting("DocumentColorPart", 1);

    /// <summary>
    /// Код зрительного канала (<see cref="ISIDA.Reflexes.AgentVisualColor"/>) для стимулов при активном документе «сборка».
    /// Дефолт 2 (совпадает с числовым значением <see cref="Velum.ReactiveCore.VelumSolidDocumentKind.Assembly"/>).
    /// </summary>
    public static int DocumentColorAssembly => GetIntSetting("DocumentColorAssembly", 2);

    /// <summary>
    /// Код зрительного канала (<see cref="ISIDA.Reflexes.AgentVisualColor"/>) для стимулов при активном документе «чертёж».
    /// Дефолт 3 (совпадает с числовым значением <see cref="Velum.ReactiveCore.VelumSolidDocumentKind.Drawing"/>).
    /// </summary>
    public static int DocumentColorDrawing => GetIntSetting("DocumentColorDrawing", 3);

    /// <summary>Шаблон имени DXF, сохранённый при последнем экспорте.</summary>
    public static string DxfFileNameTemplate =>
        GetSetting("DxfFileNameTemplate") ?? string.Empty;

    /// <summary>Последний использованный шаблон имени DXF (подставляется по умолчанию).</summary>
    public static string DxfFileNameTemplateLastUsed => GetSetting("DxfFileNameTemplateLastUsed");

    /// <summary>Список имён суффиксов имени DXF (разделитель «|»).</summary>
    public static string DxfFileNameSuffixList => GetSetting("DxfFileNameSuffixList") ?? string.Empty;

    /// <summary>Сохраняет шаблон имени DXF в Settings.xml.</summary>
    public static void SetDxfFileNameTemplate(string template) =>
        SetSetting("DxfFileNameTemplate", template ?? string.Empty);

    /// <summary>Сохраняет последний использованный шаблон имени DXF.</summary>
    public static void SetDxfFileNameTemplateLastUsed(string template) =>
        SetSetting("DxfFileNameTemplateLastUsed", template ?? string.Empty);

    /// <summary>Сохраняет список имён суффиксов имени DXF.</summary>
    public static void SetDxfFileNameSuffixList(string suffixList) =>
        SetSetting("DxfFileNameSuffixList", suffixList ?? string.Empty);

    /// <summary>Каталог DXF по умолчанию на пакетной форме.</summary>
    public static string DxfBatchDefaultDxfFolder => GetExpandedSettingOrEmpty("DxfBatchDefaultDxfFolder");

    /// <summary>Вид проекции DXF по умолчанию для headless-экспорта.</summary>
    public static string DxfDefaultProjectionView => GetSetting("DxfDefaultProjectionView") ?? "Front";

    /// <summary>Сохраняет каталог DXF для пакетной формы.</summary>
    public static void SetDxfBatchDefaultDxfFolder(string folder) =>
        SetFolderSetting("DxfBatchDefaultDxfFolder", folder);

    /// <summary>Сохраняет вид проекции DXF по умолчанию для headless-экспорта.</summary>
    public static void SetDxfDefaultProjectionView(string view) =>
        SetSetting("DxfDefaultProjectionView", view ?? "Front");

    /// <summary>Каталог PDF по умолчанию на пакетной форме.</summary>
    public static string PdfBatchDefaultPdfFolder => GetExpandedSettingOrEmpty("PdfBatchDefaultPdfFolder");

    /// <summary>Сохраняет каталог PDF для пакетной формы.</summary>
    public static void SetPdfBatchDefaultPdfFolder(string folder) =>
        SetFolderSetting("PdfBatchDefaultPdfFolder", folder);

    /// <summary>Каталог выгрузки DXF на форме реестра изделия (отдельно от россыпи).</summary>
    public static string AssemblyRegistryDxfExportFolder =>
        GetExpandedSettingOrEmpty("AssemblyRegistryDxfExportFolder");

    /// <summary>Сохраняет каталог выгрузки DXF для формы реестра изделия.</summary>
    public static void SetAssemblyRegistryDxfExportFolder(string folder) =>
        SetFolderSetting("AssemblyRegistryDxfExportFolder", folder);

    /// <summary>
    /// Каталог выгрузки PDF для формы реестра изделия.
    /// </summary>
    public static string AssemblyRegistryPdfExportFolder =>
        GetExpandedSettingOrEmpty("AssemblyRegistryPdfExportFolder");

    /// <summary>
    /// Сохраняет каталог выгрузки PDF для формы реестра изделия.
    /// </summary>
    public static void SetAssemblyRegistryPdfExportFolder(string folder) =>
        SetFolderSetting("AssemblyRegistryPdfExportFolder", folder);

    /// <summary>
    /// Каталог обмена BOM-данными с 1C (CSV-экспорт).
    /// </summary>
    public static string BomExchangeFolder =>
        GetExpandedSettingOrEmpty("BomExchangeFolder");

    /// <summary>
    /// Сохраняет каталог обмена BOM с 1C.
    /// </summary>
    public static void SetBomExchangeFolder(string folder) =>
        SetFolderSetting("BomExchangeFolder", folder);

    /// <summary>Каталог деталей по умолчанию на пакетной форме материалов.</summary>
    public static string MaterialBatchDefaultPartsFolder => GetExpandedSettingOrEmpty("MaterialBatchDefaultPartsFolder");

    /// <summary>Сохраняет каталог деталей для пакетной формы материалов.</summary>
    public static void SetMaterialBatchDefaultPartsFolder(string folder) =>
        SetFolderSetting("MaterialBatchDefaultPartsFolder", folder);

    /// <summary>Каталог изделия по умолчанию на форме обновления свойств документов.</summary>
    public static string DocumentPropertyBatchDefaultCatalogFolder =>
        GetExpandedSettingOrEmpty("DocumentPropertyBatchDefaultCatalogFolder");

    /// <summary>Сохраняет каталог изделия для формы обновления свойств.</summary>
    public static void SetDocumentPropertyBatchDefaultCatalogFolder(string folder) =>
        SetFolderSetting("DocumentPropertyBatchDefaultCatalogFolder", folder);

    /// <summary>Полный путь к шаблону свойств деталей (*.prtprp) в каталоге настроек проекта.</summary>
    public static string DocumentPropertyPartTemplatePath =>
        ResolveSettingsTemplatePath(
            GetSetting("DocumentPropertyPartTemplatePath"),
            "Деталь.prtprp");

    /// <summary>Полный путь к шаблону свойств сборок (*.asmprp) в каталоге настроек проекта.</summary>
    public static string DocumentPropertyAsmTemplatePath =>
        ResolveSettingsTemplatePath(
            GetSetting("DocumentPropertyAsmTemplatePath"),
            "Сборка.asmprp");

    /// <summary>Сохраняет путь к шаблону свойств деталей (абсолютный или имя файла в SettingsPath).</summary>
    public static void SetDocumentPropertyPartTemplatePath(string path) =>
        SetSetting("DocumentPropertyPartTemplatePath", NormalizeTemplatePathForStorage(path));

    /// <summary>Сохраняет путь к шаблону свойств сборок (абсолютный или имя файла в SettingsPath).</summary>
    public static void SetDocumentPropertyAsmTemplatePath(string path) =>
        SetSetting("DocumentPropertyAsmTemplatePath", NormalizeTemplatePathForStorage(path));

    /// <summary>
    /// Имя каталога деталей (поиск рядом со сборкой, затем на 1 уровень выше).
    /// </summary>
    public static string BatchProjectRootPartsFolderName =>
        GetFolderNameSettingOrDefault("BatchProjectRootPartsFolderName", "Детали");

    /// <summary>Сохраняет имя каталога деталей для поиска рядом со сборкой.</summary>
    public static void SetBatchProjectRootPartsFolderName(string folderName) =>
        SetSetting("BatchProjectRootPartsFolderName", (folderName ?? string.Empty).Trim());

    /// <summary>Каталог DXF по умолчанию на одиночной форме экспорта.</summary>
    public static string DxfDefaultOutputFolder => GetExpandedSettingOrEmpty("DxfDefaultOutputFolder");

    /// <summary>Каталог PDF по умолчанию на одиночной форме экспорта.</summary>
    public static string PdfDefaultOutputFolder => GetExpandedSettingOrEmpty("PdfDefaultOutputFolder");

    /// <summary>
    /// Дефолтное значение свойства «Нужен dxf» при создании новых деталей.
    /// </summary>
    public static bool NeedDxfDefault => GetBoolSetting("NeedDxfDefault", false);

    /// <summary>
    /// Дефолтное значение свойства «Нужен pdf» при создании новых чертежей.
    /// </summary>
    public static bool NeedPdfDefault => GetBoolSetting("NeedPdfDefault", true);

    /// <summary>
    /// Дефолтное значение свойства «Нужен чертеж» при создании новых деталей и сборок.
    /// </summary>
    public static bool NeedDrawingDefault => GetBoolSetting("NeedDrawingDefault", true);

    /// <summary>Сохраняет каталог DXF для одиночной формы экспорта.</summary>
    public static void SetDxfDefaultOutputFolder(string folder) =>
        SetFolderSetting("DxfDefaultOutputFolder", folder);

    /// <summary>Сохраняет каталог PDF для одиночной формы экспорта.</summary>
    public static void SetPdfDefaultOutputFolder(string folder) =>
        SetFolderSetting("PdfDefaultOutputFolder", folder);

    /// <summary>Сохраняет дефолтное значение свойства «Нужен dxf».</summary>
    public static void SetNeedDxfDefault(bool value) =>
        SetSetting("NeedDxfDefault", value.ToString());

    /// <summary>Сохраняет дефолтное значение свойства «Нужен pdf».</summary>
    public static void SetNeedPdfDefault(bool value) =>
        SetSetting("NeedPdfDefault", value.ToString());

    /// <summary>Сохраняет дефолтное значение свойства «Нужен чертеж».</summary>
    public static void SetNeedDrawingDefault(bool value) =>
        SetSetting("NeedDrawingDefault", value.ToString());

    /// <summary>
    /// Читает строковую настройку из XML (через кэш <see cref="_settingCache"/>).
    /// </summary>
    /// <param name="key">Имя элемента внутри AppSettings.</param>
    /// <returns>Значение элемента или null при ошибке или отсутствии узла.</returns>
    public static string GetSetting(string key)
    {
      if (string.IsNullOrEmpty(key))
        return null;

      try
      {
        lock (ConfigLock)
        {
          int now = Environment.TickCount;
          CachedSetting cached;
          if (_settingCache.TryGetValue(key, out cached)
              && unchecked(now - cached.StampMs) < SettingCacheTtlMs)
            return cached.Value;

          XDocument doc = XDocument.Load(ConfigFullPath);
          string value = doc.Root?
              .Element("AppSettings")?
              .Element(key)?
              .Value;

          _settingCache[key] = new CachedSetting
          {
            Value = value,
            StampMs = now
          };
          return value;
        }
      }
      catch (Exception ex)
      {
        Logger.Error(ex.Message);
        return null;
      }
    }

    /// <summary>
    /// Записывает строковую настройку в XML и обновляет её кэш.
    /// </summary>
    /// <param name="key">Имя элемента.</param>
    /// <param name="value">Новое значение.</param>
    public static void SetSetting(string key, string value)
    {
      if (string.IsNullOrEmpty(key))
        return;

      try
      {
        lock (ConfigLock)
        {
          XDocument doc = XDocument.Load(ConfigFullPath);
          XElement app = doc.Root?.Element("AppSettings");
          if (app == null)
            return;

          XElement element = app.Element(key);
          if (element != null)
            element.Value = value;
          else
            app.Add(new XElement(key, value));

          doc.Save(ConfigFullPath);
          _settingCache[key] = new CachedSetting
          {
            Value = value,
            StampMs = Environment.TickCount
          };
        }
      }
      catch (Exception ex)
      {
        Logger.Error(ex.Message);
        // Кэш мог разойтись с файлом — сбрасываем полностью.
        InvalidateSettingCache();
      }
    }

    /// <summary>
    /// Сбрасывает кэш значений Settings.xml (после сторонней правки файла,
    /// перезагрузки контекста ISIDA, смены корневых каталогов).
    /// </summary>
    public static void InvalidateSettingCache()
    {
      lock (ConfigLock)
        _settingCache.Clear();
    }

    /// <summary>Кэшированное значение одной настройки вместе с меткой времени чтения.</summary>
    private sealed class CachedSetting
    {
      /// <summary>Значение элемента (null — узел отсутствует или файл не читался успешно).</summary>
      public string Value;

      /// <summary>Environment.TickCount на момент чтения (сравнение через unchecked-вычитание).</summary>
      public int StampMs;
    }

    /// <summary>
    /// Обновляет пути в конфигурации на стандартные под %ProgramData%\VELUM (как UpdateConfigPaths в AIStudio).
    /// </summary>
    public static void UpdateConfigPaths()
    {
      string root = DefaultVelumDataRootExpression;
      try
      {
        SetSetting("DataFolderPath", root + @"\Data");
        SetSetting("SettingsPath", root + @"\Settings");
        SetSetting("LogsFolderPath", root + @"\Logs");
        SetSetting("BootDataFolderPath", root + @"\BootData");
        SetSetting("EnvironmentRecipesFilePath", root + @"\BootData\Environment\EnvironmentRecipes.yaml");
        SetSetting("ScenarioReportsFolderPath", root + @"\Scenarios\Reports");
        SetSetting("ProductRegistryFolderPath", root + @"\ProductRegistry");
        SetSetting("AssemblyRegistryFolderPath", root + @"\AssemblyRegistry");
        Logger.Info("Velum: конфигурационные пути обновлены для " + root);
      }
      catch (Exception ex)
      {
        Logger.Error(ex.Message);
      }
    }

    private static CommandBufferPolicy ResolveCommandBufferPolicy()
    {
      return CommandBufferPolicy.LoadOrDefault(VelumAdapterPackage.TryResolvePackageRoot());
    }

    private static void CreateDefaultConfig()
    {
      string root = DefaultVelumDataRootExpression;
      CommandBufferPolicy commandBufferPolicy = ResolveCommandBufferPolicy();
      var defaultConfig = new XDocument(
          new XElement("Configuration",
              new XElement("AppSettings",
                  new XElement("DataFolderPath", root + @"\Data"),
                  new XElement("SettingsPath", root + @"\Settings"),
                  new XElement("LogsFolderPath", root + @"\Logs"),
                  new XElement("BootDataFolderPath", root + @"\BootData"),
                  new XElement("EnvironmentRecipesFilePath", root + @"\BootData\Environment\EnvironmentRecipes.yaml"),
                  new XElement("ScenarioReportsFolderPath", root + @"\Scenarios\Reports"),
                  new XElement("ProductRegistryFolderPath", root + @"\ProductRegistry"),
                  new XElement("AssemblyRegistryFolderPath", root + @"\AssemblyRegistry"),
                  new XElement("DocumentRootPaths", string.Empty),
                  new XElement("DefaultGeneticReflexId", 0),
                  new XElement("DefaultStileId", 0),
                  new XElement("DefaultAdaptiveActionId", 0),
                  new XElement("DefaultThemeTypeId", 4),
                  new XElement("RecognitionThreshold", 3),
                  new XElement("CompareLevel", 30),
                  new XElement("DifSensorPar", "0.02"),
                  new XElement("HomeostasisPulseSpeedDriftEnabled", false),
                  new XElement("SolidEnvironmentMetricDeltaEpsilon", "0.51"),
                  new XElement("SolidHostImpulseMinParameterDelta", "0.25"),
                  new XElement("SolidHomeostasisPulseTrace", false),
                  new XElement("SolidProbeTimeoutMs", 2500),
                  new XElement("HeavyMetricsPulsePeriod", 5),
                  new XElement("SolidProbeUseStaleSnapshotOnTimeout", true),
                  new XElement("CadDegradedEnterThreshold", "50"),
                  new XElement("CadDegradedExitThreshold", "60"),
                  new XElement("RecipeDispatchEnabled", true),
                  new XElement("RecipeDispatchCooldownPulses", 3),
                  new XElement("DxfFileNameTemplate", string.Empty),
                  new XElement("DxfFileNameTemplateLastUsed", string.Empty),
                  new XElement("DxfFileNameSuffixList", string.Empty),
                  new XElement("DxfDefaultOutputFolder", string.Empty),
                  new XElement("PdfDefaultOutputFolder", string.Empty),
                  new XElement("NeedDxfDefault", false),
                  new XElement("NeedPdfDefault", true),
                  new XElement("NeedDrawingDefault", true),
                  new XElement("CommandBufferIdleFlushMs", commandBufferPolicy.IdleFlushMs),
                  new XElement("CommandBufferMaxTokens", commandBufferPolicy.MaxTokens),
                  new XElement("CommandBufferMaxAgeMs", commandBufferPolicy.MaxAgeMs),
                  new XElement("CommandBufferRecordingEnabled", true),
                  new XElement("DynamicTime", 50),
                  new XElement("ReflexActionDisplayDuration", 3),
                  new XElement("WaitingPeriodForActionsVal", 30),
                  new XElement("ThinkingCycleDecayAgeDivisor", 100),
                  new XElement("ThinkingCycleDecayBase", 1),
                   new XElement("ThinkingCycleMainMaxAgePulses", 1000),
                   new XElement("NoOperatorStimulusSilencePulses", 30),
                   new XElement("DocumentColorPart", 1),
                   new XElement("DocumentColorAssembly", 2),
                   new XElement("DocumentColorDrawing", 3),
                   new XElement("FirstRun", 1),
                   new XElement("LogEnabled", false),
                   new XElement("SolidHomeostasisDebugLog", false),
                   new XElement("LogFormat", "All"),
                  new XElement("VerbalAuthoritativeMode", false),
                  new XElement("ObservationMode", false),
                  new XElement("ProductRegistryAccessLevel", "admin"))));

      defaultConfig.Save(ConfigFullPath);
    }

    /// <summary>Добавляет уровень доступа к реестру изделий, если ключа ещё нет (по умолчанию admin).</summary>
    private static void EnsureProductRegistryAccessLevelSetting()
    {
      try
      {
        if (!File.Exists(ConfigFullPath))
          return;

        XDocument doc = XDocument.Load(ConfigFullPath);
        XElement app = doc.Root?.Element("AppSettings");
        if (app == null)
          return;

        if (app.Element("ProductRegistryAccessLevel") != null)
          return;

        app.Add(new XElement("ProductRegistryAccessLevel", "admin"));
        doc.Save(ConfigFullPath);
        Logger.Info("Velum: в Settings.xml добавлен ProductRegistryAccessLevel=admin");
      }
      catch (Exception ex)
      {
        Logger.Error(ex.Message);
      }
    }

    /// <summary>Добавляет путь каталога реестра изделий, если ключа ещё нет.</summary>
    private static void EnsureProductRegistryFolderPathSetting()
    {
      try
      {
        if (!File.Exists(ConfigFullPath))
          return;

        XDocument doc = XDocument.Load(ConfigFullPath);
        XElement app = doc.Root?.Element("AppSettings");
        if (app == null)
          return;

        if (app.Element("ProductRegistryFolderPath") != null)
          return;

        app.Add(new XElement("ProductRegistryFolderPath",
            DefaultVelumDataRootExpression + @"\ProductRegistry"));
        doc.Save(ConfigFullPath);
        Logger.Info("Velum: в Settings.xml добавлен ProductRegistryFolderPath");
      }
      catch (Exception ex)
      {
        Logger.Error(ex.Message);
      }
    }

    /// <summary>Добавляет путь каталога тех. требований, если ключа ещё нет.</summary>
    private static void EnsureTechRequirementsFolderPathSetting()
    {
      try
      {
        if (!File.Exists(ConfigFullPath))
          return;

        XDocument doc = XDocument.Load(ConfigFullPath);
        XElement app = doc.Root?.Element("AppSettings");
        if (app == null)
          return;

        if (app.Element("TechRequirementsFolderPath") != null)
          return;

        app.Add(new XElement("TechRequirementsFolderPath",
            DefaultVelumDataRootExpression + @"\TechRequirements"));
        doc.Save(ConfigFullPath);
        Logger.Info("Velum: в Settings.xml добавлен TechRequirementsFolderPath");
      }
      catch (Exception ex)
      {
        Logger.Error(ex.Message);
      }
    }

    /// <summary>Добавляет путь каталога настроек реестра изделия, если ключа ещё нет.</summary>
    private static void EnsureAssemblyRegistryFolderPathSetting()
    {
      try
      {
        if (!File.Exists(ConfigFullPath))
          return;

        XDocument doc = XDocument.Load(ConfigFullPath);
        XElement app = doc.Root?.Element("AppSettings");
        if (app == null)
          return;

        if (app.Element("AssemblyRegistryFolderPath") != null)
          return;

        app.Add(new XElement("AssemblyRegistryFolderPath",
            DefaultVelumDataRootExpression + @"\AssemblyRegistry"));
        doc.Save(ConfigFullPath);
        Logger.Info("Velum: в Settings.xml добавлен AssemblyRegistryFolderPath");
      }
      catch (Exception ex)
      {
        Logger.Error(ex.Message);
      }
    }

    /// <summary>
    /// Добавляет префиксы корневого каталога документов, если ключа ещё нет
    /// (по умолчанию пусто — прежнее поведение с абсолютными путями).
    /// </summary>
    private static void EnsureDocumentRootPathsSetting()
    {
      try
      {
        if (!File.Exists(ConfigFullPath))
          return;

        XDocument doc = XDocument.Load(ConfigFullPath);
        XElement app = doc.Root?.Element("AppSettings");
        if (app == null)
          return;

        if (app.Element("DocumentRootPaths") != null)
          return;

        app.Add(new XElement("DocumentRootPaths", string.Empty));
        doc.Save(ConfigFullPath);
        Logger.Info("Velum: в Settings.xml добавлен DocumentRootPaths");
      }
      catch (Exception ex)
      {
        Logger.Error(ex.Message);
      }
    }

    private static void EnsureScenarioReportsFolderSetting()
    {
      try
      {
        if (!File.Exists(ConfigFullPath))
          return;

        XDocument doc = XDocument.Load(ConfigFullPath);
        XElement app = doc.Root?.Element("AppSettings");
        if (app == null)
          return;

        if (app.Element("ScenarioReportsFolderPath") != null)
          return;

        app.Add(new XElement("ScenarioReportsFolderPath",
            DefaultVelumDataRootExpression + @"\Scenarios\Reports"));
        doc.Save(ConfigFullPath);
      }
      catch (Exception ex)
      {
        Logger.Error(ex.Message);
      }
    }

    /// <summary>
    /// Добавляет ключ дрейфа гомеостаза в существующий XML, если его ещё нет (Velum по умолчанию — false).
    /// </summary>
    private static void EnsureHomeostasisPulseSpeedDriftSetting()
    {
      try
      {
        if (!File.Exists(ConfigFullPath))
          return;

        XDocument doc = XDocument.Load(ConfigFullPath);
        XElement app = doc.Root?.Element("AppSettings");
        if (app == null)
          return;

        if (app.Element("HomeostasisPulseSpeedDriftEnabled") != null)
          return;

        app.Add(new XElement("HomeostasisPulseSpeedDriftEnabled", false));
        doc.Save(ConfigFullPath);
      }
      catch (Exception ex)
      {
        Logger.Error(ex.Message);
      }
    }

    /// <summary>
    /// Добавляет в существующий XML порог дельты метрик среды SolidWorks, если его ещё нет.
    /// </summary>
    private static void EnsureSolidEnvironmentMetricDeltaEpsilon()
    {
      try
      {
        if (!File.Exists(ConfigFullPath))
          return;

        XDocument doc = XDocument.Load(ConfigFullPath);
        XElement app = doc.Root?.Element("AppSettings");
        if (app == null)
          return;

        if (app.Element("SolidEnvironmentMetricDeltaEpsilon") != null)
          return;

        app.Add(new XElement("SolidEnvironmentMetricDeltaEpsilon", "0.51"));
        doc.Save(ConfigFullPath);
      }
      catch (Exception ex)
      {
        Logger.Error(ex.Message);
      }
    }

    /// <summary>
    /// Добавляет настройки бюджета COM-опроса SW на пульсе, если ключей ещё нет.
    /// </summary>
    private static void EnsureSolidProbeTimeoutSettings()
    {
      try
      {
        if (!File.Exists(ConfigFullPath))
          return;

        XDocument doc = XDocument.Load(ConfigFullPath);
        XElement app = doc.Root?.Element("AppSettings");
        if (app == null)
          return;

        bool changed = false;
        XElement timeoutEl = app.Element("SolidProbeTimeoutMs");
        if (timeoutEl == null)
        {
          app.Add(new XElement("SolidProbeTimeoutMs", 2500));
          changed = true;
        }
        else if (int.TryParse(timeoutEl.Value, out int timeoutMs) && timeoutMs > 0 && timeoutMs < 2000)
        {
          timeoutEl.Value = "2500";
          changed = true;
        }

        if (app.Element("SolidProbeUseStaleSnapshotOnTimeout") == null)
        {
          app.Add(new XElement("SolidProbeUseStaleSnapshotOnTimeout", true));
          changed = true;
        }

        if (changed)
          doc.Save(ConfigFullPath);
      }
      catch (Exception ex)
      {
        Logger.Error(ex.Message);
      }
    }

    /// <summary>Добавляет кратность пульса тяжёлых метрик, если ключа ещё нет.</summary>
    private static void EnsureHeavyMetricsPulsePeriodSetting()
    {
      try
      {
        if (!File.Exists(ConfigFullPath))
          return;

        XDocument doc = XDocument.Load(ConfigFullPath);
        XElement app = doc.Root?.Element("AppSettings");
        if (app == null)
          return;

        if (app.Element("HeavyMetricsPulsePeriod") != null)
          return;

        app.Add(new XElement("HeavyMetricsPulsePeriod", 5));
        doc.Save(ConfigFullPath);
        Logger.Info("Velum: в Settings.xml добавлен HeavyMetricsPulsePeriod=5");
      }
      catch (Exception ex)
      {
        Logger.Error(ex.Message);
      }
    }

    /// <summary>
    /// Добавляет настройки dispatch рецептов, если ключей ещё нет.
    /// </summary>
    private static void EnsureRecipeDispatchSettings()
    {
      try
      {
        if (!File.Exists(ConfigFullPath))
          return;

        XDocument doc = XDocument.Load(ConfigFullPath);
        XElement app = doc.Root?.Element("AppSettings");
        if (app == null)
          return;

        bool changed = false;
        if (app.Element("RecipeDispatchEnabled") == null)
        {
          app.Add(new XElement("RecipeDispatchEnabled", true));
          changed = true;
        }

        if (app.Element("RecipeDispatchCooldownPulses") == null)
        {
          app.Add(new XElement("RecipeDispatchCooldownPulses", 3));
          changed = true;
        }

        if (changed)
          doc.Save(ConfigFullPath);
      }
      catch (Exception ex)
      {
        Logger.Error(ex.Message);
      }
    }

    /// <summary>
    /// Добавляет коды зрительного канала для типов документов, если ключей ещё нет
    /// (деталь=1, сборка=2, чертёж=3).
    /// </summary>
    private static void EnsureDocumentVisualColorSettings()
    {
      try
      {
        if (!File.Exists(ConfigFullPath))
          return;

        XDocument doc = XDocument.Load(ConfigFullPath);
        XElement app = doc.Root?.Element("AppSettings");
        if (app == null)
          return;

        bool changed = false;
        if (app.Element("DocumentColorPart") == null)
        {
          app.Add(new XElement("DocumentColorPart", 1));
          changed = true;
        }

        if (app.Element("DocumentColorAssembly") == null)
        {
          app.Add(new XElement("DocumentColorAssembly", 2));
          changed = true;
        }

        if (app.Element("DocumentColorDrawing") == null)
        {
          app.Add(new XElement("DocumentColorDrawing", 3));
          changed = true;
        }

        if (changed)
          doc.Save(ConfigFullPath);
      }
      catch (Exception ex)
      {
        Logger.Error(ex.Message);
      }
    }

    /// <summary>Добавляет настройки экспорта DXF, если ключей ещё нет.</summary>
    private static void EnsureDxfExportSettings()
    {
      try
      {
        if (!File.Exists(ConfigFullPath))
          return;

        XDocument doc = XDocument.Load(ConfigFullPath);
        XElement app = doc.Root?.Element("AppSettings");
        if (app == null)
          return;

        bool changed = false;
        if (app.Element("DxfFileNameTemplate") == null)
        {
          app.Add(new XElement("DxfFileNameTemplate", string.Empty));
          changed = true;
        }

        if (app.Element("DxfFileNameTemplateLastUsed") == null)
        {
          app.Add(new XElement("DxfFileNameTemplateLastUsed", string.Empty));
          changed = true;
        }

        if (app.Element("DxfFileNameSuffixList") == null)
        {
          app.Add(new XElement("DxfFileNameSuffixList", string.Empty));
          changed = true;
        }

        if (app.Element("DxfBatchDefaultDxfFolder") == null)
        {
          app.Add(new XElement("DxfBatchDefaultDxfFolder", string.Empty));
          changed = true;
        }

        if (app.Element("PdfBatchDefaultPdfFolder") == null)
        {
          app.Add(new XElement("PdfBatchDefaultPdfFolder", string.Empty));
          changed = true;
        }

        if (app.Element("AssemblyRegistryDxfExportFolder") == null)
        {
          app.Add(new XElement("AssemblyRegistryDxfExportFolder", string.Empty));
          changed = true;
        }

        if (app.Element("AssemblyRegistryPdfExportFolder") == null)
        {
          app.Add(new XElement("AssemblyRegistryPdfExportFolder", string.Empty));
          changed = true;
        }

        if (app.Element("MaterialBatchDefaultPartsFolder") == null)
        {
          app.Add(new XElement("MaterialBatchDefaultPartsFolder", string.Empty));
          changed = true;
        }

        if (app.Element("DocumentPropertyBatchDefaultCatalogFolder") == null)
        {
          app.Add(new XElement("DocumentPropertyBatchDefaultCatalogFolder", string.Empty));
          changed = true;
        }

        if (app.Element("DocumentPropertyPartTemplatePath") == null)
        {
          app.Add(new XElement("DocumentPropertyPartTemplatePath", "Деталь.prtprp"));
          changed = true;
        }

        if (app.Element("DocumentPropertyAsmTemplatePath") == null)
        {
          app.Add(new XElement("DocumentPropertyAsmTemplatePath", "Сборка.asmprp"));
          changed = true;
        }

        if (app.Element("BatchProjectRootPartsFolderName") == null)
        {
          app.Add(new XElement("BatchProjectRootPartsFolderName", "Детали"));
          changed = true;
        }

        if (app.Element("DxfDefaultProjectionView") == null)
        {
          app.Add(new XElement("DxfDefaultProjectionView", "Front"));
          changed = true;
        }

        if (app.Element("DxfDefaultOutputFolder") == null)
        {
          app.Add(new XElement("DxfDefaultOutputFolder", string.Empty));
          changed = true;
        }

        if (app.Element("PdfDefaultOutputFolder") == null)
        {
          app.Add(new XElement("PdfDefaultOutputFolder", string.Empty));
          changed = true;
        }

        if (changed)
          doc.Save(ConfigFullPath);
      }
      catch (Exception ex)
      {
        Logger.Error(ex.Message);
      }
    }

    /// <summary>
    /// Добавляет настройки дефолтных свойств документов, если ключей ещё нет.
    /// </summary>
    private static void EnsureDocumentDefaultsSettings()
    {
      try
      {
        if (!File.Exists(ConfigFullPath))
          return;

        XDocument doc = XDocument.Load(ConfigFullPath);
        XElement app = doc.Root?.Element("AppSettings");
        if (app == null)
          return;

        bool changed = false;
        if (app.Element("NeedDxfDefault") == null)
        {
          app.Add(new XElement("NeedDxfDefault", false));
          changed = true;
        }

        if (app.Element("NeedPdfDefault") == null)
        {
          app.Add(new XElement("NeedPdfDefault", true));
          changed = true;
        }

        if (app.Element("NeedDrawingDefault") == null)
        {
          app.Add(new XElement("NeedDrawingDefault", true));
          changed = true;
        }

        if (changed)
          doc.Save(ConfigFullPath);
      }
      catch (Exception ex)
      {
        Logger.Error(ex.Message);
      }
    }

    /// <summary>
    /// Добавляет настройки Command idle-flush, если ключей ещё нет.
    /// </summary>
    private static void EnsureCommandBufferSettings()
    {
      try
      {
        if (!File.Exists(ConfigFullPath))
          return;

        XDocument doc = XDocument.Load(ConfigFullPath);
        XElement app = doc.Root?.Element("AppSettings");
        if (app == null)
          return;

        bool changed = false;
        CommandBufferPolicy commandBufferPolicy = ResolveCommandBufferPolicy();
        if (app.Element("CommandBufferIdleFlushMs") == null)
        {
          app.Add(new XElement("CommandBufferIdleFlushMs", commandBufferPolicy.IdleFlushMs));
          changed = true;
        }

        if (app.Element("CommandBufferRecordingEnabled") == null)
        {
          app.Add(new XElement("CommandBufferRecordingEnabled", true));
          changed = true;
        }

        if (app.Element("CommandBufferMaxTokens") == null)
        {
          app.Add(new XElement("CommandBufferMaxTokens", commandBufferPolicy.MaxTokens));
          changed = true;
        }

        if (app.Element("CommandBufferMaxAgeMs") == null)
        {
          app.Add(new XElement("CommandBufferMaxAgeMs", commandBufferPolicy.MaxAgeMs));
          changed = true;
        }

        if (changed)
          doc.Save(ConfigFullPath);
      }
      catch (Exception ex)
      {
        Logger.Error(ex.Message);
      }
    }

    /// <summary>
    /// Добавляет пути Reactive Core в Settings.xml после обновления с более старой версии Velum.
    /// </summary>
    private static void EnsureReactiveCorePathSettings()
    {
      try
      {
        if (!File.Exists(ConfigFullPath))
          return;

        XDocument doc = XDocument.Load(ConfigFullPath);
        XElement app = doc.Root?.Element("AppSettings");
        if (app == null)
          return;

        string root = DefaultVelumDataRootExpression;
        bool changed = false;

        if (app.Element("EnvironmentRecipesFilePath") == null)
        {
          app.Add(new XElement("EnvironmentRecipesFilePath", root + @"\BootData\Environment\EnvironmentRecipes.yaml"));
          changed = true;
        }

        if (changed)
        {
          doc.Save(ConfigFullPath);
          Logger.Info("Velum: в Settings.xml добавлен путь EnvironmentRecipesFilePath");
        }
      }
      catch (Exception ex)
      {
        Logger.Error(ex.Message);
      }
    }

    private static void EnsureCadDegradedThresholdSettings()
    {
      try
      {
        if (!File.Exists(ConfigFullPath))
          return;

        XDocument doc = XDocument.Load(ConfigFullPath);
        XElement app = doc.Root?.Element("AppSettings");
        if (app == null)
          return;

        bool changed = false;
        if (app.Element("CadDegradedEnterThreshold") == null)
        {
          app.Add(new XElement("CadDegradedEnterThreshold", "50"));
          changed = true;
        }

        if (app.Element("CadDegradedExitThreshold") == null)
        {
          app.Add(new XElement("CadDegradedExitThreshold", "60"));
          changed = true;
        }

        if (changed)
          doc.Save(ConfigFullPath);
      }
      catch (Exception ex)
      {
        Logger.Error(ex.Message);
      }
    }

    /// <summary>
    /// Добавляет порог импульсной записи P_i в движок, если ключа ещё нет (см. <see cref="SolidHostImpulseMinParameterDelta"/>).
    /// </summary>
    private static void EnsureSolidHostImpulseMinParameterDelta()
    {
      try
      {
        if (!File.Exists(ConfigFullPath))
          return;

        XDocument doc = XDocument.Load(ConfigFullPath);
        XElement app = doc.Root?.Element("AppSettings");
        if (app == null)
          return;

        if (app.Element("SolidHostImpulseMinParameterDelta") != null)
          return;

        app.Add(new XElement("SolidHostImpulseMinParameterDelta", "0.25"));
        doc.Save(ConfigFullPath);
      }
      catch (Exception ex)
      {
        Logger.Error(ex.Message);
      }
    }

    /// <summary>
    /// Добавляет флаг трассировки пульса гомеостаза, если ключа ещё нет (см. <see cref="SolidHomeostasisPulseTrace"/>).
    /// </summary>
    private static void EnsureSolidHomeostasisPulseTrace()
    {
      try
      {
        if (!File.Exists(ConfigFullPath))
          return;

        XDocument doc = XDocument.Load(ConfigFullPath);
        XElement app = doc.Root?.Element("AppSettings");
        if (app == null)
          return;

        if (app.Element("SolidHomeostasisPulseTrace") != null)
          return;

        app.Add(new XElement("SolidHomeostasisPulseTrace", false));
        doc.Save(ConfigFullPath);
      }
      catch (Exception ex)
      {
        Logger.Error(ex.Message);
      }
    }

    /// <summary>
    /// Подставляет переменные окружения в путях из XML (например <c>%ProgramData%\VELUM\...</c>).
    /// </summary>
    /// <param name="value">Строка из настроек.</param>
    /// <returns>Развёрнутый путь или исходная строка.</returns>
    private static string ExpandDataPath(string value)
    {
      if (string.IsNullOrWhiteSpace(value))
        return value;

      try
      {
        return Environment.ExpandEnvironmentVariables(value.Trim());
      }
      catch
      {
        return value.Trim();
      }
    }

    private static string GetExpandedDataPathOrDefault(string key, string defaultPath)
    {
      string path = ExpandDataPath(GetSetting(key));
      return string.IsNullOrWhiteSpace(path) ? defaultPath : path;
    }

    private static string GetExpandedSettingOrEmpty(string key)
    {
      string path = ExpandDataPath(GetSetting(key));
      return string.IsNullOrWhiteSpace(path) ? string.Empty : path;
    }

    private static string GetFolderNameSettingOrDefault(string key, string defaultName)
    {
      // null — ключ ещё не задан → дефолт; пустая строка — явное «использовать каталог сборки».
      string value = GetSetting(key);
      if (value == null)
        return defaultName ?? string.Empty;

      return value.Trim();
    }

    private static void SetFolderSetting(string key, string folder) =>
        SetSetting(key, NormalizeFolderPathForStorage(folder));

    /// <summary>
    /// Путь к шаблону в SettingsPath: абсолютный путь, либо имя файла относительно SettingsPath.
    /// </summary>
    private static string ResolveSettingsTemplatePath(string configured, string defaultFileName)
    {
      string settingsDir = SettingsPath ?? string.Empty;
      string trimmed = (configured ?? string.Empty).Trim();
      if (!string.IsNullOrEmpty(trimmed))
      {
        try
        {
          string expanded = ExpandDataPath(trimmed);
          if (!string.IsNullOrWhiteSpace(expanded) && File.Exists(expanded))
            return Path.GetFullPath(expanded);

          if (!Path.IsPathRooted(trimmed) && !string.IsNullOrWhiteSpace(settingsDir))
          {
            string underSettings = Path.Combine(settingsDir, trimmed);
            if (File.Exists(underSettings))
              return Path.GetFullPath(underSettings);
          }
        }
        catch
        {
        }
      }

      if (string.IsNullOrWhiteSpace(settingsDir) || string.IsNullOrWhiteSpace(defaultFileName))
        return string.Empty;

      try
      {
        return Path.GetFullPath(Path.Combine(settingsDir, defaultFileName));
      }
      catch
      {
        return Path.Combine(settingsDir, defaultFileName);
      }
    }

    /// <summary>
    /// Если файл лежит в SettingsPath — сохраняем только имя файла, иначе путь с нормализацией корня.
    /// </summary>
    private static string NormalizeTemplatePathForStorage(string path)
    {
      string trimmed = (path ?? string.Empty).Trim();
      if (string.IsNullOrWhiteSpace(trimmed))
        return string.Empty;

      try
      {
        string full = Path.GetFullPath(ExpandDataPath(trimmed) ?? trimmed);
        string settingsDir = SettingsPath;
        if (!string.IsNullOrWhiteSpace(settingsDir))
        {
          string settingsFull = Path.GetFullPath(settingsDir);
          if (string.Equals(
                  Path.GetDirectoryName(full) ?? string.Empty,
                  settingsFull,
                  StringComparison.OrdinalIgnoreCase))
            return Path.GetFileName(full);
        }

        return NormalizeFolderPathForStorage(full);
      }
      catch
      {
        return trimmed;
      }
    }

    /// <summary>
    /// Сохраняет каталог в XML: пути внутри корня проекта — как <c>%ProgramData%\VELUM\…</c>.
    /// Сетевые пути (\server\share) и UNC-пути пропускаются без нормализации.
    /// </summary>
    private static string NormalizeFolderPathForStorage(string path)
    {
      string trimmed = (path ?? string.Empty).Trim();
      if (string.IsNullOrWhiteSpace(trimmed))
        return string.Empty;

      // UNC/сетевые пути — не нормализуем, оставляем как есть
      if (trimmed.StartsWith("\\", StringComparison.Ordinal))
        return trimmed;

      if (trimmed.IndexOf('%') >= 0)
        return trimmed;

      try
      {
        string full = Path.GetFullPath(trimmed);
        if (SettingsValidator.TryInferProjectRoot(SettingsPath, DataFolderPath, out string projectRoot))
        {
          string rootFull = Path.GetFullPath(projectRoot);
          if (full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
          {
            string suffix = full.Substring(rootFull.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string rootExpr = ResolveProjectDataRootExpression();
            return string.IsNullOrEmpty(suffix) ? rootExpr : rootExpr + "\\" + suffix;
          }
        }
      }
      catch
      {
      }

      return trimmed;
    }

    private static string ResolveProjectDataRootExpression()
    {
      string rawSettings = GetSetting("SettingsPath");
      if (!string.IsNullOrWhiteSpace(rawSettings))
      {
        string trimmed = rawSettings.Trim().TrimEnd('\\', '/');
        const string settingsSuffix = "\\Settings";
        if (trimmed.EndsWith(settingsSuffix, StringComparison.OrdinalIgnoreCase))
          return trimmed.Substring(0, trimmed.Length - settingsSuffix.Length);
      }

      string rawData = GetSetting("DataFolderPath");
      if (!string.IsNullOrWhiteSpace(rawData))
      {
        string trimmed = rawData.Trim().TrimEnd('\\', '/');
        const string dataSuffix = "\\Data";
        if (trimmed.EndsWith(dataSuffix, StringComparison.OrdinalIgnoreCase))
          return trimmed.Substring(0, trimmed.Length - dataSuffix.Length);
      }

      return DefaultVelumDataRootExpression;
    }

    private static void TryCreateDirectory(string path)
    {
      if (string.IsNullOrWhiteSpace(path))
        return;

      try
      {
        Directory.CreateDirectory(path);
      }
      catch
      {
        // игнорируем отсутствие прав при старте
      }
    }

    /// <summary>
    /// Добавляет каталог обмена BOM с 1C, если ключа ещё нет.
    /// </summary>
    private static void EnsureBomExchangeFolderSetting()
    {
      try
      {
        if (!File.Exists(ConfigFullPath))
          return;

        XDocument doc = XDocument.Load(ConfigFullPath);
        XElement app = doc.Root?.Element("AppSettings");
        if (app == null)
          return;

        if (app.Element("BomExchangeFolder") != null)
          return;

        app.Add(new XElement("BomExchangeFolder", string.Empty));
        doc.Save(ConfigFullPath);
        Logger.Info("Velum: в Settings.xml добавлен BomExchangeFolder");
      }
      catch (Exception ex)
      {
        Logger.Error(ex.Message);
      }
    }

    private static int GetIntSetting(string key, int defaultValue)
    {
      string value = GetSetting(key);
      int result;
      if (int.TryParse(value, out result))
        return result;
      return defaultValue;
    }

    private static float GetFloatSetting(string key, float defaultValue)
    {
      string value = GetSetting(key);
      float result;
      if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
        return result;
      return defaultValue;
    }

    private static bool GetBoolSetting(string key, bool defaultValue)
    {
      string value = GetSetting(key);
      bool result;
      if (bool.TryParse(value, out result))
        return result;
      return defaultValue;
    }

    private static ResearchLogger.LogFormat GetLogFormatSetting(string key, ResearchLogger.LogFormat defaultValue)
    {
      string value = GetSetting(key);
      ResearchLogger.LogFormat parsed;
      if (Enum.TryParse(value, true, out parsed))
        return parsed;

      int intValue;
      if (int.TryParse(value, out intValue) && Enum.IsDefined(typeof(ResearchLogger.LogFormat), intValue))
        return (ResearchLogger.LogFormat)intValue;

      return defaultValue;
    }
  }
}
