# Mentor ghost
cmd-mghost-desc = Makes you a Mentor Ghost.
cmd-mghost-help = mghost

# CLF communications override
cmu-cmd-clfcomms-desc = Toggles the AU14 comms system over the CLF/INSFOR nets. Off means the cell's channels work like stock radio: no coverage requirement, no static, no callsigns.
cmu-cmd-clfcomms-help = Usage: clfcomms [on|off]. With no argument, reports the current state.
cmu-cmd-clfcomms-status-on = CLF comms: ON. The cell's nets are anchor-gated and run under the full comms system.
cmu-cmd-clfcomms-status-off = CLF comms: OFF. The cell's nets are running as stock radio.
cmu-cmd-clfcomms-master-off = Note: the master switch (au14.new_comms_system) is off, so this does nothing right now.
cmu-cmd-clfcomms-invalid-state = Could not read '{ $value }'. Use on or off.
cmu-cmd-clfcomms-already = CLF comms are already { $state }.
cmu-cmd-clfcomms-state-on = on
cmu-cmd-clfcomms-state-off = off
cmu-cmd-clfcomms-server = The server
cmu-cmd-clfcomms-admin-on = { $user } turned the comms system back on for CLF/INSFOR.
cmu-cmd-clfcomms-admin-off = { $user } turned the comms system off for CLF/INSFOR - their nets are stock radio now.
cmu-cmd-clfcomms-result-on = CLF comms ON. The cell is back under coverage rules, static and callsigns.
cmu-cmd-clfcomms-result-off = CLF comms OFF. The cell's nets now reach anywhere, unmasked and unjammed.
cmu-cmd-clfcomms-hint = on|off

# Saved builds
cmu-cmd-savebuild-desc = Save the player-built entities in a box around you to a shareable file.
cmu-cmd-savebuild-help = savebuild <name> [radius 0-5]
cmu-cmd-savebuild-player-only = This command can only be run by a player.
cmu-cmd-savebuild-radius-number = Radius must be a number.
cmu-cmd-buildsave-desc = Open the build-save selection panel.
cmu-cmd-buildsave-help = buildsave

# Z-level building
cmu-cmd-digup-desc = Dig straight up one z-level, surfacing at your current horizontal position.
cmu-cmd-digup-help = au_digup
cmu-cmd-digdown-desc = Dig straight down, creating/descending into a stone z-level beneath you.
cmu-cmd-digdown-help = au_digdown
cmu-cmd-zdig-player-only = This command must be run by an in-game player.
cmu-cmd-digup-success = Dug up a level.
cmu-cmd-digup-failed = Could not dig up here (nothing above, a wall blocks the spot above, or the feature is disabled).
cmu-cmd-digdown-success = Dug down a level.
cmu-cmd-digdown-failed = Could not dig down here (map opted out, feature disabled, or a hand-authored level is already below).

cmu-cmd-multiz-desc = List maps with their AU14 Multi Z-Level (vertical building) status, or toggle it per map / globally.
cmu-cmd-multiz-help = au_multiz  (list)  |  au_multiz <mapId> <on|off>  |  au_multiz global <on|off>
cmu-cmd-multiz-enabled = ENABLED
cmu-cmd-multiz-disabled = DISABLED
cmu-cmd-multiz-yes = Yes
cmu-cmd-multiz-no = No
cmu-cmd-multiz-global-list = Global AU14 z-building: { $state }  (toggle: au_multiz global on|off)
cmu-cmd-multiz-map-list =   MapId { $mapId } { $map } - Multi Z-Level: { $enabled }
cmu-cmd-multiz-usage = Usage: au_multiz <mapId|global> <on|off>
cmu-cmd-multiz-invalid-state = Second argument must be 'on' or 'off'.
cmu-cmd-multiz-global-changed = Global AU14 z-building is now { $state }.
cmu-cmd-multiz-invalid-map = Map argument must be a numeric MapId (run 'au_multiz' to list them) or 'global'.
cmu-cmd-multiz-map-missing = No map with MapId { $mapId }.
cmu-cmd-multiz-map-changed = Map { $mapId } Multi Z-Level set to { $enabled }. Players { $permission } build AU14 z-level stairs/floors here.
cmu-cmd-multiz-can-build = can now
cmu-cmd-multiz-cannot-build = can no longer

# Z-network weather
cmd-znetwork-weather-desc = Sets weather for all maps in a zNetwork.
cmd-znetwork-weather-help = znetwork-weather <zNetwork entity> <weather|null> [duration seconds]
cmu-cmd-znetwork-weather-entity-missing = Unable to find entity { $entity }.
cmu-cmd-znetwork-weather-component-missing = Target entity does not have CMUZLevelsNetworkComponent: { $entity }.
cmu-cmd-znetwork-weather-network-hint = zNetwork net entity
cmu-cmd-znetwork-weather-duration-hint = Duration in seconds

# Tool permissions
cmu-cmd-toolperm-desc = Grant, revoke, or list per-tool editor permissions by ckey.
cmu-cmd-toolperm-help = Usage: toolperm add <ckey> <tool> | toolperm remove <ckey> <tool> | toolperm list
    Tools: { $tools }
cmu-cmd-toolperm-no-grants = No tool grants.
cmu-cmd-toolperm-unknown-tool = Unknown tool '{ $tool }'. Tools: { $tools }
cmu-cmd-toolperm-granted = Granted '{ $tool }' to { $ckey }.
cmu-cmd-toolperm-revoked = Revoked '{ $tool }' from { $ckey }.
cmu-cmd-toolperm-no-change = Nothing changed.

# Nuke utilities
cmu-cmd-nuke-decals-desc = Deletes decals from every loaded grid.
cmu-cmd-nuke-decals-help = nuke:decals [all (true/false, default: false)] [decalId...] - Deletes decals from every loaded grid.
    By default this will only delete cleanable decals (like blood/dirt etc.) to spare map details.
    To delete all decals (including mapper placed details), pass 'true' as the first argument.
cmu-cmd-nuke-decals-scope-all = all decals
cmu-cmd-nuke-decals-scope-cleanable = cleanable only
cmu-cmd-nuke-decals-summary = Removed { $removed } decals ({ $scope }) from { $grids } grids.
cmu-cmd-nuke-decals-summary-filtered = Removed { $removed } decals matching { $filterCount } ids ({ $scope }) from { $grids } grids.
cmu-cmd-nuke-decals-skipped = [nuke:decals] { $count } matching decals were found but skipped because they have disabled defaultCleanable (janitor clean).
cmu-cmd-nuke-decals-retry = To delete them, run the command again starting with 'true' ('nuke:decals true { $ids }').
cmu-cmd-nuke-decals-hint-first = [all (default: false)] or [decalId]
cmu-cmd-nuke-decals-hint-id = [decalId...]

cmu-cmd-nuke-lights-desc = Deletes lights in a radius around you; use 'help nuke:lights' for more info.
cmu-cmd-nuke-lights-help = Usage: nuke:lights [radius=80] [energy=80] [duration=4] [x y mapId] [color=Orange]
cmu-cmd-nuke-lights-positive = Radius, energy, and duration must be greater than zero.
cmu-cmd-nuke-lights-no-attached = No attached entity. Provide x y mapId explicitly.
cmu-cmd-nuke-lights-color-error = Failed to parse color '{ $color }'. Use a name like Orange or a hex value like #ff8a00.
cmu-cmd-nuke-lights-map-missing = Map { $mapId } does not exist.
cmu-cmd-nuke-lights-spawned = Spawned global nuke light { $uid } at { $position } on map { $mapId } for { $duration }s.
cmu-cmd-nuke-lights-parse-value = Failed to parse { $name } '{ $value }'.
cmu-cmd-nuke-lights-parse-coordinates = Failed to parse coordinates '{ $x }' '{ $y }'.
cmu-cmd-nuke-lights-parse-map-id = Failed to parse map ID '{ $mapId }'.
cmu-cmd-nuke-lights-hint-radius = radius
cmu-cmd-nuke-lights-hint-energy = energy
cmu-cmd-nuke-lights-hint-duration = duration
cmu-cmd-nuke-lights-hint-x = x
cmu-cmd-nuke-lights-hint-y = y
cmu-cmd-nuke-lights-hint-map-id = mapId
cmu-cmd-nuke-lights-hint-color = color
