tiles-cmu-arena = arena floor
tiles-cmu-arena-tile = arena tile

cmu-treadmill-in-use = Someone is already running on {THE($treadmill)}.

cmu-exercise-start-machine = gets on {THE($machine)} and starts working out.
cmu-exercise-start-curls = starts curling {THE($dumbbell)}.
cmu-exercise-start-treadmill = gets on {THE($treadmill)} and starts running.

cmu-stacklifter-count = has done {$count} {$count ->
    [one] rep
   *[other] reps
} on the weight machine.
cmu-stacklifter-fail-emote = lets go of the weights, unable to do another rep after {$count} {$count ->
    [one] rep
   *[other] reps
}!
cmu-stacklifter-fail = Your arms give out! You managed {$count} {$count ->
    [one] rep
   *[other] reps
}. You feel stronger!
cmu-stacklifter-stop = You stop lifting after {$count} {$count ->
    [one] rep
   *[other] reps
}.

cmu-benchpress-count = has pressed {$count} {$count ->
    [one] rep
   *[other] reps
}.
cmu-benchpress-fail-emote = racks the bar, unable to press it again after {$count} {$count ->
    [one] rep
   *[other] reps
}!
cmu-benchpress-fail = Your chest gives out! You managed {$count} {$count ->
    [one] rep
   *[other] reps
}.
cmu-benchpress-stop = You rack the bar after {$count} {$count ->
    [one] rep
   *[other] reps
}.

cmu-curls-count = has done {$count} {$count ->
    [one] curl
   *[other] curls
}.
cmu-curls-fail-emote = lowers the dumbbell, unable to curl it again after {$count} {$count ->
    [one] rep
   *[other] reps
}!
cmu-curls-fail = Your biceps give out! You managed {$count} {$count ->
    [one] curl
   *[other] curls
}.
cmu-curls-popup-self = You do your {$ordinal} curl.
cmu-curls-popup-others = {CAPITALIZE(THE($user))} does {POSS-ADJ($user)} {$ordinal} curl.
cmu-curls-stop = You stop curling after {$count} {$count ->
    [one] rep
   *[other] reps
}.

cmu-punching-count = has thrown {$count} punches.
cmu-punching-fail-emote = leans on the punching bag, winded after {$count} {$count ->
    [one] punch
   *[other] punches
}!
cmu-punching-fail = You're winded! You threw {$count} {$count ->
    [one] punch
   *[other] punches
}. Your arms need a rest.
cmu-punching-tired = Your arms are too tired to keep punching. Give them a rest.

cmu-running-count = has been running for {$count} seconds.
cmu-running-fail-emote = collapses on the treadmill, exhausted after {$count} {$count ->
    [one] second
   *[other] seconds
} of running!
cmu-running-fail = Your legs give out! You ran for {$count} {$count ->
    [one] second
   *[other] seconds
}.
cmu-running-stop = You step off the treadmill after {$count} {$count ->
    [one] second
   *[other] seconds
} of running.
