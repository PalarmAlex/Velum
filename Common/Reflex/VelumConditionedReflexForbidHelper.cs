using System;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;
using ISIDA.Common;
using ISIDA.Reflexes;

namespace Velum.UI
{
  /// <summary>
  /// Снимок эпизода активации (У-рефлекс + путь сенсорного гейта CS₁→CS₂) при открытии формы
  /// и кнопка «Запретить»: при активации через гейт штрафует конкретную связь CS₁→CS₂,
  /// иначе — сбрасывает крепость у-рефлекса.
  /// </summary>
  internal static class VelumConditionedReflexForbidHelper
  {
    private static readonly object ReflexIdAccessSync = new object();

    private static bool _episodeAccessResolved;
    private static MethodInfo _captureEpisodeMethod;
    private static PropertyInfo _conditionedReflexIdProperty;

    private static bool _penalizeLinkResolved;
    private static MethodInfo _penalizeLinkMethod;
    private static MethodInfo _tryGetAssociabilityMethod;

    /// <summary>
    /// Снимок эпизода активации у-рефлекса: ID УР и (при активации через сенсорный гейт)
    /// пара CS₁→CS₂ выученной связи, дорогу по которой открыл гейт.
    /// </summary>
    internal struct VelumReflexEpisode
    {
      /// <summary>ID у-рефлекса, активировавшего моторное действие (0 — не у-рефлекс).</summary>
      public int ReflexId;

      /// <summary>CS₁ — пусковой стимул эпизода через гейт (0 — активация не через гейт).</summary>
      public int GateCs1;

      /// <summary>CS₂ — пусковой образ у-рефлекса (0 — активация не через гейт).</summary>
      public int GateCs2;

      /// <summary>Активация шла через сенсорный гейт — «Запретить» штрафует связь CS₁→CS₂.</summary>
      public bool IsGateActivation => VelumReflexForbidTargetRules.TargetsSensoryLink(GateCs1, GateCs2);
    }

    /// <summary>
    /// Снимает и обнуляет эпизод активации. Предпочитает атомарный
    /// <c>CaptureAndClearCurrentReflexEpisode</c> (УР + путь гейта);
    /// при старой ISIDA без него — fallback на свойство <c>CurrentConditionedReflexID</c>
    /// (путь гейта остаётся 0, поведение прежнее). Без жёсткой IL-ссылки на метод — иначе
    /// при старой ISIDA MissingMethodException возникает на JIT и не ловится try/catch.
    /// </summary>
    public static VelumReflexEpisode CaptureAndClearCurrentReflexEpisode()
    {
      try
      {
        MethodInfo capture = TryResolveCaptureEpisodeMethod();
        if (capture != null)
        {
          object raw = capture.Invoke(null, null);
          if (raw is int[] episode && episode.Length >= 3)
          {
            return new VelumReflexEpisode
            {
              ReflexId = episode[0] > 0 ? episode[0] : 0,
              GateCs1 = episode[1] > 0 ? episode[1] : 0,
              GateCs2 = episode[2] > 0 ? episode[2] : 0
            };
          }
        }

        // Старая ISIDA: только ID у-рефлекса, гейт неизвестен — штраф будет по УР.
        int reflexId = CaptureAndClearCurrentConditionedReflexIdFallback();
        return new VelumReflexEpisode { ReflexId = reflexId, GateCs1 = 0, GateCs2 = 0 };
      }
      catch
      {
        return default(VelumReflexEpisode);
      }
    }

    /// <summary>
    /// Читает <c>AppGlobalState.CurrentConditionedReflexID</c> (если есть в загруженной ISIDA)
    /// и сразу обнуляет глобальное значение. Fallback для сборок ISIDA без эпизода гейта.
    /// </summary>
    public static int CaptureAndClearCurrentConditionedReflexId()
    {
      return CaptureAndClearCurrentReflexEpisode().ReflexId;
    }

    /// <summary>Fallback-чтение только ID у-рефлекса через свойство (старая ISIDA).</summary>
    private static int CaptureAndClearCurrentConditionedReflexIdFallback()
    {
      try
      {
        PropertyInfo prop = TryResolveConditionedReflexIdProperty();
        if (prop == null || !prop.CanRead)
          return 0;

        object raw = prop.GetValue(null, null);
        int id = raw is int value ? value : 0;
        if (id > 0 && prop.CanWrite)
          prop.SetValue(null, 0, null);

        return id > 0 ? id : 0;
      }
      catch
      {
        return 0;
      }
    }

    /// <summary>Резолв статического метода <c>CaptureAndClearCurrentReflexEpisode</c> (кэш).</summary>
    private static MethodInfo TryResolveCaptureEpisodeMethod()
    {
      lock (ReflexIdAccessSync)
      {
        if (!_episodeAccessResolved)
        {
          _episodeAccessResolved = true;
          try
          {
            _captureEpisodeMethod = typeof(AppGlobalState).GetMethod(
                "CaptureAndClearCurrentReflexEpisode",
                BindingFlags.Public | BindingFlags.Static);
          }
          catch
          {
            _captureEpisodeMethod = null;
          }
        }

        return _captureEpisodeMethod;
      }
    }

    private static PropertyInfo TryResolveConditionedReflexIdProperty()
    {
      lock (ReflexIdAccessSync)
      {
        try
        {
          _conditionedReflexIdProperty = typeof(AppGlobalState).GetProperty(
              "CurrentConditionedReflexID",
              BindingFlags.Public | BindingFlags.Static);
        }
        catch
        {
          _conditionedReflexIdProperty = null;
        }

        return _conditionedReflexIdProperty;
      }
    }

    /// <summary>
    /// Резолв методов <c>SensoryAssociationSystem</c> через <see cref="Type.GetType(string)"/>
    /// без жёсткой IL-ссылки на тип: при старой/отсутствующей сборке тип не грузится,
    /// а штраф деградирует до сброса крепости У-рефлекса.
    /// </summary>
    private static void ResolvePenalizeLinkMethods()
    {
      if (_penalizeLinkResolved)
        return;

      _penalizeLinkResolved = true;
      try
      {
        Type sensory = Type.GetType("ISIDA.Reflexes.SensoryAssociationSystem, isida", throwOnError: false);
        if (sensory == null)
          return;

        _penalizeLinkMethod = sensory.GetMethod("PenalizeLinkByOperator",
            BindingFlags.Public | BindingFlags.Instance);
        _tryGetAssociabilityMethod = sensory.GetMethod("TryGetAssociability",
            BindingFlags.Public | BindingFlags.Instance);
      }
      catch
      {
        _penalizeLinkMethod = null;
        _tryGetAssociabilityMethod = null;
      }
    }

    /// <summary>Экземпляр SensoryAssociationSystem или null (старая ISIDA / не инициализирован).</summary>
    private static object TryGetSensoryInstance()
    {
      try
      {
        Type sensory = Type.GetType("ISIDA.Reflexes.SensoryAssociationSystem, isida", throwOnError: false);
        object isInitRaw = sensory?.GetProperty("IsInitialized",
            BindingFlags.Public | BindingFlags.Static)?.GetValue(null, null);
        if (!(isInitRaw is bool initialized) || !initialized)
          return null;

        PropertyInfo instance = sensory.GetProperty("Instance",
            BindingFlags.Public | BindingFlags.Static);
        return instance?.GetValue(null, null);
      }
      catch
      {
        return null;
      }
    }

    /// <summary>
    /// Текущая готовность (Associability) сенсорной связи из оперативной памяти.
    /// false — связь/метод недоступны (старая ISIDA, нет связи).
    /// </summary>
    private static bool TryGetSensoryLinkAssociability(int cs1, int cs2, out float associability)
    {
      associability = 0f;
      try
      {
        ResolvePenalizeLinkMethods();
        if (_tryGetAssociabilityMethod == null)
          return false;

        object sensory = TryGetSensoryInstance();
        if (sensory == null)
          return false;

        var args = new object[] { cs1, cs2, 0f };
        object ok = _tryGetAssociabilityMethod.Invoke(sensory, args);
        if (!(ok is bool success) || !success)
          return false;

        associability = args[2] is float value ? value : 0f;
        return true;
      }
      catch
      {
        return false;
      }
    }

    /// <summary>
    /// Штраф связи CS₁→CS₂ по операторскому запрету. Возвращает результат в виде
    /// (успех, сообщение) или null, если метод недоступен (старая ISIDA — нужен fallback).
    /// </summary>
    private static (bool Success, string Message)? TryPenalizeSensoryLink(int cs1, int cs2)
    {
      try
      {
        ResolvePenalizeLinkMethods();
        if (_penalizeLinkMethod == null)
          return null;

        object sensory = TryGetSensoryInstance();
        if (sensory == null)
          return null;

        object raw = _penalizeLinkMethod.Invoke(sensory, new object[] { cs1, cs2 });
        if (raw == null)
          return null;

        Type tupleType = raw.GetType();
        bool success = tupleType.GetProperty("Item1")?.GetValue(raw) is bool s && s;
        string message = tupleType.GetProperty("Item2")?.GetValue(raw) as string;
        return (success, message);
      }
      catch (TargetInvocationException ex) when (ex.InnerException != null)
      {
        return (false, "Ошибка при понижении готовности связи: " + ex.InnerException.Message);
      }
      catch
      {
        return null;
      }
    }

    /// <summary>
    /// Читает текущую крепость у-рефлекса из справочника в оперативной памяти
    /// (<see cref="ConditionedReflexesSystem"/>, загруженный ConditionedReflexes.dat).
    /// </summary>
    /// <param name="conditionedReflexId">ID условного рефлекса.</param>
    /// <param name="strength">Крепость связи C ∈ [0, 1].</param>
    /// <returns>true, если рефлекс найден и система инициализирована.</returns>
    public static bool TryGetConditionedReflexStrength(int conditionedReflexId, out float strength)
    {
      strength = 0f;
      if (conditionedReflexId <= 0)
        return false;

      try
      {
        if (!ConditionedReflexesSystem.IsInitialized)
          return false;

        ConditionedReflexesSystem.ConditionedReflex reflex =
            ConditionedReflexesSystem.Instance.GetConditionedReflexById(conditionedReflexId);
        if (reflex == null)
          return false;

        strength = reflex.AssociationStrength;
        return true;
      }
      catch
      {
        return false;
      }
    }

    /// <summary>
    /// Текст подсказки кнопки «Запрет»: назначение кнопки и текущий параметр цели штрафа —
    /// готовность сенсорной связи (гейт-активация) или крепость у-рефлекса (обычная активация).
    /// </summary>
    private static string BuildForbidTooltipText(VelumReflexEpisode episode)
    {
      if (episode.IsGateActivation)
      {
        string assocText = TryGetSensoryLinkAssociability(episode.GateCs1, episode.GateCs2, out float assoc)
            ? assoc.ToString("0.###", CultureInfo.InvariantCulture)
            : VelumReflexForbidTargetRules.StrengthUnavailableText;

        return VelumReflexForbidTargetRules.ForbidLinkTooltip + Environment.NewLine +
               VelumReflexForbidTargetRules.LinkAssociabilityTooltipPrefix + assocText;
      }

      string strengthText = TryGetConditionedReflexStrength(episode.ReflexId, out float strength)
          ? strength.ToString("0.###", CultureInfo.InvariantCulture)
          : VelumReflexForbidTargetRules.StrengthUnavailableText;

      return VelumReflexForbidTargetRules.ForbidReflexTooltip + Environment.NewLine +
             VelumReflexForbidTargetRules.StrengthTooltipPrefix + strengthText;
    }

    /// <summary>Настраивает кнопку «Запрет»: активна только при снимке эпизода активации.</summary>
    /// <param name="button">Кнопка «Запрет» формы.</param>
    /// <param name="episode">Снимок эпизода активации (УР + путь гейта).</param>
    /// <param name="owner">Владелец диалогов подтверждения.</param>
    public static void BindForbidButton(Button button, VelumReflexEpisode episode, IWin32Window owner)
    {
      if (button == null)
        return;

      var tip = new ToolTip();
      tip.SetToolTip(button, BuildForbidTooltipText(episode));

      bool enabled = episode.ReflexId > 0 || episode.IsGateActivation;
      button.Enabled = enabled;
      button.Visible = true;
      if (!enabled)
        return;

      // Крепость/готовость точится на пульсах ISIDA — перечитываем при наведении курсора.
      button.MouseEnter += (s, e) => tip.SetToolTip(button, BuildForbidTooltipText(episode));
      button.Click += (s, e) => OnForbidClick(owner, button, tip, episode);
    }

    /// <summary>
    /// Клик «Запретить»: гейт-активация — штраф связи CS₁→CS₂ (УР не трогается);
    /// иначе — прежний сброс крепости у-рефлекса.
    /// </summary>
    private static void OnForbidClick(IWin32Window owner, Button button, ToolTip tip, VelumReflexEpisode episode)
    {
      bool punishLink = episode.IsGateActivation;

      string confirmText = punishLink
          ? VelumReflexForbidTargetRules.BuildLinkConfirmText(episode.GateCs1, episode.GateCs2)
          : VelumReflexForbidTargetRules.BuildReflexConfirmText(episode.ReflexId);

      DialogResult confirm = MessageBox.Show(
          owner,
          confirmText,
          "Запрет",
          MessageBoxButtons.YesNo,
          MessageBoxIcon.Question,
          MessageBoxDefaultButton.Button2);

      if (confirm != DialogResult.Yes)
        return;

      if (punishLink)
      {
        OnForbidLinkClick(owner, button, tip, episode);
        return;
      }

      if (!ConditionedReflexesSystem.IsInitialized)
      {
        MessageBox.Show(
            owner,
            "Система условных рефлексов не инициализирована.",
            "Запрет",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return;
      }

      try
      {
        var result = ConditionedReflexesSystem.Instance.ResetAssociationStrengthToInitial(episode.ReflexId);
        ShowResult(owner, button, tip, episode, result.Success,
            result.Message ?? (result.Success ? "Готово." : "Не удалось понизить крепость."));
      }
      catch (Exception ex)
      {
        MessageBox.Show(
            owner,
            "Ошибка при понижении крепости: " + ex.Message,
            "Запрет",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
      }
    }

    /// <summary>
    /// Штраф сенсорной связи при активации через гейт. Если метод недоступен (старая ISIDA) —
    /// откат на сброс крепости у-рефлекса, чтобы кнопка не осталась пустой.
    /// </summary>
    private static void OnForbidLinkClick(IWin32Window owner, Button button, ToolTip tip, VelumReflexEpisode episode)
    {
      (bool Success, string Message)? linkResult =
          TryPenalizeSensoryLink(episode.GateCs1, episode.GateCs2);

      if (linkResult == null)
      {
        // Старая ISIDA без адресного штрафа: наказываем УР целиком (прежнее поведение).
        if (episode.ReflexId > 0 && ConditionedReflexesSystem.IsInitialized)
        {
          try
          {
            var fallback = ConditionedReflexesSystem.Instance
                .ResetAssociationStrengthToInitial(episode.ReflexId);
            ShowResult(owner, button, tip, episode, fallback.Success,
                (fallback.Message ?? "Готово.") + " (адресный штраф связи недоступен)");
          }
          catch (Exception ex)
          {
            MessageBox.Show(
                owner,
                "Ошибка при понижении крепости: " + ex.Message,
                "Запрет",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
          }
        }
        else
        {
          MessageBox.Show(
              owner,
              "Система сенсорных ассоциаций недоступна — штраф применить не удалось.",
              "Запрет",
              MessageBoxButtons.OK,
              MessageBoxIcon.Warning);
        }
        return;
      }

      ShowResult(owner, button, tip, episode, linkResult.Value.Success,
          linkResult.Value.Message ?? (linkResult.Value.Success ? "Готово." : "Не удалось понизить готовность связи."));
    }

    /// <summary>Показывает итог штрафа и обновляет подсказку (параметр цели изменился).</summary>
    private static void ShowResult(
        IWin32Window owner,
        Button button,
        ToolTip tip,
        VelumReflexEpisode episode,
        bool success,
        string message)
    {
      MessageBox.Show(
          owner,
          message,
          "Запрет",
          MessageBoxButtons.OK,
          success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

      if (success && button != null)
        button.Enabled = false;

      // Параметр цели изменился — подсказка должна показывать актуальное значение.
      if (tip != null && button != null)
        tip.SetToolTip(button, BuildForbidTooltipText(episode));
    }
  }
}
