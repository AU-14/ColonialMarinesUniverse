cmu-emote-name-pushups = Pushups
cmu-emote-msg-pushups = drops down and starts doing pushups.
cmu-emote-name-knee-pushups = Knee Pushups
cmu-emote-msg-knee-pushups = gets down on hands and knees and starts doing pushups.
cmu-emote-name-situps = Situps
cmu-emote-msg-situps = lies back and starts doing situps.

cmu-exercise-cant = You can't do that right now.

cmu-pushups-count = has done {$count} {$count ->
    [one] pushup
   *[other] pushups
}.
cmu-pushups-fail-emote = gets up, unable to do another pushup after {$count} {$count ->
    [one] rep
   *[other] reps
}!
cmu-pushups-fail = Your arms give out! You managed {$count} {$count ->
    [one] pushup
   *[other] pushups
}.
cmu-pushups-offer-knees = You can drop to your knees and keep going. Do knee pushups now, or you'll get back up.
cmu-pushups-stop = You stop doing pushups after {$count} {$count ->
    [one] rep
   *[other] reps
}.

cmu-situps-count = has done {$count} {$count ->
    [one] situp
   *[other] situps
}.
cmu-situps-fail-emote = gets up, unable to do another situp after {$count} {$count ->
    [one] rep
   *[other] reps
}!
cmu-situps-fail = Your core gives out! You managed {$count} {$count ->
    [one] situp
   *[other] situps
}.
cmu-situps-stop = You stop doing situps after {$count} {$count ->
    [one] rep
   *[other] reps
}.

cmu-kneepushups-count = has done {$count} {$count ->
    [one] pushup
   *[other] pushups
} on {POSS-ADJ($entity)} knees.
cmu-kneepushups-fail-emote = collapses, unable to do another pushup on {POSS-ADJ($entity)} knees after {$count} {$count ->
    [one] rep
   *[other] reps
}!
cmu-kneepushups-fail = You're spent! You managed {$count} knee {$count ->
    [one] pushup
   *[other] pushups
}.
cmu-kneepushups-stop = You stop doing knee pushups after {$count} {$count ->
    [one] rep
   *[other] reps
}.
