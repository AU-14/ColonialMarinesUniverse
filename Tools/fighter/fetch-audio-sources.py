"""Fetch the CC0 source previews used by generate-fighter-audio.py.

Public HQ previews are used because original Freesound downloads require login.
Sources remain outside game resources; license/provenance and SHA256 are saved
beside them. No Sonniss audio is redistributed in the open asset tree.
"""
from pathlib import Path
import hashlib
import json
import re
import urllib.request

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "bin/FighterAudioSources"
SOURCES = {
    "turbine": ("qubodup", 205581, "Jet Turbine Noise.flac"),
    "flyby": ("Ears68", 147819, "F-18-flybys--1m3s.wav"),
    "f18-low": ("Ears68", 150328, "F-18 fly-over-and-circle above-DR070cut.wav"),
    "f16-cruise": ("Rudmer_Rotteveel", 343746, "2 Royal Dutch Airforce F-16 Fighting Falcons flyby"),
    "f16-startup": ("ikbenraar", 322178, "F16 fighter jet start up.aif"),
    "missile": ("Jacco18", 428360, "Missile"),
    "manpad": ("NHMWretched", 151858, "Missile firing fl.mp3"),
    "blast": ("qubodup", 855898, "Fire Explosion"),
}

OUT.mkdir(parents=True, exist_ok=True)
manifest = {}
for name, (author, sound_id, title) in SOURCES.items():
    page = f"https://freesound.org/people/{author}/sounds/{sound_id}/"
    html = urllib.request.urlopen(page, timeout=30).read().decode()
    if "creativecommons.org/publicdomain/zero/1.0" not in html:
        raise RuntimeError(f"CC0 license not found: {page}")
    urls = re.findall(r'https://cdn\.freesound\.org/previews/[^\s"<>]+-hq\.mp3', html)
    if not urls:
        raise RuntimeError(f"Public HQ preview not found: {page}")
    data = urllib.request.urlopen(urls[0], timeout=30).read()
    (OUT / f"{name}.mp3").write_bytes(data)
    manifest[name] = dict(author=author, title=title, source=page, download=urls[0],
                          license="CC0-1.0", sha256=hashlib.sha256(data).hexdigest())
    print(f"{name}: {len(data)} bytes, CC0 verified")
(OUT / "sources.json").write_text(json.dumps(manifest, indent=2) + "\n")
