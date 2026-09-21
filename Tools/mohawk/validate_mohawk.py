#!/usr/bin/env python3
"""Check generated map references, tile encoding and RSI frame geometry."""
import base64
import json
from pathlib import Path
import struct

from PIL import Image
import yaml

ROOT = Path(__file__).resolve().parents[2]
RESOURCES = ROOT / "Content.CMU/Resources"


def main():
    ids = set()
    loader = getattr(yaml, "CBaseLoader", yaml.BaseLoader)
    for root in (ROOT / "Resources/Prototypes", RESOURCES / "Prototypes"):
        for path in root.rglob("*.yml"):
            try:
                data = yaml.load(path.read_text(encoding="utf-8-sig"), Loader=loader)
                if isinstance(data, list):
                    ids.update(obj["id"] for obj in data if isinstance(obj, dict) and isinstance(obj.get("id"), str))
            except yaml.YAMLError:
                # This check owns Mohawk files; the game's validator covers unrelated content.
                continue
    errors = []
    for path in (RESOURCES / "Prototypes/CMU14/Vehicles/Dropships/Mohawk").glob("*.yml"):
        for obj in yaml.load(path.read_text(), Loader=loader):
            if obj["type"] == "tile":
                assert Image.open(RESOURCES / obj["sprite"].lstrip("/")).size == (32, 32), obj["id"]
            parents = obj.get("parent", [])
            for parent in [parents] if isinstance(parents, str) else parents:
                if parent not in ids:
                    errors.append(f"{obj['id']}: missing parent {parent}")
            for comp in obj.get("components", []):
                if comp["type"] != "Sprite" or "sprite" not in comp:
                    continue
                sprite = comp["sprite"]
                folder = next((root / sprite for root in (ROOT / "Resources/Textures", RESOURCES / "Textures")
                               if (root / sprite / "meta.json").exists()), None)
                if folder is None:
                    errors.append(f"{obj['id']}: missing sprite {sprite}")
                    continue
                states = {s["name"] for s in json.loads((folder / "meta.json").read_text(encoding="utf-8-sig"))["states"]}
                for layer in comp.get("layers", []):
                    layer_sprite = layer.get("sprite", sprite)
                    layer_folder = next((root / layer_sprite for root in (ROOT / "Resources/Textures", RESOURCES / "Textures")
                                         if (root / layer_sprite / "meta.json").exists()), None)
                    if layer_folder is None:
                        errors.append(f"{obj['id']}: missing layer sprite {layer_sprite}")
                        continue
                    layer_states = {s["name"] for s in json.loads((layer_folder / "meta.json").read_text(encoding="utf-8-sig"))["states"]}
                    if "state" in layer and layer["state"] not in layer_states:
                        errors.append(f"{obj['id']}: missing state {layer['state']} in {layer_sprite}")
                if "CMUMohawkDoor" in ([parents] if isinstance(parents, str) else parents):
                    layers = {key for layer in comp.get("layers", []) for key in layer.get("map", [])}
                    for layer in ("Base", "BaseUnlit", "BaseBolted", "BaseEmergencyAccess"):
                        if f"enum.DoorVisualLayers.{layer}" not in layers:
                            errors.append(f"{obj['id']}: missing airlock animation layer {layer}")
                    for state in ("door_open", "door_closed", "door_opening", "door_closing", "door_deny"):
                        if state not in states:
                            errors.append(f"{obj['id']}: missing door animation state {state}")
    entity_count = 0
    map_counts = {"Grid": 0, "Map": 0}
    for path in (RESOURCES / "Maps/CMU14/ShuttlesDropships/Mohawk").glob("*.yml"):
        data = yaml.safe_load(path.read_text())
        assert data["meta"]["category"] in ("Grid", "Map")
        map_counts[data["meta"]["category"]] += 1
        seen = set()
        for group in data["entities"]:
            if group["proto"] and group["proto"] not in ids:
                errors.append(f"{path.name}: unknown {group['proto']}")
            for ent in group["entities"]:
                entity_count += 1
                assert ent["uid"] not in seen
                seen.add(ent["uid"])
                components = ent.get("components", [])
                kinds = [c["type"] for c in components]
                if len(kinds) != len(set(kinds)):
                    errors.append(f"{path.name}: duplicate component {ent['uid']}")
                for comp in components:
                    if comp["type"] == "Sprite":
                        errors.append(f"{path.name}: entity {ent['uid']} has a map-only sprite override; "
                                      "server-loaded sprite artwork must be defined in its prototype")
                    if comp["type"] == "MapGrid":
                        assert comp.get("canSplit") is False, f"{path.name}: a linked deck must not split into independent grids"
                        for chunk in comp["chunks"].values():
                            raw = base64.b64decode(chunk["tiles"])
                            assert len(raw) == 16 * 16 * 7
                            for tile, _, _, _ in struct.iter_unpack("<iBBB", raw):
                                assert tile in data["tilemap"]
        assert len(seen) == data["meta"]["entityCount"]
    state_count = 0
    for path in (RESOURCES / "Textures/CMU14/Dropships/Mohawk").rglob("meta.json"):
        data = json.loads(path.read_text())
        for state in data["states"]:
            art = Image.open(path.parent / (state["name"] + ".png"))
            width, height = data["size"]["x"], data["size"]["y"]
            assert art.width % width == 0 and art.height % height == 0
            frames = sum(len(d) for d in state["delays"]) if "delays" in state else state["directions"]
            assert art.width * art.height >= width * height * frames
            state_count += 1
    audio_count = 0
    for path in (RESOURCES / "Audio/CMU14/Dropships/Mohawk").rglob("*.ogg"):
        # The Vorbis identification packet stores its channel count after the
        # packet type, signature and 32-bit version. No decoder is needed here.
        header = path.read_bytes()[:128]
        packet = header.find(b"\x01vorbis")
        if not header.startswith(b"OggS") or packet < 0 or header[packet + 11] != 1:
            errors.append(f"{path.name}: positional sounds must be mono Ogg Vorbis")
        audio_count += 1
    if errors:
        raise SystemExit("\n".join(errors))
    print(f"Validated {map_counts['Grid']} grids and {map_counts['Map']} deployment maps ({entity_count} entities), "
          f"prototype references, {state_count} RSI states and {audio_count} mono sounds.")


if __name__ == "__main__":
    main()
