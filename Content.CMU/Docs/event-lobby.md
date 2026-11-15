# Event lobby access and slots

Event mode is **disabled by default** and resets to disabled each round. Open
**Event setup** from the lobby (admins only) or **Admin menu → Round → Event
setup**. Enable it with the checkbox. The panel works before and during rounds;
it requires the `Admin` permission and closes if that permission is lost.

Choose a side/platoon from the dropdown. In **Access codes**, create or select a
group, enter its name and code, then protect either the whole side or selected
roles from the searchable list. **Save / rotate code** replaces the group and
revokes its earlier unlocks. **Remove group** opens its roles again.

In **Job slots**, search and select one role, choose **Add / remove slots** or
**Set available slots**, enter the count, and press **Apply**. The panel shows
queued changes in the lobby and current slot counts during a round. **Refresh**
updates counts after other players join; **Clear queued slot changes** cancels
pending edits. No console commands are needed for this workflow.

Disabling event mode immediately stops code restrictions and hides player code
entry. It also prevents new slot edits and stops queued edits from applying at
round start. Existing code groups remain configured until round reset, so they
can be enabled again. Slot edits already applied to the live round remain.

The optional commands below also support Tab completion for operations, groups,
sides/platoons, roles, and common slot amounts. `eventsetup` or `eventaccess`
without arguments opens the panel. Server-console setup uses `eventaccess
enable` / `eventaccess disable` for the switch.

## Invite players with codes

Create named groups of jobs, each protected by a code:

```text
eventaccess enable
eventaccess set russian-side russian-code opfor all
eventaccess set command command-code govfor PlatoonCommander ExecutiveOfficer
eventaccess list
eventaccess remove command
```

`set` replaces the named group and revokes its previous unlocks. `remove` opens
those jobs again, subject to any other groups and normal job requirements.
`list` shows group names, job counts, and the number of admitted players; it does
not display codes.

Players press **Event access code** in the lobby, enter the code, then choose
their job priorities and ready up normally. The same button works for late
joining. Players can also use `eventcode russian-code` in the console. Codes
update the open customization screen immediately: protected jobs show Locked,
and Preferred side offers only sides with an unlocked military role. When just
one side is available it is selected for the event's FoF roll, without changing
the saved preference for ordinary rounds. Equivalent FoF roles have one row;
hidden faction/equipment variants are excluded from normal setup lists. Advanced
commands can still target those variants by full job ID. Codes
are case-sensitive; quote codes containing spaces when using console commands.
There is a two-second delay between attempts, including while the lobby is
paused. Entering a code unlocks every group using that code.

Groups can overlap. A player must unlock **every** group protecting a job: if
OPFOR command jobs have both a side code and a command code, they need both.
Admission belongs to the account for the round, so changing character or
reconnecting does not lose it. Rotating a code revokes that group's admissions
for future joins; it does not remove characters already in the round.

Codes do not bypass bans, whitelists, playtime, character requirements, available
slots, or Force on Force balancing and respawn restrictions. They unlock
eligibility; they do not guarantee that a player rolls a particular job.

## Change slots

```text
eventslots add opfor PlatoonCorpsman 2
eventslots add govfor SquadRifleman 10
eventslots set opfor PlatoonCommander 1
eventslots add opfor PlatoonCorpsman -1
eventslots set govfor SquadRifleman -1
eventslots list opfor
eventslots clear
```

`add` changes the number of available slots by the signed amount. `set` replaces
the number of **available** slots, rather than the total including players who
already joined. Set zero to close further entry, or -1 for unlimited slots.
Existing players keep their jobs. Use `set` to change an unlimited role back to
a finite count.

In the lobby, changes queue until maps and normal job scaling finish loading,
before round-start job assignment. `list` includes queued changes. `clear`
discards pending changes; it does not undo changes already applied to a live
round. Live changes update available jobs immediately. A role absent from the
station's job list can be added when its side has one unambiguous station.
Slot changes do not create spawn points, equipment, ships, or additional squads.

## Select sides and roles

Commands offer Tab completion. A side can be `govfor`, `opfor`, `civilian`,
`indfor`, `thirdparty`, or `threat`. The prototype ID of a currently selected
GOVFOR/OPFOR platoon also works if it identifies exactly one side. Use the side
name if both sides use the same platoon. For example, after `setopfor` selects
your Russian platoon, its prototype ID can replace `opfor` in these commands.

A role selector can be a role family such as `PlatoonCommander`, a full job
prototype ID, or a department prototype ID. Access groups also accept `all` and
multiple selectors. Slot commands require exactly one matching job; if a family
has several variants, Tab-complete the full job ID instead.

Access groups resolve their job IDs when configured. Reconfigure the group if
its intended set of jobs changes. A queued slot change using a platoon ID is
revalidated at round start; if that platoon is no longer selected, the change
fails with an admin alert and server log message instead of changing another
platoon's slots. Slot commands use the normal ship/planet routing for military
sides, so closed copies of a job on other maps do not redirect the change.

## Digging control

`cmu.game.allow_digging_down` is a server CVar, default **true**. Set it to
**false** to stop shovels, pickaxes, and other pit-digging tools from creating
downward excavations in any game mode or event. It is checked both before digging
starts and when the action completes, so disabling it stops pending digs from
opening holes. It also applies to the admin dig-down driver. Upward digging and
using existing shafts are unaffected. This CVar is separate
from the round-local event-mode switch.

```text
cvar cmu.game.allow_digging_down false
```

Use `true` to allow downward digging again.

## Verification

The focused integration fixture exercises these paths:

| Behavior | Test |
| --- | --- |
| Admin group commands, lobby window, code redemption over the network and console, separate side/command admission, assignment, direct spawning, code rotation, client role locks | `CMUEventLobbyTest.CodesGateAssignmentDirectSpawnsAndClientRequirements` |
| Admin slot command, queued changes through real round start, immediate live changes, rejected removals, unlimited-to-finite transitions, round reset | `CMUEventLobbyTest.LobbyAndLiveSlotChangesRespectRoundLifecycle` |
| Disabled-by-default behavior, admin UI mutations, enable/disable synchronization, suspended queued changes, and permission revocation | `CMUEventLobbyTest.AdminPanelToggleSuspendsGatesAndRejectsChangesAfterDeadminning` |
| Global digging control and live CVar changes during excavation | `CMUDiggingPolicyTest.DiggingCVarRechecksToolsAndPendingExcavations` |

```powershell
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.IntegrationTests -Filter 'FullyQualifiedName~CMUEventLobbyTest|FullyQualifiedName~CMUDiggingPolicyTest'
```
