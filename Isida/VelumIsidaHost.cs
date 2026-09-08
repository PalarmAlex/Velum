using System;
using System.Reflection;
using ISIDA.Actions;
using ISIDA.Common;
using ISIDA.Gomeostas;
using ISIDA.Psychic;
using ISIDA.Psychic.Automatism;
using ISIDA.Reflexes;
using ISIDA.Sensors;
using Velum.Configuration;
using Velum.SolidHomeostasis;

namespace Velum.Isida
{
  /// <summary>
  /// Ленивая инициализация движка ISIDA и доступ к <see cref="IsidaContext"/>
  /// </summary>
  public static class VelumIsidaHost
  {
    private static readonly object Gate = new object();
    private static IsidaContext _context;
    private static string _lastError;

    /// <summary>
    /// Возвращает признак успешной инициализации контекста ISIDA.
    /// </summary>
    public static bool IsReady => _context != null;

    /// <summary>
    /// Текущий контекст ISIDA; требует предварительного успешного <see cref="TryInitialize"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Контекст ещё не создан.</exception>
    public static IsidaContext Context
    {
      get
      {
        if (_context == null)
        {
          throw new InvalidOperationException(
              "ISIDA не инициализирована. Вызовите TryInitialize() или проверьте LastError.");
        }

        return _context;
      }
    }

    /// <summary>
    /// Текст последней ошибки инициализации, если <see cref="TryInitialize"/> вернул false.
    /// </summary>
    public static string LastError => _lastError;

    /// <summary>
    /// Создаёт <see cref="IsidaContext"/> с путями из <see cref="VelumAppConfig"/> (%ProgramData%\VELUM\…).
    /// </summary>
    /// <param name="errorMessage">Сообщение об ошибке при неуспехе.</param>
    /// <returns>true, если контекст готов к использованию.</returns>
    public static bool TryInitialize(out string errorMessage)
    {
      lock (Gate)
      {
        errorMessage = null;
        if (_context != null)
          return true;

        if (OrphanIsidaSubsystemsPresent())
          ReleaseOrphanPsychicSingletons();

        try
        {
          VelumAppConfig.EnsureInitialized();

          var config = new IsidaConfig();
          config.BaseDirectory = VelumAppConfig.BaseDataPath;
          config.DataFolder = VelumAppConfig.DataFolderPath;
          config.LogsFolder = VelumAppConfig.LogsFolderPath;
          config.BootDataFolder = VelumAppConfig.BootDataFolderPath;
          config.LogFormat = VelumAppConfig.LogFormat;
          config.LogEnabled = VelumAppConfig.LogEnabled;
          config.DefaultStileId = VelumAppConfig.DefaultStileId;
          config.CompareLevel = VelumAppConfig.CompareLevel;
          config.DifSensorPar = VelumAppConfig.DifSensorPar;
          config.HomeostasisPulseSpeedDriftEnabled = VelumAppConfig.HomeostasisPulseSpeedDriftEnabled;
          config.DynamicTime = VelumAppConfig.DynamicTime;
          config.ReflexActionDisplayDuration = VelumAppConfig.ReflexActionDisplayDuration;
          config.DefaultAdaptiveActionId = VelumAppConfig.DefaultAdaptiveActionId;
          config.DefaultThemeTypeId = VelumAppConfig.DefaultThemeTypeId;
          config.RecognitionThreshold = VelumAppConfig.RecognitionThreshold;
          config.WaitingPeriodForActionsVal = VelumAppConfig.WaitingPeriodForActionsVal;
          config.ThinkingCycleDecayAgeDivisor = VelumAppConfig.ThinkingCycleDecayAgeDivisor;
          config.ThinkingCycleDecayBase = VelumAppConfig.ThinkingCycleDecayBase;
          config.ThinkingCycleMainMaxAgePulses = VelumAppConfig.ThinkingCycleMainMaxAgePulses;
          config.NoOperatorStimulusSilencePulses = VelumAppConfig.NoOperatorStimulusSilencePulses;
          config.Stage2SearchPlayStyleIds = string.Join(",", VelumAppConfig.Stage2SearchPlayStyleIds);

          config.Validate();
          _context = IsidaEngine.Create(config);

          VelumAgentRuntimePreferences.ApplyFromVelumConfig();

          // Устанавливаем делегат проверки рецепта для PurposeGeneticSystem.
          // Позволяет селективный клон проверять наличие реального рецепта в RecipeCatalog.
          if (PurposeGeneticImageSystem.IsInitialized)
          {
            PurposeGeneticImageSystem.Instance.SetRecipeChecker(
                Velum.ReactiveCore.RecipeCatalog.ExistsByAdaptiveActionId);
          }

          VelumSolidEnvironmentBridge.HookAfterIsidaReady();

          // Не поднимаем стадию с 0 до 1 здесь: стадия 0 — валидное сохранённое состояние агента (файл свойств
          // в каталоге гомеостаза). Условие AppGlobalState.EvolutionStage < 1 ломало возврат на стадию 0 после перезапуска SW.
          // Для нового агента без файла ISIDA сама задаёт стадию при первом создании данных (см. GomeostasSystem.LoadAgentProperties).

          _lastError = null;
          return true;
        }
        catch (Exception ex)
        {
          errorMessage = ex.Message;
          _lastError = errorMessage;
          _context = null;
          ReleaseOrphanPsychicSingletons();
          return false;
        }
      }
    }

    /// <summary>
    /// Освобождает контекст ISIDA (вызывать из <c>OnDisconnect</c> надстройки).
    /// </summary>
    public static void Shutdown()
    {
      lock (Gate)
      {
        try
        {
          VelumSolidEnvironmentBridge.Unhook();
          if (_context != null)
          {
            try
            {
              _context.Dispose();
            }
            finally
            {
              _context = null;
            }
          }
        }
        finally
        {
          ReleaseOrphanPsychicSingletons();
        }
      }
    }

    /// <summary>
    /// Сбрасывает статические синглтоны ISIDA, если после сбоя <see cref="IsidaEngine.Create"/> или
    /// неполного <see cref="IsidaContext.Dispose"/> они остались поднятыми при <c>_context == null</c>.
    /// Вызывается из <see cref="TryInitialize"/> в <c>catch</c>, при обнаружении «осиротевших» синглтонов перед
    /// повторным Create (см. <see cref="OrphanIsidaSubsystemsPresent"/>) и из <see cref="Shutdown"/>.
    /// Порядок снятия соответствует фрагменту <see cref="IsidaContext.Dispose"/> в isida.dll.
    /// </summary>
    private static void ReleaseOrphanPsychicSingletons()
    {
      TryDisposeSubsystem(() => GeneticReflexFileLoader.IsInitialized, () => GeneticReflexFileLoader.Instance);
      TryDisposeSubsystem(() => AutomatizmFileLoader.IsInitialized, () => AutomatizmFileLoader.Instance);
      TryDisposeSubsystem(() => AutomatismResultTracker.IsInitialized, () => AutomatismResultTracker.Instance);
      TryDisposeSubsystem(() => AutomatizmChainsSystem.IsInitialized, () => AutomatizmChainsSystem.Instance);
      TryDisposeSubsystem(() => AutomatizmSystem.IsInitialized, () => AutomatizmSystem.Instance);
      TryDisposeSubsystem(() => AutomatizmTreeSystem.IsInitialized, () => AutomatizmTreeSystem.Instance);
      TryDisposeSubsystem(() => PurposeGeneticImageSystem.IsInitialized, () => PurposeGeneticImageSystem.Instance);
      TryDisposeSubsystem(() => ActionsImagesSystem.IsInitialized, () => ActionsImagesSystem.Instance);
      TryDisposeSubsystem(() => InfluenceActionsImagesSystem.IsInitialized, () => InfluenceActionsImagesSystem.Instance);
      TryDisposeSubsystem(
          () => ConditionedReflexFormationService.IsInitialized,
          () => ConditionedReflexFormationService.Instance);
      TryDisposeSubsystem(() => ReflexesActivator.IsInitialized, () => ReflexesActivator.Instance);
      TryDisposeSubsystem(() => ReflexExecutionService.IsInitialized, () => ReflexExecutionService.Instance);
      TryDisposeSubsystem(() => ReflexTreeSystem.IsInitialized, () => ReflexTreeSystem.Instance);
      TryDisposeSubsystem(() => ReflexChainsSystem.IsInitialized, () => ReflexChainsSystem.Instance);
      TryDisposeSubsystem(() => ConditionedReflexesSystem.IsInitialized, () => ConditionedReflexesSystem.Instance);
      TryDisposeSubsystem(() => GeneticReflexesSystem.IsInitialized, () => GeneticReflexesSystem.Instance);
      TryDisposeSubsystem(() => PerceptionImagesSystem.IsInitialized, () => PerceptionImagesSystem.Instance);
      TryDisposeSubsystem(() => SensorySystem.IsInitialized, () => SensorySystem.Instance);
      TryDisposeSubsystem(() => AdaptiveActionsSystem.IsInitialized, () => AdaptiveActionsSystem.Instance);
      TryDisposeSubsystem(() => EvolutionStageService.IsInitialized, () => EvolutionStageService.Instance);
      TryDisposeSubsystem(() => GomeostasSystem.IsInitialized, () => GomeostasSystem.Instance);
      TryDisposeSubsystem(() => InfluenceActionSystem.IsInitialized, () => InfluenceActionSystem.Instance);
      TryDisposeSubsystem(() => AutomatismExecutionService.IsInitialized, () => AutomatismExecutionService.Instance);
      TryDisposeSubsystem(() => OrientationReflexSystem.IsInitialized, () => OrientationReflexSystem.Instance);
      TryDisposeSubsystem(
          () => InformationEnvironmentSystem.IsInitialized,
          () => InformationEnvironmentSystem.Instance);

      TryResetAgentSleepOrchestratorStatic();
    }

    /// <summary>
    /// Признак того, что в домене приложения остались поднятые синглтоны ISIDA без живого <see cref="IsidaContext"/>
    /// у Velum (обрыв Create, неполный Dispose, сбой до присвоения <c>_context</c>).
    /// </summary>
    private static bool OrphanIsidaSubsystemsPresent()
    {
      return InformationEnvironmentSystem.IsInitialized
          || GomeostasSystem.IsInitialized
          || AdaptiveActionsSystem.IsInitialized
          || InfluenceActionSystem.IsInitialized
          || InfluenceActionsImagesSystem.IsInitialized
          || SensorySystem.IsInitialized
          || GeneticReflexesSystem.IsInitialized
          || ReflexChainsSystem.IsInitialized
          || GeneticReflexFileLoader.IsInitialized
          || AutomatizmFileLoader.IsInitialized
          || ConditionedReflexFormationService.IsInitialized;
    }

    private static void TryDisposeSubsystem(Func<bool> isReady, Func<IDisposable> getInstance)
    {
      try
      {
        if (isReady())
          getInstance().Dispose();
      }
      catch
      {
        // не блокируем завершение хоста
      }
    }

    /// <summary>
    /// В актуальной isida.dll есть <c>AgentSleepOrchestrator.Reset()</c>; в старых сборках метода нет — пропускаем.
    /// </summary>
    private static void TryResetAgentSleepOrchestratorStatic()
    {
      try
      {
        MethodInfo reset = typeof(AgentSleepOrchestrator).GetMethod(
            "Reset",
            BindingFlags.Public | BindingFlags.Static,
            null,
            Type.EmptyTypes,
            null);
        reset?.Invoke(null, null);
      }
      catch
      {
      }
    }
  }
}
