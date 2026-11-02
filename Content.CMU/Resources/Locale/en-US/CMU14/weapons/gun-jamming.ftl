## Gun wear and jamming

cmu-gun-jam-type-feed = failure to feed
cmu-gun-jam-type-eject = failure to eject
cmu-gun-jam-type-chamber = failure to chamber
cmu-gun-jam-type-fire = failure to fire
cmu-gun-jam-type-unknown = jam

cmu-gun-jam-blocked = {CAPITALIZE(THE($gun))} is jammed: {$jam}!
cmu-gun-jam-broken-blocked = {CAPITALIZE(THE($gun))} is broken beyond use!

cmu-gun-jam-verb-cycle = Clear jam
cmu-gun-jam-verb-slam = Slam stock
cmu-gun-jam-verb-slam-message = Quickly clears a jam, but wears the gun, can set off a round and can wreck the gun.

cmu-gun-jam-cycle-start = You start working the action of {THE($gun)} to clear the {$jam}.
cmu-gun-jam-cycle-success = You clear the {$jam} from {THE($gun)}.
cmu-gun-jam-cycle-fail = The {$jam} is still stuck in {THE($gun)}.

cmu-gun-jam-slam-start = You raise {THE($gun)} to slam its stock into the ground.
cmu-gun-jam-slam-self = You slam the stock of {THE($gun)} into the ground!
cmu-gun-jam-slam-others = {CAPITALIZE(THE($user))} slams the stock of {THE($gun)} into the ground!
cmu-gun-jam-slam-success = The jam in {THE($gun)} knocks loose.
cmu-gun-jam-slam-fail = {CAPITALIZE(THE($gun))} is still jammed.
cmu-gun-jam-discharge-self = {CAPITALIZE(THE($gun))} goes off as you slam it, firing into the air!
cmu-gun-jam-discharge-others = {CAPITALIZE(THE($gun))} goes off as {THE($user)} slams it, firing into the air!
cmu-gun-jam-break-self = Something inside {THE($gun)} snaps. It's ruined!
cmu-gun-jam-break-others = Something inside {THE($gun)} snaps as {THE($user)} slams it!

cmu-gun-jam-examine-jammed = [color=orange]It's jammed: {$jam}.[/color] Press your [color=cyan]unique action[/color] keybind to clear it{$slam ->
    [true] , or slam the stock from the right click menu.
   *[false] .
}
cmu-gun-jam-examine-broken = [color=red]Its internals are wrecked. It will never fire again.[/color]

cmu-gun-wear-pristine = It looks [color=green]well maintained[/color].
cmu-gun-wear-good = It looks [color=green]in good condition[/color].
cmu-gun-wear-worn = It looks [color=yellow]worn[/color].
cmu-gun-wear-poor = It looks [color=orange]badly worn[/color] and could jam.
cmu-gun-wear-critical = It looks [color=red]filthy and worn out[/color] and will jam often.

cmu-gun-maintenance-start = You start cleaning {THE($gun)} with {THE($tool)}.
cmu-gun-maintenance-done = You finish cleaning {THE($gun)}.
cmu-gun-maintenance-clean = {CAPITALIZE(THE($gun))} is as clean as {THE($tool)} can get it.
cmu-gun-maintenance-empty = {CAPITALIZE(THE($tool))} is used up.
cmu-gun-maintenance-used-up = You use up the last of {THE($tool)}.
cmu-gun-maintenance-broken = Cleaning won't fix {THE($gun)}. Its internals are wrecked.
cmu-gun-maintenance-examine = Use it on a gun to clean off wear.
cmu-gun-maintenance-examine-uses = It has enough left for {$uses ->
    [one] one more cleaning
   *[other] {$uses} more cleanings
}.
