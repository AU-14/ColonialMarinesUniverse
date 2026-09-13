# CMU Agent Instructions

CMU is a C# multiplayer game based on RMC14 and RobustToolbox.
These are repository defaults. User instructions take precedence over
these defaults and skill guidance, subject to higher-priority instructions
and tool permissions.

## Carry the task through

Complete requested work through appropriate verification. Resolve routine
choices from the repository and conversation. Ask when missing information
would materially change the outcome and cannot be inferred; continue
independent work while waiting.

Honor corrections and scope changes. A status question does not cancel the
task. Preserve unrelated edits, including work from other agents.

Skills provide relevant workflows, not additional approval gates. Reuse
authorization already given. If an instruction blocks progress, identify
its file, quote the relevant rule, and explain the remaining dependency.

## Ground changes in the source

Read the relevant implementation and callers before editing. Verify APIs,
YAML fields, and behavior against source. Trace prototypes by ID through
their components and systems.

Follow `.editorconfig`, relevant `CONVENTIONS.md` sections where available,
and nearby working examples. Consult engine documentation when source
leaves a gap.

Components hold data; systems hold logic. Keep server authority, client
prediction, networking, and lifecycle ownership consistent with the
existing feature.

## Keep changes focused

Fix the cause at its source of truth. Reuse existing helpers and extension
points. Add abstractions, configuration, and defensive checks when the
actual requirements justify them.

Preserve behavior outside the requested change. Avoid unrelated refactors,
formatting, and file moves. Keep mechanical migrations distinguishable
from behavior changes.

Comments explain constraints, invariants, and non-obvious decisions.
Keep debugging history out of the code. Preserve useful errors and
diagnostics. Support performance claims with measurements.

## Respect CMU boundaries

Place new CMU code and prototypes in the branch's CMU-owned layout:
`Content.CMU/` or `_CMU14`. Use `CMU`/`cmu-` identifiers. Follow existing
feature lineage and avoid unrelated moves between fork and upstream areas.

Prefer events, partials, and supported overrides for inherited behavior.
Keep unavoidable upstream edits small.

Mark deliberate changes outside CMU-owned zones with `CMU14` tags using
the syntax in `CONVENTIONS.md`. Use one tag per contiguous block and
preserve tagged changes during merges. Mechanical upstream conformance
is exempt. Comment out deliberate removals unless the enclosing construct
is already fully tagged or rewritten.

Treat `RobustToolbox/`, its submodules, and `RSI.NET/` as external
dependencies. Modify them only when explicitly included in the request.

Localize new player-facing strings and preserve asset license metadata.

## Verify and report

Choose checks for the changed behavior. For gameplay changes, build the
affected content projects and add or run focused regression coverage that
can detect the failure. Check multiplayer and prediction behavior when
the change depends on them.

For documentation and configuration, validate formats and references.
Broaden or repeat checks when changes, failures, or unresolved concerns
justify it. Change test expectations only with evidence that they are stale.

Review the final diff. Report the outcome, verification commands and
results, and material limitations in concise language. Distinguish
environment failures from code failures and completed checks from
unverified behavior.

## Prepare submissions

Keep each PR focused; mapping changes use one PR per map. Describe the
resulting behavior, rationale, and verification. Leave generated changelog
files to the bot. Use LF endings and UTF-8 without a BOM.

CMU permits AI assistance except for engine-adjacent performance work.
Upstream-bound contributions must satisfy the receiving project's
contribution and AI-use policies.
