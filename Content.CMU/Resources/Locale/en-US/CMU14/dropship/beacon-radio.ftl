cmu-dropship-radio-location = grid {$x}, {$y}, {$area}
cmu-dropship-radio-unknown-area = unknown area
# Nine-line precedence: critical patients are urgent, conscious ones priority, the dead routine.
cmu-dropship-radio-patient-status = { $state ->
    [dead] ROUTINE, KIA
    [critical] URGENT
   *[other] PRIORITY
}

cmu-dropship-radio-fulton-deployed = FULTON RECOVERY. Package is {$target}, balloon up at {$location}. Recovery window {$seconds} seconds, then the package drops. Over.
cmu-dropship-radio-medevac-ready = MEDEVAC REQUEST. Line 1: {$location}. Line 3: one patient, {$status}. Line 5: one litter, {$patient}. Line 7: beacon. Standing by for pickup until the beacon goes dark. Over.
cmu-dropship-radio-medevac-empty = MEDEVAC BEACON UP. Line 1: {$location}. Line 7: beacon. No patient on the litter, nothing for pickup yet. Out.
cmu-dropship-radio-medevac-patient = MEDEVAC UPDATE. Line 1: {$location}. Line 3: one patient, {$status}. Line 5: one litter, {$patient}. Ready for pickup. Over.
