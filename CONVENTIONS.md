# CMU Conventions

Technical conventions for CMU code: C#, YAML, localization, and engine idioms. Agent workflow
and operational policy live in `AGENTS-CMU.md`.

## Precedence

**CMU conventions > RMC-14 coding standards > Wizden conventions > general C# idiom / `.editorconfig`**

CMU inherits from RMC through Wizden. AU14 is a legacy CMU zone, not an upstream layer.

## CMU naming

New CMU prototype IDs use the `CMU` prefix with PascalCase; new localization IDs use `cmu-`
with kebab-case.

```yaml
id: CMUColonyBudget
```
```text
cmu-colony-budget-insufficient
```

Do not retrofit prefixes onto existing Wizden or RMC identifiers solely for consistency. The
prefix prevents collisions between independently developed layers of the fork.

## CMU14 tag format

Required on every deliberate CMU change outside CMU-owned zones (`_CMU14`, `Content.CMU/`).
Policy (what counts as divergence, removals, merge behavior) lives in `AGENTS-CMU.md`.

Base forms, with optional reason:

```csharp
// CMU14
// CMU14: required for CMU colony accounting
```
```yaml
# CMU14
```

Tweaks, usings, and field changes tag the changed line inline, per field in multi-line
additions; a lone comment line above a changed line is wrong (kind markers on new constructs
are the exception). New constructs get a kind marker on top:
`// CMU14 class`, `// CMU14 event`, `// CMU14 method`, `// CMU14 type`, `// CMU14 file`.
Large contiguous blocks may use:

```csharp
// CMU14 SomeSystem Begin: reason
...
// CMU14 End
```

Whole new YAML prototypes tag the `type:` or `id:` line (`# CMU14 entity`, `# CMU14 gameMap`,
...). In a wrapped boolean chain the tag rides the end of the changed clause:

```csharp
if (!enabled // CMU14
    || force)
```

## C# style

`.editorconfig` handles mechanically enforceable formatting. For new code:

* expression-bodied members wherever they improve clarity, with `=>` on its own line one
  indent deeper;
* boolean chains wrapped with leading `&&`/`||`, never trailing;
* no braces around single-statement `if`/`else` bodies;
* file-scoped namespaces, never block namespaces;
* wrap long parameter lists one parameter per line;
* fields and auto-properties before methods;
* XML doc comments (`/// <summary>`) on public methods and DataFields, stating what and why;
* these apply to every member we add, even beside block-bodied neighbours that predate the
  rule;
* do not restyle untouched code merely to conform.

Extend upstream types with a `partial` class in the CMU zone rather than editing the upstream
file to add CMU fields:

```csharp
namespace Content.Shared.Humanoid.Markings;

public sealed partial class MarkingPrototype
{
    [DataField]
    public readonly Vector2 Offset;
}
```

Prefix a shared type with `Shared` only when server or client counterparts of the same name
exist; a shared-only type carries no prefix.

## Data fields

Use bare `[DataField]` when the field name already maps to the desired YAML key; `[DataField]`
on `FlatReduction` already serializes as `flatReduction`. Spell out a name only when the YAML
key genuinely differs from the field name. Data definitions are `partial` with a parameterless
constructor. Type serializers must not use `IoCManager.Resolve`; serialization can run
off-thread.

## Components

Components are data; logic lives in their entity systems. New components are `partial`, carry
`[RegisterComponent]`, and keep fields public; use `[Access]` to restrict writes to the owning
system when warranted. No behavior in property setters. Put the whole component in Shared even
when some fields are one-side-only; abstract shared components with per-side inheritors are a
legacy pattern. The YAML name is the class name minus the `Component` suffix.

## Entity systems

Inject dependencies with `[Dependency]`, kept together near the top of the system; do not
inject `IEntityManager` when the base already provides `EntityManager`, and prefer system proxy
methods over reaching through it directly. Avoid polling large entity populations every tick;
prefer events or cached `EntityQuery<T>` with targeted update sets.

Public system methods follow one shape so entity/component validity is explicit: entity
arguments first (`EntityUid` or `Entity<T?>`), ordinary parameters after, and entity/component
references resolved at the top of the method.

Do not write extension methods on `EntityUid`, components, or systems; expose public system
methods instead.

To extend an upstream system's behavior without copying its body, subclass it in the CMU
zone: unseal the upstream system with `[Virtual]` and mark the target method `virtual`
(two word-level tagged edits), then call `base.` for the inherited behavior and add the CMU
behavior after. The engine instantiates only the most-derived system of a chain and
resolves the upstream type to it, so upstream changes inside `base` methods merge through
untouched and signature changes fail loudly at compile time. The base's event subscriptions
run on the override instance: never re-subscribe a pair the base already owns - override
the handler method or subscribe new pairs. One override per upstream system; multiple
children of one base make supertype resolution ambiguous.

## Events

Events are `[ByRefEvent]` structs raised by reference; classes only where the engine's event
model requires inheritance. Name events with the `Event` suffix and their handlers
`OnXEvent`. Gate cancellable actions with `Attempt` events
(`CancellableEntityEventArgs`). Prefer directed events over broadcast when only one entity or
subsystem needs to react; `RaiseLocalEvent(uid, ev)` is directed only, and a broadcast event
concerning an entity carries the `EntityUid` in a field. Component lifecycle hooks are the
`ComponentAdd`, `ComponentInit`, `ComponentStartup`, `ComponentShutdown`, and
`ComponentRemove` events. Use `before:`/`after:` ordering only when genuinely necessary (it is
the slower path). No method-events: when an action has a system-level API, wrap the raise
behind a public system method. No asynchronous simulation logic; DoAfter flows are events.
C# events belong to out-of-simulation code (UI) and must always be unsubscribed; entity
subscriptions via `SubscribeLocalEvent`/`Subs.*` already handle system lifetime. A directed
subscription pair (`SubscribeLocalEvent<TComp, TEvent>`) may be claimed by exactly one system
per process - a shared system and a server system count as two claims in the server process,
and a second one throws `InvalidOperationException: Duplicate Subscriptions` at startup,
never at compile time. Grep for the pair before adding it; when two systems need it, one owns
the subscription and the other exposes a public method for it, or subscribes via a different
component.

## Networking

`EntityUid` is not net-serializable. Convert at the boundary: `GetNetEntity` on the sender;
`TryGetEntity` (messages) or `EnsureEntity<TComp>` (component states) on the client. Store
`EntityUid` on components, never `NetEntity`, unless the data contract specifically requires
the network representation, and null out `EntityUid` fields when the referenced entity is
deleted or networking errors. Optional entities are `EntityUid?`, never `EntityUid.Invalid`.

Generated component state uses `[AutoGenerateComponentState]` with `[AutoNetworkedField]` on
the fields. Changed networked data must be dirtied or the client never sees it.

Components with several independently changing networked fields use
`[AutoGenerateComponentState(fieldDeltas: true)]` and
`DirtyField(uid, comp, nameof(Field))` instead of a full `Dirty`; roughly three or more
independent fields makes it worthwhile. Networked custom reference types implement
`IRobustCloneable` so prediction resets deep-copy them. `netsync: false` on a component opts
it out of networking entirely.

## Prediction

Plain shared code still waits for the server round-trip; predicted actions run on the client
immediately and reconcile against it. To predict a system: move the component and system to
Shared, add `[NetworkedComponent]` with `[AutoGenerateComponentState]`, and share every
dependency first; where shared code must call a server-only API, declare an empty virtual
method on the shared system and override it on the server. The client system must exist even
when empty (see Shared placement).

Effects in predicted code use the user-passing variants: `PlayPredicted`, `PopupPredicted`,
`PopupClient`, `PredictedSpawnAtPosition`, `PredictedSpawnAttachedTo`. Plain variants under
prediction replay 10x or never arrive, and the predicted ones need a user argument, so they
cannot be used in update loops or container events, which have no single user. Predicted
deletion uses `PredictedDeleteEntity`/`PredictedQueueDeleteEntity`; the plain variants error
on networked entities from the client.

`IRobustRandom` in shared code mispredicts; use `PredictedProb`/`PredictedRandom` (seeded from
the entity's `NetEntity` and the tick), and keep exploitable randomness (loot rolls, store
prices, antag selection) server-side. Events raised while applying server state (container
insert/remove, equip, damage, solution change) fire on both sides; guard state-dependent side
effects with `if (_timing.ApplyingState) return;`. Do not use `IGameTiming.IsFirstTimePredicted`
to hide mispredicts; fix the underlying misprediction instead.

Test prediction with `sudo cvar net.fakelagmin 0.5`, two client windows, and the `quickinspect`
command; flickering sprites, doubled audio, or repeated popups are mispredicts. ViewVariables
(`vv <EntityUid>`) inspects an entity on both sides at once.

## PVS and hidden information

Clients only receive entities within PVS range of their attached entity; outside it entities
are paused and detached into nullspace on the client, so they cannot be predicted. Entities
with no physical location (minds, objectives) live in nullspace to keep their data off
clients. PVS overrides (`AddSessionOverride`, `AddGlobalOverride`, `AddViewSubscriber`) are
used sparingly; each adds networking load. Components carrying secrets use `SendOnlyToOwner`
or `SessionSpecific` so only the owning session receives them.

## Appearance and visualizers

The server never drives sprites directly. It writes small appearance values via
`_appearance.SetData(ent, Visuals.Key, value)`; systems deriving `VisualizerSystem<T>` read
them in `OnAppearanceChange` and drive `SpriteComponent` layers. Appearance keys are enum
values (marked `[Serializable, NetSerializable]`), never strings. Map sprite layers with
`map: ["enum.SomeVisualLayers.Key"]` so visualizers address layers by enum. When the mapping
is just layer state or visibility per value, use the `GenericVisualizer` component in YAML
instead of writing a custom visualizer system.

## Coordinates and transforms

`EntityCoordinates` are parent-relative, not world-space; use the transform system for
world-space math. Reparenting (containers, buckling, anchoring) changes what entity-relative
coordinates mean, and space has no grid: logic that must work off-grid cannot rely on a valid
grid id. World units are meters (one tile is 1 m), right-handed with +X east and +Y north;
UI coordinates start top-left with +Y down.

## Time and timers

`TimeSpan` for static periods, never floating-point seconds; compare timers against `CurTime`
in simulation code. Advance with `NextUpdate += Interval` so the remainder is preserved.
`[AutoPausedField]` on runtime-mutated time fields that must respect paused entities.
Runtime-modified absolute times use `[DataField(customTypeSerializer:
typeof(TimeOffsetSerializer))]` so pausing and map serialization stay correct.

## Performance

No allocations or LINQ in per-frame or per-tick hot paths; iterator methods avoid
intermediate collections but still allocate their enumerator, and lambda/local-function
captures allocate too. For polling and query caching see Entity systems. Dirtying is the
expensive part of networking: guard setter methods to return early when the value is
unchanged, never dirty from an update loop every tick, and where a value changes continuously
(hunger, battery charge) network a timestamp plus rate of change and let the client infer the
current value.

## Prototypes

Prefer prototypes over enums for extensible in-game types, but do not invent a new prototype
kind where a component on the entity prototype suffices. Never mutate prototype data at
runtime; it is not synchronized state. Reference prototypes with `ProtoId<T>`, never raw
path or ID strings, and resolve through the prototype manager rather than caching.

## Entity tables

Container fills and spawners use entity tables (`EntityTableContainerFill`,
`EntityTableSpawner`), not ad-hoc spawn lists. Selectors carry explicit `!type:GroupSelector`
tags; a bare `id:` entry is the `EntSelector` shorthand. Reusable tables are `entityTable`
prototypes referenced through `!type:NestedSelector`, so the same table is never duplicated.

## Construction

Construction is defined by graph prototypes (nodes, edges, steps) referenced from recipe
prototypes. Reuse existing nodes, edges, and step types before adding new kinds.

## Resources

Sounds use `SoundSpecifier`, preferring `SoundCollectionSpecifier` over hardcoded paths when
a collection fits. Sprites and textures use `SpriteSpecifier`. No raw resource paths in data
fields where a specifier exists. RSI `meta.json` keeps field order
`version > license > copyright > size > states`, is never minified, and indents with 4 spaces,
never tabs. In-game guidebook entries are XML documents under `Resources/ServerInfo/Guidebook/`
paired with a metadata prototype, using markdown-style formatting tags.

## YAML

List items sit at the key's indent (`components:` and `- type:` flush), not indented deeper.
Use inline lists for short value sets and normal lists for everything else.

```yaml
- type: entity
  parent: [PartHuman, BaseHead]
  id: CMUExampleHead
  components:
  - type: Sprite
  - type: Tag
    tags:
    - HighRiskItem
```

Key order in the first prototype block:

`type > abstract > parent > id > categories > name > suffix > description > components`

IDs are PascalCase, keys camelCase, no `prefix.Something` prototype IDs, and new CMU IDs are
additionally prefixed `CMU`. Separate prototypes with one empty line, but never put blank
lines between the `- type:` entries of a `components:` list. `name:` and `description:` go
unquoted unless punctuation forces quoting, then single quotes. No textures in abstract
parent prototypes. Generalized/engine components before highly specialized ones.

CI validates every prototype against its component definitions with `Content.YAMLLinter`;
run it (`dotnet run --project Content.YAMLLinter`) to verify YAML changes instead of trusting
remembered field names.

## Localization

Every player-facing string goes through Fluent under `Resources/Locale/en-US/`; a Russian
downstream translates every string, and hardcoded text breaks it. New CMU IDs
use the `cmu-` prefix and kebab-case. Never treat human-readable text as a stable identifier,
never compare or index by localized strings, and never show identifiers (e.g. `Enum.ToString()`)
to players; user-facing search uses current-culture comparison, not identifier semantics.
Reuse an existing locale file when new entries fit; a new file must represent a meaningful,
cohesive group, not a handful of strings, and CMU zone locale files count as grouping
targets.

A line may not start with `[` (Fluent reads it as a variant key); escape literal leading
brackets as `{"["}bold]...`.

Pass dynamic values as Fluent variables (`("user", name)` tuples), never concatenated
fragments; translators need whole sentences. For entity-dependent grammar use the Fluent
functions (`THE`, `SUBJECT`, `CONJUGATE-BE`, `CAPITALIZE`, ...) so downstream languages can
conjugate correctly. Entity prototypes can localize name and description through the
`ent-{Id}` message with a `.desc` attribute. Indent `.ftl` files with spaces; Fluent treats
tabs literally.

## Simulation boundaries

Simulation: entity behavior, physics, atmospherics, interactions, IC chat, round state.
Out-of-simulation: OOC communication, adminhelp, administrative votes, database access,
Discord/webhook integration. Where the domains exchange information, provide an explicit
boundary instead of mixing them. Test: would this logic stop progressing if the simulation
were paused? If yes, it belongs to the simulation; if it must keep running while paused, it
is an out-of-simulation concern.

## Tests

A test earns its keep by failing when behavior breaks, **not by restating what the code
already says!** Two shapes of useless test show up constantly in PRs - both get the PR
sent back:

- **Mirror tests** assert what the implementation just did (`movementSpeed = 5` plus
  `Assert.That(boot.Speed, Is.EqualTo(5))`). They verify nothing: any change is
  "covered" by changing the assertion to match it.
- **Value lock-ins** encode a tunable (balance number, config default) as an expected
  constant. The next deliberate balance tweak fails the test, and the value now lives
  in two places. Balance values are data, not invariants - they are not test material.

Write tests for behavior that can actually break: regression tests that reproduce a real
bug first (fail, then pass with the fix), edge conditions, and invariants that must
survive refactors. When a deliberate value change breaks a test, the test encoded the
wrong thing - delete it or rewrite it against behavior, don't "fix" it by copying the
new number in.

## Shared placement

Components and events live in Shared when they participate in shared contracts, even when the
server currently owns most of the behavior; the client system must exist even when empty or
the code will not run client-side. `[NetworkedComponent]` belongs on shared components only;
it breaks silently elsewhere.

## Database

Do not modify inherited upstream tables; create a CMU table with a foreign key to the upstream
table instead. Primary-key column order follows the common query pattern; add separate indexes
when queries need additional access paths. Isolating fork schema reduces merge and migration
risk.

## Logging

Gameplay-affecting actions write an admin log:
`_adminLogs.Add(LogType.X, LogImpact.Medium, $"{ToPrettyString(uid)} did something.")`.
EntityUids appear in logs as `ToPrettyString(uid)`, never raw.

## User interface

XAML for layout. Business behavior stays in the UI/BUI system; element code is presentation
and interaction. Follow the existing SS14 UI state and component-state architecture rather
than inventing parallel synchronization. Prefer named style classes over hardcoded colors and
sizes. When component state already networks the data a `BoundUserInterface` shows, read it
directly on the client (`TryComp` plus `AfterAutoHandleStateEvent`) instead of duplicating a
BUI state, and use `SendPredictedMessage` for client input.

## Type layout

Declare classes as one of the intended kinds: `sealed`, `abstract`, `static`, or the engine's
`[Virtual]` where required. Prefer sealed unless inheritance is intentional. Use inheritance
sparingly in simulation code; composition through components and systems is the norm. Keep
types small and single-responsibility. Make illegal states unrepresentable: validate once at
a boundary, then trust the type. Expected failure is data at boundaries (`Try*` patterns);
exceptions signal bugs, not expected conditions.

## Files and formatting

Game code is organized by system: one folder per game system, with `Components` and
`EntitySystems` subfolders only when the file count warrants them. No folder holds a single
file, and no `misc` folders. Prototype files mirror the inheritance tree: one folder per
prototype family, parents in `base.yml`. Files use LF line endings and no BOMs; the repository
`.editorconfig` wins over any conflicting local rule. Generated and map YAML files are never
loaded wholesale; targeted reads only.
