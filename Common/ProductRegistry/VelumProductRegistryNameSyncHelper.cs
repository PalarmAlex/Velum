using System;
using System.IO;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Velum.ReactiveCore;
using Xarial.XCad.SolidWorks;

namespace Velum.UI.ProductRegistry
{
  /// <summary>
  /// Чтение свойства «Наименование» из документа SolidWorks для синхронизации поля <c>Name</c> реестра.
  /// </summary>
  internal static class VelumProductRegistryNameSyncHelper
  {
    internal const string NameProperty = "Наименование";

    /// <summary>
    /// Документы, для которых допускается синхронизация «Наименование» → Name:
    /// только детали и сборки (чертежи и прочие форматы игнорируются).
    /// </summary>
    internal static bool IsNameSyncSupported(string filePath)
    {
      string ext = VelumProductRegistryStore.GetDocumentTypeKey(filePath);
      return string.Equals(ext, ".sldprt", StringComparison.OrdinalIgnoreCase)
          || string.Equals(ext, ".sldasm", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Читает «Наименование»: сначала вкладка документа, при пустом значении — активная конфигурация.
    /// Уже открытый документ не закрывается.
    /// </summary>
    /// <returns>
    /// true, если документ удалось открыть или найти среди открытых;
    /// <paramref name="name"/> может быть пустым.
    /// </returns>
    internal static bool TryReadDesignationName(
        ISwApplication swApp,
        string filePath,
        out string name,
        out string error)
    {
      name = string.Empty;
      error = string.Empty;

      if (swApp?.Sw == null)
      {
        error = "SolidWorks недоступен";
        return false;
      }

      string path = VelumProductRegistryStore.NormalizeFilePathKey(filePath);
      if (string.IsNullOrEmpty(path))
      {
        error = "Путь пуст";
        return false;
      }

      if (!IsNameSyncSupported(path))
      {
        error = "Не деталь и не сборка SolidWorks";
        return false;
      }

      ModelDoc2 modelDoc;
      bool openedByUs;
      if (!TryGetOrOpenDocument(swApp, path, out modelDoc, out openedByUs, out error))
        return false;

      try
      {
        name = ReadNameProperty(modelDoc);
        return true;
      }
      catch (Exception ex)
      {
        error = ex.Message;
        return false;
      }
      finally
      {
        TryCloseIfOpened(swApp, modelDoc, openedByUs);
      }
    }

    /// <summary>
    /// Ищет документ среди уже открытых (включая загруженные компоненты сборок
    /// через <c>GetOpenDocumentByName</c>). OpenDoc — только если в сессии нет.
    /// </summary>
    internal static bool TryGetOrOpenDocument(
        ISwApplication swApp,
        string filePath,
        out ModelDoc2 modelDoc,
        out bool openedByUs,
        out string error)
    {
      modelDoc = null;
      openedByUs = false;
      error = string.Empty;

      if (swApp?.Sw == null)
      {
        error = "SolidWorks недоступен";
        return false;
      }

      string path = VelumProductRegistryStore.NormalizeFilePathKey(filePath);
      if (string.IsNullOrEmpty(path))
      {
        error = "Путь пуст";
        return false;
      }

      modelDoc = TryFindOpenDocumentByPath(swApp, path);
      if (modelDoc != null)
        return true;

      if (!File.Exists(path))
      {
        error = "Файл не найден";
        return false;
      }

      if (!TryResolveDocumentType(path, out int docType, out error))
        return false;

      try
      {
        int openErrors = 0;
        int warnings = 0;
        modelDoc = swApp.Sw.OpenDoc6(
            path,
            docType,
            (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
            string.Empty,
            ref openErrors,
            ref warnings) as ModelDoc2;

        if (modelDoc == null)
          modelDoc = TryFindOpenDocumentByPath(swApp, path);

        if (modelDoc == null)
        {
          error = "OpenDoc6 errors=" + openErrors + " warnings=" + warnings;
          return false;
        }

        openedByUs = true;
        return true;
      }
      catch (Exception ex)
      {
        error = ex.Message;
        modelDoc = null;
        return false;
      }
    }

    internal static void TryCloseIfOpened(ISwApplication swApp, ModelDoc2 modelDoc, bool openedByUs)
    {
      if (openedByUs && modelDoc != null)
        TryCloseDocument(swApp, modelDoc);
    }

    /// <summary>
    /// Читает «Наименование» только из уже открытого документа (без <c>OpenDoc6</c>).
    /// Если <paramref name="modelDocOrNull"/> задан и путь совпадает — используется он;
    /// иначе поиск среди открытых документов.
    /// </summary>
    internal static bool TryReadNameFromOpenDocumentOnly(
        ISwApplication swApp,
        string filePath,
        ModelDoc2 modelDocOrNull,
        out string name,
        out string error)
    {
      name = string.Empty;
      error = string.Empty;

      string path = VelumProductRegistryStore.NormalizeFilePathKey(filePath);
      if (string.IsNullOrEmpty(path))
      {
        error = "Путь пуст";
        return false;
      }

      if (!IsNameSyncSupported(path))
      {
        error = "Не деталь и не сборка SolidWorks";
        return false;
      }

      ModelDoc2 modelDoc = modelDocOrNull;
      if (modelDoc != null)
      {
        string openPath = string.Empty;
        try
        {
          openPath = modelDoc.GetPathName();
        }
        catch
        {
          openPath = string.Empty;
        }

        if (!PathsLikelySame(path, openPath))
          modelDoc = null;
      }

      if (modelDoc == null)
        modelDoc = TryFindOpenDocumentByPath(swApp, path);

      if (modelDoc == null)
      {
        error = "Документ не открыт";
        return false;
      }

      try
      {
        name = ReadNameProperty(modelDoc);
        return true;
      }
      catch (Exception ex)
      {
        error = ex.Message;
        return false;
      }
    }

    internal static string ReadNameProperty(ModelDoc2 modelDoc)
    {
      CustomPropertyManager docCpm =
          VelumRecipeSolidWorksCustomProperties.TryGetManager(modelDoc, "document");
      if (docCpm != null &&
          VelumRecipeSolidWorksCustomProperties.TryGetValue(docCpm, NameProperty, out string docValue))
      {
        string trimmed = (docValue ?? string.Empty).Trim();
        if (trimmed.Length > 0)
          return trimmed;
      }

      CustomPropertyManager activeCpm =
          VelumRecipeSolidWorksCustomProperties.TryGetManager(modelDoc, "active");
      if (activeCpm != null &&
          VelumRecipeSolidWorksCustomProperties.TryGetValue(activeCpm, NameProperty, out string activeValue))
      {
        return (activeValue ?? string.Empty).Trim();
      }

      return string.Empty;
    }

    private static bool TryResolveDocumentType(string path, out int docType, out string error)
    {
      docType = 0;
      error = string.Empty;
      string ext = VelumProductRegistryStore.GetDocumentTypeKey(path);
      if (string.Equals(ext, ".sldprt", StringComparison.OrdinalIgnoreCase))
      {
        docType = (int)swDocumentTypes_e.swDocPART;
        return true;
      }

      if (string.Equals(ext, ".sldasm", StringComparison.OrdinalIgnoreCase))
      {
        docType = (int)swDocumentTypes_e.swDocASSEMBLY;
        return true;
      }

      if (string.Equals(ext, ".slddrw", StringComparison.OrdinalIgnoreCase))
      {
        docType = (int)swDocumentTypes_e.swDocDRAWING;
        return true;
      }

      error = "Неизвестный тип документа";
      return false;
    }

    /// <summary>Ищет уже открытый документ по нормализованному пути (без OpenDoc).</summary>
    internal static ModelDoc2 TryFindOpenDocumentByPath(ISwApplication swApp, string filePath)
    {
      if (swApp?.Sw == null || string.IsNullOrWhiteSpace(filePath))
        return null;

      string expected = NormalizePathSafe(filePath);
      if (string.IsNullOrEmpty(expected))
        return null;

      try
      {
        object byName = swApp.Sw.GetOpenDocumentByName(expected);
        ModelDoc2 fromApi = byName as ModelDoc2;
        if (fromApi != null && PathsLikelySame(expected, fromApi.GetPathName()))
          return fromApi;
      }
      catch
      {
      }

      try
      {
        string fileName = Path.GetFileName(expected);
        if (!string.IsNullOrWhiteSpace(fileName))
        {
          object byFileName = swApp.Sw.GetOpenDocumentByName(fileName);
          ModelDoc2 fromName = byFileName as ModelDoc2;
          if (fromName != null && PathsLikelySame(expected, fromName.GetPathName()))
            return fromName;
        }
      }
      catch
      {
      }

      try
      {
        ModelDoc2 active = swApp.Sw.IActiveDoc2 as ModelDoc2;
        if (active != null && PathsLikelySame(expected, active.GetPathName()))
          return active;

        ModelDoc2 doc = swApp.Sw.GetFirstDocument() as ModelDoc2;
        while (doc != null)
        {
          if (PathsLikelySame(expected, doc.GetPathName()))
            return doc;
          doc = doc.GetNext() as ModelDoc2;
        }
      }
      catch
      {
      }

      return null;
    }

    private static void TryCloseDocument(ISwApplication swApp, ModelDoc2 modelDoc)
    {
      if (swApp?.Sw == null || modelDoc == null)
        return;

      try
      {
        string title = modelDoc.GetTitle();
        if (string.IsNullOrWhiteSpace(title))
        {
          string pathName = modelDoc.GetPathName();
          title = string.IsNullOrWhiteSpace(pathName)
              ? string.Empty
              : Path.GetFileName(pathName);
        }

        if (!string.IsNullOrWhiteSpace(title))
          swApp.Sw.CloseDoc(title);
      }
      catch
      {
      }
    }

    private static string NormalizePathSafe(string path)
    {
      try
      {
        return Path.GetFullPath((path ?? string.Empty).Trim());
      }
      catch
      {
        return (path ?? string.Empty).Trim();
      }
    }

    private static bool PathsLikelySame(string expectedNormalized, string actual)
    {
      if (string.IsNullOrWhiteSpace(actual))
        return false;
      string other = NormalizePathSafe(actual);
      return string.Equals(expectedNormalized, other, StringComparison.OrdinalIgnoreCase);
    }
  }
}
