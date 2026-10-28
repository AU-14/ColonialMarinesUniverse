colony-rotation-announcement-sender = Colonial Transit Authority
colony-rotation-pad-unknown = the colony landing pad

colony-rotation-dispatched = The shift is over. The crew rotation transport has been dispatched and will land at {$pad} in {$minutes} {$minutes ->
    [one] minute
   *[other] minutes
}. Anyone rotating out should pack up and make their way there.
colony-rotation-called = Colony administration has requested an early crew rotation. The transport will land at {$pad} in {$minutes} {$minutes ->
    [one] minute
   *[other] minutes
}. Anyone rotating out should pack up and make their way there.
colony-rotation-recalled = The crew rotation transport has been recalled. The next rotation is scheduled in {$minutes} {$minutes ->
    [one] minute
   *[other] minutes
}.
colony-rotation-landed = The crew rotation transport has landed at {$pad}. It leaves in {$minutes} {$minutes ->
    [one] minute
   *[other] minutes
}. Load any goods for the quarterly shipment aboard before then.
colony-rotation-departing-minutes = The crew rotation transport leaves in {$minutes} {$minutes ->
    [one] minute
   *[other] minutes
}. Get aboard if you're rotating out.
colony-rotation-departing-seconds = The crew rotation transport leaves in {$seconds} seconds. Final boarding call.
colony-rotation-departed = The crew rotation transport has lifted off. The shift is over.
colony-rotation-round-end = The crew rotation transport has left the colony.

colony-rotation-call-too-early = The transit authority won't send a transport this early in the shift. Try again in {$minutes} {$minutes ->
    [one] minute
   *[other] minutes
}.
colony-rotation-call-cooldown = The transit authority is still processing the last request.
colony-rotation-recall-cooldown = The transit authority is still processing the last request.
colony-rotation-recall-committed = The transport is already on its way down and can't be recalled.
colony-rotation-access-denied = Access denied. Only colony administration can arrange a crew rotation.

colony-rotation-console-header = Crew Rotation Transport
colony-rotation-console-call = Request Early Rotation
colony-rotation-console-recall = Recall Transport
colony-rotation-console-scheduled = Scheduled to be sent in {$time}
colony-rotation-console-inbound = Inbound, landing in {$time}
colony-rotation-console-approaching = On final approach
colony-rotation-console-docked = On the pad, leaving in {$time}
colony-rotation-console-departing = Lifting off

colony-rotation-summary-rotated = {$count} {$count ->
    [one] colonist
   *[other] colonists
} rotated out on the transport:
colony-rotation-summary-stayed = {$count} {$count ->
    [one] colonist
   *[other] colonists
} stayed behind on the colony.
colony-rotation-summary-no-shipment = Nothing was loaded aboard for the quarterly shipment.
colony-rotation-summary-shipment = The quarterly shipment was worth ${$value}:
colony-rotation-summary-shipment-line = {$count}x {$item}

colony-rotation-command-call-description = Sends the crew rotation transport now, skipping the minimum shift length.
colony-rotation-command-call-help = Usage: rotationcall [minutes until landing]
colony-rotation-command-recall-description = Recalls the inbound crew rotation transport.
colony-rotation-command-recall-help = Usage: rotationrecall
colony-rotation-command-minutes-hint = <minutes until landing>
colony-rotation-command-called = Crew rotation transport dispatched.
colony-rotation-command-recalled = Crew rotation transport recalled.
colony-rotation-command-no-rule = There's no crew rotation transport this round. It only runs in Colony Shift.
colony-rotation-command-already-called = The crew rotation transport is already on its way.
colony-rotation-command-nothing-inbound = There's no inbound crew rotation transport to recall.
