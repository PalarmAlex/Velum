using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ISIDA.Common;
using ISIDA.SymbiontEnv.Contract;
using Velum.Configuration;
using Velum.ReactiveCore.Export;

namespace Velum.ReactiveCore.DocumentTemplates
{
  /// <summary>
  /// Каталог библиотеки шаблонов документов: резолвинг корня, загрузка
  /// <c>document-templates.json</c>, кэш шаблонов по <c>templateId</c>,
  /// проверка существования файлов шаблонов через <c>VelumPathExists</c>.
  /// </summary>
  /// <remarks>
  /// Резолвинг корня выполняется через VelumPathExists в UI-потоке SW.
  /// Все публичные члены имеют XML-комментарии.
  /// </remarks>
  public sealed class VelumDocumentTemplateCatalog
  {
    private readonly object _sync = new object();
    private readonly Dictionary<string, EnvironmentDocumentTemplateData> _byId =
        new Dictionary<string, EnvironmentDocumentTemplateData>(StringComparer.OrdinalIgnoreCase);
    private readonly List<EnvironmentDocumentTemplateData> _all = new List<EnvironmentDocumentTemplateData>();
    private string _activeRoot;
    private List<string> _errors = new List<string>();
    private List<string> _warnings = new List<string>();

    /// <summary>
    /// Активный корень библиотеки (пусто, если не загружен).
    /// </summary>
    public string ActiveRoot
    {
      get
      {
        lock (_sync)
          return _activeRoot;
      }
    }

    /// <summary>Все загруженные шаблоны (стабильный порядок).</summary>
    public IReadOnlyList<EnvironmentDocumentTemplateData> Templates
    {
      get
      {
        lock (_sync)
          return new List<EnvironmentDocumentTemplateData>(_all);
      }
    }

    /// <summary>Ошибки последней загрузки.</summary>
    public IReadOnlyList<string> Errors
    {
      get
      {
        lock (_sync)
          return new List<string>(_errors);
      }
    }

    /// <summary>Предупреждения последней загрузки.</summary>
    public IReadOnlyList<string> Warnings
    {
      get
      {
        lock (_sync)
          return new List<string>(_warnings);
      }
    }

    /// <summary>
    /// Загружает каталог из активного корня библиотеки (проектный → общий).
    /// </summary>
    /// <returns>true, если хотя бы один шаблон загружен без ошибок.</returns>
    public bool Load()
    {
      lock (_sync)
      {
        _byId.Clear();
        _all.Clear();
        _errors.Clear();
        _warnings.Clear();
        _activeRoot = null;

        string projectRoot = VelumAppConfig.TemplatesFolderPath;
        if (string.IsNullOrWhiteSpace(projectRoot))
          projectRoot = VelumAppConfig.DefaultProjectTemplatesFolderPath;

        string sharedRoot = VelumAppConfig.SharedTemplatesFolderPath;

        string root = VelumDocumentTemplateCatalogRules.ResolveLibraryRoot(
            projectRoot, sharedRoot,
            path => VelumPathExists.FileExists(path));

        if (string.IsNullOrEmpty(root))
        {
          _warnings.Add("Корень библиотеки шаблонов не найден (проектный/общий).");
          return false;
        }

        _activeRoot = root;
        string manifest = VelumDocumentTemplateCatalogRules.GetLibraryFilePath(root);
        if (string.IsNullOrEmpty(manifest))
        {
          _errors.Add("Не удалось резолвить путь к manifest в корне: " + root);
          return false;
        }

        var templates = new List<EnvironmentDocumentTemplateData>();
        if (!DocumentTemplatesJsonCodec.TryLoad(manifest, out templates, out string readError))
        {
          _errors.Add("Ошибка загрузки " + manifest + ": " + readError);
          return false;
        }

        _all.AddRange(templates);

        // Статическая валидация библиотеки (без обращения к ФС: наличие файлов
        // шаблонов проверяется отдельно через TemplateFileExists в UI-потоке SW).
        IReadOnlyList<ValidationMessage> messages = DocumentTemplateLibraryValidator.Validate(_all, null);
        foreach (ValidationMessage message in messages)
        {
          if (message == null)
            continue;

          string text = message.Text ?? string.Empty;
          if (message.Severity == ValidationSeverity.Error)
            _errors.Add(text);
          else
            _warnings.Add(text);
        }

        // Индекс по templateId.
        foreach (EnvironmentDocumentTemplateData t in _all)
        {
          if (t == null || string.IsNullOrWhiteSpace(t.TemplateId))
            continue;

          string key = t.TemplateId.Trim();
          if (_byId.ContainsKey(key))
          {
            _warnings.Add("Дубликат templateId '" + key + "' — используется первая загрузка.");
            continue;
          }
          _byId[key] = t;
        }

        return _byId.Count > 0;
      }
    }

    /// <summary>
    /// Найти шаблон по <c>templateId</c>.
    /// </summary>
    /// <param name="templateId">Идентификатор шаблона.</param>
    /// <returns>Объект шаблона либо null.</returns>
    public EnvironmentDocumentTemplateData FindByTemplateId(string templateId)
    {
      if (string.IsNullOrWhiteSpace(templateId))
        return null;

      lock (_sync)
      {
        return _byId.TryGetValue(templateId.Trim(), out EnvironmentDocumentTemplateData result)
            ? result : null;
      }
    }

    /// <summary>
    /// Получить полный путь к файлу шаблона внутри активной библиотеки.
    /// </summary>
    /// <param name="template">Шаблон.</param>
    /// <returns>Полный путь к файлу шаблона либо пустая строка.</returns>
    public string ResolveTemplateFilePath(EnvironmentDocumentTemplateData template)
    {
      if (template == null || string.IsNullOrWhiteSpace(template.TemplateFile))
        return string.Empty;

      string root;
      lock (_sync)
        root = _activeRoot;

      if (string.IsNullOrEmpty(root))
        return string.Empty;

      return VelumDocumentTemplateCatalogRules.ResolveTemplateFile(root, template.TemplateFile);
    }

    /// <summary>
    /// Проверяет, существует ли файл шаблона на диске (через VelumPathExists).
    /// </summary>
    /// <param name="template">Шаблон.</param>
    /// <returns>true, если файл существует.</returns>
    public bool TemplateFileExists(EnvironmentDocumentTemplateData template)
    {
      string path = ResolveTemplateFilePath(template);
      return VelumPathExists.FileExists(path);
    }

    /// <summary>
    /// Возвращает шаблоны заданного типа документа.
    /// </summary>
    /// <param name="docType">Тип: "part", "asm", "drw".</param>
    /// <returns>Список шаблонов указанного типа.</returns>
    public IReadOnlyList<EnvironmentDocumentTemplateData> FindByDocumentType(string docType)
    {
      if (string.IsNullOrWhiteSpace(docType))
        return Array.Empty<EnvironmentDocumentTemplateData>();

      string type = docType.Trim().ToLowerInvariant();
      lock (_sync)
      {
        var result = new List<EnvironmentDocumentTemplateData>();
        foreach (EnvironmentDocumentTemplateData t in _all)
        {
          if (t == null)
            continue;
          string dt = (t.DocumentType ?? string.Empty).Trim().ToLowerInvariant();
          if (string.Equals(dt, type, StringComparison.Ordinal))
            result.Add(t);
        }
        return result;
      }
    }

    /// <summary>Сбрасывает кэш и загружает заново.</summary>
    public void Reload()
    {
      lock (_sync)
        Load();
    }
  }
}
