# Chembomb casing interaction strings
rmc-chembomb-full = The casing is already full!
rmc-chembomb-beaker-empty = The container is empty.
rmc-chembomb-fill = Transferred {$amount}u. ({$total}/{$max}u)
rmc-chembomb-examine-volume = Chemical fill: {$current}/{$max}u
rmc-chembomb-examine-detonator = [color=green]Detonator assembly installed.[/color]
rmc-chembomb-examine-no-detonator = [color=red]No detonator assembly.[/color]
rmc-chembomb-examine-locked = [color=orange]ARMED[/color]

# Slot labels
rmc-chembomb-detonator-slot = Detonator Assembly

# Explosive reagents
reagent-name-rmc-nitric-acid = Nitric Acid
reagent-desc-rmc-nitric-acid = A corrosive, fuming strong acid. Essential precursor for synthesizing explosive compounds. Keep cold.

reagent-name-rmc-glycerol = Glycerol
reagent-desc-rmc-glycerol = A colorless, odorless, viscous liquid. Reacts with nitric acid to form nitroglycerin under cold conditions.

reagent-name-rmc-cyclonite = Cyclonite (RDX)
reagent-desc-rmc-cyclonite = A white crystalline solid used as an explosive. One of the most powerful military explosives known.

reagent-name-rmc-anfo = ANFO
reagent-desc-rmc-anfo = Ammonium Nitrate/Fuel Oil mixture. A commonly used bulk industrial explosive.

reagent-name-rmc-nitroglycerin = Nitroglycerin
reagent-desc-rmc-nitroglycerin = An extremely sensitive oily liquid explosive. Handle with care.

reagent-name-rmc-octogen = Octogen (HMX)
reagent-desc-rmc-octogen = A powerful explosive compound, more stable than RDX but with higher performance.

reagent-name-rmc-ammonium-nitrate = Ammonium Nitrate
reagent-desc-rmc-ammonium-nitrate = A white crystalline solid used as a fertilizer and explosive precursor.

reagent-name-rmc-potassium-hydroxide = Potassium Hydroxide
reagent-desc-rmc-potassium-hydroxide = A strong alkali used in various chemical processes.

reagent-name-rmc-hexamine = Hexamine
reagent-desc-rmc-hexamine = An organic compound used as a fuel tablet and explosive precursor. Burns with a clean, hot flame.

reagent-name-rmc-potassium-chloride = Potassium Chloride
reagent-desc-rmc-potassium-chloride = A metal halide salt used in medicine and industry.

reagent-name-rmc-sodium-chloride = Sodium Chloride
reagent-desc-rmc-sodium-chloride = Common salt. Used in everything from cooking to chemical synthesis.

# Demolitions simulator
rmc-demolitions-sim-no-casing = No casing found in active hand.
rmc-demolitions-sim-not-casing = The held item is not a chembomb casing.
rmc-demolitions-sim-header = [bold]Simulation: {$name}[/bold]
rmc-demolitions-sim-volume = Chemicals loaded: {$current}/{$max}u
rmc-demolitions-sim-empty = [color=gray]No explosive or incendiary chemicals detected.[/color]
rmc-demolitions-sim-explosion = [bold]Blast:[/bold] Power {$power}, Falloff {$falloff}, Est. radius ~{$radius} tiles
rmc-demolitions-sim-fire = [bold]Fire:[/bold] Intensity {$intensity}, Radius {$radius} tiles, Duration {$duration}s

# Assembly tool steps
rmc-chembomb-seal-no-detonator = Insert a detonator assembly first.
rmc-chembomb-seal-disarm-first = Disarm the casing before unsealing.
rmc-chembomb-arm-seal-first = Seal the casing with a screwdriver first.
rmc-chembomb-sealed = You screw the casing shut.
rmc-chembomb-unsealed = You unscrew the casing.
rmc-chembomb-armed = You prime the detonator. The casing is armed.
rmc-chembomb-disarmed = You cut the arming wire. The casing is safe.
rmc-chembomb-not-armed = The casing is not ready. Complete the assembly first.

# Examine stage
rmc-chembomb-examine-open = Unsealed.

# Mine casing deployment
rmc-mine-no-detonator = No detonator assembly installed.
rmc-mine-planted = Mine planted.

# Ordnance part assembly (combine igniter/timer → detonator assembly)
rmc-ordnance-assembly-incompatible = These parts are not compatible.
rmc-ordnance-assembly-combined = You combine the parts into a {$result}.
rmc-ordnance-assembly-pry-locked = Unlock the assembly with a screwdriver first.
rmc-ordnance-assembly-locked = You lock the assembly. It is ready to be installed into a casing.
rmc-ordnance-assembly-unlocked = You unlock the assembly. The parts can be adjusted again.
rmc-ordnance-assembly-disassembled = You separate the assembly back into parts.
rmc-ordnance-payload-not-ready = The warhead is not armed yet.
rmc-ordnance-payload-no-fuel = The propulsion casing is missing fuel.
rmc-ordnance-payload-wrong-fuel = The propulsion casing must be filled with {$fuel}.
rmc-ordnance-payload-no-chemicals = The warhead has no chemical payload.
rmc-ordnance-payload-assembled = The ordnance is fully assembled.

# Ordnance assembly overrides
rmc-chembomb-examine-sealed = [color=yellow]Sealed.[/color]
rmc-ordnance-assembly-combined = You combine the parts into an ordnance assembly.
rmc-ordnance-timer-set = {$time} seconds
rmc-ordnance-timer-current = {$time} seconds (current)
rmc-ordnance-timer-popup = Timer set to {$time} seconds.
rmc-ordnance-frequency-configure = Configure frequency
rmc-ordnance-frequency-set = {$frequency}
rmc-ordnance-frequency-current = {$frequency} (current)
rmc-ordnance-frequency-popup = Frequency set to {$frequency}.
rmc-ordnance-proximity-set = {$range} tile radius
rmc-ordnance-proximity-current = {$range} tile radius (current)
rmc-ordnance-proximity-popup = Proximity radius set to {$range} tiles.
rmc-demolitions-scanner-ui-title = Demolitions Scanner
rmc-demolitions-scanner-ui-idle = Scan a custom casing to display a full readout.
rmc-demolitions-scanner-ui-status = Scan complete: {$name}
rmc-demolitions-scanner-ui-section-overview = Casing Overview
rmc-demolitions-scanner-ui-section-yield = Blast Estimate
rmc-demolitions-scanner-ui-section-fire = Incendiary Estimate
rmc-demolitions-scanner-ui-section-summary = Tactical Summary
rmc-demolitions-scanner-ui-casing = Casing: {$name}
rmc-demolitions-scanner-ui-stage = Assembly state: {$stage}
rmc-demolitions-scanner-ui-detonator = Detonator: {$status}
rmc-demolitions-scanner-ui-detonator-present = installed
rmc-demolitions-scanner-ui-detonator-missing = missing
rmc-demolitions-scanner-ui-volume = Chemicals: {$current}/{$max}u
rmc-demolitions-scanner-ui-stage-open = Open
rmc-demolitions-scanner-ui-stage-sealed = Sealed
rmc-demolitions-scanner-ui-stage-armed = Armed
rmc-demolitions-scanner-ui-blast-present = [color=#ffd37a]Projected blast:[/color] Power {$power}, falloff {$falloff}, approx. radius {$radius} tiles.
rmc-demolitions-scanner-ui-blast-none = [color=#7f8a99]No explosive payload detected.[/color]
rmc-demolitions-scanner-ui-fire-present = [color=#ff9d6b]Projected fire:[/color] Intensity {$intensity}, radius {$radius} tiles, duration {$duration}s.
rmc-demolitions-scanner-ui-fire-none = [color=#7f8a99]No incendiary payload detected.[/color]
rmc-demolitions-scanner-ui-summary-empty = [color=#7f8a99]Payload is chemically inert. No meaningful blast or fire effect is expected.[/color]
rmc-demolitions-scanner-ui-summary-payload = [color=#9effb6]Reactive payload detected. Handle as live ordnance.[/color]
rmc-demolitions-scanner-ui-summary-no-detonator = [color=#ffb36b]Trigger path incomplete: no detonator assembly is installed.[/color]
rmc-demolitions-scanner-ui-summary-not-armed = [color=#ffd37a]Trigger path present, but the casing is not fully armed yet.[/color]
rmc-demolitions-scanner-ui-summary-ready = [color=#ff7a7a]Casing is armed and has an active detonator. Treat as immediately dangerous.[/color]
rmc-demolitions-scanner-ui-summary-radius = Estimated lethal blast envelope reaches roughly {$radius} tiles from impact.
rmc-demolitions-scanner-ui-summary-fire = Incendiary spread may cover up to {$radius} tiles for about {$duration} seconds.
