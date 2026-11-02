#!/usr/bin/env python3
"""Extract deployed four-direction door-gun layers without resampling source pixels.

Requires Pillow. Run with --overwrite to regenerate the editable output artwork.
The masks separate visible gun pixels from supports; they do not invent hidden art.
"""

import argparse
import json
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[3]
TEXTURES = ROOT / "Content.CMU/Resources/Textures"
OUTPUT = TEXTURES / "CMU14/Objects/Weapons/Guns/DoorGuns"
SOURCES = {
    "M56D": ("CMU14/Objects/Weapons/Guns/HMGs/M56D/mount.rsi", "ml66d", "ml66d_magazine", None),
    "M2C": ("CMU14/Objects/Weapons/Guns/HMGs/m2c.rsi", "m2c", "m2c_magazine", "m2c_overheat"),
    "UTESC": ("_RuMC14/Objects/Weapons/Guns/HMGs/UTES-C.rsi", "utes-c", "utes-c_magazine", "utes-c_overheat"),
}


def gun_pixel(option, direction, x, y):
    # Direction indices match RSI packing: south, north, east, west.
    if option == "M56D":
        if direction >= 2:
            return y <= 18
        if direction == 0:
            return y <= 12 or (y <= 19 and 28 <= x <= 35) or 30 <= x <= 32
        return (y <= 10 and 27 <= x <= 35) or (y <= 23 and 29 <= x <= 36) or (y >= 24 and 27 <= x <= 35)
    if direction >= 2:
        return y <= (18 if option == "M2C" else 19) or (
            option == "UTESC" and y == 20 and (x <= 6 if direction == 2 else x >= 25))
    if option == "M2C":
        if direction == 0:
            return y <= 14 or 30 <= x <= 33
        return (28 <= x <= 35) or (y >= 24 and 27 <= x <= 36)
    if direction == 0:
        return y <= 11 or (y <= 17 and 12 <= x <= 19) or (y <= 19 and 13 <= x <= 18) or 14 <= x <= 17
    return (y <= 10 and 13 <= x <= 16) or (y <= 25 and 12 <= x <= 17) or (y >= 26 and 13 <= x <= 18)


def write_rsi(directory, metadata, states, images):
    directory.mkdir(parents=True, exist_ok=True)
    metadata = {**metadata, "states": [{"name": name, "directions": 4} for name in states]}
    (directory / "meta.json").write_text(json.dumps(metadata, indent=4) + "\n", encoding="utf-8")
    for name, image in images.items():
        image.save(directory / (name + ".png"))


def assert_visible_equal(expected, actual):
    for y in range(expected.height):
        for x in range(expected.width):
            source_pixel, result_pixel = expected.getpixel((x, y)), actual.getpixel((x, y))
            assert source_pixel[3] == result_pixel[3]
            assert not source_pixel[3] or source_pixel == result_pixel


def split(option, source, overwrite):
    source_dir, gun_state, ammo_state, heat_state = source
    source_dir = TEXTURES / source_dir
    metadata = json.loads((source_dir / "meta.json").read_text())
    original = Image.open(source_dir / (gun_state + ".png")).convert("RGBA")
    ammo = Image.open(source_dir / (ammo_state + ".png")).convert("RGBA")
    heat = Image.open(source_dir / (heat_state + ".png")).convert("RGBA") if heat_state else original.copy()
    width, height = metadata["size"]["x"], metadata["size"]["y"]
    assert original.size == (width * 2, height * 2)
    assert ammo.size == heat.size == original.size
    if not overwrite and any((OUTPUT / option / name).exists() for name in ["pivot.rsi", "gun.rsi"]):
        raise FileExistsError(f"{option}: output exists; use --overwrite only to replace edited artwork")

    gun = Image.new("RGBA", original.size)
    mount = Image.new("RGBA", original.size)
    gun_ammo = Image.new("RGBA", original.size) if option == "M56D" else ammo.copy()
    gun_heat = heat.copy()
    for y in range(original.height):
        for x in range(original.width):
            pixel = original.getpixel((x, y))
            assert pixel[3] in (0, 255), "The lossless split expects binary source alpha"
            direction = (y // height) * 2 + x // width
            moving = gun_pixel(option, direction, x % width, y % height)
            if moving:
                gun.putpixel((x, y), pixel)
            else:
                mount.putpixel((x, y), pixel)
            # The M56D magazine state includes the complete weapon and stand, unlike the other overlays.
            if option == "M56D" and ammo.getpixel((x, y)) != pixel:
                gun_ammo.putpixel((x, y), ammo.getpixel((x, y)))

    if heat_state is None:
        gun_heat = gun.copy()

    recomposed = Image.alpha_composite(mount, gun)
    # RGB under fully transparent pixels is irrelevant; every visible source pixel must survive exactly.
    assert_visible_equal(original, recomposed)
    assert_visible_equal(Image.alpha_composite(original, ammo), Image.alpha_composite(recomposed, gun_ammo))

    write_rsi(OUTPUT / option / "pivot.rsi", metadata, ["mount"], {"mount": mount})
    write_rsi(OUTPUT / option / "gun.rsi", metadata, ["gun", "ammo", "heat"],
              {"gun": gun, "ammo": gun_ammo, "heat": gun_heat})
    print(f"{option}: all four directions split; visible source pixels recompose exactly")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--overwrite", action="store_true", help="replace existing editable door-gun artwork")
    args = parser.parse_args()
    for option, source in SOURCES.items():
        split(option, source, args.overwrite)
