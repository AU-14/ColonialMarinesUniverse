## Corporate review desks (shared)

cmu-desk-tiers-section = Open a case
cmu-desk-tier =
    {$name}, up to ${$reward}
    {$items} documents, {$rules} {$rules ->
        [one] rule
       *[other] rules
    }
cmu-desk-rules = Rules in force
cmu-desk-queue = Queue
cmu-desk-submit = Submit decisions
cmu-desk-yes = yes
cmu-desk-no = no
cmu-desk-status-active = {$name} case. {$decided} of {$total} decided.
cmu-desk-status-result = {$correct} right, {$wrong} wrong, {$undecided} left undecided. Paid ${$payout} to the corporate budget.
cmu-desk-status-cooldown = Next case in {$seconds}s.
cmu-desk-status-idle = Pick a case to start.
cmu-desk-cooldown = The terminal is still filing the last case.

## Claims Adjuster

cmu-desk-claims-title = Claims Adjustment
cmu-desk-claims-heading = Claims Adjuster
cmu-desk-claims-instructions = Settle each claim under the policy rules in force. Pay out a claim only if it meets every rule; deny the rest. The Company prefers denials, but a wrongful denial costs as much as a wrongful payout. Rules not in force today don't count.
cmu-desk-claims-option-pay = Pay out
cmu-desk-claims-option-deny = Deny
cmu-desk-claims-tier-minor = Minor claims
cmu-desk-claims-tier-standard = Standard claims
cmu-desk-claims-tier-major = Major claims
cmu-desk-claims-rule-deadline = Claims must be filed within {$days} days of the incident.
cmu-desk-claims-rule-shift = Incidents that happen off shift are not covered.
cmu-desk-claims-rule-fauna = Incidents caused by indigenous fauna are acts of nature and are not covered.
cmu-desk-claims-rule-signature = Claims over ${$limit} need a supervisor's signature.
cmu-desk-claims-rule-employee = Only current Company employees can claim. Contractors and former staff cannot.
cmu-desk-claims-status-employee = Company employee
cmu-desk-claims-status-contractor = Contractor
cmu-desk-claims-status-terminated = Former employee
cmu-desk-claims-line-status = Status: {$value}
cmu-desk-claims-line-incident = Incident: {$value}
cmu-desk-claims-line-cause = Cause: {$value}
cmu-desk-claims-line-shift = On shift: {$value}
cmu-desk-claims-line-filed = Filed {$days} {$days ->
    [one] day
   *[other] days
} after the incident
cmu-desk-claims-line-amount = Amount claimed: ${$amount}
cmu-desk-claims-line-signed = Supervisor's signature: {$value}
cmu-desk-claims-cause-fauna = Indigenous fauna
cmu-desk-claims-cause-1 = Equipment failure
cmu-desk-claims-cause-2 = Workplace accident
cmu-desk-claims-cause-3 = Structural collapse
cmu-desk-claims-cause-4 = Vehicle collision
cmu-desk-claims-cause-5 = Electrical fault
cmu-desk-claims-cause-6 = Chemical spill
cmu-desk-claims-incident-1 = Broken arm
cmu-desk-claims-incident-2 = Burns to both hands
cmu-desk-claims-incident-3 = Lost personal effects
cmu-desk-claims-incident-4 = Damaged quarters
cmu-desk-claims-incident-5 = Concussion
cmu-desk-claims-incident-6 = Crushed foot
cmu-desk-claims-incident-7 = Acid burns
cmu-desk-claims-incident-8 = Destroyed vehicle
cmu-desk-claims-incident-9 = Missing family member
cmu-desk-claims-incident-10 = Smoke inhalation

## Comms Monitoring

cmu-desk-comms-title = Communications Monitoring
cmu-desk-comms-heading = Comms Monitoring
cmu-desk-comms-instructions = Read each intercepted message. Flag it if it breaks any rule in force; let everything else through. Flagging harmless mail wastes Security's time, and every message you wrongly pass is a leak. Rules not in force today don't count.
cmu-desk-comms-option-pass = Let through
cmu-desk-comms-option-flag = Flag
cmu-desk-comms-tier-routine = Routine traffic
cmu-desk-comms-tier-heightened = Heightened monitoring
cmu-desk-comms-tier-lockdown = Lockdown
cmu-desk-comms-rule-project = Flag any mention of a classified project: {$projects}.
cmu-desk-comms-rule-offsite = Flag anything addressed off-site.
cmu-desk-comms-rule-clf = Flag any mention of the CLF or the liberation movement.
cmu-desk-comms-rule-watchlist = Flag every message from: {$names}.
cmu-desk-comms-rule-specimens = Flag any mention of the specimens or the labs' work.
cmu-desk-comms-item = From {$from} to {$to}
cmu-desk-comms-offsite = Off-site relay
cmu-desk-comms-recipient-1 = Colony administration
cmu-desk-comms-recipient-2 = Colony medical
cmu-desk-comms-recipient-3 = Engineering
cmu-desk-comms-recipient-4 = Hydroponics
cmu-desk-comms-recipient-5 = The bar
cmu-desk-comms-recipient-6 = Family quarters
cmu-desk-comms-message-1 = Can you cover my shift on Thursday? I need to sort out {$topic}.
cmu-desk-comms-message-2 = Don't forget about {$topic} tonight.
cmu-desk-comms-message-3 = Any news on {$topic}? Nobody here will tell me anything.
cmu-desk-comms-message-4 = I've been thinking about {$topic} all week.
cmu-desk-comms-message-5 = Ask around about {$topic}, quietly.
cmu-desk-comms-message-6 = Mum, I'm fine. Busy with {$topic}, that's all.
cmu-desk-comms-message-7 = Meet me after shift, it's about {$topic}.
cmu-desk-comms-message-8 = Who signed off on {$topic}?
cmu-desk-comms-message-9 = If anyone asks, I was dealing with {$topic}.
cmu-desk-comms-message-10 = We need to talk about {$topic} before the next shuttle.
cmu-desk-comms-topic-1 = the water recycler
cmu-desk-comms-topic-2 = my quarters
cmu-desk-comms-topic-3 = the card game
cmu-desk-comms-topic-4 = the ration change
cmu-desk-comms-topic-5 = the generator noise
cmu-desk-comms-topic-6 = Danny's birthday
cmu-desk-comms-topic-7 = the shuttle schedule
cmu-desk-comms-topic-8 = the broken heater
cmu-desk-comms-topic-9 = the new hires
cmu-desk-comms-topic-10 = the overtime rota
cmu-desk-comms-topic-11 = the bar tab
cmu-desk-comms-topic-12 = the dust storm
cmu-desk-comms-project-1 = Project Lantern
cmu-desk-comms-project-2 = Project Halcyon
cmu-desk-comms-project-3 = Project Basilisk
cmu-desk-comms-project-4 = Project Meridian
cmu-desk-comms-project-5 = Project Cinder
cmu-desk-comms-project-6 = Project Tallow
cmu-desk-comms-clf-1 = the CLF
cmu-desk-comms-clf-2 = the liberation meeting
cmu-desk-comms-clf-3 = the movement
cmu-desk-comms-clf-4 = what the CLF are saying
cmu-desk-comms-specimen-1 = the thing in lab two
cmu-desk-comms-specimen-2 = the specimens
cmu-desk-comms-specimen-3 = what they're keeping in the labs
cmu-desk-comms-specimen-4 = the sealed crates from the science wing

## Hiring Desk

cmu-desk-hiring-title = Personnel Hiring
cmu-desk-hiring-heading = Hiring Desk
cmu-desk-hiring-instructions = Screen each applicant against the posting and the hiring rules in force. Hire only those who meet every rule; reject the rest. Check the employment history: nobody works two jobs at once. Rules not in force today don't count.
cmu-desk-hiring-option-hire = Hire
cmu-desk-hiring-option-reject = Reject
cmu-desk-hiring-tier-junior = Junior posting
cmu-desk-hiring-tier-specialist = Specialist posting
cmu-desk-hiring-tier-senior = Senior posting
cmu-desk-hiring-rule-qualification = Position: {$position}. Requires: {$qualification}.
cmu-desk-hiring-rule-experience = Applicants need at least {$years} years of experience.
cmu-desk-hiring-rule-history = Employment history must not overlap. Two jobs at once means the history is false.
cmu-desk-hiring-rule-references = The last employer must confirm the applicant worked there.
cmu-desk-hiring-rule-union = Do not hire anyone with a record of union organising.
cmu-desk-hiring-line-qualification = Qualification: {$value}
cmu-desk-hiring-line-experience = Stated experience: {$years} {$years ->
    [one] year
   *[other] years
}
cmu-desk-hiring-line-job = {$employer}: {$start} to {$end}
cmu-desk-hiring-line-reference-ok = Reference from {$employer}: employment confirmed
cmu-desk-hiring-line-reference-none = Reference from {$employer}: no record of this applicant
cmu-desk-hiring-line-notes-none = Notes: none
cmu-desk-hiring-line-notes-union = Notes: organised a workers' union at a previous site
cmu-desk-hiring-position-1 = Site engineer
cmu-desk-hiring-position-2 = Laboratory technician
cmu-desk-hiring-position-3 = Logistics clerk
cmu-desk-hiring-position-4 = Security officer
cmu-desk-hiring-position-5 = Medical orderly
cmu-desk-hiring-position-6 = Accounts assistant
cmu-desk-hiring-qualification-1 = Engineering certificate
cmu-desk-hiring-qualification-2 = Biology degree
cmu-desk-hiring-qualification-3 = Accounting diploma
cmu-desk-hiring-qualification-4 = Security licence
cmu-desk-hiring-qualification-5 = Nursing certificate
cmu-desk-hiring-qualification-6 = Heavy machinery licence
cmu-desk-hiring-employer-1 = Hyperdyne Systems
cmu-desk-hiring-employer-2 = Seegson
cmu-desk-hiring-employer-3 = Kelland Mining
cmu-desk-hiring-employer-4 = Lasalle Bionational
cmu-desk-hiring-employer-5 = Grant Corporation
cmu-desk-hiring-employer-6 = Arcturian Shipping
cmu-desk-hiring-employer-7 = Gateway Station
cmu-desk-hiring-employer-8 = Prodigy Corporation

## Clearance Desk

cmu-desk-clearance-title = Access Clearance
cmu-desk-clearance-heading = Clearance Desk
cmu-desk-clearance-instructions = Approve or deny each access request under the rules in force. Approve only requests that meet every rule. Compare the request against the personnel file: forged passes usually get the ID wrong. Rules not in force today don't count.
cmu-desk-clearance-option-approve = Approve
cmu-desk-clearance-option-deny = Deny
cmu-desk-clearance-tier-quiet = Quiet shift
cmu-desk-clearance-tier-busy = Busy shift
cmu-desk-clearance-tier-alert = Site alert
cmu-desk-clearance-rule-expiry = Today is day {$today}. Expired passes are void.
cmu-desk-clearance-rule-id = The ID on the request must match the personnel file.
cmu-desk-clearance-rule-area = Each area needs its own clearance: offices need General, the labs need Research, the executive suite needs Executive, the armory needs Armory.
cmu-desk-clearance-rule-terminated = These people no longer work here: {$names}.
cmu-desk-clearance-rule-night = Requests between 2200 and 0600 need a countersignature.
cmu-desk-clearance-line-request-id = ID on request: {$id}
cmu-desk-clearance-line-file-id = ID on personnel file: {$id}
cmu-desk-clearance-line-area = Area requested: {$area}
cmu-desk-clearance-line-clearance = Clearance held: {$clearance}
cmu-desk-clearance-line-expires = Pass valid until: day {$day}
cmu-desk-clearance-line-time = Time requested: {$hour}00
cmu-desk-clearance-line-countersigned = Countersigned: {$value}
cmu-desk-clearance-area-1 = Offices
cmu-desk-clearance-area-2 = Laboratories
cmu-desk-clearance-area-3 = Executive suite
cmu-desk-clearance-area-4 = Armory
cmu-desk-clearance-level-1 = General
cmu-desk-clearance-level-2 = Research
cmu-desk-clearance-level-3 = Executive
cmu-desk-clearance-level-4 = Armory
