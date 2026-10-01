using System;
using System.Collections.Generic;
using System.Linq;

namespace Velum.UI.AssemblyRegistry
{
  /// <summary>
  /// Pure-правила состава/порядка колонок выгрузки BOM: эталонные ключи, обязательные поля,
  /// нормализация набора, дефолт и миграция версии формата. Без COM, файлов, конфигурации —
  /// список имён отслеживаемых свойств передаётся параметром. Линкуется в тестовый проект
  /// Velum.ReactiveCore.Tests.
  /// </summary>
  /// <remarks>
  /// Регрессия, которую защищают правила: CASEBOOK-1 случай 14 / E33 — снятие поля с поддержки
  /// оформлено как «убрано из эталонного набора ключей», иначе <see cref="Normalize"/> вечно
  /// возвращала снятое поле (Quantity) при любой загрузке layout.
  /// </remarks>
  internal static class VelumBomExchangeLayoutRules
  {
    /// <summary>
    /// Ключи структурных полей в порядке колонок текущего CSV (жёстко в коде).
    /// Порядок соответствует прежней выгрузке: TypeDocs, ExternalId, Designation, Name,
    /// FilePath, Configuration.
    /// <c>Quantity</c> здесь отсутствует намеренно: количество вхождений описывает связь
    /// позиции со сборкой-родителем, а не саму карточку, и в <c>1C_update_*.csv</c>
    /// всегда было бы 0 (для детали) или «плавающим» числом (для сборки). Реальное
    /// количество передаётся только в <c>1C_bom_*.csv</c>. Поле <c>Quantity</c> в модели
    /// зеркала сохранено — оно нужно для <c>GetTypeDocs</c> (fallback типа старых записей).
    /// </summary>
    internal static readonly string[] StructuralKeys =
    {
      "TypeDocs", "ExternalId", "Designation", "Name", "FilePath", "Configuration"
    };

    /// <summary>
    /// Текущая версия набора колонок по умолчанию.
    /// <b>2</b> — <c>Quantity</c> в наборе по умолчанию выключен (он больше не входит
    /// в хэш карточки). <b>3</b> — <c>Quantity</c> полностью убран из структурных полей
    /// карточки (см. <see cref="StructuralKeys"/>): колонка не просто выключается, а
    /// удаляется из набора, чтобы не возвращаться при нормализации.
    /// При обнаружении файла более старой версии состав правится один раз,
    /// дальше настройка полностью за оператором.
    /// </summary>
    internal const int CurrentLayoutFormatVersion = 3;

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
          { "Configuration", 110 }
        };

    /// <summary>Проверить, является ли поле структурным.</summary>
    /// <param name="field">Имя поля.</param>
    /// <returns>true, если поле структурное.</returns>
    internal static bool IsStructural(string field)
    {
      if (string.IsNullOrWhiteSpace(field)) return false;
      return StructuralKeys.Any(k =>
          string.Equals(k, field, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Проверить, является ли поле обязательным (нельзя выключить).</summary>
    /// <param name="field">Имя поля.</param>
    /// <returns>true, если поле обязательное.</returns>
    internal static bool IsMandatory(string field)
    {
      if (string.IsNullOrWhiteSpace(field)) return false;
      return MandatoryStructuralKeys.Any(k =>
          string.Equals(k, field, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Ширина структурной колонки по умолчанию (120, если ключ неизвестен).</summary>
    /// <param name="field">Имя структурного поля.</param>
    /// <returns>Ширина в пикселях.</returns>
    internal static int DefaultWidthFor(string field)
    {
      if (!string.IsNullOrWhiteSpace(field) && StructuralDefaultWidths.TryGetValue(field.Trim(), out int w))
        return w;
      return 120;
    }

    /// <summary>
    /// Создать layout по умолчанию: все структурные поля + все отслеживаемые свойства,
    /// порядок совпадает с прежней выгрузкой.
    /// </summary>
    /// <param name="trackedNames">Имена отслеживаемых свойств (в порядке добавления).</param>
    /// <returns>Новый layout.</returns>
    internal static VelumBomExchangeLayoutFile CreateDefault(IReadOnlyList<string> trackedNames)
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
          Enabled = true,
          Order = order++,
          Width = DefaultWidthFor(key)
        });
      }
      foreach (string name in NormalizeTrackedNames(trackedNames))
      {
        file.Columns.Add(new VelumBomExchangeColumnDef
        {
          Field = name,
          Header = name,
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
    /// удаляет tracked, которых больше нет в списке, удаляет осиротевшие структурные
    /// (снятые с поддержки — например, Quantity), переуплотняет Order, чинит пустые
    /// Header и ширины.
    /// </summary>
    /// <param name="data">Нормализуемый layout.</param>
    /// <param name="trackedNames">Актуальные имена отслеживаемых свойств.</param>
    internal static void Normalize(VelumBomExchangeLayoutFile data, IReadOnlyList<string> trackedNames)
    {
      if (data == null) return;
      if (data.Columns == null)
        data.Columns = new List<VelumBomExchangeColumnDef>();

      List<string> tracked = NormalizeTrackedNames(trackedNames);
      var trackedNameSet = new HashSet<string>(tracked, StringComparer.OrdinalIgnoreCase);

      // Удаляем tracked, которых больше нет в настройках свойств.
      data.Columns.RemoveAll(c =>
          c != null &&
          c.Source == VelumBomExchangeFieldSource.Tracked &&
          !trackedNameSet.Contains((c.Field ?? string.Empty).Trim()));

      // Удаляем структурные колонки, которых больше нет в StructuralKeys
      // (например, снятый с поддержки Quantity). Иначе они бы «жили» в файле
      // вечно: ниже добавляются отсутствующие, а лишние не убирались (E33).
      data.Columns.RemoveAll(c =>
          c != null &&
          c.Source == VelumBomExchangeFieldSource.Structural &&
          !IsStructural(c.Field));

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
      foreach (string name in tracked)
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

    /// <summary>
    /// Одноразово привести набор колонок к <see cref="CurrentLayoutFormatVersion"/>.
    /// Файлы, созданные до появления версии, имеют <c>0</c>.
    /// Возвращает true, если состав изменился и файл нужно перезаписать.
    /// </summary>
    /// <param name="data">Нормализованный layout.</param>
    /// <returns>true, если версию нужно поднять и перезаписать файл.</returns>
    internal static bool MigrateFormatVersion(VelumBomExchangeLayoutFile data)
    {
      if (data == null || data.LayoutFormatVersion == CurrentLayoutFormatVersion)
        return false;

      if (data.LayoutFormatVersion > CurrentLayoutFormatVersion)
      {
        // Файл из более новой версии — обратно колонки не «чиним», чтобы не
        // затереть осознанный выбор оператора.
        return false;
      }

      // Состав колонок (в т.ч. удаление снятого с поддержки Quantity) приводит
      // Normalize, уже выполненный до этого метода — здесь остаётся только
      // поднять записанный номер версии и тем самым инициировать перезапись файла.
      data.LayoutFormatVersion = CurrentLayoutFormatVersion;
      return true;
    }

    /// <summary>Уникализирует имена отслеживаемых свойств (обрезка пробелов, без регистра).</summary>
    private static List<string> NormalizeTrackedNames(IReadOnlyList<string> trackedNames)
    {
      var result = new List<string>();
      if (trackedNames == null)
        return result;

      var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (string raw in trackedNames)
      {
        if (string.IsNullOrWhiteSpace(raw)) continue;
        string name = raw.Trim();
        if (seen.Add(name))
          result.Add(name);
      }
      return result;
    }
  }
}
