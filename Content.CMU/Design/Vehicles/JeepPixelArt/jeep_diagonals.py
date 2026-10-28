#!/usr/bin/env python3
"""Prototype diagonal (SE, NE, NW, SW) views of the jeep, for a possible 8-direction jeep later.

Not used by the game or by jeep_pixel_art.py: the jeep RSIs stay 4-directional. This only writes a
preview sheet of the four current views with the four diagonals between them.

How a diagonal is drawn: the voxel model is turned 45 degrees into a world grid (2x2 samples per
voxel, like the turret's aimed()), squeezed across to keep the jeep slim, and rendered by the same
render() as the cardinal views with a camera of its own (DIAGONAL_CAMERA, close to the E/W camera,
scaled to the cardinals' size). Outlines are only drawn where a pixel's neighbour comes from a voxel
that is not next to its own, so the 45-degree grid's stair steps get no line.

Known problems before this could ship:
- 1-voxel details (hood ribs, panel seams, grille slats, the medical cross) break up into stray
  diagonal lines and ragged pixels.
- The windshield glass is not drawn.
- The wheels read as dark lumps.
- Only the body is drawn: no layers, overlay, masks, crayon map or offsets.

Run: python Content.CMU/Design/Vehicles/JeepPixelArt/jeep_diagonals.py
"""
import math
import os

from PIL import ImageDraw

import jeep_pixel_art as J

# Jeep heading per diagonal view, as (f, r) on the screen's x and up axes.
HEADINGS = {"SE": (1, -1), "SW": (-1, -1), "NE": (1, 1), "NW": (-1, 1)}
# The cardinal view a diagonal borrows its build() from (seat backs, rear details).
BACK = {"SE": "S", "SW": "S", "NE": "N", "NW": "N"}
# (screen x, depth, height) scales of the diagonal camera, and the squeeze across the jeep.
DIAGONAL_CAMERA = (1.18, 0.83, 0.94)
DIAGONAL_WIDTH = 0.8
TURN_ORDER = ("S", "SE", "E", "NE", "N", "NW", "W", "SW")


def scene(variant, back):
    """The whole jeep, shut, with every hardpoint, and all four wheels at their true axles."""
    m = J.build(variant, back)
    wheels = J.Model()
    for axle in (J.FRONT_AXLE, J.REAR_AXLE):
        for r0, r1 in J.WHEEL_SPAN:
            J.wheel(wheels, axle, J.WHEEL_Z, r0, r1, 0.0, J.inside_tub)
    m = m.merged(wheels).merged(J.hood_lid(variant)).merged(J.fuel_door()).merged(J.panels_closed(variant))
    for name in J.FITTED:
        m = m.merged(J.spare() if name == "spare" else J.HARDPOINTS[name]())
    return m


def turned(model, view):
    """The model turned to face a diagonal, in a world grid, and each world voxel's jeep-space key."""
    dx, dy = HEADINGS[view]
    n = math.hypot(dx, dy)
    dx, dy = dx / n, dy / n
    out = J.Model()
    origin = {}
    for (f, r, z), v in model.vox.items():
        for a in (0.25, 0.75):
            for b in (0.25, 0.75):
                ff, rr = f + a, (r + b) * DIAGONAL_WIDTH
                key = (math.floor(ff * dx + rr * dy), math.floor(ff * dy - rr * dx), z)
                out.vox[key] = v
                origin[key] = (f, r, z)
    return out, origin


def shade_turned(buf, origin):
    """shade(), with outlines only on real edges: a neighbour from a voxel not next to this one."""
    col = J.shade(buf)
    for (x, y), (d, mat, part, face, key) in buf.items():
        here = origin[key]
        edge = False
        for dx, dy in ((1, 0), (-1, 0), (0, -1), (0, 1)):
            nb = buf.get((x + dx, y + dy))
            if nb is None:
                edge = True
                break
            there = origin[nb[4]]
            if max(abs(a - b) for a, b in zip(here, there)) > 1 and nb[0] > d + 1.5:
                edge = True
                break
        pal = J.PAL[mat]
        if edge:
            col[(x, y)] = pal["line"] + (255,)
        elif col[(x, y)][:3] == pal["line"]:
            col[(x, y)] = (pal["top"] if face == "top" else pal["front"]) + (255,)
    return col


def diagonal(variant, view):
    """One diagonal view of the shut jeep, centred on a canvas like the RSI frames."""
    sx, sy, sz = DIAGONAL_CAMERA
    J.DEPTH_SCALE["D"], J.HEIGHT_SCALE["D"], J.LENGTH_SCALE["D"] = sy, sz, sx
    to_world = J.to_world
    J.to_world = lambda v, f, r: (f, r) if v == "D" else to_world(v, f, r)
    try:
        model, origin = turned(scene(variant, BACK[view]), view)
        buf, _ = J.render(model, "D")
        col = shade_turned(buf, origin)
    finally:
        J.to_world = to_world
    ys = [y for _, y in col]
    shift = J.SIZE // 2 - round((min(ys) + max(ys)) / 2)
    return J.to_image({(x + J.SIZE // 2, y + shift): c for (x, y), c in col.items()})


def main():
    dy = J.view_dy()
    rows = []
    for variant in J.VARIANTS:
        per = {v: J.split_direction(variant, v, dy[v]) for v in J.DIRS}
        rows.append([J.composite(variant, v, per, dy) if len(v) == 1 else diagonal(variant, v) for v in TURN_ORDER])
    sheet = J.sheet(rows, 3)
    draw = ImageDraw.Draw(sheet)
    for i, v in enumerate(TURN_ORDER):
        draw.text((i * sheet.width // len(TURN_ORDER) + 6, 4), v, fill=(255, 220, 120, 255))
    os.makedirs(J.PREVIEW_DIR, exist_ok=True)
    path = os.path.join(J.PREVIEW_DIR, "jeep_diagonals.png")
    sheet.save(path)
    print(path)


if __name__ == "__main__":
    main()
