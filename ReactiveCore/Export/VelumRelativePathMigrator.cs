using System;
using System.Collections.Generic;
using System.IO;
using ISIDA.Common;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Xarial.XCad.SolidWorks;

namespace Velum.ReactiveCore.Export
{
  /// <summary>
  /// Перевод абсолютных путей в свойствах документов SolidWorks («путь чертежа», «Путь pdf»,
  /// «Путь dxf») в относительные — срезом префикса корневого каталога
  /// (<see cref="VelumRelativeDocumentPathResolver.ToStored"/>).
  /// Документы открываются без окон (Silent), изменённые сохраняются (<c>Save3</c>) и закрываются,
  /// если были открыты этим мигратором. При незаполненной настройке префиксов ничего не делает.
  /// </summary>
  internal static class VelumRelativePathMigrator
  {
    /// <summary>Максимум строк в отчёте об ошибках (остальные лишь считаются).</summary>
    private const int MaxReportedErrors = 40;

    /// <summary>Свойства документа, подвергаемые переводу в относительные.</summary>
    private static readonly string[] TargetProperties =
    {
      VelumExportDocumentationProperties.DrawingPath,
      VelumExportDocumentationProperties.DxfPath,
      VelumExportDocumentationProperties.PdfPath
    };

    /// <summary>Итог прогона мигратора.</summary>
    internal sealed class Result
    {
      /// <summary>Сколько путей просмотрено.</summary>
      public int Scanned;

      /// <summary>Документов, в которых изменено хотя бы одно свойство и файл сохранён.</summary>
      public int ConvertedDocuments;

      /// <summary>Суммарно свойств переведено в относительные.</summary>
      public int PropertiesConverted;

      /// <summary>Документов без изменений (пусть уже относительные или вне корня).</summary>
      public int UnchangedDocuments;

      /// <summary>Пропущено: файл не найден / не удалось открыть.</summary>
      public int SkippedMissing;

      /// <summary>Ошибок (нет менеджера свойств, не удалось сохранить, исключение).</summary>
      public int Failed;

      /// <summary>Прогон прерван пользователем.</summary>
      public bool Stopped;

      /// <summary>Нормализованные пути документов, успешно переведённых и сохранённых.</summary>
      public readonly List<string> ConvertedPaths = new List<string>();

      /// <summary>Краткие сообщения об ошибках (не более <see cref="MaxReportedErrors"/>).</summary>
      public readonly List<string> Errors = new List<string>();
    }

    /// <summary>
    /// Выполняет перевод путей в относительные для перечня документов.
    /// Вызывать из UI-потока SolidWorks (STA): внутри нет потоков, только последовательный обход.
    /// </summary>
    /// <param name="swApp">Сессия SolidWorks.</param>
    /// <param name="documentPaths">Пути документов реестра (ключи <c>FilePath</c> — относительные либо абсолютные).</param>
    /// <param name="isCancelled">Признак остановки (кнопка «Стоп»).</param>
    /// <param name="progress">Прогресс: (текущий, всего, подпись).</param>
    /// <returns>Сводка прогона.</returns>
    internal static Result Run(
        ISwApplication swApp,
        IReadOnlyList<string> documentPaths,
        Func<bool> isCancelled,
        Action<int, int, string> progress)
    {
      var result = new Result();
      if (swApp?.Sw == null || documentPaths == null || documentPaths.Count == 0)
        return result;

      // Без настроенного корневого пути срез бессмысленен — не трогаем документы.
      if (!VelumRelativeDocumentPathResolver.HasRootPath())
        return result;

      int total = documentPaths.Count;
      for (int i = 0; i < total; i++)
      {
        if (isCancelled != null && isCancelled())
        {
          result.Stopped = true;
          break;
        }

        string path = documentPaths[i];
        progress?.Invoke(i, total, Path.GetFileName((path ?? string.Empty).Trim()));
        MigrateOne(swApp, path, result);
      }

      progress?.Invoke(total, total, string.Empty);
      return result;
    }

    /// <summary>Открывает один документ, переводит свойства, сохраняет и закрывает (если открывали).</summary>
    private static void MigrateOne(ISwApplication swApp, string path, Result result)
    {
      result.Scanned++;

      string normalized = NormalizePathSafe(path);
      if (string.IsNullOrEmpty(normalized))
      {
        result.SkippedMissing++;
        return;
      }

      // Не SolidWorks-документ — молча пропускаем (в реестре бывают dxf/pdf).
      if (!TryResolveDocumentType(normalized, out int docType))
        return;

      ModelDoc2 modelDoc;
      bool openedByUs;
      if (!TryGetOrOpenDocument(swApp, normalized, docType, out modelDoc, out openedByUs, out string error))
      {
        result.SkippedMissing++;
        AddError(result, Path.GetFileName(normalized) + ": " + error);
        return;
      }

      int changed = 0;
      try
      {
        CustomPropertyManager cpm =
            VelumRecipeSolidWorksCustomProperties.TryGetManager(modelDoc, "document");
        if (cpm == null)
        {
          result.Failed++;
          AddError(result, Path.GetFileName(normalized) + ": менеджер свойств недоступен");
          return;
        }

        for (int i = 0; i < TargetProperties.Length; i++)
          changed += TryConvertProperty(cpm, TargetProperties[i], result);

        if (changed > 0)
        {
          if (TrySaveDocument(modelDoc))
          {
            result.ConvertedDocuments++;
            result.ConvertedPaths.Add(normalized);
          }
          else
          {
            result.Failed++;
            AddError(result, Path.GetFileName(normalized) + ": не удалось сохранить");
          }
        }
        else
          result.UnchangedDocuments++;
      }
      catch (Exception ex)
      {
        result.Failed++;
        AddError(result, Path.GetFileName(normalized) + ": " + ex.Message);
      }
      finally
      {
        if (openedByUs)
          TryCloseDocument(swApp, modelDoc);
      }
    }

    /// <summary>
    /// Переводит одно свойство: читает сырое значение, срезает префикс, пишет относительное.
    /// Возвращает 1, если значение изменено и записано, иначе 0.
    /// </summary>
    private static int TryConvertProperty(CustomPropertyManager cpm, string name, Result result)
    {
      if (!VelumRecipeSolidWorksCustomProperties.TryGetValue(cpm, name, out string raw))
        return 0;

      raw = (raw ?? string.Empty).Trim();
      if (raw.Length == 0)
        return 0;

      string stored = VelumRelativeDocumentPathResolver.ToStored(raw);
      if (string.Equals(stored, raw, StringComparison.OrdinalIgnoreCase))
        return 0;

      bool ok = VelumRecipeSolidWorksCustomProperties.TrySetValue(
          cpm,
          name,
          stored,
          "always",
          VelumSolidCustomPropertyTypes.TypeKeyText,
          out bool skipped,
          out _);

      if (ok && !skipped)
      {
        result.PropertiesConverted++;
        return 1;
      }

      return 0;
    }

    /// <summary>Сохраняет документ без диалогов. true — без ошибок сохранения.</summary>
    private static bool TrySaveDocument(ModelDoc2 modelDoc)
    {
      try
      {
        int errors = 0;
        int warnings = 0;
        modelDoc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref errors, ref warnings);
        return errors == 0;
      }
      catch
      {
        return false;
      }
    }

    /// <summary>Ищет открытый документ по пути, иначе открывает без окон (Silent).</summary>
    private static bool TryGetOrOpenDocument(
        ISwApplication swApp,
        string path,
        int docType,
        out ModelDoc2 modelDoc,
        out bool openedByUs,
        out string error)
    {
      modelDoc = null;
      openedByUs = false;
      error = string.Empty;

      modelDoc = TryFindOpenDocumentByPath(swApp, path);
      if (modelDoc != null)
        return true;

      if (!File.Exists(path))
      {
        error = "файл не найден";
        return false;
      }

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
          error = "не удалось открыть (errors=" + openErrors + ")";
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

    /// <summary>Ищет уже открытый документ по нормализованному пути (без OpenDoc).</summary>
    private static ModelDoc2 TryFindOpenDocumentByPath(ISwApplication swApp, string expected)
    {
      if (swApp?.Sw == null || string.IsNullOrWhiteSpace(expected))
        return null;

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

    /// <summary>Закрывает документ по заголовку (только если открывали мы).</summary>
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

    /// <summary>Определяет тип SolidWorks-документа по расширению. false — не SolidWorks-файл.</summary>
    private static bool TryResolveDocumentType(string path, out int docType)
    {
      docType = 0;
      string ext;
      try
      {
        ext = Path.GetExtension(path).ToLowerInvariant();
      }
      catch
      {
        return false;
      }

      if (ext == ".sldprt")
      {
        docType = (int)swDocumentTypes_e.swDocPART;
        return true;
      }

      if (ext == ".sldasm")
      {
        docType = (int)swDocumentTypes_e.swDocASSEMBLY;
        return true;
      }

      if (ext == ".slddrw")
      {
        docType = (int)swDocumentTypes_e.swDocDRAWING;
        return true;
      }

      return false;
    }

    private static void AddError(Result result, string message)
    {
      if (result.Errors.Count < MaxReportedErrors)
        result.Errors.Add(message);
    }

    private static string NormalizePathSafe(string path)
    {
      string value = (path ?? string.Empty).Trim();
      if (value.Length == 0)
        return string.Empty;

      try
      {
        // Ключ FilePath реестра хранится относительным корню документов —
        // сначала достройка до полного, затем канонизация.
        return Path.GetFullPath(VelumRelativeDocumentPathResolver.ToFull(value));
      }
      catch
      {
        return value;
      }
    }

    private static bool PathsLikelySame(string a, string b)
    {
      string x = NormalizePathSafe(a);
      string y = NormalizePathSafe(b);
      if (x.Length == 0 || y.Length == 0)
        return false;
      return string.Equals(x, y, StringComparison.OrdinalIgnoreCase);
    }
  }
}
