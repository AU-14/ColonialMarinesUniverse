# CMU Agent Instructions

> **Repository house style.** Advisory defaults for contributors and their agents, not a
> replacement for personal configuration. If a user-provided instruction file (`AGENTS.md`,
> `CLAUDE.md`, ...) conflicts with this file, the user's instruction wins. Technical
> conventions (C#, YAML, localization, engine idioms) live in `CONVENTIONS.md` and take
> priority over general guidance here. New agent sessions bootstrap in order: the user's
`AGENTS.md`/`CLAUDE.md` → this file → `CONVENTIONS.md` → the nearest existing example of
what you are about to build. Skipping to code is the failure mode.

## Before changing code

Read the implementation, its interfaces, and its call sites before assuming anything about
APIs, behavior, or configuration. Trace symbols concretely:

`symbol → definition → callers → state/data flow`

Search the actual codebase rather than remembered documentation or plausible-sounding APIs;
never invent RobustToolbox, SS14, RMC, or CMU APIs when the source can answer. Search the
exact identifier, never a prose description of it. Trace behavior by prototype ID, not
entity name:

`prototype ID → component → entity system`

Prefer CMU code over legacy AU14 code over upstream RMC code; `_CMU14` beats `_RMC14` always.
When creating anything new, find the closest existing equivalent (CMU zone first) and mirror
its structure; adapting real code beats writing from memory. Where these two files are silent
or ambiguous, the nearest existing CMU example is the reference.
SS14 ECS applies throughout: components hold data, systems hold logic, dependencies are
injected (no static state or gratuitous inheritance hierarchies), the server is
authoritative, clients predict, and RobustToolbox itself is never modified. Engine mechanics
are specified in `CONVENTIONS.md` and not repeated here.
The Robust Book (https://docs.spacestation14.com/, source:
https://github.com/space-wizards/docs) fills engine-level gaps. The useful parts:
`robust-toolbox/` for engine internals (`ecs.md`, `coordinate-systems.md`,
`serialization.md`, `netcode/`, `transform/`, `user-interface.md`, `toolshed/`),
`ss14-by-example/` for concrete walkthroughs that pair with our conventions
(`prediction-guide.md`, `basic-networking-and-you.md`, `fluent-and-localization.md`,
`ui-and-you/`), `space-station-14/core-tech/` for inherited content systems (some pages
predate the ECS namespace cleanup, so verify namespaces against the source), and the RSI
specification under `specifications/robust-station-image`. The rest is Wizden design and
process prose. A local clone of the docs repo (recommended) beats web fetching for agent
workflows; keep its path in personal agent configuration, not in tracked files.

## Change discipline

Make the smallest change that solves the actual problem. Add nothing unasked: no speculative
defensive branches, abstractions, configuration, comments, or refactors. Preserve existing
public behavior unless changing it is part of the task. Deleting code is a valid solution.
Edit in place and match surrounding style; reuse an existing helper before writing a new one.
Let errors surface; never swallow one to keep output clean. Simple over clever; write for the
next tired reader.

Duplication is preferable to a bad abstraction; an abstraction should remove complexity, not
relocate it. Wait for evidence of repeated use before extracting shared code; three
meaningful uses is a useful default threshold. Prefer additive API changes; removing or
breaking an established surface requires a deliberate migration or deprecation path unless
the task explicitly calls for the break.

Fix the algorithm or source of truth before micro-optimizing. Measure before optimizing when
performance is material. Keep mechanical migrations (renames, formatting, conformance to
upstream shape) separate from behavioral changes; mixed diffs make the tagging boundary
ambiguous.

## Classic principles

The house doctrine in traditional terms:

- **KISS**: the smallest diff that works; boring over clever, written for the next tired
  reader.
- **YAGNI**: no speculative flexibility; add nothing unasked.
- **Single responsibility**: small types with one reason to change; systems own their
  components' logic.
- **DRY**: fixed at the root (default, parent, constant), never materialized at every
  site.
- **Rule of three**: wait for the third real use before extracting; duplication beats a
  bad abstraction.
- **Composition over inheritance**: here that means components and systems; inheritance
  only via the engine's `[Virtual]` extension points.
- **Tell, don't ask**: logic lives in its system; call a public system method instead of
  reaching through component chains.
- **Open/closed**: additive API changes; a removal needs a deprecation path.
- **Hyrum's law**: every observable behavior gains a dependent; depend on upstream
  contracts, never incidentals.
- **Illegal states unrepresentable**: validate once at a boundary, then trust the type.
- **Errors are data**: expected failure returns `Try*`; exceptions signal bugs.
- **Fail loud**: let errors surface; never swallow one to keep output clean.
- **No defensive programming**: no speculative guards for impossible states; validate at
  real boundaries, let the rest crash.
- **Root cause, not symptom**: patching the reported path leaves every sibling caller
  broken.
- **Chesterton's fence**: know why code is unusual before changing it; it guards a
  constraint you haven't met yet.
- **Measure first**: fix the algorithm before the constant; profile before optimizing.
- **No Boy Scout rule**: never restyle untouched code to conform; churn is merge cost.

When two of these conflict, the priority stack decides. Where a principle is not named,
the nearest existing CMU example decides.

## DRY and source of truth

A value or behavior repeated across many files has one correct source. Fix it at the root:

`default → prototype parent → shared constant → shared helper`

rather than materializing the same correction at every call site. When omitted YAML fields
inherit an incorrect default, fix the default. When behavior belongs in an existing system or
component, put it there instead of teaching every caller to implement it.

A failing test is not a reason to weaken the test; change the expectation only with evidence
that it is stale.

## CMU zones

CMU-owned code lives in `_CMU14`, or `Content.CMU/` on Rebase. Legacy zones are `_AU14`/`AU14`;
upstream zones are `_RMC14`/`RMC14`. Never create new files in the inherited zones; modifying
existing files there is acceptable when kept limited and tagged. New files and new prototypes
always go in the CMU zones. On other branches, follow the branch's existing `_CMU14` layout
rather than inventing a parallel structure. New CMU identifiers use the `CMU`/`cmu-` prefix
(see `CONVENTIONS.md`).

## CMU14 tagging

Every deliberate CMU change outside a CMU-owned zone is visibly marked with a CMU14 tag;
never commit untagged divergence.

- One tag per contiguous block; additions separated by unchanged lines get their own tag.
- Mechanical migrations to current upstream shape are conformance, not divergence: no tag.
- Deliberate removals are commented out with the tag, not deleted, so a merge re-applying
  them is flagged instead of silently resurrected. Removals inside a construct that is
  itself fully tagged or rewritten are exempt.
- Tags must survive merge conflicts: never silently take the upstream side over a tagged
  block.

Exact marker syntax and placement live in `CONVENTIONS.md`.

## Upstream inheritance

Prefer extension over modification. Where an inherited type can be extended with a CMU
partial class in the CMU zone (original namespace, `CheckNamespace` suppression), prefer that
over adding CMU fields to the upstream file. Where behavior can be changed through events,
systems, or other existing extension points, prefer that over editing the upstream
implementation; where behavior must hook inside an upstream method, a `[Virtual]` override
in the CMU zone beats copying the method body (see `CONVENTIONS.md`). Do not create upstream-zone files for namespace convenience, and keep each
unavoidable divergence as small and mergeable as possible. The objective is not zero upstream
edits; it is divergence that stays explicit, localized, and cheap to reconcile.

## Comments

Comments record decisions a future contributor must preserve: why the code is unusual, what
invariant must hold, what external constraint forced the design, what breaks if it is
simplified. Do not restate what the code already says, and leave no session noise (debugging
transcripts, attempted approaches, discovery history, dates). Keep comments short and human;
elaborate prose explaining straightforward code reads as generated output. One or two lines
per decision: a multi-line narrative above an assignment or branch is session noise wearing
a comment badge - what failed, what was tried, and the debugging journey belong in the PR
description, not the code; a comment longer than the code it sits on is wrong. For formulas,
non-obvious algorithms, workarounds, and engine constraints, explain the reason and the
invariant, not a paraphrase of the implementation.

## AI-assisted development

AI assistance is permitted, but generated output is not automatically correct. Inspect the
relevant code, follow these conventions, and do not submit wholesale changes built on
plausible-looking generated APIs or patterns. The contributor responsible for a change must
understand it and be able to explain its behavior; AI-generated code is untrusted input like
any other. Changes intended to travel upstream through RMC or toward Wizden must also satisfy
the receiving project's contribution and AI-use policies (which largely prohibit AI
contributions - upstream-bound work must be human-authored and human-owned); CMU policy does
not override upstream policy. CMU is deliberately the exception: AI assistance is allowed
here, engine-adjacent performance work excepted.

## Evidence and verification

Separate observations from assumptions. Reproduce or inspect a failure before theorizing
about its cause, and do not stop at the first plausible explanation when multiple state
transitions, callers, or failure modes are possible. When verification is authorized, verify
the result rather than asserting correctness. Never claim a build, test, benchmark, or
runtime path was verified when it was not, and state skipped checks and unverified paths
honestly. Test behavior at boundaries, not implementation detail; a test that mirrors the code
catches nothing.

Verification is a ladder, and claims name their highest rung: `compiles` → `linter` → `unit`
→ `integration` → `in-game single client` → `in-game multiplayer/prediction`. A change is
described by the highest rung actually run, with the exact command; "tested" alone is not a
claim.

## Escalation

Stop and ask before proceeding when:

- requirements conflict;
- a gameplay or balance decision has no established precedent to follow;
- two materially different architectures are equally reasonable;
- repository evidence contradicts the requested approach;
- an important API or invariant cannot be established from the source;
- the change unexpectedly alters public behavior;
- a migration would choose between incompatible data or behavior without sufficient evidence.

Do not guess through a high-impact ambiguity.

A blocked change stops with three things stated: what was attempted, the exact error or
missing API, and the leading hypothesis. No partial commits, and no workarounds silently
papering over the blocker.

## Before submitting

Re-verify before opening a PR:

- every changed hunk outside CMU zones carries a CMU14 tag;
- new files and prototypes are in CMU zones with `CMU`/`cmu-` identifiers;
- every new player-facing string has a Fluent entry;
- every API and YAML key referenced exists in source, not in memory;
- LF endings, no BOMs;
- one PR per concern: features, bug fixes, and refactors never mix, mapping changes get one PR
  per map, and file moves sit in their own commit;
- the PR description states what the change does, why (or the balance rationale), the
  verification claim at its highest rung actually run, and what was not verified;
- Changelog files are untouched - the bot generates them from the PR description, so the
  PR title and description are the changelog material;
- the full diff was reviewed for unintended hunks, stray whitespace, and line-ending changes.

## Priority

When constraints conflict:

**Correctness > Verified Performance > Maintainability > Consistency > Simplicity > Locality > Conciseness**

Optimize for truth and useful outcomes over agreement.
