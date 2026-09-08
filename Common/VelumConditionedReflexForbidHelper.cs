using System;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;
using ISIDA.Common;
using ISIDA.Reflexes;

namespace Velum.UI
{
  /// <summary>
  /// Снимок ID у-рефлекса при открытии формы и кнопка «Запрет» (сброс крепости).
  /// </summary>
  internal static class VelumConditionedReflexForbidHelper
  {
    private const string ForbidTooltip =
        "Понижение крепости условного рефлекса, вызвавшего эту форму.";

    private static readonly object ReflexIdAccessSync = new object();
    private static bool _reflexIdAccessResolved;
    private static PropertyInfo _conditionedReflexIdProperty;

    /// <summary>
    /// Читает <c>AppGlobalState.CurrentConditionedReflexID</c> (если есть в загруженной ISIDA)
    /// и сразу обнуляет глобальное значение. Без жёсткой IL-ссылки — иначе при старой ISIDA
    /// MissingMethodException возникает на JIT и не ловится внутренним try/catch.
    /// </summary>
    public static int CaptureAndClearCurrentConditionedReflexId()
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

    private static PropertyInfo TryResolveConditionedReflexIdProperty()
    {
      lock (ReflexIdAccessSync)
      {
        if (_reflexIdAccessResolved)
          return _conditionedReflexIdProperty;

        _reflexIdAccessResolved = true;
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

    /// <summary>Настраивает кнопку «Запрет»: активна только при ID у-рефлекса &gt; 0.</summary>
    public static void BindForbidButton(Button button, int conditionedReflexId, IWin32Window owner)
    {
      if (button == null)
        return;

      var tip = new ToolTip();
      tip.SetToolTip(button, ForbidTooltip);

      bool enabled = conditionedReflexId > 0;
      button.Enabled = enabled;
      button.Visible = true;
      if (!enabled)
        return;

      int reflexId = conditionedReflexId;
      button.Click += (s, e) => OnForbidClick(owner, button, reflexId);
    }

    private static void OnForbidClick(IWin32Window owner, Button button, int reflexId)
    {
      DialogResult confirm = MessageBox.Show(
          owner,
          "Вы уверены, что хотите понизить крепость условного рефлекса ID=" +
          reflexId.ToString(CultureInfo.InvariantCulture) + "?",
          "Запрет",
          MessageBoxButtons.YesNo,
          MessageBoxIcon.Question,
          MessageBoxDefaultButton.Button2);

      if (confirm != DialogResult.Yes)
        return;

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
        var result = ConditionedReflexesSystem.Instance.ResetAssociationStrengthToInitial(reflexId);
        MessageBox.Show(
            owner,
            result.Message ?? (result.Success ? "Готово." : "Не удалось понизить крепость."),
            "Запрет",
            MessageBoxButtons.OK,
            result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        if (result.Success && button != null)
          button.Enabled = false;
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
  }
}
