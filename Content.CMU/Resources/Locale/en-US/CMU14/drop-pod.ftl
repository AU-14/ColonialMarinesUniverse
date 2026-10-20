cmu-drop-pod-enter-verb = Enter drop pod
cmu-drop-pod-eject-verb = Exit drop pod
cmu-drop-pod-enter = {CAPITALIZE(THE($target))} climbs into the drop pod.
cmu-drop-pod-examine-state = Status: [color=yellow]{$state ->
    [Ready] ready for launch
    [InFlight] in flight
    *[Landed] spent
}[/color]
cmu-drop-pod-weapon-slot = Weapon rack
cmu-drop-pod-transit-map = Drop pod transit

cmu-drop-pod-not-ready = {CAPITALIZE(THE($pod))} is not ready to launch.
cmu-drop-pod-no-occupant = {CAPITALIZE(THE($pod))} has no one inside.
cmu-drop-pod-no-landing = No safe landing zone found for {THE($pod)}.

cmu-drop-pod-phase-launch = The pod's clamps release and you rocket away from the ship!
cmu-drop-pod-phase-atmosphere = The hull screams as the pod tears through the atmosphere!
cmu-drop-pod-phase-chutes = Retro-thrusters fire. Brace for impact!
cmu-drop-unit-phase-launch = Docking clamps release and the deck lurches beneath you!
cmu-drop-pod-breakup-atmosphere = Something tears loose! The pod is breaking apart!
cmu-drop-pod-breakup-impact = {CAPITALIZE(THE($pod))} shatters on impact!
cmu-drop-pod-injured = The landing knocks you around badly.
cmu-drop-pod-injured-treatment = You're hurt. Stay in the pod to let its medical system patch you up, or move to climb out.
cmu-drop-pod-treatment-complete = The pod's medical system finishes its treatment.

cmu-drop-unit-invalid-grid = The drop unit core is not on a grid.
cmu-drop-unit-breach = The drop unit is not sealed: there is a hole in its floor.
cmu-drop-unit-too-large = The drop unit is not sealed by walls and doors, or is larger than {$max} tiles.
cmu-drop-unit-no-door = The drop unit has no door.
cmu-drop-unit-grid-aboard = Something too large to carry is inside the drop unit.
cmu-drop-unit-aborted = Drop aborted: {$reason}
cmu-drop-unit-breakup-atmosphere = The walls groan and plating shears away! The drop unit is breaking apart!
cmu-drop-unit-breakup = The drop unit's walls buckle and break apart around you!

cmu-drop-pod-console-title = Drop Pod Control
cmu-drop-pod-console-map-hint = Click the map to set the insertion point. Scroll to zoom, right-drag to pan.
cmu-drop-pod-console-insertion = Insertion point
cmu-drop-pod-console-target-hint = Drop units scatter around this point.
cmu-drop-pod-console-blind-hint = No point selected: the salvo will blind drop together at a random location.
cmu-drop-pod-console-no-blind-hint = Select an insertion point before firing.
cmu-drop-pod-console-clear-target = Clear insertion point
cmu-drop-pod-console-no-pods = No linked drop units.
cmu-drop-pod-console-pods = Linked drop units
cmu-drop-pod-console-select-all = Select all
cmu-drop-pod-console-refresh = Refresh
cmu-drop-pod-console-fire = Fire selected
cmu-drop-pod-console-fire-blind = Blind drop selected
cmu-drop-pod-console-target = Insertion point: {$x}, {$y}
cmu-drop-pod-console-no-target-set = No insertion point selected
cmu-drop-pod-console-cooldown-remaining = Recharging: {$seconds}s
cmu-drop-pod-console-ready = Launch systems ready
cmu-drop-pod-console-empty = empty
cmu-drop-pod-console-entry = {$name} - {$occupant} - {$weapons} {$weapons ->
    [one] weapon
    *[other] weapons
}
cmu-drop-pod-console-entry-custom = {$name} (custom unit)
cmu-drop-pod-console-cooldown = The launch systems are still recharging.
cmu-drop-pod-console-no-target = Select an insertion point first.
cmu-drop-pod-console-no-blind-target = No landable location found for a blind drop.
cmu-drop-pod-console-launched-blind = Blind dropped {$count} drop {$count ->
    [one] unit
    *[other] units
} at a random location.
cmu-drop-pod-console-launched = Launched {$count} drop {$count ->
    [one] unit
    *[other] units
}.
