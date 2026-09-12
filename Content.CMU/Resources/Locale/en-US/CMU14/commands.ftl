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
