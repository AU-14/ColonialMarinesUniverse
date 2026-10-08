# Expedition infantry: tactical design and research

Scope: the opt-in CMU scavenger controller, on expedition and ordinary maps. Decisions remain server-side;
native steering, firearms, physics, factions and medical do-afters execute actions.

## Sources and adaptations

- [Jeff Orkin, Three States and a Plan: The A.I. of F.E.A.R., GDC 2006](https://www.gamedevs.org/uploads/three-states-plan-ai-of-fear.pdf).
  The useful architectural ideas are shared working memory, action preconditions, recovery
  after failed actions, and separating individual survival from squad coordination.
  Our controller keeps remembered contacts, reserved cover/peek positions and a short
  failed-destination memory. Squad attack slots never override injury, suppression or
  player possession. This is a small action controller, not a complete GOAP implementation.

- [Arjen Beij and Remco Straatman, Killzone: Dynamic Procedural Tactics, GDCE 2005](https://www.guerrilla-games.com/media/News/Files/gdce05_killzone_ai.pdf).
  Position evaluation combines range, exposure and movement cost. For our generated maps,
  candidates must pass dry-ground, fire, leash and live collision checks before scoring.
  Shelters must conceal the guard's width; peeks must clear the body, nearby muzzle corridor
  and direct shot. Distant foliage beside the aim point can catch stray rounds.
  Scores favor short step-outs, useful range and protection from secondary observed threats.
  Bounded local A* assigns exposure costs, then native steering follows the chosen waypoints.

- [Microsoft, Halo 2 AI Behavior List](https://learn.microsoft.com/en-us/halo-master-chief-collection/h2/ai/aibehaviorlist).
  Its documented cover-peek and self-preservation behaviors inform immediate withdrawal
  under pressure and separate watch/search phases after losing contact.
  Our guards suppress their own exposure after perceived hostile shots pass near their
  position, remember a last-seen location briefly, and reassess before leaving shelter.

## Current behavior

1. Observe at 150 ms intervals and store a last-seen coordinate for six seconds. Compare
   usable shots at the four nearest visible targets plus the current one, retaining the
   current target during a viable volley. Never track an unseen target's current position.
2. Keep rifles shouldered during combat movement; lower them for actual utility work.
   Initial aim takes 180 ms and peek aim 80 ms, in addition to native weapon readiness.
   Volleys consume real rounds at the weapon's native rate; cover searches wait until the
   volley ends. A first shot starts the full burst window. Recheck geometry and the next
   shot's recoil cone for allied bodies before every shot. A crossing ally pauses fire;
   a persistently blocked lane triggers a deliberate sidestep or withdrawal.
3. Search at most 256 local cells, with an eight-step search radius. Pair an occluded
   shelter with a firing position no more than 3.2 metres away along a clear passage.
4. Move precisely into the firing position, aim briefly, fire the variant's limited volley, return
   to shelter, and reassess. Nearby squadmates reserve different positions and stagger
   peeks with local attack slots.
5. Hits or visible hostile fire passing within 1.5 metres interrupt exposure. Pressure
   delays the next peek; uncovered guards seek a safe refuge when one is reachable.
6. Wounded guards use their physical three-dose dressing pack while sheltered. They free
   a hand and complete a three-second native medical action. Damage, movement, lost
   safety, incapacitation or player possession cancels treatment.
7. A failed/timed-out movement destination is avoided for eight seconds. A combat move
   with no 20 cm progress for 1.5 seconds fails early. Pursuit route failures back off for
   one second. Movement checks use body collision rather than bullet-only obstruction;
   clear dry route segments skip intermediate tile stops. Exposure penalties are capped
   so overlapping enemy lanes do not multiply into prohibitive detours.
   Destroyed cover, changed threat angles and expired contacts invalidate the current plan.

The distances and timers above are tuning choices for this game, not values claimed by
the cited papers. The aim is readable, adaptable opposition with ordinary ammunition and
medical limits.

## Planning, squads, and experience

A bounded GOAP search (128 states) chooses from executable cover, reload, treatment, rescue,
smoke, grenade, flank and attack actions. Each action checks its preconditions again when it
starts and fails on obstruction, interruption or timeout. Movement uses a 256-node local A*
search with danger costs and real collision checks. Physical magazines, dressings and grenades
are finite inventory items. A native pulling joint drags critical squadmates into shelter.

Equipped squad headsets share a frozen observation after a short delay, within 40 metres.
Reports expire and do not reveal an unseen target's current position. Squads have separate
position reservations, staggered attack slots and one flanker at a time. Aggressive, steady
and cautious dispositions respond to pressure, wounds and nearby support.

Grenades are considered on initial contact with multiple enemies, or as a last resort after
repeated failed exposures or severe pressure. Reservations cap a squad decision at two
throwers in a two-second window; the squad then waits 35 seconds. Smoke for casualty recovery
shares that budget. Throw preparation rechecks the friendly blast area and only primes a
grenade after a successful physical throw.

Bounded aggregate exposure/flank outcomes are persisted by biome and disposition (or the
`Ordinary` environment for maps without expedition metadata) to
`/cmu-expedition-experience.json` in server user data. They adjust next-round costs within
0.75–1.25. This is modest outcome adaptation, not neural training or player-specific profiling.

## Operator controls

`cmu-expedition-ai <map|here> [1..12] [mixed|regular|poor|rich|scout]` creates a new squad
and prints its ID. `here` works from a body or observer over ground on ordinary maps too.
Numeric expedition IDs select the recovery objective. Variants have distinct finite gear,
armor and combat tuning; see [the command and variant guide](README.md#integration-boundary).
`cmu-expedition-orders <map> <squad> move <x> <y>` moves it to spread positions.
`cmu-expedition-orders here <squad> move` uses the administrator's current position.
Replace `move` with `guard` to establish a guard area and entrench after 20 quiet seconds.
Guards use their real shovel to dig and build a mound, or nearby metal to build a native
barricade. Construction stops on contact, injury, possession or a new order. One completed
fortification per guard order avoids filling every nearby tile indefinitely.

Add 2-8 locations with `patrol-add` in place of `move`, then issue `patrol-start` without
coordinates. `patrol-stop` holds the current area; `patrol-clear` also removes the points.
Combat interrupts travel and the patrol resumes after contact expires. `move`/`guard` replace
the active patrol. Explicit orders follow bounded, dry routes (2,048 cells, at most one search
per update), rechecking live obstruction and retrying blocked travel after three seconds.
`cmu-expedition-ai-status <map>` displays progress and blocked orders. Long or maze-like routes
may need intermediate waypoints; separate grids, levels and closed doors are not traversed.

Ground checks use current grid tiles, RMC water/fire entities and body collision. Generated
terrain adds water/cliff bounds, but ordinary maps need no expedition component. Local cover,
flanking and rescue use the same ground checks, including grids with negative tile coordinates.

Use `style Aggressive`, `style Steady` or `style Cautious` to tune a squad. `target GOVFOR,OPFOR`
sets explicit target factions; `friendly GOVFOR` protects that faction. `default` restores
native faction targeting or removes the friendly overrides. Friendly overrides take priority;
these commands never change the server's global faction relations.

## Verification

```text
dotnet test Content.Tests/Content.Tests.csproj --no-restore --filter FullyQualifiedName~CMUTacticalPlannerTest
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~CMUExpedition
```

Fixtures exercise real weapon fire, lane safety, corner peeks, treatment interruption,
magazine exhaustion, radio snapshots, physical casualty pulling, grenade preparation,
six-guard squads, guard construction, generated terrain and moving connected player bodies.
The six-guard fixture reports candidate-search timing and completed physical flanks.
These are engine simulations; final combat balance still needs human multiplayer playtesting.

The responsiveness, squad variants, patrols and ordinary-map support in the follow-up were
compiled without running tests, as requested. Previous fixture results do not validate these
changes. In-game verification should include multiple squads in dense vegetation, peeks beside
walls, each loadout's ammunition/reload behavior, and interrupted/resumed patrols on both an
ordinary colony grid and an expedition. Verify blocked waypoints, water/fire avoidance,
incapacitation and player possession, and record search cost with simultaneous contacts.
