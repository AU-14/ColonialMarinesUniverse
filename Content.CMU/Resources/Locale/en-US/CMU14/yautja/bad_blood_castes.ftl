# CMU14 ARCH-604 to ARCH-607: Bad Blood castes (BadBloodCasteSystem, castes.yml). The Engineer and Medic verb, taken
# and code-role keys predate this file and live in bad_blood_armory_totems.ftl.

cmu-bad-blood-caste-name-engineer = Engineer
cmu-bad-blood-caste-name-medic = Medic
cmu-bad-blood-caste-name-berserker = Berserker
cmu-bad-blood-caste-name-tracker = Tracker
cmu-bad-blood-caste-name-beast-master = Beast-Master

cmu-bad-blood-caste-verb-berserker = Take up the Berserker path
cmu-bad-blood-caste-verb-tracker = Take up the Tracker path
cmu-bad-blood-caste-verb-beast-master = Take up the Beast-Master path

cmu-bad-blood-caste-taken-berserker = You are a Berserker of the clan: you take more to put down and stagger harder, but you sprint slower and carry no stun bolt.
cmu-bad-blood-caste-taken-tracker = You are a Tracker of the clan: your cloak returns sooner and you can mark prey for the whole clan, but your armour counts for less.
cmu-bad-blood-caste-taken-beast-master = You are the clan's Beast-Master: the clan's hounds and thralls answer to you, you alone buy the falcon drone and the kennel's wares, but your armour counts for less.

cmu-bad-blood-code-role-berserker = Berserker
cmu-bad-blood-code-role-tracker = Tracker
cmu-bad-blood-code-role-beast-master = Beast-Master

# the Lodge's caste choice
cmu-bad-blood-caste-choose-verb = Choose your caste
cmu-bad-blood-caste-dialog-title = Choose your caste
cmu-bad-blood-caste-dialog-message = A caste lasts for the rest of this life.
cmu-bad-blood-caste-option = { $verb } ({ $held }/{ $cap })

# CMU14 ARCH-704: the Hunt Leader is a caste of its own
cmu-bad-blood-caste-leader = The Hunt Leader follows no other path.
cmu-bad-blood-caste-shed-for-leader = You lead the hunt now. Your old caste falls away.

# the armory's caste locks
cmu-bad-blood-caste-armory-locked = This station serves its caste. Choose your caste at the Lodge first.
cmu-bad-blood-caste-ware-locked = Only the clan's { $caste } may take this.

# Berserker
cmu-bad-blood-berserker-no-stun = A Berserker carries no stun bolt. The caster falls back to the immobilizer.

# Tracker
cmu-bad-blood-tracker-mark-verb = Mark for the clan
cmu-bad-blood-tracker-mark-message = Mark this prey on every Bad Blood's bracer map for { $seconds } seconds.
cmu-bad-blood-tracker-no-bracer = You need your bracer on to mark prey.
cmu-bad-blood-tracker-marked = Marked for the clan: every bracer map shows this prey for { $seconds } seconds.

# Falcon relay (ARCH-607): shared with honourable hunters, the text is unchanged
cmu-yautja-falcon-relay = Falcon Relay: { $name } { $verb }, "{ $message }"

# CMU14 ECS-703: Blood Rage
cmu-bad-blood-rage-start = { $name } roars in a blood rage!
cmu-bad-blood-rage-end = Your blood rage ebbs.
cmu-bad-blood-rage-healthy = You are not hurt enough to rage. Blood Rage needs you under half health.
cmu-bad-blood-rage-already = You are already raging.
cmu-bad-blood-rage-not-alive = You cannot rage like this.

# CMU14 ECS-705: War Cry
cmu-bad-blood-war-cry = { $name } lets out a war cry!

# CMU14 ECS-701: Engineer
cmu-bad-blood-bulwark-no-bricks = Hold a stack of clan bricks to raise a bulwark, { $cost } for each barricade.
cmu-bad-blood-bulwark-no-room = There is no open ground in front of you to raise a bulwark on.
cmu-bad-blood-bulwark-raised = You raise a bulwark of { $count } barricades.
cmu-bad-blood-engineer-repair-verb = Repair
cmu-bad-blood-engineer-repair-message = Mend this for the clan ({ $seconds } seconds).
cmu-bad-blood-engineer-repair-nothing = There is nothing here to mend.
cmu-bad-blood-engineer-repaired = You mend the stone.

# CMU14 ECS-704: Tracker
cmu-bad-blood-tracker-early-warning = { $count ->
    [one] Prey closes on the Totem at { $area }.
   *[other] { $count } prey close on the Totem at { $area }.
}
cmu-bad-blood-tracker-early-warning-ground = { $count ->
    [one] Prey closes on the Hunt Ground at { $area }.
   *[other] { $count } prey close on the Hunt Ground at { $area }.
}
