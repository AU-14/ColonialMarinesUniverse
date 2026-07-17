# Состояние систем яутжей

Снимок состояния репозитория на 2026-07-17. Документ описывает реализованные в коде, прототипах и тестах системы, связанные с яутжами (Yautja). Это технический инвентарь, а не руководство для игрока.

## Краткий итог

Основная функциональность яутжей собрана в отдельном модуле `Content.*\_CMU14\Yautja` и подключается прототипами из `Resources/Prototypes/_CMU14/Yautja`.

| Область | Текущее состояние | Основные точки входа |
| --- | --- | --- |
| Вид, профессии и спавн | Реализовано | `YautjaComponent`, species/job prototypes |
| Раунд яутжей и роли | Реализовано | `YautjaPredatorRoundSystem` |
| Браслет и энергия | Реализовано | `YautjaBracerComponent`, `YautjaPowerSystem`, `YautjaBracerUtilitySystem` |
| Маска, визор, режимы зрения и маскировка | Реализовано | `YautjaMaskSystem`, `YautjaCloakSystem`, client HUD |
| Охота и охотничьи полигоны | Реализовано | hunt console, teleporter, hunt prototypes |
| Метки, честь, thrall и youngblood | Реализовано | mark, thrall, youngblood, ritual systems |
| Оружие и охотничье снаряжение | Реализовано по отдельным подсистемам | bow, plasma, melee, bracer attachments |
| Hellhound, Falcon и Abomination | Реализовано | соответствующие server/client systems |
| Трофеи, разделка и ритуалы | Реализовано | `YautjaTrophySystem`, `YautjaRitualSystem` |
| Корабль охотника | Реализовано через карту, backend-прототипы и generated visual prototypes | `huntership*.yml`, `HunterShuttleTest` |
| Независимость статичных спрайтов от поворота камеры | Реализована для каталогизированных visual sprites; есть остаточный риск для тайлов и стен | `noRot`, `noDirRot`, `noRotWorldOffset` |

## 1. Архитектура и границы модуля

### Shared

В `Content.Shared/_CMU14/Yautja` находятся сетевые компоненты, action events, сериализуемые профили, общие системы и правила проверки доступа. Центральный файл компонентов содержит состояние яутжа, браслетов, масок, охоты, питомцев, трофеев и оружия: [YautjaComponents.cs](Content.Shared/_CMU14/Yautja/YautjaComponents.cs).

Ключевые общие файлы:

- [YautjaActions.cs](Content.Shared/_CMU14/Yautja/YautjaActions.cs) - действия для визора, маскировки, прыжка, меток, меню браслета, recall, диска, combistick, Falcon и self-destruct.
- [YautjaCharacterProfile.cs](Content.Shared/_CMU14/Yautja/YautjaCharacterProfile.cs) - сериализуемый профиль внешности и комплекта.
- [YautjaMarkSystem.cs](Content.Shared/_CMU14/Yautja/YautjaMarkSystem.cs) - типы меток и проверки допустимых целей.
- [YautjaCloakSystem.cs](Content.Shared/_CMU14/Yautja/YautjaCloakSystem.cs) - общая часть состояния маскировки.
- [YautjaMaskSystem.cs](Content.Shared/_CMU14/Yautja/YautjaMaskSystem.cs) - общая часть маски, визора и zoom.
- [YautjaPowerSystem.cs](Content.Shared/_CMU14/Yautja/YautjaPowerSystem.cs) - общая проверка владельца браслета, расхода и доступа к технологии.
- [YautjaHuntEvents.cs](Content.Shared/_CMU14/Yautja/YautjaHuntEvents.cs) - сетевые события выбора полигона, вызова охоты, выхода и blooding.
- [YautjaSpikeLauncherSystem.cs](Content.Shared/_CMU14/Yautja/YautjaSpikeLauncherSystem.cs) и [YautjaTechItemSystem.cs](Content.Shared/_CMU14/Yautja/YautjaTechItemSystem.cs) - общие правила технологии яутжей и отдельных предметов.

### Server

Серверная часть находится в `Content.Server/_CMU14/Yautja`. Она отвечает за фактические проверки, спавн, телепортацию, урон, cooldowns, контейнеры, UI state и очистку состояния между раундами. Ниже перечислены server systems по функциональным группам.

### Client

Клиентская часть находится в `Content.Client/_CMU14/Yautja` и содержит HUD, окна UI, визуализаторы экипировки и эффекты. Она не должна быть источником авторитетного состояния: разрешения и изменения состояния выполняются на сервере.

### Prototypes и ресурсы

Основные YAML-файлы находятся в [Resources/Prototypes/_CMU14/Yautja](Resources/Prototypes/_CMU14/Yautja). В каталоге есть отдельные файлы для ролей, видов, оружия, масок, брони, действий, структур, охоты, корабельных backend-прототипов, status effects, audio и именных наборов.

## 2. Вид, роли и жизненный цикл раунда

### Вид и базовые характеристики

Прототип `Yautja` не доступен как обычный round-start species (`roundStart: false`). Игровая сущность создается через job или специальные сценарии. Базовый `YautjaComponent` задает, среди прочего:

- скорость ходьбы и спринта;
- базовую скорость атаки и урон без оружия;
- skill level, resistance к stun и бонус к shove;
- damage modifier set и speech sounds;
- список доступных голосовых emote;
- набор action prototype ids.

Источники: [species.yml](Resources/Prototypes/_CMU14/Yautja/species.yml), [jobs.yml](Resources/Prototypes/_CMU14/Yautja/jobs.yml), [YautjaComponents.cs](Content.Shared/_CMU14/Yautja/YautjaComponents.cs).

### Роли

В прототипах определены четыре роли:

- `CMUYautjaHunter` - основная whitelisted роль охотника. Имеет стартовый комплект, браслет, маску, броню, коммуникатор, Falcon и охотничьи предметы.
- `CMUYautjaYoungblood` - скрытая роль для blooding-сценариев; стартовый комплект ограничен коммуникатором и браслетом.
- `CMUYautjaBadBlood` - скрытая whitelisted роль с отдельной экипировкой, каналом связи и faction/access.
- `CMUYautjaHellhound` - скрытая роль для управляемого или созданного hellhound.

### Predator round

`CMUYautjaPredatorRound` - game rule с режимом predator, минимумом 3 и максимумом 5 слотов. `YautjaPredatorRoundSystem` обрабатывает player spawning и ведет список youngblood, участвующих в правиле. Источники: [predator_round.yml](Resources/Prototypes/_CMU14/Yautja/predator_round.yml), [YautjaPredatorRoundSystem.cs](Content.Server/_CMU14/Yautja/YautjaPredatorRoundSystem.cs), [YautjaPredatorRoundComponent.cs](Content.Server/_CMU14/Yautja/YautjaPredatorRoundComponent.cs).

### Stats и применение профиля

`YautjaStatsSystem` создает/обновляет базовые компоненты, применяет характеристики и поддерживает их жизненный цикл. `YautjaProfileApplySystem` переносит сохраненный `YautjaCharacterProfile` в имя, внешность и комплект сущности. Это разделение позволяет отделять профиль игрока от серверных ограничений роли.

## 3. Профиль, кланы и доступ

Профиль яутжа редактируется в lobby через [YautjaProfileEditor.cs](Content.Client/_CMU14/Yautja/Lobby/YautjaProfileEditor.cs). В профиле поддерживаются:

- имя, возраст, sex/gender и humanoid appearance;
- skin color, eye color и варианты dreadlocks/quills;
- материал и стиль armor, mask и greaves;
- материал bracer и caster;
- cape style и цвет;
- translator type и звук невидимости;
- legacy-наборы `Dragon`, `Swamp`, `Enforcer`, `Collector`;
- unique-наборы `Anubys`, `Cleopatra`, `Plated`, `Ronin`;
- owner rank браслета.

Связь профиля с YAML-прототипами строится в `YautjaCharacterProfile`: стиль и материал преобразуются в prototype id конкретного предмета. Большой набор цветовых и стилевых вариантов описан в [armor.yml](Resources/Prototypes/_CMU14/Yautja/armor.yml) и [masks.yml](Resources/Prototypes/_CMU14/Yautja/masks.yml).

Доступ разделен на `Secure`, `Elite`, `Elder`, `Leader`, `Ancient` и `Bad Blood`. Группы доступа накопительные, а отдельный `CMUYautjaAccessHunterShip` объединяет доступ к кораблю. Источник: [access.yml](Resources/Prototypes/_CMU14/Yautja/access.yml).

## 4. Браслет, энергия и технологии

`YautjaBracerComponent` является центральным интерфейсом экипировки и технологии. Он хранит заряд, максимальный заряд, regeneration, owner rank, bad blood state, action whitelist и ссылки на actions. Через браслет доступны:

- маскировка;
- bracer menu и mark panel;
- recall, вызов диска и combistick;
- переключение режима взрыва и self-destruct;
- создание healing/stabilising capsules и hunting traps;
- translator и audio panel;
- ID chip и уведомления;
- tracking предметов;
- управление linked thrall и Falcon;
- управление attachments на bracer.

Реализация распределена между [YautjaBracerUtilitySystem.cs](Content.Server/_CMU14/Yautja/YautjaBracerUtilitySystem.cs), [YautjaBracerMenuSystem.cs](Content.Server/_CMU14/Yautja/YautjaBracerMenuSystem.cs), [YautjaBracerEmpSystem.cs](Content.Server/_CMU14/Yautja/YautjaBracerEmpSystem.cs), [YautjaBracerBui.cs](Content.Client/_CMU14/Yautja/YautjaBracerBui.cs), [YautjaPowerSystem.cs](Content.Shared/_CMU14/Yautja/YautjaPowerSystem.cs) и [YautjaSelfDestructSystem.cs](Content.Shared/_CMU14/Yautja/YautjaSelfDestructSystem.cs).

Технологические предметы помечаются `YautjaTechItem`. Проверки не сводятся только к наличию компонента яутжа: учитываются owner rank, youngblood, bad blood, tech-authorized thrall и faction. Это предотвращает использование высокоуровневой технологии неподходящими ролями.

## 5. Маска, зрение, HUD и маскировка

### Маска и режимы зрения

`YautjaMaskComponent`, `YautjaMaskVisorGlassesComponent`, `YautjaHudViewerComponent` и `YautjaMaskZoomComponent` описывают маску, visor, zoom и связанный HUD. Кнопки и действия находятся в [YautjaActions.cs](Content.Shared/_CMU14/Yautja/YautjaActions.cs), а клиентский слой отображения - в [YautjaHudSystem.cs](Content.Client/_CMU14/Yautja/YautjaHudSystem.cs).

### Маскировка

`YautjaCloakSystem` включает и выключает невидимость, учитывает источник действия, состояние браслета и право пользователя на технологию. В profile editor можно выбрать modern/retro sound. Визуальные изменения сущности и эффекты применяются отдельными client visual systems.

### Голос и связь

- `YautjaVoiceSystem` и `YautjaVoice` prototypes обеспечивают click, roar, laugh, growl, pain и death sounds.
- `YautjaRadioSystem` обслуживает специальные radio channels.
- `YautjaTranslatorBui` и `YautjaTranslatorWindow` предоставляют интерфейс translator.
- `YautjaAudioPanelBui` и `YautjaAudioPanelWindow` предоставляют выбор аудиосигналов.

## 6. Охота и охотничьи полигоны

Охота разделена на консольный выбор, создание вызова, спавн, телепортацию и обратный выход.

### Консоль и вызовы

`YautjaHuntConsoleSystem` обслуживает обычные hunt calls, blooding calls и escape console. Настройки вызовов находятся в `YautjaHuntConsoleComponent`: туда входят cooldowns, варианты состава группы, минимальный и максимальный опыт youngblood и стоимость/ограничения вызова.

### Полигоны и телепорты

В [hunting_grounds.yml](Resources/Prototypes/_CMU14/Yautja/hunting_grounds.yml) определены destination markers для:

- Jungle Moon;
- Desert Moon;
- отдельных youngblood destinations;
- human ship relay destination;
- prey и youngblood spawn points.

`YautjaHuntTeleporterSystem` проверяет доступ, отслеживает шаг на телепортер и выполняет deploy youngblood. `YautjaTeleportSystem` содержит вспомогательную телепортацию train/сущности. Конкретные map instances проверяются [YautjaHuntingGroundMapTest.cs](Content.IntegrationTests/_CMU14/Yautja/YautjaHuntingGroundMapTest.cs).

### Relay beacon и recall

Relay beacon позволяет задать destination и отправить яутжа на human ship. Recall возвращает связанные предметы/оружие по правилам соответствующей системы. Клиентские окна beacon и связанные UI находятся в `YautjaRelayBeaconBui.cs` и `YautjaRelayBeaconWindow.cs`.

## 7. Метки, честь, thrall и youngblood

### Метки и честь

`YautjaMarkComponent` и [YautjaMarkSystem.cs](Content.Shared/_CMU14/Yautja/YautjaMarkSystem.cs) поддерживают несколько типов отношений и статусов: `Student`, `Blooded`, `Thrall`, `Honored`, `Dishonored`. Система проверяет тип цели, фракцию, роль и ограничения Bad Blood. `YautjaHonorWorthComponent` дает цели базовую или накопленную honor value.

Проверка подсчета чести вынесена в [YautjaHonorScoringTest.cs](Content.IntegrationTests/_CMU14/Yautja/YautjaHonorScoringTest.cs).

### Thrall

`YautjaThrallSystem` поддерживает:

- применение и снятие thrall mark;
- связь master bracer с thrall bracer;
- передачу сообщений от master к thrall;
- stun linked thrall;
- self-destruct linked thrall;
- blooded thrall и выдачу blooded имени;
- hivebreak xeno;
- lock и уведомления thrall bracer.

Основной источник: [YautjaThrallSystem.cs](Content.Server/_CMU14/Yautja/YautjaThrallSystem.cs). Функциональность проверяется [YautjaThrallPhase8Test.cs](Content.IntegrationTests/_CMU14/Yautja/YautjaThrallPhase8Test.cs) и [YautjaHivebreakerThrallRuntimeTest.cs](Content.IntegrationTests/_CMU14/Yautja/YautjaHivebreakerThrallRuntimeTest.cs).

### Youngblood и blooding

`YautjaYoungbloodSystem` отвечает за ghost-role запросы, mentor/blooded state, execution youngblood и удаление связанных сущностей. `YautjaHuntConsoleSystem` создает blooding call с учетом cooldown и выбранной группы. Источники: [YautjaYoungbloodSystem.cs](Content.Server/_CMU14/Yautja/YautjaYoungbloodSystem.cs), [YautjaYoungbloodTest.cs](Content.IntegrationTests/_CMU14/Yautja/YautjaYoungbloodTest.cs).

## 8. Ритуалы, трофеи и экономика чести

`YautjaRitualSystem` реализует captive claim, освобождение captive и ritual duel, включая переходы состояния и реакцию на смерть/удаление сущностей.

`YautjaTrophySystem` реализует:

- получение trophy из цели;
- butcher action;
- scalp и skeleton trophy;
- trophy records и display;
- honor value и ограничения на цель;
- polishing rag и cleanser gel;
- обработку источников трофея.

Связанные компоненты находятся в [YautjaComponents.cs](Content.Shared/_CMU14/Yautja/YautjaComponents.cs), серверная логика - в [YautjaTrophySystem.cs](Content.Server/_CMU14/Yautja/YautjaTrophySystem.cs), [YautjaRitualSystem.cs](Content.Server/_CMU14/Yautja/YautjaRitualSystem.cs). Тесты: [YautjaRitualTest.cs](Content.IntegrationTests/_CMU14/Yautja/YautjaRitualTest.cs), [YautjaScalpTest.cs](Content.IntegrationTests/_CMU14/Yautja/YautjaScalpTest.cs), [YautjaSkeletonTrophyTest.cs](Content.IntegrationTests/_CMU14/Yautja/YautjaSkeletonTrophyTest.cs).

## 9. Оружие и охотничье снаряжение

### Ближний бой

В отдельные системы вынесены:

- wrist blades, scimitar и shield attachments;
- combistick с folding, chain state и recall;
- chain gauntlet с guard, pull, finisher, execution и проламыванием дверей;
- cleaving glaive и skull mounting;
- ceremonial dagger с flay/scalp стадиями;
- spear fishing;
- melee interference против xeno;
- shield bash и source shield.

Источники: [YautjaMeleeWeaponSystem.cs](Content.Server/_CMU14/Yautja/YautjaMeleeWeaponSystem.cs), [YautjaCombistickSystem.cs](Content.Server/_CMU14/Yautja/YautjaCombistickSystem.cs), [YautjaChainGauntletSystem.cs](Content.Server/_CMU14/Yautja/YautjaChainGauntletSystem.cs), [YautjaCleavingGlaiveSystem.cs](Content.Server/_CMU14/Yautja/YautjaCleavingGlaiveSystem.cs), [YautjaShieldBashSystem.cs](Content.Server/_CMU14/Yautja/YautjaShieldBashSystem.cs).

### Дальний бой

- Bow поддерживает обычные, explosive, EMP, dynamic и snare arrows.
- Plasma weapon хранит charge, расходует энергию, стреляет специализированными projectile и может возвращать часть заряда.
- Plasma projectile имеют stun, immobilizer, lethal и incendiary варианты.
- Cannon pack и linked cannon обрабатываются отдельной системой.
- Spike launcher имеет общий компонент и refund-логику.

Источники: [YautjaBowSystem.cs](Content.Server/_CMU14/Yautja/YautjaBowSystem.cs), [YautjaPlasmaWeaponSystem.cs](Content.Server/_CMU14/Yautja/YautjaPlasmaWeaponSystem.cs), [YautjaPlasmaProjectileSystem.cs](Content.Server/_CMU14/Yautja/YautjaPlasmaProjectileSystem.cs), [YautjaCannonPackSystem.cs](Content.Server/_CMU14/Yautja/YautjaCannonPackSystem.cs), [YautjaSpikeLauncherSystem.cs](Content.Shared/_CMU14/Yautja/YautjaSpikeLauncherSystem.cs).

### Медицинские и вспомогательные предметы

`YautjaHealingGunSystem` работает с bleeding и bloodstream, а `YautjaItemSystem` обслуживает общие предметные ограничения. Через bracer создаются stabilising crystal, human stabilising crystal, healing capsule и hunting trap. Технологические предметы дополнительно ограничиваются проверками доступа.

### Ловушки

`YautjaTrapSystem` и bow snare arrows поддерживают постановку, выбор дальности, xeno interference и срабатывание ловушек. Сценарий проверяется [YautjaHuntingTrapTest.cs](Content.IntegrationTests/_CMU14/Yautja/YautjaHuntingTrapTest.cs).

## 10. Hellhound, Falcon и Abomination

### Hellhound

`YautjaHellhoundSystem` отвечает за поведение и состояние hellhound, а `YautjaSleepingHellhoundSystem` - за спящих hellhound. Для просмотра используются houndpad/internal camera и сеть `Yautja`; сервер фильтрует живые feeds. Клиентские визуальные состояния обслуживает [YautjaHellhoundVisualsSystem.cs](Content.Client/_CMU14/Yautja/YautjaHellhoundVisualsSystem.cs).

### Falcon

Falcon представлен компонентами drone, deployed state, HUD icon, controller и source bracer. Поддерживаются deploy, управление, recall, отображение на плече и работа с tactical map. Runtime и Z-level поведение проверяются [YautjaFalconRuntimeTest.cs](Content.IntegrationTests/_CMU14/Yautja/YautjaFalconRuntimeTest.cs) и [YautjaFalconZLevelCullingTest.cs](Content.IntegrationTests/_CMU14/Yautja/YautjaFalconZLevelCullingTest.cs).

### Abomination

`YautjaAbominationSystem` реализует host/larva/abomination lifecycle, conversion, rush и roar buff. Abomination получает dishonored mark и имеет отдельные ограничения взаимодействия.

## 11. Корабль охотника и корабельные системы

### Состав

Корабль охотника состоит из:

- [huntership.yml](Resources/Prototypes/_CMU14/Maps/huntership.yml) - карта и размещение;
- [huntership_support.yml](Resources/Prototypes/_CMU14/Maps/huntership_support.yml) - поддерживающие определения;
- [huntership_visuals.yml](Resources/Prototypes/_CMU14/Maps/huntership_visuals.yml) - generated visual wrappers;
- [hunter_ship_backends.yml](Resources/Prototypes/_CMU14/Yautja/hunter_ship_backends.yml) - стабильные backend-прототипы, которые не должны теряться при регенерации визуального слоя;
- [convert_huntership.js](Tools/_CMU14/HunterShipPort/convert_huntership.js) - конвертер source/map visual данных.

### Рабочие подсистемы

На корабле проверяются и/или представлены power, atmos, telecomms, shuttle engine, tank/canister, windows, doors, tables, furniture, hydroponics, kitchen, medical machines, consoles, vending, crates, writing/cables, loose tools/items/weapons, skull wall decor и декоративные floor/structure visuals.

Отдельно представлены Yautja machinery, houndcam/houndpad, reactor/defense/stasis consoles, gear racks и ship shuttle control. Для backend-прототипов предусмотрены source-parity тесты, чтобы generated visual prototype не заменял функциональный parent.

## 12. Поворот камеры и статичные визуальные объекты

### Текущее решение

Для статичного art-слоя добавлены три независимых свойства `Sprite`:

- `noRot` (`SpriteComponent.NoRotation`) - не применять rotation сущности;
- `noDirRot` (`SpriteComponent.NoDirectionRotation`) - не применять rotation directional state;
- `noRotWorldOffset` (`SpriteComponent.NoRotationWorldOffset`) - не вращать pixel/world offset вместе с камерой.

Они реализованы на уровне sprite renderer, bounds и culling. Основные файлы: [SpriteComponent.cs](RobustToolbox/Robust.Client/GameObjects/Components/Renderable/SpriteComponent.cs), [SpriteSystem.Render.cs](RobustToolbox/Robust.Client/GameObjects/EntitySystems/SpriteSystem.Render.cs).

Generated visual prototypes корабля и raised visual prototypes Z-level используют все три флага. В текущий каталог статичных объектов входят floor overlays, border, feed, glowing shape, hypersleep chamber, animal pelt, shutters, rune, stone statue, stairs, monitors, medical pods, sarcophagus, raised metal edge/corner и другие map-placed visual wrappers.

### Что проверено тестами

`HunterShuttleTest` содержит проверки:

- `HunterShipExactVisualSpritesKeepByondOffsetsWorldRelative`;
- `HunterShipExactVisualSpriteBoundsDoNotAssertOnClient`;
- `ZLevelRaisedVisualsDoNotForceSpritesToRotateWithCamera`;
- отдельных `NoRotation` требований для корабельных wrappers и предметов.

Эти проверки гарантируют свойства у перечисленных prototype definitions и отсутствие client bounds assertion в соответствующем сценарии.

### Остаточный риск

Флаги `Sprite` применимы к entity sprites. Они не являются универсальным запретом поворота для tilemap, стен, пола-тайла или других renderer paths, где нет `SpriteComponent`. Поэтому состояние сейчас корректно сформулировано так:

> каталогизированные статичные спрайты корабля и raised visual entities защищены от поворота камеры; универсальная гарантия для всех стен и тайлов отдельно не доказана текущими sprite-тестами.

При изменении camera/Z-level renderer нужно отдельно проверять tile rendering, walls и все map layers. Нельзя решать эту часть только добавлением `noRot` на entity prototypes.

## 13. Клиентские UI и визуальные системы

В клиентском Yautja-модуле присутствуют:

- bracer menu, style и window;
- mark panel;
- translator;
- audio panel;
- thrall message window;
- relay beacon window;
- lobby profile editor;
- HUD;
- cape, mask accessory, damage, scalp и hellhound visual systems;
- bow arrow visualizer и chain gauntlet animation.

UI получает state через BUI и сетевые сообщения, а client visual systems отражают уже принятое состояние экипировки/appearance.

## 14. Прототипы и контент

В каталоге [Resources/Prototypes/_CMU14/Yautja](Resources/Prototypes/_CMU14/Yautja) присутствуют отдельные определения для:

- actions, alerts, audio и emotes;
- armor, masks, markings, body и species;
- weapons, plasma projectiles и items;
- structures, structure tiles и entities;
- status effects/status icons, reagents и damage;
- hunting grounds, hunter ship backends и LZ placer;
- hellhound, abomination, trophies/traps и predator round;
- jobs, factions, access и names.

Корабельные прототипы дополнительно используют [Resources/Prototypes/_CMU14/Maps](Resources/Prototypes/_CMU14/Maps) и ресурсы [Resources/Textures/_CMU14/Yautja](Resources/Textures/_CMU14/Yautja) / [Resources/Textures/_CMU14/HunterShip](Resources/Textures/_CMU14/HunterShip), если они присутствуют в конкретной ветке.

## 15. Тестовое покрытие

В репозитории обнаружены 27 файлов интеграционных тестов в `Content.IntegrationTests/_CMU14/Yautja` и 33 файла в `Content.IntegrationTests/_CMU14/HunterShip`.

Покрытые группы включают:

- smoke и статические prototype facts;
- predator role, character profile и role restrictions;
- honor scoring, marks, ritual, scalp/skeleton trophies;
- youngblood, thrall и hivebreaker;
- bow, melee и plasma weapon;
- hunting ground map, hunting trap и cleanup hunt data;
- Falcon runtime и Z-level culling;
- gear rack access/context menu;
- houndcam camera feeds;
- ship shuttle, camera, telecomms, power/atmos, medical, furniture и generated visual wrappers;
- независимость корабельных и raised visuals от поворота камеры.

Крупный smoke-набор находится в [YautjaSmokeTest.cs](Content.IntegrationTests/_CMU14/Yautja/YautjaSmokeTest.cs). Полный список файлов можно получить командой `rg --files Content.IntegrationTests/_CMU14/Yautja Content.IntegrationTests/_CMU14/HunterShip`.

## 16. Известные ограничения и точки контроля

1. `roundStart: false` у species означает, что создание яутжа должно идти через предусмотренные roles, ghost-role и event flows, а не через общий species selector.
2. Часть корабельного контента генерируется из source-таблиц. Изменения в generated `huntership_visuals.yml` должны сопровождаться проверкой backend parent и source-parity тестов.
3. Авторитетное состояние маскировки, энергии, доступа, охоты, меток и связей должно оставаться серверным; client UI не заменяет server validation.
4. `noRot`/`noDirRot`/`noRotWorldOffset` покрывают entity sprites. Tilemap, стены и renderer paths без `SpriteComponent` требуют отдельной проверки.
5. При добавлении нового Yautja prototype нужно определить, является ли он gameplay entity, статичным visual wrapper, интерактивным корабельным объектом или частью tilemap. От этого зависит способ защиты от camera rotation и набор тестов.

## 17. Быстрая навигация по коду

- Shared components и enums: [YautjaComponents.cs](Content.Shared/_CMU14/Yautja/YautjaComponents.cs)
- Actions и сетевые события: [YautjaActions.cs](Content.Shared/_CMU14/Yautja/YautjaActions.cs), [YautjaHuntEvents.cs](Content.Shared/_CMU14/Yautja/YautjaHuntEvents.cs)
- Раунд и роли: [YautjaPredatorRoundSystem.cs](Content.Server/_CMU14/Yautja/YautjaPredatorRoundSystem.cs), [jobs.yml](Resources/Prototypes/_CMU14/Yautja/jobs.yml)
- Браслет: [YautjaBracerUtilitySystem.cs](Content.Server/_CMU14/Yautja/YautjaBracerUtilitySystem.cs), [YautjaBracerMenuSystem.cs](Content.Server/_CMU14/Yautja/YautjaBracerMenuSystem.cs)
- Охота: [YautjaHuntConsoleSystem.cs](Content.Server/_CMU14/Yautja/YautjaHuntConsoleSystem.cs), [YautjaHuntTeleporterSystem.cs](Content.Server/_CMU14/Yautja/YautjaHuntTeleporterSystem.cs), [hunting_grounds.yml](Resources/Prototypes/_CMU14/Yautja/hunting_grounds.yml)
- Метки и honor: [YautjaMarkSystem.cs](Content.Shared/_CMU14/Yautja/YautjaMarkSystem.cs), [YautjaHonorScoringTest.cs](Content.IntegrationTests/_CMU14/Yautja/YautjaHonorScoringTest.cs)
- Thrall/youngblood: [YautjaThrallSystem.cs](Content.Server/_CMU14/Yautja/YautjaThrallSystem.cs), [YautjaYoungbloodSystem.cs](Content.Server/_CMU14/Yautja/YautjaYoungbloodSystem.cs)
- Трофеи и ритуалы: [YautjaTrophySystem.cs](Content.Server/_CMU14/Yautja/YautjaTrophySystem.cs), [YautjaRitualSystem.cs](Content.Server/_CMU14/Yautja/YautjaRitualSystem.cs)
- Корабль и визуалы: [huntership_visuals.yml](Resources/Prototypes/_CMU14/Maps/huntership_visuals.yml), [hunter_ship_backends.yml](Resources/Prototypes/_CMU14/Yautja/hunter_ship_backends.yml), [HunterShuttleTest.cs](Content.IntegrationTests/_CMU14/HunterShip/HunterShuttleTest.cs)

