cmd-clanhall-desc = Opens the Yautja clan hall.
cmd-clanhall-help = Usage: clanhall
cmd-clanhall-denied = Only blooded hunters and clan staff may enter the clan hall.

cmu-yautja-hall-verb = Open Clan Hall
cmu-yautja-bracer-menu-clan-hall = Clan hall
cmu-yautja-bracer-menu-clan-hall-detail = Clan, hunters, honor
cmu-yautja-clan-admin-open = Yautja clan hall

cmu-yautja-hall-title = CLAN HALL
cmu-yautja-hall-honor = Honor
cmu-yautja-hall-standing = Standing
cmu-yautja-hall-standing-value = #{ $standing } of { $count }
cmu-yautja-hall-viewer-line = { $rank }  ·  { $clan }
cmu-yautja-hall-viewer-staff = clan staff
cmu-yautja-hall-viewer-host = host
cmu-yautja-hall-clanless = No clan
cmu-yautja-hall-clan-unknown = Unknown clan
cmu-yautja-hall-staff = STAFF
cmu-yautja-hall-online = Online
cmu-yautja-hall-offline = Offline
cmu-yautja-hall-ready = Bracer link steady.
cmu-yautja-hall-transmitting = Transmitting
cmu-yautja-hall-error = The bracer link faltered. Try again.

cmu-yautja-hall-tab-clan = Clan
cmu-yautja-hall-tab-boards = Boards
cmu-yautja-hall-tab-hunter = Hunter
cmu-yautja-hall-tab-whitelists = Whitelists
cmu-yautja-hall-tab-round = Round

cmu-yautja-hall-apply = Apply
cmu-yautja-hall-save = Save
cmu-yautja-hall-cancel = Cancel
cmu-yautja-hall-back = Back
cmu-yautja-hall-are-you-sure = Press again to confirm
cmu-yautja-hall-search = Search hunters...

# clan page
cmu-yautja-hall-clan-line = #{ $standing } of { $clans } clans  ·  { $members ->
    [one] 1 hunter
   *[other] { $members } hunters
}
cmu-yautja-hall-notice = Clan notice
cmu-yautja-hall-notice-empty = No notice has been posted.
cmu-yautja-hall-description = Clan lore
cmu-yautja-hall-elite-notes = Elite council
cmu-yautja-hall-notes-empty = Nothing has been set down for the elite.
cmu-yautja-hall-roster = Roster
cmu-yautja-hall-roster-empty = This clan has no hunters.
cmu-yautja-hall-edit-look = Clan sigil
cmu-yautja-hall-edit-notice = Post notice
cmu-yautja-hall-edit-description = Edit lore
cmu-yautja-hall-edit-notes = Elite notes
cmu-yautja-hall-name = Name
cmu-yautja-hall-sigil = Mask
cmu-yautja-hall-color = Colour
cmu-yautja-hall-material-ebony = Ebony
cmu-yautja-hall-material-silver = Silver
cmu-yautja-hall-material-bronze = Bronze
cmu-yautja-hall-material-crimson = Crimson
cmu-yautja-hall-material-bone = Bone
cmu-yautja-hall-set-honor = Clan honor
cmu-yautja-hall-disband = Disband clan
cmu-yautja-hall-disband-confirm = Press again to disband
cmu-yautja-hall-petition = Petition to join
cmu-yautja-hall-petition-switch = Petition here instead
cmu-yautja-hall-withdraw = Withdraw petition
cmu-yautja-hall-petition-pending = Your petition stands before this clan. Its Leader or Elders will answer it.
cmu-yautja-hall-petitions = Petitions ({ $count })
cmu-yautja-hall-accept = Accept
cmu-yautja-hall-refuse = Refuse
cmu-yautja-hall-no-clan = You belong to no clan. Find one on the boards and petition to join it.
cmu-yautja-hall-no-clan-young = You are not yet blooded. Prove yourself, and the clans will open their halls to you.
cmu-yautja-hall-clan-hidden = That clan's hall is closed to you.
cmu-yautja-hall-see-boards = See the boards
cmu-yautja-hall-new-clan-description = A new clan. Its Leader has yet to set down its lore.

# boards
cmu-yautja-hall-board-clans = Clans
cmu-yautja-hall-board-hunters = Hunters
cmu-yautja-hall-col-clan = Clan
cmu-yautja-hall-col-leader = Leader
cmu-yautja-hall-col-hunters = Hunters
cmu-yautja-hall-col-hunter = Hunter
cmu-yautja-hall-col-honor = Honor
cmu-yautja-hall-filter-all = All clans
cmu-yautja-hall-clan-locked = Only blooded hunters may look inside other clans.
cmu-yautja-hall-no-clans = No clans have been founded.
cmu-yautja-hall-no-hunters = No hunters match.
cmu-yautja-hall-found = Found a clan
cmu-yautja-hall-found-name = Clan name
cmu-yautja-hall-found-button = Found

# hunter page
cmu-yautja-hall-hunter-hidden = That hunter's record is not yours to see.
cmu-yautja-hall-set-rank = Rank
cmu-yautja-hall-standing-actions = Standing
cmu-yautja-hall-exile = Exile from clan
cmu-yautja-hall-exile-confirm = Press again to exile
cmu-yautja-hall-make-ancient = Raise to Ancient
cmu-yautja-hall-strip-ancient = Strip Ancient
cmu-yautja-hall-purge = Purge profile
cmu-yautja-hall-purge-confirm = Press again to purge
cmu-yautja-hall-move = Move to clan
cmu-yautja-hall-move-button = Move
cmu-yautja-hall-whitelist = Whitelist
cmu-yautja-hall-self = This is your own record.
cmu-yautja-hall-no-authority = You hold no authority over this hunter.

# staff
cmu-yautja-hall-grant = Grant Yautja
cmu-yautja-hall-grant-placeholder = Player name or user id
cmu-yautja-hall-open-record = Open record
cmu-yautja-hall-no-whitelist = Nobody holds a Yautja whitelist.
cmu-yautja-hall-revoke = Revoke
cmu-yautja-hall-revoke-confirm = Press again: revokes all and wipes their profile
cmu-yautja-hall-flag-yautja = Yautja
cmu-yautja-hall-flag-legacy = Legacy
cmu-yautja-hall-flag-council = Council
cmu-yautja-hall-flag-councillegacy = Council legacy
cmu-yautja-hall-flag-leader = Leader
cmu-yautja-hall-round-info = Round { $round }  ·  { $active ->
    [true] in progress
   *[false] in lobby
}  ·  hunt { $initialized ->
    [true] initialized
   *[false] not initialized
}
cmu-yautja-hall-round-slot-info = Active hunter slots: { $active }  ·  configured for the next hunt: { $configured }
cmu-yautja-hall-round-init = Initialize hunt
cmu-yautja-hall-round-slots = Hunter slots
cmu-yautja-hall-round-random = Random hunts
cmu-yautja-hall-round-random-on = Random hunts on
cmu-yautja-hall-round-random-off = Random hunts off
cmu-yautja-hall-round-every = every
cmu-yautja-hall-round-rounds = rounds
cmu-yautja-hall-round-random-left = Next random hunt in { $rounds } rounds.
cmu-yautja-hall-round-random-idle = Random hunts are off.

# results
cmu-yautja-hall-not-found = No player called { $player } was found.
cmu-yautja-hall-done-rank = Rank set.
cmu-yautja-hall-done-exile = The hunter has been cast out of the clan.
cmu-yautja-hall-done-move = The hunter has been moved.
cmu-yautja-hall-done-ancient = The hunter is raised to Ancient.
cmu-yautja-hall-done-unancient = The hunter is no longer an Ancient.
cmu-yautja-hall-done-purge = The hunter's profile has been purged.
cmu-yautja-hall-done-saved = Saved.
cmu-yautja-hall-done-honor = Clan honor set.
cmu-yautja-hall-done-found = The clan is founded.
cmu-yautja-hall-done-disband = The clan is disbanded. Its hunters are clanless.
cmu-yautja-hall-done-petition = Your petition has been sent.
cmu-yautja-hall-done-withdrawn = Your petition has been withdrawn.
cmu-yautja-hall-done-accepted = The hunter has joined the clan.
cmu-yautja-hall-done-refused = The petition has been refused.
cmu-yautja-hall-done-whitelist = Whitelist updated for { $player }.

cmu-yautja-hall-denied-rank = You cannot give that hunter that rank.
cmu-yautja-hall-denied-exile = You cannot exile that hunter.
cmu-yautja-hall-denied-move = You cannot move that hunter there.
cmu-yautja-hall-denied-ancient = Only the council's leader can raise or strip an Ancient.
cmu-yautja-hall-denied-purge = You cannot purge that hunter.
cmu-yautja-hall-denied-no-profile = That hunter has no clan profile.
cmu-yautja-hall-denied-edit = You cannot change that.
cmu-yautja-hall-denied-text = That is too long, or empty where it can't be.
cmu-yautja-hall-denied-sigil = That is not a clan mask.
cmu-yautja-hall-denied-color = Colours are written as #RRGGBB.
cmu-yautja-hall-denied-name = A clan needs a name, and not too long a one.
cmu-yautja-hall-denied-honor = Only an Ancient can set a clan's honor.
cmu-yautja-hall-denied-honor-value = Honor can't go below zero.
cmu-yautja-hall-denied-found = Only an Ancient can found or disband a clan.
cmu-yautja-hall-denied-petition = Only a clanless hunter can petition a clan.
cmu-yautja-hall-denied-no-petition = That petition is no longer standing.
cmu-yautja-hall-denied-petition-answer = Only that clan's Leader or Elders can answer it.
cmu-yautja-hall-denied-already-clanned = That hunter already joined another clan.
cmu-yautja-hall-denied-clan-gone = That clan no longer exists.
cmu-yautja-hall-denied-whitelist = Only clan staff can change whitelists.
cmu-yautja-hall-denied-whitelist-flags = Unknown whitelist flags.
cmu-yautja-hall-denied-round = Only the host can change the hunt.
