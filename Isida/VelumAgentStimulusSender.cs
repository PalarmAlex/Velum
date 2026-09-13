using System;
using System.Collections.Generic;
using System.Linq;
using ISIDA.Actions;
using ISIDA.Common;
using ISIDA.Psychic.Automatism;
using ISIDA.Sensors;
using Velum.SolidHomeostasis;

namespace Velum.Isida
{
  /// <summary>
  /// Передача стимула оператора в ISIDA: речь и команды — раздельные каналы.
  /// </summary>
  internal static class VelumAgentStimulusSender
  {
    /// <summary>
    /// Распознаёт речь и команды отдельно, применяет операторские воздействия через
    /// <see cref="InfluenceActionSystem.ApplyMultipleInfluenceActions"/>.
    /// </summary>
    /// <param name="normalizedVerbalLine">Нормализованная строка вербального канала (может быть пустой).</param>
    /// <param name="normalizedCommandLine">Нормализованная строка Command-канала (может быть пустой).</param>
    /// <param name="operatorInfluenceActionIds">ID воздействий с пульта оператора (EA).</param>
    /// <param name="toneId">ID тона сообщения.</param>
    /// <param name="moodId">ID настроения.</param>
    /// <param name="errorMessage">Текст ошибки при неуспехе.</param>
    public static bool TrySendOperatorStimulus(
        string normalizedVerbalLine,
        string normalizedCommandLine,
        IReadOnlyList<int> operatorInfluenceActionIds,
        int toneId,
        int moodId,
        out string errorMessage)
    {
      return TrySendOperatorStimulus(
          normalizedVerbalLine,
          normalizedCommandLine,
          operatorInfluenceActionIds,
          toneId,
          moodId,
          commandOnlyFlush: false,
          out errorMessage,
          out _);
    }

    /// <summary>
    /// Распознаёт речь и команды отдельно, применяет операторские воздействия через
    /// <see cref="InfluenceActionSystem.ApplyMultipleInfluenceActions"/>.
    /// </summary>
    /// <param name="normalizedVerbalLine">Нормализованная строка вербального канала (может быть пустой).</param>
    /// <param name="normalizedCommandLine">Нормализованная строка Command-канала (может быть пустой).</param>
    /// <param name="operatorInfluenceActionIds">ID воздействий с пульта оператора (EA).</param>
    /// <param name="toneId">ID тона сообщения.</param>
    /// <param name="moodId">ID настроения.</param>
    /// <param name="commandOnlyFlush">
    /// Idle-flush Command-буфера: при 0% распознавании стимул не отправляется, буфер уже снят — возврат success;
    /// при частичном распознавании в ISIDA уходят только распознанные pattern id, нераспознанные токены — warning в лог.
    /// </param>
    /// <param name="errorMessage">Текст ошибки при неуспехе.</param>
    /// <param name="stimulusApplied">false при command-only flush с 0% распознаванием команд.</param>
    public static bool TrySendOperatorStimulus(
        string normalizedVerbalLine,
        string normalizedCommandLine,
        IReadOnlyList<int> operatorInfluenceActionIds,
        int toneId,
        int moodId,
        bool commandOnlyFlush,
        out string errorMessage,
        out bool stimulusApplied)
    {
      errorMessage = null;
      stimulusApplied = false;
      var influenceIds = (operatorInfluenceActionIds ?? Array.Empty<int>())
          .Where(id => id > 0)
          .Distinct()
          .ToList();

      bool hasVerbal = !string.IsNullOrWhiteSpace(normalizedVerbalLine);
      bool hasCommand = !string.IsNullOrWhiteSpace(normalizedCommandLine);
      if (!hasVerbal && !hasCommand && influenceIds.Count == 0)
      {
        errorMessage = "Пустой стимул: нет текста, команд и не выбраны воздействия.";
        return false;
      }

      if (!VelumIsidaHost.TryInitialize(out errorMessage))
        return false;

      if (!GlobalTimer.IsPulsationRunning)
      {
        errorMessage = "Пульсация выключена — воздействия не применяются.";
        return false;
      }

      if (AppGlobalState.IsDead)
      {
        errorMessage = "Агент в состоянии «мертв» — стимул не применяется.";
        return false;
      }

      var sensory = VelumIsidaHost.Context.SensorySystem;
      bool verbalAuthoritative = sensory.VerbalAuthoritativeMode
          || (hasVerbal && VelumOperatorStimulusCodec.ShouldForceAuthoritativeVerbalWrite(normalizedVerbalLine));
      bool commandAuthoritative = sensory.CommandAuthoritativeMode;

      List<int> phraseIds = new List<int>();
      if (hasVerbal)
      {
        try
        {
          phraseIds = sensory.VerbalChannel.RecognizeText(
              normalizedVerbalLine.Trim(),
              verbalAuthoritative);
        }
        catch (Exception ex)
        {
          errorMessage = ex.Message;
          return false;
        }
      }

      string trimmedCommandLine = hasCommand ? normalizedCommandLine.Trim() : string.Empty;
      List<int> commandPatternIds = new List<int>();
      if (hasCommand)
      {
        try
        {
          commandPatternIds = sensory.CommandChannel.RecognizeText(
              trimmedCommandLine,
              commandAuthoritative);
        }
        catch (Exception ex)
        {
          errorMessage = ex.Message;
          return false;
        }

        if (commandPatternIds == null || commandPatternIds.Count == 0)
        {
          if (influenceIds.Count == 0 && !hasVerbal)
          {
            if (commandOnlyFlush)
            {
              Logger.Warning(
                  "Velum command buffer flush: 0% распознавание, стимул не отправлен: " +
                  trimmedCommandLine);
              return true;
            }

            errorMessage =
                "Команды не распознаны (нет совпадения в CommandPhrases.dat). " +
                "Проверьте DefaultCommandPrimaries, авторитарный режим командного канала или состав контуров.";
            return false;
          }

          commandPatternIds = new List<int>();
        }
        else
        {
          LogUnrecognizedCommandTokens(sensory, trimmedCommandLine, commandAuthoritative);
        }
      }

      if (hasVerbal && (phraseIds == null || phraseIds.Count == 0))
      {
        if (influenceIds.Count == 0 && commandPatternIds.Count == 0)
        {
          errorMessage =
              "Текст не распознан как паттерн (нет точного совпадения в дереве фраз). " +
              "Проверьте порог песочницы или авторитарный режим вербального канала.";
          return false;
        }

        phraseIds = new List<int>();
      }

      if (phraseIds.Count == 0 && commandPatternIds.Count == 0 && influenceIds.Count == 0)
      {
        errorMessage = "Нет данных для применения стимула.";
        return false;
      }

      var influence = VelumIsidaHost.Context.InfluenceActions;
      try
      {
        // Единая точка: резолвим код зрительного канала по типу активного документа SolidWorks,
        // чтобы у-рефлексы различали контекст «деталь / сборка / чертёж» (белый = контекст не ограничен).
        int visualColorId = VelumDocumentVisualColor.ResolveActiveDocumentColorId();

        var (success, err) = influence.ApplyMultipleInfluenceActions(
            influenceIds,
            phraseIds,
            commandPatternIds,
            verbalAuthoritative || commandAuthoritative,
            toneId,
            moodId,
            visualColorId: visualColorId);

        if (!success)
        {
          errorMessage = string.IsNullOrEmpty(err) ? "Ошибка применения воздействий." : err;
          return false;
        }

        stimulusApplied = true;

        if (AppGlobalState.IsAutomatizmChainActive && AutomatismExecutionService.IsInitialized)
          AutomatismExecutionService.Instance.ApplyStimulusEffectAndAdvanceChain();

        return true;
      }
      catch (Exception ex)
      {
        errorMessage = ex.Message;
        return false;
      }
    }

    private static void LogUnrecognizedCommandTokens(
        SensorySystem sensory,
        string commandLine,
        bool commandAuthoritative)
    {
      List<string> unrecognized = CollectUnrecognizedCommandTokens(sensory, commandLine, commandAuthoritative);
      if (unrecognized.Count == 0)
        return;

      Logger.Warning(
          "Velum command buffer: нераспознанные токены (пропущены): " +
          string.Join(" ", unrecognized) +
          " in [" + commandLine + "]");
    }

    private static List<string> CollectUnrecognizedCommandTokens(
        SensorySystem sensory,
        string commandLine,
        bool commandAuthoritative)
    {
      var unrecognized = new List<string>();
      foreach (string token in VelumOperatorStimulusCodec.EnumerateCommandTokens(commandLine))
      {
        List<int> ids;
        try
        {
          ids = sensory.CommandChannel.RecognizeText(token, commandAuthoritative);
        }
        catch
        {
          unrecognized.Add(token);
          continue;
        }

        if (ids == null || ids.Count == 0)
          unrecognized.Add(token);
      }

      return unrecognized;
    }
  }

  /// <summary>
  /// Маппинг типа активного документа SolidWorks в код зрительного канала образа восприятия ISIDA
  /// (<see cref="ISIDA.Reflexes.AgentVisualColor"/>). Позволяет различать контекст
  /// «деталь / сборка / чертёж» при формировании образов у-рефлексов, чтобы у-рефлекс,
  /// выученный на одном типе документа, не срабатывал на другом.
  /// </summary>
  internal static class VelumDocumentVisualColor
  {
    /// <summary>
    /// Резолвит код цвета для текущего активного документа SolidWorks.
    /// Требуется UI-поток панели (доступ к COM). При отсутствии активного документа,
    /// неизвестном типе или сбое — белый (контекст не ограничен).
    /// </summary>
    internal static int ResolveActiveDocumentColorId()
    {
      try
      {
        Xarial.XCad.SolidWorks.ISwApplication app =
            Velum.SolidHomeostasis.VelumSolidEnvironmentBridge.TryGetSolidWorksApplication();
        if (app == null)
          return ISIDA.Reflexes.AgentVisualColor.White;

        Velum.ReactiveCore.SolidWorksSessionSnapshot snapshot =
            new Velum.ReactiveCore.VelumSolidWorksSessionProbe().Capture(app);
        return MapKindToColorId(snapshot.DocumentKind);
      }
      catch
      {
        return ISIDA.Reflexes.AgentVisualColor.White;
      }
    }

    /// <summary>
    /// Маппит тип документа в код цвета по настройкам проекта.
    /// «Нет документа» и «другой тип» — белый (стимул не ограничивает контекст по типу
    /// документа). Некорректный код из настройки — белый.
    /// </summary>
    /// <param name="kind">Тип активного документа.</param>
    internal static int MapKindToColorId(Velum.ReactiveCore.VelumSolidDocumentKind kind)
    {
      int code;
      switch (kind)
      {
        case Velum.ReactiveCore.VelumSolidDocumentKind.Part:
          code = Velum.Configuration.VelumAppConfig.DocumentColorPart;
          break;
        case Velum.ReactiveCore.VelumSolidDocumentKind.Assembly:
          code = Velum.Configuration.VelumAppConfig.DocumentColorAssembly;
          break;
        case Velum.ReactiveCore.VelumSolidDocumentKind.Drawing:
          code = Velum.Configuration.VelumAppConfig.DocumentColorDrawing;
          break;
        default:
          return ISIDA.Reflexes.AgentVisualColor.White;
      }

      return ISIDA.Reflexes.AgentVisualColor.IsValidCode(code)
          ? code
          : ISIDA.Reflexes.AgentVisualColor.White;
    }
  }
}
