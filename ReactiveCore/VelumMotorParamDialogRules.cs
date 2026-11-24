using System;
using System.Collections.Generic;
using System.Globalization;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Чистые (без WinForms / COM / isida.dll) правила диалога подтверждения параметров
  /// мотор-действия: когда показывать окно и как разбирать/валидировать введённые значения.
  /// <para>
  /// Семантика флага <c>confirm</c> на шаге <c>invoke</c>: симбионт, спроектировавший цепочку
  /// моторов и записавший под них параметры, выполняет её автономно и не должен спрашивать
  /// подтверждение на каждом шаге. Поэтому окно показывается только при ручном запуске
  /// (безусловный рефлекс / прямой вызов) и только когда режим не <see cref="ConfirmMode.Never"/>.
  /// </para>
  /// </summary>
  public static class VelumMotorParamDialogRules
  {
    /// <summary>Режим подтверждения параметров шага (значение флага <c>confirm</c>).</summary>
    public enum ConfirmMode
    {
      /// <summary>Никогда не спрашивать: параметры берутся как заданы в рецепте/переменных.</summary>
      Never = 0,

      /// <summary>Спрашивать при ручном запуске (автономный прогон психики — не спрашивает).</summary>
      Auto = 1,

      /// <summary>То же, что <see cref="Auto"/>; отдельное значение оставлено под будущие ручные источники.</summary>
      Always = 2
    }

    /// <summary>Числовой код источника активации действия (совпадает с <c>ActionActivationSource</c>).</summary>
    public static class ActivationSourceCode
    {
      /// <summary>Безусловный рефлекс (в т.ч. ручной запуск по умолчанию).</summary>
      public const int GeneticReflex = 1;

      /// <summary>Условный рефлекс.</summary>
      public const int ConditionedReflex = 2;

      /// <summary>Автоматизм.</summary>
      public const int Automatizm = 3;

      /// <summary>Вербальный ответ автоматизма.</summary>
      public const int AutomatizmVerbalResponse = 4;

      /// <summary>Эхо-комментарий (стадия 3).</summary>
      public const int AutomatizmEchoCommentary = 5;
    }

    /// <summary>
    /// Разбирает значение флага <c>confirm</c> из строки шага. Пусто/неизвестно → <see cref="ConfirmMode.Never"/>
    /// (обратная совместимость: шаги без флага не показывают окно).
    /// </summary>
    /// <param name="raw">Сырое значение (например <c>"auto"</c>, <c>"true"</c>, <c>"never"</c>).</param>
    /// <returns>Распознанный режим.</returns>
    public static ConfirmMode ParseConfirmMode(string raw)
    {
      string v = (raw ?? string.Empty).Trim().ToLowerInvariant();
      switch (v)
      {
        case "always":
        case "да":
        case "true":
        case "1":
        case "yes":
          return ConfirmMode.Always;
        case "auto":
          return ConfirmMode.Auto;
        default:
          return ConfirmMode.Never;
      }
    }

    /// <summary>
    /// Ручной ли источник запуска (оператор просил действие). Автоматизмы, вербальные ответы,
    /// эхо-комментарии и условные рефлексы — не ручные (среда/психика), подтверждение не показывают.
    /// </summary>
    /// <param name="activationSource">Числовой код <c>ActionActivationSource</c>.</param>
    /// <returns><c>true</c> для безусловного рефлекса / ручного вызова (код 0 или 1).</returns>
    public static bool IsManualSource(int activationSource)
    {
      return activationSource == 0 || activationSource == ActivationSourceCode.GeneticReflex;
    }

    /// <summary>
    /// Показывать ли диалог подтверждения параметров: только при ручном запуске и режиме не <see cref="ConfirmMode.Never"/>.
    /// </summary>
    /// <param name="mode">Режим из <see cref="ParseConfirmMode"/>.</param>
    /// <param name="activationSource">Числовой код источника активации.</param>
    /// <returns><c>true</c> — показать окно и дать оператору править параметры.</returns>
    public static bool ShouldAskDialog(ConfirmMode mode, int activationSource)
    {
      if (mode == ConfirmMode.Never)
        return false;

      return IsManualSource(activationSource);
    }

    /// <summary>
    /// Валидирует и нормализует введённое значение по типу параметра (<c>text</c>/<c>number</c>/
    /// <c>int</c>/<c>bool</c>/<c>enum</c>/<c>date</c>). Пустое значение для необязательного параметра —
    /// допустимо и означает «не задан» (возвращается пустая строка).
    /// </summary>
    /// <param name="type">Тип из argsSchema (регистронезависимо; пусто = <c>text</c>).</param>
    /// <param name="raw">Сырое введённое значение.</param>
    /// <param name="required">Обязателен ли параметр (пусто запрещено).</param>
    /// <param name="normalized">Нормализованное значение (инвариантная культура для чисел).</param>
    /// <param name="error">Текст ошибки, если значение некорректно.</param>
    /// <returns><c>true</c>, если значение корректно.</returns>
    public static bool TryValidateValue(
        string type,
        string raw,
        bool required,
        out string normalized,
        out string error)
    {
      normalized = (raw ?? string.Empty).Trim();
      error = string.Empty;
      string t = (type ?? string.Empty).Trim().ToLowerInvariant();

      if (normalized.Length == 0)
      {
        if (required)
        {
          error = "значение обязательно";
          return false;
        }

        return true;
      }

      switch (t)
      {
        case "number":
          if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
          {
            normalized = d.ToString("0.####", CultureInfo.InvariantCulture);
            return true;
          }

          error = "ожидается число";
          return false;

        case "int":
          if (long.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
          {
            normalized = l.ToString(CultureInfo.InvariantCulture);
            return true;
          }

          error = "ожидается целое число";
          return false;

        case "bool":
          string b = normalized.ToLowerInvariant();
          if (b == "true" || b == "false" || b == "да" || b == "нет" || b == "1" || b == "0")
            return true;

          error = "ожидается да/нет";
          return false;

        default:
          // text / enum / date и неизвестные типы принимаются как есть (enum ограничен списком формы).
          return true;
      }
    }

    /// <summary>
    /// Собирает финальные аргументы шага: базовые параметры, перекрытые значениями из диалога.
    /// Пустое значение из диалога для необязательного параметра удаляет ключ («не задан»).
    /// </summary>
    /// <param name="baseArgs">Исходные параметры шага.</param>
    /// <param name="dialogValues">Значения, введённые оператором (ключи как в args).</param>
    /// <returns>Слитый словарь (регистронезависимые ключи).</returns>
    public static Dictionary<string, string> MergeOverrides(
        IReadOnlyDictionary<string, string> baseArgs,
        IReadOnlyDictionary<string, string> dialogValues)
    {
      var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      if (baseArgs != null)
      {
        foreach (KeyValuePair<string, string> kv in baseArgs)
          merged[kv.Key] = kv.Value;
      }

      if (dialogValues != null)
      {
        foreach (KeyValuePair<string, string> kv in dialogValues)
        {
          if (string.IsNullOrEmpty(kv.Value))
            merged.Remove(kv.Key);
          else
            merged[kv.Key] = kv.Value;
        }
      }

      return merged;
    }
  }

  /// <summary>
  /// Описание одного редактируемого поля диалога параметров мотор-действия.
  /// Строится из фактических аргументов шага рецепта (рендерятся только реально
  /// присутствующие параметры — как стартовые значения).
  /// </summary>
  public sealed class VelumMotorParamField
  {
    /// <summary>Создаёт описание поля.</summary>
    /// <param name="key">Ключ аргумента шага (например <c>width</c>).</param>
    /// <param name="label">Отображаемая метка.</param>
    /// <param name="type">Тип значения (<c>text</c>/<c>number</c>/<c>int</c>/<c>bool</c>/<c>enum</c>).</param>
    /// <param name="initialValue">Стартовое значение из рецепта/переменных.</param>
    /// <param name="required">Обязателен ли параметр.</param>
    /// <param name="enumValues">Допустимые значения для <c>enum</c> (иначе пусто).</param>
    public VelumMotorParamField(
        string key,
        string label,
        string type,
        string initialValue,
        bool required,
        IReadOnlyList<string> enumValues)
    {
      Key = key ?? string.Empty;
      Label = string.IsNullOrWhiteSpace(label) ? key ?? string.Empty : label;
      Type = string.IsNullOrWhiteSpace(type) ? "text" : type;
      InitialValue = initialValue ?? string.Empty;
      Required = required;
      EnumValues = enumValues ?? new string[0];
    }

    /// <summary>Ключ аргумента шага.</summary>
    public string Key { get; }

    /// <summary>Отображаемая метка.</summary>
    public string Label { get; }

    /// <summary>Тип значения.</summary>
    public string Type { get; }

    /// <summary>Стартовое значение.</summary>
    public string InitialValue { get; }

    /// <summary>Обязателен ли параметр.</summary>
    public bool Required { get; }

    /// <summary>Допустимые значения для <c>enum</c>.</summary>
    public IReadOnlyList<string> EnumValues { get; }
  }
}
