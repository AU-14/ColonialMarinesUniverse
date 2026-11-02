# Deployed door-gun sprites

Only deployed door-gun artwork lives here. In-hand, back-slot, camouflage,
and attachment-box artwork uses the original shared assets.

Each of `M56D`, `M2C`, and `UTESC` contains:

- `pivot.rsi/mount.png`: stationary four-direction tripod.
- `gun.rsi/gun.png`: rotating gun with separate south, north, east, and west frames.
- `gun.rsi/ammo.png` and `heat.png`: overlays that rotate with the gun.

Edit these PNGs to change the deployed door guns without changing ordinary
ground weapons. Keep each RSI's state names, frame layout, dimensions, and
license metadata intact.

All sheets use RSI order: south, north, east, west, arranged in a two-by-two
grid. The gun keeps the directional frame of the fixed door-facing mount;
cursor movement rotates that frame without selecting another one. Camera
rotation is included when choosing the mount's displayed direction.

The gun layers are split from the existing deployed weapon sheets without
resampling or redrawing. The M56D pivot retains its extracted support pixels.
The M2C and UTES-C pivots instead use separate copies of the original bare
M56D tripod, ready to edit independently. Their metadata uses that tripod's
64-by-32 frame size, even though the UTES-C gun frames are 32-by-32.

`Content.CMU/Tools/Sprites/split_door_guns.py --overwrite` regenerates these
layers from their original assets. It replaces manual edits, so only run it
when intentionally regenerating the artwork. It requires Pillow.

Original asset attribution is preserved in every `meta.json`.

Before the directional split, all three pivots used the original bare M56D
tripod in `CMU14/Objects/Weapons/Guns/HMGs/M56D/mount.rsi/ml66d_mount.png`.
That source artwork is retained at its original path.
