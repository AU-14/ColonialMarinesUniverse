cmu-dropship-radio-location = grid {$x}, {$y}, {$area}
cmu-dropship-radio-unknown-area = unknown area
# Nine-line precedence: critical patients are urgent, conscious ones priority, the dead routine.
cmu-dropship-radio-patient-status = { $state ->
    [dead] ROUTINE, KIA
    [critical] URGENT
   *[other] PRIORITY
}

cmu-dropship-radio-fulton-deployed = FULTON RECOVERY. Package is {$target}, balloon up at {$location}. Recovery window {$seconds} seconds. Over.
cmu-dropship-radio-medevac-ready = MEDEVAC REQUEST. {$location}. One patient, {$status}. One litter, {$patient}. Marked by beacon. Standing by for pickup.
cmu-dropship-radio-medevac-empty = MEDEVAC BEACON UP. {$location}. Marked by beacon. No patient on the litter. Out.
cmu-dropship-radio-medevac-patient = MEDEVAC UPDATE. {$location}. One patient, {$status}. One litter, {$patient}. Ready for pickup. Over.
