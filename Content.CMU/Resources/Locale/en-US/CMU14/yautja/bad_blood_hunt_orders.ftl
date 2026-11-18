## CMU14 FLOW-703..707: Hunt Orders. The Clan Leader (off-screen) orders the clan; the Hunt Leader leads it

ent-CMUBadBloodHuntGround = Hunt Ground
    .desc = A bone stake hammered into the earth, daubed with fresh blood. The clan has been ordered to claim this ground.
    .suffix = Bad Blood

ent-CMUActionBadBloodPetitionClanLeader = Petition the Clan Leader
    .desc = Ask the Clan Leader for a different Hunt Ground in place of an order the clan has not started on. Costs the clan Superiority Points, and the Clan Leader hears one petition every ten minutes.

# the clan
cmu-bad-blood-hunt-order-issued-clan = The Clan Leader commands: seize the ground at { $area }. Stand on it with no prey near and a Clan Totem rises there. The order stands for { $minutes } minutes.
cmu-bad-blood-hunt-order-contested-clan = The prey stands on the Hunt Ground at { $area }. The claim does not move while they are there. Drive them off.
cmu-bad-blood-hunt-order-seized-clan = The ground at { $area } is the clan's. The Clan Leader grants { $sp } Superiority Points, and Honour to the { $hunters } who held it.
cmu-bad-blood-hunt-order-seized-honour = You held the ground. +{ $honour } Honour.
cmu-bad-blood-hunt-order-lapsed-clan = The Clan Leader's order for { $area } has lapsed. Another will come.

# the Hunt Leader's pick
cmu-bad-blood-hunt-order-pick-title = The Clan Leader's order
cmu-bad-blood-hunt-order-pick-message = The Clan Leader names two grounds. Choose where the clan strikes. In { $seconds } seconds the first is taken.
cmu-bad-blood-hunt-order-pick-option = { $area } ({ $distance } tiles from the Lodge)
cmu-bad-blood-hunt-order-pick-popup = The Clan Leader names two grounds. Choose one within { $seconds } seconds.

# the Hunt Leader's petition
cmu-bad-blood-hunt-order-petition-not-leader = Only the Hunt Leader may petition the Clan Leader.
cmu-bad-blood-hunt-order-petition-cooldown = The Clan Leader will not hear another petition for { $seconds } seconds.
cmu-bad-blood-hunt-order-petition-no-order = There is no order the clan has not started on.
cmu-bad-blood-hunt-order-petition-cannot-afford = A petition costs the clan { $cost } Superiority Points.
cmu-bad-blood-hunt-order-petition-no-lodge = The Clan Leader hears no petition while the clan has no Lodge.
cmu-bad-blood-hunt-order-petition-no-site = The Clan Leader has no other ground to give.
cmu-bad-blood-hunt-order-petition-granted-clan = The Clan Leader grants the Hunt Leader's petition. The order for { $area } is withdrawn.

# examine
cmu-bad-blood-hunt-ground-examine-clan = The clan's claim on this ground is [color=#c9a227]{ $percent }%[/color]. The order stands for [color=#c9a227]{ $minutes }[/color] more minutes.
cmu-bad-blood-hunt-ground-examine-contested = [color=red]The prey stands on it: the claim does not move.[/color]
cmu-bad-blood-hunt-ground-examine-marine = [color=#c9a227]A bloodied bone stake. The hunters mean to claim this ground: something of theirs rises here if they hold it. Its markings are { $percent }% complete. Holding the ground yourself scrapes them away.[/color]

# marines
cmu-bad-blood-hunt-ground-marine-enter = Fresh blood is daubed on a bone stake here. The hunters want this ground.
cmu-bad-blood-hunt-order-issued-marines = Sensors register unknown emitter activity near { $area }.
