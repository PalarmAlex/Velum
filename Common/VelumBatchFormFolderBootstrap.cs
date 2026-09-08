using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Velum.Configuration;
using Velum.ReactiveCore.Export;
using Velum.SolidHomeostasis;
using Xarial.XCad.SolidWorks;

namespace Velum.UI
{
  /// <summary>
  /// Начальные каталоги пакетных форм: из активного документа SW или из настроек (пустой экран).
  /// DXF/PDF delivery — каталог сборки; Material — поиск именованной папки «Детали» рядом со сборкой.
  /// </summary>
  internal static class VelumBatchFormFolderBootstrap
  {
    internal sealed class DxfFolders
    {
      internal string ExportFolder { get; set; }

      internal string ExportFolderHint { get; set; }

      /// <summary>Каталог файла активной сборки — якорь двухуровневого поиска.</summary>
      internal string AssemblyFolder { get; set; }

      internal bool FromActiveDocument { get; set; }

      internal bool FromActiveAssembly { get; set; }
    }

    internal sealed class PdfFolders
    {
      internal string ExportFolder { get; set; }

      internal string ExportFolderHint { get; set; }

      /// <summary>Каталог файла активной сборки — якорь двухуровневого поиска.</summary>
      internal string AssemblyFolder { get; set; }

      internal bool FromActiveDocument { get; set; }

      internal bool FromActiveAssembly { get; set; }
    }

    internal sealed class MaterialFolders
    {
      internal string PartsFolder { get; set; }

      internal string PartsFolderName { get; set; }

      internal string PartsFolderHint { get; set; }

      /// <summary>Каталог файла активной сборки/детали — якорь двухуровневого поиска.</summary>
      internal string AssemblyFolder { get; set; }

      internal bool FromActiveDocument { get; set; }

      internal bool FromActiveAssembly { get; set; }

      internal bool FromActivePart { get; set; }
    }

    internal static string ResolveLastUsedDeliveryFolder(string saved)
    {
      string folder = (saved ?? string.Empty).Trim();
      if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
        return folder;
      return string.Empty;
    }

    /// <summary>Начальный каталог изделия для формы обновления свойств документов.</summary>
    internal sealed class DocumentPropertyFolders
    {
      internal string CatalogFolder { get; set; }

      /// <summary>Путь активного документа (деталь или сборка), если загрузка из него.</summary>
      internal string ActiveDocumentPath { get; set; }

      internal bool FromActiveDocument { get; set; }

      internal bool FromActiveAssembly { get; set; }

      internal bool FromActivePart { get; set; }
    }

    internal static DxfFolders ResolveDxfFolders(ISwApplication swApp)
    {
      ModelDoc2 partOrAssembly = TryResolveActivePartOrAssembly(swApp);
      if (partOrAssembly != null)
      {
        if (IsAssembly(partOrAssembly))
          return ResolveDxfFoldersFromAssembly(partOrAssembly);

        string documentFolder = TryGetExistingDirectory(
            VelumMaterialSequenceState.TryGetDocumentDirectory(partOrAssembly));
        var folders = new DxfFolders
        {
          ExportFolder = string.Empty,
          AssemblyFolder = documentFolder ?? string.Empty,
          FromActiveDocument = true,
          FromActiveAssembly = false
        };
        return folders;
      }

      return new DxfFolders
      {
        ExportFolder = string.Empty,
        FromActiveDocument = false,
        FromActiveAssembly = false
      };
    }

    internal static PdfFolders ResolvePdfFolders(ISwApplication swApp)
    {
      ModelDoc2 active = TryGetActiveModelDoc(swApp);
      if (active != null)
      {
        int docType = active.GetType();
        if (docType == (int)swDocumentTypes_e.swDocDRAWING)
        {
          string documentFolder = TryGetExistingDirectory(
              VelumMaterialSequenceState.TryGetDocumentDirectory(active));
          var folders = new PdfFolders
          {
            ExportFolder = string.Empty,
            AssemblyFolder = documentFolder ?? string.Empty,
            FromActiveDocument = true,
            FromActiveAssembly = false
          };
          return folders;
        }

        if (docType == (int)swDocumentTypes_e.swDocASSEMBLY)
          return ResolvePdfFoldersFromAssembly(active);

        if (docType == (int)swDocumentTypes_e.swDocPART)
        {
          string documentFolder = TryGetExistingDirectory(
              VelumMaterialSequenceState.TryGetDocumentDirectory(active));
          var folders = new PdfFolders
          {
            ExportFolder = string.Empty,
            AssemblyFolder = documentFolder ?? string.Empty,
            FromActiveDocument = true,
            FromActiveAssembly = false
          };
          return folders;
        }
      }

      return new PdfFolders
      {
        ExportFolder = string.Empty,
        FromActiveDocument = false,
        FromActiveAssembly = false
      };
    }

    internal static MaterialFolders ResolveMaterialFolders(ISwApplication swApp)
    {
      ModelDoc2 partOrAssembly = TryResolveActivePartOrAssembly(swApp);
      if (partOrAssembly != null)
      {
        if (IsAssembly(partOrAssembly))
          return ResolveMaterialFoldersFromAssembly(partOrAssembly);

        return ResolveMaterialFoldersFromPart(partOrAssembly);
      }

      return new MaterialFolders
      {
        PartsFolder = VelumAppConfig.MaterialBatchDefaultPartsFolder ?? string.Empty,
        FromActiveDocument = false,
        FromActiveAssembly = false,
        FromActivePart = false
      };
    }

    /// <summary>
    /// Каталог изделия: активная сборка → папка сборки; активная деталь → папка детали;
    /// иначе — сохранённый путь из настроек.
    /// </summary>
    internal static DocumentPropertyFolders ResolveDocumentPropertyFolders(ISwApplication swApp)
    {
      ModelDoc2 partOrAssembly = TryResolveActivePartOrAssembly(swApp);
      if (partOrAssembly != null)
      {
        string documentFolder = TryGetExistingDirectory(
            VelumMaterialSequenceState.TryGetDocumentDirectory(partOrAssembly));
        string documentPath = string.Empty;
        try
        {
          documentPath = (partOrAssembly.GetPathName() ?? string.Empty).Trim();
        }
        catch
        {
          documentPath = string.Empty;
        }

        bool assembly = IsAssembly(partOrAssembly);
        var folders = new DocumentPropertyFolders
        {
          CatalogFolder = documentFolder ?? string.Empty,
          ActiveDocumentPath = documentPath,
          FromActiveDocument = true,
          FromActiveAssembly = assembly,
          FromActivePart = !assembly
        };

        if (!string.IsNullOrWhiteSpace(folders.CatalogFolder))
          PersistDocumentPropertyFolders(folders);

        return folders;
      }

      return new DocumentPropertyFolders
      {
        CatalogFolder = VelumAppConfig.DocumentPropertyBatchDefaultCatalogFolder ?? string.Empty,
        ActiveDocumentPath = string.Empty,
        FromActiveDocument = false,
        FromActiveAssembly = false,
        FromActivePart = false
      };
    }

    internal static void PersistDocumentPropertyFolders(DocumentPropertyFolders folders)
    {
      if (folders == null)
        return;

      VelumAppConfig.SetDocumentPropertyBatchDefaultCatalogFolder(folders.CatalogFolder ?? string.Empty);
    }

    internal static void PersistDxfFolders(DxfFolders folders)
    {
      if (folders == null)
        return;

      VelumAppConfig.SetDxfBatchDefaultDxfFolder(folders.ExportFolder ?? string.Empty);
    }

    internal static void PersistPdfFolders(PdfFolders folders)
    {
      if (folders == null)
        return;

      VelumAppConfig.SetPdfBatchDefaultPdfFolder(folders.ExportFolder ?? string.Empty);
    }

    internal static void PersistMaterialFolders(MaterialFolders folders)
    {
      if (folders == null)
        return;

      VelumAppConfig.SetMaterialBatchDefaultPartsFolder(folders.PartsFolder ?? string.Empty);
    }

    /// <summary>
    /// Ищет именованный каталог: сначала рядом со сборкой, затем на 1 уровень выше.
    /// Пустое имя — возвращает каталог активной сборки.
    /// </summary>
    /// <param name="assemblyFolder">Каталог, в котором лежит файл сборки.</param>
    /// <param name="folderName">Имя искомого подкаталога (например Детали, PDF). Пусто — каталог сборки.</param>
    internal static string TryFindNamedFolderNearAssembly(string assemblyFolder, string folderName)
    {
      if (string.IsNullOrWhiteSpace(assemblyFolder))
        return null;

      string assemblyDir = TryGetExistingDirectory(assemblyFolder.Trim());
      if (assemblyDir == null)
        return null;

      if (string.IsNullOrWhiteSpace(folderName))
        return assemblyDir;

      string name = folderName.Trim();
      string beside = TryFindNamedFolderInDirectory(assemblyDir, name);
      if (beside != null)
        return beside;

      try
      {
        DirectoryInfo parent = Directory.GetParent(assemblyDir);
        if (parent == null)
          return null;

        return TryFindNamedFolderInDirectory(parent.FullName, name);
      }
      catch
      {
        return null;
      }
    }

    internal static string FormatFolderNotFoundHint(string folderName)
    {
      string name = (folderName ?? string.Empty).Trim();
      if (string.IsNullOrEmpty(name))
        name = "???";

      return "не обнаружен каталог " + name + " рядом со сборкой и уровнем выше. Укажите каталог поиска.";
    }

    internal static void ApplyNamedFolderSearchUi(Label label, TextBox nameBox, ToolTip toolTip)
    {
      const string searchLabel = "Искать каталог:";
      const string searchTooltip =
          "Имя папки (напр. Детали). Поиск: рядом со сборкой, затем уровнем выше. Пусто = каталог сборки.";

      if (label != null)
        label.Text = searchLabel;

      if (toolTip == null)
        return;

      if (label != null)
        toolTip.SetToolTip(label, searchTooltip);
      if (nameBox != null)
        toolTip.SetToolTip(nameBox, searchTooltip);
    }

    /// <summary>
    /// Tooltip на поле каталога: при наведении показывает полный текст значения
    /// (длинный путь или подсказку, если путь не найден).
    /// </summary>
    internal static void BindFolderPathTooltip(TextBox box, ToolTip toolTip)
    {
      if (box == null || toolTip == null)
        return;

      SyncFolderPathTooltip(box, toolTip);
      box.TextChanged += (s, e) => SyncFolderPathTooltip(box, toolTip);
    }

    private static void SyncFolderPathTooltip(TextBox box, ToolTip toolTip)
    {
      string text = box.Text ?? string.Empty;
      toolTip.SetToolTip(box, text);
    }

    internal static void ApplyFolderOrHint(TextBox box, string folder, string hint)
    {
      if (box == null)
        return;

      if (!string.IsNullOrWhiteSpace(folder))
      {
        box.ForeColor = SystemColors.WindowText;
        box.Text = folder;
        return;
      }

      if (!string.IsNullOrWhiteSpace(hint))
      {
        box.ForeColor = Color.Red;
        box.Text = hint;
        return;
      }

      box.ForeColor = SystemColors.WindowText;
      box.Text = string.Empty;
    }

    internal static bool IsHintText(TextBox box)
    {
      return box != null && box.ForeColor == Color.Red;
    }

    internal static string ReadFolderPath(TextBox box)
    {
      if (box == null || IsHintText(box))
        return string.Empty;

      return (box.Text ?? string.Empty).Trim();
    }

    private static DxfFolders ResolveDxfFoldersFromAssembly(ModelDoc2 assembly)
    {
      string assemblyFolder = TryGetAssemblyFolderFromDocument(assembly);

      var folders = new DxfFolders
      {
        ExportFolder = string.Empty,
        ExportFolderHint = string.Empty,
        AssemblyFolder = assemblyFolder ?? string.Empty,
        FromActiveDocument = true,
        FromActiveAssembly = true
      };

      return folders;
    }

    private static PdfFolders ResolvePdfFoldersFromAssembly(ModelDoc2 assembly)
    {
      string assemblyFolder = TryGetAssemblyFolderFromDocument(assembly);

      var folders = new PdfFolders
      {
        ExportFolder = string.Empty,
        ExportFolderHint = string.Empty,
        AssemblyFolder = assemblyFolder ?? string.Empty,
        FromActiveDocument = true,
        FromActiveAssembly = true
      };

      return folders;
    }

    private static MaterialFolders ResolveMaterialFoldersFromAssembly(ModelDoc2 assembly)
    {
      string assemblyFolder = TryGetAssemblyFolderFromDocument(assembly);

      var folders = new MaterialFolders
      {
        PartsFolder = assemblyFolder ?? string.Empty,
        PartsFolderName = string.Empty,
        PartsFolderHint = string.Empty,
        AssemblyFolder = assemblyFolder ?? string.Empty,
        FromActiveDocument = true,
        FromActiveAssembly = true,
        FromActivePart = false
      };

      return folders;
    }

    private static MaterialFolders ResolveMaterialFoldersFromPart(ModelDoc2 part)
    {
      string documentFolder = TryGetExistingDirectory(
          VelumMaterialSequenceState.TryGetDocumentDirectory(part));

      var folders = new MaterialFolders
      {
        PartsFolder = documentFolder ?? string.Empty,
        PartsFolderName = string.Empty,
        PartsFolderHint = string.Empty,
        AssemblyFolder = documentFolder ?? string.Empty,
        FromActiveDocument = true,
        FromActiveAssembly = false,
        FromActivePart = true
      };

      PersistMaterialFolders(folders);
      return folders;
    }

    private static ModelDoc2 TryGetActiveModelDoc(ISwApplication swApp)
    {
      try
      {
        return swApp?.Sw?.IActiveDoc2 as ModelDoc2;
      }
      catch
      {
        return null;
      }
    }

    private static ModelDoc2 TryResolveActivePartOrAssembly(ISwApplication swApp)
    {
      ModelDoc2 active = TryGetActiveModelDoc(swApp);
      if (active == null)
        return null;

      int docType = active.GetType();
      if (docType == (int)swDocumentTypes_e.swDocPART ||
          docType == (int)swDocumentTypes_e.swDocASSEMBLY)
        return active;

      return null;
    }

    private static bool IsAssembly(ModelDoc2 modelDoc)
    {
      try
      {
        return modelDoc != null &&
            modelDoc.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY;
      }
      catch
      {
        return false;
      }
    }

    private static string TryGetAssemblyFolderFromDocument(ModelDoc2 modelDoc)
    {
      return TryGetExistingDirectory(
          VelumMaterialSequenceState.TryGetDocumentDirectory(modelDoc));
    }

    private static string TryFindNamedFolderInDirectory(string parentDirectory, string folderName)
    {
      if (string.IsNullOrWhiteSpace(parentDirectory) || string.IsNullOrWhiteSpace(folderName))
        return null;

      try
      {
        string candidate = Path.Combine(parentDirectory.Trim(), folderName.Trim());
        return TryGetExistingDirectory(candidate);
      }
      catch
      {
        return null;
      }
    }

    private static string ResolveDxfExportFolder(ModelDoc2 modelDoc, string documentFolder)
    {
      string catalog = VelumDxfBatchDocumentHelper.TryReadDxfCatalog(modelDoc);
      string catalogDir = TryGetExistingDirectory(catalog);
      if (!string.IsNullOrWhiteSpace(catalogDir))
        return catalogDir;

      if (!string.IsNullOrWhiteSpace(documentFolder))
        return documentFolder;

      return VelumAppConfig.DxfBatchDefaultDxfFolder ?? string.Empty;
    }

    private static string ResolvePdfExportFolder(ModelDoc2 modelDoc, string documentFolder)
    {
      string pdfPath = VelumPdfBatchDocumentHelper.TryReadPdfPath(modelDoc);
      if (!string.IsNullOrWhiteSpace(pdfPath))
      {
        try
        {
          if (Directory.Exists(pdfPath))
            return pdfPath;

          string dir = Path.GetDirectoryName(pdfPath);
          string existingDir = TryGetExistingDirectory(dir);
          if (!string.IsNullOrWhiteSpace(existingDir))
            return existingDir;
        }
        catch
        {
        }
      }

      if (!string.IsNullOrWhiteSpace(documentFolder))
        return documentFolder;

      return VelumAppConfig.PdfBatchDefaultPdfFolder ?? string.Empty;
    }

    private static string TryGetExistingDirectory(string path)
    {
      if (string.IsNullOrWhiteSpace(path))
        return null;

      try
      {
        string fullPath = Path.GetFullPath(path.Trim());
        return Directory.Exists(fullPath) ? fullPath : null;
      }
      catch
      {
        return null;
      }
    }
  }
}
