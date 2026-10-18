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

# ATM screens and transaction messages
cmu-atm-busy = Someone else is using this ATM.
cmu-atm-tampered = Warning: This device shows signs of electronic tampering.
cmu-atm-normal = The ATM appears to be functioning normally.
cmu-atm-unknown = Unknown
cmu-atm-invalid-amount = Enter a valid amount.
cmu-atm-insufficient-funds = Insufficient funds.
cmu-atm-insufficient-hand = Insufficient cash in hand.
cmu-atm-invalid-account = Enter a valid 5-digit account number.
cmu-atm-account-missing = Account not found.
cmu-atm-insufficient-cash = Insufficient cash.
cmu-atm-own-account = Cannot transfer to own account.
cmu-atm-timeout = Session timed out.
cmu-atm-take-cash-first = Please take your cash first.
cmu-atm-invalid-pin = Invalid PIN. Enter all { $digits } digits.
cmu-atm-incorrect-pin = Incorrect PIN. Attempt { $attempt }/{ $maximum }.
cmu-atm-withdraw-confirm = Withdraw ${ $amount }? You receive ${ $net } after tax.
cmu-atm-dispensed = Dispensed ${ $amount }. Balance: ${ $balance }.
cmu-atm-deposited = Deposited ${ $amount }. Balance: ${ $balance }.
cmu-atm-recipient = To: { $name }. Enter amount:
cmu-atm-remote-confirm = Deposit ${ $amount } to account #{ $account }?
cmu-atm-remote-done = Deposited ${ $amount } to #{ $account }.
cmu-atm-transfer-confirm = Transfer ${ $amount } to #{ $account }?
cmu-atm-transfer-done = Transferred ${ $amount }. Balance: ${ $balance }.
cmu-atm-welcome =
    COLONY FINANCIAL TERMINAL
    v2.7  UN TREASURY
    { "" }
    1) REMOTE DEPOSIT
    { "" }
    Insert ID card for account access.
cmu-atm-pin-prompt = ENTER PIN:
cmu-atm-locked =
    { "** CARD LOCKED **" }
    Too many incorrect attempts. Please try again later.
cmu-atm-main-menu =
    Welcome, { $name }.
    { "" }
    1) WITHDRAW
    2) DEPOSIT
    3) TRANSFER
    4) REMOTE DEPOSIT
    5) HISTORY
    6) EXIT
cmu-atm-withdraw-prompt =
    WITHDRAW
    Enter amount:
cmu-atm-deposit-prompt =
    DEPOSIT
    Enter amount:
cmu-atm-remote-prompt =
    REMOTE DEPOSIT
    Recipient account #:
cmu-atm-transfer-prompt =
    TRANSFER
    Recipient account #:
cmu-atm-history-title = ACCOUNT HISTORY
cmu-atm-history-empty = No transactions yet.
cmu-atm-history-back = OK = back
cmu-atm-history-withdrawal = -${ $amount } WITHDRAWAL
cmu-atm-history-deposit = +${ $amount } DEPOSIT
cmu-atm-history-cash = +${ $amount } CASH DEPOSIT
cmu-atm-history-out = -${ $amount } TO #{ $account }
cmu-atm-history-in = +${ $amount } FROM #{ $account }
cmu-atm-history-returned = +${ $amount } CASH RETURNED
cmu-atm-history-purchase = -${ $amount } PURCHASE
cmu-atm-fault-fatal = FATAL { $byte }: LEDGER CRC MISMATCH
cmu-atm-fault-segv = SEGV AT { $address } IN TXN_CORE
cmu-atm-fault-halted = TXN_CORE HALTED
cmu-atm-fault-watchdog = WATCHDOG RESET ... FAILED
cmu-atm-fault-uplink = UPLINK LOST, RETRY { $attempt }/3
cmu-atm-fault-dispenser = DISPENSER BUS ERROR { $byte }
cmu-atm-fault-auth = AUTH TABLE CORRUPT AT { $address }
cmu-atm-fault-kernel = KERNEL PANIC: NOT SYNCING
cmu-atm-fault-stack = STACK SMASHED, ABORTING
cmu-atm-fault-trap = UNHANDLED TRAP { $byte } AT { $address }
cmu-atm-fault-garbled = ?? ???? ?????? ??? ??

# RUMC: tax on the parcels sold through the trade platforms
admin-console-trade-tax-section = -- Parcel Sales Tax --
admin-console-current-trade-tax = Current Parcel Sales Tax: { $percent }%
admin-console-trade-tax-description = Taken from what the drone pays for a parcel sold on a trade platform. That share goes to the colony budget, the rest to the seller.
admin-console-trade-tax-announcement = Colony parcel sales tax has been set to { $percent }%. That share of every parcel sold on a trade platform goes to the colony.
