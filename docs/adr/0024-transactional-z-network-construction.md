# Construct Z-networks transactionally

A declared Multi-Z game map must load every auxiliary Z-level before its topology is committed.
If loading or validation fails, the Z-network lifecycle deletes the auxiliary maps it created and
the calling adapter fails instead of running a partial Z-network. The base level remains owned by
round orchestration, while mapping owns and rolls back every level it loads.

Complete topology is attached privately before auxiliary map initialization so map-init observers
never see a partial Z-network. The public topology-update event is raised only after every required
auxiliary map has initialized. Exceptions from component overrides, map initialization, or topology
attachment trigger compensating restoration of caller-owned components and deletion of
lifecycle-owned maps and the network.

Event dispatch itself is not atomic, so it is a post-commit notification rather than part of the
transaction. A subscriber exception is logged and the committed network is retained; rolling back
would leave arbitrary side effects from earlier subscribers attached to restored caller-owned maps.
Topology subscribers must remain exception-safe because one failure can prevent later subscribers
from receiving that notification.
