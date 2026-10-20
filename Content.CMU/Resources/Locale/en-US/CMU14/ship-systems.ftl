cmu-ship-gravity-lost = The deck lurches as the ship's gravity cuts out!
cmu-ship-gravity-restored = Your weight settles back onto your feet as the ship's gravity returns.
cmu-zero-g-no-deck = There's no deck that way.
cmu-zero-g-blocked-up = There's no open space above you to float through.
cmu-zero-g-blocked-down = There's no open space below you to float through.

cmu-cycle-airlock-other-open = The other door of the airlock is open.

cmu-station-alert-firelock = Firelock lockdown triggered at {$area}.
cmu-station-alert-unknown-area = an unknown location

# Atmospherics alert computer screen
cmu-station-alert-title = Atmospherics Alert Computer
cmu-station-alert-heading = ATMOSPHERICS ALERTS
cmu-station-alert-subheading = SHIPWIDE AIR MONITOR // ALL DECKS
cmu-station-alert-status-scanning = SCANNING
cmu-station-alert-status-clear = ALL CLEAR
cmu-station-alert-status-alerts = { $count ->
    [one] 1 ALERT
   *[other] { $count } ALERTS
}
cmu-station-alert-none = NO ATMOSPHERIC HAZARDS DETECTED
cmu-station-alert-footer = Waiting for the first scan.
cmu-station-alert-footer-scanned = Last scan { $time }. Updates every 2 seconds.
cmu-station-alert-hazard-breach = BREACH
cmu-station-alert-hazard-fire = FIRE
cmu-station-alert-hazard-toxic = TOXIC GAS
cmu-station-alert-hazard-lowoxygen = LOW OXYGEN
cmu-station-alert-hazard-lowpressure = LOW PRESSURE
cmu-station-alert-hazard-highpressure = OVERPRESSURE
cmu-station-alert-hazard-freezing = FREEZING
cmu-station-alert-hazard-firelocksealed = FIRELOCK SEALED
cmu-station-alert-detail-deck = DECK { $deck }
cmu-station-alert-detail-tiles = { $tiles } TILES
cmu-station-alert-detail-pressure = MIN { $pressure } kPa
cmu-station-alert-detail-temperature = MAX { $temperature }°C
cmu-station-alert-detail-firelocks = { $count } FIRELOCKS SEALED
