# CMU Conventions

Technical conventions for CMU code, resources, and tests. Agent workflow
and submission policy belong in the repository’s agent instructions.

Use the current checkout’s project imports and engine contracts.
`.editorconfig` governs formatting. Follow these conventions and nearby
working examples; use RMC and Wizden guidance to fill remaining gaps.

## Repository layout

- `Content.CMU/Shared/`: CMU gameplay types and logic needed by both sides.
- `Content.CMU/Server/`: authoritative server behavior and server services.
- `Content.CMU/Client/`: UI, presentation, and client-specific behavior.
- `Content.CMU/Resources/`: CMU prototypes, localization, maps, and assets.
- `Content.Tests/`: NUnit unit tests.
- `Content.IntegrationTests/`: NUnit integration and client/server tests.

The CMU code trees are imported by the existing content projects.
There is no separate `Content.CMU.csproj`.

Extend existing `_CMU14`, `_AU14`, and `_RMC14` features where they live.
Use the branch’s existing CMU layout for new CMU features. Organize files
by feature; add subfolders when they improve navigation.

Treat `RobustToolbox/`, its submodules, and `RSI.NET/` as external
dependencies. Changes require authorization covering that dependency.

## Naming and C#

- New CMU prototype IDs use `CMU` plus PascalCase; localization IDs use
  `cmu-` plus kebab-case. Preserve established inherited identifiers.
- Use file-scoped namespaces. Follow `.editorconfig` for `var`, braces,
  expression bodies, indentation, and line endings.
- Wrap long signatures and expressions for readability; wrapped boolean
  expressions use leading `&&` and `||`.
- Prefer sealed classes. Use abstract classes or `[Virtual]` when the
  design requires inheritance.
- Prefix a shared type with `Shared` when it has corresponding client or
  server specializations.
- Give public APIs and data fields useful XML documentation where their
  purpose or contract needs explanation.
- Comments explain constraints and decisions. Avoid restyling unrelated
  code.

## Fork extensions and markers

Prefer domain events, partials, and existing extension points for CMU
behavior. Partial extensions retain the original namespace; suppress
namespace checks only where needed.

Use system inheritance when the engine’s extension pattern requires it.
Check derived-system registration and inherited subscriptions before
adding an override. Preserve required base behavior.

Mark deliberate changes outside CMU-owned zones using `CMU14`.
Divergence, removal, and merge policy lives in the agent instructions.

For individual field, using, or expression changes, tag inline:

```csharp
[DataField]
public float Capacity = 10; // CMU14: colony storage capacity
```

New constructs use a marker such as `// CMU14 method`,
`// CMU14 class`, or `// CMU14 event`. Larger contiguous changes may use:

```csharp
// CMU14 Storage Begin: colony storage rules
...
// CMU14 End
```

YAML uses `# CMU14`; a whole prototype can mark its `type:` or `id:` line.
Preserve existing upstream markers and identify new CMU divergence as CMU.

## Components and data

Components store data; systems own behavior. Avoid behavior in property
setters and component inheritance used merely to split client/server data.

- Use `partial`, `[RegisterComponent]`, and the appropriate access,
  serialization, and networking attributes.
- Prefer public fields with `[Access]` restrictions where ownership matters.
- Use bare `[DataField]` when the field name naturally maps to the YAML key.
- Store tunables in component fields, prototypes, or CVars.
- Keep shared contracts in Shared. Side-specific implementation details
  may remain on their respective side.
- Follow established data-definition and serializer patterns.
  Serializers must not resolve services through `IoCManager.Resolve`;
  serialization may run off-thread.

## Entity systems and events

Group dependencies near the top, sorted alphabetically. Use concise field
names without a redundant `System` suffix. Use inherited entity-manager
access and helpers such as `TryComp`, `HasComp`, and `Transform`.

Prefer events and targeted update sets over scanning large populations
each tick. Cache queries or reuse result collections when repeated work
justifies it.

Public system methods take entity arguments first and resolve optional
components near entry. Expose behavior through system methods rather than
extension methods on entities, components, or systems.

For events:

- Prefer `[ByRefEvent]` record structs, raised by reference.
- Use readonly events for notifications and mutable fields such as
  `Cancelled` or `Handled` when handlers must affect the result.
- Preserve class-based contracts where inheritance or networking requires
  them.
- Use the `Event` suffix, `On...` handlers, and established `Entity<T>`
  subscription overloads.
- Prefer directed events for entity-local behavior. Use ordering
  constraints only when behavior requires them.
- Keep asynchronous service work outside simulation mutation; use existing
  event and DoAfter patterns for simulation actions.
- Unsubscribe ordinary C# events when their subscriber’s lifetime ends.

Before adding a component/event subscription, search shared, server,
client, base classes, and subscription helpers for the same pair.
Duplicate directed subscriptions can fail during system initialization.

Lifecycle and state hooks have one owner. This includes `ComponentStartup`,
`ComponentRemove`, `ComponentHandleState`, `ComponentGetState`, and
`AfterAutoHandleStateEvent`. Other systems should use the owner’s public
API or a domain event with an appropriate distinct subscription.

## Networking and prediction

Shared placement makes code available on both sides; prediction also
requires compatible state, dependencies, and execution paths.

A concrete shared system can run on both sides without empty client/server
subclasses. Add specializations when the shared base is abstract or
side-specific behavior requires them.

- Use `EntityUid` for local entity references and `EntityUid?` for optional
  references. Convert to `NetEntity` at network boundaries using the
  established message or component-state helpers.
- Handle unresolved and deleted references according to the data contract.
- Use `[NetworkedComponent]`, `[AutoGenerateComponentState]`, and
  `[AutoNetworkedField]` where synchronization is required.
- Dirty changed networked state. Avoid dirtying unchanged values.
- Consider field deltas when fields change independently; use the
  corresponding `DirtyField` pattern.
- Follow prediction cloning contracts for mutable reference data.
- Use predicted audio, popup, spawn, and deletion helpers in predicted
  actions, supplying the required user context.
- Keep prediction deterministic. Use established predicted randomness
  where appropriate; keep secret or exploitable outcomes server-side.
- Distinguish state application from gameplay actions. Guard side effects
  during `_timing.ApplyingState` where the event contract requires it.
  Do not hide state divergence with `IsFirstTimePredicted`.

Respect PVS and component visibility. Client logic cannot assume every
server entity or component is available. Use owner/session restrictions
for private state; add PVS overrides only when the feature requires them.

## Time, coordinates, and performance

Use `TimeSpan` for durations and simulation `CurTime` for gameplay
deadlines. Follow existing `[AutoPausedField]` and `TimeOffsetSerializer`
patterns for absolute timestamps affected by pausing and serialization.

Choose whether periodic work catches up or skips missed intervals.
Advance from the previous deadline when preserving the schedule matters.

`EntityCoordinates` are parent-relative. Use transform-system conversions
for world-space calculations and system methods for anchoring or
reparenting. Handle off-grid entities where the feature supports them.

Avoid unnecessary allocations, LINQ, captures, and repeated lookups in hot
paths. Profile material performance changes. For continuously changing
state, consider synchronizing a timestamp and rate instead of dirtying
every tick when the client can reconstruct the value correctly.

## Content sandbox

A successful compile does not establish sandbox compatibility. Check
unfamiliar external APIs and overloads against:

`RobustToolbox/Robust.Shared/ContentPack/Sandbox.yml`

Restrictions include `System.FormatException`, native interop, and
explicit-layout types with fields. Choose permitted APIs that match the
failure contract; do not substitute exception types mechanically.

## Prototypes and resources

Prototype instances are shared definitions. Never mutate them as runtime
state. Resolve definitions through the prototype manager and store mutable
values on entities or components.

Use validated `ProtoId<T>` references. For fixed C# references, prefer
`static readonly ProtoId<T>`; use data fields when configurable.

Prefer components on entity prototypes over introducing new prototype
kinds. Reuse existing entity tables for fills/spawners and construction
graphs for recipes where those systems fit.

Use `SoundSpecifier` and `SpriteSpecifier` where supported. Prefer sound
collections when variants belong together. Preserve asset licenses and
attribution.

Keep RSI `meta.json` readable, with four-space indentation and the order
`version`, `license`, `copyright`, `size`, `states`.

## YAML

Use the leading prototype field order:

`type`, `abstract`, `parent`, `id`, `categories`, `name`, `suffix`,
`description`, `components`.

```yaml
- type: entity
  parent: CMUBaseMachine
  id: CMUStorageMachine
  components:
  - type: Sprite
  - type: Tag
    tags:
    - CMUStorage
```

This example illustrates formatting; verify every referenced ID and field
against the checkout.

- List entries align with their key’s indentation.
- Use PascalCase IDs and component names, camelCase keys, and no dotted
  prototype IDs.
- Separate prototypes with one blank line; keep component entries together.
- Use inline lists for short sets and block lists for longer data.
- Use `suffix` for spawn-menu disambiguation.
- Order general components before feature-specific components where useful.
- Keep abstract parents free of concrete textures unless inheritance
  behavior requires them.
- Follow nearby quoting conventions while preserving YAML scalar types.

Validate prototype changes with the repository’s YAML linter.

## Localization

Localize new player-facing text through Fluent. New CMU entries belong in
`Content.CMU/Resources/Locale/en-US/`, or the feature’s existing CMU locale
area. Reuse cohesive files.

- Use `cmu-` IDs in kebab-case.
- Pass complete messages and dynamic variables; avoid concatenated
  sentence fragments.
- Use entity grammar functions where appropriate.
- Entity names and descriptions may use `ent-{Id}` and `.desc`.
- Keep identifiers separate from display text. Never compare localized
  strings as identifiers or show raw enum names as player-facing labels.
- Use current-culture comparison for human-language search.
- Indent with spaces. Escape a literal leading `[` as `{"["}`.

## UI and appearance

Use XAML for layout where practical. Keep `.xaml.cs` focused on
presentation and interaction; business behavior belongs in BUI,
controllers, or systems. Prefer named styles over repeated literal values.

Read existing component state when it already supplies the UI’s data.
Check state-hook ownership before adding refresh subscriptions; prefer
the owner’s domain event. Respect entity and UI lifetimes.

Use predicted BUI messages when the action supports prediction. Validate
client requests on the authoritative side.

The server sets appearance data; client visualizers update sprite layers.
Use network-serializable enum keys and named layer mappings. Prefer
`GenericVisualizer` when its mappings express the behavior.

## Services, database, and logging

Keep simulation behavior separate from database, account, webhook, and
other services that must operate independently of simulation time.

For new CMU persistence, prefer CMU tables linked to inherited tables by
foreign key. Choose composite key order and indexes from query patterns.

Add admin logs for relevant auditable gameplay actions using existing
log types and impact conventions. Format entity references with
`ToPrettyString`.

## Verification

Use the SDK selected by `global.json` and run commands from the repository
root. The solution is `SpaceStation14.slnx`.

Build affected content projects; these are alternatives:

```powershell
dotnet build Content.Shared/Content.Shared.csproj --no-restore
dotnet build Content.Server/Content.Server.csproj --no-restore
dotnet build Content.Client/Content.Client.csproj --no-restore
```

Use `--no-restore` only with current restored dependencies. Restore first
when assets are missing or package inputs changed.

Run focused NUnit tests in the appropriate test project:

```powershell
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~ActualFixtureName'
```

Replace the placeholder with a real fixture and confirm tests executed.
Use `Content.Tests/Content.Tests.csproj` for unit tests. Use `--no-build`
only when assemblies reflect current sources.

For prototype changes:

```powershell
dotnet run --project Content.YAMLLinter/Content.YAMLLinter.csproj
```

Do not run builds, tests, or the linter concurrently against shared
checkout outputs.

Tests should detect regressions and enforce behavior or stable contracts.
Avoid duplicating incidental balance values. Exact-value assertions are
appropriate when the value itself is required behavior. Change or remove
expectations only with evidence that the contract changed.

Verify multiplayer and prediction behavior when relevant, including
latency and repeated side effects. Documentation-only changes need format
and reference checks, not a game build.

Stop after sufficient checks pass unless new changes, failures, or
unresolved concerns justify more verification. Report commands, results,
and material unverified behavior accurately.
