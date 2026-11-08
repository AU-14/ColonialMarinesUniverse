# Game UDP receive buffers

Use this profile when the game socket's receive-buffer drop counter grows under
normal player load. Socket drops can discard movement inputs and acknowledgements
before the game can process them. Check the game's socket, not only cumulative
container-wide UDP counters.

The profile requests 4 MiB per game receive socket as an initial tuning value.
Validate it at comparable player load. A larger queue can absorb bursts but
cannot fix a receiver that remains slower than the incoming stream or a server
whose simulation exceeds its tick budget.

## Profile the live game socket

`profile-game-socket.py` runs on the Linux host with Python 3, `cmu-perf`,
`nsenter`, and `ss` available. It needs permission to read the game's `/proc`
entries and enter its network namespace. It discovers the current game PID
through `cmu-perf status`; do not pass a PID copied from an earlier capture.

```sh
python3 profile-game-socket.py --seconds 90 --interval-ms 50 > socket-profile.json
```

Sampling is bounded to 5-120 seconds at intervals of 50-1000 milliseconds.
The script reads socket receive/transmit queues and drops, IPv4 UDP namespace
counters, and scheduling totals for the main and Lidgren threads. It records
`ss` socket memory limits before and after. It does not attach a managed
profiler, change socket options, read packet payloads, or record peer addresses.
Capture output belongs in private diagnostic storage, never in a commit.

Use `complete`, process start ticks, socket inode, and actual sample intervals
before comparing counters. The capture stops if the process or socket changes.
`readMs` and `samplerCpuMs` record sampler overhead; missed slots are skipped.
The thread names are Linux's truncated names, so IPv4 and IPv6 Lidgren threads
may have identical labels and must be distinguished by TID.

Socket drops are specific to the owned game socket. `udp` counters cover its
whole network namespace; their packet rate is not an exact per-socket rate.
Queue bytes include kernel packet-memory accounting, not just payload bytes.
Low sampled queue occupancy does not rule out bursts between samples.
Scheduling counters separate CPU execution from runnable waiting; neither
measures managed-message queue age or identifies a blocking call. The Lidgren
thread drains the socket before the game thread dispatches its managed queue.
Keep those two sources of input delay distinct when interpreting the results.

## Receive during outgoing bursts

The CMU engine enables nonblocking socket reads between completed connection
heartbeats after 32 outgoing packets. This lets queued inputs and ACKs reach the
managed receive queue while the server is still sending other players' states.
A single connection can exceed the interval. All reads share the original
whole-heartbeat packet and byte limits, and client peers keep their original
receive schedule. The server also dispatches queued messages between additional
simulation catch-up ticks.

`net.receive_interleave_packets` controls the server's sent-packet interval at
socket creation. Set it to `0` in the instance configuration and restart during
maintenance to disable interleaving; the default is `32`. This is independent of
`net.receivebuffersize` and does not enlarge the kernel queue. Measure socket
drops and TPS at comparable load after deployment before attributing a change
in rubberbanding to either setting.

## Apply during planned maintenance

1. Record the current host `net.core.rmem_max`, the instance's existing
   `net.receivebuffersize`, and the live socket's receive-buffer size and drops.
   Preserve a private backup of the instance configuration.
2. Install `90-cmu-network-buffers.conf` under `/etc/sysctl.d/` and load that
   specific file with `sysctl -p`. Preserve any existing higher ceiling. For a
   containerized server, verify the effective ceiling in its network namespace.
3. Merge `server.toml` into the instance's existing `[net]` section. Do not
   replace the instance configuration or duplicate its `[net]` table. The
   watchdog instance config is authoritative; the packaged example config is
   not a deployment override.
4. Restart the game through its normal maintenance procedure. The game applies
   the configured buffer at socket creation. Changing the host limit or the
   CVar alone does not resize an already-open game socket.
5. Verify the new process identity and use `ss -ulnm` in the game's network
   namespace to check the bound game port. Linux reports doubled socket-buffer
   accounting, so a successful 4 MiB request normally appears as `rb8388608`.
   A smaller value indicates a cap or an unapplied configuration.
6. Compare the same socket inode's drop counter over a representative interval
   with no profiler attached. Record connected/attached players, packet rate,
   TPS, frame stalls, and client state-buffer behavior. A new socket resets its
   counters; do not subtract counters across restarts.

For rollback, restore the original instance setting and this profile's previous
host ceiling (or remove this file if it was newly installed), load the intended
sysctl configuration, and restart during maintenance. Verify the actual socket
size afterward. Neither applying nor rolling back the ceiling changes an open
socket's existing allocation.
