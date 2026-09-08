using System;
using System.Collections.Generic;
using System.IO;
using SolidWorks.Interop.sldworks;
using Xarial.XCad;
using Xarial.XCad.Documents;
using Xarial.XCad.SolidWorks;

namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Получение <see cref="ModelDoc2"/> для активного IXDocument и компонентов сборки (COM).
  /// </summary>
  internal static class VelumSolidWorksModelDocHelper
  {
    internal static ModelDoc2 TryGetActiveModelDoc2(IXApplication app, IXDocument ixDoc)
    {
      if (app == null || ixDoc == null)
        return null;
      var swApp = app as ISwApplication;
      if (swApp?.Sw == null)
        return null;
      ModelDoc2 active = null;
      try
      {
        active = swApp.Sw.IActiveDoc2 as ModelDoc2;
      }
      catch
      {
        return null;
      }

      if (active == null)
        return null;

      if (PathsLikelySame(ixDoc.Path, active.GetPathName()))
        return active;

      try
      {
        string t = ixDoc.Title;
        string at = active.GetTitle();
        if (!string.IsNullOrEmpty(t) && string.Equals(t, at, StringComparison.OrdinalIgnoreCase))
          return active;
      }
      catch
      {
      }

      return null;
    }

    internal static ModelDoc2 TryGetPartModelFromComponent(IXComponent comp)
    {
      if (comp == null)
        return null;
      try
      {
        Component2 c2 = TryGetSwComponent2(comp);
        return c2?.GetModelDoc2() as ModelDoc2;
      }
      catch
      {
        return null;
      }
    }

    internal static Component2 TryGetSwComponent2(IXComponent comp)
    {
      if (comp == null)
        return null;
      try
      {
        var swObj = comp as ISwObject;
        return swObj?.Dispatch as Component2;
      }
      catch
      {
        return null;
      }
    }

    private static bool PathsLikelySame(string pathX, string pathSw)
    {
      try
      {
        if (string.IsNullOrWhiteSpace(pathX) || string.IsNullOrWhiteSpace(pathSw))
          return false;
        string a = Path.GetFullPath(pathX.Trim());
        string b = Path.GetFullPath(pathSw.Trim());
        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
      }
      catch
      {
        return false;
      }
    }

    internal static string NormalizeModelPath(ModelDoc2 md)
    {
      if (md == null)
        return string.Empty;
      try
      {
        string p = md.GetPathName();
        if (!string.IsNullOrWhiteSpace(p))
          return Path.GetFullPath(p.Trim()).ToUpperInvariant();
      }
      catch
      {
      }

      try
      {
        return (md.GetTitle() ?? string.Empty).ToUpperInvariant();
      }
      catch
      {
        return string.Empty;
      }
    }

    /// <summary>
    /// Дешёвое чтение штампа модели и флага «требуется сохранение» для планировщика опроса на пульсе.
    /// </summary>
    internal static bool TryReadDocumentRevision(ModelDoc2 modelDoc, out int updateStamp, out bool saveFlag)
    {
      updateStamp = 0;
      saveFlag = false;
      if (modelDoc == null)
        return false;

      try
      {
        updateStamp = modelDoc.GetUpdateStamp();
        saveFlag = modelDoc.GetSaveFlag();
        return true;
      }
      catch
      {
        return false;
      }
    }

    /// <summary>
    /// Стабильный ключ документа для dispatch рецептов: путь на диске или identity несохранённого (не заголовок).
    /// </summary>
    internal static string TryGetDispatchDocumentKey(ModelDoc2 modelDoc)
    {
      if (modelDoc == null)
        return "no_doc";

      try
      {
        string path = modelDoc.GetPathName();
        if (!string.IsNullOrWhiteSpace(path))
          return "path:" + Path.GetFullPath(path.Trim());
      }
      catch
      {
      }

      try
      {
        return "unsaved:" + modelDoc.GetHashCode().ToString(System.Globalization.CultureInfo.InvariantCulture);
      }
      catch
      {
        return "no_doc";
      }
    }
  }
}
