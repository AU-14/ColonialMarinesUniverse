cmu-gun-safety-verb-on = Safety on
cmu-gun-safety-verb-off = Safety off
cmu-gun-safety-on = You put {THE($gun)} on safety.
cmu-gun-safety-off = You take {THE($gun)} off safety.
cmu-gun-safety-blocked = {CAPITALIZE(THE($gun))} is on safety!
cmu-gun-safety-examine-on = The safety is [color=yellow]on[/color].
cmu-gun-safety-examine-carried = {CAPITALIZE(POSS-ADJ($user))} {$gun} is {$loaded ->
    [yes] [color=red]loaded[/color]
   *[no] [color=gray]unloaded[/color]
}{$safety ->
    [on] {" "}with the safety [color=yellow]on[/color].
    [off] {" "}with the safety [color=orange]off[/color].
   *[none] .
}
cmu-gun-chamber-loaded-examine = A round is [color=yellow]chambered[/color].
cmu-gun-chamber-empty-examine = The chamber is [color=gray]empty[/color].
