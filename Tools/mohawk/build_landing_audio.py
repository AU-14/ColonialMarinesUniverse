#!/usr/bin/env python3
"""Build the approved ten-second Mohawk landing cue from two local source files.

Requires numpy, soundfile and ffmpeg (or imageio-ffmpeg). Source downloads stay
outside the repository; only the edited game asset and its recipe are committed.
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
SECONDS = 10
LEAD_START = 6
FADE_SECONDS = 0.25


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("lead", type=Path, help="Vospi: super specific landing")
    parser.add_argument("touchdown", type=Path, help="kalsstockmedia: Creaky Thunderous SciFi SpaceShip Landing")
    parser.add_argument("--ffmpeg", default=shutil.which("ffmpeg"))
    args = parser.parse_args()
    ffmpeg = args.ffmpeg
    if not ffmpeg:
        import imageio_ffmpeg
        ffmpeg = imageio_ffmpeg.get_ffmpeg_exe()

    def decode(path):
        result = subprocess.run([ffmpeg, "-v", "error", "-i", str(path), "-f", "f32le",
                                 "-ac", "1", "-ar", str(RATE), "pipe:1"], check=True, capture_output=True)
        return np.frombuffer(result.stdout, dtype="<f4").copy()

    lead = decode(args.lead)
    touchdown = decode(args.touchdown)
    total = SECONDS * RATE
    overlap = round(FADE_SECONDS * RATE)
    transition = total - len(touchdown)
    lead_length = transition + overlap
    if transition <= 0 or LEAD_START * RATE + lead_length > len(lead):
        raise ValueError("Source durations do not fit the ten-second landing cue")
    lead = lead[LEAD_START * RATE:LEAD_START * RATE + lead_length]
    theta = np.linspace(0, np.pi / 2, overlap)
    lead[-overlap:] *= np.cos(theta)
    touchdown[:overlap] *= np.sin(theta)
    mix = np.zeros(total, dtype=np.float32)
    mix[:lead_length] += lead
    mix[transition:] += touchdown
    mix[:480] *= np.linspace(0, 1, 480)  # Remove a click at the requested source cut.
    mix[-2400:] *= np.linspace(1, 0, 2400)
    mix *= 10 ** (-6 / 20) / float(np.max(np.abs(mix)))

    output = ROOT / "Content.CMU/Resources/Audio/CMU14/Dropships/Mohawk/landing.ogg"
    sf.write(output, mix, RATE, format="OGG", subtype="VORBIS")
    recipe = {
        "output": str(output.relative_to(ROOT)).replace("\\", "/"),
        "duration_seconds": SECONDS,
        "sample_rate": RATE,
        "channels": 1,
        "peak_dbfs_before_encoding": -6,
        "crossfade_seconds": FADE_SECONDS,
        "touchdown_starts_at_seconds": transition / RATE,
        "sources": [
            {"title": "super specific landing", "author": "Vospi", "license": "CC0-1.0",
             "page": "https://freesound.org/people/Vospi/sounds/439546/",
             "download": "https://cdn.freesound.org/previews/439/439546_91362-hq.mp3",
             "start_seconds": LEAD_START, "end_seconds": LEAD_START + lead_length / RATE,
             "sha256": hashlib.sha256(args.lead.read_bytes()).hexdigest()},
            {"title": "Creaky Thunderous SciFi SpaceShip Landing", "author": "Kalpesh Ajugia (kalsstockmedia)",
             "license": "Pixabay Content License", "license_url": "https://pixabay.com/service/terms/",
             "page": "https://pixabay.com/sound-effects/film-special-effects-creaky-thunderous-scifi-spaceship-landing-341621/",
             "download": "https://cdn.pixabay.com/audio/2025/05/14/audio_84689d3af2.mp3",
             "start_seconds": 0, "end_seconds": len(touchdown) / RATE,
             "sha256": hashlib.sha256(args.touchdown.read_bytes()).hexdigest()},
        ],
    }
    (Path(__file__).parent / "landing_audio.json").write_text(json.dumps(recipe, indent=2) + "\n", encoding="utf-8", newline="\n")
    decoded, rate = sf.read(output)
    assert rate == RATE and decoded.shape == (total,)
    assert np.isfinite(decoded).all() and np.max(np.abs(decoded)) < 1
    print(f"Built {output.name}: {len(decoded) / rate:.3f}s, mono; source starts at {LEAD_START}s, "
          f"touchdown enters at {transition / RATE:.3f}s, crossfade {FADE_SECONDS}s")


if __name__ == "__main__":
    main()
