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
    .desc = The clan's plasma caster is tuned for the eradicator. Only the Hunt Leader may fire it.

ent-CMUBadBloodKennelRate = kennel breeding
    .desc = The kennel raises its hounds twice as fast.

ent-CMUBadBloodKennelPack = kennel pack
    .desc = The kennel always keeps two more hounds ready.

ent-CMUBadBloodScavengedSpike = scrap spike

ent-CMUBadBloodScavengedSpikeLauncher = scavenged spike launcher
    .desc = A spike launcher with a cracked housing and a magazine patched from scrap. It holds half of what it should and recharges slowly.
    .suffix = Bad Blood

cmu-bad-blood-armory-not-clan = The armory answers only to the clan.
cmu-bad-blood-armory-tier-locked = The clan has not earned this yet. It needs armory tier { $tier }.
cmu-bad-blood-armory-leader-only = Only the Hunt Leader may take this.
cmu-bad-blood-armory-already-unlocked = The clan already has the eradicator.
cmu-bad-blood-armory-lethal-already-unlocked = The clan already has the lethal bolt.
cmu-bad-blood-armory-needs-lethal = The clan must earn the lethal bolt before the eradicator.
cmu-bad-blood-armory-lethal-unlocked = The clan has earned the lethal bolt. Every plasma caster can fire it.
cmu-bad-blood-armory-not-enough = You cannot pay for this. It costs { $price } Honour and you have { $sp }. Offer trophies at the Lodge to earn more.
cmu-bad-blood-armory-forge-nothing = The forge needs a bracer weapon you chose and have not forged yet. Wear your bracer.
cmu-bad-blood-armory-forged = The forge reworks your bracer weapon. It hits harder now.
cmu-bad-blood-forged-examine = It has been [color=orange]forged[/color] at the clan's Lodge and hits a fifth harder.
cmu-bad-blood-armory-eradicator-unlocked = The clan has earned the eradicator. The Hunt Leader's plasma caster can fire it.
cmu-bad-blood-armory-kennel-none = The clan has no kennel to improve.
cmu-bad-blood-armory-kennel-maxed = The kennel cannot be improved this way any further.
cmu-bad-blood-armory-kennel-improved = The clan's kennel is improved. It now stocks a hound every { $seconds } seconds and keeps { $stock } ready.

cmu-bad-blood-lethal-locked = The clan has not earned the lethal bolt. The Hunt Leader buys it for the whole clan at the Lodge armory.
cmu-bad-blood-eradicator-locked = The eradicator is not yours to fire. The Hunt Leader buys it at the Lodge armory.
cmu-bad-blood-eradicator-cooldown = The eradicator is still cooling. Wait { $seconds } more seconds.

## Clan Totems

ent-CMUBadBloodTotem = Clan Totem
    .desc = A spear driven into the ground and hung with skulls. The clan that raised it claims everything around it.
    .suffix = Bad Blood

ent-CMUBadBloodTotemObjective = Destroy the Clan Totems
    .desc = Find and destroy the totems the hostile clan has raised in the area of operations. Each one that falls exposes their Lodge on the tactical map for a short time.
# FLOW-609: the GOVFOR objectives console line (objectiveDescription in totems.yml)
cmu-bad-blood-obj-desc-totems = Destroy a Clan Totem raised by the hostile hunters. Each one that falls exposes their Lodge on the tactical map for a short time.

cmu-bad-blood-totem-roofed = A Clan Totem needs open sky. You cannot raise it under a roof.
cmu-bad-blood-totem-near-lodge = This is too close to the Lodge. Take the clan's claim further out.
cmu-bad-blood-totem-near-totem = This is too close to another Clan Totem.
cmu-bad-blood-totem-near-landing-zone = This is too close to the prey's landing zone to raise a Clan Totem.
cmu-bad-blood-totem-raised-clan = A Clan Totem stands at { $area }. It feeds the clan until the prey desecrates it.
cmu-bad-blood-totem-raised-marines = Sensors register an unknown emitter near { $area }.
cmu-bad-blood-totem-destroyed-clan = The Clan Totem at { $area } has fallen! The prey now knows where the Lodge stands.
cmu-bad-blood-totem-stage-young = young
cmu-bad-blood-totem-stage-growing = growing
cmu-bad-blood-totem-stage-ripe = ripe
cmu-bad-blood-totem-examine = This Clan Totem is [color=#c9a227]{ $stage }[/color]. It pays the clan [color=#c9a227]{ $rate }[/color] Superiority Points a minute and has paid [color=#c9a227]{ $paid }[/color] so far.
cmu-bad-blood-totem-paid = The Clan Totem has fed the clan { $paid } Superiority Points.
cmu-bad-blood-totem-destroyed-marines = The hostile totem at { $area } is down. Sensors traced its last signal back to the hunters' Lodge, now marked on the tactical map. Survey the Lodge or strike it before the trace fades.
# FLOW-604: sent once when the first dropship lands, if Totems already stand
cmu-bad-blood-totem-sensor-sweep = Dropship sensors register { $count } unknown { $count ->
        [one] emitter
       *[other] emitters
    } near { $areas }.

# FLOW-503: desecration. FLOW-608: marines only learn of it standing there, in the voice of the contest popups
cmu-bad-blood-totem-desecrated-nearby-marine = The trophies come down and the totem's hum dies away.
cmu-bad-blood-totem-desecrated-clan = The prey has desecrated the Clan Totem at { $area }! It feeds the clan nothing until one of you reconsecrates it (right-click it).
cmu-bad-blood-totem-reconsecrated-nearby-marine = The totem's hum rises again. Fresh blood glistens on its trophies.
cmu-bad-blood-totem-reconsecrated-clan = The Clan Totem at { $area } is consecrated again.
cmu-bad-blood-totem-examine-desecrated = [color=red]The prey has desecrated it. It pays nothing until a Bad Blood reconsecrates it: right-click it and hold still for 45 seconds.[/color]
cmu-bad-blood-totem-examine-marine = [color=#c9a227]A spear driven into the ground and hung with skulls and bones, an alien device lashed among them. You could [bold]Defile Totem[/bold] and tear its trophies down, or destroy it outright.[/color]
cmu-bad-blood-totem-examine-marine-desecrated = [color=#c9a227]Its trophies lie torn in the dirt and the alien device is silent. Carvings show on the bare spear: you could [bold]Read the totem[/bold] or [bold]Survey Totem[/bold]. The hunters may come back to dress it again.[/color]

cmu-bad-blood-totem-desecrate-verb = Defile Totem
cmu-bad-blood-totem-desecrate-verb-message = Tear the trophies down and silence the totem, so its carvings can be read and surveyed. Takes 45 seconds; moving or getting hurt interrupts you.
cmu-bad-blood-totem-reconsecrate-verb = Reconsecrate Totem
cmu-bad-blood-totem-reconsecrate-verb-message = Take the Totem back for the clan. Takes 45 seconds; moving or getting hurt interrupts you.
cmu-bad-blood-totem-desecrate-start = You begin tearing the trophies down. Hold still for { $seconds } seconds; moving or getting hurt interrupts you.
cmu-bad-blood-totem-reconsecrate-start = You start reconsecrating the Totem. Hold still for { $seconds } seconds.
cmu-bad-blood-totem-desecrate-marines-only = Only the prey would defile a Totem.
cmu-bad-blood-totem-reconsecrate-clan-only = Only a Bad Blood can reconsecrate this.
cmu-bad-blood-totem-already-desecrated = Its trophies are already torn down.
cmu-bad-blood-totem-not-desecrated = The Totem is already consecrated.
cmu-bad-blood-totem-consecration-gone = The totem is gone.

# FLOW-607: contested Totems. Marines hear and see it; the clan knows it
cmu-bad-blood-totem-contest-start-marine = The totem's low hum stutters as you stand over it.
cmu-bad-blood-totem-contest-broken-marine = The hum swells back. Something is coming to its call.
cmu-bad-blood-totem-examine-marine-contested = Its hum is faint and broken while you hold the ground around it.
cmu-bad-blood-totem-examine-marine-active = It hums steadily, the trophies on it still wet.
cmu-bad-blood-totem-contested-clan = The prey stands over the Totem at { $area }. It feeds the clan nothing while they hold it. Drive them off.
cmu-bad-blood-totem-contest-ended-clan = The Totem at { $area } feeds the clan again.
cmu-bad-blood-totem-examine-contested = [color=red]The prey holds the ground around it: no Superiority Points, and it is not ripening.[/color]

# FLOW-506: reading a desecrated Totem
cmu-bad-blood-totem-read-verb = Read the totem
cmu-bad-blood-totem-read-start = You start reading the totem's carvings. Hold still. If a hunter comes close before you finish, you learn nothing.
cmu-bad-blood-totem-read-consecrated = Defile the totem first. Its carvings are hidden under the hunters' trophies.
cmu-bad-blood-totem-read-hunter-near = A hunter is too close. Drive them off before you read the totem.
cmu-bad-blood-totem-read-already = You have learned all this totem has to tell until it is defiled again.
cmu-bad-blood-totem-read-lodge = The carvings point to the hunters' Lodge: X { $x1 }-{ $x2 }, Y { $y1 }-{ $y2 } (rangefinder coordinates).
cmu-bad-blood-totem-read-no-lodge = The carvings point nowhere. The hunters have no Lodge standing.
cmu-bad-blood-totem-read-ripe = The carvings count toward something. { $ripe } of { $needed } marks are lit.
cmu-bad-blood-totem-read-hold = The newest carving reads like a summons. At this pace it is finished in about { $minutes } { $minutes ->
        [one] minute
       *[other] minutes
    }.

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
cmu-bad-blood-caste-taken-engineer = You are the clan's Engineer: you build anywhere, see as an honourable hunter does, and the forge-bench arms you.
cmu-bad-blood-caste-taken-medic = You are the clan's Medic: Field Mend and Blood Rite keep the clan standing, you drag the fallen faster and brew tonic at the cauldron, but your armour is lighter and you carry no immobilizer.
cmu-bad-blood-caste-not-clan = Only a living Bad Blood can take up a caste.
cmu-bad-blood-caste-already = You already follow a path.
cmu-bad-blood-caste-full = The clan already has { $cap } of this caste.

# clan codes: how Bad Bloods know each other and how the radio names them
cmu-bad-blood-code-name = { $role } (BB-{ $number })
cmu-bad-blood-code-role-leader = Hunt Leader
cmu-bad-blood-code-role-engineer = Engineer
cmu-bad-blood-code-role-medic = Medic
cmu-bad-blood-code-role-grunt = Grunt
