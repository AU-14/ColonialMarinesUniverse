# RobustToolbox v294.0.1 integration

Implemented October 28, 2026, from content commit
`6043303a5c5a9e379a0346d58f498d63f38f5b24`.

## Engine inputs

- Previous release: v290.0.0, `fee0dd647ebe95ad62d8945440450fb6cd4749a7`.
- Release baseline: v294.0.1, `cdf8d1258e8f9117b6a74fa80132143350250f16`.
- CMU engine commit: `678764a6c844bc179bd69bb7c505da95724cda2f`.
- NetSerializer: `ae4c2543cda63173536aaff6c531f2d2e1e3bf26`.

The engine adds an acceptance result to the existing server prototype upload
API. The existing void entry point remains compatible. Accepted uploads still
use the engine's validation, replay history and late-join transport. Retrying
the most recent accepted batch can broadcast it without adding duplicate
history. Acceptance does not mean every client acknowledged delivery.

This is a custom engine based on the release, not the stock v294.0.1 binary.
Before deploying, publish the engine commit to the CMU engine distribution and
make it fetchable through the repository's submodule configuration. Then supply
`PUBLISH_ENGINE_VERSION` with that distribution's actual version and
`PUBLISH_ENGINE_COMMIT=678764a6c844bc179bd69bb7c505da95724cda2f`.
Neither engine nor content was published as part of this implementation.

## Content changes

1. Mutable stump entries implement the engine clone contract. Client state
   correction no longer shares the live tourniquet clamp/refund objects.
2. Construction, tile, mass and lathe saves check publication acceptance before
   persisting their generated YAML. Related lathe recipes and packs publish in
   one batch. Startup batches survive a send exception and retry up to three
   times, five seconds apart, with errors retained in logs.
3. Aircraft and seats send changed fields at the existing network cadence.
   The engine supports **32 delta fields**, so the aircraft's 46 values use
   three immutable scalar groups and 28 separate fields. Entity references
   retain the generator's network-ID conversion. Camera movement cadence is
   unchanged; action handlers can still request full state.
4. Weapon refresh compares deterministic snapshots. Idle loadouts and targets
   no longer dirty the component every scan; moving/expiring targets and
   server fire validation retain their refresh paths.
5. Client and server packages contain `CMU14/build-provenance.json`, including
   content, engine and serializer commits and tracked-dirty status.
   Administrators can inspect it with `cmubuild`. Publishing rejects a
   nearest-tag guess and requires an exact release or explicit custom identity.
6. Ghost and vehicle rendering use indexed `LayerData` access.
7. Gunship HUD strings use measurement without transparent draw calls.
   Existing text baselines and font scale are preserved.
8. Only the XM99A's `AU14PlasmaShot` opts into fixture-accurate hitscan.
   Unsupported edge/chain geometry retains legacy blocking, and an origin
   inside a solid fixture blocks at distance zero. Other weapons retain their
   existing raycasts. Container and explicit-target rules remain in the shared
   hitscan handler.

The release migration also replaces removed reanchor hooks and anchored
setters, preserves docking across grid transfers, and updates a test event
subscription to the engine's by-ref signature.

## Verification

- Shared, client, server and packaging projects compiled successfully.
- 57 distinct integration cases passed across focused runs: six new cases
  covering medical snapshots, fighter state, prototype publication, hitscan
  geometry and docking; plus 51 existing anchoring, atmosphere, fighter
  ground/laser and docking cases.
- The fighter scenario serialized **168 bytes for full component state versus
  17 bytes for a flight delta** (about 90% smaller). It also verified idle
  aircraft, seats and weapons stayed clean and that applying a delta to an
  earlier full state restored motion while preserving stable fields.
- Three Python tests used real temporary Git repositories to check release
  tags, dirty-engine rejection and explicit custom version/commit matching.
- A temporary packaging harness passed the provenance asset through both actual
  client/server asset graphs and their ZIP writer. JSON and commit formats were
  checked at the client path and the server's prefixed resource path.
- Whitespace checks passed for content and engine changes.

The broad integration command used the repository wrapper:

```powershell
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.IntegrationTests -Filter 'FullyQualifiedName~CMUToolboxMedicalStateTest|FullyQualifiedName~CMUFighterNetworkStateTest|FullyQualifiedName~CMUFixtureHitscanTest|FullyQualifiedName~CMUPrototypePublicationTest|FullyQualifiedName~CMUDockTransferTest|FullyQualifiedName~AnchoredTileCacheTest|FullyQualifiedName~FighterGroundTest|FullyQualifiedName~FighterLaserTest|FullyQualifiedName~AirtightTest|FullyQualifiedName~DockTest.TestDockingConfig'
python -m unittest discover -s Content.CMU/Tools -p test_cmu_engine_version.py -v
```

The broad run passed 56 cases. A hitscan fixture setup failure was corrected
and both hitscan cases then passed; the strengthened publication case also
passed separately. No skipped case is counted as a pass.

These are serialized component measurements, not total network traffic, FPS,
or live-server CPU benchmarks. Visual gameplay inspection, a full distributable
package, broadcast-failure injection and deployment remain outside the checks
performed here. The medical regression exercises client snapshot correction,
not a complete multiplayer tourniquet consumption/refund interaction.
