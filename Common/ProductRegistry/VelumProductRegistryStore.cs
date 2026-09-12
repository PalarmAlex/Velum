using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ISIDA.Common;
using Newtonsoft.Json;
using Velum.Configuration;

namespace Velum.UI.ProductRegistry
{
  /// <summary>
  /// Хранилище реестра документов: JSON в каталоге <see cref="VelumAppConfig.ProductRegistryFolderPath"/>.
  /// Ключ учёта файла — нормализованный абсолютный путь; виртуальные папки — смысловая раскладка.
  /// Для ~10k записей данные держатся в памяти; выборка по каталогу — O(1) через индекс FolderId.
  /// </summary>
  internal sealed class VelumProductRegistryStore
  {
    private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
    {
      Formatting = Formatting.Indented,
      NullValueHandling = NullValueHandling.Ignore
    };

    private readonly Dictionary<int, VelumProductFolder> _folders = new Dictionary<int, VelumProductFolder>();
    private readonly Dictionary<int, VelumProductItem> _items = new Dictionary<int, VelumProductItem>();
    private readonly Dictionary<int, List<int>> _itemsByFolder = new Dictionary<int, List<int>>();
    private int _nextFolderId = 1;
    private int _nextItemId = 1;
    private bool _foldersDirty;
    private bool _itemsDirty;

    public static string RegistryFolderPath => VelumAppConfig.ProductRegistryFolderPath;

    public static string FoldersFilePath => Path.Combine(RegistryFolderPath, "folders.json");

    public static string ItemsFilePath => Path.Combine(RegistryFolderPath, "items.json");

    public IEnumerable<VelumProductFolder> Folders => _folders.Values;

    /// <summary>Все записи изделий (стабильный порядок по Id — детерминированный обход для сканеров).</summary>
    public IReadOnlyList<VelumProductItem> GetAllItems()
    {
      var list = new List<VelumProductItem>(_items.Count);
      foreach (KeyValuePair<int, VelumProductItem> kv in _items)
        list.Add(kv.Value);
      list.Sort((a, b) => a.Id.CompareTo(b.Id));
      return list;
    }

    public void Load()
    {
      Directory.CreateDirectory(RegistryFolderPath);
      _folders.Clear();
      _items.Clear();
      _itemsByFolder.Clear();

      VelumProductFolderFile folderFile = ReadJson<VelumProductFolderFile>(FoldersFilePath)
          ?? new VelumProductFolderFile();
      _nextFolderId = Math.Max(1, folderFile.NextId);
      if (folderFile.Folders != null)
      {
        foreach (VelumProductFolder folder in folderFile.Folders)
        {
          if (folder == null || folder.Id <= 0)
            continue;
          if (string.IsNullOrWhiteSpace(folder.Name))
            folder.Name = "Каталог";
          if (folder.Description == null)
            folder.Description = string.Empty;
          else
            folder.Description = folder.Description.Trim();
          _folders[folder.Id] = folder;
          if (folder.Id >= _nextFolderId)
            _nextFolderId = folder.Id + 1;
        }
      }

      VelumProductItemFile itemFile = ReadJson<VelumProductItemFile>(ItemsFilePath)
          ?? new VelumProductItemFile();
      _nextItemId = Math.Max(1, itemFile.NextId);
      if (itemFile.Items != null)
      {
        foreach (VelumProductItem item in itemFile.Items)
        {
          if (item == null || item.Id <= 0)
            continue;
          if (!_folders.ContainsKey(item.FolderId))
            continue;
          NormalizeItem(item);
          _items[item.Id] = item;
          if (item.Id >= _nextItemId)
            _nextItemId = item.Id + 1;
          GetOrCreateFolderItemList(item.FolderId).Add(item.Id);
        }
      }

      if (_folders.Count == 0)
      {
        var root = new VelumProductFolder
        {
          Id = _nextFolderId++,
          ParentId = 0,
          Name = "Изделия",
          Description = string.Empty,
          SortOrder = 0
        };
        _folders[root.Id] = root;
        _foldersDirty = true;
        Save();
      }

      _foldersDirty = false;
      _itemsDirty = false;
    }

    public void Save()
    {
      Directory.CreateDirectory(RegistryFolderPath);

      if (_foldersDirty)
      {
        var folderFile = new VelumProductFolderFile
        {
          NextId = _nextFolderId,
          Folders = _folders.Values
              .OrderBy(f => f.ParentId)
              .ThenBy(f => f.SortOrder)
              .ThenBy(f => f.Id)
              .ToArray()
        };
        WriteJsonAtomic(FoldersFilePath, folderFile);
        _foldersDirty = false;
      }

      if (_itemsDirty)
      {
        var itemFile = new VelumProductItemFile
        {
          NextId = _nextItemId,
          Items = _items.Values
              .OrderBy(i => i.FolderId)
              .ThenBy(i => i.Id)
              .ToArray()
        };
        WriteJsonAtomic(ItemsFilePath, itemFile);
        _itemsDirty = false;
      }

      VelumProductRegistryIntegrityScheduler.NotifyRegistryChanged();
    }

    public VelumProductFolder GetFolder(int id)
    {
      VelumProductFolder folder;
      return _folders.TryGetValue(id, out folder) ? folder : null;
    }

    public VelumProductItem GetItem(int id)
    {
      VelumProductItem item;
      return _items.TryGetValue(id, out item) ? item : null;
    }

    public IReadOnlyList<VelumProductFolder> GetChildFolders(int parentId)
    {
      return _folders.Values
          .Where(f => f.ParentId == parentId)
          .OrderBy(f => f.SortOrder)
          .ThenBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase)
          .ThenBy(f => f.Id)
          .ToList();
    }

    public IReadOnlyList<VelumProductItem> GetItemsInFolder(int folderId)
    {
      List<int> ids;
      if (!_itemsByFolder.TryGetValue(folderId, out ids) || ids.Count == 0)
        return Array.Empty<VelumProductItem>();

      var result = new List<VelumProductItem>(ids.Count);
      foreach (int id in ids)
      {
        VelumProductItem item;
        if (_items.TryGetValue(id, out item))
          result.Add(item);
      }

      return result;
    }

    /// <summary>
    /// Записи выбранного каталога и всех вложенных субкаталогов.
    /// </summary>
    public IReadOnlyList<VelumProductItem> GetItemsInFolderTree(int folderId)
    {
      if (!_folders.ContainsKey(folderId))
        return Array.Empty<VelumProductItem>();

      var result = new List<VelumProductItem>();
      CollectItemsInFolderTree(folderId, result);
      return result;
    }

    private void CollectItemsInFolderTree(int folderId, List<VelumProductItem> target)
    {
      target.AddRange(GetItemsInFolder(folderId));
      foreach (VelumProductFolder child in GetChildFolders(folderId))
        CollectItemsInFolderTree(child.Id, target);
    }

    public VelumProductFolder AddFolder(int parentId, string name, bool persist = true)
    {
      if (parentId != 0 && !_folders.ContainsKey(parentId))
        throw new InvalidOperationException("Родительский каталог не найден.");

      string folderName = string.IsNullOrWhiteSpace(name) ? "Новый каталог" : name.Trim();
      int sortOrder = GetChildFolders(parentId).Count;
      var folder = new VelumProductFolder
      {
        Id = _nextFolderId++,
        ParentId = parentId,
        Name = folderName,
        Description = string.Empty,
        SortOrder = sortOrder
      };
      _folders[folder.Id] = folder;
      _foldersDirty = true;
      if (persist)
        Save();
      return folder;
    }

    /// <summary>Ищет непосредственного потомка с указанным именем (без учёта регистра).</summary>
    public VelumProductFolder FindChildFolderByName(int parentId, string name)
    {
      if (string.IsNullOrWhiteSpace(name))
        return null;

      foreach (VelumProductFolder child in GetChildFolders(parentId))
      {
        if (string.Equals(child.Name, name.Trim(), StringComparison.CurrentCultureIgnoreCase))
          return child;
      }

      return null;
    }

    public VelumProductFolder GetOrCreateChildFolder(int parentId, string name, bool persist = true)
    {
      VelumProductFolder existing = FindChildFolderByName(parentId, name);
      if (existing != null)
        return existing;
      return AddFolder(parentId, name, persist);
    }

    public void RenameFolder(int folderId, string name)
    {
      VelumProductFolder folder = GetFolder(folderId);
      if (folder == null)
        return;

      string trimmed = (name ?? string.Empty).Trim();
      if (string.IsNullOrEmpty(trimmed))
        trimmed = folder.Name;

      if (string.Equals(folder.Name, trimmed, StringComparison.Ordinal))
        return;

      folder.Name = trimmed;
      _foldersDirty = true;
      Save();
    }

    public void SetFolderDescription(int folderId, string description)
    {
      VelumProductFolder folder = GetFolder(folderId);
      if (folder == null)
        return;

      string trimmed = (description ?? string.Empty).Trim();
      if (string.Equals(folder.Description ?? string.Empty, trimmed, StringComparison.Ordinal))
        return;

      folder.Description = trimmed;
      _foldersDirty = true;
      Save();
    }

    public bool MoveFolder(int folderId, int newParentId)
    {
      VelumProductFolder folder = GetFolder(folderId);
      if (folder == null)
        return false;
      if (folderId == newParentId)
        return false;
      if (newParentId != 0 && !_folders.ContainsKey(newParentId))
        return false;
      if (IsDescendant(newParentId, folderId))
        return false;

      if (folder.ParentId == newParentId)
        return true;

      folder.ParentId = newParentId;
      folder.SortOrder = GetChildFolders(newParentId).Count(f => f.Id != folderId);
      _foldersDirty = true;
      Save();
      return true;
    }

    /// <summary>
    /// Сдвигает каталог внутри своей дочерней группы на offset позиций
    /// (offset &lt; 0 — вверх, offset &gt; 0 — вниз).
    /// true — порядок изменён; false — сдвиг невозможен (край группы или каталог не найден).
    /// </summary>
    public bool MoveFolderRelative(int folderId, int offset)
    {
      VelumProductFolder folder = GetFolder(folderId);
      if (folder == null)
        return false;

      List<VelumProductFolder> siblings = GetChildFolders(folder.ParentId).ToList();
      int index = siblings.FindIndex(f => f.Id == folderId);
      if (index < 0)
        return false;

      int target = index + offset;
      if (target < 0 || target >= siblings.Count)
        return false;

      siblings.RemoveAt(index);
      siblings.Insert(target, folder);
      for (int i = 0; i < siblings.Count; i++)
        siblings[i].SortOrder = i;

      _foldersDirty = true;
      Save();
      return true;
    }

    public void DeleteFolderCascade(int folderId)
    {
      if (!_folders.ContainsKey(folderId))
        return;

      var toDelete = new List<int>();
      CollectDescendantFolderIds(folderId, toDelete);
      toDelete.Add(folderId);

      foreach (int id in toDelete)
      {
        List<int> itemIds;
        if (_itemsByFolder.TryGetValue(id, out itemIds))
        {
          foreach (int itemId in itemIds.ToArray())
            _items.Remove(itemId);
          _itemsByFolder.Remove(id);
          _itemsDirty = true;
        }

        _folders.Remove(id);
        _foldersDirty = true;
      }

      if (_folders.Count == 0)
      {
        var root = new VelumProductFolder
        {
          Id = _nextFolderId++,
          ParentId = 0,
          Name = "Изделия",
          Description = string.Empty,
          SortOrder = 0
        };
        _folders[root.Id] = root;
        _foldersDirty = true;
      }

      Save();
    }

    public VelumProductItem AddItem(int folderId, string designation, string name, string filePath, bool persist = true)
    {
      if (!_folders.ContainsKey(folderId))
        throw new InvalidOperationException("Каталог не найден.");

      EnsureFilePathUnique(filePath, excludeItemId: 0);

      var item = new VelumProductItem
      {
        Id = _nextItemId++,
        FolderId = folderId,
        Designation = designation,
        Name = name,
        FilePath = filePath,
        NeedDrawing = true
      };
      NormalizeItem(item);
      _items[item.Id] = item;
      GetOrCreateFolderItemList(folderId).Add(item.Id);
      _itemsDirty = true;
      if (persist)
        Save();
      return item;
    }

    public bool ContainsFilePath(string filePath)
    {
      return FindItemByFilePath(filePath) != null;
    }

    /// <summary>Запись с тем же нормализованным <see cref="VelumProductItem.FilePath"/>, иначе null.</summary>
    public VelumProductItem FindItemByFilePath(string filePath)
    {
      string path = NormalizeFilePathKey(filePath);
      if (string.IsNullOrEmpty(path))
        return null;

      foreach (VelumProductItem item in _items.Values)
      {
        if (string.Equals(NormalizeFilePathKey(item.FilePath), path, StringComparison.OrdinalIgnoreCase))
          return item;
      }

      return null;
    }

    public IEnumerable<string> GetAllFilePaths()
    {
      foreach (VelumProductItem item in _items.Values)
      {
        string path = NormalizeFilePathKey(item.FilePath);
        if (!string.IsNullOrEmpty(path))
          yield return path;
      }
    }

    /// <summary>Нормализует путь файла для сравнения при дедупликации.</summary>
    public static string NormalizeFilePathKey(string filePath)
    {
      string path = (filePath ?? string.Empty).Trim();
      if (string.IsNullOrEmpty(path))
        return string.Empty;

      // \?\C:\… и \?\UNC\server\share\… → обычный вид, иначе повторная индексация
      // того же файла даёт «новый» ключ и дублирует запись.
      if (path.StartsWith(@"\?\UNC\", StringComparison.OrdinalIgnoreCase))
        path = @"\\" + path.Substring(8);
      else if (path.StartsWith(@"\?\", StringComparison.OrdinalIgnoreCase))
        path = path.Substring(4);

      if (path.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
        path = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

      try
      {
        path = Path.GetFullPath(path);
      }
      catch
      {
      }

      // Единый разделитель — иначе один и тот же файл может пройти дедупликацию дважды
      // (EnumerateFiles vs путь из JSON) или, наоборот, ложно считаться «уже в реестре».
      if (path.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
        path = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

      return path;
    }

    public void UpdateItem(VelumProductItem item, bool persist = true)
    {
      if (item == null || !_items.ContainsKey(item.Id))
        return;
      if (!_folders.ContainsKey(item.FolderId))
        throw new InvalidOperationException("Каталог не найден.");

      NormalizeItem(item);
      EnsureFilePathUnique(item.FilePath, excludeItemId: item.Id);

      // Индекс может отличаться от item.FolderId, если вызывающий код
      // уже изменил FolderId у того же объекта, что лежит в _items.
      int oldFolderId = FindIndexedFolderId(item.Id);
      if (oldFolderId < 0)
        oldFolderId = item.FolderId;

      if (oldFolderId != item.FolderId)
      {
        List<int> oldList;
        if (_itemsByFolder.TryGetValue(oldFolderId, out oldList))
          oldList.Remove(item.Id);
        GetOrCreateFolderItemList(item.FolderId).Add(item.Id);
      }

      _items[item.Id] = item;
      _itemsDirty = true;
      if (persist)
        Save();
    }

    /// <summary>Тип документа по расширению связанного файла (например «.sldprt»).</summary>
    public static string GetDocumentTypeKey(string filePath)
    {
      string path = (filePath ?? string.Empty).Trim();
      if (string.IsNullOrEmpty(path))
        return string.Empty;

      try
      {
        return VelumProductRegistryFolderAutoNames.NormalizeExtension(Path.GetExtension(path));
      }
      catch
      {
        return string.Empty;
      }
    }

    /// <summary>
    /// Проверяет, является ли файл документом SolidWorks (part/assembly/drawing).
    /// </summary>
    public static bool IsSolidWorksFileExtension(string filePath)
    {
      if (string.IsNullOrWhiteSpace(filePath))
        return false;

      string ext = Path.GetExtension(filePath);
      return string.Equals(ext, ".sldprt", StringComparison.OrdinalIgnoreCase)
          || string.Equals(ext, ".sldasm", StringComparison.OrdinalIgnoreCase)
          || string.Equals(ext, ".slddrw", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Ключ учёта — нормализованный абсолютный путь. Пустой путь допускается
    /// у нескольких записей-заготовок; непустой путь должен быть уникален.
    /// </summary>
    private void EnsureFilePathUnique(string filePath, int excludeItemId)
    {
      string path = NormalizeFilePathKey(filePath);
      if (string.IsNullOrEmpty(path))
        return;

      VelumProductItem existing = FindItemByFilePath(path);
      if (existing == null || existing.Id == excludeItemId)
        return;

      throw new InvalidOperationException(
          "Файл уже есть в реестре документов (Id=" + existing.Id + "):\n" + path);
    }

    private int FindIndexedFolderId(int itemId)
    {
      foreach (KeyValuePair<int, List<int>> pair in _itemsByFolder)
      {
        if (pair.Value.Contains(itemId))
          return pair.Key;
      }

      return -1;
    }

    public bool MoveItem(int itemId, int newFolderId, bool persist = true)
    {
      VelumProductItem item;
      if (!_items.TryGetValue(itemId, out item))
        return false;
      if (!_folders.ContainsKey(newFolderId))
        return false;
      if (item.FolderId == newFolderId)
        return true;

      List<int> oldList;
      if (_itemsByFolder.TryGetValue(item.FolderId, out oldList))
        oldList.Remove(itemId);

      item.FolderId = newFolderId;
      GetOrCreateFolderItemList(newFolderId).Add(itemId);
      _itemsDirty = true;
      if (persist)
        Save();
      return true;
    }

    public int MoveItems(IEnumerable<int> itemIds, int newFolderId)
    {
      if (!_folders.ContainsKey(newFolderId))
        return 0;

      int moved = 0;
      foreach (int itemId in itemIds)
      {
        VelumProductItem item = GetItem(itemId);
        if (item == null || item.FolderId == newFolderId)
          continue;
        if (MoveItem(itemId, newFolderId, persist: false))
          moved++;
      }

      if (moved > 0)
        Save();
      return moved;
    }

    /// <summary>Путь каталога вида «Корень\Подкаталог\Узел».</summary>
    public string GetFolderPath(int folderId)
    {
      var parts = new List<string>();
      int current = folderId;
      while (current != 0)
      {
        VelumProductFolder folder = GetFolder(current);
        if (folder == null)
          break;
        parts.Insert(0, folder.Name ?? string.Empty);
        current = folder.ParentId;
      }

      return string.Join("\\", parts);
    }

    public void DeleteItem(int itemId)
    {
      VelumProductItem item;
      if (!_items.TryGetValue(itemId, out item))
        return;

      List<int> list;
      if (_itemsByFolder.TryGetValue(item.FolderId, out list))
        list.Remove(itemId);

      _items.Remove(itemId);
      _itemsDirty = true;
      Save();
    }

    private void CollectDescendantFolderIds(int folderId, List<int> target)
    {
      foreach (VelumProductFolder child in GetChildFolders(folderId))
      {
        CollectDescendantFolderIds(child.Id, target);
        target.Add(child.Id);
      }
    }

    private bool IsDescendant(int maybeChildId, int ancestorId)
    {
      int current = maybeChildId;
      while (current != 0)
      {
        if (current == ancestorId)
          return true;
        VelumProductFolder folder = GetFolder(current);
        if (folder == null)
          return false;
        current = folder.ParentId;
      }

      return false;
    }

    private List<int> GetOrCreateFolderItemList(int folderId)
    {
      List<int> list;
      if (!_itemsByFolder.TryGetValue(folderId, out list))
      {
        list = new List<int>();
        _itemsByFolder[folderId] = list;
      }

      return list;
    }

    private static void NormalizeItem(VelumProductItem item)
    {
      item.Designation = (item.Designation ?? string.Empty).Trim();
      item.Name = (item.Name ?? string.Empty).Trim();
      // Путь сразу в каноническом виде — чтобы knownPaths при индексации совпадал с JSON.
      string path = (item.FilePath ?? string.Empty).Trim();
      item.FilePath = string.IsNullOrEmpty(path) ? string.Empty : NormalizeFilePathKey(path);
    }

    private static T ReadJson<T>(string path) where T : class
    {
      try
      {
        if (!File.Exists(path))
          return null;
        string json = File.ReadAllText(path, Encoding.UTF8);
        if (string.IsNullOrWhiteSpace(json))
          return null;
        return JsonConvert.DeserializeObject<T>(json, JsonSettings);
      }
      catch (Exception ex)
      {
        Logger.Error("ProductRegistry load failed (" + path + "): " + ex.Message);
        return null;
      }
    }

    private static void WriteJsonAtomic(string path, object value)
    {
      string json = JsonConvert.SerializeObject(value, JsonSettings);
      string tempPath = path + ".tmp";
      File.WriteAllText(tempPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
      if (File.Exists(path))
        File.Replace(tempPath, path, null);
      else
        File.Move(tempPath, path);
    }
  }
}
