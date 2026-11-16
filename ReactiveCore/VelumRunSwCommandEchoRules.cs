using System.Globalization;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Чистая (без SolidWorks / ISIDA / WinForms) логика защиты шага <c>run_sw_command</c>
  /// от задвоения команды, уже выполненной SolidWorks по нажатию оператора.
  /// <para>
  /// Когда триггер рефлекса/автоматизма пришёл из командного канала
  /// (<c>AdaptiveAction.CommandPatternId &gt; 0</c>), SolidWorks сам выполнил команду с
  /// соответствующим sw-кодом. Если рецепт содержит шаг <c>run_sw_command</c> с тем же
  /// числовым кодом, шаг дублирует действие оператора и должен быть пропущен.
  /// </para>
  /// </summary>
  internal static class VelumRunSwCommandEchoRules
  {
    /// <summary>Префикс токена команды SolidWorks в буфере / Command-канале.</summary>
    public const string SwTokenPrefix = "sw:";

    /// <summary>
    /// Разбирает человекочитаемый токен командного канала (например, <c>"sw:21"</c>)
    /// в числовой код команды SolidWorks.
    /// </summary>
    /// <param name="token">Текст паттерна команды из <c>CommandChannel.GetPhraseFromPhraseId</c>.</param>
    /// <param name="commandId">Разобранный положительный код команды; 0 при неудаче.</param>
    /// <returns><c>true</c>, если токен имеет формат <c>sw:N</c> с положительным N.</returns>
    public static bool TryParseSwCode(string token, out int commandId)
    {
      commandId = 0;
      if (string.IsNullOrWhiteSpace(token))
        return false;

      string trimmed = token.Trim();
      if (trimmed.Length <= SwTokenPrefix.Length)
        return false;

      if (!trimmed.StartsWith(SwTokenPrefix, System.StringComparison.OrdinalIgnoreCase))
        return false;

      string numberPart = trimmed.Substring(SwTokenPrefix.Length).Trim();
      if (!int.TryParse(numberPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
        return false;

      if (parsed <= 0)
        return false;

      commandId = parsed;
      return true;
    }

    /// <summary>
    /// Определяет, нужно ли пропустить шаг <c>run_sw_command</c> как дубликат команды оператора.
    /// </summary>
    /// <param name="operatorCommandId">Код команды, уже выполненной SolidWorks (0 — нет командного триггера).</param>
    /// <param name="recipeCommandId">Код команды из параметра шага рецепта.</param>
    /// <returns><c>true</c>, если коды совпадают и шаг дублирует действие оператора.</returns>
    public static bool ShouldSuppressAsEcho(int operatorCommandId, int recipeCommandId)
    {
      return operatorCommandId > 0 && recipeCommandId > 0 && operatorCommandId == recipeCommandId;
    }
  }
}
