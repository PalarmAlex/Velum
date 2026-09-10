using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;
using ISIDA.Common;
using Velum.SolidHomeostasis;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Сообщение оператору после моторного (рефлекс / триггер) исполнения рецепта.
  /// Диалоги с собственным UI и «тихие» Save-шаги не уведомляют.
  /// Автозапись при Save чертежа (propagator) сюда не попадает — вызывается только из <see cref="RecipeDispatcher"/>.
  /// </summary>
  internal static class RecipeMotorOutcomeNotifier
  {
    private static readonly HashSet<string> InteractiveHandlers =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
          "export_documentation_dialog",
          "export_drawing_pdf_dialog",
          "product_registry_show_problems",
          "bom_exchange_show_dialog"
        };

    /// <summary>
    /// Под капотом (Save / гомеостаз) — без MessageBox даже при запуске по триггеру.
    /// MessageBox только у явных ремонтных действий вроде write_drawing_path.
    /// </summary>
    private static readonly HashSet<string> SilentHandlers =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
          "save_file_name",
          "set_custom_property",
          "set_custom_property_if_part",
          "set_custom_property_if_part_or_assembly",
          "log",
          "ensure_blank_size_property_links",
          "assign_session_material"
        };

    internal static void TryNotify(RecipeDefinition recipe, RecipeExecutionResult result)
    {
      if (recipe == null || result == null)
        return;

      if (!ShouldNotify(recipe))
        return;

      string title = string.IsNullOrWhiteSpace(recipe.DisplayName)
          ? "Velum"
          : recipe.DisplayName.Trim();

      string text;
      MessageBoxIcon icon;
      BuildMessage(recipe, result, out text, out icon);
      if (string.IsNullOrWhiteSpace(text))
        return;

      VelumSolidEnvironmentBridge.RunOnTaskPaneUiThread(() =>
      {
        try
        {
          MessageBox.Show(
              text,
              title,
              MessageBoxButtons.OK,
              icon);
        }
        catch (Exception ex)
        {
          Logger.Warning("Velum motor notify: " + ex.Message);
        }
      });
    }

    private static bool ShouldNotify(RecipeDefinition recipe)
    {
      IReadOnlyList<RecipeStepDefinition> steps = recipe.Steps;
      if (steps == null || steps.Count == 0)
        return false;

      bool hasNotifyingHandler = false;
      for (int i = 0; i < steps.Count; i++)
      {
        RecipeStepDefinition step = steps[i];
        if (step == null)
          continue;

        if (!string.Equals((step.Type ?? string.Empty).Trim(), "invoke", StringComparison.OrdinalIgnoreCase))
          continue;

        string handler = TryGetHandlerId(step);
        if (string.IsNullOrEmpty(handler))
          continue;

        if (InteractiveHandlers.Contains(handler))
          return false;

        if (SilentHandlers.Contains(handler))
          continue;

        hasNotifyingHandler = true;
      }

      return hasNotifyingHandler;
    }

    private static void BuildMessage(
        RecipeDefinition recipe,
        RecipeExecutionResult result,
        out string text,
        out MessageBoxIcon icon)
    {
      string primaryHandler = TryGetPrimaryNotifyHandler(recipe);
      if (string.Equals(primaryHandler, "write_drawing_path", StringComparison.OrdinalIgnoreCase))
      {
        BuildWriteDrawingPathMessage(result, out text, out icon);
        return;
      }

      if (string.Equals(primaryHandler, "delete_pdf_artifact", StringComparison.OrdinalIgnoreCase))
      {
        BuildDeletePdfMessage(result, out text, out icon);
        return;
      }

      BuildGenericMessage(recipe, result, out text, out icon);
    }

    private static void BuildWriteDrawingPathMessage(
        RecipeExecutionResult result,
        out string text,
        out MessageBoxIcon icon)
    {
      string stepMsg = TryGetLastStepMessage(result);

      if (!result.Success)
      {
        icon = MessageBoxIcon.Warning;
        if (ContainsToken(stepMsg, "sibling_drawing_not_found"))
        {
          text =
              "Не удалось записать путь чертежа.\n" +
              "Рядом с документом нет одноимённого .slddrw.\n" +
              "Откройте чертёж и сохраните его — путь пропишется автоматически.";
          return;
        }

        text = "Не удалось записать путь чертежа." + FormatDetailSuffix(stepMsg);
        return;
      }

      if (IsEffectivelySkipped(result))
      {
        icon = MessageBoxIcon.Information;
        text = "Путь чертежа уже актуален — изменений нет.";
        return;
      }

      icon = MessageBoxIcon.Information;
      int count = TryParseCountToken(stepMsg, "count=");
      if (count > 1)
      {
        text = "Путь чертежа записан в " +
            count.ToString(CultureInfo.InvariantCulture) + " документ(ов).";
      }
      else
        text = "Путь чертежа записан.";
    }

    private static void BuildDeletePdfMessage(
        RecipeExecutionResult result,
        out string text,
        out MessageBoxIcon icon)
    {
      string stepMsg = TryGetLastStepMessage(result);
      if (!result.Success)
      {
        icon = MessageBoxIcon.Warning;
        text = "Не удалось удалить PDF." + FormatDetailSuffix(stepMsg);
        return;
      }

      if (IsEffectivelySkipped(result) || ContainsToken(stepMsg, "nothing_to_delete") ||
          ContainsToken(stepMsg, "skipped_"))
      {
        icon = MessageBoxIcon.Information;
        text = "Удаление PDF не требуется.";
        return;
      }

      icon = MessageBoxIcon.Information;
      text = "Устаревший PDF удалён.";
    }

    private static void BuildGenericMessage(
        RecipeDefinition recipe,
        RecipeExecutionResult result,
        out string text,
        out MessageBoxIcon icon)
    {
      string name = string.IsNullOrWhiteSpace(recipe.DisplayName)
          ? (recipe.RecipeId ?? "действие")
          : recipe.DisplayName.Trim();
      string stepMsg = TryGetLastStepMessage(result);

      if (!result.Success)
      {
        icon = MessageBoxIcon.Warning;
        string detail = !string.IsNullOrWhiteSpace(result.ErrorMessage)
            ? result.ErrorMessage
            : stepMsg;
        text = name + ": не выполнено." + FormatDetailSuffix(detail);
        return;
      }

      if (IsEffectivelySkipped(result))
      {
        icon = MessageBoxIcon.Information;
        text = name + ": пропущено (уже актуально или не применимо).";
        return;
      }

      icon = MessageBoxIcon.Information;
      text = name + ": выполнено.";
    }

    private static bool IsEffectivelySkipped(RecipeExecutionResult result)
    {
      IReadOnlyList<RecipeStepExecutionResult> steps = result.Steps;
      if (steps == null || steps.Count == 0)
        return false;

      bool anyInvoke = false;
      for (int i = 0; i < steps.Count; i++)
      {
        RecipeStepExecutionResult step = steps[i];
        if (step == null)
          continue;
        if (!string.Equals(step.StepType, "invoke", StringComparison.OrdinalIgnoreCase))
          continue;
        anyInvoke = true;
        if (!step.Skipped)
          return false;
      }

      return anyInvoke;
    }

    private static string TryGetPrimaryNotifyHandler(RecipeDefinition recipe)
    {
      IReadOnlyList<RecipeStepDefinition> steps = recipe.Steps;
      if (steps == null)
        return string.Empty;

      for (int i = 0; i < steps.Count; i++)
      {
        RecipeStepDefinition step = steps[i];
        if (step == null)
          continue;
        if (!string.Equals((step.Type ?? string.Empty).Trim(), "invoke", StringComparison.OrdinalIgnoreCase))
          continue;

        string handler = TryGetHandlerId(step);
        if (string.IsNullOrEmpty(handler))
          continue;
        if (SilentHandlers.Contains(handler) || InteractiveHandlers.Contains(handler))
          continue;
        return handler;
      }

      return string.Empty;
    }

    private static string TryGetHandlerId(RecipeStepDefinition step)
    {
      string handler = null;
      if (step.Parameters != null)
        step.Parameters.TryGetValue("handler", out handler);
      return (handler ?? string.Empty).Trim();
    }

    private static string TryGetLastStepMessage(RecipeExecutionResult result)
    {
      IReadOnlyList<RecipeStepExecutionResult> steps = result.Steps;
      if (steps == null || steps.Count == 0)
        return result.ErrorMessage ?? string.Empty;

      for (int i = steps.Count - 1; i >= 0; i--)
      {
        RecipeStepExecutionResult step = steps[i];
        if (step == null)
          continue;
        if (!string.IsNullOrWhiteSpace(step.Message))
          return step.Message;
      }

      return result.ErrorMessage ?? string.Empty;
    }

    private static string FormatDetailSuffix(string detail)
    {
      detail = SanitizeDetail(detail);
      if (string.IsNullOrWhiteSpace(detail))
        return string.Empty;
      return "\n" + detail;
    }

    private static string SanitizeDetail(string detail)
    {
      if (string.IsNullOrWhiteSpace(detail))
        return string.Empty;

      detail = detail.Trim();
      // Убрать технический хвост doc=...
      int docIdx = detail.IndexOf(" doc=", StringComparison.OrdinalIgnoreCase);
      if (docIdx > 0)
        detail = detail.Substring(0, docIdx).Trim();

      if (detail.Length > 240)
        detail = detail.Substring(0, 237) + "...";

      return detail;
    }

    private static bool ContainsToken(string message, string token)
    {
      return !string.IsNullOrEmpty(message) &&
          message.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int TryParseCountToken(string message, string token)
    {
      if (string.IsNullOrEmpty(message) || string.IsNullOrEmpty(token))
        return -1;

      int idx = message.IndexOf(token, StringComparison.OrdinalIgnoreCase);
      if (idx < 0)
        return -1;

      int start = idx + token.Length;
      int end = start;
      while (end < message.Length && char.IsDigit(message[end]))
        end++;

      if (end <= start)
        return -1;

      int value;
      if (!int.TryParse(message.Substring(start, end - start), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        return -1;
      return value;
    }
  }
}
