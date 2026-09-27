# AN/PRC-117G operator's panel

## toolbar and footer

anprc-op-view-guided = VIEW: GUIDED
anprc-op-view-expert = VIEW: 117G PANEL
anprc-op-view-tooltip = Switch between the guided panel and the AN/PRC-117G faceplate. The faceplate is worked like the real set: keypad, function switch and screen pages. Both drive the same radio and can do the same things.
anprc-op-help = ? HELP
anprc-op-help-tooltip = Show the operator briefing again.
anprc-op-power-on = POWER ON
anprc-op-power-off = POWER OFF
anprc-op-radio-check = RADIO CHECK
anprc-op-radio-check-tooltip = Sends a radio check on the net you're talking on and lists who can hear you.
anprc-op-radio-check-unavailable = Radio check needs the set on, worn or planted, and a net selected to talk on.
anprc-op-footer-worn = WORN
anprc-op-footer-planted = PLANTED (RETRANS)
anprc-op-footer-stowed = NOT WORN
anprc-op-more-below = MORE BELOW - SCROLL OR CLICK HERE
anprc-op-more-below-tooltip = This page is longer than the window. Scroll with the mouse wheel, or click to jump down.

## tabs

anprc-op-tab-nets = NETS
anprc-op-tab-nets-tooltip = Your net memories: what you talk on and what you relay.
anprc-op-tab-log = LOG
anprc-op-tab-log-tooltip = Everything the set has heard, including intercepted enemy traffic.
anprc-op-tab-security = SECURITY
anprc-op-tab-security-tooltip = Encryption: your fill card and what to do with it.
anprc-op-tab-search = SEARCH
anprc-op-tab-search-tooltip = Hunt the band for enemy nets.
anprc-op-tab-settings = SETTINGS
anprc-op-tab-settings-tooltip = How the set is running, monitor and scan, your callsign, the phone - and RETURN TO AUTO.
anprc-op-tab-badge = { $tab } ({ $count })
anprc-op-tab-alert = { $tab } !

## the set's screen

anprc-op-lcd-off = OFF
anprc-op-lcd-secure = { $mode } SECURE
anprc-op-lcd-unsecured = { $mode } NOT SECURE
anprc-op-lcd-clear = { $mode } IN CLEAR
anprc-op-lcd-searching = BAND SEARCH - OFF ALL NETS
anprc-op-lcd-direct = DIRECT FREQUENCY
anprc-op-lcd-no-memories = NO NETS IN MEMORY
anprc-op-lcd-no-net = NO NET SELECTED
anprc-op-lcd-talk = TALK: type :r and your message
anprc-op-lcd-talk-off = SET OFF
anprc-op-lcd-talk-monitor = LISTEN ONLY - CAN'T TRANSMIT
anprc-op-lcd-talk-searching = SEARCHING - CAN'T TRANSMIT
anprc-op-lcd-talk-stowed = NOT WORN - CAN'T TRANSMIT
anprc-op-lcd-talk-no-net = NO NET TO TALK ON
anprc-op-lcd-signal = SIG { $bars }
anprc-op-lcd-signal-direct = SIG DIRECT
anprc-op-lcd-battery = BAT { $bars }
anprc-op-lcd-no-battery = NO BATTERY
anprc-op-lcd-relay = RELAY { $count } { $count ->
    [one] NET
   *[other] NETS
} / { $range }T
anprc-op-lcd-relay-none = NOT RELAYING
anprc-op-lcd-station = STN { $callsign }

## next step banner

anprc-op-ready-title = READY - :r talks on { $label }
anprc-op-ready-detail = Type :r followed by your message to transmit. Click another net on the NETS page to switch.
anprc-op-ready-detail-relaying = You're relaying { $count } { $count ->
    [one] net
   *[other] nets
} to every headset within { $range } tiles. Type :r followed by your message to transmit.
anprc-op-also = Also: { $issue }

anprc-op-issue-no-battery = No battery
anprc-op-issue-no-battery-detail = The set won't power up without a cell. Put a charged power cell in the pack.
anprc-op-issue-off = The radio is off
anprc-op-issue-off-detail = Nothing is relayed and you can't transmit while it's off.
anprc-op-issue-stowed = Pack isn't on your back
anprc-op-issue-stowed-detail = You can program it now, but it only transmits and relays when worn in the back slot or planted as a retrans station.
anprc-op-issue-untrained = Wearer isn't a trained operator
anprc-op-issue-untrained-detail = A worn pack only relays nets for a radio operator. On anyone else it's dead weight.
anprc-op-issue-searching = Search receiver running
anprc-op-issue-searching-detail = You've dropped off every net: you hear nothing and can't transmit until you stop searching.
anprc-op-issue-squad-missing = Your squad net ({ $net }) isn't loaded
anprc-op-issue-standard-missing = Standard nets missing: { $nets }
anprc-op-issue-standard-missing-detail = Combat nets only work for headsets near a relay carrying them. Your pack relays every net in its memory, so your people need these loaded.
anprc-op-issue-no-nets = No nets in memory
anprc-op-issue-no-nets-detail = Add a memory and pick a net for it on the NETS page.
anprc-op-issue-no-active = No net selected to talk on
anprc-op-issue-no-active-detail = Your memories are loaded, but none is selected for :r.
anprc-op-issue-monitor = Listen-only is on
anprc-op-issue-monitor-detail = You hear every net in memory, but :r won't transmit.
anprc-op-issue-ct-no-fill = CT mode with no fill card
anprc-op-issue-ct-no-fill-detail = Cipher text needs encryption loaded. Until then the set refuses to transmit.
anprc-op-issue-no-fill = Not encrypted
anprc-op-issue-no-fill-detail = Enemy interceptors can read everything you send. Load your fill card into the pack.
anprc-op-issue-stale-fill = Fill card superseded
anprc-op-issue-stale-fill-detail = Command issued new encryption. Your old card is useless and your traffic is readable. Get a current card.
anprc-op-issue-plain = Transmitting in the clear (PT)
anprc-op-issue-plain-detail = Anyone can read you, jamming hits harder and you're easy to direction-find. Meant for emergencies.
anprc-op-issue-weak-link = Weak link on this net
anprc-op-issue-no-link = No relay covers this net here
anprc-op-issue-link-detail = You're at the edge of, or outside, every relay carrying the net you're talking on. Move closer to a mast or array, or load the net yourself so you relay it.
anprc-op-issue-low-battery = Battery low
anprc-op-issue-low-battery-detail = Swap the cell soon. SC and LO power stretch what's left.

anprc-op-action-power-on = TURN ON
anprc-op-action-quick-setup = TURN ON AND LOAD STANDARD NETS
anprc-op-action-stop-search = STOP SEARCHING
anprc-op-action-load-nets = LOAD { $nets }
anprc-op-action-open-nets = OPEN NETS
anprc-op-action-use-net = TALK ON { $label }
anprc-op-action-monitor-off = TURN LISTEN-ONLY OFF
anprc-op-action-mode-fh = SWITCH TO FH
anprc-op-action-open-security = OPEN SECURITY

## nets page

anprc-op-nets-intro = Each memory holds one net. You talk on the highlighted one with :r. Every friendly combat net in memory is relayed to headsets around you, so keep your squad's net and command loaded.
anprc-op-nets-empty = No memories yet. Use the standard nets button above, or add a memory and pick a net for it.
anprc-op-quick-setup-title = STANDARD NETS
anprc-op-quick-setup-text = Loads { $nets } into free memories and selects your squad net to talk on. Nothing you've already tuned is overwritten.
anprc-op-quick-setup-button = LOAD STANDARD NETS
anprc-op-quick-setup-button-off = TURN ON AND LOAD STANDARD NETS
anprc-op-quick-setup-tooltip = Does the routine setup for you: powers the set on, loads the missing standard nets and picks one to talk on. Mode, encryption and power stay as you set them.
anprc-op-add-memory = + ADD MEMORY
anprc-op-add-memory-free = + ADD MEMORY ({ $free } of { $max } free)
anprc-op-add-memory-full = ALL MEMORIES IN USE
anprc-op-add-memory-tooltip = Add an empty memory. Name it now, pick its net with EDIT.
anprc-op-add-memory-placeholder = Name, up to 8 letters (optional)
anprc-op-add = ADD
anprc-op-cancel = CANCEL
anprc-op-done = DONE

anprc-op-net-use = USE
anprc-op-net-use-tooltip = Talk on this net. :r will transmit here.
anprc-op-net-active = ON AIR
anprc-op-net-active-tooltip = You're talking on this net.
anprc-op-net-edit = EDIT
anprc-op-net-edit-tooltip = Change the net, key a frequency, rename or delete this memory.
anprc-op-net-direct = Direct frequency
anprc-op-net-unknown = Unidentified net (intercepted)
anprc-op-net-status-empty = Empty - press EDIT to pick a net
anprc-op-net-status-talking = You talk here
anprc-op-net-status-listening = Selected (listen-only)
anprc-op-net-status-hearing = Heard
anprc-op-net-status-relayed = Relayed to nearby headsets
anprc-op-net-status-not-relayed = Not relayed right now
anprc-op-net-status-foreign = Enemy net - listening only, never relayed
anprc-op-net-status-direct = Raw frequency - not relayed

anprc-op-editor-title = EDIT { $label }
anprc-op-editor-pick-net = Pick a net:
anprc-op-editor-net-entry = { $frequency }  { $net }  { $tag }
anprc-op-editor-tag-squad = [YOUR SQUAD]
anprc-op-editor-tag-standard = [STANDARD]
anprc-op-editor-tag-intercept = [INTERCEPT]
anprc-op-editor-tag-in-memory = [IN MEMORY]
anprc-op-editor-intercept-tooltip = An enemy net your search receiver fixed. You can listen and log it; it's never relayed.
anprc-op-editor-no-nets = No nets available. Key a frequency instead.
anprc-op-editor-key-frequency = Or key a frequency:
anprc-op-editor-frequency-placeholder = e.g. 250.2 or 2502
anprc-op-editor-tune = TUNE
anprc-op-editor-frequency-help = A number that matches one of your nets loads that net. Anything else becomes a direct frequency: private, unrelayed, and in the clear to anyone who finds it.
anprc-op-editor-rename = Rename:
anprc-op-editor-rename-button = RENAME
anprc-op-editor-empty = EMPTY IT
anprc-op-editor-empty-tooltip = Clear the net out of this memory but keep the memory.
anprc-op-editor-delete = DELETE MEMORY
anprc-op-editor-delete-confirm = CLICK AGAIN TO DELETE
anprc-op-editor-delete-tooltip = Remove this memory entirely. Asks twice.

## log page

anprc-op-log-intro = The set writes down everything it hears, newest first. Enemy traffic is marked INTERCEPT. Print it to hand intelligence to whoever needs it.
anprc-op-log-search-placeholder = Search sender or message
anprc-op-log-intercepts-only = INTERCEPTS
anprc-op-log-intercepts-only-tooltip = Show only enemy traffic.
anprc-op-log-all-nets = ALL NETS
anprc-op-log-print = PRINT LOG
anprc-op-log-print-tooltip = Print the whole log onto paper.
anprc-op-log-print-intercepts = PRINT INTERCEPTS
anprc-op-log-print-intercepts-tooltip = Print only intercepted enemy traffic.
anprc-op-log-count = { $count } of { $max } lines kept
anprc-op-log-count-filtered = Showing { $shown } of { $total } lines
anprc-op-log-empty = Nothing heard yet.
anprc-op-log-no-match = Nothing matches the filter.
anprc-op-log-header = [{ $time }] { $sender } - { $net }
anprc-op-log-header-intercept = [{ $time }] { $sender } - { $net } - INTERCEPT

## security page

anprc-op-sec-status-secure = ENCRYPTED
anprc-op-sec-status-none = NOT ENCRYPTED
anprc-op-sec-status-stale = FILL SUPERSEDED
anprc-op-sec-status-plain = IN THE CLEAR (PT)
anprc-op-sec-detail-secure = Your traffic is encrypted. Enemy interceptors hear static.
anprc-op-sec-detail-none = No fill card loaded. Enemy interceptors can read everything you send.
anprc-op-sec-detail-stale = Command has recrypted. Your card is out of date, so your traffic is readable. Get a current card.
anprc-op-sec-detail-plain = PT mode sends in the clear even with a fill loaded. Switch to FH, SC or CT in SETTINGS to encrypt.
anprc-op-sec-fill = Fill: { $designation } ({ $faction })
anprc-op-sec-fill-none = Fill: none
anprc-op-sec-how-to = To load encryption, use your fill card on the pack. It's issued with the radio.
anprc-op-sec-actions = FILL ACTIONS
anprc-op-sec-actions-help = None of these can be undone, so each asks twice. Zeroize or destroy before the set is captured.
anprc-op-sec-zeroize = ZEROIZE
anprc-op-sec-zeroize-confirm = CLICK AGAIN TO ZEROIZE
anprc-op-sec-zeroize-tooltip = Wipe the fill from the set and eject the card.
anprc-op-sec-destroy = DESTROY FILL
anprc-op-sec-destroy-confirm = CLICK AGAIN TO DESTROY
anprc-op-sec-destroy-tooltip = Wipe the fill and destroy the card so it can't be captured.
anprc-op-sec-recrypto = ORDER RECRYPTO
anprc-op-sec-recrypto-confirm = CLICK AGAIN TO RECRYPTO
anprc-op-sec-recrypto-tooltip = Supersedes every fill card your faction holds. Use it when a card or set has been captured. Needs command authority.

## search page

anprc-op-search-intro = The search receiver walks the band looking for other people's nets. Each pass that catches traffic sharpens the fix, one digit at a time. A fixed net can be tuned into a memory to listen in.
anprc-op-search-cost = Searching takes you off every net: you hear nothing and can't transmit while it runs.
anprc-op-search-running-warning = SEARCHING - you're off every net and can't transmit.
anprc-op-search-offline = The set has to be on and worn or planted to search.
anprc-op-search-start = START SEARCH
anprc-op-search-stop = STOP SEARCH
anprc-op-search-head = HEAD { $frequency }
anprc-op-search-idle = IDLE
anprc-op-search-contacts = CONTACTS
anprc-op-search-no-contacts = No contacts yet. Busy nets are the easiest to find.
anprc-op-search-partial = ~{ $frequency }  fix { $tier }/{ $max }
anprc-op-search-partial-help = Partial fix. Keep searching while it's talking to earn the rest.
anprc-op-search-own = { $frequency }  { $net } (yours)
anprc-op-search-fixed = { $frequency }  { $net }
anprc-op-search-tune-into = Tune into:
anprc-op-search-tune-tooltip = Put this net into that memory. What was there is replaced.
anprc-op-search-no-memory = add a memory first

## settings page

anprc-op-set-mode = WAVEFORM
anprc-op-set-mode-fh = Frequency hopping. Resists jamming and can transmit inside jammer fields. Costs 1.5x battery and leaves a small direction-finding signature.
anprc-op-set-mode-sc = Single channel. Secure and cheap (0.75x battery), no direction-finding while encrypted. No jamming resistance.
anprc-op-set-mode-ct = Cipher text. Invisible to interceptors and DF, but only other 117G packs with fill can read it: your own riflemen hear static. Won't transmit without a fill card.
anprc-op-set-mode-pt = Plain text. Anyone can read you, jamming hits harder and DF finds you fastest. Half battery. Emergency use.
anprc-op-set-power = OUTPUT POWER
anprc-op-set-power-lo = Low. 0.6x relay range, half battery and half the DF risk.
anprc-op-set-power-med = Medium. Standard range, battery use and DF risk.
anprc-op-set-power-hi = High. 1.5x relay range for your squad, but double battery and double DF risk.
anprc-op-set-squelch = SQUELCH
anprc-op-set-squelch-0 = 0: Open. Hear everything, however broken.
anprc-op-set-squelch-1 = 1: Mutes heavy static and traffic you can't decrypt.
anprc-op-set-squelch-2 = 2: Also mutes moderately jammed traffic.
anprc-op-set-squelch-3 = 3 (default): Also mutes lightly jammed and broken fringe traffic.
anprc-op-set-squelch-4 = 4: Maximum. Even slightly hissy long-range traffic is muted. Quiet, but you'll miss distant stations.
anprc-op-set-receive = RECEIVING
anprc-op-set-monitor-off = LISTEN-ONLY: OFF
anprc-op-set-monitor-on = LISTEN-ONLY: ON
anprc-op-set-monitor-help = Hear every net in memory at once. You can't transmit while it's on.
anprc-op-set-scan-off = SCAN: OFF
anprc-op-set-scan-on = SCAN: ON
anprc-op-set-scan-help = Hear every net in memory, and jump your talk net to whichever one last had traffic.
anprc-op-set-station = STATION CALLSIGN
anprc-op-set-callsign-manual = { $callsign } (set by hand)
anprc-op-set-callsign-from-wearer = { $callsign } (your callsign)
anprc-op-set-callsign-unknown = UNKNOWN STATION
anprc-op-set-callsign-help = Everything you send through the pack goes out under this name. By default it's your own callsign. Set one by hand to speak as a station, like PLT MAIN.
anprc-op-set-callsign-placeholder = Station callsign
anprc-op-set-callsign-set = SET
anprc-op-set-callsign-auto = USE MY OWN CALLSIGN
anprc-op-set-callsign-auto-tooltip = Clear the hand-set station callsign and go back to yours.
anprc-op-set-roster = ROSTER
anprc-op-set-roster-tooltip = Common station callsigns for your side.
anprc-op-set-directory = OPEN NET DIRECTORY
anprc-op-set-directory-tooltip = Who's who on your side's nets, by callsign.
anprc-op-set-phone = PHONE
anprc-op-set-phone-tooltip = Call another set or a fixed phone on the handset. Answers a ringing set; hangs up a call in progress.

## first-open briefing

anprc-op-intro-title = YOU'RE THE RADIO OPERATOR
anprc-op-intro-lead = Your pack is your squad's link to everyone else. The panel tells you what's wrong and what to press, so you can learn the rest as you go.
anprc-op-intro-point-1-title = 1. YOU ARE THE RELAY
anprc-op-intro-point-1 = Combat nets only work for headsets near a relay carrying them. Your pack relays every friendly net in its memory, so if you don't load your squad's net, your squad can't use it.
anprc-op-intro-point-2-title = 2. GET ON THE AIR
anprc-op-intro-point-2 = Wear the pack on your back and press LOAD STANDARD NETS. It turns the set on, loads your squad and command nets and selects your squad net.
anprc-op-intro-point-3-title = 3. TALK
anprc-op-intro-point-3 = Type :r and your message to transmit through the pack on the net marked ON AIR. Press USE on another net to switch.
anprc-op-intro-point-4-title = 4. WATCH THE BANNER
anprc-op-intro-point-4 = The strip under the screen shows the one thing to fix next, with a button that fixes it. Green means you're good.
anprc-op-intro-point-5-title = 5. GO DEEPER WHEN YOU'RE READY
anprc-op-intro-point-5 = Load your fill card on SECURITY to encrypt. This panel runs the set on AUTO, which is right for most jobs. When you want more - lower power to stay hidden, longer battery, the search receiver, operator techniques - the VIEW button swaps to the real 117G faceplate.
anprc-op-intro-reopen = Press ? HELP at the top of the panel to see this again.
anprc-op-intro-dismiss = GOT IT
anprc-op-intro-guide = OPEN THE FULL GUIDE
anprc-op-intro-guide-tooltip = Open the guidebook pages on getting on the net and on the AN/PRC-117G.

# guided panel on AUTO (ANPRCSettingsPage)
anprc-op-set-auto-heading = HOW THE SET IS RUNNING
anprc-op-set-auto-on = AUTO. Standard waveform, output and squelch, answering to your own callsign. Nothing to set - get on a net and talk.
anprc-op-set-auto-off = Someone has worked this set from the faceplate:
anprc-op-set-changed-mode = waveform { $mode }
anprc-op-set-changed-power = output { $power }
anprc-op-set-changed-squelch = squelch { $level }
anprc-op-set-changed-callsign = station callsign { $callsign }
anprc-op-set-changed-burst = burst transmission
anprc-op-set-changed-power-save = power save
anprc-op-set-changed-priority = priority watch on { $slot }
anprc-op-set-changed-emcon = EMCON - the set will not transmit or relay
anprc-op-set-changed-retrans = retrans bridge
anprc-op-set-changed-search = band search running - every net is dropped
anprc-op-set-return-to-auto = RETURN TO AUTO
anprc-op-set-return-to-auto-tooltip = Put every faceplate setting back to standard. Your memories and fill are not touched.
anprc-op-set-expert-hint = Output power, waveform, a station callsign, the search receiver, direct frequencies and the operator techniques are worked from the EXPERT view (the set's own faceplate). See the guidebook to learn it.
anprc-op-set-callsign-guided-help = The set answers to your own assigned callsign. A fixed station callsign is set from the EXPERT view.
anprc-op-editor-frequency-expert = Direct frequencies are keyed from the EXPERT view. Every net your side holds, and every net the search receiver has fixed, is in the list above.
anprc-op-std-net-entry = { $label } ({ $net })
