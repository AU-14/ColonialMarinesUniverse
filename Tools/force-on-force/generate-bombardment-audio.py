"""Build four variants per bombardment cue and audition reels. Requires numpy and soundfile.

Uses Little Robot Sound Factory's CC-BY-3.0 alarms and Laser 05, bart's
CC0 Space Laser, and the existing CC0 fighter recordings.
Downloads are cached in bin/ForceOnForceAudioSources.
Only finished mono cues and their attribution ship in Resources.
"""
from pathlib import Path
import hashlib
import io
import json
import sys
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "bin/ForceOnForceAudioTools"))
import numpy as np
import soundfile as sf

RATE = 44100
SOURCE = ROOT / "bin/ForceOnForceAudioSources"
OUT = ROOT / "Content.CMU/Resources/Audio/CMU14/ForceOnForce"
FIGHTER = ROOT / "Content.CMU/Resources/Audio/CMU14/Fighter"
SOURCE.mkdir(parents=True, exist_ok=True)
OUT.mkdir(parents=True, exist_ok=True)
stats = {}
sources = {}


def fetch(name, url):
    path = SOURCE / name
    if not path.exists():
        path.write_bytes(urllib.request.urlopen(url, timeout=60).read())
    data = path.read_bytes()
    sources[name] = dict(url=url, sha256=hashlib.sha256(data).hexdigest())
    return data


def read(source):
    x, rate = sf.read(source, always_2d=True)
    x = x.mean(axis=1)
    return np.interp(np.arange(round(len(x) / rate * RATE)) / RATE, np.arange(len(x)) / rate, x)


def speed(x, rate):
    return np.interp(np.arange(0, len(x), rate), np.arange(len(x)), x)


def finish(x, peak=.75):
    x = x - x.mean()
    x *= peak / max(.001, np.max(np.abs(x)))
    n = min(RATE // 20, len(x) // 4)
    x[:n] *= np.linspace(0, 1, n)
    x[-n:] *= np.linspace(1, 0, n)
    return x


def echo(x, delay=.15, gain=.3):
    gap = round(delay * RATE)
    out = np.zeros(len(x) + gap * 3)
    out[:len(x)] = x
    for i in range(1, 4):
        out[gap * i:gap * i + len(x)] += x * gain ** i
    return out


def save(category, variant, x):
    x = finish(x)
    name = f"{category}-{variant}.ogg"
    sf.write(OUT / name, x, RATE, subtype="VORBIS")
    decoded = read(OUT / name)
    assert np.isfinite(decoded).all() and np.max(np.abs(decoded)) < .99
    stats[name] = dict(seconds=round(len(decoded) / RATE, 3), peak=round(float(np.max(np.abs(decoded))), 4))
    return decoded


robot_data = fetch("little-robot.zip", "https://opengameart.org/sites/default/files/Sci-Fi%20Sound%20Library.zip")
siren_names = [f"Sci-Fi Sound Library/Wav/Alarm_Loop_{i:02}.wav" for i in range(2)]
laser_names = ["space-laser.wav", "Sci-Fi Sound Library/Wav/Laser/Laser_05.wav"]
with zipfile.ZipFile(io.BytesIO(robot_data)) as robot_zip:
    sirens = [read(io.BytesIO(robot_zip.read(name))) for name in siren_names]
    robot_laser = read(io.BytesIO(robot_zip.read(laser_names[1])))
space_laser = read(io.BytesIO(fetch("space-laser.wav", "https://opengameart.org/sites/default/files/space%20laser.wav")))
lasers = [space_laser, robot_laser]
flybys = [read(FIGHTER / name) for name in ["jet-pass.ogg", "jet-pass-high.ogg", "jet-pass.ogg", "jet-exterior.ogg"]]
blast = read(FIGHTER / "missile-impact.ogg")
reels = {category: [] for category in ["siren", "flyby", "laser", "impact"]}
for i in range(4):
    # Audition choices S2/S3 and L1/L2 keep their pitch in slots 1/2.
    # Slots 3/4 are lower-pitched variations of those same approved recordings.
    alarm = speed(sirens[i % 2], 1 if i < 2 else .9)
    alarm = np.tile(alarm, int(np.ceil(8 * RATE / len(alarm))))[:8 * RATE]
    reels["siren"].append(save("siren", i + 1, alarm))
    reels["flyby"].append(save("flyby", i + 1, speed(flybys[i], [1, 1, .9, 1.15][i])[:8 * RATE]))
    laser = lasers[i % 2]
    if i >= 2:
        laser = echo(speed(laser, .9), .18, .2)
    reels["laser"].append(save("laser", i + 1, laser))
    reels["impact"].append(save("impact", i + 1, echo(speed(blast, .65 + i * .15), .2 + .06 * i, .4)))
for category, variants in reels.items():
    reel = np.concatenate([part for cue in variants for part in [cue, np.zeros(RATE)]])
    sf.write(SOURCE / f"preview-{category}.wav", reel, RATE, subtype="PCM_16")
(SOURCE / "manifest.json").write_text(json.dumps(dict(sources=sources, cues=stats, sirens=siren_names, lasers=laser_names), indent=2))
print(json.dumps(stats, indent=2))
