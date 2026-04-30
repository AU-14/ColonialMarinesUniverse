cmu-medical-scanner-body-map-header        = Body Map
cmu-medical-scanner-pulse-label            = Pulse:
cmu-medical-scanner-body-parts-header      = Body parts
cmu-medical-scanner-organs-header          = Organs
cmu-medical-scanner-fractures-header       = Fractures
cmu-medical-scanner-bleeds-header          = Internal bleeding
cmu-medical-scanner-pulse-stopped          = [color=red][bold]No pulse — heart stopped[/bold][/color]
cmu-medical-scanner-pulse-bpm              = { $bpm } BPM
cmu-medical-scanner-part-line              = { $part }: { $current }/{ $max } HP
cmu-medical-scanner-part-suffix-splinted   = (splinted)
cmu-medical-scanner-part-suffix-cast       = (in cast)
cmu-medical-scanner-part-suffix-wounds     = ({ $count } wound{ $count ->
    [one] {""}
   *[other] s
})
cmu-medical-scanner-organ-line             = { $organ }: { $stage } ({ $current }/{ $max })
cmu-medical-scanner-organ-removed          = { $organ }: [color=red]REMOVED[/color]
cmu-medical-scanner-fracture-line-exact    = { $part }: { $severity } fracture
cmu-medical-scanner-fracture-line-vague    = { $part }: fracture detected
cmu-medical-scanner-fracture-suppressed    = (suppressed)
cmu-medical-scanner-bleed-exact            = { $part }: { $rate } bloodloss/sec
cmu-medical-scanner-bleed-vague            = Internal bleeding detected (location unknown)

cmu-medical-stethoscope-pulse              = Heart rate { $bpm }.
cmu-medical-stethoscope-pulse-qualitative  = Pulse is { $description }.
cmu-medical-stethoscope-no-pulse           = No heartbeat detected.
cmu-medical-stethoscope-no-heart           = There is no heart in the patient's chest.
cmu-medical-stethoscope-lungs-precise      = Lungs: { $stage }.
cmu-medical-stethoscope-lungs-qualitative  = Lungs sound { $description }.
cmu-medical-stethoscope-no-lungs           = There are no lungs in the patient's chest.

cmu-medical-scanner-section-head           = Head
cmu-medical-scanner-section-torso          = Torso
cmu-medical-scanner-section-arms           = Arms
cmu-medical-scanner-section-legs           = Legs
cmu-medical-scanner-section-organs         = Organs
cmu-medical-scanner-hp                     = HP
cmu-medical-scanner-bone                   = Bone
cmu-medical-scanner-fracture               = Fracture: { $severity }
cmu-medical-scanner-fracture-vague         = Fracture: detected
cmu-medical-scanner-bleed-internal         = Internal Bleed
cmu-medical-scanner-pain-unknown           = Pain: ?
cmu-medical-scanner-pain-none              = Pain: None
cmu-medical-scanner-pain-mild              = Pain: Mild
cmu-medical-scanner-pain-moderate          = Pain: Moderate
cmu-medical-scanner-pain-severe            = Pain: Severe
cmu-medical-scanner-pain-shock             = Pain: Shock

# V2-ε Stat-sheet redesign — dark cards + status banner + body chart
cmu-medical-scanner-card-body              = Body
cmu-medical-scanner-card-organs            = Organs
cmu-medical-scanner-card-reagents          = Reagents in bloodstream
cmu-medical-scanner-card-recommended       = Recommended

cmu-medical-scanner-stat-health            = HEALTH
cmu-medical-scanner-stat-pulse             = PULSE BPM
cmu-medical-scanner-stat-blood             = BLOOD
cmu-medical-scanner-stat-temp              = TEMP °C
cmu-medical-scanner-stat-pulse-stopped     = 0
cmu-medical-scanner-stat-deceased-short    = DEAD

cmu-medical-scanner-status-stable          = STABLE
cmu-medical-scanner-status-serious         = SERIOUS
cmu-medical-scanner-status-critical        = CRITICAL
cmu-medical-scanner-status-deceased        = DECEASED

cmu-medical-scanner-severity-healthy       = Healthy
cmu-medical-scanner-severity-bruised       = Bruised
cmu-medical-scanner-severity-damaged       = Damaged
cmu-medical-scanner-severity-critical      = Critical
cmu-medical-scanner-severity-severed       = Severed

cmu-medical-scanner-chip-fracture-vague    = Fracture
cmu-medical-scanner-chip-suppressed-suffix =  (suppr.)
cmu-medical-scanner-chip-bleed             = IB
cmu-medical-scanner-chip-bleeding          = Bleeding
cmu-medical-scanner-chip-splint            = Splint
cmu-medical-scanner-chip-cast              = Cast
cmu-medical-scanner-chip-tourniquet        = TQ
cmu-medical-scanner-wound-small            = small wound
cmu-medical-scanner-wound-deep             = deep wound
cmu-medical-scanner-wound-gaping           = gaping wound
cmu-medical-scanner-wound-massive          = massive wound
cmu-medical-scanner-eschar                 = eschar
cmu-medical-scanner-chip-wounds            = { $count } wound{ $count ->
    [one] {""}
   *[other] s
}

# Skill-gate hints — surface what the examiner can't read so the medic
# knows whether to study up rather than assuming the patient is fine.
cmu-medical-scanner-skill-hint-fractures   = Insufficient training to detect fractures or internal bleeding (Med-1 required).
cmu-medical-scanner-skill-hint-organs      = Insufficient training to assess organ damage (Med-2 required).

# Legacy V2-ε Mix B keys (still referenced by tests / fallback paths)
cmu-medical-scanner-vitals-pain            = Pain
cmu-medical-scanner-stable-summary         = Stable: { $list }
cmu-medical-scanner-acute-issues-header    = Acute Issues
cmu-medical-scanner-acute-severed          = Severed: { $part }
cmu-medical-scanner-acute-fracture         = { $severity } fracture: { $part }
cmu-medical-scanner-acute-fracture-vague   = Fracture: { $part }
cmu-medical-scanner-acute-bleed            = Internal bleed: { $part }
cmu-medical-scanner-acute-bleed-vague      = Internal bleeding detected
cmu-medical-scanner-acute-organ            = { $stage }: { $organ }
cmu-medical-scanner-acute-organ-removed    = Removed: { $organ }
cmu-medical-scanner-organ-removed-short    = Removed

# Organ display names — friendly labels keyed off the CMUOrganHuman*
# prototype ids. Per-organ keys keep the locale layer the only place
# that needs editing if we rename for V2.5.
cmu-medical-scanner-organ-heart            = Heart
cmu-medical-scanner-organ-lungs            = Lungs
cmu-medical-scanner-organ-liver            = Liver
cmu-medical-scanner-organ-brain            = Brain
cmu-medical-scanner-organ-kidneys          = Kidneys
cmu-medical-scanner-organ-stomach          = Stomach
cmu-medical-scanner-organ-eyes             = Eyes
cmu-medical-scanner-organ-ears             = Ears

cmu-medical-stethoscope-pain-mild          = The patient seems uncomfortable.
cmu-medical-stethoscope-pain-moderate      = The patient is in noticeable pain.
cmu-medical-stethoscope-pain-severe        = The patient is in heavy pain.
cmu-medical-stethoscope-pain-shock         = The patient is in shock.
