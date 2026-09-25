using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ISIDA.Common;
using Newtonsoft.Json;
using Velum.Configuration;

namespace Velum.UI.AssemblyRegistry
{
  /// <summary>
  /// Источник значения колонки выгрузки BOM: структурное поле зеркала или отслеживаемое свойство.
  /// </summary>
  internal enum VelumBomExchangeFieldSource
  {
    /// <summary>Структурное поле записи зеркала BOM (TypeDocs, ExternalId, …).</summary>
    Structural = 0,

    /// <summary>Значение отслеживаемого свойства из снимка <c>TrackedValues</c>.</summary>
    Tracked = 1
  }

  /// <summary>
  /// Описание одной колонки CSV/списка экспорта BOM в 1С.
  /// </summary>
  internal sealed class VelumBomExchangeColumnDef
  {
    /// <summary>Ключ структурного поля или имя отслеживаемого свойства.</summary>
    public string Field { get; set; }

    /// <summary>Заголовок колонки в CSV и в списке формы.</summary>
    public string Header { get; set; }

    /// <summary>Источник значения колонки.</summary>
    public VelumBomExchangeFieldSource Source { get; set; }

    /// <summary>Включена ли колонка в выгрузку.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Порядковый номер колонки (1..N).</summary>
    public int Order { get; set; }

    /// <summary>Ширина колонки в списке формы, пикселей.</summary>
    public int Width { get; set; } = 120;
  }

  /// <summary>
  /// Корень файла <c>bomExchangeLayout.json</c> — упорядоченный список колонок выгрузки.
  /// </summary>
  internal sealed class VelumBomExchangeLayoutFile
  {
    /// <summary>
    /// Версия формата layout, записанная в файле. Отсутствие поля (0) означает
    /// набор до появления <see cref="VelumBomExchangeLayoutStore.CurrentLayoutFormatVersion"/>.
    /// </summary>
    [JsonProperty("layoutFormatVersion")]
    public int LayoutFormatVersion { get; set; }

    /// <summary>Колонки выгрузки в порядке следования.</summary>
    public List<VelumBomExchangeColumnDef> Columns { get; set; } =
        new List<VelumBomExchangeColumnDef>();
  }

  /// <summary>
  /// Хранилище настроек выгрузки BOM (<c>bomExchangeLayout.json</c>).
  /// Задаёт состав, порядок и заголовки колонок CSV и списка формы экспорта.
  /// </summary>
  internal static class VelumBomExchangeLayoutStore
  {
    /// <summary>Настройки сериализации JSON.</summary>
    private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
    {
      Formatting = Formatting.Indented,
      NullValueHandling = NullValueHandling.Ignore
    };

    /// <summary>
    /// Ключи структурных полей в порядке колонок текущего CSV (жёстко в коде).
    /// Порядок соответствует прежней выгрузке: TypeDocs, ExternalId, Designation, Name,
    /// FilePath, Configuration, Quantity.
    /// </summary>
    internal static readonly string[] StructuralKeys =
    {
      "TypeDocs", "ExternalId", "Designation", "Name", "FilePath", "Configuration", "Quantity"
    };

    /// <summary>
    /// Текущая версия набора колонок по умолчанию.
    /// <b>2</b> — <c>Quantity</c> в наборе по умолчанию выключен (он больше не входит
    /// в хэш карточки, см. <see cref="VelumAssemblyBomTrackedPropertiesConfig.HashFormatVersion"/>).
    /// При обнаружении файла более старой версии состав правится один раз,
    /// дальше настройка полностью за оператором.
    /// </summary>
    internal const int CurrentLayoutFormatVersion = 2;

    /// <summary>
    /// Поля, которые нельзя выключить (по ТЗ).
    /// <c>Quantity</c> из списка убран: количество вхождений описывает связь позиции
    /// со сборкой-родителем, а не саму карточку, и в <c>1C_bom_*.csv</c> оно есть;
    /// дублировать его в <c>1C_update_*.csv</c> смысла нет.
    /// </summary>
    internal static readonly string[] MandatoryStructuralKeys =
    {
      "TypeDocs", "ExternalId", "Designation", "Name"
    };

    /// <summary>
    /// Ширины структурных колонок по умолчанию (сохраняют прежний вид списка формы).
    /// </summary>
    private static readonly Dictionary<string, int> StructuralDefaultWidths =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
          { "TypeDocs", 80 },
          { "ExternalId", 100 },
          { "Designation", 120 },
          { "Name", 150 },
          { "FilePath", 200 },
          { "Configuration", 110 },
          { "Quantity", 70 }
        };

    /// <summary>
    /// Структурные поля, которые по умолчанию выключены (колонка существует,
    /// но в CSV не идёт, пока оператор сам не включит).
    /// </summary>
    private static readonly HashSet<string> DefaultDisabledStructuralKeys =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Quantity" };

    /// <summary>Включена ли структурная колонка по умолчанию.</summary>
    /// <param name="field">Имя структурного поля.</param>
    /// <returns>false только для полей из <see cref="DefaultDisabledStructuralKeys"/>.</returns>
    private static bool DefaultEnabledFor(string field)
    {
      return !(!string.IsNullOrWhiteSpace(field) &&
               DefaultDisabledStructuralKeys.Contains(field.Trim()));
    }

    /// <summary>Путь к файлу настроек выгрузки.</summary>
    public static string FilePath => Path.Combine(
        VelumAppConfig.AssemblyRegistryFolderPath,
        "bomExchangeLayout.json");

    /// <summary>Проверить, является ли поле структурным.</summary>
    /// <param name="field">Имя поля.</param>
    /// <returns>true, если поле структурное.</returns>
    public static bool IsStructural(string field)
    {
      if (string.IsNullOrWhiteSpace(field)) return false;
      return StructuralKeys.Any(k =>
          string.Equals(k, field, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Проверить, является ли поле обязательным (нельзя выключить).</summary>
    /// <param name="field">Имя поля.</param>
    /// <returns>true, если поле обязательное.</returns>
    public static bool IsMandatory(string field)
    {
      if (string.IsNullOrWhiteSpace(field)) return false;
      return MandatoryStructuralKeys.Any(k =>
          string.Equals(k, field, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Загрузить layout; при отсутствии файла создать и сохранить набор по умолчанию.
    /// </summary>
    /// <returns>Загруженный или созданный layout.</returns>
    public static VelumBomExchangeLayoutFile LoadOrCreate()
    {
      Directory.CreateDirectory(VelumAppConfig.AssemblyRegistryFolderPath);
      if (!File.Exists(FilePath))
      {
        VelumBomExchangeLayoutFile created = CreateDefault();
        Save(created);
        return created;
      }

      try
      {
        string json = File.ReadAllText(FilePath, Encoding.UTF8);
        VelumBomExchangeLayoutFile data = string.IsNullOrWhiteSpace(json)
            ? CreateDefault()
            : (JsonConvert.DeserializeObject<VelumBomExchangeLayoutFile>(json, JsonSettings)
               ?? CreateDefault());
        Normalize(data);
        if (MigrateFormatVersion(data))
          Save(data);
        return data;
      }
      catch (Exception ex)
      {
        Logger.Error("Velum bomExchangeLayout load failed: " + ex.Message);
        VelumBomExchangeLayoutFile fallback = CreateDefault();
        try { Save(fallback); } catch { }
        return fallback;
      }
    }

    /// <summary>
    /// Одноразово привести набор колонок к <see cref="CurrentLayoutFormatVersion"/>.
    /// Файлы, созданные до появления версии, имеют <c>0</c>.
    /// Возвращает true, если состав изменился и файл нужно перезаписать.
    /// </summary>
    /// <param name="data">Нормализованный layout.</param>
    private static bool MigrateFormatVersion(VelumBomExchangeLayoutFile data)
    {
      if (data == null || data.LayoutFormatVersion == CurrentLayoutFormatVersion)
        return false;

      if (data.LayoutFormatVersion > CurrentLayoutFormatVersion)
      {
        // Файл из более новой версии — обратно колонки не «чиним», чтобы не
        // затереть осознанный выбор оператора.
        return false;
      }

      // Версия 2: Quantity убран из хэша карточки и больше не обязателен в CSV.
      if (data.LayoutFormatVersion < 2)
      {
        foreach (VelumBomExchangeColumnDef c in data.Columns)
        {
          if (c == null || c.Source != VelumBomExchangeFieldSource.Structural)
            continue;
          if (!string.Equals((c.Field ?? string.Empty).Trim(), "Quantity",
                  StringComparison.OrdinalIgnoreCase))
            continue;
          c.Enabled = false;
        }
      }

      data.LayoutFormatVersion = CurrentLayoutFormatVersion;
      return true;
    }

    /// <summary>
    /// Сохранить layout атомарно (нормализуя перед записью).
    /// </summary>
    /// <param name="data">Сохраняемый layout.</param>
    public static void Save(VelumBomExchangeLayoutFile data)
    {
      if (data == null) throw new ArgumentNullException(nameof(data));
      Directory.CreateDirectory(VelumAppConfig.AssemblyRegistryFolderPath);
      Normalize(data);
      string json = JsonConvert.SerializeObject(data, JsonSettings);
      WriteAtomic(FilePath, json);
    }

    /// <summary>
    /// Создать layout по умолчанию: все структурные поля + все отслеживаемые свойства,
    /// порядок совпадает с прежней выгрузкой.
    /// </summary>
    /// <returns>Новый layout.</returns>
    internal static VelumBomExchangeLayoutFile CreateDefault()
    {
      var file = new VelumBomExchangeLayoutFile
      {
        LayoutFormatVersion = CurrentLayoutFormatVersion
      };
      int order = 1;
      foreach (string key in StructuralKeys)
      {
        file.Columns.Add(new VelumBomExchangeColumnDef
        {
          Field = key,
          Header = key,
          Source = VelumBomExchangeFieldSource.Structural,
          Enabled = DefaultEnabledFor(key),
          Order = order++,
          Width = DefaultWidthFor(key)
        });
      }
      foreach (TrackedProperty prop in VelumAssemblyBomTrackedPropertiesConfig.Load())
      {
        if (string.IsNullOrWhiteSpace(prop?.Name)) continue;
        file.Columns.Add(new VelumBomExchangeColumnDef
        {
          Field = prop.Name.Trim(),
          Header = prop.Name.Trim(),
          Source = VelumBomExchangeFieldSource.Tracked,
          Enabled = true,
          Order = order++,
          Width = 120
        });
      }
      return file;
    }

    /// <summary>
    /// Синхронизация layout: добавляет отсутствующие структурные и tracked поля,
    /// удаляет tracked, которых больше нет в bomTrackedProperties.json,
    /// переуплотняет Order, чинит пустые Header и ширины.
    /// </summary>
    /// <param name="data">Нормализуемый layout.</param>
    internal static void Normalize(VelumBomExchangeLayoutFile data)
    {
      if (data == null) return;
      if (data.Columns == null)
        data.Columns = new List<VelumBomExchangeColumnDef>();

      IReadOnlyList<TrackedProperty> tracked =
          VelumAssemblyBomTrackedPropertiesConfig.Load();
      var trackedNames = new List<string>();
      var trackedNameSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (TrackedProperty p in tracked)
      {
        if (string.IsNullOrWhiteSpace(p?.Name)) continue;
        string name = p.Name.Trim();
        if (trackedNameSet.Add(name))
          trackedNames.Add(name);
      }

      // Удаляем tracked, которых больше нет в настройках свойств.
      data.Columns.RemoveAll(c =>
          c != null &&
          c.Source == VelumBomExchangeFieldSource.Tracked &&
          !trackedNameSet.Contains((c.Field ?? string.Empty).Trim()));

      // Добавляем отсутствующие структурные.
      var present = new HashSet<string>(
          data.Columns.Where(c => c != null && c.Source == VelumBomExchangeFieldSource.Structural)
              .Select(c => (c.Field ?? string.Empty).Trim()),
          StringComparer.OrdinalIgnoreCase);
      foreach (string key in StructuralKeys)
      {
        if (!present.Contains(key))
        {
          data.Columns.Add(new VelumBomExchangeColumnDef
          {
            Field = key,
            Header = key,
            Source = VelumBomExchangeFieldSource.Structural,
            Enabled = true,
            Order = int.MaxValue,
            Width = DefaultWidthFor(key)
          });
        }
      }

      // Добавляем отсутствующие tracked.
      var presentTracked = new HashSet<string>(
          data.Columns.Where(c => c != null && c.Source == VelumBomExchangeFieldSource.Tracked)
              .Select(c => (c.Field ?? string.Empty).Trim()),
          StringComparer.OrdinalIgnoreCase);
      foreach (string name in trackedNames)
      {
        if (!presentTracked.Contains(name))
        {
          data.Columns.Add(new VelumBomExchangeColumnDef
          {
            Field = name,
            Header = name,
            Source = VelumBomExchangeFieldSource.Tracked,
            Enabled = true,
            Order = int.MaxValue,
            Width = 120
          });
        }
      }

      // Чистим null, чиним Header/Width, заставляем mandatory быть включёнными.
      data.Columns.RemoveAll(c => c == null);
      foreach (VelumBomExchangeColumnDef c in data.Columns)
      {
        c.Field = (c.Field ?? string.Empty).Trim();
        c.Header = string.IsNullOrWhiteSpace(c.Header) ? c.Field : c.Header.Trim();
        if (c.Width <= 0)
          c.Width = c.Source == VelumBomExchangeFieldSource.Structural
              ? DefaultWidthFor(c.Field)
              : 120;
        else if (c.Width < 40)
          c.Width = 40;
        if (c.Source == VelumBomExchangeFieldSource.Structural && IsMandatory(c.Field))
          c.Enabled = true;
      }

      // Переуплотняем Order по текущему порядку (стабильно).
      var ordered = data.Columns
          .Select((c, idx) => new { Col = c, Idx = idx })
          .OrderBy(x => x.Col.Order)
          .ThenBy(x => x.Idx)
          .Select(x => x.Col)
          .ToList();
      for (int i = 0; i < ordered.Count; i++)
        ordered[i].Order = i + 1;
      data.Columns = ordered;
    }

    /// <summary>Ширина структурной колонки по умолчанию (120, если ключ неизвестен).</summary>
    /// <param name="field">Имя структурного поля.</param>
    /// <returns>Ширина в пикселях.</returns>
    private static int DefaultWidthFor(string field)
    {
      if (!string.IsNullOrWhiteSpace(field) && StructuralDefaultWidths.TryGetValue(field.Trim(), out int w))
        return w;
      return 120;
    }

    /// <summary>Атомарная запись файла (как в <c>VelumAssemblyBomMirrorStore</c>).</summary>
    /// <param name="path">Целевой путь.</param>
    /// <param name="json">Содержимое.</param>
    private static void WriteAtomic(string path, string json)
    {
      string tempPath = path + ".tmp";
      File.WriteAllText(tempPath, json,
          new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
      if (File.Exists(path)) File.Replace(tempPath, path, null);
      else File.Move(tempPath, path);
    }
  }
}