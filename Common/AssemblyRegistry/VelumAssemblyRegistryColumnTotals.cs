using System;
using System.Collections.Generic;
using Velum.ReactiveCore;

namespace Velum.UI.AssemblyRegistry
{
  /// <summary>Расчёт итогов по столбцу для списка и HTML-отчёта.</summary>
  internal static class VelumAssemblyRegistryColumnTotals
  {
    internal static string TotalsKindDisplayName(VelumAssemblyRegistryColumnTotalsKind kind)
    {
      switch (kind)
      {
        case VelumAssemblyRegistryColumnTotalsKind.Min:
          return "Min";
        case VelumAssemblyRegistryColumnTotalsKind.Max:
          return "Max";
        case VelumAssemblyRegistryColumnTotalsKind.Avg:
          return "Avg";
        case VelumAssemblyRegistryColumnTotalsKind.Sum:
          return "Sum";
        default:
          return "Без итогов";
      }
    }

    internal static bool TryParseTotalsKindDisplay(string text, out VelumAssemblyRegistryColumnTotalsKind kind)
    {
      kind = VelumAssemblyRegistryColumnTotalsKind.None;
      string t = (text ?? string.Empty).Trim();
      if (t.Length == 0 ||
          string.Equals(t, "Без итогов", StringComparison.OrdinalIgnoreCase) ||
          string.Equals(t, "None", StringComparison.OrdinalIgnoreCase))
      {
        kind = VelumAssemblyRegistryColumnTotalsKind.None;
        return true;
      }

      if (string.Equals(t, "Min", StringComparison.OrdinalIgnoreCase))
      {
        kind = VelumAssemblyRegistryColumnTotalsKind.Min;
        return true;
      }

      if (string.Equals(t, "Max", StringComparison.OrdinalIgnoreCase))
      {
        kind = VelumAssemblyRegistryColumnTotalsKind.Max;
        return true;
      }

      if (string.Equals(t, "Avg", StringComparison.OrdinalIgnoreCase))
      {
        kind = VelumAssemblyRegistryColumnTotalsKind.Avg;
        return true;
      }

      if (string.Equals(t, "Sum", StringComparison.OrdinalIgnoreCase))
      {
        kind = VelumAssemblyRegistryColumnTotalsKind.Sum;
        return true;
      }

      return false;
    }

    /// <summary>
    /// Столбец выглядит числовым по сэмплу компонентов (для валидации настроек).
    /// Пустой сэмпл — считаем допустимым (проверка на форме/в отчёте даст пустой итог).
    /// </summary>
    internal static bool LooksNumericForTotals(
        VelumAssemblyRegistryColumnDef column,
        IList<VelumAssemblyRegistryComponent> sample)
    {
      if (column == null || column.TotalsKind == VelumAssemblyRegistryColumnTotalsKind.None)
        return true;
      if (sample == null || sample.Count == 0)
        return true;

      int numeric = 0;
      int considered = 0;
      for (int i = 0; i < sample.Count; i++)
      {
        VelumAssemblyRegistryComponent item = sample[i];
        if (item == null)
          continue;

        string raw = VelumAssemblyRegistryColumnResolver.ResolveRaw(column, item) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raw) ||
            string.Equals(raw, VelumAssemblyRegistryPropertyReader.MissingMarker, StringComparison.Ordinal) ||
            string.Equals(raw, VelumAssemblyRegistryColumnFormula.EvaluationErrorMarker, StringComparison.Ordinal))
          continue;

        considered++;
        double value;
        if (VelumBlankSizeProperties.TryParseNumber(raw, out value))
          numeric++;
      }

      if (considered == 0)
        return true;
      return numeric * 2 >= considered;
    }

    /// <summary>
    /// Текст итога для UI/отчёта; пустая строка если итог выключен или нет числовых значений.
    /// </summary>
    internal static string ComputeDisplay(
        VelumAssemblyRegistryColumnDef column,
        IList<VelumAssemblyRegistryComponent> rows,
        int productQuantity = 1)
    {
      if (column == null || column.TotalsKind == VelumAssemblyRegistryColumnTotalsKind.None)
        return string.Empty;
      if (rows == null || rows.Count == 0)
        return string.Empty;

      int qtyScale = productQuantity < 1 ? 1 : productQuantity;
      var values = new List<double>();
      for (int i = 0; i < rows.Count; i++)
      {
        VelumAssemblyRegistryComponent item = rows[i];
        if (item == null)
          continue;

        string raw = VelumAssemblyRegistryColumnResolver.ResolveRaw(column, item, qtyScale) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raw) ||
            string.Equals(raw, VelumAssemblyRegistryPropertyReader.MissingMarker, StringComparison.Ordinal) ||
            string.Equals(raw, VelumAssemblyRegistryColumnFormula.EvaluationErrorMarker, StringComparison.Ordinal))
          continue;

        double number;
        if (VelumBlankSizeProperties.TryParseNumber(raw, out number))
          values.Add(number);
      }

      if (values.Count == 0)
        return string.Empty;

      double result;
      switch (column.TotalsKind)
      {
        case VelumAssemblyRegistryColumnTotalsKind.Min:
          result = values[0];
          for (int i = 1; i < values.Count; i++)
          {
            if (values[i] < result)
              result = values[i];
          }
          break;
        case VelumAssemblyRegistryColumnTotalsKind.Max:
          result = values[0];
          for (int i = 1; i < values.Count; i++)
          {
            if (values[i] > result)
              result = values[i];
          }
          break;
        case VelumAssemblyRegistryColumnTotalsKind.Sum:
          result = 0;
          for (int i = 0; i < values.Count; i++)
            result += values[i];
          break;
        case VelumAssemblyRegistryColumnTotalsKind.Avg:
          result = 0;
          for (int i = 0; i < values.Count; i++)
            result += values[i];
          result = result / values.Count;
          break;
        default:
          return string.Empty;
      }

      return VelumAssemblyRegistryPropertyReader.FormatListNumber(result);
    }
  }
}
