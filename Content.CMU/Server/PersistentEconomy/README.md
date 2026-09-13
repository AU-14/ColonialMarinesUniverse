# CMU persistent economy

Implements the version 2 MVP monetary cycle. The original design is preserved in
`Content.CMU/Documentation/persistent-economy-design.md`.

## Player workflow

The lobby's **Bank & personal loadout** button (or `cmubank`) opens a private
account screen for the currently selected character. Save an optional kit, buy
permanent cosmetic ownership, or opt out of the next deployment stake. The
saved projection describes that kit; the assigned job is authoritative and
ineligible items are omitted at deployment. An unaffordable kit is omitted as a
whole. Standard issue is always supplied by the existing spawn system for free.

At the first supported spawn, kit payment and stake withdrawal commit together.
Items and Dollar stacks go into the backpack, then free hands, then onto the
ground if neither can accept them. The account/round record prevents another
stake or kit on respawn. Jobs explicitly opt in with `cmuEconomyEnabled`; their
salary coefficient is `cmuSalaryPercent` (100, 105, 110, 115, 120).

Activate a colony ATM (or use an item on it) for deposit, withdrawal and transfers.
The caller's session owns the bank, not the swiped card. Cash requests require a
living controlled body, a supported deployment and unobstructed interaction
range. Transfers use the recipient's account UUID shown in their account screen.
All carried Dollar stacks, including nested bags, count for deposits; carried
people and unlimited stacks do not. Deposits consume the gross round limit.
Withdrawals do not increase or restore it.

Connected, living, non-AFK players earn salary for each full active minute.
Fractional role multipliers carry integer hundredths forward between minutes.
Ghost observation and disconnected time do not accrue salary. The former
department-to-ID-card payroll is disabled while persistent economy is enabled.
Player shop sales pay physical cash instead of bypassing the cash deposit cap.

At round end living owned bodies settle remaining cash up to their unused cap.
Dead players receive no automatic cash refund; disconnected living bodies still
settle. Each online participant receives a private statement. No KIA fee is charged.

## Configuration

| CVar | Default | Meaning |
| --- | ---: | --- |
| `cmu.economy_enabled` | true | Enable the persistent path. Configure before starting a round. |
| `cmu.economy_stake_percent` | 10 | Integer percent of balance after kit payment, 0–100. |
| `cmu.economy_stake_cap` | 2000 | Stake ceiling; 0 disables it. Configured ceilings are bounded at 100,000. |
| `cmu.economy_settlement_percent` | 200 | 200 means ×2; range 0–1000. |
| `cmu.economy_salary_per_minute` | 6 | Whole dollars per active minute before role multiplier. |

Accounts start at zero and can earn salary immediately. No undocumented starter
grant, automatic hazard award, or completion bonus is minted. The design's
allowance/reward amounts are illustrative; gameplay systems can use the service's
`Mutate`/`Change` with the appropriate ledger category and a stable operation key
when an authoritative reward condition exists. Organization budgets retain their
existing round behavior; persistent organization accounts are not part of this MVP.

## Persistence and operational constraints

`cmu-economy.sqlite` in the server user-data directory is a separate SQLite
database, even when the main preferences database uses PostgreSQL. It uses WAL,
FULL synchronization, transactions and unique operation keys. Back up it and its
WAL together with the primary server database (or use SQLite's online backup).
Do not share this database between independent game servers or restore one
database without the other: round IDs belong to the primary server database.

Accounts contain long balances, lifetime totals, purchases (price/time), and
loadouts keyed by the existing per-player profile slot. Deleting and reusing a
character slot reuses that slot's optional kit. Transactions include UUID, player,
round, signed amount, before/after balance, type, description, UTC timestamp,
related player and unique operation key. Raw account balance changes are confined
to `CMUEconomyStore.Operation.Change`.

Gameplay runs on the server thread. Debit, entity spawn, and SQL commit occur in
one synchronous handler; an exception rolls back SQL and removes spawned items.
Deposits commit before synchronously consuming validated stacks, with no await
or player interaction between them. This supports the current non-persistent
world: a server crash loses that world's physical cash. Restoring world snapshots
independently of the bank is unsupported and would require a durable world outbox.
Banks fail closed on database errors. Never retry a reward with a new operation
key after an uncertain result. UI mutations consume a server-issued one-use token.

The catalogue demonstrates four items for the two base rifleman roles and colony
administrator (sidearm restricted to riflemen). Extend `cmuLoadoutItem` prototypes
with explicit jobs, price, purchase mode, category and category limit. Cosmetics
must not grant functional equipment benefits. Existing free loadout choices are
unchanged; purchases add optional items rather than replacing standard issue.

## Administration and checks

`economy balance|history|round <username-or-UUID>` inspects account data.
`economy add|remove <username-or-UUID> <positive-amount> <reason>` records an
audited adjustment, including the administrator. `economy stats` reports wealth
percentiles, ledger totals, average stake/return/profit, cap use, cash store sinks
and remaining world cash lost at normal round end. Unexpected crashes do not
produce final lost-cash metrics. Monetary movement by organization is not inferred
from anonymous bills.

Regression tests: `CMUEconomyStoreTest` exercises real SQLite transactions,
rollback, conservation, repeated operations, opt-out and reopening a saved bank.
`CMUEconomyPrototypeTest` loads the real client/server catalogue and verifies its
jobs, entities, prices and categories.
