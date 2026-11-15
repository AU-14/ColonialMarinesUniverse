# Rubberband diagnostics

Enable with `cvar cmu.rubberband_diagnostics.enabled true` in the client console;
disable with `false`. These diagnostics are off by default.

Motion snapshots retain values without formatting component descriptions each
frame. Physics, mover and relay descriptions are formatted only when a log is
emitted. Position comparisons run after prediction replay: the older server
position seen during state application is not itself a visible correction.
Movement inherited from a vehicle or other parent is logged separately as
`parent-motion`; changing parents starts a new comparison baseline.

State progress has three messages:

- `state-catchup` warns when at least the configured number of states have applied
  in one frame.
- `state-tick-gap` warns when applied ticks skip forward without reaching the
  catch-up threshold. A tick gap is not a packet-loss counter. These two warning
  types share the configured log cooldown.
- `state-low-buffer` is an informational sample after an ordinary state
  application, at most once every five seconds (or the configured cooldown, if
  longer). Low buffer alone does not establish a stall. Buffer traversal is
  skipped between samples unless a catch-up/tick-gap warning is due.

These are selected observations, not counts of every update or low-buffer
occurrence. Each state-progress line reuses its applicable-state count. The
`payload` field is the uncompressed state size, not network bytes received.

For a short sequence-level capture, enable both network logging and Debug-level
state messages:

```text
cvar cmu.rubberband_diagnostics.enabled true
cvar net.logging true
loglevel state Debug
```

Reproduce one burst, then restore `cvar net.logging false` and
`loglevel state Info`. Save `client.stdout.log` before the next client launch and
correlate its timestamps with the server log. Received-state from/to sequences
help investigate delivery; these observations alone do not establish packet loss,
retransmission or server-side batching.
