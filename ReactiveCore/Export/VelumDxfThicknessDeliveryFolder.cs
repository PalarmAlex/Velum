using System;
using System.IO;
using SolidWorks.Interop.sldworks;
using Velum.ReactiveCore;

namespace Velum.ReactiveCore.Export
{
  /// <summary>
  /// Субкаталоги выдачи DXF по свойству «Толщина»: число → имя папки; иначе «Прочее».
  /// </summary>
  internal static class VelumDxfThicknessDeliveryFolder
  {
    internal const string OtherFolderName = "Прочее";

    /// <summary>
    /// Каталог выдачи: при <paramref name="enabled"/> — <c>dxfRoot\толщина</c> или <c>dxfRoot\Прочее</c>.
    /// </summary>
    internal static string Resolve(
        string dxfRoot,
        ModelDoc2 modelDoc,
        string configName,
        bool enabled)
    {
      string root = (dxfRoot ?? string.Empty).Trim();
      if (!enabled || string.IsNullOrWhiteSpace(root))
        return root;

      string subfolder = ResolveSubfolderName(modelDoc, configName);
      return Path.Combine(root, subfolder);
    }

    /// <summary>Имя субкаталога по «Толщина» конфигурации (или document).</summary>
    internal static string ResolveSubfolderName(ModelDoc2 modelDoc, string configName)
    {
      if (modelDoc == null)
        return OtherFolderName;

      string raw;
      bool exists;
      if (!VelumRecipeSolidWorksCustomProperties.TryReadConfigThenDocument(
              modelDoc,
              configName,
              VelumBlankSizeProperties.Thickness,
              out raw,
              out exists) ||
          !exists)
        return OtherFolderName;

      raw = (raw ?? string.Empty).Trim();
      if (raw.Length == 0)
        return OtherFolderName;

      if (!VelumBlankSizeProperties.TryParseNumber(raw, out _))
        return OtherFolderName;

      string folderName = FormatFolderName(raw);
      if (string.IsNullOrWhiteSpace(folderName) || ContainsInvalidPathChars(folderName))
        return OtherFolderName;

      return folderName;
    }

    /// <summary>Создаёт каталог выдачи при необходимости.</summary>
    internal static bool TryEnsureDirectory(string folder, out string error)
    {
      error = string.Empty;
      string path = (folder ?? string.Empty).Trim();
      if (string.IsNullOrWhiteSpace(path))
      {
        error = "delivery_folder_empty";
        return false;
      }

      try
      {
        if (!Directory.Exists(path))
          Directory.CreateDirectory(path);
        return true;
      }
      catch (Exception ex)
      {
        error = ex.Message;
        return false;
      }
    }

    /// <summary>
    /// Как <see cref="VelumBlankSizeProperties.FormatValueForFileName"/>, но десятичный разделитель — запятая (1,5).
    /// </summary>
    private static string FormatFolderName(string rawValue)
    {
      string formatted = VelumBlankSizeProperties.FormatValueForFileName(rawValue);
      if (string.IsNullOrEmpty(formatted))
        return string.Empty;

      return formatted.Replace('.', ',');
    }

    private static bool ContainsInvalidPathChars(string name)
    {
      if (string.IsNullOrEmpty(name))
        return true;

      char[] invalid = Path.GetInvalidFileNameChars();
      for (int i = 0; i < name.Length; i++)
      {
        char c = name[i];
        for (int j = 0; j < invalid.Length; j++)
        {
          if (c == invalid[j])
            return true;
        }
      }

      return false;
    }
  }
}
