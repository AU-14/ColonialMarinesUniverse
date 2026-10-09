"""Build rigid worn volumes from the original four-view equipment artwork.

Clothing visuals are composed before reconstruction, including explicit layer
colours. Front/back/side silhouettes constrain closed volumes; hidden structure
and rigid posing remain draft inferences, not fidelity approval.
"""
import argparse
from collections import defaultdict
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageColor
import yaml

import build_models as bm
import equipment_hull
import inventory as inv

ROOT = bm.ROOT
MODELS = bm.EQUIPMENT_SOURCE / 'garrison_worn_equipment.yml'
POSES = bm.EQUIPMENT_SOURCE / 'garrison_worn_equipment_poses.yml'
# Slot aliases from ClientClothingSystem.TemporarySlotMap; flags from Clothing.
SLOTS = [('jumpsuit', 'innerclothing', 'INNERCLOTHING'), ('outerClothing', 'outerclothing', 'OUTERCLOTHING'),
         ('head', 'head', 'HELMET'), ('eyes', 'eyes', 'EYES'), ('ears', 'ears', 'EARS'),
         ('mask', 'mask', 'MASK'), ('neck', 'neck', 'NECK'), ('back', 'back', 'BACKPACK'),
         ('belt', 'belt', 'BELT'), ('gloves', 'gloves', 'HAND'), ('shoes', 'feet', 'FEET'),
         ('id', 'idcard', 'IDCARD'), ('suitstorage', 'suitstorage', 'SUITSTORAGE')]


def vector(values):
    return ', '.join(f'{v:.7f}'.rstrip('0').rstrip('.') or '0' for v in values)


def source_layers(row, slot, suffix):
    clothing = row['clothing']
    rsi = clothing.get('sprite') or (row.get('sprite') or {}).get('sprite')
    custom = clothing.get('clothingVisuals', {}).get(slot)
    if not rsi and not custom:
        raise ValueError('No clothing RSI')
    state = clothing.get('equippedState') or (
        (clothing['equippedPrefix'] + '-' if clothing.get('equippedPrefix') else '') + 'equipped-' + suffix)
    layers = custom if custom is not None else [{'sprite': rsi, 'state': state, 'scale': clothing.get('scale', '1, 1')}]
    result = []
    for layer in layers:
        if layer.get('visible') is False:
            continue
        if any(layer.get(key) for key in ('texture', 'shader', 'rotation', 'offset')) or str(layer.get('scale', '1, 1')) != '1, 1':
            raise ValueError('Transformed or shader clothing layer needs an authored attachment')
        path = layer.get('sprite') or rsi
        if not path or not layer.get('state'):
            raise ValueError('Clothing layer has no static RSI state')
        color = ImageColor.getcolor(layer.get('color', '#FFFFFFFF'), 'RGBA')
        if color[3] != 255:
            raise ValueError('Translucent clothing needs a material adapter')
        result.append({'rsi': inv.texture_reference(path), 'state': layer['state'],
                       'color': '#' + ''.join(f'{v:02X}' for v in color)})
    if not result:
        raise ValueError('No visible clothing layers')
    return result


def compose(layers):
    images = [Image.new('RGBA', (32, 32)) for _ in range(4)]
    sources = []
    for layer in layers:
        frames, meta = equipment_hull.frames(layer['rsi'], layer['state'])
        state = next(s for s in meta['states'] if s['name'] == layer['state'])
        if any(len(row) != 1 for row in state.get('delays', [[1]] * 4)):
            raise ValueError('Animated clothing keeps its live sprite')
        if any(im.size != (32, 32) for im in frames):
            raise ValueError('Nonstandard body canvas')
        tint = np.asarray(ImageColor.getcolor(layer['color'], 'RGBA'), dtype=np.float32) / 255
        for i, frame in enumerate(frames):
            pixels = np.rint(np.asarray(frame) * tint).astype(np.uint8)
            images[i] = Image.alpha_composite(images[i], Image.fromarray(pixels))
        path = inv.resource_path(ROOT, layer['rsi']) / (layer['state'] + '.png')
        sources.append({**layer, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
                        'license': meta.get('license'), 'copyright': meta.get('copyright')})
    return images, sources


def author(equipment):
    groups = defaultdict(list)
    skipped = []
    for row in equipment:
        clothing = row.get('clothing') or {}
        if row.get('rigidEquipment'):
            skipped.append({'prototype': row['prototype'], 'reason': 'Rigid tool or weapon needs its complete authored model and a slot pose; silhouette intersection would fragment it.'})
            continue
        flags = clothing.get('slots') or []
        flags = [flags] if isinstance(flags, str) else flags
        flags = {flag.lower() for flag in flags}
        for slot, flag, suffix in SLOTS:
            if flag not in flags:
                continue
            try:
                layers = source_layers(row, slot, suffix)
                compose(layers)
            except (ValueError, StopIteration, FileNotFoundError) as error:
                skipped.append({'prototype': row['prototype'], 'slot': slot, 'reason': str(error) or 'Absent equipped state'})
                continue
            groups[(slot, json.dumps(layers, sort_keys=True))].append(row['prototype'])
            # Armor lamps are source-owned. Match each stable composition explicitly.
            if any(layer['state'] == 'lamp-off' for layer in layers):
                lit = [{**layer, 'state': 'lamp-on' if layer['state'] == 'lamp-off' else layer['state']} for layer in layers]
                try:
                    compose(lit)
                    groups[(slot, json.dumps(lit, sort_keys=True))].append(row['prototype'])
                except (ValueError, StopIteration, FileNotFoundError):
                    pass
    models, poses, sources = [], [], []
    reference_path = ROOT / 'Content.CMU/Resources/Textures/CMU14/ThreeD/WornEquipmentReferences.rsi'
    reference_path.mkdir(parents=True, exist_ok=True)
    reference_states = []
    ids_used = set()
    for (slot, signature), ids in sorted(groups.items()):
        layers = json.loads(signature)
        images, attribution = compose(layers)
        try:
            parts = equipment_hull.hull(images, 32, 12)
        except ValueError as error:
            skipped.append({'prototypes': ids, 'slot': slot, 'reason': str(error)})
            continue
        palette_size = 12
        while len(parts) > 512 and palette_size > 3:
            palette_size -= 1
            parts = equipment_hull.hull(images, 32, palette_size)
        if not parts or len(parts) > 512:
            skipped.append({'prototypes': ids, 'slot': slot, 'reason': 'Volume exceeds attachment budget'})
            continue
        # Nest outer layers around uniforms without coincident surfaces. Scale the
        # complete XY volume, preserving adjacency and its authored floor height.
        width = {'outerClothing': 1.02, 'belt': 1.04, 'back': 1.06, 'neck': 1.08}.get(slot, 1)
        for part in parts:
            for bound in ('min', 'max'):
                x, y, z = part[bound]
                part[bound] = vector((x * width, y * width, z))
        mid = 'CMU3DWorn' + ids[0] + slot[0].upper() + slot[1:]
        if mid in ids_used:
            mid += hashlib.sha256(signature.encode()).hexdigest()[:8]
        ids_used.add(mid)
        reference = Image.new('RGBA', (64, 64))
        for direction, frame in enumerate(images):
            reference.paste(frame, (direction % 2 * 32, direction // 2 * 32))
        reference.save(reference_path / (mid + '.png'))
        reference_states.append({'name': mid, 'directions': 4})
        models.append({'type': 'cmu3DModel', 'id': mid, 'label': ids[0] + ' worn ' + slot,
                       'status': 'draft', 'equipmentOnly': True, 'sourcePrototypes': [], 'referencePrototype': ids[0],
                       'referenceRsi': '/Textures/CMU14/ThreeD/WornEquipmentReferences.rsi', 'referenceState': mid, 'sourceDirections': 4,
                       'description': 'Rigid worn assembly. Original composed four-view silhouettes constrain closed colored volumes. Hidden construction and standing pose remain inferred drafts. Layer recipes and attribution are in SOURCES_WORN_EQUIPMENT.json.',
                       'parts': parts})
        poses.append({'type': 'cmu3DEquipmentPose', 'id': mid + 'Pose', 'model': mid,
                      'sourcePrototypes': sorted(ids), 'slot': slot,
                      'wornAppearance': True, 'wornLayers': layers})
        sources.append({'model': mid, 'prototypes': sorted(ids), 'layers': attribution,
                        'parts': len(parts), 'paletteSize': palette_size})
    for path, data in [(MODELS, models), (POSES, poses)]:
        path.write_text('# Generated by Tools/three_d/author_worn_equipment.py. Attribution: SOURCES_WORN_EQUIPMENT.json.\n' +
                        yaml.safe_dump(data, sort_keys=False, width=110), encoding='utf-8', newline='\n')
    (bm.OUTPUT / 'SOURCES_WORN_EQUIPMENT.json').write_text(json.dumps(sources, indent=2) + '\n', encoding='utf-8')
    (reference_path / 'meta.json').write_text(json.dumps({'version': 1, 'license': 'CC-BY-SA-4.0',
        'copyright': 'Composed original equipment artwork; per-state original authors, licenses and source hashes in Models/CMU14/Garrison/SOURCES_WORN_EQUIPMENT.json.',
        'size': {'x': 32, 'y': 32}, 'states': reference_states}, indent=2) + '\n', encoding='utf-8')
    report = {'models': len(models), 'assemblies': len(poses),
              'equipment': len({uid for p in poses for uid in p['sourcePrototypes']}), 'skipped': skipped}
    (ROOT / 'Tools/three_d/generated/worn-equipment-review.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({k: v for k, v in report.items() if k != 'skipped'}), 'skipped', len(skipped))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--inventory', type=Path, default=ROOT / 'Tools/three_d/generated/equipment-inventory.json')
    args = parser.parse_args()
    author(json.loads(args.inventory.read_text(encoding='utf-8'))['equipment'])
