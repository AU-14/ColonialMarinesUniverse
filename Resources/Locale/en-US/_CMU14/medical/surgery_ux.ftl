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
cmu-medical-surgery-section-parts = Body Parts
cmu-medical-surgery-section-surgeries = Surgeries
cmu-medical-surgery-section-surgeries-on = Surgeries on { $part }
cmu-medical-surgery-arm-button = Begin Surgery
cmu-medical-surgery-cancel-armed = Cancel Surgery
cmu-medical-surgery-step-hint = Step { $step }/{ $total } — { $label } ({ $tool })
cmu-medical-surgery-step-hint-prereq = Prerequisite step { $step }/{ $total } — { $label } ({ $tool })
cmu-medical-surgery-armed-heading = ARMED

# ---- In-progress hero panel ------------------------------------------

cmu-medical-surgery-in-progress-heading = IN PROGRESS
cmu-medical-surgery-in-progress-subtitle = { $surgery } · { $part }
cmu-medical-surgery-in-progress-credit = Started by { $surgeon } · { $elapsed } ago
cmu-medical-surgery-step-now = Step { $step } of { $total }: { $label }
cmu-medical-surgery-action-hint = Click { $part } with a { $tool }.
cmu-medical-surgery-action-hint-no-tool = Click { $part } to continue.
cmu-medical-surgery-continue-button = Continue Surgery
cmu-medical-surgery-abandon-button = Abandon Surgery

# ---- Per-part section labels -----------------------------------------

cmu-medical-surgery-part-heading = { $part }
cmu-medical-surgery-part-condition-healthy = Healthy
cmu-medical-surgery-part-condition-locked = Other surgery in progress on { $other } — finish or abandon first
cmu-medical-surgery-part-condition-no-eligible = No surgeries available

cmu-medical-surgery-condition-incision-open = Open incision
cmu-medical-surgery-condition-ribcage-open = Open ribcage
cmu-medical-surgery-condition-fracture = { $severity } fracture
cmu-medical-surgery-condition-internal-bleed = Internal bleeding
cmu-medical-surgery-condition-in-progress = Surgery in progress
cmu-medical-surgery-condition-missing = Severed

# ---- BUI category headers ---------------------------------------------

cmu-medical-surgery-category-fracture = Fracture
cmu-medical-surgery-category-bleed = Internal Bleeding
cmu-medical-surgery-category-remove_organ = Remove Organ
cmu-medical-surgery-category-transplant = Transplant Organ
cmu-medical-surgery-category-suture = Suture Organ
cmu-medical-surgery-category-head_organ = Head Surgery
cmu-medical-surgery-category-reattach = Reattach Limb
cmu-medical-surgery-category-close_up = Close Up
cmu-medical-surgery-category-general = Other

# ---- Examine surface (CMUSurgeryStateExamineSystem) ------------------

cmu-medical-surgery-examine-patient-in-progress = [color=#dca94c]{ $surgery } in progress (by { $surgeon }) — next: { $next }.[/color]
cmu-medical-surgery-examine-part-in-progress = [color=#dca94c]{ $surgery } in progress (by { $surgeon }) — next: { $next }.[/color]
cmu-medical-surgery-examine-part-abandoned = [color=#888888]Open wound — no surgery in progress.[/color]

# ---- Close-up step labels (RMC fallback resolution) ------------------

cmu-medical-surgery-step-close-incision-label = Close Incision
cmu-medical-surgery-step-mend-ribcage-label = Mend Ribcage
cmu-medical-surgery-step-close-bones-label = Close Bones

# ---- Armed-step status -----------------------------------------------

cmu-medical-surgery-armed-none = (no surgery armed)
cmu-medical-surgery-armed-step = Armed: { $surgery } — Step { $step } ({ $tool })
cmu-medical-surgery-armed-cancelled = Surgery cancelled.
cmu-medical-surgery-armed-expired = The surgery pick timed out.

# ---- Click-target popups ---------------------------------------------

cmu-medical-surgery-wrong-part = That isn't the part you armed the surgery on.
cmu-medical-surgery-wrong-tool = That isn't the right tool for this step.
cmu-medical-surgery-wrong-tool-damage = You slip with the { $tool }!
cmu-medical-surgery-no-tool = You need a surgical tool to perform this step.
cmu-medical-surgery-wrong-limb = That limb doesn't match any empty slot on the patient.

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
cmu-medical-surgery-step-reattach-limb-label = Reattach Severed Limb
