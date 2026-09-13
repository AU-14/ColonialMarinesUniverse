game-ticker-restart-round = Restarting round...
game-ticker-start-round = The round is starting now...
game-ticker-start-round-cannot-start-game-mode-fallback = Failed to start {$failedGameMode} mode! Defaulting to {$fallbackMode}...
game-ticker-start-round-cannot-start-game-mode-restart = Failed to start {$failedGameMode} mode! Restarting round...
game-ticker-start-round-invalid-map = Selected map {$map} is inelligible for gamemode {$mode}. Gamemode may not function as intended...
game-ticker-unknown-role = Unknown
game-ticker-delay-start = Round start has been delayed for {$seconds} seconds.
game-ticker-pause-start = Round start has been paused.
game-ticker-pause-start-resumed = Round start countdown is now resumed.
game-ticker-player-join-game-message = Welcome to CMU! If this is your first time playing, be sure to read the game rules, and don't be afraid to ask for help in LOOC (local OOC) or OOC (usually available only between rounds).
# The lobby title, shown as a heading beside SERVER INFO. Rendered as plain text, so do NOT add
# markup tags here - they would show literally. Everything else is sent as structured fields
# (GetRoundInfoFields in GameTicker.Lobby.cs) and drawn as a real table by the lobby's ServerInfo
# control, so it stays aligned at any panel width. Headings are the lobby-info-* keys below.
game-ticker-get-info-text = Colonial Marines Universe
game-ticker-get-info-preround-text = Colonial Marines Universe

# Column headings for the lobby round-info table.
lobby-info-govfor-ship = GOVFOR SHIP
lobby-info-opfor-ship = OPFOR SHIP
lobby-info-govfor-platoon = GOVFOR PLATOON
lobby-info-opfor-platoon = OPFOR PLATOON
lobby-info-planet = PLANET
lobby-info-gamemode = GAMEMODE
lobby-info-players = PLAYERS
lobby-info-round-time = ROUND TIME
lobby-info-players-value = {$count} ({$ready} ready)

game-ticker-no-map-selected = [color=#FFB500]Map not yet selected![/color]
game-ticker-no-map-selected-plain = Map not yet selected!
game-ticker-player-no-jobs-available-when-joining = When attempting to join to the game, no jobs were available.

# Displayed in chat to admins when a player joins
player-join-message = Player {$name} joined.
player-first-join-message = Player {$name} joined for the first time.

# Displayed in chat to admins when a player leaves
player-leave-message = Player {$name} left.

latejoin-arrival-announcement = {$character} ({$job}) has awakened from hypersleep!
latejoin-arrival-announcement-special = {$job} {$character} on deck!
latejoin-arrival-sender = Ship
latejoin-arrivals-direction = A shuttle transferring you to your station will arrive shortly.
latejoin-arrivals-direction-time = A shuttle transferring you to your station will arrive in {$time}.
latejoin-arrivals-dumped-from-shuttle = A mysterious force prevents you from leaving with the arrivals shuttle.
latejoin-arrivals-teleport-to-spawn = A mysterious force teleports you off the arrivals shuttle. Have a safe shift!

preset-not-enough-ready-players = Can't start {$presetName}. Requires {$minimumPlayers} players but we have {$readyPlayersCount}.
preset-no-one-ready = Can't start {$presetName}. No players are ready.

game-run-level-PreRoundLobby = Pre-round lobby
game-run-level-InRound = In round
game-run-level-PostRound = Post round
