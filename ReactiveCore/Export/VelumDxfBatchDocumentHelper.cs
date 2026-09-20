using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using ISIDA.Common;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Velum.Configuration;
using Velum.ReactiveCore;
using Velum.ReactiveCore.Export;
using Velum.SolidHomeostasis;
using Xarial.XCad.SolidWorks;

namespace Velum.SolidHomeostasis
{
  /// <summary>Открытие/закрытие документов для пакетной диагностики и экспорта DXF.</summary>
  internal static class VelumDxfBatchDocumentHelper
  {
    private const int SwFileWithSameTitleAlreadyOpenError = (int)swFileLoadError_e.swFileWithSameTitleAlreadyOpen;

    /// <summary>
    /// Пути деталей, открытых самостоятельно (есть своё окно).
    /// Детали, загруженные только как компоненты открытой сборки/чертежа
    /// (<c>ModelDoc2.Visible</c> = false), не включаются — после пакетной
    /// операции их окно нужно закрыть.
    /// </summary>
    internal static HashSet<string> CollectOpenPartPaths(ISwApplication swApp)
    {
      var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      if (swApp?.Sw == null)
        return paths;

      try
      {
        ModelDoc2 doc = swApp.Sw.GetFirstDocument() as ModelDoc2;
        while (doc != null)
        {
          if (doc.GetType() == (int)swDocumentTypes_e.swDocPART &&
              doc.Visible)
          {
            string normalized = TryNormalizePartPath(doc.GetPathName());
            if (!string.IsNullOrWhiteSpace(normalized))
              paths.Add(normalized);
          }

          doc = doc.GetNext() as ModelDoc2;
        }
      }
      catch
      {
      }

      return paths;
    }

    /// <summary>
    /// Тихое открытие для диагностики: переиспользует уже загруженную деталь (в т.ч. из сборки), без активации окна.
    /// </summary>
    internal static ModelDoc2 TryOpenPartSilent(ISwApplication swApp, string partPath, out string error)
    {
      error = string.Empty;
      if (swApp?.Sw == null)
      {
        error = "SolidWorks недоступен";
        return null;
      }

      if (string.IsNullOrWhiteSpace(partPath) || !File.Exists(partPath))
      {
        error = "Файл не найден";
        return null;
      }

      ModelDoc2 existing = TryFindOpenPartByPath(swApp, partPath);
      if (existing != null)
        return existing;

      return TryOpenPartCore(swApp, partPath, silent: true, activate: false, out error);
    }

    /// <summary>
    /// Открытие для экспорта: всегда активирует деталь в отдельном окне, иначе нельзя установить вид проекции.
    /// </summary>
    internal static ModelDoc2 TryOpenPartForExport(ISwApplication swApp, string partPath, out string error)
    {
      error = string.Empty;
      if (swApp?.Sw == null)
      {
        error = "SolidWorks недоступен";
        return null;
      }

      if (string.IsNullOrWhiteSpace(partPath) || !File.Exists(partPath))
      {
        error = "Файл не найден";
        return null;
      }

      ModelDoc2 modelDoc = TryOpenPartCore(swApp, partPath, silent: true, activate: false, out error);
      if (modelDoc == null)
        return null;

      if (!TryActivateModelDoc(swApp, modelDoc, out error))
        return null;

      return modelDoc;
    }

    private static ModelDoc2 TryOpenPartCore(
        ISwApplication swApp,
        string partPath,
        bool silent,
        bool activate,
        out string error)
    {
      error = string.Empty;
      ModelDoc2 modelDoc = null;
      int openErrors = 0;
      int warnings = 0;

      try
      {
        int options = silent ? (int)swOpenDocOptions_e.swOpenDocOptions_Silent : 0;
        modelDoc = swApp.Sw.OpenDoc6(
            partPath,
            (int)swDocumentTypes_e.swDocPART,
            options,
            string.Empty,
            ref openErrors,
            ref warnings) as ModelDoc2;
      }
      catch (Exception ex)
      {
        error = ex.Message;
        return null;
      }

      if (modelDoc == null)
      {
        modelDoc = TryFindOpenPartByPath(swApp, partPath);
        if (modelDoc == null)
        {
          if (openErrors == SwFileWithSameTitleAlreadyOpenError)
            error = "Деталь уже открыта в SolidWorks";
          else
            error = "OpenDoc6 errors=" + openErrors + " warnings=" + warnings;
          return null;
        }
      }

      if (activate && !TryActivateModelDoc(swApp, modelDoc, out error))
        return null;

      return modelDoc;
    }

    /// <summary>
    /// Save → resync export-штампов → Save (несколько проходов).
    /// Иначе Save3 после Finalize поднимает GetUpdateStamp выше записанного штампа
    /// и пакетная диагностика снова показывает «Устарел».
    /// </summary>
    internal static void TryPersistAndResyncDxfExportStamps(
        ModelDoc2 modelDoc,
        IReadOnlyList<string> configNames)
    {
      if (modelDoc == null || configNames == null || configNames.Count == 0)
        return;

      for (int pass = 0; pass < 3; pass++)
      {
        TrySavePartSilent(modelDoc, out _, force: true);
        VelumExportDocumentationGeometryStampHelper.TryResyncPerConfigDxfGeometryStamps(
            modelDoc,
            configNames);
      }

      TrySavePartSilent(modelDoc, out _, force: true);
      VelumExportDocumentationGeometryStampHelper.TryResyncPerConfigDxfGeometryStamps(
          modelDoc,
          configNames);
      TrySavePartSilent(modelDoc, out _, force: true);
    }

    /// <summary>
    /// Сохраняет изменения и закрывает деталь, если её не было среди самостоятельно
    /// открытых (<paramref name="keepOpenPartPaths"/>) на момент начала операции.
    /// Для детали, загруженной только из сборки, CloseDoc убирает отдельное окно,
    /// оставляя документ в памяти как ссылку сборки.
    /// </summary>
    internal static void TryReleasePartAfterBatch(
        ISwApplication swApp,
        ModelDoc2 modelDoc,
        string partPath,
        bool persistChanges,
        ISet<string> keepOpenPartPaths)
    {
      if (modelDoc == null)
        return;

      try
      {
        if (persistChanges)
          TrySavePartSilent(modelDoc, out _);

        string normalized = TryNormalizePartPath(partPath);
        if (!string.IsNullOrWhiteSpace(normalized) &&
            keepOpenPartPaths != null &&
            keepOpenPartPaths.Contains(normalized))
          return;

        string title = modelDoc.GetTitle();
        if (!string.IsNullOrWhiteSpace(title) && swApp?.Sw != null)
          swApp.Sw.CloseDoc(title);
      }
      catch
      {
      }
    }

    internal static void TryCloseOpenPartsByPaths(
        ISwApplication swApp,
        IReadOnlyList<string> partPaths,
        ISet<string> keepOpenPartPaths)
    {
      if (swApp?.Sw == null || partPaths == null || partPaths.Count == 0)
        return;

      for (int i = 0; i < partPaths.Count; i++)
      {
        ModelDoc2 openDoc = TryFindOpenPartByPath(swApp, partPaths[i]);
        if (openDoc != null)
          TryReleasePartAfterBatch(swApp, openDoc, partPaths[i], persistChanges: false, keepOpenPartPaths);
      }
    }

    /// <summary>
    /// Silent Save3. При <paramref name="force"/> — даже если GetSaveFlag ложен
    /// (custom property иногда не поднимает dirty).
    /// </summary>
    internal static bool TrySavePartSilent(ModelDoc2 modelDoc, out string error, bool force = false)
    {
      error = string.Empty;
      if (modelDoc == null)
      {
        error = "Нет документа";
        return false;
      }

      try
      {
        if (!force && !modelDoc.GetSaveFlag())
          return true;

        int errors = 0;
        int warnings = 0;
        bool saved = modelDoc.Save3(
            (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
            ref errors,
            ref warnings);
        if (!saved)
        {
          error = "Save3 errors=" + errors + " warnings=" + warnings;
          return false;
        }

        return true;
      }
      catch (Exception ex)
      {
        error = ex.Message;
        return false;
      }
    }

    internal static ModelDoc2 TryFindOpenPartByPath(ISwApplication swApp, string partPath)
    {
      if (swApp?.Sw == null || string.IsNullOrWhiteSpace(partPath))
        return null;

      string expectedPath = TryNormalizePartPath(partPath);
      if (string.IsNullOrWhiteSpace(expectedPath))
        return null;

      ModelDoc2 fromApi = TryGetOpenDocumentByPath(swApp, expectedPath);
      if (fromApi != null)
        return fromApi;

      try
      {
        ModelDoc2 active = swApp.Sw.IActiveDoc2 as ModelDoc2;
        if (active != null &&
            active.GetType() == (int)swDocumentTypes_e.swDocPART &&
            PathsLikelySame(expectedPath, active.GetPathName()))
          return active;

        ModelDoc2 doc = swApp.Sw.GetFirstDocument() as ModelDoc2;
        while (doc != null)
        {
          if (doc.GetType() == (int)swDocumentTypes_e.swDocPART &&
              PathsLikelySame(expectedPath, doc.GetPathName()))
            return doc;

          doc = doc.GetNext() as ModelDoc2;
        }
      }
      catch
      {
      }

      return null;
    }

    internal static bool TryReadNeedDxf(ModelDoc2 modelDoc, string configName, out bool needDxf)
    {
      bool exists;
      return VelumDxfNeedFlagResolver.TryRead(modelDoc, configName, out needDxf, out exists) && exists;
    }

    /// <summary>
    /// Совместимость: true, если хотя бы одна конфигурация с «Нужен dxf = Yes».
    /// </summary>
    internal static bool TryReadNeedDxf(ModelDoc2 modelDoc, out bool needDxf)
    {
      needDxf = VelumDxfNeedFlagResolver.HasAnyExportable(modelDoc);
      if (needDxf)
        return true;

      bool dummy;
      bool exists;
      return VelumDxfNeedFlagResolver.TryRead(modelDoc, null, out dummy, out exists) && exists;
    }

    /// <summary>
    /// На детали создаёт «Нужен dxf» по умолчанию из настроек (<see cref="VelumAppConfig.NeedDxfDefault"/>),
    /// если свойства ещё нет. На сборке не пишет.
    /// Пишет только на общую вкладку — fallback для конфигураций без своего свойства.
    /// </summary>
    internal static bool TryEnsureNeedDxfDefaultIfPart(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return false;

      try
      {
        if (modelDoc.GetType() != (int)swDocumentTypes_e.swDocPART)
          return true;
      }
      catch
      {
        return false;
      }

      bool defaultNeed = VelumAppConfig.NeedDxfDefault;

      if (VelumYesOrNoCustomPropertyWriter.Ensure(
              modelDoc,
              VelumExportDocumentationProperties.NeedDxf,
              defaultNeed,
              out string message))
        return true;

      if (!string.IsNullOrWhiteSpace(message) &&
          !string.Equals(message, "already_exists", StringComparison.OrdinalIgnoreCase))
      {
        Logger.Warning("Velum NeedDxf ensure default: " + message);
      }

      return false;
    }

    /// <summary>
    /// На детали создаёт «Нужен dxf = No», если свойства ещё нет. На сборке не пишет.
    /// Пишет только на общую вкладку — fallback для конфигураций без своего свойства.
    /// </summary>
    [Obsolete("Используйте TryEnsureNeedDxfDefaultIfPart для чтения значения из настроек.")]
    internal static bool TryEnsureNeedDxfDefaultNoIfPart(ModelDoc2 modelDoc)
    {
      return TryEnsureNeedDxfDefaultIfPart(modelDoc);
    }

    internal static bool TrySetNeedDxf(ModelDoc2 modelDoc, bool needDxf, out string error)
    {
      return TrySetNeedDxf(modelDoc, needDxf, null, out error);
    }

    internal static bool TrySetNeedDxf(ModelDoc2 modelDoc, bool needDxf, string configName, out string error)
    {
      error = string.Empty;
      if (modelDoc == null)
      {
        error = "Нет документа";
        return false;
      }

      try
      {
        if (modelDoc.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY)
        {
          error = "Свойство «Нужен dxf» не записывается для сборок";
          return false;
        }

        if (modelDoc.GetType() != (int)swDocumentTypes_e.swDocPART)
        {
          error = "Свойство «Нужен dxf» только для детали";
          return false;
        }
      }
      catch
      {
        error = "Не удалось определить тип документа";
        return false;
      }

      return VelumYesOrNoCustomPropertyWriter.TryWrite(
          modelDoc,
          VelumExportDocumentationProperties.NeedDxf,
          needDxf,
          configName,
          out error);
    }

    internal static bool TryActivateOrOpenPartVisible(ISwApplication swApp, string partPath, out string error)
    {
      error = string.Empty;
      if (swApp?.Sw == null)
      {
        error = "SolidWorks недоступен";
        return false;
      }

      if (string.IsNullOrWhiteSpace(partPath) || !File.Exists(partPath))
      {
        error = "Файл не найден";
        return false;
      }

      ModelDoc2 existing = TryFindOpenPartByPath(swApp, partPath);
      if (existing != null)
        return TryActivateModelDoc(swApp, existing, out error);

      try
      {
        int errors = 0;
        int warnings = 0;
        ModelDoc2 opened = swApp.Sw.OpenDoc6(
            partPath,
            (int)swDocumentTypes_e.swDocPART,
            0,
            string.Empty,
            ref errors,
            ref warnings) as ModelDoc2;

        if (opened != null)
          return true;

        existing = TryFindOpenPartByPath(swApp, partPath);
        if (existing != null)
          return TryActivateModelDoc(swApp, existing, out error);

        if (errors == SwFileWithSameTitleAlreadyOpenError)
          error = "Деталь уже открыта в SolidWorks";
        else
          error = "OpenDoc6 errors=" + errors + " warnings=" + warnings;
        return false;
      }
      catch (Exception ex)
      {
        error = ex.Message;
        return false;
      }
    }

    private static bool TryActivateModelDoc(ISwApplication swApp, ModelDoc2 modelDoc, out string error)
    {
      error = string.Empty;
      if (modelDoc == null)
      {
        error = "Нет документа";
        return false;
      }

      try
      {
        string title = modelDoc.GetTitle();
        if (string.IsNullOrWhiteSpace(title))
        {
          error = "Не удалось определить заголовок документа";
          return false;
        }

        int activateErrors = 0;
        swApp.Sw.ActivateDoc3(title, true, (int)swRebuildOnActivation_e.swDontRebuildActiveDoc, ref activateErrors);
        return true;
      }
      catch (Exception ex)
      {
        error = ex.Message;
        return false;
      }
    }

    internal static string TryReadDxfCatalog(ModelDoc2 modelDoc)
    {
      CustomPropertyManager cpm = VelumRecipeSolidWorksCustomProperties.TryGetManager(modelDoc, "document");
      if (cpm == null)
        return string.Empty;

      if (!VelumRecipeSolidWorksCustomProperties.TryGetValue(
              cpm,
              VelumExportDocumentationProperties.DxfPath,
              out string raw))
        return string.Empty;

      return VelumDxfArtifactResolver.NormalizeCatalogPath(raw, modelDoc);
    }

    internal static string TryNormalizePartPath(string partPath)
    {
      if (string.IsNullOrWhiteSpace(partPath))
        return string.Empty;

      try
      {
        // Относительное хранимое значение достраивается по префиксу корневого каталога
        // до GetFullPath (иначе путь развернётся от текущего каталога процесса).
        string full = VelumRelativeDocumentPathResolver.ToFull(partPath.Trim());
        string fullPath = Path.GetFullPath(full);
        string longPath = TryGetLongPath(fullPath);
        return string.IsNullOrWhiteSpace(longPath) ? fullPath : longPath;
      }
      catch
      {
        return string.Empty;
      }
    }

    private static ModelDoc2 TryGetOpenDocumentByPath(ISwApplication swApp, string expectedPath)
    {
      if (swApp?.Sw == null || string.IsNullOrWhiteSpace(expectedPath))
        return null;

      string[] candidates =
      {
        expectedPath,
        TryGetLongPath(expectedPath)
      };

      for (int i = 0; i < candidates.Length; i++)
      {
        string candidate = candidates[i];
        if (string.IsNullOrWhiteSpace(candidate))
          continue;

        try
        {
          object doc = swApp.Sw.GetOpenDocumentByName(candidate);
          ModelDoc2 modelDoc = doc as ModelDoc2;
          if (modelDoc != null &&
              modelDoc.GetType() == (int)swDocumentTypes_e.swDocPART &&
              PathsLikelySame(expectedPath, modelDoc.GetPathName()))
            return modelDoc;
        }
        catch
        {
        }
      }

      string fileName = Path.GetFileName(expectedPath);
      if (string.IsNullOrWhiteSpace(fileName))
        return null;

      try
      {
        object doc = swApp.Sw.GetOpenDocumentByName(fileName);
        ModelDoc2 modelDoc = doc as ModelDoc2;
        if (modelDoc != null &&
            modelDoc.GetType() == (int)swDocumentTypes_e.swDocPART &&
            PathsLikelySame(expectedPath, modelDoc.GetPathName()))
          return modelDoc;
      }
      catch
      {
      }

      return null;
    }

    private static bool PathsLikelySame(string expectedPath, string openPath)
    {
      string normalizedExpected = TryNormalizePartPath(expectedPath);
      string normalizedOpen = TryNormalizePartPath(openPath);
      if (string.IsNullOrWhiteSpace(normalizedExpected) || string.IsNullOrWhiteSpace(normalizedOpen))
        return false;

      if (string.Equals(normalizedExpected, normalizedOpen, StringComparison.OrdinalIgnoreCase))
        return true;

      string expectedLong = TryGetLongPath(normalizedExpected);
      string openLong = TryGetLongPath(normalizedOpen);
      if (!string.IsNullOrWhiteSpace(expectedLong) &&
          !string.IsNullOrWhiteSpace(openLong) &&
          string.Equals(expectedLong, openLong, StringComparison.OrdinalIgnoreCase))
        return true;

      string expectedFile = Path.GetFileName(normalizedExpected);
      string openFile = Path.GetFileName(normalizedOpen);
      if (string.IsNullOrWhiteSpace(expectedFile) ||
          string.IsNullOrWhiteSpace(openFile) ||
          !string.Equals(expectedFile, openFile, StringComparison.OrdinalIgnoreCase))
        return false;

      string expectedDir = Path.GetFileName(Path.GetDirectoryName(normalizedExpected));
      string openDir = Path.GetFileName(Path.GetDirectoryName(normalizedOpen));
      return !string.IsNullOrWhiteSpace(expectedDir) &&
             !string.IsNullOrWhiteSpace(openDir) &&
             string.Equals(expectedDir, openDir, StringComparison.OrdinalIgnoreCase);
    }

    private static string TryGetLongPath(string path)
    {
      if (string.IsNullOrWhiteSpace(path))
        return string.Empty;

      try
      {
        var builder = new StringBuilder(512);
        uint length = GetLongPathName(path.Trim(), builder, (uint)builder.Capacity);
        if (length == 0)
          return string.Empty;
        if (length >= builder.Capacity)
        {
          builder = new StringBuilder((int)length + 1);
          length = GetLongPathName(path.Trim(), builder, (uint)builder.Capacity);
          if (length == 0)
            return string.Empty;
        }

        return builder.ToString();
      }
      catch
      {
        return string.Empty;
      }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetLongPathName(string lpszShortPath, StringBuilder lpszLongPath, uint cchBuffer);
  }
}
