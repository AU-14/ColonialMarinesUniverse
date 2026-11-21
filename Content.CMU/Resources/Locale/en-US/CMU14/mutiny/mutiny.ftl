# wording follows cmss13's mutiny abilities (human_abilities.dm, gear_presets/mutiny.dm)

## convert

cmu-mutiny-recruit-sent = Mutiny join request sent to {$target}!
cmu-mutiny-recruit-sworn = {$target} will become a mutineer when the mutiny begins.
cmu-mutiny-recruit-joined = {$target} is now a mutineer!
cmu-mutiny-recruit-declined = {$target} refused to join the mutiny.
cmu-mutiny-recruit-fail-self = You can't convert yourself!
cmu-mutiny-recruit-fail-faction = You can't convert {$target}! They're not on your side.
cmu-mutiny-recruit-fail-already = {$target} is already converted.
cmu-mutiny-recruit-fail-other-leader = You can't convert {$target}! They're already converted by someone else.
cmu-mutiny-recruit-fail-synth = You can't convert {$target}!
cmu-mutiny-recruit-fail-loyalist = You can't convert {$target}!
cmu-mutiny-recruit-fail-unresponsive = You can't convert {$target} right now.
cmu-mutiny-recruit-fail-pending = {$target} hasn't answered yet.
cmu-mutiny-recruit-fail-declined = {$target} refused. Wait before asking again.

## chat

cmu-mutiny-sworn = [color=#E0524A]You'll become a mutineer when the mutiny begins. Prepare yourself and do not cause any harm until you've been made into a mutineer.[/color]
cmu-mutiny-sworn-released = [color=#E0524A]Your mutiny leader is gone. You are no longer going to become a mutineer.[/color]
cmu-mutiny-invite-void = [color=#E0524A]You can no longer join that mutiny.[/color]
cmu-mutiny-side-loyalist = [color=#5F8CC4][bold]You are now a Loyalist![/bold][/color] Please check the rules to see what you can and can't do as a loyalist.
cmu-mutiny-side-loyalist-forced = [color=#5F8CC4][bold]You are now a Loyalist![/bold][/color] Please check the rules to see what you can and can't do as a loyalist.
cmu-mutiny-side-noncombatant = [color=#A0A0A0][bold]You are now a Non-Combatant![/bold][/color] You are not to get involved in the mutiny. You can heal either side, but must not engage or be engaged in combat.

cmu-mutiny-announcement-sender = Command
cmu-mutiny-announcement = DANGER: Communications received; a mutiny is in progress. Code: Detain, Arrest, Defend.

## window

cmu-mutiny-window-title = Mutiny
cmu-mutiny-rules = Read the Mutinies and Riots guidelines (Core Rules -> "Mutinies, Riots").
cmu-mutiny-timer = {$seconds}s left, then: {$fallback}

cmu-mutiny-invite-heading = Become Mutineer
cmu-mutiny-invite-from = {$leader} is asking you to join a mutiny.
cmu-mutiny-invite-from-job = {$leader} ({$job}) is asking you to join a mutiny.
cmu-mutiny-invite-body = Do you want to be a mutineer?
cmu-mutiny-invite-sworn-note = You'll become a mutineer when the mutiny begins. Do not cause any harm until you've been made into a mutineer.
cmu-mutiny-invite-open-note = The mutiny has already begun. You become a mutineer right away.
cmu-mutiny-invite-accept = Yes
cmu-mutiny-invite-accept-open = Yes
cmu-mutiny-invite-decline = No

cmu-mutiny-begin-heading = Begin Mutiny?
cmu-mutiny-begin-body = Are you sure you want to begin the mutiny?
cmu-mutiny-begin-roster = Converted ({$count})
cmu-mutiny-begin-roster-empty = Nobody converted yet.
cmu-mutiny-begin-dead = dead
cmu-mutiny-begin-warning = Everyone you converted becomes a mutineer. Everyone else on your side has to choose a side. MPs and command become loyalists.
cmu-mutiny-begin-confirm = Yes
cmu-mutiny-begin-cancel = No

cmu-mutiny-side-heading = Choose a Side
cmu-mutiny-side-from = {$leader} has begun a mutiny.
cmu-mutiny-side-from-job = {$leader} ({$job}) has begun a mutiny.
cmu-mutiny-side-body = A mutiny has been started, with whom do you stand?
cmu-mutiny-side-mutineer = Mutineers
cmu-mutiny-side-mutineer-desc = Join the mutiny.
cmu-mutiny-side-mutineer-synth = Synthetics can't join the mutiny.
cmu-mutiny-side-loyalist-name = Loyalists
cmu-mutiny-side-loyalist-desc = Stand against the mutiny.
cmu-mutiny-side-noncombatant-name = Refuse to fight
cmu-mutiny-side-noncombatant-desc = You can heal either side, but must not engage or be engaged in combat.
