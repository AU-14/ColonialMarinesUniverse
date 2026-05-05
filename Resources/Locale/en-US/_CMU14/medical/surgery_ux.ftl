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
cmu-medical-surgery-in-progress-credit = Started by { $surgeon } · { $elapsed } ago
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

cmu-medical-surgery-examine-patient-in-progress = [color=#dca94c]{ $surgery } in progress (by { $surgeon }) — next: { $next }.[/color]
cmu-medical-surgery-examine-part-in-progress = [color=#dca94c]{ $surgery } in progress (by { $surgeon }) — next: { $next }.[/color]
cmu-medical-surgery-examine-part-abandoned = [color=#888888]Open wound — no surgery in progress.[/color]

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
cmu-medical-surgery-step-pain-interrupted = The patient's pain interrupts the surgical step.
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
cmu-medical-surgery-tool-category-organ_clamp = Organ Clamp
cmu-medical-surgery-tool-category-scalpel_or_burn_kit = Scalpel or burn kit

# ---- Per-step labels -------------------------------------------------

cmu-medical-surgery-step-realign-simple-label = Realign Simple Fracture
cmu-medical-surgery-step-realign-compound-label = Realign Compound Fracture
cmu-medical-surgery-step-realign-comminuted-label = Realign Comminuted Fracture
cmu-medical-surgery-step-apply-gel-label = Apply Bone Gel
cmu-medical-surgery-step-apply-gel-second-label = Apply Bone Gel (Second Layer)
cmu-medical-surgery-step-insert-graft-label = Insert Bone Graft
cmu-medical-surgery-step-cauterize-bleed-label = Clamp Internal Bleed
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
