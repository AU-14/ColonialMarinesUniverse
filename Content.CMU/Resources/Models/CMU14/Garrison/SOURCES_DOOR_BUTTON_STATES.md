# Door-control states and source-frame geometry

Both existing control models now contain six source states, with powered and power-overlay compositions for every frame. The default parts are the powered idle frame. Individual GLBs include Press and Denied clips. Source width, height, pivot, silhouette and RGBA colors follow the 32-pixel sprites; shallow .06-tile depth and the 1.335-tile sprite pivot elevation are inferred. This remains draft construction, not fidelity approval.

The native administrative preview reads the existing sprite animation and power layers. It does not subscribe to the button event or create a competing timer. Frame updates retain the preview's 0.1-second refresh period. The normal game viewport remains unchanged. Unknown, additional, tinted or transformed visible layers remain unsupported markers.

Export timelines follow RMCDoorVisualsSystem and AnimationTrackSpriteFlick: the source strip restarts at .5 seconds, clamps at its final frame, and returns to idle at 1.25 seconds. KeyFrame times are intervals; the later 1.5- and 2.75-second flicks fall outside the owner animation. doorctrl0 and doorctrl-open are resource poses, without a demonstrated trigger for these saved controls. Region GLBs remain explicit static snapshots using the default source pose; they do not replay these clips.

Mechanical articulation, material emission, remaining wall-mount defects and interactive native runtime review remain unfinished.

## Attribution

CMU3DOrangeDoorButton
CC-BY-SA-3.0
Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/c7b4d6bd868de669ad96f1d3e4dc3702a3404355/icons/obj/structures/props/stationobjs.dmi
Original pixels reconstructed as colored solids; inferred depth and mounting elevation.

CMU3DRedDoorButton
CC-BY-SA-3.0
Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/c7b4d6bd868de669ad96f1d3e4dc3702a3404355/icons/obj/structures/props/stationobjs.dmi
Original pixels reconstructed as colored solids; inferred depth and mounting elevation.

## Placement and verification checkpoint

All 88 configured Redux controls and 23 classic controls use the refined default geometry. Rear clearance adjusts 47 placements without changing saved transforms or facings. Of 63 previous contact pairs, 34 clear and 29 persist; five source-width red-control pairs are introduced, leaving 34 unresolved. The press/denied frame envelope with both power compositions adds no additional pair. Resource-only poses are not part of that contact check. Neighbor spacing and ambiguous wall sides still require review.

All 44 compositions match 45,056 source pixel/occupancy samples. The 169 Python, 28 JavaScript and 215 isolated native checks pass; client build: zero errors, 2,150 warnings. All 778 individual and 540 assembled GLBs plus two study GLBs validate without errors/warnings. Actual map export roots and 6,584 part occurrences verify 111 saved controls. Both comparison cards and browser playback/power/completion were inspected. Interactive native playback and ordinary gameplay conversion are unfinished.

Evidence: `Tools/three_d/generated/button-state-verification.json`, `button-state-animation-envelope-verification.json`, `state-inventory.json` and `button-animation-study/verification.json`. The study clips duplicate the four library sequences and are not additional completed animations.
