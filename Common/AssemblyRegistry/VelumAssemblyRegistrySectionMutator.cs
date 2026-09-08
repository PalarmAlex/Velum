using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Velum.ReactiveCore;
using Xarial.XCad.SolidWorks;

namespace Velum.UI.AssemblyRegistry
{
  internal enum VelumAssemblyRegistrySectionWriteStatus
  {
    Ok = 0,
    Skip = 1,
    Fail = 2,
    RolledBack = 3,
    RollbackFail = 4
  }

  internal sealed class VelumAssemblyRegistrySectionWriteResult
  {
    internal string Identity { get; set; }
    internal string Label { get; set; }
    internal VelumAssemblyRegistrySectionWriteStatus Status { get; set; }
    internal string Message { get; set; }
  }

  internal sealed class VelumAssemblyRegistrySectionBatchResult
  {
    internal List<VelumAssemblyRegistrySectionWriteResult> Items { get; } =
        new List<VelumAssemblyRegistrySectionWriteResult>();

    internal bool Stopped { get; set; }

    internal bool HadFailure { get; set; }

    internal string BuildReport()
    {
      int ok = 0, skip = 0, fail = 0, rolled = 0, rollFail = 0;
      var details = new StringBuilder();
      foreach (VelumAssemblyRegistrySectionWriteResult item in Items)
      {
        switch (item.Status)
        {
          case VelumAssemblyRegistrySectionWriteStatus.Ok: ok++; break;
          case VelumAssemblyRegistrySectionWriteStatus.Skip: skip++; break;
          case VelumAssemblyRegistrySectionWriteStatus.Fail: fail++; break;
          case VelumAssemblyRegistrySectionWriteStatus.RolledBack: rolled++; break;
          case VelumAssemblyRegistrySectionWriteStatus.RollbackFail: rollFail++; break;
        }

        if (item.Status == VelumAssemblyRegistrySectionWriteStatus.Ok &&
            string.IsNullOrEmpty(item.Message))
          continue;

        details.AppendLine(
            (item.Label ?? item.Identity ?? "?") + ": " +
            StatusText(item.Status) +
            (string.IsNullOrEmpty(item.Message) ? string.Empty : (" — " + item.Message)));
      }

      var sb = new StringBuilder();
      sb.Append("Готово: ").Append(ok);
      sb.Append(", пропущено: ").Append(skip);
      sb.Append(", ошибок: ").Append(fail);
      if (rolled > 0 || rollFail > 0)
      {
        sb.Append(", откат: ").Append(rolled);
        sb.Append(", сбой отката: ").Append(rollFail);
      }

      if (Stopped)
        sb.Append(" (прервано)");

      if (details.Length > 0)
      {
        sb.AppendLine();
        sb.AppendLine();
        sb.Append(details.ToString().TrimEnd());
      }

      return sb.ToString();
    }

    private static string StatusText(VelumAssemblyRegistrySectionWriteStatus status)
    {
      switch (status)
      {
        case VelumAssemblyRegistrySectionWriteStatus.Ok: return "ok";
        case VelumAssemblyRegistrySectionWriteStatus.Skip: return "skip";
        case VelumAssemblyRegistrySectionWriteStatus.Fail: return "fail";
        case VelumAssemblyRegistrySectionWriteStatus.RolledBack: return "откат";
        case VelumAssemblyRegistrySectionWriteStatus.RollbackFail: return "сбой отката";
        default: return status.ToString();
      }
    }
  }

  /// <summary>
  /// Пакетная запись «Раздел» в config вхождения: снимок → Set → Save3 → Close(если открыли мы);
  /// при ошибке/Stop — откат уже сохранённых; восстановление активной головной сборки.
  /// </summary>
  internal sealed class VelumAssemblyRegistrySectionMutator
  {
    private readonly ISwApplication _swApp;
    private readonly Func<bool> _isCancel;
    private readonly Action<int, int, string> _progress;

    internal VelumAssemblyRegistrySectionMutator(
        ISwApplication swApp,
        Func<bool> isCancel,
        Action<int, int, string> progress)
    {
      _swApp = swApp;
      _isCancel = isCancel ?? (() => false);
      _progress = progress;
    }

    internal VelumAssemblyRegistrySectionBatchResult Apply(
        IReadOnlyList<VelumAssemblyRegistryComponent> targets,
        Func<VelumAssemblyRegistryComponent, string> newValueFactory)
    {
      var result = new VelumAssemblyRegistrySectionBatchResult();
      if (targets == null || targets.Count == 0 || newValueFactory == null)
        return result;

      string headPath = string.Empty;
      string headTitle = string.Empty;
      TryCaptureActiveAssembly(out headPath, out headTitle);

      var unique = new Dictionary<string, VelumAssemblyRegistryComponent>(StringComparer.OrdinalIgnoreCase);
      foreach (VelumAssemblyRegistryComponent item in targets)
      {
        if (item == null || string.IsNullOrWhiteSpace(item.Identity))
          continue;
        if (!unique.ContainsKey(item.Identity))
          unique[item.Identity] = item;
      }

      var plan = new List<SnapshotEntry>(unique.Count);
      int index = 0;
      foreach (VelumAssemblyRegistryComponent item in unique.Values)
      {
        index++;
        Report(index, unique.Count, "Подготовка: " + (item.FileTitle ?? item.Identity));
        if (_isCancel())
        {
          result.Stopped = true;
          break;
        }

        string newValue = newValueFactory(item) ?? string.Empty;
        string label = string.IsNullOrWhiteSpace(item.Designation)
            ? (item.FileTitle ?? item.Identity)
            : item.Designation;

        SnapshotEntry entry = CreateSnapshot(item, newValue, label, out string prepError);
        if (entry == null)
        {
          result.Items.Add(new VelumAssemblyRegistrySectionWriteResult
          {
            Identity = item.Identity,
            Label = label,
            Status = VelumAssemblyRegistrySectionWriteStatus.Fail,
            Message = prepError
          });
          result.HadFailure = true;
          RollbackApplied(plan, result);
          RestoreActiveAssembly(headPath, headTitle);
          return result;
        }

        string effectiveOld = entry.ConfigPropertyExisted
            ? (entry.OldValue ?? string.Empty)
            : string.Empty;
        // Для корня Стандарты канон записи — «Стандарты»; если на config пусто,
        // но документ уже стандарт через document-level — всё равно пишем config.
        bool alreadyMatches = string.Equals(
            effectiveOld,
            entry.NewValue ?? string.Empty,
            StringComparison.Ordinal);
        if (alreadyMatches && entry.ConfigPropertyExisted)
        {
          result.Items.Add(new VelumAssemblyRegistrySectionWriteResult
          {
            Identity = item.Identity,
            Label = label,
            Status = VelumAssemblyRegistrySectionWriteStatus.Skip,
            Message = "без изменений"
          });
          continue;
        }

        if (!entry.ConfigPropertyExisted &&
            string.IsNullOrEmpty(entry.NewValue) &&
            item.Kind != VelumAssemblyRegistryNodeKind.Standard)
        {
          result.Items.Add(new VelumAssemblyRegistrySectionWriteResult
          {
            Identity = item.Identity,
            Label = label,
            Status = VelumAssemblyRegistrySectionWriteStatus.Skip,
            Message = "уже корень семейства"
          });
          continue;
        }

        plan.Add(entry);
      }

      if (result.HadFailure)
      {
        RestoreActiveAssembly(headPath, headTitle);
        return result;
      }

      for (int i = 0; i < plan.Count; i++)
      {
        if (_isCancel())
        {
          result.Stopped = true;
          result.HadFailure = true;
          RollbackApplied(plan, result);
          break;
        }

        SnapshotEntry entry = plan[i];
        Report(i + 1, plan.Count, "Запись: " + entry.Label);
        string error;
        if (!TryWriteAndSave(entry, out error))
        {
          result.Items.Add(new VelumAssemblyRegistrySectionWriteResult
          {
            Identity = entry.Identity,
            Label = entry.Label,
            Status = VelumAssemblyRegistrySectionWriteStatus.Fail,
            Message = error
          });
          result.HadFailure = true;
          RollbackApplied(plan, result);
          break;
        }

        entry.Applied = true;
        result.Items.Add(new VelumAssemblyRegistrySectionWriteResult
        {
          Identity = entry.Identity,
          Label = entry.Label,
          Status = VelumAssemblyRegistrySectionWriteStatus.Ok,
          Message = string.Empty
        });
      }

      RestoreActiveAssembly(headPath, headTitle);
      return result;
    }

    private SnapshotEntry CreateSnapshot(
        VelumAssemblyRegistryComponent item,
        string newValue,
        string label,
        out string error)
    {
      error = string.Empty;
      if (string.IsNullOrWhiteSpace(item.FilePath) || !File.Exists(item.FilePath))
      {
        error = "Файл не найден";
        return null;
      }

      string config = (item.ConfigurationName ?? string.Empty).Trim();
      if (config.Length == 0)
      {
        error = "Нет конфигурации вхождения";
        return null;
      }

      ModelDoc2 modelDoc;
      bool openedByUs;
      if (!TryOpenDocument(item.FilePath, out modelDoc, out openedByUs, out error))
        return null;

      try
      {
        bool exists;
        string oldValue;
        if (!VelumAssemblyRegistryPropertyReader.TryReadSectionOnConfiguration(
                modelDoc,
                config,
                out oldValue,
                out exists))
        {
          error = "Не удалось прочитать свойства конфигурации";
          return null;
        }

        return new SnapshotEntry
        {
          Identity = item.Identity,
          FilePath = item.FilePath,
          ConfigurationName = config,
          Label = label,
          OldValue = oldValue ?? string.Empty,
          NewValue = newValue ?? string.Empty,
          ConfigPropertyExisted = exists,
          OpenedByUs = openedByUs
        };
      }
      finally
      {
        if (openedByUs && modelDoc != null)
          TryCloseDocument(modelDoc);
      }
    }

    private bool TryWriteAndSave(SnapshotEntry entry, out string error)
    {
      error = string.Empty;
      ModelDoc2 modelDoc;
      bool openedByUs;
      if (!TryOpenDocument(entry.FilePath, out modelDoc, out openedByUs, out error))
        return false;

      try
      {
        if (!TrySetSectionOnConfiguration(modelDoc, entry.ConfigurationName, entry.NewValue, deleteIfEmpty: false, out error))
          return false;

        if (!TrySaveSilent(modelDoc, out error))
          return false;

        return true;
      }
      finally
      {
        if (openedByUs && modelDoc != null)
          TryCloseDocument(modelDoc);
      }
    }

    private void RollbackApplied(List<SnapshotEntry> plan, VelumAssemblyRegistrySectionBatchResult result)
    {
      for (int i = plan.Count - 1; i >= 0; i--)
      {
        SnapshotEntry entry = plan[i];
        if (!entry.Applied)
          continue;

        Report(0, 0, "Откат: " + entry.Label);
        string error;
        if (!TryRestoreSnapshot(entry, out error))
        {
          result.Items.Add(new VelumAssemblyRegistrySectionWriteResult
          {
            Identity = entry.Identity,
            Label = entry.Label,
            Status = VelumAssemblyRegistrySectionWriteStatus.RollbackFail,
            Message = error
          });
        }
        else
        {
          entry.Applied = false;
          // Обновляем статус соответствующего Ok → RolledBack, если уже добавлен.
          for (int j = 0; j < result.Items.Count; j++)
          {
            if (result.Items[j].Identity == entry.Identity &&
                result.Items[j].Status == VelumAssemblyRegistrySectionWriteStatus.Ok)
            {
              result.Items[j].Status = VelumAssemblyRegistrySectionWriteStatus.RolledBack;
              result.Items[j].Message = "откат выполнен";
              break;
            }
          }
        }
      }
    }

    private bool TryRestoreSnapshot(SnapshotEntry entry, out string error)
    {
      error = string.Empty;
      ModelDoc2 modelDoc;
      bool openedByUs;
      if (!TryOpenDocument(entry.FilePath, out modelDoc, out openedByUs, out error))
        return false;

      try
      {
        if (!entry.ConfigPropertyExisted)
        {
          if (!TryDeleteSectionOnConfiguration(modelDoc, entry.ConfigurationName, out error))
            return false;
        }
        else if (!TrySetSectionOnConfiguration(
                     modelDoc,
                     entry.ConfigurationName,
                     entry.OldValue,
                     deleteIfEmpty: false,
                     out error))
        {
          return false;
        }

        return TrySaveSilent(modelDoc, out error);
      }
      finally
      {
        if (openedByUs && modelDoc != null)
          TryCloseDocument(modelDoc);
      }
    }

    private static bool TrySetSectionOnConfiguration(
        ModelDoc2 modelDoc,
        string configurationName,
        string value,
        bool deleteIfEmpty,
        out string error)
    {
      error = string.Empty;
      CustomPropertyManager cpm = VelumRecipeSolidWorksCustomProperties.TryGetManager(
          modelDoc,
          configurationName);
      if (cpm == null)
      {
        error = "Нет CustomPropertyManager конфигурации";
        return false;
      }

      if (deleteIfEmpty && string.IsNullOrEmpty(value))
        return TryDeleteSectionOnConfiguration(modelDoc, configurationName, out error);

      bool skipped;
      string message;
      if (!VelumRecipeSolidWorksCustomProperties.TrySetValue(
              cpm,
              VelumAssemblyRegistrySectionPath.PropSection,
              value ?? string.Empty,
              "always",
              "text",
              out skipped,
              out message))
      {
        error = string.IsNullOrEmpty(message) ? "Не удалось записать «Раздел»" : message;
        return false;
      }

      return true;
    }

    private static bool TryDeleteSectionOnConfiguration(
        ModelDoc2 modelDoc,
        string configurationName,
        out string error)
    {
      error = string.Empty;
      CustomPropertyManager cpm = VelumRecipeSolidWorksCustomProperties.TryGetManager(
          modelDoc,
          configurationName);
      if (cpm == null)
      {
        error = "Нет CustomPropertyManager конфигурации";
        return false;
      }

      if (!VelumRecipeSolidWorksCustomProperties.TryPropertyExists(
              cpm,
              VelumAssemblyRegistrySectionPath.PropSection))
        return true;

      string canonical;
      if (!VelumRecipeSolidWorksCustomProperties.TryResolvePropertyName(
              cpm,
              VelumAssemblyRegistrySectionPath.PropSection,
              out canonical))
        canonical = VelumAssemblyRegistrySectionPath.PropSection;

      try
      {
        int deleted = cpm.Delete2(canonical);
        if (deleted == 0)
          return true;
        cpm.Delete(canonical);
        return true;
      }
      catch (Exception ex)
      {
        error = ex.Message;
        return false;
      }
    }

    private bool TryOpenDocument(
        string filePath,
        out ModelDoc2 modelDoc,
        out bool openedByUs,
        out string error)
    {
      modelDoc = null;
      openedByUs = false;
      error = string.Empty;

      if (_swApp?.Sw == null)
      {
        error = "SolidWorks недоступен";
        return false;
      }

      modelDoc = TryFindOpenDocumentByPath(filePath);
      if (modelDoc != null)
        return true;

      int docType;
      if (!TryResolveDocumentType(filePath, out docType, out error))
        return false;

      try
      {
        int openErrors = 0;
        int warnings = 0;
        modelDoc = _swApp.Sw.OpenDoc6(
            filePath,
            docType,
            (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
            string.Empty,
            ref openErrors,
            ref warnings) as ModelDoc2;

        if (modelDoc == null)
          modelDoc = TryFindOpenDocumentByPath(filePath);

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
        return false;
      }
    }

    private ModelDoc2 TryFindOpenDocumentByPath(string filePath)
    {
      if (_swApp?.Sw == null || string.IsNullOrWhiteSpace(filePath))
        return null;

      string expected = VelumAssemblyRegistryPropertyReader.NormalizePath(filePath);
      try
      {
        ModelDoc2 doc = _swApp.Sw.GetFirstDocument() as ModelDoc2;
        while (doc != null)
        {
          string path = string.Empty;
          try
          {
            path = doc.GetPathName();
          }
          catch
          {
            path = string.Empty;
          }

          if (!string.IsNullOrEmpty(path) &&
              string.Equals(
                  VelumAssemblyRegistryPropertyReader.NormalizePath(path),
                  expected,
                  StringComparison.OrdinalIgnoreCase))
            return doc;

          doc = doc.GetNext() as ModelDoc2;
        }
      }
      catch
      {
      }

      return null;
    }

    private static bool TryResolveDocumentType(string filePath, out int docType, out string error)
    {
      docType = 0;
      error = string.Empty;
      string ext = Path.GetExtension(filePath) ?? string.Empty;
      if (ext.Equals(".sldprt", StringComparison.OrdinalIgnoreCase))
      {
        docType = (int)swDocumentTypes_e.swDocPART;
        return true;
      }

      if (ext.Equals(".sldasm", StringComparison.OrdinalIgnoreCase))
      {
        docType = (int)swDocumentTypes_e.swDocASSEMBLY;
        return true;
      }

      error = "Не деталь и не сборка";
      return false;
    }

    private static bool TrySaveSilent(ModelDoc2 modelDoc, out string error)
    {
      error = string.Empty;
      try
      {
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

    private void TryCloseDocument(ModelDoc2 modelDoc)
    {
      if (modelDoc == null || _swApp?.Sw == null)
        return;
      try
      {
        string title = modelDoc.GetTitle();
        if (!string.IsNullOrWhiteSpace(title))
          _swApp.Sw.CloseDoc(title);
      }
      catch
      {
      }
    }

    private void TryCaptureActiveAssembly(out string path, out string title)
    {
      path = string.Empty;
      title = string.Empty;
      try
      {
        ModelDoc2 active = _swApp?.Sw?.IActiveDoc2 as ModelDoc2;
        if (active == null || active.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
          return;
        path = active.GetPathName() ?? string.Empty;
        title = active.GetTitle() ?? string.Empty;
      }
      catch
      {
      }
    }

    private void RestoreActiveAssembly(string path, string title)
    {
      if (_swApp?.Sw == null)
        return;

      try
      {
        ModelDoc2 target = null;
        if (!string.IsNullOrWhiteSpace(path))
          target = TryFindOpenDocumentByPath(path);

        string activateTitle = title;
        if (target != null)
        {
          try
          {
            activateTitle = target.GetTitle();
          }
          catch
          {
          }
        }

        if (string.IsNullOrWhiteSpace(activateTitle))
          return;

        int activateErrors = 0;
        _swApp.Sw.ActivateDoc3(
            activateTitle,
            true,
            (int)swRebuildOnActivation_e.swDontRebuildActiveDoc,
            ref activateErrors);
      }
      catch
      {
      }
    }

    private void Report(int current, int maximum, string status)
    {
      if (_progress != null)
        _progress(current, maximum, status);
    }

    private sealed class SnapshotEntry
    {
      internal string Identity;
      internal string FilePath;
      internal string ConfigurationName;
      internal string Label;
      internal string OldValue;
      internal string NewValue;
      internal bool ConfigPropertyExisted;
      internal bool OpenedByUs;
      internal bool Applied;
    }
  }
}
