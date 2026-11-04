"""Generate four-direction, 32-pixel displacement maps for the standard human pose.

Uses only Python's standard library. Ordinary pixels store a signed source-pixel
lookup in RG around 128, with fill shade in B and visibility in A. Side fill has
no vertical displacement, so G above 160 instead encodes 192 + a horizontal
fallback offset. The shader tries that adjacent chest pixel only if its preferred
patch sample is transparent. This keeps narrow garments from acquiring holes.
These channels must be loaded as linear data, without sRGB colour conversion.
Run this tool only when editing the pose; the game loads its PNG output and
never invokes Python during a round.
"""
import json
from pathlib import Path
import struct
import zlib

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / 'Resources/Textures/CMU14/Mobs/Surrender/pose.rsi'
BASE_STATES = ('limbs', 'clothing', 'torso', 'foreground_limbs', 'foreground_clothing', 'waist_gear',
               'armor', 'foreground_armor')
LIMB_STATES = ('limbs', 'clothing', 'foreground_limbs', 'foreground_clothing', 'armor', 'foreground_armor')
STATES = BASE_STATES + tuple(kind + suffix for kind in LIMB_STATES for suffix in ('_none', '_left', '_right'))


def side_cutout(kind, direction, x, y):
    if direction not in (2, 3):
        return x, y, 0

    # The near arm attaches at x=14 east / x=17 west in the native human frames.
    # A rigid half-turn keeps every source pixel intact, including sleeve detail.
    pivot_x = 14 if direction == 2 else 17
    sx, sy = 2 * pivot_x - x, 22 - y
    outer = 9 if kind == 'foreground_armor' else 11
    in_arm = outer <= sx < 16 if direction == 2 else 16 <= sx < 32 - outer
    last_row = 18 if kind in ('foreground_clothing', 'foreground_armor') else 21
    if not in_arm or not 10 <= sy <= last_row:
        return x, y, 0
    return sx, sy, 255


def side_fill_source(kind, direction, x, y):
    # Reflect a small chest patch into the vacated arm region. The seam-facing
    # edge samples the adjacent chest column; the middle retains texture detail
    # instead of extruding that one column across the entire side.
    inward = x - 11 if direction == 2 else 20 - x
    if kind == 'torso':
        offset = min(inward, 3 - inward)
        sx = 15 + offset if direction == 2 else 16 - offset
    else:
        offset = min(inward, 4 - inward)
        sx = 16 + offset if direction == 2 else 15 - offset
    return sx, y, 255


def base_source_pixel(kind, direction, x, y):
    side = direction in (2, 3)
    if kind.startswith('foreground_'):
        return side_cutout(kind, direction, x, y)

    if kind == 'waist_gear':
        # Sample past the hand notch; the original gear draws over this small fill.
        in_hand = 12 <= x < 16 if direction == 2 else 16 <= x < 20
        if side and in_hand and 18 <= y <= 21:
            return (17 if direction == 2 else 14), y, 255
        return x, y, 0

    if kind == 'limbs':
        # Side views draw only the near-arm cutout; the standing/far arms stay masked.
        if side:
            return x, y, 0
        sy = 22 - y
        return x, sy, 255 if 0 <= sy < 32 else 0

    if kind == 'torso':
        # The standing torso has exposed amputation seams underneath its arms.
        # Fill them from adjacent chest pixels after the arms leave that area.
        if side and 11 <= y <= 18:
            # Keep the lifted shoulder outline; the contact strip starts below it.
            if y <= 12 and x == (11 if direction == 2 else 20):
                return x, y, 0
            if direction == 2 and 11 <= x <= 14:
                return side_fill_source(kind, direction, x, y)
            if direction == 3 and 17 <= x <= 20:
                return side_fill_source(kind, direction, x, y)
        elif not side and 11 <= y <= 14:
            if x in (9, 21):
                return x, y, 0
            if x == 10:
                return 11, y, 255
            if x == 20:
                return 19, y, 255
        return x, y, 255

    if kind == 'armor' and side:
        arm = 9 <= x < 16 if direction == 2 else 16 <= x < 23
        if arm and y <= 18:
            if y <= 12 or (x < 11 if direction == 2 else x > 20):
                return x, y, 0
            return side_fill_source(kind, direction, x, y)

    if side:
        arm = 11 <= x < 16 if direction == 2 else 16 <= x < 21
        if arm and y <= 12:
            return x, y, 0
        if arm and 12 < y <= 18:
            # Fill the sleeve's contact strip without remapping the waist below the arm.
            return side_fill_source(kind, direction, x, y)
    elif (5 <= x < 11 or 20 <= x < 26) and y < 22:
        if x in (10, 20) and 12 <= y <= 14:
            return (11 if x == 10 else 19), y, 255
        return x, 22 - y, 255 if y <= 11 else 0

    return x, y, 255


def source_pixel(kind, direction, x, y):
    mask = 3
    for suffix, arms in (('_none', 0), ('_left', 1), ('_right', 2)):
        if kind.endswith(suffix):
            kind = kind[:-len(suffix)]
            mask = arms
            break

    sx, sy, alpha = base_source_pixel(kind, direction, x, y)
    if mask == 3 or alpha == 0:
        return sx, sy, alpha

    if kind.startswith('foreground_'):
        near_arm = 2 if direction == 2 else 1
        return sx, sy, alpha if mask & near_arm else 0

    if kind == 'limbs':
        left = sx >= 16 if direction == 0 else sx < 16
        return sx, sy, alpha if mask & (1 if left else 2) else 0

    # The torso/waist stays put. Only raised sleeve pixels depend on arm presence.
    if direction < 2 and y < 12 and (x < 11 or x >= 20):
        left = x >= 16 if direction == 0 else x < 16
        if not mask & (1 if left else 2):
            return sx, sy, 0
    return sx, sy, alpha


def fill_shade(kind, direction, x, y):
    if direction not in (2, 3):
        return 0
    for suffix in ('_none', '_left', '_right'):
        if kind.endswith(suffix):
            kind = kind[:-len(suffix)]
            break

    edge = 11 if direction == 2 else 20
    inward = x - edge if direction == 2 else edge - x
    if kind == 'torso':
        if 13 <= y <= 18 and 0 <= inward <= 3:
            return ((224, 112, 32, 0) if y <= 14 else (144, 48, 16, 0))[inward]
        if 11 <= y <= 12 and 1 <= inward <= 3:
            return (192, 64, 0)[inward - 1]
    if kind in ('clothing', 'armor') and 13 <= y <= 18 and 0 <= inward <= 4:
        # Contact shade is strongest at the armpit and narrows down the side.
        # Armor keeps a firmer edge; fabric retains a wider, softer fold.
        if kind == 'armor':
            shades = (224, 96, 24, 0, 0) if y <= 14 else (144, 48, 0, 0, 0)
        else:
            shades = (224, 128, 64, 16, 0) if y <= 14 else (128, 64, 24, 0, 0)
        return shades[inward]
    return 0


def encoded_pixel(kind, direction, x, y):
    sx, sy, alpha = source_pixel(kind, direction, x, y)
    green = 128 + sy - y
    base = kind.removesuffix('_none').removesuffix('_left').removesuffix('_right')
    if direction in (2, 3) and alpha and base in ('torso', 'clothing', 'armor') and sx != x:
        # Only same-row reconstructed side fill owns this spare channel range.
        # Raised limbs and front/back maps retain the ordinary displacement ABI.
        fallback = (15 if direction == 2 else 16) if base == 'torso' else (16 if direction == 2 else 15)
        green = 192 + fallback - x
    return 128 + sx - x, green, fill_shade(kind, direction, x, y), alpha


def png_chunk(kind, data):
    return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data))


def generate():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for kind in STATES:
        pixels = bytearray(64 * 64 * 4)
        for direction in range(4):
            for y in range(32):
                for x in range(32):
                    offset = (((direction // 2) * 32 + y) * 64 + (direction % 2) * 32 + x) * 4
                    pixels[offset:offset + 4] = bytes(encoded_pixel(kind, direction, x, y))
        rows = b''.join(b'\0' + pixels[y * 256:(y + 1) * 256] for y in range(64))
        png = b'\x89PNG\r\n\x1a\n'
        png += png_chunk(b'IHDR', struct.pack('>IIBBBBB', 64, 64, 8, 6, 0, 0, 0))
        png += png_chunk(b'IDAT', zlib.compress(rows, 9))
        png += png_chunk(b'IEND', b'')
        (OUTPUT / (kind + '.png')).write_bytes(png)
    metadata = {
        'version': 1,
        'license': 'CC0-1.0',
        'copyright': 'Procedural displacement data generated by Content.CMU/Tools/Surrender/generate_pose_maps.py; no source artwork copied.',
        'size': {'x': 32, 'y': 32},
        'load': {'srgb': False},
        'states': [{'name': kind, 'directions': 4} for kind in STATES],
    }
    (OUTPUT / 'meta.json').write_text(json.dumps(metadata, indent=4) + '\n', encoding='utf-8')


if __name__ == '__main__':
    generate()
