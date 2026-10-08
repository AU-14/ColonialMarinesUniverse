# AN/PRC-117G operator's panel

## toolbar and footer

anprc-op-view-guided = VIEW: GUIDED
anprc-op-view-expert = VIEW: 117G PANEL
anprc-op-view-tooltip = Switch between the guided panel and the AN/PRC-117G faceplate. The faceplate works like the real set: keypad, function switch and screen pages. Both control the same radio.
anprc-op-help = ? HELP
anprc-op-help-tooltip = Show the operator briefing again.
anprc-op-power-on = POWER ON
anprc-op-power-off = POWER OFF
anprc-op-radio-check = RADIO CHECK
anprc-op-radio-check-tooltip = Sends a radio check on your active net and shows who can hear you.
anprc-op-radio-check-unavailable = Radio check needs the set on, worn or planted, with a net selected.
anprc-op-footer-worn = WORN
anprc-op-footer-planted = PLANTED (RETRANS)
anprc-op-footer-stowed = NOT WORN
anprc-op-more-below = MORE BELOW - SCROLL OR CLICK HERE
anprc-op-more-below-tooltip = This page is longer than the window. Scroll with the mouse wheel, or click to jump down.

## tabs

anprc-op-tab-nets = NETS
anprc-op-tab-nets-tooltip = Your net memories: what you transmit on and what you relay.
anprc-op-tab-log = LOG
anprc-op-tab-log-tooltip = Everything the set has heard, including intercepted enemy traffic.
anprc-op-tab-security = SECURITY
anprc-op-tab-security-tooltip = Encryption: your fill card and its controls.
anprc-op-tab-search = SEARCH
anprc-op-tab-search-tooltip = Search the band for enemy nets.
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
} to headsets within { $range } tiles. Type :r followed by your message to transmit.
anprc-op-also = Also: { $issue }

anprc-op-issue-no-battery = No battery
anprc-op-issue-no-battery-detail = The set won't power on without a cell. Put a charged power cell in the pack.
anprc-op-issue-off = The radio is off
anprc-op-issue-off-detail = Nothing is relayed and you can't transmit while the set is off.
anprc-op-issue-stowed = Pack isn't on your back
anprc-op-issue-stowed-detail = You can program it now, but it only transmits and relays while worn in the back slot or planted as a retrans station.
anprc-op-issue-untrained = Wearer isn't a trained operator
anprc-op-issue-untrained-detail = A worn pack only relays for a trained radio operator. On anyone else it won't provide relay coverage.
anprc-op-issue-searching = Search receiver running
anprc-op-issue-searching-detail = You've dropped off every net. You can't hear or transmit until the search stops.
anprc-op-issue-squad-missing = Your squad net ({ $net }) isn't loaded
anprc-op-issue-standard-missing = Standard nets missing: { $nets }
anprc-op-issue-standard-missing-detail = Combat headsets need a nearby relay carrying their net. Your pack relays every friendly net in memory, so these should be loaded.
anprc-op-issue-no-nets = No nets in memory
anprc-op-issue-no-nets-detail = Add a memory and choose a net for it on the NETS page.
anprc-op-issue-no-active = No net selected to talk on
anprc-op-issue-no-active-detail = Your memories are loaded, but none is selected for :r.
anprc-op-issue-monitor = Listen-only is on
anprc-op-issue-monitor-detail = You hear every net in memory, but :r cannot transmit.
anprc-op-issue-ct-no-fill = CT mode with no fill card
anprc-op-issue-ct-no-fill-detail = Cipher text needs an encryption fill. Until one is loaded, the set will not transmit.
anprc-op-issue-no-fill = Not encrypted
anprc-op-issue-no-fill-detail = Enemy interceptors can read what you send. Load your fill card into the pack.
anprc-op-issue-stale-fill = Fill card superseded
anprc-op-issue-stale-fill-detail = Command issued a new key. Your card is out of date and no longer secures traffic. Get a current card.
anprc-op-issue-plain = Transmitting in the clear (PT)
anprc-op-issue-plain-detail = Anyone can read you, jamming affects you more and you're easier to direction-find. Intended for emergencies.
anprc-op-issue-weak-link = Weak link on this net
anprc-op-issue-no-link = No relay covers this net here
anprc-op-issue-link-detail = You're near the edge of, or outside, relay coverage for this net. Move closer to a relay, or load the net into your own pack.
anprc-op-issue-low-battery = Battery low
anprc-op-issue-low-battery-detail = Replace the cell soon. SC and LO power can make the remaining charge last longer.

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

anprc-op-nets-intro = Each memory holds one net. You transmit on the highlighted one with :r. Every friendly combat net in memory is relayed to nearby headsets, so keep your squad and command nets loaded.
anprc-op-nets-empty = No memories yet. Use the standard nets button above, or add a memory and choose a net for it.
anprc-op-quick-setup-title = STANDARD NETS
anprc-op-quick-setup-text = Loads { $nets } into free memories and selects your squad net for transmission. Nothing you've already tuned is overwritten.
anprc-op-quick-setup-button = LOAD STANDARD NETS
anprc-op-quick-setup-button-off = TURN ON AND LOAD STANDARD NETS
anprc-op-quick-setup-tooltip = Handles the normal setup: powers the set on, loads missing standard nets and selects one for transmission. Mode, encryption and power are left unchanged.
anprc-op-add-memory = + ADD MEMORY
anprc-op-add-memory-free = + ADD MEMORY ({ $free } of { $max } free)
anprc-op-add-memory-full = ALL MEMORIES IN USE
anprc-op-add-memory-tooltip = Add an empty memory. Name it now, then choose its net with EDIT.
anprc-op-add-memory-placeholder = Name, up to 8 letters (optional)
anprc-op-add = ADD
anprc-op-cancel = CANCEL
anprc-op-done = DONE

anprc-op-net-use = USE
anprc-op-net-use-tooltip = Talk on this net. :r will transmit here.
anprc-op-net-active = ON AIR
anprc-op-net-active-tooltip = You're transmitting on this net.
anprc-op-net-edit = EDIT
anprc-op-net-edit-tooltip = Change the net, enter a frequency, rename or delete this memory.
anprc-op-net-direct = Direct frequency
anprc-op-net-unknown = Unidentified net (intercepted)
anprc-op-net-status-empty = Empty - press EDIT to pick a net
anprc-op-net-status-talking = You talk here
anprc-op-net-status-listening = Selected (listen-only)
anprc-op-net-status-hearing = Heard
anprc-op-net-status-relayed = Relayed to nearby headsets
anprc-op-net-status-not-relayed = Not relayed right now
anprc-op-net-status-foreign = Enemy net - listen only, never relayed
anprc-op-net-status-direct = Raw frequency - not relayed

anprc-op-editor-title = EDIT { $label }
anprc-op-editor-pick-net = Pick a net:
anprc-op-editor-net-entry = { $frequency }  { $net }  { $tag }
anprc-op-editor-tag-squad = [YOUR SQUAD]
anprc-op-editor-tag-standard = [STANDARD]
anprc-op-editor-tag-intercept = [INTERCEPT]
anprc-op-editor-tag-in-memory = [IN MEMORY]
anprc-op-editor-intercept-tooltip = An enemy net fixed by your search receiver. You can listen to and log it, but it is never relayed.
anprc-op-editor-no-nets = No nets available. Enter a frequency instead.
anprc-op-editor-key-frequency = Or key a frequency:
anprc-op-editor-frequency-placeholder = e.g. 250.2 or 2502
anprc-op-editor-tune = TUNE
anprc-op-editor-frequency-help = A frequency matching one of your known nets loads that net. Anything else becomes a direct frequency: unrelayed and in the clear to anyone who finds it.
anprc-op-editor-rename = Rename:
anprc-op-editor-rename-button = RENAME
anprc-op-editor-empty = EMPTY IT
anprc-op-editor-empty-tooltip = Clear the net from this memory but keep the memory slot.
anprc-op-editor-delete = DELETE MEMORY
anprc-op-editor-delete-confirm = CLICK AGAIN TO DELETE
anprc-op-editor-delete-tooltip = Remove this memory entirely. Requires a second click.

## log page

anprc-op-log-intro = The set records everything it hears, newest first. Enemy traffic is marked INTERCEPT. Print the log when you need to pass it on.
anprc-op-log-search-placeholder = Search sender or message
anprc-op-log-intercepts-only = INTERCEPTS
anprc-op-log-intercepts-only-tooltip = Show only enemy traffic.
anprc-op-log-all-nets = ALL NETS
anprc-op-log-print = PRINT LOG
anprc-op-log-print-tooltip = Print the full log onto paper.
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
anprc-op-sec-detail-stale = Command changed the key. Your card is out of date, so your traffic is readable. Get a current card.
anprc-op-sec-detail-plain = PT mode transmits in the clear even with a fill loaded. Switch to FH, SC or CT in SETTINGS to encrypt.
anprc-op-sec-fill = Fill: { $designation } ({ $faction })
anprc-op-sec-fill-none = Fill: none
anprc-op-sec-how-to = To load encryption, use your issued fill card on the pack.
anprc-op-sec-actions = FILL ACTIONS
anprc-op-sec-actions-help = These actions require confirmation. Zeroize or destroy the fill before the set is captured.
anprc-op-sec-zeroize = ZEROIZE
anprc-op-sec-zeroize-confirm = CLICK AGAIN TO ZEROIZE
anprc-op-sec-zeroize-tooltip = Remove the fill from the set and eject the card.
anprc-op-sec-destroy = DESTROY FILL
anprc-op-sec-destroy-confirm = CLICK AGAIN TO DESTROY
anprc-op-sec-destroy-tooltip = Remove the fill and destroy the card so it cannot be captured.
anprc-op-sec-recrypto = ORDER RECRYPTO
anprc-op-sec-recrypto-confirm = CLICK AGAIN TO RECRYPTO
anprc-op-sec-recrypto-tooltip = Supersedes every fill card your faction holds. Use this after a card or set is compromised. Requires command authority.

## search page

anprc-op-search-intro = The search receiver scans the band for other factions' nets. Each transmission it catches improves the fix. Once resolved, a net can be loaded into memory and monitored.
anprc-op-search-cost = Searching takes you off every loaded net. You cannot hear them or transmit while the search is running.
anprc-op-search-running-warning = SEARCHING - you're off every net and can't transmit.
anprc-op-search-offline = The set must be on and worn or planted to search.
anprc-op-search-start = START SEARCH
anprc-op-search-stop = STOP SEARCH
anprc-op-search-head = HEAD { $frequency }
anprc-op-search-idle = IDLE
anprc-op-search-contacts = CONTACTS
anprc-op-search-no-contacts = No contacts yet. Busy nets are easier to find.
anprc-op-search-partial = ~{ $frequency }  fix { $tier }/{ $max }
anprc-op-search-partial-help = Partial fix. Keep searching while the net is active to resolve the rest.
anprc-op-search-own = { $frequency }  { $net } (yours)
anprc-op-search-fixed = { $frequency }  { $net }
anprc-op-search-tune-into = Tune into:
anprc-op-search-tune-tooltip = Load this net into that memory. The previous contents are replaced.
anprc-op-search-no-memory = add a memory first

## settings page

anprc-op-set-mode = WAVEFORM
anprc-op-set-mode-fh = Frequency hopping. Resists jamming and can transmit inside jammer fields. Uses 1.5x battery and leaves a small direction-finding signature.
anprc-op-set-mode-sc = Single channel. Secure and efficient (0.75x battery), with no direction-finding signature while encrypted. No jamming resistance.
anprc-op-set-mode-ct = Cipher text. Hidden from interceptors and DF, but only other 117G sets with a matching fill can read it. Ordinary headsets hear static. Requires a fill card.
anprc-op-set-mode-pt = Plain text. Anyone can read you, jamming affects you more and DF can find you more easily. Uses half battery. Intended for emergencies.
anprc-op-set-power = OUTPUT POWER
anprc-op-set-power-lo = Low. 0.6x relay range, half battery use and half the DF risk.
anprc-op-set-power-med = Medium. Standard range, battery use and DF risk.
anprc-op-set-power-hi = High. 1.5x relay range, but double battery use and double DF risk.
anprc-op-set-squelch = SQUELCH
anprc-op-set-squelch-0 = 0: Open. Hear everything, including badly degraded traffic.
anprc-op-set-squelch-1 = 1: Mutes heavy static and traffic you cannot decrypt.
anprc-op-set-squelch-2 = 2: Also mutes moderately jammed traffic.
anprc-op-set-squelch-3 = 3 (default): Also mutes lightly jammed and badly degraded fringe traffic.
anprc-op-set-squelch-4 = 4: Maximum. Even slightly degraded long-range traffic is muted. Quiet, but weak stations may disappear.
anprc-op-set-receive = RECEIVING
anprc-op-set-monitor-off = LISTEN-ONLY: OFF
anprc-op-set-monitor-on = LISTEN-ONLY: ON
anprc-op-set-monitor-help = Hear every net in memory at once. You cannot transmit while this is enabled.
anprc-op-set-scan-off = SCAN: OFF
anprc-op-set-scan-on = SCAN: ON
anprc-op-set-scan-help = Hear every net in memory and move your active talk net to whichever one last received traffic.
anprc-op-set-station = STATION CALLSIGN
anprc-op-set-callsign-manual = { $callsign } (set by hand)
anprc-op-set-callsign-from-wearer = { $callsign } (your callsign)
anprc-op-set-callsign-unknown = UNKNOWN STATION
anprc-op-set-callsign-help = Everything sent through the pack uses this callsign. By default it uses yours. Set one manually when the radio should identify as a fixed station, such as PLT MAIN.
anprc-op-set-callsign-placeholder = Station callsign
anprc-op-set-callsign-set = SET
anprc-op-set-callsign-auto = USE MY OWN CALLSIGN
anprc-op-set-callsign-auto-tooltip = Clear the fixed station callsign and return to your own.
anprc-op-set-roster = ROSTER
anprc-op-set-roster-tooltip = Common station callsigns for your faction.
anprc-op-set-directory = OPEN NET DIRECTORY
anprc-op-set-directory-tooltip = Shows who holds each callsign on your faction's nets.
anprc-op-set-phone = PHONE
anprc-op-set-phone-tooltip = Call another radio or fixed phone using the handset. Also answers a ringing set or hangs up an active call.

## first-open briefing

anprc-op-intro-title = YOU'RE THE RADIO OPERATOR
anprc-op-intro-lead = Your pack keeps your squad connected. The panel tells you what needs attention and what to press, so you can learn the rest as you use it.
anprc-op-intro-point-1-title = 1. YOU ARE THE RELAY
anprc-op-intro-point-1 = Combat headsets only work near a relay carrying their net. Your pack relays every friendly net in memory, so your squad net needs to be loaded.
anprc-op-intro-point-2-title = 2. GET ON THE AIR
anprc-op-intro-point-2 = Wear the pack on your back and press LOAD STANDARD NETS. This turns it on, loads your squad and command nets and selects your squad net.
anprc-op-intro-point-3-title = 3. TALK
anprc-op-intro-point-3 = Type :r followed by your message to transmit on the net marked ON AIR. Press USE on another memory to switch nets.
anprc-op-intro-point-4-title = 4. WATCH THE BANNER
anprc-op-intro-point-4 = The strip below the screen shows what needs fixing next and usually gives you a button to fix it. Green means the set is ready.
anprc-op-intro-point-5-title = 5. GO DEEPER WHEN YOU'RE READY
anprc-op-intro-point-5 = Load your fill card through SECURITY to encrypt. The guided panel keeps normal settings on AUTO. For lower power, battery-saving modes, searching and other advanced controls, use VIEW to open the 117G faceplate.
anprc-op-intro-reopen = Press ? HELP at the top of the panel to see this again.
anprc-op-intro-dismiss = GOT IT
anprc-op-intro-guide = OPEN THE FULL GUIDE
anprc-op-intro-guide-tooltip = Open the guidebook pages for getting on the net and using the AN/PRC-117G.

# guided panel on AUTO (ANPRCSettingsPage)
anprc-op-set-auto-heading = HOW THE SET IS RUNNING
anprc-op-set-auto-on = AUTO. Standard waveform, output and squelch, using your own callsign. Nothing else to set - choose a net and talk.
anprc-op-set-auto-off = Someone changed this set from the faceplate:
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
anprc-op-set-return-to-auto-tooltip = Restore all faceplate settings to standard. Your memories and fill are left unchanged.
anprc-op-set-expert-hint = Output power, waveform, fixed station callsigns, the search receiver, direct frequencies and advanced operator techniques are controlled from EXPERT view. See the guidebook for details.
anprc-op-set-callsign-guided-help = The set normally uses your assigned callsign. Fixed station callsigns are configured from EXPERT view.
anprc-op-editor-frequency-expert = Direct frequencies are entered from EXPERT view. Friendly nets and any nets resolved by the search receiver appear in the list above.
anprc-op-std-net-entry = { $label } ({ $net })
