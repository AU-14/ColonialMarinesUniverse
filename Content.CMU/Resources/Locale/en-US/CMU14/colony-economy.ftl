# Shared colony economy status
colony-economy-sales-tax = Sales Tax: { $percent }%
colony-economy-income-tax = Income Tax: { $percent }%
colony-economy-transit-tariff = Transit Tariff: { $percent }%
colony-economy-active-embargoes = Active Embargoes: { $factions }
colony-economy-no-embargoes = Embargoes: None
colony-economy-active-trade-pacts = Active Trade Pacts: { $factions }
colony-economy-no-trade-pacts = Trade Pacts: None
colony-economy-overview = -- Colony Economy Overview --
colony-economy-third-party-support = -- Third Party Support --
colony-economy-open-third-party-menu = Open Third Party Menu
colony-economy-support-dispatch-failed = Unable to dispatch support at this time.
colony-economy-unknown-faction = Unknown Faction
colony-economy-apply = Apply

# Administration console
admin-console-title = Administration Console
admin-console-colony-budget = Colony Budget: ${ $amount }
admin-console-sales-tax-section = -- Sales Tax --
admin-console-current-sales-tax = Current Sales Tax: { $percent }%
admin-console-sales-tax-description = Applies to cash vendors and corporate ASRS orders. Tax revenue goes to the colony budget.
admin-console-new-tax = New Tax (0–50%):
admin-console-tax-placeholder = e.g. 10
admin-console-income-tax-section = -- Income Tax --
admin-console-current-income-tax = Current Income Tax: { $percent }%
admin-console-income-tax-description = Deducted from salary payouts and corporate cash withdrawals. Revenue goes to the colony budget.
admin-console-announcement-sender = Administration
admin-console-sales-tax-announcement = Colony sales tax has been set to { $percent }%.
admin-console-income-tax-announcement = Colony income tax has been set to { $percent }%. This affects salary payouts and corporate withdrawals.

# Corporate console
corporate-console-title = Corporate Affairs Console
corporate-console-budget = Corporate Budget: ${ $amount }
corporate-console-withdraw-section = -- Withdraw Cash --
corporate-console-transit-tariff-section = -- Transit Tariff --
corporate-console-current-transit-tariff = Current Transit Tariff: { $percent }%
corporate-console-transit-tariff-description = A percentage of all submission storage payouts that goes to the corporate budget instead of the colony.
corporate-console-new-tariff = New Tariff (0–50%):
corporate-console-tariff-placeholder = e.g. 15
corporate-console-withdraw-tax-note = Note: Withdrawals are subject to { $percent }% income tax.
corporate-console-withdraw-no-tax = No income tax on withdrawals.
corporate-console-announcement-sender = Corporate Affairs
corporate-console-tariff-announcement = Corporate transit tariff has been set to { $percent }%. Submission payouts to the colony have been adjusted.

# Budget console
budget-console-title = Budget Console
budget-console-current-budget = Current Budget: { $amount }
budget-console-withdraw-cash = Withdraw Cash:
budget-console-dispense-salaries = Dispense All Salaries
budget-console-transfer-department = Transfer to Department:
budget-console-amount-placeholder = Amount
budget-console-department-entry = { $department } (Budget: ${ $amount })
budget-console-transfer = Transfer
budget-console-no-departments = No departments found.

# Cash vendor
cash-vendor-credit = Credit:
cash-vendor-amount = ${ $amount }
cash-vendor-scan-id = Scan ID
cash-vendor-clear-department = Clear Dept
cash-vendor-return-change = Return Change
cash-vendor-department-budget = Dept Budget:
cash-vendor-department-budget-value = ${ $amount } ({ $department })
cash-vendor-search-placeholder = Search...
cash-vendor-footer-hint = Insert cash or carry your ID, then select item.
cash-vendor-prices-include-tax = Prices incl. tax
cash-vendor-id-account = ID Account:
cash-vendor-insufficient-cash = Not enough cash, and no ID card to charge the rest to.
cash-vendor-insufficient-funds = Insufficient funds on your ID card.
cash-vendor-card-locked = Your ID card is locked.
cash-vendor-card-charged = ${ $amount } charged to your ID card.
cash-vendor-sales-tax = Sales Tax: { $percent }%
cash-vendor-no-sales-tax = No sales tax
cash-vendor-buy = Buy
cash-vendor-no-items = No items available.

# Colony ATM card reader
cmu-atm-card-slot-occupied = There's already a card in the ATM.
cmu-atm-no-card = You have no ID card to put in.
cmu-atm-take-card-verb = Take card
cmu-atm-take-cash-verb = Take cash
cmu-atm-take-card-start = You start pulling the card out of the ATM...
cmu-atm-take-card-start-others = {CAPITALIZE(THE($user))} starts pulling a card out of the ATM!

# Colony ATM power-on self test, shown on the terminal as it boots
cmu-atm-boot-title = W-Y COLONY FINANCIAL SYSTEMS
cmu-atm-boot-bios = BIOS 2.7 (C) 2179 W-Y CORP.
cmu-atm-boot-memory = MEMORY 640K
cmu-atm-boot-keypad = KEYPAD
cmu-atm-boot-reader = CARD READER
cmu-atm-boot-dispenser = CASH DISPENSER
cmu-atm-boot-uplink = UN TREASURY UPLINK
cmu-atm-boot-ok = OK
cmu-atm-boot-loading = LOADING TERMINAL...

# Colony ATM knocked out by a sapper's siphon rig: a console gone wrong behind a plain notice.
# The second line gives way to whatever message the sapper left.
cmu-atm-out-of-order = OUT OF ORDER
cmu-atm-out-of-order-sorry = PLEASE USE ANOTHER MACHINE

# Colony ATM screen hints; the keys are labelled OK and X
cmu-atm-hint-confirm = OK = confirm   X = back
cmu-atm-hint-continue = OK to continue.

# Colony ATM nav bar
cmu-atm-nav-title = Colony ATM
cmu-atm-nav-pin = Your card #{ $account } - PIN { $pin }
cmu-atm-nav-no-card = You have no card of your own
cmu-atm-nav-pin-unknown = Reading your card...
cmu-atm-nav-pop-out = Pop Out
