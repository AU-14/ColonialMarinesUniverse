# Better character examine (ported from WWDP), chat-log only

examine-name = It's [bold]{$name}[/bold]!
examine-can-see = Looking at {OBJECT($ent)}, you can see:
examine-can-see-nothing = {CAPITALIZE(GENDER($ent))} is completely naked!

id-examine = - {CAPITALIZE(POSS-ADJ($ent))} [color=silver][bold]{$item}[/bold][/color] on {POSS-ADJ($ent)} belt.
head-examine = - {CAPITALIZE(POSS-ADJ($ent))} [color=silver][bold]{$item}[/bold][/color] on {POSS-ADJ($ent)} head.
eyes-examine = - {CAPITALIZE(POSS-ADJ($ent))} [color=silver][bold]{$item}[/bold][/color] on {POSS-ADJ($ent)} eyes.
mask-examine = - {CAPITALIZE(POSS-ADJ($ent))} [color=silver][bold]{$item}[/bold][/color] on {POSS-ADJ($ent)} face.
neck-examine = - {CAPITALIZE(POSS-ADJ($ent))} [color=silver][bold]{$item}[/bold][/color] on {POSS-ADJ($ent)} neck.
ears-examine = - {CAPITALIZE(POSS-ADJ($ent))} [color=silver][bold]{$item}[/bold][/color] on {POSS-ADJ($ent)} left ear.
ears2-examine = - {CAPITALIZE(POSS-ADJ($ent))} [color=silver][bold]{$item}[/bold][/color] on {POSS-ADJ($ent)} right ear.
jumpsuit-examine = - {CAPITALIZE(POSS-ADJ($ent))} [color=silver][bold]{$item}[/bold][/color] {SUBJECT($ent)} is wearing.
outer-examine = - {CAPITALIZE(POSS-ADJ($ent))} [color=silver][bold]{$item}[/bold][/color] on {POSS-ADJ($ent)} body.
suitstorage-examine = - {CAPITALIZE(POSS-ADJ($ent))} [color=silver][bold]{$item}[/bold][/color] on {POSS-ADJ($ent)} shoulder.
back-examine = - {CAPITALIZE(POSS-ADJ($ent))} [color=silver][bold]{$item}[/bold][/color] on {POSS-ADJ($ent)} back.
gloves-examine = - {CAPITALIZE(POSS-ADJ($ent))} [color=silver][bold]{$item}[/bold][/color] on {POSS-ADJ($ent)} hands.
belt-examine = - {CAPITALIZE(POSS-ADJ($ent))} [color=silver][bold]{$item}[/bold][/color] {SUBJECT($ent)} is wearing.
shoes-examine = - {CAPITALIZE(POSS-ADJ($ent))} [color=silver][bold]{$item}[/bold][/color] on {POSS-ADJ($ent)} feet.

hand-left-examine = - {CAPITALIZE(POSS-ADJ($ent))} [color=silver][bold]{$item}[/bold][/color] in {POSS-ADJ($ent)} left hand.
hand-right-examine = - {CAPITALIZE(POSS-ADJ($ent))} [color=silver][bold]{$item}[/bold][/color] in {POSS-ADJ($ent)} right hand.
hand-middle-examine = - {CAPITALIZE(POSS-ADJ($ent))} [color=silver][bold]{$item}[/bold][/color] in one of {POSS-ADJ($ent)} hands.

id-card-examine-full = - {CAPITALIZE(POSS-ADJ($wearer))} ID: [color=silver][bold]{$nameAndJob}[/bold][/color].

# Selfaware version

examine-name-selfaware = It's you, [bold]{$name}[/bold]!
examine-can-see-selfaware = Looking at yourself, you can see:
examine-can-see-nothing-selfaware = You are completely naked!

id-examine-selfaware = - Your [color=silver][bold]{$item}[/bold][/color] on your belt.
head-examine-selfaware = - Your [color=silver][bold]{$item}[/bold][/color] on your head.
eyes-examine-selfaware = - Your [color=silver][bold]{$item}[/bold][/color] on your eyes.
mask-examine-selfaware = - Your [color=silver][bold]{$item}[/bold][/color] on your face.
neck-examine-selfaware = - Your [color=silver][bold]{$item}[/bold][/color] on your neck.
ears-examine-selfaware = - Your [color=silver][bold]{$item}[/bold][/color] on your left ear.
ears2-examine-selfaware = - Your [color=silver][bold]{$item}[/bold][/color] on your right ear.
jumpsuit-examine-selfaware = - Your [color=silver][bold]{$item}[/bold][/color] you are wearing.
outer-examine-selfaware = - Your [color=silver][bold]{$item}[/bold][/color] on your body.
suitstorage-examine-selfaware = - Your [color=silver][bold]{$item}[/bold][/color] on your shoulder.
back-examine-selfaware = - Your [color=silver][bold]{$item}[/bold][/color] on your back.
gloves-examine-selfaware = - Your [color=silver][bold]{$item}[/bold][/color] on your hands.
belt-examine-selfaware = - Your [color=silver][bold]{$item}[/bold][/color] you are wearing.
shoes-examine-selfaware = - Your [color=silver][bold]{$item}[/bold][/color] on your feet.

hand-left-examine-selfaware = - Your [color=silver][bold]{$item}[/bold][/color] in your left hand.
hand-right-examine-selfaware = - Your [color=silver][bold]{$item}[/bold][/color] in your right hand.
hand-middle-examine-selfaware = - Your [color=silver][bold]{$item}[/bold][/color] in one of your hands.

# Selfaware examine

comp-hands-examine-empty-selfaware = You are not holding anything.
comp-hands-examine-selfaware = You are holding { $items }.

humanoid-appearance-component-examine-selfaware = You are { INDEFINITE($age) } { $age } { $species }.
