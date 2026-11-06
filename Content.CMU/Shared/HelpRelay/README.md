# HelpRelay

One Discord delivery pipe for the help systems (ahelp, mhelp, bughelp) and the round news digest: per-source webhooks, one spaced queue, one embed family.
Conversations, reports and news stay their own systems. Everything after the player types a help command, plus the round-end news dump, lives here.

## Server setup

| CVar | Default | Purpose |
|---|---|---|
| `cmu.help_relay_webhook` | empty | Primary webhook for ahelp/mhelp. Empty disables sources without an override. |
| `cmu.help_relay_webhook_ahelp` / `_mhelp` | empty | Per-source overrides. mhelp falls through to the primary; ahelp replaces upstream's `discord_ahelp_webhook` when set. |
| `cmu.help_relay_webhook_bughelp` | empty | Bughelp webhook, forum channel only. Never falls through: empty disables bughelp. |
| `cmu.help_relay_webhook_news` | empty | News webhook, forum channel only. Never falls through: empty disables the round digest. |
| `cmu.help_relay_webhook_errors` | empty | Error-log relay webhook, text channel only. Never falls through: empty disables the relay. |
| `cmu.help_relay_errors_sawmills` | empty | Sawmills mirrored at info-and-up in addition to the Error+ default; comma separated, `eng` also matches `eng.net` children. |
| `cmu.help_relay_revision` | empty | Stamped in embed footers; the publish pipeline fills it. |
| `cmu.help_relay_built_at` | empty | Build date (ISO yyyy-MM-dd) in footers next to the revision; empty on dev builds. |
| `cmu.help_relay_bughelp_tags` | empty | Forum tags per report category, grammar `code:<id>,mapping:<id>`. Unknown names ignored. |
| `CCVars.AdminLogsServerName` (vanilla) | | Server name in embed fields and footers. |

Channel types: the bughelp and news webhooks must be **forum channels** - bughelp always sends `thread_name` (one thread per report, Discord rejects forum posts without one), news creates one thread per round and posts the round's articles into it via `thread_id`.
ahelp/mhelp and the error relay use **text channels**: ahelp and mhelp one message per conversation, created with `wait=true`, then edited in place per line; the error relay posts batched embeds.
When a transcript outgrows one message, the continuation post links back to the earlier message using the ids captured from the create response.

## Setup tips

Forum tag IDs are not exposed by the Discord client UI. Verified route with a throwaway bot (Developer Portal, `bot` scope, View Channels permission):

1. `GET <webhook url>` (no auth) returns `channel_id` - the forum channel behind the webhook.
2. `GET /channels/<channel_id>` with `Authorization: Bot <token>` returns `available_tags`; copy each tag's `id`.
3. Fill the cvar, e.g. `code:1234...,mapping:5678...`. Keys are category names (`code`, `mapping`); tag display names are free, so a tag named `Map` takes the `mapping:` key.

Probe the chain without the game: `POST <webhook url>?wait=true` with a JSON body of `thread_name` plus `applied_tags` (the copied ids). A created thread means the ids were accepted - read the thread back through the bot to see them on it; a 400 means an id is bad.

## What goes out

| Part | One-shot (bughelp) | Conversation (ahelp/mhelp) |
|---|---|---|
| Embed | author, title, description, color, Round/Mode/Map/Server fields, timestamp, footer | description = transcript, color, footer |
| Thread | `thread_name`, plus `applied_tags` when tags are configured | none |
| Mentions | always `{"parse":[]}`; player-filed bughelp pings its reporter (`users` override + `<@id>` in content) | same, no ping |
| Files | report.json via multipart `payload_json` + `files[0]` | none |

`allowed_mentions` is load-bearing, not decoration: message edits re-parse content with default allowances unless the request sets it,
so transcript text can never grow a ping later. Interactive components (buttons) are application-webhook-only per Discord's docs; incoming webhooks cannot send them.

News (round digest) rides both models: at round end `NewsRelaySystem` creates the round's forum thread
(`thread_name = Round {id} · {mode} · {map}`, `?wait=true`, captures `channel_id` from the response), then each article
posts into it with `?thread_id`, one message per article, ordered by publish time. Reporter identity is captured
at publish time through a CMU14-tagged raise in upstream's `TryAddNews` (articles carry only IC identity) and shows
as the linked Discord account when one exists - a mention that never pings, the payload stays quiet - the ckey
otherwise; mid-round deletions are respected. Article embeds omit the
Round/Mode/Map/Server fields - the opener carries them. If the thread cannot be created after retries the whole
round is dropped with an error log: news is best-effort, nothing was promised to a reporter. Spooled news followers
record their `thread_id` in the drop file; the payload itself carries no thread binding, so manual recovery needs that id.
Upstream's `discord.news_webhook` must stay unset, or articles deliver through both pipes.

Server errors (ErrorRelaySystem) ride the one-shot model without threads: every Error/Fatal log line buffers from any
thread and flushes as batched embeds from the main thread, one flush every 5 s and at most three posts per flush; entries the
post budget does not reach wait for the next flush. Consecutive identical
entries collapse into a ×N marker; only lines past the 256-line buffer are counted in a suppression note
rather than half-posted. Fatal entries skip the batch and post immediately with the last context snapshot: Fatal
usually precedes the shutdown that would outrun the next flush. The relay never relays its own sawmills (helprelay,
errorrelay), so a failed delivery cannot loop. A hard kill (OOM, SIGKILL, watchdog) posts nothing; the console log
stays the only record of those.

Extreme admin logs (LogImpact.Extreme) join the same channel through the ahelp conversation model instead of one-shot
posts. AdminLogManager raises them through a CMU14-tagged hook in Add - gated at the raise site, so Low/Medium/High
traffic pays nothing - and ErrorRelaySystem appends buffered groups to one persistent feed message: edited in place,
chained to a fresh message when the description overflows, restarted at round end like every conversation. The feed
message's webhook username is "Errors · admin log feed"; each line carries a UTC stamp and the log type.
Explosion and verb logs are excluded: upstream grades player explosions Extreme past an intensity threshold and
smite verbs declare Extreme impacts, which normal rounds produce in bulk; the in-game admin chat alert still
fires from the source.

Every line is scrubbed before it leaves for Discord: IP addresses, MAC addresses, hwid values and 19+ digit platform
ids (Discord snowflakes) become `[ip]` / `[mac]` / `[hwid]` / `[id]` placeholders. Ckeys and character names pass
deliberately. The scrub is label- and shape-anchored, not a parser: bare `::1`-style addresses, short uncompressed
IPv6, hostnames and file paths are not caught, so the console log remains the full-fidelity record.

## Queue behavior

| Rule | Value |
|---|---|
| Post spacing | 1200 ms, Discord rate limits webhooks harder than a help burst |
| Retry | 3 attempts, 15 s apart, then spool |
| Queue cap | 64, new posts spooled when full |
| Spool | dropped posts (retries exhausted, queue full, round end) land in `<data dir>/dropped_relay/`, one JSON per post: source, time, payload, attachment. No webhook URL in the file. |
| Round end | queue, conversations and linked-id cache all cleared; undelivered queued posts spooled |

## Files

Paths relative to `Content.CMU/`.

| Path | Job |
|---|---|
| `Server/HelpRelay/Systems/HelpRelayManager.cs` | Webhook resolution, queue, conversation edit-in-place, linked-id cache, forum thread create |
| `Server/HelpRelay/Systems/HelpRelayPayload.cs` | Embed builders and Discord DTOs (self-contained, no upstream DTOs) |
| `Server/HelpRelay/Systems/HelpRelaySystem.cs` | Round-restart cleanup |
| `Server/HelpRelay/Systems/HelpSource.cs` | Source enum, per-source colors and labels |
| `Server/HelpRelay/BugHelp/BugHelpSystem.cs` | Bug report intake, artifact, posting |
| `Server/HelpRelay/BugHelp/BugHelpCommand.cs` | The `bughelp` command |
| `Server/HelpRelay/NewsRelay/NewsRelaySystem.cs` | Round-end news digest: gathers articles, creates the round thread, enqueues per-article followers |
| `Server/HelpRelay/ErrorRelay/ErrorRelaySystem.cs` | Server log relay: buffers Error/Fatal lines (plus allowlisted sawmills), flushes one embed per batch, posts Fatal immediately, feeds Extreme admin logs as a conversation |

Checks: `Content.Tests/Server/CMU14/HelpRelayPayloadTest.cs`, `Content.Tests/Server/CMU14/ErrorRelayFormatTest.cs`.
Category tags grammar and the report flow: `Content.CMU/Server/HelpRelay/BugHelp/README.md`.
