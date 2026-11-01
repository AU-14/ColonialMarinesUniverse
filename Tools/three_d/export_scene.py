#!/usr/bin/env python3
"""Export an assembled region of the saved Garrison scene to portable GLB.

Preserves model hierarchy, map positions and rotations, source IDs and draft
status. Unmapped sprites and runtime appearance are not modeled by this export.
"""
from __future__ import annotations

import argparse
from copy import deepcopy
import json
import math
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))
from build_models import encode_glb, glb_document, load_models, vector
from primitives import triangle_count

ROOT = Path(__file__).resolve().parent
MAX_PARTS = 200_000


def export_region(scene, models, center, radius=20, inherited=False, floors=True):
    if not math.isfinite(radius) or radius <= 0 or len(center) != 3 or not all(math.isfinite(v) for v in center):
        raise ValueError("Region requires a finite positive radius and three finite center coordinates")
    def inside(position):
        return abs(position[0] - center[0]) <= radius and abs(position[1] - center[1]) <= radius and abs(position[2] - center[2]) < .01

    library = {model["id"]: model for model in models}
    geometry = {key: model['parts'] for key, model in library.items()} | scene.get('geometryVariants', {})
    def geometry_key(entity):
        return entity.get('geometryKey', entity['modelId'])
    region = [entity for entity in scene["instances"] if inside(entity["position"])]
    def source_state_proven(entity):
        model = library[entity['modelId']]
        if model.get('foamAppearance') or model.get('solutionAppearance'):
            try:
                if model.get('foamAppearance'):
                    from foam_wall_states import compose_parts
                    pose = entity['foamPose']
                    expected = compose_parts(model, pose['edgeMask'], pose['spriteTint'])
                else:
                    from solution_glass_states import compose_parts
                    pose = entity['solutionPose']
                    expected = compose_parts(model, pose['layers'], pose['spriteTint'])
                actual = scene['geometryVariants'][entity['geometryKey']]
                return json.dumps(actual, sort_keys=True) == json.dumps(expected, sort_keys=True)
            except (ValueError, KeyError, TypeError):
                return False
        if model.get('chargerAppearance'):
            from charger_states import compose_parts
            pose = entity.get('chargerPose', {})
            try:
                compose_parts(model, pose['light'], pose['frame'], pose['inserted'])
            except (ValueError, KeyError, TypeError):
                return False
            return entity.get('geometryKey') in scene.get('geometryVariants', {})
        if model.get('reagentTankAppearance'):
            pose = entity.get('reagentTankPose', {})
            return (pose.get('state') in ('tn_color-1', 'tn_color-2') and type(pose.get('visible')) is bool and
                    entity.get('geometryKey') in scene.get('geometryVariants', {}))
        return (not model.get('spriteStates') or
                entity.get('spriteState') in model['spriteStates'] and entity.get('spriteFrame') == 0 and
                entity.get('geometryKey') in scene.get('geometryVariants', {}))
    selected = [entity for entity in region if entity.get("modelId") in library and
                (entity["matchKind"] == "exact" or inherited and entity["matchKind"] == "inherited") and source_state_proven(entity)]
    def tile_inside(tile):
        yaw = tile.get("yaw", 0)
        return inside([tile["x"] + .5 * (math.cos(yaw) - math.sin(yaw)),
                       tile["y"] + .5 * (math.sin(yaw) + math.cos(yaw)), tile["z"]])

    tile_entries = [tile for tile in scene.get("tiles", []) if tile_inside(tile)] if floors else []
    used = sorted({geometry_key(entity) for entity in selected})
    floor_part_count = sum(len(tile.get('floorFragments', [[0,0,1,1]])) + bool(tile.get('elevationRamp')) for tile in tile_entries)
    part_count = sum(len(geometry[geometry_key(entity)]) for entity in selected) + floor_part_count
    if part_count == 0:
        raise ValueError("No mapped geometry or floors inside this region")
    if part_count > MAX_PARTS:
        raise ValueError(f"Region has {part_count:,} parts; reduce radius below the {MAX_PARTS:,}-part export budget")
    # One shared cube buffer and one mesh/material per color across all instances.
    templates, offsets, parts = {}, {}, []
    for model_id in used:
        offsets[model_id] = (len(parts), len(geometry[model_id]))
        # Layout variants can retain YAML's comma-separated bounds, while the
        # model library uses numeric vectors. Normalize this export boundary.
        parts.extend({**part, 'min': vector(part['min']), 'max': vector(part['max'])}
                     for part in geometry[model_id])
    palette = scene.get("tilePalette", {})
    def tile_key(tile):
        ramp = tile.get('elevationRamp')
        slope = (*ramp['direction'], ramp['top']-ramp['bottom']) if ramp else ()
        return str(tile["palette"]), int(tile.get("variant", 0)), tuple(tuple(r) for r in tile.get('floorFragments', [[0,0,1,1]])), tile.get('foundationDepth', .07), slope

    tile_offsets = {}
    for key in sorted({tile_key(tile) for tile in tile_entries}):
        entry = palette.get(key[0], {})
        variants = entry.get("variantColors", [])
        color = variants[key[1]] if 0 <= key[1] < len(variants) else entry.get("color", "#69737B")
        tile_offsets[key] = (len(parts), len(key[2]) + bool(key[4]))
        for index, (x0,y0,x1,y1) in enumerate(key[2]):
            parts.append({"min": [x0, y0, -key[3]], "max": [x1, y1, -.01],
                          "label": f"floor-{key[:2]}-fragment{index}", "color": color})
        if key[4]:
            dx, dy, rise = key[4]
            parts.append({'min': [0, 0, -.01], 'max': [1, 1, rise-.01], 'shape': 'WedgeY',
                          'yaw': math.degrees(math.atan2(-dx, dy)), 'label': 'threshold ramp', 'color': color})
    document, binary = glb_document({"id": "GarrisonScene", "label": "Garrison region", "status": "draft",
                                     "sourcePrototypes": [], "parts": parts})
    for model_id, (start, count) in offsets.items():
        templates[model_id] = document["nodes"][start:start + count]
    tile_templates = {key: document["nodes"][offset:offset+count] for key, (offset,count) in tile_offsets.items()}
    document["nodes"] = []
    document["scenes"][0]["nodes"] = []
    document["asset"]["generator"] = "CMU saved-map region exporter"
    document["asset"]["copyright"] = "CMU contributors; retain Garrison/SOURCES*.md and source RSI attributions."
    nodes = document["nodes"]
    for entity in selected:
        model = library[entity["modelId"]]
        root = len(nodes)
        x, y, z = entity["position"]
        offset = entity.get("renderOffset", [0, 0, 0])
        x, y, z = x + offset[0], y + offset[1], z + offset[2]
        yaw = entity.get('renderYaw', entity["yaw"])
        template = templates[geometry_key(entity)]
        nodes.append({"name": f"{entity['prototype']} #{entity['id']}",
                      "translation": [x, z, -y], "rotation": [0, math.sin(yaw / 2), 0, math.cos(yaw / 2)],
                      "children": list(range(root + 1, root + 1 + len(template))),
                      "extras": {"savedUid": entity["id"], "sourcePrototype": entity["prototype"],
                                 "modelId": model["id"], "matchKind": entity["matchKind"], "status": model["status"],
                                 "sourcePosition": entity['position'], "sourceYaw": entity['yaw'], "renderYaw": yaw,
                                 "connectionMask": entity.get('connectionMask'),
                                 "layoutAlignment": entity.get('layoutAlignment'),
                                  "doorState": entity.get('doorState'),
                                  "folded": entity.get('folded'),
                                 "referenceState": entity.get('referenceState'),
                                 "randomSpriteState": entity.get('randomSpriteState'),
                                 "stateOverrideComponents": entity.get("stateOverrideComponents", [])}})
        nodes.extend(deepcopy(template))
        if model.get('poweredLightStates'):
            nodes[root]['extras']['poweredLightPreview'] = {
                'state': entity.get('poweredLightState', model['referenceState']), 'runtimeStateKnown': False}
        if model.get('barricadeDamageStates'):
            nodes[root]['extras']['barricadeDamageState'] = entity.get('barricadeDamageState', '0')
            if 'barricadeWired' in entity:
                nodes[root]['extras']['barricadeWired'] = entity['barricadeWired']
            if 'barricadeAcidFrame' in entity:
                nodes[root]['extras']['barricadeAcidFrame'] = entity['barricadeAcidFrame']
            nodes[root]['extras']['runtimeStateKnown'] = False
        if model.get('doorButtonStates'):
            nodes[root]['extras']['spriteFramePreview'] = {
                'state': model['referenceState'], 'frame': 0,
                'power': 'default powered source pose', 'runtimeStateKnown': False}
        if model.get('chargerAppearance'):
            nodes[root]['extras']['chargerPreview'] = {**entity['chargerPose'], 'runtimeStateKnown': False}
        for field in ('foam', 'solution'):
            if model.get(field+'Appearance'):
                nodes[root]['extras'][field+'Preview'] = {**entity[field+'Pose'], 'runtimeStateKnown': False}
        if model.get('spriteStates'):
            nodes[root]['extras']['spriteStatePreview'] = {
                'state': entity['spriteState'], 'frame': entity['spriteFrame'],
                'sourceSpriteOffset': model['sourceSpriteOffset'], 'runtimeStateKnown': False}
        document["scenes"][0]["nodes"].append(root)
    for tile in tile_entries:
        root = len(nodes)
        yaw = tile.get("yaw", 0)
        template = tile_templates[tile_key(tile)]
        nodes.append({"name": f"Tile {tile['palette']} at {tile['x']}, {tile['y']}",
                      "translation": [tile["x"], tile["z"] + tile.get('elevation', 0), -tile["y"]],
                      "rotation": [0, math.sin(yaw / 2), 0, math.cos(yaw / 2)], "children": list(range(root+1,root+1+len(template)))})
        if not template:
            nodes[root].pop('children')
        if tile.get('openingSources'):
            nodes[root]['extras'] = {'openingSources':tile['openingSources'],
                                    'floorFragments':tile.get('floorFragments', [[0,0,1,1]])}
        nodes.extend(deepcopy(template))
        document["scenes"][0]["nodes"].append(root)
    report = {"schemaVersion": 1, "map": scene["map"]["path"], "center": list(center), "radius": radius,
              "exportedEntities": len(selected), "omittedEntities": len(region) - len(selected),
              "floorTiles": len(tile_entries), "floorParts": floor_part_count, "solidParts": part_count,
              "triangles": sum(triangle_count(geometry[geometry_key(entity)]) for entity in selected) + floor_part_count * 12
                           - 4 * sum(bool(tile.get('elevationRamp')) for tile in tile_entries),
              "models": used, "includeInheritedCandidates": inherited,
              "limitations": ["Offline art review, not a playable map or visibility feed.",
                              "Draft geometry; connected panel axes follow source neighbour keys. Detailed states, lighting, decals and layered sprites remain incomplete.",
                              "Unmapped entities are omitted. Floor colors are approximate."]}
    animated = [e for e in selected if library[e['modelId']].get('doorButtonStates')]
    tanks = [e for e in selected if library[e['modelId']].get('reagentTankAppearance')]
    if tanks:
        report['reagentTankLayerSnapshots'] = len(tanks)
        report['limitations'].append('Reagent tanks use serialized/default sprite layer snapshots, not live solution-derived Fill colors or states.')
    if animated:
        report['sourceFramePreviewInstances'] = len(animated)
        report['limitations'].append('Door controls use their default powered source pose. This saved-map region has no live power state or replayed animation clips.')
    sprite_states = [e for e in selected if library[e['modelId']].get('spriteStates')]
    if sprite_states:
        report['spriteStatePreviewInstances'] = len(sprite_states)
        report['limitations'].append('Generic RSI states use the first frame of the proven saved/default source appearance. Unknown appearances are omitted; scene exports do not replay library animation clips.')
    document["extras"] = report
    return encode_glb(document, binary), report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--scene", type=Path, default=ROOT / "generated/redux-scene.json")
    parser.add_argument("--output", type=Path, default=ROOT / "generated/garrison-region.glb")
    parser.add_argument("--center", type=float, nargs=3, metavar=("X", "Y", "Z"))
    parser.add_argument("--radius", type=float, default=20)
    parser.add_argument("--include-inherited", action="store_true")
    parser.add_argument("--no-floors", action="store_true")
    args = parser.parse_args()
    from scene_io import read_scene
    scene = read_scene(args.scene)
    data, report = export_region(scene, load_models(), args.center or scene["map"]["defaultFocus"],
                                 args.radius, args.include_inherited, not args.no_floors)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_bytes(data)
    args.output.with_suffix(".json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(f"Exported {report['exportedEntities']} entities and {report['floorTiles']} floors to {args.output}")
    print(f"Omitted {report['omittedEntities']} unmapped or disabled inherited candidates; all art status remains draft/reviewed as authored")


if __name__ == "__main__":
    main()
