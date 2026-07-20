anprc-window-title = AN/PRC-117G Tactical Radio

anprc-transmit-hint-header = TRANSMIT
anprc-transmit-hint-active = :r transmits on active preset

anprc-power-off-button = POWER OFF
anprc-power-on-button = POWER ON

anprc-status-equipped = EQUIPPED
anprc-status-unequipped = UNEQUIPPED
anprc-status-on = ON
anprc-status-off = OFF

anprc-slot-empty-display = NO CHANNEL


anprc-radio-off = The radio makes no sound. It is switched off.
anprc-not-authorized = The radio clicks. You are not trained to operate this equipment.
anprc-no-active-slot = No preset slot is active. Add a net first.
anprc-slot-empty = Preset { $slot } has no channel assigned.
anprc-no-tower = Static. { $channel } requires a communications tower or a qualified RTO relay.
anprc-not-rto-warning = You are not trained on this radio. It will not relay any nets on your back and you cannot transmit.
anprc-verb-open = Open Radio Panel
anprc-out-of-range = Static. No relay in range on { $channel }.

anprc-frequency-invalid = Invalid frequency format. Enter a number such as 1606 or 1.606.
anprc-frequency-out-of-band = No net can live there. Direct frequencies sit in 1.000-2.999 MHz or the 30.000-87.999 softwave band.
anprc-frequency-not-found = No channel found at frequency { $freq }.
anprc-frequency-set = [{ $slot }] tuned to { $freq } MHz.
anprc-frequency-set-dynamic = [{ $slot }] set to { $freq } MHz (direct frequency). Transmit with :r.

anprc-slot-max-reached = Maximum preset slots reached (4). Delete a slot first.

anprc-monitor-no-transmit = MONITOR ACTIVE - radio is in receive-only mode. Toggle MON to transmit.

anprc-ct-mode-no-fill = CT MODE - crypto fill required to transmit. Load COMSEC fill first.

anprc-scan-switched = SCAN - traffic on [{ $label }] (P{ $slot } · { $channel }). Switched active net.

anprc-squelch-suppressed = *squelch*

anprc-crypto-not-equipped = The radio must be worn to load crypto fill.
anprc-crypto-already-loaded = Already loaded: { $designation }. Zeroize first.
anprc-crypto-loaded = { $designation } loaded. Transmissions encrypted.
anprc-crypto-zeroized = { $designation } zeroized. Transmissions unsecured.
anprc-crypto-destroyed = { $designation } physically destroyed. Fill cannot be recovered.
anprc-crypto-no-card = No crypto fill loaded.
anprc-crypto-wrong-faction = This fill device is not compatible with your radio.
anprc-crypto-examine-empty = No crypto fill loaded. Transmissions unsecured.
anprc-crypto-examine-loaded = { $designation } loaded ({ $faction }).
anprc-crypto-examine-stale = { $designation } loaded ({ $faction }) - SUPERSEDED, no longer secures traffic.

anprc-comsec-unsecured = COMSEC WARNING: Transmitting on { $channel } ({ $faction }) without crypto fill. Traffic is readable by all parties.

anprc-recrypto-no-card = No valid fill card loaded. Insert your faction's fill card first.
anprc-recrypto-stale-card = This card has already been superseded. Insert a current card to order a recrypto.
anprc-recrypto-foreign-card = CHANGEOVER DENIED - loaded fill does not match this radio's issuing authority.
anprc-recrypto-ordered = COMSEC CHANGEOVER ORDERED: all { $faction } fill cards issued before this order are now superseded. Request replacement fill through the normal resupply channel.
anprc-recrypto-not-authorized = CHANGEOVER DENIED - recrypto requires command COMSEC authority.
anprc-recrypto-button = ORDER RECRYPTO - SUPERSEDE FACTION FILL
anprc-recrypto-button-confirm = PRESS AGAIN TO CONFIRM - SUPERSEDES ALL FACTION FILLS
anprc-recrypto-superseded-notice = COMSEC CHANGEOVER - your loaded fill has been superseded. Request replacement fill.

anprc-battery-depleted = The radio has no charge. Insert a battery.
anprc-battery-empty = The AN/PRC-117G shuts down, battery depleted.
anprc-battery-insufficient = Insufficient battery charge to transmit.

anprc-unknown-station = UNKNOWN STATION
anprc-radio-check-call = ALL STATIONS, THIS IS { $station }, RADIO CHECK, OVER.
anprc-radio-check-report = RADIO CHECK REPLIES - LIMA CHARLIE: { $clear } | WEAK BUT READABLE: { $degraded }
anprc-radio-check-nothing-heard = NOTHING HEARD
anprc-radio-check-interference = INTERFERENCE ON NET - strongest emitter bearing { $bearing }.

anprc-verb-plant = Set Up Retrans
anprc-verb-packup = Pack Up Radio
anprc-retrans-planted = The radio is staked down and comes up as an unattended retrans station.
anprc-retrans-packed = The retrans station is collapsed back into a manpack.
anprc-retrans-pickup-blocked = It's staked down as a retrans station. Pack it up first.

anprc-verb-handset = Take Handset
anprc-verb-handset-release = Hang Up Handset
anprc-handset-taken = You take the corded handset off { $radio }.
anprc-handset-released = You hang the handset back on { $radio }.
anprc-handset-in-use = Someone is already using that handset.
anprc-handset-hands-full = You need a free hand to take the handset.
anprc-handset-cord = The handset cord yanks out of your hand as you move away.
anprc-handset-radio-gone = The handset goes dead.
anprc-handset-hint = Speaking transmits on the pack's active net while you hold the handset. Whisper to stay off the air.

# search receiver
anprc-sweep-started = The set drops off the net and starts walking the band. You will hear nothing and send nothing until you stop.
anprc-sweep-needs-online = The set has to be on and worn to search.
anprc-sweep-aborted = The search stops as the set goes down.
anprc-sweep-aborted-power = The battery gives out and the search stops.
anprc-sweep-tx-blocked = The set is searching the band. Stop the search before you transmit.
anprc-sweep-contact = The search narrows. Something is transmitting on { $freq } MHz.
anprc-sweep-resolved = FIX: { $freq } MHz - { $net }.
anprc-sweep-unknown-net = UNIDENTIFIED NET

# net log to paper
anprc-log-print-empty = Nothing in the log worth writing down.
anprc-log-printed = You transcribe { $count } log entries onto paper.
