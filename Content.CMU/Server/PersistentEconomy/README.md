# CMU persistent economy

Implements the persistent bank and round cash cycle described in
`Content.CMU/Documentation/persistent-economy-design.md`. The existing character
loadout editor remains authoritative. `cmuLoadoutItem` prototypes add a price and
purchase mode to selected presets without creating a second loadout system.

## Player workflow

`cmubank` opens the player's persistent account. An enabled job can select priced
items in the normal lobby loadout editor. The server validates the assigned job
again while spawning each preset. Deployment items are charged on every new round;
permanent items are charged once and their ownership is retained. Unaffordable paid
items are omitted individually. Existing unpriced loadout choices and standard
issue remain free.

After paid items are processed, the first supported spawn withdraws the configured
stake and gives it as Dollar stacks. The account and round record prevent duplicate
paid gear or stake on respawn. Jobs opt in explicitly with `cmuEconomyEnabled`.

An ATM supports bank withdrawal, transfers and carried-cash deposits. Deposited
cash is held in round escrow rather than credited immediately. The player can take
escrow back as cash before settlement. Escrow plus carried cash is credited at
round end up to the gross settlement cap; bank withdrawals never restore cap.
Dead players forfeit escrow and receive no automatic cash settlement.

The caller's session owns the bank. Cash operations require a living controlled
body, a supported deployment and unobstructed interaction range. Transfers use the
recipient account UUID shown in their account screen. Nested Dollar stacks count;
carried people and unlimited stacks do not.

There is no passive playtime payroll. Existing colony department salaries keep
their round-local ID-card behavior. Persistent rewards must come from an explicit,
authoritative gameplay or RP event and call `Mutate`/`Change` with a stable operation
key. Accounts start at zero, so administrators may use an audited adjustment while
reward integrations are being added.

## Configuration

| CVar | Default | Meaning |
| --- | ---: | --- |
| `cmu.economy_enabled` | true | Enable the persistent path before a round starts. |
| `cmu.economy_stake_percent` | 10 | Integer percent of balance after paid loadout items, 0–100. |
| `cmu.economy_stake_cap` | 2000 | Stake ceiling; 0 disables it. Runtime value is bounded at 100,000. |
| `cmu.economy_settlement_percent` | 200 | Settlement cap multiplier; 200 means ×2, range 0–1000. |

## Persistence

`cmu-economy.sqlite` in the server user-data directory is a separate SQLite
database, including when the main preferences database uses PostgreSQL. It uses
WAL, FULL synchronization, transactions and unique operation keys. Back up the
database and WAL together with the primary server database. Do not share it between
independent servers or restore it independently: round IDs come from the primary
database.

Accounts store balances, lifetime totals, permanent purchases and stake preference.
Round records store deployment, stake, cap, escrow and settlement state. Ledger
entries include UUID, player, round, signed amount, before/after balance, category,
description, UTC timestamp, related player and unique operation key. Raw account
balance changes are confined to `CMUEconomyStore.Operation.Change`.

Gameplay runs synchronously on the server thread. If an entity spawn throws, the
database transaction rolls back and spawned entities are deleted. Deposits commit
before validated stacks are consumed with no await or interaction between those
steps. A process crash still destroys the non-persistent game world; restoring a
world snapshot separately from the bank is unsupported. Database errors fail
closed, and uncertain rewards must be retried with the same operation key.

## Administration

`economy balance|history|round <username-or-UUID>` inspects account data.
`economy add|remove <username-or-UUID> <positive-amount> <reason>` records an
audited adjustment including the administrator. `economy stats` reports balances,
ledger totals, stake/return figures, cap use, cash-store sinks and cash lost at a
normal round end.

The current catalogue prices two presets already available to the base GOVFOR and
OPFOR rifleman roles and the colony administrator: ballistic goggles per deployment
and black-scarf permanent ownership. Add catalogue entries only for existing
loadout presets, with explicit jobs, price, purchase mode and category.
