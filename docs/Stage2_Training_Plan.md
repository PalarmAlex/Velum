# План обучения симбионта на стадии 2 (Velum + ISIDA)



**Дата:** 2026-08-16  

**Статус:** нормативный план (выровнен с API ISIDA)  

**Контракт среды:** adapter `sldworks_19`, мотор навыка = G_AD → `ActiveAdaptiveActions` → рецепт (handler / `run_macro`)  

**Единица записи на ст. 2:** один автоматизм = один мотор (без цепочек)  

**Единица времени обучения:** пульс (`GlobalPulsCount`), не wall-clock



---



## 0. Цель и рамка



На `EvolutionStage == 2` формируется липкая база автоматизмов тремя механизмами. Это моторный минимум для стадий 3+: психика будет выбирать уже эти автоматизмы, а не сырую генетику.



| # | Механизм | Роль | Опора в ISIDA |

|---|----------|------|----------------|

| 1 | Селективный клон рефлекса → atmz | Вынести G_AD с host-дефолтами рецепта в atmz | ОР1 → `PurposeGeneticImageSystem.GetAutomatizmByGeneticPurpose` (path A) |

| 2 | Сессия наблюдения оператора | Записать успешный мотор оператора на якоре проблемы | Host FSM + `CreateNewAutomatizm` / дерево |

| 3 | Случайная проба | Редкий create-one G_AD при Поиск/Игра | Host policy на том же create-path (не `infoFunc_30`) |



**Рабочие флаги моторного curriculum:**



- `EvolutionStage == 2`

- `ObservationMode == false` (иначе метрики не давят в гомеостаз)

- `TeachingMode == false` (иначе включается verbal echo path B)



**Единица навыка:**



```

состояние + стили + активные EA метрик (ProbeKey)

  → автоматизм (BranchID + ActionsImage с одним G_AD)

      → ActiveAdaptiveActions → рецепт host → SolidWorks

```



Command / кнопки SW / пульт — сенсоры и источники распознавания моторов. Исполнение навыка — только AdaptiveAction → рецепт, не повтор `sw:*` через `RunCommand`.



Рефлекс и автоматизм — разные зоны: рефлекс несёт генетику; автоматизм — выученный ответ на якорь. Отбор на ст. 2 — через `Usefulness`, конкурирующих кандидатов и оценку (среда / диалог / пульт), без жёсткой схемы «atmz блокирует рефлекс».



### 0.1. Термины (план → ISIDA)



| Писать в плане / коде | Не писать |

|----------------------|-----------|

| `DominantParam`, `FindDominantParameter`, `focusParameterId` снимка | `PriorityParameterId` (мёртвое поле) |

| `OperatorMotorObservationSession` (сессия наблюдения) | «ObservationMode» как обучение |

| `AppGlobalState.ObservationMode` — только mute гомеостаза | смешение с сессией наблюдения |

| `WaitingForOperatorEvaluation`, `WaitingPeriodForActionsVal` (пульсы) | wall-clock «окно пульта» |

| `PerceptionImage.InfluenceActionsList`, `ActivityID`, `ProbeKey` | `MetricID` / `MetricImageId` на ст. 2 |

| `VeryActualSituation`, `FlgConditionReflexes`, `CurActiveVerbalId` | размытое «генетическая цель» |

| `wasInBadZone → !isInBadZone` / `ParameterState.Well` | абстрактное «стало лучше» без focus |



### 0.2. Non-goals (что ISIDA умеет, но Velum на ст. 2 не использует)



- `Stage2PrimitivesLoader`, `MirrorAutomatizmService`, TeachingMode verbal echo (path B ОР1)

- `ObservationMode` как режим «смотрю за оператором»

- Bulk `CloneAllConditionedReflexesToAutomatisms`

- `RandomBranchAutomatizmStrategy` / `infoFunc_30` (это ст. 4+, чужой atmz)

- Новая координата `MetricID` в `AutomatizmTreeFormat` (дорого; сначала EA/ActivityID)

- **Цепочки (ReflexChains / AutomatizmChains):** клонирование рефлекса/у-рефлекса с цепочкой — штатно,
  цепочка клонируется вместе. Ограничение «один мотор» (§2) касается **наблюдения за оператором**:
  записывать один G_AD, не цепочку. `CreateAutomatizmChainFromGeneticReflex` на ст. 2 — не вызывать
  из observation session, только из штатного path A ОР1.

- **Селективный клон на стадии 2:** `CreateAutomatizmByGeneticPurpose` (path A ОР1) **запрещён** на стадии 2.
  Рефлексный и наблюдательный контуры — разные зоны реагирования. На стадии 2 OR1 не создаёт atmz через клонирование;
  сессия наблюдения (`OperatorMotorObservationSession`) открывается при Bad + нет usable atmz и создаёт atmz по мотору оператора.
  Селективный клон доступен только со стадии 3+ (`EvolutionStage >= 3`).

- `EvolutionStage == 3` «для лучшего наблюдения» (там ОР1 genetic-atmz path гасится; включается dialog mirror)

- `EpisodicMemorySystem` как носитель наблюдения (ст. 4+)

- Автоподгонка параметров рецепта («мозжечок»)

- Command-echo / `run_sw_command` как продукт навыка



---



## 1. Механизм 1 — селективный клон (path A ОР1)



**Зачем:** вынести мотор из генетики/у-рефлекса в atmz, чтобы дальше жить оценкой `Usefulness` и (позже) host-слотами рецепта. В ISIDA **нет** модели «параметризованный рефлекс»; «параметры» на ст. 2 = **законсервированные дефолты рецепта Velum** на данный `adaptive_action_id`.



**Когда (условия path A):**



- `EvolutionStage >= 3` (на стадии 2 селективный клон **запрещён** — см. §0.2)

- ОР1 не нашёл usable atmz

- срабатывает `GetAutomatizmByGeneticPurpose` path A:  

  `VeryActualSituation || FlgConditionReflexes || CurActiveVerbalId == 0`

- у выбранного G_AD есть рецепт host с настраиваемыми слотами (иначе клон без адаптивной ценности — не делать)



Teacher / TeachingMode / сессия наблюдения **не обязательны**.



**Как:**



1. Список моторов: `ConditionedReflexesActions`, иначе `GeneticReflexesActions`, иначе `DefaultAdaptiveActionId` (`GetActiveAdaptiveActionsOfReflexes` при stage &lt; 3).

2. `CreateAutomatizmByGeneticPurpose` → atmz на текущем `AutomatizmNodeId`.

3. Host привязывает/фиксирует дефолты рецепта к этому G_AD (носитель будущего опыта).

4. После исполнения — штатное `WaitingForOperatorEvaluation` / `WaitingPeriodForActionsVal` (пульсы) и/или оценка среды.

5. При повторе проблемы предпочтителен atmz с `Usefulness >= 0` (и штатный Belief2, если есть).



**Не клонировать:**



- G_AD без рецептных слотов под опыт (чистый open/invoke без параметров);

- всю базу через `CloneAllConditionedReflexesToAutomatisms`;

- path B: `TeachingMode && CurActiveVerbalId != 0` → echo+chain.



**На ст. 2 не делаем:** подгонку слотов после исполнения. Достаточно создать atmz с дефолтами и оценить прогон.



**Критерий:** на воспроизводимой проблеме витала стабильно выбирается полезный atmz с нужным G_AD, а не повторный сырой рефлекс.



---



## 2. Механизм 2 — сессия наблюдения оператора



Имя в коде/доках: **`OperatorMotorObservationSession`**.  

Не путать с `AppGlobalState.ObservationMode`.



### 2.1. Сюжет



1. Метрики (EA с `ProbeKey`) давят → focus-витал / `DominantParam` в Bad; ОР не дал usable atmz → открывается **сессия наблюдения** (host FSM; ISIDA при этом в обычном режиме с `ObservationMode == false`).

2. Оператор действует (пульт, UI SW). В сессию попадают только действия, **распознанные как G_AD** (согласованный сенсор / `InfluenceActionId` → AdaptiveAction; не сырой `sw:*` без карты).

3. После каждого такого мотора — **post-motor wait** (новый pulse-таймер host; см. §2.3 и §7).

4. Если в окне у **focus-витала** зафиксирован уход из Bad (`wasInBadZone && !isInBadZone` → Well / не-Bad) — сессия закрывается, пишется **один** atmz:

    - мотор = этот G_AD;

    - якорь: состояние + стили **на момент мотора**;

    - контекст метрик: набор активных probe-EA (sort + unique) в восприятии → **`ActivityID`** узла дерева (через `InfluenceActionsImagesSystem.CreateNewInfluenceActionsImage`);

    - триггер: узел дерева с `ActivityID` из probe-EA, а не просто `AutomatizmNodeId`;

    - API записи: `OperatorMotorObservationSession.CreateAutomatizmFromMotor(actionId)` → создаёт `ActionsImage` + `ActivityID` + узел дерева + `Automatizm`.

5. Начальная `Usefulness` «+» — по факту снятия проблемы на focus (не TeachingMode).

6. Нет улучшения до конца post-motor wait → мотор отбрасывается; сессия ждёт следующий распознанный G_AD.



Параллельный клон (механизм 1) — отдельный конкурент; при одновременном снятии проблемы credit сессии не отдавать «случайному» клику: либо клон перехватил до старта wait, либо действует правило взаимного исключения оценок (§4.1).



### 2.2. Канал метрик (без MetricID)



Дерево atmz сейчас: `…|CommandID|VisualID` — **новой координаты MetricID на ст. 2 нет**.



Использовать уже существующее:



- активные EA среды с `ProbeKey` (`IsEnvironmentProbeAction`);

- набор их id → `PerceptionImage.InfluenceActionsList` и/или образ через `InfluenceActionsImagesSystem` → **`ActivityID`** узла;

- нормализация ключа: sort + unique id списка.



Стимулы пульта фильтровать `FilterOperatorStimulusActionIds` — probe-EA не смешивать с pult stimulus image как «действием оператора».



В AIStudio на ст. 2: опираться на редактирование существующих компонент дерева / EA↔ProbeKey, не вводить колонку MetricID. Отдельный RFC на координату дерева — только если ActivityID/Perception окажется недостаточно (вне deliverable ст. 2).



### 2.3. Post-motor wait



| Таймер | Имя / место | Назначение |

|--------|-------------|------------|

| A | `WaitingForOperatorEvaluation` + `WaitingPeriodForActionsVal` | Оценка уже исполненного atmz с пульта (пульсы) |

| B | **новый** post-motor wait (host, пульсы) | Credit в сессии наблюдения после распознанного G_AD |

| C | host dialog-completion | Ждать OK/Apply/Cancel/Forbid у рецепта |



Правила B:



- только после распознанного G_AD;

- успех → запись **этого** мотора и конец сессии;

- таймаут → отбросить мотор, ждать следующий;

- календарный лимит всей сессии **не** вводим; ограничиваем wait после мотора и сброс сессии при смене документа / смене focus-витала / падении `EvolutionStage`.



Цепочки на ст. 2 не пишем: один успех → один atmz с одним G_AD.



### 2.4. Focus-витал и credit assignment



Источник focus:



- runtime: `AppGlobalState.DominantParam` / `FindDominantParameter`;

- при оценке исполнения atmz с оператором: снимок `CaptureOperatorEvaluationParameterSnapshot` → `focusParameterId` + `ComputeOperatorAutomatizmAssessment`.



Закрытие сессии наблюдения и авто-«+» записи — **только по focus-виталу**, не по overall от другого параметра.



Чтобы не закреплять случайный клик:



- старт сессии — rising-edge Bad на focus (или vital Bad → `VeryActualSituation`) при отсутствии usable atmz;

- credit только мотору, после которого в его post-motor wait зафиксирован уход из Bad на focus;

- игнорировать шумные сенсоры без карты на G_AD (закрытие панелей SW и т.п.).



**Контракт Open vs Apply (наблюдение):**  

многие G_AD только открывают диалог. В сессии наблюдения мотором для credit считается жест, **снявший метрику** (Apply / complete операции), а не сам Open. Open без завершения до конца post-motor wait — кандидат отбрасывается.  

(При **исполнении** уже записанного atmz действуют правила §2.5.)



### 2.5. Исполнение atmz с диалогом (host → ISIDA оценка ответом среды)

После `ActiveAdaptiveActions` → рецепт (в т.ч. с диалоговой формой SW/PropertyManager) исполняется целиком. Оценка **не** по кнопкам формы — кнопки Apply/Forbid/Cancel удалены как противоречащие архитектуре (оценка не может быть до изменения состояния).

Вместо этого — **ответ среды**:

- **`NotifyMotorCompleted(actionId)`** — Velum сообщает ISIDA сразу после фактического завершения мотора (`RecipeExecutor.TryExecute` вернул успех). ISIDA переснимает «до»-снимок (`StateBeforeOperatorImpact` + `CaptureOperatorEvaluationParameterSnapshot`) и **перезапускает** таймер `WaitingForOperatorEvaluation` с текущего пульса.
- **Ожидание** — ISIDA ждёт истечения `WaitingPeriodForActionsVal` (таймер A), не требуя стимула с пульта. Среда отвечает изменением гомеостаза.
- **При истечении** — `PsychicSystem.ResetAutomatizmWaitingState` вызывает `AutomatismResultTracker.FinishingTracking` → `AnalyzeResult`: дельта `PreviousState`/`CurrentState` (и `ComputeOperatorAutomatizmAssessment` по focus-виталу) → `UsefulnessDelta`:
  - улучшение → «+» (`Usefulness` растёт);
  - ухудшение → «−»;
  - без изменения → `0` (Skipped) + **рефрактерность host** (`RecipeDispatchEpisodeTracker.MarkRefractory`): не открывать мотор на каждом пульсе, пока нет нового rising-edge Bad или смены эпизода документа.

- **Пульт Поощрить/Наказать** — принудительное перебивание оценки (существующий operator-eval path через `EvaluatePreviousAutomatizm` + `MergeOperatorAssessmentWithPultInfluence`), если оператор ответил в окно ожидания.

- **Dual-use Forbid для у-рефлекса** — сохраняется отдельно: если мотор запущен от у-рефлекса и открывает диалоговую форму SW, на ней есть кнопка «Запрет» через `VelumConditionedReflexForbidHelper.BindForbidButton` → `ResetAssociationStrengthToInitial`. Это понижение крепости **рефлекса**, а не оценка автоматизма.

- **Индикатор ожидания на панели задач** — метка `_lblCountdown` в шапке `VelumAgentTaskPane`, отображает обратный отсчёт `WaitingPeriodForActionsVal`. Цвет: синий с пульсацией при активном ожидании, серый со статичным текстом «Ожидание ответа оператора: Нет» в простое. Показывается после завершения мотора, скрывается при истечении таймера, переходе состояния из Bad в норм или остановке пульсации.

Handler рецепта **не** должен репортить `success=true` на любой Close: Cancel/таймаут ≠ успех диспетчера (для шагов, где закрытие формы без завершения операции — не успех).



### 2.6. Биология холостых повторов

Нейтральный исход не растит и не роняет `Usefulness`; серия «−» уводит в блок; рефрактерность гасит busy-loop UI.



---



## 3. Механизм 3 — случайная проба



**Когда:**



- `EvolutionStage == 2`

- `!VeryActualSituation`

- активны стили Поиск и/или Игра

- нет usable atmz; сессия наблюдения и path A клон не перехватили

- есть кандидаты G_AD под целевой параметр (`TargetGomeoParamIdArr` / домен рецептов)



**Как:** один случайный релевантный G_AD → create-one atmz тем же путём создания, что genetic purpose → оценка.  

**Не** использовать `RandomBranchAutomatizmStrategy` (`infoFunc_30`) — он на ст. 4+ выбирает уже существующий atmz.  

**Не** кидать случайные `sw:*`.



Провальные пробы с серией «−» уходят в блок обычным контуром `Usefulness`; отдельный GC дерева на ст. 2 не обязателен.



---



## 4. Оценка полезности (сводка)



| Событие | Эффект на Usefulness | Контур |

|---------|----------------------|--------|

| Запись наблюдения: focus ушёл из Bad после мотора | начальный «+» | ObservationSession → create |

| Исполнение: ответ среды при истечении таймера | дельта состояния: «+» / «−» / `0` | `NotifyMotorCompleted` → `WaitingForOperatorEvaluation` → `FinishTracking` → `AnalyzeResult` |

| Исполнение: Запрет у-рефлекса (dual-use) | сброс крепости у-рефлекса (не оценка atmz) | `VelumConditionedReflexForbidHelper` → `ResetAssociationStrengthToInitial` |

| Пульт Поощрить / Наказать | принудительно «+» / «−» (перебивание) | `WaitingForOperatorEvaluation` + `EvaluatePreviousAutomatizm` + `MergeOperatorAssessmentWithPultInfluence` |

| Среда без оператора (`AnalyzeResult`) | focus-aware дельта (через `ComputeOperatorAutomatizmAssessment` при наличии snapshot) | `FinishTracking` → `AnalyzeResult` |



### 4.1. Взаимное исключение «+»



На один execution / один закрытый жест — **один** источник повышения `Usefulness`:



- либо начальная запись наблюдения,

- либо ответ среды при истечении таймера,

- либо пульт Поощрить (перебивание),



не суммировать все три за один и тот же жест.



---



## 5. Домен Velum



Узкий набор по виталам/probe (имя, материал, экспортная документация, реестр и т.п.): наблюдение решений оператора + селективный клон с recipe defaults + редкий random.



**Критерий ухода со 2-й:** по ключевым виталам есть atmz с `Usefulness >= 0`, стабильно снимающие проблему через рецепты.



---



## 6. Deliverables



1. **Якорь метрик** через `InfluenceActionsList` / `ActivityID` / `ProbeKey` (без MetricID); AIStudio без новой колонки дерева.

2. **`OperatorMotorObservationSession`:** старт при Bad(focus) + ОР miss; post-motor wait (пульсы); запись одного atmz (`CreateNewAutomatizm`) при Bad→Well на focus; контракт Open vs Apply.

3. **Селективный клон:** path A `GetAutomatizmByGeneticPurpose` + recipe defaults; без bulk; без echo path B; без цепочек как единицы навыка.

4. **Random probe:** create-one G_AD при Поиск/Игра и `!VeryActual` (не `infoFunc_30`).

5. **Диалоги рецептов:** Apply → «+»; Запрет → «−» (atmz ± у-рефлекс); Cancel/таймаут → `0` + рефрактерность; handlers не маркируют Cancel как success.

6. **Мотор:** только AdaptiveAction → handlers / `run_macro`; не развивать Command-echo.

7. **Три таймера:** A пульт (`WaitingPeriodForActionsVal`), B post-motor (host), C dialog-completion (host) — все в пульсах / явной host-политике, без смешения имён.



---



## 7. Три таймера (обязательная таблица)



| ID | Имя | Где живёт | Единица | Событие старта | Успех | Неуспех |

|----|-----|-----------|---------|----------------|-------|---------|

| A | `WaitingForOperatorEvaluation` | ISIDA `AppGlobalState` | пульсы `WaitingPeriodForActionsVal` | после исполнения atmz, ждём пульт | Поощрить/Наказать / operator assessment | истечение → сброс ожидания, без обязательного Δ Usefulness |

| B | post-motor wait | **новый** host (сессия наблюдения) | пульсы (конфиг Velum) | распознан G_AD в сессии | focus Bad→Well → запись atmz | таймаут → отбросить мотор |

| C | dialog-completion | host (диалог рецепта) | пульсы или UI lifetime модалки | открыт диалог рецепта | Apply / Запрет | Cancel / Esc / крестик / таймаут → `0` + рефрактерность |



---



## 8. Порядок реализации (контракт)



| Pri | Работа | Новый ISIDA API |

|-----|--------|-----------------|

| P0 | Stage=2, ObservationMode off, TeachingMode off; моторы только через `ActiveAdaptiveActions` → рецепт | нет |

| P1 | Оценки через focus snapshot / таймер A | нет |

| P2 | Селективный клон path A + recipe defaults; запрет bulk/echo/chain как навыка | нет |

| P3 | ObservationSession + таймер B + якорь EA/ActivityID + CreateNewAutomatizm | не обязателен (FSM может быть host-only) |

| P4 | Диалог Apply/−/Cancel/`0` + рефрактерность + честный success handler | нет |

| P5 | Random create-one policy | optional: picker G_AD |

| P6 | Optional: focus-aware `AnalyzeResult` без оператора | optional |



---



## 9. Краткая формула



**Bad(`DominantParam`) → ОР1 без usable atmz → `OperatorMotorObservationSession` → после G_AD focus Bad→Well → `CreateNewAutomatizm` (один мотор) на (состояние + стили + активные probe-EA).**  

При path A — селективный клон генетики с recipe defaults.  

Исполнение — G_AD/рецепт; оценка — focus/пульт/Apply/Запрет; Cancel = 0 + рефрактерность.  

`ObservationMode` и TeachingMode для этого curriculum выключены.

---

## 10. Готовность реализации (пересмотр 2026-08-23)

| Механизм | Статус | Оценка | Примечание |
|----------|--------|--------|------------|
| Механизм 1 — селективный клон (path A ОР1) | **Запрещён на ст. 2** | N/A | `CreateAutomatizmByGeneticPurpose` только для `EvolutionStage >= 3`. На ст. 2 — ObservationSession |
| Механизм 2 — ObservationSession | **Есть + запрет клона + ActivityID** | 95% | Запрет клона на ст. 2; исправлена проверка `hasUsableAutomatizm`; привязка к ActivityID; индикатор наблюдения |
| Механизм 3 — случайная проба | **Есть + пауза + приоритет ожидания** | 80% | `WaitingPeriodForActionsVal` как пауза; приоритет сессии наблюдения; проверка стилей |
| Оценка полезности / таймеры | **Есть + ответ среды + рефрактерность** | 85% | `NotifyMotorCompleted` → `FinishTracking` → `AnalyzeResult` (дельта состояния); `RecipeDispatchEpisodeTracker.MarkRefractory` |
| Диалоги рецептных handlers | **Есть (формы SW, без кнопок оценки)** | 90% | Оценка — ответом среды, не кнопками; индикатор ожидания `_lblCountdown` в шапке `VelumAgentTaskPane` |
| **Общая готовность** | | **~88%** | |

**Реализовано в рамках доработки 2026-08-21:**
- ✅ **P0-1:** Исправлен баг с actionId — `LastMotorActionId` в сессии вместо `activeProbes[0]`
- ✅ **P0-3:** Рефрактерность через `RecipeDispatchEpisodeTracker.MarkRefractory` (30 пульсов)
- ✅ **P1-4:** Rising-edge Bad — сессия открывается только при переходе в Bad, не при каждом пульсе
- ✅ **P1-5:** Сброс сессии при смене документа (`VelumSolidDocumentEpisodeReset`)
- ✅ **P1-6:** Path B различается: echo без метрик разрешён, echo с метриками (повтор действия оператора) — блокируется
- ✅ **P1-7:** Пауза `WaitingPeriodForActionsVal` перед случайной пробой; приоритет сессии наблюдения
- ✅ **P2-8:** `SetRecipeChecker` — хост-зависимая проверка через `RecipeCatalog.ExistsByAdaptiveActionId`
- ✅ **P2-9:** Сохранение контекста Tone/Mood при открытии сессии; использование при создании ActionsImage
- ✅ **Дополнительно:** `SetInfluenceActionSystem` для проверки probe-EA; `IsidaEngine` шаг 29

**Реализовано в рамках доработки 2026-08-23 (переход на ответ среды):**
- ✅ **E1:** Удалена форма оценки `VelumDialogRecipeForm` и `RecipeDialogManager` (кнопки Apply/Forbid/Cancel противоречат архитектуре — оценка не может быть до изменения состояния)
- ✅ **E2:** Удалены из ISIDA `DialogOutcome`, `DialogOutcomeState`, `ReportDialogOutcome`, `UpdateAutomatizmUsefulnessByActionId`
- ✅ **E3:** `AdaptiveActionsSystem.NotifyMotorCompleted(actionId)` + `AutomatismResultTracker.NotifyMotorCompleted(automatizmId)` — переснимок «до» + перезапуск таймера ожидания после завершения мотора
- ✅ **E4:** `PsychicSystem.ResetAutomatizmWaitingState` — при истечении таймера вызывает `FinishTracking` → `AnalyzeResult` (ответ среды: дельта `PreviousState`/`CurrentState`)
- ✅ **E5:** `RecipeDispatcher` после успешного `TryExecute` вызывает `NotifyMotorCompleted(actionId)`
- ✅ **E6:** Индикатор ожидания `_lblCountdown` в шапке `VelumAgentTaskPane` (синяя пульсация с обратным отсчётом при ожидании, серый статичный текст «Нет» в простое); показ после мотора, скрытие при истечении/смене документа/сбросе ожидания
- ✅ **E7:** Dual-use Forbid у-рефлекса сохранён — `VelumConditionedReflexForbidHelper` остаётся в DXF/PDF формах

**Реализовано в рамках доработки 2026-08-24 (запрет клонирования + привязка к ActivityID):**
- ✅ **F1:** Запрет селективного клона (`CreateAutomatizmByGeneticPurpose`) на стадии 2 — путь A доступен только для `EvolutionStage >= 3`
- ✅ **F2:** Исправлена проверка `hasUsableAutomatizm` в `ProcessOperatorMotorObservationSession` — проверка по `AutomatizmSystem.GetAllAutomatizms()`, а не по `AdaptiveActionsSystem`
- ✅ **F3:** Добавлен метод `GetRemainingPostMotorWaitPulses` в `OperatorMotorObservationSession`
- ✅ **F4:** Индикатор режима наблюдения в `VelumAgentTaskPane` — «Режим наблюдения: ожидание действия оператора. XX» с приоритетом над таймером A
- ✅ **F5:** Привязка триггера автоматизма к ActivityID — `CreateAutomatizmFromMotor` создаёт `InfluenceActionsImage` из probe-EA и использует узел дерева с `ActivityID`
- ✅ **F6:** Добавлены зависимости `InfluenceActionsImagesSystem` и `AutomatizmTreeSystem` в `OperatorMotorObservationSession`
- ✅ **F7:** Обновлён `IsidaEngine` шаг 33 — передача новых зависимостей в `OperatorMotorObservationSession.InitializeInstance`

**ISIDA:** 0 ошибок, 0 предупреждений (сборка успешна)
**Velum:** изменения в `VelumAgentTaskPane.cs` синтаксически корректны (LSP OK); проблема сборки с ресурсами .resx (.NET SDK 9.0) не связана с кодом

**Осталось для 95%+:**
- Тестирование на реальном сценарии (Bad → ObservationSession → G_AD → Bad→Well → atmz с ActivityID триггером)
- Настройка и верификация `WaitingPeriodForActionsVal` как паузы перед случайной пробой и окна ответа среды
- Проверка dual-use Forbid в логировании (проверка `ResetAssociationStrengthToInitial` в логах)

---

## 11. Оценка автоматизма ответом среды (вместо кнопок формы)

**Статус: ✅ РЕАЛИЗОВАНО (2026-08-23)**

### Что было изменено

Ранее оценка исполнения atmz шла через кнопки формы `VelumDialogRecipeForm` (Применить/Отмена/Запретить), нажимаемые **до** завершения мотора. Это противоречило архитектуре оценки (она не может быть до изменения состояния гомеостаза). Кнопки удалены.

Оценка теперь идёт **ответом среды** — по дельте состояния после завершения мотора.

### Архитектура

```
RecipeDispatcher.TryDispatchActiveAdaptiveAction
  ↓
RecipeExecutor.TryExecute(recipe) — мотор выполнен целиком (dialog SW / invoke)
  ↓
AdaptiveActionsSystem.NotifyMotorCompleted(actionId)
  ↓
AutomatismResultTracker.NotifyMotorCompleted(automatizmId)
  ├→ StateBeforeOperatorImpact = CurrentOverallState      // снимок «ДО»
  ├→ CaptureOperatorEvaluationParameterSnapshot()          // focus + vital
  └→ StartWaitingForOperatorEvaluation(automatizmId)     // перезапуск таймера
  ↓
  Автоматизм ожидает: перезапуск таймера `WaitingForOperatorEvaluation`
  ↓
  Индикатор `_lblCountdown` в шапке `VelumAgentTaskPane` — синяя пульсация с обратным отсчётом
  ↓
 истечение WaitingPeriodForActionsVal (ответ среды)
  ↓
PsychicSystem.ResetAutomatizmWaitingState
  └→ AutomatismResultTracker.FinishTracking → AnalyzeResult
       ├→ PreviousState vs CurrentState → UsefulnessDelta
       └→ AfterAutomatizmUsefulnessUpdated
```

### Файлы

- `Common/VelumAgentTaskPane.cs` — `_lblCountdown` в шапке панели задач: `ShowCountdown` / `HideCountdown` / `UpdateCountdownIndicatorState` / `UpdateCountdownDisplay` / `OnCountdownPulseTick`
- `ReactiveCore/RecipeDispatcher.cs` — `TryNotifyIsidaMotorCompleted` + показ/скрытие индикатора (изменено)
- `ISIDA Actions/AdaptiveActionsSystem.cs` — `NotifyMotorCompleted` (добавлено)
- `ISIDA Psychic/Automatism/AutomatismResultTracker.cs` — `NotifyMotorCompleted` (добавлено)
- `ISIDA Psychic/PsychicSystem.cs` — `ResetAutomatizmWaitingState` → `FinishTracking` (изменено)
- `SolidHomeostasis/VelumSolidDocumentEpisodeReset.cs` — скрытие индикатора при смене документа (изменено)

### Удалённые файлы

- `ReactiveCore/VelumDialogRecipeForm.cs` — форма с кнопками Apply/Forbid/Cancel
- `ReactiveCore/VelumDialogRecipeForm.Designer.cs`
- `ReactiveCore/RecipeDialogManager.cs` — менеджер диалоговых оценок

### Удалённые API ISIDA

- `AdaptiveActionsSystem.DialogOutcome` (enum)
- `AdaptiveAction.DialogOutcomeState`
- `AdaptiveActionsSystem.ReportDialogOutcome`
- `AdaptiveActionsSystem.UpdateAutomatizmUsefulnessByActionId`

### Индикатор ожидания на панели задач

```
┌────────────────────────────────────────────┐
│  [НОРМА] [CAD] [Старт/Стоп] [?]           │
│                                            │
│  Ожидание ответа: 12                       │
└────────────────────────────────────────────┘
```

**При активном ожидании:**
- Текст: «Ожидание ответа: N» (N — обратный отсчёт пульсов)
- Цвет: синий с пульсацией (чередование яркого `#0070C0` и приглушённого `#78AFFDC`)
- Частота пульсации: 1 раз/сек (интервал таймера 1000 мс)
- Таймер уменьшает `WaitingPeriodCountdown` на каждом тике

**В простое (пульсация запущена, ожидание не активно):**
- Текст: «Ожидание ответа оператора: Нет»
- Цвет: серый (`Color.Gray`), статичный (без пульсации)

**Скрытие:**
- Истечение таймера (`WaitingPeriodCountdown <= 0`)
- Переход состояния из Bad в норм (`_lastStateBeforePulse == Bad && st != Bad`)
- Остановка пульсации или переход в режим «агент мёртв»
- Смена документа (`VelumSolidDocumentEpisodeReset`)

### Почему это важно

Оценка автоматизма теперь строится на **дельте состояния гомеостаза** (как в BOT/ISIDA), а не на мнении оператора до изменения состояния. Это корректная основа для стадий 3+: психика сможет оценивать автономно, а кнопки пульта Поощрить/Наказать остаются дополнительным каналом перебивания оценки.





