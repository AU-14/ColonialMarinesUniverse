# CMU14 Playtest 5: the Beast-Master's pack (BadBloodBeastSystem) and thralls (BadBloodThrallBeastSystem)

## pack actions (ECS-707)
cmu-bad-blood-beast-scent-invalid = Your beasts cannot be set on that.
cmu-bad-blood-beast-scent-master = Your pack has the scent of {$target}. They hit it harder for {$seconds} seconds.
cmu-bad-blood-beast-scent-beast = Your Beast-Master gives you the scent of {$target}. Hunt it!
cmu-bad-blood-beast-rally-beast = {CAPITALIZE($master)} howls for the pack. Run!
cmu-bad-blood-beast-rally-master = {$count ->
    [0] You howl, but no beast of yours is near enough to hear.
    [one] You howl, and one beast runs to your call.
   *[other] You howl, and {$count} beasts run to your call.
}
cmu-bad-blood-beast-mend-not-beast = Only the clan's living beasts can be mended this way.
cmu-bad-blood-beast-mend-cooldown = That beast was mended too recently.
cmu-bad-blood-beast-mended = You tend to {$beast}'s wounds.
cmu-bad-blood-beast-last-order = Your master is dead! Rage, then find the clan.

## kennel (STATE-702)
cmu-bad-blood-beast-war-paint = The kennel's hounds wear the clan's war-paint: they bite {$percent}% harder.

## thralls (ARCH-707, FLOW-709)
cmu-bad-blood-thrall-cage-stocked = The cage stands at the kennel. A thrall will answer to you once a spirit takes it.
cmu-bad-blood-thrall-cap = You already command as many thralls as you can keep.
cmu-bad-blood-thrall-budget = The clan is as large as the hunt allows. No thrall can join it now.
cmu-bad-blood-thrall-ghost-name = Bad Blood thrall
cmu-bad-blood-thrall-ghost-description = A xenomorph broken from its hive and leashed to the Bad Blood clan. Fight for the clan's Beast-Master.
cmu-bad-blood-thrall-ghost-rules = You are a thrall of the Bad Blood clan. Obey the Beast-Master and the Hunt Leader, and fight the clan's prey. You cannot evolve or grow a hive. Do not attack the clan or its beasts.
