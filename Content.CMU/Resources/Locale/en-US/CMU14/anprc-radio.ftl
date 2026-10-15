anprc-window-title = AN/PRC-117G Tactical Radio

anprc-radio-off = The radio is silent. It is switched off.
anprc-not-authorized = The radio clicks. You are not trained to operate it.
anprc-no-active-slot = No preset slot is active. Add a net first.
anprc-slot-empty = Preset { $slot } has no channel assigned.
anprc-no-tower = Static. { $channel } needs a communications tower or qualified RTO relay.
anprc-not-rto-warning = You are not trained on this radio. It will not relay nets while worn and you cannot transmit through it.
anprc-verb-open = Open Radio Panel
anprc-out-of-range = Static. No relay in range on { $channel }.

anprc-frequency-invalid = Invalid frequency. Enter a number such as 1606 or 1.606.
anprc-frequency-out-of-band = No net can use that frequency. Direct frequencies must be in 1.000-2.999 MHz or the 30.000-87.999 softwave band.
anprc-frequency-not-found = No channel found at { $freq }.
anprc-frequency-set = [{ $slot }] tuned to { $freq } MHz.
anprc-frequency-set-net = [{ $slot }] tuned to { $freq } MHz — { $channel } net.
anprc-frequency-set-unknown = [{ $slot }] tuned to { $freq } MHz — unidentified net. Traffic will be logged.
anprc-frequency-set-dynamic = [{ $slot }] set to { $freq } MHz (direct frequency) — no net assigned. Transmit with :r.

anprc-frequency-card-fallback =
    {"["}head=2]SIGNAL OPERATING INSTRUCTIONS[/head]
    {"["}head=3]AN/PRC-117G NET FREQUENCY ASSIGNMENTS[/head]

    {"["}bold]GOVFOR Command[/bold] - 2.592 MHz
    {"["}bold]GOVFOR Alpha[/bold]   - 2.502 MHz
    {"["}bold]GOVFOR Bravo[/bold]   - 1.606 MHz
    {"["}bold]GOVFOR Charlie[/bold] - 1.607 MHz
    {"["}bold]GOVFOR Intel[/bold]   - 1.605 MHz
    {"["}bold]GOVFOR JTAC[/bold]    - 2.598 MHz
    {"["}bold]GOVFOR MILP[/bold]    - 2.595 MHz

    Enter a frequency in the radio panel's FREQ tab. The dot is optional; 2592 and 2.592 are treated the same. A matching net will be assigned to the preset slot.

    {"["}italic]COMSEC NOTICE: This card is a controlled document. Destroy before capture. Frequencies are theatre-wide; assume the enemy knows their own assignments.[/italic]

anprc-slot-max-reached = Maximum preset slots reached (4). Delete a slot first.

anprc-monitor-no-transmit = MONITOR ACTIVE - radio is receive-only. Disable MON to transmit.

anprc-ct-mode-no-fill = CT MODE - crypto fill required to transmit. Load COMSEC fill first.

anprc-scan-switched = SCAN - traffic on [{ $label }] (P{ $slot } · { $channel }). Active net switched.

anprc-squelch-suppressed = *squelch*

anprc-crypto-not-equipped = The radio must be worn to load a crypto fill.
anprc-crypto-already-loaded = Already loaded: { $designation }. Zeroize it first.
anprc-crypto-loaded = { $designation } loaded. Transmissions are encrypted.
anprc-crypto-zeroized = { $designation } zeroized. Transmissions are unsecured.
anprc-crypto-destroyed = { $designation } destroyed. The fill cannot be recovered.
anprc-crypto-no-card = No crypto fill loaded.
anprc-crypto-wrong-faction = This fill device is not compatible with this radio.
anprc-crypto-examine-empty = No crypto fill loaded. Transmissions are unsecured.
anprc-crypto-examine-loaded = { $designation } loaded ({ $faction }).
anprc-crypto-examine-stale = { $designation } loaded ({ $faction }) - SUPERSEDED, no longer secures traffic.

anprc-comsec-unsecured = COMSEC WARNING: Transmitting on { $channel } ({ $faction }) without a crypto fill. Traffic can be read by anyone.

anprc-recrypto-no-card = No valid fill card loaded. Insert your faction's current fill card first.
anprc-recrypto-stale-card = This card has already been superseded. Insert a current card before ordering a recrypto.
anprc-recrypto-foreign-card = CHANGEOVER DENIED - loaded fill does not match this radio's issuing authority.
anprc-recrypto-ordered = COMSEC CHANGEOVER ORDERED: all { $faction } fill cards issued before this order are now superseded. Request replacement fills through normal resupply.
anprc-recrypto-not-authorized = CHANGEOVER DENIED - recrypto requires command COMSEC authority.
anprc-recrypto-superseded-notice = COMSEC CHANGEOVER - your loaded fill has been superseded. Request a replacement fill.

anprc-battery-depleted = The radio has no charge. Insert a battery.
anprc-battery-empty = The AN/PRC-117G shuts down. The battery is depleted.
anprc-battery-insufficient = Not enough battery charge to transmit.

anprc-unknown-station = UNKNOWN STATION
anprc-radio-check-call = ALL STATIONS, THIS IS { $station }, RADIO CHECK, OVER.
anprc-radio-check-report = RADIO CHECK REPLIES - LIMA CHARLIE: { $clear } | WEAK BUT READABLE: { $degraded }
anprc-radio-check-nothing-heard = NOTHING HEARD
anprc-radio-check-interference = INTERFERENCE ON NET - strongest emitter bearing { $bearing }.

anprc-verb-plant = Set Up Retrans
anprc-verb-packup = Pack Up Radio
anprc-retrans-planted = The radio is staked down and comes online as an unattended retrans station.
anprc-retrans-packed = The retrans station is packed back into a manpack.
anprc-retrans-pickup-blocked = It is staked down as a retrans station. Pack it up first.

anprc-verb-handset = Take Handset
anprc-verb-handset-release = Hang Up Handset
anprc-handset-taken = You take the corded handset from { $radio }.
anprc-handset-released = You hang the handset back on { $radio }.
anprc-handset-in-use = Someone is already using the handset.
anprc-handset-hands-full = You need a free hand to take the handset.
anprc-handset-cord = The handset cord pulls it from your hand as you move away.
anprc-handset-radio-gone = The handset goes dead.
anprc-handset-hint = Normal speech transmits on the pack's active net while you hold the handset. Whisper to stay off the air.

# search receiver
anprc-sweep-started = The set drops off the net and starts searching the band. You cannot hear or transmit on your loaded nets until you stop.
anprc-sweep-needs-online = The set must be switched on and worn to search.
anprc-sweep-aborted = The search stops as the set shuts down.
anprc-sweep-aborted-power = The battery dies and the search stops.
anprc-sweep-tx-blocked = The set is searching the band. Stop the search before transmitting.
anprc-sweep-contact = The search narrows. Something is transmitting on { $freq } MHz.
anprc-sweep-resolved = FIX: { $freq } MHz - { $net }. New traffic on it now gives a bearing and banks a key trial on the faceplate.
anprc-sweep-unknown-net = UNIDENTIFIED NET
anprc-fixed-net-bearing = DF { $net }: speaker bearing { $bearing }, about { $distance } tiles away.

# chat rows (ANPRCChatSystem): the set's own notices, and intercepts
anprc-chat-label = 117G
anprc-chat-intercept-label = INT
anprc-chat-intercept-verb = on
anprc-chat-intercept-line = [color={ $color }]\[INT\] [bold]{ $sender }[/bold] on { $net }, { chat-manager-speech-double-quote-begin }{ $message }{ chat-manager-speech-double-quote-end }[/color]

# key analysis (ANPRCCryptoSystem.Analysis)
anprc-key-untrained = You do not know how to break a key. This requires radio-operator training.
anprc-key-offline = The set must be on and either worn or staked to work a key.
anprc-key-no-fix = Fix one of that faction's nets first. You have no traffic to analyze yet.
anprc-key-bad-trial = A trial must be { $length } symbols from { $symbols }.
anprc-key-no-depth = No trials banked. Wait for traffic on a net you have fixed.
anprc-key-trial-result = TRIAL { $trial }: { $hits } of { $length } in place.
anprc-key-broken = KEY BROKEN. This set reads { $faction } traffic until they recrypto. It still cannot transmit on their nets.

# net log to paper
anprc-log-print-empty = There is nothing in the log to print.
anprc-log-printed = You transcribe { $count } log entries onto paper.

anprc-log-frequency-unknown = FREQ UNK

# languages that do not carry over the air
anprc-language-no-radio = { $language } cannot be transmitted over a radio net.

anprc-quick-setup-loaded = Standard nets loaded: { $nets }.
anprc-quick-setup-nothing = Standard nets are already loaded.
anprc-quick-setup-full = No free memory for: { $nets }. Delete or empty a memory first.

# phone (ANPRCRadioSystem.Phone)
anprc-call-says = { $name } says, "{ $message }"
anprc-call-no-fill = No COMSEC fill loaded. The set has no faction network to call through.
anprc-call-no-link = NO LINK. The set is off, stowed, jammed, out of power or searching.
anprc-call-no-link-target = NO LINK. That station is out of reach - there is no relay path or direct link between you.
anprc-call-incoming = Your AN/PRC-117G is ringing: { $caller }. Take the handset to answer.
anprc-call-incoming-connected = Incoming call from { $caller } - connected through your handset.
anprc-call-ended = You hang up.
anprc-call-far-end-hung-up = The line goes dead. The other end hung up.
anprc-call-link-lost = The call drops. Link lost.
anprc-call-net-blocked = The handset is on a call. Hang up before using the radio net again.
anprc-verb-phone = Use Phone

# expert techniques (ANPRCRadioSystem.Expert)
anprc-emcon-on = EMCON: the set is silent. It will not transmit, relay or ring, and uses very little power. It can still receive traffic.
anprc-emcon-off = EMCON lifted. The set can transmit and relay again.
anprc-emcon-no-transmit = The set is in EMCON and cannot transmit. Disable EMCON on the faceplate's OPT page.
anprc-retrans-needs-staked = Retrans only works while the set is staked down.
anprc-retrans-needs-nets = Select two different memories, each containing a named net.
anprc-retrans-bridged = Retrans active: traffic on { $a } is repeated on { $b }, and vice versa.
anprc-retrans-relayed = [RXMT] { $speaker }: { $message }
anprc-return-to-auto-done = The set is back on AUTO.
anprc-peak-needs-staked = Stake the set down and switch it on before peaking the antenna.
anprc-peak-already = The antenna is already peaked.
anprc-peak-start = You begin aiming and tuning the antenna...
anprc-peak-done = Antenna peaked. Relay range is increased until the set is packed up.
anprc-otar-needs-current-fill = You need your faction's current key loaded before sending it over the air.
anprc-otar-cooldown = The set is still cycling after the last key push.
anprc-otar-none = No reachable set has an outdated key.
anprc-otar-sent = Key sent over the air to { $count ->
    [one] one set
   *[other] { $count } sets
}.
anprc-otar-received = Your set's COMSEC key was updated over the air by { $station }.
anprc-dwell-needs-search = Start a band search before parking the search head.
anprc-dwell-on = Search head parked on the contact. The rest of the band is ignored until you release it.
anprc-dwell-lost = That contact has faded or already been resolved.
anprc-jammer-none = No jammer is affecting the set.
anprc-jammer-bearing-first = Jammer bearing { $bearing }. Move at least { $baseline } tiles and take a second bearing to fix its position.
anprc-jammer-baseline-short = Jammer bearing { $bearing }, but you have only moved { $moved } of { $baseline } tiles from the first position. Move farther before taking the second bearing.
anprc-jammer-fixed = FIX: bearings cross at { $bearing }, about { $distance } tiles away. Jammer marked on your faction's map for { $minutes } minutes.

# server-side words that used to be English literals
anprc-antenna-none = NONE
anprc-log-report-title = NET LOG
anprc-log-report-title-intercepts = INTERCEPT LOG
anprc-log-report-station = STATION:
anprc-log-report-entries = ENTRIES:
anprc-log-report-intercept = (INTERCEPT)
anprc-log-report-footer = Transcribed from an AN/PRC-117G net log. Times use the set clock, not local time.
anprc-bearing-n = N
anprc-bearing-ne = NE
anprc-bearing-e = E
anprc-bearing-se = SE
anprc-bearing-s = S
anprc-bearing-sw = SW
anprc-bearing-w = W
anprc-bearing-nw = NW

anprc-antenna-whip = WHIP
anprc-antenna-wire = WIRE
anprc-antenna-mast = MAST
