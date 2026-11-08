# Game UDP receive buffers

Use this profile when the game socket's receive-buffer drop counter grows under
normal player load. Socket drops can discard movement inputs and acknowledgements
before the game can process them. Check the game's socket, not only cumulative
container-wide UDP counters.

The profile requests 4 MiB per game receive socket as an initial tuning value.
Validate it at comparable player load. A larger queue can absorb bursts but
cannot fix a receiver that remains slower than the incoming stream or a server
whose simulation exceeds its tick budget.

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
