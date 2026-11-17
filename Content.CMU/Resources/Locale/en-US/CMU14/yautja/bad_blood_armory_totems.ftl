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
cmu-bad-blood-armory-not-enough = You cannot pay for this. It costs { $price } Honour and you have { $sp }. Offer trophies at the Lodge to earn more.
cmu-bad-blood-armory-forge-nothing = The forge needs a bracer weapon you chose and have not forged yet. Wear your bracer.
cmu-bad-blood-armory-forged = The forge reworks your bracer weapon. It hits harder now.
cmu-bad-blood-forged-examine = It has been [color=orange]forged[/color] at the clan's Lodge and hits a fifth harder.
cmu-bad-blood-armory-eradicator-unlocked = The clan has earned the eradicator. The Leader's plasma caster can fire it.

cmu-bad-blood-lethal-locked = The clan has not earned the lethal bolt. The Leader buys it for the whole clan at the Lodge armory.
cmu-bad-blood-eradicator-locked = The eradicator is not yours to fire. The clan's Leader buys it at the Lodge armory.
cmu-bad-blood-eradicator-cooldown = The eradicator is still cooling. Wait { $seconds } more seconds.

## Clan Totems

ent-CMUBadBloodTotem = Clan Totem
    .desc = A spear driven into the ground and hung with skulls. The clan that raised it claims everything around it.
    .suffix = Bad Blood

ent-CMUActionBadBloodRaiseTotem = Raise Clan Totem
    .desc = Raise a Clan Totem where you stand. It needs open sky, and distance from the Lodge, other Totems and the prey's landing zones. The clan's first is free, the rest cost Superiority Points.

ent-CMUBadBloodTotemObjective = Destroy the Clan Totems
    .desc = Find and destroy the totems the hostile clan has raised. Each one destroyed reveals their Lodge on the tactical map for 90 seconds.

cmu-bad-blood-totem-not-clan = Only a Bad Blood of a standing clan can raise a Clan Totem.
cmu-bad-blood-totem-limit = The clan holds as many Clan Totems as it can.
cmu-bad-blood-totem-roofed = A Clan Totem needs open sky. You cannot raise it under a roof.
cmu-bad-blood-totem-near-lodge = This is too close to the Lodge. Take the clan's claim further out.
cmu-bad-blood-totem-near-totem = This is too close to another Clan Totem.
cmu-bad-blood-totem-near-landing-zone = This is too close to the prey's landing zone to raise a Clan Totem.
cmu-bad-blood-totem-cannot-afford = The clan cannot pay for another Clan Totem.
cmu-bad-blood-totem-raising = You start raising a Clan Totem.
cmu-bad-blood-totem-raised-clan = A Clan Totem stands at { $area }. It feeds the clan until the prey desecrates it.
cmu-bad-blood-totem-raised-marines = Hostile clan totem raised at { $area }. It feeds the hunters while it stands and shows on the tactical map in 2 minutes. Desecrate it (right-click, 45 seconds) or destroy it.
cmu-bad-blood-totem-destroyed-clan = The Clan Totem at { $area } has fallen! The prey now knows where the Lodge stands.
cmu-bad-blood-totem-stage-young = young
cmu-bad-blood-totem-stage-growing = growing
cmu-bad-blood-totem-stage-ripe = ripe
cmu-bad-blood-totem-examine = This Clan Totem is [color=#c9a227]{ $stage }[/color]. It pays the clan [color=#c9a227]{ $rate }[/color] Superiority Points a minute and has paid [color=#c9a227]{ $paid }[/color] so far.
cmu-bad-blood-totem-paid = The Clan Totem has fed the clan { $paid } Superiority Points.
cmu-bad-blood-totem-destroyed-marines = The hostile totem at { $area } has been destroyed. The hunters' Lodge is on the tactical map for 90 seconds: survey it or strike it now.

# FLOW-503: desecration
cmu-bad-blood-totem-desecrated-marines = The hostile totem at { $area } is desecrated and feeds the hunters nothing. Right-click it: Read the totem for the Lodge's location, or Survey Totem for the clan signature. The hunters can reconsecrate it, so hold it or destroy it.
cmu-bad-blood-totem-desecrated-clan = The prey has desecrated the Clan Totem at { $area }! It feeds the clan nothing until one of you reconsecrates it (right-click it).
cmu-bad-blood-totem-reconsecrated-marines = The hunters have reconsecrated the totem at { $area }. It feeds them again. Desecrate it again or destroy it.
cmu-bad-blood-totem-reconsecrated-clan = The Clan Totem at { $area } is consecrated again.
cmu-bad-blood-totem-examine-desecrated = [color=red]The prey has desecrated it. It pays nothing until a Bad Blood reconsecrates it: right-click it and hold still for 45 seconds.[/color]
cmu-bad-blood-totem-examine-marine = [color=#c9a227]A hostile clan totem. It feeds the hunters while it stands. Right-click it and choose [bold]Desecrate Totem[/bold], then hold still for 45 seconds. Or destroy it outright: that is permanent and reveals their Lodge for 90 seconds. Explosives work best.[/color]
cmu-bad-blood-totem-examine-marine-desecrated = [color=#c9a227]This totem is desecrated and feeds the hunters nothing. Right-click it to [bold]Read the totem[/bold] (the Lodge's grid square) or [bold]Survey Totem[/bold] (clan signature for the uplink). The hunters can reconsecrate it; destroy it when you are done.[/color]

cmu-bad-blood-totem-desecrate-verb = Desecrate Totem
cmu-bad-blood-totem-desecrate-verb-message = Stop the totem feeding the hunters, so it can be read and surveyed. Takes 45 seconds; moving or getting hurt interrupts you.
cmu-bad-blood-totem-reconsecrate-verb = Reconsecrate Totem
cmu-bad-blood-totem-reconsecrate-verb-message = Take the Totem back for the clan. Takes 45 seconds; moving or getting hurt interrupts you.
cmu-bad-blood-totem-desecrate-start = You start defacing the totem. Hold still for { $seconds } seconds; moving or getting hurt interrupts you.
cmu-bad-blood-totem-reconsecrate-start = You start reconsecrating the Totem. Hold still for { $seconds } seconds.
cmu-bad-blood-totem-desecrate-marines-only = Only the prey would desecrate a Totem.
cmu-bad-blood-totem-reconsecrate-clan-only = Only a Bad Blood can reconsecrate this.
cmu-bad-blood-totem-already-desecrated = It is already desecrated.
cmu-bad-blood-totem-not-desecrated = The Totem is already consecrated.
cmu-bad-blood-totem-consecration-gone = The totem is gone.

# FLOW-506: reading a desecrated Totem
cmu-bad-blood-totem-read-verb = Read the totem
cmu-bad-blood-totem-read-start = You start reading the totem's markings. Hold still. If a hunter comes close before you finish, you learn nothing.
cmu-bad-blood-totem-read-consecrated = Desecrate the totem first. Its markings mean nothing while it is still the hunters'.
cmu-bad-blood-totem-read-hunter-near = A hunter is too close (within 7 tiles). Drive them off before you read the totem.
cmu-bad-blood-totem-read-already = You have learned all this totem has to tell until it is desecrated again.
cmu-bad-blood-totem-read-lodge = The markings point to the hunters' Lodge: X { $x1 }-{ $x2 }, Y { $y1 }-{ $y2 } (rangefinder coordinates).
cmu-bad-blood-totem-read-no-lodge = The markings point nowhere. The hunters have no Lodge standing.
cmu-bad-blood-totem-read-ripe = The hunters hold { $ripe } of the { $needed } ripe totems they need to call their ship.
cmu-bad-blood-totem-read-hold = Their ship will be called in about { $minutes } minutes unless you destroy or desecrate one of their ripe totems.

# Bad Blood vehicle breach: BadBloodVehicleBreachSystem
cmu-bad-blood-vehicle-force-verb = Force hatch
cmu-bad-blood-vehicle-force-verb-message = Rip the locked hatch open with your hands. Takes {$seconds} seconds; moving or getting hurt interrupts you.
cmu-bad-blood-vehicle-force-start = You dig your claws into the hatch seams and start wrenching it open.
cmu-bad-blood-vehicle-force-start-others = Something is tearing at the hatch!

# ECS-01: Bad Blood gear refuses anyone who is not a Yautja
cmu-bad-blood-gear-denied = The clan's gear is built for a hunter's hands. You cannot make it work.

# ARCH-04 to ARCH-06: castes
ent-CMUBadBloodCampEngineerStation = forge-bench
    .desc = Scrap, charges and a heavy plasma rifle under oiled hides. The clan's engineers arm themselves here.
ent-CMUBadBloodCampMedicStation = medicine cache
    .desc = Gels, clamps and injectors packed in bone boxes. The clan's medics stock up here.
cmu-bad-blood-caste-verb-engineer = Take up the Engineer path
cmu-bad-blood-caste-verb-medic = Take up the Medic path
cmu-bad-blood-caste-verb-message = A caste lasts for the rest of this life. The clan may have { $cap } of each.
cmu-bad-blood-caste-taken-engineer = You are the clan's Engineer: you build anywhere, see as an honourable hunter does, and the forge-bench arms you.
cmu-bad-blood-caste-taken-medic = You are the clan's Medic: you work Yautja medicine faster, and the medicine cache stocks you.
cmu-bad-blood-caste-not-clan = Only a living Bad Blood can take up a caste.
cmu-bad-blood-caste-already = You already follow a path.
cmu-bad-blood-caste-full = The clan already has { $cap } of this caste.
cmu-bad-blood-caste-station-locked = This station serves its caste. Take up the path here first.

# clan codes: how Bad Bloods know each other and how the radio names them
cmu-bad-blood-code-name = { $role } (BB-{ $number })
cmu-bad-blood-code-role-leader = Leader
cmu-bad-blood-code-role-engineer = Engineer
cmu-bad-blood-code-role-medic = Medic
cmu-bad-blood-code-role-grunt = Grunt
