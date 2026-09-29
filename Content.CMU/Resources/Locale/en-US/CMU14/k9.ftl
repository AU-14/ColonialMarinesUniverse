# K9 entities
ent-AU14MobK9 = Synthetic K9
  .desc = An Interdyne-produced synthetic dog. Standard model without special harness gear.

ent-AU14MobK9GhostRole = Synthetic K9
  .desc = An Interdyne-produced synthetic dog. Standard model without special harness gear.
  .suffix = Ghost Role

ent-AU14MobK9GhostRoleGOVFOR = Synthetic K9
  .desc = An Interdyne-produced synthetic dog. Standard model without special harness gear.
  .suffix = GOVFOR, Ghost Role

ent-AU14MobK9MP = Military Police K9
  .desc = An Interdyne-produced synthetic dog in a military police harness.

ent-AU14MobK9MPGhostRole = Military Police K9
  .desc = An Interdyne-produced synthetic dog in a military police harness.
  .suffix = MP, Ghost Role

ent-AU14MobK9MPGhostRoleGOVFOR = Military Police K9
  .desc = An Interdyne-produced synthetic dog in a military police harness.
  .suffix = MP, GOVFOR

ent-AU14MobK9BagMedic = Search and Rescue K9
  .desc = An Interdyne-produced synthetic dog in a harness for carrying medical supplies.

ent-AU14MobK9BagMedicGhostRole = Search and Rescue K9
  .desc = An Interdyne-produced synthetic dog in a harness for carrying medical supplies.
  .suffix = S&R, Ghost Role

ent-AU14MobK9BagMedicGhostRoleGOVFOR = Search and Rescue K9
  .desc = An Interdyne-produced synthetic dog in a harness for carrying medical supplies.
  .suffix = S&R, GOVFOR

# K9 Dog Actions
ent-RMCActionK9ArmGrab = Arm Grab
  .desc = Bite down onto a target's arm, restraining and dragging them. Using it a second time trips them down.

ent-RMCActionK9TrackMaster = Track Owner
  .desc = Calculate the distance and direction to your bonded owner using acoustic and biometric sensors.

ent-RMCActionK9RequestMaster = Request Bond
  .desc = Ask a nearby marine to bond. Grants access and tracking only — no handler commands.

# Handler Actions
ent-RMCActionK9Tame = Authorize (Tame)
  .desc = Send an authorization request to a synthetic K9 unit to become its handler.

ent-RMCActionK9SicEm = Sic 'Em!
  .desc = Mark an enemy target. All your bonded K9s gain increased melee damage against this target.

ent-RMCActionK9Evacuate = Evacuate!
  .desc = Order your dogs to evacuate an ally. K9s will drag them without speed penalties and with increased movement speed.

ent-RMCActionK9GoodBoy = Good Boy
  .desc = Pat and praise your loyal synthetic companion.

rmc-action-k9-arm-grab = Arm Grab
rmc-action-k9-arm-grab-desc = Bite down onto a target's arm, restraining and dragging them. Using it a second time trips them down.

rmc-action-k9-track-master = Track Owner
rmc-action-k9-track-master-desc = Calculate the distance and direction to your bonded owner using acoustic and biometric sensors.

rmc-action-k9-request-master = Request Bond
rmc-action-k9-request-master-desc = Ask a nearby marine to bond. Grants access and tracking only — no handler commands.

rmc-action-k9-tame = Authorize (Tame)
rmc-action-k9-tame-desc = Send an authorization request to a synthetic K9 unit to become its handler.

rmc-action-k9-sic-em = Sic 'Em!
rmc-action-k9-sic-em-desc = Mark an enemy target. All your bonded K9s gain increased melee damage against this target.

rmc-action-k9-evacuate = Evacuate!
rmc-action-k9-evacuate-desc = Order your dogs to evacuate an ally. K9s will drag them without speed penalties and with increased movement speed.

rmc-action-k9-good-boy = Good Boy
rmc-action-k9-good-boy-desc = Pat and praise your loyal synthetic companion.

# Arm Grab messages
rmc-k9-arm-grab-out-of-range = Target is too far away!
rmc-k9-arm-grab-success-self = You bite down firmly onto {$target}'s arm!
rmc-k9-arm-grab-success-target = {$dog} bites down firmly onto your arm! Resist to break free!
rmc-k9-arm-grab-success-others = {$dog} bites down firmly onto {$target}'s arm!
rmc-k9-arm-grab-trip-self = You yank hard on {$target}'s arm, tripping them to the ground!
rmc-k9-arm-grab-trip-target = {$dog} yanks hard on your arm and knocks you to the ground!
rmc-k9-arm-grab-trip-others = {$dog} yanks hard on {$target}'s arm and knocks them to the ground!
rmc-k9-arm-grab-escape-attempt = You attempt to pull your arm free from the synthetic jaws...
rmc-k9-arm-grab-escaped-self = You manage to break your arm free from the dog's grip!
rmc-k9-arm-grab-escaped-dog = {$target} manages to break free from your jaws!
rmc-k9-arm-grab-broken-damage = Due to heavy damage, the dog loses its grip!
rmc-k9-arm-grab-xeno = The jaws cannot get a reliable lock on a xenomorph!

# Track Owner
rmc-k9-track-master-none = You do not have a registered owner. Approach a marine and use 'Request Bond'.
rmc-k9-track-master-lost = Sensors cannot pinpoint your owner's signal.
rmc-k9-track-master-result = Sensors locate owner: direction — {$direction}, distance ~{$distance}m.
rmc-k9-request-master-already-bound = You already have an owner!
rmc-k9-request-master-not-marine = You can only bond with a marine.
rmc-k9-request-master-title = K9 Request
rmc-k9-request-master-prompt = Synthetic K9 {$dog} requests a bond. You will be its owner: the dog copies your access and can track you. Handler commands are not granted. Accept?
rmc-k9-request-master-sent = Request sent to {$target}.

# Tame
rmc-k9-tame-not-dog = This target is not a synthetic K9!
rmc-k9-tame-already-bound = This K9 is already bonded with a handler.
rmc-k9-tame-dialog-title = Handler Authorization
rmc-k9-tame-dialog-prompt = Marine {$handler} requests authorization to become your handler. Any marine bond will be dropped. Confirm protocol?
rmc-k9-tame-request-sent = Synchronization request sent to {$dog}.

# Bond
rmc-k9-bind-success-master = Protocol synchronized! You are now the handler of {$dog}.
rmc-k9-bind-success-dog = Protocol synchronized! Marine {$master} is now registered as your handler.
rmc-k9-bind-success-handler = Handler protocol synchronized! You are now the handler of {$dog}. Sic 'Em and Evacuate are active.
rmc-k9-bind-success-dog-handler = Protocol synchronized! Marine {$master} is now registered as your handler.
rmc-k9-bind-success-marine = Bond established. {$dog} copied your access and can track you. Handler commands are not available.
rmc-k9-bind-success-dog-marine = Bond established. Marine {$master} is now your owner. Access synchronized.
rmc-k9-bind-replaced-old = Your bond with {$dog} was dropped: the dog transferred to handler {$master}.

# Bodyguard Protocol
rmc-k9-protector-rage-trigger = [color=red]{$dog} detects a threat to its handler and engages combat defense protocol![/color]

# Sic Em
rmc-k9-sic-em-shout = Handler {$handler} shouts: "SIC 'EM on {$target}!"
rmc-k9-sic-em-dog-order = [color=orange]Handler order: ATTACK {$target}![/color]

# Evacuate
rmc-k9-evacuate-shout = Handler {$handler} shouts: "EVACUATE {$target}!"
rmc-k9-evacuate-dog-order = [color=cyan]Handler order: EVACUATE {$target}![/color]

# Good Boy
rmc-k9-good-boy-popup = {$user} praises {$dog}. {$dog} wags its mechanical tail and emits a happy beep-boop!

# Sensors
rmc-k9-senses-alert-growl = [color=red]{$dog} detects motion vibrations and growls into the darkness![/color]
rmc-k9-senses-alert-master = [color=red]Your K9 {$dog} detected enemy motion nearby![/color]

# Directions
rmc-k9-direction-north = north
rmc-k9-direction-northeast = north-east
rmc-k9-direction-east = east
rmc-k9-direction-southeast = south-east
rmc-k9-direction-south = south
rmc-k9-direction-southwest = south-west
rmc-k9-direction-west = west
rmc-k9-direction-northwest = north-west
