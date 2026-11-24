using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using ISIDA.Common;
using Velum.UI;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Модальный показ общей формы параметров мотор-действия и защита от повторного открытия.
  /// Используется из <see cref="RecipeStepHandlerRegistry"/> на UI-потоке SolidWorks перед
  /// исполнением шага <c>invoke</c> с флагом <c>confirm</c>.
  /// </summary>
  internal static class VelumMotorParamsDialogHost
  {
    private static int _dialogOpen;

    /// <summary>
    /// Открывает модальную форму параметров и возвращает подтверждение оператора со слитыми аргументами.
    /// </summary>
    /// <param name="title">Заголовок окна (имя рецепта/действия).</param>
    /// <param name="fields">Поля для отображения (фактические параметры шага).</param>
    /// <param name="baseArgs">Исходные параметры шага (для слияния значений из формы).</param>
    /// <param name="mergedArgs">
    /// Финальные аргументы: базовые, перекрытые введёнными значениями. При отказе — <c>null</c>.
    /// </param>
    /// <param name="confirmed">
    /// Подтвердил ли оператор параметры (форма закрыта с <c>DialogResult.OK</c>).
    /// <c>false</c> — отмена, закрытие окна либо диалог уже открыт (повторный показ).
    /// </param>
    /// <returns>Всегда <c>true</c>: шаг обработан (подтверждён или осознанно отменён оператором).</returns>
    internal static bool TryShow(
        string title,
        IReadOnlyList<VelumMotorParamField> fields,
        IReadOnlyDictionary<string, string> baseArgs,
        out IReadOnlyDictionary<string, string> mergedArgs,
        out bool confirmed)
    {
      mergedArgs = null;
      confirmed = false;

      if (Interlocked.CompareExchange(ref _dialogOpen, 1, 0) != 0)
      {
        Logger.Info("Velum motor params dialog skipped (already open)");
        return true;
      }

      try
      {
        if (fields == null || fields.Count == 0)
        {
          // Нет редактируемых параметров — не показываем пустое окно, действие подтверждено по умолчанию.
          confirmed = true;
          mergedArgs = baseArgs;
          return true;
        }

        using (var dialog = new VelumMotorParamsDialog(title, fields))
        {
          DialogResult dr = dialog.ShowDialog();
          confirmed = dr == DialogResult.OK;
          if (confirmed)
          {
            mergedArgs = VelumMotorParamDialogRules.MergeOverrides(baseArgs, dialog.Values);
          }
        }

        return true;
      }
      finally
      {
        Interlocked.Exchange(ref _dialogOpen, 0);
      }
    }
  }
}
