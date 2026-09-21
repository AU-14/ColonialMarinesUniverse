#!/usr/bin/env python3
"""Reproducible import of CM-SS13's UD6 artwork and map source.

Requires Pillow, PyYAML and SoundFile. Source is pinned; never import a moving PR head.
"""

from __future__ import annotations

import argparse
import base64
import copy
from collections import Counter
from concurrent.futures import ThreadPoolExecutor
import hashlib
import json
import math
from pathlib import Path
import re
import shutil
import struct
import urllib.request

from PIL import Image
import yaml

REVISION = "0d711eddc539ea7ac67cf6b5869a08fa33dc9391"
REPOSITORY = "PoltavskaPraca/cmss13-praca"
PR = "https://github.com/cmss13-devs/cmss13/pull/12866"
ROOT = Path(__file__).resolve().parents[2]
RESOURCES = ROOT / "Content.CMU/Resources"
MANIFEST = Path(__file__).parent / "source_manifest.json"
AUDIO_FILES = (
    "freesoundstock_step_ladder.ogg", "mountain852_climbing_ladder_human.ogg",
    "mountain852_climbing_ladder_human_after.ogg", "mountain852_climbing_ladder_initial.ogg",
    "nightcustard_engine_whirring.ogg", "nightcustard_motor_whirring.ogg",
    "omaha_ramp.ogg", "omaha_ramp_contributors.txt",
)


def split_top(text: str, delimiter: str) -> list[str]:
    result, start, depth, quote, escape = [], 0, 0, None, False
    for index, char in enumerate(text):
        if quote:
            if escape:
                escape = False
            elif char == "\\":
                escape = True
            elif char == quote:
                quote = None
        elif char in "\"'":
            quote = char
        elif char in "({[":
            depth += 1
        elif char in ")}]":
            depth -= 1
        elif char == delimiter and depth == 0:
            result.append(text[start:index].strip())
            start = index + 1
    result.append(text[start:].strip())
    return [part for part in result if part]


def value(text: str):
    text = text.strip().rstrip(";")
    if text.startswith(('"', "'")) and text.endswith(text[0]):
        return text[1:-1].replace('\\improper ', '').replace('\\proper ', '')
    if text in ("TRUE", "FALSE"):
        return text == "TRUE"
    try:
        return float(text) if "." in text else int(text)
    except ValueError:
        return text


def parse_atom(text: str) -> dict:
    path, _, fields = text.partition("{")
    props = {}
    if fields:
        for field in split_top(fields.rsplit("}", 1)[0], ";"):
            name, _, assigned = field.partition("=")
            props[name.strip()] = value(assigned)
    return {"path": path.strip(), "fields": props}


def parse_dmm(path: Path) -> dict:
    text = path.read_text(encoding="utf-8-sig")
    palette = {
        key: [parse_atom(atom) for atom in split_top(body, ",")]
        for key, body in re.findall(r'^"([^"\n]+)" = \(\n(.*?)\)\s*$', text, re.M | re.S)
    }
    if not palette:
        raise ValueError(f"No DMM palette in {path}")
    key_size = len(next(iter(palette)))
    cells = []
    for x, y, z, body in re.findall(r'\((\d+),(\d+),(\d+)\) = \{"\n(.*?)\n"\}', text, re.S):
        lines = body.splitlines()
        for row, line in enumerate(lines):
            if len(line) % key_size:
                raise ValueError(f"Invalid DMM row: {line}")
            for column in range(0, len(line), key_size):
                key = line[column:column + key_size]
                cells.append({"x": int(x) + column // key_size,
                              "y": int(y) + len(lines) - row - 1,
                              "z": int(z), "atoms": palette[key]})
    return {"palette": palette, "cells": cells}


def parse_definitions(source: Path) -> dict:
    definitions = {}
    manifest = json.loads(MANIFEST.read_text()) if MANIFEST.exists() else {}
    files = manifest.get("inputs", {})
    paths = [source / name for name in files if name.startswith("code/") and name.endswith(".dm")]
    for path in sorted(paths or source.glob("code/**/*.dm")):
        current = None
        for line in path.read_text(encoding="utf-8-sig").splitlines():
            if line.startswith("/"):
                candidate = line.split("//")[0].strip()
                current = candidate if re.fullmatch(r"/[\w/]+", candidate) else None
            elif current:
                match = re.match(r"^\t(\w+)\s*=\s*(.+?)(?:\s+//.*)?$", line)
                if match:
                    definitions.setdefault(current, {})[match[1]] = value(match[2])
    return definitions


def resolve(path: str, definitions: dict, seen=None) -> dict:
    seen = set() if seen is None else seen
    if not path or path in seen:
        return {}
    seen.add(path)
    own = definitions.get(path, {})
    parent = own.get("parent_type", path.rpartition("/")[0])
    return resolve(parent, definitions, seen) | own


def inventory(source: Path):
    definitions = parse_definitions(source)
    for variant in ("omaha", "midway"):
        data = parse_dmm(source / f"maps/shuttles/dropship_{variant}.dmm")
        counts = Counter(atom["path"] for cell in data["cells"] for atom in cell["atoms"])
        print(variant, "cells", len(data["cells"]), "types", len(counts), "levels",
              sorted({cell["z"] for cell in data["cells"]}))
        for path, count in sorted(counts.items()):
            if "/shuttle/part/" in path or path.startswith(("/turf/", "/area/")):
                continue
            props = resolve(path, definitions)
            print(count, path, props.get("icon"), props.get("icon_state"))


def dmi_states(path: Path):
    sheet = Image.open(path).convert("RGBA")
    metadata = Image.open(path).info["Description"]
    width = int(re.search(r"\bwidth = (\d+)", metadata)[1])
    height = int(re.search(r"\bheight = (\d+)", metadata)[1])
    columns, index = sheet.width // width, 0
    states = {}
    for name, block in re.findall(r'state = "([^"]*)"(.*?)(?=\nstate =|\n# END)', metadata, re.S):
        dirs = int(re.search(r"dirs = (\d+)", block)[1])
        frames = int(re.search(r"frames = (\d+)", block)[1])
        delay = re.search(r"delay = ([\d.,]+)", block)
        delays = [float(n) / 10 for n in delay[1].split(",")] if delay else [0.1] * frames
        # BYOND accepts trailing delay entries left behind after frame deletion.
        delays = delays[:frames]
        if len(delays) != frames:
            raise ValueError(f"Invalid animation in {path}: {name}")
        images = []
        for frame in range(frames * dirs):
            x, y = (index + frame) % columns * width, (index + frame) // columns * height
            images.append(sheet.crop((x, y, x + width, y + height)))
        index += frames * dirs
        if name in states:
            raise ValueError(f"Duplicate DMI state in {path}: {name}")
        states[name] = {"directions": dirs, "frames": frames, "delays": delays, "images": images}
    return width, height, states


def rsi_path(icon: str) -> str:
    return "CMU14/Dropships/Mohawk/" + icon.removeprefix("icons/").removesuffix(".dmi") + ".rsi"


def rsi_state(name: str) -> str:
    return name or "default"


def import_art(source: Path):
    import soundfile as sf

    manifest = {"pull_request": PR, "repository": REPOSITORY, "revision": REVISION, "files": {}}
    # Compact engine faces used by the Mohawk are stored outside its ship folders.
    equipment_states = {"icons/obj/structures/props/dropship/dropship_equipment64.dmi":
                        ("fuel_enhancer_omaha", "cooling_system_omaha")}
    state_count = 0
    for path in sorted(source.glob("icons/**/*.dmi")):
        relative = path.relative_to(source).as_posix()
        selected_states = equipment_states.get(relative)
        if selected_states is None and not any(f"/{variant}/" in relative for variant in ("omaha", "midway", "mohawk_navy")):
            continue
        width, height, states = dmi_states(path)
        if selected_states is not None:
            states = {name: states[name] for name in selected_states}
        target = RESOURCES / "Textures" / rsi_path(relative)
        target.mkdir(parents=True, exist_ok=True)
        meta = {"version": 1, "license": "CC-BY-SA-3.0",
                "copyright": f"CM-SS13 contributors: thwomper, PoltavskaPraca, Steelpoint. {PR}; {REVISION}.",
                "size": {"x": width, "y": height},
                "states": []}
        for name, state in states.items():
            name = rsi_state(name)
            if any(char in name for char in '<>:"/\\|?*'):
                raise ValueError(f"Non-portable state name: {name}")
            count = state["directions"] * state["frames"]
            cols = min(count, 8)
            output = Image.new("RGBA", (cols * width, math.ceil(count / cols) * height))
            # DMI frames are frame-major; RSI animations are direction-major.
            for direction in range(state["directions"]):
                for frame in range(state["frames"]):
                    i = direction * state["frames"] + frame
                    output.paste(state["images"][frame * state["directions"] + direction],
                                 (i % cols * width, i // cols * height))
            output.save(target / f"{name}.png")
            entry = {"name": name, "directions": state["directions"]}
            if state["frames"] > 1:
                entry["delays"] = [state["delays"] for _ in range(state["directions"])]
            meta["states"].append(entry)
            state_count += 1
        (target / "meta.json").write_text(json.dumps(meta, indent=4) + "\n", encoding="utf-8", newline="\n")
        manifest["files"][relative] = {"sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
                                      "states": len(states), "rsi": rsi_path(relative)}
    audio_target = RESOURCES / "Audio/CMU14/Dropships/Mohawk"
    audio_target.mkdir(parents=True, exist_ok=True)
    for path in [source / "sound/machines" / name for name in AUDIO_FILES]:
        entry = {"sha256": hashlib.sha256(path.read_bytes()).hexdigest()}
        target = audio_target / path.name
        if path.suffix == ".ogg" and sf.info(path).channels > 1:
            # Robust positional audio requires mono. Keep the source rate and
            # sample count while averaging channels instead of discarding one.
            samples, sample_rate = sf.read(path, always_2d=True)
            sf.write(target, samples.mean(axis=1), sample_rate, format="OGG", subtype="VORBIS")
            entry["conversion"] = "downmixed to mono for positional audio"
        else:
            shutil.copy2(path, target)
        manifest["files"][path.relative_to(source).as_posix()] = entry
    attributions = [{"files": [name], "license": "CC-BY-SA-3.0",
                     "copyright": "CM-SS13 contributors; freesoundstock, mountain852, nightcustard; "
                     "see omaha_ramp_contributors.txt for the ramp mix. Stereo sources downmixed to mono for positional playback.",
                     "source": f"https://github.com/{REPOSITORY}/blob/{REVISION}/sound/machines/{name}"}
                    for name in AUDIO_FILES if name.endswith(".ogg")]
    (audio_target / "attributions.yml").write_text(yaml.safe_dump(attributions, sort_keys=False), encoding="utf-8", newline="\n")
    existing = json.loads(MANIFEST.read_text()) if MANIFEST.exists() else {}
    names = set(existing.get("inputs", {}))
    if not names:
        names.update(path.relative_to(source).as_posix() for path in source.glob("code/**/*.dm"))
        names.update(f"maps/shuttles/dropship_{variant}.dmm" for variant in ("omaha", "midway"))
        names.update(manifest["files"])
    manifest["inputs"] = {name: hashlib.sha256((source / name).read_bytes()).hexdigest() for name in sorted(names)}
    MANIFEST.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(f"Imported {state_count} sprite states and {len(list(audio_target.glob('*.ogg')))} sounds.")


class MapWriter:
    def __init__(self, name: str, primary: bool, resource_name: str = ""):
        self.name, self.primary = name, primary
        self.resource_name = resource_name
        self.tiles, self.entities = {}, []

    def add(self, proto: str, x: float, y: float, components=None):
        components = components or []
        transform = {"type": "Transform", "parent": 1, "pos": f"{x:g},{y:g}"}
        for component in components:
            if component["type"] == "Transform":
                transform.update(component)
        self.entities.append({"proto": proto, "entities": [{"uid": len(self.entities) + 2,
            "components": [transform] + [c for c in components if c["type"] != "Transform"]}]})

    def write(self, path: Path, deployment=False):
        tile_names = ["Space"] + sorted(set(self.tiles.values()) - {"Space"})
        chunks = {}
        for x, y in self.tiles:
            chunks.setdefault((x // 16, y // 16), bytearray(16 * 16 * 7))
            struct.pack_into("<iBBB", chunks[x // 16, y // 16], ((y % 16) * 16 + x % 16) * 7,
                             tile_names.index(self.tiles[x, y]), 0, 0, 0)
        components = [{"type": "MetaData", "name": self.name},
                      {"type": "Transform", "parent": "invalid"},
                      {"type": "MapGrid", "canSplit": False, "chunks": {
                          f"{x},{y}": {"ind": f"{x},{y}", "tiles": base64.b64encode(data).decode(), "version": 7}
                          for (x, y), data in sorted(chunks.items())}},
                      {"type": "Physics", "bodyType": "Dynamic" if self.primary else "Kinematic",
                       "fixedRotation": not self.primary},
                      {"type": "Fixtures", "fixtures": {}}, {"type": "Gravity", "enabled": True}]
        if self.primary:
            variant = self.resource_name
            # Current CMU maps require authored cabin air; uninitialized tiles
            # otherwise begin in vacuum. Exterior servicing decks stay outside.
            air_chunks = {}
            for x, y in self.tiles:
                chunk = (x // 4, y // 4)
                air_chunks[chunk] = air_chunks.get(chunk, 0) | (1 << (x % 4 + (y % 4) * 4))
            components.append({"type": "GridAtmosphere", "version": 2, "data": {
                "tiles": {f"{x},{y}": {0: mask} for (x, y), mask in sorted(air_chunks.items())},
                "uniqueMixes": [{"volume": 2500, "temperature": 293.15,
                                 "moles": [21.824879, 82.10312] + [0] * 10}],
                "chunkSize": 4}})
            components += [{"type": "Shuttle"},
                           {"type": "Dropship", "tacticalLandFootprint": "17,24"},
                           {"type": "MultiDeckDropship", "landingOffset": 1, "exteriorDecks": [-1], "deckPaths": {
                               -1: f"/Maps/CMU14/ShuttlesDropships/Mohawk/{variant}_lower.yml",
                               1: f"/Maps/CMU14/ShuttlesDropships/Mohawk/{variant}_upper.yml"}},
                           {"type": "ShipFaction", "faction": "govfor"}, {"type": "MohawkMechanisms"}]
        grid = {"proto": "", "entities": [{"uid": 1, "components": components}]}
        result = {"meta": {"format": 7, "category": "Grid", "engineVersion": "264.0.0",
                           "forkId": "", "forkVersion": "", "time": "2026-09-18T00:00:00",
                           "entityCount": len(self.entities) + 1},
                  "maps": [], "grids": [1], "orphans": [1], "nullspace": [],
                  "tilemap": dict(enumerate(tile_names)), "entities": [grid] + self.entities}
        if deployment:
            result = copy.deepcopy(result)
            map_uid = len(self.entities) + 2
            result["meta"]["category"] = "Map"
            result["meta"]["entityCount"] += 1
            result["maps"] = [map_uid]
            result["orphans"] = []
            result["entities"][0]["entities"][0]["components"][1]["parent"] = map_uid
            result["entities"].append({"proto": "", "entities": [{"uid": map_uid, "components": [
                {"type": "Map"}, {"type": "Transform", "parent": "invalid"}]}]})
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text("# Generated by Tools/mohawk/import_mohawk.py; source: " + PR + "\n" +
                        yaml.safe_dump(result, sort_keys=False, width=120), encoding="utf-8", newline="\n")


class MapImporter:
    def __init__(self, source: Path):
        self.source = source
        self.definitions = parse_definitions(source)
        self.prototypes, self.tiles, self.audit = {}, {}, {}
        self.paint = ""
        self.paint_fallbacks = set()

    def painted_icon(self, icon: str, state: str, required=()) -> str:
        if self.paint != "navy":
            return icon
        painted = icon.replace("/omaha/", "/mohawk_navy/").replace("/midway/", "/mohawk_navy/")
        if painted == icon:
            return icon
        target = RESOURCES / "Textures" / rsi_path(painted) / "meta.json"
        if target.exists():
            meta = json.loads(target.read_text())
            states = {s["name"] for s in meta["states"]}
            if all(rsi_state(s) in states for s in (state, *required)):
                return painted
        self.paint_fallbacks.add(f"{icon}:{state}")
        return icon

    def prefix(self, variant: str) -> str:
        return "CMUMohawk" + variant.title() + self.paint.title()

    def tile(self, icon: str, state: str, direction=2):
        icon = self.painted_icon(icon, state)
        name = "CMUMohawkTile" + hashlib.sha256(f"{icon}:{state}:{direction}".encode()).hexdigest()[:12]
        target = RESOURCES / "Textures" / rsi_path(icon) / (rsi_state(state) + ".png")
        if not target.exists():
            raise ValueError(f"Missing tile art: {icon}:{state}")
        # Tile atlases accept a single 32px-wide strip, not a directional RSI sheet.
        meta = json.loads((target.parent / "meta.json").read_text())
        entry = next(s for s in meta["states"] if s["name"] == rsi_state(state))
        if meta["size"] != {"x": 32, "y": 32}:
            raise ValueError(f"Non-tile-sized source: {icon}:{state}")
        direction_index = {2: 0, 1: 1, 4: 2, 8: 3}.get(direction, 0) if entry["directions"] > 1 else 0
        frames = len(entry.get("delays", [[0]])[0])
        sheet = Image.open(target)
        index = direction_index * frames
        col, row = index % (sheet.width // 32), index // (sheet.width // 32)
        target = RESOURCES / "Textures/CMU14/Dropships/Mohawk/tiles" / (name + ".png")
        target.parent.mkdir(parents=True, exist_ok=True)
        sheet.crop((col * 32, row * 32, col * 32 + 32, row * 32 + 32)).save(target)
        self.tiles[name] = {"type": "tile", "parent": "CMShuttleTileBase", "id": name,
                            "name": "cmu-mohawk-roof" if "mohawk-top-view" in icon else
                                "cmu-mohawk-ramp" if icon.endswith("/ramp.dmi") else "cmu-mohawk-floor",
                            "sprite": "/Textures/" +
                            target.relative_to(RESOURCES / "Textures").as_posix()}
        return name

    def entity(self, atom: dict, variant: str, extra=None):
        path = atom["path"]
        props = resolve(path, self.definitions) | atom["fields"]
        parent = "CMUMohawkDecoration"
        components = []
        direction = props.get("dir", 2)
        direction = {"SOUTH": 2, "NORTH": 1, "EAST": 4, "WEST": 8}.get(direction, direction)
        rot = {2: 0, 1: math.pi, 4: math.pi / 2, 8: -math.pi / 2}.get(direction, 0)
        seat = "/bed/chair/" in path
        gear = "landing_gear_big" in path.split("/")
        gear_hatch = "landing_hatch_big" in path.split("/")
        camera = "/camera/autoname/" in path
        light = path.startswith("/obj/structure/machinery/light")
        cabinet = path.endswith("/medical/wall_med")

        def local_offset(x, y):
            # Source pixel offsets are in map axes, independent of facing.
            return {2: (x, y), 1: (-x, -y), 4: (y, -x), 8: (-y, x)}.get(direction, (x, y))
        if path.startswith("/turf/closed/") or props.get("density") is True:
            parent = "CMBaseWallInvincible"
        if "/shuttle/part/" in path and props.get("density") is not True:
            parent = "CMUMohawkDecoration"
        if seat:
            parent = "CMSeatPilot" if "pilot" in path else "CMSeatPassenger"
            # BYOND changes the passenger's pixels, not their physical location.
            # Several south-facing seats have offsets larger than half a tile.
            bx, by = local_offset((props.get("pixel_x", 0) or props.get("buckle_offset_x", 0)) / 32,
                                  (props.get("pixel_y", 0) or props.get("buckle_offset_y", 0)) / 32)
            components.extend([
                {"type": "Transform", "anchored": True, "rot": f"{rot} rad"},
                {"type": "Strap", "buckleOffset": "0,0"},
                {"type": "MohawkSeat", "visualOffset": f"{bx:g},{by:g}"},
                {"type": "Appearance"}])
            if path.endswith("/midway_gunner"):
                components.append({"type": "MohawkGunnerySeat"})
        elif "/computer/shuttle/" in path:
            parent = "CMComputerDropshipNavigation"
        elif "/computer/dropship_weapons/" in path or "/gunnery" in path:
            parent = "CMComputerDropshipWeapons"
            if "/gunnery" in path:
                components.append({"type": "DropshipTerminalWeapons", "gunnery": True})
        elif "/computer/cameras/" in path:
            parent = "CMComputerDropshipCamerasAlamo"
            components.append({"type": "CameraNetworkReceiver", "networks": ["CMUMohawk" + variant.title()],
                               "supportedSources": "Rmc"})
        elif camera:
            parent = "CMUMohawkCamera"
            components.append({"type": "CameraNetworkMember", "networks": ["CMUMohawk" + variant.title()],
                               "sourceKinds": "Rmc"})
        elif "/computer/overwatch/" in path:
            parent = "RMCOverwatchConsoleGovfor"
        elif "/CICmap/" in path:
            parent = "CMUMohawkMapTable"
            components.append({"type": "TacticalMapComputer", "faction": "govfor"})
        elif cabinet:
            parent = "CMUMohawkMedicalCabinet"
        elif "/cm_vending/sorted/medical/" in path:
            raise ValueError(f"Unknown medical vendor: {path}")
        elif "/cm_vending/" in path:
            parent = "ColMarTechGuns"
        elif "/radio/intercom/" in path:
            parent = "CMUMohawkIntercom"
        elif light:
            parent = "CMUMohawkBlueLight" if "/blue" in path else "CMUMohawkLight"
        elif "/airlock/hatch/" in path:
            parent = "CMUMohawkDoor"
            location = "Cockpit" if "/cockpit/" in path else ("Port" if props.get("id") == "port_door" else "Starboard")
            components.append({"type": "Door", "location": location})
            if location == "Cockpit":
                components.append({"type": "AccessReader", "access": [["AU14AccessGovforPilot"]]})
        elif "/door_control/" in path:
            parent = "CMUMohawkControl"
            group = "Hatch" if "hatch_ladder" in path else ("Ramp" if "ramp" in path else
                    "Port" if "left" in path else "Starboard")
            components.append({"type": "MohawkControl", "group": group})
        elif "/ladder/multiz/dropship/" in path:
            parent = "CMUMohawkHatch"
        elif "/ramp_decals/" in path:
            components.append({"type": "MohawkRampEdging"})
        elif "/attach_point/weapon/" in path:
            locations = {"left_fore": "PortFore", "right_fore": "StarboardFore",
                         "left_wing": "PortWing", "right_wing": "StarboardWing"}
            return "CMUMohawkWeapon" + locations[path.rsplit("/", 1)[-1]]
        elif "/attach_point/electronics/" in path:
            return "CMUMohawkElectronics"
        elif "/attach_point/crew_weapon/" in path:
            return "CMUGunshipHardpointAttachmentPoint"
        elif "/attach_point/fuel/" in path:
            return "CMUMohawkEngine"

        mounted = camera or light or cabinet or "/radio/intercom/" in path
        if mounted or "/computer/" in path or "/CICmap/" in path or "/cm_vending/" in path:
            components.append({"type": "Transform", "anchored": True, "noRot": False,
                               "rot": f"{rot if camera or light else 0} rad"})
        if camera or light or cabinet:
            px, py = props.get("pixel_x", 0), props.get("pixel_y", 0)
            if light:
                # CM-SS13's set_pixel_location applies these after map loading.
                if direction == 1:
                    py = 19
                elif direction == 4:
                    px = 6
                elif direction == 8:
                    px = -4
            sx, sy = local_offset(px / 32, py / 32)
            components.append({"type": "Sprite", "offset": f"{sx:g},{sy:g}", "noRot": False})
            if light:
                components.append({"type": "PointLight", "offset": f"{sx:g},{sy + .35:g}"})

        if gear or gear_hatch:
            # Deployers do not go through the cabin's per-entity map rotation.
            components.append({"type": "Transform", "rot": f"{rot} rad", "anchored": gear})
            if gear:
                if direction not in (1, 2):
                    raise ValueError(f"Unsupported landing gear facing: {direction}")
                parent = "CMUMohawkLandingGearNorth" if direction == 1 else "CMUMohawkLandingGear"

        icon, state = props.get("icon"), props.get("icon_state")
        if icon:
            # Navy's older seat sheet lacks the passenger restraint state.
            # Keep the complete working seat sprite rather than losing it.
            required = (state + "_buckled",) if seat and state == "passenger_chair" else ()
            icon = self.painted_icon(icon, state or "", required)
        imported = icon and any(f"/{v}/" in icon for v in ("omaha", "midway", "mohawk_navy"))
        if imported:
            rsi = rsi_path(icon)
            state = rsi_state(state or "")
            if not (RESOURCES / "Textures" / rsi / (state + ".png")).exists():
                raise ValueError(f"Missing entity art {path}: {icon}:{state}")
            meta = json.loads((RESOURCES / "Textures" / rsi / "meta.json").read_text())
            # BYOND anchors multi-tile icons at bottom-left; Robust anchors their centre.
            sx = props.get("pixel_x", 0) / 32 + (meta["size"]["x"] - 32) / 64
            sy = props.get("pixel_y", 0) / 32 + (meta["size"]["y"] - 32) / 64
            if parent == "CMUMohawkControl" or seat or gear or gear_hatch:
                # BYOND pixel offsets are in map axes, even for directional
                # buttons. Robust rotates Sprite.offset with the entity.
                sx, sy = local_offset(sx, sy)
            sprite = {"type": "Sprite", "sprite": rsi, "offset": f"{sx:g},{sy:g}"}
            if seat:
                sprite["noRot"] = False
            if "/fuel_lines/" in path:
                # The underside must not cover the lowered ramp's floor art.
                sprite["drawdepth"] = "BelowFloor"
            if isinstance(props.get("alpha"), int):
                sprite["color"] = f"#ffffff{props['alpha']:02x}"
            if parent == "CMUMohawkDoor":
                sprite["layers"] = [{"state": "door_closed", "map": ["enum.DoorVisualLayers.Base"]},
                                    {"state": "door_closed", "visible": False, "map": ["enum.DoorVisualLayers.BaseUnlit"]},
                                    {"sprite": "_RMC14/Structures/Doors/Airlocks/personal_door.rsi", "state": "welded", "visible": False,
                                     "map": ["enum.WeldableLayers.BaseWelded"]},
                                    {"state": "door_locked", "visible": False, "map": ["enum.DoorVisualLayers.BaseBolted"]},
                                    {"state": "door_spark", "visible": False, "map": ["enum.DoorVisualLayers.BaseEmergencyAccess"]},
                                    {"sprite": "_RMC14/Structures/Doors/Airlocks/personal_door.rsi", "state": "panel_open", "visible": False,
                                     "map": ["enum.WiresVisualLayers.MaintenancePanel"]}]
            elif parent == "CMUMohawkHatch":
                sprite["layers"] = [{"state": state, "map": ["enum.MohawkVisuals.Layer"]}]
            elif seat:
                # The commander art has no buckled state in the source sheet.
                buckled = state + "_buckled"
                if not any(s["name"] == buckled for s in meta["states"]):
                    buckled = state
                sprite["layers"] = [{"state": state, "map": ["enum.MohawkVisuals.Layer"]}]
                components.append({"type": "GenericVisualizer", "visuals": {
                    "enum.StrapVisuals.State": {"enum.MohawkVisuals.Layer": {
                        "True": {"state": buckled}, "False": {"state": state}}}}})
            elif parent == "CMUMohawkIntercom":
                sprite["layers"] = [{"state": state, "map": ["enum.PowerDeviceVisualLayers.Powered"]}]
            else:
                sprite["layers"] = [{"state": state}]
            components.append(sprite)
            if parent == "CMBaseWallInvincible" or gear:
                components.append({"type": "Icon", "sprite": rsi, "state": state})
        elif parent in ("CMUMohawkDecoration", "CMBaseWallInvincible"):
            raise ValueError(f"Unmapped atom: {path}: {props}")
        if parent == "CMUMohawkDecoration" and not path.startswith((
                "/obj/structure/shuttle/part/", "/obj/structure/overwatch_dummy", "/obj/structure/dropship_vendor_dummy")):
            raise ValueError(f"Unknown behavior for {path}; assign an explicit CMU implementation")
        if parent == "CMBaseWallInvincible":
            components.append({"type": "Occluder", "enabled": bool(props.get("opacity", True))})
        if "/computer/" in path or "/CICmap/" in path:
            components.append({"type": "Physics", "canCollide": props.get("density", False) is True})
        if extra:
            components.extend(extra)
        key = json.dumps([path, props, extra], sort_keys=True)
        label = re.sub(r"[^a-zA-Z0-9]", "", path.rsplit("/", 1)[-1].title())
        name = self.prefix(variant) + label + hashlib.sha256(key.encode()).hexdigest()[:10]
        self.prototypes[name] = {"type": "entity", "parent": parent, "id": name,
                                 "name": self.entity_name(path, props, parent),
                                 "suffix": "Mohawk, " + variant.title() + (", Navy" if self.paint else ""), "components": components}
        return name

    @staticmethod
    def entity_name(path: str, props: dict, parent: str) -> str:
        names = {
            "CMUMohawkCamera": "dropship surveillance camera",
            "CMUMohawkIntercom": "dropship intercom",
            "CMUMohawkMedicalCabinet": "NanoMed medical cabinet",
            "CMUMohawkMapTable": "tactical map table",
            "CMUMohawkLight": "dropship light fixture",
            "CMUMohawkBlueLight": "blue dropship light fixture",
            "CMUMohawkLandingGear": "rear landing gear",
            "CMUMohawkLandingGearNorth": "forward landing gear",
            "CMUMohawkDoor": "cockpit hatch" if "/cockpit/" in path else
                ("port access hatch" if props.get("id") == "port_door" else "starboard access hatch"),
            "CMSeatPilot": "pilot seat" if "pilot" in path else "command seat",
            "CMSeatPassenger": "gunner seat" if "gunner" in path else "passenger seat",
            "CMComputerDropshipNavigation": "dropship navigation console",
            "CMComputerDropshipWeapons": "gunnery console" if "/gunnery" in path else "dropship weapons console",
            "CMComputerDropshipCamerasAlamo": "dropship camera console",
            "RMCOverwatchConsoleGovfor": "overwatch console",
            "ColMarTechGuns": "dropship weapons rack",
        }
        if parent in names:
            return names[parent]
        for fragment, label in (
            ("landing_hatch_big", "landing gear hatch"), ("/canopy", "cockpit canopy"),
            ("/fuel_lines/", "underside fuel lines"), ("/ramp_decals/", "boarding ramp edging"),
            ("/platform", "raised walkway"), ("/panel_half", "walkway panel"),
            ("/overwatch_dummy", "overwatch station"), ("/dropship_vendor_dummy", "weapons rack"),
            ("/transparent/", "outer hull plating"), ("/shuttle/part/", "dropship hull plating"),
            ("/turf/closed/", "dropship bulkhead"),
        ):
            if fragment in path:
                return label
        return str(props.get("name", "dropship fitting")).lower()

    def ramp_support(self, variant: str, stage: int, floor: dict, deployer: dict) -> str:
        if stage == 4:
            icon = resolve(deployer["item_to_deploy"], self.definitions)["icon"]
            state = "3,16"
            name = self.prefix(variant) + "RampBulkhead"
        else:
            icon = floor["icon"]
            state = floor["icon_state"] + "-low"
            name = self.prefix(variant) + "LoweredRamp" + floor["icon_state"].removeprefix("ramp-")
        icon = self.painted_icon(icon, state)
        sprite = rsi_path(icon)
        if not (RESOURCES / "Textures" / sprite / (state + ".png")).exists():
            raise ValueError(f"Missing lowered ramp art: {icon}:{state}")
        # Sprite is client-only: a server map's component overrides are discarded.
        # Each tile therefore needs its artwork in the replicated prototype ID.
        parent = "CMUMohawkRampBulkhead" if stage == 4 else "CMUMohawkRampStairs" if stage == 3 else "CMUMohawkRampSupport"
        self.prototypes[name] = {"type": "entity", "parent": parent, "id": name,
                                "name": "ramp bulkhead" if stage == 4 else "boarding ramp",
                                "suffix": "Mohawk, " + variant.title(), "components": [
                                    {"type": "Sprite", "sprite": sprite, "drawdepth": "FloorTiles", "layers": [
                                        {"state": state, "visible": False, "map": ["enum.MohawkVisuals.Layer"]}]}]}
        return name

    def convert(self, variant: str, paint: str = ""):
        self.paint = paint
        self.paint_fallbacks = set()
        resource_name = variant + ("_" + paint if paint else "")
        display_name = variant.title() + (" (Navy)" if paint else "")
        data = parse_dmm(self.source / f"maps/shuttles/dropship_{variant}.dmm")
        decks = {0: MapWriter(display_name, True, resource_name),
                 -1: MapWriter(display_name + " underside", False),
                 1: MapWriter(display_name + " upper hull", False)}
        outcomes = Counter()
        decks[0].add("WarpPoint", 0.5, 0.5, [
                     {"type": "MetaData", "name": f"{display_name} cabin"},
                     {"type": "Transform", "gridTraversal": False}])
        outcomes["cabin_warp_point"] += 1
        for cell in data["cells"]:
            # Source docking port is at (9, 13). Keep all decks in this coordinate frame.
            x, y = cell["x"] - 9, cell["y"] - 13
            for atom in cell["atoms"]:
                path, fields = atom["path"], atom["fields"]
                if "/airlock/hatch/side_hatch/" in path:
                    # Both draft maps omit the starboard hatch's direction;
                    # Omaha also assigns it the port ID. Wire and face each
                    # hatch according to the actual side of the cabin.
                    fields = fields | {"id": "port_door" if x < 0 else "starboard_door",
                                       "dir": 8 if x < 0 else 4}
                    atom = atom | {"fields": fields}
                props = resolve(path, self.definitions) | fields
                if path == "/obj/item/cpr_dummy":
                    outcomes["removed_training_dummy"] += 1
                    continue
                if path.startswith("/area/") or path in ("/turf/template_noop", "/turf/open/space"):
                    outcomes["metadata_or_empty"] += 1
                    continue
                if path.startswith("/obj/docking_port/"):
                    outcomes["flight_controller"] += 1
                    continue
                if path.startswith("/obj/deployer/"):
                    outcomes["generated_exterior"] += 1
                    if "/gibber" in path:
                        decks[-1].add("CMUMohawkLandingCrush", x + .5, y + .5)
                    elif "/belly/" in path:
                        decks[-1].add(self.entity({"path": props["item_to_deploy"], "fields": {}}, variant), x - 5 + .5, y + .5)
                    elif "/landing_gear/" in path:
                        dx, dy = props.get("map_offset_x", 0), props.get("map_offset_y", 0)
                        for name in ("item_to_deploy", "item_to_deploy2"):
                            f = {"dir": props.get("dir", 2)}
                            if name.endswith("2"):
                                f |= {"pixel_x": -16, "pixel_y": -19}
                            decks[-1].add(self.entity({"path": props[name], "fields": f}, variant), x + dx + .5, y + dy + .5)
                        for gx in range(2):
                            for gy in range(2):
                                decks[-1].tiles[x + dx + gx, y + dy + gy] = "CMShuttleTileInvisible"
                    elif "/ramp_button/" in path:
                        decks[-1].add(self.entity({"path": props["item_to_deploy"], "fields": {"pixel_y": 16}}, variant), x + .5, y + .5)
                        decks[-1].tiles[x, y] = "CMShuttleTileInvisible"
                    elif "/fuel_attachment_point/" in path:
                        side = "Port" if props["offset_x"] > 0 else "Starboard"
                        decks[-1].add("CMUMohawkEngine" + side, x + .5, y + .5)
                        decks[-1].tiles[x, y] = "CMShuttleTileInvisible"
                    elif "/hardpoints/" in path:
                        point = next(a for a in cell["atoms"] if a["path"].startswith("/obj/effect/attach_point/"))
                        dx, dy = props.get("map_offset_x", 0), props.get("map_offset_y", 0)
                        decks[-1].add(self.entity(point, variant), x + dx + .5, y + dy + .5)
                        decks[-1].tiles[x + dx, y + dy] = "CMShuttleTileInvisible"
                    elif "/dummy_part/" in path:
                        stage = ["first", "second", "third", "fourth", "fifth"].index(props["mode"])
                        turf = next(a for a in cell["atoms"] if a["path"].startswith("/turf/"))
                        floor = resolve(turf["path"], self.definitions) | turf["fields"]
                        decks[0].add("CMUMohawkRampMarker", x + .5, y + .5,
                                     [{"type": "MohawkRampSegment", "stage": stage}])
                        # Only the deployed assembly moves aft. Cabin markers
                        # retain the raised floor's original coordinates.
                        # The raised end caps have no lowered counterpart. The
                        # visible deployed edge starts at ramp-4-low: no hidden
                        # tile, stair support or pickup area may extend beyond it.
                        if stage > 0:
                            decks[-1].add(self.ramp_support(variant, stage, floor, props), x + .5, y - .5,
                                          [{"type": "MohawkRampSegment", "stage": stage, "lower": True}])
                            decks[-1].tiles[x, y - 1] = "CMShuttleTileInvisible"
                    elif "/m90_minigun" in path:
                        decks[-1].add("CMUMohawkM90Point", x + .5, y + .5)
                        decks[-1].tiles[x, y] = "CMShuttleTileInvisible"
                    else:
                        raise ValueError(f"Unmapped deployer: {path}")
                    continue
                if path.startswith("/turf/open/shuttle/"):
                    decks[0].tiles[x, y] = self.tile(props["icon"], props["icon_state"], props.get("dir", 2))
                    outcomes["floor"] += 1
                    continue
                if path.startswith(("/obj/effect/attach_point/weapon/", "/obj/effect/attach_point/electronics/")):
                    # The source's ground-level servicing proxy is the actual mount
                    # in CMU, linked to the primary flight controller by DropshipDeck.
                    outcomes["equipment_on_lower_deck"] += 1
                    continue
                if path.startswith("/turf/closed/"):
                    decks[0].tiles[x, y] = "CMShuttleTileInvisible"
                proto = self.entity(atom, variant)
                generated = self.prototypes.get(proto, {})
                if generated.get("parent") == "CMBaseWallInvincible" or any(
                        c["type"] == "Transform" and c.get("anchored") for c in generated.get("components", [])):
                    # Source facade pieces can sit over empty turfs. Static
                    # Robust bodies still need a tile to anchor to the grid;
                    # otherwise rotating the ship leaves their collisions behind.
                    decks[0].tiles.setdefault((x, y), "CMShuttleTileInvisible")
                direction = props.get("dir", 2)
                direction = {"SOUTH": 2, "NORTH": 1, "EAST": 4, "WEST": 8}.get(direction, direction)
                # The imported states retain BYOND direction order. Most structure
                # sheets have one direction, so their source dir must not rotate art.
                rot = {2: 0, 1: math.pi, 4: math.pi / 2, 8: -math.pi / 2}.get(direction, 0)
                comps = []
                icon = props.get("icon")
                if icon and (RESOURCES / "Textures" / rsi_path(icon) / "meta.json").exists():
                    meta = json.loads((RESOURCES / "Textures" / rsi_path(icon) / "meta.json").read_text())
                    entry = next(s for s in meta["states"] if s["name"] == rsi_state(props.get("icon_state", "")))
                    if entry["directions"] > 1 and rot:
                        comps.append({"type": "Transform", "parent": 1, "pos": f"{x+.5:g},{y+.5:g}", "rot": f"{rot} rad"})
                decks[0].add(proto, x + .5, y + .5, comps)
                if "/ladder/multiz/dropship/" in path:
                    decks[-1].add("CMUMohawkLowerLadder", x + .5, y + .5)
                    decks[-1].tiles[x, y] = "CMShuttleTileInvisible"
                outcomes["entity"] += 1

        if variant == "omaha":
            # Empty corners beside the rear passenger row, inside the cabin.
            decks[0].add("CMUMohawkInternalModulePort", -2.5, -7.5)
            decks[0].add("CMUMohawkInternalModuleStarboard", 3.5, -7.5)
            outcomes["internal_utility_mount"] += 2

        # The PR supplies the roof as a 17x24 tile sheet rather than a second DMM.
        # Preserve its exact pixels on a real upper grid.
        roof_icon = "icons/turf/omaha/mohawk-top-view.dmi"
        _, _, roof = dmi_states(self.source / roof_icon)
        for state, art in roof.items():
            match = re.fullmatch(r"(\d+),(\d+)", state)
            if match and art["images"][0].getbbox():
                x, y = int(match[1]) - 8, int(match[2]) - 12
                decks[1].tiles[x, y] = self.tile(roof_icon, state)
        for offset, deck in decks.items():
            suffix = "" if offset == 0 else "_lower" if offset == -1 else "_upper"
            deck.write(RESOURCES / f"Maps/CMU14/ShuttlesDropships/Mohawk/{resource_name}{suffix}.yml")
            if offset == 0:
                deck.write(RESOURCES / f"Maps/CMU14/ShuttlesDropships/Mohawk/{resource_name}_deployment.yml", deployment=True)
        self.audit[resource_name] = {"source_cells": len(data["cells"]), "source_levels": [1],
                               "outcomes": dict(outcomes), "decks": {
                                   str(k): {"entities": len(d.entities), "tiles": len(d.tiles)} for k, d in decks.items()}}
        if paint:
            self.audit[resource_name]["original_art_fallbacks"] = sorted(self.paint_fallbacks)

    def write(self):
        output = RESOURCES / "Prototypes/CMU14/Vehicles/Dropships/Mohawk"
        output.mkdir(parents=True, exist_ok=True)
        for name, entries in (("imported_entities", self.prototypes), ("imported_tiles", self.tiles)):
            (output / (name + ".yml")).write_text("# Generated by Tools/mohawk/import_mohawk.py\n" +
                yaml.safe_dump(list(entries.values()), sort_keys=False, width=120), encoding="utf-8", newline="\n")
        (RESOURCES / "Textures/CMU14/Dropships/Mohawk/tiles/attributions.yml").write_text(yaml.safe_dump([{
            "files": [name + ".png" for name in sorted(self.tiles)], "license": "CC-BY-SA-3.0",
            "copyright": "CM-SS13 contributors: thwomper, PoltavskaPraca, Steelpoint. Directional tile frames extracted from the adjacent imported RSI sheets.",
            "source": PR}], sort_keys=False), encoding="utf-8", newline="\n")
        (Path(__file__).parent / "map_audit.json").write_text(json.dumps(self.audit, indent=2) + "\n", encoding="utf-8", newline="\n")
        print(json.dumps(self.audit, indent=2))


def fetch_source(source: Path):
    manifest = json.loads(MANIFEST.read_text())
    source = source.resolve()

    def fetch(entry):
        name, digest = entry
        target = (source / name).resolve()
        if not target.is_relative_to(source):
            raise ValueError(f"Unsafe source path: {name}")
        data = target.read_bytes() if target.exists() else urllib.request.urlopen(
            f"https://raw.githubusercontent.com/{REPOSITORY}/{REVISION}/{name}", timeout=60).read()
        if hashlib.sha256(data).hexdigest() != digest:
            raise ValueError(f"Source hash mismatch: {name}")
        target.parent.mkdir(parents=True, exist_ok=True)
        if not target.exists():
            target.write_bytes(data)

    with ThreadPoolExecutor(max_workers=8) as pool:
        list(pool.map(fetch, manifest["inputs"].items()))
    print(f"Verified {len(manifest['inputs'])} pinned source files.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True, help="Pinned CM-SS13 source directory")
    parser.add_argument("--inventory", action="store_true")
    parser.add_argument("--art", action="store_true")
    parser.add_argument("--maps", action="store_true")
    parser.add_argument("--fetch", action="store_true", help="Fetch and verify the pinned inputs in source_manifest.json")
    args = parser.parse_args()
    if args.fetch:
        fetch_source(args.source)
    if args.inventory:
        inventory(args.source)
    if args.art:
        import_art(args.source)
    if args.maps:
        importer = MapImporter(args.source)
        for variant in ("omaha", "midway"):
            importer.convert(variant)
            importer.convert(variant, "navy")
        importer.write()


if __name__ == "__main__":
    main()
