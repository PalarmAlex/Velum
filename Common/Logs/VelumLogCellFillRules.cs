using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Velum.UI.Logs
{
  /// <summary>
  /// Код фона ячейки отчёта логов. Значения сопоставлены CSS-классам в
  /// <see cref="VelumLogsReportHtmlBuilder"/> (светлая тема отчёта).
  /// </summary>
  internal enum VelumLogCellFill
  {
    /// <summary>Фон не задаётся (стандартная полосатая заливка таблицы).</summary>
    None = 0,

    /// <summary>Красный фон (плохо / опасность / положительное давление среды).</summary>
    Red = 1,

    /// <summary>Жёлтый фон (норма).</summary>
    Yellow = 2,

    /// <summary>Зелёный фон (хорошо / отрицательное давление среды).</summary>
    Green = 3
  }

  /// <summary>
  /// Чистые правила фоновой подсветки ячеек HTML-отчётов по логам агента.
  /// Переносит цветовую семантику живых макетов AIStudio
  /// (<c>LiveLogsView</c>, <c>ParameterLogsView</c>) в генератор отчётов Velum.
  /// Класс без внешних зависимостей (SolidWorks / WinForms / isida.dll) — линкуется в тесты.
  /// </summary>
  internal static class VelumLogCellFillRules
  {
    // Сегмент ячейки «Среда»: «id:величина» (величина может быть со знаком).
    private static readonly Regex EnvironmentSegmentRegex =
        new Regex(@"^(\d+)\s*:\s*([+\-]?\d+)$", RegexOptions.Compiled);

    /// <summary>
    /// Фон колонки «Состояние» системного лога по коду состояния гомеостаза:
    /// -1 → красный, 0 → жёлтый, 1 → зелёный (иначе — без фона).
    /// </summary>
    public static VelumLogCellFill StateFill(int? baseId)
    {
      switch (baseId)
      {
        case -1:
          return VelumLogCellFill.Red;
        case 0:
          return VelumLogCellFill.Yellow;
        case 1:
          return VelumLogCellFill.Green;
        default:
          return VelumLogCellFill.None;
      }
    }

    /// <summary>
    /// Фон колонки «Среда» по ячейке «id:величина» (сегменты через запятую).
    /// По ТЗ: положительное давление → красный, отрицательное → зелёный.
    /// Приоритет положительному сегменту (как расчёт флага в AIStudio);
    /// если знак не определён — фон не задаётся.
    /// </summary>
    public static VelumLogCellFill EnvironmentFill(string environmentCell)
    {
      if (string.IsNullOrWhiteSpace(environmentCell) || environmentCell.Trim() == "-")
        return VelumLogCellFill.None;

      bool hasPositive = false;
      bool hasNegative = false;
      foreach (string part in environmentCell.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
      {
        Match m = EnvironmentSegmentRegex.Match(part.Trim());
        if (!m.Success)
          continue;
        if (!int.TryParse(m.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int magnitude))
          continue;
        if (magnitude > 0)
          hasPositive = true;
        else if (magnitude < 0)
          hasNegative = true;
      }

      if (hasPositive)
        return VelumLogCellFill.Red;
      if (hasNegative)
        return VelumLogCellFill.Green;
      return VelumLogCellFill.None;
    }

    /// <summary>
    /// Фон колонки «Опасно»: при опасной ситуации — красный, иначе без фона
    /// (в живом макете AIStudio фон задаётся только при «опасно»).
    /// </summary>
    public static VelumLogCellFill DangerFill(bool danger)
    {
      return danger ? VelumLogCellFill.Red : VelumLogCellFill.None;
    }

    /// <summary>
    /// Фон колонки «ОР/УМ» (по аналогии с <c>OrUmCellStyle</c> AIStudio):
    /// ОР/ОР1 → красный, ОР2 → жёлтый;
    /// УМ1/УМ2 — зелёный при успехе, красный при неудаче (при неизвестном результате — без фона).
    /// </summary>
    public static VelumLogCellFill OrUmFill(string displayOrUm, bool? thinkingLevelSuccess)
    {
      if (string.IsNullOrWhiteSpace(displayOrUm))
        return VelumLogCellFill.None;

      string value = displayOrUm.Trim();
      switch (value)
      {
        case "ОР":
        case "ОР1":
          return VelumLogCellFill.Red;
        case "ОР2":
          return VelumLogCellFill.Yellow;
        case "УМ1":
        case "УМ2":
          if (!thinkingLevelSuccess.HasValue)
            return VelumLogCellFill.None;
          return thinkingLevelSuccess.Value ? VelumLogCellFill.Green : VelumLogCellFill.Red;
        default:
          return VelumLogCellFill.None;
      }
    }

    /// <summary>
    /// Фон колонки «Состояние» лога параметров по коду состояния (как <c>ParameterCellStyle</c>):
    /// -1 → красный, 1 → зелёный, 0 (норма) — без фона.
    /// </summary>
    public static VelumLogCellFill ParameterStateFill(int stateCode)
    {
      switch (stateCode)
      {
        case -1:
          return VelumLogCellFill.Red;
        case 1:
          return VelumLogCellFill.Green;
        default:
          return VelumLogCellFill.None;
      }
    }
  }
}
