"""Build the three airframe sound sets and common fighter operation cues.

Requires numpy, soundfile and PyYAML. Pass --sources pointing at the recordings listed
in airframe-audio-sources.json, or --fetch to download their public HQ previews.
Source checksums are verified before rendering. Only mono Ogg mixes ship.
"""
import argparse
import hashlib
import json
from pathlib import Path
import urllib.request

import numpy as np
import soundfile as sf

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Content.CMU/Resources/Audio/CMU14/Fighter"
RATE = 44100
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--sources", type=Path, default=ROOT / "bin/FighterAirframeSources")
parser.add_argument("--fetch", action="store_true")
args = parser.parse_args()
manifest = json.loads(Path(__file__).with_name("airframe-audio-sources.json").read_text())
args.sources.mkdir(parents=True, exist_ok=True)
recordings = {}
stats = {}
credits = {}

for name, entry in manifest.items():
    path = args.sources / (name + ".mp3")
    if args.fetch and not path.exists():
        path.write_bytes(urllib.request.urlopen(entry["download"], timeout=30).read())
    if hashlib.sha256(path.read_bytes()).hexdigest() != entry["sha256"]:
        raise ValueError(f"Source checksum changed: {name}")
    samples, rate = sf.read(path, always_2d=True)
    samples = samples.mean(axis=1)
    recordings[name] = np.interp(np.arange(round(len(samples) / rate * RATE)) / RATE,
                                  np.arange(len(samples)) / rate, samples)


def smooth(x):
    x = np.clip(x, 0, 1)
    return x * x * (3 - 2 * x)


def band(x, low=45, high=9000):
    f = np.fft.rfftfreq(len(x), 1 / RATE)
    gain = (f / low) ** 2 / (1 + (f / low) ** 2) / (1 + (f / high) ** 6)
    return np.fft.irfft(np.fft.rfft(x - x.mean()) * gain, len(x))


def excerpt(name, start, seconds, pitch=1):
    x = recordings[name]
    return np.interp(start + np.arange(round(seconds * RATE)) / RATE * pitch,
                     np.arange(len(x)) / RATE, x, left=0, right=0)


def level(x, rms=.15):
    return x * rms / max(1e-7, np.sqrt(np.mean(x * x)))


def fade(x, attack=.02, release=.08):
    t = np.arange(len(x)) / RATE
    return x * smooth(t / attack) * smooth((len(x) / RATE - t) / release)


def save(name, x, sources, loop=False, rms=.15):
    x = level(band(x, 35, 11000), rms)
    if loop:
        n = round(.25 * RATE)
        w = smooth(np.linspace(0, 1, n))
        x[-n:] = x[-n:] * (1 - w) + x[:n] * w
        x = x[n:]
    else:
        x = fade(x)
    x *= min(1, .83 / max(1e-7, np.max(np.abs(x))))
    path = OUT / name
    path.parent.mkdir(parents=True, exist_ok=True)
    sf.write(path, x, RATE, subtype="PCM_16" if name.endswith(".wav") else "VORBIS")
    decoded, rate = sf.read(path)
    assert np.all(np.isfinite(decoded)) and np.max(np.abs(decoded)) < .99
    stats[name] = dict(seconds=round(len(decoded) / rate, 3), peak=round(float(np.max(np.abs(decoded))), 3), loop=loop)
    for source in sources:
        credits.setdefault(source, []).append(name)


# Separate source engines, pitch and spectral balances give each airframe an
# identity. Operations retain that identity instead of switching recordings.
profiles = {
    "original": ("cf18-climb", 12, "f15-start", 18, 1.0, 7000, 6, 6),
    "upp": ("f16-takeoff", 14, "j79-start-stop", 65, .84, 6200, 7, 6.5),
    "swept": ("typhoon-pass", 7, "f15-start", 22, 1.14, 9800, 5.5, 5.5),
}
for variant, (roar, at, turbine, idle_at, pitch, high, takeoff, landing) in profiles.items():
    sources = [roar, turbine]

    def engine(seconds, power=.6):
        body = level(band(excerpt(roar, at, seconds, pitch), 55, high))
        spool = level(band(excerpt(turbine, idle_at, seconds, pitch), 160, high * .65))
        return body * power + spool * (1 - power) * .7

    def output(cue, x, **kwargs):
        save(f"{variant}/{cue}.ogg", x, sources, **kwargs)

    output("idle", band(engine(6.25, .15), 110, 3000), loop=True, rms=.10)
    output("taxi", engine(6.25, .38), loop=True, rms=.12)
    output("exterior", engine(8.25, .85), loop=True)
    output("cockpit", band(engine(8.25), 60, 1600), loop=True, rms=.09)
    t = np.arange(round(takeoff * RATE)) / RATE
    output("takeoff", engine(takeoff, .8) * (.12 + .88 * smooth(t / (takeoff * .65))) *
           smooth((takeoff - t) / .55), rms=.19)
    t = np.arange(round(landing * RATE)) / RATE
    output("landing", engine(landing, .55) * smooth(t / .45) * (1 - .8 * smooth(t / landing)), rms=.15)
    output("startup", fade(engine(3.4, .25), 2.5, .15), rms=.12)
    if variant == "upp":
        output("shutdown", fade(excerpt(turbine, 104, 4.5, 4), .08, 1.3), rms=.12)
    else:
        output("shutdown", fade(engine(4.5, .15), .1, 4.3), rms=.10)
    pass_sound = fade(excerpt(roar, max(0, at - 3), 8, pitch), 1.5, 2.6)
    output("pass", pass_sound, rms=.20)
    output("pass-high", band(pass_sound, 70, 2200), rms=.12)
    # Rubber scrub and suspension thump, with the variant's engine behind it.
    rng = np.random.default_rng(61)
    t = np.arange(round(.9 * RATE)) / RATE
    wheels = band(rng.normal(0, 1, len(t)), 400, 1800) * np.exp(-t * 9)
    thump = np.sin(2 * np.pi * (70 * t - 15 * t * t)) * np.exp(-t * 13)
    output("touchdown", wheels * .18 + thump * .4 + engine(.9, .2) * .2, rms=.13)

cannon = excerpt("cannon", .1, 1.2)
save("gau-ground.ogg", cannon, ["cannon"], rms=.20)
save("gau-cockpit.ogg", band(cannon, 100, 2400), ["cannon"], rms=.13)
save("gau-impact.ogg", fade(excerpt("cannon", .3, .18), .003, .12), ["cannon"], rms=.16)
rocket = excerpt("rocket-launch", .05, 1.8)
save("rocket-release.ogg", rocket, ["rocket-launch"], rms=.17)
save("missile-release.ogg", excerpt("rocket-launch", .05, 2.2, .83), ["rocket-launch"], rms=.18)
blast = recordings["impact-whoosh"]
# Start on the actual transient, excluding the source's incoming whistle.
peak = np.argmax(np.convolve(blast * blast, np.ones(2205) / 2205, mode="same")) / RATE
for name, duration, pitch in [("missile-impact", 3.5, .88), ("rocket-impact", 2.2, 1.08), ("airburst", 1.6, 1.2), ("airframe-hit", .65, .78)]:
    save(name + ".ogg", excerpt("impact-whoosh", max(0, peak - .07), duration, pitch), ["impact-whoosh"], rms=.18)
save("ejection.ogg", fade(excerpt("rocket-launch", .1, .85, 1.4), .01, .4), ["rocket-launch"])
save("countermeasures.ogg", fade(excerpt("rocket-launch", .15, .45, 1.7), .01, .3), ["rocket-launch"], rms=.13)
save("pressure-vent.ogg", fade(band(excerpt("j79-start-stop", 111, 1.3), 900, 7000), .06, .7), ["j79-start-stop"], rms=.10)
# Original short physical control cues and an unmistakable incoming pulse.
rng = np.random.default_rng(20261101)
for name, seconds in [("control-servo.ogg", .45), ("control-click.ogg", .09), ("strike-warning.ogg", .92), ("manpad-incoming.wav", 1.8)]:
    t = np.arange(round(seconds * RATE)) / RATE
    if "warning" in name or "incoming" in name:
        envelope = (.5 + .5 * np.sin(2 * np.pi * 5 * t)) ** 4
        x = (np.sin(2 * np.pi * 720 * t) + .3 * np.sin(2 * np.pi * 1440 * t)) * envelope
    else:
        x = band(rng.normal(0, 1, len(t)), 300, 3200) * np.exp(-t * 18)
        x += .2 * np.sin(2 * np.pi * (180 * t + 70 * t * t)) * smooth((seconds - t) / seconds)
    save(name, x, ["CMU"], rms=.09)

# Keep unrelated/legacy assets' provenance intact while replacing credits for
# the newly rendered common cues. Variant directories have their own metadata.
import yaml
attribution = OUT / "attributions.yml"
existing = yaml.safe_load(attribution.read_text())
for entry in existing:
    entry["files"] = [f for f in entry["files"] if f not in stats]
existing = [entry for entry in existing if entry["files"]]
by_directory = {"": existing}
for source, files in credits.items():
    entry = manifest.get(source)
    for directory in {str(Path(f).parent).replace(".", "") for f in files}:
        names = [Path(f).name for f in files if str(Path(f).parent).replace(".", "") == directory]
        license_id = "CC0-1.0" if entry is None or "zero" in entry["license"] else "CC-BY-4.0"
        by_directory.setdefault(directory, []).append(dict(files=names, license=license_id,
            copyright=(f"{entry['author']}; public HQ recording preview. " if entry else "Original CMU synthesis. ") +
                "CMU edits: excerpts, mono, filtering, pitch, layering, envelopes, crossfades and level matching. Rebuild with Tools/fighter/generate-airframe-audio.py.",
            source=entry["source"] if entry else "https://github.com/AU-14/ColonialMarinesUniverse"))
for directory, entries in by_directory.items():
    (OUT / directory / "attributions.yml").write_text(yaml.safe_dump(entries, sort_keys=False, width=130), encoding="utf-8", newline="\n")
(args.sources / "render-stats.json").write_text(json.dumps(stats, indent=2) + "\n")
print(f"Rendered and decoded {len(stats)} cues; all finite, mono and below full scale.")
