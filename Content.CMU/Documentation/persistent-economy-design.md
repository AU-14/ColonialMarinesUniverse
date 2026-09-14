# CMU Persistent Economy

## Кроссраундовая экономика, наличность, банковские счета и персональные лодауты

**Статус:** Design Specification
**Проект:** Colonial Marines Universe / RuCM
**Версия:** 2.0

---

# 1. Концепция

CMU получает единую кроссраундовую экономику.

Игрок зарабатывает деньги непосредственно через игровой процесс и RP:

* зарплата;
* надбавки;
* премии;
* контракты;
* торговля;
* банковские переводы;
* ограбления;
* найденная и захваченная наличность.

Деньги сохраняются между раундами и используются для:

* персональных лодаутов;
* внутриигровых покупок;
* косметики;
* утилит;
* услуг;
* торговли;
* накопления капитала.

Главная особенность системы:

> **Часть кроссраундового капитала игрок добровольно вводит в текущий раунд в виде физической наличности. От размера этого капитала зависит, сколько наличности игрок сможет вернуть обратно в кроссраундовую экономику.**

---

# 2. Основной денежный цикл

Базовый цикл выглядит так:

```text
Persistent Bank
      │
      │ deployment
      │
      ├── оплата лодаута
      │
      └── 10% остатка → Round Stake
                         │
                         ▼
                   Physical Cash
                         │
             ┌───────────┼───────────┐
             │           │           │
          торговля    покупки    ограбления
             │           │           │
             └───────────┼───────────┘
                         │
                         ▼
                 Cash Settlement
                         │
                         │ максимум Stake × 2
                         ▼
                  Persistent Bank
```

При этом зарплата и официальные выплаты поступают непосредственно на persistent bank и не зависят от лимита наличности.

---

# 3. Банковский аккаунт

Кроссраундовый счёт принадлежит игроку.

```text
PlayerId
└── Bank Account
    ├── Balance
    ├── Lifetime Earned
    ├── Lifetime Spent
    └── Transaction History
```

Баланс общий для всех персонажей игрока.

Например:

```text
Advancer
Balance: $8 000

├── Maxim "Kaban" Kiryukhin
├── John Smith
└── Robert Hayes
```

---

# 4. Персональные лодауты

Деньги относятся к `PlayerId`.

Лодаут относится к конкретному `ProfileId`.

```text
PlayerId
└── Bank Account

ProfileId
└── Saved Loadouts
```

Таким образом персонажи одного игрока могут иметь разные комплекты, но оплачиваются они из общего капитала.

---

# 5. Deployment

Когда игрок впервые получает экономически поддерживаемого персонажа в текущем раунде, выполняется deployment.

Порядок:

```text
1. Определяется назначенная роль.
2. Проверяется сохранённый лодаут.
3. С банковского счёта оплачивается платное снаряжение.
4. Рассчитывается оставшийся баланс.
5. Из него выделяется Round Stake.
6. Stake снимается с банка.
7. Игрок получает эквивалентную сумму физической наличностью.
8. Фиксируется Cash Settlement Cap.
```

---

# 6. Round Stake

По умолчанию:

```text
RoundStake =
10% от банковского баланса после оплаты лодаута
```

Пример:

```text
Баланс до раунда:       $8 000

Лодаут:                  -$500
Осталось:                $7 500

Round Stake 10%:          $750

Persistent balance:      $6 750
Physical cash:             $750
```

Эти `$750` больше не находятся в банке.

Это настоящие физические деньги текущего раунда.

---

# 7. Ограничение максимального Stake

Для защиты поздней экономики рекомендуется опциональный предел:

```text
RoundStake =
min(
    Balance × StakePercent,
    StakeCap
)
```

Стартовые параметры:

```text
StakePercent = 10%
StakeCap     = $2 000
```

Таким образом:

| Баланс после лодаута |     10% | Round Stake |
| -------------------: | ------: | ----------: |
|                 $500 |     $50 |         $50 |
|               $2 000 |    $200 |        $200 |
|              $10 000 |  $1 000 |      $1 000 |
|              $20 000 |  $2 000 |      $2 000 |
|              $50 000 |  $5 000 |      $2 000 |
|             $100 000 | $10 000 |      $2 000 |

`StakeCap` должен быть конфигурируемым и при необходимости может быть отключён.

---

# 8. Cash Settlement Cap

Количество физической наличности, которое игрок может вернуть в persistent economy за один раунд, зависит от его Round Stake.

Базовая формула:

```text
CashSettlementCap =
RoundStake × SettlementMultiplier
```

Начальный multiplier:

```text
SettlementMultiplier = 2.0
```

Если игрок внёс в раунд `$750`:

```text
Round Stake:          $750
Settlement Cap:     $1 500
```

Следовательно максимальная прибыль именно от раундовой наличности:

```text
$1 500 - $750 = $750
```

Игрок может максимум удвоить поставленный под риск капитал.

---

# 9. Пример успешного раунда

Игрок начинает с:

```text
Persistent balance:     $8 000
```

Лодаут:

```text
-$500
```

После него:

```text
$7 500
```

Stake:

```text
10% = $750
```

После deployment:

```text
Persistent Bank:        $6 750
Cash:                     $750
Settlement Cap:         $1 500
```

За раунд персонаж:

```text
продал вещи              +$250
получил долю добычи      +$700
нашёл                    +$100
```

К концу:

```text
Cash:                    $1 800
```

Но вывести можно:

```text
max $1 500
```

Следовательно:

```text
Persistent Bank:
$6 750
+$1 500
= $8 250
```

Прибыль на рискованном капитале:

```text
+$750
```

Оставшиеся `$300` должны быть потрачены, переданы или будут потеряны после завершения раунда.

---

# 10. Зарплата не входит в Settlement Cap

Системные выплаты не являются раундовым лутом.

Они поступают непосредственно на persistent account.

Например:

```text
Persistent Bank:       $6 750

Salary:                 +$450
Hazard Pay:             +$100
Completion:             +$100

Bank after payroll:     $7 400
```

После этого settlement наличности:

```text
+$1 500
```

Финальный баланс:

```text
$8 900
```

Начальный баланс составлял `$8 000`.

Изменение:

```text
+$900
```

Из них:

```text
+$650 — зарплата и официальные выплаты
+$750 — максимальная прибыль наличными
-$500 — deployment loadout
```

---

# 11. Дополнительное снятие денег в раунде

Игрок может пользоваться банкоматом после deployment.

Например:

```text
Withdraw $500
```

Но это **не увеличивает Round Stake**.

И соответственно не увеличивает Settlement Cap.

Round Stake фиксируется только при первоначальном deployment.

Это предотвращает схему:

```text
получить $5 000 добычи
↓
за минуту до конца снять $2 500
↓
искусственно увеличить лимит
↓
вернуть $5 000
```

Такой обход невозможен.

---

# 12. Фиксация Stake

Для каждого игрока в раунде хранится:

```text
RoundStake
SettlementCap
CashCreditedThisRound
DeploymentCompleted
```

Ключ:

```text
PlayerId + RoundId
```

Stake нельзя повторно получить через:

* respawn;
* reconnect;
* смену тела;
* revive;
* повторный spawn;
* смену роли.

---

# 13. Cash Deposit

Физическую наличность разрешено вносить обратно в банк.

Но за весь раунд:

```text
TotalCashCredited <= SettlementCap
```

Например:

```text
Stake:              $500
Settlement Cap:   $1 000
```

Игрок в середине раунда внёс:

```text
$400
```

Его оставшийся лимит:

```text
$600
```

Позже он может внести ещё максимум `$600`.

---

# 14. Повторное снятие уже внесённых денег

Если игрок:

```text
внёс $400
↓
снял $400
↓
пытается внести снова
```

первоначальные `$400` уже считаются использованной частью Settlement Cap.

То есть учитывается **gross cash credit**, а не текущий net-flow.

Это упрощает защиту системы от циклических операций.

---

# 15. Игрок без Stake

Игрок может иметь:

```text
RoundStake = $0
```

В этом случае:

```text
SettlementCap = $0
```

Он всё равно может:

* получать зарплату;
* покупать вещи;
* получать банковские переводы;
* пользоваться бесплатным standard issue;
* находить деньги;
* торговать наличными.

Но физические деньги текущего раунда не могут стать его новым persistent wealth.

Таким образом возможность заработать на раундовой наличности требует предварительно поставить под риск собственный капитал.

---

# 16. Раздел добычи

Игроки могут делиться деньгами.

Это допустимая часть системы.

Например КОФ украли `$20 000`.

У десяти участников:

```text
Round Stake:
по $500

Settlement Cap:
по $1 000
```

Максимально вся группа сможет вывести:

```text
10 × $1 000
= $10 000
```

Даже если физически было украдено `$20 000`.

При этом каждый участник предварительно поставил по `$500` собственного капитала.

Раздел награбленного поэтому становится осмысленной частью RP, а не обходом бесплатного персонального лимита.

---

# 17. Ограбление администрации

Крупные ограбления остаются полноценной механикой.

Пример:

```text
Administration Vault:
$30 000

CLF steals:
$25 000
```

Эти `$25 000` существуют в текущем раунде и могут быть:

* поделены;
* потрачены;
* использованы для закупок;
* переданы союзникам;
* спрятаны;
* потеряны;
* частично выведены через settlement.

Но они не превращаются автоматически в `$25 000` persistent wealth.

Количество выводимых денег ограничивается суммой капитала, который участники сами ввели в операцию.

---

# 18. Почему происхождение купюр не отслеживается

Система принципиально не должна определять:

```text
эта купюра из администрации
эта из зарплаты
эта украдена
эта получена через торговлю
эта лежала на карте
```

После появления физической наличности все купюры эквивалентны.

Контроль осуществляется не по происхождению денег, а через:

```text
Round Stake
+
Settlement Cap
```

Это значительно проще технически и устойчивее к обходам.

---

# 19. Банк и наличность

Банк и наличность являются одной валютой, но выполняют разные функции.

### Persistent Bank

* безопасен;
* сохраняется между раундами;
* используется для лодаутов;
* используется для permanent purchases;
* принимает зарплату;
* позволяет банковские переводы.

### Physical Cash

* существует внутри мира;
* можно украсть;
* можно потерять;
* можно передать;
* можно использовать в RP;
* можно превратить обратно в persistent wealth только в пределах Settlement Cap.

---

# 20. Смерть

Персонаж не удаляется после смерти.

```text
Round #100:
Maxim "Kaban" Kiryukhin
KIA

Round #101:
тот же Maxim "Kaban" Kiryukhin
снова доступен
```

CMU не использует permanent death персонажей.

---

# 21. Деньги и смерть

Главный экономический риск смерти — физическая наличность.

Например:

```text
Bank:             $8 000
Round Stake:        $800
Cash:               $800
```

Игрок погиб.

Его `$800` остаются на теле.

Их может:

* забрать товарищ;
* забрать противник;
* украсть мародёр;
* вернуть кто-либо игроку;
* потерять вместе с картой.

Таким образом игрок действительно рискует частью накопленного состояния каждый раунд.

---

# 22. KIA Fee

Так как Round Stake уже создаёт серьёзное финансовое последствие смерти, прежний большой процентный KIA fee становится менее необходимым.

Для первой версии рекомендуется:

```text
KIA Fee: disabled
```

или использовать небольшой фиксированный recovery cost:

```text
$50–100
```

Если статистика покажет недостаточный общий денежный sink, позднее можно включить:

```text
Fixed Fee
+
малый Percentage
+
Cap
```

Например:

```text
$100 + 5%
max $1 000
```

Но главным финансовым риском смерти должна оставаться потеря находящейся при персонаже наличности, а не двойное списание состояния.

---

# 23. Settlement в конце раунда

Если персонаж заканчивает раунд живым, оставшуюся при нём наличность можно автоматически обработать.

Формула:

```text
AvailableCap =
SettlementCap - CashCreditedThisRound
```

```text
AutoSettlement =
min(
    CashCarried,
    AvailableCap
)
```

Например:

```text
Settlement Cap:          $1 000
Ранее внесено:             $300
Осталось лимита:           $700

Cash carried:              $950
```

Автоматически вернётся:

```text
$700
```

Оставшиеся `$250` не становятся persistent-деньгами.

---

# 24. Settlement погибшего игрока

Если персонаж считается окончательно погибшим на момент окончания раунда:

```text
Automatic Cash Settlement = $0
```

Деньги, оставшиеся на его теле, не возвращаются владельцу автоматически.

Если их до конца раунда забрал другой живой игрок, он может вывести их в пределах собственного Settlement Cap.

Это создаёт реальный экономический смысл:

* эвакуации;
* спасению товарищей;
* возвращению имущества;
* мародёрству;
* ограблениям.

---

# 25. Зарплата

Главный контролируемый faucet — зарплата.

Стартовая модель:

```text
Base salary:
$6 / active minute
```

За 75 минут:

```text
$450
```

Дополнительно:

```text
Role allowance        ~$50
Hazard pay           ~$100
Completion           ~$100
```

Типичный полноценный доход:

```text
$650–800
```

---

# 26. Разница между ролями

Различия должны быть заметны для RP, но недостаточны для выбора должности исключительно ради денег.

Рекомендуемые коэффициенты:

| Категория            | Коэффициент |
| -------------------- | ----------: |
| Обычный персонал     |       ×1.00 |
| Специалисты          |       ×1.05 |
| NCO                  |       ×1.10 |
| Офицеры              |       ×1.15 |
| Старшее командование |       ×1.20 |

---

# 27. Другие контролируемые faucets

Допускаются:

```text
Payroll
Hazard Pay
Contracts
Official Bonuses
Commendations
Scripted Rewards
Administration Compensation
```

Не рекомендуется выдавать значимые persistent-деньги напрямую за убийства.

---

# 28. Лодауты

Standard Issue всегда бесплатен.

Например:

```text
Standard rifle          FREE
Standard armor          FREE
Standard helmet         FREE
Standard ammunition     FREE
Basic equipment         FREE
```

Игрок никогда не должен оказаться неспособным выполнять роль только потому, что у него нет денег.

---

# 29. Платные лодауты

Деньги позволяют персонализировать deployment.

Например:

```text
Polarized goggles        $40
Black scarf              $20
Large utility pouch     $100
Alternative belt        $120
Personal sidearm        $250
```

Итого:

```text
Deployment cost:        $530
```

Стоимость списывается до расчёта Round Stake.

---

# 30. Почему лодаут оплачивается первым

Если сначала вывести 10% баланса наличными, а потом оплачивать комплект, можно получить недостаток средств.

Поэтому порядок всегда:

```text
Balance
↓
Loadout Payment
↓
Remaining Balance
↓
Stake Calculation
```

---

# 31. Permanent Purchases

Косметика может покупаться навсегда.

Например:

```text
Veteran M10 Helmet
$5 000

[BUY]
```

После покупки:

```text
OWNED
```

Permanent-косметика не требует повторной оплаты deployment.

---

# 32. Functional Equipment

Функциональное дополнительное снаряжение оплачивается за deployment.

Это создаёт постоянный sink.

```text
COSMETIC
→ permanent purchase

FUNCTIONAL EQUIPMENT
→ deployment fee

CONSUMABLE
→ deployment fee / in-round purchase
```

---

# 33. Ограничения экипировки

Цена не является единственным балансным фактором.

Используются:

```text
Price
+
Job restrictions
+
Slots
+
Category limits
```

Например:

```text
Primary Weapon      0/1
Sidearm             0/1
Armor               0/1
Helmet              0/1
Back                0/1
Belt                0/1
Pouches              0/2
Utility              0/3
Explosives           0/1
```

Богатый Rifleman не должен покупать возможности Specialist, Medic и Engineer одновременно.

---

# 34. Внутриигровые покупки

Деньги должны использоваться не только в меню лодаута.

Примерные категории:

| Категория       | Примерная стоимость |
| --------------- | ------------------: |
| Напитки         |               $3–15 |
| Еда             |               $5–25 |
| Сигареты        |              $10–30 |
| Личные предметы |             $20–100 |
| Одежда          |             $50–300 |
| Утилиты         |             $50–300 |
| Развлечения     |             $10–150 |
| Услуги          | зависит от механики |

---

# 35. NPC и player commerce

Покупка у системного продавца:

```text
Player -$100
System receives nothing

ΔM = -$100
```

Это sink.

Покупка у игрока:

```text
Buyer  -$100
Seller +$100

ΔM = 0
```

Это перераспределение.

---

# 36. Организационные деньги

Организации также могут иметь счета:

```text
Colonial Administration
CLF
Weyland-Yutani
Military Command
```

Например администрация имеет:

```text
$100 000
```

и снимает в сейф:

```text
$20 000
```

Тогда:

```text
Organization bank:
$100 000 → $80 000

Physical vault:
$0 → $20 000

ΔM = 0
```

Это предпочтительнее бесконечного спавна крупных сумм каждый раунд.

---

# 37. Общая денежная масса

Концептуально:

```text
Total Money =
Persistent Accounts
+
Organization Accounts
+
Physical Cash
```

Операции:

```text
Bank → Cash
ΔM = 0

Cash → Bank
ΔM = 0

Player → Player
ΔM = 0

Robbery
ΔM = 0

Salary
ΔM > 0

NPC purchase
ΔM < 0

Lost cash
ΔM < 0
```

---

# 38. Контроль инфляции

Основное уравнение:

```text
Net Emission =
System Faucets
-
System Sinks
```

К системным faucets относятся:

```text
Salary
Bonuses
Contracts
Scripted Rewards
New Organization Funding
```

К sinks:

```text
Paid Loadouts
NPC Shops
Services
Permanent Cosmetics
Lost Cash
Optional KIA Fees
```

Settlement наличности сам по себе не является faucet.

Он лишь возвращает часть уже существовавших денег обратно в persistent economy.

---

# 39. Экономический профиль среднего игрока

Пример:

```text
INCOME

Salary                +$450
Role Allowance          +$50
Hazard Pay             +$100
Completion             +$100
────────────────────────────
                       +$700
```

Расходы:

```text
Loadout                -$250
Stores                  -$90
Utilities               -$40
Average Cosmetics       -$70
Misc                    -$30
────────────────────────────
                       -$480
```

Дополнительно игрок вводит часть капитала в Round Stake.

Stake не является расходом автоматически — деньги могут вернуться.

Но он является капиталом под риском.

---

# 40. Три исхода Stake

## Успешный

```text
Stake:        $500
Returned:   $1 000

Profit:       $500
```

## Нейтральный

```text
Stake:        $500
Returned:     $500

Profit:         $0
```

## Неудачный

```text
Stake:        $500
Returned:     $100

Loss:        -$400
```

## Полная потеря

```text
Stake:        $500
Returned:       $0

Loss:        -$500
```

Это создаёт настоящий риск капитала.

---

# 41. Почему ×2

Multiplier ×2 создаёт понятную симметрию:

> Игрок может потерять до 100% поставленного капитала или заработать до 100% поверх него.

При:

```text
Stake = X
```

диапазон результата:

```text
0 … 2X
```

Чистое изменение:

```text
-X … +X
```

Это просто объяснить игрокам и просто балансировать.

Multiplier должен быть конфигурируемым.

---

# 42. Поздний вход

Latejoin-игрок получает Round Stake только при первом фактическом deployment.

Stake считается от его текущего банковского баланса после оплаты лодаута.

Не требуется пересчитывать процент из-за меньшей продолжительности раунда.

При необходимости позднее можно добавить коэффициент по оставшемуся времени, но для MVP это излишне.

---

# 43. Повторный spawn

Round Stake выдаётся строго один раз.

Например:

```text
PlayerId
RoundId
DeploymentStakeIssued = true
```

Повторный spawn не создаёт новую наличность и не меняет Settlement Cap.

---

# 44. Экономически неподдерживаемые роли

Не все роли обязаны участвовать в денежной системе.

Например:

```text
Xeno
Event Monster
Certain Admin Roles
```

могут иметь:

```text
EconomyEnabled = false
```

В таком случае:

```text
RoundStake = 0
SettlementCap = 0
```

и банковский баланс игрока не затрагивается.

---

# 45. Transaction Ledger

Каждое изменение persistent balance обязательно логируется.

Пример:

```text
14.09 20:21  PAYROLL             +$450
14.09 20:30  LOADOUT             -$250
14.09 20:30  ROUND_STAKE         -$500
14.09 21:10  CASH_DEPOSIT        +$300
14.09 22:01  CASH_SETTLEMENT     +$700
```

---

# 46. Структура транзакции

Минимальные поля:

```text
TransactionId
PlayerId
RoundId
Amount
BalanceBefore
BalanceAfter
Type
Description
Timestamp
RelatedPlayerId
UniqueOperationKey
```

---

# 47. Типы операций

Рекомендуемые категории:

```text
Payroll
RoleAllowance
HazardPay
CompletionPay
ContractReward

LoadoutDeployment
PermanentPurchase
StorePurchase
ServicePurchase

RoundStake
CashWithdrawal
CashDeposit
CashSettlement

PlayerTransferIn
PlayerTransferOut

KiaRecovery
Refund
AdminAdjustment
```

---

# 48. Идемпотентность

Критические операции должны выполняться только один раз.

Особенно:

```text
Payroll
RoundStake
LoadoutDeployment
CashDeposit
RoundEndSettlement
PermanentPurchase
PlayerTransfer
```

Повторный network event, reconnect или server retry не должен создавать деньги.

---

# 49. База данных аккаунта

Пример:

```csharp
[Table("cmu_player_accounts")]
public sealed class CMUPlayerAccount
{
    [Key]
    public Guid PlayerId { get; set; }

    public long Balance { get; set; }

    public long LifetimeEarned { get; set; }

    public long LifetimeSpent { get; set; }

    public DateTime UpdatedAt { get; set; }
}
```

Для денег используется целочисленный тип.

`float` и `double` не применяются.

---

# 50. Round Economy State

Необходимо хранить временное состояние раунда:

```text
PlayerId
RoundId

Stake
SettlementCap
CashCredited
DeploymentIssued
```

Его можно хранить:

* в серверной памяти с persistence-critical transaction records;
* либо в отдельной DB-таблице.

Для защиты от рестартов предпочтительнее persistence.

---

# 51. Permanent Purchases

Отдельно:

```text
cmu_player_purchases

PlayerId
PurchaseId
PurchasedAt
PricePaid
```

---

# 52. Loadout Data

Экономические свойства предметов должны задаваться прототипами.

Например:

```yaml
- type: cmuLoadoutItem
  id: PersonalSidearm

  entity: SomeSidearm
  price: 250

  purchaseMode: Deployment

  jobs:
  - AU14JobGOVFORPlatoonRifleman

  category: Sidearm
```

Permanent:

```yaml
- type: cmuLoadoutItem
  id: VeteranHelmet

  entity: VeteranHelmetEntity
  price: 5000

  purchaseMode: Permanent

  category: CosmeticHelmet
```

---

# 53. Единая точка управления балансом

Системы не должны напрямую менять поле `Balance`.

Используется единый сервис:

```text
Credit(...)
Debit(...)
Transfer(...)
CreateStake(...)
DepositCash(...)
SettleCash(...)
```

Он отвечает за:

* DB transaction;
* ledger;
* проверки;
* недостаток средств;
* idempotency;
* audit.

---

# 54. Банкомат

ATM должен предоставлять:

```text
BALANCE

WITHDRAW

DEPOSIT

TRANSFER

ROUND FINANCES

HISTORY
```

Раздел `ROUND FINANCES`:

```text
Deployment Stake:        $750
Settlement Limit:      $1 500
Already Deposited:       $400
Remaining Capacity:    $1 100
```

---

# 55. Интерфейс лодаута

Пример:

```text
MAXIM "KABAN" KIRYUKHIN

Bank:
$8 000

LOADOUT

M10 Helmet                FREE
Polarized Goggles        OWNED
Large Utility Pouch       $100
Personal Sidearm          $250

Loadout Cost:
$350

Projected Balance:
$7 650

Projected Round Stake:
$765

Settlement Limit:
$1 530

[ SAVE ]
```

Игрок заранее видит финансовое последствие своего deployment.

---

# 56. Финансовый отчёт после раунда

Пример:

```text
ROUND FINANCIAL STATEMENT

Starting Balance        $8 000

Loadout                  -$350
Round Stake              -$765

Payroll                  +$450
Hazard Pay               +$100
Completion               +$100

Cash Deposited           +$500
End Settlement         +$1 030

─────────────────────────────
Final Balance           $9 065

Cash profit              +$765
Service income           +$650
Loadout cost             -$350
```

Так игроку понятно, почему баланс изменился.

---

# 57. Административные инструменты

Необходимы:

```text
economy balance <player>

economy history <player>

economy round <player>

economy add <player> <amount> <reason>

economy remove <player> <amount> <reason>

economy stats
```

Любые ручные изменения также проходят через ledger.

---

# 58. Аналитика

Минимально собирать:

```text
Median Bank Balance
Average Bank Balance
P90
P99

Average Stake
Average Settlement
Average Cash Profit

Payroll Created
Loadout Sink
Store Sink
Lost Cash

Total Cash Deposited
Total Cash Withdrawn

Average Settlement Cap Utilization

CLF / Administration cash movement
```

Особенно важна метрика:

```text
Cash Profit / Stake
```

Она покажет, насколько легко игроки достигают максимального ×2.

---

# 59. Тревожные признаки

Если:

```text
почти все каждый раунд получают ×2
```

экономика слишком щедра.

Если:

```text
почти никто не возвращает даже собственный Stake
```

риск слишком высокий.

Если:

```text
большинство выбирает Stake = 0
```

награда недостаточна.

Если:

```text
игроки постоянно выбирают максимально возможный Stake
```

вероятно риск слишком мал либо доход слишком велик.

---

# 60. Стартовая конфигурация

Рекомендуемая первая настройка:

```text
StakePercent          = 10%
StakeCap              = $2 000
SettlementMultiplier = 2.0

Salary                = $6 / active minute

Typical Income        = $650–800 / round

Typical Loadout       = $200–350

KIA Percentage Fee    = disabled

Optional Recovery Fee = $0–100
```

---

# 61. MVP

Первая версия должна включать:

```text
Persistent Bank Account
Transaction Ledger

Payroll

Paid Loadouts

10% Deployment Stake

Physical Cash Spawn

Settlement Cap ×2

ATM Withdraw
ATM Deposit

Round-end Cash Settlement

Player Transfers

Permanent Cosmetics

Admin Economy Tools

Basic Economy Analytics
```

---

# 62. Не входит в MVP

Не требуется сразу реализовывать:

```text
Loans
Interest
Taxes
Investments
Stock Market
Complex Insurance
Businesses
Player Corporations
Multiple Banks
Dynamic Currency Exchange
```

Эти механики можно добавлять только после стабилизации базового денежного цикла.

---

# 63. Итоговая модель

Игрок имеет:

```text
$10 000 persistent capital
```

Он покупает лодаут:

```text
-$500
```

Остаётся:

```text
$9 500
```

В раунд автоматически входит:

```text
$950 cash
```

Банк:

```text
$8 550
```

Игрок получает:

```text
Settlement Cap:
$1 900
```

В течение раунда эти `$950` находятся под настоящим риском.

Игрок может:

```text
потерять всё
→ -$950

сохранить свои деньги
→ ±$0

успешно заработать
→ до +$950
```

Отдельно он получает обычную зарплату за службу.

---

# 64. Главный принцип системы

Кроссраундовая экономика не должна быть отдельным меню прогрессии поверх игры.

Она должна проходить непосредственно через игровой мир:

> **Игрок зарабатывает состояние службой, вводит часть этого состояния в текущий раунд, рискует им в процессе игры и может увеличить его за счёт торговли, добычи, ограблений и других RP-взаимодействий.**

Ключевая формула:

```text
Stake = часть собственного капитала

Maximum Cash Return =
Stake × SettlementMultiplier
```

При базовом ×2:

```text
максимальный убыток = -100% Stake
максимальная прибыль = +100% Stake
```

Таким образом деньги одновременно являются:

* прогрессией;
* ресурсом;
* объектом риска;
* предметом торговли;
* наградой за RP;
* причиной для взаимодействия;
* долгосрочным состоянием игрока.

**Сам игровой денежный оборот становится кроссраундовой прогрессией.**
