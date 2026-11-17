using System;
using System.Collections.Generic;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Отображаемые в UI названия типов пользовательских свойств SolidWorks и их сопоставление
  /// с внутренними ключами записи (<c>text</c> / <c>date</c> / <c>number</c> / <c>boolean</c>).
  /// Чистый класс без зависимостей от COM/WinForms — линкуется в тестовый проект.
  /// </summary>
  internal static class VelumDocumentPropertyTypeDisplayRules
  {
    /// <summary>Внутренний ключ текстового свойства (совпадает с <c>VelumSolidCustomPropertyTypes.TypeKeyText</c>).</summary>
    internal const string KeyText = "text";

    /// <summary>Внутренний ключ даты (совпадает с <c>VelumSolidCustomPropertyTypes.TypeKeyDate</c>).</summary>
    internal const string KeyDate = "date";

    /// <summary>Внутренний ключ числа (совпадает с <c>VelumSolidCustomPropertyTypes.TypeKeyNumber</c>).</summary>
    internal const string KeyNumber = "number";

    /// <summary>Внутренний ключ логического свойства (совпадает с <c>VelumSolidCustomPropertyTypes.TypeKeyBoolean</c>).</summary>
    internal const string KeyBoolean = "boolean";

    /// <summary>Отображаемое имя текстового свойства.</summary>
    internal const string DisplayText = "Текст";

    /// <summary>Отображаемое имя свойства-даты.</summary>
    internal const string DisplayDate = "Дата";

    /// <summary>Отображаемое имя числового свойства.</summary>
    internal const string DisplayNumber = "Номер";

    /// <summary>Отображаемое имя логического свойства (Да/Нет).</summary>
    internal const string DisplayBoolean = "Да или Нет";

    /// <summary>Отображаемое имя типа по умолчанию.</summary>
    internal const string DefaultDisplay = DisplayText;

    // Пары «отображаемое имя → ключ» в порядке отображения в списке.
    private static readonly KeyValuePair<string, string>[] Pairs =
    {
      new KeyValuePair<string, string>(DisplayText, KeyText),
      new KeyValuePair<string, string>(DisplayDate, KeyDate),
      new KeyValuePair<string, string>(DisplayNumber, KeyNumber),
      new KeyValuePair<string, string>(DisplayBoolean, KeyBoolean)
    };

    /// <summary>Отображаемые имена типов в порядке вывода в выпадающем списке.</summary>
    internal static IReadOnlyList<string> DisplayNames
    {
      get
      {
        var names = new List<string>(Pairs.Length);
        for (int i = 0; i < Pairs.Length; i++)
          names.Add(Pairs[i].Key);
        return names;
      }
    }

    /// <summary>
    /// Внутренний ключ записи по отображаемому имени; при неизвестном значении — текст.
    /// </summary>
    internal static string DisplayToKey(string display)
    {
      string d = (display ?? string.Empty).Trim();
      for (int i = 0; i < Pairs.Length; i++)
      {
        if (string.Equals(Pairs[i].Key, d, StringComparison.OrdinalIgnoreCase))
          return Pairs[i].Value;
      }

      return KeyText;
    }

    /// <summary>
    /// Отображаемое имя по внутреннему ключу записи; при неизвестном/пустом значении — текст.
    /// Пустое значение (атрибут отсутствует в старом шаблоне) трактуется как текст по умолчанию.
    /// </summary>
    internal static string KeyToDisplay(string key)
    {
      string k = (key ?? string.Empty).Trim();
      for (int i = 0; i < Pairs.Length; i++)
      {
        if (string.Equals(Pairs[i].Value, k, StringComparison.OrdinalIgnoreCase))
          return Pairs[i].Key;
      }

      return DefaultDisplay;
    }
  }
}
