# Client state recovery reports

The `cmu.client_state` sawmill correlates full-state requests, receipt ACKs, client-reported state application,
and nearby server performance incidents.
It is enabled by default; disable it at runtime with `cvar cmu.diagnostics.client_state_enabled false`.

- `full-state-request`: player account ID/name, requested/server/last received ACK ticks, repeat counts,
  ping, attached and missing entity IDs/prototypes/lifecycle/parent/map/grid, and time since round cleanup.
  Tick-zero requests are labeled `initial-or-manual`; nonzero requests are labeled `recovery`.
- `state-request-summary`: 30-second request totals, affected connected players, repeated requests,
  ACK progress, affected disconnections, suppressed detail count, and up to eight player/tick samples.
  Samples prioritize clients making the most recovery requests and include ACK lag/age and application telemetry.
- `sync-incident-open`: three nonzero requests in a reporting window, or a client report showing no application
  progress for at least ten seconds and five seconds of tick lag during an unpaused round. Each has a stable
  `syncIncidentId` and requests a bounded performance capture even if server TPS is healthy.
- `sync-incident-close`: requests stopped and recent client telemetry reports application progress, or requests
  stopped for a minute and receipt ACKs advanced without fresh application evidence. The outcome explicitly
  distinguishes these cases; receipt progress alone is not confirmed application recovery.
- `recent-server-error`: up to four errors from the preceding minute, with their original timestamps,
  categories and existing exception/inner-exception stack traces. These are correlation evidence, not
  proof that the server error caused the client failure. Each retained error is emitted at most once.
- `round-cleanup` and `disconnect-after-state-request`: markers for following failures across a restart
  or connection loss. Connection history is not discarded at round cleanup.

Search by account ID, requested tick and cleanup tick, then inspect the original server errors at the
reported timestamps. Repeated requests at the same tick across many users point to a shared incident;
one request can also be a manual reset and is not proof of a bug.

ACKs acknowledge receipt/queuing **before** the client applies the state. Updated clients send a small scalar
report every five seconds based on the engine's `GameStateApplied` callback, buffer counts and average FPS.
`clientAppliedState=client-reported` is explicitly untrusted diagnostic evidence, never gameplay authority.
`clientAppliedAgeSeconds` is the age at client sampling; `healthReportAgeSeconds` is the server-side age of
that sample. Stale telemetry is not current health. `clientAppliedState=unknown` means no accepted sample is
available. A fully hung/disconnected client cannot send reports; this does not collect client exception
stacks or prove rendering succeeded. Preserve the affected client's `client.stdout.log` as well.
`clientAvgFps=-1` means frame-timing samples are unavailable; application-progress evidence is still retained.

Use `perfIncidentId`, `lastStallTick`, `lastStallMs`, `lastStallAgeSeconds`, `serverTps`, `serverTpsValid` and
`perfSampleAgeSeconds` to correlate client symptoms with server stalls. Performance `detail-begin` records
with `source=client-sync` carry the corresponding `syncIncidentId`.

The observer never requests resets, alters PVS, captures fresh stack traces or scans entity trees. Client
health transport is controlled by the replicated `cmu.diagnostics.client_state_health_enabled` CVar, enabled
by default. The server rejects nonfinite/out-of-range values and accepts at most one sample per connection
every two seconds. Healthy reports are retained as scalar context rather than logged individually.
It reads a few entity fields only for emitted details. All client detail rows share a limit of
eight globally per 30 seconds. Full-state-request details additionally have a limit of one per player
per 30 seconds; summaries include suppressed requests.
Recent error text is capped at 8,192 characters per entry, with full text remaining in the original server log.
