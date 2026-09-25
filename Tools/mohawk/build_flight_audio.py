#!/usr/bin/env python3
"""Build approved Mohawk takeoff and flight audio from local source downloads."""
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


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("takeoff", type=Path)
    parser.add_argument("plasma", type=Path)
    parser.add_argument("reactor", type=Path)
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

    takeoff = decode(args.takeoff)
    takeoff[:480] *= np.linspace(0, 1, 480)
    takeoff[-480:] *= np.linspace(1, 0, 480)

    # Use the sustained plasma section, wrapping its tail into its head over one second.
    plasma = decode(args.plasma)[4 * RATE:35 * RATE]
    reactor = decode(args.reactor)
    if len(plasma) != 31 * RATE or len(reactor) != 15 * RATE:
        raise ValueError("Expected a 31-second plasma section and a 15-second reactor loop")
    flight = plasma[RATE:].copy()
    theta = np.linspace(0, np.pi / 2, RATE)
    flight[-RATE:] = plasma[-RATE:] * np.cos(theta) + plasma[:RATE] * np.sin(theta)

    # Rustbound is an undertone: its RMS level is 12 dB below the plasma layer.
    reactor = np.tile(reactor, 2)
    reactor *= np.sqrt(np.mean(flight ** 2) / np.mean(reactor ** 2)) * 10 ** (-12 / 20)
    flight += reactor
    output_dir = ROOT / "Content.CMU/Resources/Audio/CMU14/Dropships/Mohawk"
    for name, samples in [("takeoff", takeoff), ("flight", flight)]:
        samples *= 10 ** (-6 / 20) / float(np.max(np.abs(samples)))
        output = output_dir / (name + ".ogg")
        subprocess.run([ffmpeg, "-v", "error", "-y", "-f", "f32le", "-ac", "1", "-ar", str(RATE),
                        "-i", "pipe:0", "-c:a", "libvorbis", "-q:a", "5", str(output)],
                       input=samples.astype("<f4").tobytes(), check=True)
        decoded, rate = sf.read(output)
        assert rate == RATE and decoded.shape == samples.shape
        assert np.isfinite(decoded).all() and np.max(np.abs(decoded)) < 1
        print(f"Built {name}.ogg: {len(decoded) / rate:.3f}s, mono, peak {np.max(np.abs(decoded)):.3f}")

    recipe = {
        "sample_rate": RATE, "channels": 1, "peak_dbfs_before_encoding": -6,
        "takeoff_duration_seconds": len(takeoff) / RATE,
        "takeoff_edge_fades_seconds": 0.01,
        "flight_duration_seconds": 30, "plasma_source_range_seconds": [4, 35],
        "plasma_loop_crossfade_seconds": 1, "reactor_rms_relative_to_plasma_db": -12,
        "sources": [
            {"title": "Spaceship Whoosh 2", "author": "steshystesh", "license": "CC-BY-4.0",
             "license_url": "https://creativecommons.org/licenses/by/4.0/",
             "page": "https://freesound.org/people/steshystesh/sounds/336741/",
             "download": "https://cdn.freesound.org/previews/336/336741_3034244-hq.mp3",
             "sha256": hashlib.sha256(args.takeoff.read_bytes()).hexdigest()},
            {"title": "plasma engine fx", "author": "Insu (Freesound)", "license": "Pixabay Content License",
             "license_url": "https://pixabay.com/service/terms/",
             "page": "https://pixabay.com/sound-effects/film-special-effects-plasma-engine-fx-33559/",
             "download": "https://cdn.pixabay.com/audio/2022/03/10/audio_472768a080.mp3",
             "sha256": hashlib.sha256(args.plasma.read_bytes()).hexdigest()},
            {"title": "Rustbound Reactor 02 (Loop)", "author": "TommasoMotteran", "license": "CC0-1.0",
             "page": "https://freesound.org/people/TommasoMotteran/sounds/851916/",
             "download": "https://cdn.freesound.org/previews/851/851916_18681946-hq.mp3",
             "sha256": hashlib.sha256(args.reactor.read_bytes()).hexdigest()},
        ],
    }
    (Path(__file__).parent / "flight_audio.json").write_text(json.dumps(recipe, indent=2) + "\n", encoding="utf-8", newline="\n")


if __name__ == "__main__":
    main()
