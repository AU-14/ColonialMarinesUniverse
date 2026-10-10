# Проверка производительности CMU — 2026-10-09–10

## Current-base integration — 2026-10-10

PR 2135 now incorporates master `101156f22f`. Metabolism retains master's
sandbox-compatible, per-system pool of reagent lists and its
`MetabolismSnapshotReuseTest` coverage. The redundant `ArrayPool` helper and
its helper-only tests were removed because the engine sandbox rejects that API.
Diplomacy retains master's change-driven and one-second UI refresh schedule;
callsigns and radio retain the newer hearing behavior.

The measurements and commands below describe the original `2044182137`-based
implementation. They are historical evidence, including commands for the removed
snapshot helper, and are not measurements of the integrated revision.

## Historical validation

Изменения выполнены в `codex/performance-500` поверх текущего рабочего дерева.
Ранее внесённые оптимизации радио, метаболизма, питания и черт сохранены.
Двигатель и его подмодули не изменялись.

## Исправленные пути

| Область | Прежняя работа | Новая работа |
| --- | --- | --- |
| Клиент, речевые облачка | Повторное вычисление ширины текста для halo, stroke и fill; повторный обход границ glyph при каждом Draw | Размеры фрагментов и границы строки вычисляются при построении layout и повторно используются |
| Клиент, длинные слова | Создание и измерение каждого растущего префикса слова | Один проход по Unicode rune с накоплением ширины |
| Клиент, масштаб UI | Добавление ранее измеренного control в другой UI root могло сохранять размеры старого DPI | Layout проверяет фактический UIScale, включая attach и reparent без UIScaleChanged |
| Сервер, дипломатические консоли | Построение обеих проекций для закрытых окон каждый тик, повторная синхронизация фракции и копирование коллекций | Построение только открытых окон; немедленный актуальный снимок при открытии; одна обработка фракции |
| Сервер, позывные | Полный поиск занятых позывных для каждого проверяемого кандидата | Один проход по участникам соответствующего подразделения за назначение |
| Shared, паника | Повторный spatial lookup при одной смерти для каждого наблюдателя | Один lookup для каждого уникального радиуса внутри события; индивидуальная проверка LOS сохраняется |
| Сервер, химическая зависимость | Перебор всех зависимостей каждый тик при таймерах в несколько минут | Пропуск обновления до ближайшего срока; инвалидация при дозе, startup, shutdown, unpause и новом раунде |
| Shared, эффекты голода и жажды | Dirty и полный пересчёт скорости при каждом изменении питания внутри одного порога | Пересчёт скорости при изменении множителя; изменённый срок по-прежнему помечается для синхронизации; MapInit всегда применяет эффект |

Открытые дипломатические окна обновляются с прежней частотой. Расходы,
пополнение бюджета, границы позывных, пороги питания и времена зависимости
сохраняются. Пространственные результаты паники живут только внутри одного
события, поэтому следующее событие учитывает новую геометрию и позиции.

## Воспроизведение проверки

Запускать из корня последовательно, чтобы проверки не конкурировали за CPU:

```powershell
dotnet build Content.IntegrationTests/Content.IntegrationTests.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false --verbosity quiet -clp:ErrorsOnly
dotnet test Content.Tests/Content.Tests.csproj -c Release --no-build --no-restore --filter '(FullyQualifiedName~CMUPerformance|FullyQualifiedName~CMUClientPerformanceTest|FullyQualifiedName~CMUHandsetChannelsTest|FullyQualifiedName~CMUReagentSnapshotTest|FullyQualifiedName~CMUSatiationThresholdLookupTest)&Name!~Measure'
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj -c Release --no-build --no-restore --filter '(FullyQualifiedName~RunechatLayoutPerformanceTest|FullyQualifiedName~CMUAmbassadorUpdateTest|FullyQualifiedName~CMUCallsignAssignmentTest|FullyQualifiedName~CMUPanicDeathLookupTest|FullyQualifiedName~CMUChemicalAddictionDeadlineTest|FullyQualifiedName~CMUSatiationEffectRefreshTest|FullyQualifiedName~CMUSatiationDeadlineTest|FullyQualifiedName~CMUSatiationMigrationTest|FullyQualifiedName~CMUHandsetHearingTest|FullyQualifiedName~CMUIdleTraitReplicationTest|FullyQualifiedName~AmbassadorThirdPartyCallInsTest)&Name!~Measure'
$previousTiered = [Environment]::GetEnvironmentVariable('DOTNET_TieredCompilation', 'Process')
try {
    $env:DOTNET_TieredCompilation = '0'
    dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~MeasureCrowdedElementAssignments' --logger 'console;verbosity=normal'
    dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~SustainedMedication&Name~500&Name~True' --logger 'console;verbosity=detailed'
}
finally {
    [Environment]::SetEnvironmentVariable('DOTNET_TieredCompilation', $previousTiered, 'Process')
}
```

## Результаты локальной проверки 2026-10-10

- Release-сборка `Content.IntegrationTests` и зависимых клиентского, серверного
  и shared-проектов: 0 ошибок, 3074 предупреждения. После исправления helper
  теста инкрементальная пересборка: 0 ошибок, 529 предупреждений.
- Перед публикацией PR отдельно собран `Content.Benchmarks` в Release:
  0 ошибок, 0 предупреждений в инкрементальном прогоне. В PR входят также
  ранее выполненные оптимизации радио, метаболизма, поиска порогов питания,
  сетевых состояний черт и расширение стенда PVS; их результаты за 2026-10-01
  сохранены в [отчёте предыдущих этапов](SCALING-500.md).
- Unit-проверки алгоритмов, диагностики и ранее внесённых оптимизаций: 48/48.
- Проверены 27 различных интеграционных случаев. Первый запуск: 23/27;
  четыре клиентских случая падали из-за неоднозначного reflection-поиска
  `Draw` в helper теста. Helper исправлен, затем вся клиентская fixture
  прошла повторно: 6/6. Повторный общий запуск всех 27 не выполнялся.
- Клиентские счётчики подтвердили отсутствие повторных запросов метрик текста
  при неизменном layout, пересчёт при DPI/смене UI root и один запрос метрик
  на rune при переносе длинного слова. Проверено совпадение повторно рисуемых
  glyph, включая жирный шрифт, курсив и цвет.
- Позывные: 500 занятых сущностей, 100 назначений, Release,
  `DOTNET_TieredCompilation=0`, 10 прогревочных вызовов каждого алгоритма.
  Прежний алгоритм: **324,598 мс / 1 603 200 байт**; новый:
  **2,740 мс / 6 400 байт**. Это один локальный блок измерений, без нагрузки
  реальных клиентов; baseline воспроизводит прежний поиск в одном подразделении.
- Зависимость: 500 сущностей, 4096 обновлений до ближайшего срока:
  **0,2222 мс / 0 выделенных байт** в измеряемом цикле.
- Регрессии проверяют открытие/повторное открытие обоих дипломатических окон,
  сроки зависимости и повторную дозу, разделение радиусов и LOS для паники,
  эффекты питания при MapInit, изменение срока и переход порога, а также
  отсутствие лишних обновлений скорости/версий у 500 сущностей.
- Медицинская нагрузка `SustainedMedication(500,True)`: прошла; 500 персонажей,
  половина с травмами, 192 тика / 6,4 секунды симуляции, label `cmu-20261010`.
  Wall-time фазы обновления серверных entity systems: медиана **1,1845 мс**,
  p99 **17,1077 мс**, максимум **23,0599 мс**; выделения в этой фазе за 192 тика:
  **12 954 416 байт**. Перцентили — nearest rank по 192 наблюдениям.
  Сюда входят тестовый observer и серверные системы; event-bus flush, culling,
  сеть, реальный рендеринг и выдача лекарства между тиками не входят.
  Проверены сила и сроки лекарственных эффектов. Это регрессионный прогон;
  контрольный медицинский прогон старой реализации в этой серии не проводился.
- Независимая проверка шести production diff и вызывающего кода не выявила
  новых блокирующих дефектов. `git diff --check --ignore-submodules` прошёл;
  13 изменённых/добавленных файлов этой серии проверены на UTF-8 без BOM и LF.

Журналы находятся в корне рабочего дерева: `performance-20261010-*.log`.
Предупреждения сборки не устранены в рамках этой серии изменений.

Измерения алгоритмов, счётчики вызовов font metrics, выделения памяти и версии
компонентов не являются измерением FPS реального клиента или времени полного
серверного кадра. Nullspace-проверки питания наблюдают Dirty и работу движения,
а не передачу пакетов и сетевое предсказание.
Планирование зависимости охватывает текущие игровые вызовы `AddOrSatisfy` и
жизненный цикл компонента. Прямое изменение записей через административный VV
не вызывает инвалидацию ближайшего срока.

## Проверка конкретного фриза

На клиенте запустить `cmu_client_perf`, закрыть консоль и воспроизвести проблему.
После захвата `cmu_client_perf open` открывает каталог с `client-perf-*.log`.
Для более долгого захвата: `cmu_client_perf start 300 20`.
Подробнее: [диагностика клиента](Client/Diagnostics/Performance/README.md).

На сервере сохранить `cmuperf status`, `cmuperf report` и строки `[CMU-PERF]`
до и после фриза. Указать карту, онлайн, фазу раунда, версию сборки и действие,
вызвавшее задержку. Для ограниченного профиля доступны `lagprofile start`,
`lagprofile report`, `lagprofile stop`.
Подробнее: [диагностика сервера](Server/Diagnostics/Performance/README.md).

Без профиля проблемной сцены и журналов ошибок причина конкретных клиентских
или серверных фризов остаётся неустановленной. Устранение всех ошибок и отсутствие
падений FPS на любой карте и нагрузке этими локальными проверками не подтверждаются.
