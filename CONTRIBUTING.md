# Contributing to CMU

Thanks for contributing to **CMU** (Colonial Marines Universe), a fork of [RMC14](https://github.com/RMC-14/RMC-14).
Itself a parity-based rendition of [CM-SS13](https://cm-ss13.com/) in [Space Station 14](https://github.com/space-wizards/space-station-14).

Pick something our users reported in the #code-bug-report channel on Discord, the repository issues tab (less maintained),
or join the [CMU Discord](https://discord.gg/colonialmarines) to discuss ideas and #feedback before diving in,
we have a very active community interested in helping you fill in gaps which you may have like
sprites, maps, code/yaml or otherwise ideas crafting and brainstorming, we don't bite!
If you'd like to create a discussion on your contribution we invite you to post a thread in #contributor-projects.

## Read these first

Two files in this repository define how we work, and they apply to every contribution:

- **[CONVENTIONS.md](CONVENTIONS.md)** - the technical rules: C# and YAML style, components
  and systems, events, networking, prediction, prototypes, localization, performance.
- **[AGENTS-CMU.md](AGENTS-CMU.md)** - the policy and (AI) workflow: change discipline, tagging
  policy, verification standards, and the pre-submission checklist.
- **[Robust Book](https://docs.spacestation14.com/)** - engine-level questions. Useful
  sections: `robust-toolbox/`, `general-development/`, `ss14-by-example/`,
  `space-station-14/core-tech/` (verify namespaces against source; some pages predate the
  ECS cleanup).

They are also what automated coding agents in this repo follow. Using one? Add this line to
your personal `AGENTS.md`/`CLAUDE.md` and the whole convention set bootstraps:

```text
Before coding in this repository, read AGENTS-CMU.md and CONVENTIONS.md at the repo root
and follow them.
This file overrides both; below it, CONVENTIONS.md wins on technical rules and
AGENTS-CMU.md covers policy and workflow.
```

The second line matters: your personal file outranks the house rules, so bootstrapping
never costs you your own setup. (Claude Code users: `@AGENTS-CMU.md` alone suffices - it
imports, the header chain pulls in the rest, and personal config wins there too.)

The last major guidelines update was on **September 7th, 2026**.

### Why is this here?
GitHub remembers when you last read `CONTRIBUTING.md` and warns you on the pull-request
form if it has changed since then. Bump this date whenever the guidelines meaningfully
change, so contributors get nudged to re-read them.

## How the fork is layered

CMU inherits code from two upstream layers, and the tree marks which layer owns what:

| Zone | Owner |
| ---- | ----- |
| `_CMU14` (or `Content.CMU/` on the Rebase branch) | CMU: new code and resources go here |
| `_AU14` / `AU14` | Legacy CMU zone: do not extend, deprecated |
| `_RMC14` / `RMC14` and everything else | Upstream (RMC14 and Space Station 14) |

- **New files and new prototypes always go in the CMU zone.** Never create files in the
  upstream or legacy zones; modifying an existing upstream file in place is acceptable when
  the change stays limited.
- Prefer extension over modification (partial class > upstream fields, events/systems >
  upstream edits, `[Virtual]` override > copied bodies); see
  [AGENTS-CMU.md](AGENTS-CMU.md).

## The CMU14 tag

Every deliberate CMU change **outside** the CMU zones is marked with a `CMU14` tag so merges
against upstream surface our divergence instead of silently erasing it. If you change an
upstream or legacy file without a tag, the **PR will be sent back**!

Tweaks tag the changed line inline; new constructs get a kind marker on top:

```csharp
falloff = radius / strength; // CMU14: guard divide-by-zero on zero strength
```
```csharp
// CMU14 method
public void ApplyBurstDistortion(EntityUid uid, float radius)
```

- One tag per contiguous block of changed lines; separated additions get their own tag.
- **Removing code is a no-go**: deliberate removals are commented out with the tag, never
  deleted, so a downmerge re-applying them is flagged instead of silently resurrected.

Syntax and placement: [CONVENTIONS.md](CONVENTIONS.md); policy: [AGENTS-CMU.md](AGENTS-CMU.md).

## Naming

New CMU prototype IDs are PascalCase with a `CMU` prefix (`CMUColonyBudget`); new
localization IDs are kebab-case with a `cmu-` prefix (`cmu-colony-budget-insufficient`).
Do not retrofit prefixes onto inherited Wizden or RMC identifiers.

## House style essentials

The short list that catches most first PRs. Full detail in [CONVENTIONS.md](CONVENTIONS.md):

- **C#**: expression-bodied members with `=>` on their own line; boolean chains wrapped with
  leading `&&`/`||`; no braces around single-statement `if`/`else`; `sealed` by default;
  data definitions `partial` with a parameterless constructor; bare `[DataField]` unless the
  YAML key differs from the field name.
- **YAML**: list items at the key's indent (`components:` and `- type:` flush); prototype key
  order `type > abstract > parent > id > categories > name > suffix > description >
  components`; one blank
  line between prototypes, none inside a `components:` list.
- **Localization**: every player-facing string goes through Fluent under
  `Resources/Locale/en-US/`. Downstreams translate everything, and hardcoded text
  causes issues. Reuse an existing file when entries fit; a line may not start with `[`.
- **LF line endings, no BOMs.**
- **Tests**: only for behavior that can break: regressions, edge conditions, invariants.
  Never lock balance values or mirror the implementation; see [CONVENTIONS.md](CONVENTIONS.md).
- **Changelogs**: a bot generates them from the `:cl:` block in your PR description. Never
  edit `Resources/Changelog/` yourself; write the PR entry as player-facing (non-technical) text.

## Building and testing locally

- Build: `dotnet build` at the repository root.
- Run the server: `dotnet run --project Content.Server`, the client:
  `dotnet run --project Content.Client` (Windows wrappers: `runserver.bat`, `runclient.bat`).
- Build configurations: `Debug` has asserts on and halts on the first one, use it for
  breakpoints and catching failures at the source. `Release` runs asserts off and keeps
  going, so most day-to-day playtesting happens there. `DebugOpt` keeps asserts while
  optimizing. Everything except `Release` defines the `TOOLS` symbol, so dev
  tools and `development.toml` config preset are active; `Tools` is
  `Release` plus those. `Debug`/`DebugOpt` also try to load a `debug.toml` preset (missing
  one is skipped). Select with `-c`, e.g. `dotnet run --project Content.Server -c Release`.
- Before working on maps, configure the map merge driver once - without it git silently falls
  back to line-based merging of map YAML:

  ```
  git config merge.mapping-merge-driver.name "SS14 map YAML merge driver"
  git config merge.mapping-merge-driver.driver "dotnet run --project ./Content.Tools -- %A %O %B %P"
  ```

- Validate YAML against the component definitions instead of trusting remembered field names:
  `dotnet run --project Content.YAMLLinter`.

## Pull requests

- **One PR per concern.** Features, bug fixes, and refactors never mix; mapping changes get
  one PR per map; file moves sit in their own commit.
- Fill in the PR template: what the change does, why (or the balance rationale), the
  technical summary, a test plan, and media for anything visible in game.
- State honestly how the PR was tested and what was not verified.

Before submitting, re-check the diff:

- every changed hunk outside the CMU zones carries a CMU14 tag, removals commented out;
- new files and prototypes are in CMU zones with `CMU`/`cmu-` identifiers;
- any tests assert behavior that can break, never locked-in values or mirrored
  implementation - these get the PR sent back;
- every API and YAML key referenced exists in the source, not in memory;
- the full diff was reviewed: no stray whitespace, unintended hunks, or line-ending changes.

Balance and gameplay decisions without an established precedent should be discussed on
Discord or in an issue first. Don't guess through a high-impact ambiguity.

## Reviews

Findings are severity-ordered: bugs and regressions first, then unnecessary complexity and
missing edge cases, then the rest. Stylistic preference is not a finding unless it is
material to correctness, performance, or maintainability - `.editorconfig` and
[CONVENTIONS.md](CONVENTIONS.md) already decide most of it. Point at the rule instead of
rewriting the author's code; the author fixes their PR.

Review effort goes where behavior lives: system code gets read, bulk prototype YAML gets
spot-checks (the linter covers its shape), tests get the anti-pattern scan, not a full read.

A revert is routine maintenance, not a verdict. A change that breaks the game goes back
out fast and returns fixed; a revert says "not this version", not "not this contributor".
Iterate and PR again, don't be discouraged.

## AI-assisted contributions

AI assistance is welcome, but you own what you submit: understand the change, be able to
explain its behavior, and follow the policy in [AGENTS-CMU.md](AGENTS-CMU.md). Changes
bound for RMC14 or Wizden must satisfy their policies, which largely prohibit AI
contributions - work headed upstream has to be human-authored and human-owned. CMU is
deliberately the exception here: we allow AI assistance and take the good with the bad.

## License

By submitting code you confirm you own it or may license it to us; see the
[README](README.md) for the license breakdown (MIT for code predating May 2026, AGPL-3.0 for
code after, CC-BY-SA-3.0 for most assets).
