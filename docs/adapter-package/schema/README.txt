Каталог schema\ — машиночитаемое описание возможностей адаптера для AIStudio (contract 3.2).

Студия читает эти JSON при редактировании проекта симбионта (редакторы «Среда», combobox ProbeKey
в справочнике воздействий «Давление среды на виталы» — InfluenceActions.dat, поле ProbeKey для ID ≥ 50).
Runtime host DLL для этого не нужен.

Версия формата: schemaVersion 3.2 (совпадает с contractVersion 3.2 в manifest.json).

-------------------------------------------------------------------------------------

Обязательные файлы (для «Проверить» без Error):

handlers-catalog.json
  Handler'ы для шагов type: invoke в рецептах.
  Массив handlers[]: id, label, description, argsSchema[] (key, label, type, required, values, editorHint).
  editorHint: template_placeholder | property_name | macro_file — кнопки справочника / выбора файла в редакторе шагов.

recipe-catalog.json
  Каталог допустимых ID рецептов для редактора рецептов среды.
  Массив recipes[]: id (обязателен), label, description.

command-buffer-policy.json
  Defaults Command idle-flush для host (Velum: seed в Velum.Settings.xml).
  Поля: idle_flush_ms, max_tokens, max_age_ms.

metric-probes.json
  Ключи ProbeKey для InfluenceActions.dat (Velum host, ID ≥ 50).
  Массив probes[]: key (обязателен), label, description.

  Актуальные ключи (sldworks_19 / Velum):
    Velum.Solid.Material
    Velum.Solid.Sketch.NoBadIntersections
    Velum.Solid.Sketch.NoDisjointContours
    Velum.Solid.Sketch.ContourProfile
    Velum.Solid.Sketch.FullyDefined
    Velum.Solid.Sketch.NoZeroLength
    Velum.Solid.Document.UnsavedNew
    Velum.Solid.Dxf.FileExists
    Velum.Solid.Dxf.IsOutdated
    Velum.Solid.Pdf.FileExists
    Velum.Solid.Pdf.IsOutdated

  Handler'ы экспортной документации (sldworks_19 / Velum):
    set_custom_property_if_part
    export_documentation_log_issue
    export_documentation_dialog
    export_drawing_pdf_dialog
    export_documentation_create_files

  Handler макросов (sldworks_19 / Velum):
    run_macro — RunMacro2; args: path (обязателен, editorHint macro_file), module (по умолчанию Module1), procedure (по умолчанию main)

Опциональный файл:

recipe-template-catalog.json
  Справочник масок {PLACEHOLDER} (placeholders[]) и имён свойств документа (propertyNames[]).
  Используется кнопками «Вставить…» / «Справочник…» в редакторе шагов рецепта.
  Runtime Velum разрешает шаблоны в коде (VelumRecipeTemplateResolver); этот JSON — только для UI студии.

Шаги рецепта в YAML:
  - type: invoke — handler + flat-ключи из argsSchema
  - type: comment — text (пропускается runtime)

Поведение на сборке
-------------------

Метрики материала, эскизов и экспортной документации — не обход дерева сборки.

  • Открытая деталь (SLDPRT) — опрашиваются материал, эскизы, DXF и «Документ не сохранён».
  • Открытый чертёж (SLDDRW) — опрашиваются PDF и «Документ не сохранён».
  • Активна сборка без редактирования компонента — только «Документ не сохранён» для файла сборки;
    материал, эскизы, PDF и DXF не дают значения (пропуск / нейтрально).
  • Сборка с компонентом в режиме редактирования (Edit Target, правка из дерева сборки) —
    материал, эскизы и DXF считаются по редактируемой детали (AssemblyDoc.GetEditTarget).

Формулировки «проверяемая деталь» и «режим редактирования в сборке» в описаниях проб
отражают это поведение. Агрегация по всем деталям сборки не выполняется.

Удалено (contract 3.2):
  trigger-detect.json, trigger-catalog.json, expression-pattern-catalog.json.
  EnvironmentTriggers.yaml; expression_pattern_id; recommended_trigger_keys.
  Velum.Solid.Assembly.* (метрики GetWhatsWrong / Resolve по дереву сборки).

Правило
-------

Каждый handler id / recipe id / ProbeKey в schema должен согласовываться с boot проекта и runtime host Velum.
Dispatch рецепта — по adaptive_action_id (G_AD) на OnPulseCompleted, не по expression pattern.

«Проверить»: schema\, валидный JSON, обязательные массивы
(handlers, recipes, probes) + command-buffer-policy.json.
Наличие trigger-detect.json или trigger-catalog.json — Error.
