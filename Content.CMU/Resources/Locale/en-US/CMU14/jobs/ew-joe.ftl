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
