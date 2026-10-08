#!/usr/bin/env python3
"""Export a saved Garrison level for local 3D review, without running the game.

The format-7 entity stream is read a component at a time. Large decal and lighting
components are skipped; only grid chunks, transforms, containment and appearance
overrides are materialized. Tile decoding follows Robust's MapChunkSerializer.
This is an offline saved-map inspection tool, not a live gameplay renderer.
"""

from __future__ import annotations

import argparse
import base64
from collections import Counter, defaultdict
from functools import cache
import json
import math
from pathlib import Path
import re
import struct
from typing import Any
from urllib.parse import quote

from PIL import Image, ImageColor

import inventory as art_inventory
from placement import resolve_placements
from layout import resolve_layout

ROOT = Path(__file__).resolve().parents[2]
MAP_DEFINITIONS = {
    "classic": ("stable_garrison.yml", "stablegarrison"),
    "redux": ("stable_garrison_redux.yml", "StableGarrisonRedux"),
}
STATE_COMPONENTS = {"Appearance", "Door", "Foldable", "RandomSprite", "Storage", "EntityStorage", "ItemCabinet",
                    "WallMount", "Rotatable", "LightToggle", "ItemToggle", "PowerCellSlot", "Charger",
                    "Stack", "StackLayerThreshold", "ItemCounter", "GenericVisualizer", "WebbingClothing", "CMUItemStain"}
READ_COMPONENTS = {"Transform", "Map", "MapGrid", "ContainerContainer", "Sprite", "IconSmooth", *STATE_COMPONENTS,
                   "Damageable", "Injurable", "DamageVisuals", "Barbed", "SprayAcided", "Corrodible", "Flammable",
                   "HandheldLight", "ToggleableVisuals", "SubFloorHide", "PowerChargerVisuals", "ItemMapper",
                   "ItemSlots", "Battery", "Tag", "SmoothEdge", "CMIconSmooth", "Solution", "SolutionContainerVisuals",
                   "SolutionContainerManager", "CMU3DElevation", "CMUZLevelHighGround"}


def finite_vector(value: Any, length: int = 2) -> tuple[float, ...]:
    values = value.split(",") if isinstance(value, str) else value
    if not isinstance(values, (list, tuple)) or len(values) != length:
        raise ValueError(f"Expected {length} coordinates: {value!r}")
    result = tuple(float(coordinate) for coordinate in values)
    if not all(math.isfinite(coordinate) for coordinate in result):
        raise ValueError("Coordinates must be finite")
    return result


def radians(value: Any) -> float:
    """Robust angles are degrees unless the serialized scalar ends with 'rad'."""
    text = str(value).strip()
    angle = float(text[:-3]) if text.endswith("rad") else math.radians(float(text))
    if not math.isfinite(angle):
        raise ValueError("Rotation must be finite")
    return angle


def select_map(definition: dict, level: int) -> str:
    if level == 0:
        return definition["mapPath"]
    paths = definition.get("mapsAbove" if level > 0 else "mapsBelow", [])
    if abs(level) > len(paths):
        raise ValueError(f"Configured map has no level {level}")
    return paths[abs(level) - 1]


def configured_map(root: Path, variant: str, level: int) -> tuple[Path, dict]:
    filename, prototype_id = MAP_DEFINITIONS[variant]
    path = root / "Content.CMU/Resources/Prototypes/CMU14/Maps/Planets/ua" / filename
    definitions = art_inventory.load_yaml(path.read_text(encoding="utf-8-sig"))
    definition = next(entry for entry in definitions if entry.get("type") == "gameMap" and entry.get("id") == prototype_id)
    reference = select_map(definition, level)
    result = art_inventory.resource_path(root, reference)
    if result is None:
        raise FileNotFoundError(reference)
    return result, definition


def read_map(path: Path) -> tuple[dict, dict[int, dict]]:
    """Parse format-7 headers and selected components, never the complete map AST."""
    records: dict[int, dict] = {}
    header_lines: list[str] = []
    component_lines: list[str] = []
    in_entities = False
    current_prototype = ""
    current: dict | None = None
    current_component: str | None = None

    def finish_component() -> None:
        nonlocal component_lines
        if component_lines and current is not None and current_component is not None:
            component = art_inventory.load_yaml("".join(component_lines))
            current["components"][current_component] = component
        component_lines = []

    def finish_entity() -> None:
        finish_component()
        if current is None:
            return
        uid = current["id"]
        if uid <= 0 or uid in records:
            raise ValueError(f"Nonpositive or duplicate saved entity UID {uid} in {path}")
        records[uid] = current

    with path.open(encoding="utf-8-sig") as stream:
        for line in stream:
            if not in_entities:
                if line.rstrip() == "entities:":
                    in_entities = True
                else:
                    header_lines.append(line)
                continue
            if line.startswith("- proto:"):
                finish_entity()
                current = None
                current_component = None
                current_prototype = str(art_inventory.load_yaml(line.split(":", 1)[1]) or "")
            elif line.startswith("  - uid:"):
                finish_entity()
                current = {"id": int(line.split(":", 1)[1]), "prototype": current_prototype,
                           "components": {}, "componentTypes": []}
                current_component = None
            elif line.startswith("    - type:"):
                finish_component()
                if current is None:
                    raise ValueError(f"Component without an entity in {path}")
                current_component = str(art_inventory.load_yaml(line.split(":", 1)[1]))
                current["componentTypes"].append(current_component)
                if current_component in READ_COMPONENTS:
                    component_lines = [line[6:]]
            elif line.startswith("      ") and component_lines:
                component_lines.append(line[6:])
            elif line.strip() and not line.startswith(("#", "    components:")):
                # Entity-level fields and document terminators end component data.
                finish_component()
                current_component = None
    finish_entity()
    header = art_inventory.load_yaml("".join(header_lines))
    if not in_entities or header.get("meta", {}).get("format") != 7:
        raise ValueError(f"Only format-7 maps are supported: {path}")
    return header, records


class TransformError(ValueError):
    pass


class WorldTransforms:
    def __init__(self, records: dict[int, dict], defaults: dict[str, dict] | None = None):
        self.records = records
        self.defaults = defaults or {}
        self.cache: dict[int, tuple[float, float, float, int]] = {}

    def local(self, uid: int) -> dict:
        record = self.records[uid]
        default = self.defaults.get(record["prototype"], {}).get("Transform", {})
        return {**default, **record["components"].get("Transform", {})}

    def resolve(self, uid: int, stack: tuple[int, ...] = ()) -> tuple[float, float, float, int]:
        if uid in self.cache:
            return self.cache[uid]
        if uid in stack:
            raise TransformError("Transform cycle: " + " -> ".join(map(str, (*stack, uid))))
        if uid not in self.records:
            raise TransformError("Missing transform parent: " + " -> ".join(map(str, (*stack, uid))))
        local = self.local(uid)
        try:
            x, y = finite_vector(local.get("pos", "0,0"))
            yaw = radians(local.get("rot", 0))
            parent_value = local.get("parent", 0)
            parent = 0 if parent_value in {None, "invalid", "null", ""} else int(parent_value)
        except (TypeError, ValueError) as error:
            raise TransformError(f"Invalid transform on {uid}: {error}") from error
        if parent:
            px, py, pyaw, root = self.resolve(parent, (*stack, uid))
            cosine, sine = math.cos(pyaw), math.sin(pyaw)
            result = px + cosine * x - sine * y, py + sine * x + cosine * y, pyaw + yaw, root
        else:
            result = x, y, yaw, uid
        self.cache[uid] = result
        return result


def decode_chunk(chunk: dict) -> list[dict]:
    """Decode tile index, flags, variant and mirroring in the engine's Y-major order."""
    version = int(chunk.get("version", 1))
    size = int(chunk.get("size", 16))
    if not 1 <= version <= 7 or not 1 <= size <= 256:
        raise ValueError(f"Unsupported tile chunk version/size: {version}/{size}")
    cx, cy = finite_vector(chunk["ind"])
    if not cx.is_integer() or not cy.is_integer():
        raise ValueError("Chunk indices must be integers")
    tile_struct = struct.Struct("<iBBB" if version >= 7 else "<iBB" if version >= 6 else "<HBB")
    data = base64.b64decode(chunk["tiles"], validate=True)
    if len(data) != tile_struct.size * size * size:
        raise ValueError(f"Tile chunk length {len(data)} does not match {size}x{size} version {version}")
    tiles = []
    for index, values in enumerate(tile_struct.iter_unpack(data)):
        tiles.append({"x": int(cx) * size + index % size, "y": int(cy) * size + index // size,
                      "palette": values[0], "flags": values[1], "variant": values[2],
                      "rotationMirroring": values[3] if version >= 7 else 0})
    return tiles


def hidden_container_entities(records: dict[int, dict]) -> set[int]:
    hidden = set()
    for record in records.values():
        containers = record["components"].get("ContainerContainer", {}).get("containers", {}) or {}
        for container in containers.values():
            if not isinstance(container, dict) or container.get("showEnts", False):
                continue
            children = [container.get("ent"), *(container.get("ents", []) or [])]
            hidden.update(child for child in children if isinstance(child, int) and child > 0)
    # Any child of a hidden entity is also hidden, including nested containers.
    children_by_parent: dict[int, list[int]] = defaultdict(list)
    for uid, record in records.items():
        parent = record["components"].get("Transform", {}).get("parent")
        if isinstance(parent, int):
            children_by_parent[parent].append(uid)
    queue = list(hidden)
    while queue:
        for child in children_by_parent.get(queue.pop(), []):
            if child not in hidden:
                hidden.add(child)
                queue.append(child)
    return hidden


def model_index(models: list[dict]) -> dict[str, list[dict]]:
    result: dict[str, list[dict]] = defaultdict(list)
    for model in sorted(models, key=lambda item: (item.get("status") != "reviewed", item["id"])):
        for source in model.get("sourcePrototypes", []):
            result[source].append(model)
    return result


def resolve_direction(instance, library):
    model = library.get(instance.get('modelId'))
    if not model or not model.get('directionalModels'):
        return True
    directions = model.get('sourceDirections')
    yaw = instance['yaw']
    target = None
    if directions in (4, 8) and math.isfinite(yaw) and len(model['directionalModels']) == directions:
        turn = math.floor(yaw / (math.tau / directions) + .5) % directions
        index = ((0, 2, 1, 3) if directions == 4 else (0, 4, 2, 6, 1, 7, 3, 5))[turn]
        target = library.get(model['directionalModels'][index])
        if (target and target.get('referenceDirection') == index and
                all(target.get(key) == model.get(key) for key in
                    ('directionalModels', 'sourceDirections', 'referenceRsi', 'referenceState', 'sourceSpriteRotates'))):
            instance.update(modelId=target['id'], modelStatus=target.get('status', 'draft'),
                            candidateModelIds=[target['id']], sourceDirection=index)
            return True
    instance.update(baseModelId=model['id'], modelId=None, modelStatus=None, matchKind='unmapped',
                    candidateModelIds=[uid for uid in model['directionalModels'] if uid],
                    unsupportedState='No authored model for the selected source direction')
    return False


def match_model(prototype: dict, by_reference: dict[str, list[dict]]) -> dict:
    for reference in [prototype["id"], *prototype.get("ancestors", [])]:
        matches = by_reference.get(reference, [])
        if matches:
            return {"modelId": matches[0]["id"], "matchKind": "exact" if reference == prototype["id"] else "inherited",
                    "matchedPrototype": reference, "modelStatus": matches[0].get("status", "draft"),
                    "candidateModelIds": [model["id"] for model in matches]}
    return {"modelId": None, "matchKind": "unmapped", "matchedPrototype": None, "modelStatus": None}


def resolve_door_pose(instance: dict, door: dict, library: dict) -> bool:
    """Match explicit stable geometry to effective saved state. Never close an open passage by guessing."""
    state = door.get('state', 'Closed')
    instance['doorState'] = state
    reference_fields = {'Closed': ('closedSpriteState', 'closed'), 'Open': ('openSpriteState', 'open'),
                        'Opening': ('openingSpriteState', 'opening'), 'Closing': ('closingSpriteState', 'closing')}
    if state in reference_fields:
        field, fallback = reference_fields[state]
        instance['referenceState'] = door.get(field, fallback)
    model = library.get(instance.get('modelId'))
    if model is None:
        if state != 'Closed':
            instance['unsupportedState'] = f'Door {state}'
        return False
    if state in ('Open', 'Closed'):
        if model.get('doorState', 'Closed') != state:
            model = library.get(model.get('alternateDoorModel'))
        if model is not None and model.get('doorState', 'Closed') == state:
            instance['modelId'] = model['id']
            instance['modelStatus'] = model.get('status', 'draft')
            instance['candidateModelIds'] = [model['id']]
            return True
    instance['baseModelId'] = instance['modelId']
    instance['modelId'] = None
    instance['matchKind'] = 'unmapped'
    instance['unsupportedState'] = f'Door {state}'
    return False


@cache
def robust_named_colors() -> dict[str, str]:
    """Read the engine's named RGBA constants, including its non-CSS colors."""
    path = ROOT / "RobustToolbox/Robust.Shared.Maths/Color.cs"
    text = path.read_text(encoding="utf-8")
    constants = {}
    for name, values in re.findall(r"public static Color (\w+) => new\(([\d, ]+)\);", text):
        channels = [int(value) for value in values.split(",")]
        if len(channels) == 4 and all(0 <= value <= 255 for value in channels):
            constants[name] = "#" + "".join(f"{value:02X}" for value in channels)
    table = re.search(r"\bDefaultColors\s*=.*?\{(.*?)\}\.ToFrozenDictionary\(\)", text, re.DOTALL)
    entries = re.findall(r'\["([^"\n]+)"\]\s*=\s*(\w+)', table[1]) if table else []
    if not entries or any(constant not in constants for _, constant in entries):
        raise ValueError(f"Cannot resolve the complete engine named-color table in {path}")
    return {name: constants[constant] for name, constant in entries}


def normalize_tint(value: str) -> str:
    """Return viewer-compatible hex while preserving Robust ColorSerializer semantics."""
    if not isinstance(value, str):
        raise ValueError(f"Invalid Robust color: {value!r}")
    if re.fullmatch(r"#(?:[0-9A-Fa-f]{3}|[0-9A-Fa-f]{4}|[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})", value):
        digits = value[1:].upper()
        if len(digits) in (3, 4):
            digits = "".join(character * 2 for character in digits)
        normalized = "#" + digits
    else:
        normalized = robust_named_colors().get(value.lower())
        if normalized is None:
            raise ValueError(f"Unknown Robust color: {value!r}")
    return normalized[:-2] if len(normalized) == 9 and normalized.endswith("FF") else normalized


def source_reference(prototype: dict, resources: dict) -> dict:
    source = {key: prototype.get(key) for key in ("source", "sprite", "icon", "resources", "components")}
    for appearance in (prototype.get("sprite"), prototype.get("icon")):
        if not appearance:
            continue
        for layer in appearance.get("layers") or [appearance]:
            if not isinstance(layer, dict) or layer.get("visible") is False:
                continue
            state = layer.get("state")
            reference = layer.get("rsi", layer.get("sprite", appearance.get("sprite")))
            if not isinstance(reference, str) or state is None:
                continue
            reference = art_inventory.texture_reference(reference)
            metadata = resources.get(reference, {})
            state_metadata = next((item for item in metadata.get("states", [])
                                   if item["name"] == str(state) and item.get("imageExists")), None)
            if state_metadata:
                size = metadata.get("size", {}) or {}
                source["preview"] = {"url": "/resources" + quote(reference + "/" + str(state) + ".png", safe="/"),
                                     "resource": reference + "/" + str(state) + ".png",
                                     "state": str(state), "frameSize": [size.get("x", 32), size.get("y", 32)],
                                     "directions": state_metadata.get("directions", 1),
                                     "animated": state_metadata.get("animated", False),
                                     "spriteTint": normalize_tint(appearance.get("color", "#FFFFFF")),
                                     "layerTint": normalize_tint(layer.get("color", "#FFFFFF"))
                                     if layer is not appearance else "#FFFFFF"}
                return source
    return source


def sampled_color(image: Image.Image, tints: tuple[str, ...] = ()) -> str:
    """Alpha-weighted mean of source pixels, multiplied by declared default tints."""
    channels = [0.0, 0.0, 0.0]
    alpha_sum = 0
    for red, green, blue, alpha in struct.iter_unpack("BBBB", image.convert("RGBA").tobytes()):
        alpha_sum += alpha
        for index, channel in enumerate((red, green, blue)):
            channels[index] += channel * alpha
    if not alpha_sum:
        return "#00000000"
    channels = [channel / alpha_sum for channel in channels]
    for tint in tints:
        color = ImageColor.getcolor(normalize_tint(tint), "RGBA")
        channels = [channel * color[index] / 255 for index, channel in enumerate(channels)]
    return "#" + "".join(f"{min(255, max(0, round(channel))):02X}" for channel in channels)


def enrich_materials(scene: dict, root: Path) -> None:
    errors = []
    for entry in scene["tilePalette"].values():
        reference = entry.get("sprite")
        if not isinstance(reference, str):
            continue
        path = art_inventory.resource_path(root, art_inventory.texture_reference(reference))
        if path is None:
            errors.append({"tile": entry["prototype"], "error": "Missing tile texture " + reference})
            continue
        try:
            with Image.open(path) as image:
                variants = int(entry.get("variants") or 1)
                if image.height != 32 or image.width != 32 * variants:
                    raise ValueError(f"Tile sprite dimensions {image.size} do not match 32x32 variant strip")
                entry["variantColors"] = [sampled_color(image.crop((index * 32, 0, (index + 1) * 32, 32)))
                                          for index in range(variants)]
                entry["color"] = entry["variantColors"][0]
                entry["textureUrl"] = "/resources" + quote(art_inventory.texture_reference(reference), safe="/")
                entry["colorMethod"] = "alpha-weighted source sprite mean; per-variant colors retained"
        except (OSError, ValueError) as error:
            errors.append({"tile": entry["prototype"], "error": str(error)})
    for prototype_id, source in scene["sourceReferences"].items():
        preview = source.get("preview")
        if not preview:
            continue
        path = art_inventory.resource_path(root, preview["resource"])
        if path is None:
            continue
        try:
            with Image.open(path) as image:
                width, height = preview["frameSize"]
                source["color"] = sampled_color(image.crop((0, 0, width, height)),
                                                 (preview["spriteTint"], preview["layerTint"]))
                source["colorMethod"] = ("first reference frame alpha-weighted mean with prototype sprite/layer tint; "
                                         "not a composite of all sprite layers")
        except (OSError, ValueError) as error:
            errors.append({"prototype": prototype_id, "error": str(error)})
    scene["diagnostics"]["materialErrors"] = errors
    scene["diagnostics"]["limitations"].append("Floor and fallback colors are sampled averages; they approximate source textures.")


def fold_reference_state(components: dict, folded: bool) -> str | None:
    """Find an unambiguous visible source layer even when the matching 3D pose is missing."""
    if not isinstance(folded, bool):
        return None
    rules = components.get('GenericVisualizer', {}).get('visuals', {}).get('enum.FoldedVisuals.State', {})
    states = []
    for layer in components.get('Sprite', {}).get('layers', []):
        for key in layer.get('map', []):
            options = rules.get(key, {})
            values = options.get(folded, options.get(str(folded).lower(), {}))
            if values.get('visible') is True and isinstance(layer.get('state'), str):
                states.append(layer['state'])
    return states[0] if len(states) == 1 else None


def resolve_fold_pose(instance: dict, foldable: dict, library: dict) -> bool:
    """Select only a matching authored Foldable pose, retaining the original reference provenance."""
    folded = foldable.get('folded', False)
    instance['folded'] = folded
    model = library.get(instance.get('modelId'))
    original = model
    if model is not None and isinstance(folded, bool):
        if model.get('folded', False) != folded:
            model = library.get(model.get('alternateFoldModel'))
            if model is not None and model.get('alternateFoldModel') != original['id']:
                model = None
        if model is not None and model.get('folded', False) == folded:
            instance['modelId'] = model['id']
            instance['modelStatus'] = model.get('status', 'draft')
            instance['candidateModelIds'] = [model['id']]
            if model.get('referenceState'):
                instance['referenceState'] = model['referenceState']
            return True
    if original is not None:
        instance['baseModelId'] = original['id']
        instance['modelId'] = None
        instance['matchKind'] = 'unmapped'
    if original is not None or folded:
        instance['unsupportedState'] = 'Folded' if folded is True else 'Unfolded' if folded is False else 'Invalid fold state'
    return False


def choose_focus(instances: list[dict], bounds: list[float], z: float) -> list[float]:
    groups: dict[tuple[int, int], list[dict]] = defaultdict(list)
    for instance in instances:
        if instance["modelId"]:
            x, y, _ = instance["position"]
            groups[(math.floor(x / 24), math.floor(y / 24))].append(instance)
    if groups:
        furniture_terms = ("chair", "table", "bed", "bench", "desk", "locker", "crate", "console", "rack",
                           "bookcase", "couch", "fridge", "vendor", "sink", "toilet", "stool", "cabinet")

        def score(items: list[dict]) -> tuple[int, int, int]:
            models = {item["modelId"] for item in items}
            furniture = {model for model in models if any(term in model.lower() for term in furniture_terms)}
            return len(furniture), len(models), min(len(items), 100)

        selected = max(groups.values(), key=score)
        xs = sorted(item["position"][0] for item in selected)
        ys = sorted(item["position"][1] for item in selected)
        return [xs[len(xs) // 2], ys[len(ys) // 2], z]
    return [(bounds[0] + bounds[2]) / 2, (bounds[1] + bounds[3]) / 2, z]


def in_region(x: float, y: float, region: tuple[float, ...] | None) -> bool:
    return region is None or region[0] <= x < region[2] and region[1] <= y < region[3]


def random_sprite_index(models: list[dict]) -> dict:
    """Exact, conditional references; never treat a random source's default frame as its chosen state."""
    result = {}
    for model in models:
        for prototype in model.get('randomSpritePrototypes', []):
            variants = result.setdefault(prototype, {})
            key = (model.get('randomSpriteLayer'), model.get('referenceState'))
            if not all(key) or not model.get('referenceRsi'):
                raise ValueError(f"Incomplete random sprite reference on {model['id']}")
            if key in variants:
                raise ValueError(f"Duplicate random sprite reference: {prototype}/{key}")
            variants[key] = model
    return result


def resolve_random_sprite(instance: dict, selected: Any, variants: dict) -> bool:
    """Read both Robust ValueTuple forms. Only one known layer with no color modification is supported."""
    reason = 'Random sprite selection is not saved'
    if selected:
        reason = 'Unsupported random sprite selection'
        if isinstance(selected, dict) and len(selected) == 1:
            layer, choice = next(iter(selected.items()))
            if isinstance(choice, dict) and len(choice) == 1:
                choice = next(iter(choice.items()))
            if isinstance(choice, (list, tuple)) and len(choice) == 2 and isinstance(choice[0], str):
                state, tint = choice
                instance['randomSpriteState'] = state
                white = tint is None
                try:
                    white = white or normalize_tint(tint) == '#FFFFFF'
                except ValueError:
                    pass
                model = variants.get((layer, state))
                if model is not None and white:
                    instance.update(modelId=model['id'], modelStatus=model.get('status', 'draft'),
                                    matchKind='exact', matchedPrototype=instance['prototype'],
                                    candidateModelIds=[model['id']], referenceState=state)
                    return True
                reason = 'Unsupported random sprite color' if not white else 'Random sprite state has no model'
    if instance.get('modelId'):
        instance['baseModelId'] = instance['modelId']
    instance.update(modelId=None, modelStatus=None, matchKind='unmapped', matchedPrototype=None,
                    candidateModelIds=sorted(model['id'] for model in variants.values()), unsupportedState=reason)
    return False


def viewer_geometry_variants(variants: dict) -> dict:
    # Layout and appearance variants can copy raw YAML parts. JSON consumers need
    # numeric vectors, just like the separately validated model catalog provides.
    return {key: [{**part, **{bound: finite_vector(part[bound], 3)
                             for bound in ('min', 'max') if isinstance(part[bound], str)}}
                  for part in parts] for key, parts in variants.items()}


def build_scene(header: dict, records: dict[int, dict], inventory: dict, models: list[dict],
                defaults: dict[str, dict] | None = None, *, level: int = 0,
                region: tuple[float, ...] | None = None, limit: int = 0) -> dict:
    prototypes = {entry["id"]: entry for entry in inventory["prototypes"]}
    by_reference = model_index(models)
    library = {model['id']: model for model in models}
    random_variants = random_sprite_index(models)
    transforms = WorldTransforms(records, defaults)
    hidden = hidden_container_entities(records)
    map_roots = set(header.get("maps", []))
    if not map_roots:
        map_roots = {uid for uid, record in records.items() if "Map" in record["components"]}
    exclusions: Counter[str] = Counter()
    errors: list[dict] = []
    state_counts: Counter[str] = Counter()
    instances = []
    referenced_prototypes = set()
    matches = {prototype_id: match_model(prototype, by_reference) for prototype_id, prototype in prototypes.items()}
    sprite_overrides = 0
    door_poses: Counter[str] = Counter()
    fold_poses: Counter[str] = Counter()
    random_poses: Counter[str] = Counter()
    sprite_poses: Counter[str] = Counter()
    from subfloor_visibility import PROTOTYPES as subfloor_prototypes, SubfloorVisibility
    subfloor_owner = SubfloorVisibility(header, records, defaults or {},
        {entry['id']: entry for entry in inventory.get('tiles', [])}, transforms, decode_chunk)
    subfloor_hidden, subfloor_unknown = [], {}
    for uid, record in sorted(records.items()):
        prototype_id = record["prototype"]
        prototype = prototypes.get(prototype_id)
        category = prototype.get("classification", "unresolved") if prototype else "anonymous" if not prototype_id else "unresolved"
        if category != "visual":
            exclusions[category] += 1
            continue
        if uid in hidden:
            exclusions["hidden_container"] += 1
            continue
        override = record["components"].get("Sprite")
        if override and override.get("visible") is False:
            exclusions["instance_hidden"] += 1
            continue
        try:
            x, y, yaw, map_root = transforms.resolve(uid)
        except TransformError as error:
            exclusions["invalid_transform"] += 1
            if len(errors) < 100:
                errors.append({"id": uid, "prototype": prototype_id, "error": str(error)})
            continue
        if map_root not in map_roots:
            exclusions["nullspace_or_orphan"] += 1
            continue
        if not in_region(x, y, region):
            exclusions["outside_region"] += 1
            continue
        if prototype_id in subfloor_prototypes:
            visible, reason, tile_id = subfloor_owner.check(uid)
            if visible is False:
                exclusions['source_subfloor_hidden'] += 1
                subfloor_hidden.append(dict(id=uid, prototype=prototype_id, tile=tile_id, reason=reason))
                continue
            if visible is None:
                subfloor_unknown[uid] = reason
        if limit and len(instances) >= limit:
            exclusions["instance_limit"] += 1
            continue
        instance = {"id": uid, "prototype": prototype_id, "position": [round(x, 6), round(y, 6), level],
                    "yaw": round(yaw, 9), **matches[prototype_id]}
        random_applied = False
        if prototype_id in random_variants:
            # Selected is instance state, not the prototype's default Sprite layer or random pool.
            selected = record['components'].get('RandomSprite', {}).get('selected')
            random_applied = resolve_random_sprite(instance, selected, random_variants[prototype_id])
            random_poses['modeled' if random_applied else instance['unsupportedState']] += 1
        base_door = (defaults or {}).get(prototype_id, {}).get('Door')
        saved_door = record['components'].get('Door')
        door_applied = False
        if base_door is not None or saved_door is not None:
            door_applied = resolve_door_pose(instance, {**(base_door or {}), **(saved_door or {})}, library)
            door_poses[('modeled' if door_applied else 'unmodeled') + instance['doorState']] += 1
        base_fold = (defaults or {}).get(prototype_id, {}).get('Foldable')
        saved_fold = record['components'].get('Foldable')
        fold_applied = False
        if base_fold is not None or saved_fold is not None:
            fold_applied = resolve_fold_pose(instance, {**(base_fold or {}), **(saved_fold or {})}, library)
            if not fold_applied and (reference := fold_reference_state((defaults or {}).get(prototype_id, {}), instance['folded'])):
                instance['referenceState'] = reference
            fold_poses[('modeled' if fold_applied else 'unmodeled') + ('Folded' if instance['folded'] else 'Unfolded')] += 1
        if (model := library.get(instance.get('modelId'))) and 'anchored' in model:
            from disposal_pipe_states import select_saved
            selected, reason = select_saved(model, library, (defaults or {}).get(prototype_id, {}), record['components'])
            if selected is None or instance.get('matchKind') != 'exact' or prototype_id != model.get('referencePrototype'):
                instance.update(baseModelId=model['id'], modelId=None, modelStatus=None, matchKind='unmapped',
                    unsupportedState=reason or 'Anchor model requires an exact audited source')
            else:
                instance.update(modelId=selected['id'], modelStatus=selected.get('status', 'draft'),
                    candidateModelIds=[selected['id']], anchored=selected['anchored'])
        resolve_direction(instance, library)
        if (model := library.get(instance.get('modelId'))) and model.get('solutionAppearance'):
            from solution_glass_states import resolve_scene_pose
            resolve_scene_pose(instance, model, (defaults or {}).get(prototype_id, {}), record['components'], normalize_tint)
        if (model := library.get(instance.get('modelId'))) and model.get('chargerAppearance'):
            from charger_states import resolve_scene_pose
            resolve_scene_pose(instance, model, (defaults or {}).get(prototype_id, {}), record['components'],
                               normalize_tint, records, defaults or {})
        if (model := library.get(instance.get('modelId'))) and model.get('reagentTankAppearance'):
            from reagent_tank_states import resolve_scene_pose
            default_components = (defaults or {}).get(prototype_id, {})
            if 'Sprite' not in default_components:
                default_components = {**default_components, 'Sprite': prototype.get('sprite') or {}}
            resolve_scene_pose(instance, model, default_components, record['components'], normalize_tint)
        if (model := library.get(instance.get('modelId'))) and model.get('spriteStates'):
            from sprite_states import resolve_scene_pose
            if 'anchored' in model:
                from disposal_pipe_states import resolve_scene_pose
            default_components = (defaults or {}).get(prototype_id, {})
            if 'Sprite' not in default_components:
                default_components = {**default_components, 'Sprite': prototype.get('sprite') or {}}
            applied = resolve_scene_pose(instance, model, default_components, record['components'], normalize_tint)
            sprite_poses['modeledFrameZero' if applied else instance['unsupportedState']] += 1
        if (model := library.get(instance.get('modelId'))) and model.get('barricadeDamageStates'):
            from barricade_states import saved_pose
            pose = saved_pose(model, (defaults or {}).get(prototype_id, {}), record['components'])
            if pose is None:
                instance.update(baseModelId=model['id'], modelId=None, matchKind='unmapped',
                                unsupportedState='Barricade saved appearance requires a reviewed state composition')
            else:
                instance['barricadeDamageState'] = pose
        if uid in subfloor_unknown:
            instance.update(baseModelId=instance.get('modelId'), modelId=None, modelStatus=None,
                matchKind='unmapped', unsupportedState='Subfloor visibility: ' + subfloor_unknown[uid])
        state_overrides = sorted(set(record["componentTypes"]) & STATE_COMPONENTS)
        if state_overrides:
            instance["stateOverrideComponents"] = state_overrides
            unapplied = [name for name in state_overrides if not ((name == 'Door' and door_applied) or
                         (name == 'Foldable' and fold_applied) or (name == 'RandomSprite' and random_applied))]
            instance['unappliedStateComponents'] = unapplied
            state_counts.update(unapplied)
        if override:
            sprite_overrides += 1
            instance["spriteOverride"] = {key: value for key, value in override.items() if key != "type"}
        high_ground = {**(defaults or {}).get(prototype_id, {}).get('CMUZLevelHighGround', {}),
                       **record['components'].get('CMUZLevelHighGround', {})}
        if (high_ground.get('stick') and not high_ground.get('supportOnlyFromAbove') and
                len(high_ground.get('heightCurve', [])) > 1 and
                max(high_ground['heightCurve']) - min(high_ground['heightCurve']) > .01):
            instance['zStair'] = high_ground
        instances.append(instance)
        referenced_prototypes.add(prototype_id)
    tile_definitions = {entry["id"]: entry for entry in inventory.get("tiles", [])}
    palette = {str(key): {"prototype": value, **{field: tile_definitions.get(value, {}).get(field)
                                                for field in ("sprite", "variants", "edgeSprites", "allowRotationMirror")}}
               for key, value in header.get("tilemap", {}).items()}
    tiles = []
    tile_errors = []
    tile_chunk_count = 0
    for uid, record in sorted(records.items()):
        grid = record["components"].get("MapGrid")
        if not grid:
            continue
        try:
            gx, gy, yaw, map_root = transforms.resolve(uid)
            if map_root not in map_roots:
                raise TransformError("Grid is not attached to an exported map")
        except TransformError as error:
            tile_errors.append({"grid": uid, "error": str(error)})
            continue
        cosine, sine = math.cos(yaw), math.sin(yaw)
        tile_size = float(grid.get("tileSize", 1))
        if tile_size != 1:
            tile_errors.append({"grid": uid, "error": "Non-unit grid tiles are unsupported"})
            continue
        for chunk_id, chunk in (grid.get("chunks", {}) or {}).items():
            tile_chunk_count += 1
            try:
                decoded = decode_chunk(chunk)
            except (ValueError, KeyError, TypeError) as error:
                tile_errors.append({"grid": uid, "chunk": str(chunk_id), "error": str(error)})
                continue
            for tile in decoded:
                tile_id = str(tile["palette"])
                if tile_id not in palette:
                    tile_errors.append({"grid": uid, "chunk": str(chunk_id), "error": "Unknown palette ID " + tile_id})
                    continue
                definition = tile_definitions.get(palette[tile_id]["prototype"])
                if palette[tile_id]["prototype"] == "Space" or definition is not None and not definition.get("sprite"):
                    continue
                x = gx + cosine * tile["x"] - sine * tile["y"]
                y = gy + sine * tile["x"] + cosine * tile["y"]
                # Region selection tests the tile center; output retains its corner.
                if not in_region(x + .5 * (cosine - sine), y + .5 * (sine + cosine), region):
                    continue
                tiles.append({**tile, "x": round(x, 6), "y": round(y, 6), "z": level,
                              "yaw": round(yaw, 9), "grid": uid})
    points = [(instance["position"][0], instance["position"][1]) for instance in instances]
    for tile in tiles:
        cosine, sine = math.cos(tile["yaw"]), math.sin(tile["yaw"])
        points.extend((tile["x"] + cosine * x - sine * y, tile["y"] + sine * x + cosine * y)
                      for x, y in ((0, 0), (0, 1), (1, 0), (1, 1)))
    bounds = ([min(point[0] for point in points), min(point[1] for point in points),
               max(point[0] for point in points), max(point[1] for point in points)] if points else [0, 0, 0, 0])
    geometry_variants, layout_stats = resolve_layout(instances, models, records, defaults or {}, transforms,
                                                    excluded_terrain_sources=hidden)
    from sprite_states import scene_variants
    scene_variants(instances, library, geometry_variants)
    from reagent_tank_states import scene_variants as tank_variants
    tank_variants(instances, library, geometry_variants)
    from charger_states import scene_variants as charger_variants
    charger_variants(instances, library, geometry_variants)
    from solution_glass_states import scene_variants as solution_variants
    solution_variants(instances, library, geometry_variants)
    from foam_wall_states import apply_scene as foam_variants
    foam_stats = foam_variants(instances, library, records, defaults or {}, transforms, geometry_variants, normalize_tint)
    placement_stats = resolve_placements(instances, models, geometry_variants)
    from slab_openings import apply_scene as apply_slab_openings
    slab_stats = apply_slab_openings(instances, tiles, library, records, defaults or {}, transforms, geometry_variants)
    from elevation import apply_scene as apply_elevation
    elevation_stats = apply_elevation(instances, tiles, records, transforms, library, geometry_variants)
    return {"schemaVersion": 1, "modelsUrl": "../generated/models.json",
            "coordinates": {"axes": "X/Y horizontal; Z up", "unit": "one game tile", "yaw": "radians, counterclockwise about +Z",
                            "tileOrigin": "x/y is the world position of the lower-left tile corner before yaw rotation; "
                                          "center adds rotate(yaw, [0.5,0.5])", "z": "selected map level index; not physical floor height"},
            "map": {"level": level, "bounds": [round(value, 6) for value in bounds],
                    "defaultFocus": choose_focus(instances, bounds, level), "levels": [{"z": level, "label": f"Level {level}"}]},
            "instances": instances, "tiles": tiles, "tilePalette": palette, "geometryVariants": viewer_geometry_variants(geometry_variants),
            "sourceReferences": {prototype_id: source_reference(prototypes[prototype_id], inventory.get("resources", {}))
                                 for prototype_id in sorted(referenced_prototypes)},
            "diagnostics": {**placement_stats, "elevation": elevation_stats, "foamAppearance": foam_stats, "slabOpenings": slab_stats, "layout": layout_stats, "doorPoses": dict(door_poses), "foldPoses": dict(fold_poses), "randomSpritePoses": dict(random_poses), "savedEntityCount": len(records), "metadataEntityCount": header.get("meta", {}).get("entityCount"),
                            "exportedInstances": len(instances), "exportedTiles": len(tiles), "tileChunksRead": tile_chunk_count,
                            "excluded": dict(sorted(exclusions.items())), "transformErrors": errors, "tileErrors": tile_errors,
                            "spriteOverrideCount": sprite_overrides, "unappliedStateComponentCounts": dict(sorted(state_counts.items())),
                            "spriteStatePoses": dict(sprite_poses),
                            "sourceSubfloorHidden": subfloor_hidden,
                            "matchCounts": dict(sorted(Counter(instance["matchKind"] for instance in instances).items())),
                            "region": region, "instanceLimit": limit,
                            "limitations": ["Offline saved-map state only; no live simulation, visibility or runtime-spawned entities.",
                                            "Draft and inherited model mappings are review candidates, not approved faithful replacements.",
                                            "Explicit stable door and fold poses select linked models. Animation, occupancy, power and damage remain unsupported.",
                                            "Explicit single-layer RandomSprite mappings require a saved selection and white/default color; unknown selections remain markers.",
                                            "Explicit generic RSI state models select frame zero for a proven saved/default appearance; unsupported appearances remain sprite fallbacks. Other sprite overrides are exposed as reference data.",
                                            "Reagent tank geometry uses serialized/default four-layer sprite snapshots before the live solution visualizer; saved appearance data requiring that owner remains unresolved.",
                                             "Selected panels and authored floor corners follow source neighbour keys; other corner families, decals, lighting and edge overlays remain incomplete.",
                                            "Non-unit grid tiles and malformed/unresolved transforms are reported and omitted."]}}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=ROOT)
    parser.add_argument("--inventory", type=Path, default=ROOT / "Tools/three_d/generated/inventory.json")
    parser.add_argument("--models", type=Path, default=ROOT / "Content.CMU/Resources/Prototypes/CMU14/ThreeD")
    parser.add_argument("--variant", choices=MAP_DEFINITIONS, default="classic")
    parser.add_argument("--level", type=int, default=0)
    parser.add_argument("--all-levels", action="store_true", help="Assemble every configured map at physical floor spacing")
    parser.add_argument("--region", nargs=4, type=float, metavar=("MIN_X", "MIN_Y", "MAX_X", "MAX_Y"))
    parser.add_argument("--limit", type=int, default=0, help="Maximum visible entities; zero exports all")
    parser.add_argument("--output", type=Path, default=ROOT / "Tools/three_d/generated/scene.json")
    args = parser.parse_args()
    if args.limit < 0:
        parser.error("--limit cannot be negative")
    if args.region and (not all(math.isfinite(value) for value in args.region) or
                        args.region[0] >= args.region[2] or args.region[1] >= args.region[3]):
        parser.error("--region must be finite and have MIN < MAX")
    root = args.root.resolve()
    map_path, definition = configured_map(root, args.variant, args.level)
    inventory = json.loads(args.inventory.read_text(encoding="utf-8"))
    models = []
    for path in ([args.models] if args.models.is_file() else sorted(args.models.glob("*.yml"))):
        models.extend(entry for entry in art_inventory.load_yaml(path.read_text(encoding="utf-8"))
                      if entry.get("type") == "cmu3DModel")
    kinds, load_issues, _ = art_inventory.load_prototypes(root)
    from solution_glass_states import configure_source
    configure_source(kinds)
    resolver = art_inventory.Resolver(kinds.get("entity", {}))
    levels = range(-len(definition.get('mapsBelow', [])), len(definition.get('mapsAbove', []))+1) if args.all_levels else [args.level]
    scenes = []
    for level in levels:
        map_path, definition = configured_map(root, args.variant, level)
        print(f"Reading {map_path.relative_to(root).as_posix()}...", flush=True)
        header, records = read_map(map_path)
        defaults, prototype_errors = {}, []
        for prototype_id in sorted({record["prototype"] for record in records.values()} - {""}):
            try:
                defaults[prototype_id] = art_inventory.component_map(resolver.resolve(prototype_id))
            except art_inventory.ResolutionError as error:
                prototype_errors.append({"prototype": prototype_id, "error": str(error)})
        data = build_scene(header, records, inventory, models, defaults, level=level,
                           region=tuple(args.region) if args.region else None, limit=args.limit)
        data["map"].update({"path": map_path.relative_to(root).as_posix(), "variant": args.variant,
                             "name": definition.get("mapName", args.variant)})
        data["diagnostics"]["prototypeResolutionErrors"] = prototype_errors
        data["diagnostics"]["prototypeLoadIssues"] = load_issues
        enrich_materials(data, root)
        scenes.append(data)
    if args.all_levels:
        from multiz import stack_scenes
        scene = stack_scenes(scenes, args.level)
    else:
        scene = scenes[0]
    from scene_io import write_scene
    write_scene(scene, args.output)
    print(json.dumps({"map": scene["map"], "diagnostics": scene["diagnostics"]}, indent=2))


if __name__ == "__main__":
    main()
