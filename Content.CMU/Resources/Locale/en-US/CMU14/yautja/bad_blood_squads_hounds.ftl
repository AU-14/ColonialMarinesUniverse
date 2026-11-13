# Reinforcement squads

ent-CMUBadBloodSquadSeats = Bad Blood reinforcements
    .desc = Seats in a squad the clan has called to its Lodge.

cmu-bad-blood-squad-ghost-name = Bad Blood reinforcement
cmu-bad-blood-squad-ghost-description = The Bad Blood clan has called a squad to its Lodge. Win a seat and you arrive with the rest of the squad as a Bad Blood grunt.
cmu-bad-blood-squad-ghost-rules = You are a Bad Blood: a dishonoured Yautja, hostile to everyone but your clan. Follow the clan's Leader and defend the Lodge.

cmu-bad-blood-squad-verb = Call a squad of { $seats } ({ $cost } SP)
cmu-bad-blood-squad-verb-message = Spend the clan's Superiority Points to call reinforcements to the Lodge.
cmu-bad-blood-squad-leader-only = Only the clan's Leader calls reinforcements.
cmu-bad-blood-squad-no-lodge = The clan has no Lodge to call reinforcements to.
cmu-bad-blood-squad-cap = The clan cannot take that many more hunters.
cmu-bad-blood-squad-cannot-afford = The clan needs { $cost } Superiority Points for that squad.
cmu-bad-blood-squad-called = Reinforcements have been called to the Lodge: { $seats } { $seats ->
        [one] seat
       *[other] seats
    } to fill.
cmu-bad-blood-squad-arrived = { $count } { $count ->
        [one] reinforcement has
       *[other] reinforcements have
    } arrived at the Lodge.
cmu-bad-blood-squad-unclaimed = { $count } { $count ->
        [one] seat
       *[other] seats
    } in the squad went unanswered.
cmu-bad-blood-squad-unclaimed-refund = { $count } { $count ->
        [one] seat
       *[other] seats
    } in the squad went unanswered. { $refund } Superiority Points return to the clan.

cmu-bad-blood-squad-seat-won = You hold a seat in the Bad Blood squad. You arrive at the Lodge with the rest of the squad, in { $seconds } seconds at the latest.
cmu-bad-blood-squad-seat-closed = That squad has no seat left.
cmu-bad-blood-squad-seat-held = You already hold a seat in a Bad Blood squad.
cmu-bad-blood-squad-seat-hunter = You hunted with honour this round. The Bad Bloods will not have you.
cmu-bad-blood-squad-seat-reserve-only = These seats are held for the Blood Reserve.
cmu-bad-blood-squad-seat-priority = The Blood Reserve and the clan's fallen choose first. Ask again in a moment.

# Hounds

ent-CMUMobYautjaBadBloodHound = clan hound
    .desc = A scarred hunting beast that answers to the Bad Bloods now.
    .suffix = Yautja, Bad Blood

ent-CMUBadBloodSleepingHound = sleeping clan hound
    .desc = One of the clan's hounds, asleep. A Bad Blood could wake it.
    .suffix = Bad Blood

ent-CMUBadBloodHoundObservationPad = clan hound observation pad
    .desc = A scavenged camera pad, tuned to the clan's hounds.
    .suffix = Yautja, Bad Blood

ent-CMUBadBloodHoundObservationPadInternalCamera = Clan Hound Observation Interface
    .desc = If you can see this, someone messed up.

cmu-bad-blood-houndpad-interface-title = Clan Hound Observation
cmu-bad-blood-hound-ghost-name = Bad Blood clan hound
cmu-bad-blood-hound-ghost-description = A hunting hound woken by a Bad Blood. You are bound to the one who woke you.
cmu-bad-blood-hound-ghost-rules = You are a Bad Blood's hound. Obey your master, stay with the clan and attack whatever they hunt.

cmu-bad-blood-hound-verb = Call a hound ({ $cost } SP)
cmu-bad-blood-hound-verb-message = Spend the clan's Superiority Points on a sleeping hound. Only you can wake it at first.
cmu-bad-blood-hound-no-kennel = The clan has no kennel to keep a hound in.
cmu-bad-blood-hound-cap = The clan cannot keep any more hounds.
cmu-bad-blood-hound-cannot-afford = The clan needs { $cost } Superiority Points for a hound.
cmu-bad-blood-hound-bought = A hound sleeps at the kennel. It is yours alone to wake for { $seconds } seconds.
cmu-bad-blood-hound-sleeper-not-clan = The hound does not know you. It sleeps on.
cmu-bad-blood-hound-sleeper-reserved = This hound was called by another of the clan. Leave it to them for now.
cmu-bad-blood-hound-seat-hunter = You hunted with honour this round. The clan's hounds will not answer you.
cmu-bad-blood-hound-seat-reserve = The Blood Reserve chooses first. Ask again in a moment.
cmu-bad-blood-hound-seat-priority = The clan's fallen choose first. Ask again in a moment.
