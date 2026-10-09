"""Build portable GLB assets and sprite/model review sheets from CMU model prototypes.

Requires PyYAML, Pillow, and NumPy. No Blender, remote service, or engine modification.
The editable YAML remains the source of truth for both this exporter and the game preview.
"""
from __future__ import annotations

import argparse
import hashlib
from io import BytesIO
import json
import math
from pathlib import Path
import re
import struct

from PIL import Image, ImageDraw, ImageFont
import numpy as np
import yaml
from primitives import ellipsoid_geometry, solid_geometry, triangle_count
import surfaces

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Content.CMU/Resources/ThreeD/Prototypes"
WORLD_SOURCE = SOURCE / "World"
EQUIPMENT_SOURCE = SOURCE / "Equipment"
OUTPUT = ROOT / "Content.CMU/Resources/Models/CMU14/Garrison"
REVIEW = ROOT / "Tools/three_d/generated/review"
VIEWER = ROOT / "Tools/three_d/generated"


def vector(value):
    values = value.split(",") if isinstance(value, str) else value
    if not isinstance(values, (list, tuple)) or len(values) != 3:
        raise ValueError(f"Expected three coordinates: {value!r}")
    result = tuple(float(v) for v in values)
    if not all(math.isfinite(v) and abs(v) <= 32 for v in result):
        raise ValueError(f"Nonfinite or out-of-budget coordinates: {value!r}")
    return result


def rgba(value):
    if not isinstance(value, str) or not re.fullmatch(r"#[0-9A-Fa-f]{6}(?:[0-9A-Fa-f]{2})?", value):
        raise ValueError(f"Expected #RRGGBB or #RRGGBBAA: {value!r}")
    return tuple(int(value[i:i + 2], 16) / 255 for i in (1, 3, 5)) + (
        int(value[7:9], 16) / 255 if len(value) == 9 else 1.0,)


def part_bounds(part):
    low, high = vector(part['min']), vector(part['max'])
    if not part.get('yaw', 0) and not part.get('pitch', 0):
        return low, high
    angle = math.radians(part.get('yaw', 0))
    c, s = abs(math.cos(angle)), abs(math.sin(angle))
    center = [(a+b)/2 for a,b in zip(low,high)]
    half = [(b-a)/2 for a,b in zip(low,high)]
    tilt = math.radians(part.get('pitch', 0))
    cp, sp = abs(math.cos(tilt)), abs(math.sin(tilt))
    half = [half[0]*cp+half[2]*sp, half[1], half[0]*sp+half[2]*cp]
    extent = [half[0]*c+half[1]*s,half[0]*s+half[1]*c,half[2]]
    return tuple(center[i]-extent[i] for i in range(3)), tuple(center[i]+extent[i] for i in range(3))


def validate_model(model, *, part_limit=128):
    model = dict(model)
    if model.get('equipmentOnly'):
        if model.get('sourcePrototypes') or model.get('randomSpritePrototypes'):
            raise ValueError(f"{model['id']}: equipment-only volumes cannot bind world entities")
        part_limit = 512
    if not re.fullmatch(r"[A-Za-z][A-Za-z0-9_]*", str(model.get("id", ""))):
        raise ValueError("Model ID must be a safe prototype identifier")
    if not model.get("label") or not isinstance(model["label"], str):
        raise ValueError(f"{model['id']}: missing label")
    if model.get("status", "draft") not in ("draft", "reviewed"):
        raise ValueError(f"{model['id']}: status must be draft or reviewed")
    refs = model.get("sourcePrototypes", [])
    if not isinstance(refs, list) or any(not isinstance(ref, str) or not ref for ref in refs):
        raise ValueError(f"{model['id']}: invalid sourcePrototypes")
    raw_parts = model.get("parts", [])
    if not isinstance(raw_parts, list) or not 1 <= len(raw_parts) <= part_limit:
        raise ValueError(f"{model['id']}: expected 1..{part_limit} parts")
    parts = []
    for index, part in enumerate(raw_parts):
        low, high = vector(part["min"]), vector(part["max"])
        if any(a >= b for a, b in zip(low, high)):
            raise ValueError(f"{model['id']} part {index}: bounds must enclose positive volume")
        color = part.get("color", "#FFFFFF")
        rgba(color)
        shape = part.get('shape', 'Box')
        if shape not in ('Box', 'Ellipsoid', 'CylinderX', 'CylinderY', 'CylinderZ', 'WedgeY', 'WedgeYReverse',
                         'SlantedX', 'SlantedXReverse', 'SlantedY', 'SlantedYReverse', 'Foliage'):
            raise ValueError(f"{model['id']} part {index}: unsupported shape {shape!r}")
        parts.append({"min": low, "max": high, "color": color.upper(),
                      "label": part.get("label", f"part-{index:02d}")})
        if shape != 'Box':
            parts[-1]['shape'] = shape
        yaw = part.get('yaw', 0)
        if type(yaw) not in (int, float) or not math.isfinite(yaw) or abs(yaw) > 360:
            raise ValueError(f"{model['id']} part {index}: invalid part yaw")
        if yaw:
            if model.get('connectToNeighbours') or model.get('wallMounted'):
                raise ValueError(f"{model['id']}: part yaw is unsupported for connected or wall-mounted geometry")
            parts[-1]['yaw'] = yaw
        pitch = part.get('pitch', 0)
        if type(pitch) not in (int, float) or not math.isfinite(pitch) or abs(pitch) > 90:
            raise ValueError(f"{model['id']} part {index}: invalid part pitch")
        if pitch:
            if model.get('connectToNeighbours') or model.get('wallMounted') or part.get('surface'):
                raise ValueError(f"{model['id']}: part pitch is unsupported for connected, wall-mounted or textured geometry")
            parts[-1]['pitch'] = pitch
        omit = part.get('omitWhenConnected', 0)
        if type(omit) is not int or not 0 <= omit <= 15:
            raise ValueError(f"{model['id']} part {index}: invalid connected edge mask")
        if omit:
            if not model.get('connectToNeighbours'):
                raise ValueError(f"{model['id']} part {index}: edge trim needs connected geometry")
            parts[-1]['omitWhenConnected'] = omit
        if part.get('surface'):
            surface, axis = part['surface'], part.get('surfaceAxis', 'XZ')
            if shape not in ('Box', 'WedgeY', 'WedgeYReverse') or axis not in surfaces.AXES or surface not in surfaces.load_surfaces():
                raise ValueError(f"{model['id']} part {index}: surface requires a known image, box/prism shape and XZ/XY/YZ projection")
            if model.get('connectToNeighbours'):
                raise ValueError(f"{model['id']}: connected surface UV transforms must be authored explicitly")
            if model.get('wallMounted') and axis != 'XZ':
                raise ValueError(f"{model['id']}: wall-mounted artwork requires XZ projection")
            if not isinstance(part.get('surfaceFlipU', False), bool):
                raise ValueError(f"{model['id']}: surfaceFlipU must be a boolean")
            parts[-1].update(surface=surface, surfaceAxis=axis)
            if part.get('surfaceFlipU'):
                parts[-1]['surfaceFlipU'] = True
        elif 'surfaceAxis' in part:
            raise ValueError(f"{model['id']} part {index}: surfaceAxis requires a surface")
    corners = model.get('cornerSurfaces', [])
    if not isinstance(corners, list) or (corners and (len(corners) != 32 or any(not isinstance(uid, str) or uid not in surfaces.load_surfaces() for uid in corners))):
        raise ValueError(f"{model['id']}: cornerSurfaces requires eight states in RSI S/N/E/W order")
    if corners:
        if (len(parts) < 4 or any(model.get(field) for field in ('connectToNeighbours', 'wallMounted', 'faceAwayFromWall', 'useEntityRotation', 'yawOffset', 'directionalModels')) or model.get('placement', 'floor') != 'floor'):
            raise ValueError(f"{model['id']}: corner surfaces require four leading grid-aligned patches")
        sx, sy = parts[0]['min'][0], parts[0]['max'][1]
        if not (-.5 < sx < .5 and -.5 < sy < .5):
            raise ValueError(f"{model['id']}: corner split must lie inside the tile")
        quadrants = ((sx, -.5, .5, sy), (sx, sy, .5, .5), (-.5, sy, sx, .5), (-.5, -.5, sx, sy))
        for part, (left, bottom, right, top) in zip(parts, quadrants):
            if (part['min'][:2] != (left, bottom) or part['max'][:2] != (right, top) or part.get('shape', 'Box') != 'Box' or part.get('yaw') or part.get('pitch') or part.get('surfaceFlipU') or part.get('surfaceAxis') != 'XY'):
                raise ValueError(f"{model['id']}: corner parts must tile unrotated SE/NE/NW/SW rectangles with XY artwork")
    if model.get("placement", "floor") not in ("floor", "surface"):
        raise ValueError(f"{model['id']}: placement must be floor or surface")
    support_labels = model.get('supportSurfaces', [])
    if (not isinstance(support_labels, list) or any(not isinstance(label, str) or not label for label in support_labels)
            or len(set(support_labels)) != len(support_labels)):
        raise ValueError(f"{model['id']}: supportSurfaces must contain unique nonempty part labels")
    if support_labels and (model.get('supportSurface') or model.get('connectToNeighbours')):
        raise ValueError(f"{model['id']}: supportSurfaces cannot combine with supportSurface or connected geometry")
    for label in support_labels or ([model['supportSurface']] if model.get('supportSurface') else []):
        matches = [part for part in parts if part['label'] == label]
        if len(matches) != 1 or matches[0].get('shape', 'Box') != 'Box' or matches[0]['max'][2] <= 0 or matches[0].get('yaw', 0) or matches[0].get('pitch', 0):
            raise ValueError(f"{model['id']}: supportSurface(s) must name exactly one positive-height Box per label")
    if model.get('sourceDirections', 1) not in (1, 4, 8):
        raise ValueError(f"{model['id']}: sourceDirections must be 1, 4 or 8")
    direction = model.get('referenceDirection')
    if direction is not None and (type(direction) is not int or not 0 <= direction < model.get('sourceDirections', 1)
                                  or not model.get('referenceRsi') or not model.get('referenceState')):
        raise ValueError(f"{model['id']}: referenceDirection requires a valid explicit RSI direction")
    directed = model.get('directionalModels', [])
    if (not isinstance(directed, list) or any(not isinstance(uid, str) for uid in directed) or
            (directed and (model.get('sourceDirections') not in (4, 8) or len(directed) != model['sourceDirections']
                           or direction is None or directed[direction] != model['id']))):
        raise ValueError(f"{model['id']}: directionalModels must contain its own indexed pose and one slot per RSI direction")
    facings = model.get('sourceCardinalFacings', [])
    if (not isinstance(facings, list) or (facings and (model.get('sourceDirections', 1) != 4 or
            len(facings) != 4 or any(type(value) is not int or not 0 <= value <= 3 for value in facings)))):
        raise ValueError(f"{model['id']}: sourceCardinalFacings must map four source frames to physical turns 0..3")
    targets = model.get('wallFacingTargets', [])
    if not isinstance(targets, list) or any(not isinstance(p, str) or not p for p in targets) or (targets and not model.get('wallMounted')):
        raise ValueError(f"{model['id']}: wallFacingTargets requires named prototypes and a wall-mounted model")
    back_targets = model.get('backWallMountTargets', [])
    inset = model.get('connectionEndInset', 0)
    if (type(inset) not in (int, float) or not math.isfinite(inset) or not 0 <= inset <= .2 or
            inset and (not model.get('connectToNeighbours') or model.get('supportSurface') or model.get('supportSurfaces'))):
        raise ValueError(f"{model['id']}: connectionEndInset requires a connected panel and a finite inset up to .2")
    if type(model.get('fitInsideWall', False)) is not bool or (model.get('fitInsideWall') and
            (not model.get('wallMounted') or not back_targets)):
        raise ValueError(f"{model['id']}: fitInsideWall requires a wall fixture and explicit backing prototypes")
    if (not isinstance(back_targets, list) or any(not isinstance(p, str) or not p for p in back_targets)
            or len(set(back_targets)) != len(back_targets)
            or (back_targets and (any(model.get(field) for field in ('connectToNeighbours', 'faceAwayFromWall', 'cornerSurfaces', 'windowMountTargets'))
                                 or model.get('placement', 'floor') not in ('floor', 'surface')))):
        raise ValueError(f"{model['id']}: rear wall mounting requires unique prototype targets and independent floor/surface geometry")
    window_targets = model.get('windowMountTargets', [])
    if type(model.get('windowMountInside',False)) is not bool or (model.get('windowMountInside') and not window_targets):
        raise ValueError(f"{model['id']}: windowMountInside requires explicit glazing targets")
    panel_targets = model.get('panelEndTargets', [])
    opening_targets = model.get('openingFacingTargets', [])
    if (not isinstance(opening_targets,list) or any(not isinstance(p,str) or not p for p in opening_targets) or
            len(set(opening_targets)) != len(opening_targets) or opening_targets and
            (not model.get('useEntityRotation') or model.get('sourceDirections',1) != 1 or
             any(model.get(k) for k in ('wallMounted','connectToNeighbours','faceAwayFromWall','cornerSurfaces','supportSurface','supportSurfaces')))):
        raise ValueError(f"{model['id']}: openingFacingTargets requires named fixtures and an independent single-frame opening")
    if (not isinstance(panel_targets,list) or any(not isinstance(p,str) or not p for p in panel_targets) or
            len(set(panel_targets)) != len(panel_targets) or
            panel_targets and (any(model.get(k) for k in ('connectToNeighbours','wallMounted','faceAwayFromWall','cornerSurfaces','backWallMountTargets','supportSurface','supportSurfaces')) or
                any(p.get('shape','Box') != 'Box' or p.get('yaw',0) or p.get('pitch',0) for p in model['parts']))):
        raise ValueError(f"{model['id']}: panelEndTargets requires independent axis-aligned box parts and unique prototypes")
    if (not isinstance(window_targets, list) or any(not isinstance(p, str) or not p for p in window_targets)
            or len(set(window_targets)) != len(window_targets)
            or (window_targets and (any(model.get(field) for field in ('connectToNeighbours', 'wallMounted', 'faceAwayFromWall', 'cornerSurfaces'))
                                   or model.get('placement', 'floor') != 'floor'))):
        raise ValueError(f"{model['id']}: window mounting requires unique prototype targets and independent floor geometry")
    if not isinstance(model.get('swapEastWest', False), bool) or (model.get('swapEastWest') and model.get('sourceDirections', 1) != 4):
        raise ValueError(f"{model['id']}: swapEastWest requires a four-direction source")
    if (not isinstance(model.get('connectToNeighbours', False), bool) or
            not isinstance(model.get('faceAwayFromWall', False), bool) or
            not isinstance(model.get('useEntityRotation', False), bool) or not math.isfinite(model.get('yawOffset', 0))):
        raise ValueError(f"{model['id']}: invalid layout metadata")
    if (not isinstance(model.get('faceAwayFromWindows', False), bool) or
            (model.get('faceAwayFromWindows') and not model.get('faceAwayFromWall'))):
        raise ValueError(f"{model['id']}: window backing requires wall-context appliance facing")
    if model.get('doorState', 'Closed') not in ('Closed', 'Open'):
        raise ValueError(f"{model['id']}: only stable Closed/Open door poses may be authored")
    if not isinstance(model.get('folded', False), bool):
        raise ValueError(f"{model['id']}: folded must be a boolean pose")
    if model.get('alternateFoldModel') and (not isinstance(model['alternateFoldModel'], str) or
            not model.get('referenceRsi') or not model.get('referenceState')):
        raise ValueError(f"{model['id']}: alternateFoldModel requires an explicit source RSI and state")
    if bool(model.get('referenceRsi')) != bool(model.get('referenceState')):
        raise ValueError(f"{model['id']}: explicit RSI references require both referenceRsi and referenceState")
    random_sources = model.get('randomSpritePrototypes', [])
    if (not isinstance(random_sources, list) or any(not isinstance(p, str) or not p for p in random_sources) or
            len(set(random_sources)) != len(random_sources) or
            (random_sources and (not isinstance(model.get('randomSpriteLayer'), str) or
                                 not model['randomSpriteLayer'] or not model.get('referenceState') or not model.get('referenceRsi')))):
        raise ValueError(f"{model['id']}: random sprite variants require explicit prototype, layer, RSI and state")
    rgba(model.get('referenceTint', '#FFFFFF'))
    rgba(model.get('bakedSpriteTint', '#FFFFFF'))
    if 'groundOffset' in model:
        offset = model['groundOffset']
        offset = offset.split(',') if isinstance(offset, str) else offset
        if not isinstance(offset, (list, tuple)) or len(offset) != 2:
            raise ValueError(f"{model['id']}: groundOffset requires two map-axis coordinates")
        offset = tuple(float(v) for v in offset)
        if not all(math.isfinite(v) and abs(v) <= 32 for v in offset):
            raise ValueError(f"{model['id']}: invalid groundOffset")
        model['groundOffset'] = offset
    from terrain_cutouts import validate_contract
    validate_contract(model, vector)
    states = model.get('doorButtonStates', {})
    animations = model.get('frameAnimations', {})
    if not isinstance(states, dict) or not isinstance(animations, dict) or bool(states) != bool(animations):
        raise ValueError(f"{model['id']}: source-frame states and animation timelines must be supplied together")
    if states:
        if (not model.get('referenceRsi') or not model.get('referenceState') or model.get('sourceDirections', 1) != 1 or
                any(model.get(field) for field in ('cornerSurfaces', 'connectToNeighbours', 'directionalModels'))):
            raise ValueError(f"{model['id']}: door-control frames require a single-direction explicit RSI reference")
        validated = {}
        template = {k: v for k, v in model.items() if k not in ('doorButtonStates', 'frameAnimations')}
        for state, definition in states.items():
            if not isinstance(state, str) or not state or ':' in state or not isinstance(definition, dict):
                raise ValueError(f"{model['id']}: invalid source state")
            validated[state] = {}
            for key in ('frames', 'unpoweredFrames'):
                frames = definition.get(key)
                if not isinstance(frames, list) or not 1 <= len(frames) <= 64:
                    raise ValueError(f"{model['id']}: each source state requires 1..64 frames and power compositions")
                validated[state][key] = [{'parts': validate_model({**template, 'parts': frame['parts']})['parts']} for frame in frames]
            if len(validated[state]['frames']) != len(validated[state]['unpoweredFrames']):
                raise ValueError(f"{model['id']}: powered and unpowered frame counts must match")
        if (model['referenceState'] not in validated or
                parts != validated[model['referenceState']]['frames'][0]['parts']):
            raise ValueError(f"{model['id']}: default model must equal the initial powered reference frame")
        for name, keys in animations.items():
            if not isinstance(name, str) or not name or not isinstance(keys, list) or len(keys) < 2:
                raise ValueError(f"{model['id']}: animation requires a name and at least two keys")
            previous = -1
            for key in keys:
                time, state, frame = key.get('time'), key.get('state'), key.get('frame', 0)
                if (type(time) not in (int, float) or not math.isfinite(time) or time < 0 or time <= previous or
                        state not in validated or type(frame) is not int or frame < 0 or
                        frame >= len(validated[state]['frames']) or type(key.get('unpowered', False)) is not bool):
                    raise ValueError(f"{model['id']}: animation keys must be ordered finite times and known frames")
                previous = time
            if keys[0]['time'] != 0:
                raise ValueError(f"{model['id']}: animation must start at zero")
        model['doorButtonStates'] = validated
    result = {**model, "parts": parts, "sourcePrototypes": refs, "status": model.get("status", "draft")}
    if any(k in model for k in ('floorOpening', 'ceilingOpening')):
        from slab_openings import validate
        result = validate(result)
    if 'reagentTankAppearance' in model:
        from reagent_tank_states import validate
        result = validate(result)
    if 'chargerAppearance' in model:
        from charger_states import validate
        result = validate(result, validate_model)
    if 'foamAppearance' in model:
        from foam_wall_states import validate
        result = validate(result, validate_model)
    if 'solutionAppearance' in model:
        from solution_glass_states import validate
        result = validate(result, validate_model)
    # CMU14: independently visible vehicle hardpoints use the existing source owners.
    if 'vehicleLayers' in model:
        from vehicle_states import validate, validate_source
        result = validate(result, validate_model)
        validate_source(result, resource_file)
    if any(k in model for k in ('spriteStates', 'sourceSpriteOffset', 'sourceSpriteRotates')):
        from sprite_states import validate
        result = validate(result, validate_model)
    if 'poweredLightStates' in model:
        from light_states import validate
        result = validate(result, validate_model)
    if any(k in model for k in ('doorSpriteStates', 'doorAnimationDurations')):
        from door_states import validate
        result = validate(result, validate_model)
    if any(key.startswith('barricade') for key in model):
        from barricade_states import validate
        result = validate(result, validate_model)
    if 'wallPaper' in model:
        from wall_paper import validate
        result = validate(result)
    return result


def load_models(source=SOURCE):
    paths = [source] if source.is_file() else sorted(source.rglob("*.yml"))
    models, seen, references, random_references = [], set(), {}, set()
    for path in paths:
        entries = yaml.load(path.read_text(encoding="utf-8"), Loader=getattr(yaml, "CSafeLoader", yaml.SafeLoader))
        if not isinstance(entries, list):
            raise ValueError(f"Expected prototype list: {path}")
        for entry in entries:
            if entry.get("type") != "cmu3DModel":
                continue
            model = validate_model(entry)
            from disposal_pipe_states import validate as validate_anchor
            validate_anchor(model)
            if 'supportProbePart' in model:
                from support_probe import validate
                validate(model)
            if model.get('reagentTankAppearance'):
                from reagent_tank_states import validate_source
                validate_source(model, resource_file)
            if model.get('chargerAppearance'):
                from charger_states import validate_source
                validate_source(model, resource_file)
            if model.get('foamAppearance'):
                from foam_wall_states import validate_source
                validate_source(model, resource_file)
            if model.get('solutionAppearance'):
                from solution_glass_states import validate_source
                validate_source(model, resource_file)
            if model.get('spriteStates'):
                from sprite_states import validate_source
                validate_source(model, resource_file)
            if model.get('wallPaper'):
                from wall_paper import validate_source
                validate_source(model, resource_file)
            if model["id"] in seen:
                raise ValueError(f"Duplicate model ID: {model['id']}")
            for source_id in model["sourcePrototypes"]:
                if source_id in references:
                    raise ValueError(f"Duplicate source prototype {source_id}: {references[source_id]} and {model['id']}")
                references[source_id] = model["id"]
            seen.add(model["id"])
            for source_id in model.get('randomSpritePrototypes', []):
                key = (source_id, model['randomSpriteLayer'], model['referenceState'])
                if key in random_references:
                    raise ValueError(f"Duplicate random sprite model mapping: {key}")
                random_references.add(key)
            models.append(model)
    if not models:
        raise ValueError(f"No cmu3DModel prototypes in {source}")
    from disposal_pipe_states import validate_links as validate_anchor_links
    validate_anchor_links(models)
    by_id = {model['id']: model for model in models}
    for model in models:
        for direction, uid in enumerate(model.get('directionalModels', [])):
            if not uid:
                continue
            target = by_id.get(uid)
            if (not target or target.get('referenceDirection') != direction or
                    any(target.get(key) != model.get(key) for key in
                        ('directionalModels', 'sourceDirections', 'referenceRsi', 'referenceState', 'sourceSpriteRotates'))):
                raise ValueError(f"{model['id']}: directional models must link matching reciprocal source-frame poses")
            if model.get('spriteStates') and (set(target.get('spriteStates', {})) != set(model['spriteStates']) or
                    target.get('sourceSpriteOffset') != model.get('sourceSpriteOffset') or
                    any(target['spriteStates'][state]['delays'] != data['delays'] for state, data in model['spriteStates'].items())):
                raise ValueError(f"{model['id']}: directional sprite states require matching states, source offsets and timing")
        if alternate := model.get('alternateFoldModel'):
            target = by_id.get(alternate)
            if not target or target.get('folded', False) == model.get('folded', False) or target.get('alternateFoldModel') != model['id']:
                raise ValueError(f"{model['id']}: alternate fold model must be a reciprocal opposite pose")
        if alternate := model.get('alternateDoorModel'):
            target = by_id.get(alternate)
            if not target or target.get('doorState', 'Closed') == model.get('doorState', 'Closed') or target.get('alternateDoorModel') != model['id']:
                raise ValueError(f"{model['id']}: alternate door model must be a reciprocal opposite pose")
    return sorted(models, key=lambda model: model["id"])


def linear(channel):
    return channel / 12.92 if channel <= 0.04045 else ((channel + 0.055) / 1.055) ** 2.4


# Each tangent pair has cross(u, v) = normal: triangle winding is outward in glTF.
FACES = (
    ((1, 0, 0), (0, 1, 0), (0, 0, 1)),
    ((-1, 0, 0), (0, -1, 0), (0, 0, 1)),
    ((0, 1, 0), (0, 0, 1), (1, 0, 0)),
    ((0, -1, 0), (0, 0, -1), (1, 0, 0)),
    ((0, 0, 1), (1, 0, 0), (0, 1, 0)),
    ((0, 0, -1), (-1, 0, 0), (0, 1, 0)),
)


def cube_geometry():
    positions, normals, indices = [], [], []
    for normal, tangent, bitangent in FACES:
        start = len(positions)
        for u, v in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            positions.append(tuple((normal[i] + u * tangent[i] + v * bitangent[i]) * .5 for i in range(3)))
            normals.append(normal)
        indices.extend((start, start + 1, start + 2, start, start + 2, start + 3))
    return positions, normals, indices


def glb_document(model):
    positions, normals, indices = cube_geometry()
    pbytes = struct.pack("<72f", *(v for point in positions for v in point))
    nbytes = struct.pack("<72f", *(v for point in normals for v in point))
    ibytes = struct.pack("<36H", *indices)
    binary = pbytes + nbytes + ibytes
    materials, meshes, nodes, color_ids, mesh_ids = [], [], [], {}, {}
    views = [
        {"buffer": 0, "byteOffset": 0, "byteLength": len(pbytes), "target": 34962},
        {"buffer": 0, "byteOffset": len(pbytes), "byteLength": len(nbytes), "target": 34962},
        {"buffer": 0, "byteOffset": len(pbytes) + len(nbytes), "byteLength": len(ibytes), "target": 34963}]
    accessors = [
        {"bufferView": 0, "componentType": 5126, "count": 24, "type": "VEC3", "min": [-.5]*3, "max": [.5]*3},
        {"bufferView": 1, "componentType": 5126, "count": 24, "type": "VEC3"},
        {"bufferView": 2, "componentType": 5123, "count": 36, "type": "SCALAR"}]
    shape_offsets = {'Box': 0}
    for shape in sorted({part.get('shape', 'Box') for part in model['parts']} - {'Box'}):
        shape_offsets[shape] = len(accessors)
        vertices, sphere_normals, sphere_indices = solid_geometry(shape)
        vertices = [(x, z, -y) for x, y, z in vertices]
        sphere_normals = [(x, z, -y) for x, y, z in sphere_normals]
        for values, component, target, kind, count in [
            ([v for point in vertices for v in point], 'f', 34962, 'VEC3', len(vertices)),
            ([v for point in sphere_normals for v in point], 'f', 34962, 'VEC3', len(vertices)),
            (sphere_indices, 'H', 34963, 'SCALAR', len(sphere_indices))]:
            chunk = struct.pack(f'<{len(values)}{component}', *values)
            views.append({'buffer': 0, 'byteOffset': len(binary), 'byteLength': len(chunk), 'target': target})
            accessor = {'bufferView': len(views)-1, 'componentType': 5126 if component == 'f' else 5123,
                        'count': count, 'type': kind}
            if len(accessors) == shape_offsets[shape]:
                # Slanted solids stay inside the unit bounds, but their sampled mesh
                # extrema need not touch every bound. glTF records the actual data.
                points = np.frombuffer(chunk, dtype='<f4').reshape((-1, 3))
                accessor.update(min=points.min(axis=0).tolist(), max=points.max(axis=0).tolist())
            accessors.append(accessor)
            binary += chunk + b'\0' * (-len(chunk) % 4)
    images, texture_ids, uv_accessors = [], {}, {}
    def surface_uv_accessor(axis, flip, shape):
        nonlocal binary
        uv_key = (axis, flip, shape)
        if uv_key not in uv_accessors:
            canonical = [(x,-z,y) for x,y,z in positions] if shape == 'Box' else solid_geometry(shape)[0]
            coords = [surfaces.uv(p, axis) for p in canonical]
            values = [component for u, v in coords for component in ((1-u if flip else u), v)]
            chunk = struct.pack(f'<{len(values)}f', *values)
            views.append({'buffer': 0, 'byteOffset': len(binary), 'byteLength': len(chunk), 'target': 34962})
            accessors.append({'bufferView': len(views)-1, 'componentType': 5126, 'count': len(canonical), 'type': 'VEC2'})
            uv_accessors[uv_key] = len(accessors)-1
            binary += chunk
        return uv_accessors[uv_key]
    for part in model["parts"]:
        color = part["color"]
        surface_id = part.get('surface')
        material_key = (color, surface_id)
        if material_key not in color_ids:
            material_id = len(materials)
            color_ids[material_key] = material_id
            r, g, b, a = rgba(color)
            material = {"name": color, "pbrMetallicRoughness": {
                "baseColorFactor": [linear(r), linear(g), linear(b), a],
                "metallicFactor": 0, "roughnessFactor": .82}}
            if a < 1:
                material.update(alphaMode="BLEND", doubleSided=True)
            if surface_id:
                if surface_id not in texture_ids:
                    image_bytes = surfaces.png_bytes(surfaces.load_surfaces()[surface_id]['image'])
                    views.append({'buffer': 0, 'byteOffset': len(binary), 'byteLength': len(image_bytes)})
                    binary += image_bytes + b'\0' * (-len(image_bytes) % 4)
                    texture_ids[surface_id] = len(images)
                    images.append({'name': surface_id, 'bufferView': len(views)-1, 'mimeType': 'image/png'})
                material['name'] += '/' + surface_id
                material['pbrMetallicRoughness']['baseColorTexture'] = {'index': texture_ids[surface_id]}
                source_alpha = surfaces.load_surfaces()[surface_id]['image'].getchannel('A').histogram()
                if a < 1 or any(source_alpha[128:255]):
                    material.update(alphaMode='BLEND', doubleSided=True)
                else:
                    material.update(alphaMode='MASK', alphaCutoff=.5, doubleSided=True)
            materials.append(material)
        shape = part.get('shape', 'Box')
        key = color, shape, surface_id, part.get('surfaceAxis'), part.get('surfaceFlipU',False)
        if key not in mesh_ids:
            mesh_ids[key] = len(meshes)
            offset = shape_offsets[shape]
            meshes.append({"name": f"solid-{len(meshes)}", "primitives": [{
                "attributes": {"POSITION": offset, "NORMAL": offset+1}, "indices": offset+2, "material": color_ids[material_key]}]})
            if surface_id:
                meshes[-1]['primitives'][0]['attributes']['TEXCOORD_0'] = surface_uv_accessor(part.get('surfaceAxis', 'XZ'), part.get('surfaceFlipU',False), shape)
        center = [(a + b) / 2 for a, b in zip(part["min"], part["max"])]
        size = [b - a for a, b in zip(part["min"], part["max"])]
        # Right-handed rotation: game (x, y, z) -> glTF (x, z, -y).
        nodes.append({"name": part["label"], "mesh": mesh_ids[key],
                      "translation": [center[0], center[2], -center[1]],
                      "scale": [size[0], size[2], size[1]]})
        if part.get('yaw', 0) or part.get('pitch', 0):
            angle = math.radians(part.get('yaw', 0)) / 2
            tilt = math.radians(part.get('pitch', 0)) / 2
            sy, cy, sp, cp = math.sin(angle), math.cos(angle), math.sin(tilt), math.cos(tilt)
            nodes[-1]['rotation'] = [sy*sp, sy*cp, cy*sp, cy*cp]
    data = {"asset": {"version": "2.0", "generator": "CMU solid-part exporter",
                      "copyright": "CMU contributors; see SOURCES*.md for model and reference attribution."},
            "scene": 0, "scenes": [{"name": model["label"], "nodes": list(range(len(nodes)))}],
            "nodes": nodes, "meshes": meshes, "materials": materials,
            "buffers": [{"byteLength": len(binary)}],
            "bufferViews": views, "accessors": accessors,
            "extras": {"cmuPrototype": model["id"], "status": model["status"],
                       "sourcePrototypes": model["sourcePrototypes"], "sourceCoordinateSystem": "Z-up; front -Y; one tile per unit"}}
    if images:
        data.update(images=images, textures=[{'source': index, 'sampler': 0} for index in range(len(images))],
                    samplers=[{'magFilter': 9728, 'minFilter': 9728, 'wrapS': 33071, 'wrapT': 33071}])
    for key in ('equipmentOnly', 'doorState', 'alternateDoorModel', 'folded', 'alternateFoldModel', 'randomSpritePrototypes', 'randomSpriteLayer', 'referencePrototype', 'referenceRsi', 'referenceState', 'referenceDirection', 'directionalModels', 'referenceTint', 'useEntityRotation', 'bakedSpriteTint', 'swapEastWest', 'faceAwayFromWall', 'sourceCardinalFacings', 'placement', 'supportSurface', 'supportSurfaces', 'groundOffset', 'terrainCutoutTargets', 'terrainCutoutMin', 'terrainCutoutMax', 'floorOpening', 'ceilingOpening', 'preserveSlabCladding', 'anchored', 'alternateAnchorModel'):
        if key in model:
            data['extras'][key] = model[key]
    return data, binary


def encode_glb(data, binary):
    encoded = json.dumps(data, ensure_ascii=False, separators=(",", ":"), allow_nan=False).encode("utf-8")
    encoded += b" " * (-len(encoded) % 4)
    binary += b"\0" * (-len(binary) % 4)
    return (struct.pack("<III", 0x46546C67, 2, 12 + 8 + len(encoded) + 8 + len(binary)) +
            struct.pack("<II", len(encoded), 0x4E4F534A) + encoded +
            struct.pack("<II", len(binary), 0x004E4942) + binary)


def glb_bytes(model):
    if model.get('foamAppearance'):
        from foam_wall_states import model_document, validate_source
        validate_source(model, resource_file)
        return encode_glb(*model_document(model, glb_document))
    if model.get('solutionAppearance'):
        from solution_glass_states import model_document, validate_source
        validate_source(model, resource_file)
        return encode_glb(*model_document(model, glb_document))
    if model.get('chargerAppearance'):
        from charger_states import model_document, validate_source
        validate_source(model, resource_file)
        return encode_glb(*model_document(model, glb_document))
    if model.get('reagentTankAppearance'):
        from reagent_tank_states import model_document, validate_source
        validate_source(model, resource_file)
        return encode_glb(*model_document(model, glb_document))
    if model.get('spriteStates'):
        from sprite_states import model_document, validate_source
        validate_source(model, resource_file)
        return encode_glb(*model_document(model, glb_document))
    if model.get('doorSpriteStates'):
        from door_states import model_document
        return encode_glb(*model_document(model, glb_document))
    if model.get('poweredLightStates'):
        from light_states import model_document
        return encode_glb(*model_document(model, glb_document))
    if model.get('barricadeDamageStates'):
        from barricade_states import model_document
        return encode_glb(*model_document(model, glb_document))
    if model.get('doorButtonStates'):
        from frame_animation import model_document
        return encode_glb(*model_document(model, glb_document))
    return encode_glb(*glb_document(model))


def dot(a, b):
    return sum(x * y for x, y in zip(a, b))


def render_model(model, size=(220, 220), yaw=-math.pi / 3, pitch=.55, *, pixels_per_unit=None, screen_origin=None):
    """Orthographic review with depth and dithered glass, matching the browser's alpha policy."""
    if pixels_per_unit is not None and (not math.isfinite(pixels_per_unit) or pixels_per_unit <= 0):
        raise ValueError('Review pixels_per_unit must be finite and positive')
    if screen_origin is not None and (pixels_per_unit is None or len(screen_origin) != 2 or not all(math.isfinite(v) for v in screen_origin)):
        raise ValueError('Review screen_origin requires a fixed scale and two finite coordinates')
    pixels = np.full((size[1], size[0], 3), (23, 33, 43), dtype=np.uint8)
    depths = np.full((size[1], size[0]), -np.inf, dtype=np.float64)
    pattern = (np.array([[0, 8, 2, 10], [12, 4, 14, 6],
                         [3, 11, 1, 9], [15, 7, 13, 5]]) + .5) / 16
    right = (-math.sin(yaw), math.cos(yaw), 0)
    up = (-math.cos(yaw) * math.sin(pitch), -math.sin(yaw) * math.sin(pitch), math.cos(pitch))
    eye = (math.cos(yaw) * math.cos(pitch), math.sin(yaw) * math.cos(pitch), math.sin(pitch))
    faces = []
    all_points = []
    for part in model["parts"]:
        center = [(a + b) / 2 for a, b in zip(part["min"], part["max"])]
        extent = [(b - a) / 2 for a, b in zip(part["min"], part["max"])]
        polygons = []
        smooth_faces = {}
        if part.get('shape', 'Box') != 'Box':
            vertices, vertex_normals, indices = solid_geometry(part['shape'])
            points = [tuple(center[i] + 2 * extent[i] * v[i] for i in range(3)) for v in vertices]
            for j in range(0, len(indices), 3):
                polygon = [points[indices[j+k]] for k in range(3)]
                normal = np.cross(np.subtract(polygon[1], polygon[0]), np.subtract(polygon[2], polygon[0]))
                normal /= np.linalg.norm(normal)
                polygons.append((normal, polygon))
                smooth_faces[id(polygon)] = [vertex_normals[indices[j+k]] for k in range(3)]
        else:
            for normal, tangent, bitangent in FACES:
                points = [tuple(center[i] + extent[i] * (normal[i] + u * tangent[i] + v * bitangent[i]) for i in range(3))
                          for u, v in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
                polygons.append((normal, points))
        for normal, points in polygons:
            smooth_normals = smooth_faces.get(id(points))
            if part.get('yaw', 0) or part.get('pitch', 0):
                angle = math.radians(part.get('yaw', 0))
                c, s = math.cos(angle), math.sin(angle)
                tilt = math.radians(part.get('pitch', 0))
                cp, sp = math.cos(tilt), math.sin(tilt)
                rotate = lambda p: (c*(cp*p[0]-sp*p[2])-s*p[1],s*(cp*p[0]-sp*p[2])+c*p[1],sp*p[0]+cp*p[2])
                points = [tuple(center[i]+value for i,value in enumerate(rotate(tuple(p[j]-center[j] for j in range(3))))) for p in points]
                normal = rotate(normal)
            if dot(normal, eye) <= 0 and not part.get('surface'):
                continue
            projected = [(dot(p, right), -dot(p, up)) for p in points]
            all_points.extend(projected)
            shade = .62 + .38 * max(0, dot(normal, (-.35, -.45, .82)))
            r, g, b, a = rgba(part["color"])
            color = tuple(round(255 * channel * shade) for channel in (r, g, b))
            if smooth_normals is not None:
                color = []
                for vertex_normal in smooth_normals:
                    smooth = np.array([vertex_normal[i] / extent[i] for i in range(3)])
                    smooth /= np.linalg.norm(smooth)
                    if part.get('yaw', 0) or part.get('pitch', 0):
                        smooth = rotate(smooth)
                    brightness = .62 + .38 * max(0, dot(smooth, (-.35, -.45, .82)))
                    color.append([255 * channel * brightness for channel in (r, g, b)])
            image, uvs = None, None
            if part.get('surface'):
                image = np.asarray(surfaces.load_surfaces()[part['surface']]['image'])
                local_points = [tuple(point[i]-center[i] for i in range(3)) for point in points]
                if part.get('yaw', 0):
                    local_points = [(c*p[0]+s*p[1],-s*p[0]+c*p[1],p[2]) for p in local_points]
                uvs = [surfaces.uv([point[i]/(2*extent[i]) for i in range(3)], part.get('surfaceAxis','XZ')) for point in local_points]
                if part.get('surfaceFlipU'): uvs = [(1-u, v) for u,v in uvs]
            faces.append((projected, [dot(p, eye) for p in points], color, a, image, uvs))
    low = [min(p[i] for p in all_points) for i in range(2)]
    high = [max(p[i] for p in all_points) for i in range(2)]
    scale = min((size[i] - 32) / max(.001, high[i] - low[i]) for i in range(2))
    center = [(a + b) / 2 for a, b in zip(low, high)]
    for points, distance, color, alpha, image, uvs in faces:
        if pixels_per_unit is None:
            screen = [(size[0] / 2 + (p[0] - center[0]) * scale,
                       size[1] / 2 + (p[1] - center[1]) * scale) for p in points]
        else:
            origin = screen_origin or (size[0] / 2, size[1] / 2)
            screen = [(origin[0] + p[0] * pixels_per_unit, origin[1] + p[1] * pixels_per_unit) for p in points]
        # A per-pixel z-buffer matters even for simple boxes: sorting the center of a large face
        # can otherwise paint over small buttons below its center which sit in front of it.
        for indices in (((0, 1, 2),) if len(points) == 3 else ((0, 1, 2), (0, 2, 3))):
            triangle = [screen[index] for index in indices]
            z = [distance[index] for index in indices]
            x0, y0 = triangle[0]
            x1, y1 = triangle[1]
            x2, y2 = triangle[2]
            denominator = (y1 - y2) * (x0 - x2) + (x2 - x1) * (y0 - y2)
            if abs(denominator) < 1e-9:
                continue
            left = max(0, math.floor(min(p[0] for p in triangle)))
            top = max(0, math.floor(min(p[1] for p in triangle)))
            right_edge = min(size[0], math.ceil(max(p[0] for p in triangle)))
            bottom = min(size[1], math.ceil(max(p[1] for p in triangle)))
            if right_edge <= left or bottom <= top:
                continue
            x, y = np.meshgrid(np.arange(left, right_edge) + .5, np.arange(top, bottom) + .5)
            w0 = ((y1 - y2) * (x - x2) + (x2 - x1) * (y - y2)) / denominator
            w1 = ((y2 - y0) * (x - x2) + (x0 - x2) * (y - y2)) / denominator
            w2 = 1 - w0 - w1
            depth = w0 * z[0] + w1 * z[1] + w2 * z[2]
            local_depths = depths[top:bottom, left:right_edge]
            visible = (w0 >= -1e-8) & (w1 >= -1e-8) & (w2 >= -1e-8) & (depth >= local_depths - 1e-8)
            # Discarded glass pixels must not occlude interior solids. A shared pixel
            # pattern avoids order dependence and double tinting overlapping box faces.
            coverage = alpha
            sampled = None
            if image is not None:
                coords = [np.asarray(uvs[index]) for index in indices]
                texture_uv = w0[...,None]*coords[0] + w1[...,None]*coords[1] + w2[...,None]*coords[2]
                tx = np.clip((texture_uv[...,0]*image.shape[1]).astype(int), 0, image.shape[1]-1)
                ty = np.clip((texture_uv[...,1]*image.shape[0]).astype(int), 0, image.shape[0]-1)
                sampled = image[ty,tx]
                visible &= sampled[...,3] >= 128
                coverage = alpha * sampled[...,3] / 255
            visible &= coverage >= pattern[y.astype(int) % 4, x.astype(int) % 4]
            local_depths[visible] = depth[visible]
            shaded = np.asarray(color)
            if np.ndim(color) == 2:
                colors = [np.asarray(color[index]) for index in indices]
                shaded = w0[..., None] * colors[0] + w1[..., None] * colors[1] + w2[..., None] * colors[2]
            if sampled is not None:
                shaded = sampled[...,:3] * (shaded / 255)
            if sampled is not None or np.ndim(color) == 2:
                pixels[top:bottom, left:right_edge][visible] = np.rint(shaded[visible]).astype(np.uint8)
            else:
                pixels[top:bottom, left:right_edge][visible] = color
    return Image.fromarray(pixels)


def resource_file(path):
    path = str(path).replace("\\", "/").removeprefix("/")
    if not path.startswith("Textures/"):
        path = "Textures/" + path
    for resources in (ROOT / "Content.CMU/Resources", ROOT / "Resources"):
        result = (resources / path).resolve()
        if result.is_relative_to(resources.resolve()) and result.exists():
            return result
    return None


def reference_frame(model, inventory, barricade_wired=False, barricade_acid_frame=None):
    if model.get('foamAppearance'):
        from foam_wall_states import viewer_references
        files, refs = viewer_references(model, resource_file, surfaces.png_bytes)
        template = {k: v for k, v in model.items() if k != 'foamAppearance'}
        _, info = reference_frame(template, inventory)
        key = refs['edges-15'][0].removeprefix('../generated/')
        return Image.open(BytesIO(files[key])).convert('RGBA'), {
            **info, 'note': 'Original five foam layers with source offsets and alpha; isolated exposed-edge pose.'}
    if model.get('solutionAppearance'):
        from solution_glass_states import composite
        template = {k: v for k, v in model.items() if k != 'solutionAppearance'}
        _, info = reference_frame(template, inventory)
        layers = model['solutionAppearance']['defaultLayers']
        return composite(model, layers, resource_file), {
            **info, 'layers': layers, 'note': 'Source solution default vessel, fill level, tint and visible layers.'}
    if model.get('chargerAppearance'):
        from charger_states import validate_source
        images = validate_source(model, resource_file)
        template = {k: v for k, v in model.items() if k != 'chargerAppearance'}
        _, info = reference_frame(template, inventory)
        return Image.alpha_composite(images['recharger'][0], images['recharger-0'][0]), {
            **info, 'layers': ['recharger', 'recharger-0'],
            'note': 'Empty default source charger; live indicator and inserted layers follow their original owners.'}
    if model.get('reagentTankAppearance'):
        from reagent_tank_states import validate_source
        images = validate_source(model, resource_file)
        template = {k: v for k, v in model.items() if k != 'reagentTankAppearance'}
        _, info = reference_frame(template, inventory)
        composed = Image.alpha_composite(images['tank_normal'], images['tn_color-1'])
        composed = Image.alpha_composite(composed, images['t_inactive'])
        return composed, {**info, 'layers': ['tank_normal', 'tn_color-1', 't_inactive'],
                          'note': 'Permanent white vessel and inactive layer; dynamic Fill hidden. Source solution owner supplies live tint.'}
    if model.get('barricadeDamageStates'):
        template = {k: v for k, v in model.items() if k != 'barricadeDamageStates'}
        body, info = reference_frame(template, inventory)
        reinforcement, reinforcement_info = reference_frame({**template, 'referenceRsi': model['barricadeReinforcementRsi'],
                                                              'referenceState': 'Additional' + model['referenceState']}, inventory)
        if body is None or reinforcement is None or body.size != reinforcement.size:
            raise ValueError(f"{model['id']}: missing compatible barricade layer reference")
        composed = Image.alpha_composite(body, reinforcement)
        evidence = {**info, 'additionalLayer': reinforcement_info,
                    'note': 'Static body/reinforcement composition; acid absent. Native playback and full fidelity unverified.'}
        if barricade_acid_frame is not None:
            if not model.get('barricadeAcidStates') or type(barricade_acid_frame) is not int or not 0 <= barricade_acid_frame < 5:
                raise ValueError(f"{model['id']}: unsupported acid frame")
            from barricade_state_review import source_state
            frames, acid_info = source_state(resource_file(model['barricadeAcidRsi']), model['barricadeAcidState'])
            acid = frames[model.get('referenceDirection', 0)][barricade_acid_frame]
            if acid.size != composed.size:
                raise ValueError(f"{model['id']}: incompatible acid frame dimensions")
            composed = Image.alpha_composite(composed, acid)
            evidence.update(acidLayer={**acid_info, 'frame': barricade_acid_frame},
                            note='Body/reinforcement, acid frame, then wire when present. Native interaction and geometry fidelity unverified.')
        if barricade_wired:
            if not model.get('barricadeWiredStates'):
                raise ValueError(f"{model['id']}: no authored wire composition")
            wire, wire_info = reference_frame({**template, 'referenceRsi': model['barricadeWireRsi'],
                                               'referenceState': model['barricadeWireState']}, inventory)
            if wire is None or wire.size != composed.size:
                raise ValueError(f"{model['id']}: missing compatible wire reference")
            composed = Image.alpha_composite(composed, wire)
            evidence['wireLayer'] = wire_info
        return composed, evidence
    sources = model['sourcePrototypes'] or ([model['referencePrototype']] if model.get('referencePrototype') else [])
    for source in sources:
        prototype = inventory.get(source) or {}
        if not prototype and not model.get('referenceRsi'):
            continue
        sprite = prototype.get("sprite") or {}
        icon = prototype.get("icon") or {}
        # SpriteComponent only creates a layer from state/texture when no explicit
        # layers exist. An inherited state must not replace a child's visible art.
        layers = sprite.get("layers") or []
        candidates = ([{**sprite, **layer} for layer in layers
                       if isinstance(layer, dict) and layer.get("visible", True)] if layers else [sprite])
        candidates.append(icon)
        if model.get('referenceRsi'):
            candidates = [{'sprite': model['referenceRsi'], 'state': model['referenceState']}]
        for candidate in candidates:
            texture = candidate.get("texture")
            if texture and (file := resource_file(texture)):
                return Image.open(file).convert("RGBA"), {"prototype": source, "file": file.relative_to(ROOT).as_posix()}
            rsi = candidate.get("rsi") or candidate.get("sprite")
            state = candidate.get("state")
            if not rsi or not state or not isinstance(state, str):
                continue
            directory = resource_file(rsi)
            if not directory or not directory.is_dir():
                continue
            file = directory / (state + ".png")
            meta_file = directory / "meta.json"
            if not file.exists() or not meta_file.exists():
                continue
            meta = json.loads(meta_file.read_text(encoding="utf-8-sig"))
            frame_size = meta.get("size", {"x": 32, "y": 32})
            image = Image.open(file).convert("RGBA")
            direction = model.get('referenceDirection', 0)
            state_meta = next((row for row in meta.get('states', []) if row['name'] == state), {})
            directions = state_meta.get('directions', 1)
            if direction >= directions:
                raise ValueError(f"{model['id']}: referenceDirection is absent from source RSI")
            # RSI packs each direction's animation frames consecutively. This review uses its first frame.
            delays = state_meta.get('delays') or [[1]] * directions
            frame_index = sum(len(row) for row in delays[:direction])
            columns = image.width // frame_size['x']
            x, y = frame_index % columns * frame_size['x'], frame_index // columns * frame_size['y']
            frame = image.crop((x, y, x + frame_size['x'], y + frame_size['y']))
            if model.get('referenceTint'):
                frame = Image.fromarray(np.rint(np.asarray(frame) * rgba(model['referenceTint'])).astype(np.uint8))
            return frame, {"prototype": source, "file": file.relative_to(ROOT).as_posix(),
                           "meta": meta_file.relative_to(ROOT).as_posix(), "state": state,
                           "license": meta.get("license"), "copyright": meta.get("copyright"),
                           **({'direction': direction} if 'referenceDirection' in model else {}),
                           "note": ("First animation frame of the explicit reference direction; not a composed live sprite or an automatic fidelity approval."
                                    if 'referenceDirection' in model else
                                    "First reference frame; not a composed live sprite or an automatic fidelity approval.")}
    return None, None


def write_reviews(models, destination, inventory):
    destination.mkdir(parents=True, exist_ok=True)
    font = ImageFont.load_default(size=16)
    small = ImageFont.load_default(size=12)
    index = []
    overview = Image.new("RGB", (1000, math.ceil(len(models) / 5) * 230 + 64), "#101820")
    overview_draw = ImageDraw.Draw(overview)
    overview_draw.text((20, 15), "GARRISON / 3D ASSET LIBRARY", fill="#F0E3BE", font=font)
    overview_draw.text((20, 38), "Draft procedural geometry. Sprite matching and animation review remain open.", fill="#9CAEBB", font=small)
    for i, model in enumerate(models):
        sheet = Image.new("RGB", (1100, 290), "#101820")
        draw = ImageDraw.Draw(sheet)
        draw.text((16, 12), f"{model['label']}  |  {model['status'].upper()}", fill="#F0E3BE", font=font)
        reference, source = reference_frame(model, inventory)
        if reference:
            # Reference padding encodes sprite placement, not visible art size.
            # Trim it only on comparison sheets; exported reference frames retain it.
            if bounds := reference.getbbox():
                reference = reference.crop(bounds)
            scale = min(5, 180 // max(reference.size))
            scale = max(1, scale)
            reference = reference.resize((reference.width * scale, reference.height * scale), Image.Resampling.NEAREST)
            sheet.paste(reference, ((220 - reference.width) // 2, 50 + (200 - reference.height) // 2), reference)
        else:
            draw.text((28, 130), "No reference frame", fill="#9CAEBB", font=small)
        draw.text((16, 263), "Source art (padding cropped)", fill="#9CAEBB", font=small)
        for view, yaw in enumerate((-math.pi / 2, 0, math.pi / 2, math.pi)):
            sheet.paste(render_model(model, yaw=yaw), (220 * (view + 1), 36))
            draw.text((220 * (view + 1) + 16, 263), ("South / front", "East", "North / back", "West")[view], fill="#9CAEBB", font=small)
        sheet.save(destination / f"{model['id']}.png")
        x, y = (i % 5) * 200, (i // 5) * 230 + 64
        overview.paste(render_model(model, (196, 192)), (x + 2, y))
        overview_draw.text((x + 8, y + 196), model["label"][:27], fill="#D9E3E9", font=small)
        overview_draw.text((x + 8, y + 212), f"{len(model['parts'])} parts / draft" if model["status"] == "draft" else "Reviewed", fill="#9CAEBB", font=small)
        index.append({"id": model["id"], "reference": source, "sheet": f"{model['id']}.png", "status": model["status"]})
    overview.save(destination / "overview.png")
    (destination / "index.json").write_text(json.dumps(index, indent=2) + "\n", encoding="utf-8")
    featured = ("CMU3DOfficeChairDark", "CMU3DFusionGenerator", "CMU3DArmoryLocker", "CMU3DHybrisaDoubleGlassDoor",
                "CMU3DMarineDraft", "CMU3DPulseRifleDraft", "CMU3DBunkBed", "CMU3DBarrelRed")
    by_id = {model["id"]: model for model in models}
    selected = [model_id for model_id in featured if model_id in by_id]
    selected += [model["id"] for model in models if model["id"] not in selected][:8-len(selected)]
    showcase = Image.new("RGB", (1000, 558), "#101820")
    showcase_draw = ImageDraw.Draw(showcase)
    showcase_draw.text((20, 15), f"GARRISON / {len(models)} MODEL ASSETS", fill="#F0E3BE", font=font)
    showcase_draw.text((20, 39), f"{len(selected)} draft examples. Source matching, state and gameplay review remain open.", fill="#9CAEBB", font=small)
    for i, model_id in enumerate(selected):
        model = by_id[model_id]
        x, y = (i % 4) * 250, (i // 4) * 242 + 68
        showcase.paste(render_model(model, (244, 208)), (x + 3, y))
        showcase_draw.text((x + 10, y + 214), model["label"][:31], fill="#D9E3E9", font=small)
    showcase.save(destination / "showcase.png")


def viewer_outputs(models, inventory):
    """Use the same validated geometry as GLB export, retaining reference attribution."""
    outputs, entries = {}, []
    for model in models:
        entry = {key: model[key] for key in
                 ("id", "label", "status", "sourcePrototypes", "parts")}
        entry["description"] = model.get("description", "")
        for field in ("equipmentOnly", "floorOpening", "ceilingOpening", "preserveSlabCladding", "anchored", "alternateAnchorModel", "supportProbePart"):
            if field in model:
                entry[field] = model[field]
        if model.get('reagentTankAppearance'):
            from reagent_tank_states import viewer_references
            entry['reagentTankAppearance'] = model['reagentTankAppearance']
            references, entry['reagentTankReferences'] = viewer_references(model, resource_file, surfaces.png_bytes)
            outputs.update(references)
        if model.get('chargerAppearance'):
            from charger_states import viewer_references, portable_states
            entry['chargerAppearance'] = model['chargerAppearance']
            entry['chargerStates'] = portable_states(model)
            references, entry['chargerStateReferences'] = viewer_references(model, resource_file, surfaces.png_bytes)
            outputs.update(references)
        for appearance, family, module in (
                ('foamAppearance', 'foam', 'foam_wall_states'),
                ('solutionAppearance', 'solution', 'solution_glass_states')):
            if not model.get(appearance):
                continue
            from importlib import import_module
            adapter = import_module(module)
            entry[appearance] = model[appearance]
            entry[family+'States'] = adapter.portable_states(model)
            references, entry[family+'StateReferences'] = adapter.viewer_references(model, resource_file, surfaces.png_bytes)
            outputs.update(references)
        if model.get('spriteStates'):
            from sprite_states import viewer_references
            entry['spriteStates'] = model['spriteStates']
            entry['sourceSpriteOffset'] = model['sourceSpriteOffset']
            references, entry['spriteStateReferences'] = viewer_references(model, resource_file, surfaces.png_bytes)
            outputs.update(references)
        if model.get('doorSpriteStates'):
            from door_states import viewer_references
            for field in ('doorSpriteStates', 'doorAnimationDurations'):
                entry[field] = model[field]
            references, entry['doorSpriteReferences'] = viewer_references(model, resource_file, surfaces.png_bytes)
            outputs.update(references)
        if model.get('poweredLightStates'):
            entry['poweredLightStates'] = model['poweredLightStates']
            entry['poweredLightReferences'] = {}
            for state in model['poweredLightStates']:
                urls = []
                for direction in range(4):
                    frame, _ = reference_frame({**model, 'referenceState': state, 'referenceDirection': direction}, inventory)
                    if frame is None:
                        raise ValueError(f"Missing light reference: {model['id']} {state}")
                    name = f"references/{model['id']}-{state}-dir{direction}.png"
                    outputs[name] = surfaces.png_bytes(frame)
                    urls.append('../generated/' + name)
                entry['poweredLightReferences'][state] = urls
        if model.get('doorButtonStates'):
            entry['doorButtonStates'] = model['doorButtonStates']
            entry['frameAnimations'] = model['frameAnimations']
        if model.get('barricadeDamageStates'):
            entry['barricadeDamageStates'] = model['barricadeDamageStates']
            entry['barricadeReinforcementRsi'] = model['barricadeReinforcementRsi']
            entry['barricadeReferences'] = {}
            for suffix in ('0', '4', '8', '12'):
                urls = []
                for direction in range(4):
                    frame, _ = reference_frame({**model, 'referenceState': 'DamageOverlay_' + suffix,
                                                 'referenceDirection': direction}, inventory)
                    name = f"references/{model['id']}-damage{suffix}-dir{direction}.png"
                    outputs[name] = surfaces.png_bytes(frame)
                    urls.append('../generated/' + name)
                entry['barricadeReferences'][suffix] = urls
            if model.get('barricadeWiredStates'):
                for field in ('barricadeWiredStates', 'barricadeWireRsi', 'barricadeWireState'):
                    entry[field] = model[field]
                entry['barricadeWiredReferences'] = {}
                for suffix in ('0', '4', '8', '12'):
                    urls = []
                    for direction in range(4):
                        frame, _ = reference_frame({**model, 'referenceState': 'DamageOverlay_' + suffix,
                                                     'referenceDirection': direction}, inventory, barricade_wired=True)
                        name = f"references/{model['id']}-damage{suffix}-wired-dir{direction}.png"
                        outputs[name] = surfaces.png_bytes(frame)
                        urls.append('../generated/' + name)
                    entry['barricadeWiredReferences'][suffix] = urls
            if model.get('barricadeAcidStates'):
                for field in ('barricadeAcidStates', 'barricadeAcidRsi', 'barricadeAcidState', 'barricadeAcidDelays'):
                    entry[field] = model[field]
                entry['barricadeAcidReferences'] = {}
                for wired in (False, True):
                    for suffix in ('0', '4', '8', '12'):
                        frames = []
                        for index in range(5):
                            urls = []
                            for direction in range(4):
                                frame, _ = reference_frame({**model, 'referenceState': 'DamageOverlay_'+suffix,
                                                             'referenceDirection': direction}, inventory, wired, index)
                                name = f"references/{model['id']}-damage{suffix}-wire{int(wired)}-acid{index}-dir{direction}.png"
                                outputs[name] = surfaces.png_bytes(frame)
                                urls.append('../generated/'+name)
                            frames.append(urls)
                        entry['barricadeAcidReferences'][suffix+(':wire' if wired else '')] = frames
        entry["placement"] = model.get("placement", "floor")
        entry["supportSurface"] = model.get("supportSurface")
        entry["supportSurfaces"] = model.get("supportSurfaces", [])
        entry["sourceDirections"] = model.get("sourceDirections", 1)
        entry["swapEastWest"] = model.get("swapEastWest", False)
        entry["sourceCardinalFacings"] = model.get("sourceCardinalFacings", [])
        entry["yawOffset"] = model.get("yawOffset", 0)
        entry["useEntityRotation"] = model.get("useEntityRotation", False)
        entry["faceAwayFromWall"] = model.get("faceAwayFromWall", False)
        if model.get('faceAwayFromWindows'):
            entry['faceAwayFromWindows'] = True
        entry["connectToNeighbours"] = model.get("connectToNeighbours", False)
        if model.get('cornerSurfaces'):
            entry['cornerSurfaces'] = model['cornerSurfaces']
        entry["wallMounted"] = model.get("wallMounted", False)
        if model.get('wallPaper'):
            entry['wallPaper'] = True
        if model.get('sourceSpriteRotates'):
            entry['sourceSpriteRotates'] = True
        entry["wallFacingTargets"] = model.get("wallFacingTargets", [])
        if model.get('windowMountTargets'):
            entry['windowMountTargets'] = model['windowMountTargets']
        if model.get('windowMountInside'):
            entry['windowMountInside'] = True
        if model.get('panelEndTargets'):
            entry['panelEndTargets'] = model['panelEndTargets']
        if model.get('terrainCutoutTargets'):
            for key in ('terrainCutoutTargets', 'terrainCutoutMin', 'terrainCutoutMax'):
                entry[key] = model[key]
        if model.get('openingFacingTargets'):
            entry['openingFacingTargets'] = model['openingFacingTargets']
        if model.get('backWallMountTargets'):
            entry['backWallMountTargets'] = model['backWallMountTargets']
        if model.get('fitInsideWall'):
            entry['fitInsideWall'] = True
        if model.get('connectionEndInset'):
            entry['connectionEndInset'] = model['connectionEndInset']
        for key in ('equipmentOnly', 'doorState', 'alternateDoorModel', 'folded', 'alternateFoldModel', 'randomSpritePrototypes', 'randomSpriteLayer', 'referencePrototype', 'referenceRsi', 'referenceState', 'referenceDirection', 'directionalModels', 'referenceTint', 'bakedSpriteTint', 'groundOffset'):
            if key in model:
                entry[key] = model[key]
        reference, source = reference_frame(model, inventory)
        if reference is not None:
            name = f"references/{model['id']}.png"
            stream = BytesIO()
            reference.save(stream, format="PNG")
            outputs[name] = stream.getvalue()
            entry["reference"] = {**source, "imageUrl": f"../generated/{name}"}
        entries.append(entry)
    document = {"schemaVersion": 1,
                "coordinates": "X/Y horizontal, Z up; front -Y; one unit per tile. Colors are sRGB.",
                "models": entries}
    outputs["models.json"] = (json.dumps(document, indent=2) + "\n").encode("utf-8")
    outputs.update(surfaces.viewer_outputs())
    return outputs


def write_outputs(outputs, destination):
    """Remove only unchanged exports owned by the previous manifest after a rename/removal."""
    destination = destination.resolve()
    destination.mkdir(parents=True, exist_ok=True)
    manifest_path = destination / "manifest.json"
    if manifest_path.exists():
        previous = json.loads(manifest_path.read_text(encoding="utf-8"))
        for entry in previous.get("models", []):
            name = entry.get("file", "")
            if name in outputs:
                continue
            if not re.fullmatch(r"[A-Za-z][A-Za-z0-9_]*\.glb", name):
                raise ValueError(f"Unsafe filename in previous export manifest: {name!r}")
            path = (destination / name).resolve()
            if path.parent != destination:
                raise ValueError(f"Export path escapes its output directory: {name!r}")
            if path.exists():
                if hashlib.sha256(path.read_bytes()).hexdigest() != entry.get("sha256"):
                    raise ValueError(f"Obsolete export {name} has local edits; preserved for manual review")
                path.unlink()
    for name, data in outputs.items():
        (destination / name).write_bytes(data)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, default=SOURCE)
    parser.add_argument("--output", type=Path, default=OUTPUT)
    parser.add_argument("--review-output", type=Path, default=REVIEW)
    parser.add_argument("--viewer-output", type=Path, default=VIEWER)
    parser.add_argument("--inventory", type=Path, default=ROOT / "Tools/three_d/generated/inventory.json")
    parser.add_argument("--check", action="store_true", help="Check GLBs, manifest and scene-viewer assets without writing")
    parser.add_argument("--no-review", action="store_true")
    args = parser.parse_args()
    models = load_models(args.source)
    inventory = {}
    if args.inventory.exists():
        inventory = {entry["id"]: entry for entry in json.loads(args.inventory.read_text(encoding="utf-8"))["prototypes"]}
    outputs, entries = {}, []
    for model in models:
        binary = glb_bytes(model)
        name = model["id"] + ".glb"
        outputs[name] = binary
        entries.append({"id": model["id"], "label": model["label"], "file": name,
                        "status": model["status"], "parts": len(model["parts"]),
                        "triangles": triangle_count(model["parts"]),
                        "sha256": hashlib.sha256(binary).hexdigest(), "sourcePrototypes": model["sourcePrototypes"]})
        if model.get('equipmentOnly'):
            entries[-1]['equipmentOnly'] = True
        if model.get('doorButtonStates'):
            entries[-1]['animationClips'] = len(model['frameAnimations'])
            entries[-1]['storedFrameParts'] = sum(len(f['parts']) for s in model['doorButtonStates'].values()
                                                 for key in ('frames', 'unpoweredFrames') for f in s[key])
        if model.get('chargerAppearance'):
            from charger_states import portable_states
            poses = portable_states(model)
            entries[-1]['animationClips'] = sum(len(s['frames']) > 1 for s in poses.values())
            entries[-1]['staticChargerScenes'] = sum(len(s['frames']) == 1 for s in poses.values())
            entries[-1]['storedFrameParts'] = sum(len(f['parts']) for s in poses.values() for f in s['frames'])
        for appearance, family, module in (
                ('foamAppearance', 'Foam', 'foam_wall_states'),
                ('solutionAppearance', 'Solution', 'solution_glass_states')):
            if not model.get(appearance):
                continue
            from importlib import import_module
            poses = import_module(module).portable_states(model)
            entries[-1]['animationClips'] = sum(len(s['frames']) > 1 for s in poses.values())
            entries[-1]['static'+family+'Scenes'] = sum(len(s['frames']) == 1 for s in poses.values())
            entries[-1]['storedFrameParts'] = sum(len(f['parts']) for s in poses.values() for f in s['frames'])
        if model.get('spriteStates'):
            entries[-1]['animationClips'] = sum(len(s['frames']) > 1 for s in model['spriteStates'].values())
            entries[-1]['staticSpriteScenes'] = sum(len(s['frames']) == 1 for s in model['spriteStates'].values())
            entries[-1]['storedFrameParts'] = sum(len(f['parts']) for s in model['spriteStates'].values() for f in s['frames'])
        if model.get('doorSpriteStates'):
            entries[-1]['animationClips'] = 2
            entries[-1]['storedFrameParts'] = sum(len(f['parts']) for s in model['doorSpriteStates'].values() for f in s['frames'])
        if model.get('poweredLightStates'):
            entries[-1]['staticPoweredLightScenes'] = len(model['poweredLightStates'])
            entries[-1]['storedFrameParts'] = sum(len(f['parts']) for f in model['poweredLightStates'].values())
        if model.get('barricadeDamageStates'):
            entries[-1]['staticDamageScenes'] = len(model['barricadeDamageStates'])
            entries[-1]['storedFrameParts'] = sum(len(frame['parts']) for frame in model['barricadeDamageStates'].values())
            if model.get('barricadeWiredStates'):
                entries[-1]['staticWiredScenes'] = len(model['barricadeWiredStates'])
                entries[-1]['storedFrameParts'] += sum(len(frame['parts']) for frame in model['barricadeWiredStates'].values())
            if model.get('barricadeAcidStates'):
                entries[-1]['acidFrameCompositions'] = 40
                entries[-1]['animationClips'] = 8
                entries[-1]['distinctAcidSequences'] = 1
                entries[-1]['storedFrameParts'] += sum(len(f['parts']) for s in model['barricadeAcidStates'].values()
                                                      for key in ('frames', 'wiredFrames') for f in s[key])
    manifest = {"schemaVersion": 1, "coordinateSystem": "glTF right-handed Y-up; game (x,y,z) maps to (x,z,-y)",
                "scope": "Draft solid-part assets. Runtime use requires explicit world bindings or equipment poses. No skinning or fidelity approval.", "models": entries}
    outputs["manifest.json"] = (json.dumps(manifest, indent=2) + "\n").encode("utf-8")
    browser_outputs = viewer_outputs(models, inventory)
    if args.check:
        stale = [name for name, data in outputs.items() if not (args.output / name).exists() or (args.output / name).read_bytes() != data]
        stale += [path.name for path in args.output.glob("*.glb") if path.name not in outputs]
        stale += [f"viewer/{name}" for name, data in browser_outputs.items()
                  if not (args.viewer_output / name).exists() or (args.viewer_output / name).read_bytes() != data]
        if stale:
            raise SystemExit("Stale generated assets: " + ", ".join(stale))
        print(f"Verified {len(models)} deterministic GLBs, manifest and viewer assets")
        return
    write_outputs(outputs, args.output)
    for name, data in browser_outputs.items():
        path = args.viewer_output / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
    if not args.no_review:
        write_reviews(models, args.review_output, inventory)
    print(f"Built {len(models)} GLBs; {sum(e['triangles'] for e in entries)} total triangles; all statuses preserved")
    print(f"Models: {args.output}\nReview sheets: {args.review_output}")


if __name__ == "__main__":
    main()
