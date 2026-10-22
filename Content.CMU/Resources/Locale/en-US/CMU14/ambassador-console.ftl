ambassador-console-title = Ambassador Console
ambassador-console-budget = Budget: ${ $amount }
ambassador-console-faction = Faction: { $faction }
ambassador-console-withdraw-section = -- Withdraw --
ambassador-console-economy-section = -- Economy --
ambassador-console-toggle-embargo = Toggle Embargo
ambassador-console-toggle-trade-pact = Toggle Trade Pact
ambassador-console-communications-section = -- Communications --
ambassador-console-toggle-comms-jam = Toggle Comms Jam
ambassador-console-broadcast-section = -- Broadcast --
ambassador-console-broadcast-placeholder = Enter message...
ambassador-console-send = Send
ambassador-console-signal-control-section = -- Signal Control --
ambassador-console-signal-boost = Signal Boost
ambassador-console-signal-jam = Signal Jam
ambassador-console-economy-overview-section = -- Colony Economy Overview --
ambassador-console-radar-section = -- Incoming Shuttle Radar --
ambassador-console-scan-radar = Scan Radar
ambassador-console-third-party-section = -- Third Party Support --
ambassador-console-open-third-party = Open Third Party Menu

ambassador-console-embargo-active = [ACTIVE] Embargo: -20% submission payouts (${ $cost }/min)
ambassador-console-embargo-inactive = Embargo: Inactive (${ $cost }/min)
ambassador-console-trade-pact-active = [ACTIVE] Trade Pact: +20% submission payouts (${ $cost }/min)
ambassador-console-trade-pact-inactive = Trade Pact: Inactive (${ $cost }/min)
ambassador-console-comms-jam-active = [ACTIVE] Comms Jammed - All radio blocked (${ $cost }/min)
ambassador-console-comms-normal = Comms: Normal (${ $cost }/min)
ambassador-console-broadcast-cost = Broadcast cost: ${ $cost } per message
ambassador-console-signal-normal = Signal: Normal
ambassador-console-signal-boost-active = [ACTIVE] Signal Boost - Third parties arrive faster (${ $cost }/min)
ambassador-console-signal-jam-active = [ACTIVE] Signal Jam - Third parties arrive slower (${ $cost }/min)
ambassador-console-signal-normal-costs = Signal: Normal (Boost: ${ $boost }/min | Jam: ${ $jam }/min)
ambassador-console-scan-radar-cost = Scan Radar (${ $cost })
ambassador-console-radar-no-data = No scan data. Press 'Scan Radar' to detect incoming shuttles.
ambassador-console-radar-entry = • { $name }
ambassador-console-sales-tax = Sales Tax: { $value }%
ambassador-console-income-tax = Income Tax: { $value }%
ambassador-console-transit-tariff = Transit Tariff: { $value }%
ambassador-console-active-embargoes = Active Embargoes: { $values }
ambassador-console-no-embargoes = Embargoes: None
ambassador-console-active-trade-pacts = Active Trade Pacts: { $values }
ambassador-console-no-trade-pacts = Trade Pacts: None

ambassador-third-party-title = Call Third Party Support
ambassador-third-party-available = Available Third Parties:
ambassador-third-party-cost = Cost: ${ $cost }
ambassador-third-party-cost-na = Cost: N/A
ambassador-third-party-request = Request Support
ambassador-third-party-requesting = Requesting support...
ambassador-third-party-none = No third parties available to call.
ambassador-third-party-entry = { $name } - ${ $cost }
ambassador-third-party-entry-called = { $name } - ${ $cost } [CALLED]

ambassador-console-embargo-ended-no-funds = Trade embargo by { $faction } has ended due to insufficient funds.
ambassador-console-trade-pact-ended-no-funds = Trade pact by { $faction } has ended due to insufficient funds.
ambassador-console-comms-jam-ended-no-funds = Communications jamming by { $faction } has ended due to insufficient funds.
ambassador-console-embassy-sender = { $faction } Embassy
ambassador-console-default-sender = Ambassador Console
ambassador-console-embargo-enabled = A trade embargo has been activated by { $faction }. Submission point payouts are reduced by 20%.
ambassador-console-embargo-disabled = The trade embargo by { $faction } has been lifted.
ambassador-console-trade-pact-enabled = A trade pact has been activated by { $faction }. Submission point payouts are increased by 20%.
ambassador-console-trade-pact-disabled = The trade pact by { $faction } has ended.
ambassador-console-comms-jam-enabled = Planeside communications have been jammed by { $faction }. All radio transmissions are blocked.
ambassador-console-comms-jam-disabled = Communications jamming by { $faction } has been disabled. Radio transmissions are restored.
ambassador-third-party-calls-left = Support requests authorized: { $left }
ambassador-third-party-cooldown = Support requests authorized: { $left }. Command will hear another request in { $minutes } { $minutes ->
    [one] minute
   *[other] minutes
}.
ambassador-third-party-no-calls-left = Command has authorized all the support it will send for this operation.

# Diplomatic console screen
cmu-ambassador-heading = DIPLOMATIC CONSOLE
cmu-ambassador-subheading = { $faction } EMBASSY // DIPLOMATIC RELAY
cmu-ambassador-treasury = Treasury
cmu-ambassador-treasury-hint = Insert cash to add it to the embassy budget. Measures below are paid from it every minute and stop when it runs dry.
cmu-ambassador-withdraw = WITHDRAW ${ $amount }
cmu-ambassador-trade = Trade Measures
cmu-ambassador-embargo = Trade Embargo
cmu-ambassador-embargo-desc = Cuts the colony's submission payouts by 20%.
cmu-ambassador-trade-pact = Trade Pact
cmu-ambassador-trade-pact-desc = Raises the colony's submission payouts by 20%.
cmu-ambassador-signals = Signals & Communications
cmu-ambassador-comms-jam = Comms Jam
cmu-ambassador-comms-jam-desc = Blocks every planetside radio transmission.
cmu-ambassador-signal-boost = Signal Boost
cmu-ambassador-signal-boost-desc = Third parties on their way arrive faster.
cmu-ambassador-signal-jam = Signal Jam
cmu-ambassador-signal-jam-desc = Third parties on their way arrive slower.
cmu-ambassador-active = ACTIVE
cmu-ambassador-inactive = INACTIVE
cmu-ambassador-start = ENACT
cmu-ambassador-stop = LIFT
cmu-ambassador-cost-per-minute = { $cost } PER MINUTE WHILE ACTIVE
cmu-ambassador-broadcast = Colony Broadcast
cmu-ambassador-send = SEND
cmu-ambassador-broadcast-cost = { $cost } PER MESSAGE
cmu-ambassador-economy = Colony Economy
cmu-ambassador-radar = Shuttle Radar
cmu-ambassador-scan = SCAN ({ $cost })
cmu-ambassador-radar-none = No scan data. Scan to detect incoming shuttles.
cmu-ambassador-radar-contact = INBOUND
cmu-ambassador-third-party = Third-Party Support
cmu-ambassador-open-third-party = OPEN SUPPORT MENU
cmu-ambassador-third-party-hint = Request mercenaries, special forces or other outside help, paid from the embassy budget.
cmu-ambassador-footer = Diplomatic actions are announced to the colony.

# Third-party support screen
cmu-third-party-heading = THIRD-PARTY SUPPORT
cmu-third-party-subheading = AVAILABLE BUDGET { $budget }
cmu-third-party-calls = { $left } { $left ->
    [one] REQUEST
   *[other] REQUESTS
} LEFT
cmu-third-party-request = REQUEST SUPPORT
cmu-third-party-confirm = CONFIRM REQUEST
cmu-third-party-pick = SELECT A PARTY
cmu-third-party-cost = COSTS { $cost }
cmu-third-party-called = CALLED
cmu-third-party-short = NOT ENOUGH FUNDS
cmu-third-party-footer = Each party can only be called once. Support takes time to arrive.
