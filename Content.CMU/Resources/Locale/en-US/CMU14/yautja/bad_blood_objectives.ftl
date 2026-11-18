# The marine objective chain against the Bad Blood clan: infrastructure gate, clan signature, uplink, field relay.
# BadBloodObjectiveChainSystem, BadBloodFieldRelaySystem

## infrastructure gate
cmu-bad-blood-obj-part-power = colony reactor power
cmu-bad-blood-obj-part-comms = a planetary comms tower
cmu-bad-blood-obj-part-sensor = the GOVFOR sensor tower
cmu-bad-blood-obj-gate-part = Colony infrastructure: {$part} restored. {$have} of the {$need} systems needed for an orbital uplink against the clan are online.
cmu-bad-blood-obj-gate-met = ARES: colony infrastructure restored. An orbital uplink against the clan vessel is now possible. Routes are on the objectives console.

## uplink sites
cmu-bad-blood-obj-site-sensor = sensor tower
cmu-bad-blood-obj-site-comms = communications tower
cmu-bad-blood-obj-site-relay = field uplink relay

cmu-bad-blood-obj-no-rule = There is no clan to track.
cmu-bad-blood-obj-marines-only = You have no idea how to work this.
cmu-bad-blood-obj-site-not-working = This uplink site is down. A tower must be switched On, and a field relay must be planted with charge left.

cmu-bad-blood-obj-uplink-verb = Run uplink
cmu-bad-blood-obj-uplink-verb-message = Send the clan's signature to orbit and lock onto their vessel. Needs the colony infrastructure and the clan signature. Moving or getting hurt interrupts you.
cmu-bad-blood-obj-uplink-done = The uplink has already been run.
cmu-bad-blood-obj-uplink-no-gate = The colony infrastructure is not restored yet. You need two of: a colony reactor running for 5 minutes, a comms tower On, the sensor tower On.
cmu-bad-blood-obj-uplink-no-signature = You have no clan signature to send yet. Survey a defiled Totem or the revealed Lodge, take the clan star chart, or wire a looted Bad Blood bracer into an uplink site.
cmu-bad-blood-obj-uplink-start = You start the uplink. Stay still for {$seconds} seconds; moving or getting hurt breaks it.
cmu-bad-blood-obj-uplink-finished = The uplink is through. This site is now the Vessel Strike lock site.
cmu-bad-blood-obj-uplink-done-announce = Orbital uplink established through the {$site} at {$area}. The clan vessel's coordinates are locked. The CO, XO or a Staff Officer can now fire the Vessel Strike from a GOVFOR overwatch console. Keep the {$site} at {$area} working: it is the lock site.

## clan signature
cmu-bad-blood-obj-avenue-bracer = a reverse-engineered clan bracer
cmu-bad-blood-obj-avenue-totem = a surveyed clan Totem
cmu-bad-blood-obj-avenue-lodge = the clan Lodge
cmu-bad-blood-obj-signature = Clan signature acquired from {$avenue}. Next: run the uplink at a working comms tower, sensor tower or field uplink relay once the colony infrastructure is restored.
cmu-bad-blood-obj-signature-done = The clan signature has already been acquired.

cmu-bad-blood-obj-bracer-slot-full = A bracer is already wired into this site.
cmu-bad-blood-obj-bracer-cannot-insert = The bracer will not fit.
cmu-bad-blood-obj-bracer-inserted = You seat the bracer in the site. Next: cut into it with wirecutters.
cmu-bad-blood-obj-bracer-missing = The bracer is gone.
cmu-bad-blood-obj-bracer-cut = You cut into the bracer's wiring. Next: fasten it down with a wrench.
cmu-bad-blood-obj-bracer-done = The bracer's transmitter is wired in. Its signature is yours.

cmu-bad-blood-obj-survey-verb = Survey Lodge
cmu-bad-blood-obj-survey-verb-message = Record the clan's signature from their Lodge. The Lodge must stay revealed on the tactical map until you finish. Moving or getting hurt interrupts you.
cmu-bad-blood-obj-survey-hidden = The Lodge must be revealed on the tactical map to survey it. Hitting it, decrypting a bracer or destroying a Totem reveals it.
cmu-bad-blood-obj-survey-start = You start surveying the Lodge. Hold still, and keep it revealed until you finish.
cmu-bad-blood-obj-survey-done = You finish surveying the Lodge.

cmu-bad-blood-obj-totem-survey-verb = Survey Totem
cmu-bad-blood-obj-totem-survey-verb-message = Record the clan's signature for the orbital uplink from this defiled totem. Takes 75 seconds; moving or getting hurt interrupts you.
cmu-bad-blood-obj-totem-survey-consecrated = The totem has to be defiled before you can survey it. Use Defile Totem first.
cmu-bad-blood-obj-totem-survey-start = You start surveying the totem. Hold still for {$seconds} seconds.
cmu-bad-blood-obj-totem-survey-done = You finish surveying the totem.

## field uplink relay
cmu-bad-blood-obj-relay-not-kit = That is not a relay kit.
cmu-bad-blood-obj-relay-no-ground = There is no ground here to plant it on.
cmu-bad-blood-obj-relay-roofed = The relay needs open sky above it.
cmu-bad-blood-obj-relay-plant-start = You start setting up the relay.
cmu-bad-blood-obj-relay-planted = The relay is planted. Right-click it to Run uplink, or keep it as a backup site for the Vessel Strike lock.
cmu-bad-blood-obj-relay-empty = The relay's battery dies.
cmu-bad-blood-obj-relay-examine-charge = Its battery has about {$minutes} minutes left.
cmu-bad-blood-obj-relay-examine-empty = Its battery is dead.

## FLOW-609: the GOVFOR objectives console lines (objectiveDescription in objectives.yml)
cmu-bad-blood-obj-desc-gate = Restore two of colony power, communications and the sensor tower
cmu-bad-blood-obj-desc-signature = Acquire the clan's signature from a bracer, a defiled Totem or the Lodge
cmu-bad-blood-obj-desc-uplink = Uplink the clan vessel's coordinates from a tower or a field relay
cmu-bad-blood-obj-desc-strike = Fire the Vessel Strike and hold the lock until the clan vessel is destroyed

## FLOW-609: round-end objective timeline details (BadBloodObjectiveTimelineSystem). A Lodge reveal starts with its
## source's enum name, which the timeline test reads
cmu-bad-blood-timeline-detail-read = read by { $reader }
cmu-bad-blood-timeline-detail-reveal = { $source }, { $seconds }s
cmu-bad-blood-timeline-detail-reveal-held = { $source }, until cleared
cmu-bad-blood-timeline-detail-hold = ship call in { $seconds }s
cmu-bad-blood-timeline-detail-ship = arrives in { $seconds }s
cmu-bad-blood-timeline-detail-by = by { $user }
cmu-bad-blood-timeline-detail-item-by = { $item } by { $user }
