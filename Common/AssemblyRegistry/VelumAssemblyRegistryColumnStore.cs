using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ISIDA.Common;
using Newtonsoft.Json;
using Velum.Configuration;

namespace Velum.UI.AssemblyRegistry
{
  /// <summary>Тип итога по столбцу в списке и отчёте.</summary>
  internal enum VelumAssemblyRegistryColumnTotalsKind
  {
    None = 0,
    Min = 1,
    Max = 2,
    Avg = 3,
    Sum = 4
  }

  /// <summary>Начальная сортировка списка/отчёта по столбцу шаблона.</summary>
  internal enum VelumAssemblyRegistryColumnSortKind
  {
    None = 0,
    Ascending = 1,
    Descending = 2
  }

  /// <summary>Определение столбца шаблона реестра изделия.</summary>
  internal sealed class VelumAssemblyRegistryColumnDef
  {
    public int Order { get; set; }

    /// <summary>Ключ/заголовок: свойство документа или <c>[Reserved]</c>.</summary>
    public string Name { get; set; }

    /// <summary>Выражение ячейки; пусто — значение берётся из <see cref="Name"/>.</summary>
    public string Formula { get; set; }

    /// <summary>Tooltip столбца.</summary>
    public string Description { get; set; }

    /// <summary>
    /// Префилл поля фильтра UI при выборе шаблона (DSL <see cref="Velum.UI.VelumListFilterHelper"/>).
    /// </summary>
    public string Filter { get; set; }

    /// <summary>Итог по столбцу: без итогов / Min / Max / Avg / Sum.</summary>
    public VelumAssemblyRegistryColumnTotalsKind TotalsKind { get; set; }

    /// <summary>
    /// Группировка строк в HTML-отчёте по этому столбцу (не более одного столбца в шаблоне).
    /// На список формы не влияет.
    /// </summary>
    public bool GroupBy { get; set; }

    /// <summary>
    /// Начальная сортировка списка и отчёта по этому столбцу (не более одного в шаблоне).
    /// </summary>
    public VelumAssemblyRegistryColumnSortKind SortKind { get; set; }

    /// <summary>
    /// Показывать столбец в списке и HTML-отчёте.
    /// Фильтрация по скрытым столбцам сохраняется.
    /// </summary>
    public bool ShowInList { get; set; } = true;
  }

  /// <summary>Именованный шаблон набора столбцов.</summary>
  internal sealed class VelumAssemblyRegistryColumnTemplate
  {
    public string Name { get; set; }

    public List<VelumAssemblyRegistryColumnDef> Columns { get; set; } =
        new List<VelumAssemblyRegistryColumnDef>();
  }

  /// <summary>Корень JSON настроек столбцов реестра изделия.</summary>
  internal sealed class VelumAssemblyRegistryColumnSettingsFile
  {
    public List<string> TemplateNames { get; set; } = new List<string>();

    public List<VelumAssemblyRegistryColumnTemplate> Templates { get; set; } =
        new List<VelumAssemblyRegistryColumnTemplate>();
  }

  /// <summary>
  /// Хранилище шаблонов столбцов
  /// (<c>columnTemplates.json</c> в <see cref="VelumAppConfig.AssemblyRegistryFolderPath"/>).
  /// </summary>
  internal static class VelumAssemblyRegistryColumnStore
  {
    private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
    {
      Formatting = Formatting.Indented,
      NullValueHandling = NullValueHandling.Ignore
    };

    internal const string DefaultTemplateName = "По умолчанию";

    internal static string FolderPath => VelumAppConfig.AssemblyRegistryFolderPath;

    internal static string FilePath => Path.Combine(FolderPath, "columnTemplates.json");

    internal static VelumAssemblyRegistryColumnSettingsFile LoadOrCreate()
    {
      Directory.CreateDirectory(FolderPath);
      if (!File.Exists(FilePath))
      {
        VelumAssemblyRegistryColumnSettingsFile created = CreateDefault();
        Save(created);
        return created;
      }

      try
      {
        string json = File.ReadAllText(FilePath);
        VelumAssemblyRegistryColumnSettingsFile data =
            JsonConvert.DeserializeObject<VelumAssemblyRegistryColumnSettingsFile>(json, JsonSettings)
            ?? CreateDefault();
        Normalize(data);
        if (data.Templates.Count == 0)
        {
          data = CreateDefault();
          Save(data);
        }

        return data;
      }
      catch (Exception ex)
      {
        Logger.Error("AssemblyRegistry columnTemplates load failed: " + ex.Message);
        VelumAssemblyRegistryColumnSettingsFile fallback = CreateDefault();
        try
        {
          Save(fallback);
        }
        catch
        {
        }

        return fallback;
      }
    }

    internal static void Save(VelumAssemblyRegistryColumnSettingsFile data)
    {
      if (data == null)
        throw new ArgumentNullException("data");

      Directory.CreateDirectory(FolderPath);
      Normalize(data);
      string json = JsonConvert.SerializeObject(data, JsonSettings);
      File.WriteAllText(FilePath, json);
    }

    internal static VelumAssemblyRegistryColumnTemplate FindTemplate(
        VelumAssemblyRegistryColumnSettingsFile data,
        string name)
    {
      if (data?.Templates == null || string.IsNullOrWhiteSpace(name))
        return null;

      return data.Templates.FirstOrDefault(t =>
          string.Equals(t.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    internal static string ResolveActiveTemplateName(VelumAssemblyRegistryColumnSettingsFile data)
    {
      if (data == null)
        return DefaultTemplateName;

      string last = (VelumAppConfig.AssemblyRegistryLastTemplate ?? string.Empty).Trim();
      if (last.Length > 0 && FindTemplate(data, last) != null)
        return FindTemplate(data, last).Name;

      if (data.Templates != null && data.Templates.Count > 0)
        return data.Templates[0].Name ?? DefaultTemplateName;

      return DefaultTemplateName;
    }

    internal static void SetLastTemplateName(string name)
    {
      VelumAppConfig.AssemblyRegistryLastTemplate = (name ?? string.Empty).Trim();
    }

    internal static List<VelumAssemblyRegistryColumnDef> GetOrderedColumns(
        VelumAssemblyRegistryColumnTemplate template)
    {
      if (template?.Columns == null || template.Columns.Count == 0)
        return new List<VelumAssemblyRegistryColumnDef>();

      return template.Columns
          .OrderBy(c => c.Order)
          .ThenBy(c => c.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
          .ToList();
    }

    internal static VelumAssemblyRegistryColumnDef FindGroupByColumn(
        IList<VelumAssemblyRegistryColumnDef> columns)
    {
      if (columns == null)
        return null;
      for (int i = 0; i < columns.Count; i++)
      {
        VelumAssemblyRegistryColumnDef col = columns[i];
        if (col != null && col.GroupBy)
          return col;
      }

      return null;
    }

    internal static VelumAssemblyRegistryColumnDef FindSortColumn(
        IList<VelumAssemblyRegistryColumnDef> columns)
    {
      if (columns == null)
        return null;
      for (int i = 0; i < columns.Count; i++)
      {
        VelumAssemblyRegistryColumnDef col = columns[i];
        if (col != null && col.SortKind != VelumAssemblyRegistryColumnSortKind.None)
          return col;
      }

      return null;
    }

    internal static int IndexOfSortColumn(IList<VelumAssemblyRegistryColumnDef> columns)
    {
      if (columns == null)
        return -1;
      for (int i = 0; i < columns.Count; i++)
      {
        VelumAssemblyRegistryColumnDef col = columns[i];
        if (col != null && col.SortKind != VelumAssemblyRegistryColumnSortKind.None)
          return i;
      }

      return -1;
    }

    internal static string SortKindDisplayName(VelumAssemblyRegistryColumnSortKind kind)
    {
      switch (kind)
      {
        case VelumAssemblyRegistryColumnSortKind.Ascending:
          return "По возрастанию";
        case VelumAssemblyRegistryColumnSortKind.Descending:
          return "По убыванию";
        default:
          return "Нет";
      }
    }

    internal static bool TryParseSortKindDisplay(string text, out VelumAssemblyRegistryColumnSortKind kind)
    {
      kind = VelumAssemblyRegistryColumnSortKind.None;
      string t = (text ?? string.Empty).Trim();
      if (t.Length == 0 ||
          string.Equals(t, "Нет", StringComparison.OrdinalIgnoreCase) ||
          string.Equals(t, "None", StringComparison.OrdinalIgnoreCase))
      {
        kind = VelumAssemblyRegistryColumnSortKind.None;
        return true;
      }

      if (string.Equals(t, "По возрастанию", StringComparison.OrdinalIgnoreCase) ||
          string.Equals(t, "Ascending", StringComparison.OrdinalIgnoreCase) ||
          string.Equals(t, "Asc", StringComparison.OrdinalIgnoreCase))
      {
        kind = VelumAssemblyRegistryColumnSortKind.Ascending;
        return true;
      }

      if (string.Equals(t, "По убыванию", StringComparison.OrdinalIgnoreCase) ||
          string.Equals(t, "Descending", StringComparison.OrdinalIgnoreCase) ||
          string.Equals(t, "Desc", StringComparison.OrdinalIgnoreCase))
      {
        kind = VelumAssemblyRegistryColumnSortKind.Descending;
        return true;
      }

      return false;
    }

    internal static VelumAssemblyRegistryColumnDef CloneColumn(VelumAssemblyRegistryColumnDef source)
    {
      if (source == null)
        return null;
      return new VelumAssemblyRegistryColumnDef
      {
        Order = source.Order,
        Name = source.Name,
        Formula = source.Formula,
        Description = source.Description,
        Filter = source.Filter,
        TotalsKind = source.TotalsKind,
        GroupBy = source.GroupBy,
        SortKind = source.SortKind,
        ShowInList = source.ShowInList
      };
    }

    internal static List<VelumAssemblyRegistryColumnDef> CloneColumns(
        IEnumerable<VelumAssemblyRegistryColumnDef> columns)
    {
      var result = new List<VelumAssemblyRegistryColumnDef>();
      if (columns == null)
        return result;
      foreach (VelumAssemblyRegistryColumnDef col in columns)
      {
        VelumAssemblyRegistryColumnDef copy = CloneColumn(col);
        if (copy != null)
          result.Add(copy);
      }

      return result;
    }

    internal static string AllocateUniqueTemplateName(
        VelumAssemblyRegistryColumnSettingsFile data,
        string baseName)
    {
      string root = string.IsNullOrWhiteSpace(baseName) ? "Новая сводка" : baseName.Trim();
      if (FindTemplate(data, root) == null)
        return root;

      for (int i = 2; i < 10000; i++)
      {
        string candidate = root + " (" + i + ")";
        if (FindTemplate(data, candidate) == null)
          return candidate;
      }

      return root + " (" + Guid.NewGuid().ToString("N").Substring(0, 8) + ")";
    }

    internal static string CaptionFromName(string name)
    {
      string text = (name ?? string.Empty).Trim();
      if (text.Length >= 2 && text[0] == '[' && text[text.Length - 1] == ']')
        return text.Substring(1, text.Length - 2).Trim();
      return text;
    }

    internal static VelumAssemblyRegistryColumnSettingsFile CreateDefault()
    {
      var template = new VelumAssemblyRegistryColumnTemplate
      {
        Name = DefaultTemplateName,
        Columns = new List<VelumAssemblyRegistryColumnDef>
        {
          Col(1, "Обозначение", "", "Обозначение из свойств конфигурации вхождения (или имя файла)."),
          Col(2, "Наименование", "", "Наименование из свойств конфигурации вхождения."),
          Col(3, "Материал", "", "Материал из свойств конфигурации вхождения."),
          Col(4, "Масса", "", "Масса одной позиции (свойство конфигурации)."),
          Col(5, "Прокат", "", "Прокат из свойств заготовки."),
          Col(6, "Толщина", "", "Толщина из свойств заготовки."),
          Col(7, "Ширина", "", "Ширина из свойств заготовки."),
          Col(8, "Длина", "", "Длина из свойств заготовки."),
          Col(9, "[Кол-во]", "[Quantity]", "Число вхождений в головной сборке (изделии)."),
          Col(10, "[Всего масса]", "[Масса]*[Quantity]", "Суммарная масса: масса × кол-во."),
          Col(11, "[Всего длина]", "[Длина]*[Quantity]", "Суммарная длина: длина × кол-во.")
        }
      };

      return new VelumAssemblyRegistryColumnSettingsFile
      {
        TemplateNames = new List<string> { DefaultTemplateName },
        Templates = new List<VelumAssemblyRegistryColumnTemplate> { template }
      };
    }

    private static VelumAssemblyRegistryColumnDef Col(
        int order,
        string name,
        string formula,
        string description)
    {
      return new VelumAssemblyRegistryColumnDef
      {
        Order = order,
        Name = name,
        Formula = formula,
        Description = description
      };
    }

    private static void Normalize(VelumAssemblyRegistryColumnSettingsFile data)
    {
      if (data.TemplateNames == null)
        data.TemplateNames = new List<string>();
      if (data.Templates == null)
        data.Templates = new List<VelumAssemblyRegistryColumnTemplate>();

      var names = new List<string>();
      foreach (string raw in data.TemplateNames)
      {
        string name = (raw ?? string.Empty).Trim();
        if (name.Length == 0)
          continue;
        if (names.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)))
          continue;
        names.Add(name);
      }

      foreach (VelumAssemblyRegistryColumnTemplate template in data.Templates)
      {
        if (template == null)
          continue;
        template.Name = (template.Name ?? string.Empty).Trim();
        if (template.Name.Length == 0)
          continue;
        if (!names.Any(n => string.Equals(n, template.Name, StringComparison.OrdinalIgnoreCase)))
          names.Add(template.Name);
        if (template.Columns == null)
          template.Columns = new List<VelumAssemblyRegistryColumnDef>();
        bool groupAssigned = false;
        bool sortAssigned = false;
        foreach (VelumAssemblyRegistryColumnDef col in template.Columns)
        {
          if (col == null)
            continue;
          col.Name = (col.Name ?? string.Empty).Trim();
          col.Formula = (col.Formula ?? string.Empty).Trim();
          col.Description = (col.Description ?? string.Empty).Trim();
          col.Filter = (col.Filter ?? string.Empty).Trim();
          if (!Enum.IsDefined(typeof(VelumAssemblyRegistryColumnTotalsKind), col.TotalsKind))
            col.TotalsKind = VelumAssemblyRegistryColumnTotalsKind.None;
          if (!Enum.IsDefined(typeof(VelumAssemblyRegistryColumnSortKind), col.SortKind))
            col.SortKind = VelumAssemblyRegistryColumnSortKind.None;
          if (col.GroupBy)
          {
            if (groupAssigned)
              col.GroupBy = false;
            else
              groupAssigned = true;
          }

          if (col.SortKind != VelumAssemblyRegistryColumnSortKind.None)
          {
            if (sortAssigned)
              col.SortKind = VelumAssemblyRegistryColumnSortKind.None;
            else
              sortAssigned = true;
          }
        }
      }

      data.TemplateNames = names;

      // Удалить шаблоны без имени; создать пустые шаблоны для имён без тела.
      data.Templates = data.Templates
          .Where(t => t != null && !string.IsNullOrWhiteSpace(t.Name))
          .GroupBy(t => t.Name.Trim(), StringComparer.OrdinalIgnoreCase)
          .Select(g => g.First())
          .ToList();

      foreach (string name in names)
      {
        if (FindTemplate(data, name) != null)
          continue;
        data.Templates.Add(new VelumAssemblyRegistryColumnTemplate
        {
          Name = name,
          Columns = new List<VelumAssemblyRegistryColumnDef>()
        });
      }

      // Синхронизировать TemplateNames с фактическими Templates.
      data.TemplateNames = data.Templates
          .Select(t => t.Name)
          .Where(n => !string.IsNullOrWhiteSpace(n))
          .Distinct(StringComparer.OrdinalIgnoreCase)
          .ToList();
    }
  }
}
