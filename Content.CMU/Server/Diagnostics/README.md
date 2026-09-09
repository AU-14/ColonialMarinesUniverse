# Client state recovery reports

The `cmu.client_state` sawmill adds server-side context to the engine's existing full-state-request logs.
It is enabled by default; disable it at runtime with `cvar cmu.diagnostics.client_state_enabled false`.

- `full-state-request`: player account ID/name, requested/server/last received ACK ticks, repeat counts,
  ping, attached and missing entity IDs/prototypes/lifecycle/parent/map/grid, and time since round cleanup.
- `state-request-summary`: 30-second request totals, affected connected players, repeated requests,
  ACK progress, affected disconnections, suppressed detail count, and up to eight player/tick samples.
- `recent-server-error`: up to four errors from the preceding minute, with their original timestamps,
  categories and existing exception/inner-exception stack traces. These are correlation evidence, not
  proof that the server error caused the client failure. Each retained error is emitted at most once.
- `round-cleanup` and `disconnect-after-state-request`: markers for following failures across a restart
  or connection loss. Connection history is not discarded at round cleanup.

Search by account ID, requested tick and cleanup tick, then inspect the original server errors at the
reported timestamps. Repeated requests at the same tick across many users point to a shared incident;
one request can also be a manual reset and is not proof of a bug.

ACKs acknowledge receipt/queuing **before** the client applies the state. `clientAppliedState=unknown`
is deliberate: this observer cannot prove recovery, detect every client freeze, or obtain the client's
exception stack. Keep the affected client's `client.stdout.log` for that part of the trace.

The observer does not send messages, request resets, alter PVS, capture fresh stack traces, scan entity
trees or run client code. It reads a few entity fields only for emitted details. Details are limited to
eight globally per 30 seconds and one per player per 30 seconds; summaries include suppressed requests.
Recent error text is capped at 8,192 characters per entry, with full text remaining in the original server log.
