## Weapon jams

cmu-dropship-weapon-jammed = WEAPON STOPPAGE: {$reason}. Clear it at the hardpoint with a multitool.
cmu-dropship-fault-reason-jam-feed = feed tray jam
cmu-dropship-fault-reason-jam-double = double-feed stoppage
cmu-dropship-fault-reason-jam-gremlin = something is rattling around in the breech

## Ground crew checklist

cmu-dropship-checklist-header = [color=cyan]GROUND CREW CHECKLIST[/color]
cmu-dropship-checklist-mounted = Mounted: {$weapon}
cmu-dropship-checklist-ammo = Loaded: {$ammo} ({$rounds}/{$max})
cmu-dropship-checklist-no-ammo = Loaded: [color=orange]NONE[/color]
cmu-dropship-checklist-faulted = Status: [color=red]FAULTED[/color] ({$reason})
cmu-dropship-checklist-serviceable = Status: [color=lime]SERVICEABLE[/color]
cmu-dropship-checklist-wear = Wear: {$uses} engagements, {$landings} landings since service. Landing fault chance {$landing}%, stoppage chance {$jam}%.
cmu-dropship-checklist-ok = [color=lime]{$text}[/color]
cmu-dropship-checklist-bad = [color=orange]{$text}[/color]

## Pilot console summaries

cmu-dropship-loadout-title = LOADOUT
cmu-dropship-loadout-line = {$location} {$weapon}: {$ammo}
cmu-dropship-loadout-ammo = {$ammo} {$rounds}/{$max}
cmu-dropship-loadout-empty = EMPTY
cmu-dropship-loadout-none = NO WEAPONS MOUNTED
cmu-dropship-preflight-title = PREFLIGHT
cmu-dropship-preflight-booting = FIRE CONTROL BOOTING
cmu-dropship-preflight-fault = FAULT {$location}: {$reason}
cmu-dropship-preflight-empty = EMPTY MOUNT {$location}
cmu-dropship-preflight-dry = NO AMMO {$location}
cmu-dropship-preflight-ok = ALL SYSTEMS NOMINAL

## M90 heat

cmu-mohawk-heat = BARREL HEAT: {$heat}/{$max}
cmu-mohawk-heat-overheated = BARREL HEAT: {$heat}/{$max} - OVERHEATED
cmu-mohawk-overheated = M90 OVERHEATED: barrels locked until they cool.
cmu-mohawk-overheat-cleared = M90 barrels cooled. Fire control restored.

## Admin verbs

cmu-cas-admin-verb-category = CAS (Admin)
cmu-cas-admin-clear-fault = Clear fault and wear
cmu-cas-admin-force-fault = Force fault
cmu-cas-admin-refill = Refill ammunition
cmu-cas-admin-restore-m90 = Restore M90 (repair, cool, calibrate)
cmu-cas-admin-skip-boot = Skip fire-control boot
cmu-cas-admin-landing-reboot = Force landing reboot roll
cmu-cas-admin-refill-all = Refill every hardpoint
cmu-cas-admin-clear-all = Clear every fault and restore M90

## Rocket fallout
cmu-cas-fallout-designator-obscured = Smoke scatters the laser. The target can't be designated through it.
