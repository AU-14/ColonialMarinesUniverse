# AN/PRC-117G expert view (the faceplate). Everything the set's glass, lamps and
# engravings say. The glass is a fixed-width character display: keep translations
# short and in capitals where the English is, or they get clipped at the edge.
# The keypad's letter groups (ABC1, DEF2, ...) are not here on purpose - callsign
# and label entry is keyed on them.

## shared words
anprc-fp-on = ON
anprc-fp-off = OFF
anprc-fp-auto = AUTO
anprc-fp-none = NONE
anprc-fp-unknown = UNKNOWN
anprc-fp-send = SEND
anprc-fp-stowed = STOWED
anprc-fp-deployed = DEPLOYED
anprc-fp-standby = STANDBY
anprc-fp-no-net = NO NET
anprc-fp-handset = HANDSET
anprc-fp-headset = HEADSET
anprc-fp-callsign-auto = { $callsign } AUTO
anprc-fp-unavailable-off = SET OFF - USE 6 PWR OR FUNCTION SWITCH
anprc-fp-note-wear-or-stake = WEAR OR STAKE THE SET TO USE A NET

## waveform, power, bands
anprc-fp-mode-short-fh = FH
anprc-fp-mode-short-sc = SC
anprc-fp-mode-short-ct = CT
anprc-fp-mode-short-pt = PT
anprc-fp-mode-fh = FREQ HOP
anprc-fp-mode-sc = SINGLE CH
anprc-fp-mode-ct = CIPHER
anprc-fp-mode-pt = PLAIN
anprc-fp-power-lo = LO
anprc-fp-power-med = MED
anprc-fp-power-hi = HI
anprc-fp-band-lf = LF
anprc-fp-band-hf = HF
anprc-fp-band-vhf = VHF
anprc-fp-band-uhf = UHF
anprc-fp-band-shf = SHF

## memory contents
anprc-fp-slot-net = { $freq } MHz  { $net }
anprc-fp-slot-direct = { $freq } MHz  DIRECT
anprc-fp-slot-unknown-net = { $freq } MHz  UNKNOWN NET
anprc-fp-slot-empty = --- EMPTY ---

## OPT screen
anprc-fp-opt-title = OPT  SET CONFIG
anprc-fp-opt-mode = MODE
anprc-fp-opt-monitor = MONITOR
anprc-fp-opt-scan = SCAN
anprc-fp-opt-squelch = SQUELCH
anprc-fp-opt-output = OUTPUT
anprc-fp-opt-backlight = BACKLIGHT
anprc-fp-opt-backlight-value = LT
anprc-fp-opt-techniques = TECHNIQUES
anprc-fp-opt-burst = BURST TX
anprc-fp-opt-power-save = POWER SAVE
anprc-fp-opt-priority = PRIORITY WATCH
anprc-fp-opt-emcon = EMCON
anprc-fp-opt-emcon-silent = SILENT
anprc-fp-opt-staked = STAKED SET
anprc-fp-opt-station = STATION
anprc-fp-opt-callsign = CALLSIGN
anprc-fp-opt-clear-override = CLEAR OVERRIDE
anprc-fp-opt-roster = ROSTER
anprc-fp-opt-directory = NET DIRECTORY
anprc-fp-opt-phone = PHONE
anprc-fp-opt-radio-check = RADIO CHECK
anprc-fp-opt-status = SET STATUS
anprc-fp-opt-role = ROLE
anprc-fp-role-retrans = RETRANS
anprc-fp-role-manpack = MANPACK
anprc-fp-opt-antenna = ANTENNA
anprc-fp-opt-relay = RELAY
anprc-fp-opt-relay-value = { $count } NETS { $full }/{ $partial }T
anprc-fp-opt-operator = OPERATOR
anprc-fp-opt-operator-untrained = UNTRAINED - NO RELAY
anprc-fp-opt-audio = AUDIO
anprc-fp-opt-link = LINK
anprc-fp-opt-link-quality = QUALITY
anprc-fp-opt-link-via = VIA
anprc-fp-opt-link-via-none = NO RELAY IN REACH
anprc-fp-opt-link-via-value = { $name } { $bearing } { $distance }T
anprc-fp-opt-draw = DRAW
anprc-fp-opt-draw-value = { $draw }/S
anprc-fp-opt-endurance = ENDURANCE
anprc-fp-opt-endurance-value = ~{ $minutes } MIN

## OPT / STAKED screen
anprc-fp-staked-title = OPT  STAKED SET
anprc-fp-staked-needs-stake = STAKE THE SET DOWN FOR RETRANS AND PEAKING
anprc-fp-staked-retrans = RETRANS
anprc-fp-staked-bridge = BRIDGE
anprc-fp-staked-bridge-value = { $a } <> { $b }
anprc-fp-staked-break = BREAK THE BRIDGE
anprc-fp-staked-side-a = SIDE A
anprc-fp-staked-side-b = SIDE B
anprc-fp-staked-make-bridge = BRIDGE A <> B
anprc-fp-staked-retrans-note = TRAFFIC ON ONE SIDE IS REPEATED ON THE OTHER
anprc-fp-staked-antenna = ANTENNA
anprc-fp-staked-peak = PEAK THE ANTENNA
anprc-fp-staked-peaked = PEAKED
anprc-fp-staked-unpeaked = UNTUNED
anprc-fp-staked-peak-note = +25% RANGE UNTIL THE SET IS PACKED UP

## roster
anprc-fp-roster-title = OPT  ROSTER
anprc-fp-roster-none = NO ROSTER HELD

## keypad entry prompts
anprc-fp-entry-station = STATION
anprc-fp-entry-label = LABEL
anprc-fp-entry-frequency = FREQ [{ $slot }]

## acknowledgements printed across the glass
anprc-fp-ack-mode = MODE { $mode }
anprc-fp-ack-monitor = MONITOR { $state }
anprc-fp-ack-scan = SCAN { $state }
anprc-fp-ack-squelch = SQUELCH { $level }
anprc-fp-ack-output = OUTPUT { $power }
anprc-fp-ack-burst = BURST { $state }
anprc-fp-ack-power-save = POWER SAVE { $state }
anprc-fp-ack-priority = WATCHING { $slot }
anprc-fp-ack-priority-off = PRIORITY WATCH OFF
anprc-fp-ack-emcon-on = EMCON - SET IS SILENT
anprc-fp-ack-emcon-off = EMCON LIFTED
anprc-fp-ack-station = STATION { $callsign }
anprc-fp-ack-station-cleared = STATION CLEARED
anprc-fp-ack-radio-check = RADIO CHECK SENT
anprc-fp-ack-retrans-on = BRIDGE UP
anprc-fp-ack-retrans-off = BRIDGE BROKEN
anprc-fp-ack-peak = PEAKING - HOLD STILL

## PGM screens
anprc-fp-pgm-title = PGM  NET MEMORY
anprc-fp-pgm-load-std = LOAD STD NETS
anprc-fp-pgm-no-nets = NO NETS IN MEMORY
anprc-fp-pgm-add = ADD NET
anprc-fp-pgm-note-ent = ENT OPENS THE MEMORY UNDER THE CURSOR
anprc-fp-pgm-note-working = *  IS THE WORKING NET. :r TRANSMITS ON IT
anprc-fp-memory-title = PGM  MEMORY
anprc-fp-memory-loaded = LOADED
anprc-fp-memory-work = WORK THIS NET
anprc-fp-memory-active = ACTIVE
anprc-fp-memory-tune = TUNE TO A NET
anprc-fp-memory-key-freq = KEY A FREQUENCY
anprc-fp-memory-empty = EMPTY THE MEMORY
anprc-fp-memory-delete = DELETE THE MEMORY
anprc-fp-memory-rename = RENAME
anprc-fp-netlist-title = PGM  NET LIST
anprc-fp-netlist-intercept = { $net } (INT)
anprc-fp-netlist-none = NO NETS HELD - KEY A FREQUENCY
anprc-fp-ack-std-loading = STD NETS LOADING
anprc-fp-ack-memory-added = MEMORY { $label } ADDED
anprc-fp-ack-net-selected = NET { $label } SELECTED
anprc-fp-ack-memory-emptied = MEMORY EMPTIED
anprc-fp-ack-memory-deleted = MEMORY CLEARED
anprc-fp-ack-memory-renamed = MEMORY { $label }
anprc-fp-ack-net-loaded = NET LOADED

## SEC screen
anprc-fp-sec-title = SEC  COMSEC
anprc-fp-sec-secured = SECURED
anprc-fp-sec-superseded = SUPERSEDED
anprc-fp-sec-unsecured = UNSECURED
anprc-fp-sec-switch = SWITCH
anprc-fp-sec-switch-load = LD - FILL WORK
anprc-fp-sec-switch-zeroize = Z - WIPE READY
anprc-fp-sec-switch-operating = { $position } - OPERATING
anprc-fp-sec-fill = FILL
anprc-fp-sec-issued-to = ISSUED TO
anprc-fp-sec-note-superseded = SUPERSEDED - NEW KEY REQUIRED
anprc-fp-sec-note-insert = INSERT A FILL CARD TO LOAD
anprc-fp-sec-traffic = TRAFFIC
anprc-fp-sec-encrypted = ENCRYPTED
anprc-fp-sec-in-clear = IN CLEAR
anprc-fp-sec-fill-actions = FILL ACTIONS
anprc-fp-sec-zeroize = ZEROIZE
anprc-fp-sec-confirm-wipe = CONFIRM WIPE
anprc-fp-sec-eject = EJECT
anprc-fp-sec-no-fill = NO FILL
anprc-fp-sec-destroy = DESTROY
anprc-fp-sec-confirm-burn = CONFIRM BURN
anprc-fp-sec-burn = BURN
anprc-fp-sec-recrypto = RECRYPTO
anprc-fp-sec-confirm-recrypto = CONFIRM RECRYPTO
anprc-fp-sec-supersede = SUPERSEDE
anprc-fp-sec-needs-fill = NEEDS FILL
anprc-fp-sec-note-authority = RECRYPTO NEEDS COMMAND AUTHORITY
anprc-fp-sec-note-supersedes = IT INVALIDATES EVERY OLD FACTION CARD
anprc-fp-sec-otar = OVER-THE-AIR REKEY
anprc-fp-sec-otar-send = SEND KEY OTA
anprc-fp-sec-confirm-otar = CONFIRM KEY PUSH
anprc-fp-sec-otar-targets = { $count } SETS OUT OF DATE
anprc-fp-sec-note-otar = SENDS CURRENT KEY TO OUTDATED SETS IN REACH
anprc-fp-sec-note-otar-risk = CAPTURED SETS WITH YOUR CARD GET THE KEY TOO
anprc-fp-ack-zeroize-armed = ZEROIZE ARMED - ENT AGAIN TO WIPE
anprc-fp-ack-zeroized = FILL ZEROIZED
anprc-fp-ack-destroy-armed = DESTROY ARMED - ENT AGAIN TO BURN
anprc-fp-ack-destroyed = FILL DESTROYED
anprc-fp-ack-recrypto-armed = RECRYPTO ARMED - ENT AGAIN TO ORDER
anprc-fp-ack-recrypto = RECRYPTO ORDERED
anprc-fp-ack-otar-armed = KEY PUSH ARMED - ENT AGAIN TO SEND
anprc-fp-ack-otar = KEY SENT OVER THE AIR

## SRCH screen
anprc-fp-srch-title = SRCH  RECEIVER
anprc-fp-srch-searching = SEARCHING
anprc-fp-srch-dwelling = DWELLING
anprc-fp-srch-idle = IDLE
anprc-fp-srch-start = START SEARCH
anprc-fp-srch-stop = STOP SEARCH
anprc-fp-srch-running = RUNNING
anprc-fp-srch-ready = READY
anprc-fp-srch-head = HEAD
anprc-fp-srch-release = RELEASE THE HEAD
anprc-fp-srch-state = STATE
anprc-fp-srch-state-inhibited = NETS DROPPED, TX INHIBITED
anprc-fp-srch-note-drops = SEARCHING DROPS EVERY NET
anprc-fp-srch-note-deploy = WEAR OR STAKE THE SET TO SEARCH THE BAND
anprc-fp-srch-contacts = CONTACTS
anprc-fp-srch-no-contacts = NO CONTACTS
anprc-fp-srch-contact-partial = PARTIAL { $tier }/{ $max }
anprc-fp-srch-contact-dwell = DWELL { $tier }/{ $max }
anprc-fp-srch-contact-own = { $net } (OWN)
anprc-fp-srch-contact-no-mem = { $net } (NO MEM)
anprc-fp-srch-note-select-memory = SELECT A MEMORY ON PGM TO TUNE A FIX
anprc-fp-srch-note-dwell = ENT ON A PARTIAL CONTACT PARKS THE HEAD ON IT
anprc-fp-srch-jammer = JAMMER DF
anprc-fp-srch-jammer-clear = NO JAMMING HEARD
anprc-fp-srch-jammer-first = FIRST BEARING
anprc-fp-srch-jammer-baseline = BASELINE
anprc-fp-srch-jammer-baseline-value = { $moved }/{ $needed }T
anprc-fp-srch-jammer-take = TAKE A BEARING
anprc-fp-srch-jammer-second = TAKE SECOND BEARING
anprc-fp-srch-jammer-note = TWO SPACED BEARINGS FIX IT ON THE MAP
anprc-fp-ack-search-running = SEARCH RUNNING
anprc-fp-ack-search-stopped = SEARCH STOPPED
anprc-fp-srch-keys = KEY ANALYSIS
anprc-fp-srch-keys-none = FIX AN ENEMY NET TO ANALYZE ITS KEY

## KEY screen (a pushed page off SRCH)
anprc-fp-key-title = KEY  { $faction }
anprc-fp-key-state = KEY
anprc-fp-key-broken = BROKEN
anprc-fp-key-broken-note = THIS SET READS THEIR TRAFFIC UNTIL THEY RECRYPTO. NO TX ON THEIR NETS
anprc-fp-key-depth = { $depth }/{ $max } BANKED
anprc-fp-key-test = TEST A TRIAL KEY
anprc-fp-key-prompt = KEY A-H x6
anprc-fp-key-rules = 6 SYMBOLS A-H, NONE TWICE. HITS = SYMBOLS IN THE RIGHT PLACE
anprc-fp-key-no-depth = NO TRIALS BANKED. EACH LINE ON A FIXED NET BANKS ONE
anprc-fp-key-trials = TRIALS
anprc-fp-key-no-trials = NO TRIALS YET
anprc-fp-key-hits = { $hits }/{ $length }
anprc-fp-key-gone = NO KEY TO WORK. FIX ONE OF THEIR NETS FIRST
anprc-fp-ack-key-sent = TRIAL SENT
anprc-fp-ack-key-bad = 6 LETTERS A-H
anprc-fp-ack-dwell-on = HEAD PARKED
anprc-fp-ack-dwell-off = HEAD SWEEPING
anprc-fp-ack-contact-tuned = CONTACT TUNED
anprc-fp-ack-bearing = BEARING TAKEN

## ? key card. numbered lines are one glass line each - re-break freely, but keep the count
anprc-fp-help-title = ?  KEY CARD
anprc-fp-help-off = SET OFF
anprc-fp-help-online = ONLINE
anprc-fp-help-screen = WORKING THE SCREEN
anprc-fp-help-pre = +/- PRE
anprc-fp-help-pre-what = MOVE THE CURSOR
anprc-fp-help-ent = ENT
anprc-fp-help-ent-what = USE THE LINE UNDER IT
anprc-fp-help-clr = CLR
anprc-fp-help-clr-what = BACK OUT ONE SCREEN
anprc-fp-help-pg = +/- PG
anprc-fp-help-pg-what = CHANGE PAGES
anprc-fp-help-soft-keys = SOFT KEYS BELOW JUMP TO PAGES
anprc-fp-help-keypad = KEYPAD
anprc-fp-help-key-1 = 1 CALL
anprc-fp-help-key-1-what = RADIO CHECK
anprc-fp-help-key-2 = 2 LT
anprc-fp-help-key-2-what = BACKLIGHT
anprc-fp-help-key-3 = 3 MODE
anprc-fp-help-key-3-what = WAVEFORM
anprc-fp-help-key-4 = 4 SQL
anprc-fp-help-key-4-what = SQUELCH
anprc-fp-help-key-5 = 5 ZERO
anprc-fp-help-key-5-what = COMSEC, WIPE ARMED
anprc-fp-help-key-6 = 6 PWR
anprc-fp-help-key-6-what = SET ON AND OFF
anprc-fp-help-key-pages = 7 8 9 0
anprc-fp-help-key-pages-what = OPT PGM SEC LOG
anprc-fp-help-keying = KEYING NUMBERS AND TEXT
anprc-fp-help-keying-1 = OPENING A FIELD ACTIVATES THE KEYPAD.
anprc-fp-help-keying-2 = FREQUENCIES USE ___.___ MHZ. ENTER MHZ
anprc-fp-help-keying-3 = FIRST. UNUSED DIGITS ARE READ AS ZERO.
anprc-fp-help-keying-4 = TEXT USES THE LETTERS OVER EACH KEY -
anprc-fp-help-keying-5 = PRESS AGAIN TO CYCLE A B C. THE CAPS SHOW
anprc-fp-help-keying-6 = EACH KEY'S GROUP WHILE YOU TYPE.
anprc-fp-help-keying-7 = 0 ENTERS A SPACE, THEN A DASH.
anprc-fp-help-keying-8 = CLR DELETES, ENT CONFIRMS.
anprc-fp-help-switch = FUNCTION SWITCH
anprc-fp-help-switch-off = OFF
anprc-fp-help-switch-off-what = SET OFF
anprc-fp-help-switch-ct = CT
anprc-fp-help-switch-ct-what = SECURE - AGAIN CYCLES FH SC CT
anprc-fp-help-switch-pt = PT
anprc-fp-help-switch-pt-what = IN CLEAR - ANYONE CAN READ YOU
anprc-fp-help-switch-ld = LD
anprc-fp-help-switch-ld-what = FILL PAGE - SPRUNG
anprc-fp-help-switch-z = Z
anprc-fp-help-switch-z-what = ARMS THE WIPE - SPRUNG
anprc-fp-help-switch-1 = LD AND Z ARE SPRUNG POSITIONS. THE SWITCH
anprc-fp-help-switch-2 = RETURNS ON ITS OWN. YOUR WAVEFORM STAYS
anprc-fp-help-switch-3 = THE SAME.
anprc-fp-help-techniques = TECHNIQUES (FACEPLATE ONLY)
anprc-fp-help-techniques-1 = OPT: BURST HALVES YOUR DF RISK, POWER
anprc-fp-help-techniques-2 = SAVE STRETCHES THE CELL, PRIORITY WATCH
anprc-fp-help-techniques-3 = HEARS A 2ND NET, EMCON GOES SILENT.
anprc-fp-help-techniques-4 = OPT > STAKED SET: RETRANS BRIDGES TWO
anprc-fp-help-techniques-5 = MEMORIES, PEAKING ADDS 25% RANGE.
anprc-fp-help-techniques-6 = SEC: SEND KEY OTA AFTER A RECRYPTO.
anprc-fp-help-techniques-7 = SRCH: ENT ON A PARTIAL CONTACT DWELLS,
anprc-fp-help-techniques-8 = TWO JAMMER BEARINGS FIX IT ON THE MAP.
anprc-fp-help-procedure = PROCEDURE
anprc-fp-help-procedure-1 = THE PANEL WORKS ON ANY SWITCHED-ON SET,
anprc-fp-help-procedure-2 = IN THE HAND OR ON THE GROUND. WEAR IT OR
anprc-fp-help-procedure-3 = STAKE IT DOWN TO USE A NET.
anprc-fp-help-procedure-4 = ADD A MEMORY ON PGM, TUNE IT, SELECT IT.
anprc-fp-help-procedure-5 = USE :r ON THE WORKING MEMORY TO TRANSMIT.
anprc-fp-help-procedure-6 = NO FILL MEANS YOUR TRAFFIC GOES OUT CLEAR.

## LOG screen
anprc-fp-log-title = LOG  NET LOG
anprc-fp-log-filter = NET FILTER
anprc-fp-log-all-nets = ALL NETS
anprc-fp-log-intercepts-only = INTERCEPTS ONLY
anprc-fp-log-print = PRINT LOG
anprc-fp-log-print-intercepts = PRINT INTERCEPTS
anprc-fp-log-paper = PAPER
anprc-fp-log-traffic = TRAFFIC
anprc-fp-log-empty = LOG EMPTY
anprc-fp-log-intercept = { $net } INT
anprc-fp-log-no-match = NO TRAFFIC MATCHES THE FILTER
anprc-fp-ack-log-printed = LOG PRINTED
anprc-fp-ack-intercepts-printed = INTERCEPTS PRINTED

## readout strip across the top of the glass
anprc-fp-lamp-rx = RX
anprc-fp-lamp-tx = TX
anprc-fp-rd-sec = SEC
anprc-fp-rd-unsec = UNSEC
anprc-fp-rd-clear = CLEAR
anprc-fp-rd-sig = SIG { $bars }
anprc-fp-rd-bat = BAT { $bars }
anprc-fp-rd-no-net-loaded = NO NET LOADED
anprc-fp-rd-no-net-memory = NO NET IN MEMORY
anprc-fp-rd-slot-empty = SLOT EMPTY
anprc-fp-rd-search-slot = SR
anprc-fp-rd-search = BAND SEARCH - NETS DROPPED
anprc-fp-rd-dwell = DWELLING ON A CONTACT - NETS DROPPED
anprc-fp-rd-search-band = SEARCH
anprc-fp-rd-direct = DIRECT FREQUENCY
anprc-fp-rd-band-sat = { $band } SAT
anprc-fp-rd-band-los = { $band } LOS
anprc-fp-rd-settings = { $mode }  SQL { $squelch }  PWR { $power }
anprc-fp-rd-flag-mon = MON
anprc-fp-rd-flag-scan = SCAN
anprc-fp-rd-flag-burst = BURST
anprc-fp-rd-flag-ps = PS
anprc-fp-rd-flag-pri = PRI
anprc-fp-rd-flag-emcon = EMCON
anprc-fp-rd-flag-rxmt = RXMT
anprc-fp-rd-stn = STN { $station }
anprc-fp-rd-bit = BIT { $result }
anprc-fp-rd-bit-no-cell = NO CELL
anprc-fp-rd-bit-search = SRCH
anprc-fp-rd-bit-pass = PASS

## engravings, soft keys and the plate's status line
anprc-fp-eng-function = FUNCTION
anprc-fp-eng-sprung = LD / Z SPRUNG
anprc-fp-tab-pgm = PGM
anprc-fp-tab-pgm-key = KEY 8
anprc-fp-tab-sec = SEC
anprc-fp-tab-sec-key = KEY 9
anprc-fp-tab-srch = SRCH
anprc-fp-tab-srch-key = BAND
anprc-fp-tab-log = LOG
anprc-fp-tab-log-key = KEY 0
anprc-fp-tab-opt = OPT
anprc-fp-tab-opt-key = KEY 7
anprc-fp-tab-help-key = KEYS
anprc-fp-no-data = NO DATA FROM THE SET
anprc-fp-worn = WORN
anprc-fp-status = { $deployment }  { $power }  { $net }
anprc-fp-status-direct = DIRECT { $freq }
anprc-fp-status-no-net = no net

## prompt line
anprc-fp-prompt-ld-held = LD HELD - FILL PAGE, SWITCH SPRINGS BACK
anprc-fp-prompt-z-armed = Z HELD - WIPE ARMED, SWITCH SPRINGS BACK
anprc-fp-prompt-z-nothing = Z HELD - NO FILL TO WIPE
anprc-fp-prompt-no-cell = NO CELL - FIT A BATTERY
anprc-fp-prompt-off = SET OFF - SWITCH OR 6 PWR
anprc-fp-prompt-stowed = STOWED - SETTINGS OK, WEAR IT TO USE A NET
anprc-fp-prompt-no-net = NO NET - 8 PGM TO LOAD ONE

## keypad and switch acknowledgements
anprc-fp-ack-backlight = BACKLIGHT { $level }
anprc-fp-ack-entry-abandoned = ENTRY ABANDONED
anprc-fp-ack-three-digits = ENTER AT LEAST 3 DIGITS
anprc-fp-ack-check-stowed = SET STOWED - WEAR IT OR STAKE IT DOWN TO CHECK
anprc-fp-ack-check-searching = SEARCHING - TRANSMIT INHIBITED
anprc-fp-ack-check-no-net = NO NET TO CHECK
anprc-fp-ack-nothing-to-wipe = NOTHING TO WIPE
anprc-fp-ack-no-cell = NO CELL FITTED - PWR REFUSED
anprc-fp-ack-set-on = SET ON
anprc-fp-ack-set-off = SET OFF
anprc-fp-ack-mode-plain = MODE PLAIN - TRAFFIC IN CLEAR
anprc-fp-ack-set-on-comsec = SET ON - COMSEC UP, SWITCH SPRINGS BACK
anprc-fp-ack-ld-comsec = LD - COMSEC UP, SWITCH SPRINGS BACK
anprc-fp-ack-z-armed = Z - WIPE ARMED, CONFIRM WITH ENT. SWITCH SPRINGS BACK
anprc-fp-ack-z-nothing = Z - NOTHING TO WIPE
anprc-fp-ack-sprung-back = SWITCH SPRUNG BACK TO { $position } - MODE UNCHANGED

## function switch detents
anprc-fp-knob-off = OFF
anprc-fp-knob-ct = CT
anprc-fp-knob-pt = PT
anprc-fp-knob-ld = LD
anprc-fp-knob-z = Z

## backlight
anprc-fp-backlight-day = DAY
anprc-fp-backlight-night = NGT

## connector plate
anprc-fp-port-ant = ANT
anprc-fp-port-audio = AUDIO
anprc-fp-port-fill = KDU/FILL
anprc-fp-port-power = PWR/GPS

## keypad engravings. the letter groups beside them are not translated: text entry is keyed on them
anprc-fp-key-clr = CLR
anprc-fp-key-ent = ENT
anprc-fp-key-fn-scrn = SCRN
anprc-fp-key-fn-curs = CURS
anprc-fp-key-fn-back = BACK
anprc-fp-key-fn-work = WORK
anprc-fp-key-fn-call = CALL
anprc-fp-key-fn-lt = LT
anprc-fp-key-fn-mode = MODE
anprc-fp-key-fn-sql = SQL
anprc-fp-key-fn-zero = ZERO
anprc-fp-key-fn-pwr = PWR
anprc-fp-key-fn-opt = OPT
anprc-fp-key-fn-pgm = PGM
anprc-fp-key-fn-sec = SEC
anprc-fp-key-fn-log = LOG
