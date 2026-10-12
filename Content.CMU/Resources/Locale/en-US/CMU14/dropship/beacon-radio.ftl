cmu-dropship-radio-location = {$area} ({$x}, {$y})
cmu-dropship-radio-unknown-area = an unknown area
cmu-dropship-radio-patient-status = { $state ->
    [dead] dead
    [critical] in critical condition
   *[other] conscious
}

cmu-dropship-radio-fulton-deployed = Fulton deployed on {$target} at {$location}. Ready for dropship recovery for the next {$seconds} seconds, then it drops back to the ground.
cmu-dropship-radio-medevac-ready = Medevac beacon active at {$location} with {$patient} aboard, {$status}. Ready for dropship pickup until the beacon is switched off.
cmu-dropship-radio-medevac-empty = Medevac beacon active at {$location}. No patient on the stretcher yet, so there's nothing to pick up.
cmu-dropship-radio-medevac-patient = {$patient} is strapped onto the medevac stretcher at {$location}, {$status}. Ready for dropship pickup.
