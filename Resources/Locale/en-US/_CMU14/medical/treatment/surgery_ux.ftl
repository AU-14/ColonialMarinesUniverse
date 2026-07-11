# V2-β surgery UX strings.
# - Window header / hint
# - Armed-step status line
# - Wrong-tool / wrong-part / no-tool popups
# - Tool category names (resolver categories from SharedCMUSurgeryFlowSystem)
# - Per-step labels for all 19 V1 CMU surgeries

# ---- Window chrome ---------------------------------------------------

cmu-medical-surgery-window-title = Surgical Procedure
cmu-medical-surgery-window-hint = Pick a body part, pick a surgery, then click the patient with the required tool.
cmu-medical-surgery-no-eligible = No surgeries available here.
cmu-medical-surgery-section-patient = Patient
cmu-medical-surgery-section-workflow = Workflow
cmu-medical-surgery-workflow-ready = No active procedure selected.
cmu-medical-surgery-workflow-active = { $surgery } active on { $part }.
cmu-medical-surgery-section-parts = Body Parts
cmu-medical-surgery-section-surgeries = Surgeries
cmu-medical-surgery-section-surgeries-on = Surgeries on { $part }
cmu-medical-surgery-no-part-selected = Select a body part.
cmu-medical-surgery-procedure-detail = { $step } / { $tool }
cmu-medical-surgery-arm-button = Begin Surgery
cmu-medical-surgery-cancel-armed = Cancel Surgery
cmu-medical-surgery-step-hint = Step { $step }/{ $total } — { $label } ({ $tool })
cmu-medical-surgery-step-hint-prereq = Prerequisite step { $step }/{ $total } — { $label } ({ $tool })
cmu-medical-surgery-armed-heading = ARMED

# ---- In-progress hero panel ------------------------------------------

cmu-medical-surgery-in-progress-heading = IN PROGRESS
cmu-medical-surgery-in-progress-subtitle = { $surgery } · { $part }
cmu-medical-surgery-in-progress-credit = Last step by { $surgeon } · started { $elapsed } ago
cmu-medical-surgery-step-now = Step { $step }: { $label }
cmu-medical-surgery-action-hint = Click { $part } with a { $tool }.
cmu-medical-surgery-action-hint-no-tool = Click { $part } to continue.
cmu-medical-surgery-choose-next-heading = Choose next surgery
cmu-medical-surgery-choose-next-hint = Continue another repair on this open part, or close them up.
cmu-medical-surgery-continue-with-button = Continue with { $surgery }
cmu-medical-surgery-close-up-button = Close Up
cmu-medical-surgery-continue-button = Continue Surgery
cmu-medical-surgery-abandon-button = Abandon Surgery
cmu-medical-surgery-actions-heading = Actions

# ---- Per-part section labels -----------------------------------------

cmu-medical-surgery-part-heading = { $part }
cmu-medical-surgery-part-condition-healthy = Healthy
cmu-medical-surgery-part-condition-locked = Other surgery in progress on { $other } — finish or abandon first
cmu-medical-surgery-part-condition-no-eligible = No surgeries available

cmu-medical-surgery-condition-incision-open = Open incision
cmu-medical-surgery-condition-ribcage-open = Open ribcage
cmu-medical-surgery-condition-skull-open = Open skull
cmu-medical-surgery-condition-bones-open = Open bones
cmu-medical-surgery-condition-fracture = { $severity } fracture
cmu-medical-surgery-condition-internal-bleed = Internal bleeding
cmu-medical-surgery-condition-eschar = Eschar
cmu-medical-surgery-condition-wounds = Wounds
cmu-medical-surgery-condition-damaged = Damaged
cmu-medical-surgery-condition-vascular-tear = Torn vessel
cmu-medical-surgery-condition-embedded-foreign-body = Foreign body
cmu-medical-surgery-condition-compartment-pressure = Compartment pressure
cmu-medical-surgery-condition-contaminated-wound = Contaminated wound
cmu-medical-surgery-condition-bone-splinters = Bone splinters
cmu-medical-surgery-condition-organ-adhesion = Organ adhesions
cmu-medical-surgery-condition-organ-hemorrhage = Organ hemorrhage
cmu-medical-surgery-condition-in-progress = Surgery in progress
cmu-medical-surgery-condition-missing = Severed

# ---- BUI category headers ---------------------------------------------

cmu-medical-surgery-category-fracture = Fracture
cmu-medical-surgery-category-bleed = Internal Bleeding
cmu-medical-surgery-category-burn = Burns
cmu-medical-surgery-category-remove_organ = Remove Organ
cmu-medical-surgery-category-transplant = Transplant Organ
cmu-medical-surgery-category-suture = Suture Organ
cmu-medical-surgery-category-head_organ = Head Surgery
cmu-medical-surgery-category-amputation = Remove Limb
cmu-medical-surgery-category-reattach = Reattach Limb
cmu-medical-surgery-category-parasite = Parasite Removal
cmu-medical-surgery-category-close_up = Close Up
cmu-medical-surgery-category-general = Other

# ---- Examine surface (CMUSurgeryStateExamineSystem) ------------------

cmu-medical-surgery-examine-patient-in-progress = [color=#dca94c]{ $surgery } in progress (last step by { $surgeon }) — next: { $next }.[/color]
cmu-medical-surgery-examine-part-in-progress = [color=#dca94c]{ $surgery } in progress (last step by { $surgeon }) — next: { $next }.[/color]
cmu-medical-surgery-examine-part-abandoned = [color=#888888]Open wound — no surgery in progress.[/color]

cmu-medical-surgery-examine-incision = [color=#888888]There is a surgical incision on the { $part }.[/color]
cmu-medical-surgery-examine-site-details = [color=#dca94c]{ $part }: { $access }; { $hemostasis }; current step: { $step }.[/color]
cmu-medical-surgery-examine-no-active-step = no active procedure
cmu-medical-surgery-access-closed = closed
cmu-medical-surgery-access-incised = incision only
cmu-medical-surgery-access-shallow = shallow access
cmu-medical-surgery-access-bone-cut = bone cut, not yet opened
cmu-medical-surgery-access-deep = deep access
cmu-medical-surgery-hemostasis-none = no surgical bleeding
cmu-medical-surgery-hemostasis-uncontrolled = uncontrolled surgical bleeding
cmu-medical-surgery-hemostasis-clamped = bleeders clamped

# ---- Close-up step labels (RMC fallback resolution) ------------------

cmu-medical-surgery-step-close-incision-label = Close Incision
cmu-medical-surgery-step-mend-ribcage-label = Mend Ribcage
cmu-medical-surgery-step-mend-skull-label = Mend Skull
cmu-medical-surgery-step-mend-bones-label = Mend Bones
cmu-medical-surgery-step-close-bones-label = Close Bones

# ---- Armed-step status -----------------------------------------------

cmu-medical-surgery-armed-none = (no surgery armed)
cmu-medical-surgery-armed-step = Armed: { $surgery } — Step { $step } ({ $tool })
cmu-medical-surgery-armed-cancelled = Surgery cancelled.
cmu-medical-surgery-armed-expired = The surgery pick timed out.
cmu-medical-surgery-auto-armed = Selected { $surgery }.
cmu-medical-surgery-ui-less-select-part = Select a body part before using a surgery tool.
cmu-medical-surgery-ui-less-no-action = That tool has no clear action at the selected surgical site.
cmu-medical-surgery-unclamped-closure = The incision is closed over uncontrolled bleeding, causing internal bleeding.
cmu-medical-surgery-amputation-cancelled = You pack the incision and cancel the pending amputation.
cmu-medical-surgery-auto-continue = Continuing with { $surgery }.
cmu-medical-surgery-choose-repair-or-close = Choose an organ repair or close them up.

# ---- Click-target popups ---------------------------------------------

cmu-medical-surgery-wrong-part = That isn't the part you armed the surgery on.
cmu-medical-surgery-wrong-tool = That isn't the right tool for this step.
cmu-medical-surgery-wrong-tool-damage = You slip with the { $tool }!
cmu-medical-surgery-improvised-mishap = The improvised { $tool } slips and causes extra trauma.
cmu-medical-surgery-step-failed = The operation slips and causes trauma.
cmu-medical-surgery-step-failed-with-tool = The { $tool } slips and causes surgical trauma.
cmu-medical-surgery-no-tool = You need a surgical tool to perform this step.
cmu-medical-surgery-missing-skills = You don't know how to perform this step.
cmu-medical-surgery-cannot-start = That surgery is no longer available.
cmu-medical-surgery-step-busy = Another surgery action is already in progress on this patient.
cmu-medical-surgery-needs-operating-table = Move them to an operating table first.
cmu-medical-surgery-remove-helmet = Remove their helmet first.
cmu-medical-surgery-remove-armor = Remove the obstructing armor first.
cmu-medical-surgery-wrong-limb = That limb doesn't match any empty slot on the patient.
cmu-medical-surgery-welder-not-lit = Light the tool first.
cmu-medical-surgery-patient-not-lying = The patient must be lying down or strapped to a surgery table.
cmu-medical-surgery-patient-not-controlled = The patient needs anesthesia, strong painkillers, or restraints before surgery.
cmu-medical-surgery-self-pain-control = Self-surgery requires strong painkillers first.
cmu-medical-surgery-self-not-secured = Buckle yourself to a chair, bed, or roller before attempting self-surgery.
cmu-medical-surgery-self-not-allowed = You can't perform that surgery on yourself.
cmu-medical-surgery-step-pain-uncontrolled = The patient is in too much pain to continue surgery. Use anesthesia or strong painkillers before trying again.
cmu-medical-amputation-success = The limb is removed.

# ---- Tool category names (used in the BUI button + armed line) -------

cmu-medical-surgery-tool-category-scalpel = Scalpel
cmu-medical-surgery-tool-category-hemostat = Hemostat
cmu-medical-surgery-tool-category-retractor = Retractor
cmu-medical-surgery-tool-category-cautery = Cautery
cmu-medical-surgery-tool-category-bone_saw = Bone Saw
cmu-medical-surgery-tool-category-bone_setter = Bone Setter
cmu-medical-surgery-tool-category-bone_gel = Bone Gel
cmu-medical-surgery-tool-category-bone_graft = Bone Graft
cmu-medical-surgery-tool-category-fix_o_vein = Fix-O-Vein
cmu-medical-surgery-tool-category-organ_clamp = Organ Clamp
cmu-medical-surgery-tool-category-scalpel_or_burn_kit = Scalpel or burn kit
cmu-medical-surgery-tool-category-severed_limb = Matching Limb
cmu-medical-surgery-tool-category-blowtorch = Lit Welder
cmu-medical-surgery-tool-category-cable_coil = Cable Coil

# ---- Per-step labels -------------------------------------------------

cmu-medical-surgery-step-realign-simple-label = Realign Simple Fracture
cmu-medical-surgery-step-realign-compound-label = Realign Compound Fracture
cmu-medical-surgery-step-realign-shattered-label = Realign Shattered Fracture
cmu-medical-surgery-step-apply-gel-label = Apply Bone Gel
cmu-medical-surgery-step-apply-gel-second-label = Apply Bone Gel (Second Layer)
cmu-medical-surgery-step-insert-graft-label = Insert Bone Graft
cmu-medical-surgery-step-cauterize-bleed-label = Mend Internal Bleed
cmu-medical-surgery-step-tie-vessel-label = Tie Off Torn Vessel
cmu-medical-surgery-step-extract-foreign-body-label = Extract Foreign Body
cmu-medical-surgery-step-relieve-pressure-label = Relieve Compartment Pressure
cmu-medical-surgery-step-debride-contamination-label = Debride Contaminated Tissue
cmu-medical-surgery-step-remove-bone-fragments-label = Remove Bone Fragments
cmu-medical-surgery-step-free-organ-adhesions-label = Free Organ Adhesions
cmu-medical-surgery-step-pack-organ-bleed-label = Pack Organ Bleed
cmu-medical-surgery-step-clamp-liver-label = Clamp Liver Vessels
cmu-medical-surgery-step-clamp-lungs-label = Clamp Lung Vessels
cmu-medical-surgery-step-clamp-kidneys-label = Clamp Kidney Vessels
cmu-medical-surgery-step-clamp-heart-label = Clamp Heart Vessels
cmu-medical-surgery-step-clamp-stomach-label = Clamp Stomach Vessels
cmu-medical-surgery-step-extract-liver-label = Extract Liver
cmu-medical-surgery-step-extract-lungs-label = Extract Lungs
cmu-medical-surgery-step-extract-kidneys-label = Extract Kidneys
cmu-medical-surgery-step-extract-heart-label = Extract Heart
cmu-medical-surgery-step-extract-stomach-label = Extract Stomach
cmu-medical-surgery-step-reinsert-liver-label = Insert Replacement Liver
cmu-medical-surgery-step-reinsert-lungs-label = Insert Replacement Lungs
cmu-medical-surgery-step-reinsert-kidneys-label = Insert Replacement Kidneys
cmu-medical-surgery-step-reinsert-stomach-label = Insert Replacement Stomach
cmu-medical-surgery-step-transplant-heart-label = Transplant Donor Heart
cmu-medical-surgery-step-suture-liver-label = Suture Liver
cmu-medical-surgery-step-suture-lungs-label = Suture Lungs
cmu-medical-surgery-step-suture-kidneys-label = Suture Kidneys
cmu-medical-surgery-step-suture-heart-label = Suture Heart
cmu-medical-surgery-step-suture-stomach-label = Suture Stomach
cmu-medical-surgery-step-amputate-limb-label = Amputate Limb
cmu-medical-surgery-step-reattach-limb-label = Reattach Severed Limb

# ---- Autodoc ---------------------------------------------------------

cmu-autodoc-window-title = Autodoc
cmu-autodoc-no-patient = No patient
cmu-autodoc-status-no-pod = No autodoc pod linked nearby.
cmu-autodoc-status-empty = Linked pod is empty.
cmu-autodoc-status-ready = Ready to queue automated procedures.
cmu-autodoc-status-running = Executing queued procedures.
cmu-autodoc-current-idle = Current procedure: idle
cmu-autodoc-current-step = Current procedure: { $step }
cmu-autodoc-current-step-timed = Current procedure: { $step } ({ $time } remaining)
cmu-autodoc-current-step-detail = { $surgery } / { $part } / { $step }
cmu-autodoc-start-button = Start
cmu-autodoc-stop-button = Stop
cmu-autodoc-clear-button = Clear
cmu-autodoc-eject-button = Eject Patient
cmu-autodoc-remove-button = Remove
cmu-autodoc-queue-button = Queue
cmu-autodoc-queue-heading = Queue
cmu-autodoc-parts-heading = Parts
cmu-autodoc-surgeries-heading = Surgeries
cmu-autodoc-queue-empty = No queued procedures.
cmu-autodoc-queue-summary = { $count } queued procedure(s)
cmu-autodoc-available-procedures = { $count } available procedure(s)
cmu-autodoc-part-procedures = { $count } procedure(s)
cmu-autodoc-surgery2-required = Surgery 2 training is required to queue autodoc steps.
cmu-autodoc-no-surgeries = No surgeries available here.
cmu-autodoc-queue-row = #{ $index } { $surgery } on { $part } - { $step }
cmu-autodoc-surgery-row = { $surgery } - { $step }
cmu-autodoc-automated-step-label = Automated repair cycle
cmu-autodoc-automated-step-note = Autodoc repairs this target on a machine timer.
cmu-autodoc-repair-wounds-surgery = Repair Wounds / Burns
cmu-autodoc-procedure-time-note = { $time } automated procedure.
cmu-autodoc-minutes = { $minutes } min

# ---- Body scanner ----------------------------------------------------

cmu-body-scanner-window-title = Body Scanner
cmu-body-scanner-no-patient = No patient
cmu-body-scanner-status-no-pod = No body scanner pod linked nearby.
cmu-body-scanner-status-empty = Linked scanner pod is empty.
cmu-body-scanner-status-ready = Patient scan ready.
cmu-body-scanner-status-no-skill = Surgery 1 training is required to complete scans.
cmu-body-scanner-boost-active = Surgical assist calibrated: { $time } remaining.
cmu-body-scanner-boost-inactive = Surgical assist not calibrated.
cmu-body-scanner-scan-heading = Scan
cmu-body-scanner-terms-heading = Slice Layers
cmu-body-scanner-targets-heading = Active Slice Readings
cmu-body-scanner-start-button = Start Calibration
cmu-body-scanner-reset-button = Reset Calibration
cmu-body-scanner-eject-button = Eject Patient
cmu-body-scanner-surgery1-required = Surgery 1 training is required for body scans.
cmu-body-scanner-no-scan-lines = No scan data.
cmu-body-scanner-diagnostic-summary = { $count } diagnostic line(s)
cmu-body-scanner-match-summary = { $matched }/{ $required } locked, { $time } remaining
cmu-body-scanner-match-summary-idle = { $matched }/{ $required } locked, not started
cmu-body-scanner-calibrated-summary = Calibrated, { $time } assist remaining
cmu-body-scanner-calibrated-badge = CALIBRATED { $time }
cmu-body-scanner-calibration-ready = 2:00
cmu-body-scanner-lockout-summary = Active slice locked, { $time } remaining
cmu-body-scanner-lockout-status = Active slice locked: { $time } remaining.
cmu-body-scanner-lockout-detail = Calibration failed. Wait for the lockout to clear.
cmu-body-scanner-no-surgical-targets = No targets detected.
cmu-body-scanner-no-surgical-targets-detail = No boost awarded.
cmu-body-scanner-calibration-heading = Anatomical Slice Scan
cmu-body-scanner-sweep-title = Layered scanner sweep
cmu-body-scanner-sweep-detail = Tune a slice to begin.
cmu-body-scanner-layer-selected = Tuned slice - { $locked }/{ $total } locked
cmu-body-scanner-layer-ready = { $locked }/{ $total } locked
cmu-body-scanner-layer-empty = No abnormal readings
cmu-body-scanner-signal-locked = Signal locked
cmu-body-scanner-signal-ready = { $detail } - lock on cyan
cmu-body-scanner-start-status = Start calibration to begin the slice scan.
cmu-body-scanner-ready-status = Tune a slice, then lock abnormal readings while the sweep is cyan.
cmu-body-scanner-armed-status = Slice tuned: { $layer }. Lock readings as the sweep enters cyan.
cmu-body-scanner-penalty-status = Bad timing or slice: -{ $seconds }s.
cmu-body-scanner-feedback-correct = Signal locked.
cmu-body-scanner-feedback-wrong-timing = Sweep missed the capture band: -{ $seconds }s.
cmu-body-scanner-feedback-wrong-layer = Layer interference: -{ $seconds }s.
cmu-body-scanner-expired-status = Time expired. Reset calibration to retry.
cmu-body-scanner-complete-status = All readings locked. Surgical assist calibrated.
cmu-body-scanner-timer-active = ACTIVE SLICE TIMER
cmu-body-scanner-timer-expired = TIMER EXPIRED
cmu-body-scanner-timer-locked = SLICE LOCKED
cmu-body-scanner-timer-detail = Lock the readings before the scan window closes.
cmu-body-scanner-no-layer-signals = No abnormal readings on { $layer }.
cmu-body-scanner-interference-title = Unresolved reading
cmu-body-scanner-interference-detail = Interference on { $layer }
cmu-body-scanner-decoy-ready = { $detail } - noisy echo
cmu-body-scanner-decoy-vitals-1 = Cardiac echo spike
cmu-body-scanner-decoy-vitals-2 = Blood oxygen shimmer
cmu-body-scanner-decoy-detail-vitals = transient vital artifact
cmu-body-scanner-decoy-skeleton-1 = Hairline bone shadow
cmu-body-scanner-decoy-skeleton-2 = Joint alignment ghost
cmu-body-scanner-decoy-detail-skeleton = unstable bone silhouette
cmu-body-scanner-decoy-organs-1 = Soft organ bloom
cmu-body-scanner-decoy-organs-2 = Density reflection
cmu-body-scanner-decoy-detail-organs = inconsistent organ density
cmu-body-scanner-decoy-tissue-1 = Surface tissue flare
cmu-body-scanner-decoy-tissue-2 = Vascular noise band
cmu-body-scanner-decoy-detail-tissue = noisy soft-tissue return
cmu-body-scanner-triage-stable = Stable readout
cmu-body-scanner-triage-serious = Serious findings
cmu-body-scanner-triage-critical = Critical findings
cmu-body-scanner-triage-clear = No immediate abnormal findings.
cmu-body-scanner-health-stable = Stable
cmu-body-scanner-health-damaged = Damaged
cmu-body-scanner-health-critical = Critical
cmu-body-scanner-section-vitals = Vitals
cmu-body-scanner-section-body = Body
cmu-body-scanner-section-organs = Organs
cmu-body-scanner-term-assigned = { $term } -> { $target }
cmu-body-scanner-target-filled = { $target }: { $term }
cmu-body-scanner-line-state = State: { $state }
cmu-body-scanner-line-damage = Damage: total { $total } (brute { $brute }, burn { $burn })
cmu-body-scanner-line-blood = Blood: { $blood } / { $max }
cmu-body-scanner-heart-stopped = Heart: no activity detected
cmu-body-scanner-heart-active = Heart: { $bpm } bpm
cmu-body-scanner-line-no-data = No diagnostic data available.
cmu-body-scanner-line-part = { $part }: { $details }
cmu-body-scanner-part-health = HP { $current } / { $max }
cmu-body-scanner-part-wounds = { $count } untreated wound(s)
cmu-body-scanner-part-fracture = { $severity } fracture
cmu-body-scanner-part-bleed = internal bleed { $rate }/s
cmu-body-scanner-part-eschar = eschar
cmu-body-scanner-part-splinted = splinted
cmu-body-scanner-part-cast = casted
cmu-body-scanner-part-tourniquet = tourniqueted
cmu-body-scanner-part-missing-limb = missing / severed limb
cmu-body-scanner-line-organ = { $organ }: { $stage } ({ $current } / { $max })
cmu-body-scanner-line-missing-organ = Missing { $organ } in { $part }
cmu-body-scanner-title-state = State
cmu-body-scanner-title-damage = Damage
cmu-body-scanner-title-blood = Blood
cmu-body-scanner-title-heart = Heart
cmu-body-scanner-title-no-data = Diagnostics
cmu-body-scanner-title-missing-organ = Missing { $organ }
cmu-body-scanner-detail-damage = total { $total } (brute { $brute }, burn { $burn })
cmu-body-scanner-detail-blood = { $blood } / { $max }
cmu-body-scanner-detail-heart-stopped = no activity detected
cmu-body-scanner-detail-heart-active = { $bpm } bpm
cmu-body-scanner-detail-no-data = No diagnostic data available.
cmu-body-scanner-detail-organ = { $stage } ({ $current } / { $max })
cmu-body-scanner-detail-missing-organ = in { $part }
cmu-body-scanner-signal-heart-stopped = Heart: no activity detected
cmu-body-scanner-signal-organ-damage = { $organ }: { $stage } organ damage
cmu-body-scanner-signal-low-blood = Blood volume low: { $blood } / { $max }
cmu-body-scanner-signal-internal-bleed = { $part }: internal bleed { $rate }/s
cmu-body-scanner-signal-fracture = { $part }: { $severity } fracture
cmu-body-scanner-signal-wounds = { $part }: { $count } untreated wound(s)
cmu-body-scanner-signal-trauma = { $part }: tissue trauma { $current } / { $max }
cmu-body-scanner-signal-missing-organ = Missing { $organ } in { $part }
cmu-body-scanner-signal-missing-limb = { $part }: missing / severed limb
cmu-body-scanner-slice-detail-cardiac = cardiac rhythm
cmu-body-scanner-slice-detail-organ = organ density
cmu-body-scanner-slice-detail-blood = blood volume
cmu-body-scanner-slice-detail-bleed = tissue flow
cmu-body-scanner-slice-detail-fracture = bone alignment
cmu-body-scanner-slice-detail-wound = tissue disruption
cmu-body-scanner-slice-detail-trauma = soft tissue density
cmu-body-scanner-slice-detail-missing-organ = organ silhouette
cmu-body-scanner-slice-detail-missing-limb = limb silhouette

cmu-limb-printer-window-title = Limb Printer
cmu-limb-printer-header = Limb Fabrication
cmu-limb-printer-matrix-heading = Synthesis Matrix
cmu-limb-printer-blood-heading = Blood Template
cmu-limb-printer-metal-heading = Robotic Frame Stock
cmu-limb-printer-metal-type = Metal Sheets
cmu-limb-printer-no-beaker = No matrix beaker inserted.
cmu-limb-printer-no-syringe = No blood syringe inserted.
cmu-limb-printer-no-metal = No metal sheets inserted.
cmu-limb-printer-fluid-amount = { $current } / { $max }u
cmu-limb-printer-stack-amount = { $current } / { $max }
cmu-limb-printer-matrix-cost = { $cost }u matrix per print
cmu-limb-printer-blood-cost = { $cost }u blood per print
cmu-limb-printer-metal-cost = { $cost } sheets per robotic print
cmu-limb-printer-remove-beaker = Remove Beaker
cmu-limb-printer-remove-syringe = Remove Syringe
cmu-limb-printer-remove-metal = Remove Metal
cmu-limb-printer-left-heading = Left
cmu-limb-printer-right-heading = Right
cmu-limb-printer-print-ready = Ready to print
cmu-limb-printer-status-ready = Ready to synthesize.
cmu-limb-printer-missing-beaker = Insert a beaker of biogenic matrix.
cmu-limb-printer-missing-matrix = Biogenic matrix is too low.
cmu-limb-printer-missing-syringe = Insert a syringe containing patient blood.
cmu-limb-printer-missing-blood = Patient blood sample is too low.
cmu-limb-printer-missing-metal-slot = Insert metal sheets.
cmu-limb-printer-missing-metal = Metal sheets are too low.
cmu-limb-printer-wrong-metal = Insert metal sheets, not base steel.
cmu-limb-printer-printed = Printed { $limb }.
cmu-limb-printer-left-arm = Left arm
cmu-limb-printer-left-leg = Left leg
cmu-limb-printer-right-arm = Right arm
cmu-limb-printer-right-leg = Right leg
cmu-limb-printer-left-robotic-arm = Left robotic arm
cmu-limb-printer-left-robotic-leg = Left robotic leg
cmu-limb-printer-right-robotic-arm = Right robotic arm
cmu-limb-printer-right-robotic-leg = Right robotic leg
cmu-limb-printer-slot-beaker = matrix beaker
cmu-limb-printer-slot-syringe = blood syringe
cmu-limb-printer-slot-metal = Metal Sheets
