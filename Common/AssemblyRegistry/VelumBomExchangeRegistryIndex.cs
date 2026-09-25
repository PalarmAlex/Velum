using System;
using System.Collections.Generic;
using ISIDA.Common;
using Velum.UI.ProductRegistry;

namespace Velum.UI.AssemblyRegistry
{
  /// <summary>
  /// Индекс нормализованных путей реестра изделий: отвечает на вопрос «зарегистрирована
  /// ли позиция BOM в реестре изделий».
  /// <para>
  /// Сверка идёт по пути файла, а не по обозначению: путь — единственный ключ, общий у
  /// зеркала карточек (<c>bomMirror.json</c>) и реестра. Обе стороны приводятся к одному
  /// виду одним и тем же <see cref="VelumProductRegistryStore.NormalizeFilePathKey"/>,
  /// поэтому различия регистра, разделителей и записи «относительно корня документов»
  /// не дают ложного «не в реестре».
  /// </para>
  /// </summary>
  internal sealed class VelumBomExchangeRegistryIndex
  {
    private readonly HashSet<string> _registeredPaths;

    private VelumBomExchangeRegistryIndex(
        HashSet<string> registeredPaths,
        bool available,
        int itemCount)
    {
      _registeredPaths = registeredPaths;
      Available = available;
      ItemCount = itemCount;
    }

    /// <summary>
    /// Удалось ли загрузить реестр изделий. При <c>false</c> сверка не применяется
    /// (см. <see cref="IsRegistered(string)"/>), а форма показывает причину.
    /// </summary>
    internal bool Available { get; }

    /// <summary>Число записей реестра, участвующих в сверке (для лога и статус-строки).</summary>
    internal int ItemCount { get; }

    /// <summary>
    /// Загрузить реестр изделий и построить индекс путей.
    /// Ошибка чтения не считается ошибкой обмена: индекс остаётся пустым,
    /// <see cref="Available"/> = <c>false</c>, и ни одна позиция не отбрасывается —
    /// недоступный каталог настроек не должен останавливать выгрузку в 1С.
    /// </summary>
    internal static VelumBomExchangeRegistryIndex Load()
    {
      var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      try
      {
        var store = new VelumProductRegistryStore();
        store.Load();

        foreach (string path in store.GetAllFilePaths())
        {
          if (!string.IsNullOrWhiteSpace(path))
            paths.Add(path);
        }

        return new VelumBomExchangeRegistryIndex(paths, true, paths.Count);
      }
      catch (Exception ex)
      {
        Logger.Warning(
            "Velum bomExchange: product registry unavailable, registry check skipped: " +
            ex.Message);
        return new VelumBomExchangeRegistryIndex(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            false,
            0);
      }
    }

    /// <summary>
    /// Есть ли файл позиции в реестре изделий.
    /// Пустой путь — позиции нечего сверять, она считается незарегистрированной.
    /// При недоступном реестре возвращается <c>true</c>, чтобы отбор ничего не отсекал.
    /// </summary>
    internal bool IsRegistered(string filePath)
    {
      if (!Available)
        return true;

      string key = VelumProductRegistryStore.NormalizeFilePathKey(filePath);
      return !string.IsNullOrEmpty(key) && _registeredPaths.Contains(key);
    }

    /// <summary>Есть ли запись зеркала карточек в реестре изделий.</summary>
    internal bool IsRegistered(VelumAssemblyBomMirrorEntry entry)
    {
      return entry != null && IsRegistered(entry.FilePath);
    }
  }
}
