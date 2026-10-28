## CMU14 evolution queue: waiting in line for contested tier slots

# Evolutions UI
cmu-xeno-ui-evolution-queue-choice = {$name} (queue)

# Popups for the waiting xeno
cmu-xeno-evolution-queue-joined = We will wait in line to take shape at tier {$tier}. We are number {$position} in the queue.
cmu-xeno-evolution-queue-left = We no longer wait in the tier {$tier} evolution queue.
cmu-xeno-evolution-queue-removed = We have been removed from the tier {$tier} evolution queue!
cmu-xeno-evolution-failed-queue = We must wait our turn! {$ahead ->
    [one] One of our sisters is
   *[other] {$ahead} of our sisters are
} ahead of us in the evolution queue.

# Hive management dialogs
cmu-xeno-evolution-queue-manage = Evolution Queue
cmu-xeno-evolution-queue-title = Evolution Queue
cmu-xeno-evolution-queue-message = Choose a tier queue to manage. Contested tier slots open in queue order.
cmu-xeno-evolution-queue-empty = No sisters are waiting in the evolution queue.
cmu-xeno-evolution-queue-tier-option = Tier {$tier} — {$count} waiting
cmu-xeno-evolution-queue-tier-title = Tier {$tier} Evolution Queue
cmu-xeno-evolution-queue-tier-message = Choose a sister to reorder or remove. The front of the queue evolves first.
cmu-xeno-evolution-queue-tier-empty = No sisters are waiting in the tier {$tier} evolution queue.
cmu-xeno-evolution-queue-member-option = {$position}. {$name} — {$caste}{$leader ->
    [true] {" (Leader)"}
   *[false] {""}
}
cmu-xeno-evolution-queue-member-message = Reorder {$name} in the evolution queue.
cmu-xeno-evolution-queue-move-front = Move to front
cmu-xeno-evolution-queue-move-up = Move up
cmu-xeno-evolution-queue-move-down = Move down
cmu-xeno-evolution-queue-remove = Remove from queue
