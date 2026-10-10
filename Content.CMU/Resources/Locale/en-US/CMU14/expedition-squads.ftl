cmd-cmu-squads-desc = Open expedition squad management and tactical diagnostics.
cmd-cmu-squads-help = cmu-squads
cmu-squads-title = Expedition squad management
cmu-squads-refresh = Refresh
cmu-squads-copy = Copy diagnostics
cmu-squads-copy-tip = Copy the latest displayed diagnostics for every member of the selected squad.
cmu-squads-copied = Squad diagnostics copied to clipboard.
cmu-squads-live = Live updates
cmu-squads-spawn = Spawn squad
cmu-squads-doctrine = Apply doctrine
cmu-squads-clothing-note = Faction kits set clothing, primary weapons and ammunition at spawn.
    Kits and doctrine do not change team or IFF.
    Set friendly and hostile factions in Relations.
cmu-squads-here = At my body / ghost
cmu-squads-map = Map
cmu-squads-move = Move
cmu-squads-move-tip = Travel to the destination while responding to threats along the route.
cmu-squads-assault = Assault
cmu-squads-assault-tip = Fight toward this objective in short bounds. Engage contacts, keep safe firing lanes, and resume the advance when able. Immediate threats, treatment and ammunition still take priority.
cmu-squads-guard = Guard / fortify
cmu-squads-hold = Hold position
cmu-squads-patrol-add = Add patrol point
cmu-squads-patrol-start = Start patrol
cmu-squads-patrol-stop = Stop patrol
cmu-squads-regroup = Regroup
cmu-squads-resupply = Stand down / resupply
cmu-squads-add-friendly = Add friendly
cmu-squads-add-target = Add target
cmu-squads-set-friendly = Apply friendlies
cmu-squads-set-target = Apply targets
cmu-squads-factions = Comma-separated factions; default resets overrides
cmu-squads-diagram = Tactical diagram: 48 m, centered on selected member
cmu-squads-legend = White: ready | Orange: injured | Gray: inactive
    Cyan: selection / route | Red: contact | Yellow: destination
    Green: cover | Orange rings: rejected cover
cmu-squads-select = Select a squad and member to inspect decisions.
cmu-squads-summary = Squad {$squad}: {$count} members | Map {$map} | {$phase}
cmu-squads-applied = Applied to {$count} members.
cmu-squads-invalid = Invalid selection, position, faction or parameters.
cmu-squads-orders-tab = Orders
cmu-squads-deploy-tab = Deploy
cmu-squads-relations-tab = Relations
cmu-squads-map-tab = Tactical view
cmu-squads-diagnostics-tab = Diagnostics
cmu-squads-destination = Order destination
cmu-squads-facing = Defense facing
cmu-squads-count = Squad size (1–12)
cmu-squads-composition = Squad composition / role
cmu-squads-outfit = Faction equipment kit
cmu-squads-doctrine-label = Starting doctrine
cmu-squads-deploy-note = Deploys at the destination set in Orders.
    “At my body / ghost” uses your current position.
cmu-squads-patrol-heading = Patrol route
cmu-squads-auto-patrol = Automatic local patrol
cmu-squads-auto-patrol-tip = Creates a walkable local route around this squad. Combat pauses patrol; explicit movement or hold orders replace it.
cmu-squads-patrol-clear = Clear patrol points
cmu-squads-patrol-no-space = No safe patrol loop found. The squad will retry while automatic patrol is enabled.
cmu-squads-coordination-heading = Squad behavior
cmu-squads-aim-skill = Aim skill: {$percent}%
cmu-squads-aim-skill-tip = Applies to this squad when released. Controls target leading and aiming precision. Lower skill adds a stable aiming error; 100% uses full tracking. Native weapon spread still applies.
cmu-squads-cooperate = Assist nearby friendly squads
cmu-squads-cooperate-tip = Share contacts by compatible radio and let one free squad assist from a different approach. Hold, guard and explicit travel orders take priority.
cmu-squads-relations-note = Applies to the selected squad.
    Use “default” to restore faction defaults.
    In Targets, AllHostile engages every non-friendly combatant.
    Squadmates, friendly factions and ignored entities remain protected.
cmu-squads-all-hostile-option = AllHostile — targets all non-friendlies
cmu-squads-friendly-heading = Friendly factions
cmu-squads-target-heading = Hostile factions
cmu-squads-overview = {$active}/{$total} active · {$phase} · {$points} patrol points · {$operation}
cmu-squads-member-summary = {$role} · {$duty} · {$condition}
    {$weapon} — {$ammo} rounds · Damage {$damage} · {$state}
cmu-squads-unarmed = No active weapon
cmu-squads-diagnostic-controller = Controller: {$owner} — {$reason} | State changes: {$changes}
cmu-squads-diagnostic-timing = Decision time: {$last} ms | Moving average: {$average} ms | Peak: {$peak} ms | Samples: {$samples}
cmu-squads-diagnostic-learning = Experience: {$group} | Outcomes: {$samples} | Flank cost: {$flank} | Danger cost: {$danger}
cmu-squads-diagnostic-corner = Corner: {$decision} | Remembered lanes: {$lanes} | Alternate approaches: {$flanks} | Staging moves: {$staging}
cmu-squads-diagnostic-patrol = Patrol: {$decision} | Visits: {$visits} | Searches: {$searches} | Failed routes: {$failures}
cmu-squads-diagnostic-assistance = Assistance: {$decision} | Accepted: {$accepted} | Declined: {$declined}
cmu-squads-diagnostic-grenade = Ordnance: {$decision}
cmu-squads-diagnostic-fire-response = Fire response: {$decision} | Ally pats: {$pats} | Self rolls: {$rolls} | Ground-fire escapes: {$escapes}
cmu-squads-diagnostic-close-quarters = Teammate clearance steps: {$nudges} | Last-resort melee strikes: {$strikes}
cmu-squads-condition-player = player-controlled
cmu-squads-condition-dead = dead
cmu-squads-condition-critical = critical
cmu-squads-condition-disabled = AI disabled
cmu-squads-condition-active = active
cmu-squads-member-recorded = {$role} · {$duty} · {$condition}
    Last AI record: {$weapon} — {$ammo} rounds · Damage {$damage} · {$state}
cmu-squads-diagnostic-recorded = Current condition: {$condition}
    Last living AI record at {$at}s, sampled {$age}s before control ended.
    Equipment and remembered lanes below describe that sample, before stop cleanup.
cmu-squads-diagnostic-recorded-activity = {$state} | {$duty} / {$doctrine} / {$phase}
    Controller: {$owner} — {$reason}
    Fire: {$fire} | Weapon decision: {$weaponDecision}
    Active weapon: {$weapon} | Rounds: {$ammo} | Damage: {$damage} | Stress: {$stress}
    Native fire wait: {$nativeWait}s | AI aim wait: {$aimWait}s
    Corner: {$corner} | Remembered lanes: {$lanes}
cmu-squads-diagnostic-recorded-history = Decisions before AI control ended:
cmu-squads-diagnostic-recorded-movement = Squad: {$squad} | Traffic: {$traffic} | Door: {$door} | Vault: {$vault}
    Firing movement: {$movement} | Sustained fire: {$sustained} | Volley: {$volley}
cmu-squads-diagnostic-lifetime = Lifetime counters and decision timing:
cmu-squads-diagnostic-lifetime-movement = State changes: {$changes} | Route searches: {$searches} / {$milliseconds} ms
    Detours: {$detours} | Clearance steps: {$nudges} | Doors opened: {$doors} | Door failures: {$doorFailures}
    Covered moves: {$covered} | Interrupted moves: {$interrupted}
cmu-squads-diagnostic-lifetime-combat = Moving shots: {$movingShots} | Reloads: {$reloads} | Grenades: {$grenades}
    Failed plans: {$failedPlans} | Corner flanks: {$flanks} | Corner staging moves: {$staging}
cmu-squads-diagnostic-no-record = Current condition: {$condition}. No living AI activity was sampled before control ended.
cmu-squads-diagnostic-hearing = Last heard: {$kind} | Age: {$age}s (sound alone does not authorize a shot)
cmu-squads-diagnostic-hearing-none = Last heard: no sound recorded
cmu-squads-noise-gunfire = gunfire
cmu-squads-noise-door = door opening
cmu-squads-treatment-native = Self-treatment: native dose ({$status})
cmu-squads-treatment-preparing = Self-treatment: freeing a hand | Preparation time remaining: {$remaining}s
cmu-squads-treatment-idle = Self-treatment: no active dose | Next attempt allowed in {$retry}s
cmu-squads-medic-task = Medic task: {$phase} — {$decision} | Doses: {$doses} | Shocks: {$shocks}
cmu-squads-medic-none = Medic task: none
cmu-squads-activity-heading = Current activity
cmu-squads-activity-recorded = Last living activity
cmu-squads-order-label = Order
cmu-squads-tactic-label = Squad tactic
cmu-squads-action-label = Executing
cmu-squads-status-label = Status / wait
cmu-squads-current-action = {$state} · {$owner} — {$reason}
cmu-squads-order-point = {$order} · Map {$map}, {$x}, {$y}
cmu-squads-order-assault = Assault objective
cmu-squads-order-guard = Guard / fortify destination
cmu-squads-order-auto-patrol = Automatic local patrol
cmu-squads-order-patrol = Follow patrol route
cmu-squads-order-move = Move to destination
cmu-squads-order-hold = Hold current position
cmu-squads-order-support = Support a friendly squad
cmu-squads-order-local = Local squad behavior
cmu-squads-order-ended = AI control ended; earlier activity is shown below
cmu-squads-phase-holding = Holding
cmu-squads-phase-travelling = Travelling
cmu-squads-phase-anti-rush = Defending against a closing attacker
cmu-squads-phase-withdraw = Withdrawing
cmu-squads-phase-anti-armor = Engaging armor
cmu-squads-phase-fire-and-move = Fire and maneuver
cmu-squads-phase-assault = Assaulting the objective
cmu-squads-phase-assault-advance = Advancing to the assault objective
cmu-squads-phase-no-active-members = No active members
cmu-squad-phase-reason-no-active-members = No nearby member is available for squad orders.
cmu-squad-phase-reason-closing-melee-threat = A current or recently observed melee attacker is closing in.
cmu-squad-phase-reason-casualties-or-empty-weapons = Casualties or ammunition shortages require recovery.
cmu-squad-phase-reason-vehicle-contact = An armed vehicle is engaged by an anti-armor member.
cmu-squad-phase-reason-assault-contact = Engaging a contact while advancing toward the assault objective.
cmu-squad-phase-reason-assault-objective = Advancing toward the ordered assault objective.
cmu-squad-phase-reason-moving-to-order = Following an explicit movement order.
cmu-squad-phase-reason-no-contact = No current contact requires a combat maneuver.
cmu-squad-phase-reason-engaging-contact = A contact is being engaged with covering fire and movement.
cmu-squads-recorded-status = Now {$condition} · Record at {$at}s · Last fire check: {$fire}
cmu-squads-status-utility-cleanup = Waiting to store or release a held utility item.
cmu-squads-status-weapon = Preparing a weapon: {$reason}
cmu-squads-status-door = Door handling: {$reason}
cmu-squads-status-traffic = Teammate clearance: {$reason}
cmu-squads-status-route = The current order route is blocked.
cmu-squads-status-cover = Maneuver coordination: {$reason}
cmu-squads-status-corner = Holding a dangerous corner: {$reason}
cmu-squads-status-shot = Firing lane paused: {$reason}
cmu-squads-status-sight = Waiting for a visible aim point.
cmu-squads-status-cadence = Normal weapon fire interval: {$remaining}s remaining.
cmu-squads-status-aim = Aiming / planned firing pause: {$remaining}s remaining.
cmu-squads-status-none = No active wait reported. Detailed fire checks are in Diagnostics.
cmu-squads-diagnostic-assault = Assault: {$decision} | Completed bounds: {$bounds}
cmu-squads-diagnostic-recorded-assault = Assault: {$decision}
cmu-squads-diagnostic-rocket = Launcher selection / safety: {$decision}
cmu-squads-variant-mixed = Squad · balanced combined arms
cmu-squads-variant-fireteam = Squad · rifle fireteam
cmu-squads-variant-patrol = Squad · mobile patrol
cmu-squads-variant-defense = Squad · defensive support
cmu-squads-variant-raiders = Squad · close assault
cmu-squads-variant-medical = Squad · medical escort
cmu-squads-variant-rifleman = Role · rifleman (mixed rifles)
cmu-squads-variant-assault = Role · assault (SMGs / breachers)
cmu-squads-variant-support = Role · support (automatic weapons)
cmu-squads-variant-marksman = Role · marksman (DMRs / snipers)
cmu-squads-variant-rocketeer = Role · rocketeer (launcher + primary)
cmu-squads-variant-medic = Role · medic (medical tools + primary)
