ent-CMDemolitionsScanner = demolitions scanner
    .desc = A handheld scanner for analyzing custom ordnance. Use it on a chembomb casing to get a readout of its projected explosive and fire characteristics.

ent-RMCExplosionSimulator = explosion simulation computer
    .desc = A high-powered simulation computer for analyzing ready ordnance payloads against different target types. Insert a finished grenade, mine, rocket, or other explosive sample, select a target profile, and run a two-minute simulation.

ent-RMCDemolitionsCamera = simulation camera
    .desc = A disposable chamber camera used by the demolitions simulators.

rmc-explosion-sim-slot-sample = Sample Slot

rmc-demolitions-simulator-ui-title = Demolitions Simulator
rmc-demolitions-simulator-ui-blast-zone = Blast Zone
rmc-demolitions-simulator-ui-target = Target:
rmc-demolitions-simulator-ui-target-xenomorph-drone = Xenomorph Drone
rmc-demolitions-simulator-ui-target-xenomorph-warrior = Xenomorph Warrior
rmc-demolitions-simulator-ui-target-xenomorph-crusher = Xenomorph Crusher
rmc-demolitions-simulator-ui-target-marine-light = Marine (no armour)
rmc-demolitions-simulator-ui-target-marine-heavy = Marine (full armour)
rmc-demolitions-simulator-ui-target-metal-wall = Metal wall
rmc-demolitions-simulator-ui-no-casing = No casing simulated.
rmc-demolitions-simulator-ui-blast-parameters = Blast Parameters
rmc-demolitions-simulator-ui-fire-parameters = Fire Parameters
rmc-demolitions-simulator-ui-damage-estimates = Damage Estimates
rmc-demolitions-simulator-ui-simulate = Simulate
rmc-demolitions-simulator-ui-status = Hold a chembomb casing in your active hand and press Simulate.
rmc-demolitions-simulator-ui-cooldown = Cooling down... {$seconds}s
rmc-demolitions-simulator-ui-damage-idle = [color=gray]Hold a chembomb casing and press Simulate.[/color]
rmc-demolitions-simulator-ui-simulating = Simulating: {$name}
rmc-demolitions-simulator-ui-volume = Chemicals: {$current}/{$max} u
rmc-demolitions-simulator-ui-blast-stats = Power {$power}   Falloff {$falloff}   Radius ~{$radius} tiles
rmc-demolitions-simulator-ui-blast-none = No explosive chemicals.
rmc-demolitions-simulator-ui-fire-stats = Intensity {$intensity}   Radius {$radius} tiles   Duration {$duration}s
rmc-demolitions-simulator-ui-fire-none = No incendiary chemicals.
rmc-demolitions-simulator-ui-damage-header = [bold][color=#88dd88]{$target}[/color][/bold] - HP: {$hp}  Armour: {$armor}%
rmc-demolitions-simulator-ui-status-lethal = [color=#ff2222]LETHAL[/color]
rmc-demolitions-simulator-ui-status-critical = [color=#ff8800]Critical[/color]
rmc-demolitions-simulator-ui-status-wounded = [color=#ffdd00]Wounded[/color]
rmc-demolitions-simulator-ui-status-alive = [color=#88ff88]Alive[/color]
rmc-demolitions-simulator-ui-range-epicenter = 0 (epicentre)
rmc-demolitions-simulator-ui-range-tiles = {$count} {$count ->
        [one] tile
       *[other] tiles
    }
rmc-demolitions-simulator-ui-damage-row = [color=#aaaaaa]{$label}[/color] {$blast}+{$fire} = [bold]{$total}[/bold] dmg -> {$status}

rmc-explosion-sim-ui-title = Explosion Simulation Computer
rmc-explosion-sim-ui-subtitle = Two-minute analysis for custom explosive payloads.
rmc-explosion-sim-ui-analysis-cycle = Analysis Cycle
rmc-explosion-sim-ui-standby = Standby
rmc-explosion-sim-ui-target-profile = Target Profile
rmc-explosion-sim-ui-target-help = Select the formation for replay.
rmc-explosion-sim-ui-target-marines = USCM Marines
rmc-explosion-sim-ui-target-special-forces = Special Forces
rmc-explosion-sim-ui-target-xenomorphs = Xenomorph Hive
rmc-explosion-sim-ui-threat-notes = Threat Notes
rmc-explosion-sim-ui-sample-status = Sample Status
rmc-explosion-sim-ui-no-sample = No ordnance sample inserted.
rmc-explosion-sim-ui-blast-projection = Blast Projection
rmc-explosion-sim-ui-incendiary-projection = Incendiary Projection
rmc-explosion-sim-ui-summary = Simulation Summary
rmc-explosion-sim-ui-run-analysis = Run Analysis
rmc-explosion-sim-ui-replay = Replay
rmc-explosion-sim-ui-footer-left = Explosion Analysis Suite
rmc-explosion-sim-ui-footer-right = OT Research Annex
rmc-explosion-sim-ui-header-idle = Explosion Simulation Computer
rmc-explosion-sim-ui-header-processing = Chemical Analysis In Progress
rmc-explosion-sim-ui-header-ready = Simulation Package Ready
rmc-explosion-sim-ui-processing-remaining = {$seconds}s remaining
rmc-explosion-sim-ui-processing-complete = Analysis complete
rmc-explosion-sim-ui-sample-loaded = Sample loaded: {$name}
rmc-explosion-sim-ui-readiness-no-sample-line1 = Insert a finished payload
rmc-explosion-sim-ui-readiness-no-sample-line2 = to unlock analysis.
rmc-explosion-sim-ui-readiness-processing-line1 = Sample is sealed
rmc-explosion-sim-ui-readiness-processing-line2 = Profiling against {$target}.
rmc-explosion-sim-ui-readiness-ready-line1 = Solution package cached.
rmc-explosion-sim-ui-readiness-ready-line2 = Replay is ready.
rmc-explosion-sim-ui-readiness-staged-line1 = Sample staged.
rmc-explosion-sim-ui-readiness-staged-line2 = Run analysis for {$target}.
rmc-explosion-sim-ui-blast-none-line1 = No explosive output
rmc-explosion-sim-ui-blast-none-line2 = predicted from this mix.
rmc-explosion-sim-ui-blast-present-line1 = Power {$power}
rmc-explosion-sim-ui-blast-present-line2 = Falloff {$falloff}
rmc-explosion-sim-ui-blast-present-line3 = Peak {$peak}
rmc-explosion-sim-ui-blast-present-line4 = Radius about {$radius} tiles
rmc-explosion-sim-ui-fire-none-line1 = No sustained fire
rmc-explosion-sim-ui-fire-none-line2 = predicted from this mix.
rmc-explosion-sim-ui-fire-present-line1 = Intensity {$intensity}
rmc-explosion-sim-ui-fire-present-line2 = Radius {$radius} tiles
rmc-explosion-sim-ui-fire-present-line3 = Duration {$duration} seconds
rmc-explosion-sim-ui-status-processing-line1 = Processing the loaded sample against the {$target} formation.
rmc-explosion-sim-ui-status-processing-line2 = Playback data will unlock in approximately {$seconds} seconds.
rmc-explosion-sim-ui-status-idle-line1 = Load a finished ordnance sample, pick a target profile,
rmc-explosion-sim-ui-status-idle-line2 = and run the two-minute simulation cycle.
rmc-explosion-sim-ui-status-ready-line1 = Simulation complete for the {$target} formation.
rmc-explosion-sim-ui-status-ready-blast = Blast envelope reaches roughly {$radius} tiles from the epicenter.
rmc-explosion-sim-ui-status-ready-no-blast = Mixture does not generate a meaningful blast wave.
rmc-explosion-sim-ui-status-ready-fire = Incendiary spread is projected to cover {$radius} tiles for about {$duration} seconds.
rmc-explosion-sim-ui-status-ready-no-fire = No persistent fire spread is expected.
rmc-explosion-sim-ui-status-ready-replay = Use Replay Chamber to spawn the chamber and watch the predicted detonation.
rmc-explosion-sim-ui-target-summary-marines = USCM marine squad
rmc-explosion-sim-ui-target-summary-special-forces = special forces breach stack
rmc-explosion-sim-ui-target-summary-xenomorphs = xenomorph assault wave
rmc-explosion-sim-ui-target-summary-unknown = unknown profile
rmc-explosion-sim-ui-target-notes-marines-1 = Standard infantry spread.
rmc-explosion-sim-ui-target-notes-marines-2 = Seven dummies are staggered
rmc-explosion-sim-ui-target-notes-marines-3 = to show casualty drop-off.
rmc-explosion-sim-ui-target-notes-special-forces-1 = Compact armored cluster.
rmc-explosion-sim-ui-target-notes-special-forces-2 = Five dummies are packed tighter
rmc-explosion-sim-ui-target-notes-special-forces-3 = for breaching mix stress tests.
rmc-explosion-sim-ui-target-notes-xenomorphs-1 = Dense organic rush.
rmc-explosion-sim-ui-target-notes-xenomorphs-2 = Nine dummies are packed forward
rmc-explosion-sim-ui-target-notes-xenomorphs-3 = to show short-range saturation
rmc-explosion-sim-ui-target-notes-xenomorphs-4 = and fire spread through a hive push.
rmc-explosion-sim-ui-target-notes-unknown = No target profile loaded.
