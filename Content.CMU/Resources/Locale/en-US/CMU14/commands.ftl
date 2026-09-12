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
