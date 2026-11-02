cmd-yautjaannounce-desc = Sends an Elder Overseer message to the Yautja.
cmd-yautjaannounce-help = Usage: yautjaannounce <all|hunt|stranded> <message>. Shows on the screen of every living Yautja on that network. Check with online staff before sending.
cmd-yautjaannounce-usage = Usage: yautjaannounce <all|hunt|stranded> <message>
cmd-yautjaannounce-arg-network = <all|hunt|stranded>
cmd-yautjaannounce-arg-message = <message>
cmd-yautjaannounce-sent = Elder Overseer message sent to { $count } { $count ->
    [one] recipient
   *[other] recipients
}.

cmu-yautja-overseer-title-elder = Elder Overseer
cmu-yautja-overseer-title-stranded = Faint Broadcast
cmu-yautja-overseer-title-all = Unidentified Broadcast
cmu-yautja-overseer-message = [font size=16][bold][color=#af0614]{ $title }[/color][/bold][/font] [font size=14][bold][color=#af0614]{ $text }[/color][/bold][/font]
cmu-yautja-overseer-admin-alert = { $author } has created a Yautja Elder Overseer message for { $network }.
