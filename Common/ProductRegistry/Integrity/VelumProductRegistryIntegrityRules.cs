using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Velum.UI.ProductRegistry
{
  /// <summary>Правила поиска головного каталога и папки чертежей по folderAutoNames.</summary>
  internal static class VelumProductRegistryIntegrityRules
  {
    internal static bool IsPartOrAssemblyPath(string filePath)
    {
      string ext = GetExtension(filePath);
      return string.Equals(ext, ".sldprt", StringComparison.OrdinalIgnoreCase)
          || string.Equals(ext, ".sldasm", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsPartPath(string filePath)
    {
      return string.Equals(GetExtension(filePath), ".sldprt", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsDrawingPath(string filePath)
    {
      return string.Equals(GetExtension(filePath), ".slddrw", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsPdfPath(string filePath)
    {
      return string.Equals(GetExtension(filePath), ".pdf", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsDxfPath(string filePath)
    {
      return string.Equals(GetExtension(filePath), ".dxf", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Чертёж, PDF или DXF — цели копирования «Наименование» с одноименной детали/сборки.</summary>
    internal static bool IsRelatedDocumentPath(string filePath)
    {
      return IsDrawingPath(filePath) || IsPdfPath(filePath) || IsDxfPath(filePath);
    }

internal static string GetExtension(string filePath)
    {
      // Расширение не зависит от корневого каталога — полная нормализация
      // (развертывание относительного пути + GetFullPath) здесь не нужна:
      // она читала Settings.xml/резолвила корень на каждую проверку типа.
      string path = (filePath ?? string.Empty).Trim();
      if (path.StartsWith(@"\?\UNC\", StringComparison.OrdinalIgnoreCase))
        path = @"\" + path.Substring(8);
      else if (path.StartsWith(@"\?\", StringComparison.OrdinalIgnoreCase))
        path = path.Substring(4);

      if (string.IsNullOrEmpty(path))
        return string.Empty;
      try
      {
        return (Path.GetExtension(path) ?? string.Empty).Trim().ToLowerInvariant();
      }
      catch
      {
        return string.Empty;
      }
    }

    internal static string ResolveFolderNameForExtension(
        IList<VelumProductFolderAutoNameMapping> mappings,
        string extension)
    {
      string ext = NormalizeExtension(extension);
      if (mappings == null || ext.Length == 0)
        return string.Empty;

      foreach (VelumProductFolderAutoNameMapping m in mappings)
      {
        if (m == null)
          continue;
        if (string.Equals(NormalizeExtension(m.Extension), ext, StringComparison.OrdinalIgnoreCase))
          return (m.FolderName ?? string.Empty).Trim();
      }

      return string.Empty;
    }

    /// <summary>
    /// Подъём от folderId до первого предка, у которого среди детей есть
    /// субкаталог деталей или сборок и субкаталог чертежей (имена из folderAutoNames).
    /// </summary>
    internal static int FindHeadCatalogId(
        VelumProductRegistryStore store,
        int folderId,
        IList<VelumProductFolderAutoNameMapping> mappings)
    {
      if (store == null || folderId <= 0)
        return 0;

      string partsName = ResolveFolderNameForExtension(mappings, ".sldprt");
      string asmName = ResolveFolderNameForExtension(mappings, ".sldasm");
      string drwName = ResolveFolderNameForExtension(mappings, ".slddrw");
      if (string.IsNullOrEmpty(drwName))
        return 0;

      int current = folderId;
      int guard = 0;
      while (current > 0 && guard++ < 256)
      {
        if (IsHeadCatalog(store, current, partsName, asmName, drwName))
          return current;

        VelumProductFolder folder = store.GetFolder(current);
        if (folder == null)
          break;
        current = folder.ParentId;
      }

      return 0;
    }

    internal static int FindDrawingsFolderId(
        VelumProductRegistryStore store,
        int headCatalogId,
        IList<VelumProductFolderAutoNameMapping> mappings)
    {
      if (store == null || headCatalogId <= 0)
        return 0;

      string drwName = ResolveFolderNameForExtension(mappings, ".slddrw");
      if (string.IsNullOrEmpty(drwName))
        return 0;

      VelumProductFolder found = store.FindChildFolderByName(headCatalogId, drwName);
      return found != null ? found.Id : 0;
    }

/// <summary>
    /// Путь считается «битым» только при явном ответе «нет файла» (No).
    /// Unknown (таймаут проверки сетевого пути) missing'ом НЕ считается:
    /// по таймауту нельзя судить о существовании файла — недоступный шар по VPN
    /// не должен создавать тысячи ложных проблем BrokenLink.
    /// </summary>
    internal static bool PathExistsOrTimedOutIsMissing(string filePath)
    {
      VelumProductRegistryPathStatus status = VelumProductRegistryPathChecker.CheckOne(
          filePath,
          VelumProductRegistryPathChecker.TimeoutMilliseconds);
      return status == VelumProductRegistryPathStatus.No;
    }

    private static bool IsHeadCatalog(
        VelumProductRegistryStore store,
        int folderId,
        string partsName,
        string asmName,
        string drwName)
    {
      IReadOnlyList<VelumProductFolder> children = store.GetChildFolders(folderId);
      bool hasModelChild = false;
      bool hasDrawingChild = false;
      foreach (VelumProductFolder child in children)
      {
        if (child == null)
          continue;
        string name = (child.Name ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(drwName) &&
            string.Equals(name, drwName, StringComparison.OrdinalIgnoreCase))
          hasDrawingChild = true;
        if ((!string.IsNullOrEmpty(partsName) &&
             string.Equals(name, partsName, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrEmpty(asmName) &&
             string.Equals(name, asmName, StringComparison.OrdinalIgnoreCase)))
          hasModelChild = true;
      }

      return hasModelChild && hasDrawingChild;
    }

    private static string NormalizeExtension(string extension)
    {
      string ext = (extension ?? string.Empty).Trim().ToLowerInvariant();
      if (ext.Length == 0)
        return string.Empty;
      if (!ext.StartsWith(".", StringComparison.Ordinal))
        ext = "." + ext;
      return ext;
    }
  }
}
