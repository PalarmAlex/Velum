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
  /// Хранилище настроек выгрузки BOM (<c>bomExchangeLayout.json</c>).
  /// Задаёт состав, порядок и заголовки колонок CSV и списка формы экспорта.
  /// Файловый ввод-вывод — здесь; чистые правила состава — в
  /// <see cref="VelumBomExchangeLayoutRules"/>, DTO — в
  /// <see cref="VelumBomExchangeLayoutFile"/> / <see cref="VelumBomExchangeColumnDef"/>.
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
    /// См. <see cref="VelumBomExchangeLayoutRules.StructuralKeys"/>.
    /// </summary>
    internal static string[] StructuralKeys => VelumBomExchangeLayoutRules.StructuralKeys;

    /// <summary>Текущая версия набора колонок по умолчанию.</summary>
    internal const int CurrentLayoutFormatVersion = VelumBomExchangeLayoutRules.CurrentLayoutFormatVersion;

    /// <summary>Поля, которые нельзя выключить (по ТЗ).</summary>
    internal static string[] MandatoryStructuralKeys => VelumBomExchangeLayoutRules.MandatoryStructuralKeys;

    /// <summary>Путь к файлу настроек выгрузки.</summary>
    public static string FilePath => Path.Combine(
        VelumAppConfig.AssemblyRegistryFolderPath,
        "bomExchangeLayout.json");

    /// <summary>Проверить, является ли поле структурным.</summary>
    /// <param name="field">Имя поля.</param>
    /// <returns>true, если поле структурное.</returns>
    public static bool IsStructural(string field)
    {
      return VelumBomExchangeLayoutRules.IsStructural(field);
    }

    /// <summary>Проверить, является ли поле обязательным (нельзя выключить).</summary>
    /// <param name="field">Имя поля.</param>
    /// <returns>true, если поле обязательное.</returns>
    public static bool IsMandatory(string field)
    {
      return VelumBomExchangeLayoutRules.IsMandatory(field);
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
    /// Возвращает true, если состав изменился и файл нужно перезаписать.
    /// </summary>
    /// <param name="data">Нормализованный layout.</param>
    private static bool MigrateFormatVersion(VelumBomExchangeLayoutFile data)
    {
      return VelumBomExchangeLayoutRules.MigrateFormatVersion(data);
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
      return VelumBomExchangeLayoutRules.CreateDefault(LoadTrackedNames());
    }

    /// <summary>
    /// Синхронизация layout: добавляет отсутствующие структурные и tracked поля,
    /// удаляет tracked, которых больше нет в настройках, удаляет осиротевшие
    /// структурные (снятые с поддержки), переуплотняет Order, чинит пустые
    /// Header и ширины.
    /// </summary>
    /// <param name="data">Нормализуемый layout.</param>
    internal static void Normalize(VelumBomExchangeLayoutFile data)
    {
      VelumBomExchangeLayoutRules.Normalize(data, LoadTrackedNames());
    }

    /// <summary>Имена отслеживаемых свойств из bomTrackedProperties.json (уникальные).</summary>
    private static IReadOnlyList<string> LoadTrackedNames()
    {
      var names = new List<string>();
      foreach (TrackedProperty prop in VelumAssemblyBomTrackedPropertiesConfig.Load())
      {
        if (string.IsNullOrWhiteSpace(prop?.Name)) continue;
        names.Add(prop.Name.Trim());
      }
      return names;
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
