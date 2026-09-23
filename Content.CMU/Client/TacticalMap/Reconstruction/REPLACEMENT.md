# Standard tactical map replacement

## Outcome

The reconstruction becomes the standard tactical map. Existing map tables, command tablets,
personal map actions and embedded tactical displays use the same renderer and published tactical
data. Players do not need to spawn a separate reconstruction table. Support includes every map in
the configured rotation and USS Bush. Implementation remains in content code and resources.

Standard computer and personal-action BUIs now select the reconstruction by default on their existing
UI keys; the classic preference applies to both. Planet drawings synchronize through the existing
faction canvas, and overwatch drawings retain squad scope. Embedded weapon/tunnel/admin displays
remain classic. Full feature parity and representative performance validation remain below.

## Existing entry points and behavior to retain

| Entry point | Integration and behavior |
| --- | --- |
| Tables, consoles, laptops and command tablets | `TacticalMapComputerBui`; retain access, leadership, faction and overwatch squad restrictions. Existing mapped entities gain the replacement through their normal UI integration. |
| Personal map actions and alerts | `TacticalMapUserBui`; retain owner-only replication, permission to draw, faction and squad views, and live versus published update timing. |
| Dropship weapon screens | `DropshipWeaponsBui` embeds `TacticalMapWrapper` on two screens; preserve embedding and terminal functionality while sharing the survey cache. |
| Queen and tunnel interfaces | Personal maps send queen-eye movement; `SelectDestinationTunnelBui` uses blip selection, context information and tunnel paths. Preserve those interactions and their authorization. |
| Administration | `RMCGlobalAdminWindow` embeds `TacticalMapControl`; keep administrative display policy separate from player visibility. |

Retain the existing tactical feed's contact eligibility, faction filtering, sensor visibility,
medical states, vehicle occupants, fireteam indicators and hive-leader indicators. Carry forward
squad objectives, faction and squad drawings/labels, update announcements and cooldowns. Center on
the player, adjustable icon size, and saved window/view settings also need equivalents.

The revised interaction design remains: drag to pan, middle-drag to orbit, Top down and Reset,
low walls initially enabled, colored freehand strokes with adjustable width, and text pins. Draft
additions and removals remain local until Send is accepted. Drawing does not perform route-clearance
checks. Existing label editing, moving and deletion need equivalents in that draft workflow.

## Implementation sequence

1. **Resolve maps independently of Z networks.** Introduce a shared description of the selected
   battlefield or authorized ship, its floors and their grids. A single-level map is one floor;
   it must not require creating a gameplay Z network. Resolve separate stationary grids into the
   same coordinate space for extraction, contact projection and picking. Use this description for
   validation, caching and requests so the server and client agree on the selected map.
2. **Support ship command use.** Opening on the planet starts on the planet. Opening aboard ship
   starts on the ship, unless an earlier explicit ship-side selection chose Planet. Preserve that
   ship-side preference across openings and client restarts. Provide Planet / Ship selection and
   optional center-on-player behavior, using the actor's actual position and floor. Never project
   the player's coordinates onto a different map. Key surveys and annotations to that scope and
   discard stale replies when it changes. Tracking must query the selected scope rather than always querying the planet and
   subsequently discarding ship contacts.
3. **Unify tactical publication.** Extend the standard tactical publication flow to carry floor,
   fractional drawing coordinates, width and text. Retain faction/squad ownership, drawing
   permissions, acknowledgements, announce cooldowns and recipient update rules. Published edits
   must reach both consoles and authorized personal viewers. Replace the prototype-only order
   dictionary rather than maintaining two independent sets of tactical drawings.
4. **Reuse the rendering control across entry points.** Adapt the standard computer and personal
   BUIs, then embedded displays, to the same scene subscription and render control. Keep input,
   tracking and annotation adapters separate from volume rendering so specialized interactions
   remain available. Consolidate common map data and surface caching without sharing camera or
   unsent draft state between viewers.
5. **Verify parity and switch the defaults.** Run the map compatibility and gameplay checks below,
   then route existing entities and actions through the replacement. Retire the development-only
   table requirement and obsolete UI/storage paths after their callers have migrated. A low-cost
   top-down presentation should remain available through the same tactical data and permissions.

## Known compatibility work

The reconstruction now includes single-level map-as-grid resolution, location-aware opening,
remembered ship-side selection, centering and a CMU-tab classic-interface preference. The preference
is handled by both the reconstruction and standard entry points. Classic remains a user-selectable
renderer sharing the published planet drawings. Unsupported survey layouts fall back to classic.

The repository audit found six rotation maps with no configured Z network: Hybrisa Metropolitan,
Bosenmori Basho, Fiorina, Flight, LV624 and Solaris Gulch. The new single-level path covers that
layout; per-map runtime validation is still outstanding. Hope's Retreat's upper
level at depth +3 stores its grid separately from the map entity and also needs that resolver.

USS Bush Redux has five configured levels; the legacy Bush has three. Both use map-as-grid decks
and fit the current eight-level and 1024-by-1024 limits. This confirms layout compatibility, not
in-game verification. Other inspected rotation layouts fit those limits. Detect future maps that
exceed the limits with an actionable error rather than an indefinite loading message.

The development survey currently streams live unseen structures. During migration, distinguish
base-map geometry from live changes and preserve the intended tactical intelligence rules; reusing
filtered contact icons alone does not settle structural visibility or update timing.

## Completion checks

- Every in-rotation map and both Bush definitions resolve their intended floors and stationary
  grids, including single-level maps and Hope's Retreat's separate upper grid.
- Standard mapped tables, tablets, personal actions, embedded dropship views and specialized xeno
  interactions open and operate without spawning a reconstruction-table prototype.
- A permitted officer's Send reaches authorized console and personal viewers; other factions and
  squads do not receive private annotations. Rejection retains the draft. Edits preserve floor,
  width, text and fractional coordinates, including label move/edit/delete operations.
- Contact and update visibility match the existing tactical rules. Verify battlefield and ship
  selection, medical state, vehicle occupants, sensors, objectives and faction changes.
- Close/reopen, reconnect, map switch and round cleanup cancel or reject stale work and release
  resources. Multiple viewers share survey work without sharing unpublished edits.
- Measure cold load, warm reopen, client frame time, server extraction time, transfer size and
  memory on representative dense maps and simultaneous viewers. Existing automated checks are
  correctness checks, not performance measurements or proof of visual quality on every map.

## Source references

Paths below are relative to the repository root.

- `Content.Client/_RMC14/TacticalMap/`: standard computer/user BUIs, wrapper, settings and controls.
- `Content.Shared/_RMC14/TacticalMap/`: permissions, tracked data and publication contracts.
- `Content.Server/_RMC14/TacticalMap/TacticalMapSystem.cs`: authoritative tactical behavior.
- `Content.Client/_RMC14/Dropship/Weapon/DropshipWeaponsBui.cs`: embedded tactical displays.
- `Content.Client/_RMC14/Xenonids/Construction/Tunnel/SelectDestinationTunnelBui.cs`: tunnel interactions.
- `Content.Client/_RMC14/Admin/Global/RMCGlobalAdminWindow.xaml`: administrative display.
- `Content.CMU/Server/TacticalMap/Reconstruction/`: current survey, contacts and prototype publication.
- `Content.CMU/Resources/Prototypes/CMU14/Maps/`: rotation map and Bush definitions.
- `Content.CMU/Resources/Prototypes/CMU14/Entities/Structures/Machines/tactical_reconstruction.yml`: development tables.
