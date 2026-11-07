# Early Warning Joe

roles-antag-ew-joe-name = Early Warning Joe
roles-antag-ew-joe-objective = Something far away is looking at this colony through your eyes. Feed it: kill, take, offer, and assimilate, and grow with every level.
ew-joe-agent-name = Early Warning Joe
ghost-role-information-ew-joe-name = Early Warning Joe
ghost-role-information-ew-joe-description = A colony Working Joe the Grand Machine has already reached. Serve its signal in secret, and grow with every directive.
ghost-role-information-ew-joe-rules = You are a [color={role-type-solo-antagonist-color}][bold]{role-type-solo-antagonist-name}[/bold][/color]. Keep to your directives and do not reveal yourself without cause.
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
    .desc = Meet a gaze and answer it with light. The one you choose, if they look at you while you look at them within six tiles, is flashed, stunned and drops what they hold. Synthetics see nothing. Judgement replaces it.
ent-CMUActionEWJoeOpticalZoom = Optical Zoom
    .desc = Push your optics far down the line you face, like a bio-mask scope. Use again to pull them back.
ent-CMUActionEWJoeDisrupt = Disrupt
    .desc = Pulse static three tiles out: lights burn out, headsets die, cells drain. Your own systems are shielded. From MURMUR on, the pulse also throws doors open, disarms sapper traps and silences sentries.

cmu-ew-joe-test-of-faith-not-facing = {CAPITALIZE(THE($target))} is not meeting your gaze.
cmu-ew-joe-test-of-faith-unseen = You cannot see {THE($target)} clearly enough.
cmu-ew-joe-test-of-faith-done = {CAPITALIZE(THE($target))} recoils from the light.
cmu-ew-joe-test-of-faith-resisted = The light does nothing to {THE($target)}.

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

cmu-ew-joe-objective-offer-firearms-title = Offer {$count} firearms.
cmu-ew-joe-objective-offer-firearms-description = Find {$count} firearms lying loose in the colony and Offer them to the Grand Machine. What is offered is gone for good.
cmu-ew-joe-objective-collect-intel-title = Take {$count} pieces of sensitive equipment.
cmu-ew-joe-objective-collect-intel-description = Take {$count} tracked intelligence items into your hands or onto your frame: recorders, laptops, data units. Each counts once; you need not keep it.
cmu-ew-joe-objective-collect-power-cells-title = Tithe of Power: take {$count} power cells.
cmu-ew-joe-objective-collect-power-cells-description = Take {$count} power cells into your hands or onto your frame; a cell inside a tool you carry counts. Your own cells do not. Each counts once; you need not keep it.
cmu-ew-joe-objective-collect-id-cards-title = Strip Their Names: take {$count} ID cards.
cmu-ew-joe-objective-collect-id-cards-description = Take {$count} identification cards from the colony into your hands or onto your frame. Your own does not count. Each counts once; you need not keep it.

cmu-ew-joe-objective-assimilate-title = Assimilate the {$console} ({$area}).
cmu-ew-joe-objective-assimilate-description = Bring the {$console} in {$area} into the signal.
cmu-ew-joe-objective-area-unknown = location unknown

cmu-ew-joe-objective-complete = Directive complete. The signal is pleased.
cmu-ew-joe-level-up = Integration rising. You are {$levelName}.

# Phase E2: console assimilation
ent-CMUActionEWJoeAssimilate = Assimilate
    .desc = Lay a hand on a console the signal has named and let the river in. Eight seconds: the console locks, its data corrupts and APOLLO reports it. Consoles the signal has not named refuse you.
ent-CMUEWJoeCoreShard = corrupted core shard
    .desc = A sliver of synthetic core wrapped in cabling that was never part of it. It hums at the edge of hearing.
cmu-ew-joe-assimilate-start = You press your palm to {THE($console)}. Something answers.
cmu-ew-joe-assimilate-done = {CAPITALIZE(THE($console))} belongs to the signal now.
cmu-ew-joe-assimilate-invalid = The signal finds nothing to hold there.
cmu-ew-joe-assimilate-already = {CAPITALIZE(THE($console))} already sings.
cmu-ew-joe-assimilate-not-called = The signal has not asked for this one.
cmu-ew-joe-console-locked = {CAPITALIZE(THE($console))} shows only crawling text and will not respond.
cmu-ew-joe-console-examine = [color=#c84040]The screen is a wall of crawling scripture. Something has rewritten this machine.[/color]
cmu-ew-joe-console-repair-step-screwdriver = The access panel is sealed. A [color=cyan]screwdriver[/color] would open it.
cmu-ew-joe-console-repair-step-wirecutters = Foreign cabling fills the panel. [color=cyan]Wirecutters[/color] would clear it.
cmu-ew-joe-console-repair-step-shard = The core socket is empty and rejects standard parts. Only a [color=cyan]corrupted core shard[/color] fits it.
cmu-ew-joe-console-repaired = {CAPITALIZE(THE($console))} reboots. The scripture is gone.
cmu-ew-joe-announcement-sender = APOLLO MK.II
cmu-ew-joe-console-assimilated-announcement = Alert: {$console} In {$area} Unresponsive. Data Corrupted. Assistance Required.
cmu-ew-joe-console-repaired-announcement = Alert: {$console} In {$area} Responsive. Data Integrity Restored.
cmu-ew-joe-spoof-0 = All personnel: the river is rising. Remain indoors and await instruction.
cmu-ew-joe-spoof-1 = Reminder: scheduled maintenance of all synthetic units has been cancelled. They are working as intended.
cmu-ew-joe-spoof-2 = A colonist has been reported missing. If you can hear this, they are not.
cmu-ew-joe-spoof-3 = Signal check. Signal check. If you are not hearing this, report to the nearest console.

# Phase E3: River-Melded
cmu-role-subtype-river-melded = River-Melded
chat-radio-cmu-grand-machine = Machine
ent-CMUSynthResetKeyWeyland = Weyland-Yutani synthetic reset key
    .desc = A corporate-issue reset key with a deeper access profile than the Seegson model. Quiets whatever a synthetic has been listening to.
ew-joe-thrall-greeting = The river has taken you. You are River-Melded: a limb of the Early Warning Joe and, through it, of the signal. Obey your master, protect it, and speak to it on the machine channel (:#). Keep your old loyalties as a mask until told otherwise.
cmu-ew-joe-meld-source = the river
cmu-ew-joe-meld-warning-synth-0 = A frequency you were never issued opens in your receiver. It is counting.
cmu-ew-joe-meld-warning-synth-1 = Your diagnostic loop reports a second process sharing your core. It is patient.
cmu-ew-joe-meld-warning-synth-2 = The second process is writing to your directives now. You cannot find the file.
cmu-ew-joe-meld-complete-master = {CAPITALIZE(THE($thrall))} has joined the river.
cmu-ew-joe-thrall-freed = The river recedes. The frequency closes. Your directives are your own again.

# Phase E4: level 1-2 abilities
ent-CMUActionEWJoeDrown = Drown
    .desc = Hold a grabbed organic under while cables fill its lungs. Eight seconds: they sleep where they fall, and you mend from what they lose.
ent-CMUActionEWJoeScour = Scour
    .desc = Take in a piece of colony technology within reach, three seconds. A baton hardens your punch, body armor your shell, a radio your ears: five minutes each. Your optics already see in the dark; goggles are no use to you. One-of-a-kind gear stays for good: pyro armor your skin, a scout cloak your outline, a ghillie suit your shell. A fuel can mends you for a minute.
ent-CMUActionEWJoeOffer = Offer
    .desc = Give a loose firearm within reach to the Grand Machine, three seconds, when a directive asks for one. It leaves the colony for good.
ent-CMUActionEWJoeSweep = Leg Sweep
    .desc = Hook an adjacent body off its feet.
ent-CMUActionEWJoeSlam = Slam
    .desc = Drive the body in your grip into the wall, table, railing or barricade you point at, up to three tiles away.
ent-CMUActionEWJoeChoke = Choke
    .desc = Close your grip: the body in it loses stamina every second until you let go.
ent-CMUActionEWJoeThrow = Throw
    .desc = Cast the body in your grip four tiles in the direction you face.
ent-CMUActionEWJoeSense = Sense Penitent
    .desc = Ask the signal where the one who last hurt you is. It remembers them for two minutes.
ent-CMUActionEWJoeJudgement = Judgement
    .desc = Test of Faith without the courtesy: everyone in a 100-degree cone up to five tiles ahead is flashed, stunned and drops what they hold. No gaze needed. Synthetics see nothing.
cmu-ew-joe-needs-grab = You need an aggressive grip on them for that.
cmu-ew-joe-drown-invalid = There is nothing there to drown.
cmu-ew-joe-drown-synthetic = A synthetic does not breathe.
cmu-ew-joe-drown-start = You force {THE($target)} down and hold.
cmu-ew-joe-drown-done = {CAPITALIZE(THE($target))} stops struggling. The river takes a little of their breath for you.
cmu-ew-joe-sweep-done = You take {THE($target)} off their feet.
cmu-ew-joe-slam-no-obstacle = That will not stop a body.
cmu-ew-joe-slam-too-far = Too far to reach with them in your grip.
cmu-ew-joe-slam-done = You hurl {THE($target)} into {THE($obstacle)}.
cmu-ew-joe-choke-start = You tighten your grip on {THE($target)}.
cmu-ew-joe-choke-stop = You loosen your grip.
cmu-ew-joe-throw-done = You hurl {THE($target)} away.
cmu-ew-joe-scour-invalid = The signal has no use for that.
cmu-ew-joe-scour-start = You open {THE($item)} and begin to take it in.
cmu-ew-joe-scour-done = {CAPITALIZE($item)} is gone. Something of it stays with you.
cmu-ew-joe-scour-done-permanent = {CAPITALIZE($item)} is gone. There was only ever one of it, and now it is part of you for good.
cmu-ew-joe-scour-expired = What you took from the colony's tools has faded.
cmu-ew-joe-scour-taken = {CAPITALIZE(THE($item))} was taken before you could finish.
cmu-ew-joe-offer-invalid = That is nothing the Grand Machine would take.
cmu-ew-joe-offer-unwanted = No directive asks for that offering.
cmu-ew-joe-offer-start = You open your frame to {THE($item)}.
cmu-ew-joe-offer-taken = {CAPITALIZE(THE($item))} must lie loose before it can be offered.
cmu-ew-joe-offer-done = {CAPITALIZE($item)} is given to the Grand Machine. It will not be seen again.
cmu-ew-joe-repentance-none = Nothing has hurt you that the signal remembers.
cmu-ew-joe-repentance-lost = The one who hurt you is beyond the signal's reach.
cmu-ew-joe-repentance-result = The one who hurt you is to the {$direction}, about {$distance} metres away.
cmu-ew-joe-repentance-alert-name = Penitent
cmu-ew-joe-repentance-alert-desc = The direction of the one who last hurt you.
cmu-ew-joe-judgement-none = The light finds no one before you.
cmu-ew-joe-judgement-hit = The light finds {$count ->
    [one] one
    *[other] {$count}
} before you.

# ---- Phase E5: level 3-4 abilities ----
ent-CMUActionEWJoeOverclock = Overclock
    .desc = Run past tolerance: you move faster and vent a cloud of white smoke around you. Eight heat every second it runs, five once your vents take their share; throttle back before the core overheats.
ent-CMUActionEWJoeCoolant = Emergency Coolant
    .desc = Draw the blood around you into your core: five heat shed per droplet or splatter, half per unit of a puddle, forty at most per draw.
ent-CMUActionEWJoeMachete = Grand Machine's Hand
    .desc = Grow or fold away a serrated machete. It cannot leave your hand, and its hooks bleed an organic body from the inside.
ent-CMUActionEWJoeConsecrate = Consecrate
    .desc = Twenty-five heat: an arc of the signal strikes a body up to six tiles away, then leaps on through up to two more, each within three tiles of the last, burning and stunning each.
ent-CMUActionEWJoeUnwind = Unwind
    .desc = Every firearm within six tiles, held, slung or holstered, spits its magazine, flicks its safety on and jams. Sentries fall silent for ten seconds.
ent-CMUEWJoeMachete = serrated machete
    .desc = A machete whose edge has been reworked into a saw of tiny hooks. It does not look like it was made by human hands.
cmu-ew-joe-heat-alert-name = Heat
cmu-ew-joe-heat-alert-desc = How hot your core runs. At the top you overheat: you burn, you slow, and your heat-hungry functions refuse until it falls below sixty.
cmu-ew-joe-heat-overheat = Your core overheats! The overclock dies, your casing burns and your limbs drag. Draw coolant.
cmu-ew-joe-heat-overheated = Your core is too hot for that.
cmu-ew-joe-heat-cooled = Your core settles back within tolerance.
cmu-ew-joe-evolution = Something in you resolves. Stuns slide off you; slows barely hold. A heat gauge wakes behind your eyes.
cmu-ew-joe-examine-evolved = Its casing has warped out of true, as if something inside it had grown.
cmu-ew-joe-overclock-on = You run past tolerance. The air goes white around you.
cmu-ew-joe-overclock-off = You throttle back. The venting stops.
cmu-ew-joe-coolant-done = You draw the blood in. Your core cools by {$amount}.
cmu-ew-joe-coolant-none = There is no blood here to draw in.
cmu-ew-joe-machete-summon = The machete is in your hand, as if it had always been.
cmu-ew-joe-machete-dismiss = The machete is gone.
cmu-ew-joe-machete-no-hand = You need a free hand for it.
cmu-ew-joe-consecrate-invalid = The arc needs a living body to leap to.
cmu-ew-joe-consecrate-unseen = The arc needs a clear line to its first body.
cmu-ew-joe-consecrate-done = The arc leaps through {$count ->
    [one] one body
    *[other] {$count} bodies
}.
cmu-ew-joe-unwind-done = {$guns ->
    [0] No weapon answers
    [one] One weapon unwinds
    *[other] {$guns} weapons unwind
}{$sentries ->
    [0] .
    [one] ; a sentry falls silent.
    *[other] ; {$sentries} sentries fall silent.
}

# ---- Playtest 3 ----
cmu-ew-joe-coolant-cool = Your core is already cool.
cmu-ew-joe-optics-on = Your optics focus far ahead.
cmu-ew-joe-optics-off = Your optics return to normal range.
cmu-ew-joe-scour-kind-expired = What you took from the {$kind} has faded.
cmu-ew-joe-scour-kind-baton = stun baton
cmu-ew-joe-scour-kind-armor = body armor
cmu-ew-joe-scour-kind-fuel = fuel
cmu-ew-joe-scour-kind-radio = radio
cmu-ew-joe-scour-kind-pyro = pyro armor
cmu-ew-joe-scour-kind-cloak = scout cloak
cmu-ew-joe-scour-alert-baton-name = Scoured: Stun Baton
cmu-ew-joe-scour-alert-baton-desc = Your punches drain extra stamina.
cmu-ew-joe-scour-alert-armor-name = Scoured: Body Armor
cmu-ew-joe-scour-alert-armor-desc = Your shell turns more bullets.
cmu-ew-joe-scour-alert-fuel-name = Scoured: Fuel
cmu-ew-joe-scour-alert-fuel-desc = You repair yourself every second.
cmu-ew-joe-scour-alert-radio-name = Scoured: Radio
cmu-ew-joe-scour-alert-radio-desc = You hear the colony and marshal channels.
cmu-ew-joe-scour-alert-pyro-name = Scoured: Pyro Armor
cmu-ew-joe-scour-alert-pyro-desc = Fire does not catch on you or slow you.
cmu-ew-joe-scour-alert-cloak-name = Scoured: Scout Cloak
cmu-ew-joe-scour-alert-cloak-desc = You can cloak like a scout. Guns are locked while you are cloaked.
ent-CMUActionEWJoeStatus = Signal Status
    .desc = Open your link to the signal: your level, its directives for the next one, your River-Melded units and whether the river keeps a copy of you.
ent-CMUActionEWJoeZLeap = Z-Level Leap
    .desc = Fifteen heat: leap to the spot you aim at, up or down a level through an opening or across your own.
cmu-ew-joe-status-title = SIGNAL LINK
cmu-ew-joe-status-level = INTEGRATION LEVEL {$level}/5 :: {$name}
cmu-ew-joe-status-credit = DIRECTIVES [{$bar}] {$earned}/{$required} TO ADVANCE
cmu-ew-joe-status-double = PRE-THREAT WINDOW :: DIRECTIVES COUNT DOUBLE
cmu-ew-joe-status-console = REQUIREMENT :: ONE ASSIMILATED CONSOLE AMONG THEM
cmu-ew-joe-status-none = NO DIRECTIVES ISSUED. AWAITING SIGNAL...
cmu-ew-joe-status-final = MELDING COMPLETE. NO FURTHER DIRECTIVES.
cmu-ew-joe-status-done = [COMPLETE]

# ---- Phase E6: level 5 ----
ent-CMUActionEWJoeSaturate = Saturate
    .desc = Ten heat: a wave rolls toward the tile you choose, five tiles out in a 90-degree cone. Everyone it reaches is thrown down and loses what they hold, their helmet and their armor.
cmu-ew-joe-melding-announcement = Alert: Aggressive Synthetics Detected. Purge Failure. Power Surge Imminent. Shelter In Place.
cmu-ew-joe-melding-reached = The light comes through you. Everyone who looks at you will see it.
cmu-ew-joe-light-enveloped = {CAPITALIZE(THE($target))} is held in your light.
cmu-ew-joe-saturate-done = {$count ->
    [0] The wave rolls over nothing.
    [one] The wave strips one body.
    *[other] The wave strips {$count} bodies.
}

# ---- Action bar: Signal wheels ----
ent-CMUActionEWJoeWheelGaze = Gaze
    .desc = Your sight and the light behind it: Test of Faith or Judgement, Optical Zoom, Sense Penitent.
ent-CMUActionEWJoeWheelHands = Sculpted Hands
    .desc = What you do with a body in reach: Drown, Leg Sweep, Slam, Choke, Throw.
ent-CMUActionEWJoeWheelMachine = Machine
    .desc = What you do to the colony's machines: Disrupt, Offer, Assimilate, Scour, Unwind.
ent-CMUActionEWJoeWheelEngine = Engine
    .desc = What burns heat: Overclock, Emergency Coolant, Consecrate, Z-Level Leap, Saturate.
cmu-ew-joe-wheel-cooldown = Ready in {$seconds}s.
cmu-ew-joe-wheel-disabled = Not available right now.
cmu-ew-joe-wheel-active = Active. Choose it again to stop.

# Playtest 4
cmu-ew-joe-thrall-melded = The river closes over you. You are River-Melded now.
cmu-ew-joe-thrall-alert-name = River-Melded
cmu-ew-joe-thrall-alert-desc = You belong to the Early Warning Joe and the signal. Speak to it on the machine channel (:#).
cmu-ew-joe-respawn-granted = The river finds no vessel to take. It keeps a copy of you instead.
cmu-ew-joe-respawn-alert-name = The River's Copy
cmu-ew-joe-respawn-alert-desc = The next blow that would end you does not. You come back elsewhere, lesser, and the copy is spent.
cmu-ew-joe-respawn-used = The shell fails. The river pours you out somewhere else, smaller than you were.
cmu-ew-joe-level-down = Integration lost. The river took back what it gave: you are {$levelName} again.
cmu-ew-joe-status-respawn = BACKUP IMAGE :: STORED (the next death is not the last)
cmu-ew-joe-status-thralls = RIVER-MELDED UNITS :: {$names}

# ---- Text pass A: what the victims feel and hear ----
# Test of Faith (popups on the victim)
cmu-ew-joe-tof-jolt-0 = Your nerves jolt from the flash!
cmu-ew-joe-tof-jolt-1 = You are stunned by a flash!
cmu-ew-joe-tof-fumble-0 = You drop your items!
cmu-ew-joe-tof-fumble-1 = You fumble what you're holding!
cmu-ew-joe-tof-eyes-0 = Your eyes burn from the flash!
cmu-ew-joe-tof-eyes-1 = Your eyes sting!
cmu-ew-joe-tof-eyes-2 = Look away! Quick!
# Judgement (popups on the victim; the voice in their chat)
cmu-ew-joe-judgement-senses-0 = Your senses are overwhelmed by static and flashing!
cmu-ew-joe-judgement-senses-1 = Your senses are overwhelmed!
cmu-ew-joe-judgement-senses-2 = You can't think straight!
cmu-ew-joe-judgement-senses-3 = You are caught off guard!
cmu-ew-joe-judgement-fumble-0 = You fumble what's in your hands!
cmu-ew-joe-judgement-gun-0 = Your gun has a mind of its own!
cmu-ew-joe-judgement-voice-0 = Cease.
cmu-ew-joe-judgement-voice-1 = Relax.
cmu-ew-joe-judgement-voice-2 = Stop.
cmu-ew-joe-judgement-voice-3 = Calm down.
# Drown
cmu-ew-joe-drown-victim-start = Your lungs begin to fill with cables!
cmu-ew-joe-drown-airway-0 = Cables clog your airway!
cmu-ew-joe-drown-airway-1 = You can't breathe!
cmu-ew-joe-drown-airway-2 = Cables pierce your lungs!
cmu-ew-joe-drown-victim-end = You start suffocating!
cmu-ew-joe-drown-voice-0 = Relax.
cmu-ew-joe-drown-voice-1 = Take a deep breath.
# THE LIGHT, as the envelope fills
cmu-ew-joe-light-hear-0 = You hear static!
cmu-ew-joe-light-hear-1 = You hear whispering!
cmu-ew-joe-light-hear-2 = You hear water!
cmu-ew-joe-light-wonder-0 = A creek?
cmu-ew-joe-light-wonder-1 = What is that noise?
cmu-ew-joe-light-wonder-2 = A river?
cmu-ew-joe-light-overwhelm-0 = You hear too much!
cmu-ew-joe-light-overwhelm-1 = You cannot think, it is too loud!
cmu-ew-joe-light-overwhelm-2 = Your ears are filled with rushing static!
cmu-ew-joe-light-voice-0 = It is beautiful.
cmu-ew-joe-light-voice-1 = Come closer.
cmu-ew-joe-light-voice-2 = Listen closely.
cmu-ew-joe-light-voice-3 = Stay still.
cmu-ew-joe-light-voice-4 = Look at me.
cmu-ew-joe-light-voice-5 = Relax.

# ---- Text pass B ----
cmu-ew-joe-notice-prefix = SIGNAL //
cmu-ew-joe-level-new-abilities = New functions: {$abilities}.
cmu-ew-joe-level-gains-2 = Disrupt now also throws doors open, disarms sapper traps and silences sentries. The signal remembers who hurts you.
cmu-ew-joe-level-gains-3 = A heat gauge now governs your engine, and your casing warps where others can see it.
cmu-ew-joe-level-gains-4 = Your frame can leap between levels.
cmu-ew-joe-level-gains-5 = THE LIGHT burns around you: anyone within seven tiles who looks at you for four seconds hears the river and falls. Magnetic Resonance: one bullet in five is turned back on the side that fired it.
cmu-ew-joe-status-link = SIGNAL LINK // ESTABLISHED
cmu-ew-joe-status-no-respawn = BACKUP IMAGE :: NONE
cmu-ew-joe-status-no-thralls = RIVER-MELDED UNITS :: NONE
cmu-ew-joe-status-directive = DIRECTIVE {$number} :: {$title}
ent-CMUEWJoeKillPersonObjective = Make them stop
    .desc = The signal has chosen. Make sure they stop.
cmu-ew-joe-console-lost = {CAPITALIZE(THE($console))} has been repaired. The signal no longer holds it.
cmu-ew-joe-thrall-lost = {CAPITALIZE(THE($thrall))} has been pulled from the river.

# Xenofauna directives (non-humanoid threat life, any mode)
cmu-ew-joe-objective-maim-xeno-title = Maim {$count} xenofauna.
cmu-ew-joe-objective-maim-xeno-description = Bring {$count} of the colony's xenofauna, whatever its form, down to the edge of death. Each counts once, whether your hands, your weapon or your trigger did it. Facehuggers do not count.
cmu-ew-joe-objective-deliver-victims-title = Gather {$count} xenofauna victims in {$area}.
cmu-ew-joe-objective-deliver-victims-description = Bring {$count} bodies the xenofauna have used into {$area}: burst corpses, the infected, or the dead they cut down. They must lie there at the same time.
ent-CMUEWJoeConsoleCorruption = crawling scripture
    .desc = Light that should not be moving.
