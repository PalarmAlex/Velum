using System;
using System.Collections.Generic;

namespace Velum.UI.AssemblyRegistry
{
  /// <summary>Разрешение значения ячейки по определению столбца и кэшу свойств.</summary>
  internal static class VelumAssemblyRegistryColumnResolver
  {
    internal static string ResolveDisplay(
        VelumAssemblyRegistryColumnDef column,
        VelumAssemblyRegistryComponent item,
        int productQuantity = 1)
    {
      string raw = ResolveRaw(column, item, productQuantity);
      if (string.Equals(raw, VelumAssemblyRegistryColumnFormula.EvaluationErrorMarker, StringComparison.Ordinal))
        return raw;
      return VelumAssemblyRegistryPropertyReader.FormatListNumberText(raw);
    }

    internal static string ResolveRaw(
        VelumAssemblyRegistryColumnDef column,
        VelumAssemblyRegistryComponent item,
        int productQuantity = 1)
    {
      if (column == null || item == null)
        return VelumAssemblyRegistryColumnFormula.EvaluationErrorMarker;

      int qtyScale = productQuantity < 1 ? 1 : productQuantity;

      string formula = (column.Formula ?? string.Empty).Trim();
      if (formula.Length > 0)
        return VelumAssemblyRegistryColumnFormula.Evaluate(formula, key => LookupRaw(item, key, qtyScale));

      string name = (column.Name ?? string.Empty).Trim();
      if (name.Length == 0)
        return string.Empty;

      if (name.Length >= 2 && name[0] == '[' && name[name.Length - 1] == ']')
      {
        string key = name.Substring(1, name.Length - 2).Trim();
        string reserved = LookupRaw(item, key, qtyScale);
        return reserved ?? VelumAssemblyRegistryColumnFormula.EvaluationErrorMarker;
      }

      string prop = LookupRaw(item, name, qtyScale);
      if (prop == null)
        return VelumAssemblyRegistryPropertyReader.MissingMarker;
      return prop;
    }

    /// <summary>
    /// Сырое значение ссылки. null — ключ отсутствует в кэше (или «?»).
    /// Пустая строка допускается (свойство есть, но пустое).
    /// </summary>
    internal static string LookupRaw(
        VelumAssemblyRegistryComponent item,
        string key,
        int productQuantity = 1)
    {
      if (item == null || string.IsNullOrWhiteSpace(key))
        return null;

      string name = key.Trim();
      int qtyScale = productQuantity < 1 ? 1 : productQuantity;

      // Reserved всегда с typed-полей: Quantity растёт после FillProperties, кэш мог остаться «0».
      if (string.Equals(name, "Quantity", StringComparison.OrdinalIgnoreCase) ||
          string.Equals(name, "Кол-во", StringComparison.OrdinalIgnoreCase))
      {
        int qty = item.Quantity;
        if (qtyScale > 1 && qty > 0)
          qty = qty * qtyScale;
        return qty.ToString(System.Globalization.CultureInfo.InvariantCulture);
      }

      if (string.Equals(name, "FileName", StringComparison.OrdinalIgnoreCase) ||
          string.Equals(name, "ИмяФайла", StringComparison.OrdinalIgnoreCase))
        return item.FileTitle ?? string.Empty;

      Dictionary<string, string> cache = item.PropertyValues;
      if (cache != null)
      {
        string value;
        if (cache.TryGetValue(name, out value))
        {
          if (value == null)
            return null;
          if (string.Equals(value.Trim(), VelumAssemblyRegistryPropertyReader.MissingMarker, StringComparison.Ordinal))
            return null;
          return value;
        }
      }

      return null;
    }

    internal sealed class ValidationHit
    {
      internal string ComponentLabel { get; set; }

      internal string ColumnCaption { get; set; }

      internal string Detail { get; set; }
    }

    /// <summary>
    /// Прогон столбцов по кэшу компонентов: синтаксис + вычисление.
    /// </summary>
    internal static List<ValidationHit> ValidateAgainstCache(
        IList<VelumAssemblyRegistryColumnDef> columns,
        IEnumerable<VelumAssemblyRegistryComponent> components,
        int maxHits)
    {
      var hits = new List<ValidationHit>();
      if (columns == null || components == null)
        return hits;

      foreach (VelumAssemblyRegistryColumnDef column in columns)
      {
        if (column == null)
          continue;

        string formula = (column.Formula ?? string.Empty).Trim();
        if (formula.Length > 0)
        {
          string syntaxError;
          if (!VelumAssemblyRegistryColumnFormula.TryValidateSyntax(formula, out syntaxError))
          {
            hits.Add(new ValidationHit
            {
              ComponentLabel = "—",
              ColumnCaption = VelumAssemblyRegistryColumnStore.CaptionFromName(column.Name),
              Detail = "Синтаксис: " + (syntaxError ?? "ошибка")
            });
            if (maxHits > 0 && hits.Count >= maxHits)
              return hits;
            continue;
          }
        }

        string caption = VelumAssemblyRegistryColumnStore.CaptionFromName(column.Name);
        foreach (VelumAssemblyRegistryComponent item in components)
        {
          if (item == null)
            continue;

          string value = ResolveRaw(column, item);
          if (!string.Equals(value, VelumAssemblyRegistryColumnFormula.EvaluationErrorMarker, StringComparison.Ordinal))
            continue;

          hits.Add(new ValidationHit
          {
            ComponentLabel = BuildComponentLabel(item),
            ColumnCaption = caption,
            Detail = DescribeFailure(column, item)
          });
          if (maxHits > 0 && hits.Count >= maxHits)
            return hits;
        }
      }

      return hits;
    }

    private static string BuildComponentLabel(VelumAssemblyRegistryComponent item)
    {
      string designation;
      if (item.PropertyValues != null &&
          item.PropertyValues.TryGetValue("Обозначение", out designation) &&
          !string.IsNullOrWhiteSpace(designation))
        return designation;

      if (!string.IsNullOrWhiteSpace(item.Designation))
        return item.Designation;
      if (!string.IsNullOrWhiteSpace(item.FileTitle))
        return item.FileTitle;
      return item.Identity ?? "?";
    }

    private static string DescribeFailure(
        VelumAssemblyRegistryColumnDef column,
        VelumAssemblyRegistryComponent item)
    {
      string formula = (column.Formula ?? string.Empty).Trim();
      string source = formula.Length > 0 ? formula : (column.Name ?? string.Empty);
      foreach (string key in VelumAssemblyRegistryColumnFormula.ExtractRefs(source))
      {
        string raw = LookupRaw(item, key);
        if (raw == null)
          return "Нет числа для [" + key + "]";
      }

      if (formula.Length == 0)
      {
        string name = (column.Name ?? string.Empty).Trim();
        if (name.Length >= 2 && name[0] == '[' && name[name.Length - 1] == ']')
          return "Не удалось разрешить " + name;
      }

      return VelumAssemblyRegistryColumnFormula.EvaluationErrorMarker;
    }
  }
}
