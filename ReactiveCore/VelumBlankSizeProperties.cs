using System;
using System.Globalization;
using System.Text;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Имена служебных свойств габарита/проката заготовки (спецификация / суффикс DXF).
  /// Толщина/Длина/Ширина — живые ссылки; Прокат — константа на вкладке документа.
  /// </summary>
  public static class VelumBlankSizeProperties
  {
    /// <summary>Свойство конфигурации: толщина листа (ссылка).</summary>
    public const string Thickness = "Толщина";

    /// <summary>Свойство конфигурации: больший габарит развёртки (ссылка).</summary>
    public const string Length = "Длина";

    /// <summary>Свойство конфигурации: меньший габарит развёртки (ссылка).</summary>
    public const string Width = "Ширина";

    /// <summary>Свойство документа (главная вкладка): вид проката для листовой детали.</summary>
    public const string RolledStock = "Прокат";

    /// <summary>Значение <see cref="RolledStock"/> для листового металла.</summary>
    public const string RolledStockSheetValue = "Лист";

    /// <summary>Метрика: ссылки Толщина/Длина/Ширина и Прокат=Лист валидны (листовая + Нужен dxf).</summary>
    public const string ProbeKeyLinksOk = "Velum.Solid.BlankSize.LinksOk";

    /// <summary>true, если имя — одно из свойств габарита заготовки.</summary>
    public static bool IsBlankSizePropertyName(string propertyName)
    {
      string name = (propertyName ?? string.Empty).Trim();
      if (name.Length == 0)
        return false;

      return string.Equals(name, Thickness, StringComparison.Ordinal)
          || string.Equals(name, Length, StringComparison.Ordinal)
          || string.Equals(name, Width, StringComparison.Ordinal);
    }

    /// <summary>true, если ключ пробы — <see cref="ProbeKeyLinksOk"/>.</summary>
    public static bool IsBlankSizeProbeKey(string probeKey)
    {
      return string.Equals(
          (probeKey ?? string.Empty).Trim(),
          ProbeKeyLinksOk,
          StringComparison.Ordinal);
    }

    /// <summary>
    /// Нормализует Толщина/Длина/Ширина для имени DXF: целое без дроби,
    /// иначе округление до 1 знака (инвариантная точка).
    /// SW часто отдаёт «5.000» из точности документа — без нормализации имя файла
    /// и ожидаемое имя метрики расходятся после пересборки свойств.
    /// </summary>
    /// <param name="rawValue">Сырое/вычисленное значение свойства SW.</param>
    /// <returns>Строка для суффикса имени файла.</returns>
    public static string FormatValueForFileName(string rawValue)
    {
      string trimmed = (rawValue ?? string.Empty).Trim();
      if (trimmed.Length == 0)
        return string.Empty;

      if (!TryParseNumber(trimmed, out double value))
        return trimmed;

      double rounded = Math.Round(value, 1, MidpointRounding.AwayFromZero);
      long asInt = (long)Math.Round(rounded, MidpointRounding.AwayFromZero);
      if (Math.Abs(rounded - asInt) < 1e-9)
        return asInt.ToString(CultureInfo.InvariantCulture);

      return rounded.ToString("0.0", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Парсит число из resolved-строки свойства (точка/запятая, опциональный хвост единиц).
    /// Число должно начинаться с начала строки (после trim); ведущий текст («Ст3сп5», «Швеллер 10П») не число.
    /// </summary>
    public static bool TryParseNumber(string text, out double value)
    {
      value = 0;
      if (string.IsNullOrWhiteSpace(text))
        return false;

      string t = text.Trim();
      var sb = new StringBuilder(t.Length);
      bool seenDigit = false;
      bool seenDot = false;
      for (int i = 0; i < t.Length; i++)
      {
        char c = t[i];
        if (c >= '0' && c <= '9')
        {
          sb.Append(c);
          seenDigit = true;
          continue;
        }

        if ((c == '.' || c == ',') && !seenDot)
        {
          sb.Append('.');
          seenDot = true;
          continue;
        }

        if ((c == '-' || c == '+') && sb.Length == 0)
        {
          sb.Append(c);
          continue;
        }

        // хвост единиц: mm, мм, in …
        if (seenDigit)
          break;

        // Ведущий нечисловой текст — это не числовое свойство.
        return false;
      }

      if (!seenDigit)
        return false;

      return double.TryParse(sb.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
  }
}
