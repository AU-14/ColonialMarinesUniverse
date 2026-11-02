# RussianCM industry import

For the basics, use the [illustrated player quickstart](quickstart.md).

Read [gameplay and design](gameplay-design.md) for the player-facing explanation
and [equipment and translated research](upstream-equipment-and-research.md) for
the equipment catalog and source links.

## Source and scope

Source repository: [flex5hybrid/RussianCM](https://github.com/flex5hybrid/RussianCM).
The imported file contents are pinned to commit
`0e24ae881d565777cc2ef47f6c38d9ff1df8d4d6`, rather than the current moving branch.
The CMU baseline is `8fb71c688b6dd1b159a9c2717e2ca2407cbd1b70`.

The file set comes from these merged changes and their final state at the
pinned source revision:

| Change | Scope | Merge commit |
|---|---|---|
| [522](https://github.com/flex5hybrid/RussianCM/pull/522) | Materials, mining, production and related equipment | `3fa7014c2525f6402bae38147bfe3a417aae3938` |
| [534](https://github.com/flex5hybrid/RussianCM/pull/534) | Industry fixes | `dcb95590ff1dc82780ca3a8f30d635e7db517962` |
| [545](https://github.com/flex5hybrid/RussianCM/pull/545) | Automation, pits, crafting changes and removal of superseded equipment | `5127563530435b89829e82bfd4f29e6020862fd8` |
| [561](https://github.com/flex5hybrid/RussianCM/pull/561) | Ambassador world map | `c8f5cb1d65b3898cf928b884f14e50a5ae7e716f` |
| [566](https://github.com/flex5hybrid/RussianCM/pull/566) | Removal of the superseded colony console | `abbdc2daa9c6ae3e0cb46e46d2770cf43f4c3f1e` |
| [569](https://github.com/flex5hybrid/RussianCM/pull/569) | Government and shuttle trade | `fd1b7976e064214b5a8447fc81a4f1ea6fec39b2` |
| [570](https://github.com/flex5hybrid/RussianCM/pull/570) | Console anchoring | `5c7cfb27a123855c6c5454cc69ac29b7a55f7d73` |
| [571](https://github.com/flex5hybrid/RussianCM/pull/571) | Pit/ladder and world-map follow-ups | `7084d1c1429c4fb479bea753642066bcf27ce550` |

The final source state also carries later fixes within those files, including
the saved-build graph traversal guard. Five machine-part compatibility tags
were brought across from source commit
`90640ef0ac3d3e2dba1fd957b939e695ca13ab0b` after prototype validation identified
the dependency. PR 544's unmerged crafting-ID proposal is not included.

This includes mining, pumps, fluids, steam, forging, electric production,
oil/chemistry, agriculture/cooking, crafting, automation, equipment, pits,
government, world markets and shuttle trade. It is a scoped import of the
discussed systems; unrelated changes elsewhere in the RussianCM fork are not
part of this baseline.

## Integration choices

- `Content.RUMC/` files live under `Content.CMU/` and use CMU's existing project
  and resource imports. Established upstream prototype IDs and namespaces are
  retained to keep recipes and assets connected.
- New resource files from the root resource tree live under
  `Content.CMU/Resources/`, preserving their virtual resource paths.
- The pit mine-detector component lives under
  `Content.CMU/Shared/Industry/MineDetector/`, retaining the namespace expected
  by the detector hook.
- Existing CMU files were reconciled with three-way merges using each feature
  file's pre-change source version. Conflicts in colony-economy localization,
  LACN belts and the administration console preserve current CMU additions and
  add the imported behavior.
- Changes to inherited construction, power, weather, subfloor hiding, tools,
  trees and mine detection use CMU14 markers. External engine and submodule
  revisions are unchanged.
- The source references a steel grinder RSI that is absent from the pinned
  repository. The industrial steam grinder inherits the existing grinder art
  so it can render. Its industrial behavior is retained; custom steel artwork
  remains a visual follow-up.
- Existing copyright headers, attribution and RSI license metadata are
  preserved. Farming retains its crystall-punk-14 port/adaptation attribution.

The imported balance is intentionally available as a baseline for rework.
The gameplay guide records recipe, economy and authority questions separately
from these compatibility changes.

## Verification

The isolated checkout uses `.codex/scripts/dev.slnf` to build the content server
and client, with the repository's build lock and isolated package/output paths.
The initial build passed. Focused tests run through the repository wrapper:

```powershell
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.IntegrationTests -Filter 'FullyQualifiedName~StructuralLifecycleTest|FullyQualifiedName~ToolsConstructionMergeRegressionTest'
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.IntegrationTests -Filter 'FullyQualifiedName~RecipeMachineLifecycleTest|FullyQualifiedName~TestAllServerPrototypesAreSerializable|FullyQualifiedName~TestAllClientPrototypesAreSerializable'
```

The construction and underground group passed all 20 cases. Prototype
validation initially identified the missing machine-part tags described above.
After that correction, both server/client prototype checks and the machine
lifecycle regression passed (3/3), for 23 passing cases across the two groups.
The new machine regression exercises a real electrolyzer through power loss,
blocked liquid output, resumption and a subsequent update to detect duplicate
production. It supplies the receiver's power state explicitly; it is not an
end-to-end electrical-network test.

Static checks passed for 188 imported RSI metadata files, referenced RSI paths,
state PNG presence, documentation links, encoding and staged diff whitespace.
Literal audio, map and texture resource references resolve. These checks do
not replace visual inspection of every sprite or a multiplayer playtest.

Full-round progression, factory throughput under multiplayer load, shuttle
cargo handling and the complete government/ambassador UI workflows still need
playtesting. No balance claim or map-readiness claim follows from the build and
regression checks. Mapping changes should be reviewed separately per map.
