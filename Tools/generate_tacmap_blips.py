# CMU14 file: generates tacmap blip RSIs from job icon RSIs (upstream keeps separate
# lobby/tacmap sprite sets; this mirrors that split for our job icons)
#
# Trims each state's transparent margins and re-centers the glyph on a 12x12 canvas
# (lossless - no resampling). The tacmap stretches the whole canvas to the blip box,
# so glyph-vs-canvas ratio is what controls rendered icon size; 8x8 glyph in 12x12
# approximates upstream map_blips coverage (5x5 in 7x7). The canvas must leave an
# even leftover after subtracting the glyph (12-8=4), an odd one (11-8=3) cannot be
# split symmetrically and renders every blip visibly off-center.
#
# Leader pip: job icons for leader roles carry a small tapering pip below (or above)
# the badge - 8-row badge + narrow 2-4px tail. Upstream tacmap blips carry no pip
# (map_blips leader states are plain 5x5 glyphs; the below-badge pip is an
# overhead-HUD-only convention there), so the pip is cropped from the tacmap copies.
# The crop keeps the 8 rows adjacent to the wider glyph end, so top and bottom pips
# both work; any dropped row wider than 4px (crown, unknown tall art) errors the run
# instead of silently cutting real ink.
#
# Run after adding/changing job icon sprites:
#   python3 Tools/generate_tacmap_blips.py
# Then point the job's TacticalMapIcon at the generated RSI (states keep their names).
# Requires ImageMagick (magick).

import json
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
TEXTURES = ROOT / "Resources" / "Textures"
CANVAS = 12

# source job icon RSI -> generated tacmap blip RSI
PAIRS = {
    TEXTURES / "_CMU14/Interface/cmucolonyjobicons.rsi": TEXTURES / "_CMU14/Interface/cmucolonyblips.rsi",
    TEXTURES / "_CMU14/Interface/cmugovforjobicons.rsi": TEXTURES / "_CMU14/Interface/cmugovforblips.rsi",
    TEXTURES / "_CMU14/Interface/cmuopforjobicons.rsi": TEXTURES / "_CMU14/Interface/cmuopforblips.rsi",
}


def generate(src: Path, dst: Path) -> None:
    meta = json.loads((src / "meta.json").read_text(encoding="utf-8"))
    dst.mkdir(parents=True, exist_ok=True)
    for stale in dst.glob("*.png"):
        stale.unlink()

    states = []
    for state in meta["states"]:
        png = src / f"{state['name']}.png"
        proc = subprocess.run(
            ["magick", str(png), "-trim", "+repage", "-format", "%wx%h", "info:"],
            capture_output=True, text=True, check=True)
        w, h = map(int, proc.stdout.split("x"))
        crop_h, y_off = h, 0
        if h > 8:
            if h > 11:
                sys.exit(f"{png.name}: {w}x{h} glyph is neither an 8-row badge nor "
                         f"badge+pip - handle manually")
            alpha = subprocess.run(
                ["magick", str(png), "-trim", "+repage", "-alpha", "extract",
                 "-depth", "8", "gray:-"], capture_output=True, check=True).stdout
            widths = [sum(1 for x in range(w) if alpha[y * w + x] > 128) for y in range(h)]
            y_off = h - 8 if widths[0] < widths[-1] else 0
            dropped = widths[:y_off] + widths[y_off + 8:]
            if any(dw > 4 for dw in dropped):
                sys.exit(f"{png.name}: tall glyph has non-pip rows {dropped} - "
                         f"refusing to crop, handle manually")
            crop_h = 8
        if w > CANVAS or crop_h > CANVAS:
            sys.exit(f"{png.name}: glyph {w}x{crop_h} exceeds {CANVAS}x{CANVAS} canvas - "
                     f"cannot trim losslessly; enlarge CANVAS or redraw smaller")
        trimmed = ["magick", str(png), "-trim", "+repage"]
        if crop_h != h:
            trimmed += ["-crop", f"{w}x{crop_h}+0+{y_off}", "+repage"]
        subprocess.run(
            trimmed + ["-gravity", "center", "-background", "none",
             "-extent", f"{CANVAS}x{CANVAS}", "-strip", str(dst / png.name)], check=True)
        states.append(state)

    out = {
        "version": meta["version"],
        "license": meta["license"],
        "copyright": meta["copyright"]
            + " | Tacmap blips in this RSI are auto-generated from "
            + src.name
            + " by Tools/generate_tacmap_blips.py (margin trim, leader pip crop, no other artistic change).",
        "size": {"x": CANVAS, "y": CANVAS},
        "states": states,
    }
    (dst / "meta.json").write_text(json.dumps(out, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(f"{dst.relative_to(ROOT)}: {len(states)} states")


def verify(dst: Path) -> None:
    for png in dst.glob("*.png"):
        proc = subprocess.run(["magick", str(png), "-format", "%wx%h %@", "info:"],
                              capture_output=True, text=True, check=True)
        canvas, ink = proc.stdout.split(" ")
        if canvas != f"{CANVAS}x{CANVAS}":
            sys.exit(f"{png}: expected {CANVAS}x{CANVAS}, got {canvas}")
        dims, x, y = ink.split("+")
        w, h = map(int, dims.split("x"))
        x, y = int(x), int(y)
        # odd leftover cannot center losslessly - would render off-center on the tacmap
        if (CANVAS - w) % 2 or (CANVAS - h) % 2 or x != (CANVAS - w) // 2 or y != (CANVAS - h) // 2:
            sys.exit(f"{png}: ink {w}x{h} at +{x}+{y} is not centered on {CANVAS}x{CANVAS}")
    print(f"{dst.relative_to(ROOT)}: all {CANVAS}x{CANVAS} centered OK")


if __name__ == "__main__":
    if shutil.which("magick") is None:
        sys.exit("ImageMagick (magick) not found in PATH")
    for src, dst in PAIRS.items():
        if not (src / "meta.json").is_file():
            sys.exit(f"missing {src}/meta.json")
        generate(src, dst)
        verify(dst)
