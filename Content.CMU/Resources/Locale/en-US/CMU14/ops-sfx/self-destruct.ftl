# Announcement copy for the self-destruct sequence (self_destruct.yml).
# Movie-verbatim MU/TH/UR lines; markup matches the announcement panel's tag whitelist.
-cmu-ops-sfx-muthur-header = [color=#4d6b58]MU/TH/UR 6000 :: EMERGENCY DESTRUCT INTERFACE[/color]

cmu-ops-sfx-activated =
    {-cmu-ops-sfx-muthur-header}
    {"["}bold][color=#ff3838]DANGER![/color][/bold] The Emergency Destruct System is now [bold][color=#ff3838]Activated[/color][/bold]...

cmu-ops-sfx-destruct-with-override =
    {-cmu-ops-sfx-muthur-header}
    The ship will detonate in T Minus... [bold][color=#ffb000]{$detonation}[/color][/bold]... The option to override automatic detonation expires in T Minus... [bold][color=#ffb000]{$override}[/color][/bold].

cmu-ops-sfx-override-expires =
    {-cmu-ops-sfx-muthur-header}
    The option to override automatic detonation expires in T Minus... [bold][color=#ffb000]{$time}[/color][/bold]{$punct}

cmu-ops-sfx-override-expired =
    {-cmu-ops-sfx-muthur-header}
    The option to override detonation procedure has now [bold][color=#ff3838]expired[/color][/bold].

cmu-ops-sfx-destruct-countdown =
    {-cmu-ops-sfx-muthur-header}
    The ship will automatically destruct in T Minus... [bold][color=#ffb000]{$time}[/color][/bold]...

cmu-ops-sfx-abandon-1 =
    {-cmu-ops-sfx-muthur-header}
    You now have [bold][color=#ffb000]1 Minute[/color][/bold] to [bold][color=#ff3838]Abandon Ship[/color][/bold].

cmu-ops-sfx-t-30 =
    {-cmu-ops-sfx-muthur-header}
    {"["}bold][color=#ff3838]T Minus 30 Seconds.[/color][/bold]

cmu-ops-sfx-structural-failures =
    {-cmu-ops-sfx-muthur-header}
    {"["}bold][color=#ff3838]WARNING.[/color][/bold] Multiple catastrophic structural failures detected throughout the vessel.

cmu-ops-sfx-total-collapse =
    {-cmu-ops-sfx-muthur-header}
    {"["}bold][color=#ff3838]TOTAL STRUCTURAL COLLAPSE IN PROGRESS.[/color][/bold]
