cmu-ssd-timer-examined = [color=yellow]{ CAPITALIZE(SUBJECT($ent)) } { CONJUGATE-HAVE($ent) } been asleep for { $time ->
    [0] less than a minute
    [one] { $time } minute
   *[other] { $time } minutes
}.[/color]
