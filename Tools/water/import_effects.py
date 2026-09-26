"""Import the water effects from cmss13#12918, pinned to the reviewed source revision.

Run with Python, Pillow and soundfile from the repository root. DMI direction/frame
order is converted to RSI's direction-major sheets; positional audio is mixed to mono.
"""

import io
import json
import math
from pathlib import Path
import re
from urllib.request import urlopen

from PIL import Image
import soundfile

REVISION = "185b5417a29541d5c314409b9125a7137ec22e05"
BASE = f"https://raw.githubusercontent.com/kugamo/cmss13/{REVISION}/"
ROOT = Path(__file__).resolve().parents[2]


def fetch(path):
    with urlopen(BASE + path, timeout=60) as response:
        return response.read()


def positional_audio(source):
    samples, rate = soundfile.read(io.BytesIO(source), always_2d=True)
    if samples.shape[1] == 1:
        return source
    output = io.BytesIO()
    soundfile.write(output, samples.mean(axis=1), rate, format="OGG", subtype="VORBIS")
    return output.getvalue()


def convert(path, destination, keep):
    image = Image.open(io.BytesIO(fetch(path)))
    description = image.info["Description"]
    width = int(re.search(r"\bwidth = (\d+)", description)[1])
    height = int(re.search(r"\bheight = (\d+)", description)[1])
    states = []
    offset = 0
    destination.mkdir(parents=True, exist_ok=True)
    for block in re.split(r'\nstate = ', description)[1:]:
        name = re.match(r'"([^"]*)"', block)[1]
        dirs_match = re.search(r"\bdirs = (\d+)", block)
        frames_match = re.search(r"\bframes = (\d+)", block)
        dirs = int(dirs_match[1]) if dirs_match else 1
        frames = int(frames_match[1]) if frames_match else 1
        if name in keep:
            moving = re.search(r"\bmovement = 1", block) is not None
            output_name = name + "_moving" if moving else name
            count = dirs * frames
            columns = math.ceil(math.sqrt(count))
            sheet = Image.new("RGBA", (width * columns, height * math.ceil(count / columns)))
            for direction in range(dirs):
                for frame in range(frames):
                    source = offset + frame * dirs + direction
                    x = source % (image.width // width) * width
                    y = source // (image.width // width) * height
                    target = direction * frames + frame
                    sheet.paste(image.crop((x, y, x + width, y + height)),
                                (target % columns * width, target // columns * height))
            sheet.save(destination / f"{output_name}.png")
            state = {"name": output_name}
            if dirs > 1:
                state["directions"] = dirs
            if frames > 1:
                delay = re.search(r"\bdelay = ([^\n]+)", block)
                delays = [float(d) / 10 for d in delay[1].split(",")] if delay else [0.1] * frames
                state["delays"] = [delays for _ in range(dirs)]
            states.append(state)
        offset += dirs * frames
    missing = keep - {state["name"] for state in states}
    if missing:
        raise ValueError(f"Missing DMI states in {path}: {missing}")
    metadata = {"version": 1, "license": "CC-BY-SA-3.0",
                "copyright": f"Kugamo / CM-SS13 contributors, ported from cmss13#12918. {BASE}{path}",
                "size": {"x": width, "y": height}, "states": states}
    (destination / "meta.json").write_text(json.dumps(metadata, indent=2) + "\n", encoding="utf-8")


def main():
    texture_root = ROOT / "Resources/Textures/_RMC14/Effects/Water"
    for size in (32, 48, 64, 88):
        keep = {"coast_shallow", "coast_deep", "shallow", "intermediate", "deep", "bubbles"}
        if size == 32:
            keep |= {f"human_resting_{depth}_{direction}" for depth in ("coast", "deep") for direction in ("e", "w")}
        convert(f"icons/effects/water_overlay_effects/_{size}.dmi", texture_root / f"splash{size}.rsi",
                keep)
    convert("icons/effects/water.dmi", texture_root / "impact.rsi", {"splash"})
    audio_root = ROOT / "Resources/Audio/_RMC14/Effects/Water"
    audio_root.mkdir(parents=True, exist_ok=True)
    sounds = [f"sound/effects/water/{prefix}{i}.ogg"
              for prefix, count in (("shallowwading", 6), ("wading", 3), ("deepwading", 4))
              for i in range(1, count + 1)]
    sounds += [f"sound/effects/alien_footstep_large_water{i}.ogg" for i in range(1, 4)]
    sounds.append("sound/effects/water/Splash.ogg")
    for path in sounds:
        (audio_root / Path(path).name.lower()).write_bytes(positional_audio(fetch(path)))
    names = ", ".join(json.dumps(Path(path).name.lower()) for path in sounds)
    (audio_root / "attributions.yml").write_text(
        f'- files: [{names}]\n  license: "CC-BY-SA-3.0"\n'
        '  copyright: "Kugamo / CM-SS13 contributors, ported from cmss13#12918; mixed to mono for positional playback"\n'
        f'  source: "https://github.com/kugamo/cmss13/tree/{REVISION}/sound/effects"\n', encoding="utf-8")


if __name__ == "__main__":
    main()
