# AHelp tickets

Round-scoped tickets layered over upstream's bwoink AHelp system: a player's first message opens a
ticket, later messages append to it, and a message after a close opens a new one. Everything lives
in memory and drops on round restart. Upstream keeps the conversation itself (routing, rate limits,
its own edit-in-place Discord relay through `discord.ahelp_webhook`); this layer adds the ticket
state, the admin controls and a replayable history on top of it.

## Server setup

| CVar | Default | Purpose |
|---|---|---|
| `cmu.ahelp_ticket_show_claimer` | true | Tell the player the claiming admin's name on claim and acknowledge lines. Stealthed admins are never named. |
| `CCVars.AdminAhelpOverrideClientName` (vanilla) | empty | AHelp name override, respected for the player-facing claim/acknowledge name. |

## Behavior

| Rule | Value |
|---|---|
| Open | First player message in their own channel with no open ticket |
| Answered | First admin reply that players can see, whatever the claim state; survives claim and unclaim |
| Claim | Acknowledge claims too; a reply into an unclaimed ticket claims it; claiming takes over from another admin |
| Release | Claims return to the open queue when the holding admin leaves or loses Adminhelp; in-game admins keep theirs |
| Close | Trimmed non-empty reason, sent to the player and admin-logged; closed tickets stay visible until round end |
| History | 200 lines per channel, server and admin mirror alike; lines past 4000 chars store as a placeholder |
| Snapshot | Admins get every ticket plus up to 100 recent channels: on login, on reaching InGame, or on request. Open first, then most recently active |
| Permissions | Only Adminhelp admins act on tickets, checked on every request; in-game admins count (they are inactive but handle AHelps) |

Status lines and close reasons echo into the channel formatted like live lines, so a replayed
channel reads like the live one, and queue on the Discord relay as `:ticket:` lines through
`BwoinkSystem`'s existing webhook conversation. The relay is upstream's, not HelpRelayManager's:
ahelp delivery stays on `discord.ahelp_webhook` by design (`Docs/HELP-RELAY-DESIGN.md` §5).

## Files

Paths relative to `Content.CMU/`.

| Path | Job |
|---|---|
| `Shared/HelpRelay/AHelp/CMUTicketMessages.cs` | Net messages: ticket state, snapshot/update events, request events, limits |
| `Server/HelpRelay/AHelp/CMUTicketSystem.cs` | Ticket lifecycle, history, snapshots, permission checks |
| `Server/HelpRelay/AHelp/BwoinkSystem.CMU.cs` | Partial over upstream `BwoinkSystem`: AHelp admin views, ticket hooks, Discord ticket lines |
| `Client/HelpRelay/AHelp/CMUTicketClientSystem.cs` | Admin mirror: tickets plus live history, snapshot requests |
| `Client/HelpRelay/AHelp/AHelpUIController.CMU.cs` | Partial over upstream `AHelpUIController`: replay after a window rebuild, in-game admin window access |
| `Client/HelpRelay/AHelp/BwoinkControl.CMU.cs` | Partial over upstream `BwoinkControl`: acknowledge/claim/close controls, ticket glyph, status line |
| `Client/HelpRelay/AHelp/CMUTicketCloseWindow.xaml(.cs)` | Close-reason prompt; server rechecks the reason |
| `Server/Administration/AdminManager.CMU.cs` | AHelp view of admin data: in-game admins count as ticket admins |
| `Resources/Locale/en-US/CMU14/administration/cmu-ahelp-tickets.ftl` | All strings |

The three `*.CMU.cs` partials keep their upstream namespaces on purpose (they extend upstream
classes); only their folder is CMU-side. Don't "fix" that.

Checks: `Content.IntegrationTests/_CMU14/Administration/CMUTicketSystemTest.cs`,
`Content.IntegrationTests/_CMU14/Administration/CMUAHelpReplayTest.cs`,
`Content.IntegrationTests/_CMU14/Administration/CMUIngameAdminTest.cs`.
Discord delivery and the other sources: `Content.CMU/Shared/HelpRelay/README.md`.
