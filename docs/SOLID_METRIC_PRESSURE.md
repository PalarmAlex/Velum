# Давление метрик SolidWorks на параметры гомеостаза

Документ описывает, как Velum переносит «плохие» метрики среды (probe) в значения параметров P_i движка ISIDA на такте `OnPulseBeforeGomeostasis`.

Связанные типы: `VelumSolidMetricPressureOrchestrator`, `VelumSolidMetricCumulativePressureTarget`, `VelumSolidMetricPressureEngageRegistry`, `VelumSolidMetricLatchedBadValue` (Clamp / IsInBadZone), `VelumSolidProbeCategoryPolicy`.

## Источники данных

| Слой | Что читает |
|------|------------|
| SolidWorks | Снимок проб по `ProbeKey` из `InfluenceActions.dat` (опубликованный gate) |
| EA среды | Шаблон влияний: `paramId → величина` (−10…+10), с масштабом слотов `VelumSolidProbeInfluenceContext` (YesSlots / TotalSlots) |
| Движок | Текущие `Value`, `Speed`, `NormaWell`, `LastState`, `DynamicTime`, `DifSensorPar` |

Порог «плохая / хорошая» метрика: `MetricProbeThresholds` через `VelumSolidMetricProbeThresholds` + `SolidEnvironmentMetricDeltaEpsilon` (Velum.Settings.xml).

Сумма вкладов bad-метрик на один P_i ограничивается `EnvironmentMetricPressureComposer.MaxDeltaPerParameterPerPulse` (±10).

## Инварианты

1. **Плохие метрики в gate** → cumulative engage: цель `NormaWell + Σ(effect × scale)` по still-bad пробам; запись в host — абсолютная цель, не инкремент каждый пульс.
2. **Хорошие метрики / release в норму** — только если снимок gate доверенный (`SnapshotUntrustworthy == false`), полный для параметра и все влияющие пробы явно good. Если в текущем edit-контексте на P_i не осталось применимых проб (например Export/DXF на сборке без edit-target), это тоже считается условием full release для ранее engaged параметра — иначе плашка гаснет, а value/состояние «Плохо» зависают.
3. **P_i уже engaged** → повторная запись пропускается, если цель не изменилась (`MaintainEpsilon = 0.02`), value уже у цели и параметр всё ещё в bad zone (`IsInBadZone`).
4. **Пропуск COM-refresh** не отменяет давление: опубликованный снимок в gate остаётся → оркестратор продолжает engage **до** COM-опроса на том же пульсе.
5. **Таймаут COM** → partial merge **не** публикуется в gate (прежние значения сохраняются); метаданные помечают timeout → `SnapshotUntrustworthy` → release блокируется.
6. **`DynamicTime`** задаёт только удержание транзиторных Well/Bad **в движке**; частота host-записей от давления метрик **не** привязана к `DynamicTime`.

## Cumulative engage

Пока на P_i есть still-bad метрики в опубликованном снимке:

1. `VelumSolidMetricCumulativePressureTarget.TryEvaluateParamFromSnapshot` суммирует scaled EA-вклады всех bad-проб на параметр.
2. Цель: `target = Clamp(NormaWell + pressureDelta)` (`VelumSolidMetricLatchedBadValue.Clamp` по CriticalMin/Max и 0…100).
3. Цель регистрируется в `VelumSolidMetricPressureEngageRegistry`.
4. Host-запись выполняется, если цель изменилась, value ушёл от цели (`|Value − target| > MaintainEpsilon`) или P_i вышел из bad zone.
5. Пока engaged + цель стабильна + всё ещё bad zone — rewrite пропускается (иначе ISIDA трактует движение к цели внутри bad zone как transient Well / `isImproving`).

Это не per-pulse increment шаблона и не фиксированный «latch» от `NormaWell ± DifSensorPar`: при отпускании одной из нескольких bad-метрик цель пересчитывается (например четыре метрики с |effect|=1 → NormaWell−4, после отпускания одной → NormaWell−3).

### Почему не инкремент каждый пульс

Инкрементальное давление создавало мерцание Bad/Normal на param с `Speed < 0`: value то уходила ниже `NormaWell`, то поднималась между ударами (CompareLevel 30%). Absolute target + hold rewrite удерживает P_i в устойчивой bad zone, пока метрики плохие.

## Release (все метрики параметра стали хорошими)

Полный возврат P_i в норму по `Speed` (100 / 0 / `NormaWell`) через `VelumSolidMetricParameterRelease.TryComposeFullNormWriteForParam`, только если:

- параметр был engaged;
- `!SnapshotUntrustworthy`;
- снимок полный для всех влияющих проб параметра **или** в текущем контексте нет применимых влияющих проб;
- все влияющие пробы явно good (`allExplicitGood`) — при нуле применимых проб это тоже true;
- нет still-bad на этот параметр.

Partial release в live-пути оркестратора **не используется** (улучшение value внутри bad zone ISIDA трактует как transient Well).

При успешном release `EngageRegistry.ClearEngaged(paramId)`.

Host-записи engage/release идут через `HostBatchUpdateParameterValues` и **не** проходят фильтр `SolidHostImpulseMinParameterDelta`.

## Context-aware polling

`VelumSolidProbeCategoryPolicy` и `VelumSolidDocumentEditContextResolver` выбирают категории проб по контексту SW:

| Контекст | Категории (policy) |
|----------|-------------------|
| Чертёж | Document + ExportDocumentation |
| Сборка без edit-target | только Document |
| Деталь / edit-target в сборке | Document + PartMaterial + ExportDocumentation |

`VelumSolidWorksHomeostasisMetrics.IsProbeAllowedInContext` дополнительно фильтрует ключи:

| Контекст | Допускается |
|----------|-------------|
| Чертёж | Document; из Export — PDF-пробы (`FileExists`, `IsOutdated`, `DrawingPathAvailable`) |
| Деталь / edit-target | PartMaterial; из Export — DXF-пробы + blank-size; также `DrawingPathAvailable` (свойство источника) |
| Сборка (корень) | Document; `DrawingPathAvailable` на самой сборке |
| Сборка без edit-target (прочие Export) | part/export DXF отсекаются |

### `Velum.Solid.Pdf.DrawingPathAvailable`

Свойство документа «путь чертежа» связывает деталь/сборку с нативным `.slddrw`. Автозапись при Save чертежа — в коде Velum (`VelumDrawingPathSavePropagator`); рецепт/мотор — точка **починки**.

| Активный документ | Когда метрика плохая | Когда хорошая |
|-------------------|----------------------|---------------|
| Деталь / сборка | Есть `Нужен pdf = Да` и путь пуст **или** файл по пути отсутствует | `Нужен pdf` нет/No, либо путь указывает на существующий `.slddrw` |
| Чертёж | Есть `Нужен pdf = Да` и у загруженных referenced источников путь пуст / битый / не на **этот** `.slddrw`; либо источники не загружены в сессию | Нет потребности в PDF; нет модельных видов; либо у всех проверенных источников путь = текущий чертёж |

**Как снять проблему (рецепт `write_drawing_path`, adaptive action 47):**

1. **Открыт чертёж** — invoke прописывает полный путь текущего `.slddrw` во все referenced детали/сборки (silent open при необходимости). Точный канал.
2. **Открыта деталь/сборка** — если путь уже валиден, no-op; если пуст или файл пропал — ищет `{каталог документа}\{имя}.slddrw` и пишет его; если не нашёл — ошибка «откройте чертёж…». Валидный чужой/library-путь **не** затирается эвристикой.
3. Опциональный аргумент `drawing_path` на детали/сборке задаёт путь явно.
4. Параллельно Save `.slddrw` в SW сам вызывает тот же helper — после обычного сохранения путь появляется без рефлекса.

Стандарт ISIDA: автоматика в коде; рефлекс с триггером → мотор → рецепт — для явной починки и операторского действия, без конкуренции за клетку level+level «под капотом».

Тот же паттерн на Save Part/Assembly (`OnModelFileSavePostNotify`):

| Автоматика (сразу после Save) | Рецепт / AA (ручная починка по триггеру) |
|-------------------------------|------------------------------------------|
| `VelumMaterialAssignOnSaveCoordinator` | `material_assign_session_learned` / 40 |
| `VelumBlankSizeLinksOnSaveCoordinator` | `ensure_blank_size_links` / 45 |
| `VelumDrawingPathSavePropagator` (Save чертежа) | `write_drawing_path` / 47 |

`VelumSolidProbeRefreshPlanner` использует policy вместо жёсткого Full bootstrap — снижает COM-нагрузку и TimedOut.

События SW только помечают категории stale:

- Part/Assembly: Regen → PartMaterial; Modify → PartMaterial + ExportDocumentation; Save → все три.
- Drawing: Modify / AddItem / DeleteItem / DimensionChange / SketchSolve / ViewNew / Undo / Redo / Regen → ExportDocumentation; Save → все три.
- Правки чертежа: события `DrawingDoc` + токен геометрии на пульсе → `NotifyDrawingModified` / `TryEnsurePdfPendingMarksOutdated` (не через `CommandOpen`: Save тоже даёт CommandOpen и ложно писал pending).
- На пульсе: сравнение токена геометрии видов (точки эскизов + число аннотаций) с baseline после PDF-экспорта / Save — ловит сдвиг линии / добавление размера без CommandOpen.
- Pending пишется как `export+1` (`TryEnsurePdfPendingMarksOutdated`); baseline токена обновляется только после успешной записи.
- После экспорта PDF: `TryFinalizePdfExportStamps` + `ArmAfterPdfExport` (глотает dirty свойств; Ensure только при смене токена; Save без правок — `NotifyDrawingSaved` снова финализирует штампы).

## Устойчивость при буферизации снимка

На такте `OnPulseBeforeGomeostasis` порядок: **сначала** engage/release по последнему опубликованному gate, **затем** асинхронный COM-refresh на UI-потоке SW (не блокирует пульс). После успешного UI-refresh давление может примениться ещё раз на том же пульсе.

При серии неполных опросов (`NotifyRefreshIncomplete`, ≥2) следующий COM откладывается (каждый 3-й пульс). COM выполняется только на STA UI-потоке панели задач.

Если gate пуст, но в кэше есть снимок — `EnsurePublishedSnapshotForPressure` восстанавливает его как stale (давление может продолжаться; release по stale/timeout заблокирован).

## Сброс состояния

Оркестратор (WasBad-состояния), pause-registry и engage-registry очищаются через `VelumSolidMetricPressureOrchestrator.Clear` при:

- остановке пульсации / sleep-reset адаптера (`VelumAdapterSleepReset`, `VelumEnginePulseBridge`);
- смене активного документа SW (`VelumSolidDocumentEpisodeReset` → **сначала** `TryReleaseAllEngagedParameters`, затем `ResetVelumAdapterState`);
- отсутствии активного документа (`VelumSolidMetricPressureReset.OnNoActiveSolidDocument` — release engaged + stranded env-bad P_i, затем Clear); на пульсе без документа — safety-net, если остались engage/gate;
- ручном «Н» (`OnManualNormHomeostasis` → ClearPublishedMetricState).

При смене документа A→B: документные пробы снимаются из gate (`StripNonHostGlobalFromPublishedGate`, host-global реестра сохраняется), затем снимок помечается stale; in-flight COM-опрос предыдущего документа отбрасывается по generation. Жёсткий Clear gate/cache — при отсутствии документа и при stop с `clearMetricGateSnapshot`. Без strip оркестратор успевал снова engage по метрикам старого файла до COM-опроса нового — «фокус внимания» и подсказка с пульта оставались чужими.

Ручное отключение метрики в чек-листе (`VelumEnvironmentMetricsPickerForm` → `ClearPausesForProbeKey`) снимает pause-флаги для probeKey; само давление останавливается деактивацией EA в каталоге.

## Поток на одном пульсе

```
OnPulseBeforeGomeostasis
  → EnsurePublishedSnapshotForPressure (восстановить из cache, если gate пуст)
  → PrunePublishedSnapshotForEditContext
  → ApplyMetricPressureBeforeGomeostasis:
       SyncWithGomeostasis (pause-registry)
       classify probes from gate
       cumulative engage OR full release (если trusted + complete + all good)
       HostBatchUpdate: сначала release, затем engage (без minDelta)
  → ScheduleSolidProbeRefresh (async UI COM, context-aware categories)
       → при успехе Publish + возможно повторный ApplyMetricPressure
       → при timeout: gate unchanged, SnapshotUntrustworthy
```

## Режим наблюдения

При `ObservationMode` оркестратор не составляет host-writes; bridge дополнительно не вызывает `HostBatchUpdate`. Давление в движок не уходит.

## Настройки

| Ключ | Назначение для метрик среды |
|------|----------------------------|
| `DynamicTime` | Удержание Well/Bad в ISIDA; **не** задаёт частоту host-записей |
| `DifSensorPar` | Чувствительность зон/удержания в ISIDA; **не** задаёт цель cumulative engage |
| `MaintainEpsilon` (код, 0.02) | Порог «цель изменилась» / «value уже у цели» для engage/release rewrite |
| `SolidHostImpulseMinParameterDelta` | **Не** применяется к engage/release метрик среды |
| `SolidProbeTimeoutMs` | Бюджет COM-опроса; при превышении gate не обновляется partial merge |
| `SolidEnvironmentMetricDeltaEpsilon` | ε порогов bad/good и отсечения микро-вкладов composite scale |
| `ObservationMode` | Запрет host-записей давления метрик в гомеостаз |
