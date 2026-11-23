using System;
using System.Globalization;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Чистая (без SolidWorks / ISIDA / WinForms) логика разбора числовых параметров геометрии
  /// рецепта: проверка, что разрешённый шаблон стал числом, и перевод миллиметров в метры
  /// для COM SolidWorks.
  /// <para>
  /// Резолвинг самого шаблона (окружение <c>{NAME}</c> + переменные психики <c>{V:имя}</c>)
  /// выполняет вызывающий handler через <c>VelumRecipeTemplateResolver</c>; сюда приходит уже
  /// подставленная строка. Благодаря этому класс не зависит от SolidWorks и тестируется
  /// изолированно (слинкован в тестовый проект).
  /// </para>
  /// </summary>
  public static class VelumRecipeGeometryRules
  {
    /// <summary>Коэффициент перевода миллиметров в метры (единицы COM SolidWorks — метры).</summary>
    public const double MillimetersToMeters = 0.001;

    /// <summary>
    /// Разбирает уже разрешённое значение линейного размера: проверяет отсутствие неразрешённых
    /// токенов шаблона, парсит положительное число миллиметров и возвращает метры.
    /// </summary>
    /// <param name="resolvedValue">Строка после подстановки шаблона (например <c>"100"</c>).</param>
    /// <param name="meters">Разрешённое значение в метрах при успехе; иначе 0.</param>
    /// <param name="error">Код ошибки при неудаче (пусто при успехе).</param>
    /// <returns><c>true</c>, если значение разрешено и является положительным числом.</returns>
    public static bool TryResolvedToMeters(
        string resolvedValue,
        out double meters,
        out string error)
    {
      meters = 0;
      error = string.Empty;

      if (string.IsNullOrWhiteSpace(resolvedValue))
      {
        error = "missing_value";
        return false;
      }

      string trimmed = resolvedValue.Trim();

      // Остатки неразрешённого шаблона ({...} или $PRP:"...") — не число, шаг не выполнять.
      if (ContainsUnresolvedToken(trimmed))
      {
        error = "unresolved_token:" + trimmed;
        return false;
      }

      if (!TryParseMeters(trimmed, out double millimeters, out error))
        return false;

      if (millimeters <= 0)
      {
        error = "non_positive:" + trimmed;
        return false;
      }

      meters = millimeters * MillimetersToMeters;
      return true;
    }

    /// <summary>
    /// Разбирает строку миллиметров в <see cref="double"/>. Принимает точку и запятую как
    /// десятичный разделитель (инвариантно к культуре шаблона).
    /// </summary>
    /// <param name="raw">Числовая строка в мм (например <c>"120"</c> или <c>"12.5"</c>).</param>
    /// <param name="millimeters">Разобранное значение в мм.</param>
    /// <param name="error">Код ошибки при неудаче.</param>
    /// <returns><c>true</c>, если строка — корректное конечное число.</returns>
    public static bool TryParseMeters(string raw, out double millimeters, out string error)
    {
      millimeters = 0;
      error = string.Empty;

      string trimmed = (raw ?? string.Empty).Trim();
      if (trimmed.Length == 0)
      {
        error = "empty_number";
        return false;
      }

      // Запятая как десятичный разделитель → точка (шаблоны пишут в разных локалях).
      string normalized = trimmed.Replace(',', '.');

      if (!double.TryParse(
              normalized,
              NumberStyles.Float,
              CultureInfo.InvariantCulture,
              out double parsed) ||
          double.IsNaN(parsed) ||
          double.IsInfinity(parsed))
      {
        error = "invalid_number:" + trimmed;
        return false;
      }

      millimeters = parsed;
      return true;
    }

    /// <summary>
    /// Есть ли в строке неразрешённые токены шаблона (<c>{...}</c> или <c>$PRP:</c>).
    /// Локальная копия признака из <c>VelumRecipeTemplateResolver</c>, чтобы класс оставался
    /// независимым от SolidWorks.
    /// </summary>
    /// <param name="value">Проверяемая строка.</param>
    /// <returns><c>true</c>, если токен шаблона остался неразрешённым.</returns>
    public static bool ContainsUnresolvedToken(string value)
    {
      if (string.IsNullOrWhiteSpace(value))
        return false;

      return value.IndexOf("$PRP:", StringComparison.OrdinalIgnoreCase) >= 0 ||
             value.IndexOf('{') >= 0;
    }
  }
}
