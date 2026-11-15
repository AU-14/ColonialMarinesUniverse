<!-- Guidelines: CONTRIBUTING.md at the repository root --> <!-- CMU14 -->

## About the PR
<!-- What did you change? -->

## Why / Balance
<!-- See BALANCE.md. Pick a tier. The bigger it is, the more you write here.
None: one line on why.
Minor: problem and baseline, every value before -> after.
Significant: answer all five.
Major: all five in detail, plus a link to the design issue or Discord thread and a test plan.
Delete what your tier doesn't need. -->

**Tier:** None / Minor / Significant / Major

**Problem:** <!-- What goes wrong in a round? Which mode, which situation? -->

**Baseline:** <!-- What does cmss13/RMC14 do, what does CMU do now? Every changed value before -> after. -->

**Why this fix:** <!-- Why this and not something else, or nothing? -->

**Counterplay:** <!-- Who's on the receiving end and what can they still do about it? -->

**Revert if:** <!-- What would make us revert it? -->

## Technical details
<!-- Summary of code changes for easier review. -->

## Test plan
<!--
Describe how you tested the pull request, and how someone reviewing this PR can test it themselves.
-->

## Media
<!-- Attach media if the PR makes ingame changes (clothing, items, features, etc).
Small fixes/refactors are exempt. Media may be used in progress reports with credit. -->

## Requirements
<!-- Confirm the following by placing an X in the brackets [X]: -->
- [ ] I have read and am following the [contributing guidelines](CONTRIBUTING.md).
- [ ] I have tested this pull request and written instructions on how to test it.
- [ ] My tests assert behavior that can break: no locked-in values or mirrored implementation ([CONVENTIONS.md](CONVENTIONS.md#tests)).
- [ ] Every change outside the CMU zones carries a CMU14 tag; new files and prototypes are in CMU zones.
- [ ] I have added media to this PR or it does not require an ingame showcase.
- [ ] I have included a **brief** detail of the major changes for this PR in the changelog below.
- [ ] By submitting this code and/or assets, I confirm that I either own them or have provided the correct necessary licenses to use and distribute them. I agree to be fully responsible for any legal claims or issues arising from the use of these materials.
<!-- You should understand that not following the above may get your PR closed at maintainer’s discretion -->

## Breaking changes
<!-- List any breaking changes, including namespaces, public class/method/field changes, prototype renames; and provide instructions for fixing them.
This will be posted in #codebase-changes. -->

**Changelog**
<!-- Add a Changelog entry to make players aware of new features or changes that could affect gameplay.
Any of the below listed tags will function, for example admin changes may be listed for admins to see with admin:
Coding changes with no changes visible in-game may be listed for other contributors with code:
Changelog must have a singular :cl: symbol at the top, so the bot recognizes the changes and adds them to the game's changelog. -->
:cl:
- add: Added fun!
- remove: Removed fun!
- tweak: Changed fun!
- fix: Fixed fun!
- map: Mapped fun!
- admin: Admin related change!
- code: Code related change for contributors!
