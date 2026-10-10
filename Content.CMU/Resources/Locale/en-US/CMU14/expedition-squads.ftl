cmd-cmu-squads-desc = Open expedition squad management and tactical diagnostics.
cmd-cmu-squads-help = cmu-squads
cmu-squads-title = Expedition squad management
cmu-squads-refresh = Refresh
cmu-squads-live = Live updates
cmu-squads-spawn = Spawn squad
cmu-squads-doctrine = Apply doctrine
cmu-squads-clothing-note = Outfit and doctrine do not change team or IFF.
    Set friendly and hostile factions in Relations.
cmu-squads-here = At my body / ghost
cmu-squads-map = Map
cmu-squads-move = Move
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
cmu-squads-composition = Roles and weapons
cmu-squads-outfit = Faction outfit
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
