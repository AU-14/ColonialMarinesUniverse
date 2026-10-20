"""Find existing game art with pixels matching placed Bosenmori exact-port art."""

from __future__ import annotations

from collections import defaultdict
from hashlib import sha256
from pathlib import Path
import json
import re
import subprocess

from PIL import Image

from bosenmori_prototype_audit import ROOT, load


TEXTURE_ROOTS = [ROOT / "Resources/Textures", ROOT / "Content.CMU/Resources/Textures"]
REPORT = ROOT / ".codex-build/bosenmori_sprite_matches.json"


def pixel_hash(path: Path, frame_size: tuple[int, int] | None = None) -> str:
    with Image.open(path) as image:
        if frame_size and image.size != frame_size:
            image = image.crop((0, 0, *frame_size))
        image = image.convert("RGBA")
        return f"{image.width}x{image.height}:{sha256(image.tobytes()).hexdigest()}"


def run() -> None:
    # An art match is useful here only when an entity prototype uses the art.
    # Scanning every texture also includes a very large number of mob and UI sprites.
    placeholders, counts = load()
    targets: dict[str, list[str]] = defaultdict(list)
    for identifier in counts:
        sprite = str(placeholders[identifier]["sprite"])
        path = TEXTURE_ROOTS[1] / sprite / "source.png"
        if path.is_file():
            targets[pixel_hash(path)].append(identifier)

    matches: dict[str, list[dict[str, str]]] = defaultdict(list)
    scanned = 0
    result = subprocess.run(
        ["rg", "--no-filename", "-o", r"sprite: [^ #]+\.rsi",
         str(ROOT / "Resources/Prototypes"), str(ROOT / "Content.CMU/Resources/Prototypes")],
        capture_output=True, text=True, check=True,
    )
    sprite_paths = {re.sub(r"^sprite: ", "", line.strip()) for line in result.stdout.splitlines()}
    print(f"Checking {len(sprite_paths)} entity sprite paths", flush=True)
    for root in TEXTURE_ROOTS:
        for sprite in sorted(sprite_paths):
            if sprite.startswith("CMU14/Bosenmori/Exact/"):
                continue
            meta_path = root / sprite / "meta.json"
            if not meta_path.is_file():
                continue
            try:
                meta = json.loads(meta_path.read_text(encoding="utf-8-sig"))
                size = (meta["size"]["x"], meta["size"]["y"])
                sprite = meta_path.parent.relative_to(root).as_posix()
                for state in meta["states"]:
                    state_name = state["name"]
                    state_path = meta_path.parent / f"{state_name}.png"
                    if not state_path.is_file():
                        continue
                    scanned += 1
                    digest = pixel_hash(state_path, size)
                    if digest in targets:
                        for identifier in targets[digest]:
                            matches[identifier].append({"sprite": sprite, "state": state_name})
            except (KeyError, ValueError, OSError) as error:
                print(f"Skipping {meta_path}: {error}")

    REPORT.parent.mkdir(exist_ok=True)
    REPORT.write_text(json.dumps({"scanned": scanned, "matches": matches}, indent=2) + "\n", encoding="utf-8")
    print(f"Scanned {scanned} states; {len(matches)}/{len(counts)} used placeholders have exact pixel matches")
    print(f"Saved {REPORT}")


if __name__ == "__main__":
    run()
