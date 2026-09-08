using System;
using ISIDA.Common;
using SolidWorks.Interop.sldworks;
using Velum.ReactiveCore;

namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Подстановка предлагаемого имени файла в диалог Save/Save As через COM SolidWorks.
  /// </summary>
  internal static class VelumSolidWorksSaveFileNameHelper
  {
    /// <summary>
    /// Задаёт имя файла для следующего диалога сохранения активного документа.
    /// </summary>
    internal static bool TrySetSuggestedSaveFileName(
        ModelDoc2 modelDoc,
        SldWorks sw,
        string baseNameWithoutExtension,
        string extensionWithDot)
    {
      if (modelDoc == null || string.IsNullOrWhiteSpace(baseNameWithoutExtension))
        return false;

      string trimmed = NormalizeBaseName(baseNameWithoutExtension);
      string ext = NormalizeExtension(extensionWithDot);
      string withExtension = trimmed + ext;
      bool isUnsaved = IsUnsavedDocument(modelDoc);

      // Для несохранённых документов SetTitle2 — единственный надёжный способ предзаполнить Save As (см. codestack.net).
      if (isUnsaved && TrySetTitle2(modelDoc, trimmed))
        return true;

      if (!isUnsaved)
      {
        try
        {
          modelDoc.SetSaveAsFileName(withExtension);
        }
        catch (Exception ex)
        {
          Logger.Warning("Velum KB SetSaveAsFileName(full) failed: " + DescribeException(ex));
        }

        try
        {
          modelDoc.SetSaveAsFileName(trimmed);
        }
        catch (Exception ex)
        {
          Logger.Warning("Velum KB SetSaveAsFileName(base) failed: " + DescribeException(ex));
        }
      }

      if (TrySetTitle2(modelDoc, trimmed))
        return true;

      if (sw != null)
      {
        try
        {
          sw.SetPromptFilename2(withExtension, string.Empty);
          return true;
        }
        catch (Exception ex)
        {
          Logger.Warning("Velum KB SetPromptFilename2 failed: " + DescribeException(ex));
        }
      }

      return false;
    }

    internal static string TryGetDefaultExtension(ModelDoc2 modelDoc)
    {
      return TryGetDefaultExtensionForKind(VelumFileNameSequenceState.ClassifyDocumentKind(modelDoc));
    }

    internal static string TryGetDefaultExtensionForKind(VelumSolidDocumentKind kind)
    {
      if (kind == VelumSolidDocumentKind.Assembly)
        return ".sldasm";
      if (kind == VelumSolidDocumentKind.Drawing)
        return ".slddrw";
      return ".sldprt";
    }

    internal static bool IsUnsavedDocument(ModelDoc2 modelDoc)
    {
      try
      {
        return string.IsNullOrWhiteSpace(modelDoc?.GetPathName());
      }
      catch
      {
        return true;
      }
    }

    private static bool TrySetTitle2(ModelDoc2 modelDoc, string baseNameWithoutExtension)
    {
      try
      {
        if (modelDoc.SetTitle2(baseNameWithoutExtension))
          return true;
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum KB SetTitle2 failed: " + DescribeException(ex));
      }

      return false;
    }

    /// <summary>
    /// Убирает декоративные кавычки вокруг имени (часто встречаются в шаблонах КБ для «Наименования»).
    /// </summary>
    internal static string NormalizeBaseName(string baseNameWithoutExtension)
    {
      if (string.IsNullOrWhiteSpace(baseNameWithoutExtension))
        return string.Empty;

      string trimmed = baseNameWithoutExtension.Trim();
      while (trimmed.Length >= 2)
      {
        char first = trimmed[0];
        char last = trimmed[trimmed.Length - 1];
        if ((first == '\'' && last == '\'') || (first == '"' && last == '"'))
          trimmed = trimmed.Substring(1, trimmed.Length - 2).Trim();
        else
          break;
      }

      return trimmed;
    }

    private static string NormalizeExtension(string extensionWithDot)
    {
      string ext = string.IsNullOrWhiteSpace(extensionWithDot) ? string.Empty : extensionWithDot.Trim();
      if (ext.Length > 0 && ext[0] != '.')
        ext = "." + ext;
      return ext;
    }

    private static string DescribeException(Exception ex)
    {
      if (ex is System.Reflection.TargetInvocationException tie && tie.InnerException != null)
        return tie.InnerException.GetType().Name + ": " + tie.InnerException.Message;
      return ex.GetType().Name + ": " + ex.Message;
    }
  }
}
