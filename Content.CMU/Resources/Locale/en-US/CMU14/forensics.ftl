cmu-forensic-scanner-copy = Copy
cmu-forensic-scanner-copy-tooltip = Copy to clipboard, then paste it into the criminal records search.
cmu-forensic-scanner-time-since-death = Time Since Death
cmu-forensic-scanner-matches = Record Matches
cmu-forensic-scanner-no-matches = No matching records

cmu-forensics-gunshot-residue = gunshot residue
cmu-forensics-death-recent = less than a minute ago
cmu-forensics-death-minutes = about {$minutes} {$minutes ->
    [one] minute
   *[other] minutes
} ago
cmu-forensics-match = Match: {$names}
cmu-forensics-no-match = No matching records.

cmu-forensic-scanner-card-death = Death
cmu-forensic-scanner-card-injuries = Injuries
cmu-forensic-scanner-card-evidence = Evidence
cmu-forensic-scanner-cause-of-death = Cause of death:
cmu-forensic-scanner-none = None found
cmu-forensic-scanner-severity-minor = MINOR
cmu-forensic-scanner-severity-moderate = MODERATE
cmu-forensic-scanner-severity-severe = SEVERE
cmu-forensic-scanner-severity-critical = CRITICAL
cmu-forensic-scanner-severity-missing = MISSING

cmu-forensics-cause-decapitation = Decapitation
cmu-forensics-cause-blood-loss = Blood loss
cmu-forensics-cause-gunshot = Gunshot wounds
cmu-forensics-cause-explosive = Explosive trauma
cmu-forensics-cause-stab = Stab wounds
cmu-forensics-cause-laceration = Lacerations
cmu-forensics-cause-blunt = Blunt force trauma
cmu-forensics-cause-piercing = Penetrating trauma
cmu-forensics-cause-burns = Burns
cmu-forensics-cause-hypothermia = Hypothermia
cmu-forensics-cause-electrocution = Electrocution
cmu-forensics-cause-chemical-burns = Chemical burns
cmu-forensics-cause-asphyxiation = Asphyxiation
cmu-forensics-cause-poisoning = Poisoning
cmu-forensics-cause-radiation = Radiation poisoning
cmu-forensics-cause-cellular = Cellular damage
cmu-forensics-cause-unknown = Undetermined

cmu-forensics-wound-gunshot = gunshot wound
cmu-forensics-wound-stab = stab wound
cmu-forensics-wound-laceration = laceration
cmu-forensics-wound-blunt = blunt trauma
cmu-forensics-wound-burn = burn
cmu-forensics-wound-blast = blast wound
cmu-forensics-wound-shrapnel = shrapnel wound
cmu-forensics-wound-surgical = surgical incision
cmu-forensics-wound-bruise = bruise
cmu-forensics-wound-stump = stump
cmu-forensics-wound-generic = wound

cmu-forensics-injury-wound = { $count ->
    [one] { $kind }
   *[other] { $count } { $kind }s
} (worst: { $worst }){ $treated ->
    [yes] , treated
   *[other] {""}
}
cmu-forensics-injury-bleeding = { $tier } external bleeding
cmu-forensics-injury-internal-bleeding = internal bleeding
cmu-forensics-injury-fracture = { $severity } fracture
cmu-forensics-injury-shrapnel = { $count } { $count ->
    [one] fragment
   *[other] fragments
} of shrapnel lodged
cmu-forensics-injury-missing = missing
