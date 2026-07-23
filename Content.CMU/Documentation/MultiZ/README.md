# CMU Multi-Z Modernization

This directory is the engineering record for restoring and modernizing CMU's Multi-Z system.

The active implementation sequence is:

1. Restore feature parity with the pre-rebase implementation.
2. Audit every affected subsystem before changing behavior.
3. Modernize in reviewable, measured increments.
4. Validate performance and multiplayer behavior with profiling evidence.

The legacy reference is the final tree of `origin/Zlevels` at `5322cb4ee55bcfe2ae0b91b2a41d4db3b786e9f6`.
The initial post-rebase port target is `d25e5d8950a0c2f25a67c900e2246ad1a68f6327`.

See [phase-1-port-log.md](phase-1-port-log.md) for the compatibility port and
[phase-2-audit-report.md](phase-2-audit-report.md) for the subsystem review,
[phase-3-modernization-log.md](phase-3-modernization-log.md) for the implemented modernization,
and [audit-log.md](audit-log.md) for the canonical running finding register.
