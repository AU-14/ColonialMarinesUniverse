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
