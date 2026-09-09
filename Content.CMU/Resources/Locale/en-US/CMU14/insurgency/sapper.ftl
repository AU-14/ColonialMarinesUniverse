# CLF Sapper role.
au14-job-name-clfsapper = CLF Sapper
au14-job-description-clfsapper = A guerilla trained in fieldcraft and demolitions. You lay traps, conceal them, and turn the colony's own ground against GOVFOR.
# Radio prefix must NOT out the sapper: undercover CLF roles all read as ordinary civilians on comms
# (same "CIV" prefix as au14-job-prefix-civiliancolonist / the CLF guerilla).
au14-job-prefix-clfsapper = CIV

# Sapper trap deploy / disarm feedback.
insfor-sapper-trap-deployed = You plant the charge and it settles out of sight.
insfor-sapper-trap-disarmed = You cut the tripline and pack the charge away.
insfor-sapper-trap-deploy-container = You can't plant that in here.
insfor-sapper-trap-deploy-occupied = There's already a trap on this tile.
insfor-sapper-trap-unskilled = You fiddle with it, but you have no idea how to set this up.

# Two-part tripwire.
insfor-sapper-tripwire-attached = You lash the explosive onto the tripwire charge.
insfor-sapper-tripwire-full = There's no room to attach another explosive.
insfor-sapper-tripwire-need-explosive = You need to attach an explosive to the tripwire before you can plant it.
insfor-sapper-tripwire-place-other-end = You plant the charge. Now carry the wire to where you want it to end - up to { $range } tiles away, in a straight line and in sight - and use it there.
insfor-sapper-tripwire-strung = The tripwire is strung tight and set.
insfor-sapper-tripwire-charge-gone = The charge this wire runs back to is gone.
insfor-sapper-tripwire-bad-spot = You can't run the wire out here.
insfor-sapper-tripwire-too-close = You're standing right on top of the charge.
insfor-sapper-tripwire-not-straight = The wire has to run in a straight line from the charge.
insfor-sapper-tripwire-too-far = That's too far from the charge to reach.
insfor-sapper-tripwire-no-los = There's no clear line back to the charge.
insfor-sapper-tripwire-eject-verb = Remove explosive
insfor-sapper-tripwire-ejected = You take the explosive back off the charge.

# Audio (early-warning) trap.
insfor-sapper-audio-name-title = Audio Trap
insfor-sapper-audio-name-prompt = Name this trap
insfor-sapper-audio-default-name = Unnamed
insfor-sapper-audio-location-unknown = unknown location
insfor-sapper-audio-radio-alert = {$name} audio trap has been triggered. Location: {$location}.

# Sapper's Workbench.
insfor-sapper-workbench-deployed = You unfold the workbench and lock its legs down.
insfor-sapper-workbench-need-materials = The bench is missing materials or loose ingredients (place ingredient items on or next to it).
insfor-sapper-workbench-crafted = You build {$item}.

# The "Switch" auto-sear chip.
au14-switch-on = You flip the switch. The trigger group stops caring.
au14-switch-off = You flip the switch back off.
au14-switch-jammed = The action seizes - the gun is jammed!
au14-switch-exploded = The gun tears itself apart in your hands!
au14-switch-jammed-shoot = The gun is jammed! Rack it clear first.
au14-switch-rack-verb = Rack the gun (clear jam)
au14-switch-rack-fail = You rack the action, but the casing stays wedged in.
au14-switch-rack-success = The mangled casing flies clear. The gun will fire again.

# Workbench gunsmithing (force attach/detach).
insfor-sapper-workbench-weapon-placed = You lay the weapon out on the bench.
insfor-sapper-workbench-weapon-occupied = There's already a weapon on the bench.
insfor-sapper-workbench-no-weapon = Lay a weapon on the bench first.
insfor-sapper-workbench-slots-full = Every slot on this weapon is already filled.
insfor-sapper-workbench-attached = You force the attachment into place ({$slot}).
insfor-sapper-workbench-wrong-slot = That attachment doesn't fit any slot this weapon has.
insfor-sapper-workbench-hold-attachment = Hold an attachment first.
insfor-sapper-workbench-take-weapon = Take weapon
insfor-sapper-workbench-detach = Detach: {$name}

# ATM hacking.
insfor-sapper-atm-already-hacked = This machine has already been bled dry.
insfor-sapper-atm-hacked = The ATM shudders and spits out {$amount} in cash.
insfor-sapper-atm-malfunction = ERROR: THIS DEVICE HAS MALFUNCTIONED. PLEASE CONTACT YOUR ADMINISTRATOR.
insfor-sapper-console-drained = The console's funds drain away - {$amount} in cash spills out.
insfor-sapper-asrs-drained = The ASRS account empties into your hands - {$amount} in cash.
insfor-sapper-asrs-empty = This terminal has no funds to siphon.

# Spy-camera network.
device-frequency-prototype-name-surveillance-camera-clf = CLF Spy Cameras

# Snare trap.
insfor-sapper-snare-caught = A snare whips tight around you, binding your hands and flipping the world!
insfor-sapper-snare-struggled-free = You wrench yourself free of the snare.
insfor-sapper-snare-cutting = You start cutting them free of the snare.
insfor-sapper-snare-cut-free = The snare is cut away and you drop free.
