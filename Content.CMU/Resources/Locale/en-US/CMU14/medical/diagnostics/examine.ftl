cmu-medical-examine-wound-line = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } { $wounds } on { POSS-ADJ($target) } { $part }.[/color]
cmu-medical-examine-fracture-line = [color=#dca94c]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } { $fracture } in { POSS-ADJ($target) } { $part }.[/color]
cmu-medical-examine-wounds-line = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } wounds: { $parts }.[/color]
cmu-medical-examine-fractures-line = [color=#dca94c]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-HAVE($target) } fractures: { $parts }.[/color]
cmu-medical-examine-body-part-line = { $part }: { $conditions }.
cmu-medical-detailed-examine-verb = Inspect injuries
cmu-medical-detailed-examine-verb-message = Take a closer look at their injuries.
cmu-medical-detailed-examine-start = You begin checking { THE($target) } for injuries.
cmu-medical-detailed-examine-none = No obvious injuries found.
cmu-medical-detailed-examine-window-title = Injuries - { $target }
cmu-medical-detailed-examine-window-heading = Injury report
cmu-medical-detailed-examine-window-bleeding = Bleeding: { $tier }

cmu-robotic-limb-material-synthetic = synthetic
cmu-robotic-limb-examine-state = prosthesis
cmu-robotic-limb-examine-brute = dented plating
cmu-robotic-limb-examine-burn = scorched wiring
cmu-robotic-limb-detailed-state = prosthesis
cmu-robotic-limb-detailed-brute = mechanical trauma: dented plating
cmu-robotic-limb-detailed-burn = thermal damage: scorched wiring
cmu-robotic-limb-inspect-header = Robotic Limb Damage

cmu-medical-examine-and = and
cmu-medical-examine-active-bleeding = active bleeding
cmu-medical-examine-charred-burn-tissue = charred burn tissue
cmu-medical-examine-severed = SEVERED
cmu-medical-detailed-examine-severed = severed
cmu-medical-examine-stump-tourniquet = tourniquet on the stump
cmu-medical-examine-stump-bleeding = the stump is squirting blood
cmu-medical-examine-external-bleeding-detailed = external bleeding: { $tier }
cmu-medical-examine-burn-eschar-detailed = burn eschar: charred tissue
cmu-medical-examine-burn-eschar-header = Burn Eschar
cmu-medical-examine-charred-tissue = charred tissue
cmu-medical-examine-arterial-bleeding-header = Arterial Bleeding

cmu-medical-examine-wound-indef = a { $stage }
cmu-medical-examine-treated-wounds = { $count ->
    [one] wound treated
   *[other] wounds treated
}

cmu-medical-examine-fracture-simple = a simple fracture
cmu-medical-examine-fracture-simple-stabilized = a stabilized simple fracture
cmu-medical-examine-fracture-compound = a compound fracture
cmu-medical-examine-fracture-compound-stabilized = a stabilized compound fracture
cmu-medical-examine-fracture-shattered = a shattered bone
cmu-medical-examine-fracture-shattered-stabilized = a stabilized shattered bone
cmu-medical-examine-fracture-default = a broken bone
cmu-medical-examine-fracture-hairline = hairline fracture
cmu-medical-examine-fracture-hairline-stabilized = stabilized hairline fracture

cmu-medical-examine-wound-title-bullet = Bullet Wounds
cmu-medical-examine-wound-title-stab = Stab Wounds
cmu-medical-examine-wound-title-slash = Slash Wounds
cmu-medical-examine-wound-title-crush = Crush Wounds
cmu-medical-examine-wound-title-burn = Burns
cmu-medical-examine-wound-title-blast = Blast Wounds
cmu-medical-examine-wound-title-fragment = Fragment Wounds
cmu-medical-examine-wound-title-surgical = Surgical Wounds
cmu-medical-examine-wound-title-default = Wounds

cmu-medical-examine-severity-minor = Minor
cmu-medical-examine-severity-moderate = Moderate
cmu-medical-examine-severity-severe = Severe
cmu-medical-examine-severity-massive = Massive

cmu-medical-examine-treated = treated
cmu-medical-examine-untreated = untreated
cmu-medical-examine-bleed-tier-none = none
cmu-medical-examine-bleed-tier-minor = minor
cmu-medical-examine-bleed-tier-moderate = moderate
cmu-medical-examine-bleed-tier-severe = severe
cmu-medical-examine-bleed-tier-arterial = arterial

cmu-medical-examine-part-head = Head
cmu-medical-examine-part-head-left = Left head
cmu-medical-examine-part-head-right = Right head
cmu-medical-examine-part-torso = Torso
cmu-medical-examine-part-torso-left = Left torso
cmu-medical-examine-part-torso-right = Right torso
cmu-medical-examine-part-arm = Arm
cmu-medical-examine-part-arm-left = Left arm
cmu-medical-examine-part-arm-right = Right arm
cmu-medical-examine-part-hand = Hand
cmu-medical-examine-part-hand-left = Left hand
cmu-medical-examine-part-hand-right = Right hand
cmu-medical-examine-part-leg = Leg
cmu-medical-examine-part-leg-left = Left leg
cmu-medical-examine-part-leg-right = Right leg
cmu-medical-examine-part-foot = Foot
cmu-medical-examine-part-foot-left = Left foot
cmu-medical-examine-part-foot-right = Right foot
cmu-medical-examine-part-tail = Tail
cmu-medical-examine-part-tail-left = Left tail
cmu-medical-examine-part-tail-right = Right tail
cmu-medical-examine-part-other = Other
cmu-medical-examine-part-other-left = Left other
cmu-medical-examine-part-other-right = Right other
