# Console and factory physical-art drafts

This bounded batch authors 21 draft assemblies for 16 previously missing Stable
Garrison Redux prototype IDs. The historical inventory counts 24 Redux and seven
classic occurrences. Those counts are not native admission, current scene coverage,
state completeness, mounting approval, or a fidelity score.

All source files were retrieved from TheHellFireo/CMU-Garrison-3D at
`Chip/garrison-3d`. Source PNGs used the GitHub connector's base64 file response.
The source manifest records every exact repository path, URL and Git blob SHA.
The independent integrity check verifies all fetched bytes against those SHAs.
Source definitions, PNGs and metadata remain unchanged.

## Deliverables

- Canonical models: `Content.CMU/Resources/ThreeD/Prototypes/World/garrison_console_factory_cloud.yml`
- Canonical surfaces: `Content.CMU/Resources/ThreeD/Prototypes/World/garrison_console_factory_cloud_art.yml`
- Original-detail crops: `Content.CMU/Resources/Textures/CMU14/ThreeD/console_factory_cloud/`
- 21 GLBs in this directory, emitted directly by the unchanged `build_models.glb_bytes`
- Source-facing, front-orbit, rear-orbit and source/model comparison PNGs under
  `Tools/three_d/generated/review/console-factory-cloud/`
- Source and original-layer evidence: `Tools/three_d/generated/console-factory-cloud-sources.json`
- Composition/crop and limitation evidence: `Tools/three_d/generated/console-factory-cloud-verification.json`
- Independent byte/geometry proof: `Tools/three_d/generated/console-factory-cloud-integrity.json`
- Blender portable import proof: `Tools/three_d/generated/console-factory-cloud-blender-import.json`

The batch has 668 physical parts, 83 unique cropped-art surfaces in atlas slots
3211–3293, and 97 byte-exact crop references. No crop is an entire sprite card.
The front artwork is divided among real bezels, panels, modules, insets and rails;
the models also contain solid cabinet construction, distinct outfeeds and open
frame bays. Their hidden depth, underside construction, metal properties and
proportions remain authored inferences.

## Actual source contracts and deliberate limits

### Four-screen monitor

`RMCMultiMonitorBig` inherits `RMCMachineScreenBasePowered` and
`RMCMachineScreenBase` from `machine_screens.yml`. Its source is
`hybrisa_computer_props.rsi`. Four assemblies preserve the South, North, East and
West RSI slots and the distinct casing colors. The static visual study composes
`multimonitorbig_off` and frame zero of `multimonitorbig_on`; the latter has two
0.1-second frames per direction. Four separate CRT recesses, perimeter rails,
central dividers and rear mounting battens are physical parts. The original
WallMount and powered display owner are not replaced. Wall mounting and the
animated layered on/off composition are unverified.

### Almayer computers

`RMCTelecomMissionPlanningSystem`, `RMCTelecomSensorComputer` and
`RMCTelecomSensorComputer2` inherit `RMCTelecomBusMainframe` in
`telecommunications.yml`. All three use `almayer_props.rsi` and one direction.

- MPS: `mps_off` + `mps`; narrow taupe cabinet, recessed small CRT, media slot,
  plain upper panel and lower grille
- Keyboard sensor: `sensor_comp1_off` + frame zero of `sensor_comp1`; left button
  and numeric modules, right CRT, broad keyboard, service cabinet
- Wide sensor: `sensor_comp2_off` + frame zero of `sensor_comp2`; wide display,
  lower grille and two raised levers

`sensor_comp1` has three 0.3-second source frames. `sensor_comp2` has five source
frames with delays 1.0, 0.4, 0.3, 0.3 and 0.3 seconds. The original layered power
and text animations are inventoried but not bound to the physical draft.

### Medical lathes

`RMCMedilathe` and `RMCMedilatheLeft` inherit `BaseMachinePowered`; source defaults
are in `_RMC14/Entities/Structures/Machines/Lathe/medilathe.yml`, with machine and
structure ancestry fetched separately. Each 64x32 source frame contains a tall
left or right main machine and an adjoining lower output assembly. The physical
draft includes a pale recessed viewport, red controls, recessed instrument bay,
cartridge deck, couplers, guard rails and separately modeled output rollers.

The right-output study composes `medilathe` + `medilathe_u`. The left-output study
uses the separately inspected `medilathe_f` + `medilathe_u_f`. Maintenance overlays
`medilathe_t` and `medilathe_t_f` are fetched and excluded from these intact studies.
The source layers have respective X offsets -0.5 and +0.5 tiles. Those offsets
agree with the actual two-tile fixture bounds (-1.5..0.5 and -0.5..1.5), so the
authored X coordinates retain the asymmetric physical pivot. No saved transform
is changed. Screen-offset handling is not generalized to other machines.

Running and unlit-running states have eight 0.1-second frames in both source
orientations. Material/refill or output sequences also exist in the metadata.
None is claimed as playable geometry. Multiple source layers, shaders and layer
offsets do not match the existing single-visible-layer sprite-state adapter.

### Telecom equipment

`RMCTelecomBusMainframe`, `RMCTelecomProcessorUnit` and `RMCTelecomReceiver` use
`AIStuff/telecommunication.rsi` and the original layered power owner.

- Bus: `bus_off` + frame zero of `bus`; two upper shoulders, vent, eight ports,
  three separate rack bays, gold lamps and protruding red jumpers
- Processor: `processor_off` + frame zero of `processor`; unequal-height towers,
  open upper gap, inset cyan readouts, diagnostic face, stacked fans, output rack
  and distinct central red interconnect
- Receiver: `broadcast_receiver_off` + frame zero of `broadcast_receiver`;
  offset rack, antenna support block, solid mast and separate red branches with
  empty space between them

Source power animations retain their metadata: bus is four 0.3-second frames,
processor three 0.1-second frames, receiver two 0.2-second frames. These remain
unbound static studies.

`CMTelecomServer` inherits `BaseMachinePowered` and `ConstructibleMachine`, uses
`telecomms.rsi`, and snapCardinals. Its static study composes `comm_server_off`
with frame zero of the three-frame `comm_server` animation (0.3 seconds each),
excluding the fetched maintenance panel. Separate upper cooling ribs, recessed
service cover and handle, lower control modules, blue display, three cartridge
slots and a gold indicator column provide depth. Power, maintenance, lighting,
encryption contents and actual initialized appearance are unclaimed.

### Open machine frames

`CMMachineFrameUnfinished` and `CMMachineFrame` inherit `UnfinishedMachineFrame`
and `MachineFrame`. Those exact parents were fetched from
`Resources/Prototypes/Entities/Structures/Machines/frame.yml` and provide the
`box_0` and `box_1` states, snapCardinals, construction ownership, and the ready
frame's ItemMapper board overlay.

Both physical drafts contain four depth-separated posts, horizontal structural
members, empty bays and three lower modules. The ready frame has additional red,
blue and gold wire segments traced from exact source-colored pixel runs. Stored
boards, `box_2`, construction transitions and destruction are not represented.

### Dropship fabricator

`RMCDropshipFabricator` inherits `BaseMachinePowered` and uses a single mapped
layer of `dropship_fabricator.rsi`, with an actual +0.5 X layer offset. Its source
GenericVisualizer owns Idle and Fabricating. The 64x32 draft has a large main
housing, three upper tooling covers, recessed green screen, narrow feed slot,
projecting product tray, lower plinth and a separate right-hand roller outfeed.

Only frame zero of `drone_fab_idle` is authored. All four 0.5-second idle frames,
three 0.1-second active frames, and `drone_fab_nopower` remain unbound. The source
layer offset prevents claiming support through the existing single-layer adapter.
Physical X spans are authored around the source wide-art pivot, without changing
saved transforms or runtime components.

### Installed sentry deployment hatch

`RMCDeployerSentry` and `AU14DeployerSentryWeYu` share the identical inherited
Sprite contract. Their visual art uses `sentry_deployer.rsi`, initially visible
`floor_sentry_installed`, and initially hidden `floor_sentry_deployed`. No turret
or weapon functionality is authored or modified.

The four installed source slots are preserved in four reciprocal directional
assemblies. Raised perimeter rails, mounting ears, recessed mechanical hatch,
red latch blocks and small control modules lie in a low horizontal floor unit.
The exact directional top layouts are already baked into each assembly;
`sourceCardinalFacings: [0, 0, 0, 0]` avoids another cardinal turn of that layout.
This is explicit art metadata, not proof of native source-selection behavior.
Deployed geometry, deployment transitions and map-level clearance remain unclaimed.

### SMES

`CMSMESBasic` inherits `CMSMESBase` and `BaseSMES`. The prototype and ancestor
definitions were fetched. The static study uses source `smes`, `smes-oc0`, and
`smes-op1`, with the source-declared initially hidden charge layer omitted. It is
an explicitly chosen source composition, not an assertion that a fully charged
live SMES will retain that initial visual state.

The energy-store housing is actually cylindrical, with a lid/rim, lower ribs,
top terminals, dark top ventilation faces, a yellow left conduit and an
independent front control enclosure. Control artwork is an exact cropped source
composition. Initialization, charge/input/output states, critical animation,
lighting and electrical behavior remain unclaimed. The lid ventilation faces are dark surface markers on a solid lid; no
through-holes or geometric depressions are claimed.

## Verification and reproduction

Run from the repository root:

1. `python Tools/three_d/author_console_factory_cloud.py`
2. `python Tools/three_d/verify_console_factory_cloud.py`
3. `blender --background --factory-startup --python Tools/three_d/check_console_factory_blender.py`

The authoring script is bounded to this family's YAML, textures, models and review
outputs. It uses the existing exporter without modifying it. Verification checks
all 21 direct re-export bytes, GLB headers, buffer views, index ranges, finite
accessor values and bounds, embedded PNGs, draft status, no animation clips,
reciprocal source direction links, exact crop bytes, source Git blob hashes and
canonical atlas/model/source-mapping uniqueness. Blender 4.3.2 imports all 21
portable GLBs successfully without re-export. Khronos glTF validation was not run
because that package is not installed. No game, server, engine, native scene or
saved map was started, edited, admitted or approved by this batch.

## Source artwork attribution

All nine source RSI resources declare CC-BY-SA-3.0. The unchanged original
metadata and complete upstream attribution text are retained with the fetched
resources. Derived source crops and source-derived geometry in this batch retain
that attribution and license. The source-manifest URLs point to the exact CMU
repository branch used; the upstream copyright references follow.

- `hybrisa_computer_props.rsi`: cmss13
  https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/structures/props/hybrisa/computers.dmi
- `almayer_props.rsi`: cmss13
  https://github.com/cmss13-devs/cmss13/blob/4aff6b0a6fbc1b5f31ba7575a70dc39838e1970f/icons/obj/structures/props/almayer_props.dmi
  and https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/structures/props/props.dmi
- `medilathe.rsi`: cmss13, modified by Vermidia
  https://github.com/cmss13-devs/cmss13/blob/6791699de749afc1c62bc65ec6f2326a00b3bb61/icons/obj/structures/machinery/science_machines_64x32.dmi
- `AIStuff/telecommunication.rsi`: cmss13
  https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/server_equipment.dmi
- `telecomms.rsi`: cmss13
  https://github.com/cmss13-devs/cmss13/blob/c67c5213507a19e10e252ef2898d94354c0709bf/icons/obj/structures/props/stationobjs.dmi
- `parts.rsi`: cmss13
  https://github.com/cmss13-devs/cmss13/blob/f7b9367cb5c083a8c052a40dece9417031e28ffe/icons/obj/structures/machinery/stock_parts.dmi
- `dropship_fabricator.rsi`: cmss13
  https://github.com/cmss13-devs/cmss13/blob/71d46ee8057d19b12e1495419cb299d2fedef6cc/icons/obj/structures/machinery/drone_fab.dmi
- `sentry_deployer.rsi`: cmss13
  https://github.com/cmss13-devs/cmss13/blob/9d6826cfca155f055d1616ed287aaebe9196aae9/icons/obj/structures/props/almayer_props.dmi
- `Power/smes.rsi`: cmss13
  https://github.com/cmss13-devs/cmss13/blob/6f8162a66bed3a5aa38587d30ae95242fcf038e7/icons/obj/structures/machinery/power.dmi

License reference: https://creativecommons.org/licenses/by-sa/3.0/


## Cumulative atlas assignment
This cumulative snapshot uses centrally allocated, nonconflicting atlas slots. Any authoring-time numeric range in this historical family note is superseded by the canonical surface YAML and `Tools/three_d/generated/cloud-review/atlas-allocation-current.json`. Source IDs, original PNG pixels and modeled geometry are unchanged by atlas-index remapping.
