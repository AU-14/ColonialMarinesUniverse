## RTO close air support requests

cmu-cas-requests-title = CAS REQUESTS
cmu-cas-requests-none = NO OPEN REQUESTS
cmu-cas-request-card = #{$id} {$callsign} | {$status}
    TGT: {$target} @ {$location}
    DESC: {$description}
    FRND: {$friendlies}
    ORD: {$munition}{$danger}
cmu-cas-request-card-danger = {" "}| DANGER CLOSE
cmu-cas-request-accept = ACCEPT
cmu-cas-request-deny = DENY
cmu-cas-request-complete = ROUNDS COMPLETE
cmu-cas-status-pending = PENDING
cmu-cas-status-accepted = ACCEPTED
cmu-cas-status-denied = DENIED
cmu-cas-status-clearedhot = CLEARED HOT
cmu-cas-status-complete = COMPLETE
cmu-cas-request-duplicate = You already have an open CAS request on that target.
cmu-cas-request-full = Too many open CAS requests. Clear or abort an existing request first.
cmu-cas-request-no-target = No CAS mark within twelve tiles. Throw a signal flare or laser a target first.
cmu-cas-request-sent = CAS request #{$id} transmitted.
cmu-cas-request-not-rto = You need a long-range radio pack to request close air support.

cmu-cas-verb-category = Request CAS
cmu-cas-verb-request = {$munition}
cmu-cas-verb-clear-hot = Clear hot: #{$id} {$target}
cmu-cas-verb-abort = Abort: #{$id} {$target}

cmu-cas-munition-any = Any available
cmu-cas-munition-guns = Guns
cmu-cas-munition-rockets = Rockets
cmu-cas-munition-bombs = Bombs
cmu-cas-munition-lasers = Lasers

cmu-cas-target-none = no hostiles observed
cmu-cas-target-hostiles = {$count ->
    [one] one hostile in the open
   *[other] {$count} hostiles in the open
}
cmu-cas-friendlies-none = none within fifteen tiles
cmu-cas-friendlies-nearest = nearest {$distance} tiles {$direction}

cmu-cas-radio-request = {$callsign}, CAS request {$id}. Mark: {$target}, {$location}. Target: {$description}. Friendlies: {$friendlies}. Ordnance: {$munition}.{$danger} Awaiting acknowledgement.
cmu-cas-radio-danger-close = {" "}DANGER CLOSE.
cmu-cas-radio-cleared = {$callsign}: {$aircraft}, you are CLEARED HOT on {$target}.
cmu-cas-radio-abort = {$callsign}: ABORT, ABORT, ABORT on request {$id}. Hold fire.
cmu-cas-radio-accepted = {$aircraft}: copy {$callsign}, request {$id} on {$target} accepted. Inbound, standby for cleared hot.
cmu-cas-radio-denied = {$aircraft}: {$callsign}, unable request {$id}. Negative support at this time.
cmu-cas-radio-complete = {$aircraft}: {$callsign}, rounds complete on {$target}. {$bda}
cmu-cas-bda = Request BDA: {$down} down, {$standing} still standing.
cmu-cas-bda-unknown = Request BDA.

cmu-cas-danger-close-warning = DANGER CLOSE: {$count ->
    [one] one friendly
   *[other] {$count} friendlies
} near the impact point. Fire again within {$seconds} seconds to confirm.

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

cmu-mohawk-heat = BARREL HEAT: {$heat}%
cmu-mohawk-heat-overheated = BARREL HEAT: {$heat}% - OVERHEATED
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

