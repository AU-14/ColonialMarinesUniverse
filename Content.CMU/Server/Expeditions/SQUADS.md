# Squad management and coordination

Open `cmu-squads` in the client console, or use **Expedition squad management** in an agent's
right-click menu. Both require Admin permission. Requests are checked again on the server;
losing permission closes the panel. Player-controlled, critical and dead members remain visible
in diagnostics but are excluded from orders. This works on colony and expedition maps.

## Panel controls

- Spawn 1-12 agents: choose composition, clothing and doctrine separately. Clothing and doctrine
  do not change team, native IFF or weapon skills. Spawn reports the actual number deployed.
- Choose a squad, then Move, Guard / fortify, Hold position, Regroup, or Stand down / resupply.
  Position orders use your current body/ghost position, or map ID and world X/Y when unchecked.
  Guard accepts a facing and enables the existing quiet-time shovel/metal construction behavior.
  Move and Hold do not enable construction. Regroup uses the current active leader's location.
- Add 2-8 patrol points and start/stop the patrol. Existing console commands, including
  `patrol-clear`, remain available. Travel pauses for combat and resumes afterward.
- Select a faction and add it to the friendly/target list, or enter comma-separated prototype IDs,
  then Apply. `default` clears that override. Friendly overrides take priority over target overrides.
  These are targeting orders, not faction membership or IFF reconfiguration.
- Apply a doctrine to the selected squad. Choices are balanced, scavenger, USCM, RMC, UPP, PMC,
  CLF, CMB, LACN, CCAF, UACG and Prodigy. Changes adjust the role's baseline aggression, courage,
  preferred range and position commitment without accumulating on repeated applications.
- Live updates refresh once per second. Select a member for health/ammo/stress, duty, state,
  movement/door/supply/fire reasons, native weapon cooldown versus AI aim delay, recent decisions,
  route-search time/cells and squad-update time.

The 48 m tactical diagram is a schematic centered on the selected member, not a terrain map or
click-to-order interface. White circles are same-map members; cyan is the route, red the last
contact, yellow the destination, green cover and orange rejected cover. Diagnostics are capped
at 100 squads, 32 members per selected squad, 48 route points and 12 recent decisions per member.

## Behavior changes

| Area | Implemented behavior |
| --- | --- |
| Decision stability | A productive stationary volley keeps ownership of optional decisions. Immediate danger, lost shot clearance, depleted ammunition and urgent injury release it. State changes and reasons enter a bounded history. |
| Shared travel | Members can borrow and revalidate another member's route corridor to a shared rally. Existing queues and spaced destinations remain. After six seconds blocked, an agent can attempt a route toward its active leader, then resume the original destination. Regroup searches have a twelve-second backoff. |
| Squad plans | A once-per-second coordinator retains ordinary phases for eight seconds and roles for six. Holding, travel, fire-and-move, anti-rush, withdrawal and anti-armor phases assign advance, overwatch, rear guard, medic, recovery and anti-armor duties. Emergencies override ordinary commitments. |
| Failed cover | Hits while hidden and repeated hits at a peek mark nearby positions unusable for 12-45 seconds. Up to twelve recent failures are retained. Live shelter and firing-lane checks still run. |
| Alien pressure | Spaced lateral/rear duty positions bias escape choices. A small number of available shooters prioritize the known threat chasing a member; remaining members retain target distribution and rear coverage. Existing short-lived melee memory and acid avoidance remain. |
| Logistics | Squads remember up to twelve visible loose storage/crate locations for ninety seconds. One quiet, unordered member may visit a remembered cache, collect compatible items for teammates and deliver surplus to a low member. Real item extraction, capacity, locks and handoff checks apply. |
| Casualties | Medics reuse a currently safe collection area, with room for other patients. A covered critical bleeding patient can receive an available bleeding dressing before extraction. Successfully revived AI get a twenty-second recovery period that defers optional pursuit/flanking, while retaining return fire and treatment. |
| Specialists | Riflemen favor visible infantry over armored vehicles. Between actions, allies can take a short safe bound out of a ready rocketeer's firing/backblast lane. Reserved grenade/smoke areas can pause a squad movement leg while firing continues. Emergency escape takes priority. |
| Doctrine | Independent presets change behavioral preferences while retaining weapon-role baselines. Courage contributes to withdrawal decisions; aggression, range and commitment feed existing combat choices. |
| Diagnostics | Server-authorized panel orders and live decision/path/contact/cover inspection, without additional combat chatter. |
| Connected grids and levels | Same-map routes can cross touching ground grids. Cross-level orders find a chain of native CMU ladders/stairs, approach it, perform real native traversal, and continue the final order. |
| Hearing | Gunfire and opening doors produce uncertain, temporary investigation points. Walls reduce hearing distance; suppressed gunfire has shorter range. Sound never assigns a firing target, visual contact or muzzle-flash permission. |

Supply trips and deliveries each have a twenty-second deadline. Cache travel is limited to 24 m
and the guard leash; deliveries start within 14 m and use bounded local routes. Failed recipients
back off for thirty seconds. A courier keeps its minimum own reserves and shares physical items
through the existing interruptible handoff. The runner supports compatible ammunition, known
grenades, flares, dressings and tagged fresh medic tools; it does not act as a universal equipment
courier. Individual rocketeers still scavenge usable replacement launchers through their weapon logic.

The portal graph refreshes every five seconds, considers at most 64 visited maps per chain search,
and chooses a transition that reduces remaining map hops. A portal approach times out after
45 seconds; failed transitions back off for fifteen seconds. Ladder traversal uses its native
interaction range and do-after; stairs use their native directional crossing trigger. Contact,
injury, control transfer and replacing orders cancel AI-owned climbing. Blocked or destroyed
portals and unsafe landings cannot create a teleport fallback.

Hearing uses 18 m for ordinary gunfire, 4 m for suppressed shots and 5 m for doors, shortened to
35% through obstruction. The guessed position has up to 2 m per-axis error in clear conditions
or 4 m when obstructed, lasts ten seconds and respects the guard leash. Medics, rear guards,
fortifying guards and members following explicit travel orders do not abandon those tasks for noise.

## Limits and verification

These are bounded extensions to the existing executors and GOAP planner, not persistent learning
between rounds or a globally optimal multi-agent pathfinder. Native doors, inventories, medicine,
weapons, projectile safety and permission checks remain authoritative. Multi-level travel requires
connected CMU z-level ladders/stairs and safe ground; it does not add vehicle boarding, shuttle travel,
gap jumping, or arbitrary portal support. Long mazes can still need intermediate waypoints.

Server/client builds and static file checks are the validation performed for this change. No tests
or game session were run. The following runtime checks remain required before considering behavior
verified:

1. Open the panel as admin, spawn six `mixed` agents, change selection during live updates and
   issue each order. Check partial spawns, invalid coordinates/factions, destroyed squads, permission
   revocation and a member possessed by a player. Editing a numeric input must not stop refresh/Hold.
2. Repeat with twelve members through doors, streetlights, rotated touching grids, dense trees and
   a narrow corridor. Block one member and kill the leader. Confirm shared routes still obey access,
   progress resumes or reports blocked, and unreachable members do not freeze the others.
3. Order a squad across at least two connected levels. Exercise both ladder directions and stair
   orientations, an obstructed landing, destroyed portal, contact during climbing and an explicit
   Hold. Confirm native timing, no duplicate climb actions and resumption of the final destination.
4. Attack from two directions during long travel and sustained fire. Watch productive volleys,
   return fire, one advance assignment, rearguard response and emergency interruption. Shoot agents
   behind penetrable cover and at repeated peeks; check orange rejected positions and expiry.
5. Rush with two aliens through smoke and around acid patches. Watch separated escape choices,
   limited support redirection and maintained firing lanes. Check that doctrine changes do not
   grant perception, skills, ammunition or IFF changes.
6. Deplete differing weapons and medical tools; provide visible compatible, incompatible, locked
   and exhausted crates. Observe one runner, actual item counts, carried reserves, deliveries around
   a corner and timeout/backoff for an unreachable or fighting recipient. No items may duplicate.
7. Put a friendly in bleeding crit under covered fire, then revive an eligible casualty. Check real
   dressing/charge use, collection-point safety, interruption when the medic is hit and the revival
   recovery period. Repeat with no cover, exhausted tools and an unrevivable corpse.
8. Engage an armed hostile vehicle with infantry nearby. Confirm allies clear a launch lane only
   when the short move is safe; rocket blast/backblast and friendly interlocks still reject bad shots.
   Order travel through a reserved throw area and check movement resumes when the reservation ends.
9. Fire unseen ordinary and suppressed weapons behind walls; open a nearby door. Verify an uncertain
   investigation and no sound-only shooting. Then expose the target in light and confirm normal fire.
10. Record squad-update and candidate/route-search time for six and twelve agents in simultaneous
    contact. Compare with the pre-change build on the same scene; watch frame spikes during shared
    route rejection, portal searches, supply selection and panel refresh.
