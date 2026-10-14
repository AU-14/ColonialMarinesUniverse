# Exterior shutter assemblies

This follow-up corrects twelve saved window-shutter mounts: six co-centered
`RMCDoubleDoorGlassHybrisa` doors on Redux surface and six
`CMAirlockGlassHybrisa` airlocks on Redux -2. Both exact source prototypes are
added to `windowMountTargets` on the two existing window-shutter models.
No model parts, original texels, source transforms, entity identities, physics,
door-state ownership or animation timings change.

The shutter rear now sits .02 tile in front of the supporting door's complete
authored envelope. This envelope includes the closed and open assemblies and
any authored door frames. It remains fixed as either object changes state.
The double-door mount is .2765 tile from the saved pivot; the glass-airlock
mount is .2665 tile. These distances follow their existing modeled surfaces,
not a new offset inferred from the flat sprite.

The additional face contract is restricted to the two window-shutter model
IDs and the two exact door prototypes. It requires an anchored co-located
support, a matching opening direction and compatible alternate-pose facing
metadata. Off-pivot, perpendicular and unsupported cases do not acquire a
guessed exterior mount. Ordinary glazing mounts and inside curtains keep
their existing behavior.

## Evidence

`shutter-exterior-mounting-source-pipeline.json` recomputes all 1,488 shutter
records from the actual five saved maps. Exactly twelve change presentation
offset and mounting metadata; the other 1,476 records remain identical.
All 1,321 prior mounts are retained, giving 1,333 mounts. The source model
file remains byte-identical to the captured 867 checkpoint after removing
the four new target-list lines.

The candidate geometry audit checks all fourteen shutter poses and both
authored door poses at each of the twelve door placements. It measures the
.02 gap and finds no new contact with current modeled same-level neighbors
within four tiles. This does not sweep independently animated neighboring
fixtures, certify unmodeled neighbors or validate native renderer admission.
Seven focused Python mounting regressions pass. Parent-owned client/native
verification and final scene/GLB export checks are recorded separately.

## Prison observation windows: rejected exterior offset and later compound fit

Ten saved shutters share a tile with `RMCWindowPrisonCell`. The existing
window draft is a full-depth wall module; moving a shutter outside its
.525-tile facade requires a .6075-tile translation. Although that clears
the window itself, eight placements then encounter chairs, wall trim,
lights or classic-map requisition tables and their containers. Fourteen
distinct neighbor pairs are introduced across the tested poses.

That exterior-offset candidate is not activated in production. The original
square-window fallback remains unchanged. The source description identifies
a rod matrix inside a wall frame, and the inherited fixture fills one tile;
neither recovers the exact physical facade depth. Nearby furniture is saved
on the adjacent tile and its draft bounds remain inside that tile, so there
is no evidence supporting moving those source entities out of the way.

An offline compound-assembly candidate reserves the shutter depth inside
the wall-frame footprint by recessing the facing window facade. It preserves
the opposite side, inner glazing depth, complete facade profiles and shutter
texels. All ten assemblies clear their backing through fourteen shutter
poses, with no new shutter or window neighbor pairs in the captured context.
The fourteen previously introduced neighbor pairs have positive part-bound
clearance of at least .033526 tile; the table clearance is .05, lights .175,
chairs .21 and coffee containers at least .19056. Twenty front/back profile
renders preserve all 2,240,000 comparison pixels exactly, alongside unchanged
profile coordinates, materials and inner glazing parts.
These comparisons are between the original and candidate 3D draft facades,
not a claim that the original sprite fully specifies the 3D reconstruction.
Eight pre-existing contacts with neighboring reinforced-prison-wall trim
remain in the candidate audit; the correction does not certify those seams.

The candidate reserves an outer assembly face of .45 tile: half a tile,
minus the adjacent .04 wall-trim projection and .01 clearance. Subtracting
the .125 shutter depth and .02 backing gap places the window facade at
.305; the shutter center is then .3875. Only the facing depth beyond the
preserved .045 glazing is monotonically remapped. This depth is inferred
from the compound assembly and context, not recovered from a flat picture.

The original candidate checkpoint remains historical. The later bounded
implementation and 872-baseline source checks are documented in
`SOURCES_PRISON_SHUTTER_COMPOUND.md` and
`prison-shutter-compound-verification.json`. Native/offline context hooks
now apply its assembly fit; final native capture and scene/GLB validation
are recorded separately. The later exact joining-face correction for the
eight old wall-trim pairs is measured in
`prison-shutter-joined-verification.json`; the compound source note explains
its source connection evidence and retained full-width opening.
Historical candidate records stay in `prison-shutter-recess-candidate.json`;
the failed unrecessed case remains in
`shutter-exterior-mounting-verification.json`.
The review sheet includes the failed exterior offset between the current
buried shutter and the candidate. Its open-side camera shows the actual
chair, wall light and adjoining wall without omitting any implicated object.

## Source and attribution

Original shutter art is
`Resources/Textures/_RMC14/Structures/Doors/Shutters/Hybrisa/window_shutter.rsi`.
The two supporting door RSIs are
`_RMC14/Structures/Doors/Airlocks/Double/hybrisa_glass.rsi` and
`_RMC14/Structures/Doors/Airlocks/hybrisa_door_glass.rsi`; authoritative resource
paths are also retained in the exported model references.
The prison-window source is
`_RMC14/Structures/Windows/prison_cellwindow.rsi`, with entity definitions in
`Resources/Prototypes/_RMC14/Entities/Structures/Windows/prison_windows.yml`.

These CM-SS13-derived resources retain their original RSI attribution and
CC-BY-SA-3.0 licensing. No artwork is repainted or resampled by this mounting
change. The shutter-source and consolidation notes retain the complete
source URL and exact-pixel preservation evidence. Physical reconstruction
and context remain drafts; the game and server stayed closed throughout.
