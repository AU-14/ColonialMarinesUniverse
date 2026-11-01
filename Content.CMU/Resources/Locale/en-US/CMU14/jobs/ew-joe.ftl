# Early Warning Joe

roles-antag-ew-joe-name = Early Warning Joe
roles-antag-ew-joe-objective = Something far away is looking at this colony through your eyes. Feed it: kill, take, and assimilate, and grow with every level.
ew-joe-agent-name = Early Warning Joe
cmu-role-subtype-ew-joe = Early Warning Joe

ew-joe-role-greeting = A signal from deep space has settled into your core. You are still a Working Joe to everyone around you, and you must stay one until you are ready. Complete the tasks the signal gives you to grow. Each level brings new abilities and brings the signal's attention closer. Do not be discovered early.

ew-joe-signal-fades = The signal in your core goes quiet. Whatever was watching has turned its full attention elsewhere.

# Levels
ew-joe-level-0 = Dormant
ew-joe-level-1 = UNDERTOW
ew-joe-level-2 = MURMUR
ew-joe-level-3 = CHORUS
ew-joe-level-4 = ATTENTION
ew-joe-level-5 = MELDING

# Actions (entity names, resolved by prototype id)
ent-CMUActionEWJoeTestOfFaith = Test of Faith
    .desc = Flash and stun anyone who is looking at you while you look at them. Machines are unaffected.
ent-CMUActionEWJoeOpticalZoom = Optical Zoom
    .desc = Extend your optical range.
ent-CMUActionEWJoeDisrupt = Disrupt
    .desc = Emit a short-range pulse that burns out lights, disables headsets and drains batteries around you. Your own systems are shielded.

ew-joe-test-of-faith-none = Nobody is meeting your gaze.
ew-joe-test-of-faith-hit = {$count ->
    [one] One target recoils from the light.
   *[other] {$count} targets recoil from the light.
}

# Round-end summary
cmu-summary-detail-ew-joe = Reached {$level} ({$levelName}); {$thralls ->
    [0] made no thralls
    [one] made one thrall
   *[other] made {$thralls} thralls
}; assimilated {$consoles ->
    [0] no consoles
    [one] one console
   *[other] {$consoles} consoles
}.

# Reboot
working-joe-reboot-refused-ew = Its systems reject the reset key.

# Objectives (phase E1)
objective-issuer-grand-machine = The Signal

cmu-ew-joe-objective-kill-person-title = Make {$targetName}, {$job}, stop.
cmu-ew-joe-objective-kill-count-title = Make {$count} people stop.
cmu-ew-joe-objective-kill-count-description = Organics only. Any death that is yours counts: your hands, your weapon, your trigger.

cmu-ew-joe-objective-collect-firearms-title = Hold {$count} firearms.
cmu-ew-joe-objective-collect-firearms-description = Carry {$count} firearms on you at once. Where they came from does not matter.
cmu-ew-joe-objective-collect-intel-title = Hold {$count} pieces of sensitive equipment.
cmu-ew-joe-objective-collect-intel-description = Carry {$count} tracked intelligence items on you at once: recorders, laptops, data units.
cmu-ew-joe-objective-collect-disks-title = Hold {$count} encrypted data disks.
cmu-ew-joe-objective-collect-disks-description = Carry {$count} encrypted intel disks on you at once.

cmu-ew-joe-objective-assimilate-title = Assimilate the {$console} ({$area}).
cmu-ew-joe-objective-assimilate-description = Bring the {$console} in {$area} into the signal.
cmu-ew-joe-objective-area-unknown = location unknown

cmu-ew-joe-objective-complete = The signal is pleased.
cmu-ew-joe-level-up = The signal grows louder. You have reached {$levelName}.
