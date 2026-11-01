# BugHelp

In-game bug reports: the `bughelp` command plus a report window,
relayed to a Discord forum channel as one thread per report through HelpRelay.
Player-typed context is minimal by design. The server snapshots what it can observe.

## Server setup

| CVar | Default | Purpose |
|---|---|---|
| `cmu.help_relay_webhook_bughelp` | empty | Bug report webhook. Empty falls through to the primary webhook. |
| `cmu.help_relay_bughelp_tags` | empty | Forum tag ids per category, grammar `code:<id>,mapping:<id>`. Malformed entries are skipped with a server warning. |
| `cmu.help_relay_webhook` | empty | Primary webhook (ahelp/mhelp/bughelp). Both empty disables bughelp. |
| `cmu.help_relay_revision` | empty | Build revision stamped on reports; the publish pipeline fills it. |
| `cmu.help_relay_built_at` | empty | Build date (ISO yyyy-MM-dd) stamped with the revision; says how old the build is. |
| `cmu.bug_help_rate_limit_period` | 300 | Seconds per rate limit window. |
| `cmu.bug_help_rate_limit_count` | 3 | Reports a player may file per window. |
| `discord.token` + `discord.guild_id` (vanilla) | empty | Vanilla DiscordLink bot: fills `discord_id` on reports via linked accounts. Without them, reports post without the reporter mention. |

The webhook must point at a Discord **forum channel**, with `Code` and `Map` tags.
each report opens one thread titled `[Category] BUG-R<round>-<seq> · <what happened>` (100 char cap).

## Install tips

Tag keys are category names, not tag names: `mapping:` is correct even when the forum tag is called `Map`.
Tag IDs are invisible in the Discord client; the bot route that fetches them, plus a webhook probe that verifies them, lives in `Content.CMU/Server/HelpRelay/README.md` (Setup tips).

Reporter pings need the vanilla bot (`discord.token` + `discord.guild_id`) **in the guild** - linked accounts resolve through it.
Without it the pipeline still works; reports just post without the mention.
Keep throwaway dev tokens out of tracked config: blank the line before staging, or use the gitignored exe-relative `bin/Content.Server/server_config.toml`, which outranks the build preset.

Quick check before a test round: the probe post creates a tagged thread (HelpRelay README), then `bughelp` in-game closes the loop.

## Player surface

| Entry | Behavior |
|---|---|
| `bughelp` | Opens the report window. |
| `bughelp <text>` | Direct submit as Code. |
| Window: Talk to staff | Handoff to the mentor help window. |
| Mentor promote | Staff files a conversation transcript as an anonymized report (mentee shows as User); ack/decline arrive as SERVER lines. |

Form fields (client caps mirror the server trust boundary):

| Field | Required | Cap |
|---|---|---|
| Category | yes (dropdown) | enum |
| What happened | yes | 2000 |
| Expected / Steps | no | 1000 each |
| Examine text | no | 2000 |
| Item involved | no | 64 chars, resolves to 5 prototype candidates server-side |

## Pipeline

`BugHelpSubmitMsg`
→ `BugHelpSystem.TrySubmit` (cap, rate limit, snapshot)
→ `BugReport` JSON (`cmu-bugreport/1`)
→ `HelpRelay.Post` → named, tagged forum thread + attached artifact JSON.

The snapshot carries position, grid, job, active hand, clothing and nearby prototypes.
Promoted reports file as Code.

## Adding a category

Add a `BugHelpCategory` member and a `cmu-bughelp-category-<name>` line in `bug-help.ftl`.
Window dropdown, submit path, artifact and thread title pick it up.
Unknown wire values coerce to Code at the server boundary.

## Files

Paths relative to `Content.CMU/`.

| Path | Job |
|---|---|
| `Shared/BugHelp/BugHelpMessages.cs` | Net messages, `BugHelpCategory` |
| `Server/BugHelp/BugHelpSystem.cs` | Submit/promote flows, snapshot, report build, thread title |
| `Server/BugHelp/BugHelpCommand.cs` | `bughelp` command |
| `Client/BugHelp/BugHelpWindow.xaml(.cs)` | Report form |
| `Client/BugHelp/BugHelpUIController.cs` | Window lifecycle, send |
| `Server/HelpRelay/` | Discord delivery, per-source webhooks (separate subsystem) |
| `Resources/Locale/en-US/CMU14/bug-help.ftl` | All strings |
| `Content.Tests/Server/CMU14/BugHelpItemScorerTest.cs` | Item candidate scorer |
