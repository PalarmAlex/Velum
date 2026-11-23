using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Чистая (без SolidWorks / ISIDA / WinForms) подстановка плейсхолдеров <c>{V:имя}</c> —
  /// «переменных психики» рецепта.
  /// <para>
  /// Синтаксис <c>{V:имя}</c> отделён от окруженческих плейсхолдеров <c>{NAME}</c>
  /// (<see cref="VelumRecipeTemplateResolver"/>) именно потому, что значение берётся не из
  /// снимка сессии SolidWorks, а из мутабельного состояния симбионта (параметры гомеостаза,
  /// активные адаптивные действия). Психика меняет эти значения от пульса к пульсу — рецепт
  /// остаётся тем же, меняется лишь подставляемое число.
  /// </para>
  /// <para>
  /// Класс не знает, откуда берутся значения: словарь <c>variables</c> передаёт вызывающий код
  /// (<see cref="RecipeExecutor"/> собирает его из ISIDA). Благодаря этому логика тестируется
  /// изолированно, а источник значений подменяем.
  /// </para>
  /// </summary>
  public static class VelumRecipeVariableResolver
  {
    /// <summary>Префикс переменной психики в имени плейсхолдера: <c>{V:имя}</c>.</summary>
    public const string VariablePrefix = "V:";

    private static readonly Regex VariableRegex = new Regex(
        @"\{\s*V\s*:\s*([^{}]+?)\s*\}",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// Подставляет <c>{V:имя}</c> из словаря переменных. Неизвестное имя оставляется как есть
    /// (чтобы <see cref="VelumRecipeTemplateResolver.ContainsUnresolvedTemplateTokens"/> позже
    /// распознал неразрешённый шаблон и не записал мусор).
    /// </summary>
    /// <param name="input">Исходная строка (может содержать <c>{V:...}</c> и другие токены).</param>
    /// <param name="variables">Словарь переменных психики (регистронезависимые ключи без префикса).</param>
    /// <returns>Строка с подставленными известными переменными.</returns>
    public static string Resolve(string input, IReadOnlyDictionary<string, string> variables)
    {
      if (string.IsNullOrEmpty(input) || !ContainsVariableTokens(input))
        return input ?? string.Empty;

      return VariableRegex.Replace(
          input,
          m =>
          {
            string name = (m.Groups[1].Value ?? string.Empty).Trim();
            if (variables != null &&
                variables.TryGetValue(name, out string value) &&
                !string.IsNullOrWhiteSpace(value))
              return value;

            // Неизвестная переменная — оставляем токен целиком (не пустую строку).
            return m.Value;
          });
    }

    /// <summary>Есть ли в строке хотя бы один токен <c>{V:...}</c>.</summary>
    /// <param name="input">Проверяемая строка.</param>
    /// <returns><c>true</c>, если токен переменной найден.</returns>
    public static bool ContainsVariableTokens(string input)
    {
      if (string.IsNullOrEmpty(input))
        return false;

      return VariableRegex.IsMatch(input);
    }

    /// <summary>
    /// Собирает имена всех переменных <c>{V:имя}</c> из строки (для диагностики и валидации
    /// рецепта: какие переменные психики шагу реально нужны).
    /// </summary>
    /// <param name="input">Исходная строка шаблона.</param>
    /// <returns>Уникальные имена переменных без префикса (в порядке первого вхождения).</returns>
    public static IReadOnlyList<string> CollectVariableNames(string input)
    {
      var names = new List<string>();
      if (string.IsNullOrEmpty(input) || !ContainsVariableTokens(input))
        return names;

      var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (Match m in VariableRegex.Matches(input))
      {
        string name = (m.Groups[1].Value ?? string.Empty).Trim();
        if (name.Length > 0 && seen.Add(name))
          names.Add(name);
      }

      return names;
    }
  }
}
