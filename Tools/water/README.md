# Water immersion port

Source: [Kugamo's cmss13#12918, Water Overlay Effects - Redux](https://github.com/cmss13-devs/cmss13/pull/12918),
revision `185b5417a29541d5c314409b9125a7137ec22e05` (the PR was open when ported).

The content implementation uses `RMCWaterComponent.Depth` in pixels and
`ContentTileDefinition.RMCWaterDepth` for shoreline tiles. Existing shallow water
uses 8 pixels, deep water 18, desert slopes 12, and shoreline edges 2 or 4.
These are visual depths; existing movement penalties and damage remain separate.

The client lowers the sprite and applies a keyed post shader to its composed
silhouette, sampling the water entity's current animation and color. This replaces
the previous `FloorOccluder` effect on RMC water. Clothing and held items follow the
same mask. Wakes are independent client entities so camouflage does not hide them.
Prone humans have matching wake sprites, deep prone bodies and facehuggers are
immersed, and living immersed mobs emit bubbles. Prone xenos omit the standing
overlay in shallower water, as in the source PR. Catwalks, buckling, carrying and
leaping suppress immersion. Running uses depth-specific water sounds; walking is
silent. Throws and xeno leaps create impact splashes.

The BYOND turf-layer rewrite and generated icon subsystem are replaced by content
rendering and per-entity state. No engine files or map files are required. Vehicles
and other objects do not receive the mob immersion effect, matching the source's
scope; thrown objects still create a landing splash. Drowning is not introduced.

`import_effects.py` reproducibly imports the pinned DMI art and OGG sounds. Run it
from any directory using Python with Pillow and soundfile. It preserves movement variants,
direction order and frame timings, and writes license attribution beside assets.
Audio is mixed to mono for positional playback; stereo sources trigger the engine's
positional-audio assertion in development clients.

Regression fixture: `Content.IntegrationTests._RMC14.WaterSubmersionTest`.

The `CMUWaterTest` map includes five depth lanes (2/4/8/12/18), marines and
xenos of all wake sizes, a Yautja with cloak equipment, production shoreline
variants, red/dark red/river/translucent water, catwalk/grate/cover comparisons,
chairs and a roller bed, living/dead submerged bodies, resting specimens, and
throw/pounce targets. Examine the yellow signs or read the paper at spawn.
Local admins can right-click specimens and use **Debug > Control mob**.

Run from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/water/start-test.ps1
```

The launcher builds the content, starts a separate localhost-only server on port
1221, and connects one client directly to a playable body in Sandbox. Add
`-SkipBuild` when the current sources are already built. It leaves other servers
alone; repeated launches reuse the water-test processes. Logs and disposable
server data are under `bin/water-test/`. The normal development launcher is
unchanged. `server.toml` selects this map only for this launcher.

The saved map is `Content.CMU/Resources/Maps/CMU14/Test/water.yml`; rebuild it
with `python Tools/water/build_test_map.py` (requires PyYAML).
`WaterTestMapSystem` only acts on explicitly marked test maps/specimens: it
bypasses saved job preferences, initializes poses, disables parasite AI and
keeps test bodies alive with godmode.
