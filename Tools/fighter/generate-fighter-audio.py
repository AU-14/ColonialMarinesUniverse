"""Build the fighter mix from licensed recordings, without runtime DSP.

Requires NumPy and soundfile. Run fetch-audio-sources.py first. Source previews
and checksums live in bin/FighterAudioSources; only finished mono cues ship.
The approved MANPAD acquisition sound is deliberately preserved (no dinks).
"""
from pathlib import Path
import argparse
import hashlib
import json
import numpy as np
import soundfile as sf

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "bin/FighterAudioSources"
OUT = ROOT / "Content.CMU/Resources/Audio/CMU14/Fighter"
RATE = 44100
STATS = {}
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--flight-only", action="store_true", help="Rebuild only the selected jet recordings")
parser.add_argument("--manpad-only", action="store_true", help="Rebuild only the selected MANPAD launch recording")
ARGS = parser.parse_args()
FLIGHT_ONLY = ARGS.flight_only
MANPAD_ONLY = ARGS.manpad_only
FLIGHT_FILES = {"jet-exterior.ogg", "jet-cockpit.ogg", "jet-pass.ogg", "jet-pass-high.ogg", "vtol-takeoff.wav"}

for name, source in json.loads(Path(__file__).with_name("audio-sources.json").read_text()).items():
    data = (SRC / f"{name}.mp3").read_bytes()
    if hashlib.sha256(data).hexdigest() != source["sha256"]:
        raise RuntimeError(f"{name}: source changed; review it and update audio-sources.json before rebuilding")


def read(path):
    x, rate = sf.read(path, always_2d=True)
    x = x.mean(axis=1)
    return np.interp(np.arange(round(len(x) / rate * RATE)) / RATE, np.arange(len(x)) / rate, x)


def smooth(x):
    x = np.clip(x, 0, 1)
    return x * x * (3 - 2 * x)


def band(x, low=40, high=7000):
    f = np.fft.rfftfreq(len(x), 1 / RATE)
    weights = (f / low) ** 2 / (1 + (f / low) ** 2) / (1 + (f / high) ** 6)
    return np.fft.irfft(np.fft.rfft(x - np.mean(x)) * weights, len(x))


def clip(x, start, seconds, pitch=1):
    t = start + np.arange(round(seconds * RATE)) / RATE * pitch
    return np.interp(t, np.arange(len(x)) / RATE, x, left=0, right=0)


def fit(x, seconds):
    return np.interp(np.linspace(0, len(x) - 1, round(seconds * RATE)), np.arange(len(x)), x)


def layer(x, y, at=0, gain=1):
    start = round(at * RATE)
    count = min(len(y), len(x) - start)
    x[start:start + count] += y[:count] * gain


def save(name, x, rms=.17, loop=False):
    if MANPAD_ONLY and name != "manpad-launch.wav":
        return
    if FLIGHT_ONLY and name not in FLIGHT_FILES:
        return
    x = band(x, 32, 12000)
    x *= rms / max(.0001, np.sqrt(np.mean(x * x)))
    peak = np.max(np.abs(x))
    if peak > .89:
        x *= .89 / peak
    if loop:
        # Overlap the end with the start; the final sample leads into the kept start.
        n = round(.18 * RATE)
        fade = smooth(np.linspace(0, 1, n))
        x[-n:] = x[-n:] * (1 - fade) + x[:n] * fade
        x = x[n:]
    else:
        n = min(round(.012 * RATE), len(x) // 4)
        x[:n] *= smooth(np.linspace(0, 1, n))
        x[-n:] *= smooth(np.linspace(1, 0, n))
    assert np.all(np.isfinite(x)) and np.max(np.abs(x)) <= .90
    sf.write(OUT / name, x, RATE, subtype="PCM_16" if name.endswith(".wav") else "VORBIS")
    STATS[name] = dict(seconds=round(len(x) / RATE, 3), peak=round(float(np.max(np.abs(x))), 4),
                       rms=round(float(np.sqrt(np.mean(x * x))), 4), loop=loop)


turbine = band(read(SRC / "turbine.mp3"), 45, 7800)
low_pass = band(read(SRC / "f18-low.mp3"), 70, 9500)
cruise = band(read(SRC / "f16-cruise.mp3"), 55, 8500)
startup = band(read(SRC / "f16-startup.mp3"), 55, 9500)
missile = band(read(SRC / "missile.mp3"), 60, 9500)
blast = band(read(SRC / "blast.mp3"), 35, 11000)
mechanical = read(ROOT / "Content.CMU/Resources/Audio/CMU14/Blackfoot/mechanical.wav")
gau = read(ROOT / "Resources/Audio/_RMC14/Dropship/gau.ogg")
gau_cabin = read(ROOT / "Resources/Audio/_RMC14/Dropship/gau_incockpit.ogg")
gau_hit = read(ROOT / "Resources/Audio/_RMC14/Dropship/gauimpact.ogg")

# The user's F-16 recording supplies cruise; use its steady section for loops.
save("jet-exterior.ogg", clip(cruise, 37, 8.18), loop=True)
save("jet-cockpit.ogg", band(clip(cruise, 37, 8.18), 60, 1800), rms=.11, loop=True)
save("jet-idle.ogg", band(clip(turbine, 8, 6.18, .72), 80, 3500), rms=.10, loop=True)
# Preserve the original pitch and approach/recession of both selected passes.
# Low passes use the F-18; cloud/high passes use the F-16 with softer treble.
t = np.arange(8 * RATE) / RATE
pass_envelope = smooth(t / .35) * (1 - smooth((t - 6.3) / 1.7))
save("jet-pass.ogg", clip(low_pass, 2.5, 8) * pass_envelope, .20)
save("jet-pass-high.ogg", band(clip(cruise, 14, 8), 60, 4500) * pass_envelope, .14)

# The requested F-16 startup fits the existing eight-second vertical launch.
# No time compression or pitch ramp: retain the recorded engine character.
x = clip(startup, 0, 8) * (.4 + .6 * smooth(t / 3)) * (1 - .92 * smooth((t - 6) / 2))
save("vtol-takeoff.wav", x, .19)
t = np.arange(6 * RATE) / RATE
x = clip(turbine, 40, 6, .9) * (.08 + .92 * smooth(t / 4.8))
x *= 1 - .32 * smooth((t - 5) / 1)
save("vtol-descent.wav", x, .19)
t = np.arange(round(2.7 * RATE)) / RATE
x = band(clip(turbine, 12, 2.7, .7), 45, 2200) * np.exp(-t * 1.9) * .5
layer(x, band(clip(blast, 0, .45, .7), 38, 240), .025, 1.7)
layer(x, band(clip(blast, 0, .3, .9), 38, 380), .14, .65)
layer(x, band(clip(missile, .8, 1.7), 250, 3000), .4, .55)
save("vtol-touchdown.wav", x, .13)

# User-selected Missile firing fl, preserving its recorded pitch and tail.
save("manpad-launch.wav", read(SRC / "manpad.mp3"), .20)
t = np.arange(5 * RATE) / RATE
x = clip(turbine, 29, 5) * (.025 + .65 * np.exp(-((t - 4.55) / .7) ** 2))
save("manpad-incoming.wav", band(x, 100, 4800), .13)
save("missile-release.ogg", fit(missile, 2.5), .18)
save("rocket-release.ogg", fit(missile, 1.35), .17)
save("missile-impact.ogg", fit(blast, 1.8), .18)
save("rocket-impact.ogg", fit(blast, 1.05), .17)
save("gau-ground.ogg", band(gau, 60, 6800), .20)
save("gau-cockpit.ogg", band(gau_cabin, 80, 4400), .15)
save("gau-impact.ogg", band(gau_hit, 100, 9500), .13)

# Physical feedback instead of stock chimes, radar pings and alarm beeps.
x = np.zeros(round(1.4 * RATE))
for when in (0, .22, .44):
    layer(x, band(clip(blast, 0, .12, 1.4), 300, 6500), when, .65)
    layer(x, band(clip(missile, .8, .55, 1.4), 700, 6500), when, .4)
save("countermeasures.ogg", x, .13)
x = np.zeros(round(2.2 * RATE))
layer(x, band(blast, 40, 3400), gain=.9)
layer(x, band(clip(mechanical, .1, .65, .65), 80, 1800), .18, .32)
save("airframe-hit.ogg", x, .16)
save("airburst.ogg", band(fit(blast, 1.9), 45, 1600), .10)
save("pressure-vent.ogg", band(clip(missile, .65, .9), 700, 5000), .07)
save("control-servo.ogg", band(clip(mechanical, .10, .55, .9), 160, 1900), .055)
save("ejection.ogg", fit(missile, .85), .18)

validation = SRC / "mix-validation.json"
previous_stats = json.loads(validation.read_text()) if (FLIGHT_ONLY or MANPAD_ONLY) and validation.exists() else {}
validation.write_text(json.dumps(previous_stats | STATS, indent=2) + "\n")
for name, stats in STATS.items():
    print(name, stats)
# A review reel, never shipped as a game asset.
parts = []
preview_files = ("manpad-lock.wav", "manpad-launch.wav") if MANPAD_ONLY else ("jet-pass.ogg", "jet-pass-high.ogg", "jet-exterior.ogg", "vtol-takeoff.wav") if FLIGHT_ONLY else (
    "manpad-lock.wav", "manpad-launch.wav", "jet-pass.ogg", "jet-pass-high.ogg", "gau-ground.ogg",
    "rocket-release.ogg", "vtol-takeoff.wav", "vtol-touchdown.wav")
for name in preview_files:
    parts.extend((read(OUT / name), np.zeros(round(.6 * RATE))))
preview_name = "manpad-audio-preview.wav" if MANPAD_ONLY else "fighter-flight-audio-preview.wav" if FLIGHT_ONLY else "fighter-audio-preview.wav"
sf.write(SRC / preview_name, np.concatenate(parts), RATE, subtype="PCM_16")
