#!/usr/bin/env python3
"""Sync sldworks_19 adapter package in ProgramData from Velum adapter-package + bin\\Debug."""

from __future__ import annotations

import json
import shutil
from pathlib import Path

import markdown

PACKAGE_DIR = Path(r"C:\ProgramData\ISIDA\Adapters\sldworks_19")
ADAPTER_PACKAGE = Path(r"D:\VELUM\velum\docs\adapter-package")
VELUM_BIN = Path(r"D:\VELUM\velum\bin\Debug")
SOURCE_MD = Path(r"D:\ISIDA\Programms\app\AIStudio\docs\AdapterContract.md")
VELUM_DOCS = Path(r"D:\VELUM\velum\docs")
AISTUDIO_DOCS = Path(r"D:\ISIDA\Programms\app\AIStudio\docs")

ADAPTER_ID = "sldworks_19"
ADAPTER_DISPLAY_NAME = "Адаптер SolidWorks 2019 (Velum)"

RUNTIME_FILES = (
    "velum.dll",
    "isida.dll",
    "isida.dll.config",
    "isida.xml",
    "SymbiontEnv.Contract.dll",
    "SymbiontEnv.Contract.xml",
    "Newtonsoft.Json.dll",
)

LEGACY_SCHEMA_FILES = ("expression-pattern-catalog.json", "trigger-detect.json", "trigger-catalog.json")
LEGACY_BOOT_FILES = ("EnvironmentTriggers.yaml",)

HTML_HEAD = """<!DOCTYPE html>
<html lang="ru">
<head>
  <meta charset="utf-8"/>
  <meta name="viewport" content="width=device-width, initial-scale=1"/>
  <title>Контракт платформы адаптеров среды — sldworks_19</title>
  <style>
    * { box-sizing: border-box; }
    html, body { margin: 0; padding: 0; height: 100%; }
    body {
      font-family: "Segoe UI", Tahoma, Geneva, Verdana, sans-serif;
      font-size: 14px; line-height: 1.5; color: #1a1a1a; background: #fff;
    }
    .help-content {
      max-width: 960px; margin: 0 auto; padding: 28px 40px 48px;
    }
    h1 {
      font-size: 26px; font-weight: 300; color: #005a9e;
      margin: 0 0 8px; padding-bottom: 8px; border-bottom: 1px solid #d6d6d6;
    }
    h2 { font-size: 20px; color: #005a9e; margin: 2rem 0 0.75rem; border-bottom: 1px solid #eee; padding-bottom: 0.2em; }
    h3 { font-size: 16px; margin: 1.5rem 0 0.5rem; }
    h4 { font-size: 14px; margin: 1rem 0 0.5rem; }
    p, li { margin: 0.5rem 0; }
    code, pre { font-family: Consolas, "Courier New", monospace; font-size: 13px; }
    pre {
      background: #f6f8fa; padding: 12px 16px; overflow-x: auto;
      border: 1px solid #e1e4e8; border-radius: 4px;
    }
    table { border-collapse: collapse; width: 100%; margin: 1rem 0; }
    th, td { border: 1px solid #d6d6d6; padding: 8px 12px; text-align: left; vertical-align: top; }
    th { background: #f3f3f3; }
    hr { border: none; border-top: 1px solid #d6d6d6; margin: 2rem 0; }
    a { color: #005a9e; }
    blockquote { margin: 1rem 0; padding: 0.5rem 1rem; border-left: 4px solid #d6d6d6; color: #444; }
  </style>
</head>
<body>
<main class="help-content">
"""

HTML_TAIL = """
</main>
</body>
</html>
"""

README = """# Пакет адаптера SolidWorks 19 (`sldworks_19`)

Зарегистрированный пакет среды **Velum** для SolidWorks 2019. AIStudio использует его при создании проекта симбионта с типом среды `sldworks_19`.

**Версии:** `contractVersion` и `schemaVersion` в `manifest.json` — **`3.1`**.

**Нормативная модель:** G_AD + Command idle-flush + `homeostasis_deltas`. Contract v3.0 (Expression, `expression_pattern_id`, `IHostMotorDispatcher`) **не поддерживается**.

## Архитектура

| Документ | Путь |
|----------|------|
| Архитектура ISIDA ↔ адаптер | `D:\\VELUM\\Velum\\docs\\SymbiontArchitecture_IsidaAdapter.md` |
| План рефакторинга каналов | `D:\\VELUM\\Velum\\docs\\RefactoringPlan_OperatorEnvironmentChannels.md` |
| Контракт YAML/пакета (этот каталог) | `AdapterContract.md` / `AdapterContract.html` |

Runtime host (Velum): dispatch рецептов по **`adaptive_action_id`** (G_AD) на `OnPulseCompleted`; SW-команды — **Command idle-flush**; механика — **pressure rules** → P_i. Auto-path через EA для событий SW **запрещён**.

## Структура пакета

| Путь | Назначение |
|------|------------|
| `manifest.json` | Метаданные; `id`: `sldworks_19`; `contractVersion` / `schemaVersion`: `3.2` |
| `BootData/Environment/EnvironmentRecipes.yaml` | Образец рецептов (seed проекта; **обязателен** `schema: environment-recipes/3.2`) |
| `schema/*.json` | Capability для редакторов «Среда» (см. `schema/README.txt`) |
| `runtime/` | `velum.dll`, `isida.dll`, `SymbiontEnv.Contract.dll` |
| `AdapterContract.md` / `.html` | Норматив contract 3.2 (синхронизирован с AIStudio) |

## Schema (contract 3.2)

| Файл | Назначение |
|------|------------|
| `handlers-catalog.json` | Handler'ы для шагов `type: invoke` |
| `recipe-catalog.json` | Каталог ID рецептов |
| `command-buffer-policy.json` | Defaults idle-flush (`idle_flush_ms`, `max_tokens`, `max_age_ms`) |
| `metric-probes.json` | ProbeKey для «Давление среды на виталы» |
| `recipe-template-catalog.json` | **Опционально:** маски `{PLACEHOLDER}` и имена свойств для редактора шагов |

**Удалено в v3.2:** `trigger-detect.json`, `trigger-catalog.json`, `EnvironmentTriggers.yaml`, `expression-pattern-catalog.json`, `Velum.Solid.Assembly.*` (метрики сборки).

## YAML (runtime contract)

- **Рецепты:** `schema: environment-recipes/3.2`, `adaptive_action_id`, шаги `invoke` / `comment`.
- Mechanical path — `InfluenceActions.dat` (ProbeKey, ID ≥ 50); пуск Command — `GeneticReflexes.dat` → `command_pattern_ids`.

Подробнее — `AdapterContract.md`. Практика сборки пакета — `D:\\ISIDA\\Programms\\app\\AIStudio\\docs\\AdapterAuthorGuide.md`.

## Runtime vs schema

`VelumRecipeTemplateResolver` разрешает шаблоны при исполнении рецепта (контекст SW-сессии).  
`recipe-template-catalog.json` нужен **только AIStudio** — справочник масок в UI; host может не читать этот файл.

## Миграция с contract 3.0

1. Обновить `manifest.json` и все `schema/*.json` до `schemaVersion: "3.1"`.
2. Добавить `command-buffer-policy.json`; удалить `expression-pattern-catalog.json`.
3. Переписать BootData YAML: добавить `schema:`, заменить `expression_pattern_id` → `adaptive_action_id`, `event` → `event_kind`.
4. Переписать BootData **открытых проектов** (seed не перезаписывает существующие файлы).

См. § 16 в `AdapterContract.md`.
"""

SCHEMA_README = """Каталог schema\\ — машиночитаемое описание возможностей адаптера для AIStudio (contract 3.2).

Студия читает эти JSON при редактировании проекта симбионта (редакторы «Среда», combobox ProbeKey
в справочнике воздействий «Давление среды на виталы» — InfluenceActions.dat, поле ProbeKey для ID ≥ 50).
Runtime host DLL для этого не нужен.

Версия формата: schemaVersion 3.2 (совпадает с contractVersion 3.2 в manifest.json).

-------------------------------------------------------------------------------------

Обязательные файлы (для «Проверить» без Error):

handlers-catalog.json
  Handler'ы для шагов type: invoke в рецептах.
  Массив handlers[]: id, label, description, argsSchema[] (key, label, type, required, values, editorHint).
  editorHint: template_placeholder | property_name — кнопки справочника в редакторе шагов.

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
    Velum.Solid.Document.UnsavedNew

  Удалены (runtime Velum их не опрашивает):
    Velum.Solid.Sketch.NoBadIntersections
    Velum.Solid.Sketch.NoDisjointContours
    Velum.Solid.Sketch.ContourProfile
    Velum.Solid.Sketch.FullyDefined
    Velum.Solid.Sketch.NoZeroLength
    Velum.Solid.Assembly.WhatsWrong.Critical
    Velum.Solid.Assembly.WhatsWrong.Warnings
    Velum.Solid.Assembly.Resolve

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

Метрики материала и экспортной документации — пробы уровня детали, не обход дерева сборки.

  • Открытая деталь (SLDPRT) — опрашиваются материал и «Документ не сохранён».
  • Активна сборка без редактирования компонента — только «Документ не сохранён» для файла сборки;
    материал не даёт значения (пропуск / нейтрально).
  • Сборка с компонентом в режиме редактирования (Edit Target, правка из дерева сборки) —
    материал считается по редактируемой детали (AssemblyDoc.GetEditTarget).

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

«Проверить»: schema\\, валидный JSON, обязательные массивы
(handlers, recipes, probes) + command-buffer-policy.json.
Наличие trigger-detect.json или trigger-catalog.json — Error.
"""

BOOTDATA_README = """Каталог BootData\\Environment\\ — образцы YAML каталогов среды для нового проекта симбионта (contract 3.2).

При создании проекта или «дополнить BootData из пакета» студия копирует файлы в BootData проекта
(без перезаписи уже существующих). Дальше редакторы «Среда» работают с YAML проекта, не с пакетом.

Файлы
-----

EnvironmentRecipes.yaml

  Каталог рецептов: моторика host по adaptive_action_id (G_AD из AdaptiveActions.dat).

  Корневой ключ schema: environment-recipes/3.2 и recipes: — массив рецептов.
  Dispatch — rising edge ActiveAdaptiveActions на OnPulseCompleted.

  Минимальный каркас:

    schema: environment-recipes/3.2
    recipes: []

  Пример (sldworks_19 / Velum):

    schema: environment-recipes/3.2
    recipes:
      - id: kb_name_on_save
        display_name: Наименование по КБ после Save
        adaptive_action_id: 37
        reactive_eligible: true
        steps:
          - type: invoke
            handler: save_file_name

  Запрещено (contract 3.2): expression_pattern_id, recommended_trigger_keys,
  genetic_reflex_id, influence_action_id, EnvironmentTriggers.yaml.

Mechanical path и Command-пуск — не в этом YAML:
  - InfluenceActions.dat (ProbeKey, воздействия на виталы, ID ≥ 50)
  - GeneticReflexes.dat (command_pattern_ids)

Формат
------

SymbiontEnv.Contract (EnvironmentYamlCodec), contractVersion 3.2 в manifest.json.
Норма полей — AdapterContract.md в корне пакета.

Шаги invoke: handler + flat-ключи argsSchema (строковый args не поддерживается).
Маски шаблона значения — schema\\recipe-template-catalog.json (справочник в UI студии).

«Проверить»: разбор YAML; отсутствие schema: environment-recipes/3.2 или legacy ключи — Error.
Наличие EnvironmentTriggers.yaml в пакете — Error.
"""


def adapt_markdown(text: str) -> str:
    package_note = (
        "\n\n> **Пакет:** `%ProgramData%\\ISIDA\\Adapters\\sldworks_19` — адаптер Velum для SolidWorks 2019. "
        "Экземпляр contract 3.1; `id` в manifest — `sldworks_19` (не `velum` из эталона Velum).\n"
    )
    marker = "**Нормативная модель v3.1:**"
    idx = text.find(marker)
    if idx >= 0:
        line_end = text.find("\n", idx)
        if line_end >= 0:
            text = text[: line_end + 1] + package_note + text[line_end + 1 :]

    replacements = {
        "[`SymbiontArchitecture_IsidaAdapter.md`](../../../../VELUM/Velum/docs/SymbiontArchitecture_IsidaAdapter.md) (v1.4+)":
            f"[`SymbiontArchitecture_IsidaAdapter.md`](file:///{VELUM_DOCS.as_posix()}/SymbiontArchitecture_IsidaAdapter.md) (v1.4+)",
        "[`RefactoringPlan_OperatorEnvironmentChannels.md`](../../../../VELUM/Velum/docs/RefactoringPlan_OperatorEnvironmentChannels.md) (v1.6+)":
            f"[`RefactoringPlan_OperatorEnvironmentChannels.md`](file:///{VELUM_DOCS.as_posix()}/RefactoringPlan_OperatorEnvironmentChannels.md) (v1.6+)",
        "[`AdapterAuthorGuide.md`](AdapterAuthorGuide.md)":
            f"[`AdapterAuthorGuide.md`](file:///{AISTUDIO_DOCS.as_posix()}/AdapterAuthorGuide.md)",
        "[`AdapterPlatform_ImplementationPlan.md`](AdapterPlatform_ImplementationPlan.md)":
            f"[`AdapterPlatform_ImplementationPlan.md`](file:///{AISTUDIO_DOCS.as_posix()}/AdapterPlatform_ImplementationPlan.md)",
        '[`SymbiontArchitecture_IsidaAdapter.md`](../../../../VELUM/Velum/docs/SymbiontArchitecture_IsidaAdapter.md)':
            f"[`SymbiontArchitecture_IsidaAdapter.md`](file:///{VELUM_DOCS.as_posix()}/SymbiontArchitecture_IsidaAdapter.md)",
        '[`RefactoringPlan_OperatorEnvironmentChannels.md`](../../../../VELUM/Velum/docs/RefactoringPlan_OperatorEnvironmentChannels.md)':
            f"[`RefactoringPlan_OperatorEnvironmentChannels.md`](file:///{VELUM_DOCS.as_posix()}/RefactoringPlan_OperatorEnvironmentChannels.md)",
        '"id": "velum"':
            f'"id": "{ADAPTER_ID}"',
        '"displayName": "Velum — SolidWorks / Reactive Core"':
            f'"displayName": "{ADAPTER_DISPLAY_NAME}"',
    }
    for old, new in replacements.items():
        text = text.replace(old, new)
    return text


def md_to_html(md_text: str) -> str:
    body = markdown.markdown(
        md_text,
        extensions=["tables", "fenced_code", "nl2br", "sane_lists"],
        output_format="html5",
    )
    return HTML_HEAD + body + HTML_TAIL


def write_manifest() -> None:
    source = json.loads((ADAPTER_PACKAGE / "manifest.json").read_text(encoding="utf-8"))
    manifest = {
        "id": ADAPTER_ID,
        "displayName": ADAPTER_DISPLAY_NAME,
        "version": source.get("version", "1.0.0"),
        "contractVersion": source.get("contractVersion", "3.2"),
        "schemaVersion": source.get("schemaVersion", "3.2"),
        "author": source.get("author", "VELUM"),
        "bootDataRelativePath": source.get("bootDataRelativePath", "BootData"),
        "description": source.get(
            "description",
            "G_AD dispatch, Command idle-flush, pressure rules (contract 3.2).",
        ),
    }
    path = PACKAGE_DIR / "manifest.json"
    path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def sync_schema() -> None:
    src = ADAPTER_PACKAGE / "schema"
    dst = PACKAGE_DIR / "schema"
    dst.mkdir(parents=True, exist_ok=True)
    for file in src.iterdir():
        if file.is_file():
            shutil.copy2(file, dst / file.name)
    for legacy in LEGACY_SCHEMA_FILES:
        legacy_path = dst / legacy
        if legacy_path.exists():
            legacy_path.unlink()
    readme_src = src / "README.txt"
    if not readme_src.is_file():
        (dst / "README.txt").write_text(SCHEMA_README, encoding="utf-8", newline="\r\n")


def sync_bootdata() -> None:
    src = ADAPTER_PACKAGE / "BootData" / "Environment"
    dst = PACKAGE_DIR / "BootData" / "Environment"
    dst.mkdir(parents=True, exist_ok=True)
    for file in src.iterdir():
        if file.is_file() and (
            file.suffix.lower() in {".yaml", ".yml"} or file.name.lower() == "readme.txt"
        ):
            shutil.copy2(file, dst / file.name)
    for legacy in LEGACY_BOOT_FILES:
        legacy_path = dst / legacy
        if legacy_path.exists():
            legacy_path.unlink()
    readme_src = ADAPTER_PACKAGE / "BootData" / "Environment" / "README.txt"
    if not readme_src.is_file():
        (dst / "README.txt").write_text(BOOTDATA_README, encoding="utf-8", newline="\r\n")


def sync_runtime() -> None:
    dst = PACKAGE_DIR / "runtime"
    dst.mkdir(parents=True, exist_ok=True)
    missing = []
    for name in RUNTIME_FILES:
        src = VELUM_BIN / name
        if not src.is_file():
            missing.append(str(src))
            continue
        shutil.copy2(src, dst / name)
    if missing:
        raise SystemExit("Runtime files not found:\n" + "\n".join(missing))


def sync_docs() -> None:
    if not SOURCE_MD.is_file():
        raise SystemExit(f"Source not found: {SOURCE_MD}")
    md_text = adapt_markdown(SOURCE_MD.read_text(encoding="utf-8"))
    html_text = md_to_html(md_text)
    (PACKAGE_DIR / "AdapterContract.md").write_text(md_text, encoding="utf-8", newline="\r\n")
    (PACKAGE_DIR / "AdapterContract.html").write_text(html_text, encoding="utf-8", newline="\r\n")
    (PACKAGE_DIR / "README.md").write_text(README, encoding="utf-8", newline="\r\n")


def main() -> None:
    if not ADAPTER_PACKAGE.is_dir():
        raise SystemExit(f"Adapter package not found: {ADAPTER_PACKAGE}")
    PACKAGE_DIR.mkdir(parents=True, exist_ok=True)

    write_manifest()
    sync_schema()
    sync_bootdata()
    sync_runtime()
    sync_docs()

    print(f"Synced package: {PACKAGE_DIR}")
    print(f"  manifest.json -> id={ADAPTER_ID}, contractVersion=3.2")
    print(f"  schema/ from {ADAPTER_PACKAGE / 'schema'}")
    print(f"  BootData/Environment/ from {ADAPTER_PACKAGE / 'BootData' / 'Environment'}")
    print(f"  runtime/ from {VELUM_BIN}")


if __name__ == "__main__":
    main()
