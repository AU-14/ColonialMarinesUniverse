## Records & Audit terminal

cmu-audit-title = Records & Audit
cmu-audit-heading = Expense Report Audit
cmu-audit-instructions = Match every line of the submitted report to the ledger. Flag each line whose department or amount doesn't match, then submit. False flags cancel out correct ones.
cmu-audit-tiers-section = Open a case
cmu-audit-tier = {$name}: {$rows} lines, {$errors} {$errors ->
    [one] error
   *[other] errors
} (up to ${$reward})
cmu-audit-tier-routine = Routine
cmu-audit-tier-standard = Standard
cmu-audit-tier-forensic = Forensic
cmu-audit-ledger = Ledger
cmu-audit-report = Submitted report (click to flag)
cmu-audit-flag-tooltip = Flag this line as not matching the ledger.
cmu-audit-submit = Submit audit
cmu-audit-entry =
    {$item}
    {$department} · ${$amount}
cmu-audit-status-active = {$name} case open. Find {$errors} {$errors ->
    [one] discrepancy
   *[other] discrepancies
}.
cmu-audit-status-result = Last audit: {$found} found, {$missed} missed, {$false} false {$false ->
    [one] flag
   *[other] flags
}. Paid ${$payout} to the corporate budget.
cmu-audit-status-cooldown = Next case available in {$seconds}s.
cmu-audit-status-idle = Pick a case to start.
cmu-audit-cooldown = The terminal is still filing the last audit.

cmu-audit-department-1 = Spc Serv.
cmu-audit-department-2 = C.R.
cmu-audit-department-3 = Comms.
cmu-audit-department-4 = PMC Disp.
cmu-audit-department-5 = R&D
cmu-audit-department-6 = Eco.
cmu-audit-department-7 = Per. Pro.

cmu-audit-item-1 = Office supplies
cmu-audit-item-2 = Coffee service contract
cmu-audit-item-3 = Shuttle fuel surcharge
cmu-audit-item-4 = Hazard pay
cmu-audit-item-5 = Lab glassware
cmu-audit-item-6 = Sample containment units
cmu-audit-item-7 = Radio relay maintenance
cmu-audit-item-8 = Encryption licence renewal
cmu-audit-item-9 = Body armor replacement
cmu-audit-item-10 = Ammunition restock
cmu-audit-item-11 = Executive travel
cmu-audit-item-12 = Client hospitality
cmu-audit-item-13 = Legal retainer
cmu-audit-item-14 = Insurance premium
cmu-audit-item-15 = Colony liaison gifts
cmu-audit-item-16 = Synthetic servicing
cmu-audit-item-17 = Medical stock
cmu-audit-item-18 = Freight handling
cmu-audit-item-19 = Data storage drives
cmu-audit-item-20 = Security consulting
cmu-audit-item-21 = Press release printing
cmu-audit-item-22 = Overtime payroll
cmu-audit-item-23 = Vehicle lease
cmu-audit-item-24 = Sensitive waste disposal
