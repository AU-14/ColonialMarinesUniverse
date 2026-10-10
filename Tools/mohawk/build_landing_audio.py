#!/usr/bin/env python3
"""Build the ten-second Mohawk landing cue from 0:15 of Extended spaceship lift-off.

Requires numpy, soundfile and ffmpeg (or imageio-ffmpeg). Keep the source download
outside the repository; commit only the edited game asset and its recipe.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess

import numpy as np
import soundfile as sf

ROOT = Path(__file__).resolve().parents[2]
RATE = 48000
START = 15
SECONDS = 10


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path, help="pointparkcinema: Extended spaceship lift-off")
    parser.add_argument("--ffmpeg", default=shutil.which("ffmpeg"))
    args = parser.parse_args()
    ffmpeg = args.ffmpeg
    if not ffmpeg:
        import imageio_ffmpeg
        ffmpeg = imageio_ffmpeg.get_ffmpeg_exe()

    result = subprocess.run([ffmpeg, "-v", "error", "-i", str(args.source), "-f", "f32le",
                             "-ac", "1", "-ar", str(RATE), "pipe:1"], check=True, capture_output=True)
    decoded = np.frombuffer(result.stdout, dtype="<f4")
    cue = decoded[START * RATE:(START + SECONDS) * RATE].copy()
    if len(cue) != SECONDS * RATE:
        raise ValueError("Source must contain the full 15-to-25-second landing section")
    cue[:480] *= np.linspace(0, 1, 480)
    cue[-2400:] *= np.linspace(1, 0, 2400)
    cue *= 10 ** (-6 / 20) / float(np.max(np.abs(cue)))

    output = ROOT / "Content.CMU/Resources/Audio/CMU14/Dropships/Mohawk/landing.ogg"
    subprocess.run([ffmpeg, "-v", "error", "-y", "-f", "f32le", "-ac", "1", "-ar", str(RATE),
                    "-i", "pipe:0", "-c:a", "libvorbis", "-q:a", "5", str(output)],
                   input=cue.astype("<f4").tobytes(), check=True)
    recipe = {
        "output": output.relative_to(ROOT).as_posix(),
        "duration_seconds": SECONDS,
        "sample_rate": RATE,
        "channels": 1,
        "peak_dbfs_before_encoding": -6,
        "fade_in_seconds": 0.01,
        "fade_out_seconds": 0.05,
        "sources": [
            {"title": "spaceship takeoff.wav", "preview_label": "Extended spaceship lift-off", "author": "pointparkcinema", "license": "CC0-1.0",
             "license_url": "https://creativecommons.org/publicdomain/zero/1.0/",
             "page": "https://freesound.org/people/pointparkcinema/sounds/407253/",
             "download": "https://cdn.freesound.org/previews/407/407253_7237186-hq.mp3",
             "start_seconds": START, "end_seconds": START + SECONDS,
             "sha256": hashlib.sha256(args.source.read_bytes()).hexdigest()},
        ],
    }
    (Path(__file__).parent / "landing_audio.json").write_text(json.dumps(recipe, indent=2) + "\n", encoding="utf-8", newline="\n")
    samples, rate = sf.read(output)
    assert rate == RATE and samples.shape == (SECONDS * RATE,)
    assert np.isfinite(samples).all() and np.max(np.abs(samples)) < 1
    print(f"Built {output.name}: {len(samples) / rate:.3f}s, mono, source {START}-{START + SECONDS}s, "
          f"peak {np.max(np.abs(samples)):.3f}")


if __name__ == "__main__":
    main()
