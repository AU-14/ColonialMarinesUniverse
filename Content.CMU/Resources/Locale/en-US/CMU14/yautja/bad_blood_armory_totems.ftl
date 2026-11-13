## The Lodge armory

ent-CMUBadBloodArmoryTrapsBundle = hunting traps
    .desc = Two hunting traps.

ent-CMUBadBloodWristBladesAttachment = { ent-CMUYautjaWristBladesAttachment }
    .desc = { ent-CMUYautjaWristBladesAttachment.desc }
    .suffix = Bad Blood

ent-CMUBadBloodScimitarAttachment = { ent-CMUYautjaScimitarAttachment }
    .desc = { ent-CMUYautjaScimitarAttachment.desc }
    .suffix = Bad Blood

ent-CMUBadBloodChainGauntletAttachment = { ent-CMUYautjaChainGauntletsAttachment }
    .desc = { ent-CMUYautjaChainGauntletsAttachment.desc }
    .suffix = Bad Blood

ent-CMUBadBloodArmoredMask = armored clan mask
    .desc = A clan mask backed with scavenged plating.
    .suffix = Bad Blood, Armored

ent-CMUBadBloodLeaderArmor = clan leader's armor
    .desc = Heavy plate of the kind a clan's Leader wears.
    .suffix = Bad Blood, Leader

ent-CMUBadBloodLethalUnlock = lethal bolt
    .desc = The clan's plasma casters are tuned for the lethal bolt. Every Bad Blood may fire it.

ent-CMUBadBloodEradicatorUnlock = eradicator mode
    .desc = The clan's plasma caster is tuned for the eradicator. Only the Leader may fire it.

ent-CMUBadBloodScavengedSpike = scrap spike

ent-CMUBadBloodScavengedSpikeLauncher = scavenged spike launcher
    .desc = A spike launcher with a cracked housing and a magazine patched from scrap. It holds half of what it should and recharges slowly.
    .suffix = Bad Blood

cmu-bad-blood-armory-not-clan = The armory answers only to the clan.
cmu-bad-blood-armory-tier-locked = The clan has not earned this yet. It needs armory tier { $tier }.
cmu-bad-blood-armory-leader-only = Only the clan's Leader may take this.
cmu-bad-blood-armory-already-unlocked = The clan already has the eradicator.
cmu-bad-blood-armory-lethal-already-unlocked = The clan already has the lethal bolt.
cmu-bad-blood-armory-needs-lethal = The clan must earn the lethal bolt before the eradicator.
cmu-bad-blood-armory-lethal-unlocked = The clan has earned the lethal bolt. Every plasma caster can fire it.
cmu-bad-blood-armory-not-enough = The clan cannot pay for this. It costs { $price } Superiority Points and the clan has { $sp }.
cmu-bad-blood-armory-eradicator-unlocked = The clan has earned the eradicator. The Leader's plasma caster can fire it.

cmu-bad-blood-lethal-locked = The clan has not earned the lethal bolt. Any Bad Blood can buy it at the Lodge armory.
cmu-bad-blood-eradicator-locked = The eradicator is not yours to fire. The clan's Leader buys it at the Lodge armory.
cmu-bad-blood-eradicator-cooldown = The eradicator is still cooling. Wait { $seconds } more seconds.

## Clan Totems

ent-CMUBadBloodTotem = Clan Totem
    .desc = A spear driven into the ground and hung with skulls. The clan that raised it claims everything around it.
    .suffix = Bad Blood

ent-CMUActionBadBloodRaiseTotem = Raise Clan Totem
    .desc = Raise a Clan Totem where you stand. It needs open sky, and distance from the Lodge, other Totems and the prey's landing zones. The clan's first is free, the rest cost Superiority Points.

ent-CMUBadBloodTotemObjective = Destroy the Clan Totems
    .desc = Find and destroy the totems the hostile clan has raised in the area of operations.

cmu-bad-blood-totem-not-clan = Only a Bad Blood of a standing clan can raise a Clan Totem.
cmu-bad-blood-totem-limit = The clan holds as many Clan Totems as it can.
cmu-bad-blood-totem-roofed = A Clan Totem needs open sky. You cannot raise it under a roof.
cmu-bad-blood-totem-near-lodge = This is too close to the Lodge. Take the clan's claim further out.
cmu-bad-blood-totem-near-totem = This is too close to another Clan Totem.
cmu-bad-blood-totem-near-landing-zone = This is too close to the prey's landing zone to raise a Clan Totem.
cmu-bad-blood-totem-cannot-afford = The clan cannot pay for another Clan Totem.
cmu-bad-blood-totem-raising = You start raising a Clan Totem.
cmu-bad-blood-totem-raised-clan = A Clan Totem stands at { $area }. Hold it and it feeds the clan.
cmu-bad-blood-totem-raised-marines = Hostile structure detected at { $area }. Unknown hunters are marking territory.
cmu-bad-blood-totem-destroyed-clan = The Clan Totem at { $area } has fallen! The prey now knows where the Lodge stands.
cmu-bad-blood-totem-stage-young = young
cmu-bad-blood-totem-stage-growing = growing
cmu-bad-blood-totem-stage-ripe = ripe
cmu-bad-blood-totem-examine = This Clan Totem is [color=#c9a227]{ $stage }[/color]. It pays the clan [color=#c9a227]{ $rate }[/color] Superiority Points a minute and has paid [color=#c9a227]{ $paid }[/color] so far.
cmu-bad-blood-totem-examine-contested = [color=red]Prey stands near it. A contested Totem pays nothing.[/color]
cmu-bad-blood-totem-paid = The Clan Totem has fed the clan { $paid } Superiority Points.
cmu-bad-blood-totem-destroyed-marines = The hostile totem at { $area } has been destroyed. Its fall has exposed the hunters' camp on the tactical map.
