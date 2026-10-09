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
  candidates must pass traversable-ground, fire, leash and live collision checks before scoring.
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
   usable shots at the four nearest visible targets, the current one and eight directional representatives, retaining the
   current target during a viable volley. Never track an unseen target's current position.
   Retain visible targets through committed movement and utility work, with a 1.5-second
   minimum between ordinary target switches. Brief sight loss holds the stance for 350 ms
   without firing; movement destinations survive contact changes. Healing and reloads are
   not cancelled merely because a different enemy becomes the preferred target.
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
   Stop a peek at usable geometry even when a teammate temporarily blocks firing. Stops
   inside valid shelter tolerate 55 cm of endpoint error, avoiding needless tiny corrections.
   Coverless recovery resumes aim only with a visible target and no active utility action.
5. Hits or visible hostile fire passing within 1.5 metres interrupt exposure. Pressure
   delays the next peek; uncovered guards seek a safe refuge when one is reachable.
6. Wounded guards use their physical three-dose dressing pack while sheltered. They free
   a hand and complete a three-second native medical action. Damage, movement, lost
   safety, incapacitation or player possession cancels treatment.
7. A failed/timed-out movement destination is avoided for eight seconds. A combat move
   with no new 10 cm closest approach to its current waypoint for 1.5 seconds fails early. Pursuit route failures back off for
   one second. Movement checks use body collision rather than bullet-only obstruction;
   clear traversable route segments skip intermediate tile stops. Exposure penalties are capped
   so overlapping enemy lanes do not multiply into prohibitive detours.
   Destroyed cover, changed threat angles and expired contacts invalidate the current plan.

Ordered travel and tactical manoeuvres use the RMC human's 35 cm circular footprint and a
continuous swept corridor, including furniture and barricade collision layers. A* edges use
the actual body radius and smoothed segments prefer 40 cm clearance. Narrow passages retain
individual cell stops; endpoint connections can use the actual radius beside an obstacle.
Endpoint tile centres remain available as
waypoints, allowing an off-centre body to align before joining the route. Within the usual
25 cm arrival band, a corner is only dequeued when the next segment is clear from the actual
body position. Otherwise steering approaches within 5 cm of the corner. Validated segments
use native local avoidance without a second navmesh path overriding the selected waypoints.
Only a new closest approach resets stall timing; sideways wall jitter does not. Interrupted
ordered segments retry after 0.5 seconds, while failed searches retain a three-second backoff.
Route budgets remain 256 cells for tactics and 2048 for orders, with one ordered search per frame.

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
Reports retain their original reception deadline when further reports arrive, so a busy
channel cannot keep delaying the reaction. Accepted snapshots expire after twelve seconds
and do not reveal an unseen target's current position. Recent visual contact and active
survival/utility actions take precedence. Recipients acknowledge a new support response
(at most once per twelve seconds), then approach the reported area in at most eight-metre
steps with separate destinations. This keeps each step inside the local search bound even
when the report came from farther away. Responders remain within their guard leash and
wait near the reported location if they find no enemy; expired reports release the response.
Pursuit destinations are retained until meaningful contact movement or arrival, with a
one-second replanning interval and a wider stop band at rifle range.

Squads have separate
position reservations, staggered attack slots and one flanker at a time. Aggressive, steady
and cautious dispositions respond to pressure, wounds and nearby support.
Guards fighting different opponents within the same eight-metre contact area count as
supporting one another for covering fire and attack slots.

Grenades are considered on initial contact with multiple enemies, or as a last resort after
repeated failed exposures or severe pressure. Reservations cap a squad decision at two
throwers in a two-second window; the squad then waits 35 seconds. Smoke for withdrawals and casualty recovery
shares that budget. Throw preparation rechecks the friendly blast area and only primes a
grenade after a successful physical throw.

Bounded aggregate exposure/flank outcomes are persisted by biome and disposition (or the
`Ordinary` environment for maps without expedition metadata) to
`/cmu-expedition-experience.json` in server user data. They adjust next-round costs within
0.75–1.25. This is modest outcome adaptation, not neural training or player-specific profiling.

## Operator controls

`cmu-expedition-ai <map|here> [1..12] [mixed|regular|poor|rich|scout|assault|support|marksman|rocketeer|specialists]` creates a new squad
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

Ground checks use current grid tiles, fire entities and hard body collision. Shallow/deep RMC
water is traversable with native contact slowdown; sensor fixtures do not block body clearance.
Generated terrain adds cliff and map bounds, but ordinary maps need no expedition component. Local cover,
flanking and rescue use the same ground checks, including grids with negative tile coordinates.

Use `style Aggressive`, `style Steady` or `style Cautious` to tune a squad. `target GOVFOR,OPFOR`
sets explicit target factions; `friendly GOVFOR` protects that faction. `default` restores
native faction targeting or removes the friendly overrides. Friendly overrides take priority;
these commands never change the server's global faction relations.

`cmu-expedition-ai-status here` also shows radio reports received/accepted, the latest
decision and the current approach point. `maintaining-current-action`, `outside-guard-area`,
`stale-report`, `support-route-blocked` and `watching-reported-area` explain why hearing a
callout may not result in immediate movement. Acknowledgements do not broadcast contacts.

## Disarms, rushes and sustained pressure

Each guard remembers the rifle it physically held. After native stun/knockdown and stand-up
finish, it selects a rifle still in either hand or picks its dropped rifle up through normal
hands and interaction checks. Recovery approaches are limited to six metres, visible loose
weapons and bounded traversable routes. Held, stored, hidden, deleted or anchored weapons are not
retrieved. A close rush interrupts a distant retrieval, but permits picking up a reachable
rifle. Failed pickups/routes have retry delays; ammunition is never replaced by recovery.

Visible xenos (including neomorphs) and unarmed melee opponents get an urgent priority inside
the six-metre standoff, including their projected approach over 0.65 seconds. This priority
can break an ordinary target lock or utility action. Up to fourteen short escape corridors
are scored against the closest six visible melee threats, terrain, hazards and squad spacing.
Committed escape steps continue while the rifle fires through the normal aim, ammo, fire-rate,
wield and friendly-fire checks. There is no speed boost or guaranteed escape from faster aliens.
If trapped, the guard returns fire. After sight loss it briefly holds the approach and avoids
walking inside the remembered melee standoff; it does not track an unseen body's movement.

Cover anchors are rechecked against all known threats before use and at volley completion.
A hit while waiting at an anchor invalidates that location even when geometry reports it
sheltered. Invalid cover is avoided for eight seconds. Exposed guards can fire regardless of
squad exposure slots. Ordinary suppression allows at least two return shots before cutting
a volley short, and repeated suppression cannot extend the shelter pause indefinitely.
Critical injury still permits immediate withdrawal. If no real shelter exists, an armed
guard keeps fighting rather than travelling home to wait in the open. Treatment/reload
safety also requires a brief break in actual damage.

Cover/peek reservations and physical squad spacing prevent multiple guards selecting nearly
the same stance. A crowded exposed pair yields one guard between volleys, at most once every
three seconds. Escape destinations are reserved too; physical bodies in other squads are
avoided without sharing their future-position reservations.

Idle fortification has an explicit preparation state. Retry timers no longer lower rifles,
and tools are only prepared when usable ground or metal is present. Newly spawned squads
wait twenty seconds before attempting construction. The survival line in
`cmu-expedition-ai-status here` reports weapon recovery, escape decisions, invalidated cover
and preparation/work state.

## Multiple attackers

Target selection includes the nearest four enemies, the current target and representatives
from eight attack directions. A recent visible shooter gets higher priority; other squadmates'
fresh firing commitments discourage piling onto one ordinary target. Existing volleys and
target locks remain stable, and an imminent melee rusher takes priority over target sharing.
Target ranking reuses one nearby-entity lookup for its friendly-fire checks; actual shots
still evaluate live positions and firing lanes.

Position scores use eight weighted attack directions, including nearby enemy concentration
and recent gunfire. Scores are cached within one observation cycle. Shelter and treatment
eligibility continue to check every visible opponent and the remembered primary contact.
An escape from a melee threat also considers exposure to ranged attackers.

After returning fire, a guard under attacks from directions more than sixty degrees apart
can seek a better firing position. At most sixteen short corridors are considered per search,
with a three-second cooldown. The destination must reduce exposure, preserve a firing lane,
avoid melee range and have a traversable, unobstructed approach without higher midpoint exposure.
Nearby engaged squadmates retain covering fire while others move. If no useful position
exists the guard continues fighting. Partial protection never authorizes treatment or a
hidden-cover pause. Longer optional flanks are deferred during crossfire.

Repeated bullets from known shooters update suppression without forcing the complete
decision/search loop to run for every shot. A newly observed shooter can still wake it
immediately. Recent shooters are capped at sixteen and expire after two seconds. These
work bounds have been reviewed in source; runtime performance has not been measured.

`cmu-expedition-ai-status here` adds visible enemy/direction counts, recent shooters,
crossfire status, completed move decisions and squadmates assigned to the current target.

A currently visible shooter firing from more than sixty degrees off the remembered target
bearing can interrupt the ordinary lock. This requires a usable shot from the actual body
position, never an imagined peek, and has a two-second response cooldown. It cancels obsolete
optional repositioning, allows return fire before another plan and preserves active medical
or reload interruption rules. Imminent melee still takes precedence.

## Mobile fire and reaction exceptions

Movement and firing have separate executors. Repositions, peeks, withdrawals, retreats,
investigation and cover/flank plan movement can fire at visible targets without unregistering
steering or waiting for arrival. Opening contact preserves a patrol/investigation's current
movement for up to 750 ms while responding. Moving volleys have their own ammunition counters;
they do not finish a movement action or overwrite its destination.

A newly acquired target, incoming fire or imminent melee bypasses the extra AI aim pause.
Exposed guards under that pressure can resume stationary fire without waiting out an artificial
burst pause; mobile volleys use a 100 ms pressure pause. Native wield delays, recoil, gun rate,
ammunition, visibility and friendly-fire checks remain mandatory. An unfired volley can wait
up to three seconds for native readiness without cycling through another aim/recovery pause.
These exceptions never fire through a reload, grenade preparation, treatment or weapon switch.

## Equipment and ordnance

Guards select from guns physically held, slung in suit storage or carried in their field pack.
Selection considers range, ammunition, role and a preference for keeping the current weapon.
A 250 ms hand transition frees the native wielding hand, then normal hands/inventory APIs
perform the swap. Successful swaps have a two-second commitment; failures back off three seconds.
A one-handed backup can be used while retaining the primary in the other hand if stowing is
blocked. In safety, an empty preferred firearm with compatible spare magazines can be selected
for the existing physical reload action. Disarm recovery takes precedence over optional swaps.

Assault troops use M63 SMGs, support gunners use M41AE2 heavy pulse rifles, marksmen use M4SPR
rifles, and rich scavengers use the modern M41A/2. All except poor scrappers carry backup pistols.
Rocketeers carry an SMG, pistol and one HE-loaded RPG-36. The AI uses a conservative
4.5–6 m window within the rocket's native range. It discards the spent tube and selects a firearm again.
There is no rocket refill. Selection reserves a squad launcher; successful shots impose a
20-second squad rocket cooldown. Clustered enemies or repeatedly punished peeks justify its use.
Close melee rushes favor the ready firearm. Each shot checks splash safety, nearer bodies that
could intercept the rocket, the forward corridor, and the native two-tile cardinal backblast area.

Blast grenades score clusters around at most eight visible directional contacts, with a short
observed-velocity lead. Lone fast targets do not justify an opening grenade; severe pressure
or repeated failed peeks permit a last-resort throw. Live contact and blast safety are checked
again on release. Predicted friendly travel checks the entire swept segment over the fuse,
including known cover/escape destinations. The two-throw/35-second squad discipline remains.

Smoke can screen a threatened reload, withdrawal, crossfire or casualty recovery. Placement
is between the protected position and the enemy, and nearby queued/previous screens prevent
duplicates. Real anchored opaque smoke clouds block the guards' vision beyond point-blank
range; a planned throw does not fabricate concealment. Smoke is not hard cover and does not
authorize exposed medical work. A nearby cloud can conceal an enemy from the throwing squad too.

RMC water keeps native speed penalties. Slow combat movement earns deadline extensions only
when the body makes progress; stall checks remain. Short escape strides scale down with current
movement speed. Cliffs, hard river boundaries, space, fire and grenade hazards stay blocked.

Status output includes flank response count, weapon decisions/switches, rockets, smoke decisions
and shots fired during ordinary travel. These diagnostics support in-game verification below.

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
ordinary colony grid and an expedition. Verify blocked waypoints, water crossing and fire avoidance,
incapacitation and player possession, and record search cost with simultaneous contacts.

The survival/suppression follow-up is also build-only; no tests or in-game checks were run.
Verify disarm and knockdown with the rifle underfoot and several tiles away, a stolen/stored
rifle, missing hands, and a revived guard. Rush a squad with moving xenos/neomorphs around trees
and corners, then retreat out of sight. Check fire during escape, no blind shots, no pursuit
into the remembered melee gap, water crossing, fire avoidance and behavior when no escape exists.
Keep firing while advancing on a squad: guards must return shots between withdrawals and
reject an exposed or penetrable shelter instead of repeatedly waiting there. Check crowded
groups and friendly-fire lanes. Spawn `cmu-expedition-ai here 5 rich` on both dirt and indoor
flooring and observe idle weapon handling before/after the twenty-second construction delay.

For the multiple-attacker follow-up, use two or more hostile riflemen from opposite sides,
then a larger group from one side and a flanker from another. Add an alien rush during the
firefight. Check divided targets, rusher priority, fire during short crossfire moves and
continued fighting when no better stance exists. Verify that some squadmates keep firing
while others move and that brief sight loss does not reveal hidden targets. Repeat with
six guards under automatic fire and record search timings; no runtime cost claim is made
from the build alone. This follow-up was compiled without running tests or in-game checks.

Manual verification for equipment, water and mobile fire (not yet run):

1. Spawn `cmu-expedition-ai here 4 specialists`, then `cmu-expedition-ai here 5 rich`.
   Confirm gear, finite magazines, safe swaps, pistol use when crowded/out of primary ammo,
   and no idle wield cycle. Disarm and knock down each role; check native recovery and possession.
2. Order a patrol through shallow/deep RMC water and across a catwalk on both map types.
   Check native slowdown, continued progress, blocked hard boundaries/cliffs and fire avoidance.
3. Enter a walking patrol's sight, strafe, flank and fire automatically. Verify shots before
   arrival/stopping, no repeated aim reset, return fire during retreats/peeks, and no firing
   during treatment/reload/throw/switch. Inspect the travelling-shot counter in status.
4. Present clustered enemies at 7–9 m, then move allies through the intended blast zone during
   preparation. Confirm useful throws, cancellation when unsafe and at most two per 35-second
   decision. Apply crossfire or expose a casualty: confirm smoke placement, no duplicate screen,
   actual cloud-based sight loss and no automatic healing in exposed smoke.
5. Present a cluster at 5–6 m to a rocketeer. Repeat with a friendly behind the tube, a nearer
   intercepting body, a blocked corridor and a rushing alien. Confirm held unsafe shots, one
   actual rocket, native wield/backblast effects, a discarded spent tube, firearm return and
   the 20-second squad rocket cooldown. Launchers have no reload supply in these loadouts.
6. Generate an expedition and confirm one short GOVFOR ARES priority assignment when the LZ
   opens. Use the status command during all scenarios and record six-guard search cost.

Manual verification for corner routing (not yet run; build-only follow-up):

1. On both a colony grid and an expedition, order a six-guard squad across 40–100 tiles with
   multiple wall corners, one-tile corridors, offset doors, barricades and dense trees. Repeat
   in reverse, from sub-tile spawn offsets and on a rotated grid. Check continuous corner
   turns, no wall penetration, arrival and resumed patrols after combat interruptions.
2. Trigger tactical flanks, retreats and radio approaches around the same corners, with moving
   targets and allies. Check that firing while travelling and native friendly separation remain.
3. Add/remove a blocking crate during travel. Confirm the route reconnects or reports blocked,
   retries without an endless wobble, and resumes when a route exists. Repeat through RMC water;
   native slowdown should still allow progress, while fire, space and hard banks remain blocked.
4. Record route/search timings with six guards receiving simultaneous long move orders and
   contact. Cell budgets remain bounded; runtime cost has not been measured for this change.
