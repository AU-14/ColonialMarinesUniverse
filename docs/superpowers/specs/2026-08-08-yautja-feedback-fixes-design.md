# Yautja Feedback Fixes Design

## Goal

Исправить подтверждённые дефекты PR #1648, сохранив поведение CMSS13 там, где оно проверено, и сделать Yautja-сценарии проверяемыми через runtime regression tests.

## Scope

1. Dropship FTL: Hunter Ship A/B/Hangar должны использовать точные destination coordinates даже при раздельных map/grid entities.
2. Combat/leap: реальные melee-атаки ксеносов должны наносить уменьшенный, но ненулевой урон; fire resistance должна быть покрыта фактическим tick-тестом; leap не должен наносить self-slam damage и слышимый pain-emote.
3. Medicomp: полный timed-цикл сохраняется; добавляются open-incision semantics и возврат хирургического разреза к surface; active external bleed закрывается только финальным clamp.
4. Radio: Yautja communicator получает свободный английский keycode `#r`, совместимый с CMSS13, и faction frequency для comms tower.
5. Hunting grounds: ghost-role requirements принадлежат CMU и не наследуют RMC tracker time.

Self-destruct не меняется без воспроизводимого action/UI-дефекта: server-side detonation path уже совпадает с CMSS13 и покрыт parity-тестом.

## Architecture

Исправления остаются локальными для существующих CMU/RMC integration points. FTL получает отдельный выбор между точным position arrival и docking proximity fallback; карта и faction whitelist не смешиваются. Medicomp использует существующий surgery session flow, а open-вариант описывается отдельным surgery metadata/prototype. Радио использует штатную faction-frequency систему вместо обходного hard-coded tower поведения.

## Testing

Каждый блок получает regression test на реальный event/system path: FTL после завершения arrival, melee event, fire tick, leap collision/trajectory, timed Medicomp, radio receive через prefix, ghost-role eligibility. После targeted tests выполняются полный Release build и smoke-запуск server/client.

## Acceptance criteria

- A/B/Hangar arrival distance < 2 tiles on actual Hunter Ship map.
- Ravager/T1 melee damage delta on Yautja is > 0 and matches resistance coefficient.
- Leap free-flight and wall collision produce no Yautja self damage or external bleed; normal CMSS13 leap sound remains.
- Full Medicomp timed cycle passes; open incision skips stabilizer and clamp restores surface depth.
- `#r` is not claimed by another channel; two Yautja entities exchange a communicator message; comms tower lists Yautja frequency.
- Hunting-ground ghost roles are eligible with zero CMU rifleman tracker hours.
- Server starts and client connects to it without startup errors.
